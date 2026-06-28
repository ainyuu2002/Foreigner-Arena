namespace FA.Core
{
    /// <summary>
    /// (P13) MỘT NGUỒN buff đóng góp multiplier + cờ. Cùng "shape" với <see cref="IBuffContext"/> nhưng là
    /// đóng góp của 1 nguồn ĐƠN LẺ (Rage Mode P10, DMG Buff item P13, debuff tương lai...). Khi nguồn KHÔNG
    /// active → trả mặc định vô hại (1f cho mult, false cho cờ).
    ///
    /// LÝ DO TỒN TẠI (research §4 — vấn đề đa-nguồn buff): trước P13 chỉ 1 component (RageModeController)
    /// implement IBuffContext, consumer (HitboxAuthoring/HitReaction/Hitstop/Motor) dùng GetComponent
    /// &lt;IBuffContext&gt;() → chỉ lấy được 1. DMG Buff item cần STACK với Rage → cần GOM nhiều nguồn.
    /// Giải pháp (Phương án B đã chốt): tách <see cref="IBuffSource"/> (nguồn) khỏi <see cref="IBuffContext"/>
    /// (mặt tiền). Mỗi nguồn implement IBuffSource; <see cref="BuffAggregator"/> (1 component, implement
    /// IBuffContext) gom mọi IBuffSource trên nhân vật → TÍCH multiplier + OR cờ. Consumer GIỮ NGUYÊN cách
    /// đọc GetComponent&lt;IBuffContext&gt;() (giờ trỏ BuffAggregator).
    ///
    /// RageModeController (FA.Specials) đổi từ "implement IBuffContext" → "implement IBuffSource" (CHỈ 1 dòng
    /// khai báo interface; body GIỮ NGUYÊN vì 6 thành viên cùng tên/cùng kiểu). DMG Buff item =
    /// ItemDamageBuff (FA.Items) implement IBuffSource. KHÔNG reference chéo: cả Specials lẫn Items đều chỉ
    /// ref Core; Aggregator ở Core gom qua GetComponents&lt;IBuffSource&gt;().
    /// </summary>
    public interface IBuffSource
    {
        /// <summary>Hệ số nhân DAMAGE % gây ra của RIÊNG nguồn này (×1.0 khi nguồn không active). Attacker-side.</summary>
        float DamageDealtMult { get; }

        /// <summary>Hệ số nhân KNOCKBACK gây ra của RIÊNG nguồn này (×1.0 khi không active). Attacker-side.</summary>
        float KnockbackDealtMult { get; }

        /// <summary>Hệ số nhân HURTSTUN NHẬN của RIÊNG nguồn này (×1.0 thường; &lt;1 = nhận ít stun). Victim-side.</summary>
        float HitstunTakenMult { get; }

        /// <summary>Hệ số nhân TỐC ĐỘ di chuyển của RIÊNG nguồn này (×1.0 thường; &gt;1 khi buff speed).</summary>
        float MoveSpeedMult { get; }

        /// <summary>Nguồn này đang đặt cờ "Rage active" (Burst bị chặn khi true). Aggregator OR các nguồn.</summary>
        bool IsRageActive { get; }

        /// <summary>Nguồn này đang đặt cờ "Ultimate active" (miễn hitstop). Aggregator OR các nguồn.</summary>
        bool IsUltimateActive { get; }
    }
}
