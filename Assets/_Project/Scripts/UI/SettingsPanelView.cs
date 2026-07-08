using UnityEngine;
using UnityEngine.UI;

namespace FA.UI
{
    /// <summary>
    /// (P12 Settings) PANEL SETTINGS dùng chung 2 ngữ cảnh — Main Menu (đầy đủ) và Pause Menu trong trận
    /// (<see cref="inMatchMode"/> = true → ẩn <see cref="menuOnlySections"/>, chỉ còn mục chỉnh được giữa
    /// trận: âm thanh (+ phím, khi làm rebind sau)). 1 NGUỒN: builder dựng cùng 1 hàm cho cả 2 chỗ —
    /// sửa layout 1 lần ăn cả 2.
    ///
    /// Slider ghi thẳng <see cref="GameSettings"/> (PlayerPrefs); đóng panel mới <c>PlayerPrefs.Save()</c>.
    /// Mở/đóng qua <see cref="Open"/>/<see cref="Close"/> — persistent listener của nút gọi được cả khi
    /// panel đang inactive (target là component, không cần GO active).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsPanelView : MonoBehaviour
    {
        [Header("Âm lượng (0..1)")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;

        [Tooltip("Label % cạnh mỗi slider (tùy chọn — null thì bỏ qua).")]
        [SerializeField] private Text masterValueLabel;
        [SerializeField] private Text bgmValueLabel;
        [SerializeField] private Text sfxValueLabel;

        [Header("Ngữ cảnh")]
        [Tooltip("TRUE = mở từ pause menu trong trận → ẩn các mục menuOnlySections.")]
        [SerializeField] private bool inMatchMode;

        [Tooltip("Các mục CHỈ cho chỉnh ngoài trận (ẩn khi inMatchMode) — hiện trống, để dành khi thêm mục mới.")]
        [SerializeField] private GameObject[] menuOnlySections;

        [Header("Cài đặt phím")]
        [Tooltip("Panel rebind phím (con của root này, tắt sẵn) — mở bằng nút CÀI ĐẶT PHÍM.")]
        [SerializeField] private KeybindPanelView keybindPanel;

        public void Open() => gameObject.SetActive(true);

        public void Close()
        {
            if (keybindPanel != null && keybindPanel.gameObject.activeSelf)
                keybindPanel.Close(); // kẻo lần mở sau panel phím hiện sẵn đè lên settings.
            GameSettings.Save();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// ESC từ PauseMenuView: đóng LỚP TRONG CÙNG đang mở (keybind trước, settings sau).
        /// Trả TRUE nếu đã tiêu thụ phím (panel này đang mở); FALSE = không có gì để đóng.
        /// </summary>
        public bool HandleBack()
        {
            if (!gameObject.activeSelf) return false;
            if (keybindPanel != null && keybindPanel.gameObject.activeSelf)
            {
                keybindPanel.Close();
                return true;
            }
            Close();
            return true;
        }

        private void OnEnable()
        {
            // Nạp giá trị đã lưu TRƯỚC khi gắn listener (SetValueWithoutNotify — không tự bắn event nạp).
            if (masterSlider != null)
            {
                masterSlider.SetValueWithoutNotify(GameSettings.Master);
                masterSlider.onValueChanged.AddListener(OnMaster);
            }
            if (bgmSlider != null)
            {
                bgmSlider.SetValueWithoutNotify(GameSettings.Bgm);
                bgmSlider.onValueChanged.AddListener(OnBgm);
            }
            if (sfxSlider != null)
            {
                sfxSlider.SetValueWithoutNotify(GameSettings.Sfx);
                sfxSlider.onValueChanged.AddListener(OnSfx);
            }
            RefreshLabels();

            if (menuOnlySections != null)
                foreach (var go in menuOnlySections)
                    if (go != null) go.SetActive(!inMatchMode);
        }

        private void OnDisable()
        {
            if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(OnMaster);
            if (bgmSlider != null) bgmSlider.onValueChanged.RemoveListener(OnBgm);
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfx);
        }

        private void OnMaster(float v) { GameSettings.Master = v; RefreshLabels(); }
        private void OnBgm(float v) { GameSettings.Bgm = v; RefreshLabels(); }
        private void OnSfx(float v) { GameSettings.Sfx = v; RefreshLabels(); }

        private void RefreshLabels()
        {
            if (masterValueLabel != null) masterValueLabel.text = Percent(GameSettings.Master);
            if (bgmValueLabel != null) bgmValueLabel.text = Percent(GameSettings.Bgm);
            if (sfxValueLabel != null) sfxValueLabel.text = Percent(GameSettings.Sfx);
        }

        private static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";
    }
}
