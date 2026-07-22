namespace FA.Core
{
    /// <summary>
    /// Hợp đồng NHỎ cấp các MULTIPLIER runtime của Rage Mode (P10) cho lớp Combat ĐỌC mà KHÔNG sửa file
    /// P5/P7 (§1.3 + "thêm file mới, hạn chế sửa file phase trước"). 1 component <see cref="object"/>
    /// (RageModeController ở FA.Specials) implement; các listener (HitboxAuthoring/HurtState/Hitstop
    /// Controller) đọc NULL-SAFE — prefab KHÔNG gắn → coi như ×1.0 / false (giống cách thêm SetAttacker
    /// ở P9, KHÔNG đổi logic cũ).
    ///
    /// CÁCH ÁP BUFF KHÔNG SỬA MoveData (research §3.4):
    ///  • +DMG / +Knockback dealt → áp ở ATTACKER-side (HitboxAuthoring đọc của attacker khi dựng HitData).
    ///  • −hitstun nhận / +resist → áp ở VICTIM-side (HurtState đọc của victim).
    ///  • +Speed → CharacterMotor đọc khi tính velocity (P10 wire qua field optional, KHÔNG ép).
    ///  • Ultimate miễn nhiễm hitstop → HitstopController bỏ qua freeze khi attacker/victim IsUltimateActive.
    ///  • Ultimate phá block → truyền MoveRole.BlockBreak qua HitData (P6 đọc Role) — KHÔNG cần field mới ở đây.
    ///
    /// MỌI giá trị mặc định (không buff) = 1f cho mult, false cho cờ → listener nhân vô hại.
    /// </summary>
    public interface IBuffContext
    {
        /// <summary>Hệ số nhân DAMAGE % gây ra (×1.0 khi không Rage). Attacker-side.</summary>
        float DamageDealtMult { get; }

        /// <summary>Hệ số nhân KNOCKBACK gây ra (×1.0 khi không Rage). Attacker-side (base+growth).</summary>
        float KnockbackDealtMult { get; }

        /// <summary>Hệ số nhân HURTSTUN NHẬN (×1.0 thường; &lt;1 khi Rage = nhận ít stun hơn). Victim-side.</summary>
        float HitstunTakenMult { get; }

        /// <summary>Hệ số nhân TỐC ĐỘ di chuyển (×1.0 thường; &gt;1 khi Rage). Motor đọc nếu wire.</summary>
        float MoveSpeedMult { get; }

        /// <summary>Hệ số nhân % DAMAGE NHẬN VÀO (&lt;1 = chịu đòn trâu hơn — UBW install). Victim-side,
        /// KnockbackPercent nhân khi cộng %. Default ×1.0 để implementer cũ không phải sửa.</summary>
        float DamageTakenMult => 1f;

        /// <summary>Đang trong Rage Mode (buff active). Burst KHÔNG dùng được khi true (GDD).</summary>
        bool IsRageActive { get; }

        /// <summary>Đang chạy Ultimate (miễn nhiễm hitstop, animation speed cố định). HitstopController đọc.</summary>
        bool IsUltimateActive { get; }
    }
}
