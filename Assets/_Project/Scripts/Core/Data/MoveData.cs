using UnityEngine;

namespace FA.Core
{
    /// <summary>Phân loại move (§3.4) — nguồn để state quyết nhánh xử lý (Normal vs Special vs Super).</summary>
    public enum MoveCategory
    {
        Normal = 0,         // L/M/H mặt đất/trên không (P5/P7)
        CommandNormal = 1,  // normal kèm hướng (vd ↘+H)
        Special = 2,        // motion input (P9)
        Super = 3,          // nút riêng + cost mana (P9)
    }

    /// <summary>
    /// Vai trò GDD của move (§3.4 / GDD). Dùng để hệ thống khác xử lý đặc thù (P9/P10):
    /// Projectile → spawn prefab layer ProjectileP1/P2; Reversal/AntiAir → i-frame/ưu tiên;
    /// MixUp/BlockBreak → tương tác block. P4 chỉ định nghĩa enum (chưa tiêu thụ).
    /// </summary>
    public enum MoveRole
    {
        Normal = 0,
        Projectile = 1,
        Reversal = 2,
        AntiAir = 3,
        MixUp = 4,
        BlockBreak = 5,
    }

    /// <summary>
    /// Dữ liệu 1 đòn đánh (§3.4). ScriptableObject ⇒ thêm move = thêm asset, KHÔNG sửa code (extensible §1.2).
    /// Đặt ở FA.Core vì nhiều module dùng chung (Combat P5–7, Specials P9–10).
    ///
    /// QUY TẮC VÀNG (§1.1/§3.4): **KHÔNG có field startupFrames/activeFrames/recoveryFrames**.
    /// Khoảng active = giữa Animation Event HitboxOn→HitboxOff trên clip; hết recovery = event RecoveryEnd.
    /// MoveData chỉ giữ tham số GAMEPLAY (cost, knockback, role, advantage) — KHÔNG số frame timing.
    ///
    /// onBlock/onHitAdvantage LÀ frame-advantage (tham số balance, hợp lệ ở SO) — KHÁC startup/active/recovery.
    /// </summary>
    [CreateAssetMenu(menuName = "FA/Combat/Move Data", fileName = "Move")]
    public sealed class MoveData : ScriptableObject
    {
        [Header("Nhận diện / Animation")]
        [Tooltip("Định danh move (debug/log/tra cứu). Vd 'nL', 'qcf_L', 'super1'.")]
        public string moveId = "move";

        [Tooltip("Phân loại move (Normal/CommandNormal/Special/Super).")]
        public MoveCategory category = MoveCategory.Normal;

        [Tooltip("Animator StateId để chuyển sang clip đòn (cầu nối code↔Animator §3.1). " +
                 "Ưu tiên id thay vì ref clip cứng. P5 đặt số state Attack_*.")]
        public int animatorStateId = -1;

        [Tooltip("(Tùy chọn) Tham chiếu clip — chỉ để tiện tra/đặt Animation Event. KHÔNG bắt buộc.")]
        public AnimationClip clip;

        [Header("Input")]
        [Tooltip("Motion input kích đòn (None cho normal). Special P9 dùng. Parser tra tương-đối-facing.")]
        public MotionInput motion = MotionInput.None;

        [Tooltip("Nút tấn công kích đòn (L/M/H/Super1/Super2/Rage). Normal: chọn L/M/H.")]
        public InputButton button = InputButton.L;

        [Tooltip("Frame giữ để charge mạnh hơn (0 = không charge). KHUNG P9. CẦN TEST.")]
        [Min(0)] public int chargeFrames = 0;

        [Header("Cost")]
        [Tooltip("Số Ô mana tiêu thụ (P8/P9). 0 cho normal.")]
        [Min(0)] public int manaCostUnits = 0;

        [Tooltip("Nộ tiêu thụ (P10, vd burst/ultimate). 0 nếu không dùng.")]
        [Min(0f)] public float rageCost = 0f;

        [Tooltip("Yêu cầu đang trong Rage Mode mới ra được (Ultimate P10).")]
        public bool requiresRageMode = false;

        [Header("Damage / Knockback (Smash %-model + SF base)")]
        [Tooltip("Phần trăm cộng vào thanh % mỗi hit (+5% base GDD). P7 dùng. CẦN TEST.")]
        public float baseDamagePercent = 5f;

        [Tooltip("Góc tung (độ, 0 = ngang ra trước, 90 = thẳng lên). P7 áp knockback theo góc này.")]
        public float launchAngle = 45f;

        [Tooltip("Lực knockback cơ sở (P7). Lực thực = base + growth*%địch.")]
        [Min(0f)] public float baseKnockback = 5f;

        [Tooltip("Hệ số tăng knockback theo % địch (P7 Smash-model). CẦN TEST.")]
        [Min(0f)] public float knockbackGrowth = 0.1f;

        [Tooltip("Đòn spike (đánh thẳng xuống) — P7. launchAngle nên hướng xuống nếu bật.")]
        public bool isSpike = false;

        [Tooltip("Đòn KẾT COMBO / LAUNCHER (P7): ép victim bay NGAY (không chờ release-timer) + vào " +
                 "tumble (LauncherState) dù chưa đủ tốc độ. Đòn thường (false) chỉ tích % + hurtstun nhẹ, " +
                 "bay xa khi combo kết thúc. Đặt true cho 1 đòn 'kết' (vd nH hoặc 1 aerial). CẦN TEST.")]
        public bool causesLaunch = false;

        [Header("Role (GDD)")]
        [Tooltip("Vai trò đặc thù (Projectile/Reversal/AntiAir/MixUp/BlockBreak). P9/P10 tiêu thụ.")]
        public MoveRole role = MoveRole.Normal;

        [Header("Block / Frame Advantage (GHI CHÚ — dùng P6, KHÔNG phải startup/active/recovery)")]
        [Tooltip("Lợi/thiệt frame sau khi đối thủ BLOCK (+ = mình lợi). Tham số balance. CẦN TEST.")]
        public int onBlockAdvantage = 0;

        [Tooltip("Lợi/thiệt frame sau khi đối thủ TRÚNG (+ = mình lợi). Tham số balance. CẦN TEST.")]
        public int onHitAdvantage = 0;

        [Tooltip("Đòn đánh THẤP (block-low mới đỡ được) — P6.")]
        public bool hitsLow = false;

        [Tooltip("Đòn đánh CAO (block-high mới đỡ được) — P6. Cả low+high = overhead tùy thiết kế.")]
        public bool hitsHigh = true;

        [Header("Hit Reaction (P6)")]
        [Tooltip("Số frame HURTSTUN mà đòn này gây cho victim (hằng per-move kiểu SF; mang qua HitData). " +
                 "Đòn nặng dài hơn để link combo. Đây KHÔNG phải startup/active/recovery của ĐÒN MÌNH " +
                 "(vẫn cấm, dùng Animation Event) — mà là thuộc tính gameplay 'đòn này khóa victim bao lâu'. " +
                 "Gợi ý nL/nM/nH = 12/16/20. CẦN TEST.")]
        [Min(0)] public int hitstunFrames = 16;

        [Header("Hitstop")]
        [Tooltip("Số frame freeze cả 2 khi trúng (4–8). P5 dùng (Animator.speed=0 tạm). CẦN TEST.")]
        [Min(0)] public int hitstopFrames = 6;
    }
}
