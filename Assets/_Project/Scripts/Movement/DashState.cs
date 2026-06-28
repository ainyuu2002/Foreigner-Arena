using UnityEngine;
using FA.Core;

namespace FA.Movement
{
    /// <summary>
    /// Normal Dash (Phase 3) — lướt NGANG nhanh, dùng cho cả mặt đất và trên không (air dash).
    /// GDD §5.1: 1 nút, FREE, KHÔNG cancel-anim, KHÔNG cooldown, KHÔNG i-frame.
    ///
    /// - Enter: chốt hướng dash từ MoveX (else Facing) + SetFacing + reset timer + StateId=Dash;
    ///   KHÓA trục Y (gravityScale=0) → dash PHẲNG, không trôi xuống (game-feel, quyết định người dùng).
    /// - FixedTick: GÁN linearVelocity = (dir·dashSpeed, 0) → vy=0 tuyệt đối, deterministic. KHÔNG
    ///   AddForce, KHÔNG tự tích phân. Dash air vẫn lao NGANG vào tường (IsTouchingRaw) → entry-point
    ///   WallHang ở CharacterMotor lo việc bám.
    /// - Tick: SPAM chỉ khi GROUNDED (dash đất re-enter reset timer/đổi hướng). TRÊN KHÔNG không spam —
    ///   air-dash giới hạn 1 lần/lần-nhảy (quota _airDashesRemaining ở Motor, nạp lại mỗi cú nhảy).
    ///   Hết dashDuration → Idle/Walk (nếu IsGrounded) hoặc Air. Phân nhánh kết thúc theo IsGrounded LÚC HẾT.
    /// - Exit: ResetGravity() khôi phục gravity baseline (đối xứng với khóa Y ở Enter).
    ///
    /// DashState KHÔNG kế thừa AirborneStateBase: dash có 2 nhánh ground/air, logic landing của base
    /// không hợp (research §3.6) → IState trực tiếp. KHÔNG đụng hurtbox/layer (no i-frame — P5 lo box).
    /// Timer là thời lượng GAMEPLAY của state cơ động (không phải frame-data đòn) → đếm timer OK.
    /// </summary>
    public sealed class DashState : IState
    {
        private const float MoveDeadzone = 0.1f;

        private readonly ICharacterContext _ctx;
        private int _dir;          // hướng dash hiện tại (+1/-1)
        private float _timer;      // thời gian còn lại của cú dash

        public DashState(ICharacterContext ctx) => _ctx = ctx;

        public void Enter()
        {
            BeginDash();
            _ctx.Body.gravityScale = 0f; // KHÓA trục Y suốt cú dash → không trôi xuống (game-feel).
            _ctx.Animator.SetInteger(MovementAnimatorParams.StateId, (int)CharacterStateId.Dash);
        }

        /// <summary>Chốt hướng từ input (else facing) + lật nhìn + reset timer. Dùng cho cả Enter & spam.</summary>
        private void BeginDash()
        {
            float moveX = _ctx.Input.MoveX;
            _dir = Mathf.Abs(moveX) > MoveDeadzone ? (int)Mathf.Sign(moveX) : _ctx.Facing;
            if (_dir == 0) _dir = 1; // an toàn nếu Facing trả 0
            _ctx.SetFacing(_dir);
            _timer = _ctx.Data.dashDuration;
        }

        public void Tick(float dt)
        {
            _timer -= dt;

            // SPAM chỉ khi GROUNDED: bấm Dash lại trong lúc dash đất → reset timer + (có thể) đổi hướng,
            // KHÔNG cooldown. TRÊN KHÔNG KHÔNG spam → air-dash giới hạn 1 lần/lần-nhảy (quota ở Motor;
            // muốn dash tiếp trên không phải NHẢY để nạp lại quota).
            if (_ctx.Input.DashPressed && _ctx.IsGrounded)
            {
                _ctx.ConsumeDashEdge(); // P4: 1 tap = 1 reset (edge buffer không re-trigger suốt window)
                BeginDash();
            }

            // (Air-dash chạm tường → bám: nay do entry-point WallHang TOÀN CỤC ở CharacterMotor lo —
            // bám cho MỌI airborne state khi ép input vào tường, không riêng dash → tránh double-transition.)

            // Hết dash → phân nhánh theo trạng thái LÚC NÀY (đất → Idle/Walk; air → Air/Fall).
            if (_timer <= 0f)
            {
                EndDash();
                return;
            }
        }

        private void EndDash()
        {
            if (_ctx.IsGrounded)
            {
                bool moving = Mathf.Abs(_ctx.Input.MoveX) > MoveDeadzone;
                _ctx.Machine.ChangeState(moving ? _ctx.WalkState() : _ctx.IdleState());
            }
            else
            {
                _ctx.Machine.ChangeState(_ctx.AirState());
            }
        }

        public void FixedTick(float dt)
        {
            // GÁN vx = dir·dashSpeed, vy = 0 (trục Y khóa — gravityScale=0 ở Enter). Dash PHẲNG tuyệt đối,
            // deterministic. KHÔNG cộng dồn → spam (đất) không tăng tốc.
            Rigidbody2D rb = _ctx.Body;
            // (P10) Nhân buff +Speed của Rage Mode cho khớp spec "Rage Mode buff rõ" (dash cũng nhanh hơn).
            // ×1.0 nếu không Rage / chưa gắn RageModeController → hành vi P3 không đổi. Giữ vy=0 (khóa Y).
            float speed = _ctx.Data.dashSpeed * _ctx.MoveSpeedMult;
            rb.linearVelocity = new Vector2(_dir * speed, 0f);
        }

        public void Exit()
        {
            _ctx.ResetGravity(); // khôi phục gravity baseline (đối xứng với khóa Y ở Enter).
        }
    }
}
