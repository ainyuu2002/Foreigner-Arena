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

        // Hitstop cùng prefab (cùng module) — ĐỨNG đồng hồ blockstun + hoãn pushback trong freeze
        // (đối xứng HurtState). Null → đếm liên tục như cũ (no-regression).
        private readonly HitstopController _hitstop;

        private bool _low;         // true = block-Low (Back+Down), false = block-High (Back).
        private int _framesLeft;   // số FixedUpdate blockstun còn lại (đếm hợp lệ — phản ứng bị đòn).
        private float _pushVx;     // pushback ra-xa-địch chờ áp (đẩy dần ra rìa — GDD).
        private bool _pushPending; // áp 1 lần ở FixedTick kế SAU hitstop (không thì bị zero-velocity nuốt).

        public BlockState(ICombatContext ctx, HitstopController hitstop = null)
        {
            _ctx = ctx;
            _hitstop = hitstop;
        }

        /// <summary>Loại block hiện hành — HitReactionController đọc để khớp HitsHigh/HitsLow.</summary>
        public bool IsLow => _low;

        /// <summary>HitReactionController gọi TRƯỚC ChangeState(Block) khi đỡ trúng. Nếu ĐANG block
        /// (đỡ tiếp) → refresh blockstun + cập nhật cao↔thấp (ChangeState sẽ no-op self → không giật).
        /// pushbackVx = vận tốc ngang ra-xa-địch (đã nhân dấu FacingSign) — áp PENDING ở FixedTick sau
        /// hitstop, KHÔNG set thẳng velocity tại frame trúng (hitstop zero-velocity sẽ nuốt, và kết quả
        /// phụ thuộc thứ tự subscriber OnHitLanded — không deterministic).</summary>
        public void SetBlock(bool low, int blockstunFrames, float pushbackVx = 0f)
        {
            bool already = ReferenceEquals(_ctx.Machine.CurrentState, this);
            _framesLeft = Mathf.Max(1, blockstunFrames);
            if (pushbackVx != 0f)
            {
                _pushVx = pushbackVx;
                _pushPending = true;
            }
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
            // ĐỨNG YÊN trong hitstop (đối xứng HurtState): không đếm blockstun, không áp/ghì velocity.
            if (_hitstop != null && _hitstop.IsFrozen) return;

            if (_pushPending)
            {
                // Pushback ra-xa-địch 1 lần SAU hitstop (đẩy dần ra rìa — GDD). Frame sau ghì lại vx=0.
                _pushPending = false;
                Vector2 pv = _ctx.Body.linearVelocity;
                pv.x = _pushVx;
                _ctx.Body.linearVelocity = pv;
            }
            else if (_ctx.IsGrounded)
            {
                // Đứng thủ: ghì vx≈0 khi đứng đất (giữ vy cho gravity).
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
