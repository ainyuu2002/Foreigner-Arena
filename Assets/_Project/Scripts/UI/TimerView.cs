using UnityEngine;
using UnityEngine.UI;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// (P12) HUD đồng hồ trận — subscribe <see cref="FloatEventChannelSO"/> (OnTimerChanged, giây còn lại
    /// MatchTimer raise) → set <c>Text.text</c>. Decoupling (§1.3): chỉ nghe kênh Core, KHÔNG ref FA.GameFlow.
    /// Code chỉ format chuỗi; layout/font để uGUI lo (§1.1). Dùng uGUI Text (nhất quán P8, không thêm dep TMP).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimerView : MonoBehaviour
    {
        [Header("Kênh sự kiện")]
        [Tooltip("OnTimerChanged (FloatEventChannelSO) — giây còn lại từ MatchTimer.")]
        [SerializeField] private FloatEventChannelSO channel;

        [Header("Hiển thị")]
        [Tooltip("Text hiển thị thời gian. Format MM:SS hoặc giây nguyên tùy showAsMinutes.")]
        [SerializeField] private Text label;

        [Tooltip("True = MM:SS; False = giây nguyên (vd '99').")]
        [SerializeField] private bool showAsMinutes = false;

        private void OnEnable() { if (channel != null) channel.OnEventRaised += OnTimer; }
        private void OnDisable() { if (channel != null) channel.OnEventRaised -= OnTimer; }

        private void OnTimer(float seconds)
        {
            if (label == null) return;
            int s = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            label.text = showAsMinutes ? $"{s / 60:0}:{s % 60:00}" : s.ToString();
        }
    }
}
