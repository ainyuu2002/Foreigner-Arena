using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FA.Core;

namespace FA.Items
{
    /// <summary>Điều khiển rơi, nhặt, áp effect và trả item về pool.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Item : MonoBehaviour, IPoolable
    {
        [Header("Dữ liệu")]
        [SerializeField] private ItemData data;

        [Header("Tham chiếu")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [SerializeField] private Animator visualAnimator;

        [SerializeField] private ParticleSystem pickupVfx;

        [Header("Đáp đất (CẦN TEST)")]
        [Range(0f, 1f)] [SerializeField] private float minGroundNormalY = 0.5f;

        [Min(0f)] [SerializeField] private float maxLandingSpeedY = 0.2f;

        [Header("Thông số (CẦN TEST — mặc định lấy từ config khi spawn)")]
        [Min(1f)] [SerializeField] private float lifetime = 12f;

        [Tooltip("Ngưỡng MoveY coi là 'đang ngồi/ấn xuống' (âm). Khớp CrouchDownThreshold của Motor.")]
        [Range(-1f, 0f)] [SerializeField] private float downThreshold = -0.5f;

        [Header("Hiệu ứng (CẦN TEST)")]
        [SerializeField] private Color statusTint = new Color(1f, 0.35f, 0.25f, 1f);

        [SerializeField] private Vector2 trapKnockbackForce = new Vector2(5f, 8f);

        private Rigidbody2D _body;
        private bool _consumed;
        private bool _landed;
        private bool _clampEnabled;
        private Rect _safeRect;
        private Coroutine _lifeRoutine;
        private bool _returningToPool;
        private Transform _pickupVfxParent;
        private Vector3 _pickupVfxLocalPosition;
        private Quaternion _pickupVfxLocalRotation;
        private Vector3 _pickupVfxLocalScale;
        private Sprite _defaultSprite;

        public event System.Action<Item> Finished;

        private readonly List<PickupCandidate> _overlaps = new List<PickupCandidate>(2);

        /// <summary>ItemSpawner gọi NGAY sau PrefabPool.Spawn: gán data + cấu hình giữ-trong-map + lifetime.</summary>
        public void Init(ItemData itemData, float itemLifetime, Rect keepInRect, bool clampToRect)
        {
            data = itemData;
            lifetime = itemLifetime;
            _safeRect = keepInRect;
            _clampEnabled = clampToRect;
            ApplyVisual();
            ResetForDrop();
            RestartLifetime();
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (visualAnimator == null) visualAnimator = GetComponentInChildren<Animator>(true);
            if (spriteRenderer != null) _defaultSprite = spriteRenderer.sprite;
            CachePickupVfxTransform();
            ApplyVisual();
            ResetForDrop();
        }

        private void Start()
        {
            if (data != null && _lifeRoutine == null) RestartLifetime();
        }

        // ---- Rơi / đáp (Unity-native physics) ----

        private void ResetForDrop()
        {
            if (_body == null) _body = GetComponent<Rigidbody2D>();
            _landed = false;

            if (_body != null)
            {
                _body.bodyType = RigidbodyType2D.Dynamic;
                _body.gravityScale = 1f;
                _body.linearVelocity = Vector2.zero;
                _body.angularVelocity = 0f;
                _body.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            if (visualAnimator != null)
            {
                visualAnimator.enabled = false;
                visualAnimator.transform.localRotation = Quaternion.identity;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision) => TryLand(collision);
        private void OnCollisionStay2D(Collision2D collision) => TryLand(collision);

        private void TryLand(Collision2D collision)
        {
            if (_landed || _body == null) return;
            if (Mathf.Abs(_body.linearVelocity.y) > maxLandingSpeedY) return;

            for (int i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y < minGroundNormalY) continue;
                Land();
                return;
            }
        }

        private void Land()
        {
            _landed = true;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _body.gravityScale = 0f;
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.constraints = RigidbodyConstraints2D.FreezeAll;

            if (visualAnimator != null)
            {
                visualAnimator.enabled = true;
                visualAnimator.Play(0, 0, 0f);
            }
        }

        private void FixedUpdate()
        {
            // CLAMP giữ-trong-map CHỈ khi còn Dynamic (đang rơi/trượt). Sau Land() (Kinematic) thì bỏ.
            if (!_clampEnabled || _landed) return;

            Vector2 p = transform.position;
            float x = Mathf.Clamp(p.x, _safeRect.xMin, _safeRect.xMax);
            float y = Mathf.Clamp(p.y, _safeRect.yMin, _safeRect.yMax);
            if (!Mathf.Approximately(x, p.x) || !Mathf.Approximately(y, p.y))
                transform.position = new Vector3(x, y, transform.position.z);
        }

        // ---- Visual ----

        private void ApplyVisual()
        {
            if (data == null || spriteRenderer == null) return;
            spriteRenderer.color = data.tintColor;              // loại = màu (GDD).
            spriteRenderer.sprite = data.icon != null ? data.icon : _defaultSprite;
            transform.localScale = Vector3.one * data.visualScale; // tier = size (GDD).
        }

        // ---- Lifetime ----

        private void RestartLifetime()
        {
            if (_lifeRoutine != null) StopCoroutine(_lifeRoutine);
            _lifeRoutine = StartCoroutine(LifetimeRoutine());
        }

        private IEnumerator LifetimeRoutine()
        {
            yield return new WaitForSeconds(lifetime);
            _lifeRoutine = null;
            if (!_consumed) Despawn();
        }

        // ---- Pickup (Enter/Exit + poll trong Update — miễn nhiễm body-sleep) ----

        private void OnTriggerEnter2D(Collider2D other)
        {
            Transform root = ResolvePlayerRoot(other);
            if (root == null) return;

            PickupCandidate candidate = FindCandidate(root);
            if (candidate == null)
            {
                candidate = new PickupCandidate(root);
                _overlaps.Add(candidate);
            }
            candidate.OverlapCount++;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Transform root = ResolvePlayerRoot(other);
            if (root == null) return;

            PickupCandidate candidate = FindCandidate(root);
            if (candidate == null) return;
            candidate.OverlapCount--;
            if (candidate.OverlapCount <= 0) _overlaps.Remove(candidate);
        }

        private void Update()
        {
            if (_consumed || data == null || _overlaps.Count == 0) return;

            Transform picker = FindQualifyingPicker();
            if (picker != null) Consume(picker);
        }

        private Transform FindQualifyingPicker()
        {
            for (int i = _overlaps.Count - 1; i >= 0; i--)
            {
                PickupCandidate candidate = _overlaps[i];
                if (candidate.Root == null)
                {
                    _overlaps.RemoveAt(i);
                    continue;
                }

                if (candidate.MoveInput == null || candidate.MoveInput.MoveY > downThreshold) continue;
                if (candidate.Combat != null && candidate.Combat.IsInHitstun) continue;
                return candidate.Root;
            }

            return null;
        }

        private PickupCandidate FindCandidate(Transform root)
        {
            for (int i = 0; i < _overlaps.Count; i++)
                if (_overlaps[i].Root == root) return _overlaps[i];
            return null;
        }

        private static Transform ResolvePlayerRoot(Collider2D other)
        {
            int layer = other.attachedRigidbody != null
                ? other.attachedRigidbody.gameObject.layer
                : other.gameObject.layer;
            if (layer != GameLayers.Player1 && layer != GameLayers.Player2) return null;

            return other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform.root;
        }

        private void Consume(Transform root)
        {
            _consumed = true; // chốt 1 người/item NGAY (frame sau bị chặn).
            ApplyEffect(root);

            if (pickupVfx != null)
            {
                pickupVfx.transform.SetParent(null, true); // tách để particle sống tiếp khi root trở về pool.
                pickupVfx.Play();
            }
            Despawn();
        }

        public void Despawn()
        {
            if (_returningToPool) return;
            _returningToPool = true;
            if (_lifeRoutine != null) StopCoroutine(_lifeRoutine);
            _lifeRoutine = null;
            Finished?.Invoke(this);
            Finished = null;
            PrefabPool.Despawn(gameObject);
        }

        public void OnSpawned()
        {
            _returningToPool = false;
            _consumed = false;
            _landed = false;
            _clampEnabled = false;
            _overlaps.Clear();
            if (_lifeRoutine != null) StopCoroutine(_lifeRoutine);
            _lifeRoutine = null;
            RestorePickupVfx();
            ResetForDrop();
        }

        public void OnDespawned()
        {
            if (_lifeRoutine != null) StopCoroutine(_lifeRoutine);
            _lifeRoutine = null;
            _overlaps.Clear();
            if (_body != null)
            {
                _body.linearVelocity = Vector2.zero;
                _body.angularVelocity = 0f;
            }
            Finished = null;
        }

        private void CachePickupVfxTransform()
        {
            if (pickupVfx == null) return;
            Transform t = pickupVfx.transform;
            _pickupVfxParent = t.parent;
            _pickupVfxLocalPosition = t.localPosition;
            _pickupVfxLocalRotation = t.localRotation;
            _pickupVfxLocalScale = t.localScale;
        }

        private void RestorePickupVfx()
        {
            if (pickupVfx == null) return;
            pickupVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Transform t = pickupVfx.transform;
            t.SetParent(_pickupVfxParent != null ? _pickupVfxParent : transform, false);
            t.localPosition = _pickupVfxLocalPosition;
            t.localRotation = _pickupVfxLocalRotation;
            t.localScale = _pickupVfxLocalScale;
        }

        private void ApplyEffect(Transform root)
        {
            // Item thường → effect cố định; Mystery → roll bảng. Cùng 1 đường applier.
            ItemEffect effect = data.BuildEffect();
            Color tint = data.type == ItemType.DamageBuff ? data.tintColor : statusTint;
            ItemEffectApplier.Apply(root, effect, tint, trapKnockbackForce);
            ItemPickupNotice.ShowPickup(root, data, effect);
        }

        private sealed class PickupCandidate
        {
            public readonly Transform Root;
            public readonly IMoveInput MoveInput;
            public readonly ICombatContext Combat;
            public int OverlapCount;

            public PickupCandidate(Transform root)
            {
                Root = root;
                MoveInput = root.GetComponentInChildren<IMoveInput>();
                Combat = root.GetComponentInChildren<ICombatContext>();
            }
        }
    }
}
