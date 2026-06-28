using UnityEngine;
using FA.Core;

namespace FA.Resources
{
    /// <summary>
    /// THANH MANA (P8, research §1/§3.2) — logic SỐ THUẦN trên nhân vật (đặt cạnh KnockbackPercent).
    /// Kiểu SFV/GG Tension: lưu MỘT float 0..MaxMana, HIỂN THỊ chia ô (1 ô = manaPerSegment). Đầy theo
    /// CHỈ damage GÂY RA (attacker) — KHÔNG hồi khi bị đánh, KHÔNG passive regen, KHÔNG decay, GIỮ khi chết.
    ///
    /// Subscribe ĐỒNG THỜI OnHitConfirmed (trúng thật) + OnHitBlocked (đỡ đúng) — CÙNG asset
    /// HitReactionController raise. Tự phân vai bằng ReferenceEquals(hit.Attacker, me) (đúng pattern
    /// KnockbackPercent.cs:60-63 / ComboCounter.cs:60-63). CHỈ nhánh attacker mới cộng:
    ///   • OnHitConfirmed &amp; me==Attacker → gain = manaGainPerDamage × DamagePercent (full).
    ///   • OnHitBlocked   &amp; me==Attacker → gain × blockMultiplier (ít hơn).
    ///   • me==Victim (cả 2 kênh) → KHÔNG cộng gì (GDD: mana không hồi khi bị đánh).
    ///
    /// Raise OnManaChanged(fill 0..1) + OnManaSegmentsChanged(int ô) qua channel cho HUD ĐỌC — KHÔNG
    /// code UI ở đây (§1.3). API TrySpend/Segments/CurrentMana chuẩn bị cho P9 (tiêu nguyên ô).
    /// Đặt FA.Resources, chỉ ref Core. P12 RespawnSystem KHÔNG được gọi reset thanh này (giữ qua chết).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManaBar : MonoBehaviour, IManaSpender
    {
        [Header("Kênh sự kiện — NGUỒN (tái dùng asset Combat raise)")]
        [Tooltip("OnHitConfirmed (HitEventChannelSO) — trúng THẬT không-block. CÙNG asset KnockbackPercent/ComboCounter.")]
        [SerializeField] private HitEventChannelSO onHitConfirmed;

        [Tooltip("OnHitBlocked (HitEventChannelSO) — đỡ ĐÚNG loại. Attacker vẫn hồi mana nhưng × blockMultiplier.")]
        [SerializeField] private HitEventChannelSO onHitBlocked;

        [Header("Kênh sự kiện — HIỂN THỊ (per-player asset, HUD subscribe)")]
        [Tooltip("OnManaChanged (FloatEventChannelSO) — fill 0..1 cho HUD. Per-player (P1 vs P2 khác asset).")]
        [SerializeField] private FloatEventChannelSO onManaChanged;

        [Tooltip("OnManaSegmentsChanged (IntEventChannelSO) — SỐ Ô nguyên cho HUD vẽ ô rời. Per-player.")]
        [SerializeField] private IntEventChannelSO onManaSegmentsChanged;

        [Header("Tham chiếu")]
        [Tooltip("Config mana/rage (số balance). BẮT BUỘC gán.")]
        [SerializeField] private ResourceConfigSO config;

        [Tooltip("Root để so 'mình có phải attacker không'. Bỏ trống = lấy transform.root.")]
        [SerializeField] private Transform root;

        private float _mana;

        /// <summary>Mana hiện tại (đơn vị nội bộ float). P9 đọc/tiêu.</summary>
        public float CurrentMana => _mana;

        /// <summary>Số ô ĐẦY hiện có (floor). P9 đọc để cho phép special.</summary>
        public int Segments
        {
            get
            {
                if (config == null || config.ManaPerSegment <= 0f) return 0;
                return Mathf.FloorToInt(_mana / config.ManaPerSegment);
            }
        }

        private void Awake()
        {
            if (root == null) root = transform.root;
            if (config == null)
            {
                Debug.LogError("[ManaBar] Thiếu ResourceConfigSO.", this);
                return;
            }
            // GDD: start 1 ô. Khởi tạo + raise lần đầu để HUD hiện đúng ngay frame đầu (research §4.9).
            _mana = Mathf.Clamp(config.StartMana, 0f, config.MaxMana);
        }

        private void Start()
        {
            // Raise ở Start (sau khi HUD đã OnEnable subscribe) để giá trị khởi đầu hiện đúng.
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

        /// <summary>Cộng mana — CHỈ khi mình là ATTACKER (GDD: không hồi khi bị đánh).</summary>
        private void Accumulate(HitData hit, bool blocked)
        {
            if (config == null) return;
            GameObject me = root != null ? root.gameObject : gameObject;
            if (!ReferenceEquals(hit.Attacker, me)) return; // victim KHÔNG hồi mana.

            float gain = config.ManaGainPerDamage * hit.DamagePercent;
            if (blocked) gain *= config.ManaBlockMultiplier;
            if (gain <= 0f) return;

            float before = _mana;
            _mana = Mathf.Clamp(_mana + gain, 0f, config.MaxMana);
            if (!Mathf.Approximately(before, _mana)) RaiseChanged();
        }

        /// <summary>Tiêu nguyên <paramref name="segments"/> ô (P9 special/super). Trả false nếu thiếu ô.</summary>
        public bool TrySpend(int segments)
        {
            if (config == null || segments <= 0) return false;
            if (Segments < segments) return false; // chưa đủ ô.
            _mana = Mathf.Max(0f, _mana - segments * config.ManaPerSegment);
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// (P13) IManaSpender.AddMana — CỘNG <paramref name="fractionOfMax"/> × MaxMana (Mana Potion item).
        /// Additive: đi qua ĐÚNG clamp ≤ MaxMana + raise OnManaChanged/OnManaSegmentsChanged như gain P8 →
        /// HUD ô tự cập nhật. KHÔNG đổi logic gain P8. Item gọi null-safe qua interface (Core).
        /// </summary>
        public void AddMana(float fractionOfMax)
        {
            if (config == null || fractionOfMax <= 0f) return;
            float add = config.MaxMana * fractionOfMax;
            if (add <= 0f) return;
            float before = _mana;
            _mana = Mathf.Clamp(_mana + add, 0f, config.MaxMana);
            if (!Mathf.Approximately(before, _mana)) RaiseChanged();
        }

        /// <summary>DEBUG/test only (P10) — nạp ĐẦY mana (mọi ô) để test né/RC tốn mana nhanh. KHÔNG gameplay.</summary>
        public void DebugFillMax()
        {
            if (config == null) return;
            _mana = config.MaxMana;
            RaiseChanged();
        }

        /// <summary>DEBUG/test only — đặt lại mana về startSegments. P12 RespawnSystem KHÔNG gọi (giữ qua chết).</summary>
        public void ResetMana()
        {
            if (config == null) return;
            _mana = Mathf.Clamp(config.StartMana, 0f, config.MaxMana);
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            if (config == null) return;
            float max = config.MaxMana;
            float fill = max > 0f ? Mathf.Clamp01(_mana / max) : 0f;
            if (onManaChanged != null) onManaChanged.RaiseEvent(fill);
            if (onManaSegmentsChanged != null) onManaSegmentsChanged.RaiseEvent(Segments);
        }
    }
}
