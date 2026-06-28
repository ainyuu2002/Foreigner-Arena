namespace FA.Movement
{
    /// <summary>
    /// Định danh state dùng làm CẦU NỐI DUY NHẤT giữa code-state và Animator (§3.1).
    /// Mỗi state khi Enter() set Animator int "StateId" = giá trị này; transition trong
    /// CỬA SỔ Animator dựa trên điều kiện StateId == n (Has Exit Time OFF). KHÔNG Animator.Play.
    ///
    /// Giá trị enum = số dùng trong Animator condition → giữ ổn định, KHÔNG đổi số cũ.
    /// Thêm state ở phase sau = thêm 1 giá trị mới (không sửa giá trị có sẵn).
    /// </summary>
    public enum CharacterStateId
    {
        Idle = 0,
        Walk = 1,
        // --- Phase 2: nhảy/rơi (giữ đúng số đã chừa, KHÔNG đổi Idle/Walk) ---
        Jump = 2,        // cú nhảy đầu từ đất (đang lên)
        Fall = 3,        // rơi tự nhiên trên không
        DoubleJump = 4,  // cú nhảy thứ 2 trên không
        // --- Phase 3: dash + tường. Số 5 trống (FastFall đã bỏ) → lấp liên tục, KHÔNG đổi số cũ ---
        Dash = 5,        // lướt ngang (mặt đất + trên không), 1 nút, spam được
        WallHang = 6,    // bám tường (dash vào tường khi trên không), trượt chậm
        WallJump = 7,    // bật khỏi tường (vector chéo lên-ra), spam để leo
        // Chừa chỗ cho phase sau (đừng tái dùng số cũ): Crouch, Attack_L/M/H, Hurt, Block, ...
    }
}
