namespace FA.Core
{
    /// <summary>
    /// (P12) Định danh 2 chỗ ngồi người chơi trong 1 trận. Đặt ở Core để FA.GameFlow (logic) và FA.UI
    /// (màn hình) cùng nói chung "ngôn ngữ" mà KHÔNG reference chéo (§1.3). Mọi kênh per-player (combo,
    /// lives, score) và struct kết quả tham chiếu slot này thay vì "P1/P2" rải rác trong code.
    ///
    /// CHỈ 2 slot (scope nộp môn 1v1). Mở rộng 4 người = thêm enum value + tăng mảng — KHÔNG sửa logic.
    /// </summary>
    public enum PlayerSlot
    {
        P1 = 0,
        P2 = 1
    }
}
