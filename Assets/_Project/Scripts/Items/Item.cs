using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FA.Core;

namespace FA.Items
{
    /// <summary>
    /// (Hợp nhất item) ITEM TRÊN MAP — 1 hệ DUY NHẤT cho bình thuốc LẪN Mystery Block (Mystery chỉ là item
    /// type đặc biệt roll hiệu ứng). Gắn lên prefab item: Rigidbody2D (Dynamic khi rơi) + collider thân +
    /// child trigger Layer Item.
    ///
    /// VÒNG ĐỜI: rơi ĐỨNG (FreezeRotation) → chạm đất đủ điều kiện (normal.y ≥ minGroundNormalY, |vy| ≤
    /// maxLandingSpeedY) → <see cref="Land"/>: velocity 0, gravity 0, Kinematic, FreezeAll, bật anim đáp
    /// (visualAnimator, nếu có — Mystery flip). CLAMP giữ-trong-map CHỈ khi CÒN Dynamic (bỏ sau khi Kinematic).
    ///
    /// PICKUP (VÁ BUG SLEEP): dùng OnTriggerEnter2D/OnTriggerExit2D giữ ref player đang overlap + POLL crouch
    /// trong Update() (đọc IMoveInput.MoveY mỗi frame) ⇒ miễn nhiễm body-sleep (KHÔNG dùng OnTriggerStay).
    /// Người nhặt phải: đè trigger + giữ ↓ (MoveY ≤ downThreshold) + KHÔNG ICombatContext.IsInHitstun.
    /// Consume-once (_consumed) = 1 người/item. Áp hiệu ứng qua <see cref="ItemEffectApplier"/> CHUNG:
    /// item thường = <see cref="ItemData.BuildEffect"/> cố định; Mystery = roll bảng.
    ///
    /// DESPAWN: coroutine WaitForSeconds(lifetime) → Destroy. Đọc ý định ↓ qua IMoveInput (KHÔNG ref
    /// FA.Movement, KHÔNG raw input). Hiệu ứng qua interface Core (KHÔNG ref module gameplay).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Item : MonoBehaviour
    {
        [Header("Dữ liệu")]
        [Tooltip("ItemData (loại/tier/giá trị). ItemSpawner gán qua Init; có thể gán tay để test 1 item.")]
        [SerializeField] private ItemData data;

        [Header("Tham chiếu")]
        [Tooltip("SpriteRenderer item — đặt màu (loại) + scale (tier) theo ItemData. Bỏ trống = lấy children.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("(Tùy) Animator anim ĐÁP (Mystery flip). Bỏ trống = lấy child. Tắt tới khi Land().")]
        [SerializeField] private Animator visualAnimator;

        [Tooltip("(Tùy) ParticleSystem phát khi NHẶT (Unity-native VFX). Optional.")]
        [SerializeField] private ParticleSystem pickupVfx;

        [Header("Đáp đất (CẦN TEST)")]
        [Tooltip("normal.y tối thiểu của mặt va chạm để coi là 'đất' (≥ = sàn phẳng). CẦN TEST: 0.5.")]
        [Range(0f, 1f)] [SerializeField] private float minGroundNormalY = 0.5f;

        [Tooltip("|vận tốc y| tối đa khi chạm để khoá (tránh khoá lúc còn nảy mạnh). CẦN TEST: 0.2.")]
        [Min(0f)] [SerializeField] private float maxLandingSpeedY = 0.2f;

        [Header("Thông số (CẦN TEST — mặc định lấy từ config khi spawn)")]
        [Tooltip("Tự biến mất sau N giây nếu không ai nhặt. Spawner ghi đè bằng config.")]
        [Min(1f)] [SerializeField] private float lifetime = 12f;

        [Tooltip("Ngưỡng MoveY coi là 'đang ngồi/ấn xuống' (âm). Khớp DropThroughController P11.")]
        [Range(-1f, 0f)] [SerializeField] private float downThreshold = -0.5f;

        [Header("Hiệu ứng (CẦN TEST)")]
        [Tooltip("Màu tint cho status có thời hạn từ Mystery (DMG/slow/weather). Item DMG thường dùng ItemData.tintColor.")]
        [SerializeField] private Color statusTint = new Color(1f, 0.35f, 0.25f, 1f);

        [Tooltip("Lực đẩy khi trúng bẫy Mystery (TrapBall). x lật theo hướng mặt nhân vật. CẦN TEST.")]
        [SerializeField] private Vector2 trapKnockbackForce = new Vector2(5f, 8f);

        private Rigidbody2D _body;
        private bool _consumed;
        private bool _landed;
        private bool _clampEnabled;
        private Rect _safeRect;
        private Coroutine _lifeRoutine;

        // Người chơi đang đè trigger — refcount theo root (miễn nhiễm body-sleep vs OnTriggerStay).
        private readonly Dictionary<Transform, int> _overlaps = new Dictionary<Transform, int>();

        /// <summary>ItemSpawner gọi NGAY sau Instantiate: gán data + cấu hình giữ-trong-map + lifetime.</summary>
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
            ApplyVisual();
            ResetForDrop();
        }

        private void OnEnable()
        {
            if (_lifeRoutine == null) RestartLifetime();
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
            if (data.icon != null) spriteRenderer.sprite = data.icon;
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
            if (!_consumed) Destroy(gameObject);
        }

        // ---- Pickup (Enter/Exit + poll trong Update — miễn nhiễm body-sleep) ----

        private void OnTriggerEnter2D(Collider2D other)
        {
            Transform root = ResolvePlayerRoot(other);
            if (root == null) return;
            _overlaps.TryGetValue(root, out int n);
            _overlaps[root] = n + 1;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            Transform root = ResolvePlayerRoot(other);
            if (root == null) return;
            if (!_overlaps.TryGetValue(root, out int n)) return;
            if (n <= 1) _overlaps.Remove(root);
            else _overlaps[root] = n - 1;
        }

        private void Update()
        {
            if (_consumed || data == null || _overlaps.Count == 0) return;

            Transform picker = FindQualifyingPicker();
            if (picker != null) Consume(picker);
        }

        private Transform FindQualifyingPicker()
        {
            // Prune root đã bị Destroy + tìm người đầu tiên: giữ ↓ và KHÔNG hitstun.
            Transform result = null;
            List<Transform> stale = null;

            foreach (var kv in _overlaps)
            {
                Transform root = kv.Key;
                if (root == null)
                {
                    (stale ??= new List<Transform>()).Add(root);
                    continue;
                }

                var moveInput = root.GetComponentInChildren<IMoveInput>();
                if (moveInput == null || moveInput.MoveY > downThreshold) continue;

                var combat = root.GetComponentInChildren<ICombatContext>();
                if (combat != null && combat.IsInHitstun) continue;

                result = root;
                break;
            }

            if (stale != null)
                for (int i = 0; i < stale.Count; i++) _overlaps.Remove(stale[i]);

            return result;
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
                pickupVfx.transform.SetParent(null, true); // tách để particle sống tiếp sau Destroy.
                pickupVfx.Play();
            }
            Destroy(gameObject);
        }

        private void ApplyEffect(Transform root)
        {
            // Item thường → effect cố định; Mystery → roll bảng. Cùng 1 đường applier.
            ItemEffect effect = data.BuildEffect();
            Color tint = data.type == ItemType.DamageBuff ? data.tintColor : statusTint;
            ItemEffectApplier.Apply(root, effect, tint, trapKnockbackForce);
            ShowPickupFeedback(root, effect);
        }

        /// <summary>Thông báo HUD: người chơi, item và kết quả roll nếu là Mystery Block.</summary>
        private void ShowPickupFeedback(Transform root, ItemEffect effect)
        {
            string who = root.gameObject.layer == GameLayers.Player2 ? "P2" : "P1";
            string itemName = data.type switch
            {
                ItemType.KnockbackPotion => $"BÌNH HỒI {effect.value:0.#}% KHÁNG LỰC",
                ItemType.ManaPotion => $"BÌNH MANA +{effect.value * 100f:0.#}%",
                ItemType.DamageBuff => $"BÙA TĂNG SÁT THƯƠNG +{effect.value * 100f:0.#}%" +
                                       (effect.duration > 0f ? $" • {effect.duration:0.#} GIÂY" : string.Empty),
                ItemType.MysteryBlock => $"KHỐI BÍ ẨN  →  {DescribeMysteryEffect(effect)}",
                _ => data.name.Replace('_', ' '),
            };
            ItemPickupNotice.Show($"{who} NHẶT: {itemName}");
        }

        private static string DescribeMysteryEffect(ItemEffect effect)
        {
            string duration = effect.duration > 0f ? $" • {effect.duration:0.#} GIÂY" : string.Empty;
            switch (effect.type)
            {
                case ItemEffectType.ResistanceRecovery:
                    return $"BUFF • HỒI {effect.value:0.#}% KHÁNG LỰC";
                case ItemEffectType.ManaRecovery:
                    return $"BUFF • +{effect.value * 100f:0.#}% MANA";
                case ItemEffectType.WeatherImmunity:
                    return $"BUFF • MIỄN NHIỄM THỜI TIẾT{duration}";
                case ItemEffectType.ResistanceIncreaseDebuff:
                    return $"DEBUFF • +{effect.value:0.#}% KNOCKBACK";
                case ItemEffectType.SlowFall:
                    return $"BUFF • GIẢM {effect.value * 100f:0.#}% TRỌNG LỰC{duration}";
                case ItemEffectType.DamageBuff:
                    return $"BUFF • +{effect.value * 100f:0.#}% SÁT THƯƠNG{duration}";
                case ItemEffectType.TrapBall:
                    return $"DEBUFF • BẪY HẤT VĂNG +{effect.value * 100f:0.#}%";
                default:
                    return effect.type.ToString();
            }
        }
    }
}
