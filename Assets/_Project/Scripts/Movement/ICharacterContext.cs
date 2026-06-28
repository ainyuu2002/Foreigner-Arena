using UnityEngine;
using FA.Core;

namespace FA.Movement
{
    /// <summary>
    /// Các phụ thuộc mà MovementState cần, gom 1 chỗ để state KHÔNG tự GetComponent (research §3.2).
    /// CharacterMotor implement interface này và truyền chính nó vào constructor mỗi state.
    /// State đọc qua đây → thuần logic, dễ test, dễ thay nguồn input (IMoveInput) ở phase sau.
    /// </summary>
    public interface ICharacterContext
    {
        /// <summary>Thân vật lý — nơi state set velocity (trong FixedTick).</summary>
        Rigidbody2D Body { get; }

        /// <summary>Animator — state chỉ set int "StateId", KHÔNG Play clip.</summary>
        Animator Animator { get; }

        /// <summary>Số liệu nhân vật (walkSpeed...). Không hardcode.</summary>
        CharacterData Data { get; }

        /// <summary>
        /// (P10) Hệ số nhân TỐC ĐỘ di chuyển từ Rage Mode buff (×1.0 thường; &gt;1 khi Rage). Motor
        /// resolve NULL-SAFE từ <c>IBuffContext</c> (FA.Core) trên root — prefab KHÔNG gắn
        /// RageModeController → ×1.0 (không hồi quy P1/P3). State nhân walkSpeed/dashSpeed với giá trị
        /// này. Đặt ở Motor (KHÔNG để state tự GetComponent — đúng research §3.2) + chỉ qua interface
        /// Core (KHÔNG ref FA.Specials, §1.3).
        /// </summary>
        float MoveSpeedMult { get; }

        /// <summary>Nguồn ý định di chuyển (P1: FA_Controls; P4: buffer; P12: pairing).</summary>
        IMoveInput Input { get; }

        /// <summary>State machine sở hữu bởi Motor — state gọi ChangeState qua đây.</summary>
        StateMachine Machine { get; }

        /// <summary>Lật hướng nhìn (gom 1 chỗ): dir &lt; 0 quay trái, dir &gt; 0 quay phải, 0 giữ nguyên.</summary>
        void SetFacing(int dir);

        // ===== Phase 2: trạng thái trên không (Motor cấp, state đọc) =====

        /// <summary>Đang chạm sàn không (từ GroundCheck, refresh trong FixedUpdate). Dùng cho landing/ground-jump.</summary>
        bool IsGrounded { get; }

        /// <summary>Số lần nhảy thêm trên không CÒN LẠI (double jump). Reset = maxAirJumps khi chạm đất.</summary>
        int AirJumpsRemaining { get; }

        /// <summary>Tiêu thụ 1 air-jump (khi double jump). Không xuống dưới 0.</summary>
        void ConsumeAirJump();

        /// <summary>
        /// Phase 4: tiêu thụ EDGE Jump trong InputBuffer (đánh dấu lần nhấn hiện tại đã dùng → không
        /// re-trigger trong buffer-window). Gọi khi double-jump kích từ state, giữ đúng ngữ nghĩa
        /// "1 lần nhấn = 1 cú nhảy" của P2 (edge buffer thay cho WasPressedThisFrame). No-op nếu chưa
        /// có parser. KHÔNG đổi IMoveInput — đây là accessor THÊM (state cũ không gọi vẫn chạy).
        /// </summary>
        void ConsumeJumpEdge();

        /// <summary>
        /// Phase 4: tiêu thụ EDGE Dash trong InputBuffer (đánh dấu lần nhấn hiện tại đã dùng). Gọi khi
        /// DashState SPAM (1 tap Dash = 1 lần reset dash, KHÔNG re-trigger suốt buffer-window). Giữ đúng
        /// ngữ nghĩa edge của P3. No-op nếu chưa có parser. KHÔNG đổi IMoveInput.
        /// </summary>
        void ConsumeDashEdge();

        /// <summary>Reset air-jump về maxAirJumps (gọi khi landing).</summary>
        void ResetAirJumps();

        /// <summary>
        /// Nạp lại quota air-dash về maxAirDashes. Gọi ở Enter của MỌI cú nhảy (JumpState/
        /// DoubleJumpState/WallJumpState) và khi chạm đất → mỗi lần nhảy cho thêm 1 air-dash.
        /// Phase 3 (chỉnh dash). Air-dash bị tiêu 1 khi vào DashState trên không.
        /// </summary>
        void RefreshAirDash();

        /// <summary>gravityScale baseline của nhân vật (= Data.gravityScale). Dùng để khôi phục gravity.</summary>
        float BaseGravityScale { get; }

        /// <summary>
        /// Trả gravityScale của Rigidbody2D về baseline (Data.gravityScale). Gọi 1 CHỖ DUY NHẤT
        /// khi landing (no-op an toàn — gravity P2 không còn bị đổi). P10 (RageMode) cũng gọi lại đây.
        /// </summary>
        void ResetGravity();

        // --- Bảng state dùng lại (Motor tạo 1 lần, state lấy ra để ChangeState) ---
        // Cách này tránh state tự "new" state khác (đỡ tạo rác mỗi frame) và tránh
        // phụ thuộc vòng giữa các state. Thêm state phase sau = thêm accessor mới.

        /// <summary>State Idle dùng lại.</summary>
        IState IdleState();

        /// <summary>State Walk dùng lại.</summary>
        IState WalkState();

        /// <summary>State Jump (cú nhảy đầu từ đất). Phase 2.</summary>
        IState JumpState();

        /// <summary>State Air/Fall (rơi & khung trên không chung). Phase 2.</summary>
        IState AirState();

        /// <summary>State DoubleJump (cú nhảy thứ 2 trên không). Phase 2.</summary>
        IState DoubleJumpState();

        // ===== Phase 3: dash + tường (Motor cấp, state đọc) =====

        /// <summary>
        /// Hướng nhìn hiện tại: +1 nhìn phải, -1 nhìn trái (suy từ localScale.x). Dùng cho dash
        /// ĐỨNG-YÊN (MoveX≈0) để biết hướng mặc định mà KHÔNG tự đọc transform trong state.
        /// </summary>
        int Facing { get; }

        /// <summary>Đang chạm tường không (từ WallCheck: IsTouchingLayers(WallMask), refresh FixedUpdate).</summary>
        bool IsWalled { get; }

        /// <summary>
        /// Phía tường đang chạm: +1 tường bên PHẢI nhân vật, -1 bên TRÁI, 0 không xác định.
        /// Wall-jump bật ra theo -WallSide. WallCheck suy từ contact normal (Unity-native).
        /// </summary>
        int WallSide { get; }

        /// <summary>State Dash (lướt ngang, mặt đất + trên không). Phase 3.</summary>
        IState DashState();

        /// <summary>State WallHang (bám tường, trượt chậm). Phase 3.</summary>
        IState WallHangState();

        /// <summary>
        /// Phía tường ĐÃ CHỐT lúc đang bám (WallHangState.WallSide). WallJumpState đọc giá trị này để
        /// bật ra đúng phía NHẤT QUÁN — không đọc lại WallCheck/Facing (tránh fallback Facing sai sau
        /// khi đã SetFacing đảo). +1 tường phải, -1 trái. Phase 3.
        /// </summary>
        int WallHangSide { get; }

        /// <summary>State WallJump (bật khỏi tường, spam để leo). Phase 3.</summary>
        IState WallJumpState();

        /// <summary>
        /// WallJumpState gọi ở Enter để báo cú wall-jump VỪA bắt đầu → Motor "disarm" re-grab WallHang
        /// (đặt cờ armed=false). Chỉ khi nhân vật thực sự RỜI tường (WallCheck.IsTouchingRaw==false ≥1
        /// frame) Motor mới arm lại → cho bám lại để LEO BẬC. Tránh re-grab ngay giữa cú nhảy (triệt
        /// tiêu vận tốc bật ra + flip facing) mà KHÔNG dùng lockout-timer. Phase 3.
        /// </summary>
        void OnWallJumpStarted();
    }
}
