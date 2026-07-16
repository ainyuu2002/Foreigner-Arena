using System;
using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// CẤU HÌNH POOL (§3.4 số balance trong ScriptableObject, KHÔNG hardcode): danh sách prefab cần
    /// prewarm + giới hạn maxSize. <see cref="PrefabPool.Configure"/> đọc asset này lúc đầu trận để
    /// tạo sẵn (prewarm) N instance mỗi prefab (tránh khựng lần bắn/hit đầu) + đặt maxSize (Release
    /// vượt max → Destroy object dư thay vì phình vô hạn).
    ///
    /// Prefab KHÔNG có trong list vẫn pool được (PrefabPool tạo pool lazy với maxSize mặc định) —
    /// list chỉ để TINH CHỈNH prewarm/max cho prefab nóng (spark, đạn). Tất cả số CẦN TEST.
    /// </summary>
    [CreateAssetMenu(fileName = "PoolConfig", menuName = "FA/Core/Pool Config")]
    public sealed class PoolConfigSO : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Prefab VFX/projectile cần prewarm.")]
            public GameObject prefab;

            [Tooltip("Số instance tạo sẵn đầu trận (CẦN TEST). Spark hit/block 8-12, VFX khác 3-4, " +
                     "đạn thường 4, drill 1.")]
            [Min(0)] public int prewarm;

            [Tooltip("Trần số instance sống trong pool (CẦN TEST). Release vượt max → Destroy object dư. " +
                     "Spark 24, VFX khác 8, đạn 12, drill 2.")]
            [Min(1)] public int maxSize;
        }

        [Tooltip("Danh sách prefab prewarm + maxSize. Thêm prefab nóng = thêm phần tử.")]
        public Entry[] entries;
    }
}
