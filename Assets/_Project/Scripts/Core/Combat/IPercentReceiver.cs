namespace FA.Core
{
    /// <summary>
    /// Hợp đồng NHỎ (P11) để một module MÔI TRƯỜNG (FA.Stage FireBiome) CỘNG % vào nhân vật mà KHÔNG
    /// reference FA.Combat (thanh % <c>KnockbackPercent</c> nằm ở FA.Combat). Đây đúng pattern Core-interface
    /// đã dùng cho <see cref="IBuffContext"/>/<see cref="ICombatContext"/>/<see cref="IPendingKnockback"/>:
    /// interface định ở Core, module gameplay implement THÊM, listener gọi null-safe.
    ///
    /// <see cref="KnockbackPercent"/> (FA.Combat) implement THÊM (additive, 1 method — đi qua clamp +
    /// raise OnPercentChanged hiện có, KHÔNG đổi logic P7). FireBiome (FA.Stage) gọi
    /// <c>GetComponentInChildren&lt;IPercentReceiver&gt;()</c> trên player (null-safe nếu không có).
    /// </summary>
    public interface IPercentReceiver
    {
        /// <summary>% hiện tại (đọc để hiển thị/debug; cộng đã clamp ở phía implement).</summary>
        float CurrentPercent { get; }

        /// <summary>Cộng <paramref name="amount"/> % vào thanh % (đi qua clamp/cap của implement).
        /// (P13 NỚI) amount ÂM được HỖ TRỢ: Knockback Potion (item) giảm % → KnockbackPercent đã clamp
        /// [0, cap] (clamp đáy 0) nên giảm xuống dưới 0 sẽ về 0. Không đổi logic gain P7/P11 — chỉ làm rõ
        /// hợp đồng cho giá trị âm (implement đã đúng).</summary>
        void AddPercent(float amount);

        /// <summary>
        /// (P12 ADD) Đưa % về 0 — RespawnSystem (FA.GameFlow) gọi qua interface khi player mất mạng &amp;
        /// respawn (GDD §8.1: respawn → % về 0, mana/nộ GIỮ NGUYÊN). <see cref="KnockbackPercent"/> đã có
        /// sẵn method cùng tên (raise OnPercentChanged(0)) → chỉ KHAI BÁO vào interface (additive, không
        /// đổi logic P7). RespawnSystem gọi <c>GetComponentInChildren&lt;IPercentReceiver&gt;()?.ResetPercent()</c>
        /// → KHÔNG reference chéo FA.Combat.
        /// </summary>
        void ResetPercent();
    }
}
