using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace FA.Core
{
    /// <summary>
    /// SERVICE object-pool DÙNG CHUNG (§3.1 Unity-native: bọc <see cref="ObjectPool{T}"/> của Unity,
    /// KHÔNG tự viết stack). Thay <c>Instantiate</c>/<c>Destroy</c> cho VFX one-shot + projectile bằng
    /// <see cref="Spawn"/>/<see cref="Despawn"/> để tránh GC spike + hitch khi bắn/hit liên tục.
    ///
    /// Semantics (bám research): Get() ưu tiên object vừa Release rồi mới createFunc; Release() vượt
    /// maxSize → actionOnDestroy hủy object dư; collectionCheck BẬT (bắt double-release, chỉ Editor);
    /// KHÔNG thread-safe (chỉ gọi main thread). Callback:
    ///   createFunc      = Instantiate + gắn <see cref="PooledInstance"/> (cache state gốc + prefab key),
    ///   actionOnGet     = SetActive(true) + PooledInstance.OnGet (RESTORE tuyệt đối scale/gravity/velocity),
    ///   actionOnRelease = PooledInstance.OnRelease (IPoolable) + SetActive(false),
    ///   actionOnDestroy = Object.Destroy.
    /// Prewarm = Get N rồi Release N (Unity không có API sẵn). SCOPED THEO TRẬN: <see cref="Clear"/>
    /// khi đổi map/kết trận (tránh giữ ref object đã Destroy theo scene).
    ///
    /// Static (không cần serialize ref ở mọi caller → giữ decoupling §1.3): mọi module ref FA.Core gọi
    /// trực tiếp. Container giữ instance gọn hierarchy + DontDestroyOnLoad để additive-unload map không
    /// giết object đang bay giữa chừng (Clear mới dọn).
    /// </summary>
    public static class PrefabPool
    {
        private const int DefaultMaxSize = 32;

        private static readonly Dictionary<GameObject, ObjectPool<GameObject>> s_pools =
            new Dictionary<GameObject, ObjectPool<GameObject>>();
        private static readonly Dictionary<GameObject, int> s_maxSizes =
            new Dictionary<GameObject, int>();

        private static Transform s_container;

        /// <summary>Reset static giữa các lần Play khi "Enter Play Mode Options" tắt Domain Reload
        /// (nếu bật thì static không sống sót qua domain reload nên method này vô hại/không cần thiết).
        /// KHÔNG Destroy s_container ở đây: lúc SubsystemRegistration chạy, container cũ (nếu có) đã
        /// tiêu theo domain trước đó rồi — chỉ null hoá ref để tránh giữ object đã chết.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_pools.Clear();
            s_maxSizes.Clear();
            s_container = null;
        }

        private static Transform Container
        {
            get
            {
                if (s_container == null)
                {
                    var go = new GameObject("[PrefabPool]");
                    Object.DontDestroyOnLoad(go);
                    s_container = go.transform;
                }
                return s_container;
            }
        }

        /// <summary>
        /// Đọc <see cref="PoolConfigSO"/> → prewarm mỗi prefab với maxSize khai báo. KHÔNG idempotent
        /// về Prewarm: gọi lại luôn chạy Get/Release <c>e.prewarm</c> lần nữa dù pool đã tồn tại — CHỦ
        /// Ý (gọi mỗi đầu trận sau <see cref="Clear"/> để prewarm lại từ đầu, vì Clear đã dọn sạch pool
        /// cũ). GetOrCreatePool bên trong mới là phần idempotent (tạo pool 1 lần, lần sau trả về pool
        /// có sẵn). null config → bỏ qua.
        /// </summary>
        public static void Configure(PoolConfigSO config)
        {
            if (config == null || config.entries == null) return;
            foreach (PoolConfigSO.Entry e in config.entries)
            {
                if (e.prefab == null) continue;
                s_maxSizes[e.prefab] = Mathf.Max(1, e.maxSize);
                GetOrCreatePool(e.prefab);
                if (e.prewarm > 0) Prewarm(e.prefab, e.prewarm);
            }
        }

        /// <summary>Tạo sẵn <paramref name="count"/> instance (Get rồi Release ngay) để lần Spawn thật
        /// đầu tiên không phải Instantiate (tránh hitch). Không vượt maxSize (Release dư sẽ Destroy).
        /// KHÔNG idempotent: mỗi lần gọi Get/Release đủ <paramref name="count"/> lần nữa (xem
        /// <see cref="Configure"/>) — chủ ý để prewarm lại mỗi đầu trận sau <see cref="Clear"/>.</summary>
        public static void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;
            ObjectPool<GameObject> pool = GetOrCreatePool(prefab);
            var temp = new List<GameObject>(count);
            for (int i = 0; i < count; i++) temp.Add(pool.Get());
            for (int i = 0; i < temp.Count; i++) pool.Release(temp[i]);
        }

        /// <summary>Lấy 1 instance từ pool của <paramref name="prefab"/> (tạo pool lazy nếu chưa có —
        /// backward-compat: prefab chưa khai trong PoolConfigSO vẫn pool được với maxSize mặc định).
        /// Đặt parent + position + rotation TUYỆT ĐỐI (không kế thừa của lần dùng trước).</summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation,
                                       Transform parent = null)
        {
            if (prefab == null) return null;
            ObjectPool<GameObject> pool = GetOrCreatePool(prefab);
            GameObject go = pool.Get();
            Transform t = go.transform;
            t.SetParent(parent != null ? parent : Container, false);
            t.position = position;
            t.rotation = rotation;
            return go;
        }

        /// <summary>Trả instance về pool nguồn. KHÔNG có marker (không do pool tạo) → Destroy thường
        /// (fallback backward-compat). Reparent về Container trước Release để không chết theo parent tạm.</summary>
        public static void Despawn(GameObject instance)
        {
            if (instance == null) return;
            var marker = instance.GetComponent<PooledInstance>();
            if (marker == null || marker.SourcePrefab == null ||
                !s_pools.TryGetValue(marker.SourcePrefab, out ObjectPool<GameObject> pool))
            {
                Object.Destroy(instance);
                return;
            }
            instance.transform.SetParent(Container, false);
            pool.Release(instance);
        }

        /// <summary>Dọn TOÀN BỘ pool (kết trận/đổi map): Clear từng pool (Destroy object đang nằm chờ)
        /// + hủy Container (giết luôn object đang active parent dưới Container) + xóa registry. Tránh
        /// giữ ref object đã bị Destroy theo scene ở lần trận sau.</summary>
        public static void Clear()
        {
            foreach (ObjectPool<GameObject> pool in s_pools.Values) pool.Clear();
            s_pools.Clear();
            s_maxSizes.Clear();
            if (s_container != null)
            {
                Object.Destroy(s_container.gameObject);
                s_container = null;
            }
        }

        private static ObjectPool<GameObject> GetOrCreatePool(GameObject prefab)
        {
            if (s_pools.TryGetValue(prefab, out ObjectPool<GameObject> pool)) return pool;

            int maxSize = s_maxSizes.TryGetValue(prefab, out int m) ? m : DefaultMaxSize;
            pool = new ObjectPool<GameObject>(
                createFunc: () => CreateInstance(prefab),
                actionOnGet: go =>
                {
                    go.SetActive(true);
                    var marker = go.GetComponent<PooledInstance>();
                    if (marker != null) marker.OnGet();
                },
                actionOnRelease: go =>
                {
                    var marker = go.GetComponent<PooledInstance>();
                    if (marker != null) marker.OnRelease();
                    go.SetActive(false);
                },
                actionOnDestroy: go => { if (go != null) Object.Destroy(go); },
                collectionCheck: true,
                defaultCapacity: 8,
                maxSize: maxSize);

            s_pools[prefab] = pool;
            return pool;
        }

        private static GameObject CreateInstance(GameObject prefab)
        {
            GameObject go = Object.Instantiate(prefab);
            // Parent NGAY vào Container ([PrefabPool], DontDestroyOnLoad): mọi instance — kể cả cái tạo
            // qua Prewarm (Get/Release thẳng, KHÔNG qua Spawn/Despawn nên không tự reparent) — sống gọn
            // dưới 1 node thay vì rơi vào root scene map (bừa hierarchy + chết theo additive-unload map).
            go.transform.SetParent(Container, false);
            var marker = go.GetComponent<PooledInstance>();
            if (marker == null) marker = go.AddComponent<PooledInstance>();
            marker.Setup(prefab);
            return go;
        }
    }
}
