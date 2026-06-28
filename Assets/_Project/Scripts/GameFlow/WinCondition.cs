using UnityEngine;
using FA.Core;

namespace FA.GameFlow
{
    /// <summary>
    /// (P12, research §3.5) PHÂN ĐỊNH THẮNG/THUA — logic thuần, kích bởi 2 nguồn (GDD §9.1):
    ///   (a) <c>onOutOfLives</c> (GameObjectEventChannelSO, LivesSystem raise SELF khi hết mạng) → người
    ///       đó THUA tức thì → người kia thắng (nếu cả 2 hết cùng frame → so điểm).
    ///   (b) <c>onTimeUp</c> (VoidEventChannelSO, MatchTimer) → SO MẠNG còn lại; bằng mạng → so điểm
    ///       (TotalDamage + comboBonus×TotalComboCount, công thức MatchSettingsSO).
    ///
    /// Dựng <see cref="MatchResult"/> (Core struct) từ ScoreSystem 2 người → raise <c>onMatchResult</c>
    /// (cho EndScreen UI) + <c>onMatchEnded</c> (Void, MatchManager chuyển phase End). MatchManager đăng ký
    /// 2 player runtime qua <see cref="RegisterPlayers"/> (player spawn động). Chỉ resolve 1 lần/trận.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WinCondition : MonoBehaviour
    {
        [Header("Cấu hình")]
        [Tooltip("MatchSettingsSO — công thức điểm tie-break. BẮT BUỘC gán.")]
        [SerializeField] private MatchSettingsSO settings;

        [Header("Kênh sự kiện (NGHE)")]
        [Tooltip("OnPlayerOutOfLives (GameObjectEventChannelSO) — LivesSystem raise self khi hết mạng.")]
        [SerializeField] private GameObjectEventChannelSO onOutOfLives;

        [Tooltip("OnTimeUp (VoidEventChannelSO) — MatchTimer raise khi hết giờ.")]
        [SerializeField] private VoidEventChannelSO onTimeUp;

        [Header("Kênh sự kiện (BẮN)")]
        [Tooltip("OnMatchResult (MatchResultEventChannelSO) — gửi kết quả + stats cho EndScreen (FA.UI).")]
        [SerializeField] private MatchResultEventChannelSO onMatchResult;

        [Tooltip("OnMatchEnded (VoidEventChannelSO) — MatchManager nghe để chuyển phase End.")]
        [SerializeField] private VoidEventChannelSO onMatchEnded;

        private LivesSystem _livesP1, _livesP2;
        private ScoreSystem _scoreP1, _scoreP2;
        private bool _resolved;

        private void OnEnable()
        {
            if (onOutOfLives != null) onOutOfLives.OnEventRaised += OnOutOfLives;
            if (onTimeUp != null) onTimeUp.OnEventRaised += OnTimeUp;
        }

        private void OnDisable()
        {
            if (onOutOfLives != null) onOutOfLives.OnEventRaised -= OnOutOfLives;
            if (onTimeUp != null) onTimeUp.OnEventRaised -= OnTimeUp;
        }

        /// <summary>MatchManager gắn 2 player vừa spawn (lives+score mỗi người). Reset cờ resolve.</summary>
        public void RegisterPlayers(LivesSystem livesP1, ScoreSystem scoreP1,
                                    LivesSystem livesP2, ScoreSystem scoreP2)
        {
            _livesP1 = livesP1; _scoreP1 = scoreP1;
            _livesP2 = livesP2; _scoreP2 = scoreP2;
            _resolved = false;
        }

        private void OnOutOfLives(GameObject who)
        {
            if (_resolved) return;
            Resolve(MatchEndReason.OutOfLives);
        }

        private void OnTimeUp()
        {
            if (_resolved) return;
            Resolve(MatchEndReason.TimeUp);
        }

        private void Resolve(MatchEndReason reason)
        {
            _resolved = true;

            PlayerStats s1 = _scoreP1 != null ? _scoreP1.BuildStats() : default;
            PlayerStats s2 = _scoreP2 != null ? _scoreP2.BuildStats() : default;

            MatchWinner winner = DecideWinner(s1, s2);

            MatchResult result = new MatchResult(winner, reason, s1, s2);
            if (onMatchResult != null) onMatchResult.RaiseEvent(result);
            if (onMatchEnded != null) onMatchEnded.RaiseEvent();
        }

        /// <summary>
        /// So mạng trước (out-of-lives = mạng = 0 → người kia nhiều mạng hơn thắng). Bằng mạng → so điểm.
        /// Cả 2 hết mạng cùng lúc / điểm bằng → Draw (GDD: chết đồng thời ván cuối → so điểm).
        /// </summary>
        private MatchWinner DecideWinner(PlayerStats s1, PlayerStats s2)
        {
            if (s1.LivesRemaining != s2.LivesRemaining)
                return s1.LivesRemaining > s2.LivesRemaining ? MatchWinner.P1 : MatchWinner.P2;

            // Bằng mạng → so điểm theo công thức SO.
            float score1 = settings != null ? settings.ComputeScore(s1.TotalDamage, s1.TotalComboCount)
                                            : s1.TotalDamage;
            float score2 = settings != null ? settings.ComputeScore(s2.TotalDamage, s2.TotalComboCount)
                                            : s2.TotalDamage;

            if (Mathf.Approximately(score1, score2)) return MatchWinner.Draw;
            return score1 > score2 ? MatchWinner.P1 : MatchWinner.P2;
        }
    }
}
