using UnityEngine;
using FA.Core;

namespace FA.Movement
{
    /// <summary>
    /// Wall Hang (Phase 3) — BÁM tường sau khi dash vào tường lúc trên không (GDD §5.1).
    /// Trượt xuống CHẬM hơn rơi tự do: mỗi FixedTick SET THẲNG vy = -wallSlideSpeed (tốc độ trượt cố
    /// định, deterministic — không chỉ clamp cận dưới, vì ép vx vào tường tăng friction sẽ giữ vy≈0
    /// gây "đứng im") + ép NHẸ vx vào tường (vx = WallSide * wallStickSpeed) để collider áp sát,
    /// tránh khe hở chớp-mất-contact gây flicker WallHang⇄Air. Dùng property Rigidbody2D, KHÔNG tự tích phân.
    ///
    /// - Tick: JumpPressed (edge) → WallJump (spam để leo); chạm đất (IsGrounded) → Idle/Walk;
    ///   rời tường (!IsWalled) hoặc đẩy input RA khỏi tường → Air. Ưu tiên Ground trước Wall.
    /// - Exit: ResetGravity() BẮT BUỘC (an toàn nếu sau này đổi sang cách giảm-gravity — tránh bug
    ///   'rơi chậm mãi' như fast-fall P2). Đây là bug số 1 của state bám tường (research §4).
    ///
    /// KHÔNG kế thừa AirborneStateBase: hành vi dính-tường + trượt-cố-định khác hẳn rơi tự do của base.
    /// KHÔNG đụng airJumpsRemaining (wall-jump là cú nhảy riêng, double-jump độc lập — research §3.4).
    /// </summary>
    public sealed class WallHangState : IState
    {
        private const float MoveDeadzone = 0.1f;

        private readonly ICharacterContext _ctx;
        private int _wallSide; // phía tường lúc bám (+1 phải, -1 trái) — WallJump bật ra -wallSide

        public WallHangState(ICharacterContext ctx) => _ctx = ctx;

        /// <summary>Phía tường đã chốt lúc bám — WallJumpState đọc để bật ra đúng phía.</summary>
        public int WallSide => _wallSide;

        public void Enter()
        {
            // Chốt phía tường lúc bám (từ WallCheck; fallback theo Facing nếu chưa xác định).
            _wallSide = _ctx.WallSide != 0 ? _ctx.WallSide : _ctx.Facing;
            _ctx.Animator.SetInteger(MovementAnimatorParams.StateId, (int)CharacterStateId.WallHang);
        }

        public void Tick(float dt)
        {
            // Đáp đất khi đang bám sát góc tường-sàn → ưu tiên đứng.
            if (_ctx.IsGrounded)
            {
                bool moving = Mathf.Abs(_ctx.Input.MoveX) > MoveDeadzone;
                _ctx.Machine.ChangeState(moving ? _ctx.WalkState() : _ctx.IdleState());
                return;
            }

            // Spam Jump khi bám → bật khỏi tường (leo dần). Edge-triggered.
            if (_ctx.Input.JumpPressed)
            {
                _ctx.ConsumeJumpEdge(); // P4: tiêu thụ edge buffer → 1 nhấn = 1 wall-jump (spam cần nhấn lại)
                _ctx.Machine.ChangeState(_ctx.WallJumpState());
                return;
            }

            // Rời tường: không còn chạm, hoặc người chơi đẩy input RA xa tường → buông về Air.
            bool pushedAway = Mathf.Abs(_ctx.Input.MoveX) > MoveDeadzone &&
                              (int)Mathf.Sign(_ctx.Input.MoveX) == -_wallSide;
            if (!_ctx.IsWalled || pushedAway)
            {
                _ctx.Machine.ChangeState(_ctx.AirState());
                return;
            }
        }

        public void FixedTick(float dt)
        {
            // ÉP NHẸ vào tường (vx = WallSide * wallStickSpeed) thay vì vx=0: CapsuleCollider (cạnh đứng
            // bo tròn) với vx=0 dễ tạo khe hở siêu nhỏ → IsTouchingLayers(Wall) chớp false 1 frame →
            // flicker WallHang⇄Air. Áp sát tường giữ bám ỔN ĐỊNH. WallSide +1 = tường bên phải → vx dương.
            //
            // Trượt xuống: SET THẲNG vy = -wallSlideSpeed mỗi FixedTick (không chỉ clamp cận dưới).
            // Lý do: ép vx vào tường làm tăng normal force → friction collider giữ vy≈0 → "đứng im".
            // Set thẳng cho trượt CHẬM CỐ ĐỊNH, deterministic, không phụ thuộc friction (vẫn chậm hơn
            // rơi tự do vì wallSlideSpeed nhỏ). Dùng property Rigidbody2D, KHÔNG tự tích phân.
            Rigidbody2D rb = _ctx.Body;
            float vx = _wallSide * _ctx.Data.wallStickSpeed;
            rb.linearVelocity = new Vector2(vx, -_ctx.Data.wallSlideSpeed);
        }

        public void Exit()
        {
            // BẮT BUỘC: trả gravity về baseline (an toàn nếu đổi sang cách giảm-gravity). Bug số 1.
            _ctx.ResetGravity();
        }
    }
}
