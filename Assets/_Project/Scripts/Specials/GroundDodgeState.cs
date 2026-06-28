using UnityEngine;
using FA.Core;

namespace FA.Specials
{
    /// <summary>
    /// GROUND DODGE (StateId 26, P10) — né TẠI CHỖ mặt đất (Smash spot-dodge). i-frame = TẮT Hurtbox
    /// trong cửa sổ né: clip đặt Animation Event <c>HurtboxOff</c> đầu, <c>HurtboxOn</c> cuối →
    /// HurtboxController (P5/P10) disable/enable collider → OnTriggerEnter2D KHÔNG kêu → vô hiệu hit TẬN
    /// GỐC (không hitstop/mana/nộ cho địch). Đây là cách Unity-native (research §3.1) — KHÔNG cờ
    /// <c>_invincible</c> rải rác, KHÔNG sửa HitReactionController P6.
    ///
    /// Clone pattern <see cref="SpecialState"/>: set StateId → Animator chạy clip; KHÓA input tới
    /// Animation Event RecoveryEnd (KHÔNG đếm frame — QUY TẮC VÀNG §1.1); subscribe relay per-instance,
    /// Exit unsubscribe. Mana TRỪ do DefenseController TRƯỚC ChangeState (state này KHÔNG đụng mana).
    ///
    /// FAIL-SAFE (research §4 cạm bẫy 2): Exit() gọi relay.HurtboxOn() → nếu né bị NGẮT giữa chừng (RC
    /// cancel / bị burst đẩy) thì Hurtbox CHẮC CHẮN bật lại → tránh "bất tử vĩnh viễn".
    /// Đặt FA.Specials, dùng ICombatContext (Core) — KHÔNG ref FA.Movement/FA.Combat (§1.3).
    /// </summary>
    public sealed class GroundDodgeState : IState
    {
        private const float MoveDeadzone = 0.1f;

        private readonly ICombatContext _ctx;
        private readonly AnimationEventRelay _relay;
        private readonly DefenseConfigSO _config;

        private bool _recoveryDone;

        public GroundDodgeState(ICombatContext ctx, AnimationEventRelay relay, DefenseConfigSO config)
        {
            _ctx = ctx;
            _relay = relay;
            _config = config;
        }

        public void Enter()
        {
            _recoveryDone = false;
            _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, DefenseStateIds.GroundDodge);
            if (_relay != null) _relay.RecoveryEndEvent += OnRecoveryEnd;
        }

        public void Tick(float dt)
        {
            if (!_recoveryDone) return;
            bool moving = Mathf.Abs(_ctx.MoveX) > MoveDeadzone;
            _ctx.Machine.ChangeState(moving ? _ctx.WalkState() : _ctx.IdleState());
        }

        public void FixedTick(float dt)
        {
            // Đứng tại chỗ (hoặc lùi nhẹ theo config). Set linearVelocity — KHÔNG tự dời transform (§1.1).
            if (!_ctx.IsGrounded) return;
            Rigidbody2D rb = _ctx.Body;
            Vector2 v = rb.linearVelocity;
            float back = _config != null ? _config.groundDodgeBackSpeed : 0f;
            v.x = back > 0f ? -_ctx.Facing * back : 0f; // lùi = ngược hướng nhìn.
            rb.linearVelocity = v;
        }

        public void Exit()
        {
            if (_relay != null)
            {
                _relay.RecoveryEndEvent -= OnRecoveryEnd;
                _relay.HurtboxOn(); // fail-safe: chắc chắn bật lại Hurtbox khi rời state (research §4.2).
            }
        }

        private void OnRecoveryEnd() => _recoveryDone = true;
    }
}
