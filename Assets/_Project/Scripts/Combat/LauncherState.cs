using UnityEngine;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// TUMBLE / bay-không-điều-khiển (P7, research §3.3) — victim vào đây SAU cú knockback cuối combo
    /// (KnockbackResolver set linearVelocity rồi ChangeState về đây). NUỐT input thường, CHỈ vật lý lo
    /// việc bay: <see cref="Rigidbody2D"/> + gravity kéo xuống (KHÔNG tự tích phân §1.1). Hết tốc độ +
    /// chạm đất → về Idle/Walk.
    ///
    /// Literal Combat StateId = 16 (Hurt=11, Block 12/13 đã chiếm — air-reaction dời lên 16/17/18 để
    /// KHÔNG trùng BlockHigh=12). KHÔNG đụng enum CharacterStateId (Movement, cross-module §1.3).
    /// Animator: state "Launch/Tumble" transition Any State theo StateId==16, Has Exit Time OFF, BỎ Can
    /// Transition To Self (memory gotcha).
    ///
    /// LANDING-GATE (research §4 cạm bẫy 11): set <c>_hasLeftGround</c> khi rời đất ≥1 frame; chỉ cho
    /// landing-check sau khi đã rời đất → tránh "đáp ghost" ngay frame vào (knockback bốc lên rồi mới
    /// cho đáp). Wall-splat / ground-bounce do <see cref="KnockbackCollisionRelay"/> đổi state (không ở
    /// đây — state này chỉ lo tumble + về-ground).
    ///
    /// FAILSAFE chống kẹt (fix bug wall-splat sát đất): vào tumble khi ĐÃ grounded + velocity đã bị zero
    /// (WallSplat vừa ghì) / knockback trượt ngang không bốc lên nổi → KHÔNG BAO GIỜ rời đất → gate trên
    /// chặn landing VĨNH VIỄN (victim kẹt 16, nuốt input mãi). Nếu chưa-từng-rời-đất mà đứng trên đất với
    /// tốc độ ≤ tumbleEndSpeed đủ groundedFailsafeFrames liên tục → coi như đã đáp, về Idle. KHÔNG phá
    /// gate ghost-landing: launch thật có speed lớn ở các frame đầu (resolver vừa set velocity) → counter
    /// reset, chưa kịp chạm ngưỡng đã rời đất.
    /// </summary>
    public sealed class LauncherState : IState
    {
        public const int TumbleStateId = 16;

        private readonly ICombatContext _ctx;
        private readonly KnockbackConfigSO _config;

        private bool _hasLeftGround;
        private int _groundedStuckFrames; // failsafe: đếm frame "trên đất + hết đà" khi chưa từng rời đất.

        public LauncherState(ICombatContext ctx, KnockbackConfigSO config)
        {
            _ctx = ctx;
            _config = config;
        }

        public void Enter()
        {
            _hasLeftGround = false;
            _groundedStuckFrames = 0;
            // Cầu nối code↔Animator (§3.1): set StateId → transition Tumble cấu hình TRONG Animator.
            _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, TumbleStateId);
            _ctx.SetHitstun(true); // tumble = đang bị juggle → Air Dodge bị chặn (chỉ Burst thoát).
        }

        public void Tick(float dt)
        {
            // NUỐT input thường (không cho đánh/dash/jump trong tumble). DI đã áp 1 lần ở Resolver lúc
            // set velocity (research §3.5) → state KHÔNG đọc lại input. Chuyển state quyết ở FixedTick.
        }

        public void FixedTick(float dt)
        {
            bool grounded = _ctx.IsGrounded;

            // Landing-gate: phải rời đất ≥1 frame rồi mới cho landing-check (tránh đáp ghost).
            if (!grounded) { _hasLeftGround = true; return; }

            float speed = _ctx.Body.linearVelocity.magnitude;
            float endSpeed = _config != null ? _config.tumbleEndSpeed : 1.5f;

            if (!_hasLeftGround)
            {
                // FAILSAFE (xem doc class): chưa từng rời đất — còn đà (trượt ngang/sắp bốc lên) thì chờ;
                // hết đà đủ N frame liên tục → không bao giờ bốc lên nổi nữa → coi như đã đáp.
                if (speed > endSpeed) { _groundedStuckFrames = 0; return; }
                _groundedStuckFrames++;
                int failsafe = _config != null ? _config.groundedFailsafeFrames : 8;
                if (_groundedStuckFrames < failsafe) return;
            }
            else if (speed > endSpeed)
            {
                return; // còn trượt/đà → ở lại tumble (Rigidbody2D ma sát giảm dần).
            }

            _ctx.Machine.ChangeState(_ctx.IdleState());
        }

        public void Exit()
        {
            _ctx.SetHitstun(false); // thoát tumble về ground → mở lại Air Dodge.
        }
    }
}
