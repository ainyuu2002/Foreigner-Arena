using UnityEngine;
using FA.Core;

namespace FA.Movement
{
    /// <summary>
    /// Kiểm tra chạm sàn bằng cách Unity-native: Collider2D.IsTouchingLayers(StandableMask)
    /// (§1.1 — KHÔNG raycast tay nếu collider đủ dùng). Mask gộp Ground + OneWayPlatform
    /// đã có sẵn trong FA.Core GameLayers.StandableMask.
    ///
    /// IsTouchingLayers cập nhật theo physics step → đọc/refresh trong FixedUpdate (research §3.3).
    /// Phase 1 chỉ cần "có đang chạm sàn không" để verify đứng vững; P2 nhảy/coyote dùng nhiều hơn.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class GroundCheck : MonoBehaviour
    {
        [Tooltip("Collider thân để kiểm tra chạm sàn. Bỏ trống = lấy Collider2D trên cùng object.")]
        [SerializeField] private Collider2D bodyCollider;

        /// <summary>True nếu collider thân đang chạm bất kỳ bề mặt đứng được nào (Ground/OneWayPlatform).</summary>
        public bool IsGrounded { get; private set; }

        private void Awake()
        {
            if (bodyCollider == null)
                bodyCollider = GetComponent<Collider2D>();
        }

        private void FixedUpdate()
        {
            // Đọc đúng nhịp physics. StandableMask do Core cấp (không hardcode index).
            IsGrounded = bodyCollider != null &&
                         bodyCollider.IsTouchingLayers(GameLayers.StandableMask);
        }
    }
}
