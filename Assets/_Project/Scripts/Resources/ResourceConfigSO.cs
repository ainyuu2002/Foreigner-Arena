using UnityEngine;

namespace FA.Resources
{
    /// <summary>
    /// CONFIG cho 2 thanh tài nguyên Mana + Rage (P8, research §5). Gom MỌI hằng số balance vào một
    /// ScriptableObject để chỉnh trong Inspector — KHÔNG hardcode trong ManaBar/RageBar (§1.2/§6).
    /// Code chỉ ĐỌC SO này + cộng dồn + clamp + raise channel ("điều phối", không thay Unity).
    ///
    /// TẤT CẢ con số dưới đây = placeholder hợp lý, **CẦN TEST balance** (§6/§13: "tốc độ hồi nộ",
    /// "cost mana từng action"). Cost (tiêu) để P9/P10; P8 chỉ có TĂNG + HIỂN THỊ.
    ///
    /// Per-character: GDD cho phép số khác nhau theo nhân vật → có thể gán SO riêng mỗi nhân vật
    /// (hoặc tham chiếu từ CharacterData sau). P8: 1 SO dùng chung cho SampleFighter là đủ.
    /// </summary>
    [CreateAssetMenu(menuName = "FA/Data/Resource Config", fileName = "ResourceConfig")]
    public sealed class ResourceConfigSO : ScriptableObject
    {
        // ───────────────────────── MANA (kiểu SFV/GG Tension, chia ô) ─────────────────────────
        [Header("MANA — chia ô (kiểu SFV EX / GG Tension)")]
        [Tooltip("Đơn vị nội bộ cho 1 ô mana (chỉ là scale; mana lưu float, 1 ô = giá trị này). CẦN TEST.")]
        [Min(1f)]
        [SerializeField] private float manaPerSegment = 100f;

        [Tooltip("Tổng số ô mana tối đa → maxMana = manaPerSegment × maxSegments. CẦN TEST (đủ 1 super " +
                 "50-75% mana ở P9; SFV dùng 3 ô EX).")]
        [Min(1)]
        [SerializeField] private int maxSegments = 3;

        [Tooltip("Số ô mana KHỞI ĐẦU (GDD: 'start 1 ô').")]
        [Min(0)]
        [SerializeField] private int startSegments = 1;

        [Tooltip("Mana cộng cho mỗi 1% damage GÂY RA (attacker, trúng thật). VD nL ~5% × 0.8 = 4 mana; " +
                 "~25 đòn đầy 1 ô. CẦN TEST tốc độ nạp.")]
        [Min(0f)]
        [SerializeField] private float manaGainPerDamage = 0.8f;

        [Tooltip("Hệ số mana khi đòn bị BLOCK (attacker vẫn hồi nhưng ít hơn trúng thật). CẦN TEST.")]
        [Range(0f, 1f)]
        [SerializeField] private float blockMultiplier = 0.3f;

        // ───────────────────────── RAGE (kiểu SF4 Revenge, lai) ─────────────────────────
        [Header("RAGE — thanh nộ 0..max (P10 đọc IsFull mở Rage Mode)")]
        [Tooltip("Giá trị nộ tối đa (đầy = mở Rage Mode/Ultimate ở P10).")]
        [Min(1f)]
        [SerializeField] private float rageMax = 100f;

        [Tooltip("Nộ cộng cho mỗi 1% damage GÂY RA (attacker = NHIỀU). CẦN TEST.")]
        [Min(0f)]
        [SerializeField] private float rageGainDealt = 1.0f;

        [Tooltip("Nộ cộng cho mỗi 1% damage BỊ ĐÁNH (victim = ÍT hơn dealt nhưng đáng kể → comeback). CẦN TEST.")]
        [Min(0f)]
        [SerializeField] private float rageGainTaken = 0.6f;

        [Tooltip("Hệ số nộ khi đòn bị BLOCK (block = RẤT ÍT — nhân vào lượng dealt-block VÀ taken-block). CẦN TEST.")]
        [Range(0f, 1f)]
        [SerializeField] private float rageBlockMultiplier = 0.15f;

        // ───────────────────────── Accessors (read-only, derived) ─────────────────────────
        /// <summary>Đơn vị nội bộ 1 ô mana.</summary>
        public float ManaPerSegment => manaPerSegment;

        /// <summary>Số ô tối đa (HUD vẽ N ô).</summary>
        public int MaxSegments => maxSegments;

        /// <summary>Số ô khởi đầu (GDD start 1 ô).</summary>
        public int StartSegments => startSegments;

        /// <summary>Mana tối đa = manaPerSegment × maxSegments.</summary>
        public float MaxMana => manaPerSegment * maxSegments;

        /// <summary>Mana khởi đầu = manaPerSegment × startSegments.</summary>
        public float StartMana => manaPerSegment * startSegments;

        /// <summary>Mana cộng mỗi 1% damage dealt (attacker, trúng thật).</summary>
        public float ManaGainPerDamage => manaGainPerDamage;

        /// <summary>Hệ số mana khi đòn bị block.</summary>
        public float ManaBlockMultiplier => blockMultiplier;

        /// <summary>Nộ tối đa.</summary>
        public float RageMax => rageMax;

        /// <summary>Nộ cộng mỗi 1% damage dealt (attacker).</summary>
        public float RageGainDealt => rageGainDealt;

        /// <summary>Nộ cộng mỗi 1% damage taken (victim).</summary>
        public float RageGainTaken => rageGainTaken;

        /// <summary>Hệ số nộ khi block.</summary>
        public float RageBlockMultiplier => rageBlockMultiplier;
    }
}
