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
    /// + child Hitbox/Hurtbox). KHÔNG `using FA.Movement`. Gate = CurrentState ∈ {Idle, Walk} qua
    /// ICombatContext (bao trùm cả state FA.Specials mà module này không ref được).
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

        [Tooltip("Khoảng cách x tối thiểu để đổi mặt (tránh rung khi 2 nhân vật gần trùng x). CẦN TEST.")]
        [SerializeField] private float faceDeadzone = 0.05f;

        private ICombatContext _ctx;

        /// <summary>P12 MatchManager (hoặc scene setup) cấp đối thủ runtime.</summary>
        public void SetOpponent(Transform t) => opponent = t;

        private void Awake()
        {
            _ctx = GetComponent<ICombatContext>();
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
        /// Chỉ auto-face khi GROUND-NEUTRAL THẬT: đứng đất + CurrentState là Idle/Walk. So state qua
        /// ICombatContext (pattern CanGuardNow P6) → bao trùm MỌI state khác Idle/Walk: attack/hurt/block
        /// (FA.Combat) LẪN special/super/dodge (FA.Specials — không ref chéo được §1.3) lẫn dash. Fix bug
        /// special lướt xuyên địch bị tự lật mặt giữa đòn (gate cờ CombatController cũ không biết special).
        /// (Trên không = !IsGrounded → khóa; cross-up không xoay liên tục, đổi mặt khi ĐÁP. CẦN TEST.)
        /// </summary>
        private bool CanAutoFace()
        {
            if (!_ctx.IsGrounded) return false;
            IState s = _ctx.Machine.CurrentState;
            return ReferenceEquals(s, _ctx.IdleState()) || ReferenceEquals(s, _ctx.WalkState());
        }
    }
}
