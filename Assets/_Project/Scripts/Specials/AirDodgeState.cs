using UnityEngine;
using FA.Core;

namespace FA.Specials
{
    /// <summary>
    /// AIR DODGE (StateId 27, P10) — né TRÊN KHÔNG (Smash air-dodge): i-frame + LANDING LAG. 1 lần/lần-
    /// trên-không (quota do DefenseController giữ — pattern air-dash P3). i-frame = TẮT Hurtbox qua
    /// Animation Event HurtboxOff/On trên clip air-dodge (research §3.1, Unity-native — KHÔNG cờ).
    ///
    /// 2 PHA trong 1 state (giữ 1-file-1-chức-năng):
    ///  A) TRÊN KHÔNG: khóa input; Animator dùng StateId 27 + bool IsGrounded=false → clip air-dodge.
    ///     Gravity do Rigidbody2D (giữ momentum). Gate HasLeftGround (P2/memory fixed-jump) chống
    ///     "ghost-landing": phải RỜI đất ≥1 frame rồi mới cho vào landing-lag.
    ///  B) ĐÁP ĐẤT: set bool IsGrounded=true → Animator vào clip LANDING (cùng StateId 27) → khóa tới
    ///     Animation Event RecoveryEnd (landing lag = frame trên clip landing, KHÔNG timer — QUY TẮC
    ///     VÀNG §1.1). RecoveryEnd → về Idle/Walk.
    ///
    /// FAIL-SAFE: Exit() gọi relay.HurtboxOn() (bật lại Hurtbox nếu bị ngắt giữa né, research §4.2).
    /// Mana/quota TRỪ do DefenseController TRƯỚC ChangeState. Đặt FA.Specials, dùng ICombatContext (Core).
    /// </summary>
    public sealed class AirDodgeState : IState
    {
        private const float MoveDeadzone = 0.1f;
        // Param phụ Animator để phân biệt pha air-dodge vs landing trong CÙNG StateId 27 (đã có "IsGrounded"
        // do Movement set mỗi frame — air-dodge clip lọc IsGrounded=false, landing clip lọc =true).
        private readonly ICombatContext _ctx;
        private readonly AnimationEventRelay _relay;

        private bool _hasLeftGround;   // gate chống ghost-landing (P2): chỉ đáp khi đã rời đất thật.
        private bool _landing;         // đã vào pha landing-lag chưa.
        private bool _recoveryDone;    // RecoveryEnd của clip landing.

        public AirDodgeState(ICombatContext ctx, AnimationEventRelay relay)
        {
            _ctx = ctx;
            _relay = relay;
        }

        public void Enter()
        {
            _hasLeftGround = false;
            _landing = false;
            _recoveryDone = false;
            _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, DefenseStateIds.AirDodge);
            if (_relay != null) _relay.RecoveryEndEvent += OnRecoveryEnd;
        }

        public void Tick(float dt)
        {
            // Gate HasLeftGround: né thường bắt đầu khi đang trên không, nhưng chống case đáp-ngay.
            if (!_ctx.IsGrounded) _hasLeftGround = true;

            if (!_landing)
            {
                // Vào pha LANDING khi đã rời đất rồi & nay chạm đất (HasLeftGround đảm bảo không ghost).
                if (_hasLeftGround && _ctx.IsGrounded)
                {
                    _landing = true;
                    // Re-set StateId (giữ 27) — Animator chuyển sang clip landing nhờ bool IsGrounded=true.
                    _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, DefenseStateIds.AirDodge);
                }
                return; // còn trên không: khóa input (i-frames clip air-dodge).
            }

            // Pha landing-lag: khóa tới RecoveryEnd (landing lag = Animation Event trên clip landing).
            if (!_recoveryDone) return;
            bool moving = Mathf.Abs(_ctx.MoveX) > MoveDeadzone;
            _ctx.Machine.ChangeState(moving ? _ctx.WalkState() : _ctx.IdleState());
        }

        public void FixedTick(float dt)
        {
            // Trên không: GIỮ momentum (gravity do Rigidbody2D — KHÔNG ghì). Landing: ghì vx≈0 (đứng yên đáp).
            if (_landing && _ctx.IsGrounded)
            {
                Rigidbody2D rb = _ctx.Body;
                Vector2 v = rb.linearVelocity;
                v.x = 0f;
                rb.linearVelocity = v;
            }
        }

        public void Exit()
        {
            if (_relay != null)
            {
                _relay.RecoveryEndEvent -= OnRecoveryEnd;
                _relay.HurtboxOn(); // fail-safe i-frame off (research §4.2).
            }
        }

        private void OnRecoveryEnd() => _recoveryDone = true;
    }
}
