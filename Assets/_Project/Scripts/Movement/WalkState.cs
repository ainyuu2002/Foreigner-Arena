using UnityEngine;
using FA.Core;

namespace FA.Movement
{
    /// <summary>
    /// Đi bộ ngang (Phase 1). Set velocity.x = dir * walkSpeed (GIỮ velocity.y), lật hướng nhìn
    /// theo dir. Input ngang ~0 → về Idle. Tốc độ cố định kiểu SF (không gia tốc/đà ở P1; dash ở P3).
    /// KHÔNG dời transform, KHÔNG Animator.Play.
    /// </summary>
    public sealed class WalkState : IState
    {
        private const float MoveDeadzone = 0.1f;

        private readonly ICharacterContext _ctx;

        public WalkState(ICharacterContext ctx) => _ctx = ctx;

        public void Enter()
        {
            _ctx.Animator.SetInteger(MovementAnimatorParams.StateId, (int)CharacterStateId.Walk);
        }

        public void Tick(float dt)
        {
            float moveX = _ctx.Input.MoveX;
            if (Mathf.Abs(moveX) <= MoveDeadzone)
            {
                _ctx.Machine.ChangeState(_ctx.IdleState());
                return;
            }

            // P6 — CHỐT FACING MODEL: KHÔNG còn flip-theo-hướng-đi ở đây. Facing giờ do
            // FacingController (FA.Combat) độc quyền: auto-face-enemy kiểu SF khi ground-neutral
            // (Idle/Walk), khóa khi đánh/hurt/block/trên không. WalkState chỉ lo VẬN TỐC ngang.
            // (Lý do bỏ: block High/Low + motion QCF/QCB cần "forward/back theo ĐỊCH", không theo
            // hướng đi. Đây là sửa-có-chủ-đích — xem Guide Phase 6.)
        }

        public void FixedTick(float dt)
        {
            float moveX = _ctx.Input.MoveX;
            // Đứng yên thì không đẩy ngang (chuyển state sẽ xảy ra ở Tick frame kế).
            float dir = Mathf.Abs(moveX) > MoveDeadzone ? Mathf.Sign(moveX) : 0f;

            Rigidbody2D rb = _ctx.Body;
            // (P10) Nhân buff +Speed của Rage Mode (×1.0 nếu không Rage / chưa gắn RageModeController →
            // hành vi P1 không đổi). MoveSpeedMult do Motor resolve null-safe qua IBuffContext (FA.Core).
            float speed = _ctx.Data.walkSpeed * _ctx.MoveSpeedMult;
            rb.linearVelocity = new Vector2(dir * speed, rb.linearVelocity.y);
        }

        public void Exit() { }
    }
}
