using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FA.UI
{
    /// <summary>
    /// (P12 Menu FX) CHUYỂN CẢNH Main Menu — overlay full-screen (Image + CanvasGroup) trên CÙNG canvas
    /// với menu (prefab-safe: mọi ref nội bộ 1 root):
    ///   • <see cref="StartGame"/> (nút START): chớp sét trắng-xanh 2 nhịp → phủ trắng → ẨN content menu
    ///     (lộ CharSelect dưới) → fade overlay ra. Cảm giác "dịch chuyển qua năng lượng".
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
        [SerializeField] private Color startFlashColor = new Color(0.75f, 0.85f, 1f, 1f); // trắng-xanh sét
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

        private IEnumerator StartRoutine()
        {
            _playing = true;
            overlayGroup.blocksRaycasts = true;
            overlay.color = startFlashColor;

            // 2 nhịp chớp sét (mạnh → dư âm).
            SetAlpha(0.55f); yield return Wait(0.05f);
            SetAlpha(0f);    yield return Wait(0.07f);
            SetAlpha(0.85f); yield return Wait(0.05f);
            SetAlpha(0f);    yield return Wait(0.06f);

            // Phủ trắng kín → ẩn menu (và BG/FX ngoài canvas) sau lớp phủ.
            yield return Fade(0f, 1f, 0.18f);
            if (content != null) content.SetActive(false);
            if (alsoHide != null)
                foreach (var go in alsoHide)
                    if (go != null) go.SetActive(false);

            // Fade ra — lộ CharSelect.
            yield return Fade(1f, 0f, 0.35f);
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

        private static WaitForSecondsRealtime Wait(float s) => new WaitForSecondsRealtime(s);

        private void SetAlpha(float a)
        {
            if (overlay == null) return;
            Color c = overlay.color; c.a = a; overlay.color = c;
            if (overlayGroup != null) overlayGroup.alpha = 1f; // alpha đi qua màu overlay; group chỉ chặn raycast.
        }
    }
}
