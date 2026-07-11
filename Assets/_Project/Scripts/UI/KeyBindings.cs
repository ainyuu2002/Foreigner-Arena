using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// (P12 Settings — rebind phím) MODEL keybind: giữ 1 instance <see cref="FA_Controls"/> RIÊNG cho UI
    /// (không bao giờ Enable — chỉ để hiển thị + bắt phím qua PerformInteractiveRebinding), lưu binding
    /// overrides dạng JSON vào PlayerPrefs (contract key <see cref="PrefsKey"/>), và ÁP overrides lên mọi
    /// instance FA_Controls đang chạy của gameplay.
    ///
    /// KHÔNG sửa code gameplay (§1.3): mỗi CharacterMotor tự <c>new FA_Controls()</c> (instance asset riêng
    /// từ FromJson) — UI không với tới field private, nhưng KHÔNG cần: mọi action đang Enable đều liệt kê
    /// được qua <c>InputSystem.ListEnabledActions</c> → lấy <c>action.actionMap.asset</c> tên "FA_Controls"
    /// → <c>LoadBindingOverridesFromJson</c> (Input System cho phép áp override khi action đang enable —
    /// tự re-resolve, bindingMask/devices của motor giữ nguyên). Motor spawn SAU khi lưu do
    /// <see cref="KeybindApplier"/> bắt qua InputSystem.onActionChange.
    ///
    /// Rebind theo NHÓM control scheme: "Keyboard" (P1) và "Gamepad" (P2) — panel chọn tab; overrides của
    /// cả 2 nhóm nằm chung 1 JSON, bindingMask của motor tự lọc nhóm của mình.
    /// </summary>
    public static class KeyBindings
    {
        /// <summary>PlayerPrefs key chứa JSON overrides (SaveBindingOverridesAsJson).</summary>
        public const string PrefsKey = "FA.Keybinds";

        /// <summary>TRUE khi panel đang chờ bắt phím — PauseMenuView nuốt ESC (ESC là phím HỦY rebind).</summary>
        public static bool RebindActive;

        /// <summary>Thời điểm (unscaled) kết thúc lần rebind gần nhất — chống ESC "xuyên" xuống pause menu
        /// cùng frame với thao tác hủy rebind (thứ tự callback Input System vs Update không đảm bảo).</summary>
        public static float LastRebindEnd = -10f;

        /// <summary>PauseMenuView hỏi trước khi xử lý ESC/Start.</summary>
        public static bool SuppressPauseKey =>
            RebindActive || Time.unscaledTime - LastRebindEnd < 0.15f;

        private static FA_Controls _display;

        /// <summary>Asset hiển thị/bắt phím của UI (lazy, đã nạp overrides từ PlayerPrefs). KHÔNG Enable.</summary>
        public static InputActionAsset DisplayAsset
        {
            get
            {
                if (_display == null)
                {
                    _display = new FA_Controls();
                    LoadInto(_display.asset);
                }
                return _display.asset;
            }
        }

        /// <summary>Xóa overrides cũ rồi nạp bản đã lưu (JSON rỗng = về mặc định).</summary>
        public static void LoadInto(InputActionAsset asset)
        {
            asset.RemoveAllBindingOverrides();
            string json = PlayerPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(json)) asset.LoadBindingOverridesFromJson(json);
        }

        /// <summary>Lưu overrides của DisplayAsset vào PlayerPrefs + áp ngay lên gameplay đang chạy.</summary>
        public static void SaveAndApply()
        {
            PlayerPrefs.SetString(PrefsKey, DisplayAsset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
            ApplyToRunningInstances();
        }

        /// <summary>Về phím mặc định: xóa overrides + PlayerPrefs + áp lên gameplay.</summary>
        public static void ResetAll()
        {
            DisplayAsset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
            ApplyToRunningInstances();
        }

        /// <summary>
        /// Áp overrides đã lưu lên MỌI asset FA_Controls đang có action enable (mỗi motor 1 instance).
        /// DisplayAsset không bao giờ enable nên không tự đè chính mình.
        /// </summary>
        public static void ApplyToRunningInstances()
        {
            // CHƯA từng lưu rebind → KHÔNG đụng asset đang chạy. RemoveAllBindingOverrides trên asset
            // ENABLE ép re-resolve binding giữa lúc motor đang chạy — vô ích khi không có override và
            // là nghi phạm làm rớt input P1 lúc vào trận (Lobby luôn có KeybindApplier, scene test không).
            if (!PlayerPrefs.HasKey(PrefsKey)) return;

            var enabled = new List<InputAction>();
            InputSystem.ListEnabledActions(enabled);

            var seen = new HashSet<InputActionAsset>();
            foreach (var action in enabled)
            {
                var asset = action.actionMap != null ? action.actionMap.asset : null;
                if (asset == null || asset == DisplayAsset || asset.name != "FA_Controls") continue;
                if (seen.Add(asset)) LoadInto(asset);
            }
        }

        /// <summary>
        /// Index binding của action theo NHÓM control scheme ("Keyboard"/"Gamepad"): compositePart rỗng →
        /// binding lẻ trong nhóm; "up"/"down"/"left"/"right" → phần composite (WASD ở Keyboard, D-Pad ở
        /// Gamepad) tương ứng. -1 = không thấy.
        /// </summary>
        public static int BindingIndex(InputAction action, string group, string compositePart)
        {
            if (action == null) return -1;
            var binds = action.bindings;
            for (int i = 0; i < binds.Count; i++)
            {
                var b = binds[i];
                if (b.isComposite) continue;
                if (b.groups == null || !b.groups.Contains(group)) continue;

                if (string.IsNullOrEmpty(compositePart))
                {
                    if (!b.isPartOfComposite) return i;
                }
                else if (b.isPartOfComposite &&
                         string.Equals(b.name, compositePart, System.StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
