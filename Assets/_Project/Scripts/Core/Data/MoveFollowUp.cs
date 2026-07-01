using System;

namespace FA.Core
{
    /// <summary>
    /// (P15) Điều kiện để một đòn NỐI (follow-up) được kích hoạt. Chốt người dùng: CHỈ 2 điều kiện.
    /// Block KHÔNG là một condition — đòn bị đỡ tự động KHÔNG thoả OnHit vì tín hiệu "trúng thật"
    /// (kênh OnHitConfirmed) chỉ raise khi không-block (xem <see cref="HitConfirmTracker"/>).
    /// </summary>
    public enum FollowUpCondition
    {
        /// <summary>Nối TỰ DO: miễn còn trong cửa sổ nhận nút (cancel window). Dùng cho đòn 1→2 của JJJ
        /// (combo-bait). Quá window thì ấn lại = đòn 1 từ neutral (CombatController xử lý).</summary>
        Always = 0,

        /// <summary>Chỉ nối khi đòn TRƯỚC ĐÃ TRÚNG THẬT (hit-confirm). Block KHÔNG tính (đạt tự nhiên vì
        /// HitConfirmTracker chỉ nghe OnHitConfirmed — block raise OnHitBlocked, kênh khác). Dùng cho
        /// mắt cuối punish-confirm (vd 2→3 của JJJ).</summary>
        OnHit = 1,
    }

    /// <summary>
    /// (P15) MỘT mắt xích nối đòn (combo chain / target combo / rekka). Khai trong
    /// <see cref="MoveData.followUps"/> của đòn TRƯỚC. Trong cửa sổ cancel (Animation Event
    /// CancelWindowOpen/Close), nếu nút <see cref="button"/> vừa nhấn (và motion khớp, và điều kiện
    /// thoả) → đòn chuyển NGAY sang <see cref="nextMove"/> (cancel recovery, KHÔNG về neutral).
    ///
    /// Struct serializable ⇒ khai trong Inspector của MoveData; thêm chuỗi mới = thêm dữ liệu, KHÔNG
    /// sửa code (extensible §1.2). Áp cho MỌI move (normal/special/super) vì <see cref="MoveData"/> ở
    /// FA.Core dùng chung.
    /// </summary>
    [Serializable]
    public struct FollowUp
    {
        /// <summary>Nút để nối (vd L cho JJJ). Đọc edge qua CommandParser.WasPressedWithin.</summary>
        public InputButton button;

        /// <summary>Motion yêu cầu (None = không cần hướng — đa số target combo). Đặt vd QCF nếu muốn
        /// "đòn thường → special" bằng motion. Khớp qua CommandParser.TryMatchMotion (tương-đối-facing).</summary>
        public MotionInput motion;

        /// <summary>Always = nối tự do trong window; OnHit = đòn trước phải TRÚNG (block không tính).</summary>
        public FollowUpCondition condition;

        /// <summary>Đòn kế trong chuỗi. Null → resolver BỎ QUA an toàn (cổng no-regression).</summary>
        public MoveData nextMove;
    }
}
