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
    /// CHARGED (giữ→nhả nút): việc CHỌN MoveData charged do SpecialController lo.
    ///
    /// (P15) NỐI ĐÒN — INPUT-LATCH: THÊM subscribe CancelWindowOpen/Close (SpecialState P9 chưa có) + cơ
    /// chế BeginMove/chain Y NHƯ AttackState → áp combo chain cho special (rekka). Mỗi Tick TryLatch bắt
    /// intent sớm; cancel window mở mới TryResolveLatched. Re-point hitbox qua SendMessage("SetCurrentMove")
    /// để KHÔNG ref FA.Combat (§1.3). Special không có followUps → TryLatch trả false ngay → KHÔNG latch →
    /// chạy hết recovery như P9 (no-regression).
    ///
    /// Đặt ở FA.Specials, dùng ICombatContext (Core) — KHÔNG reference FA.Movement/FA.Combat (§1.3).
    /// Gate grounded do SpecialController quyết TRƯỚC khi vào (special thường = ground-only ở P9).
    /// </summary>
    public sealed class SpecialState : IState
    {
        private const float MoveDeadzone = 0.1f;

        private readonly ICombatContext _ctx;
        private readonly AnimationEventRelay _relay;
        // (P14) Uỷ quyền vx khi special có chuyển động. Null ở prefab cũ → giữ đứng yên (no-regression).
        private readonly MoveMotionController _motion;
        // (P15) Re-point MoveData hitbox mỗi mắt nối — qua SendMessage (HitboxController ở FA.Combat, KHÔNG ref).
        private readonly MonoBehaviour _hitbox;
        // (P15) Theo dõi hit-confirm cho follow-up điều kiện OnHit. Null → coi như chưa trúng.
        private readonly HitConfirmTracker _hitConfirm;
        // (P16) Kênh "VĂNG knockback" — raise tại Animation Event KnockbackPoint khi KHÔNG nối. Null → không văng.
        private readonly GameObjectEventChannelSO _onKnockbackLaunch;
        private readonly GameObject _attackerRoot;

        // MoveData HIỆN HÀNH. SpecialController set _pendingMove qua SetMove TRƯỚC ChangeState; Enter gọi
        // BeginMove(_pendingMove). Mắt nối kế set qua BeginMove trực tiếp (không ChangeState — self bị skip).
        private MoveData _pendingMove;
        private MoveData _move;

        private bool _recoveryDone;
        private bool _canCancel;   // (P15) cửa sổ cancel mở/đóng theo Animation Event.

        // (P15 latch) Nút follow-up đã bắt được SỚM (intent), chờ cancel window mở mới thực thi.
        private InputButton? _latchedButton;

        public SpecialState(ICombatContext ctx, AnimationEventRelay relay,
                            MoveMotionController motion = null,
                            MonoBehaviour hitbox = null, HitConfirmTracker hitConfirm = null,
                            GameObjectEventChannelSO onKnockbackLaunch = null, GameObject attackerRoot = null)
        {
            _ctx = ctx;
            _relay = relay;
            _motion = motion;
            _hitbox = hitbox;
            _hitConfirm = hitConfirm;
            _onKnockbackLaunch = onKnockbackLaunch;
            _attackerRoot = attackerRoot;
        }

        /// <summary>SpecialController set special sẽ ra TRƯỚC khi ChangeState. Enter() dùng làm đòn đầu chuỗi.</summary>
        public void SetMove(MoveData move) => _pendingMove = move;

        public void Enter()
        {
            // Subscribe relay per-instance (đúng nhân vật này). Unsubscribe ở Exit (tránh leak/double-fire).
            if (_relay != null)
            {
                _relay.RecoveryEndEvent += OnRecoveryEnd;
                _relay.CancelWindowOpenEvent += OnCancelOpen;
                _relay.CancelWindowCloseEvent += OnCancelClose;
                _relay.KnockbackPointEvent += OnKnockbackPoint;
            }
            BeginMove(_pendingMove);
        }

        /// <summary>(P15) GOM init PER-MOVE — gọi cả ở Enter (đòn đầu) lẫn khi NỐI (đòn kế) trong cùng state.
        /// Reset cờ flow + set StateId + re-point MoveData hitbox (SendMessage — không ref FA.Combat) +
        /// đẩy MoveData xuống MoveMotionController (P14) + reset cờ hit-confirm.</summary>
        private void BeginMove(MoveData m)
        {
            _move = m;
            _recoveryDone = false;
            _canCancel = false;
            _latchedButton = null;   // (P15 latch) mắt mới bắt đầu sạch — không bleed intent mắt trước.

            // Cầu nối code↔Animator (§3.1): set StateId → transition Any State → clip special (cấu hình Animator).
            if (_move != null)
                _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, _move.animatorStateId);

            // Re-point hitbox qua SendMessage (HitboxController.SetCurrentMove ở FA.Combat — KHÔNG ref §1.3).
            // Projectile dùng ProjectileSpawner riêng → DontRequireReceiver an toàn nếu không có hitbox trực tiếp.
            if (_hitbox != null)
                _hitbox.SendMessage("SetCurrentMove", _move, SendMessageOptions.DontRequireReceiver);
            if (_motion != null) _motion.SetMove(_move);
            if (_hitConfirm != null) _hitConfirm.BeginWindow();
        }

        public void Tick(float dt)
        {
            // (P15 latch) NHẬN INPUT SỚM: mỗi Tick từ ĐẦU đòn, bắt cú ấn follow-up (ghi đè cú mới nhất).
            if (MoveChainResolver.TryLatch(_move, _ctx.Parser, _ctx.EdgeWindow, out InputButton latched))
                _latchedButton = latched;

            // (P15) THỰC THI NỐI: chỉ khi cancel window MỞ + có intent. Re-check OnHit tại đây; chưa thoả
            // thì GIỮ latch chờ hit (KHÔNG clear). Re-init tại chỗ (self-transition bị bỏ qua ở StateMachine).
            if (_canCancel && _latchedButton.HasValue)
            {
                MoveData next = MoveChainResolver.TryResolveLatched(
                    _move, _latchedButton.Value, _ctx.Parser, MotionWindow, _ctx.Facing,
                    _hitConfirm != null && _hitConfirm.HitSinceReset);
                if (next != null) { BeginMove(next); return; }
            }

            // KHÓA input: KHÔNG chuyển state tới khi Animation Event RecoveryEnd bắn (KHÔNG timer).
            if (!_recoveryDone) return;

            // Hết recovery → về Idle/Walk theo ý định di chuyển (special thường ground-only ở P9).
            bool moving = Mathf.Abs(_ctx.MoveX) > MoveDeadzone;
            _ctx.Machine.ChangeState(moving ? _ctx.WalkState() : _ctx.IdleState());
        }

        public void FixedTick(float dt)
        {
            // Chỉ xử lý vx khi đang đứng đất.
            if (!_ctx.IsGrounded) return;

            // (P14) SPECIAL CÓ CHUYỂN ĐỘNG (vd command-dash/lao): uỷ quyền vx cho MoveMotionController.
            // Special KHÔNG khai báo motion hoặc prefab chưa gắn controller → GIỮ NGUYÊN VĂN ghì vx=0
            // (giữ vy cho gravity/landing) như cũ — cổng no-regression.
            if (_motion != null && _motion.OwnsGroundVelocity)
            {
                _motion.ApplyGroundVelocity();
                return;
            }

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
                _relay.CancelWindowOpenEvent -= OnCancelOpen;
                _relay.CancelWindowCloseEvent -= OnCancelClose;
                _relay.KnockbackPointEvent -= OnKnockbackPoint;
            }
        }

        // (P15) Cửa sổ motion cho follow-up có hướng = EdgeWindow×2 (đồng nhất SpecialController). CẦN TEST.
        private int MotionWindow => _ctx.EdgeWindow * 2;

        /// <summary>(P16) Animation Event KnockbackPoint (frame hit cuối): nếu KHÔNG nối → raise
        /// OnKnockbackLaunch(attackerRoot) cho victim văng pending. Đang nối → KHÔNG raise (combo tiếp).
        /// KHÔNG đếm/đoán "hit cuối" trong code — chỉ phản ứng Animation Event (§1.1).</summary>
        private void OnKnockbackPoint()
        {
            if (_onKnockbackLaunch == null || _attackerRoot == null) return;
            if (IsChaining()) return;
            _onKnockbackLaunch.RaiseEvent(_attackerRoot);
        }

        /// <summary>True khi đang trong cửa sổ nối + đã latch follow-up hợp lệ (sẽ chuyển sang mắt kế).</summary>
        private bool IsChaining()
        {
            if (!_latchedButton.HasValue) return false;
            MoveData next = MoveChainResolver.TryResolveLatched(
                _move, _latchedButton.Value, _ctx.Parser, MotionWindow, _ctx.Facing,
                _hitConfirm != null && _hitConfirm.HitSinceReset);
            return next != null;
        }

        // ===== Animation Event callbacks (per-instance relay) =====
        private void OnRecoveryEnd() => _recoveryDone = true;
        private void OnCancelOpen() => _canCancel = true;
        private void OnCancelClose() => _canCancel = false;
    }
}
