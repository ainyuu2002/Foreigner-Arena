using UnityEngine;
using FA.Core;

namespace FA.Specials
{
    /// <summary>
    /// MỘT state cho MỌI special (motion input) — tham số hóa bởi <see cref="MoveData"/> (research §3.2).
    /// Clone pattern <c>AttackState</c> (P5): set StateId=move.animatorStateId, KHÓA input tới Animation
    /// Event <see cref="AnimationEventRelay.RecoveryEndEvent"/> (KHÔNG đếm frame — QUY TẮC VÀNG §1.1),
    /// subscribe relay per-instance, Exit unsubscribe. Đòn mới = thêm asset + entry MoveSetData, KHÔNG
    /// thêm class (extensible §1.2).
    ///
    /// KHÁC AttackState: special có thể là PROJECTILE (role=Projectile) — spawn chưởng do
    /// <c>ProjectileSpawner</c> nghe Animation Event SpawnProjectile (KHÔNG ở đây). Special cũng có thể
    /// CHARGED (giữ→nhả nút): việc CHỌN MoveData charged do SpecialController lo (nó truyền MoveData đã
    /// chọn qua <see cref="SetMove"/>); state này chỉ chạy ĐÚNG 1 MoveData được giao (StateId charged
    /// riêng nếu MoveData.charged có animatorStateId khác).
    ///
    /// Đặt ở FA.Specials, dùng ICombatContext (Core) — KHÔNG reference FA.Movement/FA.Combat (§1.3).
    /// Gate grounded do SpecialController quyết TRƯỚC khi vào (special thường = ground-only ở P9).
    /// </summary>
    public sealed class SpecialState : IState
    {
        private const float MoveDeadzone = 0.1f;

        private readonly ICombatContext _ctx;
        private readonly AnimationEventRelay _relay;

        // MoveData special HIỆN HÀNH — SpecialController set qua SetMove TRƯỚC khi ChangeState.
        private MoveData _move;

        private bool _recoveryDone;

        public SpecialState(ICombatContext ctx, AnimationEventRelay relay)
        {
            _ctx = ctx;
            _relay = relay;
        }

        /// <summary>Đặt special sẽ ra TRƯỚC khi ChangeState (SpecialController gọi). 1 state phục vụ mọi special.</summary>
        public void SetMove(MoveData move) => _move = move;

        public void Enter()
        {
            _recoveryDone = false;

            // Cầu nối code↔Animator (§3.1): set StateId = MoveData.animatorStateId → transition Any State
            // → clip special cấu hình TRONG cửa sổ Animator. KHÔNG Animator.Play.
            if (_move != null)
                _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, _move.animatorStateId);

            // Subscribe relay per-instance (đúng nhân vật này). Unsubscribe ở Exit (tránh leak/double-fire).
            if (_relay != null)
                _relay.RecoveryEndEvent += OnRecoveryEnd;
        }

        public void Tick(float dt)
        {
            // KHÓA input: KHÔNG chuyển state tới khi Animation Event RecoveryEnd bắn (KHÔNG timer).
            if (!_recoveryDone) return;

            // Hết recovery → về Idle/Walk theo ý định di chuyển (special thường ground-only ở P9).
            bool moving = Mathf.Abs(_ctx.MoveX) > MoveDeadzone;
            _ctx.Machine.ChangeState(moving ? _ctx.WalkState() : _ctx.IdleState());
        }

        public void FixedTick(float dt)
        {
            // Special mặt đất đứng tại chỗ: ghì vx≈0 (giữ vy cho gravity/landing). Đòn có di chuyển =
            // đặt vận tốc qua đòn-cụ-thể sau (P10). Chỉ ghì khi đang đứng đất.
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
                _relay.RecoveryEndEvent -= OnRecoveryEnd;
        }

        // ===== Animation Event callback (per-instance relay) =====
        private void OnRecoveryEnd() => _recoveryDone = true;
    }
}
