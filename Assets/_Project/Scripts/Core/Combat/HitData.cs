using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// SNAPSHOT BẤT BIẾN của MỘT cú trúng (§3.3/§3.4). Value-type readonly → no GC, không sửa
    /// được sau khi tạo ("1 cú trúng = 1 sự kiện"). Combat (P5) RAISE qua <see cref="HitEventChannelSO"/>;
    /// Resources/UI/Combo/Knockback (P6/P7/P8) SUBSCRIBE mà KHÔNG reference Combat (§1.3 decoupling).
    ///
    /// Đặt ở FA.Core vì nhiều module đọc chung — mọi module chỉ reference Core.
    ///
    /// PHẠM VI P5: chỉ tiêu thụ <see cref="DamagePercent"/> (in ra) + <see cref="HitstopFrames"/>
    /// (freeze). Các field knockback (LaunchAngle/BaseKnockback/KnockbackGrowth/FacingSign/IsSpike/
    /// CausesLaunch) MANG SẴN trong payload nhưng CHƯA apply ở P5 — P7 thêm KnockbackResolver subscribe
    /// cùng kênh, KHÔNG sửa HitDetection. HitsLow/HitsHigh cho Block high/low (P6).
    /// </summary>
    public readonly struct HitData
    {
        /// <summary>Root GameObject NGƯỜI ĐÁNH (để Mana/Rage/Combo quy về "ai gây").</summary>
        public readonly GameObject Attacker;

        /// <summary>Root GameObject NGƯỜI NHẬN (cộng % / chuyển HurtState ở P6).</summary>
        public readonly GameObject Victim;

        /// <summary>+% cộng vào thanh % của victim mỗi hit (P7 apply; P5 in ra console).</summary>
        public readonly float DamagePercent;

        /// <summary>Góc tung (độ, đã theo facing attacker). P7 apply knockback.</summary>
        public readonly float LaunchAngle;

        /// <summary>Lực knockback cơ sở (P7). Lực thực = base + growth*%victim.</summary>
        public readonly float BaseKnockback;

        /// <summary>Hệ số tăng knockback theo % victim (P7 Smash-model).</summary>
        public readonly float KnockbackGrowth;

        /// <summary>Số frame freeze cả 2 khi trúng (P5 DÙNG NGAY qua HitstopController).</summary>
        public readonly int HitstopFrames;

        /// <summary>Hướng đẩy +1/-1 (= Facing attacker lúc trúng). P7 dùng để định hướng knockback.</summary>
        public readonly int FacingSign;

        /// <summary>Đòn spike (đánh thẳng xuống) — P7.</summary>
        public readonly bool IsSpike;

        /// <summary>Đòn KẾT/launcher (mang từ MoveData.causesLaunch) — P7: ép victim bay NGAY (không chờ
        /// release-timer) + vào tumble. KnockbackResolver đọc để launch tức thì như IsSpike.</summary>
        public readonly bool CausesLaunch;

        /// <summary>Đòn đánh THẤP (block-low mới đỡ) — P6.</summary>
        public readonly bool HitsLow;

        /// <summary>Đòn đánh CAO (block-high mới đỡ) — P6.</summary>
        public readonly bool HitsHigh;

        /// <summary>Số frame HURTSTUN đòn này gây ra (mang từ MoveData.hitstunFrames). Victim đọc ở
        /// HurtState để khóa hành động đúng thời lượng (P6). Đếm frame hurtstun HỢP LỆ — là thuộc
        /// tính của đòn ĐỊCH, không phải frame-data clip của victim.</summary>
        public readonly int HitstunFrames;

        /// <summary>Điểm tiếp xúc gần đúng (cho VFX spark P5/P8). = vị trí victim nếu không có chính xác.</summary>
        public readonly Vector2 ContactPoint;

        /// <summary>(P10 ADD) Vai trò đòn (MoveData.role). HitReactionController (P6) đọc: nếu
        /// <see cref="MoveRole.BlockBreak"/> → BỎ nhánh block, LUÔN vào Hurt (Ultimate phá block hoàn
        /// toàn). Mặc định <see cref="MoveRole.Normal"/> (constructor cũ không truyền) → P5/P7 không đổi.</summary>
        public readonly MoveRole Role;

        /// <summary>(VFX/SFX ADD) Hiệu ứng trúng đòn PER-MOVE (MoveData.hitEffect — spark + hitsound
        /// riêng từng đòn như MUGEN). null = HitEffectPlayer dùng mặc định nhân vật. CHỈ cho
        /// presentation — không đọc số gameplay từ đây.</summary>
        public readonly EffectData HitEffect;

        /// <summary>(VFX/SFX ADD) Hiệu ứng bị đỡ PER-MOVE (MoveData.blockEffect). null = mặc định.</summary>
        public readonly EffectData BlockEffect;

        /// <summary>(Balance ADD) Hệ số hồi mana của đòn này (MoveData.manaGainMultiplier). ManaBar (P8)
        /// nhân vào gain — đòn tiêu mana đặt &lt;1/0 để không "hòa vốn" meter. Constructor cũ không truyền
        /// → mặc định 1 (đòn thường không đổi).</summary>
        public readonly float ManaGainMult;

        public HitData(
            GameObject attacker, GameObject victim,
            float damagePercent, float launchAngle, float baseKnockback, float knockbackGrowth,
            int hitstopFrames, int facingSign, bool isSpike, bool causesLaunch,
            bool hitsLow, bool hitsHigh,
            int hitstunFrames,
            Vector2 contactPoint,
            MoveRole role = MoveRole.Normal,
            EffectData hitEffect = null,
            EffectData blockEffect = null,
            float manaGainMult = 1f)
        {
            HitEffect = hitEffect;
            BlockEffect = blockEffect;
            ManaGainMult = manaGainMult;
            Role = role;
            Attacker = attacker;
            Victim = victim;
            DamagePercent = damagePercent;
            LaunchAngle = launchAngle;
            BaseKnockback = baseKnockback;
            KnockbackGrowth = knockbackGrowth;
            HitstopFrames = hitstopFrames;
            FacingSign = facingSign;
            IsSpike = isSpike;
            CausesLaunch = causesLaunch;
            HitsLow = hitsLow;
            HitsHigh = hitsHigh;
            HitstunFrames = hitstunFrames;
            ContactPoint = contactPoint;
        }
    }
}
