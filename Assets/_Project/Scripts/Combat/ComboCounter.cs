using UnityEngine;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// COMBO COUNTER (P6, research §3.4) — đếm hit liên tục của NGƯỜI ĐÁNH (per-attacker). Logic SỐ
    /// THUẦN: nghe OnHitConfirmed (CHỈ trúng-không-block — HitReactionController raise; KHÔNG nghe
    /// OnHitLanded để block không tăng combo, research §4 cạm bẫy). Mỗi hit của MÌNH → +1, bắn số qua
    /// IntEventChannelSO (asset OnComboChangedChannel) cho UI debug. Reset bằng TIMEOUT (đếm fixed-step):
    /// không có hit mới trong comboResetFrames → combo đứt (victim về neutral) → bắn 0.
    ///
    /// Milestone 10/20 → +1%/hit (GDD): P6 tính & in giá trị bonus; thanh % thật áp ở P7. Đặt ở
    /// FA.Combat, chỉ ref Core (HitData/HitEventChannelSO/IntEventChannelSO) — KHÔNG ref module khác.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComboCounter : MonoBehaviour
    {
        [Header("Kênh sự kiện")]
        [Tooltip("OnHitConfirmed (HitEventChannelSO) — CÙNG asset HitReactionController raise khi trúng thật.")]
        [SerializeField] private HitEventChannelSO onHitConfirmed;

        [Tooltip("OnComboChanged (IntEventChannelSO) — bắn số combo cho UI debug. RaiseEvent(count)/0 khi reset.")]
        [SerializeField] private IntEventChannelSO onComboChanged;

        [Header("Tham chiếu")]
        [Tooltip("Root NGƯỜI ĐÁNH (so hit.Attacker). Bỏ trống = lấy transform.root.")]
        [SerializeField] private Transform root;

        [Header("Tham số (giá trị test)")]
        [Tooltip("Timeout reset combo (số fixed-step không có hit mới → đứt). ≈ max hurtstun + grace. CẦN TEST.")]
        [SerializeField] private int comboResetFrames = 24;

        [Tooltip("Mốc combo thứ nhất cho +1%/hit (GDD).")]
        [SerializeField] private int milestone1 = 10;

        [Tooltip("Mốc combo thứ hai cho +1%/hit nữa (GDD).")]
        [SerializeField] private int milestone2 = 20;

        private int _count;
        private int _framesSinceLastHit;

        public int Count => _count;

        private void Awake()
        {
            if (root == null) root = transform.root;
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
            if (!ReferenceEquals(hit.Attacker, me)) return; // chỉ đếm combo CỦA MÌNH gây.

            _count++;
            _framesSinceLastHit = 0;

            int milestoneBonus = ComputeMilestoneBonus(_count);
            if (onComboChanged != null) onComboChanged.RaiseEvent(_count);

            DebugLog.Log($"[COMBO] {me.name}: {_count} HITS" +
                      (milestoneBonus > 0 ? $"  (milestone +{milestoneBonus}%/hit)" : ""), me);
        }

        private void FixedUpdate()
        {
            if (_count <= 0) return; // không có combo đang chạy → khỏi đếm timeout.

            _framesSinceLastHit++;
            if (_framesSinceLastHit >= comboResetFrames)
                ResetCombo();
        }

        private void ResetCombo()
        {
            if (_count > 0) DebugLog.Log($"[COMBO] {(root != null ? root.name : name)}: reset (đứt combo)", this);
            _count = 0;
            _framesSinceLastHit = 0;
            if (onComboChanged != null) onComboChanged.RaiseEvent(0);
        }

        /// <summary>+1%/hit tại mỗi mốc đạt (10 → +1, 20 → +2,... GDD). P6 chỉ tính/in; áp % thật ở P7.</summary>
        private int ComputeMilestoneBonus(int count)
        {
            int bonus = 0;
            if (count >= milestone1) bonus++;
            if (count >= milestone2) bonus++;
            return bonus;
        }
    }
}
