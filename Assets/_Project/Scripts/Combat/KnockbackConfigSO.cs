using UnityEngine;

namespace FA.Combat
{
    /// <summary>
    /// GOM MỌI HẰNG knockback Phase 7 (research §5.C) — 1 asset chỉnh trong Inspector, KHÔNG hardcode
    /// (§6 CLAUDE.md). Đặt ở FA.Combat (KHÔNG đọc CharacterData của Movement → tránh cross-module §1.3):
    /// cap % + referenceWeight ở đây; weight thực của victim đọc trực tiếp Rigidbody2D.mass.
    ///
    /// KnockbackPercent (thanh %), KnockbackResolver (áp lực), KnockbackCollisionRelay (bounce/splat),
    /// các air-reaction state đều ĐỌC config này. Mọi field đánh dấu CẦN TEST = giá trị mặc định hợp lý
    /// theo research, balance sau khi chơi thử.
    ///
    /// PhysicsMaterial2D bounce (wallBounce/groundBounce) là ASSET RIÊNG gán tay (§3.4 research) — SO này
    /// chỉ giữ bounciness gợi ý + ngưỡng tốc độ để CHỌN state; material vật lý do Unity lo (KHÔNG code
    /// phản xạ vector).
    /// </summary>
    [CreateAssetMenu(menuName = "FA/Combat/Knockback Config", fileName = "KnockbackConfig")]
    public sealed class KnockbackConfigSO : ScriptableObject
    {
        [Header("Thanh % (KnockbackPercent)")]
        [Tooltip("Cap % tối đa (200–300, để ngỏ §13 → chọn 250). Clamp khi cộng → knockback không vô cực. CẦN TEST.")]
        [Min(1f)] public float percentCap = 250f;

        [Tooltip("+%/hit nếu MoveData.baseDamagePercent ≤ 0 (fallback GDD +5%). CẦN TEST.")]
        [Min(0f)] public float percentPerHitFallback = 5f;

        [Tooltip("Mốc combo 1 cho +1%/hit (khớp ComboCounter.milestone1). GDD.")]
        [Min(1)] public int milestone1 = 10;

        [Tooltip("Mốc combo 2 cho +1%/hit nữa (khớp ComboCounter.milestone2). GDD.")]
        [Min(1)] public int milestone2 = 20;

        [Header("Áp lực (KnockbackResolver) — Smash-lite")]
        [Tooltip("Weight chuẩn: force × (referenceWeight / victimMass). Nặng hơn ref → bay ít hơn. CẦN TEST.")]
        [Min(0.01f)] public float referenceWeight = 1f;

        // (P16) ĐÃ BỎ `knockbackReleaseFrames` (P7 cũ = 26): không còn chờ timer "combo coi như kết thúc"
        // mới văng. KnockbackResolver giờ VĂNG tại Animation Event KnockbackPoint (frame hit cuối) — bay
        // ngay khi hết hitstop; attacker đang nối → không raise launch → không văng. Xem
        // [[phase16-knockback]] / [[phase7-knockback]].

        [Header("DI (Directional Influence) — digital")]
        [Tooltip("DI xoay vector knockback tối đa ±N° theo input victim (digital, vài độ §5 CLAUDE.md). " +
                 "Nhỏ (~15°) để KHÔNG lật được mọi combo (research §4 cạm bẫy 8). CẦN TEST.")]
        [Range(0f, 45f)] public float diMaxDegrees = 15f;

        [Tooltip("SDI (lắc trong hitlag) — để ngỏ §13, TẮT mặc định. Hook cho sau, P7 KHÔNG bật.")]
        public bool enableSDI = false;

        [Header("Wall Splat")]
        [Tooltip("BẬT cơ chế DÁN tường (WallSplat 17)? Quyết định người dùng 2026-07-02: TẮT — va tường " +
                 "khi bị đòn LUÔN BẬT RA bằng PhysicsMaterial2D bounciness (như bóng, hấp thụ theo hệ số " +
                 "trên material), không dán. Tick lại nếu muốn kiểu SFV wall-splat.")]
        public bool enableWallSplat = false;

        [Tooltip("Tốc độ va tường tối thiểu để DÁN tường (dưới = chỉ mất đà, Rigidbody2D tự xử). " +
                 "Chỉ dùng khi enableWallSplat bật. CẦN TEST.")]
        [Min(0f)] public float wallSplatSpeedThreshold = 12f;

        [Tooltip("Số fixed-step DÁN tường (stun) trước khi rơi xuống juggle tiếp. CẦN TEST.")]
        [Min(1)] public int wallSplatStunFrames = 18;

        [Header("Ground Bounce")]
        [Tooltip("Tốc độ va đất tối thiểu để NẢY (đặc biệt spike). Dưới = đáp thường. CẦN TEST.")]
        [Min(0f)] public float groundBounceSpeedThreshold = 14f;

        [Header("PhysicsMaterial2D bounciness (gợi ý — gán tay lên asset material)")]
        [Tooltip("Bounciness tường (0..1, <1 = hấp thụ một phần GDD). Gán vào PhysicsMaterial2D 'WallBounce'. CẦN TEST.")]
        [Range(0f, 1f)] public float wallBounciness = 0.5f;

        [Tooltip("Bounciness sàn. Gán vào PhysicsMaterial2D 'GroundBounce'. CẦN TEST.")]
        [Range(0f, 1f)] public float groundBounciness = 0.4f;

        [Header("Tumble / về-ground")]
        [Tooltip("Tốc độ tổng dưới ngưỡng này + chạm đất → THOÁT tumble về Idle/Walk (hết bay). CẦN TEST.")]
        [Min(0f)] public float tumbleEndSpeed = 1.5f;

        [Tooltip("FAILSAFE chống kẹt: vào Tumble/GroundBounce khi ĐÃ đứng đất (wall-splat sát đất, bounce " +
                 "bị hấp thụ, knockback trượt ngang) → không bao giờ 'rời đất' → gate landing _hasLeftGround " +
                 "chặn vĩnh viễn. Sau N fixed-step liên tục trên đất không rời được (tumble: kèm hết đà) → " +
                 "coi như đã đáp. CẦN TEST.")]
        [Min(1)] public int groundedFailsafeFrames = 8;

        [Tooltip("TRẦN thời gian nằm-trượt trên đất (fixed-step) sau khi đã đáp: mặt sàn không ma sát " +
                 "(băng) / gió bão bơm vx liên tục có thể giữ tốc độ > tumbleEndSpeed vô hạn → victim " +
                 "nằm trượt mãi. Đủ N step chạm đất liên tục → dậy dù còn đà (Idle sẽ ghì vx). " +
                 "0 = tắt (hành vi cũ). CẦN TEST.")]
        [Min(0)] public int maxLyingFrames = 45;

        [Tooltip("BẬT DẬY SỚM (feedback test map băng): nằm đủ N fixed-step thì ẤN DI CHUYỂN là dậy ngay " +
                 "(không chờ hết đà / hết maxLyingFrames — victim chủ động cứu mình khỏi trượt). " +
                 "0 = tắt (chỉ dậy tự động). CẦN TEST.")]
        [Min(0)] public int earlyWakeUpFrames = 10;

        [Tooltip("SAFETY-NET tuyệt đối (fix kẹt nảy-vô-hạn): TỔNG fixed-step tối đa được ở trong Tumble " +
                 "TÍNH CẢ lúc trên không (KHÔNG cần grounded). Vượt trần này → tự thoát về Idle dù chưa " +
                 "đáp. Chặn ca 'không bao giờ grounded' (rơi trúng nấm bouncy + material nảy → nảy mãi, " +
                 "hoặc collider lơ lửng). Đặt RẤT RỘNG (juggle thật kết thúc sớm hơn nhiều qua grounded) " +
                 "để KHÔNG đổi cảm giác chơi bình thường — chỉ là lưới cuối. 0 = tắt. ~300 = 5s @50fps.")]
        [Min(0)] public int tumbleHardTimeoutFrames = 300;

        /// <summary>+1%/hit milestone (khớp ComboCounter) — KnockbackPercent gọi để cộng đúng GDD.</summary>
        public int MilestoneBonus(int comboCount)
        {
            int bonus = 0;
            if (comboCount >= milestone1) bonus++;
            if (comboCount >= milestone2) bonus++;
            return bonus;
        }
    }
}
