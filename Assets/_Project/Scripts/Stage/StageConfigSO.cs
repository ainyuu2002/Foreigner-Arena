using UnityEngine;

namespace FA.Stage
{
    /// <summary>
    /// GOM MỌI HẰNG map Phase 11 (research §5) — 1 asset/map, chỉnh trong Inspector, KHÔNG hardcode
    /// (§6 CLAUDE.md). Mỗi map (Ice/Fire) tạo 1 asset riêng để tinh chỉnh độc lập (ngưỡng vỡ tường,
    /// tốc độ lava, cường độ bão tuyết...). Mọi field đánh dấu CẦN TEST = giá trị mặc định hợp lý theo
    /// research, balance sau khi chơi thử trong Unity.
    ///
    /// PhysicsMaterial2D "IceSurface" là ASSET RIÊNG gán tay lên collider tile băng (§3.4 research) —
    /// SO này KHÔNG giữ friction (Unity vật lý lo trượt, KHÔNG nhân hệ số code). SO chỉ giữ số gameplay
    /// điều phối (ngưỡng, chu kỳ, lực nudge, %/giây).
    /// </summary>
    [CreateAssetMenu(menuName = "FA/Stage/Stage Config", fileName = "StageConfig")]
    public sealed class StageConfigSO : ScriptableObject
    {
        [Header("Breakable Wall (BreakableWall)")]
        [Tooltip("Break-stat tích lũy để VỠ tường (đơn vị = tổng relativeVelocity.magnitude các cú đập). " +
                 "Phụ thuộc thang lực P7. CẦN TEST.")]
        [Min(1f)] public float breakThreshold = 100f;

        [Tooltip("Ngưỡng tốc độ va chạm tối thiểu tính-là-knockback (loại va đi-bộ thường). Nên ĐỒNG BỘ " +
                 "KnockbackConfigSO.wallSplatSpeedThreshold (~12). CẦN TEST.")]
        [Min(0f)] public float wallImpactMinSpeed = 12f;

        [Tooltip("Map này tường CÓ HỒI không? (GDD 'depends on map'). Mặc định bật; Inspector tắt cho map " +
                 "muốn vỡ-là-vỡ. CẦN TEST theo map.")]
        public bool wallRecovers = true;

        [Tooltip("Sau N giây KHÔNG bị đập thì break-stat bắt đầu hồi (GDD ~5s). CẦN TEST.")]
        [Min(0f)] public float wallRecoverDelay = 5f;

        [Tooltip("Tốc độ hồi break-stat (đơn vị/giây) khi đã quá recoverDelay. Mặc định = breakThreshold/5 " +
                 "(hồi đầy trong ~5s). CẦN TEST.")]
        [Min(0f)] public float wallRecoverPerSec = 20f;

        [Tooltip("Damage % của HITBOX mảnh tường khi NỨT (đẩy/gây dmg nhẹ khi đứng cạnh tường đang vỡ). " +
                 "Mặc định NHỎ — chờ sprite/asset thật. CẦN TEST + phụ thuộc sprite (§6).")]
        [Min(0f)] public float crackedHitboxDamagePercent = 3f;

        [Header("Biome Ice — Bão tuyết (SnowstormHazard)")]
        [Tooltip("Khoảng cách trung bình giữa 2 trận bão (giây). Thực tế random quanh giá trị này. CẦN TEST.")]
        [Min(0f)] public float snowstormInterval = 12f;

        [Tooltip("Mỗi trận bão kéo dài bao lâu (giây). CẦN TEST.")]
        [Min(0f)] public float snowstormDuration = 5f;

        [Tooltip("Random ± quanh interval/duration (giây) để bão không đều đặn đoán trước được. CẦN TEST.")]
        [Min(0f)] public float snowstormRandomJitter = 3f;

        [Tooltip("Khoảng cách giữa 2 cú đẩy ngẫu nhiên trong lúc bão (giây). CẦN TEST.")]
        [Min(0.05f)] public float snowstormNudgeInterval = 0.4f;

        [Tooltip("Cường độ xung lực ngang ngẫu nhiên mỗi cú đẩy (impulse). Đủ 'phá combo' nhưng vẫn chơi " +
                 "được — quá mạnh = mất kiểm soát. 'Đẩy random phá combo là CHỦ Ý' (GDD). CẦN TEST.")]
        [Min(0f)] public float snowstormPushForce = 3f;

        [Tooltip("Hệ số làm CHẬM vận tốc ngang trong lúc bão (1 = không chậm, 0.7 = còn 70%/giây). CẦN TEST.")]
        [Range(0f, 1f)] public float snowstormSlowFactor = 0.85f;

        [Tooltip("GIÓ CÓ HƯỚNG trong bão: tốc độ gió cộng vào vận tốc ngang mỗi FixedUpdate (motor ghi lại vx " +
                 "mỗi tick nên hiệu quả: đứng yên bị TRÔI theo gió ~ giá trị này, đi NGƯỢC gió chậm đi, đi " +
                 "THEO gió nhanh lên). Hướng gió random trái/phải mỗi trận bão. CẦN TEST.")]
        [Min(0f)] public float snowstormWindSpeed = 2.5f;

        [Tooltip("Trần tốc độ ngang mà gió còn đẩy thêm được (chặn cộng dồn vô hạn khi đang bay knockback). CẦN TEST.")]
        [Min(1f)] public float snowstormWindMaxCarry = 12f;

        [Header("Chung — Tường bám trên không (ClingWall, cả 3 map)")]
        [Tooltip("Tốc độ TRƯỢT XUỐNG tối đa khi áp vào tường bám (kẹp vy khi đang rơi). " +
                 "Nhỏ hơn = bám dính hơn. CẦN TEST.")]
        [Min(0f)] public float wallClingSlideSpeed = 1.2f;

        [Header("Biome Fire — Đứng yên tăng % (FireBiome)")]
        [Tooltip("%/giây cộng cho nhân vật ĐỨNG YÊN trong vùng lửa (đủ răn đe đứng yên, ép di chuyển). " +
                 "Đi qua IPercentReceiver → clamp tại percentCap. CẦN TEST.")]
        [Min(0f)] public float firePercentPerSec = 2.5f;

        [Tooltip("Ngưỡng tốc độ coi-là 'đứng yên' (vx magnitude dưới ngưỡng + grounded). CẦN TEST.")]
        [Min(0f)] public float fireStillSpeedEpsilon = 0.1f;

        [Header("Biome Fire — Dung nham dâng (RisingLava)")]
        [Tooltip("Tốc độ lava dâng (đơn vị world/giây). CHẬM để map đủ cao (GDD). CẦN TEST.")]
        [Min(0f)] public float lavaRiseSpeed = 0.4f;

        [Tooltip("Số giây nhấp nháy CẢNH BÁO trước khi lava bắt đầu dâng. CẦN TEST.")]
        [Min(0f)] public float lavaWarningLead = 2.5f;

        [Tooltip("Chu kỳ nhấp nháy icon cảnh báo (giây/lần bật-tắt). CẦN TEST.")]
        [Min(0.05f)] public float lavaWarningBlinkPeriod = 0.25f;

        [Tooltip("Lava dâng tới đỉnh thì GIỮ bao lâu (giây) trước khi RÚT xuống. CẦN TEST.")]
        [Min(0f)] public float lavaHoldSeconds = 5f;

        [Tooltip("Tốc độ lava RÚT xuống (world/giây). CẦN TEST.")]
        [Min(0.01f)] public float lavaRecedeSpeed = 0.5f;

        [Tooltip("Nghỉ bao lâu giữa 2 đợt dâng (giây, random ± jitter bên dưới) — 'dâng núi lửa NGẪU NHIÊN'. CẦN TEST.")]
        [Min(0f)] public float lavaRestSeconds = 14f;

        [Tooltip("Random ± quanh lavaRestSeconds để đợt dâng không đoán trước được. CẦN TEST.")]
        [Min(0f)] public float lavaRestJitter = 6f;

        [Tooltip("%/giây cộng cho người ĐỨNG TRÊN Ô ĐẤT DUNG NHAM nóng (LavaFloorZone — khác FireBiome: " +
                 "tính cả khi đang di chuyển, chỉ cần đứng trong vùng + chạm đất). CẦN TEST.")]
        [Min(0f)] public float lavaFloorPercentPerSec = 4f;

        [Header("Biome Swamp — Slowness + giảm lực nhảy (SwampBiome)")]
        [Tooltip("Hệ số nhân vận tốc NGANG mỗi FixedUpdate khi ĐỨNG ĐẤT trong đầm lầy (motor ghi lại vx mỗi " +
                 "step nên hiệu quả ổn định = walkSpeed × hệ số). 1 = không chậm. GDD: 'slowness thường trực'. CẦN TEST.")]
        [Range(0.3f, 1f)] public float swampSpeedFactor = 0.8f;

        [Tooltip("Hệ số nhân vận tốc NHẢY (vy) ngay khi phát hiện cú nhảy trong đầm lầy. 0.85 ≈ giảm ~25-28% " +
                 "chiều cao (GDD: 'lực nhảy suy giảm phẳng 25%'). CẦN TEST.")]
        [Range(0.5f, 1f)] public float swampJumpScale = 0.85f;

        [Tooltip("Hệ số vận tốc ngang NHÂN THÊM khi đứng trong VŨNG BÙN ĐẶC (MudPitZone — CHỒNG với " +
                 "swampSpeedFactor: ô bùn ướt lầy RÕ RỆT hơn nền đầm thường). CẦN TEST.")]
        [Range(0.3f, 1f)] public float mudSpeedFactor = 0.72f;

        [Tooltip("Vận tốc bật lên (vy) khi đáp xuống MŨ NẤM NHÚN (MushroomBouncer — bù trừ giảm lực nhảy, " +
                 "đường thoát bệ chìm giữa map / lên tầng cao). CẦN TEST.")]
        [Min(0f)] public float mushroomBounceSpeed = 18f;

        [Tooltip("Ngưỡng vy tăng đột ngột trong 1 FixedUpdate để coi-là cú nhảy (impulse). Nhỏ hơn jumpSpeed " +
                 "nhân vật (~12) nhưng lớn hơn dao động thường. CẦN TEST.")]
        [Min(1f)] public float swampJumpImpulseThreshold = 6f;
    }
}
