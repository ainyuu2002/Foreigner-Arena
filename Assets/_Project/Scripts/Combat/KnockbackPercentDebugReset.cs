using UnityEngine;
using UnityEngine.InputSystem;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// DEBUG-ONLY (P7, research §6) — nút/phím reset thanh % về 0 để test "% cao bay xa hơn % thấp" mà
    /// không cần mạng (mạng/respawn = P12, sẽ THAY component này). Lắng phím trong Update, gọi
    /// <see cref="KnockbackPercent.ResetPercent"/> cho danh sách thanh % đã gán.
    ///
    /// KHÔNG phải gameplay thật — chỉ tiện ích scene Test_Phase07. Đọc phím qua Input System
    /// (<see cref="Keyboard.current"/>) vì project đã chuyển active input handling sang Input System
    /// package (Input cũ ném InvalidOperationException). Đặt FA.Combat (cùng module KnockbackPercent),
    /// không ref module khác.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KnockbackPercentDebugReset : MonoBehaviour
    {
        [Header("Thanh % cần reset (gán cả P1 + P2)")]
        [SerializeField] private KnockbackPercent[] targets;

        [Tooltip("Phím reset tất cả về 0% (Input System Key).")]
        [SerializeField] private Key resetKey = Key.R;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!keyboard[resetKey].wasPressedThisFrame) return;
            if (targets == null) return;
            foreach (var p in targets)
                if (p != null) p.ResetPercent();
            DebugLog.Log("[Knockback] Reset % về 0 (debug).");
        }
    }
}
