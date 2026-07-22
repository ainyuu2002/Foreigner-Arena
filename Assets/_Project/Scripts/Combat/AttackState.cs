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
    /// (P15) NỐI ĐÒN (Combo Chain) — INPUT-LATCH: tách "nhận input" khỏi "thực thi nối" để bắt cú ấn
    /// follow-up SỚM (vd ấn J2 ngay frame 2 của đòn 30 frame). MỖI Tick TỪ ĐẦU đòn gọi
    /// MoveChainResolver.TryLatch → nếu vừa ấn nút có trong followUps thì NHỚ (latch) cú đó (đã consume
    /// edge). KHI cancel window mở (CancelWindowOpen/Close) mới TryResolveLatched với latch + didHit hiện
    /// thời → có nextMove thì <see cref="BeginMove"/>(đòn kế) NGAY trong CÙNG state instance, KHÔNG
    /// ChangeState (self-transition bị StateMachine bỏ qua → Enter không chạy lại). Đòn KHÔNG có followUps
    /// → TryLatch trả false ngay → KHÔNG latch → chạy hết recovery, một-nhấn-một-đòn như P5 (no-regression).
    /// Đặt ở FA.Combat, dùng ICombatContext (Core) — KHÔNG reference FA.Movement (§1.3).
    /// </summary>
    public sealed class AttackState : IState
    {
        private const float MoveDeadzone = 0.1f;

        private readonly ICombatContext _ctx;
        private readonly AnimationEventRelay _relay;
        // (P14) Uỷ quyền vx khi đòn có chuyển động. Null ở prefab cũ → giữ hành vi đứng yên (no-regression).
        private readonly MoveMotionController _motion;
        // (P15) HitboxController cùng prefab — re-point MoveData mỗi mắt nối (mỗi đòn dmg/kb khác).
        private readonly HitboxController _hitbox;
        // (P15) Theo dõi "đòn hiện hành đã trúng thật chưa" cho follow-up điều kiện OnHit. Null → coi như chưa trúng.
        private readonly HitConfirmTracker _hitConfirm;
        // (P16) Kênh báo "VĂNG knockback" — raise tại Animation Event KnockbackPoint (frame hit cuối) KHI
        // KHÔNG đang nối. Null → đòn KHÔNG văng (chỉ spike/launcher văng). Mang attacker root.
        private readonly GameObjectEventChannelSO _onKnockbackLaunch;
        // (P16) Root attacker truyền vào payload OnKnockbackLaunch (so với HitData.Attacker bên victim).
        private readonly GameObject _attackerRoot;

        // MoveData đòn HIỆN HÀNH. CombatController set _pendingMove qua SetMove TRƯỚC khi ChangeState;
        // Enter() gọi BeginMove(_pendingMove). Mắt nối kế set qua BeginMove trực tiếp (không ChangeState).
        private MoveData _pendingMove;
        private MoveData _move;

        // Cờ điều khiển flow — đặt lại mỗi BeginMove (KHÔNG phải bộ đếm frame).
        private bool _recoveryDone;
        private bool _canCancel;   // cửa sổ cancel mở/đóng theo Animation Event (P15 tiêu thụ để nối).

        // (P15 latch) Nút follow-up đã bắt được SỚM (intent), chờ tới khi cancel window mở mới thực thi.
        // null = chưa có intent. Ghi đè bằng cú mới nhất. Reset mỗi BeginMove (mắt mới bắt đầu sạch).
        private InputButton? _latchedButton;

        public AttackState(ICombatContext ctx, AnimationEventRelay relay,
                           MoveMotionController motion = null,
                           HitboxController hitbox = null, HitConfirmTracker hitConfirm = null,
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

        /// <summary>CombatController set đòn sẽ ra TRƯỚC khi ChangeState. Enter() dùng làm đòn đầu chuỗi.</summary>
        public void SetMove(MoveData move) => _pendingMove = move;

        /// <summary>HOOK: cửa sổ cancel đang mở không (combo).</summary>
        public bool CanCancel => _canCancel;

        public void Enter()
        {
            // Subscribe relay per-instance (đúng nhân vật này). Unsubscribe ở Exit (tránh leak/double-fire).
            if (_relay != null)
            {
                _relay.RecoveryEndEvent += OnRecoveryEnd;
                _relay.CancelWindowOpenEvent += OnCancelOpen;
                _relay.CancelWindowCloseEvent += OnCancelClose;
                _relay.KnockbackPointEvent += OnKnockbackPoint;
                _relay.ChargeCheckEvent += OnChargeCheck;
            }
            BeginMove(_pendingMove);
        }

        /// <summary>(P15) GOM toàn bộ init PER-MOVE — gọi cả ở Enter (đòn đầu) lẫn khi NỐI (đòn kế) trong
        /// cùng state. Reset cờ flow + set StateId (cầu nối code↔Animator §3.1) + re-point MoveData cho
        /// hitbox (mỗi mắt dmg/kb khác) + đẩy MoveData xuống MoveMotionController (P14 motion mỗi mắt) +
        /// reset cờ hit-confirm (mắt nối kế không thừa hưởng hit mắt trước).</summary>
        private void BeginMove(MoveData m)
        {
            _move = m;
            _recoveryDone = false;
            _canCancel = false;
            _latchedButton = null;   // (P15 latch) mắt mới bắt đầu sạch — không bleed intent mắt trước.

            if (_move != null)
            {
                Animator anim = _ctx.Animator;
                // FIX kẹt spam-cùng-đòn (bắt qua test H): đòn kế TRÙNG clip Animator đang giữ — vd ấn H
                // ngay frame đòn H trước vừa về Idle: StateId đi 10→0→10 trong CÙNG frame nên Animator
                // (evaluate cuối frame) không thấy giá trị đổi, transition Any State lại tắt
                // canTransitionToSelf → clip KHÔNG restart, đứng im frame cuối → RecoveryEnd không bao
                // giờ bắn → kẹt AttackState. Phát hiện bằng "clip hiện hành == clip đòn mới" → ép Play
                // lại state đó từ 0 (restart clip, Animation Event bắn lại từ đầu).
                bool restart = false;
                if (_move.clip != null)
                {
                    var cur = anim.GetCurrentAnimatorClipInfo(0);
                    restart = cur.Length > 0 && ReferenceEquals(cur[0].clip, _move.clip);
                }
                anim.SetInteger(CoreAnimatorParams.StateId, _move.animatorStateId);
                if (restart)
                    anim.Play(anim.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, 0f);
            }

            if (_hitbox != null) _hitbox.SetCurrentMove(_move);
            if (_motion != null) _motion.SetMove(_move);
            if (_hitConfirm != null) _hitConfirm.BeginWindow();
        }

        public void Tick(float dt)
        {
            // (P15 latch) NHẬN INPUT SỚM: mỗi Tick từ ĐẦU đòn, bắt cú ấn follow-up (ghi đè cú mới nhất).
            // Tách khỏi thực thi → ấn rất sớm (trước khi window mở) vẫn được nhớ, không rớt theo edgeWindow.
            if (MoveChainResolver.TryLatch(_move, _ctx.Parser, _ctx.EdgeWindow, out InputButton latched))
                _latchedButton = latched;

            // (P15) THỰC THI NỐI: chỉ khi cancel window MỞ + có intent đã latch. Re-check điều kiện OnHit
            // (đọc HitSinceReset TẠI ĐÂY) → ấn sớm + đòn trúng trong window vẫn nối; chưa thoả thì GIỮ latch
            // chờ hit (hit có thể tới sau trong cùng window — KHÔNG clear). KHÔNG ChangeState (self-skip).
            if (_canCancel && _latchedButton.HasValue)
            {
                MoveData next = MoveChainResolver.TryResolveLatched(
                    _move, _latchedButton.Value, _ctx.Parser, MotionWindow, _ctx.Facing,
                    _hitConfirm != null && _hitConfirm.HitSinceReset);
                if (next != null) { BeginMove(next); return; }
            }

            // KHÓA input: KHÔNG chuyển state cho tới khi Animation Event RecoveryEnd bắn (KHÔNG timer).
            if (!_recoveryDone) return;

            // Hết recovery → về Idle/Walk theo ý định di chuyển (đứng đất). (Aerial/landing-từ-đòn = P7.)
            bool moving = Mathf.Abs(_ctx.MoveX) > MoveDeadzone;
            _ctx.Machine.ChangeState(moving ? _ctx.WalkState() : _ctx.IdleState());
        }

        public void FixedTick(float dt)
        {
            // Chỉ xử lý vx khi đang đứng đất (aerial = P7; trên không không ghì).
            if (!_ctx.IsGrounded) return;

            // (P14) ĐÒN CÓ CHUYỂN ĐỘNG: uỷ quyền vx cho MoveMotionController (impulse trôi / drift sustained).
            // Đòn KHÔNG khai báo motion (OwnsGroundVelocity=false) hoặc prefab chưa gắn controller →
            // GIỮ NGUYÊN VĂN hành vi cũ: ghì vx=0 (giữ vy cho gravity/landing). Đây là cổng no-regression.
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
                _relay.ChargeCheckEvent -= OnChargeCheck;
            }
        }

        // (P15) Cửa sổ motion cho follow-up có hướng = EdgeWindow×2 (đồng nhất SpecialController). CẦN TEST.
        private int MotionWindow => _ctx.EdgeWindow * 2;

        /// <summary>(P16) Animation Event KnockbackPoint (frame hit CUỐI của đòn): quyết VĂNG hay không.
        /// Nếu đang NỐI (TryResolveLatched ra nextMove hợp lệ) → KHÔNG raise (combo tiếp, đòn này không
        /// văng). Nếu KHÔNG nối → raise OnKnockbackLaunch(attackerRoot) → victim áp pending (hit gần nhất)
        /// tại hitstop+1. KHÔNG đếm/đoán "hit cuối" trong code — chỉ phản ứng Animation Event (§1.1).</summary>
        private void OnKnockbackPoint()
        {
            if (_onKnockbackLaunch == null || _attackerRoot == null) return;

            // Đang nối? (cùng điều kiện thực thi nối ở Tick — latch hợp lệ trong cancel window). Nối → đòn
            // này KHÔNG văng (mắt kế tiếp tục). KHÔNG nối → văng.
            if (IsChaining()) return;
            _onKnockbackLaunch.RaiseEvent(_attackerRoot);
        }

        /// <summary>True khi đang trong cửa sổ nối + đã latch follow-up hợp lệ (sẽ chuyển sang mắt kế). Dùng
        /// để KnockbackPoint biết "đòn này nối tiếp → không văng".</summary>
        private bool IsChaining()
        {
            if (!_latchedButton.HasValue) return false;
            MoveData next = MoveChainResolver.TryResolveLatched(
                _move, _latchedButton.Value, _ctx.Parser, MotionWindow, _ctx.Facing,
                _hitConfirm != null && _hitConfirm.HitSinceReset);
            return next != null;
        }

        /// <summary>Animation Event ChargeCheck (frame "ngắm" của đòn giữ-để-charge): còn GIỮ nút kích đòn
        /// đủ ≥ chargeFrames → swap sang biến thể charged (BeginMove — cùng cơ chế nối P15, KHÔNG
        /// ChangeState). Đòn không khai chargedVariant → bỏ qua (no-regression). Timing nằm TRÊN CLIP
        /// (§1.1); trần đo giữ = kích thước InputBuffer (~10f) nên chargeFrames nên ≤ 8.</summary>
        private void OnChargeCheck()
        {
            if (_move == null || _move.chargedVariant == null || _ctx.Parser == null) return;
            if (_ctx.Parser.HeldFrames(_move.button) >= Mathf.Max(1, _move.chargeFrames))
                BeginMove(_move.chargedVariant);
        }

        // ===== Animation Event callbacks (per-instance relay) =====
        private void OnRecoveryEnd() => _recoveryDone = true;
        private void OnCancelOpen() => _canCancel = true;

        /// <summary>Đóng cửa sổ nối + LATCH RỚT (spec P15: "quá window không nối thì latch rớt — ấn lại =
        /// đòn 1 từ neutral"). Bắt buộc khi KnockbackPoint đặt SAU CancelWindowClose (fix vùng-chết nối
        /// đòn): không xả latch thì cú ấn quá-muộn làm IsChaining()=true tại KnockbackPoint → vừa KHÔNG
        /// nối (window đóng) vừa KHÔNG văng (KP bị nuốt) = mất knockback.</summary>
        private void OnCancelClose()
        {
            _canCancel = false;
            _latchedButton = null;
        }
    }
}
