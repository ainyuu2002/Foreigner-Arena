using UnityEngine;
using FA.Core;

namespace FA.Specials
{
    /// <summary>
    /// BURST BREAKER (StateId 29, P10, GG Burst) — dùng KHI ĐANG BỊ COMBO/juggle để THOÁT HIỂM. Tiêu 50%
    /// nộ flat (DefenseController trừ TRƯỚC). i-frame DÀI = clip burst đặt HurtboxOff đầu, HurtboxOn cuối
    /// (đủ dài che phản đòn — research §3.1; KHÔNG đo "hitstun địch" bằng code). KHÔNG gây dmg.
    ///
    /// ĐẨY ĐỊCH RA XA (research §3 / §4 cạm bẫy 3): tái dùng HỆ HIT P5/P7 Unity-native — burst dùng 1
    /// HITBOX riêng với <see cref="MoveData"/> baseDamagePercent=0 + knockback lớn (set qua HitboxController
    /// như special reversal P9, bật bằng Animation Event HitboxOn trên clip burst). KnockbackResolver của
    /// ĐỊCH (P7) đọc HitData → bốc địch bay → cắt combo địch. KHÔNG code overlap tay (§1.1).
    ///
    /// HỦY pending knockback của CHÍNH MÌNH: Enter gọi IKnockbackCanceler.CancelPending() (null-safe) →
    /// không bị bay theo cú đánh cuối của combo vừa thoát.
    ///
    /// FAIL-SAFE Exit() relay.HurtboxOn(). DefenseController cho phép burst OVERRIDE HurtState (gate riêng).
    /// Đặt FA.Specials, ICombatContext (Core) — KHÔNG ref FA.Combat/Resources (§1.3).
    /// </summary>
    public sealed class BurstState : IState
    {
        private const float MoveDeadzone = 0.1f;

        private readonly ICombatContext _ctx;
        private readonly AnimationEventRelay _relay;
        private readonly IKnockbackCanceler _selfKnockbackCanceler; // null ở prefab chưa có resolver.

        private bool _recoveryDone;

        public BurstState(ICombatContext ctx, AnimationEventRelay relay,
                          IKnockbackCanceler selfKnockbackCanceler = null)
        {
            _ctx = ctx;
            _relay = relay;
            _selfKnockbackCanceler = selfKnockbackCanceler;
        }

        public void Enter()
        {
            _recoveryDone = false;
            _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, DefenseStateIds.Burst);
            if (_relay != null) _relay.RecoveryEndEvent += OnRecoveryEnd;

            // Thoát combo: hủy cú knockback đang chờ áp trên mình (không bị bay theo cú cuối, research §4.3).
            _selfKnockbackCanceler?.CancelPending();

            // Dừng quán tính bị-combo (đứng vững tại chỗ khi bung burst). Gravity giữ nếu trên không.
            Rigidbody2D rb = _ctx.Body;
            Vector2 v = rb.linearVelocity;
            v.x = 0f;
            rb.linearVelocity = v;
        }

        public void Tick(float dt)
        {
            if (!_recoveryDone) return;
            if (_ctx.IsGrounded)
            {
                bool moving = Mathf.Abs(_ctx.MoveX) > MoveDeadzone;
                _ctx.Machine.ChangeState(moving ? _ctx.WalkState() : _ctx.IdleState());
                return;
            }
            // Trên không (burst khi juggle): set clip Fall (cầu nối Animator) → về Idle, Motor tiếp quản AirState.
            _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, DefenseStateIds.Fall);
            _ctx.Machine.ChangeState(_ctx.IdleState());
        }

        public void FixedTick(float dt)
        {
            // Ghì vx≈0 mặt đất (đứng burst). Trên không giữ momentum tối thiểu (gravity do Rigidbody2D).
            if (_ctx.IsGrounded)
            {
                Rigidbody2D rb = _ctx.Body;
                Vector2 v = rb.linearVelocity;
                v.x = 0f;
                rb.linearVelocity = v;
            }
        }

        public void Exit()
        {
            if (_relay != null)
            {
                _relay.RecoveryEndEvent -= OnRecoveryEnd;
                _relay.HurtboxOn(); // fail-safe i-frame off.
            }
        }

        private void OnRecoveryEnd() => _recoveryDone = true;
    }
}
