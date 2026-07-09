using UnityEngine;
using UnityEngine.InputSystem;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// (P12 Settings) PAUSE MENU trong trận — ESC (bàn phím) / Start (tay cầm) mở bảng pause:
    ///   • TIẾP TỤC — đóng bảng, trả <c>Time.timeScale = 1</c>.
    ///   • SETTINGS — mở <see cref="SettingsPanelView"/> bản rút gọn (in-match mode).
    ///   • ĐẤU LẠI — raise kênh RematchPressed (MatchManager nghe sẵn: GoToMatch reload map, spawn lại
    ///     player sạch — cùng nhân vật/map). Tắt-HUD-trước để GoToMatch bật lại → views resetOnEnable về 0.
    ///   • THOÁT — về màn chọn nhân vật: raise kênh BackToCharSelect (MatchManager NGHE SẴN kênh này —
    ///     routine unload map + reset lựa chọn chạy được từ mọi phase, KHÔNG cần sửa code gameplay §1.3)
    ///     + tự tắt HUD (GoToCharSelect không tắt matchHud — chỉ GoToEnd tắt).
    ///
    /// Pause = <c>Time.timeScale = 0</c>: MatchTimer trừ <c>Time.deltaTime</c> (scaled) nên tự đứng;
    /// physics/animation cũng đứng theo — đúng nghĩa pause local 2v2. UI vẫn chạy (uGUI không cần timeScale).
    /// CHỈ mở được khi <see cref="hudRoot"/> đang active (= đang trong trận); ở menu phím ESC bị bỏ qua.
    /// Root canvas LUÔN active để Update nghe phím; bảng <see cref="panel"/> là con, tắt sẵn.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenuView : MonoBehaviour
    {
        [Header("Bảng pause (con, tắt sẵn)")]
        [SerializeField] private GameObject panel;

        [Tooltip("Panel settings bản in-match (con, tắt sẵn).")]
        [SerializeField] private SettingsPanelView settingsPanel;

        [Header("Trạng thái trận")]
        [Tooltip("Canvas HUD_InMatch — active = đang trong trận (MatchManager bật/tắt). Gate cho phím ESC.")]
        [SerializeField] private GameObject hudRoot;

        [Header("Kênh (raise)")]
        [Tooltip("BackToCharSelectChannel — MatchManager nghe sẵn: unload map + về màn chọn nhân vật.")]
        [SerializeField] private VoidEventChannelSO backToCharSelectChannel;

        [Tooltip("RematchPressedChannel — MatchManager nghe sẵn: reload map, đấu lại cùng nhân vật/map.")]
        [SerializeField] private VoidEventChannelSO rematchChannel;

        private bool IsPaused => panel != null && panel.activeSelf;
        private bool InMatch => hudRoot != null && hudRoot.activeInHierarchy;

        private void OnDisable()
        {
            // Phòng hộ: object bị tắt/destroy khi đang pause → không kẹt timeScale = 0.
            if (IsPaused) Time.timeScale = 1f;
        }

        private void Update()
        {
            // Đang bắt phím rebind: ESC là phím HỦY của rebind (WithCancelingThrough) — không xử lý ở đây,
            // kèm khoảng đệm sau khi rebind kết thúc (thứ tự callback Input System vs Update không đảm bảo
            // trong cùng frame → không có đệm thì 1 lần ESC vừa hủy rebind vừa đóng panel).
            if (KeyBindings.SuppressPauseKey) return;
            if (!PausePressedThisFrame()) return;

            if (settingsPanel != null && settingsPanel.HandleBack())
                return;                         // ESC lớp 1: đóng keybind/settings (lớp trong cùng), vẫn pause.
            if (IsPaused)
                Resume();                       // ESC lớp 2: đóng pause.
            else if (InMatch)
                Pause();                        // ESC trong trận: mở pause.
        }

        private static bool PausePressedThisFrame()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            return gp != null && gp.startButton.wasPressedThisFrame;
        }

        public void Pause()
        {
            if (panel == null) return;
            panel.SetActive(true);
            Time.timeScale = 0f;
        }

        /// <summary>Nút TIẾP TỤC (persistent listener).</summary>
        public void Resume()
        {
            if (settingsPanel != null && settingsPanel.gameObject.activeSelf) settingsPanel.Close();
            if (panel != null) panel.SetActive(false);
            Time.timeScale = 1f;
        }

        /// <summary>Nút SETTINGS (persistent listener).</summary>
        public void OpenSettings()
        {
            if (settingsPanel != null) settingsPanel.Open();
        }

        /// <summary>Nút ĐẤU LẠI (persistent listener) — trận MỚI cùng nhân vật/map, ngay từ pause.</summary>
        public void Rematch()
        {
            Resume();                                        // trả timeScale + đóng bảng TRƯỚC khi reload.
            // Tắt HUD trước khi raise: GoToMatch SetActive(true) lại ngay → OnEnable của các view
            // (resetOnEnable) đưa mana/nộ/% về 0 — không lộ giá trị trận cũ trong lúc chờ spawn.
            if (hudRoot != null) hudRoot.SetActive(false);
            if (rematchChannel != null) rematchChannel.RaiseEvent();
        }

        /// <summary>Nút THOÁT (persistent listener) — thoát trận về màn chọn nhân vật.</summary>
        public void QuitToMenu()
        {
            Resume();                                        // trả timeScale + đóng bảng TRƯỚC khi chuyển phase.
            if (hudRoot != null) hudRoot.SetActive(false);   // GoToCharSelect không tắt HUD — UI tự tắt.
            if (backToCharSelectChannel != null) backToCharSelectChannel.RaiseEvent();
        }
    }
}
