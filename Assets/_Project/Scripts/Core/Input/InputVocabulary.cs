namespace FA.Core
{
    /// <summary>
    /// Từ vựng (vocabulary) cho lớp Input Buffer + Command Parser (Phase 4, §3.2).
    /// Gom các enum nhỏ vào 1 file (mỗi enum &lt; vài dòng, cùng chủ đề "ngôn ngữ input")
    /// để tránh vỡ file vụn. KHÔNG chứa logic — chỉ định danh.
    ///
    /// Quy ước HƯỚNG TƯƠNG-ĐỐI-FACING: parser làm việc trên "forward/back" theo hướng nhìn,
    /// KHÔNG trái/phải tuyệt đối → special "thuận tay" cả 2 phía (research §3.2 cạm bẫy "Motion
    /// theo facing"). Caller truyền facing (±1) vào parser; parser tự normalize.
    /// </summary>

    /// <summary>
    /// Motion input nhận diện được (notation số-pad, tương-đối-facing).
    /// Phase 4 hiện thực: QCF/QCB/DPForward/DPBack. Charge khai báo KHUNG (dùng thật P9).
    /// Mở rộng dần = thêm giá trị mới, KHÔNG đổi giá trị cũ.
    /// </summary>
    public enum MotionInput
    {
        None = 0,
        QCF = 1,            // ↓ ↘ → (quarter-circle forward)
        QCB = 2,            // ↓ ↙ ← (quarter-circle back)
        DPForward = 3,      // → ↓ ↘ (dragon punch / Z-motion forward)
        DPBack = 4,         // ← ↓ ↙ (dragon punch back)
        ChargeBackForward = 5,  // KHUNG P9: giữ ← rồi →
        ChargeDownUp = 6,       // KHUNG P9: giữ ↓ rồi ↑
    }

    /// <summary>
    /// Kết quả phân biệt tap-vs-hold cho 1 hướng (research §3.2 — Né(tap ↓) vs Ngồi(hold ↓), GDD §4.1).
    /// None = không giữ hướng đó; Tap = nhả trong tapMaxHold; Hold = giữ vượt ngưỡng.
    /// </summary>
    public enum HoldKind
    {
        None = 0,
        Tap = 1,
        Hold = 2,
    }

    /// <summary>
    /// Hướng 8-chiều tương-đối-facing để parser truy vấn. Forward/Back = theo hướng nhìn
    /// (không trái/phải tuyệt đối). Dùng cho GetDirectionHold + nội bộ motion matching.
    /// </summary>
    public enum InputDirection
    {
        Neutral = 0,
        Forward = 1,
        Back = 2,
        Up = 3,
        Down = 4,
        UpForward = 5,
        UpBack = 6,
        DownForward = 7,
        DownBack = 8,
    }

    /// <summary>
    /// Nút có thể truy vấn edge qua buffer (WasPressedWithin). Trùng tên action trong
    /// FA_Controls.inputactions (đã bind Keyboard + Gamepad ở P0). Move là analog (không ở đây).
    /// </summary>
    public enum InputButton
    {
        Jump = 0,
        Dash = 1,
        L = 2,
        M = 3,
        H = 4,
        Super1 = 5,
        Super2 = 6,
        Rage = 7,
    }
}
