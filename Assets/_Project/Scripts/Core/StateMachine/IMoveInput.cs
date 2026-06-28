namespace FA.Core
{
    /// <summary>
    /// Nguồn "ý định di chuyển" mà state đọc, TÁCH KHỎI nguồn input cụ thể.
    /// Phase 1: CharacterMotor implement bằng FA_Controls (Input System) trực tiếp.
    /// Phase 4: đổi sang đọc qua InputBuffer/CommandParser.
    /// Phase 12: đổi sang PlayerInput pairing 2 người chơi.
    /// Mọi lần đổi nguồn input chỉ thay IMPLEMENT — state đọc qua interface này nên BẤT BIẾN.
    ///
    /// Phase 1 khai MoveX. Phase 2 THÊM (đúng vai "bổ sung field cho phase sau"):
    /// JumpPressed (edge), MoveY (trục dọc — dùng ở phase sau: ngồi/block-low,
    /// dodge ↓+Super, motion input).
    /// Field mới = chỉ THÊM, KHÔNG đổi MoveX → state P1 không hồi quy.
    /// </summary>
    public interface IMoveInput
    {
        /// <summary>Trục ngang [-1..1]: âm = trái, dương = phải, 0 = đứng yên.</summary>
        float MoveX { get; }

        /// <summary>Trục dọc [-1..1]: âm = xuống (↓ ngồi/block-low P6 / dodge P10 / nhặt item P13), dương = lên.</summary>
        float MoveY { get; }

        /// <summary>
        /// True ĐÚNG 1 frame khi vừa NHẤN Jump (edge-triggered). Dùng cho cả ground jump &amp;
        /// double jump → 1 lần nhấn = 1 cú nhảy. P4 đổi nguồn (Command Parser) — interface bất biến.
        /// </summary>
        bool JumpPressed { get; }

        /// <summary>
        /// True ĐÚNG 1 frame khi vừa NHẤN Dash (edge-triggered, Phase 3). 1 nút Dash riêng — KHÔNG
        /// phải Dodge (né là tổ hợp ↓+Super / Dash+né, do Command Parser nhận ở P4/P10).
        /// Edge → spam được nhưng mỗi lần nhấn tiêu 1 lần (giữ nút KHÔNG dash liên tục ngoài ý muốn).
        /// Field mới = chỉ THÊM, KHÔNG đổi field cũ → state P1/P2 không hồi quy.
        /// </summary>
        bool DashPressed { get; }
    }
}
