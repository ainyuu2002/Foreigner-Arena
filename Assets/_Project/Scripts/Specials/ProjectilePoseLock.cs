using System.Collections.Generic;
using UnityEngine;

namespace FA.Specials
{
    /// <summary>
    /// KHÓA TƯ THẾ theo projectile (Super 2): người bắn bị giữ nguyên pose (không được hành động)
    /// chừng nào projectile khai giữ-pose còn sống — cân bằng "bắn xong không được nhảy vào bồi"
    /// (user chốt). Registry tĩnh theo owner-root: ProjectileDrill Acquire lúc spawn / Release lúc
    /// hủy; SuperState (cùng module) hỏi IsHeld để HOÃN thoát state sau RecoveryEnd. Đếm số (một
    /// owner có thể có nhiều projectile giữ-pose về lý thuyết). Projectile luôn có lifetime nên khóa
    /// luôn tự nhả — không deadlock.
    /// </summary>
    public static class ProjectilePoseLock
    {
        private static readonly Dictionary<GameObject, int> s_locks = new Dictionary<GameObject, int>();

        /// <summary>Reset static giữa các lần Play khi "Enter Play Mode Options" tắt Domain Reload —
        /// tránh giữ ref owner đã Destroy theo domain trước từ lần Play trước.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => s_locks.Clear();

        public static void Acquire(GameObject owner)
        {
            if (owner == null) return;
            s_locks.TryGetValue(owner, out int n);
            s_locks[owner] = n + 1;
        }

        public static void Release(GameObject owner)
        {
            if (owner == null || !s_locks.TryGetValue(owner, out int n)) return;
            if (n <= 1) s_locks.Remove(owner);
            else s_locks[owner] = n - 1;
        }

        /// <summary>Owner này còn projectile giữ-pose đang sống không (SuperState gate exit).</summary>
        public static bool IsHeld(GameObject owner) => owner != null && s_locks.ContainsKey(owner);
    }
}
