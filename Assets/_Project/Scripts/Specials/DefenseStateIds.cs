namespace FA.Specials
{
    /// <summary>
    /// Animator StateId của 6 state phòng thủ/comeback P10 (research §6 "StateId allocation"). LITERAL
    /// dải 26–31 — tiếp sau Movement (0–7), Attack (8–10), Hurt/Block (11–13), Launcher/Splat/Bounce
    /// (16–18), Special/Super data-driven (19–25). Đặt 1 chỗ để tránh trùng + tiện tra Animator.
    ///
    /// Cầu nối code↔Animator (§3.1): state Enter set <c>StateId = &lt;hằng&gt;</c> → transition
    /// Any State → clip cấu hình TRONG cửa sổ Animator (Has Exit Time OFF, bỏ "Can Transition To Self").
    /// KHÔNG Animator.Play. (Ultimate là MoveData data-driven NHƯNG cần state riêng cho speed-cố-định +
    /// IsUltimate flag → cấp StateId 31 rõ ràng, research §6 note.)
    /// </summary>
    public static class DefenseStateIds
    {
        public const int GroundDodge = 26;
        public const int AirDodge = 27;
        public const int RCDash = 28;
        public const int Burst = 29;
        public const int RageMode = 30;
        public const int Ultimate = 31;

        /// <summary>StateId Fall (3) của Movement — dùng khi né/ultimate kết thúc TRÊN KHÔNG (cầu nối
        /// Animator, literal vì cross-module §1.3). Motor frame kế tự tiếp quản sang AirState.</summary>
        public const int Fall = 3;
    }
}
