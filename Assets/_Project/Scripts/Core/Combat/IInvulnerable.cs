namespace FA.Core
{
    /// <summary>
    /// CẦU CORE cho trạng thái BẤT TỬ (i-frame hồi sinh P12): hazard môi trường (gió bão, đầm lầy,
    /// bùn, gai băng, đất nóng, fire biome — FA.Stage) hỏi "player này đang bất tử không?" để BỎ QUA
    /// hoàn toàn (feedback test: vừa hồi sinh đã bị gió đẩy). Qua interface Core → không ref chéo
    /// module (§1.3, cùng pattern IPercentReceiver). RespawnInvulnerability (FA.GameFlow) implement.
    /// </summary>
    public interface IInvulnerable
    {
        /// <summary>True khi đang trong thời gian bất tử — mọi tác động môi trường phải bỏ qua.</summary>
        bool IsInvulnerable { get; }
    }
}
