using UnityEngine;
using FA.Core;

namespace FA.Stage
{
    /// <summary>
    /// DUNG NHAM DÂNG (P11, biome Fire, research §3.5) — death-zone DI ĐỘNG. Lava là Kinematic
    /// <see cref="Rigidbody2D"/> + BoxCollider2D trigger Layer DeathZone (+ DeathZone.cs để raise kênh khi
    /// player chạm). Dâng bằng <c>MovePosition</c> trong FixedUpdate (Unity vật lý lo va trigger chuẩn,
    /// KHÔNG SetPosition nhảy-qua-player research §4 cạm bẫy 10). Có cảnh báo NHẤP NHÁY icon TRƯỚC khi dâng.
    ///
    /// Map đủ cao để lava lúc mới dâng không chạm người ngay (GDD). Code chỉ điều phối tốc-độ-dâng +
    /// warning blink (số ở StageConfigSO, CẦN TEST). Chết khi chạm lava do Layer DeathZone + DeathZone.cs
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
        private enum Phase { Idle, Warning, Rising }
        private Phase _phase = Phase.Idle;
        private float _warningTimer;
        private float _blinkTimer;
        private bool _iconOn;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Kinematic; // môi trường: Kinematic + MovePosition.
            if (config == null)
                Debug.LogError("[RisingLava] Thiếu StageConfigSO.", this);
            if (warningIcon != null) warningIcon.enabled = false;
        }

        private void Start()
        {
            if (autoStart) BeginWarning();
        }

        /// <summary>Bắt đầu chu kỳ: cảnh báo nhấp nháy → dâng. Gọi từ test/event nếu autoStart tắt.</summary>
        public void BeginWarning()
        {
            if (config == null) return;
            _phase = Phase.Warning;
            _warningTimer = config.lavaWarningLead;
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
            if (_phase != Phase.Warning || config == null) return;

            _warningTimer -= Time.deltaTime;
            _blinkTimer -= Time.deltaTime;
            if (_blinkTimer <= 0f && warningIcon != null)
            {
                _iconOn = !_iconOn;
                warningIcon.enabled = _iconOn;
                _blinkTimer = Mathf.Max(0.05f, config.lavaWarningBlinkPeriod);
            }

            if (_warningTimer <= 0f)
            {
                if (warningIcon != null) warningIcon.enabled = false;
                _phase = Phase.Rising;
            }
        }

        private void FixedUpdate()
        {
            if (_phase != Phase.Rising || config == null) return;
            if (_rb.position.y >= maxRiseY) return;

            Vector2 next = _rb.position + new Vector2(0f, config.lavaRiseSpeed * Time.fixedDeltaTime);
            _rb.MovePosition(next); // Unity lo va trigger chuẩn, không nhảy-qua player.
        }
    }
}
