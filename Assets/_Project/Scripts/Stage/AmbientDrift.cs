using UnityEngine;

namespace FA.Stage
{
    /// <summary>
    /// CHUYỂN ĐỘNG NỀN (polish P11): dao động sin quanh vị trí ban đầu cho lớp trang trí —
    /// cực quang trôi, váng sáng lava dập dềnh, dây leo đung đưa. Thuần decorative, dùng
    /// localPosition nên an toàn cả khi là CHILD của object vật lý (không đánh nhau với Rigidbody cha).
    /// KHÔNG đụng gameplay/collision.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbientDrift : MonoBehaviour
    {
        [Tooltip("Biên độ dao động theo X/Y (world unit). CẦN TEST theo mắt.")]
        [SerializeField] private Vector2 amplitude = new Vector2(0.5f, 0f);

        [Tooltip("Chu kỳ 1 vòng dao động (giây).")]
        [SerializeField, Min(0.1f)] private float period = 12f;

        private Vector3 _startLocal;
        private float _phase;

        private void OnEnable()
        {
            _startLocal = transform.localPosition;
            _phase = Random.value * Mathf.PI * 2f; // lệch pha ngẫu nhiên mỗi object cho tự nhiên.
        }

        private void LateUpdate()
        {
            float w = Mathf.PI * 2f / period;
            float t = Time.time;
            transform.localPosition = _startLocal + new Vector3(
                Mathf.Sin(t * w + _phase) * amplitude.x,
                Mathf.Sin(t * w * 0.83f + _phase * 1.7f) * amplitude.y,
                0f);
        }
    }
}
