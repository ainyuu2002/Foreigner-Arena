using UnityEngine;
using FA.Core;

namespace FA.Items
{
    /// <summary>
    /// (P13) RAGE ORB (legendary, clone Smash Ball — research §3.6). Bay random trong map; cần 15 HIT CHUNG
    /// (cả 2 người cộng dồn) để phá → người gây hit thứ 15 nhận FULL nộ. Bị đánh → hitstun nhẹ + đổi hướng;
    /// counter DECAY clamp 0 (counter 0 KHÔNG biến mất — sống tới hết lifetime); tồn tại 20s thu nhỏ dần.
    ///
    /// UNITY-NATIVE (§1.1):
    ///  • Bay: Rigidbody2D Kinematic (gravityScale 0) + đặt linearVelocity random; đổi hướng định kỳ + khi
    ///    bị đánh; nảy ở mép vùng (đảo velocity) — KHÔNG tích phân vị trí tay.
    ///  • Nhận hit từ CẢ 2 người: child collider Layer Hurtbox → hitbox player va (Collision Matrix P0 lo).
    ///    Orb KHÔNG ref FA.Combat: subscribe <see cref="HitEventChannelSO"/> (OnHitLandedChannel — HitboxAuthoring
    ///    P5 raise cho MỌI hurtbox) và LỌC hit.Victim == orb (research §3.6). Đếm chung → +full nộ cho
    ///    hit.Attacker qua IRageSpender.RefundRage(1f) (Core interface, null-safe).
    ///  • Decay/lifetime/shrink: dùng Time.deltaTime trong Update cho hành vi MÔI TRƯỜNG (giống lava-rise P11,
    ///    NGOẠI LỆ hợp lệ — không phải frame-data clip nhân vật). Hitstun nhẹ đếm bằng timer.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class RageOrb : MonoBehaviour
    {
        [Header("Cấu hình")]
        [Tooltip("RageOrbConfigSO — 15 hit/decay/tốc/lifetime... BẮT BUỘC gán.")]
        [SerializeField] private RageOrbConfigSO config;

        [Tooltip("OnHitLanded (HitEventChannelSO 'OnHitLandedChannel') — CÙNG asset HitboxAuthoring raise. " +
                 "Orb lọc Victim==mình để đếm hit chung. BẮT BUỘC gán.")]
        [SerializeField] private HitEventChannelSO onHitLanded;

        [Tooltip("(Tùy) ParticleSystem phát khi PHÁ orb (Unity-native VFX). Optional.")]
        [SerializeField] private ParticleSystem breakVfx;

        private Rigidbody2D _body;
        private float _hitCount;          // counter CHUNG (float để decay mượt).
        private float _redirectTimer;
        private float _hitstunTimer;      // >0 = đang hitstun nhẹ (đứng yên).
        private float _ageTimer;
        private bool _broken;
        private Vector3 _baseScale;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;            // bay tự do, không rơi.
            _baseScale = transform.localScale;
            if (config == null) Debug.LogError("[RageOrb] Thiếu RageOrbConfigSO.", this);
        }

        private void Start() => Redirect();

        private void OnEnable()
        {
            if (onHitLanded != null) onHitLanded.OnEventRaised += OnHit;
        }

        private void OnDisable()
        {
            if (onHitLanded != null) onHitLanded.OnEventRaised -= OnHit;
        }

        private void Update()
        {
            if (config == null || _broken) return;

            // Lifetime + thu nhỏ dần (cảnh báo) → hết thì Destroy (không ai phá).
            _ageTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_ageTimer / config.lifetime);
            float scale = Mathf.Lerp(1f, config.minScaleAtEnd, t);
            transform.localScale = _baseScale * scale;
            if (_ageTimer >= config.lifetime) { Destroy(gameObject); return; }

            // Counter decay (clamp đáy 0; ở 0 KHÔNG biến mất).
            if (_hitCount > 0f)
                _hitCount = Mathf.Max(0f, _hitCount - config.counterDecayPerSecond * Time.deltaTime);

            // Hitstun nhẹ: đứng yên một lúc khi vừa bị đánh, rồi bay lại.
            if (_hitstunTimer > 0f)
            {
                _hitstunTimer -= Time.deltaTime;
                _body.linearVelocity = Vector2.zero;
                if (_hitstunTimer <= 0f) Redirect();
                return;
            }

            // Đổi hướng/tốc định kỳ.
            _redirectTimer -= Time.deltaTime;
            if (_redirectTimer <= 0f) Redirect();
        }

        private void FixedUpdate()
        {
            if (config == null || _broken) return;
            // Nảy ở mép vùng bay (đảo velocity) — giữ orb trong map (KHÔNG ref FA.Stage).
            Vector2 p = _body.position;
            Vector2 half = config.boundsSize * 0.5f;
            Vector2 min = config.boundsCenter - half;
            Vector2 max = config.boundsCenter + half;
            Vector2 v = _body.linearVelocity;
            if ((p.x <= min.x && v.x < 0f) || (p.x >= max.x && v.x > 0f)) v.x = -v.x;
            if ((p.y <= min.y && v.y < 0f) || (p.y >= max.y && v.y > 0f)) v.y = -v.y;
            _body.linearVelocity = v;
            _body.position = new Vector2(Mathf.Clamp(p.x, min.x, max.x), Mathf.Clamp(p.y, min.y, max.y));
        }

        private void Redirect()
        {
            if (config == null) return;
            float angle = Random.value * Mathf.PI * 2f;
            float speed = Random.Range(config.speedMin, config.speedMax);
            _body.linearVelocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            _redirectTimer = config.redirectInterval;
        }

        private void OnHit(HitData hit)
        {
            if (_broken || config == null) return;
            // CHỈ đếm hit vào CHÍNH orb (research §3.6). Victim đã là root (HitboxAuthoring set victimRoot).
            if (!ReferenceEquals(hit.Victim, gameObject)) return;

            _hitCount += 1f;                         // +1 hit CHUNG (không phân biệt ai).
            _hitstunTimer = config.hitstunFramesOnHit / 60f; // hitstun nhẹ (frame→giây, 60fps fixed).

            if (_hitCount >= config.hitsToBreak)
                Break(hit.Attacker);                 // tie-break frame: người gây hit thứ 15 = nhặt.
        }

        private void Break(GameObject attacker)
        {
            _broken = true;
            // FULL nộ cho người phá (research §3.4): RefundRage(1f) đã clamp ≤ max = đổ đầy.
            if (attacker != null)
                attacker.GetComponentInChildren<IRageSpender>()?.RefundRage(1f);

            if (breakVfx != null)
            {
                breakVfx.transform.SetParent(null, true);
                breakVfx.Play();
            }
            Destroy(gameObject);
        }
    }
}
