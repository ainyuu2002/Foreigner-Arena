using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FA.UI
{
    /// <summary>
    /// (P12 Menu FX) HIỆU ỨNG NÚT "sạc năng lượng" — gắn cùng GameObject với Button:
    ///   • Bình thường: lớp glow (Image accent phủ nút) alpha thấp âm ỉ.
    ///   • Hover: glow PULSE nhanh (lách tách năng lượng) + nút phồng nhẹ (scale).
    ///   • Click: 1 flash trắng ngắn từ tâm (Image con) — "phóng điện".
    /// Code-only tầng 1: glow = Image màu, chưa cần sprite sét/lửa — swap sprite sau KHÔNG sửa code.
    /// Thuần trình bày (§1.3), không đụng logic onClick (raiser/persistent listener vẫn chạy bình thường).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ButtonGlowFX : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Lớp hiệu ứng (Image con — builder tạo)")]
        [Tooltip("Glow accent phủ nút (raycastTarget=false). Alpha do FX điều khiển.")]
        [SerializeField] private Graphic glow;

        [Tooltip("Flash trắng khi click (stretch full, raycastTarget=false, alpha 0).")]
        [SerializeField] private Graphic clickFlash;

        [Header("Thông số")]
        [SerializeField] private float idleAlpha = 0.16f;
        [SerializeField] private float hoverAlphaMin = 0.35f;
        [SerializeField] private float hoverAlphaMax = 0.85f;

        [Tooltip("Tốc độ pulse khi hover (lách tách).")]
        [SerializeField] private float pulseSpeed = 9f;

        [Tooltip("Scale nút khi hover (1.05 = phồng 5%).")]
        [SerializeField] private float hoverScale = 1.05f;

        private bool _hovering;
        private Vector3 _baseScale;

        private void Awake() => _baseScale = transform.localScale;

        private void OnDisable()
        {
            _hovering = false;
            transform.localScale = _baseScale;
            SetAlpha(glow, idleAlpha);
            SetAlpha(clickFlash, 0f);
        }

        private void Update()
        {
            if (glow == null) return;
            float a = _hovering
                ? Mathf.Lerp(hoverAlphaMin, hoverAlphaMax,
                             (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f)
                : idleAlpha;
            SetAlpha(glow, a);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            _hovering = true;
            transform.localScale = _baseScale * hoverScale;
        }

        public void OnPointerExit(PointerEventData e)
        {
            _hovering = false;
            transform.localScale = _baseScale;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (clickFlash != null && isActiveAndEnabled) StartCoroutine(Flash());
        }

        private IEnumerator Flash()
        {
            const float dur = 0.16f;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                SetAlpha(clickFlash, Mathf.Lerp(0.85f, 0f, t / dur));
                yield return null;
            }
            SetAlpha(clickFlash, 0f);
        }

        private static void SetAlpha(Graphic g, float a)
        {
            if (g == null) return;
            Color c = g.color; c.a = a; g.color = c;
        }
    }
}
