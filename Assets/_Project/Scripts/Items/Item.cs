using System.Collections;
using UnityEngine;
using FA.Core;

namespace FA.Items
{
    /// <summary>
    /// (P13) ITEM TRÊN MAP — pickup bằng NGỒI LÊN (giữ ↓), DÙNG NGAY (GDD §12, khác Smash cầm-tay). Gắn lên
    /// prefab item: Rigidbody2D (Dynamic, trôi/đáp do physics) + BoxCollider2D thân (PhysicsMaterial2D băng +
    /// bounciness 0 — Unity-native trượt/không-nảy, §1.1) + child trigger Layer Item (overlap người chơi).
    ///
    /// PICKUP (research §3.3): OnTriggerStay2D — người ĐẦU TIÊN thỏa (đang đè + IMoveInput.MoveY&lt;0 + KHÔNG
    /// ICombatContext.IsInHitstun) → áp hiệu ứng qua interface CORE trên ROOT người nhặt → set _consumed →
    /// Destroy. Cờ _consumed = 1 người/item (research §5). Đọc ý định ↓ qua IMoveInput (precedent
    /// DropThroughController P11) — KHÔNG ref FA.Movement, KHÔNG đọc raw input. Rage Mode nhặt bình thường
    /// (không gate gì thêm; IsInHitstun không bật khi Rage).
    ///
    /// HIỆU ỨNG áp null-safe qua Core interface (KHÔNG ref module gameplay — §1.3):
    ///  • KnockbackPotion → IPercentReceiver.AddPercent(-reduce) (clamp đáy 0).
    ///  • ManaPotion      → IManaSpender.AddMana(fraction).
    ///  • DamageBuff      → ItemDamageBuff.ApplyOrRefresh (IBuffSource, sống trên nhân vật).
    ///
    /// DESPAWN: coroutine WaitForSeconds(lifetime) → Destroy (Unity-native timer). GIỮ TRONG MAP (quyết định
    /// người dùng #2): clamp vị trí trong safe-rect (ItemSpawnConfigSO) mỗi FixedUpdate → KHÔNG trôi vào death
    /// zone. KHÔNG code va chạm sàn (Tilemap/Composite P11 lo).
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

        [Tooltip("(Tùy) ParticleSystem phát khi NHẶT (Unity-native VFX). Optional.")]
        [SerializeField] private ParticleSystem pickupVfx;

        [Header("Thông số (CẦN TEST — mặc định lấy từ config khi spawn)")]
        [Tooltip("Tự biến mất sau N giây nếu không ai nhặt. Spawner ghi đè bằng config.")]
        [Min(1f)] [SerializeField] private float lifetime = 12f;

        [Tooltip("Ngưỡng MoveY coi là 'đang ngồi/ấn xuống' (âm). Khớp DropThroughController P11.")]
        [Range(-1f, 0f)] [SerializeField] private float downThreshold = -0.5f;

        private bool _consumed;
        private bool _clampEnabled;
        private Rect _safeRect;
        private Coroutine _lifeRoutine;

        /// <summary>ItemSpawner gọi NGAY sau Instantiate: gán data + cấu hình giữ-trong-map + lifetime.</summary>
        public void Init(ItemData itemData, float itemLifetime, Rect keepInRect, bool clampToRect)
        {
            data = itemData;
            lifetime = itemLifetime;
            _safeRect = keepInRect;
            _clampEnabled = clampToRect;
            ApplyVisual();
            RestartLifetime();
        }

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            ApplyVisual();
        }

        private void OnEnable()
        {
            if (_lifeRoutine == null) RestartLifetime();
        }

        private void ApplyVisual()
        {
            if (data == null || spriteRenderer == null) return;
            spriteRenderer.color = data.tintColor;          // loại = màu (GDD).
            if (data.icon != null) spriteRenderer.sprite = data.icon;
            transform.localScale = Vector3.one * data.visualScale; // tier = size (GDD).
        }

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

        private void FixedUpdate()
        {
            if (!_clampEnabled) return;
            // GIỮ TRONG MAP (quyết định #2): clamp vào safe-rect → KHÔNG trôi (trên băng) vào death zone.
            Vector2 p = transform.position;
            float x = Mathf.Clamp(p.x, _safeRect.xMin, _safeRect.xMax);
            float y = Mathf.Clamp(p.y, _safeRect.yMin, _safeRect.yMax);
            if (!Mathf.Approximately(x, p.x) || !Mathf.Approximately(y, p.y))
                transform.position = new Vector3(x, y, transform.position.z);
        }

        // Người chơi đè LÊN item (trigger child). OnTriggerStay để chờ giữ ↓ qua nhiều frame.
        private void OnTriggerStay2D(Collider2D other)
        {
            if (_consumed || data == null) return;

            Transform root = ResolveRoot(other);
            if (root == null) return;

            // Gate ý định ↓ (ngồi lên) — qua IMoveInput (Core).
            var moveInput = root.GetComponentInChildren<IMoveInput>();
            if (moveInput == null || moveInput.MoveY > downThreshold) return;

            // Gate KHÔNG nhặt khi hitstun (research §3.3/§5) — qua ICombatContext (Core), null-safe.
            var combat = root.GetComponentInChildren<ICombatContext>();
            if (combat != null && combat.IsInHitstun) return;

            Consume(root);
        }

        private static Transform ResolveRoot(Collider2D other)
        {
            // Collider người chơi (thân/hurtbox) → root prefab nhân vật.
            if (other.attachedRigidbody != null) return other.attachedRigidbody.transform;
            return other.transform.root;
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
            switch (data.type)
            {
                case ItemType.KnockbackPotion:
                {
                    var receiver = root.GetComponentInChildren<IPercentReceiver>();
                    receiver?.AddPercent(-data.knockbackReducePercent); // âm → giảm % (clamp 0 ở implement).
                    break;
                }
                case ItemType.ManaPotion:
                {
                    var mana = root.GetComponentInChildren<IManaSpender>();
                    mana?.AddMana(data.manaAddFraction);
                    break;
                }
                case ItemType.DamageBuff:
                {
                    var buff = root.GetComponentInChildren<ItemDamageBuff>();
                    if (buff == null) buff = root.gameObject.AddComponent<ItemDamageBuff>();
                    buff.ApplyOrRefresh(data.damageMultiplier, data.damageBuffDuration, data.tintColor);
                    break;
                }
            }
        }
    }
}
