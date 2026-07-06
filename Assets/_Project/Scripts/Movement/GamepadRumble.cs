using UnityEngine;
using UnityEngine.InputSystem;
using FA.Core;

namespace FA.Movement
{
    /// <summary>
    /// RUNG TAY CẦM khi CHÍNH MÌNH ăn đòn (haptic feedback). MonoBehaviour trên prefab nhân vật,
    /// subscribe OnHitConfirmed (trúng thật → rung mạnh) + OnHitBlocked (đỡ được → rung nhẹ) qua
    /// event channel Core — KHÔNG ref FA.Combat (§1.3). Tay cầm lấy từ
    /// <see cref="CharacterMotor.PairedGamepad"/> (cùng module) → rung ĐÚNG tay của người bị đánh
    /// (scheme Keyboard → null → im lặng, không lỗi; 2 pad → theo gamepadIndex, không rung nhầm).
    ///
    /// Unity-native (§1.1): Gamepad.SetMotorSpeeds (Input System dual-motor haptics) — hỗ trợ
    /// DualSense/DualShock/Xbox. KHÔNG tự viết driver/plugin. Hit chồng nhau → lấy MAX cường độ +
    /// restart thời gian (giống HitstopController — không cộng dồn thành rung vô hạn).
    /// 1 FILE 1 VIỆC: chỉ rumble; quyết hurt-vs-block là HitReactionController (nghe kênh kết quả).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamepadRumble : MonoBehaviour
    {
        [Header("Kênh sự kiện (Core)")]
        [Tooltip("OnHitConfirmed (HitEventChannelSO) — trúng THẬT không-block → rung mạnh. " +
                 "CÙNG asset HitReactionController raise.")]
        [SerializeField] private HitEventChannelSO onHitConfirmed;

        [Tooltip("(Tùy) OnHitBlocked (HitEventChannelSO) — đỡ trúng → rung nhẹ (×blockMultiplier). " +
                 "Bỏ trống = block không rung.")]
        [SerializeField] private HitEventChannelSO onHitBlocked;

        [Header("Tham chiếu")]
        [Tooltip("Root để so 'mình có phải victim không'. Bỏ trống = lấy transform.root.")]
        [SerializeField] private Transform root;

        [Tooltip("CharacterMotor cùng prefab — lấy tay cầm đã pair. Bỏ trống = lấy trên object.")]
        [SerializeField] private CharacterMotor motor;

        [Header("Cường độ (0..1, CẦN TEST theo cảm giác)")]
        [Tooltip("Motor TRẦM (low-frequency, rung 'nặng' bên trái). DualSense/Xbox đều có.")]
        [SerializeField, Range(0f, 1f)] private float lowFrequency = 0.7f;

        [Tooltip("Motor CAO (high-frequency, rung 'sắc' bên phải).")]
        [SerializeField, Range(0f, 1f)] private float highFrequency = 0.5f;

        [Tooltip("Thời gian rung mỗi hit (giây). Ngắn + mạnh = cảm giác 'chát'; dài = 'ù'. CẦN TEST.")]
        [SerializeField] private float duration = 0.15f;

        [Tooltip("Hệ số cường độ khi BLOCK (đỡ được thì rung nhẹ hơn ăn đòn thật).")]
        [SerializeField, Range(0f, 1f)] private float blockMultiplier = 0.35f;

        [Tooltip("Scale cường độ theo hitstopFrames của đòn (đòn nặng hitstop dài → rung mạnh hơn). " +
                 "Hệ số = hitstop/referenceHitstop, clamp [0.5, 1.5].")]
        [SerializeField] private bool scaleWithHitstop = true;

        [Tooltip("Mốc hitstop 'chuẩn' để scale (đòn có hitstop = mốc này → rung đúng cường độ đặt).")]
        [SerializeField] private int referenceHitstopFrames = 6;

        private Gamepad _activePad;  // pad đang rung (cache để tắt đúng tay + max-hit-chồng).
        private float _timeLeft;     // giây còn rung (0 = im).
        private float _curLow;
        private float _curHigh;

        private void Awake()
        {
            if (root == null) root = transform.root;
            if (motor == null) motor = GetComponent<CharacterMotor>();
        }

        private void OnEnable()
        {
            if (onHitConfirmed != null) onHitConfirmed.OnEventRaised += OnHurt;
            if (onHitBlocked != null) onHitBlocked.OnEventRaised += OnBlocked;
        }

        private void OnDisable()
        {
            if (onHitConfirmed != null) onHitConfirmed.OnEventRaised -= OnHurt;
            if (onHitBlocked != null) onHitBlocked.OnEventRaised -= OnBlocked;
            StopRumble(); // BẮT BUỘC: SetMotorSpeeds giữ nguyên tới khi set lại — không dọn là rung mãi.
        }

        private void OnHurt(HitData hit) => Rumble(in hit, 1f);

        private void OnBlocked(HitData hit) => Rumble(in hit, blockMultiplier);

        private void Rumble(in HitData hit, float mult)
        {
            GameObject me = root != null ? root.gameObject : gameObject;
            if (!ReferenceEquals(hit.Victim, me)) return;

            Gamepad pad = motor != null ? motor.PairedGamepad : Gamepad.current;
            if (pad == null || !pad.added) return; // keyboard-player / pad đã rút → im lặng, không lỗi.

            float scale = mult;
            if (scaleWithHitstop && referenceHitstopFrames > 0)
                scale *= Mathf.Clamp((float)hit.HitstopFrames / referenceHitstopFrames, 0.5f, 1.5f);

            float low = Mathf.Clamp01(lowFrequency * scale);
            float high = Mathf.Clamp01(highFrequency * scale);

            // Hit chồng khi đang rung cùng pad → MAX cường độ + restart thời gian (không cộng dồn).
            if (_timeLeft > 0f && ReferenceEquals(pad, _activePad))
            {
                low = Mathf.Max(low, _curLow);
                high = Mathf.Max(high, _curHigh);
            }

            _activePad = pad;
            _curLow = low;
            _curHigh = high;
            _timeLeft = Mathf.Max(0.01f, duration);
            pad.SetMotorSpeeds(low, high);
        }

        private void Update()
        {
            if (_timeLeft <= 0f) return;
            _timeLeft -= Time.deltaTime;
            if (_timeLeft <= 0f) StopRumble();
        }

        private void StopRumble()
        {
            _timeLeft = 0f;
            if (_activePad == null) return;
            if (_activePad.added) _activePad.SetMotorSpeeds(0f, 0f);
            _activePad = null;
        }
    }
}
