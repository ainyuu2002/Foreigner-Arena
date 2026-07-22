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
        // --- A2: Ngồi (pose-only). Lấy 41 (8-40 đã dùng rải bởi Combat/Specials — xem memory
        //         fighter-stateid-map; KHÔNG tái dùng số cũ). ---
        Crouch = 41,     // ngồi thuần (giữ Xuống), pose-only — KHÔNG ra đòn; block-low giữ nguyên
        // --- MỤC D: Intro/kết trận (pose-only, KHÔNG ra đòn). Điều khiển bởi orchestrator FA.Stage
        //         (RoundIntroController/MatchOutroController) qua Animator "StateId" (literal 42-44 vì
        //         FA.Stage chỉ ref Core, không ref FA.Movement). 42-44 TRỐNG trong toàn dải (memory
        //         fighter-stateid-map). ---
        Intro = 42,      // diễn intro vào trận (act191) — RecoveryEnd cuối clip báo "xong"
        Win = 43,        // pose thắng (act181) — giữ frame cuối
        Lose = 44,       // pose thua (act171) — giữ frame cuối, KHÔNG fade
        Draw = 45,       // pose hoà (dùng chung Lose.anim) — MỜ DẦN rồi biến mất (PoseEffectController)
        // Chừa chỗ cho phase sau (đừng tái dùng số cũ): Attack_L/M/H, Hurt, Block, ...
    }
}
