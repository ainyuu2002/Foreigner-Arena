using UnityEngine;

namespace FA.Stage
{
    /// <summary>
    /// LỚP NỀN PARALLAX (P11 visual) — dịch layer theo delta vị trí camera × hệ số (0 = dán chặt màn hình
    /// như bầu trời, 1 = đứng yên trong world như tiền cảnh). Unity không có component parallax built-in
    /// cho URP 2D nên đây là code ĐIỀU PHỐI tối thiểu (§1.1): chỉ đọc Camera.main và dời transform layer
    /// trang trí — KHÔNG đụng gameplay, không ref module khác.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParallaxLayer : MonoBehaviour
    {
        [Tooltip("Hệ số bám camera theo trục X/Y (0..1): 0.9 = gần như dán màn hình (trời), 0.5 = lưng chừng " +
                 "(núi xa), 0.2 = gần tiền cảnh. CẦN TEST theo mắt.")]
        [SerializeField] private Vector2 followFactor = new Vector2(0.8f, 0.8f);

        private Transform _cam;
        private Vector3 _startPos;
        private Vector3 _camStartPos;

        private void OnEnable()
        {
            var cam = Camera.main;
            if (cam == null) { enabled = false; return; }
            _cam = cam.transform;
            _startPos = transform.position;
            _camStartPos = _cam.position;
        }

        private void LateUpdate()
        {
            if (_cam == null) return;
            Vector3 delta = _cam.position - _camStartPos;
            transform.position = new Vector3(
                _startPos.x + delta.x * followFactor.x,
                _startPos.y + delta.y * followFactor.y,
                _startPos.z);
        }
    }
}
