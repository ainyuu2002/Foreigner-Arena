using UnityEngine;
using FA.Core;

namespace FA.GameFlow
{
    /// <summary>
    /// (P12, research §3.6) ĐỒNG HỒ TRẬN — đếm NGƯỢC từ <c>MatchSettingsSO.matchSeconds</c> bằng
    /// <c>Time.deltaTime</c> (Unity-native, KHÔNG tự tích phân khác). CHỈ chạy khi <see cref="IsRunning"/>
    /// (MatchManager bật/tắt theo phase — pause ở menu/End). Hitstop P5 dùng Animator.speed (KHÔNG
    /// Time.timeScale) → Time.deltaTime KHÔNG bị hitstop làm sai (research §4 cạm bẫy 9).
    ///
    /// Mỗi frame raise <see cref="onTimerChanged"/> (FloatEventChannelSO, giây còn lại) cho TimerView
    /// (FA.UI). Hết giờ → raise <see cref="onTimeUp"/> (VoidEventChannelSO) MỘT lần → WinCondition nghe.
    /// KHÔNG đếm frame data ở đây — đây là đồng hồ vòng đời trận, không phải timing đòn (Animation Event).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchTimer : MonoBehaviour
    {
        [Header("Cấu hình")]
        [Tooltip("MatchSettingsSO — matchSeconds (mặc định 99). BẮT BUỘC gán.")]
        [SerializeField] private MatchSettingsSO settings;

        [Header("Kênh sự kiện (BẮN)")]
        [Tooltip("OnTimerChanged (FloatEventChannelSO) — giây còn lại, cho TimerView (FA.UI).")]
        [SerializeField] private FloatEventChannelSO onTimerChanged;

        [Tooltip("OnTimeUp (VoidEventChannelSO) — raise 1 lần khi hết giờ. WinCondition nghe.")]
        [SerializeField] private VoidEventChannelSO onTimeUp;

        private float _remaining;
        private bool _running;
        private bool _firedTimeUp;

        /// <summary>Có đang đếm không (MatchManager set).</summary>
        public bool IsRunning => _running;

        /// <summary>Giây còn lại (debug/HUD).</summary>
        public float Remaining => _remaining;

        /// <summary>Đặt lại đồng hồ về full (MatchManager gọi khi vào Match / Rematch).</summary>
        public void ResetTimer()
        {
            _remaining = settings != null ? settings.matchSeconds : 99f;
            _firedTimeUp = false;
            if (onTimerChanged != null) onTimerChanged.RaiseEvent(_remaining);
        }

        /// <summary>Bật/tắt đếm (pause ở menu/End).</summary>
        public void SetRunning(bool running) => _running = running;

        private void Update()
        {
            if (!_running || _firedTimeUp) return;

            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
            {
                _remaining = 0f;
                _firedTimeUp = true;
                if (onTimerChanged != null) onTimerChanged.RaiseEvent(0f);
                if (onTimeUp != null) onTimeUp.RaiseEvent();
                return;
            }

            if (onTimerChanged != null) onTimerChanged.RaiseEvent(_remaining);
        }
    }
}
