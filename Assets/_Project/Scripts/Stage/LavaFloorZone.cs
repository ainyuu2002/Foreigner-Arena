using System.Collections.Generic;
using UnityEngine;
using FA.Core;

namespace FA.Stage
{
    /// <summary>
    /// Ô ĐẤT DUNG NHAM NÓNG (feedback test map Fire): vùng sàn phát sáng gây +% liên tục cho player
    /// ĐỨNG TRÊN nó (chạm đất trong vùng) — KHÁC FireBiome: tính CẢ khi đang di chuyển. Tạo điểm nóng
    /// tranh chấp giữa map. Cộng % qua IPercentReceiver (Core) — không ref FA.Combat (§1.3).
    /// Số ở StageConfigSO.lavaFloorPercentPerSec (CẦN TEST).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LavaFloorZone : MonoBehaviour
    {
        [Tooltip("StageConfigSO của map Fire — lavaFloorPercentPerSec. BẮT BUỘC.")]
        [SerializeField] private StageConfigSO config;

        [Tooltip("Vùng ô đất nóng (BoxCollider2D trigger phủ đoạn sàn phát sáng).")]
        [SerializeField] private BoxCollider2D hotArea;

        [Tooltip("Layer player.")]
        [SerializeField] private LayerMask playerLayers;

        [Tooltip("Layer mặt đất — xác nhận 'đang đứng' (không tính khi bay ngang qua).")]
        [SerializeField] private LayerMask groundLayers;

        [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.15f;
        [SerializeField] private float groundCheckYOffset = -1f;

        private readonly List<Collider2D> _hits = new List<Collider2D>(8);

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
                var receiver = rb.GetComponent<IPercentReceiver>();
                receiver?.AddPercent(config.lavaFloorPercentPerSec * Time.fixedDeltaTime);
            }
        }

        private bool IsGrounded(Rigidbody2D rb)
        {
            Vector2 foot = rb.position + new Vector2(0f, groundCheckYOffset);
            return Physics2D.OverlapCircle(foot, groundCheckRadius, groundLayers) != null;
        }
    }
}
