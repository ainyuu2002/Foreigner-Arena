using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// (P12) SNAPSHOT BẤT BIẾN kết quả 1 trận (research §3.4/§3.7). Value-type readonly → no GC. WinCondition
    /// (FA.GameFlow) DỰNG khi trận kết thúc; EndScreenView (FA.UI) HIỂN THỊ — UI nghe qua
    /// <see cref="MatchResultEventChannelSO"/> mà KHÔNG reference FA.GameFlow (§1.3 decoupling).
    ///
    /// Đặt ở Core vì cả GameFlow (raise) và UI (subscribe) đọc chung. Gói stats 2 người (mảng index theo
    /// <see cref="PlayerSlot"/>) để End Screen in "Total Damage / Longest Combo / KO / % cuối" như GDD §9.1.
    /// </summary>
    public readonly struct MatchResult
    {
        /// <summary>Người thắng (hoặc <see cref="MatchWinner.Draw"/> khi hòa điểm).</summary>
        public readonly MatchWinner Winner;

        /// <summary>Lý do kết thúc (out-of-lives / time-up) — End Screen có thể in.</summary>
        public readonly MatchEndReason Reason;

        /// <summary>Stats P1 (index 0) — xem <see cref="PlayerSlot"/>.</summary>
        public readonly PlayerStats P1;

        /// <summary>Stats P2 (index 1).</summary>
        public readonly PlayerStats P2;

        public MatchResult(MatchWinner winner, MatchEndReason reason, PlayerStats p1, PlayerStats p2)
        {
            Winner = winner;
            Reason = reason;
            P1 = p1;
            P2 = p2;
        }

        /// <summary>Lấy stats theo slot (tiện cho UI lặp 2 người).</summary>
        public PlayerStats StatsFor(PlayerSlot slot) => slot == PlayerSlot.P1 ? P1 : P2;
    }

    /// <summary>Kết quả người thắng của 1 trận.</summary>
    public enum MatchWinner { P1, P2, Draw }

    /// <summary>Vì sao trận kết thúc (cho End Screen hiển thị + WinCondition phân nhánh tie-break).</summary>
    public enum MatchEndReason { OutOfLives, TimeUp }

    /// <summary>
    /// Thống kê tích lũy 1 người chơi suốt trận (research §3.4). ScoreSystem gom; tie-break điểm =
    /// <c>TotalDamage + comboBonus*TotalComboCount</c> (công thức ở MatchSettingsSO, chỉnh được). Struct
    /// readonly để truyền qua MatchResult không sửa được sau khi dựng.
    /// </summary>
    public readonly struct PlayerStats
    {
        /// <summary>Tổng % damage đã GÂY RA (cộng từ OnHitConfirmed.DamagePercent khi mình là Attacker).</summary>
        public readonly float TotalDamage;

        /// <summary>Combo dài nhất đạt được (max count nhận từ kênh combo của mình).</summary>
        public readonly int LongestCombo;

        /// <summary>Số combo đã bắt đầu (mỗi lần count 0→1) — dùng cho công thức tie-break.</summary>
        public readonly int TotalComboCount;

        /// <summary>Số lần KO đối thủ (đối thủ rơi death zone).</summary>
        public readonly int KoCount;

        /// <summary>Số mạng còn lại (so mạng khi hết giờ).</summary>
        public readonly int LivesRemaining;

        /// <summary>% knockback hiện tại cuối trận (End Screen hiển thị).</summary>
        public readonly float FinalPercent;

        public PlayerStats(float totalDamage, int longestCombo, int totalComboCount,
                           int koCount, int livesRemaining, float finalPercent)
        {
            TotalDamage = totalDamage;
            LongestCombo = longestCombo;
            TotalComboCount = totalComboCount;
            KoCount = koCount;
            LivesRemaining = livesRemaining;
            FinalPercent = finalPercent;
        }
    }
}
