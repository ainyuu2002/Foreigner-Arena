using System.Collections.Generic;
using UnityEngine;

namespace FA.Items
{
    /// <summary>Cấu hình nhịp spawn, vùng di chuyển và bảng tỉ lệ item.</summary>
    [CreateAssetMenu(menuName = "FA/Items/Item Spawn Config", fileName = "ItemSpawnConfig")]
    public sealed class ItemSpawnConfigSO : ScriptableObject
    {
        [Header("Nhịp spawn")]
        [Min(0.5f)] public float spawnInterval = 8f;

        [Min(1)] public int maxConcurrent = 5;

        [Min(1f)] public float itemLifetime = 12f;

        [Header("Mystery Block")]
        public Vector2 mysterySpawnIntervalRange = new Vector2(30f, 40f);

        [Header("Vùng spawn an toàn")]
        public Vector2 safeRectCenter = Vector2.zero;

        public Vector2 safeRectSize = new Vector2(16f, 6f);

        public bool keepItemsInRect = true;

        [Header("Bảng item (weighted random)")]
        public List<ItemData> itemPool = new List<ItemData>();

        [Header("Vùng bay của spawner")]
        public Vector2 flyBoundsCenter = new Vector2(0f, 2.5f);

        public Vector2 flyBoundsSize = new Vector2(12f, 4f);

        public Vector2 flySpeedRange = new Vector2(1.5f, 3f);

        public Vector2 directionChangeIntervalRange = new Vector2(1.5f, 3.5f);

        /// <summary>Vùng item được phép xuất hiện và di chuyển.</summary>
        public Rect SafeRect => new Rect(
            safeRectCenter.x - safeRectSize.x * 0.5f,
            safeRectCenter.y - safeRectSize.y * 0.5f,
            safeRectSize.x, safeRectSize.y);

        /// <summary>Vùng spawner được phép bay.</summary>
        public Rect FlyBounds => new Rect(
            flyBoundsCenter.x - flyBoundsSize.x * 0.5f,
            flyBoundsCenter.y - flyBoundsSize.y * 0.5f,
            flyBoundsSize.x, flyBoundsSize.y);

        public ItemData PickRegular()
        {
            return WeightedRandom.Pick(itemPool, item => item.spawnWeight,
                item => item.type != ItemType.MysteryBlock);
        }

        public ItemData PickMystery()
        {
            return WeightedRandom.Pick(itemPool, item => item.spawnWeight,
                item => item.type == ItemType.MysteryBlock);
        }
    }
}
