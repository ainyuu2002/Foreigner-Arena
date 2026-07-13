namespace FA.Core
{
    /// <summary>
    /// CẦU CORE cho cơ chế môi trường "bấm nhảy đúng nhịp" (nấm nhún P11 v2): stage-side hỏi
    /// "player vừa bấm Jump trong N frame gần đây?" mà KHÔNG ref FA.Movement (§1.3 — cùng pattern
    /// IPercentReceiver/IBuffContext). CONSUME cú bấm khi nhận: nếu không, DoubleJumpState sẽ ăn lại
    /// đúng cú bấm đó ngay sau khi nấm boost và ghi đè vy = jumpSpeed → mất lực bật.
    /// CharacterMotor (FA.Movement) implement bằng CommandParser.WasPressedWithin + ConsumePress.
    /// </summary>
    public interface IJumpIntent
    {
        /// <summary>True nếu có cú bấm Jump trong <paramref name="withinFrames"/> frame gần nhất —
        /// đồng thời TIÊU THỤ cú bấm đó khỏi parser.</summary>
        bool TryConsumeRecentJumpPress(int withinFrames);

        /// <summary>TẮT double-jump cho cú bay hiện tại (nấm nhún gọi sau mỗi cú bật — bấm J giữa
        /// chuỗi nhún không bị nuốt thành double-jump làm đứt chuỗi). Motor tự phục hồi khi đáp
        /// xuống đất thật (ngoài cửa sổ chống "landing giả" lúc lướt qua mặt nấm).</summary>
        void SuppressAirJumps();
    }
}
