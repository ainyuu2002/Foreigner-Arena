using UnityEngine;
using UnityEngine.UI;

namespace FA.UI
{
    /// <summary>
    /// (P12 Menu FX) NHỊP THỞ cho 1 uGUI Graphic — dao động sin alpha (và tùy chọn scale + lerp 2 màu) theo
    /// thời gian. Dùng cho: lớp nền "nhiệt" cam-đỏ cuộn nhẹ, glow logo nóng đỏ, viền nút sạc năng lượng.
    /// Code-only tầng 1 (chưa có art): mô phỏng cảm giác lửa/năng lượng bằng nhấp nhô alpha/màu thay vì
    /// particle. <c>phase</c> đặt lệch nhau giữa các lớp để không nhấp nháy đồng loạt (trông máy móc).
    /// Decoupling (§1.3): thuần trình bày, KHÔNG ref gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIPulse : MonoBehaviour
    {
        [Header("Đối tượng")]
        [Tooltip("Graphic bị điều khiển (Image/Text). Bỏ trống = lấy trên chính GameObject.")]
        [SerializeField] private Graphic target;

        [Header("Alpha (dao động sin)")]
        [SerializeField] private float alphaMin = 0.25f;
        [SerializeField] private float alphaMax = 0.6f;

        [Tooltip("Chu kỳ/giây (1 = 1 nhịp mỗi ~6.28s; 2 = nhanh gấp đôi).")]
        [SerializeField] private float speed = 1f;

        [Tooltip("Pha lệch (radian) — đặt khác nhau giữa các lớp để không đồng nhịp.")]
        [SerializeField] private float phase = 0f;

        [Header("Màu (tùy — lerp A↔B theo cùng nhịp; bỏ qua nếu 2 màu bằng nhau)")]
        [SerializeField] private Color colorA = Color.white;
        [SerializeField] private Color colorB = Color.white;

        [Header("Scale (tùy — 0 = tắt)")]
        [Tooltip("Biên độ scale cộng thêm (vd 0.03 = 1.00↔1.03).")]
        [SerializeField] private float scaleAmplitude = 0f;

        private Vector3 _baseScale;

        private void Awake()
        {
            if (target == null) target = GetComponent<Graphic>();
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            if (target == null) return;
            // 0..1 theo sin — unscaledTime để menu không khựng khi timeScale đổi (hitstop/pause).
            float t = (Mathf.Sin(Time.unscaledTime * speed + phase) + 1f) * 0.5f;

            Color c = (colorA == colorB) ? target.color : Color.Lerp(colorA, colorB, t);
            c.a = Mathf.Lerp(alphaMin, alphaMax, t);
            target.color = c;

            if (scaleAmplitude > 0f)
                transform.localScale = _baseScale * (1f + scaleAmplitude * t);
        }
    }
}
