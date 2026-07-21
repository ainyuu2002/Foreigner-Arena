using UnityEngine;
using UnityEngine.UI;
using FA.Core;

namespace FA.Effects
{
    /// <summary>
    /// LỚP PHỦ super flash (prefab Canvas Screen-Space-Overlay, sortingOrder cao → che cả HUD):
    /// nền tối + CUT-IN chân dung trượt vào từ trái/phải (theo phía của người ra chiêu — kiểu MUGEN
    /// helper 9935). Chạy bằng UNSCALED time vì flash đi kèm Time.timeScale=0
    /// (<see cref="SuperFlashController"/> điều khiển vòng đời: Show → Hide → tự Despawn về pool).
    /// </summary>
    public sealed class SuperFlashOverlay : MonoBehaviour
    {
        [Tooltip("Ảnh nền tối phủ toàn màn hình.")]
        [SerializeField] private Image dim;

        [Tooltip("Ảnh cut-in chân dung.")]
        [SerializeField] private Image portrait;

        [Tooltip("Thời gian trượt vào (giây, unscaled).")]
        [Min(0.01f)] [SerializeField] private float slideSeconds = 0.18f;

        [Tooltip("Quãng trượt ngang (px theo canvas 1920×1080).")]
        [SerializeField] private float slideDistance = 260f;

        [Tooltip("Độ tối của nền (alpha). 1 = đen hẳn.")]
        [Range(0f, 1f)] [SerializeField] private float dimAlpha = 1f;

        [Tooltip("Thời gian tan biến khi Hide (giây, unscaled).")]
        [Min(0.01f)] [SerializeField] private float fadeOutSeconds = 0.12f;

        private float _t;
        private bool _hiding;
        private Vector2 _shownPos;
        private float _fromX;

        /// <summary>SuperFlashController gọi ngay sau Instantiate. left = cut-in phía trái màn hình
        /// (người ra chiêu đứng nửa trái); phải thì neo phải + lật ảnh cho đối xứng.</summary>
        public void Show(Sprite sprite, bool left)
        {
            if (portrait != null)
            {
                if (sprite != null) portrait.sprite = sprite;
                portrait.preserveAspect = true;
                RectTransform rt = portrait.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(left ? 0f : 1f, 0.5f);
                // PIVOT luôn (0, .5): khi ở PHẢI có localScale.x=-1 (lật ảnh nhìn vào giữa), rect lật
                // quanh pivot → pivot mép TRÁI + scale âm = hình trải VỀ BÊN TRÁI của neo phải (nằm
                // trong màn). Pivot đặt mép phải như trước đây khiến hình lật văng RA NGOÀI màn hình.
                rt.pivot = new Vector2(0f, 0.5f);
                rt.localScale = new Vector3(left ? 1f : -1f, 1f, 1f);
                _shownPos = new Vector2(left ? 30f : -30f, 0f);
                _fromX = _shownPos.x + (left ? -slideDistance : slideDistance);
                rt.anchoredPosition = new Vector2(_fromX, _shownPos.y);
            }
            if (dim != null) dim.color = new Color(0f, 0f, 0f, 0f);
            _t = 0f;
            _hiding = false;
        }

        /// <summary>SuperFlashController gọi khi flash kết thúc → tan biến rồi tự Despawn.</summary>
        public void Hide()
        {
            _hiding = true;
            _t = 0f;
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;

            if (!_hiding)
            {
                float k = Mathf.Clamp01(_t / slideSeconds);
                k = 1f - (1f - k) * (1f - k);   // ease-out.
                if (dim != null) dim.color = new Color(0f, 0f, 0f, dimAlpha * k);
                if (portrait != null)
                {
                    portrait.rectTransform.anchoredPosition =
                        new Vector2(Mathf.Lerp(_fromX, _shownPos.x, k), _shownPos.y);
                    var c = portrait.color; c.a = k; portrait.color = c;
                }
                return;
            }

            float f = 1f - Mathf.Clamp01(_t / fadeOutSeconds);
            if (dim != null) dim.color = new Color(0f, 0f, 0f, dimAlpha * f);
            if (portrait != null) { var c = portrait.color; c.a = f; portrait.color = c; }
            if (f <= 0f) PrefabPool.Despawn(gameObject);   // POOLING: trả về pool thay Destroy.
        }
    }
}
