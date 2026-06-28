using UnityEngine;
using UnityEngine.UI;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// (P12) MÀN KẾT THÚC — subscribe <see cref="MatchResultEventChannelSO"/> (WinCondition raise) → in
    /// winner + stats 2 người (Total Damage, Longest Combo, Total Combo, KO, % cuối — GDD §9.1). Decoupling
    /// (§1.3): nghe kênh Core (MatchResult struct), KHÔNG ref FA.GameFlow. Code chỉ format text; layout uGUI lo.
    ///
    /// Nút Rematch / Back-to-CharSelect = component <see cref="VoidButtonRaiser"/> riêng raise kênh tương ứng
    /// (MatchManager nghe) — KHÔNG gọi MatchManager trực tiếp.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndScreenView : MonoBehaviour
    {
        [Header("Kênh sự kiện")]
        [Tooltip("OnMatchResult (MatchResultEventChannelSO) — kết quả + stats từ WinCondition.")]
        [SerializeField] private MatchResultEventChannelSO channel;

        [Header("Hiển thị")]
        [SerializeField] private Text winnerLabel;
        [SerializeField] private Text statsP1Label;
        [SerializeField] private Text statsP2Label;

        private void OnEnable() { if (channel != null) channel.OnEventRaised += OnResult; }
        private void OnDisable() { if (channel != null) channel.OnEventRaised -= OnResult; }

        private void OnResult(MatchResult result)
        {
            if (winnerLabel != null)
                winnerLabel.text = result.Winner switch
                {
                    MatchWinner.P1 => "PLAYER 1 WINS",
                    MatchWinner.P2 => "PLAYER 2 WINS",
                    _ => "DRAW"
                };

            if (statsP1Label != null) statsP1Label.text = Format("P1", result.P1);
            if (statsP2Label != null) statsP2Label.text = Format("P2", result.P2);
        }

        private static string Format(string who, PlayerStats s)
            => $"{who}\n" +
               $"Lives: {s.LivesRemaining}\n" +
               $"Total Damage: {s.TotalDamage:0}%\n" +
               $"Longest Combo: {s.LongestCombo}\n" +
               $"Combos: {s.TotalComboCount}\n" +
               $"KO: {s.KoCount}\n" +
               $"Final %: {s.FinalPercent:0}%";
    }
}
