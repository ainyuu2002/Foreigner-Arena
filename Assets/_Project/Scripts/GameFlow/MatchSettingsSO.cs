using UnityEngine;

namespace FA.GameFlow
{
    /// <summary>
    /// (P12, research §5) GOM MỌI HẰNG vòng đời trận — 1 asset, chỉnh trong Inspector, KHÔNG hardcode
    /// (§6 CLAUDE.md). Mọi field đánh dấu CẦN TEST = giá trị mặc định người dùng đã chốt (lives=3, time=99,
    /// tie = TotalDamage + 2×TotalComboCount), balance lại sau khi chơi thử.
    ///
    /// LivesSystem/RespawnSystem/MatchTimer/WinCondition đọc asset này — KHÔNG ai giữ số cứng.
    /// </summary>
    [CreateAssetMenu(menuName = "FA/GameFlow/Match Settings", fileName = "MatchSettings")]
    public sealed class MatchSettingsSO : ScriptableObject
    {
        [Header("Mạng (Lives) — CẦN TEST")]
        [Tooltip("Số mạng đầu trận (GDD 1-5). Người dùng chốt mặc định = 3. CẦN TEST.")]
        [Range(1, 5)] public int startingLives = 3;

        [Header("Thời gian trận (Timer) — CẦN TEST")]
        [Tooltip("Thời lượng trận tính bằng giây, đếm NGƯỢC. Người dùng chốt = 99. Hết giờ → so mạng. CẦN TEST.")]
        [Min(1f)] public float matchSeconds = 99f;

        [Header("Respawn (GDD §8.1 cố định)")]
        [Tooltip("Respawn → % về 0 (GDD cố định). Tắt CHỈ để debug.")]
        public bool resetPercentOnRespawn = true;

        [Tooltip("i-frame respawn KẾT THÚC khi đáp đất (GDD cố định). Tắt = dùng max-seconds thuần (debug).")]
        public bool iFrameEndsOnLanding = true;

        [Tooltip("Trần an toàn (giây) cho i-frame respawn nếu KHÔNG kịp đáp (kẹt trên không). CẦN TEST.")]
        [Min(0f)] public float iFrameMaxSeconds = 3f;

        [Tooltip("Mana giữ nguyên qua respawn (GDD cố định — cơ chế comeback). KHÔNG nên tắt.")]
        public bool keepManaOnRespawn = true;

        [Tooltip("Nộ giữ nguyên qua respawn (GDD cố định). KHÔNG nên tắt.")]
        public bool keepRageOnRespawn = true;

        [Header("Tie-break điểm (so điểm khi bằng mạng) — CẦN TEST")]
        [Tooltip("Trọng số combo trong công thức điểm. Người dùng chốt = 2 → " +
                 "score = TotalDamage + 2×TotalComboCount. CẦN TEST.")]
        [Min(0f)] public float comboBonusWeight = 2f;

        /// <summary>
        /// Công thức điểm tie-break (research §5, GDD §9.1). Người dùng chốt:
        /// <c>TotalDamage + comboBonusWeight × TotalComboCount</c>. Đổi công thức = sửa 1 chỗ này.
        /// </summary>
        public float ComputeScore(float totalDamage, int totalComboCount)
            => totalDamage + comboBonusWeight * totalComboCount;
    }
}
