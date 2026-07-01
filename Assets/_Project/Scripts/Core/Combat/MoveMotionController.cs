using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// (P14) DI CHUYỂN TRONG ĐÒN (Move Motion: Lunge + Drift). MonoBehaviour đặt CÙNG prefab với
    /// Rigidbody2D + CharacterMotor (ICombatContext) + AnimationEventRelay. Cho phép đòn TIẾN/LÙI/LAO theo
    /// HƯỚNG NHÌN thay vì luôn đứng yên — nhưng đòn KHÔNG khai báo gì thì đứng yên y hệt P5/P9 (no-regression).
    ///
    /// QUY TẮC VÀNG (§1.1): TIMING 100% do Animation Event (MoveImpulse / MoveDriftOn / MoveDriftOff) trên
    /// clip → AnimationEventRelay → controller này. KHÔNG đếm frame trong code. Độ lớn = tham số gameplay
    /// đọc từ MoveData (facing-relative); hướng nhân ICombatContext.Facing tại thời điểm apply.
    ///
    /// UNITY-NATIVE (research §3): mọi chuyển động qua Rigidbody2D.linearVelocity (KHÔNG dời transform →
    /// không xuyên sàn/tường; gravity/giảm-tốc do Rigidbody2D/PhysicsMaterial2D lo, KHÔNG tích phân tay).
    ///
    /// QUYỀN SỞ HỮU vx (research §4): controller KHÔNG có FixedUpdate riêng ghi vx (tránh tranh chấp với
    /// state). Nó chỉ (a) NHẬN impulse từ relay (set velocity 1 phát, tức thời), (b) cung cấp
    /// <see cref="ApplyGroundVelocity"/> để AttackState/SpecialState GỌI trong FixedTick khi
    /// <see cref="OwnsGroundVelocity"/>. Vì CharacterMotor.FixedUpdate chỉ drive machine.FixedTick một lần,
    /// và khi đang đánh Motor KHÔNG chạy logic walk → state là CHỦ vx DUY NHẤT → 0 tranh chấp.
    ///
    /// CHỈ reference FA.Core (Rigidbody2D + ICombatContext) — KHÔNG ref FA.Combat/FA.Specials/FA.Movement
    /// (§1.3). Đặt ở Core để cả Combat (P5) lẫn Specials (P9/P10) dùng chung.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MoveMotionController : MonoBehaviour
    {
        [Header("Tham chiếu (cùng prefab — tự lấy ở Awake nếu để trống)")]
        [Tooltip("Thân vật lý nhận impulse/drift. Tự GetComponent nếu null.")]
        [SerializeField] private Rigidbody2D body;

        [Tooltip("AnimationEventRelay cùng object Animator — subscribe MoveImpulse/MoveDriftOn/Off.")]
        [SerializeField] private AnimationEventRelay relay;

        // ICombatContext (CharacterMotor) — đọc Facing để nhân hướng. Phụ thuộc Core interface, KHÔNG class.
        private ICombatContext _ctx;

        // MoveData đòn HIỆN HÀNH — state set qua SetMove TRƯỚC khi clip chạy tới MoveImpulse/Drift.
        private MoveData _move;

        // Cờ cửa sổ drift — bật bởi MoveDriftOn, tắt bởi MoveDriftOff (KHÔNG timer). Reset mỗi SetMove.
        private bool _driftOpen;

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            _ctx = GetComponent<ICombatContext>();
            if (relay == null) relay = GetComponentInChildren<AnimationEventRelay>();

            if (body == null)
                Debug.LogError("[MoveMotionController] Thiếu Rigidbody2D trên prefab.", this);
            if (_ctx == null)
                Debug.LogError("[MoveMotionController] Thiếu ICombatContext (CharacterMotor) trên prefab.", this);
        }

        // Subscribe per-instance (đúng nhân vật này). Vòng đời = component (không gắn vào Enter/Exit của
        // state vì controller dùng chung cho nhiều state); unsubscribe ở OnDisable để tránh leak/double-fire.
        private void OnEnable()
        {
            if (relay == null) return;
            relay.MoveImpulseEvent += OnMoveImpulse;
            relay.MoveDriftOnEvent += OnMoveDriftOn;
            relay.MoveDriftOffEvent += OnMoveDriftOff;
        }

        private void OnDisable()
        {
            if (relay == null) return;
            relay.MoveImpulseEvent -= OnMoveImpulse;
            relay.MoveDriftOnEvent -= OnMoveDriftOn;
            relay.MoveDriftOffEvent -= OnMoveDriftOff;
        }

        /// <summary>State gọi khi MỘT đòn bắt đầu (Enter / kích đòn): lưu MoveData + reset cờ drift (không
        /// rò drift sang đòn nối nếu clip trước quên MoveDriftOff). null = đòn không-motion (đứng yên).</summary>
        public void SetMove(MoveData move)
        {
            _move = move;
            _driftOpen = false;
        }

        /// <summary>True khi đòn hiện hành có khai báo chuyển động → state UỶ QUYỀN vx cho controller.
        /// False ⇒ state giữ NGUYÊN hành vi cũ (ghì vx=0) — cổng no-regression.</summary>
        public bool OwnsGroundVelocity => _move != null && _move.HasMoveMotion;

        /// <summary>State GỌI trong FixedTick (chỉ khi OwnsGroundVelocity). Drift mở → gán
        /// vx = facing×driftSpeed (đè trôi tự nhiên). Drift đóng → KHÔNG làm gì: để momentum của impulse tự
        /// trôi giảm dần qua linearDamping/ma sát (research §4 — tránh "ghì 0 giết cú lao"). TUYỆT ĐỐI
        /// KHÔNG set vx=0 ở đây.</summary>
        public void ApplyGroundVelocity()
        {
            if (!_driftOpen || _move == null || body == null) return;
            Vector2 v = body.linearVelocity;
            v.x = Facing * _move.driftSpeed;
            body.linearVelocity = v;
        }

        private int Facing => _ctx != null ? _ctx.Facing : 1;

        // ===== Animation Event callbacks (per-instance relay) =====

        /// <summary>Cú đẩy tức thời tại frame MoveImpulse(i). SET linearVelocity (deterministic, không phụ
        /// thuộc mass — research §3). vx = facing×impulses[i].x. y!=0 mới set vy (lao chéo lên, rồi gravity
        /// kéo xuống tự nhiên); y==0 GIỮ vy hiện tại (không phá rơi/đứng). Index ngoài phạm vi → bỏ qua an toàn.
        /// Ghi chú: nếu designer muốn impulse CỘNG DỒN lên đà có sẵn, đổi sang AddForce(Impulse) — mặc định SET.</summary>
        private void OnMoveImpulse(int index)
        {
            if (_move == null || body == null) return;
            var list = _move.motionImpulses;
            if (list == null || index < 0 || index >= list.Count) return;

            Vector2 imp = list[index];
            Vector2 v = body.linearVelocity;
            v.x = Facing * imp.x;
            if (!Mathf.Approximately(imp.y, 0f)) v.y = imp.y; // y==0 ⇒ giữ vy cũ.
            body.linearVelocity = v;
        }

        private void OnMoveDriftOn() => _driftOpen = true;
        private void OnMoveDriftOff() => _driftOpen = false;
    }
}
