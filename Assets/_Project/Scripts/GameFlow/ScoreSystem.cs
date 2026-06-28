using UnityEngine;
using FA.Core;

namespace FA.GameFlow
{
    /// <summary>
    /// (P12, research §3.4) ĐIỂM/THỐNG KÊ per-player — gắn lên player prefab. Gom stats CỦA MÌNH:
    ///   • TotalDamage: nghe <c>OnHitConfirmed</c> (HitEventChannelSO), cộng DamagePercent khi hit.Attacker
    ///     LÀ MÌNH (block KHÔNG raise kênh này → không tính). KHÔNG ref FA.Combat — chỉ đọc HitData (Core).
    ///   • Longest/Total combo: nghe combo channel CỦA MÌNH (IntEventChannelSO per-player). Longest =
    ///     max(count); TotalComboCount += 1 mỗi khi count 0→1 (bắt đầu combo mới). ComboCounter (P6) raise
    ///     count + 0-on-reset đủ thông tin → KHÔNG sửa ComboCounter (research §3.4).
    ///   • KO: nghe <c>OnPlayerDied</c> — victim KHÔNG phải mình (1v1) → +1 KO cho mình (mình đẩy địch ra).
    ///
    /// WinCondition đọc <see cref="BuildStats"/> cuối trận. Đặt FA.GameFlow, chỉ ref Core (HitData/channels).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoreSystem : MonoBehaviour
    {
        [Header("Kênh sự kiện (NGHE)")]
        [Tooltip("OnHitConfirmed (HitEventChannelSO) — CÙNG asset Combat raise. Cộng DamagePercent khi Attacker=mình.")]
        [SerializeField] private HitEventChannelSO onHitConfirmed;

        [Tooltip("Combo channel CỦA MÌNH (IntEventChannelSO per-player) — ComboCounter của player này raise.")]
        [SerializeField] private IntEventChannelSO onMyComboChanged;

        [Tooltip("OnPlayerDied (GameObjectEventChannelSO) — victim≠mình → +1 KO cho mình.")]
        [SerializeField] private GameObjectEventChannelSO onPlayerDied;

        [Header("Tham chiếu cùng player")]
        [Tooltip("Root để so Attacker/Victim. Bỏ trống = transform.root.")]
        [SerializeField] private Transform root;

        [Tooltip("LivesSystem cùng player — cho LivesRemaining vào stats. Bỏ trống = GetComponent.")]
        [SerializeField] private LivesSystem livesSystem;

        private float _totalDamage;
        private int _longestCombo;
        private int _totalComboCount;
        private int _koCount;
        private int _prevComboCount;
        private float _finalPercent;

        private void Awake()
        {
            if (root == null) root = transform.root;
            if (livesSystem == null) livesSystem = GetComponent<LivesSystem>();
        }

        private void OnEnable()
        {
            if (onHitConfirmed != null) onHitConfirmed.OnEventRaised += OnHit;
            if (onMyComboChanged != null) onMyComboChanged.OnEventRaised += OnComboChanged;
            if (onPlayerDied != null) onPlayerDied.OnEventRaised += OnDied;
        }

        private void OnDisable()
        {
            if (onHitConfirmed != null) onHitConfirmed.OnEventRaised -= OnHit;
            if (onMyComboChanged != null) onMyComboChanged.OnEventRaised -= OnComboChanged;
            if (onPlayerDied != null) onPlayerDied.OnEventRaised -= OnDied;
        }

        /// <summary>Reset stats đầu trận (MatchManager gọi khi vào Match / Rematch).</summary>
        public void ResetStats()
        {
            _totalDamage = 0f;
            _longestCombo = 0;
            _totalComboCount = 0;
            _koCount = 0;
            _prevComboCount = 0;
            _finalPercent = 0f;
        }

        private void OnHit(HitData hit)
        {
            GameObject me = root != null ? root.gameObject : gameObject;
            if (!ReferenceEquals(hit.Attacker, me)) return;
            _totalDamage += hit.DamagePercent;
        }

        private void OnComboChanged(int count)
        {
            if (count > _longestCombo) _longestCombo = count;
            if (_prevComboCount == 0 && count > 0) _totalComboCount++; // bắt đầu combo mới (0→1).
            _prevComboCount = count;
        }

        private void OnDied(GameObject victim)
        {
            GameObject me = root != null ? root.gameObject : gameObject;
            if (ReferenceEquals(victim, me)) return; // mình chết → không cộng KO cho mình.
            _koCount++; // 1v1: địch chết = mình KO.
        }

        /// <summary>Đóng gói stats cuối trận (WinCondition gọi). finalPercent đọc % hiện tại của player.</summary>
        public PlayerStats BuildStats()
        {
            int lives = livesSystem != null ? livesSystem.Lives : 0;
            IPercentReceiver pr = GetComponentInChildren<IPercentReceiver>();
            _finalPercent = pr != null ? pr.CurrentPercent : 0f;
            return new PlayerStats(_totalDamage, _longestCombo, _totalComboCount, _koCount, lives, _finalPercent);
        }
    }
}
