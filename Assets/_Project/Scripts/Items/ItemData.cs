using UnityEngine;

namespace FA.Items
{
    /// <summary>Loại item thường (GDD §12). Phân LOẠI bằng MÀU, phân TIER bằng SIZE.</summary>
    public enum ItemType
    {
        /// <summary>Knockback Potion — GIẢM % của người nhặt (-5/-10/-20% theo tier). IPercentReceiver.AddPercent(âm).</summary>
        KnockbackPotion = 0,

        /// <summary>Mana Potion — CỘNG mana người nhặt (+5/+10/+20% Max theo tier). IManaSpender.AddMana(fraction).</summary>
        ManaPotion = 1,

        /// <summary>DMG Buff — gắn ItemDamageBuff (IBuffSource) lên người nhặt: ×damage có thời hạn (3 tier).</summary>
        DamageBuff = 2,
    }

    /// <summary>
    /// (P13) DỮ LIỆU 1 item (ScriptableObject — §1.1 dữ liệu = SO, KHÔNG hardcode). Extensible: thêm item =
    /// thêm 1 asset, KHÔNG sửa code (Item/ItemSpawner đọc qua data này). 9 asset chuẩn = 3 loại × 3 tier.
    ///
    /// GDD §12: tier = SIZE (visualScale), loại = MÀU (tintColor — set lên SpriteRenderer item). Mọi số ở đây
    /// là DEFAULT hợp lý — CẦN TEST balance (chỉnh trong Inspector, không sửa code).
    /// </summary>
    [CreateAssetMenu(menuName = "FA/Items/Item Data", fileName = "ItemData")]
    public sealed class ItemData : ScriptableObject
    {
        [Header("Phân loại")]
        [Tooltip("Loại item — quyết định hiệu ứng khi nhặt.")]
        public ItemType type = ItemType.KnockbackPotion;

        [Tooltip("Màu item (GDD: phân LOẠI bằng màu). Đặt lên SpriteRenderer khi spawn. Đề xuất: KB=lục, Mana=lam, DMG=đỏ.")]
        public Color tintColor = Color.white;

        [Tooltip("Scale item (GDD: phân TIER bằng size). Nhỏ→lớn = tier thấp→cao. CẦN TEST: 0.8/1.0/1.3.")]
        [Min(0.1f)] public float visualScale = 1f;

        [Tooltip("(Tùy) Sprite riêng cho item. Bỏ trống = dùng sprite mặc định của prefab.")]
        public Sprite icon;

        [Header("Giá trị hiệu ứng (CẦN TEST)")]
        [Tooltip("KnockbackPotion: GIẢM bao nhiêu % (số DƯƠNG — Item sẽ AddPercent(-amount)). CẦN TEST: 5/10/20.")]
        [Min(0f)] public float knockbackReducePercent = 10f;

        [Tooltip("ManaPotion: CỘNG bao nhiêu PHẦN TRĂM MaxMana (0..1; vd 0.10 = +10%). CẦN TEST: 0.05/0.10/0.20.")]
        [Range(0f, 1f)] public float manaAddFraction = 0.10f;

        [Tooltip("DamageBuff: HỆ SỐ NHÂN damage (× — multiplicative, stack với Rage). CẦN TEST: 1.3/1.5/2.0.")]
        [Min(1f)] public float damageMultiplier = 1.5f;

        [Tooltip("DamageBuff: thời lượng buff (giây). Nhấp nháy 3s cuối. CẦN TEST: 10s.")]
        [Min(0.5f)] public float damageBuffDuration = 10f;

        [Header("Spawn (weighted random ở ItemSpawner)")]
        [Tooltip("Trọng số xuất hiện so với item khác (cao = hay ra hơn). CẦN TEST.")]
        [Min(0f)] public float spawnWeight = 1f;
    }
}
