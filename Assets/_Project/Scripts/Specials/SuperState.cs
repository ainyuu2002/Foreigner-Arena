using UnityEngine;
using FA.Core;

namespace FA.Specials
{
    /// <summary>
    /// MỘT state cho MỌI super (nút riêng Super1/Super2 + cost mana) — tham số bởi <see cref="MoveData"/>
    /// (research §3.2). Như <see cref="SpecialState"/> nhưng:
    ///  - DÙNG ĐƯỢC TRÊN KHÔNG (GDD §4.2 "usable in the air") → KHÔNG ghì vx≈0, KHÔNG gate grounded.
    ///  - KHÔNG cần motion (kích bằng nút, SpecialController tra MoveSetData entry motion=None).
    ///  - Khi RecoveryEnd: về Idle/Walk nếu ĐANG ĐẤT; nếu còn TRÊN KHÔNG → set StateId Fall (3) để
    ///    Animator vào clip rơi rồi Motor tự tiếp quản (đi-khỏi-mép-sàn → AirState) khi Tick movement
    ///    chạy lại. ICombatContext CHƯA expose air-state (research §4.4 cạm bẫy) → dùng StateId Fall =
    ///    cầu nối Animator, KHÔNG ref FA.Movement (§1.3).
    ///
    /// Mana TRỪ do SpecialController (TrySpend TRƯỚC ChangeState) — state này KHÔNG đụng mana.
    /// Đặt ở FA.Specials, dùng ICombatContext (Core) — KHÔNG ref FA.Movement/FA.Combat (§1.3).
    /// </summary>
    public sealed class SuperState : IState
    {
        private const float MoveDeadzone = 0.1f;

        // StateId Fall = 3 (CharacterStateId.Fall của Movement, literal vì cross-module §1.3). Khi super
        // kết thúc trên không → vào clip rơi; Motor tiếp quản chuyển AirState ở frame Update kế.
        private const int FallStateId = 3;

        private readonly ICombatContext _ctx;
        private readonly AnimationEventRelay _relay;

        private MoveData _move;
        private bool _recoveryDone;

        public SuperState(ICombatContext ctx, AnimationEventRelay relay)
        {
            _ctx = ctx;
            _relay = relay;
        }

        /// <summary>Đặt super sẽ ra TRƯỚC khi ChangeState (SpecialController gọi).</summary>
        public void SetMove(MoveData move) => _move = move;

        public void Enter()
        {
            _recoveryDone = false;
            if (_move != null)
                _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, _move.animatorStateId);
            if (_relay != null)
                _relay.RecoveryEndEvent += OnRecoveryEnd;
        }

        public void Tick(float dt)
        {
            if (!_recoveryDone) return;

            // ĐẤT: về Idle/Walk theo ý định di chuyển (giống AttackState).
            if (_ctx.IsGrounded)
            {
                bool moving = Mathf.Abs(_ctx.MoveX) > MoveDeadzone;
                _ctx.Machine.ChangeState(moving ? _ctx.WalkState() : _ctx.IdleState());
                return;
            }

            // TRÊN KHÔNG: set clip Fall (Animator-bridge) rồi về IdleState — Motor.Update frame KẾ thấy
            // (!IsGrounded && IsGroundState) → tự ChangeState sang AirState (đúng cơ chế "đi-khỏi-mép-sàn").
            // Đặt StateId Fall TRƯỚC ChangeState để IdleState.Enter override = Idle chỉ 1 frame quá độ;
            // ngay frame kế Motor về AirState (StateId Fall) → không kẹt clip Idle giữa trời.
            _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, FallStateId);
            _ctx.Machine.ChangeState(_ctx.IdleState());
        }

        public void FixedTick(float dt)
        {
            // KHÔNG ghì vx (super air-usable — cho phép giữ momentum trên không). Gravity do Rigidbody2D.
        }

        public void Exit()
        {
            if (_relay != null)
                _relay.RecoveryEndEvent -= OnRecoveryEnd;
        }

        private void OnRecoveryEnd() => _recoveryDone = true;
    }
}
