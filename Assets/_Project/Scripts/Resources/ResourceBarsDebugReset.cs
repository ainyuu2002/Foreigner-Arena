using UnityEngine;
using UnityEngine.InputSystem;

namespace FA.Resources
{
    /// <summary>
    /// DEBUG-ONLY (P8, research §6) — phím giả lập "mất mạng" để verify Mana/Rage GIỮ NGUYÊN qua chết
    /// (chỉ % reset, do KnockbackPercentDebugReset của P7 lo). Ở đây CHO PHÉP reset 2 thanh THỦ CÔNG để
    /// test lại từ đầu — KHÔNG phải gameplay thật (P12 RespawnSystem sẽ KHÔNG reset 2 thanh này).
    ///
    /// Đọc phím qua Input System (<see cref="Keyboard.current"/>) — project đã chuyển active input sang
    /// Input System package (UnityEngine.Input cũ ném InvalidOperationException). Đặt FA.Resources (cùng
    /// module ManaBar/RageBar), KHÔNG ref module khác. Bỏ component này khỏi prefab khi build thật.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceBarsDebugReset : MonoBehaviour
    {
        [Header("Thanh cần reset (gán cả P1 + P2)")]
        [SerializeField] private ManaBar[] manaBars;
        [SerializeField] private RageBar[] rageBars;

        [Tooltip("Phím reset Mana + Rage về mặc định (Input System Key). Mặc định T (R đã dùng cho %).")]
        [SerializeField] private Key resetKey = Key.T;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!keyboard[resetKey].wasPressedThisFrame) return;

            if (manaBars != null)
                foreach (var m in manaBars)
                    if (m != null) m.ResetMana();
            if (rageBars != null)
                foreach (var r in rageBars)
                    if (r != null) r.ResetRage();

            Debug.Log("[Resources] Reset Mana + Rage (debug).");
        }
    }
}
