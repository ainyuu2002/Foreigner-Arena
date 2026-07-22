using UnityEngine;
using UnityEngine.InputSystem;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// (v10.11) ESC = QUAY LẠI ở các màn menu lobby — gắn lên chính PANEL màn hình (CharSelectPanel/
    /// StageSelectPanel) nên chỉ nghe phím khi màn đó đang hiện. Hai kiểu đích (builder wire 1 trong 2):
    ///   • <see cref="menuFx"/> — gọi <see cref="MenuTransitionFX.BackToMenu"/> (CharSelect: dip-to-black
    ///     hiện lại Main Menu). KHÔNG SetActive canvas MainMenu — root đó LUÔN active, START chỉ ẩn
    ///     content/alsoHide bên trong (bài học v10.10: guard activeSelf trên root nuốt mất phím ESC).
    ///   • <see cref="raiseOnBack"/> — raise kênh Void (StageSelect: BackToCharSelect — MatchManager
    ///     NGHE SẴN kênh này, reset lựa chọn + về CharSelect, KHÔNG sửa code gameplay §1.3).
    ///
    /// Không đụng PauseMenuView: bên đó gate ESC bằng hudRoot active (chỉ trong trận) — hai màn menu
    /// này HUD luôn tắt nên không bao giờ xử lý trùng 1 lần bấm. Đang rebind phím thì ESC là phím hủy
    /// rebind (SuppressPauseKey) — bỏ qua. Menu đang hiện/đang chuyển cảnh → bỏ qua.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EscBackNavigator : MonoBehaviour
    {
        [Header("Đích quay lại (builder wire 1 trong 2)")]
        [Tooltip("MenuTransitionFX trên canvas MainMenu — BackToMenu() hiện lại menu (CharSelect dùng).")]
        [SerializeField] private MenuTransitionFX menuFx;

        [Tooltip("Hoặc raise kênh Void (vd BackToCharSelectChannel — MatchManager nghe sẵn).")]
        [SerializeField] private VoidEventChannelSO raiseOnBack;

        private void Update()
        {
            if (KeyBindings.SuppressPauseKey) return;
            if (menuFx != null && (menuFx.IsBusy || menuFx.IsMenuShown)) return;

            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

            UIAudioManager.PlayClick();
            if (menuFx != null) menuFx.BackToMenu();
            else if (raiseOnBack != null) raiseOnBack.RaiseEvent();
            DebugLog.Log($"[FA.UI] ESC back — {name} → {(menuFx != null ? "MainMenu" : raiseOnBack != null ? raiseOnBack.name : "(chưa wire)")}");
        }
    }
}
