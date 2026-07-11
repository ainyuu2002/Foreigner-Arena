using System.Collections.Generic;
using UnityEngine;
using FA.Core;

namespace FA.Stage
{
    /// <summary>
    /// BIOME ĐẦM LẦY (P11 mở rộng theo GDD Trang 9) — hazard THƯỜNG TRỰC, KHÔNG thiên tai ngẫu nhiên:
    /// (a) Slowness: nhân vận tốc ngang mỗi FixedUpdate khi player ĐỨNG ĐẤT trong vùng đầm (motor ghi lại
    ///     vx mỗi step nên tốc độ hiệu dụng ổn định = walkSpeed × swampSpeedFactor; trên không KHÔNG áp
    ///     để không phá knockback/juggle P7).
    /// (b) Giảm lực nhảy ~25%: phát hiện IMPULSE nhảy (vy tăng đột ngột trong 1 step — jump/double-jump/
    ///     wall-jump đều set vy 1 lần trong Enter() theo P2/P3) rồi nhân vy × swampJumpScale ĐÚNG 1 lần.
    ///
    /// Unity-native lo vận tốc (Rigidbody2D), code chỉ điều phối (§1.1). KHÔNG ref module khác — chỉ chạm
    /// Rigidbody2D + Layer (GameLayers — Core). Mọi số ở StageConfigSO (CẦN TEST). Pattern mirror
    /// SnowstormHazard/FireBiome: config + area trigger + auto-tìm theo playerLayers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SwampBiome : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("StageConfigSO của map Swamp — swampSpeedFactor/swampJumpScale/swampJumpImpulseThreshold. BẮT BUỘC.")]
        [SerializeField] private StageConfigSO config;

        [Header("Vùng đầm lầy")]
        [Tooltip("BoxCollider2D trigger vùng đầm — chỉ ảnh hưởng player trong vùng. Bỏ trống = toàn map (dùng affectedBodies).")]
        [SerializeField] private BoxCollider2D swampArea;

        [Tooltip("Rigidbody2D 2 player. Bỏ trống = tự tìm theo playerLayers trong swampArea.")]
        [SerializeField] private List<Rigidbody2D> affectedBodies = new List<Rigidbody2D>();

        [Tooltip("Layer player để auto-tìm nếu affectedBodies trống.")]
        [SerializeField] private LayerMask playerLayers;

        [Header("Ground check (chỉ slow khi đứng đất trong đầm)")]
        [Tooltip("Layer mặt đất để xác nhận 'đang đứng' (Ground | OneWayPlatform).")]
        [SerializeField] private LayerMask groundLayers;

        [Tooltip("Bán kính vòng overlap dưới chân. CẦN TEST theo kích thước collider nhân vật.")]
        [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.15f;

        [Tooltip("Offset Y từ tâm Rigidbody2D xuống chân.")]
        [SerializeField] private float groundCheckYOffset = -1f;

        private readonly List<Rigidbody2D> _scratch = new List<Rigidbody2D>(4);
        private readonly Dictionary<Rigidbody2D, float> _prevVy = new Dictionary<Rigidbody2D, float>(4);

        private void Awake()
        {
            if (config == null)
                Debug.LogError("[SwampBiome] Thiếu StageConfigSO.", this);
        }

        private void FixedUpdate()
        {
            if (config == null) return;

            foreach (var rb in ResolveBodies())
            {
                if (rb == null) continue;
                Vector2 v = rb.linearVelocity;

                // (b) Giảm lực nhảy: vy vọt lên đột ngột so với step trước = cú nhảy → scale 1 lần.
                float prev = _prevVy.TryGetValue(rb, out float p) ? p : v.y;
                if (v.y > 0f && v.y - prev >= config.swampJumpImpulseThreshold)
                    v.y *= config.swampJumpScale;

                // (a) Slowness thường trực: chỉ khi đứng đất (bùn níu chân; trên không giữ nguyên knockback).
                if (IsGrounded(rb))
                    v.x *= config.swampSpeedFactor;

                rb.linearVelocity = v;
                _prevVy[rb] = v.y;
            }
        }

        private bool IsGrounded(Rigidbody2D rb)
        {
            Vector2 foot = rb.position + new Vector2(0f, groundCheckYOffset);
            return Physics2D.OverlapCircle(foot, groundCheckRadius, groundLayers) != null;
        }

        private IEnumerable<Rigidbody2D> ResolveBodies()
        {
            if (affectedBodies.Count > 0)
            {
                foreach (var rb in affectedBodies)
                    if (InArea(rb)) yield return rb;
                yield break;
            }

            _scratch.Clear();
            if (swampArea != null)
            {
                var filter = new ContactFilter2D { useLayerMask = true, layerMask = playerLayers, useTriggers = true };
                var results = new List<Collider2D>(8);
                swampArea.Overlap(filter, results);
                foreach (var c in results)
                    if (c.attachedRigidbody != null && !_scratch.Contains(c.attachedRigidbody))
                        _scratch.Add(c.attachedRigidbody);
            }
            foreach (var rb in _scratch) yield return rb;
        }

        private bool InArea(Rigidbody2D rb)
        {
            if (rb == null) return false;
            if (swampArea == null) return true;
            return swampArea.OverlapPoint(rb.position);
        }
    }
}
