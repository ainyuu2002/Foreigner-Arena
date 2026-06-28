namespace FA.Core
{
    /// <summary>
    /// Hợp đồng NHỎ để lớp Specials (P9) tiêu mana mà KHÔNG reference FA.Resources (§1.3 — module chỉ
    /// reference FA.Core, giao tiếp qua interface trong Core). <see cref="FA.Resources.ManaBar"/> (P8)
    /// implement THÊM interface này (mọi method đã có sẵn — chỉ thêm "<c>: IManaSpender</c>", KHÔNG sửa
    /// logic). SpecialController/SuperState GetComponent&lt;IManaSpender&gt;() → phụ thuộc Core, KHÔNG
    /// class Resources.
    ///
    /// Model mana = "chia ô" đã chốt ở P8 (SFV EX / GG Tension) → tiêu theo SỐ Ô NGUYÊN, không theo %
    /// tuyệt đối. Super1 ≈ 2 ô (~50%), Super2 ≈ 3 ô (~75%) khi MaxSegments=4 (CẦN TEST — chỉnh
    /// ResourceConfigSO, KHÔNG sửa code).
    /// </summary>
    public interface IManaSpender
    {
        /// <summary>Số ô mana ĐẦY hiện có (floor). P9 kiểm trước khi cho special/super.</summary>
        int Segments { get; }

        /// <summary>
        /// Tiêu nguyên <paramref name="segments"/> ô mana. Trả false (KHÔNG trừ) nếu thiếu ô.
        /// Caller PHẢI gọi cái này TRƯỚC khi ChangeState (research §4.3 — thiếu mana thì không vào
        /// state để tránh kẹt animation/whiff).
        /// </summary>
        bool TrySpend(int segments);

        /// <summary>
        /// (P13 ADD) CỘNG mana theo PHẦN TRĂM của MaxMana — Mana Potion (item) nạp +5/+10/+20% (GDD §12 ghi
        /// %). <paramref name="fractionOfMax"/> ∈ [0..1] (vd 0.10 = +10% Max). <see cref="FA.Resources.ManaBar"/>
        /// implement THÊM (clamp ≤ MaxMana + raise OnManaChanged/OnManaSegmentsChanged hiện có) — additive,
        /// KHÔNG đổi đường gain P8. Item gọi qua interface (Core) → KHÔNG reference chéo FA.Resources (§1.3).
        /// Đối xứng <see cref="IRageSpender.RefundRage"/> (cộng nộ theo fraction).
        /// </summary>
        void AddMana(float fractionOfMax);
    }
}
