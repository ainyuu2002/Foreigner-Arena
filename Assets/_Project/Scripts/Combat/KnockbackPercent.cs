using UnityEngine;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// THANH % (P7, research §3.1) — logic SỐ THUẦN trên VICTIM. Thay <c>_accumulatedPercent</c> debug
    /// của HitReactionController (P6) bằng thanh % thật. Subscribe OnHitConfirmed (CHỈ trúng-không-block →
    /// block KHÔNG tích %, đúng GDD); khi mình là Victim → cộng <see cref="HitData.DamagePercent"/> (+
    /// milestone bonus đọc combo của attacker) rồi clamp tại <c>percentCap</c>. KHÔNG decay (đơn giản:
    /// không Update trừ). <see cref="ResetPercent"/> public (P7 nút debug; P12 RespawnSystem gọi → % về 0).
    ///
    /// Bắn OnPercentChanged(float) qua FloatEventChannelSO (đã có ở Core) cho HUD P8 ĐỌC — KHÔNG code UI
    /// ở đây (§1.3). KnockbackResolver ĐỌC <see cref="Current"/> để tính lực. Đặt FA.Combat, chỉ ref Core.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KnockbackPercent : MonoBehaviour, IPercentReceiver
    {
        [Header("Kênh sự kiện")]
        [Tooltip("OnHitConfirmed (HitEventChannelSO) — CÙNG asset HitReactionController/ComboCounter raise. " +
                 "Block KHÔNG raise kênh này → block không tích %.")]
        [SerializeField] private HitEventChannelSO onHitConfirmed;

        [Tooltip("OnPercentChanged (FloatEventChannelSO) — bắn % mới cho HUD P8. RaiseEvent(percent).")]
        [SerializeField] private FloatEventChannelSO onPercentChanged;

        [Header("Tham chiếu")]
        [Tooltip("Config knockback (cap %, milestone). BẮT BUỘC gán.")]
        [SerializeField] private KnockbackConfigSO config;

        [Tooltip("Root để so 'mình có phải victim không'. Bỏ trống = lấy transform.root.")]
        [SerializeField] private Transform root;

        [Tooltip("(Tùy) ComboCounter của ĐỐI THỦ đang đánh mình — để cộng milestone +1%/hit theo combo. " +
                 "Bỏ trống = không cộng milestone (chỉ base). P7 test 1v1: gán combo của địch.")]
        [SerializeField] private ComboCounter attackerCombo;

        private float _percent;

        /// <summary>% hiện tại (KnockbackResolver đọc để tính lực).</summary>
        public float Current => _percent;

        /// <summary>IPercentReceiver (P11) — Core interface để module môi trường (FA.Stage FireBiome) đọc %.</summary>
        public float CurrentPercent => _percent;

        /// <summary>
        /// IPercentReceiver.AddPercent (P11, additive — KHÔNG đổi logic P7). FireBiome (FA.Stage) gọi để
        /// tăng % khi nhân vật đứng yên trong lửa. Đi qua ĐÚNG clamp/cap + raise OnPercentChanged như hit
        /// thường → HUD P8 tự cập nhật, KnockbackResolver đọc Current như cũ. Không reference chéo: Stage
        /// chỉ biết IPercentReceiver (Core), không biết KnockbackPercent.
        /// </summary>
        public void AddPercent(float amount)
        {
            if (amount == 0f) return;
            _percent += amount;
            float cap = config != null ? config.percentCap : 999f;
            _percent = Mathf.Clamp(_percent, 0f, cap);
            if (onPercentChanged != null) onPercentChanged.RaiseEvent(_percent);
        }

        // Buff context của CHÍNH MÌNH (victim-side): DamageTakenMult <1 khi UBW install (P-Ultimate).
        private IBuffContext _buff;

        private void Awake()
        {
            if (root == null) root = transform.root;
            if (config == null)
                Debug.LogError("[KnockbackPercent] Thiếu KnockbackConfigSO.", this);
            _buff = root != null ? root.GetComponent<IBuffContext>() : GetComponent<IBuffContext>();
        }

        private void OnEnable()
        {
            if (onHitConfirmed != null) onHitConfirmed.OnEventRaised += OnHitConfirmed;
        }

        private void OnDisable()
        {
            if (onHitConfirmed != null) onHitConfirmed.OnEventRaised -= OnHitConfirmed;
        }

        private void OnHitConfirmed(HitData hit)
        {
            GameObject me = root != null ? root.gameObject : gameObject;
            if (!ReferenceEquals(hit.Victim, me)) return;

            float add = hit.DamagePercent;
            if (add <= 0f && config != null) add = config.percentPerHitFallback;

            // Buff phòng thủ của victim (UBW install: DamageTakenMult <1 → chịu đòn trâu hơn). Null-safe ×1.
            if (_buff != null) add *= _buff.DamageTakenMult;

            // Milestone +1%/hit (GDD) — đếm theo combo của attacker (per-attacker ở ComboCounter).
            if (config != null && attackerCombo != null)
                add += config.MilestoneBonus(attackerCombo.Count);

            _percent += add;
            float cap = config != null ? config.percentCap : 999f;
            _percent = Mathf.Clamp(_percent, 0f, cap);   // clamp → knockback formula không vô cực.

            if (onPercentChanged != null) onPercentChanged.RaiseEvent(_percent);
        }

        /// <summary>Reset % về 0 (P7 nút debug; P12 RespawnSystem khi mất mạng). Bắn 0 cho HUD.
        /// Tên ResetPercent (KHÔNG 'Reset') để KHÔNG đụng Unity magic method MonoBehaviour.Reset().</summary>
        public void ResetPercent()
        {
            _percent = 0f;
            if (onPercentChanged != null) onPercentChanged.RaiseEvent(0f);
        }
    }
}
