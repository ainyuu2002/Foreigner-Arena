using UnityEngine;
using UnityEngine.Serialization;
using FA.Core;

namespace FA.Specials
{
    /// <summary>
    /// ENTRY-POINT special/super (research §3.3) — MonoBehaviour SONG SONG <c>CombatController</c> (P5),
    /// TÁCH KHỎI CharacterMotor để FA.Specials KHÔNG reference FA.Movement/FA.Combat (§1.3). Mỗi Update
    /// hỏi parser (qua ICombatContext ở Core): motion vừa hoàn tất + nút edge → tra <see cref="MoveSetData"/>
    /// → kiểm mana (IManaSpender) → ConsumePress (special "thắng" normal) → ChangeState(Special/Super).
    ///
    /// THỨ TỰ ƯU TIÊN vs CombatController (research §3.3 cách A — KHUYẾN NGHỊ): đặt Script Execution Order
    /// của SpecialController TRƯỚC CombatController. Khi special ăn input nó ConsumePress(nút) → frame đó
    /// CombatController thấy edge đã tiêu thụ → KHÔNG ra normal. Dùng đúng cơ chế consume P4, KHÔNG cần 2
    /// module nói chuyện trực tiếp.
    ///
    /// GATE: special motion-input chỉ khi GROUNDED + ground-neutral (Idle/Walk — giống P5). Super dùng
    /// ĐƯỢC CẢ TRÊN KHÔNG (GDD §4.2) → bỏ gate grounded cho super, chỉ chặn khi đang ở state combat lạ
    /// (đang đánh/hurt — tránh hủy hitstun). Mana TRỪ TRƯỚC ChangeState (research §4.3).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpecialController : MonoBehaviour
    {
        [Header("Tham chiếu (cùng prefab)")]
        [Tooltip("AnimationEventRelay cùng object Animator — SpecialState/SuperState subscribe RecoveryEnd.")]
        [SerializeField] private AnimationEventRelay relay;

        [Tooltip("Bảng special/super (MoveSetData, Core). Thêm đòn = thêm ENTRY asset (extensible).")]
        [SerializeField] private MoveSetData moveSet;

        [Tooltip("(P16) OnKnockbackLaunch (GameObjectEventChannelSO) — Special/SuperState RAISE tại Animation " +
                 "Event KnockbackPoint (frame hit cuối) khi KHÔNG nối → victim VĂNG pending. CÙNG asset gán " +
                 "vào KnockbackResolver (listen) & CombatController. Bỏ trống = đòn KHÔNG văng (chỉ spike/launcher).")]
        [FormerlySerializedAs("onComboContinues")]
        [SerializeField] private GameObjectEventChannelSO onKnockbackLaunch;

        [Tooltip("HitboxController cùng prefab (P5) — set MoveData hiện hành cho special CÓ HITBOX TRỰC " +
                 "TIẾP (reversal/anti-air). Projectile KHÔNG dùng cái này (ProjectileSpawner gán riêng). " +
                 "Bỏ trống nếu mọi special đều là projectile.")]
        [SerializeField] private MonoBehaviour hitboxController; // optional, gọi qua reflection-free SendMessage

        // ICombatContext + IManaSpender lấy từ cùng prefab (CharacterMotor / ManaBar implement). Phụ
        // thuộc Core interface, KHÔNG class module khác (§1.3).
        private ICombatContext _ctx;
        private IManaSpender _mana;
        private SpecialState _specialState;
        private SuperState _superState;
        // DefenseController CÙNG module (ref trực tiếp hợp lệ §1.3) — chặn super cắt ngang state phòng
        // thủ (rage pose/chant, dodge, burst, ultimate). Null ở prefab chưa có P10 → như cũ.
        private DefenseController _defense;

        private void Awake()
        {
            _ctx = GetComponent<ICombatContext>();
            _mana = GetComponentInChildren<IManaSpender>(); // ManaBar có thể ở object con
            if (relay == null) relay = GetComponentInChildren<AnimationEventRelay>();

            if (_ctx == null)
            {
                Debug.LogError("[SpecialController] Thiếu ICombatContext (CharacterMotor) trên prefab.", this);
                enabled = false;
                return;
            }
            // (P14) MoveMotionController cùng prefab (Core) — uỷ quyền vx cho special/super CÓ chuyển động.
            // Null ở prefab cũ → các state giữ hành vi cũ (no-regression).
            var motion = GetComponent<MoveMotionController>();
            // (P15) HitConfirmTracker cùng prefab (Core) — follow-up điều kiện OnHit. Null → coi như chưa trúng.
            // hitboxController truyền vào state để BeginMove re-point MoveData mỗi mắt nối (qua SendMessage).
            var hitConfirm = GetComponent<HitConfirmTracker>();
            _defense = GetComponent<DefenseController>();
            // (P16) Root attacker → raise OnKnockbackLaunch (so HitData.Attacker bên victim).
            GameObject attackerRoot = transform.root.gameObject;
            _specialState = new SpecialState(_ctx, relay, motion, hitboxController, hitConfirm,
                                             onKnockbackLaunch, attackerRoot);
            _superState = new SuperState(_ctx, relay, motion, hitboxController, hitConfirm,
                                         onKnockbackLaunch, attackerRoot);
        }

        // ===== Cờ trạng thái (FacingController/khác gate auto-face nếu cần — đối xứng CombatController) =====
        public bool IsSpecial => ReferenceEquals(_ctx?.Machine.CurrentState, _specialState);
        public bool IsSuper => ReferenceEquals(_ctx?.Machine.CurrentState, _superState);

        private void Update()
        {
            if (_ctx == null || _ctx.Parser == null || moveSet == null) return;
            if (CinematicGate.IsActive) return;   // đang chant/flash — không kích gì giữa cinematic.

            // 1) SUPER trước (nút riêng, air-usable). Quét entry có category=Super.
            if (TrySuper()) return;

            // 2) SPECIAL motion-input (ground-only ở P9). Chỉ khi đang ground-neutral.
            if (!CanSpecialNow()) return;
            TrySpecial();
        }

        /// <summary>Super: nút Super1/Super2 edge + đủ mana → vào SuperState (cho cả trên không).</summary>
        private bool TrySuper()
        {
            // Đang ở state combat LẠ (không phải state Movement của Motor)? Cho phép — Motor chặn riêng;
            // nhưng tránh kích super khi đang special/super rồi (đang khóa input đến RecoveryEnd).
            if (IsSpecial || IsSuper) return false;
            // KHÔNG cho super cắt ngang state phòng thủ P10 (fix kẹt flash: super giữa RageModeState kéo
            // Animator khỏi clip chant UBW → ChantEnd/UltimateInstall không bắn → freeze kẹt vĩnh viễn).
            if (_defense != null && _defense.OwnsCurrentState) return false;
            // KHÔNG cho super kích giữa hitstun/juggle (HurtState/Launcher/WallSplat/GroundBounce, ICombatContext
            // IsInHitstun — cùng cờ DefenseController dùng chặn Air Dodge). Bug đơ vĩnh viễn: nếu kích trong
            // lúc HitstopController đang freeze Animator.speed=0, event SuperFlashStart ở frame 0 clip Super
            // vẫn bắn được (transition-enter không cần elapsed time) → StartFlash() set Time.timeScale=0 →
            // FixedUpdate (đồng hồ un-freeze của Hitstop) NGỪNG HẲN vì timeScale=0 → Animator.speed kẹt ở 0
            // vĩnh viễn → clip Super không bao giờ tới SuperFlashEnd → CinematicGate không bao giờ Release →
            // đơ trong flash vĩnh viễn (bug user bắt: dùng super lúc đang hurt/hitstun/hitstop).
            if (_ctx.IsInHitstun) return false;

            int win = _ctx.EdgeWindow;
            for (int i = 0; i < moveSet.Count; i++)
            {
                MoveSetData.Entry e = moveSet.Get(i);
                if (e.move == null || e.move.category != MoveCategory.Super) continue;
                if (e.button != InputButton.Super1 && e.button != InputButton.Super2) continue;
                if (!_ctx.Parser.WasPressedWithin(e.button, win)) continue;

                // Mana check TRƯỚC ChangeState. Thiếu → KHÔNG consume edge/animation (research §4.3).
                if (!SpendIfNeeded(e.move)) { return false; }

                _ctx.Parser.ConsumePress(e.button, win); // 1 nhấn = 1 super (research §4.10).
                _superState.SetMove(e.move);
                _ctx.Machine.ChangeState(_superState);
                return true;
            }
            return false;
        }

        /// <summary>Special: motion khớp + nút L/M/H edge + (đủ mana nếu cost) → vào SpecialState.</summary>
        private bool TrySpecial()
        {
            int win = _ctx.EdgeWindow;
            int facing = _ctx.Facing;

            for (int i = 0; i < moveSet.Count; i++)
            {
                MoveSetData.Entry e = moveSet.Get(i);
                if (e.move == null || e.move.category != MoveCategory.Special) continue;

                // CHARGE = cơ chế GIỮ→NHẢ (GDD §5 P9). Entry charge-able (có biến thể charged + chargeFrames>0)
                // KÍCH KHI NHẢ nút (release edge): đo số frame đã giữ NGAY TRƯỚC khi nhả → ≥ chargeFrames thì
                // ra biến thể charged, chưa đủ thì ra biến thể base. Entry thường (không charge) vẫn kích khi
                // NHẤN (press edge) như cũ. Tách rõ 2 entry-point ⇒ biến thể charged ĐẠT ĐƯỢC, không double-fire.
                bool chargeable = e.charged != null && e.move.chargeFrames > 0;

                MoveData chosen;
                bool releaseTrigger;
                if (chargeable)
                {
                    if (!_ctx.Parser.WasReleasedWithin(e.button, win))
                    {
                        // ĐANG GIỮ để charge (chưa nhả): nếu motion entry này ĐÃ KHỚP (charge thực sự
                        // active) thì nút đang giữ THUỘC về charged-special → CONSUME press edge ngay để
                        // CombatController (chạy sau) KHÔNG bắn normal trong lúc giữ. Vẫn KHÔNG kích đòn,
                        // chờ release edge ở frame sau. Yêu cầu motion != None để KHÔNG nuốt normal vô cớ
                        // (entry charge không-motion sẽ không suppress nút normal cùng tên).
                        if (e.motion != MotionInput.None &&
                            _ctx.Parser.HeldFrames(e.button) > 0 &&
                            _ctx.Parser.TryMatchMotion(e.motion, MotionWindow, facing))
                        {
                            _ctx.Parser.ConsumePress(e.button, win);
                        }
                        continue;
                    }
                    int held = _ctx.Parser.HeldFramesBeforeRelease(e.button, win);
                    chosen = held >= e.move.chargeFrames ? e.charged : e.move;
                    releaseTrigger = true;
                }
                else
                {
                    if (!_ctx.Parser.WasPressedWithin(e.button, win)) continue;
                    chosen = e.move;
                    releaseTrigger = false;
                }

                // Motion phải khớp trong cửa sổ motion (rộng hơn edge).
                if (e.motion != MotionInput.None &&
                    !_ctx.Parser.TryMatchMotion(e.motion, MotionWindow, facing)) continue;

                if (!SpendIfNeeded(chosen)) return false; // thiếu mana → không consume/whiff.

                // Tiêu thụ đúng loại edge đã kích (1 nhấn/nhả = 1 đòn; special "thắng" normal qua ConsumePress).
                if (releaseTrigger) _ctx.Parser.ConsumeRelease(e.button, win);
                else _ctx.Parser.ConsumePress(e.button, win);

                // (P15) Re-point MoveData hitbox đã CHUYỂN vào SpecialState.BeginMove (gọi SendMessage
                // "SetCurrentMove") để mỗi mắt nối tự cập nhật — KHÔNG cần gọi ở đây nữa. FA.Specials vẫn
                // KHÔNG ref FA.Combat (§1.3) vì BeginMove cũng dùng SendMessage. Projectile dùng
                // ProjectileSpawner riêng → DontRequireReceiver an toàn.
                _specialState.SetMove(chosen);
                _ctx.Machine.ChangeState(_specialState);
                return true;
            }
            return false;
        }

        /// <summary>Đủ mana? Trừ ngay nếu move có cost. move không cost (0 ô) → luôn true. Thiếu ô → false.</summary>
        private bool SpendIfNeeded(MoveData move)
        {
            int cost = move.manaCostUnits;
            if (cost <= 0) return true;
            if (_mana == null) return false; // có cost nhưng không có thanh mana → không cho ra.
            return _mana.TrySpend(cost);
        }

        /// <summary>Special motion chỉ khi GROUNDED & đang Idle/Walk (ground-neutral). Super không qua đây.</summary>
        private bool CanSpecialNow()
        {
            if (!_ctx.IsGrounded) return false;
            IState s = _ctx.Machine.CurrentState;
            return ReferenceEquals(s, _ctx.IdleState()) || ReferenceEquals(s, _ctx.WalkState());
        }

        // Cửa sổ motion: dùng EdgeWindow×2 tạm (motion cần nhiều frame hơn edge). InputConfigSO có
        // motionWindow nhưng ICombatContext chưa expose → nhân đôi edge cho gần đúng (CẦN TEST; chỉnh
        // bằng cách mở rộng ICombatContext sau nếu muốn đọc đúng InputConfigSO.motionWindow).
        private int MotionWindow => _ctx.EdgeWindow * 2;
    }
}
