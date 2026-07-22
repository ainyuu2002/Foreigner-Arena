using UnityEngine;

namespace FA.Stage
{
    /// <summary>
    /// GHIM THEO MÀN HÌNH (feedback test P11): giữ object tại 1 điểm viewport của camera chính —
    /// dùng cho icon CẢNH BÁO lava (bản cũ đặt world-space ở đáy map, camera không nhìn tới là
    /// không ai thấy lava sắp dâng). Thuần visual: chỉ dời transform, chạy LateUpdate SAU
    /// CinemachineBrain (execution order) để không trễ 1 frame. KHÔNG đụng gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(300)] // sau CinemachineBrain (LateUpdate) — bám đúng vị trí camera frame này.
    public sealed class CameraEdgeAnchor : MonoBehaviour
    {
        [Tooltip("Điểm neo viewport (0..1): (0.5, 0.1) = giữa mép DƯỚI màn hình. CẦN TEST theo HUD.")]
        [SerializeField] private Vector2 viewportAnchor = new Vector2(0.5f, 0.1f);

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 w = cam.ViewportToWorldPoint(
                new Vector3(viewportAnchor.x, viewportAnchor.y, cam.nearClipPlane));
            transform.position = new Vector3(w.x, w.y, transform.position.z);
        }
    }
}
