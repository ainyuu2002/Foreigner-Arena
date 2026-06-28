using UnityEngine;
using FA.Core;

namespace FA.Movement
{
    /// <summary>
    /// Cú nhảy THỨ 2 trên không (Phase 2). Enter: tiêu 1 air-jump, set lại linearVelocity.y = jumpSpeed
    /// (ghi đè — kể cả đang rơi, "cứu" độ cao kiểu Smash), StateId = DoubleJump (Animator phát clip xoay).
    /// Khi qua đỉnh (vy ≤ 0) → bàn giao AirState (Fall). Air drift + landing kế thừa AirborneStateBase.
    ///
    /// Không cho double jump tiếp (đã tiêu lượt → AirJumpsRemaining về 0). Mọi cú nhảy = một độ cao
    /// cố định (jumpSpeed). Mọi chạm Rigidbody2D ở FixedTick.
    /// </summary>
    public sealed class DoubleJumpState : AirborneStateBase
    {
        public DoubleJumpState(ICharacterContext ctx) : base(ctx) { }

        protected override CharacterStateId DefaultStateId => CharacterStateId.DoubleJump;

        // Đã tiêu lượt nên base sẽ không double tiếp (AirJumpsRemaining == 0).

        public override void Enter()
        {
            // Launch NGAY trong Enter (gọi từ Update): GHI ĐÈ vy = jumpSpeed (kể cả đang rơi — "cứu"
            // độ cao kiểu Smash), GIỮ vx. Set linearVelocity 1 lần ở đây hợp lệ — physics đọc ở
            // FixedUpdate kế. Nhờ vậy frame Enter vy > 0 → nhánh apex (vy≤0) cùng frame KHÔNG kích →
            // cú nhảy đôi không bị đẩy sang AirState ngay khi vừa bấm lúc đang rơi.
            Rigidbody2D rb = Ctx.Body;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Ctx.Data.jumpSpeed);
            SetStateId(CharacterStateId.DoubleJump);
            Ctx.ConsumeAirJump();
            Ctx.RefreshAirDash(); // double jump nạp lại +1 air-dash (đúng yêu cầu: nhảy → +1 dash).
            HasLeftGround = false; // không gọi base.Enter ở đây nên reset cờ gate-landing thủ công.
        }

        public override void Tick(float dt)
        {
            // Ghi nhận đã rời đất (gate landing) trước khi check đáp. Double jump vào lúc đang air nên
            // cờ bật true ngay frame này → đáp đúng khi chạm sàn.
            if (!Ctx.IsGrounded)
                HasLeftGround = true;

            // Landing: chỉ đáp khi ĐÃ rời đất rồi chạm lại — không phụ thuộc dấu vy (jitter có thể >0).
            if (HasLeftGround && Ctx.IsGrounded)
            {
                Land();
                return;
            }

            // Giữ clip DoubleJump khi còn đang LÊN; qua đỉnh → AirState lo rơi.
            if (Ctx.Body.linearVelocity.y <= 0f)
            {
                Ctx.Machine.ChangeState(Ctx.AirState());
                return;
            }
        }

        public override void FixedTick(float dt)
        {
            ApplyAirDrift();
        }
    }
}
