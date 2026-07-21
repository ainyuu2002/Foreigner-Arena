using UnityEngine;
using FA.Core;

namespace FA.Specials
{
    /// <summary>
    /// ULTIMATE (StateId 31, P10) — đòn kết liễu khi đang Rage Mode (nút Rage trong Rage). Đặc tính GDD:
    ///  • ANIMATION SPEED CỐ ĐỊNH = MIỄN NHIỄM hitstop/slow → Enter set RageModeController.SetUltimateActive
    ///    (true); HitstopController (P5, ADD null-safe) BỎ QUA freeze khi attacker/victim IsUltimateActive
    ///    → clip ultimate chạy mượt dù trúng (research §3.5). Exit set false.
    ///  • PHÁ BLOCK HOÀN TOÀN: MoveData.role = <see cref="MoveRole.BlockBreak"/> → HitboxAuthoring mang Role
    ///    vào HitData → HitReactionController (P6, ADD) thấy BlockBreak thì BỎ nhánh block, LUÔN Hurt.
    ///  • HITBOX TO + startup nhanh + % thấp đẩy xa / % cao KILL: tất cả là tham số MoveData (asset
    ///    Ultimate: baseKnockback/growth lớn) → TÁI DÙNG KnockbackResolver P7, KHÔNG code mới.
    ///
    /// Clone pattern SpecialState: set StateId=31 → Animator clip ultimate; HitboxController.SetCurrentMove
    /// + Animation Event HitboxOn bật hitbox; khóa input tới RecoveryEnd (KHÔNG đếm frame). DefenseController
    /// trừ tài nguyên/kiểm Rage TRƯỚC ChangeState. Đặt FA.Specials, ICombatContext (Core) — §1.3.
    /// </summary>
    public sealed class UltimateState : IState
    {
        private const float MoveDeadzone = 0.1f;

        private readonly ICombatContext _ctx;
        private readonly AnimationEventRelay _relay;
        private readonly RageModeController _rage;
        // (Ultimate-trong-UBW, Archer) Root người đánh — gate ProjectilePoseLock: beam Excalibur giữ
        // caster nguyên pose tới khi đánh xong (pattern SuperState/mũi khoan). Null = không gate.
        private readonly GameObject _attackerRoot;
        // (Ultimate-trong-UBW) Install-mode để KẾT THÚC khi đòn kết xong ("dùng chiêu là hết nộ luôn",
        // user chốt). Null (nhân vật không có UBW) = giữ hành vi cũ.
        private readonly UBWInstallController _install;

        private MoveData _move;
        private bool _recoveryDone;

        public UltimateState(ICombatContext ctx, AnimationEventRelay relay, RageModeController rage,
                             GameObject attackerRoot = null, UBWInstallController install = null)
        {
            _ctx = ctx;
            _relay = relay;
            _rage = rage;
            _attackerRoot = attackerRoot;
            _install = install;
        }

        /// <summary>DefenseController set MoveData Ultimate TRƯỚC ChangeState (1 state, data-driven).</summary>
        public void SetMove(MoveData move) => _move = move;

        public void Enter()
        {
            _recoveryDone = false;
            if (_move != null)
                _ctx.Animator.SetInteger(CoreAnimatorParams.StateId,
                    _move.animatorStateId >= 0 ? _move.animatorStateId : DefenseStateIds.Ultimate);
            else
                _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, DefenseStateIds.Ultimate);

            if (_relay != null)
            {
                _relay.RecoveryEndEvent += OnRecoveryEnd;
                // I-FRAME TOÀN STATE (MUGEN NotHitBy trigger1=1): tắt Hurtbox suốt đòn kết — cinematic
                // không bị ngắt. Exit() bật lại (fail-safe pattern GroundDodgeState).
                _relay.HurtboxOff();
            }

            // Miễn nhiễm hitstop suốt Ultimate (animation speed cố định, research §3.5/§4 cạm bẫy 7).
            if (_rage != null) _rage.SetUltimateActive(true);
        }

        public void Tick(float dt)
        {
            if (!_recoveryDone) return;

            // GIỮ TƯ THẾ tới khi projectile giữ-pose (beam Excalibur) biến mất — pattern SuperState.
            if (_attackerRoot != null && ProjectilePoseLock.IsHeld(_attackerRoot)) return;
            if (_ctx.IsGrounded)
            {
                bool moving = Mathf.Abs(_ctx.MoveX) > MoveDeadzone;
                _ctx.Machine.ChangeState(moving ? _ctx.WalkState() : _ctx.IdleState());
                return;
            }
            // Kết thúc trên không → clip Fall (cầu nối Animator) → Motor tiếp quản AirState.
            _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, DefenseStateIds.Fall);
            _ctx.Machine.ChangeState(_ctx.IdleState());
        }

        public void FixedTick(float dt)
        {
            // Ghì vx≈0 mặt đất (cinematic đứng đánh). Trên không giữ momentum (gravity Rigidbody2D).
            if (!_ctx.IsGrounded) return;
            Rigidbody2D rb = _ctx.Body;
            Vector2 v = rb.linearVelocity;
            v.x = 0f;
            rb.linearVelocity = v;
        }

        public void Exit()
        {
            if (_relay != null)
            {
                _relay.RecoveryEndEvent -= OnRecoveryEnd;
                _relay.HurtboxOn();   // fail-safe: chắc chắn bật lại Hurtbox khi rời state.
            }
            if (_rage != null) _rage.SetUltimateActive(false); // hết miễn nhiễm hitstop.

            // Gọi VÔ ĐIỀU KIỆN (ForceEnd tự guard nội bộ): dọn UBW đang install (fade đen revert
            // bg/nhạc/aura) LẪN ca chant bị huỷ giữa chừng trước khi kịp install (đã duck BGM map ở
            // ChantStart nhưng chưa có bg/buff gì — chỉ cần trả BGM lại, không chuyển cảnh).
            _install?.ForceEnd();

            // MỌI Ultimate: DÙNG XONG = HẾT SẠCH NỘ (user chốt 2026-07-11) — tắt buff không refund +
            // xóa thanh về 0. Trước đây chỉ nhánh UBW tắt buff và KHÔNG xóa thanh → rage hết hạn
            // tự nhiên sau ulti vẫn refund 22% / nộ gain sót còn nguyên → exploit nộ liên tục.
            _rage?.DeactivateAndClear();
        }

        private void OnRecoveryEnd() => _recoveryDone = true;
    }
}
