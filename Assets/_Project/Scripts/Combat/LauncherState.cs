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
        private int _lyingFrames;         // trần nằm-trượt: đếm frame chạm đất SAU khi đã đáp.
        private int _airBlipFrames;       // đếm chuỗi frame rời đất — phân biệt "bay thật" vs blip trượt.
        private int _totalFrames;         // TỔNG frame trong tumble (cả trên không) — safety-net hard-timeout.

        // Trượt tốc độ cao trên băng làm IsTouchingLayers CHỚP false 1-2 frame (qua mép tile/va li ti)
        // → nếu reset _lyingFrames mỗi blip thì trần nằm KHÔNG BAO GIỜ đầy (bug "xuôi gió không dậy
        // nổi"). Chỉ coi là BAY THẬT (juggle/re-launch → reset đồng hồ nằm) khi rời đất đủ lâu.
        private const int AirBlipToleranceFrames = 6;

        public LauncherState(ICombatContext ctx, KnockbackConfigSO config)
        {
            _ctx = ctx;
            _config = config;
        }

        public void Enter()
        {
            _hasLeftGround = false;
            _groundedStuckFrames = 0;
            _lyingFrames = 0;
            _airBlipFrames = 0;
            _totalFrames = 0;
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
            // SAFETY-NET tuyệt đối (fix kẹt nảy-vô-hạn): đếm TỔNG frame trong tumble kể cả trên không.
            // Vượt trần cấu hình → thoát về Idle DÙ CHƯA grounded. Chặn ca "không bao giờ chạm đất": rơi
            // trúng nấm bouncy + material FighterBounce → nảy mãi, hoặc collider lơ lửng (nhân vật WIP).
            // Ngưỡng đặt rất rộng (StageConfig ~300 = 5s) nên juggle thật (thoát qua grounded sớm hơn
            // nhiều) KHÔNG bao giờ chạm — không đổi cảm giác chơi; chỉ là lưới cuối chống kẹt vĩnh viễn.
            int hardCap = _config != null ? _config.tumbleHardTimeoutFrames : 300;
            if (hardCap > 0 && ++_totalFrames >= hardCap)
            {
                _ctx.Machine.ChangeState(_ctx.IdleState());
                return;
            }

            bool grounded = _ctx.IsGrounded;

            // Landing-gate: phải rời đất ≥1 frame rồi mới cho landing-check (tránh đáp ghost).
            // Đồng hồ nằm CHỈ reset khi bay thật (rời đất > AirBlipToleranceFrames) — blip trượt bỏ qua.
            if (!grounded)
            {
                _hasLeftGround = true;
                if (++_airBlipFrames > AirBlipToleranceFrames) _lyingFrames = 0;
                return;
            }
            _airBlipFrames = 0;

            float speed = _ctx.Body.linearVelocity.magnitude;
            float endSpeed = _config != null ? _config.tumbleEndSpeed : 1.5f;

            // TRẦN NẰM-TRƯỢT (fix map băng): sàn không ma sát / gió bơm vx → speed có thể KHÔNG BAO GIỜ
            // tụt dưới tumbleEndSpeed → mọi nhánh "chờ hết đà" dưới đây kẹt vĩnh viễn (trượt khỏi map
            // chưa dậy). Đếm frame chạm đất liên tục Ở MỌI NHÁNH (kể cả chưa-từng-rời-đất — bị đánh
            // ngay đầu trận, cú nhấc quá thấp); vượt trần → dậy dù còn đà (Idle/Walk sẽ ghì vx).
            int maxLying = _config != null ? _config.maxLyingFrames : 45;
            _lyingFrames++;
            bool overLyingCap = maxLying > 0 && _lyingFrames >= maxLying;

            // BẬT DẬY SỚM (feedback test): nằm đủ earlyWakeUpFrames → ấn di chuyển là dậy ngay,
            // victim chủ động cứu mình khỏi trượt (băng/gió) thay vì chờ hết đà.
            int earlyWake = _config != null ? _config.earlyWakeUpFrames : 10;
            if (earlyWake > 0 && _lyingFrames >= earlyWake && Mathf.Abs(_ctx.MoveX) > 0.1f)
                overLyingCap = true;

            if (!_hasLeftGround)
            {
                // FAILSAFE (xem doc class): chưa từng rời đất — còn đà (trượt ngang/sắp bốc lên) thì chờ;
                // hết đà đủ N frame liên tục → không bao giờ bốc lên nổi nữa → coi như đã đáp.
                if (speed > endSpeed)
                {
                    _groundedStuckFrames = 0;
                    if (!overLyingCap) return;
                }
                else
                {
                    _groundedStuckFrames++;
                    int failsafe = _config != null ? _config.groundedFailsafeFrames : 8;
                    if (_groundedStuckFrames < failsafe && !overLyingCap) return;
                }
            }
            else if (speed > endSpeed && !overLyingCap)
            {
                return; // còn trượt/đà trong hạn mức → ở lại tumble (ma sát giảm dần).
            }

            _ctx.Machine.ChangeState(_ctx.IdleState());
        }

        public void Exit()
        {
            _ctx.SetHitstun(false); // thoát tumble về ground → mở lại Air Dodge.
        }
    }
}
