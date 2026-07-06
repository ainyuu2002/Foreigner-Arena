namespace FA.Core
{
    /// <summary>
    /// Hợp đồng NHỎ (P7) cho HurtState hỏi "victim còn knockback CHỜ áp không?" mà KHÔNG reference class
    /// cụ thể của FA.Combat ở mức Core (HurtState ở FA.Combat đọc qua interface này — giữ HurtState tách
    /// khỏi KnockbackResolver). KnockbackResolver (FA.Combat) implement.
    ///
    /// LÝ DO (neutral-gap fix): sau hit cuối HurtState có thể hết hurtstun & tự về Idle TRƯỚC khi resolver
    /// bốc victim vào tumble. HurtState hỏi cờ này trước khi ChangeState về Idle/Walk → nếu còn pending thì
    /// Ở LẠI Hurt tới khi knockback apply. (P16: pending áp tại hitstop+1, không còn release-timer 26f —
    /// gap thu hẹp; khi attacker nối thì pending bị CancelPending → cờ false → về neutral bình thường.)
    /// </summary>
    public interface IPendingKnockback
    {
        /// <summary>True khi còn 1 cú knockback đang CHỜ áp trên victim này (chưa release/launch).</summary>
        bool HasPendingKnockback { get; }
    }
}
