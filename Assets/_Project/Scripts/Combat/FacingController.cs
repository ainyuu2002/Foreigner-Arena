using UnityEngine;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// FACING MODEL chốt ở P6 (research §3.1, Phương án B) — auto-face-enemy kiểu SF: nhân vật trên
    /// ĐẤT-neutral (Idle/Walk) luôn quay mặt về ĐỐI THỦ; KHÓA facing khi đang đánh / hurt / block /
    /// trên không. Lý do: block High/Low + motion QCF/QCB cần "forward/back theo ĐỊCH". Thay
    /// flip-theo-hướng-đi của WalkState (P1–5, đã gỡ).
    ///
    /// Đặt ở FA.Combat, gọi <see cref="ICombatContext.SetFacing"/> (gom 1 chỗ ở Motor — lật localScale
    /// + child Hitbox/Hurtbox). KHÔNG `using FA.Movement`. Cờ gate lấy từ CombatController (cùng module).
    ///
    /// Đối thủ: field set TAY trong scene test P6 (2 nhân vật gán chéo). P12 MatchManager sẽ cấp runtime
    /// (có SetOpponent). Null → KHÔNG auto-face (giữ hướng) → an toàn khi chưa có địch.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FacingController : MonoBehaviour
    {
        [Header("Đối thủ (set tay P6; MatchManager cấp ở P12)")]
        [Tooltip("Transform đối thủ để quay mặt về. Null = không auto-face (giữ hướng).")]
        [SerializeField] private Transform opponent;

        [Tooltip("CombatController cùng prefab — đọc cờ IsAttacking/IsHurt/IsBlocking để gate. " +
                 "Bỏ trống = lấy trên cùng object.")]
        [SerializeField] private CombatController combat;

        [Tooltip("Khoảng cách x tối thiểu để đổi mặt (tránh rung khi 2 nhân vật gần trùng x). CẦN TEST.")]
        [SerializeField] private float faceDeadzone = 0.05f;

        private ICombatContext _ctx;

        /// <summary>P12 MatchManager (hoặc scene setup) cấp đối thủ runtime.</summary>
        public void SetOpponent(Transform t) => opponent = t;

        private void Awake()
        {
            _ctx = GetComponent<ICombatContext>();
            if (combat == null) combat = GetComponent<CombatController>();
            if (_ctx == null)
                Debug.LogError("[FacingController] Thiếu ICombatContext (CharacterMotor) trên prefab.", this);
        }

        // LateUpdate: sau khi state/motor đã chạy Update → facing chốt cuối frame (không bị Walk ghi đè
        // — Walk đã gỡ SetFacing). Auto-face chỉ ở ground-neutral; mọi state khác giữ hướng.
        private void LateUpdate()
        {
            if (_ctx == null || opponent == null) return;
            if (!CanAutoFace()) return;

            float dx = opponent.position.x - transform.position.x;
            if (Mathf.Abs(dx) < faceDeadzone) return;   // quá gần → giữ hướng (chống rung).
            _ctx.SetFacing(dx > 0f ? 1 : -1);
        }

        /// <summary>
        /// Chỉ auto-face khi GROUND-NEUTRAL: đứng đất + KHÔNG đang đánh/hurt/block. (Trên không =
        /// !IsGrounded → khóa; cross-up nhảy qua đầu không xoay liên tục, đổi mặt khi ĐÁP. CẦN TEST.)
        /// </summary>
        private bool CanAutoFace()
        {
            if (!_ctx.IsGrounded) return false;
            if (combat != null && (combat.IsAttacking || combat.IsHurt || combat.IsBlocking)) return false;
            return true;
        }
    }
}
