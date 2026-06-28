namespace FA.Core
{
    /// <summary>
    /// Hợp đồng cho MỘT trạng thái nhân vật (§3.1). Mỗi state cụ thể = 1 file riêng
    /// (IdleState, WalkState, JumpState... ở các module phase tương ứng).
    ///
    /// Phân vai Tick vs FixedTick (xem research §3.1):
    /// - <see cref="Tick"/>  chạy trong Update: đọc input, quyết định ĐỔI state.
    /// - <see cref="FixedTick"/> chạy trong FixedUpdate: mọi tác động Rigidbody2D
    ///   (set velocity / AddForce) PHẢI nằm ở đây vì physics chạy theo fixed step.
    /// - <see cref="Enter"/>/<see cref="Exit"/>: set tham số Animator, reset cờ.
    ///
    /// State chỉ chứa LOGIC. Hình ảnh + thời điểm do Animator/Animation Event lo (§3.1).
    /// </summary>
    public interface IState
    {
        /// <summary>Gọi 1 lần khi vào state (set Animator StateId, reset cờ).</summary>
        void Enter();

        /// <summary>Gọi mỗi Update. Đọc input, quyết định chuyển state.</summary>
        void Tick(float dt);

        /// <summary>Gọi mỗi FixedUpdate. Nơi DUY NHẤT chạm Rigidbody2D.</summary>
        void FixedTick(float dt);

        /// <summary>Gọi 1 lần khi rời state (dọn cờ tạm).</summary>
        void Exit();
    }
}
