using System.Collections.Generic;
using FA.Core;
using UnityEngine;

namespace FA.Effects
{
    /// <summary>
    /// SUPER FLASH CINEMATIC (thiết kế Phase 17, mảnh "time stop + cut-in"): nghe Animation Event
    /// <c>SuperFlashStart</c>/<c>SuperFlashEnd</c> qua relay (per-instance — đúng người ra chiêu):
    ///  1. FREEZE toàn cục: <c>Time.timeScale = 0</c>; caster chạy tiếp bằng
    ///     <c>Animator.updateMode = UnscaledTime</c> → các event sau (kể cả SuperFlashEnd) vẫn bắn.
    ///  2. TỐI ĐEN HẲN nền: quad đen TRONG WORLD (sprite 1×1 runtime, sortingOrder <see cref="dimOrder"/>)
    ///     phủ map + đối thủ; RIÊNG CASTER nâng sortingOrder mọi Renderer con lên trên lớp đen (kiểu
    ///     MUGEN BGPalFX + sprpriority) → nhân vật NỔI trên nền đen, không bị đen theo.
    ///  3. HUD ẩn tạm: disable mọi Canvas khác (restore sau) — màn chỉ còn caster + cut-in.
    ///  4. ZOOM camera vào caster tới orthographicSize TUYỆT ĐỐI <see cref="zoomOrthoSize"/> (lerp
    ///     unscaled). Scene test dùng Camera thường → thao tác Camera.main + restore; map thật
    ///     Cinemachine (P11) thì thay ruột bằng vcam priority.
    ///  5. CUT-IN chân dung trượt vào theo phía caster đứng (<see cref="SuperFlashOverlay"/>).
    /// <c>EndFlash()</c> restore ĐỐI XỨNG toàn bộ (timeScale/animator/camera/sorting/canvas/dim/overlay)
    /// — gói 1 hàm; OnDisable force-restore; static guard 1 flash toàn trận tại 1 thời điểm.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SuperFlashController : MonoBehaviour
    {
        public enum PortraitSide { Auto = 0, Left = 1, Right = 2 }

        [Header("Tham chiếu")]
        [Tooltip("AnimationEventRelay cùng object Animator. Trống = tìm trong children.")]
        [SerializeField] private AnimationEventRelay relay;

        [Tooltip("Prefab SuperFlashOverlay (Canvas cut-in chân dung).")]
        [SerializeField] private SuperFlashOverlay overlayPrefab;

        [Tooltip("Chân dung cut-in của NHÂN VẬT NÀY (MUGEN sprite nhóm 9000) — mỗi super 1 ảnh; " +
                 "Animation Event SuperFlashStart(Int) chọn theo chỉ số (không điền Int → 0).")]
        [SerializeField] private Sprite[] portraits;

        [Header("Zoom (unscaled)")]
        [Tooltip("orthographicSize ĐÍCH khi zoom (tuyệt đối — nhân vật ~0.6u chiếm ~2/3 màn khi ~0.5). CẦN TEST.")]
        [Min(0.1f)] [SerializeField] private float zoomOrthoSize = 0.5f;

        [Tooltip("Thời gian zoom vào (giây, unscaled).")]
        [Min(0.01f)] [SerializeField] private float zoomInSeconds = 0.12f;

        [Tooltip("Camera ngắm cao hơn chân nhân vật bấy nhiêu unit (tâm thân).")]
        [SerializeField] private float aimHeight = 0.35f;

        [Tooltip("GIỮ caster đứng im bấy nhiêu giây đầu flash (khoe zoom + cut-in cho rõ) rồi mới chạy " +
                 "tiếp pose — kéo dài flash mà KHÔNG sửa clip (áp mọi super). 0 = không giữ. CẦN TEST.")]
        [Min(0f)] [SerializeField] private float holdSeconds = 1f;

        [Header("Tối màn")]
        [Tooltip("sortingOrder của quad đen (trên map/đối thủ; renderer caster được nâng lên trên nữa).")]
        [SerializeField] private int dimOrder = 90;

        [Tooltip("Phía hiện cut-in. Auto = phía ĐỐI DIỆN nửa màn caster đang đứng — không bao giờ " +
                 "che nhân vật (caster sát mép + portrait cùng phía = bị che, bug user bắt).")]
        [SerializeField] private PortraitSide side = PortraitSide.Auto;

        // Chỉ 1 flash toàn trận tại 1 thời điểm.
        private static bool s_flashActive;

        /// <summary>ChantController (cinematic lồng) tra để NHƯỜNG quyền ghi camera khi flash mở.</summary>
        internal static bool IsFlashActive => s_flashActive;
        private static Sprite s_blackSprite;   // 1×1 đen dùng chung, tạo lười 1 lần.

        private bool _active;
        private float _t;
        private float _prevTimeScale;
        private Camera _cam;
        private Vector3 _camStartPos, _camTargetPos;
        private float _camStartSize;
        private float _ownerScale = 1f;   // độ phóng nhân vật (lossyScale) — nhân vào zoom/aim.
        private Animator _animator;
        private AnimatorUpdateMode _prevUpdateMode;
        private float _prevAnimSpeed = 1f;
        private bool _holding;
        private SuperFlashOverlay _overlay;
        private GameObject _dimQuad;
        private readonly List<(Renderer r, int order)> _bumped = new List<(Renderer, int)>();
        private readonly List<Canvas> _hiddenCanvases = new List<Canvas>();

        private void Awake()
        {
            if (relay == null) relay = GetComponentInChildren<AnimationEventRelay>();
        }

        private void OnEnable()
        {
            if (relay == null) return;
            relay.SuperFlashStartEvent += StartFlash;
            relay.SuperFlashEndEvent += EndFlash;
        }

        private void OnDisable()
        {
            if (relay != null)
            {
                relay.SuperFlashStartEvent -= StartFlash;
                relay.SuperFlashEndEvent -= EndFlash;
            }
            EndFlash();   // an toàn: bị disable giữa flash → trả thế giới về như cũ.
        }

        private void StartFlash(int portraitIndex)
        {
            if (_active || s_flashActive) return;
            _active = true;
            s_flashActive = true;
            _t = 0f;
            CinematicGate.Acquire();   // chặn mọi entry-point kích move mới giữa flash (fix kẹt flash).

            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            _animator = GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                _prevUpdateMode = _animator.updateMode;
                _animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                // HOLD: đóng băng pose caster holdSeconds đầu (thế giới đã freeze sẵn) — flash dài ra,
                // người xem kịp thấy zoom + cut-in; hết hold pose chạy tiếp → SuperFlashEnd vẫn bắn.
                _prevAnimSpeed = _animator.speed;
                if (holdSeconds > 0f) { _animator.speed = 0f; _holding = true; }
            }

            _cam = Camera.main;
            if (_cam != null)
            {
                CameraCinematicLock.Acquire(_cam);   // tắt tạm Cinemachine Brain — nó đè zoom mỗi LateUpdate.
                _camStartPos = _cam.transform.position;
                _camStartSize = _cam.orthographicSize;
                // × độ phóng nhân vật (root ×2.5): zoomOrthoSize/aimHeight tinh chỉnh cho char scale 1
                // (~2/3 màn) — nhân lossyScale giữ ĐÚNG tỉ lệ đó mọi scale (fix "zoom to quá" sau ×2.5).
                _ownerScale = Mathf.Abs(transform.lossyScale.y);
                _camTargetPos = new Vector3(transform.position.x,
                                            transform.position.y + aimHeight * _ownerScale,
                                            _camStartPos.z);
            }

            // Quad đen world phủ nền (map + đối thủ chìm dưới).
            _dimQuad = new GameObject("SuperFlashDim");
            var sr = _dimQuad.AddComponent<SpriteRenderer>();
            sr.sprite = BlackSprite();
            sr.sortingOrder = dimOrder;
            _dimQuad.transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
            _dimQuad.transform.localScale = new Vector3(400f, 400f, 1f);   // sprite 1×1 @ PPU 1.

            // Nâng mọi Renderer của caster (thân + VFX đang bám) lên TRÊN lớp đen.
            _bumped.Clear();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                _bumped.Add((r, r.sortingOrder));
                r.sortingOrder += dimOrder + 10;
            }

            // Ẩn HUD/mọi canvas khác (overlay cut-in spawn SAU nên không dính).
            _hiddenCanvases.Clear();
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.enabled) continue;
                c.enabled = false;
                _hiddenCanvases.Add(c);
            }

            if (overlayPrefab != null)
            {
                bool left = side == PortraitSide.Left;
                if (side == PortraitSide.Auto && _cam != null)
                    left = _cam.WorldToViewportPoint(transform.position).x >= 0.5f;   // phía ĐỐI DIỆN caster.
                Sprite portrait = (portraits != null && portraits.Length > 0)
                    ? portraits[Mathf.Clamp(portraitIndex, 0, portraits.Length - 1)] : null;
                _overlay = Instantiate(overlayPrefab);
                _overlay.Show(portrait, left);
            }
        }

        private void EndFlash()
        {
            if (!_active) return;
            _active = false;
            s_flashActive = false;
            CinematicGate.Release();

            Time.timeScale = _prevTimeScale;
            if (_animator != null)
            {
                _animator.updateMode = _prevUpdateMode;
                if (_holding) _animator.speed = _prevAnimSpeed;   // bị end sớm giữa hold (OnDisable) — trả speed.
            }
            _holding = false;
            if (_cam != null)
            {
                _cam.transform.position = _camStartPos;
                _cam.orthographicSize = _camStartSize;
                CameraCinematicLock.Release();
            }
            foreach ((Renderer r, int order) in _bumped)
                if (r != null) r.sortingOrder = order;
            _bumped.Clear();
            foreach (Canvas c in _hiddenCanvases)
                if (c != null) c.enabled = true;
            _hiddenCanvases.Clear();
            if (_dimQuad != null) { Destroy(_dimQuad); _dimQuad = null; }
            if (_overlay != null) { _overlay.Hide(); _overlay = null; }
        }

        private void Update()
        {
            if (!_active) return;
            _t += Time.unscaledDeltaTime;
            if (_holding && _t >= holdSeconds)
            {
                _holding = false;
                if (_animator != null) _animator.speed = _prevAnimSpeed;
            }
        }

        // Ghi camera ở LATEUPDATE (assert MỖI frame): camera-follow của scene (Cinemachine/custom) cũng
        // ghi ở LateUpdate — assert liên tục sau nó để zoom cinematic không bị đè (bug user: "không thấy zoom").
        private void LateUpdate()
        {
            if (!_active || _cam == null) return;
            float k = Mathf.Clamp01(_t / zoomInSeconds);
            k = 1f - (1f - k) * (1f - k);   // ease-out.
            _cam.transform.position = Vector3.Lerp(_camStartPos, _camTargetPos, k);
            _cam.orthographicSize = Mathf.Lerp(_camStartSize, zoomOrthoSize * _ownerScale, k);
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
