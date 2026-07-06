using UnityEngine;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// GROUND BOUNCE (P7, research §3.3/§3.4) — victim đập đất tốc-độ-cao (đặc biệt SPIKE) → NẢY lên cho
    /// combo tiếp. <see cref="KnockbackCollisionRelay"/> phát hiện va sàn ≥ ngưỡng → ChangeState về đây.
    ///
    /// NẢY vật lý do <see cref="PhysicsMaterial2D"/> bounciness (relay BẬT material 'GroundBounce' lúc va,
    /// hấp thụ một phần GDD) — KHÔNG tự code phản xạ vector (§1.1). State này CHỈ đổi Animator + giữ tới
    /// khi rời đất lại (đã nảy lên) rồi trả về tumble để Rigidbody2D lo phần bay/rơi juggle.
    ///
    /// Literal Combat StateId = 18. Animator: state "GroundBounce" transition Any State theo StateId==18,
    /// Has Exit Time OFF, BỎ Can Transition To Self. KHÔNG zero velocity (để bounciness văng lên) — khác
    /// WallSplat (dán). Gate "đã rời đất" tránh kẹt frame 0 (relay vừa va = vẫn đang chạm).
    ///
    /// FAILSAFE chống kẹt (cùng lớp bug wall-splat-sát-đất của LauncherState): nếu cú nảy bị hấp thụ hết
    /// (bounciness thấp / prefab CHƯA gán bounceMaterial) → victim KHÔNG rời đất → _leftGround không bao
    /// giờ true → kẹt 18 vĩnh viễn. Sau groundedFailsafeFrames không rời được đất → trả về tumble
    /// (tumble có failsafe riêng sẽ đáp về Idle).
    /// </summary>
    public sealed class GroundBounceState : IState
    {
        public const int GroundBounceStateId = 18;

        private readonly ICombatContext _ctx;
        private readonly KnockbackConfigSO _config;
        private readonly LauncherState _tumble;

        private bool _leftGround;
        private int _stuckFrames; // failsafe: đếm frame vẫn dính đất sau va (nảy không nhấc nổi).

        public GroundBounceState(ICombatContext ctx, KnockbackConfigSO config, LauncherState tumble)
        {
            _ctx = ctx;
            _config = config;
            _tumble = tumble;
        }

        public void Enter()
        {
            _leftGround = false;
            _stuckFrames = 0;
            _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, GroundBounceStateId);
            _ctx.SetHitstun(true); // ground-bounce = đang bị juggle → Air Dodge bị chặn.
        }

        public void Tick(float dt) { }

        public void FixedTick(float dt)
        {
            // KHÔNG chạm velocity — PhysicsMaterial2D bounciness đã văng victim lên (Unity-native).
            // Chờ rời đất (đã nảy lên) rồi mới trả về tumble cho Rigidbody2D lo bay/rơi.
            if (!_ctx.IsGrounded) { _leftGround = true; }

            if (_leftGround)
            {
                _ctx.Machine.ChangeState(_tumble);
                return;
            }

            // FAILSAFE (xem doc class): nảy bị hấp thụ → không rời đất nổi → về tumble sau N frame.
            _stuckFrames++;
            int failsafe = _config != null ? _config.groundedFailsafeFrames : 8;
            if (_stuckFrames >= failsafe)
                _ctx.Machine.ChangeState(_tumble);
        }

        public void Exit()
        {
            // ChangeState(_tumble) sẽ set lại true ở Launcher.Enter (đồng bộ, không có frame xen giữa).
            _ctx.SetHitstun(false);
        }
    }
}
