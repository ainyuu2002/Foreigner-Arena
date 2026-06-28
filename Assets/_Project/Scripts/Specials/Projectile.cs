using UnityEngine;
using FA.Core;

namespace FA.Specials
{
    /// <summary>
    /// CHƯỞNG (projectile) — di chuyển + despawn (P9, research §3.4). MOVEMENT = <see cref="Rigidbody2D"/>
    /// (KHÔNG tự dời transform §1.1): ProjectileSpawner set linearVelocity 1 lần lúc spawn; gravity do
    /// gravityScale của Rigidbody2D (0 = bay thẳng). DESPAWN theo 3 đường:
    ///   (a) LIFETIME timer (số balance, không phải frame-data đòn — hợp lệ giống dashDuration),
    ///   (b) RA NGOÀI tầm nhìn camera (khoảng cách từ điểm bắn vượt maxTravel),
    ///   (c) ON-HIT: nghe OnHitLandedChannel, hit có Attacker == owner sau khi chưởng đã ACTIVE → 1-hit
    ///       chưởng tự tắt (research §4.11 — tránh multi-hit ngoài ý muốn).
    /// Va TƯỜNG (layer Wall) → despawn (collision matrix lọc; trigger body chỉ kêu khi chạm Wall).
    ///
    /// Đặt ở FA.Specials, chỉ ref Core (HitData/HitEventChannelSO ở Core) — KHÔNG ref Combat (§1.3).
    /// Child Hitbox dùng HitboxAuthoring (P5) — KHÔNG đụng tới ở đây; ProjectileSpawner gán attackerRoot
    /// + hitChannel cho HitboxAuthoring lúc spawn.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Projectile : MonoBehaviour
    {
        [Header("Despawn (số balance — CẦN TEST)")]
        [Tooltip("Sống tối đa bao lâu trước khi tự tắt (giây). CẦN TEST.")]
        [Min(0.1f)] [SerializeField] private float lifetime = 2.5f;

        [Tooltip("Khoảng cách tối đa từ điểm bắn trước khi tắt (units). Phòng khi chưởng ra ngoài map.")]
        [Min(1f)] [SerializeField] private float maxTravel = 25f;

        [Tooltip("Tắt NGAY sau hit đầu tiên (1-hit chưởng). Tắt cờ này = chưởng xuyên (multi-hit).")]
        [SerializeField] private bool despawnOnHit = true;

        // Cấp lúc spawn (ProjectileSpawner.OnSpawnProjectile → Init): để lọc ON-HIT đúng "chưởng của ai".
        private GameObject _owner;
        private HitEventChannelSO _hitChannel;

        private float _age;
        private Vector2 _spawnPos;
        private bool _active;      // đã bắt đầu bay (đăng ký lifetime). Tránh despawn-on-hit trước khi spawn xong.

        /// <summary>
        /// ProjectileSpawner.OnSpawnProjectile gọi NGAY sau Instantiate: gán owner + kênh hit để lọc
        /// despawn-on-hit. Vận tốc/hướng do Spawner set riêng trên Rigidbody2D (Unity-native).
        /// </summary>
        public void Init(GameObject owner, HitEventChannelSO hitChannel)
        {
            _owner = owner;
            _hitChannel = hitChannel;
            _spawnPos = transform.position;
            _age = 0f;
            _active = true;
            if (_hitChannel != null) _hitChannel.OnEventRaised += OnHitLanded;
        }

        private void OnDisable()
        {
            // Hủy subscribe đối xứng (tránh leak/double khi pool tái dùng — P9 dùng Destroy, vẫn an toàn).
            if (_hitChannel != null) _hitChannel.OnEventRaised -= OnHitLanded;
            _active = false;
        }

        private void Update()
        {
            if (!_active) return;
            _age += Time.deltaTime;
            if (_age >= lifetime) { Despawn(); return; }
            if (((Vector2)transform.position - _spawnPos).sqrMagnitude >= maxTravel * maxTravel) Despawn();
        }

        private void OnHitLanded(HitData hit)
        {
            if (!despawnOnHit || !_active) return;
            // Chỉ tắt khi đây là chưởng CỦA owner gây ra hit (research §4.5 attribution). Nhiều chưởng
            // cùng owner = giới hạn hiếm ở P9 (cap 1 chưởng/lần đề xuất); chấp nhận đơn giản.
            if (_owner != null && ReferenceEquals(hit.Attacker, _owner)) Despawn();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Body projectile (layer ProjectileP1/P2) chỉ va Wall theo Collision Matrix → ở đây = chạm
            // tường. KHÔNG "if(layer==hurtbox)" (hit-detection là việc của child Hitbox/HitboxAuthoring).
            if (!_active) return;
            if (other.gameObject.layer == GameLayers.Wall) Despawn();
        }

        private void Despawn()
        {
            _active = false;
            // P9: Destroy đơn giản (pool = tối ưu sau, research §3.4). OnDisable gỡ subscribe.
            Destroy(gameObject);
        }
    }
}
