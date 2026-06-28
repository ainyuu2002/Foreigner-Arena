using UnityEngine;
using FA.Core;

namespace FA.Specials
{
    /// <summary>
    /// RC DASH (Roman Cancel Dash, StateId 28, P10) — lướt ngang + CANCEL đòn đang ra + i-frame ngắn
    /// (research §3.3, GG Roman Cancel). KHÁC combo-cancel (CancelWindowOpen/Close của AttackState):
    /// RC cancel KHÔNG cần cửa sổ — tiêu MANA là "giấy phép" cancel BẤT CỨ LÚC NÀO. DefenseController
    /// ÉP ChangeState(this) kể cả khi đang AttackState/SpecialState/SuperState (state cũ Exit() tự
    /// unsubscribe relay → sạch, không leak).
    ///
    /// LƯỚT: set linearVelocity.x = rcDashSpeed × facing trong Enter (impulse), khóa Y phẳng (giống
    /// DashState P3). i-frame ngắn = clip RC đặt Animation Event HurtboxOff đầu, HurtboxOn cuối (Unity-
    /// native, §3.1). KHÓA input tới RecoveryEnd (KHÔNG đếm frame). Mana TRỪ do DefenseController TRƯỚC.
    ///
    /// FAIL-SAFE: Exit() relay.HurtboxOn() (research §4.2). Đặt FA.Specials, ICombatContext (Core) — §1.3.
    /// </summary>
    public sealed class RCDashState : IState
    {
        private const float MoveDeadzone = 0.1f;

        private readonly ICombatContext _ctx;
        private readonly AnimationEventRelay _relay;
        private readonly DefenseConfigSO _config;

        private bool _recoveryDone;

        public RCDashState(ICombatContext ctx, AnimationEventRelay relay, DefenseConfigSO config)
        {
            _ctx = ctx;
            _relay = relay;
            _config = config;
        }

        public void Enter()
        {
            _recoveryDone = false;
            _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, DefenseStateIds.RCDash);
            if (_relay != null) _relay.RecoveryEndEvent += OnRecoveryEnd;

            // Lướt ngang theo facing (impulse 1 lần). Khóa Y phẳng (giống DashState P3): set vy=0.
            float speed = _config != null ? _config.rcDashSpeed : 14f;
            Rigidbody2D rb = _ctx.Body;
            rb.linearVelocity = new Vector2(_ctx.Facing * speed, 0f);
        }

        public void Tick(float dt)
        {
            if (!_recoveryDone) return;
            bool moving = Mathf.Abs(_ctx.MoveX) > MoveDeadzone;
            _ctx.Machine.ChangeState(moving ? _ctx.WalkState() : _ctx.IdleState());
        }

        public void FixedTick(float dt)
        {
            // Giữ lướt phẳng: ghì vy=0 trong suốt RC (khóa trục Y giống dash P3). vx do impulse Enter giữ.
            Rigidbody2D rb = _ctx.Body;
            Vector2 v = rb.linearVelocity;
            v.y = 0f;
            rb.linearVelocity = v;
        }

        public void Exit()
        {
            if (_relay != null)
            {
                _relay.RecoveryEndEvent -= OnRecoveryEnd;
                _relay.HurtboxOn(); // fail-safe i-frame off.
            }
        }

        private void OnRecoveryEnd() => _recoveryDone = true;
    }
}
