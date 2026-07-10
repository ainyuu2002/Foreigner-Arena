using UnityEngine;
using UnityEngine.UI;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// HUD FX "ĐẦY THANH" (P12): khi tài nguyên đạt ngưỡng đầy → vầng glow sau thanh NHẤP NHÁY + các ô/fill
    /// sáng dần về trắng theo nhịp (cháy sáng kiểu SF6 Drive/Super đầy). HUD là canvas OVERLAY nên KHÔNG
    /// dùng được particle world (overlay đè mọi renderer) — hiệu ứng thuần uGUI: đổi alpha/scale/màu Image.
    ///
    /// Nguồn trạng thái = kênh sự kiện (1 trong 2, gán trong Inspector):
    ///   • <see cref="intChannel"/> (số ô mana) — đầy khi value ≥ <see cref="intFullAt"/>.
    ///   • <see cref="floatChannel"/> (fill 0..1 của nộ) — đầy khi value ≥ <see cref="floatFullAt"/>.
    /// Nhịp chạy bằng unscaled time → pause (timeScale=0) glow vẫn thở. Đặt FA.UI, chỉ ref Core.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BarFullGlowFX : MonoBehaviour
    {
        [Header("Kênh số ô (mana) — đầy khi ≥ intFullAt")]
        [SerializeField] private IntEventChannelSO intChannel;
        [SerializeField] private int intFullAt = 3;

        [Header("Kênh fill 0..1 (nộ) — đầy khi ≥ floatFullAt")]
        [SerializeField] private FloatEventChannelSO floatChannel;
        [SerializeField] private float floatFullAt = 1f;

        [Header("Hình ảnh")]
        [Tooltip("Vầng sáng sau thanh (Image alpha 0 khi chưa đầy) — builder dựng sẵn, ignoreLayout.")]
        [SerializeField] private Image glowImage;
        [Tooltip("Các Image fill sẽ sáng dần về trắng theo nhịp khi đầy (ô mana / fill nộ).")]
        [SerializeField] private Image[] tintImages;

        [Header("Nhịp")]
        [SerializeField] private float pulseSpeed = 6f;
        [SerializeField] private float glowAlphaMin = 0.22f;
        [SerializeField] private float glowAlphaMax = 0.65f;
        [SerializeField] private float scalePulse = 0.06f;
        [Range(0f, 1f)]
        [SerializeField] private float tintToWhite = 0.45f;

        private bool _full;
        private Color[] _baseCols;
        private RectTransform _glowRt;
        private Vector3 _glowBaseScale = Vector3.one;

        private void Awake()
        {
            if (glowImage != null)
            {
                _glowRt = glowImage.rectTransform;
                _glowBaseScale = _glowRt.localScale;
            }
            if (tintImages != null)
            {
                _baseCols = new Color[tintImages.Length];
                for (int i = 0; i < tintImages.Length; i++)
                    if (tintImages[i] != null) _baseCols[i] = tintImages[i].color;
            }
        }

        private void OnEnable()
        {
            SetFull(false); // HUD bật đầu trận: coi như chưa đầy cho tới khi kênh nói khác.
            if (intChannel != null) intChannel.OnEventRaised += OnIntChanged;
            if (floatChannel != null) floatChannel.OnEventRaised += OnFloatChanged;
        }

        private void OnDisable()
        {
            if (intChannel != null) intChannel.OnEventRaised -= OnIntChanged;
            if (floatChannel != null) floatChannel.OnEventRaised -= OnFloatChanged;
            SetFull(false); // trả màu gốc — không kẹt trạng thái sáng khi HUD tắt giữa chừng.
        }

        private void OnIntChanged(int v) => SetFull(v >= intFullAt);
        private void OnFloatChanged(float v) => SetFull(v >= floatFullAt - 0.0005f);

        private void Update()
        {
            if (!_full) return;
            // p chạy 0..1 theo sin — cùng pha cho glow + tint để "thở" đồng bộ.
            float p = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;

            if (glowImage != null)
            {
                var c = glowImage.color;
                c.a = Mathf.Lerp(glowAlphaMin, glowAlphaMax, p);
                glowImage.color = c;
                if (_glowRt != null) _glowRt.localScale = _glowBaseScale * (1f + scalePulse * p);
            }
            if (tintImages != null && _baseCols != null)
                for (int i = 0; i < tintImages.Length; i++)
                    if (tintImages[i] != null)
                        tintImages[i].color = Color.Lerp(_baseCols[i], Color.white, tintToWhite * p);
        }

        private void SetFull(bool full)
        {
            if (_full == full) return;
            _full = full;
            if (full) return; // Update lo phần nhấp nháy.

            // Tắt: glow về alpha 0 + scale gốc, fill về màu gốc.
            if (glowImage != null)
            {
                var c = glowImage.color; c.a = 0f; glowImage.color = c;
                if (_glowRt != null) _glowRt.localScale = _glowBaseScale;
            }
            if (tintImages != null && _baseCols != null)
                for (int i = 0; i < tintImages.Length; i++)
                    if (tintImages[i] != null) tintImages[i].color = _baseCols[i];
        }
    }
}
