using UnityEngine;
using FA.Core;

namespace FA.Specials
{
    /// <summary>
    /// RAGE MODE (P10, lai SF4 Revenge) — BUFF TOÀN DIỆN CÓ THỜI HẠN (mode, KHÔNG phải "đứng yên": di
    /// chuyển/đánh bình thường, research §3.4). MonoBehaviour implement <see cref="IBuffSource"/> (P13 ĐỔI
    /// từ IBuffContext — CHỈ đổi tên interface, body GIỮ NGUYÊN: 6 thành viên cùng tên/kiểu). Listener Combat
    /// nay ĐỌC qua <see cref="BuffAggregator"/> (Core, implement IBuffContext) — gom Rage + DMG Buff item để
    /// stack được nhiều nguồn (research §4, Phương án B). Bản thân RageModeController giờ là 1 NGUỒN trong
    /// Aggregator, không còn là component consumer GetComponent&lt;IBuffContext&gt;() trực tiếp.
    /// Bật khi nộ = 100% (DefenseController gọi <see cref="ActivateRageMode"/> sau TrySpendRage).
    ///
    /// THỜI LƯỢNG = đếm GIÂY THỰC (NGOẠI LỆ hợp lệ của đếm-time: buff theo thời gian, KHÔNG phải frame-
    /// data clip — research §3.4). Hết → tắt buff + HOÀN 20–25% nộ (IRageSpender.RefundRage). Aura =
    /// Animator bool "Rage" (Animator LAYER phụ) HOẶC ParticleSystem — KHÔNG hardcode VFX (§1.1).
    ///
    /// ULTIMATE: trong Rage Mode bấm nút Rage → Ultimate (DefenseController vào UltimateState). Khi Ultimate
    /// active, timer Rage KHÔNG tick giảm (research §4 cạm bẫy 6 — khóa tới khi Ultimate xong). Ultimate
    /// đặt cờ qua <see cref="SetUltimateActive"/> (UltimateState gọi Enter/Exit). Đặt FA.Specials, ref Core.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RageModeController : MonoBehaviour, IBuffSource
    {
        [Header("Config")]
        [Tooltip("DefenseConfigSO — duration/refund/multiplier buff. BẮT BUỘC gán.")]
        [SerializeField] private DefenseConfigSO config;

        [Header("Aura (Unity-native — KHÔNG hardcode VFX, §1.1)")]
        [Tooltip("Animator để set bool 'Rage' (bật Animator LAYER aura). Bỏ trống = lấy children.")]
        [SerializeField] private Animator animator;

        [Tooltip("ParticleSystem aura (Play khi bật Rage, Stop khi hết). Optional — để trống nếu chỉ dùng Animator Layer.")]
        [SerializeField] private ParticleSystem auraParticles;

        [Tooltip("Tên bool Animator bật aura layer (khớp param trong AnimatorController). Mặc định 'Rage'.")]
        [SerializeField] private string rageAnimatorBool = "Rage";

        [Header("Hoàn nộ (qua IRageSpender — RageBar P8 implement)")]
        [Tooltip("Component RageBar (IRageSpender) để RefundRage khi hết Rage Mode. Bỏ trống = tìm children.")]
        [SerializeField] private MonoBehaviour rageSpenderBehaviour; // phải implement IRageSpender.

        private IRageSpender _rageSpender;
        private int _rageBoolHash;

        private bool _rageActive;
        private float _timeLeft;          // GIÂY còn lại (time-based — buff, hợp lệ).
        private bool _ultimateActive;     // Ultimate đang chạy → khóa tick timer (research §4.6).

        // ===== IBuffSource (P13; trước P10 là IBuffContext — body GIỮ NGUYÊN; không Rage → ×1.0 / false) =====
        public float DamageDealtMult => _rageActive && config != null ? config.rageDamageMult : 1f;
        public float KnockbackDealtMult => _rageActive && config != null ? config.rageKnockbackMult : 1f;
        public float HitstunTakenMult => _rageActive && config != null ? config.rageHitstunTakenMult : 1f;
        public float MoveSpeedMult => _rageActive && config != null ? config.rageSpeedMult : 1f;
        public bool IsRageActive => _rageActive;
        public bool IsUltimateActive => _ultimateActive;

        private void Awake()
        {
            if (config == null) Debug.LogError("[RageModeController] Thiếu DefenseConfigSO.", this);
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _rageSpender = rageSpenderBehaviour as IRageSpender;
            if (_rageSpender == null) _rageSpender = GetComponentInChildren<IRageSpender>();
            _rageBoolHash = Animator.StringToHash(rageAnimatorBool);
        }

        /// <summary>DefenseController gọi (sau TrySpendRage 100%) → bật buff + aura + bắt đầu đếm thời lượng.</summary>
        public void ActivateRageMode()
        {
            _rageActive = true;
            _timeLeft = config != null ? config.rageModeDuration : 9f;
            SetAura(true);
        }

        /// <summary>UltimateState Enter/Exit gọi → khóa/mở tick timer Rage (Ultimate không bị "hết giờ giữa chừng").</summary>
        public void SetUltimateActive(bool active) => _ultimateActive = active;

        private void Update()
        {
            if (!_rageActive) return;
            if (_ultimateActive) return; // Ultimate đang chạy → KHÔNG đếm (research §4.6).

            _timeLeft -= Time.deltaTime;
            if (_timeLeft > 0f) return;

            // Hết Rage Mode → tắt buff + aura + HOÀN một phần nộ (research §5, 20–25%).
            _rageActive = false;
            SetAura(false);
            float refund = config != null ? config.rageModeRefundFraction : 0.22f;
            _rageSpender?.RefundRage(refund);
        }

        private void SetAura(bool on)
        {
            if (animator != null) animator.SetBool(_rageBoolHash, on);
            if (auraParticles != null)
            {
                if (on) auraParticles.Play();
                else auraParticles.Stop();
            }
        }
    }
}
