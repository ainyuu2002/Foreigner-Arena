using UnityEngine;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// BLOCK stance (P6, §5) — đỡ đòn theo High/Low (SF directional block). 1 STATE class + cờ _low
    /// (giống AttackState 1-class-nhiều-data) → set Animator StateId khác (BlockHigh=12 / BlockLow=13).
    /// Đặt ở FA.Combat, dùng <see cref="ICombatContext"/> (Core) — KHÔNG ref Movement.
    ///
    /// REACTIVE (KHÁC AttackState, chốt khi test P6): KHÔNG vào block bằng cách giữ-lùi. Victim chỉ vào
    /// BlockState khi BỊ ĐÒN TRÚNG lúc đang giữ lùi — HitReactionController gọi <see cref="SetBlock"/>
    /// (loại High/Low + số frame BLOCKSTUN) rồi ChangeState(Block). Block tự thoát sau khi hết blockstun
    /// (đếm fixed-step, HỢP LỆ — như hurtstun, không phải frame-data đòn mình). Đỡ tiếp khi đang blockstun
    /// (giữ lùi) → SetBlock refresh frames (ChangeState no-op self) → blockstun nối, không giật clip.
    ///
    /// Block giảm dmg + giảm knockback (KHÔNG triệt tiêu), FREE (không mana/nộ — P8). Quyết "đỡ đúng
    /// loại hay không" + áp giảm dmg/pushback = HitReactionController (nơi DUY NHẤT biết hit-vs-block).
    /// BlockState chỉ giữ STANCE + loại (High/Low) + đếm blockstun.
    /// </summary>
    public sealed class BlockState : IState
    {
        private const int BlockHighStateId = 12;
        private const int BlockLowStateId = 13;
        private const float MoveDeadzone = 0.1f;

        private readonly ICombatContext _ctx;

        private bool _low;        // true = block-Low (Back+Down), false = block-High (Back).
        private int _framesLeft;  // số FixedUpdate blockstun còn lại (đếm hợp lệ — phản ứng bị đòn).

        public BlockState(ICombatContext ctx) => _ctx = ctx;

        /// <summary>Loại block hiện hành — HitReactionController đọc để khớp HitsHigh/HitsLow.</summary>
        public bool IsLow => _low;

        /// <summary>HitReactionController gọi TRƯỚC ChangeState(Block) khi đỡ trúng. Nếu ĐANG block
        /// (đỡ tiếp) → refresh blockstun + cập nhật cao↔thấp (ChangeState sẽ no-op self → không giật).</summary>
        public void SetBlock(bool low, int blockstunFrames)
        {
            bool already = ReferenceEquals(_ctx.Machine.CurrentState, this);
            _framesLeft = Mathf.Max(1, blockstunFrames);
            if (_low != low)
            {
                _low = low;
                if (already) ApplyStateId();   // đổi stance cao↔thấp khi đang block.
            }
        }

        public void Enter()
        {
            ApplyStateId();
        }

        public void Tick(float dt) { }   // thoát theo blockstun ở FixedTick (deterministic, khớp physics).

        public void FixedTick(float dt)
        {
            // Đứng thủ: ghì vx≈0 khi đứng đất (giữ vy cho gravity). Pushback khi đỡ trúng do
            // HitReactionController áp ở frame trúng → frame sau mới ghì lại → vẫn lùi dần ra rìa.
            if (_ctx.IsGrounded)
            {
                Vector2 v = _ctx.Body.linearVelocity;
                v.x = 0f;
                _ctx.Body.linearVelocity = v;
            }

            if (_framesLeft > 0) _framesLeft--;
            if (_framesLeft > 0) return;

            // Hết blockstun → về Idle/Walk theo ý định di chuyển (đứng đất). Nếu vẫn giữ lùi: thành đi lùi
            // bình thường; đòn kế trúng lúc giữ lùi sẽ lại vào block (reactive).
            bool moving = Mathf.Abs(_ctx.MoveX) > MoveDeadzone;
            _ctx.Machine.ChangeState(moving ? _ctx.WalkState() : _ctx.IdleState());
        }

        public void Exit() { }

        private void ApplyStateId()
        {
            // Cầu nối code↔Animator (§3.1) — transition Any State → BlockHigh/BlockLow trong cửa sổ Animator.
            _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, _low ? BlockLowStateId : BlockHighStateId);
        }
    }
}
