using UnityEngine;
using UnityEngine.UI;
using FA.GameFlow;

namespace FA.UI
{
    /// <summary>
    /// (P12 Settings) HÀNG LUẬT TRẬN trên màn chọn map — chỉnh 2 luật per-trận của
    /// <see cref="MatchSettingsSO"/> ngay trước khi vào trận (đúng chỗ hơn Settings toàn cục —
    /// thời gian/mạng là luật CỦA TRẬN, không phải cấu hình máy):
    ///   • Thời gian: xoay vòng <see cref="timeOptions"/> (180/360/600 giây — mốc team chốt tuần 3).
    ///   • Mạng: 1..5 (khớp [Range] của SO).
    ///
    /// GHI THẲNG asset MatchSettingsSO — MatchTimer/LivesSystem đọc asset này lúc vào trận nên nhận
    /// giá trị mới không cần wire thêm. FA.UI ref FA.GameFlow CHỈ cho data-SO này (không ref hệ thống
    /// gameplay nào — MatchManager/LivesSystem vẫn không biết UI, §1.3 giữ nguyên chiều ngược).
    /// Nút &lt; &gt; wire persistent listener vào NextTime/PrevTime/NextLives/PrevLives.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchRulesView : MonoBehaviour
    {
        [Tooltip("MatchSettingsSO — asset luật trận (MatchTimer/LivesSystem cùng đọc). BẮT BUỘC gán.")]
        [SerializeField] private MatchSettingsSO settings;

        [Header("Labels giá trị")]
        [SerializeField] private Text timeValueLabel;
        [SerializeField] private Text livesValueLabel;

        [Header("Lựa chọn thời gian (giây)")]
        [Tooltip("Các mốc xoay vòng cho nút < >. Team chốt: 180 / 360 / 600.")]
        [SerializeField] private float[] timeOptions = { 180f, 360f, 600f };

        private void OnEnable() => Refresh();

        public void NextTime() => CycleTime(+1);
        public void PrevTime() => CycleTime(-1);
        public void NextLives() => CycleLives(+1);
        public void PrevLives() => CycleLives(-1);

        private void CycleTime(int dir)
        {
            if (settings == null || timeOptions == null || timeOptions.Length == 0) return;
            int idx = NearestTimeIndex();
            idx = (idx + dir + timeOptions.Length) % timeOptions.Length;
            settings.matchSeconds = Mathf.Max(1f, timeOptions[idx]);
            Refresh();
        }

        private void CycleLives(int dir)
        {
            if (settings == null) return;
            settings.startingLives = Mathf.Clamp(settings.startingLives + dir, 1, 5);
            Refresh();
        }

        /// <summary>Mốc gần matchSeconds nhất — asset có thể đang giữ giá trị ngoài danh sách (chỉnh tay Inspector).</summary>
        private int NearestTimeIndex()
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < timeOptions.Length; i++)
            {
                float d = Mathf.Abs(timeOptions[i] - settings.matchSeconds);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        private void Refresh()
        {
            if (settings == null) return;
            if (timeValueLabel != null) timeValueLabel.text = Mathf.RoundToInt(settings.matchSeconds) + "s";
            if (livesValueLabel != null) livesValueLabel.text = settings.startingLives.ToString();
        }
    }
}
