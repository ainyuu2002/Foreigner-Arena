using System.Collections.Generic;
using UnityEngine;
using FA.Core;

namespace FA.Stage
{
    /// <summary>
    /// VŨNG BÙN ĐẶC (P11 nâng cấp Swamp): vùng trigger phủ đúng các Ô BÙN ƯỚT (TileTopMud) — player
    /// ĐỨNG TRONG vũng bị lầy CHẬM RÕ RỆT hơn nữa (nhân thêm với slowness thường trực của SwampBiome).
    /// Cơ chế ĐỊA HÌNH CỐ ĐỊNH, deterministic — GDD Trang 9: swamp KHÔNG thiên tai ngẫu nhiên.
    /// Cùng pattern poll-Overlap như LavaFloorZone. Số ở StageConfigSO.mudSpeedFactor (CẦN TEST).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)] // chạy SAU CharacterMotor (order 0): state ghi vx trước, slow nhân đè sau — nếu không hiệu ứng bị motor nuốt.
    public sealed class MudPitZone : MonoBehaviour
    {
        [Tooltip("StageConfigSO của map Swamp — mudSpeedFactor. BẮT BUỘC.")]
        [SerializeField] private StageConfigSO config;

        [Tooltip("Vùng vũng bùn (BoxCollider2D trigger phủ dải ô bùn ướt).")]
        [SerializeField] private BoxCollider2D mudArea;

        [Tooltip("Layer player.")]
        [SerializeField] private LayerMask playerLayers;

        [Tooltip("Layer mặt đất — xác nhận 'đang đứng' (không lầy khi bay ngang qua).")]
        [SerializeField] private LayerMask groundLayers;

        [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.15f;
        [SerializeField] private float groundCheckYOffset = -1f;

        private readonly List<Collider2D> _hits = new List<Collider2D>(8);

        private void Awake()
        {
            if (config == null) Debug.LogError("[MudPitZone] Thiếu StageConfigSO.", this);
            if (mudArea == null) mudArea = GetComponent<BoxCollider2D>();
        }

        private void FixedUpdate()
        {
            if (config == null || mudArea == null) return;

            var filter = new ContactFilter2D { useLayerMask = true, layerMask = playerLayers, useTriggers = true };
            _hits.Clear();
            mudArea.Overlap(filter, _hits);
            foreach (var c in _hits)
            {
                var rb = c.attachedRigidbody;
                if (rb == null || !IsGrounded(rb)) continue;
                var inv = rb.GetComponent<IInvulnerable>(); // bất tử hồi sinh → môi trường bỏ qua.
                if (inv != null && inv.IsInvulnerable) continue;
                // Motor ghi lại vx mỗi FixedUpdate → nhân 1 lần/step = tốc độ ổn định walkSpeed × hệ số
                // (cùng cơ chế SwampBiome — 2 hệ số CHỒNG nhau: bùn ướt lầy hơn hẳn nền đầm thường).
                var v = rb.linearVelocity;
                v.x *= config.mudSpeedFactor;
                rb.linearVelocity = v;
            }
        }

        private bool IsGrounded(Rigidbody2D rb)
        {
            Vector2 foot = rb.position + new Vector2(0f, groundCheckYOffset);
            return Physics2D.OverlapCircle(foot, groundCheckRadius, groundLayers) != null;
        }
    }
}
