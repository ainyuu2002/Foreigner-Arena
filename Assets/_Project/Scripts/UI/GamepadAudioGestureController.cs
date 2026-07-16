using System;
using UnityEngine;

namespace FA.UI
{
    /// <summary>
    /// CỬ CHỈ TAY CẦM CHỈNH ÂM LƯỢNG (yêu cầu riêng): <b>LẮC</b> DualSense → giảm âm lượng tổng,
    /// <b>THỔI</b> vào mic tay cầm → tăng âm lượng tổng. Ghi thẳng <see cref="GameSettings.Master"/>
    /// (0..1 → AudioListener.volume). CHỈ chạy khi có panel Settings đang mở
    /// (<see cref="SettingsPanelView.AnyOpen"/>) — đóng Settings thì tắt mic, không nghe lén; và khi chơi/
    /// nói chuyện KHÔNG kích nhầm (gating theo ngữ cảnh như Astro's Playroom / Ghost of Tsushima).
    ///
    /// • LẮC: đọc accelerometer + gyroscope qua <see cref="JoyShockNative"/> (JoyShockLibrary.dll) — Unity
    ///   Input System không cấp IMU của tay PS trên Windows. Thiếu DLL → phần lắc im lặng (không crash).
    /// • THỔI: <see cref="Microphone"/> bắt mic DualSense (Windows hiện nó như audio input), đo RMS. Mic
    ///   DualSense CHỈ chạy qua USB.
    ///
    /// Tự bootstrap 1 instance bền (DontDestroyOnLoad) nếu scene chưa có — hoặc đặt sẵn 1 component đã tune
    /// vào scene để chỉnh ngưỡng trong Inspector. KHÔNG đụng code gameplay (§1.3): chỉ đọc phần cứng + ghi
    /// PlayerPrefs volume. Các ngưỡng dưới là CẢM GIÁC PHẦN CỨNG → CẦN TEST tay với DualSense thật.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamepadAudioGestureController : MonoBehaviour
    {
        [Header("Tốc độ đổi âm lượng")]
        [Tooltip("Đơn vị/giây khi cử chỉ ở cường độ tối đa (Master 0..1). CẦN TEST.")]
        [SerializeField] private float volumeChangePerSecond = 0.6f;

        [Header("LẮC (accelerometer + gyro, qua JoyShockLibrary)")]
        [Range(0f, 1f)]
        [Tooltip("Cường độ lắc (0..1) tối thiểu để bắt đầu GIẢM âm lượng. CẦN TEST.")]
        [SerializeField] private float shakeThreshold = 0.35f;
        [Tooltip("Độ lệch gia tốc (g) so với 1g nghỉ được coi là 'lắc mạnh' = 1.0. CẦN TEST.")]
        [SerializeField] private float refAccelDeviationG = 1.2f;
        [Tooltip("Vận tốc góc (độ/giây) được coi là 'lắc mạnh' = 1.0. CẦN TEST.")]
        [SerializeField] private float refGyroDegPerSec = 900f;
        [Tooltip("Giây giữa các lần thử kết nối lại JSL khi chưa thấy tay cầm.")]
        [SerializeField] private float jslRetrySeconds = 2f;

        [Header("THỔI (mic tay cầm, qua UnityEngine.Microphone)")]
        [Range(0f, 1f)]
        [Tooltip("Cường độ thổi (0..1) tối thiểu để bắt đầu TĂNG âm lượng. CẦN TEST (tránh trigger bởi tiếng ồn).")]
        [SerializeField] private float blowThreshold = 0.2f;
        [Tooltip("RMS biên độ mic được coi là 'thổi mạnh' = 1.0. CẦN TEST.")]
        [SerializeField] private float refBlowRms = 0.25f;
        [Range(0f, 1f)]
        [Tooltip("Lọc chống ồn: chỉ tính là THỔI khi zero-crossing rate ≤ ngưỡng này (hơi thổi = tần số " +
                 "thấp → ít đổi dấu; giọng nói/tiếng rè đổi dấu nhiều → bị loại). Tăng nếu thổi không nhận, " +
                 "giảm nếu vẫn dính tiếng ồn. CẦN TEST.")]
        [SerializeField] private float maxBlowZcr = 0.12f;
        [Tooltip("Lọc chống tiếng động ngắn (gõ/click): phải duy trì đủ lâu (giây) mới bắt đầu tăng. CẦN TEST.")]
        [SerializeField] private float blowSustainSeconds = 0.12f;
        [Tooltip("Chuỗi con để nhận diện mic DualSense trong Microphone.devices (mic tay cầm ~ 'Controller'). " +
                 "KHÔNG fallback sang mic khác nếu không tìm thấy — tránh nghe nhầm mic thật của người dùng.")]
        [SerializeField] private string micNameFilter = "Controller";
        [Tooltip("Số mẫu cuối dùng tính RMS/ZCR mỗi frame.")]
        [SerializeField] private int micSampleWindow = 1024;

        // --- Trạng thái runtime ---
        private static GamepadAudioGestureController _instance;
        private readonly int[] _handleBuf = new int[8];
        private bool _jslConnected;
        private float _nextJslConnect;
        private AudioClip _micClip;
        private string _micDevice;
        private float[] _micBuffer;
        private bool _loggedNoMic;
        private float _blowHeld; // giây đã giữ điều kiện thổi liên tục (lọc tiếng động ngắn).

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            if (FindFirstObjectByType<GamepadAudioGestureController>() != null) return; // đã đặt sẵn (đã tune).
            var go = new GameObject(nameof(GamepadAudioGestureController));
            go.AddComponent<GamepadAudioGestureController>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            _micBuffer = new float[Mathf.Max(64, micSampleWindow)];
        }

        private void Update()
        {
            // CHỈ nghe khi panel Settings đang MỞ (né kích nhầm khi chơi/nói chuyện — cách game AAA làm:
            // gating theo ngữ cảnh). Đóng Settings → đảm bảo mic đã tắt (không nghe lén) rồi thôi.
            if (!SettingsPanelView.AnyOpen) { StopMic(); return; }

            // unscaled: hoạt động cả khi game pause (Settings mở trong trận → Time.timeScale=0).
            float dt = Time.unscaledDeltaTime;

            float blow = ReadBlowLevel();
            if (blow > blowThreshold) GameSettings.NudgeMaster(+volumeChangePerSecond * dt * blow);

            float shake = ReadShakeLevel();
            if (shake > shakeThreshold) GameSettings.NudgeMaster(-volumeChangePerSecond * dt * shake);
        }

        // ───────────────────── LẮC ─────────────────────

        /// <summary>Cường độ lắc 0..1 = max(lệch gia tốc, vận tốc góc) đã chuẩn hoá. 0 nếu thiếu DLL/không tay cầm.</summary>
        private float ReadShakeLevel()
        {
            if (!JoyShockNative.Available) return 0f;
            EnsureJsl();

            int count = JoyShockNative.GetHandles(_handleBuf);
            if (count <= 0) { _jslConnected = false; return 0f; } // rút tay → thử kết nối lại lần sau.
            if (!JoyShockNative.TryGetImu(_handleBuf[0], out var s)) return 0f;

            float accelMag = Mathf.Sqrt(s.accelX * s.accelX + s.accelY * s.accelY + s.accelZ * s.accelZ);
            float accelDev = Mathf.Abs(accelMag - 1f); // nghỉ ≈ 1g; lắc → lệch.
            float gyroMag = Mathf.Sqrt(s.gyroX * s.gyroX + s.gyroY * s.gyroY + s.gyroZ * s.gyroZ);

            float level = Mathf.Max(accelDev / Mathf.Max(0.01f, refAccelDeviationG),
                                    gyroMag / Mathf.Max(1f, refGyroDegPerSec));
            return Mathf.Clamp01(level);
        }

        private void EnsureJsl()
        {
            if (_jslConnected) return;
            if (Time.unscaledTime < _nextJslConnect) return;
            _nextJslConnect = Time.unscaledTime + Mathf.Max(0.25f, jslRetrySeconds);
            _jslConnected = JoyShockNative.Connect() > 0;
        }

        // ───────────────────── THỔI ─────────────────────

        /// <summary>
        /// Cường độ thổi 0..1 sau LỌC CHỐNG ỒN. Trả >0 chỉ khi: biên độ đủ lớn (RMS) + chủ yếu tần số thấp
        /// (zero-crossing rate ≤ <see cref="maxBlowZcr"/> — hơi thổi ít đổi dấu, tiếng nói/rè bị loại) +
        /// duy trì ≥ <see cref="blowSustainSeconds"/> (loại tiếng động ngắn). 0 nếu không có mic tay cầm.
        /// Bỏ DC offset trước khi đo (mic hay có bias làm sai RMS/ZCR).
        /// </summary>
        private float ReadBlowLevel()
        {
            if (!EnsureMic()) { _blowHeld = 0f; return 0f; }

            int end = Microphone.GetPosition(_micDevice);
            int start = end - _micBuffer.Length;
            if (start < 0) return 0f; // chưa đủ mẫu (đầu mỗi vòng loop) — bỏ frame, không sai lệch đáng kể.
            _micClip.GetData(_micBuffer, start);

            int n = _micBuffer.Length;
            float mean = 0f;
            for (int i = 0; i < n; i++) mean += _micBuffer[i];
            mean /= n;

            double energy = 0.0;
            int crossings = 0;
            float prevC = 0f;
            for (int i = 0; i < n; i++)
            {
                float c = _micBuffer[i] - mean;               // bỏ DC.
                energy += (double)c * c;
                if (i > 0 && (c >= 0f) != (prevC >= 0f)) crossings++;
                prevC = c;
            }
            float rms = Mathf.Sqrt((float)(energy / n));
            float zcr = crossings / (float)(n - 1);          // 0..1: tỉ lệ đổi dấu ~ độ "cao tần".

            float loud = Mathf.Clamp01(rms / Mathf.Max(1e-4f, refBlowRms));
            bool blowing = loud > blowThreshold && zcr <= maxBlowZcr;

            // Duy trì liên tục: cộng dồn thời gian giữ điều kiện; reset khi đứt (loại gõ/click tức thời).
            _blowHeld = blowing ? _blowHeld + Time.unscaledDeltaTime : 0f;
            return _blowHeld >= blowSustainSeconds ? loud : 0f;
        }

        /// <summary>Mở mic tay cầm nếu chưa mở. FALSE nếu không có thiết bị mic khớp bộ lọc.</summary>
        private bool EnsureMic()
        {
            if (_micClip != null && _micDevice != null && Microphone.IsRecording(_micDevice)) return true;

            _micDevice = FindControllerMic();
            if (_micDevice == null)
            {
                if (!_loggedNoMic)
                {
                    _loggedNoMic = true;
                    Debug.LogWarning($"[GamepadGesture] Không thấy mic tay cầm (lọc '{micNameFilter}'). " +
                                     "Cắm DualSense bằng USB (mic không chạy qua Bluetooth). Phần 'THỔI' tạm tắt.");
                }
                return false;
            }
            _loggedNoMic = false;
            _micClip = Microphone.Start(_micDevice, true, 1, 44100);
            return _micClip != null;
        }

        private string FindControllerMic()
        {
            foreach (var d in Microphone.devices)
                if (!string.IsNullOrEmpty(d) &&
                    d.IndexOf(micNameFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                    return d;
            return null; // KHÔNG fallback → không bao giờ mở mic thật của người dùng.
        }

        private void StopMic()
        {
            if (_micDevice != null && Microphone.IsRecording(_micDevice)) Microphone.End(_micDevice);
            _micClip = null;
            _blowHeld = 0f;
        }

        // ───────────────────── Dọn dẹp ─────────────────────

        private void OnDisable() => StopMic();

        private void OnDestroy()
        {
            StopMic();
            JoyShockNative.DisposeAll();
            if (_instance == this) _instance = null;
        }
    }
}
