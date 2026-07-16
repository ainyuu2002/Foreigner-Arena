using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// KHỞI TẠO <see cref="PrefabPool"/> đầu trận: nạp <see cref="PoolConfigSO"/> → prewarm các prefab
    /// nóng (spark/đạn) để lần bắn/hit đầu không hitch. Đặt component này trên 1 object bền (Lobby/
    /// bootstrap scene) — <c>DontDestroyOnLoad</c> để sống xuyên các map additive.
    ///
    /// Prewarm chạy khi <see cref="onMatchStart"/> raise (sau <see cref="PrefabPool.Clear"/> giữa 2 trận
    /// nên prewarm LẠI mỗi trận). Bỏ trống kênh = prewarm ngay Awake (test scene không có MatchManager).
    /// <see cref="PrefabPool.Configure"/> KHÔNG idempotent về prewarm (luôn Get/Release lại) — CHỦ Ý vì
    /// pool đã bị <see cref="PrefabPool.Clear"/> dọn sạch giữa 2 trận nên cần prewarm lại từ đầu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoolBootstrap : MonoBehaviour
    {
        [Tooltip("Cấu hình prewarm/maxSize cho các prefab nóng. Trống = mọi prefab pool lazy maxSize mặc định.")]
        [SerializeField] private PoolConfigSO config;

        [Tooltip("MatchStart (VoidEventChannelSO) — prewarm lại mỗi đầu trận (sau Clear). Trống = prewarm ở Awake.")]
        [SerializeField] private VoidEventChannelSO onMatchStart;

        private static PoolBootstrap s_instance;

        private void Awake()
        {
            // SINGLETON-GUARD: DontDestroyOnLoad sống xuyên map → scene chứa bootstrap load lại (vd
            // quay về Lobby) sẽ tạo instance thứ 2 cùng subscribe onMatchStart → Prewarm chạy trùng
            // lặp. Instance sau tự hủy, giữ instance đầu tiên.
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (s_instance != this) return;   // instance dư (đã bị Destroy ở Awake) không subscribe.
            if (onMatchStart != null) onMatchStart.OnEventRaised += Prewarm;
            else Prewarm();   // không có kênh (test scene) → prewarm ngay.
        }

        private void OnDisable()
        {
            if (onMatchStart != null) onMatchStart.OnEventRaised -= Prewarm;
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        private void Prewarm() => PrefabPool.Configure(config);
    }
}
