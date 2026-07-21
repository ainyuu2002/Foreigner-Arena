using UnityEngine;

namespace FA.Items
{
    /// <summary>Loại item dùng cho hiển thị và phân nhóm.</summary>
    public enum ItemType
    {
        /// <summary>Bình hồi kháng lực.</summary>
        KnockbackPotion = 0,

        /// <summary>Bình hồi mana.</summary>
        ManaPotion = 1,

        /// <summary>Bùa tăng sát thương.</summary>
        DamageBuff = 2,

        /// <summary>Khối bí ẩn chọn một effect ngẫu nhiên.</summary>
        MysteryBlock = 3,
    }

    /// <summary>Dữ liệu hiển thị, effect, trọng số và prefab của một item.</summary>
    [CreateAssetMenu(menuName = "FA/Items/Item Data", fileName = "ItemData")]
    public sealed class ItemData : ScriptableObject
    {
        [Header("Phân loại")]
        public ItemType type = ItemType.KnockbackPotion;

        public Color tintColor = Color.white;

        [Min(0.1f)] public float visualScale = 1f;

        public Sprite icon;

        [Header("Hiệu ứng")]
        [SerializeField] private string displayName;
        [SerializeField] private ItemEffectData effect;

        [Header("Spawn (weighted random ở ItemSpawner)")]
        [Min(0f)] public float spawnWeight = 1f;

        public GameObject prefabOverride;

        [Header("Mystery Block (chỉ dùng khi type = MysteryBlock)")]
        public MysteryBlockOutcomeConfigSO mysteryOutcomeConfig;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? name.Replace('_', ' ')
            : displayName;

        /// <summary>Tạo effect khi người chơi nhặt item.</summary>
        public ItemEffect BuildEffect()
        {
            if (type == ItemType.MysteryBlock && mysteryOutcomeConfig != null)
                return mysteryOutcomeConfig.PickRandomEffect();

            return effect.Build();
        }
    }
}
