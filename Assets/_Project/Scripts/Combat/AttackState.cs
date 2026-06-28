using UnityEngine;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// MỘT state cho cả L/M/H (và P7 aerial) — tham số hóa bởi <see cref="MoveData"/> (§3.4/§5).
    /// Đòn mới = thêm 1 asset MoveData, KHÔNG thêm class (extensible §1.2). 1 state class, KHÔNG 3 class.
    ///
    /// QUY TẮC VÀNG (§1.1): KHÔNG đếm frame startup/active/recovery. State chỉ KHÓA input từ lúc vào
    /// tới khi nhận <see cref="AnimationEventRelay.RecoveryEndEvent"/> (Animation Event trên clip) →
    /// mới về Idle/Walk. Hitbox active = HitboxOn/Off (do HitboxController lo, §3.4) — KHÔNG ở đây.
    ///
    /// State NUỐT input (bỏ qua dash/jump/đi) suốt startup→recovery: nó đơn giản KHÔNG ChangeState khi
    /// chưa RecoveryEnd. Motor entry-point chỉ kích dash/jump/walk-off khi đang Idle/Walk (IsGroundState)
    /// → đang AttackState (state lạ) KHÔNG bị Motor kích → input khóa đúng (research §3.3 verify).
    ///
    /// CancelWindowOpen/Close = HOOK P7 (combo cancel) — P5 chỉ giữ cờ _canCancel, CHƯA cho cancel.
    /// Đặt ở FA.Combat, dùng ICombatContext (Core) — KHÔNG reference FA.Movement (§1.3).
    /// </summary>
    public sealed class AttackState : IState
    {
        private const float MoveDeadzone = 0.1f;

        private readonly ICombatContext _ctx;
        private readonly AnimationEventRelay _relay;

        // MoveData đòn HIỆN HÀNH — CombatController set qua SetMove TRƯỚC khi ChangeState (Enter dùng).
        private MoveData _move;

        // Cờ điều khiển flow — đặt lại mỗi Enter (KHÔNG phải bộ đếm frame).
        private bool _recoveryDone;
        private bool _canCancel;   // HOOK P7: cửa sổ cancel mở/đóng theo Animation Event.

        public AttackState(ICombatContext ctx, AnimationEventRelay relay)
        {
            _ctx = ctx;
            _relay = relay;
        }

        /// <summary>Đặt đòn sẽ ra TRƯỚC khi ChangeState (CombatController gọi). 1 state phục vụ L/M/H.</summary>
        public void SetMove(MoveData move) => _move = move;

        /// <summary>HOOK P7: cửa sổ cancel đang mở không (combo). P5 chưa tiêu thụ.</summary>
        public bool CanCancel => _canCancel;

        public void Enter()
        {
            _recoveryDone = false;
            _canCancel = false;

            // Cầu nối code↔Animator (§3.1): set StateId = MoveData.animatorStateId → transition Any State
            // → Attack_L/M/H cấu hình TRONG cửa sổ Animator. KHÔNG Animator.Play, KHÔNG đụng enum Movement.
            if (_move != null)
                _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, _move.animatorStateId);

            // Subscribe relay per-instance (đúng nhân vật này). Unsubscribe ở Exit (tránh leak/double-fire).
            if (_relay != null)
            {
                _relay.RecoveryEndEvent += OnRecoveryEnd;
                _relay.CancelWindowOpenEvent += OnCancelOpen;
                _relay.CancelWindowCloseEvent += OnCancelClose;
            }
        }

        public void Tick(float dt)
        {
            // KHÓA input: KHÔNG chuyển state cho tới khi Animation Event RecoveryEnd bắn (KHÔNG timer).
            if (!_recoveryDone) return;

            // Hết recovery → về Idle/Walk theo ý định di chuyển (đứng đất). (Aerial/landing-từ-đòn = P7.)
            bool moving = Mathf.Abs(_ctx.MoveX) > MoveDeadzone;
            _ctx.Machine.ChangeState(moving ? _ctx.WalkState() : _ctx.IdleState());
        }

        public void FixedTick(float dt)
        {
            // Đòn thường mặt đất đứng tại chỗ: giữ vx≈0 (giữ vy cho gravity/landing tự nhiên). KHÔNG
            // AddForce. (Đòn có di chuyển/aerial = P7.) Chỉ ghì ngang khi đang đứng đất.
            if (_ctx.IsGrounded)
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
                _relay.CancelWindowOpenEvent -= OnCancelOpen;
                _relay.CancelWindowCloseEvent -= OnCancelClose;
            }
        }

        // ===== Animation Event callbacks (per-instance relay) =====
        private void OnRecoveryEnd() => _recoveryDone = true;
        private void OnCancelOpen() => _canCancel = true;    // HOOK P7
        private void OnCancelClose() => _canCancel = false;  // HOOK P7
    }
}
