using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FA.UI
{
    /// <summary>
    /// (P12 Menu FX) CHUYỂN CẢNH Main Menu — overlay full-screen (Image + CanvasGroup) trên CÙNG canvas
    /// với menu (prefab-safe: mọi ref nội bộ 1 root):
    ///   • <see cref="StartGame"/> (nút START): DIP-TO-BLACK — phủ TỐI dần → ẨN content menu (lộ
    ///     CharSelect dưới) → fade overlay ra. Mượt, không chớp, không lóa sáng (yêu cầu P5 tuần 3 —
    ///     bản phủ trắng bị chê "màn hình trở nên sáng").
    ///   • <see cref="QuitGame"/> (nút QUIT): phủ ĐEN dần + rung nhẹ content ("cháy tắt") → thoát game
    ///     (Editor: dừng Play).
    /// Wire qua persistent listener onClick (builder). Chặn double-click bằng cờ _playing. Thuần trình bày —
    /// KHÔNG đụng MatchManager: menu chỉ tự ẩn, flow CharSelect→trận vẫn do kênh sự kiện (§1.3).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuTransitionFX : MonoBehaviour
    {
        [Header("Overlay (con cùng canvas, trên cùng)")]
        [Tooltip("Image full-screen của overlay — FX đổi màu/alpha.")]
        [SerializeField] private Image overlay;

        [Tooltip("CanvasGroup của overlay — chặn raycast khi đang chuyển cảnh.")]
        [SerializeField] private CanvasGroup overlayGroup;

        [Header("Đối tượng menu")]
        [Tooltip("Cụm nội dung menu (BG + cột nút) — StartGame ẨN cụm này (canvas + overlay vẫn sống).")]
        [SerializeField] private GameObject content;

        [Tooltip("Các object PHỤ ẩn cùng lúc với content khi StartGame — BG canvas riêng + FX world " +
                 "(sét/tia lửa) nằm NGOÀI canvas menu nên không ẩn theo content được.")]
        [SerializeField] private GameObject[] alsoHide;

        [Tooltip("RectTransform bị RUNG khi QuitGame (thường = cột nút).")]
        [SerializeField] private RectTransform shakeTarget;

        [Header("Màu")]
        [SerializeField] private Color startFlashColor = Color.black; // dip-to-black — hợp nền tối, không lóa.
        [SerializeField] private Color quitColor = Color.black;

        private bool _playing;

        private void OnEnable()
        {
            if (overlayGroup != null) { overlayGroup.alpha = 0f; overlayGroup.blocksRaycasts = false; }
            if (overlay != null) SetAlpha(0f);
            _playing = false;
        }

        /// <summary>Nút START gọi (persistent listener): chớp sét → phủ trắng → ẩn menu → fade ra.</summary>
        public void StartGame()
        {
            if (!_playing) StartCoroutine(StartRoutine());
        }

        /// <summary>Nút QUIT gọi (persistent listener): phủ đen + rung → thoát game.</summary>
        public void QuitGame()
        {
            if (!_playing) StartCoroutine(QuitRoutine());
        }

        /// <summary>(v10.11) ESC ở CharSelect gọi (<see cref="EscBackNavigator"/>): NGƯỢC của StartGame —
        /// phủ tối → HIỆN lại content + BG/FX → fade ra. Root canvas vốn luôn active (StartGame chỉ ẩn
        /// content/alsoHide) nên back = bật lại đúng những gì StartGame đã ẩn.</summary>
        public void BackToMenu()
        {
            if (!_playing) StartCoroutine(BackRoutine());
        }

        /// <summary>Menu đang hiện (content active)? — navigator gate để ESC không xử lý khi đang ở menu.</summary>
        public bool IsMenuShown => content != null && content.activeSelf;

        /// <summary>Đang chạy chuyển cảnh? — chặn ESC chen giữa lúc dip-to-black.</summary>
        public bool IsBusy => _playing;

        private IEnumerator StartRoutine()
        {
            _playing = true;
            overlayGroup.blocksRaycasts = true;
            overlay.color = startFlashColor;

            // Phủ TỐI dần kín màn → ẩn menu (và BG/FX ngoài canvas) sau lớp phủ. Không chớp nhịp.
            yield return Fade(0f, 1f, 0.40f);
            if (content != null) content.SetActive(false);
            if (alsoHide != null)
                foreach (var go in alsoHide)
                    if (go != null) go.SetActive(false);

            // Fade ra — lộ CharSelect (chậm hơn nhịp phủ một chút cho êm mắt).
            yield return Fade(1f, 0f, 0.50f);
            overlayGroup.blocksRaycasts = false;
            _playing = false;
        }

        private IEnumerator BackRoutine()
        {
            _playing = true;
            overlayGroup.blocksRaycasts = true;
            overlay.color = startFlashColor;

            yield return Fade(0f, 1f, 0.40f);
            if (content != null) content.SetActive(true);
            if (alsoHide != null)
                foreach (var go in alsoHide)
                    if (go != null) go.SetActive(true);

            yield return Fade(1f, 0f, 0.50f);
            overlayGroup.blocksRaycasts = false;
            _playing = false;
        }

        private IEnumerator QuitRoutine()
        {
            _playing = true;
            overlayGroup.blocksRaycasts = true;
            overlay.color = quitColor;

            Vector2 basePos = shakeTarget != null ? shakeTarget.anchoredPosition : Vector2.zero;
            const float dur = 0.55f;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = t / dur;
                SetAlpha(k);
                if (shakeTarget != null)
                    shakeTarget.anchoredPosition = basePos + Random.insideUnitCircle * 6f * (1f - k);
                yield return null;
            }
            SetAlpha(1f);
            if (shakeTarget != null) shakeTarget.anchoredPosition = basePos;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator Fade(float from, float to, float dur)
        {
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(from, to, t / dur));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float a)
        {
            if (overlay == null) return;
            Color c = overlay.color; c.a = a; overlay.color = c;
            if (overlayGroup != null) overlayGroup.alpha = 1f; // alpha đi qua màu overlay; group chỉ chặn raycast.
        }
    }
}
