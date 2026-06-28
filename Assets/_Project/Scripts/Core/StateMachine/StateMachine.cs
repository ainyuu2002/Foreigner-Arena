namespace FA.Core
{
    /// <summary>
    /// Bộ máy trạng thái THUẦN (KHÔNG MonoBehaviour — §3.1). Chỉ giữ state hiện tại
    /// và điều phối chuyển state; TUYỆT ĐỐI không chứa logic gameplay (không biết "walk"
    /// là gì). Một MonoBehaviour bên ngoài (vd CharacterMotor) sở hữu instance này và
    /// forward Tick/FixedTick xuống.
    ///
    /// Thêm state mới ở phase sau = new thêm 1 IState rồi ChangeState — không sửa lớp này.
    /// </summary>
    public sealed class StateMachine
    {
        /// <summary>State đang chạy. Null trước lần ChangeState đầu tiên.</summary>
        public IState CurrentState { get; private set; }

        /// <summary>
        /// Chuyển sang state mới: Exit() state cũ → gán mới → Enter() mới.
        /// Bỏ qua nếu next null hoặc trùng state hiện tại (tránh self-transition lặp Enter/Exit).
        /// </summary>
        public void ChangeState(IState next)
        {
            if (next == null || ReferenceEquals(next, CurrentState))
                return;

            CurrentState?.Exit();
            CurrentState = next;
            CurrentState.Enter();
        }

        /// <summary>Forward Update xuống state hiện tại.</summary>
        public void Tick(float dt) => CurrentState?.Tick(dt);

        /// <summary>Forward FixedUpdate xuống state hiện tại.</summary>
        public void FixedTick(float dt) => CurrentState?.FixedTick(dt);
    }
}
