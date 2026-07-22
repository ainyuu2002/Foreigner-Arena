using Unity.Cinemachine;
using UnityEngine;

namespace FA.Stage
{
    /// <summary>
    /// (Camera trận) TỰ NẠP 2 fighter vào <see cref="CinemachineTargetGroup"/> của map — player do
    /// PlayerSpawner (FA.GameFlow) Instantiate lúc RUNTIME nên không thể gán sẵn trong scene. Tìm theo
    /// LAYER Player1/Player2 trên root có Rigidbody2D (projectile dùng layer ProjectileP* nên không
    /// dính). KHÔNG ref module khác (§1.3). Zoom/framing/giới hạn zoom do CinemachineGroupFraming
    /// (Unity-native §1.1) trên CinemachineCamera đảm nhiệm — code này KHÔNG đụng lens.
    /// Respawn = reposition (player sống suốt trận) → nạp 1 lần là đủ; rematch destroy + spawn lại →
    /// member chết bị dọn và tự quét lại.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchCameraTargets : MonoBehaviour
    {
        [Tooltip("TargetGroup mà CinemachineCamera Follow. Bỏ trống = lấy trên chính GameObject này.")]
        [SerializeField] private CinemachineTargetGroup targetGroup;

        [Tooltip("Bán kính mỗi fighter trong group — nới khung quanh nhân vật. CẦN TEST.")]
        [SerializeField] private float memberRadius = 1.5f;

        [Tooltip("Chu kỳ quét lại khi chưa đủ 2 fighter (giây).")]
        [SerializeField] private float rescanInterval = 0.25f;

        private Transform _p1, _p2;
        private float _nextScan;

        private void Awake()
        {
            if (targetGroup == null) targetGroup = GetComponent<CinemachineTargetGroup>();
            if (targetGroup == null)
                Debug.LogError("[MatchCameraTargets] Thiếu CinemachineTargetGroup.", this);
        }

        private void Update()
        {
            if (targetGroup == null) return;
            if (_p1 != null && _p2 != null) return;          // đủ 2 người — không quét nữa.
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + rescanInterval;
            Scan();
        }

        private void Scan()
        {
            int layerP1 = LayerMask.NameToLayer("Player1");
            int layerP2 = LayerMask.NameToLayer("Player2");
            foreach (var rb in FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
            {
                if (rb.gameObject.layer == layerP1) _p1 = rb.transform;
                else if (rb.gameObject.layer == layerP2) _p2 = rb.transform;
            }

            // Dọn member đã destroy (rematch) rồi add member còn thiếu — AddMember/FindMember là API CM3.
            for (int i = targetGroup.Targets.Count - 1; i >= 0; i--)
                if (targetGroup.Targets[i].Object == null) targetGroup.Targets.RemoveAt(i);
            TryAdd(_p1);
            TryAdd(_p2);
        }

        private void TryAdd(Transform t)
        {
            if (t == null || targetGroup.FindMember(t) >= 0) return;
            targetGroup.AddMember(t, 1f, memberRadius);
        }
    }
}
