namespace FA.Items
{
    /// <summary>
    /// (Hợp nhất item) LOẠI hiệu ứng DÙNG CHUNG cho MỌI item (bình thường + Mystery). Giá trị int TRÙNG
    /// KHỚP <see cref="MysteryBlockOutcomeType"/> (0..6) để cast an toàn — <see cref="MysteryBlockOutcome"/>
    /// (dữ liệu asset) chuyển sang <see cref="ItemEffect"/> bằng cast trực tiếp. ĐỪNG đổi thứ tự/giá trị
    /// (asset MysteryBlockOutcomeConfig lưu type theo int).
    /// </summary>
    public enum ItemEffectType
    {
        /// <summary>Hồi resistance: IPercentReceiver.AddPercent(-value). [Knockback Potion / Mystery hồi %].</summary>
        ResistanceRecovery = 0,

        /// <summary>Hồi mana: IManaSpender.AddMana(value fraction). [Mana Potion / Mystery hồi mana].</summary>
        ManaRecovery = 1,

        /// <summary>Miễn thời tiết có thời hạn (WeatherImmunityStatus). value bỏ qua, dùng duration.</summary>
        WeatherImmunity = 2,

        /// <summary>Debuff tăng resistance: IPercentReceiver.AddPercent(+value). [Mystery bẫy nhẹ].</summary>
        ResistanceIncreaseDebuff = 3,

        /// <summary>Rơi chậm có thời hạn (SlowFallStatus): gravityScale ×(1-value).</summary>
        SlowFall = 4,

        /// <summary>Buff damage có thời hạn (ItemDamageBuff): multiplier = 1 + value. [DMG Buff / Mystery].</summary>
        DamageBuff = 5,

        /// <summary>Bẫy: AddPercent(+value) + đẩy knockback tức thời. [Mystery bẫy].</summary>
        TrapBall = 6,
    }

    /// <summary>
    /// (Hợp nhất item) MODEL hiệu ứng bất biến mà 1 item áp lên người nhặt. Item bình thường dựng 1
    /// <see cref="ItemEffect"/> CỐ ĐỊNH từ <see cref="ItemData"/>; Mystery ROLL 1 cái từ bảng
    /// <see cref="MysteryBlockOutcomeConfigSO"/>. Cả hai đi CHUNG qua <see cref="ItemEffectApplier"/>.
    ///
    /// Ý NGHĨA <see cref="value"/> theo <see cref="type"/> (giữ nguyên quy ước cũ của MysteryBlockOutcome —
    /// KHÔNG đổi để không hồi quy balance): ResistanceRecovery/Debuff/Trap = số % cộng thẳng (đơn vị theo
    /// data — CẦN TEST đồng bộ vì Knockback Potion dùng điểm % nguyên còn Mystery dùng fraction);
    /// ManaRecovery = fraction MaxMana; DamageBuff = phần TĂNG (multiplier = 1+value); SlowFall = fraction
    /// giảm trọng lực; WeatherImmunity = không dùng value.
    /// </summary>
    public readonly struct ItemEffect
    {
        public readonly ItemEffectType type;
        public readonly float value;
        public readonly float duration;

        public ItemEffect(ItemEffectType type, float value, float duration)
        {
            this.type = type;
            this.value = value;
            this.duration = duration;
        }
    }
}
