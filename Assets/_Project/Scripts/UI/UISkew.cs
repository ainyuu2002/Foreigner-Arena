using UnityEngine;
using UnityEngine.UI;

namespace FA.UI
{
    /// <summary>
    /// (P8/P12 HUD) Hiệu ứng BẺ NGHIÊNG mesh của 1 uGUI Graphic (Image/Text...) thành HÌNH BÌNH HÀNH kiểu
    /// Street Fighter 6 — KHÔNG cần sprite riêng. Shear toạ độ x theo chiều cao: đỉnh lệch +<c>slant·H/2</c>,
    /// đáy lệch −<c>slant·H/2</c> (nghiêng quanh tâm). Vì offset tính theo CHIỀU CAO nên góc nghiêng GIỮ
    /// NGUYÊN ở mọi bề rộng thanh (khác xoay/scale). Đặt cùng GameObject với Image cần nghiêng.
    ///
    /// Decoupling (§1.3): thuần uGUI, KHÔNG ref gameplay. Chỉ đổi hình học hiển thị (§1.1) — không đụng logic.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UISkew : BaseMeshEffect
    {
        [SerializeField]
        [Tooltip("Tỉ lệ nghiêng theo chiều cao: offset đỉnh-đáy = slant·H. 0.45 ≈ 24°. Âm = nghiêng ngược.")]
        private float slant = 0.45f;

        public float Slant { get => slant; set { slant = value; if (graphic != null) graphic.SetVerticesDirty(); } }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            var rt = transform as RectTransform;
            Rect r = rt.rect;
            float h = r.height;
            if (h <= 0.0001f) return;

            float off = slant * h;
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float t = (v.position.y - r.yMin) / h;   // 0 (đáy) .. 1 (đỉnh)
                v.position.x += off * (t - 0.5f);          // shear quanh tâm → nghiêng đều, không lệch tổng thể.
                vh.SetUIVertex(v, i);
            }
        }
    }
}
