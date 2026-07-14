using UnityEngine;
using FA.Core;

namespace FA.Movement
{
    /// <summary>
    /// NGỒI (Crouch) — POSE-ONLY (A2). Vào từ Idle/Walk khi ĐỨNG ĐẤT + giữ Xuống THUẦN (MoveY xuống,
    /// MoveX≈0 → down-forward/down-back vẫn ĐI BỘ, không ngồi). Đứng yên (vx=0, GIỮ vy cho gravity —
    /// đối xứng IdleState). Thoát khi nhả Xuống hoặc bắt đầu ép ngang → Idle/Walk.
    ///
    /// Nhảy / Dash / rời-đất THOÁT khỏi Ngồi do ENTRY-POINT TOÀN CỤC ở <see cref="CharacterMotor"/> lo
    /// (Crouch nằm trong IsGroundState + IsOwnMovementState) — giống Idle/Walk, KHÔNG tự xử ở đây.
    /// KHÔNG ra đòn tấn công từ Ngồi (CombatController chỉ cho đánh từ Idle/Walk — không đụng). Block-low
    /// giữ NGUYÊN (HitReactionController vẫn chỉ thủ từ Idle/Walk/Block — ngoài phạm vi A2, quyết định user).
    /// </summary>
    public sealed class CrouchState : IState
    {
        private const float MoveDeadzone = 0.1f;   // ép ngang > ngưỡng này → đi bộ (thoát Ngồi).
        private const float DownThreshold = 0.5f;  // giữ Xuống < -ngưỡng này mới coi là đang ngồi.

        private readonly ICharacterContext _ctx;

        public CrouchState(ICharacterContext ctx) => _ctx = ctx;

        public void Enter()
        {
            // Cầu nối DUY NHẤT tới Animator: set StateId, transition do cửa sổ Animator lo (§3.1).
            _ctx.Animator.SetInteger(MovementAnimatorParams.StateId, (int)CharacterStateId.Crouch);
        }

        public void Tick(float dt)
        {
            // Thoát Ngồi: nhả Xuống → Idle; bắt đầu ép ngang (down-forward/back = đi bộ) → Walk.
            // Rời đất / nhảy / dash do entry-point TOÀN CỤC ở Motor (Crouch là ground-state) — không xử ở đây.
            bool holdingDown = _ctx.Input.MoveY < -DownThreshold;
            bool pressingSide = Mathf.Abs(_ctx.Input.MoveX) > MoveDeadzone;
            if (!holdingDown || pressingSide)
                _ctx.Machine.ChangeState(pressingSide ? _ctx.WalkState() : _ctx.IdleState());
        }

        public void FixedTick(float dt)
        {
            // Đứng yên (vx=0), GIỮ vy cho gravity (đối xứng IdleState — không dời transform §1.1).
            Rigidbody2D rb = _ctx.Body;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        public void Exit() { }
    }
}
