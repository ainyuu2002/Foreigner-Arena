using UnityEngine;

namespace FA.Items
{
    /// <summary>
    /// (P13) CẤU HÌNH RAGE ORB (ScriptableObject — mọi số balance, §6 "để ngỏ → SO + CẦN TEST"). RageOrb +
    /// RageOrbSpawner đọc. Clone Smash Ball nhưng cần 15 HIT CHUNG cả 2 người → nhặt = FULL nộ.
    /// </summary>
    [CreateAssetMenu(menuName = "FA/Items/Rage Orb Config", fileName = "RageOrbConfig")]
    public sealed class RageOrbConfigSO : ScriptableObject
    {
        [Header("Điều kiện phá (CẦN TEST)")]
        [Tooltip("Số HIT CHUNG (cả 2 người cộng dồn) để phá orb → người gây hit cuối = full nộ. GDD chốt 15.")]
        [Min(1)] public int hitsToBreak = 15;

        [Tooltip("Counter giảm dần: số HIT mất mỗi giây nếu không ai đánh tiếp (clamp đáy 0). CẦN TEST: 0.4/s.")]
        [Min(0f)] public float counterDecayPerSecond = 0.4f;

        [Header("Bay (CẦN TEST)")]
        [Tooltip("Tốc độ bay TỐI THIỂU (u/s). CẦN TEST: 4.")]
        [Min(0f)] public float speedMin = 4f;

        [Tooltip("Tốc độ bay TỐI ĐA (u/s). CẦN TEST: 9.")]
        [Min(0f)] public float speedMax = 9f;

        [Tooltip("Đổi hướng/tốc định kỳ mỗi N giây. CẦN TEST: 1.5s.")]
        [Min(0.1f)] public float redirectInterval = 1.5f;

        [Tooltip("Frame HITSTUN NHẸ của orb khi bị đánh (đứng yên/đổi hướng) — KHÔNG gây dmg cho người. CẦN TEST: 6f.")]
        [Min(0)] public int hitstunFramesOnHit = 6;

        [Header("Vùng bay (giữ orb trong map — KHÔNG ref FA.Stage)")]
        [Tooltip("Tâm vùng orb bay (world).")]
        public Vector2 boundsCenter = Vector2.zero;

        [Tooltip("Kích thước vùng orb bay (world). Chạm mép → nảy (đảo velocity). Đặt trong tường, xa death zone.")]
        public Vector2 boundsSize = new Vector2(20f, 10f);

        [Header("Vòng đời (GDD chốt 20s)")]
        [Tooltip("Tồn tại N giây rồi Destroy nếu chưa phá. Thu nhỏ dần trong thời gian này (cảnh báo). GDD: 20s.")]
        [Min(1f)] public float lifetime = 20f;

        [Tooltip("Tỉ lệ scale nhỏ nhất ở cuối lifetime (vd 0.3 = co còn 30% lúc gần hết).")]
        [Range(0.05f, 1f)] public float minScaleAtEnd = 0.3f;
    }
}
