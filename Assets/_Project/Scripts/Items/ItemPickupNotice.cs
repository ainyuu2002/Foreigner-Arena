using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FA.Items
{
    /// <summary>Thông báo pickup gọn trên HUD. Chỉ hiện khi scene có component này (scene Items test).</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ItemPickupNotice : MonoBehaviour
    {
        [SerializeField] private Text label;
        [Min(0.1f)] [SerializeField] private float visibleDuration = 3f;
        [Min(0.05f)] [SerializeField] private float fadeDuration = 0.35f;

        private static ItemPickupNotice s_instance;
        private CanvasGroup _canvasGroup;
        private Coroutine _routine;

        private void Awake()
        {
            s_instance = this;
            _canvasGroup = GetComponent<CanvasGroup>();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        public static void Show(string message)
        {
            if (s_instance == null || string.IsNullOrWhiteSpace(message)) return;
            s_instance.Display(message);
        }

        private void Display(string message)
        {
            if (label == null) return;

            label.text = message;
            SetVisible(true);
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSecondsRealtime(visibleDuration);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            SetVisible(false);
            _routine = null;
        }

        private void SetVisible(bool visible)
        {
            if (_canvasGroup != null) _canvasGroup.alpha = visible ? 1f : 0f;
            if (!visible && label != null) label.text = string.Empty;
        }
    }
}
