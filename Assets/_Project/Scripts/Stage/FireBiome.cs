using System.Collections.Generic;
using UnityEngine;
using FA.Core;

namespace FA.Stage
{
    /// <summary>
    /// BIOME LỬA (P11, research §3.5) — ĐỨNG YÊN tăng % liên tục (ép người chơi di chuyển). Quét player
    /// trong vùng lửa; player nào vx≈0 + grounded ("đứng yên") thì cộng <c>firePercentPerSec * dt</c> qua
    /// <see cref="IPercentReceiver"/> (Core interface — KHÔNG ref FA.Combat nơi KnockbackPercent sống).
    ///
    /// "Đứng yên" = |linearVelocity.x| dưới fireStillSpeedEpsilon. Đang hitstun/knockback (đang bay) →
    /// vx lớn → tự nhiên KHÔNG bị tính (research §4 cạm bẫy 9). % đã clamp ở percentCap phía
    /// KnockbackPercent → KHÔNG cần clamp lại ở Stage. Code chỉ điều phối; KHÔNG dời transform.
    ///
    /// Grounded check: nếu gán groundLayers thì dùng overlap nhỏ dưới chân (Unity-native); bỏ trống = chỉ
    /// dựa vx (đủ cho test). Áp cho CẢ 2 player (GDD biome áp 2 người).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FireBiome : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("StageConfigSO của map Fire — firePercentPerSec + fireStillSpeedEpsilon. BẮT BUỘC.")]
        [SerializeField] private StageConfigSO config;

        [Header("Vùng lửa")]
        [Tooltip("Vùng áp lực lửa (BoxCollider2D trigger). Player trong vùng + đứng yên → tăng %. " +
                 "Bỏ trống = toàn map (dùng affectedRoots).")]
        [SerializeField] private BoxCollider2D fireArea;

        [Tooltip("Layer player để auto-tìm trong fireArea.")]
        [SerializeField] private LayerMask playerLayers;

        [Tooltip("Root 2 player (tùy chọn) — nếu gán thì dùng trực tiếp thay vì overlap. " +
                 "Phải có component implement IPercentReceiver (KnockbackPercent P7).")]
        [SerializeField] private List<GameObject> affectedRoots = new List<GameObject>();

        [Header("Grounded (tùy chọn)")]
        [Tooltip("Layer sàn để xác nhận 'đứng yên trên đất' (không tính khi đang bay/nhảy). " +
                 "Bỏ trống = chỉ dựa vx (đủ cho test).")]
        [SerializeField] private LayerMask groundLayers;

        [Tooltip("Bán kính overlap dưới chân để check grounded (chỉ dùng nếu set groundLayers).")]
        [Min(0.01f)] [SerializeField] private float groundCheckRadius = 0.15f;

        [Tooltip("Offset Y từ vị trí Rigidbody xuống chân để check grounded.")]
        [SerializeField] private float groundCheckYOffset = -1f;

        private readonly List<Collider2D> _overlap = new List<Collider2D>(8);
        private readonly HashSet<Rigidbody2D> _seen = new HashSet<Rigidbody2D>();

        private void Awake()
        {
            if (config == null)
                Debug.LogError("[FireBiome] Thiếu StageConfigSO.", this);
        }

        private void Update()
        {
            if (config == null) return;
            float add = config.firePercentPerSec * Time.deltaTime;
            if (add <= 0f) return;

            _seen.Clear();

            if (affectedRoots.Count > 0)
            {
                foreach (var go in affectedRoots)
                    TryApply(go, add);
                return;
            }

            // Auto-tìm theo layer trong fireArea.
            if (fireArea == null) return;
            _overlap.Clear();
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = playerLayers, useTriggers = true };
            fireArea.Overlap(filter, _overlap);
            foreach (var c in _overlap)
            {
                Rigidbody2D rb = c.attachedRigidbody;
                if (rb == null || _seen.Contains(rb)) continue;
                _seen.Add(rb);
                TryApply(rb.gameObject, add);
            }
        }

        private void TryApply(GameObject go, float add)
        {
            if (go == null) return;
            Rigidbody2D rb = go.GetComponentInChildren<Rigidbody2D>();
            if (rb == null) rb = go.GetComponentInParent<Rigidbody2D>();
            if (rb == null) return;

            var inv = rb.GetComponent<IInvulnerable>(); // bất tử hồi sinh → môi trường bỏ qua.
            if (inv != null && inv.IsInvulnerable) return;

            // "Đứng yên" = vx nhỏ.
            if (Mathf.Abs(rb.linearVelocity.x) > config.fireStillSpeedEpsilon) return;

            // Grounded (tùy chọn) — không tính khi đang bay.
            if (groundLayers.value != 0)
            {
                Vector2 foot = rb.position + new Vector2(0f, groundCheckYOffset);
                if (Physics2D.OverlapCircle(foot, groundCheckRadius, groundLayers) == null) return;
            }

            // Cộng % qua Core interface (null-safe nếu nhân vật không có thanh %).
            IPercentReceiver receiver = go.GetComponentInChildren<IPercentReceiver>();
            if (receiver == null) receiver = go.GetComponentInParent<IPercentReceiver>();
            receiver?.AddPercent(add);
        }
    }
}
