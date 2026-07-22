using UnityEngine;
using UnityEngine.UI;

namespace FA.UI
{
    /// <summary>
    /// (P12 Menu FX) GRADIENT DỌC cho 1 uGUI Graphic — nhân màu đỉnh/đáy vào vertex color (BaseMeshEffect,
    /// cùng pattern <see cref="UISkew"/>) → có gradient KHÔNG cần texture/sprite riêng. Dùng cho lớp nền
    /// "nhiệt": đáy cam-đỏ đậm → đỉnh trong suốt (lửa âm ỉ dưới mép màn hình). Thuần trình bày (§1.1/§1.3).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIGradient : BaseMeshEffect
    {
        [Tooltip("Màu nhân ở MÉP TRÊN (alpha 0 = tan dần lên trời).")]
        [SerializeField] private Color topColor = new Color(1f, 1f, 1f, 0f);

        [Tooltip("Màu nhân ở MÉP DƯỚI (đậm — gốc lửa).")]
        [SerializeField] private Color bottomColor = Color.white;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            var rt = transform as RectTransform;
            Rect r = rt.rect;
            if (r.height <= 0.0001f) return;

            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float t = Mathf.Clamp01((v.position.y - r.yMin) / r.height); // 0 đáy .. 1 đỉnh
                v.color *= Color.Lerp(bottomColor, topColor, t);
                vh.SetUIVertex(v, i);
            }
        }
    }
}
