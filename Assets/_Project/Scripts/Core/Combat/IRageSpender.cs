namespace FA.Core
{
    /// <summary>
    /// Hợp đồng NHỎ để lớp Specials (P10) TIÊU / HOÀN nộ mà KHÔNG reference FA.Resources (§1.3 — module
    /// chỉ reference FA.Core, giao tiếp qua interface trong Core). <see cref="FA.Resources.RageBar"/> (P8)
    /// implement THÊM interface này (chỉ thêm "<c>: IRageSpender</c>" + vài method — KHÔNG sửa logic GAIN
    /// đã chốt P8). DefenseController/BurstState/RageModeState GetComponentInChildren&lt;IRageSpender&gt;()
    /// → phụ thuộc Core, KHÔNG class Resources. Đối xứng <see cref="IManaSpender"/> (P9).
    ///
    /// Nộ tiêu theo PHẦN TRĂM của RageMax (Burst = 50% flat, Rage Mode = 100%) — KHÁC mana (tiêu theo Ô
    /// nguyên). Caller PHẢI gọi <see cref="TrySpendRage"/> TRƯỚC khi ChangeState (research §4.3 — thiếu
    /// nộ thì KHÔNG vào state để tránh kẹt animation/whiff). Hết Rage Mode → <see cref="RefundRage"/>
    /// hoàn 20–25% (CẦN TEST, đọc từ DefenseConfigSO/ResourceConfigSO).
    /// </summary>
    public interface IRageSpender
    {
        /// <summary>Tỉ lệ nộ hiện có trên RageMax (0..1). P10 kiểm "đủ 50% cho burst?" / "đầy 100%?".</summary>
        float Fraction { get; }

        /// <summary>Đã đầy 100% nộ — P10 mở Rage Mode/Ultimate (đối xứng <c>IsFull</c> của RageBar P8).</summary>
        bool IsFull { get; }

        /// <summary>
        /// Tiêu <paramref name="fraction"/> (0..1) × RageMax nộ. Trả false (KHÔNG trừ) nếu thiếu. Caller
        /// gọi cái này TRƯỚC ChangeState (research §4.3). VD burst: TrySpendRage(0.5f); rage: 1f.
        /// </summary>
        bool TrySpendRage(float fraction);

        /// <summary>Hoàn <paramref name="fraction"/> (0..1) × RageMax nộ (clamp ≤ max). Rage Mode hết hạn
        /// gọi để hoàn 20–25% (research §5 — tránh người chơi "mất sạch" cảm giác).</summary>
        void RefundRage(float fraction);
    }
}
