using System.Collections.Generic;
using UnityEngine;

namespace FA.Items
{
    /// <summary>
    /// (P13) CẤU HÌNH SPAWN ITEM (ScriptableObject — mọi số balance, §1.1 + §6 "để ngỏ → SO + CẦN TEST").
    /// ItemSpawner đọc. Vùng spawn an toàn = Rect (KHÔNG ref FA.Stage — research §3.2: người dựng map khai
    /// safe-rect 1 lần, spawner KHÔNG cần biết DeathZone ở đâu). Cũng dùng làm vùng GIỮ item trong map
    /// (quyết định người dùng #2 — chặn item trôi ra death zone).
    ///
    /// MỌI default là gợi ý hợp lý — CẦN TEST balance (chỉnh Inspector, KHÔNG sửa code).
    /// </summary>
    [CreateAssetMenu(menuName = "FA/Items/Item Spawn Config", fileName = "ItemSpawnConfig")]
    public sealed class ItemSpawnConfigSO : ScriptableObject
    {
        [Header("Nhịp spawn (CẦN TEST)")]
        [Tooltip("Giây giữa 2 lần spawn. CẦN TEST (Smash Normal ~ vài giây).")]
        [Min(0.5f)] public float spawnInterval = 8f;

        [Tooltip("Số item TỐI ĐA cùng tồn tại. GDD chốt 5.")]
        [Min(1)] public int maxConcurrent = 5;

        [Tooltip("Item tự biến mất sau N giây nếu không ai nhặt. CẦN TEST: 12s.")]
        [Min(1f)] public float itemLifetime = 12f;

        [Header("Vùng spawn an toàn (tránh death zone — KHÔNG ref FA.Stage)")]
        [Tooltip("Tâm vùng spawn (world). Đặt theo map (giữa, trên platform).")]
        public Vector2 safeRectCenter = Vector2.zero;

        [Tooltip("Kích thước vùng spawn (rộng × cao, world units). Phải nằm GỌN trong tường, xa death zone.")]
        public Vector2 safeRectSize = new Vector2(16f, 6f);

        [Tooltip("Có CLAMP item trong vùng này không (quyết định #2: chặn item trôi ra rìa). Bật = không cho mất.")]
        public bool keepItemsInRect = true;

        [Header("Kiểu xuất hiện (research §3.2)")]
        [Tooltip("Tỉ lệ RƠI-TỪ-TRÊN (vs teleport-in). 0.5 = 50/50 (default — GDD ghi cả 2 kiểu). CẦN TEST.")]
        [Range(0f, 1f)] public float dropFromAboveChance = 0.5f;

        [Tooltip("Độ cao THÊM so với mép trên safe-rect khi RƠI-TỪ-TRÊN (item rớt xuống do gravity).")]
        [Min(0f)] public float dropExtraHeight = 4f;

        [Header("Bảng item (weighted random)")]
        [Tooltip("Danh sách ItemData có thể spawn. Trọng số đọc từ ItemData.spawnWeight. Thêm item = thêm asset.")]
        public List<ItemData> itemPool = new List<ItemData>();

        /// <summary>Rect vùng an toàn (tính từ center+size). Dùng cho spawn + clamp giữ-trong-map.</summary>
        public Rect SafeRect => new Rect(
            safeRectCenter.x - safeRectSize.x * 0.5f,
            safeRectCenter.y - safeRectSize.y * 0.5f,
            safeRectSize.x, safeRectSize.y);

        /// <summary>Chọn 1 ItemData theo trọng số. Trả null nếu pool rỗng.</summary>
        public ItemData PickWeighted()
        {
            if (itemPool == null || itemPool.Count == 0) return null;
            float total = 0f;
            for (int i = 0; i < itemPool.Count; i++)
                if (itemPool[i] != null) total += Mathf.Max(0f, itemPool[i].spawnWeight);
            if (total <= 0f) return itemPool[0];

            float r = Random.value * total;
            for (int i = 0; i < itemPool.Count; i++)
            {
                if (itemPool[i] == null) continue;
                r -= Mathf.Max(0f, itemPool[i].spawnWeight);
                if (r <= 0f) return itemPool[i];
            }
            return itemPool[itemPool.Count - 1];
        }
    }
}
