using UnityEngine;
using UnityEngine.UI;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// (P8 HUD) Thanh % kiểu "đổi MÀU" — KHÁC <see cref="ResourceBarView"/> (đổi fillAmount). Thanh LUÔN ĐẦY,
    /// chỉ LERP màu theo % knockback: 0% = <see cref="lowColor"/> (trắng) → max% = <see cref="highColor"/> (đỏ).
    /// Hợp với Smash-% (càng cao càng nguy → càng đỏ). Subscribe MỘT <see cref="FloatEventChannelSO"/> (raw 0..cap).
    /// Decoupling (§1.3): nghe kênh Core, KHÔNG ref FA.Combat. Code chỉ set màu; layout để uGUI lo (§1.1).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PercentColorView : MonoBehaviour
    {
        [Header("Kênh sự kiện (% raw 0..cap)")]
        [Tooltip("FloatEventChannelSO bắn % thô (vd 0..250). Per-player asset (OnPercentChanged_Pn).")]
        [SerializeField] private FloatEventChannelSO channel;

        [Header("Hình ảnh")]
        [Tooltip("Image thanh — LUÔN đầy, chỉ đổi color. KHÔNG cần Image Type = Filled.")]
        [SerializeField] private Image barImage;

        [Tooltip("% MAX để chuẩn hóa (vd 250 = percentCap). Tại đây màu = highColor.")]
        [SerializeField] private float maxRawValue = 250f;

        [Header("Dải màu theo %")]
        [Tooltip("Màu ở 0% (mặc định trắng).")]
        [SerializeField] private Color lowColor = Color.white;

        [Tooltip("Màu ở max% (mặc định đỏ — càng cao càng nguy).")]
        [SerializeField] private Color highColor = new Color(0.90f, 0.15f, 0.15f, 1f);

        private void OnEnable()
        {
            if (channel != null) channel.OnEventRaised += OnValueChanged;
            Apply(0f); // bắt đầu trắng (0%).
        }

        private void OnDisable()
        {
            if (channel != null) channel.OnEventRaised -= OnValueChanged;
        }

        private void OnValueChanged(float raw)
        {
            Apply(maxRawValue > 0f ? raw / maxRawValue : raw);
        }

        private void Apply(float t01)
        {
            if (barImage != null) barImage.color = Color.Lerp(lowColor, highColor, Mathf.Clamp01(t01));
        }
    }
}
