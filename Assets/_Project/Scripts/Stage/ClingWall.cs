using System.Collections.Generic;
using UnityEngine;
using FA.Core;

namespace FA.Stage
{
    /// <summary>
    /// TƯỜNG BÁM TRÊN KHÔNG (P11 feedback, cả 3 map): cột/tường lơ lửng — nhân vật TRÊN KHÔNG
    /// áp vào mặt bên là BÁM: trượt xuống chầm chậm (kẹp vy tại -wallClingSlideSpeed) thay vì rơi
    /// tự do → đủ thời gian nhảy tiếp / chỉnh hướng. Khối ĐẶC nằm layer Ground (đứng được trên
    /// đỉnh); vùng bám là trigger ốp quanh 2 mặt bên (child object). Chỉ can thiệp khi ĐANG RƠI —
    /// không ảnh hưởng nhảy lên / knockback bay lên (không phá cơ chế P7).
    /// Số ở StageConfigSO.wallClingSlideSpeed (CẦN TEST).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClingWall : MonoBehaviour
    {
        [Tooltip("StageConfigSO của map — wallClingSlideSpeed. BẮT BUỘC.")]
        [SerializeField] private StageConfigSO config;

        [Tooltip("Vùng bám (BoxCollider2D trigger rộng hơn khối tường ~0.4 mỗi bên).")]
        [SerializeField] private BoxCollider2D clingArea;

        [Tooltip("Layer player.")]
        [SerializeField] private LayerMask playerLayers;

        private readonly List<Collider2D> _hits = new List<Collider2D>(8);

        private void Awake()
        {
            if (config == null) Debug.LogError("[ClingWall] Thiếu StageConfigSO.", this);
            if (clingArea == null) clingArea = GetComponent<BoxCollider2D>();
        }

        private void FixedUpdate()
        {
            if (config == null || clingArea == null) return;

            var filter = new ContactFilter2D { useLayerMask = true, layerMask = playerLayers, useTriggers = true };
            _hits.Clear();
            clingArea.Overlap(filter, _hits);
            foreach (var c in _hits)
            {
                var rb = c.attachedRigidbody;
                if (rb == null) continue;
                var v = rb.linearVelocity;
                if (v.y < -config.wallClingSlideSpeed) // chỉ kẹp khi RƠI nhanh hơn tốc độ trượt.
                {
                    v.y = -config.wallClingSlideSpeed;
                    rb.linearVelocity = v;
                }
            }
        }
    }
}
