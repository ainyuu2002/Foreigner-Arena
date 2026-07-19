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
    ///
    /// (fix, PHẠM VI HẸP LẠI) GOTCHA PlatformEffector2D (one-way): effector chỉ tắt PHẢN LỰC va chạm khi
    /// xuyên qua từ dưới lên, KHÔNG tắt truy vấn touching — IsTouchingLayers vẫn báo true suốt lúc nhân
    /// vật đang bay XUYÊN QUA platform 1 chiều (đã ghi nhận riêng lẻ ở PlatformDropThrough.cs +
    /// CharacterMotor.SuppressAirJumps cho ca nấm nhún) → cắt cụt nhảy + refill double-jump SAI giữa
    /// chừng (feedback test). CHỈ lọc theo vy cho tiếp xúc OneWayPlatform — Ground CỨNG luôn tin tuyệt
    /// đối, KHÔNG lọc gì cả (đúng quyết định thiết kế cũ ở AirborneStateBase.Land(): "vy lúc tiếp đất
    /// đôi khi dương do jitter" — lọc theo vy cho CẢ Ground từng gây regression thật: LauncherState.Tumble
    /// nằm trên Ground cứng bị filter bắt nhầm, kẹt nằm vĩnh viễn — feedback test đã gặp, ĐỪNG lặp lại).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class GroundCheck : MonoBehaviour
    {
        [Tooltip("Collider thân để kiểm tra chạm sàn. Bỏ trống = lấy Collider2D trên cùng object.")]
        [SerializeField] private Collider2D bodyCollider;

        [Tooltip("Rigidbody2D để lọc false-positive lúc xuyên platform 1 chiều (đang bay lên). Bỏ trống = lấy trên object.")]
        [SerializeField] private Rigidbody2D body;

        [Tooltip("Ngưỡng vy (world/giây) coi là 'đang xuyên qua platform 1 chiều' — CHỈ áp dụng cho tiếp xúc " +
                 "OneWayPlatform, KHÔNG áp dụng cho Ground cứng. Đủ thấp hơn jumpSpeed (12) để lọc được lúc xuyên thật.")]
        [SerializeField] private float risingVelocityThreshold = 1.5f;

        /// <summary>True nếu chạm Ground cứng (luôn tin), HOẶC chạm OneWayPlatform mà KHÔNG đang bay lên
        /// rõ rệt (loại false-positive lúc xuyên qua one-way platform).</summary>
        public bool IsGrounded { get; private set; }

        private void Awake()
        {
            if (bodyCollider == null)
                bodyCollider = GetComponent<Collider2D>();
            if (body == null)
                body = GetComponent<Rigidbody2D>();
        }

        private void FixedUpdate()
        {
            if (bodyCollider == null) { IsGrounded = false; return; }

            // Ground cứng: tin tuyệt đối, KHÔNG lọc vy (giữ đúng hành vi gốc — landing đôi khi vy dương
            // do jitter de-penetration là BÌNH THƯỜNG, không phải dấu hiệu "đang xuyên qua").
            bool touchingGround = bodyCollider.IsTouchingLayers(GameLayers.GroundMask);

            // OneWayPlatform: CHỈ nơi có gotcha effector-vẫn-báo-touching-khi-xuyên-qua → lọc vy.
            bool touchingOneWay = bodyCollider.IsTouchingLayers(GameLayers.OneWayPlatformMask);
            bool risingThroughPlatform = touchingOneWay && body != null &&
                                          body.linearVelocity.y > risingVelocityThreshold;

            IsGrounded = touchingGround || (touchingOneWay && !risingThroughPlatform);
        }
    }
}
