using UnityEngine;
using FA.Core;

namespace FA.Resources
{
    /// <summary>
    /// THANH NỘ (P8, research §1/§3.2) — logic SỐ THUẦN trên nhân vật (đặt cạnh ManaBar/KnockbackPercent).
    /// Kiểu SF4 Revenge (lai): meter 0..RageMax, đầy 100% mở Rage Mode/Ultimate (P10 đọc <see cref="IsFull"/>).
    /// KHÔNG decay, GIỮ qua mạng. Đầy theo 3 NGUỒN khác hệ số:
    ///   • gây dmg trúng (attacker, Confirmed)  → +rageGainDealt × DamagePercent (NHIỀU).
    ///   • bị đánh (victim, Confirmed)          → +rageGainTaken × DamagePercent (ÍT → comeback).
    ///   • block (cả attacker-block & victim-block, Blocked) → nhân thêm rageBlockMultiplier (RẤT ÍT).
    ///
    /// Subscribe OnHitConfirmed + OnHitBlocked (CÙNG asset Combat raise). Phân vai bằng
    /// ReferenceEquals(hit.Attacker/Victim, me) — đúng pattern KnockbackPercent/ComboCounter. Raise
    /// OnRageChanged(fill 0..1) qua FloatEventChannelSO cho HUD. KHÔNG code UI (§1.3). KHÔNG Update
    /// (không decay). Đặt FA.Resources, chỉ ref Core. P12 RespawnSystem KHÔNG reset thanh này (giữ qua chết).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RageBar : MonoBehaviour, IRageSpender
    {
        [Header("Kênh sự kiện — NGUỒN (tái dùng asset Combat raise)")]
        [Tooltip("OnHitConfirmed (HitEventChannelSO) — trúng THẬT. attacker → dealt, victim → taken.")]
        [SerializeField] private HitEventChannelSO onHitConfirmed;

        [Tooltip("OnHitBlocked (HitEventChannelSO) — đỡ ĐÚNG. Cả 2 bên × rageBlockMultiplier (rất ít).")]
        [SerializeField] private HitEventChannelSO onHitBlocked;

        [Header("Kênh sự kiện — HIỂN THỊ (per-player asset, HUD subscribe)")]
        [Tooltip("OnRageChanged (FloatEventChannelSO) — fill 0..1 cho HUD. Per-player (P1 vs P2 khác asset).")]
        [SerializeField] private FloatEventChannelSO onRageChanged;

        [Header("Tham chiếu")]
        [Tooltip("Config mana/rage (số balance). BẮT BUỘC gán.")]
        [SerializeField] private ResourceConfigSO config;

        [Tooltip("Root để so attacker/victim. Bỏ trống = lấy transform.root.")]
        [SerializeField] private Transform root;

        private float _rage;

        /// <summary>Nộ hiện tại (đơn vị nội bộ). P10 đọc.</summary>
        public float Current => _rage;

        /// <summary>Đã đầy (≥ RageMax) — P10 mở Rage Mode/Ultimate.</summary>
        public bool IsFull => config != null && _rage >= config.RageMax;

        // ===== IRageSpender (P10 ADD — KHÔNG sửa logic GAIN P8) =====

        /// <summary>Tỉ lệ nộ trên RageMax (0..1). DefenseController kiểm "đủ 50% burst?".</summary>
        public float Fraction => config != null && config.RageMax > 0f
            ? Mathf.Clamp01(_rage / config.RageMax) : 0f;

        /// <summary>Tiêu fraction×RageMax nộ (false nếu thiếu — KHÔNG trừ). Burst 0.5, Rage Mode 1.0.</summary>
        public bool TrySpendRage(float fraction)
        {
            if (config == null) return false;
            float cost = config.RageMax * Mathf.Clamp01(fraction);
            if (_rage + 1e-4f < cost) return false; // thiếu nộ (epsilon cho sai số float).
            _rage = Mathf.Clamp(_rage - cost, 0f, config.RageMax);
            RaiseChanged();
            return true;
        }

        /// <summary>Hoàn fraction×RageMax nộ (clamp ≤ max). Rage Mode hết hạn gọi (20–25%).</summary>
        public void RefundRage(float fraction)
        {
            if (config == null) return;
            float add = config.RageMax * Mathf.Clamp01(fraction);
            if (add <= 0f) return;
            float before = _rage;
            _rage = Mathf.Clamp(_rage + add, 0f, config.RageMax);
            if (!Mathf.Approximately(before, _rage)) RaiseChanged();
        }

        private void Awake()
        {
            if (root == null) root = transform.root;
            if (config == null)
                Debug.LogError("[RageBar] Thiếu ResourceConfigSO.", this);
        }

        private void Start()
        {
            // Bắn giá trị khởi đầu (0) sau khi HUD subscribe — HUD hiện đúng frame đầu.
            RaiseChanged();
        }

        private void OnEnable()
        {
            if (onHitConfirmed != null) onHitConfirmed.OnEventRaised += OnHitConfirmed;
            if (onHitBlocked != null) onHitBlocked.OnEventRaised += OnHitBlocked;
        }

        private void OnDisable()
        {
            if (onHitConfirmed != null) onHitConfirmed.OnEventRaised -= OnHitConfirmed;
            if (onHitBlocked != null) onHitBlocked.OnEventRaised -= OnHitBlocked;
        }

        private void OnHitConfirmed(HitData hit) => Accumulate(hit, blocked: false);

        private void OnHitBlocked(HitData hit) => Accumulate(hit, blocked: true);

        /// <summary>Cộng nộ theo vai (attacker = dealt, victim = taken), block nhân thêm hệ số.</summary>
        private void Accumulate(HitData hit, bool blocked)
        {
            if (config == null) return;
            GameObject me = root != null ? root.gameObject : gameObject;

            float gain;
            if (ReferenceEquals(hit.Attacker, me))
                gain = config.RageGainDealt * hit.DamagePercent;
            else if (ReferenceEquals(hit.Victim, me))
                gain = config.RageGainTaken * hit.DamagePercent;
            else
                return; // hit không liên quan tới mình.

            if (blocked) gain *= config.RageBlockMultiplier;
            if (gain <= 0f) return;

            float before = _rage;
            _rage = Mathf.Clamp(_rage + gain, 0f, config.RageMax);
            if (!Mathf.Approximately(before, _rage)) RaiseChanged();
        }

        /// <summary>DEBUG/test only — đặt lại nộ về 0. P12 RespawnSystem KHÔNG gọi (giữ qua chết).</summary>
        public void ResetRage()
        {
            _rage = 0f;
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            if (config == null) return;
            float max = config.RageMax;
            float fill = max > 0f ? Mathf.Clamp01(_rage / max) : 0f;
            if (onRageChanged != null) onRageChanged.RaiseEvent(fill);
        }
    }
}
