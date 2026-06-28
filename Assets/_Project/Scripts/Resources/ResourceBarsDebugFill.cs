using UnityEngine;
using UnityEngine.InputSystem;

namespace FA.Resources
{
    /// <summary>
    /// DEBUG-ONLY (P10 test) — nạp ĐẦY Mana + Rage thủ công để test nhanh tầng phòng thủ (Ground/Air Dodge,
    /// RC Dash tốn mana; Burst 50% nộ; Rage Mode 100% nộ; Ultimate). KHÔNG phải gameplay (build thật gỡ bỏ).
    /// Đọc phím qua Input System (<see cref="Keyboard.current"/>) — project đã chuyển active input sang
    /// Input System (UnityEngine.Input cũ ném exception, theo memory). Đặt FA.Resources (cùng module bar).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceBarsDebugFill : MonoBehaviour
    {
        [Header("Thanh cần nạp (gán cả P1 + P2 để test 2 phía)")]
        [SerializeField] private ManaBar[] manaBars;
        [SerializeField] private RageBar[] rageBars;

        [Tooltip("Phím nạp ĐẦY Mana (test né/RC). Mặc định Y.")]
        [SerializeField] private Key fillManaKey = Key.Y;

        [Tooltip("Phím nạp ĐẦY Rage (test Burst/Rage Mode/Ultimate). Mặc định U.")]
        [SerializeField] private Key fillRageKey = Key.U;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[fillManaKey].wasPressedThisFrame)
            {
                if (manaBars != null)
                    foreach (var m in manaBars)
                        if (m != null) m.DebugFillMax();
                Debug.Log("[Resources] DEBUG nạp đầy Mana.");
            }

            if (kb[fillRageKey].wasPressedThisFrame)
            {
                // Nạp đầy nộ qua IRageSpender.RefundRage(1) (clamp ≤ max). Không thêm API GAIN P8.
                if (rageBars != null)
                    foreach (var r in rageBars)
                        if (r != null) r.RefundRage(1f);
                Debug.Log("[Resources] DEBUG nạp đầy Rage.");
            }
        }
    }
}
