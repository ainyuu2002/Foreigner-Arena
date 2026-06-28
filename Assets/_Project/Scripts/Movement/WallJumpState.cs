using UnityEngine;
using FA.Core;

namespace FA.Movement
{
    /// <summary>
    /// Wall Jump (Phase 3) — bật KHỎI tường khi đang Wall Hang (spam Jump để LEO, GDD §5.1, KHÔNG
    /// giới hạn số lần khi còn bám). Sau cú bật, hành xử như TRÊN KHÔNG → kế thừa AirborneStateBase
    /// để tái dùng air-drift + double-jump + landing (research §3.6) — chỉ override Enter để set vector bật.
    ///
    /// - Enter: ResetGravity() (rời tường → gravity bình thường) + GÁN linearVelocity = vector chéo
    ///   lên-ra-xa-tường: x = -wallSide·wallJumpSpeedX (bật NGƯỢC phía tường), y = wallJumpSpeedY.
    ///   StateId=WallJump. wallSide lấy từ WallHangState đang rời (qua Ctx.WallSide hiện tại / fallback).
    /// - Cửa sổ control-lock NGẮN (wallJumpControlLock): ApplyAirDrift sẽ set vx theo input ngay FixedTick kế,
    ///   có thể triệt tiêu momentum bật ngang (research §4) → trong cửa sổ này, GIỮ vx cú bật. HẾT cửa sổ
    ///   này (và chưa Land/DoubleJump) → tự ChangeState sang AirState (rơi tự do thật, khớp Animator Fall,
    ///   §3.1) → CurrentState rời khỏi _wallJump đúng lúc, cho phép Motor re-grab WallHang để LEO BẬC.
    /// - Disarm RE-GRAB WallHang (Ctx.OnWallJumpStarted): Motor cấm bám lại tới khi nhân vật RỜI tường thật
    ///   (IsTouchingRaw==false) → không re-grab ngay giữa cú nhảy (triệt tiêu vận tốc bật + flip facing).
    ///
    /// KHÔNG tiêu airJumpsRemaining (double-jump độc lập). Mọi chạm Rigidbody2D ở Enter/FixedTick.
    /// </summary>
    public sealed class WallJumpState : AirborneStateBase
    {
        private float _controlLockTimer;

        public WallJumpState(ICharacterContext ctx) : base(ctx) { }

        protected override CharacterStateId DefaultStateId => CharacterStateId.WallJump;

        public override void Enter()
        {
            HasLeftGround = true; // wall-jump xảy ra khi ĐANG trên không → cho phép landing-gate đúng ngay.

            // Phía tường: dùng side ĐÃ CHỐT lúc đang bám (WallHang.WallSide qua Ctx). KHÔNG đọc lại
            // WallCheck/Facing để tránh fallback Facing sai sau khi SetFacing đảo. Đọc MỘT LẦN trước SetFacing.
            int wallSide = Ctx.WallHangSide;

            Ctx.ResetGravity(); // rời tường → gravity baseline (đề phòng nếu WallHang từng đổi gravity).

            Rigidbody2D rb = Ctx.Body;
            rb.linearVelocity = new Vector2(-wallSide * Ctx.Data.wallJumpSpeedX, Ctx.Data.wallJumpSpeedY);

            // Lật nhìn theo hướng bật ra (đi ra khỏi tường) — đúng "flip theo hướng đi" P1–P5.
            Ctx.SetFacing(-wallSide);

            // Báo Motor disarm re-grab WallHang tới khi cú nhảy thực sự RỜI tường (IsTouchingRaw==false).
            Ctx.OnWallJumpStarted();

            Ctx.RefreshAirDash(); // wall-jump cũng là một cú nhảy → nạp lại +1 air-dash.

            _controlLockTimer = Ctx.Data.wallJumpControlLock;
            SetStateId(CharacterStateId.WallJump);
        }

        public override void FixedTick(float dt)
        {
            // Trong cửa sổ control-lock: GIỮ momentum bật ngang (KHÔNG để ApplyAirDrift triệt tiêu).
            if (_controlLockTimer > 0f)
            {
                _controlLockTimer -= dt;
                return;
            }
            // Hết cửa sổ giữ momentum & chưa Land/DoubleJump (Tick của base xử lý 2 cái đó trước) →
            // chuyển sang RƠI TỰ DO thật (AirState) để CurrentState rời _wallJump → khớp Animator Fall
            // (§3.1) + cho phép re-grab WallHang leo bậc tiếp. AirState.FixedTick tự lo air-drift.
            Ctx.Machine.ChangeState(Ctx.AirState());
        }
    }
}
