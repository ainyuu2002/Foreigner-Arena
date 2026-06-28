using UnityEngine;
using FA.Core;

namespace FA.Movement
{
    /// <summary>
    /// Đứng yên (Phase 1). Dừng vận tốc ngang (GIỮ velocity.y để P2 gravity/jump không bị reset).
    /// Có input ngang → chuyển sang Walk. KHÔNG Animator.Play — chỉ set int StateId.
    /// </summary>
    public sealed class IdleState : IState
    {
        /// <summary>Ngưỡng coi là "có ý định đi" — tránh nhiễu analog/deadzone.</summary>
        private const float MoveDeadzone = 0.1f;

        private readonly ICharacterContext _ctx;

        public IdleState(ICharacterContext ctx) => _ctx = ctx;

        public void Enter()
        {
            // Cầu nối DUY NHẤT tới Animator: set StateId, transition do cửa sổ Animator lo.
            _ctx.Animator.SetInteger(MovementAnimatorParams.StateId, (int)CharacterStateId.Idle);
        }

        public void Tick(float dt)
        {
            // Quyết định đổi state ở Update (đọc input). Không chạm Rigidbody2D ở đây.
            if (Mathf.Abs(_ctx.Input.MoveX) > MoveDeadzone)
                _ctx.Machine.ChangeState(_ctx.WalkState());
        }

        public void FixedTick(float dt)
        {
            // Dừng ngang, GIỮ y. Vận tốc do Rigidbody2D lo (§1.1) — không dời transform.
            Rigidbody2D rb = _ctx.Body;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        public void Exit() { }
    }
}
