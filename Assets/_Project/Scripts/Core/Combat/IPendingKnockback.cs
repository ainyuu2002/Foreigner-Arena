namespace FA.Core
{
    /// <summary>
    /// Hợp đồng NHỎ (P7) cho HurtState hỏi "victim còn knockback CHỜ áp không?" mà KHÔNG reference class
    /// cụ thể của FA.Combat ở mức Core (HurtState ở FA.Combat đọc qua interface này — giữ HurtState tách
    /// khỏi KnockbackResolver). KnockbackResolver (FA.Combat) implement.
    ///
    /// LÝ DO (neutral-gap fix): hurtstun (≤20f) < knockbackReleaseFrames (26f) → sau hit cuối HurtState
    /// hết hurtstun & tự về Idle ~6 frame TRƯỚC khi resolver bốc victim vào tumble. HurtState hỏi cờ này
    /// trước khi ChangeState về Idle/Walk → nếu còn pending thì Ở LẠI Hurt tới khi knockback apply.
    /// </summary>
    public interface IPendingKnockback
    {
        /// <summary>True khi còn 1 cú knockback đang CHỜ áp trên victim này (chưa release/launch).</summary>
        bool HasPendingKnockback { get; }
    }
}
