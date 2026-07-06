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
    /// (P15) NỐI ĐÒN — INPUT-LATCH: THÊM subscribe CancelWindowOpen/Close + cơ chế BeginMove/chain Y NHƯ
    /// AttackState → super CŨNG có thể có followUps (vd super → super-finisher). Mỗi Tick TryLatch bắt
    /// intent sớm; THỰC THI nối CHỈ khi cancel window mở + ĐANG ĐẤT (trên không bỏ qua nhánh nối để giữ
    /// logic Fall). Super thường đơn lẻ → followUps rỗng → TryLatch trả false ngay → KHÔNG latch → chạy hết
    /// recovery như P9 (no-regression).
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
        // (P14) Hook TUỲ CHỌN: super hiếm khi cần motion (đa số tại-chỗ/cinematic) và KHÔNG ghì vx (air-usable)
        // nên việc uỷ quyền giá trị thấp. Vẫn truyền controller để impulse/drift HOẠT ĐỘNG nếu super khai báo.
        private readonly MoveMotionController _motion;
        // (P15) Re-point MoveData hitbox mỗi mắt nối — qua SendMessage (HitboxController ở FA.Combat, KHÔNG ref).
        private readonly MonoBehaviour _hitbox;
        // (P15) Theo dõi hit-confirm cho follow-up điều kiện OnHit. Null → coi như chưa trúng.
        private readonly HitConfirmTracker _hitConfirm;
        // (P16) Kênh "VĂNG knockback" — raise tại Animation Event KnockbackPoint khi KHÔNG nối. Null → không văng.
        private readonly GameObjectEventChannelSO _onKnockbackLaunch;
        private readonly GameObject _attackerRoot;

        private MoveData _pendingMove;
        private MoveData _move;
        private bool _recoveryDone;
        private bool _canCancel;   // (P15) cửa sổ cancel mở/đóng theo Animation Event (chỉ nối khi đứng đất).

        // (P15 latch) Nút follow-up đã bắt được SỚM (intent), chờ cancel window mở + đứng đất mới thực thi.
        private InputButton? _latchedButton;

        public SuperState(ICombatContext ctx, AnimationEventRelay relay,
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

        /// <summary>SpecialController set super sẽ ra TRƯỚC khi ChangeState. Enter() dùng làm đòn đầu chuỗi.</summary>
        public void SetMove(MoveData move) => _pendingMove = move;

        public void Enter()
        {
            if (_relay != null)
            {
                _relay.RecoveryEndEvent += OnRecoveryEnd;
                _relay.CancelWindowOpenEvent += OnCancelOpen;
                _relay.CancelWindowCloseEvent += OnCancelClose;
                _relay.KnockbackPointEvent += OnKnockbackPoint;
            }
            BeginMove(_pendingMove);
        }

        /// <summary>(P15) GOM init PER-MOVE — gọi cả ở Enter (đòn đầu) lẫn khi NỐI (đòn kế).</summary>
        private void BeginMove(MoveData m)
        {
            _move = m;
            _recoveryDone = false;
            _canCancel = false;
            _latchedButton = null;   // (P15 latch) mắt mới bắt đầu sạch — không bleed intent mắt trước.

            if (_move != null)
                _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, _move.animatorStateId);

            if (_hitbox != null)
                _hitbox.SendMessage("SetCurrentMove", _move, SendMessageOptions.DontRequireReceiver);
            if (_motion != null) _motion.SetMove(_move);
            if (_hitConfirm != null) _hitConfirm.BeginWindow();
        }

        public void Tick(float dt)
        {
            // (P15 latch) NHẬN INPUT SỚM: mỗi Tick từ ĐẦU đòn, bắt cú ấn follow-up (ghi đè cú mới nhất).
            // Latch kể cả khi đang trên không là vô hại — chỉ THỰC THI khi window mở + đứng đất.
            if (MoveChainResolver.TryLatch(_move, _ctx.Parser, _ctx.EdgeWindow, out InputButton latched))
                _latchedButton = latched;

            // (P15) THỰC THI NỐI — CHỈ khi cancel window MỞ + ĐANG ĐẤT (trên không giữ logic Fall bên dưới)
            // + có intent. Re-check OnHit tại đây; chưa thoả thì GIỮ latch chờ hit (KHÔNG clear). Re-init tại chỗ.
            if (_canCancel && _ctx.IsGrounded && _latchedButton.HasValue)
            {
                MoveData next = MoveChainResolver.TryResolveLatched(
                    _move, _latchedButton.Value, _ctx.Parser, MotionWindow, _ctx.Facing,
                    _hitConfirm != null && _hitConfirm.HitSinceReset);
                if (next != null) { BeginMove(next); return; }
            }

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
            _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, FallStateId);
            _ctx.Machine.ChangeState(_ctx.IdleState());
        }

        public void FixedTick(float dt)
        {
            // KHÔNG ghì vx (super air-usable — cho phép giữ momentum trên không). Gravity do Rigidbody2D.
            // (P14) DUY NHẤT trường hợp can thiệp: super CÓ drift (OwnsGroundVelocity) ĐANG ĐỨNG ĐẤT →
            // gán vx=facing×driftSpeed khi cửa sổ drift mở (ApplyGroundVelocity tự no-op nếu drift đóng).
            // Super không-motion ⇒ OwnsGroundVelocity=false ⇒ KHÔNG đụng vx (giữ nguyên hành vi cũ).
            if (_motion != null && _motion.OwnsGroundVelocity && _ctx.IsGrounded)
                _motion.ApplyGroundVelocity();
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

        /// <summary>True khi đang nối hợp lệ. Như Tick: nối CHỈ xét khi ĐANG ĐẤT (super trên không giữ
        /// logic Fall, không nối) — nên trên không luôn coi là KHÔNG nối → văng bình thường.</summary>
        private bool IsChaining()
        {
            if (!_ctx.IsGrounded || !_latchedButton.HasValue) return false;
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
