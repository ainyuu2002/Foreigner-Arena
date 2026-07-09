using UnityEngine;
using UnityEngine.UI;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// HUD WIDGET thanh fill liên tục (P8, research §3.1) — Unity-native: subscribe MỘT
    /// <see cref="FloatEventChannelSO"/> (payload fill 0..1) → set <c>Image.fillAmount</c>. Image phải
    /// đặt <c>Image Type = Filled</c> (Horizontal) trong Inspector → KHÔNG code vẽ bar (§1.1).
    ///
    /// Dùng CHUNG cho:
    ///   • Thanh % (gán asset OnPercentChanged P7 — HUD CHỈ hiển thị, KHÔNG dựng lại logic %).
    ///   • Thanh Nộ (gán asset OnRageChanged P8).
    /// Generic, gán asset + Image trong Inspector. Mỗi widget = 1 instance/người. Đặt FA.UI, chỉ ref Core.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceBarView : MonoBehaviour
    {
        [Header("Kênh sự kiện (fill 0..1)")]
        [Tooltip("FloatEventChannelSO bắn fill 0..1. % → OnPercentChanged (P7); Nộ → OnRageChanged (P8). Per-player asset.")]
        [SerializeField] private FloatEventChannelSO channel;

        [Header("Hình ảnh")]
        [Tooltip("Image có Image Type = Filled (Horizontal). fillAmount sẽ bị set theo payload.")]
        [SerializeField] private Image fillImage;

        [Tooltip("(Tùy) % không nằm trong 0..1 — nếu kênh bắn giá trị thô (vd 0..300%), bật chuẩn hóa.")]
        [SerializeField] private bool normalizeRawValue = false;

        [Tooltip("Giá trị MAX để chuẩn hóa khi normalizeRawValue bật (vd 300 cho %). Bỏ qua nếu kênh đã 0..1.")]
        [SerializeField] private float maxRawValue = 300f;

        [Header("Reset khi bật (HUD bật mỗi đầu trận)")]
        [Tooltip("TRUE → OnEnable áp initialFill (đã chuẩn hóa 0..1) ngay — không giữ fill của trận trước.")]
        [SerializeField] private bool resetOnEnable;
        [SerializeField] private float initialFill;

        [Header("Ô khoảng (chia 1 thanh thành nhiều Ô RỜI kiểu mana)")]
        [Tooltip("Ô này nhận khoảng [rangeStart..rangeEnd] của giá trị 0..1: fill = (v−start)/(end−start). " +
                 "Vd nộ 2 ô: ô đầu 0–0.5, ô sau 0.5–1 → đầy lần lượt. Để end ≤ start = tắt (cả thanh như cũ).")]
        [SerializeField] private float rangeStart;
        [SerializeField] private float rangeEnd;

        private void OnEnable()
        {
            if (resetOnEnable && fillImage != null) fillImage.fillAmount = Mathf.Clamp01(initialFill);
            if (channel != null) channel.OnEventRaised += OnValueChanged;
        }

        private void OnDisable()
        {
            if (channel != null) channel.OnEventRaised -= OnValueChanged;
        }

        private void OnValueChanged(float value)
        {
            if (fillImage == null) return;
            float fill = value;
            if (normalizeRawValue && maxRawValue > 0f) fill = value / maxRawValue;
            if (rangeEnd > rangeStart) fill = (fill - rangeStart) / (rangeEnd - rangeStart);
            fillImage.fillAmount = Mathf.Clamp01(fill);
        }
    }
}
