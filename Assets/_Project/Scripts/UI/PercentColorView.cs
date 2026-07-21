using UnityEngine;
using UnityEngine.UI;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// (P8 HUD) Thanh % kiểu "đổi MÀU" — KHÁC <see cref="ResourceBarView"/> (đổi fillAmount). Thanh LUÔN ĐẦY,
    /// đổi màu THEO BẬC (task 6, chốt 2026-07-18): trắng → xanh → vàng → cam → đỏ, mỗi bậc chiếm 1 khoảng %
    /// bằng nhau (maxRawValue / (số màu - 1) — vd max 200% với 5 màu thì cứ 50% nhảy sang màu kế), THAY VÌ
    /// lerp liên tục trắng→đỏ như bản cũ (người chơi liếc là biết mình đang ở "vùng" nào, như đèn cảnh báo).
    /// Hợp với Smash-% (càng cao càng nguy). Subscribe MỘT <see cref="FloatEventChannelSO"/> (raw 0..cap).
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

        [Tooltip("% MAX để chuẩn hóa (vd 250 = percentCap). Tại đây màu = bậc cuối (đỏ).")]
        [SerializeField] private float maxRawValue = 250f;

        [Header("Bậc màu theo % (đều nhau — bậc N bắt đầu tại N × max/(số màu-1))")]
        [Tooltip("Thứ tự an toàn → nguy hiểm. Mặc định: trắng, xanh, vàng, cam, đỏ.")]
        [SerializeField] private Color[] tierColors =
        {
            Color.white,                              // 0 — an toàn
            new Color(0.21f, 0.78f, 0.29f, 1f),       // xanh
            new Color(0.95f, 0.82f, 0.24f, 1f),       // vàng
            new Color(0.95f, 0.55f, 0.16f, 1f),       // cam
            new Color(0.90f, 0.18f, 0.18f, 1f),       // đỏ — sắp bay màu
        };

        private void OnEnable()
        {
            if (channel != null) channel.OnEventRaised += OnValueChanged;
            Apply(0f); // bắt đầu bậc 0 (trắng).
        }

        private void OnDisable()
        {
            if (channel != null) channel.OnEventRaised -= OnValueChanged;
        }

        private void OnValueChanged(float raw) => Apply(raw);

        private void Apply(float raw)
        {
            if (barImage == null || tierColors == null || tierColors.Length == 0) return;

            // Khoảng mỗi bậc = max / (số màu - 1) → đủ max là ĐÚNG bậc cuối (vd 200/4 = 50: 0..49 trắng,
            // 50..99 xanh, 100..149 vàng, 150..199 cam, ≥200 đỏ). 1 màu duy nhất thì luôn màu đó.
            int tier = 0;
            if (tierColors.Length > 1 && maxRawValue > 0f)
            {
                float tierSize = maxRawValue / (tierColors.Length - 1);
                tier = Mathf.Clamp(Mathf.FloorToInt(raw / tierSize), 0, tierColors.Length - 1);
            }
            barImage.color = tierColors[tier];
        }
    }
}
