using UnityEngine;
using FA.Core;

namespace FA.Specials
{
    /// <summary>
    /// ENTRY-POINT phòng thủ/comeback P10 (research §3.6) — MonoBehaviour SONG SONG SpecialController/
    /// CombatController (giữ 1-file-1-chức-năng §1.2). Mỗi Update đọc TỔ HỢP PHÍM qua Parser (ICombatContext,
    /// Core) → SPEND tài nguyên TRƯỚC ChangeState → ConsumePress (không leak sang special/normal).
    ///
    /// SCRIPT EXECUTION ORDER (BẮT BUỘC, research §3.6): DefenseController TRƯỚC SpecialController TRƯỚC
    /// CombatController. Khi defense ăn input nó ConsumePress(nút) → frame đó special/normal thấy edge đã
    /// tiêu thụ → KHÔNG kích. (Đặt trong Project Settings → Script Execution Order — xem Guide.)
    ///
    /// TỔ HỢP (quy ước — CẦN TEST, research §3.6/§4):
    ///  • Ground Dodge : GROUNDED + giữ Down + edge Super1 → GroundDodgeState (tiêu mana).
    ///  • Air Dodge    : !grounded + KHÔNG bị juggle (IsInHitstun==false) + giữ Down + edge Dash + còn
    ///                   quota → AirDodgeState (tiêu mana + quota). KHÔNG thoát juggle (chỉ Burst thoát).
    ///  • RC Dash      : đang ở state ĐÒN-CỦA-MÌNH (không neutral, không hurt) + giữ Down + edge Dash →
    ///                   RCDashState (tiêu mana, OVERRIDE state — cancel đòn đang ra).
    ///  • Burst        : nộ ≥ cost + đang BỊ ĐÁNH (state KHÔNG phải neutral) + (giữ Down/Back) + edge Rage
    ///                   + !RageActive → BurstState (tiêu 50% nộ flat). Cho phép OVERRIDE Hurt (gate riêng).
    ///  • Rage Mode    : nộ đầy 100% + neutral + edge Rage + !RageActive → RageModeState.
    ///  • Ultimate     : đang RageActive + edge Rage → UltimateState (data-driven MoveData).
    ///
    /// Phụ thuộc Core interface (ICombatContext/IManaSpender/IRageSpender) — KHÔNG class module khác (§1.3).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DefenseController : MonoBehaviour
    {
        private const float DownDeadzone = 0.5f;
        private const float BackDeadzone = 0.3f;

        [Header("Tham chiếu (cùng prefab)")]
        [Tooltip("AnimationEventRelay cùng object Animator — các state né subscribe RecoveryEnd/Hurtbox.")]
        [SerializeField] private AnimationEventRelay relay;

        [Tooltip("Config né/rage (DefenseConfigSO). BẮT BUỘC gán.")]
        [SerializeField] private DefenseConfigSO config;

        [Tooltip("RageModeController cùng prefab (IBuffContext + bật mode). BẮT BUỘC gán.")]
        [SerializeField] private RageModeController rageMode;

        [Tooltip("MoveData Ultimate (asset, role=BlockBreak, knockback lớn). BẮT BUỘC gán cho Ultimate.")]
        [SerializeField] private MoveData ultimateMove;

        [Tooltip("HitboxController cùng prefab (P5) — set MoveData cho hitbox Ultimate VÀ hitbox Burst-push. " +
                 "Bỏ trống nếu prefab chưa có (Ultimate/Burst-push sẽ không có hitbox).")]
        [SerializeField] private MonoBehaviour hitboxController; // gọi SetCurrentMove qua SendMessage.

        [Tooltip("(Tùy) MoveData cho hitbox ĐẨY của Burst (baseDamagePercent=0, knockback lớn). " +
                 "Bỏ trống = burst chỉ i-frame, KHÔNG đẩy địch.")]
        [SerializeField] private MoveData burstPushMove;

        // Lấy từ Core interface (CharacterMotor/ManaBar/RageBar implement) — KHÔNG class module khác.
        private ICombatContext _ctx;
        private IManaSpender _mana;
        private IRageSpender _rageSpender;
        private IKnockbackCanceler _selfKnockback;

        // (Archer) UBW install-mode CÙNG prefab: có component này thì Ultimate CHỈ dùng được KHI ĐANG
        // install (ấn nộ lần 2 trong UBW) — chặn kích mid-chant. Nhân vật thường: null → như cũ.
        private UBWInstallController _install;

        private GroundDodgeState _groundDodge;
        private AirDodgeState _airDodge;
        private RCDashState _rcDash;
        private BurstState _burst;
        private RageModeState _rageState;
        private UltimateState _ultimate;

        // Quota air-dodge: 1 lần/lần-trên-không (pattern air-dash P3). Reset khi grounded.
        private bool _airDodgeUsed;

        private void Awake()
        {
            _ctx = GetComponent<ICombatContext>();
            _mana = GetComponentInChildren<IManaSpender>();
            _rageSpender = GetComponentInChildren<IRageSpender>();
            _selfKnockback = GetComponent<IKnockbackCanceler>();
            if (relay == null) relay = GetComponentInChildren<AnimationEventRelay>();
            if (rageMode == null) rageMode = GetComponent<RageModeController>();

            if (_ctx == null)
            {
                Debug.LogError("[DefenseController] Thiếu ICombatContext (CharacterMotor).", this);
                enabled = false;
                return;
            }

            _groundDodge = new GroundDodgeState(_ctx, relay, config);
            _airDodge = new AirDodgeState(_ctx, relay);
            _rcDash = new RCDashState(_ctx, relay, config);
            _install = GetComponent<UBWInstallController>();
            _burst = new BurstState(_ctx, relay, _selfKnockback);
            _rageState = new RageModeState(_ctx, relay, rageMode);
            _ultimate = new UltimateState(_ctx, relay, rageMode, gameObject, _install);
        }

        /// <summary>State hiện tại là 1 trong các state CỦA controller này (rage pose/chant, dodge, RC,
        /// burst, ultimate)? SpecialController tra để KHÔNG cho super cắt ngang (fix kẹt flash: super
        /// kích giữa RageModeState kéo Animator khỏi clip chant → ChantEnd không bao giờ bắn).</summary>
        public bool OwnsCurrentState
        {
            get
            {
                if (_ctx == null) return false;
                IState s = _ctx.Machine.CurrentState;
                return ReferenceEquals(s, _groundDodge) || ReferenceEquals(s, _airDodge)
                    || ReferenceEquals(s, _rcDash) || ReferenceEquals(s, _burst)
                    || ReferenceEquals(s, _rageState) || ReferenceEquals(s, _ultimate);
            }
        }

        private void Update()
        {
            if (_ctx == null || _ctx.Parser == null) return;
            if (CinematicGate.IsActive) return;   // đang chant/flash — không kích gì giữa cinematic.

            if (_ctx.IsGrounded) _airDodgeUsed = false; // nạp lại quota air-dodge khi chạm đất.

            // THỨ TỰ ƯU TIÊN: Rage-button (Ultimate > Rage Mode) trước, rồi né (RC > AirDodge > GroundDodge).
            // BURST BREAKER ĐÃ BỎ (quyết định người dùng 2026-07-22): ấn Rage khi đang bị combo/block
            // KHÔNG còn kích Burst. Trước đây TryBurst nuốt nút Rage ở state non-neutral + trừ 50% nộ rồi
            // vào BurstState — mà clip Burst của nhân vật WIP chưa có Animation Event RecoveryEnd nên
            // BurstState kẹt (_recoveryDone mãi false) → đơ nuốt input. Gỡ entry-point là hết. (BurstState/
            // TryBurst giữ nguyên trong file, KHÔNG gọi tới — muốn khôi phục chỉ cần gọi lại TryBurst ở đây.)
            if (TryUltimate()) return;
            if (TryRageMode()) return;
            if (TryRCDash()) return;
            if (TryAirDodge()) return;
            if (TryGroundDodge()) return;
        }

        // ===================== RAGE BUTTON =====================

        /// <summary>Đang RageActive + edge Rage → Ultimate (data-driven). Nút Rage trong Rage = Ultimate (không Burst).</summary>
        private bool TryUltimate()
        {
            if (rageMode == null || !rageMode.IsRageActive) return false;
            if (ultimateMove == null) return false;
            // Có UBW install-mode (Archer): chỉ ultimate KHI ĐANG install (nộ chưa drain hết) —
            // chặn kích giữa chant. Và chỉ từ neutral (không phải công cụ thoát combo — Burst mới là).
            if (_install != null && !_install.IsInstalled) return false;
            if (!IsNeutralState()) return false;
            if (!Edge(InputButton.Rage)) return false;

            // Ultimate cần MoveData cho hitbox (block-break + knockback). Set TRƯỚC ChangeState (HitboxOn sau).
            if (hitboxController != null)
                hitboxController.SendMessage("SetCurrentMove", ultimateMove, SendMessageOptions.DontRequireReceiver);

            Consume(InputButton.Rage);
            _ultimate.SetMove(ultimateMove);
            _ctx.Machine.ChangeState(_ultimate);
            return true;
        }

        /// <summary>Nộ ≥ cost + đang BỊ ĐÁNH (state KHÔNG neutral) + giữ Down/Back + edge Rage + !Rage → Burst.</summary>
        private bool TryBurst()
        {
            if (rageMode != null && rageMode.IsRageActive) return false; // không burst trong Rage Mode (GDD).
            if (_rageSpender == null || config == null) return false;
            if (!Edge(InputButton.Rage)) return false;
            if (IsNeutralState()) return false;              // burst dùng KHI ĐANG BỊ ĐÁNH (override Hurt).
            if (!HoldingDownOrBack()) return false;

            // Nộ TRỪ TRƯỚC ChangeState (research §4.3). Thiếu → không consume edge/animation.
            if (!_rageSpender.TrySpendRage(config.burstRageCost)) return false;

            // Burst-push hitbox (đẩy địch) nếu có MoveData (baseDamagePercent=0). Tái dùng hệ hit P5/P7.
            if (hitboxController != null && burstPushMove != null)
                hitboxController.SendMessage("SetCurrentMove", burstPushMove, SendMessageOptions.DontRequireReceiver);

            Consume(InputButton.Rage);
            _ctx.Machine.ChangeState(_burst);
            return true;
        }

        /// <summary>Nộ đầy 100% + neutral + edge Rage + !Rage → Rage Mode (tiêu 100%).</summary>
        private bool TryRageMode()
        {
            if (rageMode == null || rageMode.IsRageActive) return false;
            if (_rageSpender == null || config == null) return false;
            if (!_rageSpender.IsFull) return false;
            if (!IsNeutralState()) return false;
            if (!Edge(InputButton.Rage)) return false;

            if (!_rageSpender.TrySpendRage(config.rageModeRageCost)) return false;

            Consume(InputButton.Rage);
            _ctx.Machine.ChangeState(_rageState);
            return true;
        }

        // ===================== NÉ (Dash/Super combos) =====================

        /// <summary>Đang ở state ĐÒN-CỦA-MÌNH (không neutral, không hurt-air) + giữ Down + edge Dash → RC Dash
        /// (override cancel). Phân biệt attack vs hurt KHÔNG ref Combat → quy ước: chỉ RC khi KHÔNG neutral
        /// VÀ ĐANG GROUNDED (đòn mặt đất đang ra). CẦN TEST tổ hợp (research §3.6).</summary>
        private bool TryRCDash()
        {
            if (config == null) return false;
            if (IsNeutralState()) return false;     // RC để CANCEL đòn đang ra — không dùng ở neutral.
            if (!_ctx.IsGrounded) return false;     // hạn chế RC ở mặt đất (tránh nhầm hurt-air). CẦN TEST.
            if (!HoldingDown()) return false;
            if (!Edge(InputButton.Dash)) return false;

            if (_mana == null || !_mana.TrySpend(config.rcDashManaCost)) return false;

            Consume(InputButton.Dash);
            _ctx.Machine.ChangeState(_rcDash);      // OVERRIDE mọi state đòn (state cũ Exit tự dọn relay).
            return true;
        }

        /// <summary>!grounded + giữ Down + edge Dash + còn quota → Air Dodge (tiêu mana + 1 quota/lần-trên-không).</summary>
        private bool TryAirDodge()
        {
            if (config == null || _ctx.IsGrounded || _airDodgeUsed) return false;
            if (!IsAirNeutralState()) return false; // chỉ né từ airborne-neutral (không cắt hurt-air).
            if (!HoldingDown()) return false;
            if (!Edge(InputButton.Dash)) return false;

            if (_mana == null || !_mana.TrySpend(config.airDodgeManaCost)) return false;

            _airDodgeUsed = true;
            Consume(InputButton.Dash);
            _ctx.Machine.ChangeState(_airDodge);
            return true;
        }

        /// <summary>GROUNDED + neutral + giữ Down + edge Super1 → Ground Dodge (tiêu mana). KHÔNG cooldown (GDD).</summary>
        private bool TryGroundDodge()
        {
            if (config == null || !_ctx.IsGrounded) return false;
            if (!IsNeutralState()) return false;
            if (!HoldingDown()) return false;
            if (!Edge(InputButton.Super1)) return false;

            if (_mana == null || !_mana.TrySpend(config.groundDodgeManaCost)) return false;

            Consume(InputButton.Super1);
            _ctx.Machine.ChangeState(_groundDodge);
            return true;
        }

        // ===================== Helpers =====================

        private bool Edge(InputButton b) => _ctx.Parser.WasPressedWithin(b, _ctx.EdgeWindow);
        private void Consume(InputButton b) => _ctx.Parser.ConsumePress(b, _ctx.EdgeWindow);

        private bool HoldingDown() => _ctx.MoveY < -DownDeadzone;
        private bool HoldingDownOrBack()
        {
            if (HoldingDown()) return true;
            // Back = ép ngược hướng nhìn.
            return Mathf.Abs(_ctx.MoveX) > BackDeadzone &&
                   (int)Mathf.Sign(_ctx.MoveX) == -_ctx.Facing;
        }

        /// <summary>Neutral mặt đất = đang Idle/Walk (state CỦA Motor). Dùng để phân biệt Rage Mode (neutral)
        /// vs Burst (đang bị đánh — non-neutral).</summary>
        private bool IsNeutralState()
        {
            IState s = _ctx.Machine.CurrentState;
            return ReferenceEquals(s, _ctx.IdleState()) || ReferenceEquals(s, _ctx.WalkState());
        }

        /// <summary>Air-neutral = trên không, KHÔNG đang bị-đánh/juggle, và KHÔNG ở state né.
        /// QUYẾT ĐỊNH NGƯỜI DÙNG (đã chốt): Air Dodge KHÔNG được dùng để thoát juggle — đang bị
        /// hitstun/tumble/wall-splat/ground-bounce (<see cref="ICombatContext.IsInHitstun"/>) thì Air
        /// Dodge bị CHẶN; chỉ Burst Breaker (tốn 50% nộ) mới thoát combo. Cờ IsInHitstun do hurt-state
        /// FA.Combat set (Core interface) → KHÔNG ref cross-module §1.3.</summary>
        private bool IsAirNeutralState()
        {
            if (_ctx.IsInHitstun) return false; // đang bị juggle → cấm Air Dodge (chỉ Burst thoát).
            // Không tham chiếu air-state Movement (cross-module §1.3) → dùng !grounded + chưa ở né.
            IState s = _ctx.Machine.CurrentState;
            return !_ctx.IsGrounded
                && !ReferenceEquals(s, _airDodge)
                && !ReferenceEquals(s, _rcDash);
        }
    }
}
