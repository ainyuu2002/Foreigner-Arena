using UnityEngine;
using FA.Core;

namespace FA.Movement
{
    /// <summary>
    /// Lớp NỀN chung cho mọi state trên không (Jump/Fall/DoubleJump) — Phase 2.
    /// Gom phần LẶP LẠI để mỗi state con chỉ override điểm khác biệt (đúng "extensible by design"):
    ///   - Air drift NGANG: set linearVelocity.x = MoveX * walkSpeed * airControl (GIỮ vy — §1.1).
    ///   - Double jump: JumpPressed (edge) &amp; còn air-jump → chuyển DoubleJumpState.
    ///   - Landing: ĐÃ rời đất (HasLeftGround) &amp;&amp; IsGrounded → ResetAirJumps + ResetGravity → Idle/Walk theo MoveX.
    ///   - Animator StateId Rising/Falling theo dấu vy (apex deadzone chống nhấp nháy) + set VelY.
    ///
    /// KHÔNG tự tích phân trọng lực (Rigidbody2D.gravityScale lo). KHÔNG Animator.Play.
    /// State con quyết StateId mặc định (FallStateId) + có thể override các Allow* / hook Enter.
    /// </summary>
    public abstract class AirborneStateBase : IState
    {
        /// <summary>Ngưỡng coi là "có ý định đi ngang".</summary>
        protected const float MoveDeadzone = 0.1f;

        /// <summary>Deadzone vy quanh đỉnh để khỏi nhấp nháy Rising↔Falling.</summary>
        protected const float ApexDeadzone = 0.01f;

        protected readonly ICharacterContext Ctx;

        /// <summary>
        /// Cờ "đã thực sự rời đất". Reset = false ở Enter; bật true ngay khi thấy IsGrounded=false.
        /// Landing CHỈ kích khi cờ này true → diệt "ghost landing" ở frame vừa Enter Jump (lúc launch
        /// vy>0 nhưng IsGrounded có thể còn true) mà KHÔNG phụ thuộc dấu vy (vy lúc tiếp đất đôi khi
        /// dương do jitter de-penetration/contact). Đây là gốc fix kẹt AirState khi giữ hướng + đáp.
        /// </summary>
        protected bool HasLeftGround;

        protected AirborneStateBase(ICharacterContext ctx) => Ctx = ctx;

        /// <summary>StateId Animator của state con khi mặc định trên không (vd Fall, DoubleJump...).</summary>
        protected abstract CharacterStateId DefaultStateId { get; }

        /// <summary>State con có cho phép double jump không (Jump/Fall/DoubleJump: có).</summary>
        protected virtual bool AllowDoubleJump => true;

        public virtual void Enter()
        {
            HasLeftGround = false;
            SetStateId(DefaultStateId);
        }

        public virtual void Tick(float dt)
        {
            // Ghi nhận đã rời đất (gate landing). Đặt TRƯỚC landing-check để frame nào IsGrounded=false
            // cũng bật cờ; frame tiếp đất sau đó mới được phép Land.
            if (!Ctx.IsGrounded)
                HasLeftGround = true;

            // Landing: chỉ đáp khi ĐÃ rời đất rồi mới chạm lại — KHÔNG phụ thuộc dấu vy (vy lúc tiếp đất
            // đôi khi dương do jitter). Frame vừa Enter Jump có HasLeftGround=false → không "ghost landing".
            if (HasLeftGround && Ctx.IsGrounded)
            {
                Land();
                return;
            }

            // Double jump: edge-triggered (1 lần nhấn = 1 cú nhảy), còn lượt.
            if (AllowDoubleJump && Ctx.Input.JumpPressed && Ctx.AirJumpsRemaining > 0)
            {
                Ctx.ConsumeJumpEdge(); // P4: tiêu thụ edge buffer → 1 lần nhấn = 1 double-jump
                Ctx.Machine.ChangeState(Ctx.DoubleJumpState());
                return;
            }

            UpdateRisingFallingStateId();
        }

        public virtual void FixedTick(float dt)
        {
            ApplyAirDrift();
        }

        public virtual void Exit() { }

        // ===== Helper dùng chung =====

        /// <summary>Air drift ngang: set vx theo input (× airControl), GIỮ vy (gravity/jump không bị reset).</summary>
        protected void ApplyAirDrift()
        {
            Rigidbody2D rb = Ctx.Body;
            float moveX = Ctx.Input.MoveX;
            float dir = Mathf.Abs(moveX) > MoveDeadzone ? Mathf.Sign(moveX) : 0f;
            float vx = dir * Ctx.Data.walkSpeed * Ctx.Data.airControl;
            rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);

            if (dir != 0f)
                Ctx.SetFacing(dir > 0f ? 1 : -1);
        }

        /// <summary>Đáp đất: reset air-jump + gravity, về Idle/Walk theo input ngang.</summary>
        protected void Land()
        {
            Ctx.ResetAirJumps();
            Ctx.ResetGravity();
            bool moving = Mathf.Abs(Ctx.Input.MoveX) > MoveDeadzone;
            Ctx.Machine.ChangeState(moving ? Ctx.WalkState() : Ctx.IdleState());
        }

        /// <summary>Cập nhật StateId Rising(Jump)/Falling(Fall) theo dấu vy — chỉ khi state con cho phép.</summary>
        protected void UpdateRisingFallingStateId()
        {
            float vy = Ctx.Body.linearVelocity.y;
            if (vy > ApexDeadzone)
                SetStateId(CharacterStateId.Jump);
            else if (vy < -ApexDeadzone)
                SetStateId(CharacterStateId.Fall);
            // Vùng đỉnh (|vy| ≤ deadzone): giữ StateId hiện tại để không nhấp nháy.
        }

        /// <summary>Set Animator StateId (cầu nối logic) + VelY (phụ trợ blend, set-only).</summary>
        protected void SetStateId(CharacterStateId id)
        {
            Ctx.Animator.SetInteger(MovementAnimatorParams.StateId, (int)id);
        }
    }
}
