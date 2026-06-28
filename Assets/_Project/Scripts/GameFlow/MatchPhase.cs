namespace FA.GameFlow
{
    /// <summary>
    /// (P12, research §3.1) Các phase vòng đời 1 trận (Smash VS loop, GDD §10.1). MatchManager chuyển theo
    /// SỰ KIỆN UI/điều kiện (không physics tick) → enum nhẹ, KHÔNG dựng lại IState (research §3.1: IState là
    /// state-machine GAMEPLAY per-frame, không hợp điều phối load scene/toggle panel).
    /// </summary>
    public enum MatchPhase
    {
        CharSelect,
        StageSelect,
        Match,
        End
    }
}
