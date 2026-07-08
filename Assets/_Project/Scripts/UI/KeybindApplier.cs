using UnityEngine;
using UnityEngine.InputSystem;

namespace FA.UI
{
    /// <summary>
    /// (P12 Settings — rebind phím) ÁP keybind overrides đã lưu lên instance FA_Controls MỚI enable —
    /// motor spawn lúc vào trận (sau khi user đã lưu phím) tự nhận phím tùy chỉnh mà KHÔNG sửa code
    /// gameplay: nghe <c>InputSystem.onActionChange</c>, thấy map thuộc asset "FA_Controls" vừa enable
    /// thì đánh dấu dirty, ÁP Ở UPDATE FRAME SAU (không đụng state Input System ngay trong callback
    /// của chính nó). Gắn trên root canvas PauseMenu (luôn active trong scene Lobby).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeybindApplier : MonoBehaviour
    {
        private bool _dirty;

        private void OnEnable()
        {
            InputSystem.onActionChange += OnActionChange;
            _dirty = true; // quét lần đầu: motor có thể enable trước applier.
        }

        private void OnDisable()
        {
            InputSystem.onActionChange -= OnActionChange;
        }

        private void OnActionChange(object subject, InputActionChange change)
        {
            if (change != InputActionChange.ActionMapEnabled &&
                change != InputActionChange.ActionEnabled) return;

            var map = subject as InputActionMap;
            if (map == null && subject is InputAction action) map = action.actionMap;
            if (map != null && map.asset != null && map.asset.name == "FA_Controls")
                _dirty = true;
        }

        private void Update()
        {
            if (!_dirty) return;
            _dirty = false;
            KeyBindings.ApplyToRunningInstances();
        }
    }
}
