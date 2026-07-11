using System.Collections.Generic;
using UnityEngine;
using FA.Core;

namespace FA.Stage
{
    /// <summary>
    /// NẤM NHÚN (P11 nâng cấp Swamp): đáp XUỐNG mũ nấm khổng lồ → bật tung lên cao. Bù trừ cơ chế
    /// giảm lực nhảy 25% của đầm lầy + là đường thoát khỏi bệ chìm giữa map / đường tắt lên tầng cao.
    /// Cơ chế ĐỊA HÌNH CỐ ĐỊNH, deterministic — GDD Trang 9: swamp KHÔNG thiên tai ngẫu nhiên.
    /// Chỉ bật khi player đang RƠI (vy &lt; 0) chạm vùng mũ — không ảnh hưởng đi ngang/nhảy xuyên lên.
    /// Số ở StageConfigSO.mushroomBounceSpeed (CẦN TEST). Kèm hiệu ứng NHÚN co-giãn mũ (không cần Animator).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MushroomBouncer : MonoBehaviour
    {
        [Tooltip("StageConfigSO của map Swamp — mushroomBounceSpeed. BẮT BUỘC.")]
        [SerializeField] private StageConfigSO config;

        [Tooltip("Vùng MŨ nấm (BoxCollider2D trigger dải mỏng trên đỉnh mũ).")]
        [SerializeField] private BoxCollider2D capArea;

        [Tooltip("Layer player.")]
        [SerializeField] private LayerMask playerLayers;

        [Tooltip("Transform co giãn khi nhún (bỏ trống = chính object này).")]
        [SerializeField] private Transform squashTarget;

        [Tooltip("Chỉ tính là 'đáp xuống' khi vy dưới ngưỡng này (chặn trigger khi lướt ngang). CẦN TEST.")]
        [SerializeField] private float fallSpeedThreshold = -0.5f;

        private readonly List<Collider2D> _hits = new List<Collider2D>(8);
        private readonly Dictionary<Rigidbody2D, float> _cooldownUntil = new Dictionary<Rigidbody2D, float>(4);
        private Vector3 _baseScale;
        private float _squashT = 1f; // 1 = đứng yên, 0 = vừa bị đáp.

        private void Awake()
        {
            if (config == null) Debug.LogError("[MushroomBouncer] Thiếu StageConfigSO.", this);
            if (capArea == null) capArea = GetComponent<BoxCollider2D>();
            if (squashTarget == null) squashTarget = transform;
            _baseScale = squashTarget.localScale;
        }

        private void FixedUpdate()
        {
            if (config == null || capArea == null) return;

            var filter = new ContactFilter2D { useLayerMask = true, layerMask = playerLayers, useTriggers = true };
            _hits.Clear();
            capArea.Overlap(filter, _hits);
            foreach (var c in _hits)
            {
                var rb = c.attachedRigidbody;
                if (rb == null) continue;
                var v = rb.linearVelocity;
                if (v.y > fallSpeedThreshold) continue; // chỉ bật khi ĐANG RƠI xuống mũ.
                if (_cooldownUntil.TryGetValue(rb, out float until) && Time.time < until) continue;
                _cooldownUntil[rb] = Time.time + 0.25f;

                v.y = config.mushroomBounceSpeed; // giữ vx — điều khiển hướng bay như Smash.
                rb.linearVelocity = v;
                _squashT = 0f; // kích hoạt hiệu ứng nhún.
            }
        }

        private void Update()
        {
            if (_squashT >= 1f) return;
            _squashT = Mathf.Min(1f, _squashT + Time.deltaTime * 3.2f);
            float k = Mathf.Sin(_squashT * Mathf.PI); // 0→1→0: ép dẹt rồi nảy về nguyên trạng.
            squashTarget.localScale = new Vector3(
                _baseScale.x * (1f + 0.22f * k),
                _baseScale.y * (1f - 0.3f * k), 1f);
        }
    }
}
