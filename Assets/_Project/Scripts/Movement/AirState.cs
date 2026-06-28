using FA.Core;

namespace FA.Movement
{
    /// <summary>
    /// Khung TRÊN KHÔNG chung — chủ yếu là RƠI (Phase 2). Vào khi: qua đỉnh nhảy, hoặc đi khỏi mép
    /// sàn (rơi không nhảy). Dùng trọn logic AirborneStateBase:
    ///   - Air drift ngang (FixedTick), GIỮ vy (gravity Rigidbody2D lo — KHÔNG tự tích phân).
    ///   - Double jump (JumpPressed + còn lượt) → DoubleJumpState.
    ///   - Landing → reset air-jump + gravity → Idle/Walk.
    ///   - StateId Rising(Jump)/Falling(Fall) theo vy (apex deadzone).
    ///
    /// File này KHÔNG thêm logic riêng — chỉ chốt DefaultStateId = Fall. Mọi điểm khác = base.
    /// </summary>
    public sealed class AirState : AirborneStateBase
    {
        public AirState(ICharacterContext ctx) : base(ctx) { }

        protected override CharacterStateId DefaultStateId => CharacterStateId.Fall;
    }
}
