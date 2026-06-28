using UnityEngine;
using FA.Core;

namespace FA.Movement
{
    /// <summary>
    /// Cú nhảy ĐẦU từ đất (Phase 2). Enter: set linearVelocity.y = jumpSpeed (GIỮ vx — mang đà ngang),
    /// StateId = Jump. Mọi cú nhảy = MỘT độ cao cố định (jumpSpeed) — giữ nút KHÔNG ảnh hưởng độ cao.
    /// Khi vy ≤ 0 (qua đỉnh) → chuyển AirState (Fall).
    ///
    /// Set vy bằng GÁN THẲNG (deterministic — không AddForce, không phụ thuộc mass/vy hiện tại).
    /// Mọi chạm Rigidbody2D ở FixedTick. Air drift (base.FixedTick) + double jump + landing kế thừa
    /// AirborneStateBase.
    /// </summary>
    public sealed class JumpState : AirborneStateBase
    {
        public JumpState(ICharacterContext ctx) : base(ctx) { }

        protected override CharacterStateId DefaultStateId => CharacterStateId.Jump;

        public override void Enter()
        {
            // Launch NGAY trong Enter (gọi từ Update): gán thẳng vy = jumpSpeed, GIỮ vx (mang đà ngang).
            // Set linearVelocity 1 lần ở đây là hợp lệ — physics đọc ở FixedUpdate kế. Nhờ vậy NGAY
            // frame Enter vy > 0 → cùng frame Tick: landing-check & nhánh apex (vy≤0) KHÔNG kích →
            // nhảy thực sự rời đất (không bị "ghost landing"/apex đẩy đi sớm).
            Rigidbody2D rb = Ctx.Body;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Ctx.Data.jumpSpeed);
            SetStateId(CharacterStateId.Jump);
            Ctx.RefreshAirDash(); // mỗi cú nhảy nạp lại 1 air-dash (dùng trên không tới lần nhảy kế).
            HasLeftGround = false; // không gọi base.Enter ở đây nên reset cờ gate-landing thủ công.
        }

        public override void Tick(float dt)
        {
            // Ghi nhận đã rời đất (gate landing) trước khi check đáp.
            if (!Ctx.IsGrounded)
                HasLeftGround = true;

            // Landing: chỉ đáp khi ĐÃ rời đất rồi chạm lại — không phụ thuộc dấu vy (jitter có thể >0).
            // Frame Enter HasLeftGround=false nên không "ghost landing".
            if (HasLeftGround && Ctx.IsGrounded)
            {
                Land();
                return;
            }

            if (Ctx.Input.JumpPressed && Ctx.AirJumpsRemaining > 0)
            {
                Ctx.ConsumeJumpEdge(); // P4: tiêu thụ edge buffer → 1 lần nhấn = 1 double-jump
                Ctx.Machine.ChangeState(Ctx.DoubleJumpState());
                return;
            }

            // Qua đỉnh → bàn giao cho AirState (Fall) lo phần rơi.
            if (Ctx.Body.linearVelocity.y <= 0f)
            {
                Ctx.Machine.ChangeState(Ctx.AirState());
                return;
            }
        }

        // FixedTick dùng base (chỉ air-drift) — không còn jump-cut nên không cần override.
    }
}
