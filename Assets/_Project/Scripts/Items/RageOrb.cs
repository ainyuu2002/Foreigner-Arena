using UnityEngine;
using FA.Core;
using RageOrbFinished = System.Action<FA.Items.RageOrb>;

namespace FA.Items
{
    /// <summary>Orb bay trong map, nhận hit và nạp đầy nộ cho người phá.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class RageOrb : MonoBehaviour, IPoolable
    {
        [Header("Cấu hình")]
        [SerializeField] private RageOrbConfigSO config;

        [SerializeField] private HitEventChannelSO onHitLanded;

        [SerializeField] private ParticleSystem breakVfx;

        [SerializeField] private SpriteRenderer visual;

        [SerializeField] private RageOrbVfx vfx;

        private Rigidbody2D _body;
        private float _hitCount;
        private float _redirectTimer;
        private float _hitstunTimer;
        private float _decayDelayTimer;
        private float _hitPulseTimer;
        private float _finishDelayTimer;
        private float _ageTimer;
        private bool _broken;
        private Vector3 _baseScale;

        public event RageOrbFinished Finished;
        public float HitProgress => config != null ? Mathf.Clamp01(_hitCount / config.hitsToBreak) : 0f;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.gravityScale = 0f;
            _baseScale = transform.localScale;
            if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
            if (vfx == null) vfx = GetComponent<RageOrbVfx>();
            if (config == null) Debug.LogError("[RageOrb] Thiếu RageOrbConfigSO.", this);
        }

        private void Start()
        {
            // Instance tạo trực tiếp (không qua pool) vẫn khởi tạo đúng.
            if (_redirectTimer <= 0f) Redirect();
        }

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
            if (_finishDelayTimer > 0f)
            {
                _finishDelayTimer -= Time.deltaTime;
                if (_finishDelayTimer <= 0f) FinishImmediately();
                return;
            }
            if (config == null || _broken) return;

            // Thu nhỏ dần theo thời gian tồn tại.
            _ageTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_ageTimer / config.lifetime);
            float lifetimeScale = Mathf.Lerp(1f, config.minScaleAtEnd, t);
            float hitPulse = _hitPulseTimer > 0f ? 1f + 0.12f * (_hitPulseTimer / 0.12f) : 1f;
            transform.localScale = _baseScale * lifetimeScale * hitPulse;
            if (_hitPulseTimer > 0f) _hitPulseTimer -= Time.deltaTime;
            if (visual != null)
            {
                visual.transform.Rotate(0f, 0f, config.visualSpinDegreesPerSecond * Time.deltaTime);
                visual.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.2f, 1f), HitProgress);
            }
            if (vfx != null) vfx.SetProgress(HitProgress);
            if (_ageTimer >= config.lifetime) { FinishImmediately(); return; }

            // Giảm tiến độ nếu orb lâu không bị đánh.
            if (_decayDelayTimer > 0f)
                _decayDelayTimer -= Time.deltaTime;
            else if (_hitCount > 0f)
                _hitCount = Mathf.Max(0f, _hitCount - config.counterDecayPerSecond * Time.deltaTime);

            // Dừng ngắn sau khi trúng đòn.
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

            Vector2 p = _body.position;
            Vector2 v = _body.linearVelocity;
            ResolveWorldCollision(ref p, ref v);

            // Trừ bán kính để toàn bộ orb luôn nằm trong map.
            float radius = Mathf.Max(0.05f, config.worldCollisionRadius);
            Vector2 half = Vector2.Max(config.boundsSize * 0.5f - Vector2.one * radius,
                                      Vector2.one * 0.01f);
            Vector2 min = config.boundsCenter - half;
            Vector2 max = config.boundsCenter + half;
            Vector2 projected = p + v * Time.fixedDeltaTime;
            if ((projected.x <= min.x && v.x < 0f) || (projected.x >= max.x && v.x > 0f)) v.x = -v.x;
            if ((projected.y <= min.y && v.y < 0f) || (projected.y >= max.y && v.y > 0f)) v.y = -v.y;
            _body.linearVelocity = v;
            _body.position = new Vector2(Mathf.Clamp(p.x, min.x, max.x), Mathf.Clamp(p.y, min.y, max.y));
        }

        private void ResolveWorldCollision(ref Vector2 position, ref Vector2 velocity)
        {
            float speed = velocity.magnitude;
            if (speed <= 0.001f) return;

            Vector2 direction = velocity / speed;
            float radius = Mathf.Max(0.05f, config.worldCollisionRadius);
            float skin = Mathf.Max(0f, config.worldCollisionSkin);
            float distance = speed * Time.fixedDeltaTime + skin;
            int worldMask = GameLayers.GroundMask | GameLayers.WallMask;
            RaycastHit2D hit = Physics2D.CircleCast(position, radius, direction, distance, worldMask);
            if (hit.collider == null) return;

            Vector2 normal = hit.normal.sqrMagnitude > 0.001f ? hit.normal.normalized : -direction;
            if (Vector2.Dot(velocity, normal) >= 0f) return;

            position += direction * Mathf.Max(0f, hit.distance - skin);
            position += normal * skin;
            velocity = Vector2.Reflect(velocity, normal);
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
            // Chỉ nhận hit hướng tới chính orb này.
            if (!ReferenceEquals(hit.Victim, gameObject)) return;

            _hitCount += 1f;
            _hitstunTimer = config.hitstunFramesOnHit / 60f;
            _decayDelayTimer = config.counterDecayDelay;
            _hitPulseTimer = 0.12f;
            if (vfx != null) vfx.PlayHit(HitProgress);

            if (_hitCount >= config.hitsToBreak)
                Break(hit.Attacker);
        }

        private void Break(GameObject attacker)
        {
            _broken = true;
            if (attacker != null)
                attacker.GetComponentInChildren<IRageSpender>()?.RefundRage(1f);

            if (breakVfx != null)
            {
                breakVfx.transform.SetParent(null, true);
                breakVfx.Play();
            }
            if (vfx != null) vfx.PlayBreak();
            _body.linearVelocity = Vector2.zero;
            _finishDelayTimer = 0.45f;
        }

        /// <summary>Spawner gọi khi trận kết thúc/rematch để dọn orb đang hoạt động.</summary>
        public void Despawn() => FinishImmediately();

        private void FinishImmediately()
        {
            if (!_broken) _broken = true;
            _body.linearVelocity = Vector2.zero;
            Finished?.Invoke(this);
            Finished = null;
            PrefabPool.Despawn(gameObject);
        }

        public void OnSpawned()
        {
            _hitCount = 0f;
            _redirectTimer = 0f;
            _hitstunTimer = 0f;
            _decayDelayTimer = 0f;
            _hitPulseTimer = 0f;
            _finishDelayTimer = 0f;
            _ageTimer = 0f;
            _broken = false;
            transform.localScale = _baseScale;
            if (visual != null) visual.color = Color.white;
            if (vfx != null) vfx.ResetVfx();
            Redirect();
        }

        public void OnDespawned()
        {
            _body.linearVelocity = Vector2.zero;
            _finishDelayTimer = 0f;
            if (vfx != null) vfx.StopVfx();
            Finished = null;
        }
    }
}
