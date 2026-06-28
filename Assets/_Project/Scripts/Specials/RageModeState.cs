using UnityEngine;
using FA.Core;

namespace FA.Specials
{
    /// <summary>
    /// RAGE MODE ENTER POSE (StateId 30, P10) — clip "bùng nộ" NGẮN khi bật Rage Mode. CHỈ là pose vào;
    /// BUFF THỰC + thời lượng + aura do <see cref="RageModeController"/> (mode, không phải state — research
    /// §3.4). Enter gọi controller.ActivateRageMode() rồi khóa input tới Animation Event RecoveryEnd →
    /// về Idle/Walk (buff vẫn chạy nền). KHÔNG khóa di chuyển dài (chỉ pose kích hoạt).
    ///
    /// Clone pattern SpecialState: set StateId → Animator clip enter-rage; KHÓA tới RecoveryEnd (KHÔNG
    /// đếm frame). Nộ TRỪ 100% do DefenseController TRƯỚC ChangeState. Đặt FA.Specials, ICombatContext.
    /// </summary>
    public sealed class RageModeState : IState
    {
        private const float MoveDeadzone = 0.1f;

        private readonly ICombatContext _ctx;
        private readonly AnimationEventRelay _relay;
        private readonly RageModeController _rage;

        private bool _recoveryDone;

        public RageModeState(ICombatContext ctx, AnimationEventRelay relay, RageModeController rage)
        {
            _ctx = ctx;
            _relay = relay;
            _rage = rage;
        }

        public void Enter()
        {
            _recoveryDone = false;
            _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, DefenseStateIds.RageMode);
            if (_relay != null) _relay.RecoveryEndEvent += OnRecoveryEnd;

            // Bật buff + aura + đếm thời lượng (mode chạy nền sau khi pose xong).
            if (_rage != null) _rage.ActivateRageMode();
        }

        public void Tick(float dt)
        {
            if (!_recoveryDone) return;
            bool moving = Mathf.Abs(_ctx.MoveX) > MoveDeadzone;
            _ctx.Machine.ChangeState(moving ? _ctx.WalkState() : _ctx.IdleState());
        }

        public void FixedTick(float dt)
        {
            // Pose tại chỗ mặt đất (ghì vx≈0). Trên không giữ momentum (gravity Rigidbody2D).
            if (!_ctx.IsGrounded) return;
            Rigidbody2D rb = _ctx.Body;
            Vector2 v = rb.linearVelocity;
            v.x = 0f;
            rb.linearVelocity = v;
        }

        public void Exit()
        {
            if (_relay != null) _relay.RecoveryEndEvent -= OnRecoveryEnd;
        }

        private void OnRecoveryEnd() => _recoveryDone = true;
    }
}
