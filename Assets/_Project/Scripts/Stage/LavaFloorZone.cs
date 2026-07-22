using System.Collections.Generic;
using UnityEngine;
using FA.Core;

namespace FA.Stage
{
    /// <summary>
    /// Ô ĐẤT DUNG NHAM NÓNG (map Fire): vùng sàn phát sáng gây +% cho player ĐỨNG TRÊN nó (chạm đất
    /// trong vùng) — tính CẢ khi di chuyển (khác FireBiome). Cộng % qua IPercentReceiver (Core §1.3).
    ///
    /// v2 (feedback test: "không có gì báo hiệu mất máu"): DoT cộng theo TICK rời rạc thay vì mượt —
    /// mỗi hazardTickSeconds cộng (lavaFloorPercentPerSec × tick) + FLASH màu nhân vật 1 nhịp
    /// (HazardFlashRunner). Vào vùng là ăn tick ĐẦU NGAY (feedback tức thì). Số ở StageConfigSO (CẦN TEST).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LavaFloorZone : MonoBehaviour
    {
        [Tooltip("StageConfigSO của map — lavaFloorPercentPerSec + hazardTick*/hazardFlash*. BẮT BUỘC.")]
        [SerializeField] private StageConfigSO config;

        [Tooltip("Vùng ô đất nóng (BoxCollider2D trigger phủ đoạn sàn phát sáng).")]
        [SerializeField] private BoxCollider2D hotArea;

        [Tooltip("Layer player.")]
        [SerializeField] private LayerMask playerLayers;

        [Tooltip("Layer mặt đất — xác nhận 'đang đứng' (không tính khi bay ngang qua).")]
        [SerializeField] private LayerMask groundLayers;

        [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.15f;
        [SerializeField] private float groundCheckYOffset = -1f;

        [Tooltip("Màu flash nhân vật mỗi tick (đỏ cam = bỏng).")]
        [SerializeField] private Color flashColor = new Color(1f, 0.45f, 0.2f, 1f);

        private readonly List<Collider2D> _hits = new List<Collider2D>(8);
        private readonly Dictionary<Rigidbody2D, float> _nextTickAt = new Dictionary<Rigidbody2D, float>(4);

        private void Awake()
        {
            if (config == null) Debug.LogError("[LavaFloorZone] Thiếu StageConfigSO.", this);
            if (hotArea == null) hotArea = GetComponent<BoxCollider2D>();
        }

        private void FixedUpdate()
        {
            if (config == null || hotArea == null || config.lavaFloorPercentPerSec <= 0f) return;

            var filter = new ContactFilter2D { useLayerMask = true, layerMask = playerLayers, useTriggers = true };
            _hits.Clear();
            hotArea.Overlap(filter, _hits);
            foreach (var c in _hits)
            {
                var rb = c.attachedRigidbody;
                if (rb == null || !IsGrounded(rb)) continue;
                var inv = rb.GetComponent<IInvulnerable>(); // bất tử hồi sinh → môi trường bỏ qua.
                if (inv != null && inv.IsInvulnerable) continue;

                // Rời vùng đủ lâu (quá 2 chu kỳ không tick) → reset, lần vào sau ăn tick đầu NGAY.
                if (!_nextTickAt.TryGetValue(rb, out float next) || Time.time - next > config.hazardTickSeconds * 2f)
                    next = Time.time;
                if (Time.time < next) continue;
                _nextTickAt[rb] = Time.time + config.hazardTickSeconds;

                var receiver = rb.GetComponent<IPercentReceiver>();
                if (receiver == null) continue;
                receiver.AddPercent(config.lavaFloorPercentPerSec * config.hazardTickSeconds);
                HazardFlashRunner.Flash(rb, flashColor, config.hazardFlashSeconds);
            }
        }

        private bool IsGrounded(Rigidbody2D rb)
        {
            Vector2 foot = rb.position + new Vector2(0f, groundCheckYOffset);
            return Physics2D.OverlapCircle(foot, groundCheckRadius, groundLayers) != null;
        }
    }
}
