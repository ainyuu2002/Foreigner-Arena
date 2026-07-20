using UnityEngine;
using UnityEngine.InputSystem;
using FA.Core;

namespace FA.Movement
{
    /// <summary>Thiết bị input gán cho 1 Motor (local 2-player: P1 Keyboard, P2 Gamepad).</summary>
    public enum InputScheme { AnyDevice, Keyboard, Gamepad }

    /// <summary>
    /// Cầu nối Input → StateMachine (research §3.4). MonoBehaviour DUY NHẤT drive cả FSM:
    /// Update → Tick, FixedUpdate → FixedTick. Sở hữu FA_Controls (mỗi Motor 1 wrapper để
    /// 2 người chơi không đè input — pairing device dứt khoát ở P6/P12).
    ///
    /// Đồng thời là ICharacterContext + IMoveInput: cấp ref (rb/animator/data) và "ý định"
    /// di chuyển cho state. Đổi nguồn input ở phase sau = đổi field _input / cách đọc, state
    /// đọc qua IMoveInput nên KHÔNG đổi. Lật hướng nhìn gom tại SetFacing (1 chỗ).
    ///
    /// Phase 2: thêm input nhảy (JumpPressed edge / MoveY plumbing), quản air-jump + ResetGravity,
    /// và "ground jump command" toàn cục (Idle/Walk → Jump) đặt TẠI Motor — KHÔNG sửa Idle/Walk
    /// (giống "Any State → Jump" trong Animator). Cũng phát hiện đi khỏi mép sàn → rơi (AirState).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(PlatformDropThrough))]
    public sealed class CharacterMotor : MonoBehaviour, ICharacterContext, IMoveInput, ICombatContext, IJumpIntent
    {
        // A2 (Ngồi): ngưỡng input NHẬN vào Ngồi — deadzone kiểu input (như MoveDeadzone trong các state),
        // KHÔNG phải số balance nên để const (đối xứng const trong IdleState/WalkState).
        private const float CrouchDownThreshold = 0.5f;   // giữ Xuống < -ngưỡng này mới ngồi
        private const float CrouchMoveDeadzone = 0.1f;    // |MoveX| < ngưỡng này = "thuần" (không ép ngang)

        [Header("Tham chiếu")]
        [Tooltip("Số liệu nhân vật (walkSpeed, mass, gravityScale, nhảy...). BẮT BUỘC gán asset CharacterData.")]
        [SerializeField] private CharacterData data;

        [Tooltip("Rigidbody2D thân. Bỏ trống = lấy trên cùng object.")]
        [SerializeField] private Rigidbody2D body;

        [Tooltip("Animator. Bỏ trống = lấy trên cùng object.")]
        [SerializeField] private Animator animator;

        [Tooltip("GroundCheck thân (Phase 2: landing/ground-jump). Bỏ trống = lấy trên cùng object.")]
        [SerializeField] private GroundCheck groundCheck;

        [Tooltip("WallCheck thân (Phase 3: bám/leo tường). Bỏ trống = lấy trên cùng object.")]
        [SerializeField] private WallCheck wallCheck;

        [Tooltip("Cấu hình buffer/parser (Phase 4). Bỏ trống = dùng mặc định an toàn (bufferFrames=10...).")]
        [SerializeField] private InputConfigSO inputConfig;

        [Header("Thiết bị input (local 2-player)")]
        [Tooltip("AnyDevice = nghe mọi thiết bị (1 người). Keyboard = chỉ bàn phím (P1). " +
                 "Gamepad = chỉ tay cầm (P2). Đặt P1=Keyboard, P2=Gamepad để 2 người không đè input.")]
        [SerializeField] private InputScheme inputScheme = InputScheme.AnyDevice;

        [Tooltip("Khi inputScheme=Gamepad & có NHIỀU tay cầm: chọn tay thứ mấy (0 = tay đầu tiên).")]
        [SerializeField] private int gamepadIndex = 0;

        [Tooltip("CHỈ có tác dụng khi inputScheme=Keyboard (prefab P1). Bật = P1 tự 'nhặt' tay cầm PHỤ nếu " +
                 "người chơi cắm thêm 1 tay nữa giữa trận → P1 điều khiển được CẢ keyboard lẫn tay cầm đó " +
                 "(keyboard vẫn là backup). Hot-plug tự nhận, không cần bật lại game.")]
        [SerializeField] private bool pairExtraGamepad = false;

        [Tooltip("Khi pairExtraGamepad: lấy tay cầm thứ mấy làm tay PHỤ cho P1. Mặc định 1 vì P2 (Gamepad) " +
                 "đã ăn tay index 0 — tay mới cắm rơi vào index 1 = đúng P1.")]
        [SerializeField] private int extraGamepadIndex = 1;

        // --- Nguồn input (P1: Input System trực tiếp; P4: chèn InputBuffer/CommandParser vào giữa) ---
        private FA_Controls _controls;
        private float _moveX;
        private float _moveY;
        // Edge nút Jump/Dash giờ suy TỪ buffer qua parser (WasPressedWithin) — KHÔNG đọc thẳng
        // WasPressedThisFrame nữa. _jumpPressed/_dashPressed là CACHE 1 frame (đọc 1 lần ở đầu Update
        // từ parser) để IMoveInput.JumpPressed/DashPressed giữ nguyên ngữ nghĩa & 4 thành viên cũ.
        private bool _jumpPressed;
        private bool _dashPressed;

        // --- Phase 4: Input Buffer + Command Parser (1 cặp/Motor → 2 player độc lập) ---
        private InputBuffer _inputBuffer;
        private CommandParser _parser;
        private IInputSnapshotSource _alternateInput;
        private int _fixedFrame;          // số fixed-step (deterministic) để stamp snapshot
        // Window đọc từ InputConfigSO (KHÔNG hardcode); fallback nếu chưa gán asset.
        private int BufferFrames => inputConfig != null ? inputConfig.bufferFrames : 10;
        private int EdgeWindow => inputConfig != null ? inputConfig.edgeWindow : 6;
        private int MotionWindow => inputConfig != null ? inputConfig.motionWindow : 12;
        private int TapMaxHold => inputConfig != null ? inputConfig.tapMaxHold : 7;
        private int DoubleTapDownWindow => inputConfig != null ? inputConfig.doubleTapDownWindow : 15;
        private float DropThroughIgnoreSeconds => inputConfig != null ? inputConfig.dropThroughIgnoreSeconds : 0.35f;

        // Cho overlay test P4 đọc trạng thái parser mà KHÔNG lộ field input nội bộ.
        public InputBuffer DebugBuffer => _inputBuffer;
        public CommandParser DebugParser => _parser;
        public int MotionWindowFrames => MotionWindow;
        public int TapMaxHoldFrames => TapMaxHold;

        // --- Trạng thái air (Phase 2) ---
        private int _airJumpsRemaining;

        // --- Quota air-dash (Phase 3, chỉnh dash): 1 air-dash/lần-nhảy ---
        // Reset khi grounded; refresh = maxAirDashes mỗi cú nhảy (RefreshAirDash); tiêu 1 khi vào
        // air-dash. Trên không không spam được air-dash (DashState chỉ spam khi grounded).
        private int _airDashesRemaining;

        // --- Arm re-grab WallHang sau wall-jump (Phase 3) ---
        // Cờ "được phép bám lại". WallJumpState.Enter gọi OnWallJumpStarted → disarm (false).
        // Arm lại (true) NGAY KHI WallCheck.IsTouchingRaw==false (đã rời tường THẬT ≥1 frame).
        // Thay cho lockout-timer: dùng sự kiện "đã rời tường thật" thay cho mốc thời gian → leo
        // bậc mượt, không re-grab giữa cú nhảy (chưa rời tường thì chưa armed).
        private bool _wallJumpReGrabArmed = true;

        // --- FSM (thuần, do Motor sở hữu) ---
        private readonly StateMachine _machine = new StateMachine();
        private IState _idle;
        private IState _walk;
        private IState _jump;
        private IState _air;
        private IState _doubleJump;
        private IState _dash;
        private WallHangState _wallHang; // typed: Motor đọc WallSide đã chốt để cấp cho WallJump
        private IState _wallJump;
        private IState _crouch;          // A2: ngồi pose-only

        // Rơi xuyên one-way platform (double-tap ↓) — component cùng object, tự gắn ở Awake (mọi nhân vật).
        private PlatformDropThrough _dropThrough;

        // ===== IMoveInput =====
        public float MoveX => _moveX;
        public float MoveY => _moveY;
        public bool JumpPressed => _jumpPressed;
        public bool DashPressed => _dashPressed;

        // ===== ICharacterContext =====
        public Rigidbody2D Body => body;
        public Animator Animator => animator;
        public CharacterData Data => data;
        public IMoveInput Input => this;
        public StateMachine Machine => _machine;

        public bool IsGrounded => groundCheck != null && groundCheck.IsGrounded;
        public int AirJumpsRemaining => _airJumpsRemaining;
        public float BaseGravityScale => data != null ? data.gravityScale : body.gravityScale;

        // (P10) Buff context (Rage Mode +Speed). Resolve 1 LẦN ở Awake từ chính object (root) qua
        // interface Core IBuffContext — null nếu prefab KHÔNG gắn RageModeController (FA.Specials). Giống
        // pattern Combat (HitboxAuthoring cache IBuffContext), chỉ qua interface → §1.3 không ref module.
        private IBuffContext _buff;
        public float MoveSpeedMult => _buff != null ? _buff.MoveSpeedMult : 1f;

        // ===== ICombatContext (P5) — chỉ EXPOSE field đã có; KHÔNG sửa logic di chuyển =====
        // Machine/Body/Animator/IsGrounded/Facing/SetFacing/MoveX/IdleState/WalkState đã public sẵn
        // (dùng chung với ICharacterContext/IMoveInput). Chỉ cần lộ thêm Parser + EdgeWindow cho Combat.
        // EdgeWindow ở interface implement explicit để KHÔNG đụng property private EdgeWindow nội bộ.
        public CommandParser Parser => _parser;
        int ICombatContext.EdgeWindow => EdgeWindow;

        // (P11 nấm nhún v2) IJumpIntent — môi trường (FA.Stage) hỏi "vừa bấm Jump?" để boost bật
        // đúng nhịp, qua interface Core (§1.3). CONSUME để DoubleJump không ăn lại cùng cú bấm
        // (nó sẽ ghi đè vy = jumpSpeed ngay sau boost).
        public bool TryConsumeRecentJumpPress(int withinFrames)
        {
            if (_parser == null || !_parser.WasPressedWithin(InputButton.Jump, withinFrames))
                return false;
            _parser.ConsumePress(InputButton.Jump, withinFrames);
            return true;
        }

        // Cờ "đang bị đánh/juggle" do hurt-state (FA.Combat) set qua SetHitstun — chỉ là CỜ TRẠNG THÁI,
        // KHÔNG đụng logic di chuyển. DefenseController (FA.Specials) đọc để chặn Air Dodge khi bị juggle.
        private bool _inHitstun;
        public bool IsInHitstun => _inHitstun;
        public void SetHitstun(bool value) => _inHitstun = value;

        // ===== Phase 3: facing + tường =====
        public int Facing => transform.localScale.x >= 0f ? 1 : -1;
        public bool IsWalled => wallCheck != null && wallCheck.IsWalled;
        public int WallSide => wallCheck != null ? wallCheck.WallSide : 0;
        public int WallHangSide => _wallHang != null ? _wallHang.WallSide : 0;

        public void ConsumeAirJump()
        {
            if (_airJumpsRemaining > 0) _airJumpsRemaining--;
        }

        public void ConsumeJumpEdge()
        {
            // Tiêu thụ edge Jump trong buffer + cache frame này → double-jump không re-trigger trong window.
            _jumpPressed = false;
            _parser?.ConsumePress(InputButton.Jump, EdgeWindow);
        }

        public void ConsumeDashEdge()
        {
            // Tiêu thụ edge Dash → 1 tap = 1 lần reset dash (spam cần nhấn lại), không re-trigger window.
            _dashPressed = false;
            _parser?.ConsumePress(InputButton.Dash, EdgeWindow);
        }

        // (P12 fix) RespawnSystem BroadcastMessage("OnRespawn") — reset TOÀN BỘ trạng thái điều khiển
        // về neutral (feedback test: chết giữa knockback → respawn vẫn dính tumble, "teleport về rồi
        // knockback tiếp"). ChangeState(_idle) Exit state cũ tử tế (tumble tự SetHitstun(false)).
        private void OnRespawn()
        {
            if (body != null) body.linearVelocity = Vector2.zero;
            ResetGravity();
            ResetAirJumps();
            RefreshAirDash();
            SetHitstun(false);
            _machine.ChangeState(_idle);
        }

        public void ResetAirJumps()
        {
            // (P11 nấm nhún v2) Trong cửa sổ suppress: KHÔNG phục hồi. Chặn "landing giả" 1-2 frame
            // khi lướt qua mặt nấm (cap thuộc StandableMask → Unity coi là đáp) xóa mất suppress.
            _airJumpsRemaining = Time.time < _airJumpSuppressUntil
                ? 0
                : (data != null ? data.maxAirJumps : 0);
        }

        // (P11 nấm nhún v2) IJumpIntent.SuppressAirJumps — nấm gọi sau mỗi cú bật: J giữa chuỗi nhún
        // không bị nuốt thành double-jump (đứt chuỗi). Đáp đất thật (sau cửa sổ) → ResetAirJumps phục hồi.
        private const float AirJumpSuppressWindow = 0.3f; // đủ che landing-giả trên mặt nấm. CẦN TEST.
        private float _airJumpSuppressUntil;

        public void SuppressAirJumps()
        {
            _airJumpsRemaining = 0;
            _airJumpSuppressUntil = Time.time + AirJumpSuppressWindow;
        }

        public void RefreshAirDash()
        {
            _airDashesRemaining = data != null ? data.maxAirDashes : 0;
        }

        public void ResetGravity()
        {
            if (data != null) body.gravityScale = data.gravityScale;
        }

        /// <summary>Tay cầm đang PAIR với Motor này — cho haptics/rumble (GamepadRumble). Scheme
        /// Gamepad → đúng pad theo gamepadIndex (P2 không rung nhầm tay P1); Keyboard → null (không
        /// rumble); AnyDevice → Gamepad.current (tay cầm active gần nhất, null nếu không cắm).
        /// Resolve MỖI LẦN GỌI (không cache) — bền với rút/cắm tay cầm giữa trận.</summary>
        public Gamepad PairedGamepad
        {
            get
            {
                switch (inputScheme)
                {
                    case InputScheme.Gamepad:
                        var pads = Gamepad.all;
                        return pads.Count > gamepadIndex ? pads[gamepadIndex] : null;
                    case InputScheme.Keyboard:
                        // P1: rung tay cầm PHỤ nếu đã pair (pairExtraGamepad); chỉ keyboard → null (không rung).
                        return ResolveExtraGamepad();
                    default:
                        return Gamepad.current;
                }
            }
        }

        public IState IdleState() => _idle;
        public IState WalkState() => _walk;
        public IState JumpState() => _jump;
        public IState AirState() => _air;
        public IState DoubleJumpState() => _doubleJump;
        public IState DashState() => _dash;
        public IState WallHangState() => _wallHang;
        public IState WallJumpState() => _wallJump;
        public IState CrouchState() => _crouch;

        public void OnWallJumpStarted()
        {
            // Disarm: cấm bám lại cho tới khi nhân vật thực sự RỜI tường (IsTouchingRaw==false).
            _wallJumpReGrabArmed = false;
        }

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (animator == null) animator = GetComponent<Animator>();
            if (groundCheck == null) groundCheck = GetComponent<GroundCheck>();
            if (wallCheck == null) wallCheck = GetComponent<WallCheck>();

            // Rơi xuyên one-way platform: tự gắn (RequireComponent lo prefab mới; GetComponent??AddComponent
            // lo prefab ĐÃ instantiate chưa có) → áp dụng cho MỌI nhân vật, KHÔNG wire tay prefab.
            _dropThrough = GetComponent<PlatformDropThrough>();
            if (_dropThrough == null) _dropThrough = gameObject.AddComponent<PlatformDropThrough>();

            // (P10) Cache buff context (Rage Mode) NULL-SAFE — RageModeController nằm cùng root khi P10
            // gắn; không gắn → null → MoveSpeedMult=×1.0 (P1/P3 không đổi). Chỉ qua interface Core (§1.3).
            _buff = GetComponent<IBuffContext>();
            _alternateInput = GetComponent<IInputSnapshotSource>();

            // Áp số liệu vật lý từ data (KHÔNG hardcode). gravityScale dùng thật ở P2.
            if (data != null)
            {
                body.mass = data.mass;
                body.gravityScale = data.gravityScale;
            }

            // Tạo state 1 lần (dùng lại — không new mỗi frame).
            _idle = new IdleState(this);
            _walk = new WalkState(this);
            _jump = new JumpState(this);
            _air = new AirState(this);
            _doubleJump = new DoubleJumpState(this);
            _dash = new DashState(this);
            _wallHang = new WallHangState(this);
            _wallJump = new WallJumpState(this);
            _crouch = new CrouchState(this);

            // Phase 4: dựng buffer + parser (1 cặp/Motor). State đọc input qua IMoveInput nên
            // KHÔNG biết nguồn đã đổi → không hồi quy P1-P3.
            _inputBuffer = new InputBuffer(BufferFrames);
            _parser = new CommandParser(_inputBuffer);

            ResetAirJumps();
            RefreshAirDash();
        }

        private void OnEnable()
        {
            _controls ??= new FA_Controls();
            ApplyInputScheme();
            _controls.Enable();
            // Hot-plug: cắm/rút thiết bị giữa chừng → re-pair (P2 chết input khi rút pad, sống lại khi cắm).
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Removed:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.Disconnected:
                    ApplyInputScheme();
                    break;
            }
        }

        /// <summary>
        /// Pairing thiết bị (local 2-player): giới hạn wrapper này chỉ đọc 1 control scheme + 1 thiết bị.
        /// bindingMask lọc binding theo group ("Keyboard"/"Gamepad"); devices lọc thiết bị vật lý. Đặt
        /// P1=Keyboard, P2=Gamepad → 2 Motor không đè input. AnyDevice = bỏ lọc (1 người, nghe tất cả).
        /// THIẾU thiết bị của scheme → wrapper bị KHÓA (devices rỗng, không nghe gì) — quyết định người
        /// dùng: chỉ có keyboard thì P2 (Gamepad) ĐỨNG YÊN, KHÔNG fallback nghe mọi thiết bị (2 motor
        /// cùng ăn keyboard). Hot-plug lo bởi OnDeviceChange → re-apply.
        /// </summary>
        private void ApplyInputScheme()
        {
            switch (inputScheme)
            {
                case InputScheme.Keyboard:
                {
                    var kb = Keyboard.current;
                    var extra = ResolveExtraGamepad();   // null nếu không bật/không đủ tay cầm
                    if (kb == null && extra == null) { LockNoDevice("Keyboard"); return; }

                    // Có tay cầm phụ → bỏ mask (null) để CẢ binding Keyboard lẫn Gamepad resolve (P1 dùng
                    // được cả 2). Không có → khóa về group "Keyboard" như cũ (không hồi quy P1 1-người).
                    _controls.bindingMask = (extra != null) ? (InputBinding?)null
                                                            : InputBinding.MaskByGroup("Keyboard");
                    _controls.devices = BuildDevices(kb, extra);
                    DebugLog.Log($"[CharacterMotor] {name}: paired Keyboard" +
                              $"{(extra != null ? $" + {extra.name}" : "")}.", this);
                    break;
                }

                case InputScheme.Gamepad:
                    _controls.bindingMask = InputBinding.MaskByGroup("Gamepad");
                    var pads = Gamepad.all;
                    if (pads.Count <= gamepadIndex) { LockNoDevice($"Gamepad[{gamepadIndex}]"); return; }
                    _controls.devices = new InputDevice[] { pads[gamepadIndex] };
                    break;

                default: // AnyDevice — bỏ lọc, nghe mọi thiết bị (mặc định 1 người chơi).
                    _controls.bindingMask = null;
                    _controls.devices = null;
                    break;
            }
        }

        /// <summary>Tay cầm PHỤ cho P1 khi bật pairExtraGamepad: Gamepad.all[extraGamepadIndex] nếu đủ tay,
        /// ngược lại null. Resolve mỗi lần gọi (không cache) — bền với cắm/rút giữa trận (OnDeviceChange
        /// re-apply). Index mặc định 1 vì P2 đã ăn tay index 0.</summary>
        private Gamepad ResolveExtraGamepad()
        {
            if (!pairExtraGamepad) return null;
            var pads = Gamepad.all;
            return pads.Count > extraGamepadIndex ? pads[extraGamepadIndex] : null;
        }

        /// <summary>Gộp keyboard + tay cầm phụ thành mảng devices (bỏ phần tử null).</summary>
        private static InputDevice[] BuildDevices(InputDevice a, InputDevice b)
        {
            if (a != null && b != null) return new[] { a, b };
            if (a != null) return new[] { a };
            if (b != null) return new[] { b };
            return new InputDevice[0];
        }

        /// <summary>Thiếu thiết bị của scheme → KHÓA wrapper (mảng devices RỖNG = không nghe thiết bị nào).
        /// Nhân vật đứng yên tới khi thiết bị được cắm (OnDeviceChange tự re-apply).</summary>
        private void LockNoDevice(string which)
        {
            _controls.devices = new InputDevice[0];
            Debug.LogWarning($"[CharacterMotor] inputScheme={inputScheme} nhưng không tìm thấy {which}. " +
                             "Nhân vật KHÔNG nhận input tới khi cắm thiết bị (tự nhận khi cắm).", this);
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            _controls?.Disable();
        }

        private void OnDestroy()
        {
            _controls?.Dispose();
        }

        private void Start()
        {
            // Vào FSM ở state Idle. (Sau Awake để chắc state đã khởi tạo.)
            _machine.ChangeState(_idle);
        }

        private void Update()
        {
            // (MỤC D) CINEMATIC GATE — intro vào trận / pose kết trận đang mở: nhân vật ĐỨNG YÊN, KHÔNG
            // nhận điều khiển. Zero ý định + vận tốc ngang rồi RETURN TRƯỚC mọi transition + _machine.Tick
            // → FSM giữ nguyên state hiện tại (không re-assert Idle/đè StateId Intro/Win/Lose do orchestrator
            // set). Super/chant cũng Acquire nhưng lúc đó timeScale=0 (thế giới đứng hình) nên guard KHÔNG
            // đổi hành vi cũ; chỉ thực sự tác dụng lúc intro/outro (timeScale=1). Chỉ zero vận tốc NGANG —
            // trọng lực vẫn kéo về đất để pose đứng trên sàn.
            if (CinematicGate.IsActive)
            {
                _moveX = _moveY = 0f;
                _jumpPressed = _dashPressed = false;
                if (body != null)
                {
                    Vector2 v = body.linearVelocity;
                    v.x = 0f;
                    body.linearVelocity = v;
                }
                return;
            }

            // Đọc input ở Update (không chạm physics). Phase 4: MoveX/MoveY giữ ANALOG (đọc thẳng
            // Input System — di chuyển cần độ mượt). Edge Jump/Dash suy TỪ buffer qua parser
            // (WasPressedWithin) → buffer-tha-thứ: nhập sớm vài frame vẫn ăn. Buffer được nuôi
            // bằng snapshot RAW push ở FixedUpdate.
            bool useAlternate = _alternateInput != null && _alternateInput.IsInputActive;
            Vector2 move = useAlternate
                ? _alternateInput.Move
                : _controls != null ? _controls.Gameplay.Move.ReadValue<Vector2>() : Vector2.zero;
            _moveX = move.x;
            _moveY = move.y;
            // Cache edge "chưa-tiêu-thụ" 1 frame; entry-point gọi ConsumePress để tiêu thụ
            // (giữ ngữ nghĩa "1 lần nhấn = 1 lần kích" của Motor P3).
            _jumpPressed = _parser != null && _parser.WasPressedWithin(InputButton.Jump, EdgeWindow);
            _dashPressed = _parser != null && _parser.WasPressedWithin(InputButton.Dash, EdgeWindow);

            // ARM lại re-grab WallHang ngay khi đã RỜI tường thật (raw, không tính grace) ≥1 frame.
            // Sau wall-jump cờ này = false; nhân vật bay ra → IsTouchingRaw=false → armed lại → cho
            // bám lại bậc trên. KHÔNG re-grab giữa cú nhảy vì lúc đó vẫn còn chạm tường (raw=true).
            if (wallCheck != null && !wallCheck.IsTouchingRaw)
                _wallJumpReGrabArmed = true;

            // Nạp lại air-dash khi đang đứng đất (mặt đất dash thoải mái, không tiêu quota). Trên không
            // chỉ nạp khi NHẢY (RefreshAirDash gọi ở Enter mỗi cú nhảy) → "1 air-dash tới lần nhảy kế".
            if (IsGrounded) RefreshAirDash();

            // Lệnh DASH TOÀN CỤC (Idle/Walk → Dash mặt đất; air states → Dash air nếu allowAirDash) —
            // đặt ở Motor để KHÔNG sửa Idle/Walk/Jump/Air (giống "Any State → Dash"). DashState tự
            // đọc DashPressed để SPAM/đổi-hướng khi đang dash → không xử lý re-enter ở đây.
            if (_dashPressed && !ReferenceEquals(_machine.CurrentState, _dash) && CanEnterDash())
            {
                _dashPressed = false;                                  // tiêu thụ trong frame này
                _parser.ConsumePress(InputButton.Dash, EdgeWindow);   // tiêu thụ edge trong buffer (không kích lại)
                // Air-dash (state hiện tại KHÔNG phải mặt đất) → tiêu 1 quota air-dash; dash mặt đất free.
                if (!IsGroundState(_machine.CurrentState)) _airDashesRemaining--;
                _machine.ChangeState(_dash);
            }
            // Lệnh BÁM TƯỜNG TOÀN CỤC — đặt ở Motor để KHÔNG sửa Jump/Air/DoubleJump/Dash (giống
            // "Any State → WallHang"). Bám khi: đang trên không (BẤT KỲ airborne state) + chạm tường +
            // đang ÉP input về phía tường. Bao trùm cả air-dash → DashState không cần check inline nữa.
            // (Quyết định người dùng P3: bám không bắt buộc đang dash — ép input vào tường là đủ.)
            else if (CanEnterWallHang())
            {
                _machine.ChangeState(_wallHang);
            }
            // Lệnh nhảy TOÀN CỤC từ đất (Idle/Walk → Jump) — đặt ở Motor để KHÔNG sửa Idle/Walk
            // (giống "Any State → Jump"). Chỉ khi đang đứng đất & là state mặt đất.
            else if (_jumpPressed && IsGrounded && IsGroundState(_machine.CurrentState))
            {
                _jumpPressed = false;                                 // tiêu thụ trong frame này
                _parser.ConsumePress(InputButton.Jump, EdgeWindow);   // 1 lần nhấn không kích double-jump cùng edge
                _machine.ChangeState(_jump);
            }
            // RƠI XUYÊN ONE-WAY PLATFORM (double-tap ↓): đặt TRƯỚC Crouch để double-tap ăn trước khi rơi
            // vào pose Ngồi (giữ ↓ liên tục chỉ 1 edge → không kích drop, vẫn Ngồi bình thường). CHỈ khi
            // đang đứng trên one-way (IsOnOneWayPlatform) — double-tap trên sàn Ground cứng no-op, rơi
            // xuống nhánh Crouch. Logic tắt/bật va chạm nằm trong PlatformDropThrough (Motor chỉ điều phối).
            else if (_parser != null && IsGrounded && IsGroundState(_machine.CurrentState)
                     && _dropThrough != null && _dropThrough.IsOnOneWayPlatform()
                     && _parser.WasDownDoubleTapped(DoubleTapDownWindow))
            {
                _parser.ConsumeDownDoubleTap(DoubleTapDownWindow);   // 1 cặp tap = 1 lần drop
                _dropThrough.TryDrop(DropThroughIgnoreSeconds);
            }
            // Lệnh NGỒI TOÀN CỤC (A2): từ Idle/Walk khi đứng đất + giữ Xuống THUẦN → Crouch. Đặt ở Motor
            // (giống "Any State → Crouch"), SAU dash/wallhang/jump nên các lệnh đó vẫn thoát khỏi Ngồi được
            // (Crouch ∈ IsGroundState). Crouch tự thoát về Idle/Walk khi nhả xuống/ép ngang (CrouchState.Tick).
            else if (IsGrounded && CanEnterCrouch())
            {
                _machine.ChangeState(_crouch);
            }
            // Đi khỏi mép sàn (không nhảy) → rơi. Chỉ khi đang ở ground-state mà không còn chạm sàn (gồm Crouch).
            else if (!IsGrounded && IsGroundState(_machine.CurrentState))
            {
                _machine.ChangeState(_air);
            }

            _machine.Tick(Time.deltaTime);

            // Param phụ trợ blend (set-only mỗi frame — không quyết logic).
            animator.SetFloat(MovementAnimatorParams.VelY, body.linearVelocity.y);
            animator.SetBool(MovementAnimatorParams.IsGrounded, IsGrounded);
        }

        private void FixedUpdate()
        {
            // Phase 4: ĐẨY snapshot input RAW vào buffer ĐÚNG 1 lần/FixedUpdate (deterministic 60Hz,
            // research §4 "Buffer push sai nhịp"). Edge/motion/tap-hold đều suy TỪ buffer này. Đọc giá
            // trị "đang giữ" mới nhất qua Input System (ReadValue/IsPressed) — hợp lệ trong FixedUpdate.
            PushInputSnapshot();

            // Mọi tác động Rigidbody2D xảy ra trong FixedTick của state.
            _machine.FixedTick(Time.fixedDeltaTime);
        }

        /// <summary>
        /// Chụp trạng thái input THÔ của fixed-step này (hướng + held mỗi nút) và Push vào buffer.
        /// DirX/DirY lượng tử hóa về {-1,0,1} (deadzone 0.5) để notation số-pad ổn định cho motion.
        /// </summary>
        private void PushInputSnapshot()
        {
            if (_inputBuffer == null) return;
            _fixedFrame++;

            if (_alternateInput != null && _alternateInput.IsInputActive)
            {
                InputSnapshot alternate = _alternateInput.ReadSnapshot(_fixedFrame);
                _inputBuffer.Push(in alternate);
                return;
            }

            if (_controls == null) return;

            Vector2 move = _controls.Gameplay.Move.ReadValue<Vector2>();
            sbyte dx = Quantize(move.x);
            sbyte dy = Quantize(move.y);

            var g = _controls.Gameplay;
            var snap = new InputSnapshot(
                dx, dy,
                g.Jump.IsPressed(), g.Dash.IsPressed(),
                g.L.IsPressed(), g.M.IsPressed(), g.H.IsPressed(),
                g.Super1.IsPressed(), g.Super2.IsPressed(), g.Rage.IsPressed(),
                _fixedFrame);
            _inputBuffer.Push(in snap);
        }

        /// <summary>Lượng tử trục analog về {-1,0,1} với deadzone 0.5 (ổn định cho motion notation).</summary>
        private static sbyte Quantize(float v)
        {
            if (v > 0.5f) return 1;
            if (v < -0.5f) return -1;
            return 0;
        }

        /// <summary>True nếu state là state mặt đất (Idle/Walk/Crouch) — nơi cho phép lệnh nhảy/dash từ đất
        /// & phát hiện đi khỏi mép sàn. Crouch (A2) là ground-state → nhảy/dash/rời-đất thoát khỏi Ngồi.</summary>
        private bool IsGroundState(IState s) =>
            ReferenceEquals(s, _idle) || ReferenceEquals(s, _walk) || ReferenceEquals(s, _crouch);

        /// <summary>Được vào Ngồi (A2): đang Idle/Walk (KHÔNG phải Crouch, tránh re-enter), giữ Xuống THUẦN
        /// (MoveY xuống, |MoveX| nhỏ → down-forward/back vẫn đi bộ). Grounded do người gọi kiểm.</summary>
        private bool CanEnterCrouch()
        {
            IState s = _machine.CurrentState;
            if (!ReferenceEquals(s, _idle) && !ReferenceEquals(s, _walk)) return false;
            return _moveY < -CrouchDownThreshold && Mathf.Abs(_moveX) < CrouchMoveDeadzone;
        }

        /// <summary>
        /// True nếu state hiện tại là MỘT TRONG các state CỦA CHÍNH Motor (Movement). Whitelist này
        /// chặn entry-point toàn cục Dash/WallHang khi FSM đang ở state LẠ — vd Hurt/Attack/Block do
        /// FA.Combat sở hữu trên CÙNG StateMachine (P6). Motor (FA.Movement) KHÔNG được reference class
        /// state FA.Combat (§1.3), nên Motor chỉ kiểm "state này có phải của tôi không": nếu KHÔNG →
        /// combat đang giữ FSM → KHÔNG cho mash Dash/WallHang thoát hurtstun (P6 DoD: victim NUỐT input
        /// suốt hitstun). Movement-state hợp lệ vẫn vào dash/wall bình thường (không hồi quy P1–P3).
        /// </summary>
        private bool IsOwnMovementState(IState s) =>
            ReferenceEquals(s, _idle) || ReferenceEquals(s, _walk) ||
            ReferenceEquals(s, _jump) || ReferenceEquals(s, _air) ||
            ReferenceEquals(s, _doubleJump) || ReferenceEquals(s, _dash) ||
            ReferenceEquals(s, _wallHang) || ReferenceEquals(s, _wallJump) ||
            ReferenceEquals(s, _crouch);

        /// <summary>
        /// Được phép vào Dash khi: đang ở Idle/Walk (dash mặt đất — free, không quota), HOẶC đang trên
        /// không (Jump/Air/DoubleJump/WallJump) nếu allowAirDash VÀ còn quota air-dash (_airDashesRemaining
        /// > 0). KHÔNG cho dash khi đang WallHang (đang bám tường — thoát bằng wall-jump/buông) để tránh
        /// re-trigger bám liền. Spam khi-đang-dash do DashState lo (chỉ spam khi grounded). Quota air-dash
        /// nạp lại mỗi cú NHẢY (RefreshAirDash) hoặc khi chạm đất → "1 air-dash tới lần nhảy kế".
        /// </summary>
        private bool CanEnterDash()
        {
            IState s = _machine.CurrentState;
            // Combat đang giữ FSM (Hurt/Attack/Block — state KHÔNG thuộc Movement) → chặn (P6: NUỐT input).
            if (!IsOwnMovementState(s)) return false;
            if (IsGroundState(s)) return true;
            if (ReferenceEquals(s, _wallHang)) return false;
            return data != null && data.allowAirDash && _airDashesRemaining > 0;
        }

        /// <summary>
        /// Được phép vào WallHang khi: đang TRÊN KHÔNG (không grounded) & state hiện tại là airborne
        /// (KHÔNG phải Idle/Walk, KHÔNG phải đang WallHang) & ĐANG CHẠM TƯỜNG THẬT FRAME NÀY
        /// (IsTouchingRaw — KHÔNG dùng IsWalled có grace) & ĐANG ÉP input về PHÍA tường
        /// (sign(MoveX) == WallSide). Điều kiện !IsGrounded + non-ground state đảm bảo "airborne
        /// đáng tin" → tránh bám nhầm khi đứng cạnh tường trên mặt đất.
        ///
        /// QUAN TRỌNG (fix re-grab): cổng VÀO bám phải dựa trên CHẠM TƯỜNG THẬT (raw) của frame này,
        /// KHÔNG dùng IsWalled (có grace ~0.1s). Grace chỉ để GIỮ bám (WallHangState.Tick), không để
        /// vào bám. Nếu dùng IsWalled+WallSide (cả hai bị grace giữ giá trị CŨ) thì ngay sau wall-jump
        /// khi người chơi vẫn ép stick vào tường, armed bật lại trong grace → re-grab NGAY bậc vừa rời
        /// → triệt tiêu momentum cú bật ("dính lại tường", không leo lên được). Dùng IsTouchingRaw:
        /// bay ra (raw=false) → cổng false → giữ trọn momentum; chạm bậc/tường mới (raw=true) →
        /// WallSide vừa cập nhật theo contact THẬT của frame đó → bám lại để leo.
        /// </summary>
        private bool CanEnterWallHang()
        {
            IState s = _machine.CurrentState;
            // Combat đang giữ FSM (vd Hurt airborne — state KHÔNG thuộc Movement) → chặn bám tường giữa
            // hurtstun (P6: NUỐT input suốt hitstun, không thoát hurtstun bằng wall-hang trên không).
            if (!IsOwnMovementState(s)) return false;
            if (IsGrounded || IsGroundState(s) || ReferenceEquals(s, _wallHang)) return false;
            // Chặn re-grab giữa cú wall-jump bằng cờ ARMED (sự kiện "đã rời tường thật") thay cho
            // lockout-timer: armed=false từ lúc wall-jump tới khi IsTouchingRaw==false (đã rời tường).
            // Khi chưa rời tường (vẫn raw-touching) → chưa armed → không re-grab giữa cú nhảy. Rời ra
            // rồi ép lại vào tường → armed → bám lại được (leo nhiều bậc mượt). KHÔNG dựa vào _wallJump
            // state (state đó tự thoát sang Air khi hết control-lock).
            if (!_wallJumpReGrabArmed) return false;
            // Chạm tường THẬT frame này (raw, KHÔNG grace) — WallSide khi đó là contact thật, đúng phía.
            if (wallCheck == null || !wallCheck.IsTouchingRaw || WallSide == 0) return false;
            return Mathf.Abs(_moveX) > 0.1f && (int)Mathf.Sign(_moveX) == WallSide;
        }

        /// <summary>
        /// Lật hướng nhìn bằng localScale.x = ±|x| (research §3.2) — lật cả child Hitbox/Hurtbox
        /// (P5) theo phía đòn, Unity-native. Dùng Abs để bền với scale ≠ 1. dir 0 = giữ nguyên.
        /// </summary>
        public void SetFacing(int dir)
        {
            if (dir == 0) return;
            Vector3 s = transform.localScale;
            float magnitude = Mathf.Abs(s.x);
            s.x = dir > 0 ? magnitude : -magnitude;
            transform.localScale = s;
        }
    }
}
