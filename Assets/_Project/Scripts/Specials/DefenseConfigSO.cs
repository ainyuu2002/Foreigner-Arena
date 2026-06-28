using UnityEngine;

namespace FA.Specials
{
    /// <summary>
    /// CONFIG cho tầng phòng thủ/comeback P10 (research §5). Gom MỌI hằng số balance riêng P10 vào một
    /// ScriptableObject để chỉnh trong Inspector — KHÔNG hardcode (§1.2/§6 "không tự ý chốt, đưa vào SO").
    /// TẤT CẢ con số = placeholder hợp lý, **CẦN TEST** (đánh dấu trong tooltip). Cost mana né = SỐ Ô
    /// nguyên (tái dùng IManaSpender.TrySpend(int) P9); nộ tiêu/hoàn = PHẦN TRĂM (IRageSpender, fraction).
    ///
    /// i-frame WINDOW của né KHÔNG nằm ở đây — nó đến từ Animation Event HurtboxOff/On trên clip (QUY TẮC
    /// VÀNG §1.1, frame data = Animation Event). Air-dodge landing lag CŨNG là Animation Event RecoveryEnd
    /// trên clip landing (không phải số ở đây). SO này chỉ giữ tham số GAMEPLAY (cost, multiplier, duration).
    ///
    /// Đặt ở FA.Specials (cùng module P9/P10). Code chỉ ĐỌC SO + điều phối — KHÔNG thay Unity.
    /// </summary>
    [CreateAssetMenu(menuName = "FA/Data/Defense Config", fileName = "DefenseConfig")]
    public sealed class DefenseConfigSO : ScriptableObject
    {
        // ───────────────────────── COST tài nguyên né ─────────────────────────
        [Header("MANA cost (số Ô nguyên — tái dùng IManaSpender.TrySpend(int) P9)")]
        [Tooltip("Ground Dodge: số ô mana tiêu (gate spam thay cooldown — GDD KHÔNG cooldown). CẦN TEST.")]
        [Min(0)] public int groundDodgeManaCost = 1;

        [Tooltip("Air Dodge: số ô mana tiêu. 1 lần/lần-trên-không (quota như air-dash P3). CẦN TEST.")]
        [Min(0)] public int airDodgeManaCost = 1;

        [Tooltip("RC Dash: số ô mana tiêu (đắt hơn dodge — mạnh hơn: cancel mọi đòn đang ra). CẦN TEST.")]
        [Min(0)] public int rcDashManaCost = 2;

        [Header("NỘ cost / refund (PHẦN TRĂM RageMax — IRageSpender, fraction 0..1)")]
        [Tooltip("Burst Breaker: % nộ tiêu flat (GDD chốt 50%). CẦN TEST.")]
        [Range(0f, 1f)] public float burstRageCost = 0.5f;

        [Tooltip("Rage Mode: % nộ tiêu khi BẬT (GDD: tiêu hết 100%). Thường = 1.0.")]
        [Range(0f, 1f)] public float rageModeRageCost = 1f;

        [Tooltip("Rage Mode HẾT HẠN: % nộ HOÀN lại (GDD 20–25%, giữa = 22%). §6 'để ngỏ' — CẦN TEST.")]
        [Range(0f, 1f)] public float rageModeRefundFraction = 0.22f;

        // ───────────────────────── Tốc độ lướt né ─────────────────────────
        [Header("Tốc độ lướt (đơn vị/giây — set qua Rigidbody2D.linearVelocity)")]
        [Tooltip("RC Dash: vận tốc lướt ngang × facing (lướt phẳng, khóa Y). CẦN TEST.")]
        [Min(0f)] public float rcDashSpeed = 14f;

        [Tooltip("Ground Dodge: lùi nhẹ ra-sau (0 = đứng tại chỗ). CẦN TEST.")]
        public float groundDodgeBackSpeed = 0f;

        // LƯU Ý DESIGNER: 2 field dưới KHÔNG được code đọc. Burst push thực tế lấy KNOCKBACK từ
        // 'burstPushMove' (MoveData) gán trên DefenseController (đẩy địch qua HitData/KnockbackResolver,
        // §3.4 — KHÔNG áp lực thủ công ở đây). Giữ lại làm tham chiếu giá trị mong muốn; chỉnh LỰC ĐẨY
        // burst trên burstPushMove.MoveData, KHÔNG ở đây.
        [Tooltip("[KHÔNG dùng — burst push lấy knockback từ burstPushMove MoveData] Lực đẩy địch ra xa.")]
        [Min(0f)] public float burstPushbackForce = 16f;

        [Tooltip("[KHÔNG dùng — burst push lấy knockback từ burstPushMove MoveData] Thành phần đẩy lên.")]
        [Min(0f)] public float burstPushbackUp = 6f;

        // ───────────────────────── Rage Mode buff multiplier ─────────────────────────
        [Header("RAGE MODE — buff multiplier (qua IBuffContext, listener đọc null-safe ×1.0)")]
        [Tooltip("Thời lượng Rage Mode (GIÂY thực — buff theo thời gian, NGOẠI LỆ hợp lệ của đếm-time, " +
                 "research §3.4). §6 'để ngỏ' gợi ý 8–10s. CẦN TEST.")]
        [Min(0.1f)] public float rageModeDuration = 9f;

        [Tooltip("Hệ số +DAMAGE gây ra khi Rage. Gợi ý ×1.2. CẦN TEST.")]
        [Min(0f)] public float rageDamageMult = 1.2f;

        [Tooltip("Hệ số +KNOCKBACK gây ra khi Rage. Gợi ý ×1.2. CẦN TEST.")]
        [Min(0f)] public float rageKnockbackMult = 1.2f;

        [Tooltip("Hệ số +SPEED di chuyển khi Rage. Gợi ý ×1.15. CẦN TEST.")]
        [Min(0f)] public float rageSpeedMult = 1.15f;

        [Tooltip("Hệ số HURTSTUN NHẬN khi Rage (×0.8 = nhận ít stun hơn). Gợi ý ×0.8. CẦN TEST.")]
        [Min(0f)] public float rageHitstunTakenMult = 0.8f;
    }
}
