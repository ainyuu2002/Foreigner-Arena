using UnityEngine;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// WALL SPLAT (P7, research §3.3/§3.4) — victim bay tốc-độ-cao đập tường → DÁN tường ngắn (stun)
    /// rồi RƠI tiếp (juggle). <see cref="KnockbackCollisionRelay"/> phát hiện va tường ≥ ngưỡng tốc độ
    /// → ChangeState về đây. Đập tường ở đây = "dán" (zero velocity + stun), KHÔNG để bounciness văng đi
    /// (relay tắt material lúc splat) — khác wall-bounce; lai SFV wall-bounce.
    ///
    /// Literal Combat StateId = 17. ĐẾM stun bằng fixed-step HỢP LỆ (thuộc tính đòn/va, không phải
    /// frame-data clip riêng-từng-đòn — cùng lý do HurtState đếm được, research §3 verify). Hết stun →
    /// về tumble (LauncherState) để Rigidbody2D + gravity lo rơi xuống juggle tiếp.
    ///
    /// Animator: state "WallSplat" transition Any State theo StateId==17, Has Exit Time OFF, BỎ Can
    /// Transition To Self. (Tùy chọn: Animation Event RecoveryEnd thay đếm frame — research §3 ghi cả 2;
    /// chọn đếm fixed-step vì 1 clip dùng chung mọi đòn, không đặt event riêng-đòn được.)
    /// </summary>
    public sealed class WallSplatState : IState
    {
        public const int WallSplatStateId = 17;

        private readonly ICombatContext _ctx;
        private readonly KnockbackConfigSO _config;
        private readonly LauncherState _tumble;

        private int _stunLeft;

        public WallSplatState(ICombatContext ctx, KnockbackConfigSO config, LauncherState tumble)
        {
            _ctx = ctx;
            _config = config;
            _tumble = tumble;
        }

        public void Enter()
        {
            _stunLeft = _config != null ? Mathf.Max(1, _config.wallSplatStunFrames) : 18;
            _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, WallSplatStateId);
            _ctx.SetHitstun(true); // wall-splat = đang bị juggle → Air Dodge bị chặn.
        }

        public void Tick(float dt)
        {
            // NUỐT input — dán tường, không hành động.
        }

        public void FixedTick(float dt)
        {
            // Dán: ghì velocity ≈ 0 trong stun (không trượt/rơi khi đang splat). Gravity vẫn kéo nhưng
            // ta zero lại mỗi step → "dính tường". Hết stun mới buông cho rơi.
            _ctx.Body.linearVelocity = Vector2.zero;

            if (_stunLeft > 0) _stunLeft--;
            if (_stunLeft > 0) return;

            // Hết dán → về tumble: gravity + Rigidbody2D lo rơi xuống, mở juggle tiếp.
            _ctx.Machine.ChangeState(_tumble);
        }

        public void Exit()
        {
            // ChangeState(_tumble) sẽ set lại true ở Launcher.Enter (đồng bộ, không có frame xen giữa).
            _ctx.SetHitstun(false);
        }
    }
}
