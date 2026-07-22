namespace FA.Core
{
    /// <summary>
    /// HOOK vòng đời POOL (§3.1 Unity-native: dùng <c>UnityEngine.Pool.ObjectPool</c>, object KHÔNG bị
    /// Destroy giữa các lần dùng → Awake/Start CHỈ chạy 1 lần đời object). Component nào cần logic chạy
    /// LẠI mỗi lần spawn/despawn (reset state tương đối, subscribe/unsubscribe) implement interface này;
    /// <see cref="PooledInstance"/> gọi <see cref="OnSpawned"/> khi Get, <see cref="OnDespawned"/> khi
    /// Release. Optional — component chỉ cần reset ở OnEnable/OnDisable (VFX flipbook) KHÔNG cần implement.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>Gọi mỗi lần object được lấy khỏi pool (sau SetActive(true)).</summary>
        void OnSpawned();

        /// <summary>Gọi mỗi lần object được trả về pool (trước SetActive(false)).</summary>
        void OnDespawned();
    }
}
