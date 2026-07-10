using System.Collections.Generic;
using FA.Core;
using UnityEngine;

namespace FA.Effects
{
    /// <summary>
    /// CINEMATIC NIỆM CHÚ ultimate (mảnh 1 của UBW): nghe Animation Event <c>ChantStart</c>/<c>ChantEnd</c>
    /// qua relay. Nhẹ hơn <see cref="SuperFlashController"/> — dùng CHO ĐOẠN ĐỌC CHÚ DÀI trước flash:
    ///  1. FREEZE thế giới (Time.timeScale=0) + caster chạy unscaled → đối thủ đứng nhìn (MUGEN
    ///     SuperPause + TargetState 8400).
    ///  2. TỐI NHẸ màn (quad world alpha <see cref="dimAlpha"/>, order <see cref="dimOrder"/> — THẤP hơn
    ///     flash 90 để flash đè lên khi tới đoạn kết): map + đối thủ mờ đi nhưng VẪN THẤY; caster bump
    ///     lên trên lớp mờ.
    ///  3. ZOOM 1/2 màn (orthoSize <see cref="zoomOrthoSize"/> — to hơn flash 0.5).
    ///  KHÔNG portrait, KHÔNG tắt Canvas (dòng chữ niệm chú cần hiện — ChantTextOverlay).
    /// Flash thật (SuperFlashStart) CÓ THỂ chạy LỒNG bên trong: nó tự lưu/khôi phục trạng thái hiện
    /// tại (timeScale=0 của chant, camera đang zoom 0.7) nên hai lớp không giẫm nhau.
    /// ChantEnd restore đối xứng; OnDisable force-restore.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChantController : MonoBehaviour
    {
        [Tooltip("AnimationEventRelay cùng object Animator. Trống = tìm trong children.")]
        [SerializeField] private AnimationEventRelay relay;

        [Tooltip("orthographicSize ĐÍCH khi zoom (nhân vật ~1/2 màn — flash là 0.5). CẦN TEST.")]
        [Min(0.1f)] [SerializeField] private float zoomOrthoSize = 0.72f;

        [Tooltip("Thời gian zoom vào (giây, unscaled).")]
        [Min(0.01f)] [SerializeField] private float zoomInSeconds = 0.5f;

        [Tooltip("Camera ngắm cao hơn chân nhân vật bấy nhiêu unit.")]
        [SerializeField] private float aimHeight = 0.35f;

        [Tooltip("Độ tối của màn khi niệm chú (0..1 — tối NHẸ, vẫn thấy đối thủ).")]
        [Range(0f, 1f)] [SerializeField] private float dimAlpha = 0.65f;

        [Tooltip("sortingOrder quad tối (THẤP hơn dim của SuperFlash=90 để flash đè lên được).")]
        [SerializeField] private int dimOrder = 80;

        private static Sprite s_blackSprite;

        private bool _active;
        private float _t;
        private float _prevTimeScale;
        private Camera _cam;
        private Vector3 _camStartPos, _camTargetPos;
        private float _camStartSize;
        private Animator _animator;
        private AnimatorUpdateMode _prevUpdateMode;
        private GameObject _dimQuad;
        private readonly List<(Renderer r, int order)> _bumped = new List<(Renderer, int)>();

        private void Awake()
        {
            if (relay == null) relay = GetComponentInChildren<AnimationEventRelay>();
        }

        private void OnEnable()
        {
            if (relay == null) return;
            relay.ChantStartEvent += StartChant;
            relay.ChantEndEvent += EndChant;
        }

        private void OnDisable()
        {
            if (relay != null)
            {
                relay.ChantStartEvent -= StartChant;
                relay.ChantEndEvent -= EndChant;
            }
            EndChant();
        }

        private void StartChant()
        {
            if (_active) return;
            _active = true;
            _t = 0f;
            CinematicGate.Acquire();   // chặn mọi entry-point kích move mới giữa chant (fix kẹt flash).

            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            _animator = GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                _prevUpdateMode = _animator.updateMode;
                _animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }

            _cam = Camera.main;
            if (_cam != null)
            {
                CameraCinematicLock.Acquire(_cam);   // tắt tạm Cinemachine Brain — nó đè zoom mỗi LateUpdate.
                _camStartPos = _cam.transform.position;
                _camStartSize = _cam.orthographicSize;
                _camTargetPos = new Vector3(transform.position.x, transform.position.y + aimHeight,
                                            _camStartPos.z);
            }

            _dimQuad = new GameObject("ChantDim");
            var sr = _dimQuad.AddComponent<SpriteRenderer>();
            sr.sprite = BlackSprite();
            sr.color = new Color(0f, 0f, 0f, dimAlpha);   // TỐI NHẸ — map/đối thủ vẫn lờ mờ thấy.
            sr.sortingOrder = dimOrder;
            _dimQuad.transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
            _dimQuad.transform.localScale = new Vector3(400f, 400f, 1f);

            _bumped.Clear();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                _bumped.Add((r, r.sortingOrder));
                r.sortingOrder += dimOrder + 5;   // caster nổi trên lớp mờ (vẫn dưới dim flash 90).
            }
        }

        private void EndChant()
        {
            if (!_active) return;
            _active = false;
            CinematicGate.Release();
            Time.timeScale = _prevTimeScale;
            if (_animator != null) _animator.updateMode = _prevUpdateMode;
            if (_cam != null)
            {
                _cam.transform.position = _camStartPos;
                _cam.orthographicSize = _camStartSize;
                CameraCinematicLock.Release();
            }
            foreach ((Renderer r, int order) in _bumped)
                if (r != null) r.sortingOrder = order;
            _bumped.Clear();
            if (_dimQuad != null) { Destroy(_dimQuad); _dimQuad = null; }
        }

        private void Update()
        {
            if (_active) _t += Time.unscaledDeltaTime;
        }

        // Ghi camera ở LATEUPDATE mỗi frame (sau camera-follow của scene — không bị đè, cùng lý do
        // SuperFlashController). NHƯỜNG khi SuperFlash lồng bên trong đang mở (nó cần toàn quyền zoom
        // sâu hơn; kết thúc nó tự restore về đúng mức chant rồi ta assert tiếp).
        private void LateUpdate()
        {
            if (!_active || _cam == null || SuperFlashController.IsFlashActive) return;
            float k = Mathf.Clamp01(_t / zoomInSeconds);
            k = 1f - (1f - k) * (1f - k);
            _cam.transform.position = Vector3.Lerp(_camStartPos, _camTargetPos, k);
            _cam.orthographicSize = Mathf.Lerp(_camStartSize, zoomOrthoSize, k);
        }

        private static Sprite BlackSprite()
        {
            if (s_blackSprite != null) return s_blackSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.black);
            tex.Apply();
            s_blackSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return s_blackSprite;
        }
    }
}
