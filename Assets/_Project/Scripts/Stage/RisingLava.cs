using UnityEngine;
using FA.Core;

namespace FA.Stage
{
    /// <summary>
    /// DUNG NHAM DÂNG (P11, biome Fire, research §3.5). Lava là Kinematic <see cref="Rigidbody2D"/> +
    /// BoxCollider2D trigger. Dâng bằng <c>MovePosition</c> trong FixedUpdate (Unity vật lý lo va trigger
    /// chuẩn, KHÔNG SetPosition nhảy-qua-player research §4 cạm bẫy 10). Có cảnh báo NHẤP NHÁY icon TRƯỚC khi dâng.
    ///
    /// Map đủ cao để lava lúc mới dâng không chạm người ngay (GDD). Code chỉ điều phối tốc-độ-dâng +
    /// warning blink (số ở StageConfigSO, CẦN TEST). CHẠM LAVA = CHẾT do Layer DeathZone + DeathZone.cs
    /// (gắn cùng object) lo — RisingLava KHÔNG tự xử mạng (chống cross-module §1.3).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class RisingLava : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("StageConfigSO của map Fire — lavaRiseSpeed + lavaWarningLead + lavaWarningBlinkPeriod. BẮT BUỘC.")]
        [SerializeField] private StageConfigSO config;

        [Header("Cảnh báo")]
        [Tooltip("Icon/SpriteRenderer cảnh báo nhấp nháy TRƯỚC khi lava dâng. Tùy chọn.")]
        [SerializeField] private SpriteRenderer warningIcon;

        [Header("Hành vi")]
        [Tooltip("Tự bắt đầu cảnh-báo→dâng ngay khi vào scene. Tắt để gọi BeginRising() từ event/test.")]
        [SerializeField] private bool autoStart = true;

        [Tooltip("Trần dâng tối đa (Y world). Tới đây thì dừng. Để rất cao = dâng mãi.")]
        [SerializeField] private float maxRiseY = 100f;

        private Rigidbody2D _rb;
        private enum Phase { Resting, Warning, Rising, Holding, Receding }
        private Phase _phase = Phase.Resting;
        private float _timer;
        private float _blinkTimer;
        private bool _iconOn;
        private float _baseY; // mực nghỉ ban đầu — rút xuống là về đây.

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Kinematic; // môi trường: Kinematic + MovePosition.
            _baseY = _rb != null ? transform.position.y : 0f;
            if (config == null)
                Debug.LogError("[RisingLava] Thiếu StageConfigSO.", this);
            if (warningIcon != null) warningIcon.enabled = false;
        }

        private void Start()
        {
            // CHU KỲ NGẪU NHIÊN (feedback test): nghỉ (random) → cảnh báo → DÂNG → giữ → RÚT → nghỉ → lặp.
            if (autoStart) ScheduleRest();
        }

        private void ScheduleRest()
        {
            _phase = Phase.Resting;
            float jitter = config != null ? Random.Range(-config.lavaRestJitter, config.lavaRestJitter) : 0f;
            _timer = Mathf.Max(2f, (config != null ? config.lavaRestSeconds : 12f) + jitter);
        }

        /// <summary>Bắt đầu chu kỳ: cảnh báo nhấp nháy → dâng. Gọi từ test/event nếu autoStart tắt.</summary>
        public void BeginWarning()
        {
            if (config == null) return;
            _phase = Phase.Warning;
            _timer = config.lavaWarningLead;
            _blinkTimer = 0f;
        }

        /// <summary>Bỏ qua cảnh báo, dâng ngay.</summary>
        public void BeginRising()
        {
            _phase = Phase.Rising;
            if (warningIcon != null) warningIcon.enabled = false;
        }

        private void Update()
        {
            if (config == null) return;

            switch (_phase)
            {
                case Phase.Resting:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f) BeginWarning();
                    break;

                case Phase.Warning:
                    _timer -= Time.deltaTime;
                    _blinkTimer -= Time.deltaTime;
                    if (_blinkTimer <= 0f && warningIcon != null)
                    {
                        _iconOn = !_iconOn;
                        warningIcon.enabled = _iconOn;
                        _blinkTimer = Mathf.Max(0.05f, config.lavaWarningBlinkPeriod);
                    }
                    if (_timer <= 0f)
                    {
                        if (warningIcon != null) warningIcon.enabled = false;
                        _phase = Phase.Rising;
                        Debug.Log("[RisingLava] Dung nham DÂNG!", this);
                    }
                    break;

                case Phase.Holding:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f) _phase = Phase.Receding;
                    break;
            }
        }

        private void FixedUpdate()
        {
            if (config == null) return;

            if (_phase == Phase.Rising)
            {
                if (_rb.position.y >= maxRiseY)
                {
                    _phase = Phase.Holding;
                    _timer = config.lavaHoldSeconds;
                    return;
                }
                Vector2 next = _rb.position + new Vector2(0f, config.lavaRiseSpeed * Time.fixedDeltaTime);
                _rb.MovePosition(next); // Unity lo va trigger chuẩn, không nhảy-qua player.
            }
            else if (_phase == Phase.Receding)
            {
                if (_rb.position.y <= _baseY)
                {
                    ScheduleRest(); // về mực nghỉ → chờ đợt dâng ngẫu nhiên kế tiếp.
                    return;
                }
                Vector2 next = _rb.position - new Vector2(0f, config.lavaRecedeSpeed * Time.fixedDeltaTime);
                _rb.MovePosition(next);
            }
        }
    }
}
