using System.Collections.Generic;
using UnityEngine;
using FA.Core;

namespace FA.Stage
{
    /// <summary>
    /// DROP-THROUGH PLATFORM (P11, quyết định người dùng) — kiểu Smash: đứng trên platform 1 chiều, GIỮ ↓
    /// (đủ dropHoldTime) thì TẠM TẮT va chạm với platform đó để rớt xuống, TỰ KHÔI PHỤC sau dropIgnoreTime.
    ///
    /// Unity-native nhất + per-player (không đụng layer toàn cục): <c>Physics2D.IgnoreCollision(playerCol,
    /// platformCol, true)</c> trong cửa sổ ngắn rồi tự bật lại — không raycast bỏ-qua-sàn thủ công (§1.1).
    /// PlatformEffector2D vẫn lo "đứng-từ-trên/xuyên-từ-dưới"; controller này CHỈ thêm "chủ động rớt xuống".
    ///
    /// DECOUPLED: đọc ý định ↓ qua <see cref="IMoveInput"/> (Core) — KHÔNG ref FA.Movement, KHÔNG đọc raw
    /// input, KHÔNG sửa state cũ. Gắn lên cùng object có CharacterMotor (implement IMoveInput) + collider
    /// thân. Tìm platform đang đứng bằng overlap dưới chân trên Layer OneWayPlatform (GameLayers — Core).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DropThroughController : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [Tooltip("Nguồn ý định ↓ (IMoveInput — CharacterMotor implement). Bỏ trống = tìm trên object/parent.")]
        [SerializeField] private MonoBehaviour moveInputSource;

        [Tooltip("Collider VẬT LÝ thân nhân vật (CapsuleCollider2D). Bỏ trống = lấy collider non-trigger.")]
        [SerializeField] private Collider2D bodyCollider;

        [Header("Phát hiện platform")]
        [Tooltip("Layer platform 1 chiều (OneWayPlatform). Khớp GameLayers/Project Settings.")]
        [SerializeField] private LayerMask oneWayPlatformLayers;

        [Tooltip("Offset Y từ tâm body xuống chân để overlap tìm platform đang đứng.")]
        [SerializeField] private float footYOffset = -1f;

        [Tooltip("Bán kính overlap dưới chân tìm platform.")]
        [Min(0.01f)] [SerializeField] private float footRadius = 0.2f;

        [Header("Thông số (CẦN TEST)")]
        [Tooltip("Phải GIỮ ↓ liên tục đủ N giây mới kích drop (tránh rớt ngoài ý muốn khi chỉ ngồi/block-low).")]
        [Min(0f)] [SerializeField] private float dropHoldTime = 0.18f;

        [Tooltip("Ngưỡng MoveY coi là 'đang ấn xuống' (input âm).")]
        [Range(-1f, 0f)] [SerializeField] private float downThreshold = -0.5f;

        [Tooltip("Số giây TẠM bỏ qua va chạm để rớt xuyên qua trước khi khôi phục. Đủ để rơi khỏi platform.")]
        [Min(0.05f)] [SerializeField] private float dropIgnoreTime = 0.35f;

        private IMoveInput _input;
        private float _holdTimer;
        private readonly Collider2D[] _overlap = new Collider2D[4];
        private readonly List<PendingRestore> _restores = new List<PendingRestore>(4);

        private struct PendingRestore { public Collider2D platform; public float restoreAt; }

        private void Awake()
        {
            _input = moveInputSource as IMoveInput;
            if (_input == null) _input = GetComponent<IMoveInput>();
            if (_input == null) _input = GetComponentInParent<IMoveInput>();
            if (_input == null)
                Debug.LogError("[DropThroughController] Không tìm thấy IMoveInput (CharacterMotor).", this);

            if (bodyCollider == null)
            {
                foreach (var c in GetComponentsInChildren<Collider2D>())
                    if (!c.isTrigger) { bodyCollider = c; break; }
            }
        }

        private void Update()
        {
            ProcessRestores();
            if (_input == null || bodyCollider == null) return;

            bool holdingDown = _input.MoveY <= downThreshold;
            if (!holdingDown) { _holdTimer = 0f; return; }

            _holdTimer += Time.deltaTime;
            if (_holdTimer < dropHoldTime) return;

            if (TryDrop()) _holdTimer = 0f;
        }

        private bool TryDrop()
        {
            Vector2 foot = (Vector2)bodyCollider.bounds.center + new Vector2(0f, footYOffset);
            int n = Physics2D.OverlapCircleNonAlloc(foot, footRadius, _overlap, oneWayPlatformLayers);
            bool dropped = false;
            for (int i = 0; i < n; i++)
            {
                Collider2D plat = _overlap[i];
                if (plat == null) continue;
                Physics2D.IgnoreCollision(bodyCollider, plat, true); // Unity-native tạm bỏ va chạm.
                _restores.Add(new PendingRestore { platform = plat, restoreAt = Time.time + dropIgnoreTime });
                dropped = true;
            }
            return dropped;
        }

        private void ProcessRestores()
        {
            for (int i = _restores.Count - 1; i >= 0; i--)
            {
                if (Time.time < _restores[i].restoreAt) continue;
                if (_restores[i].platform != null && bodyCollider != null)
                    Physics2D.IgnoreCollision(bodyCollider, _restores[i].platform, false); // khôi phục.
                _restores.RemoveAt(i);
            }
        }
    }
}
