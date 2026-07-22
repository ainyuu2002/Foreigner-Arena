using UnityEngine.InputSystem;

namespace FA.Core
{
    /// <summary>Core-owned device query so GameFlow does not reference the Input System package directly.</summary>
    public static class LocalInputDevices
    {
        public static bool HasGamepad => Gamepad.all.Count > 0;
    }
}
