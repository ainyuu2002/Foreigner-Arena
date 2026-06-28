using UnityEngine;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// HITSTUN state (P6, §5) — victim NUỐT input suốt thời lượng hurtstun của đòn vừa trúng, hết →
    /// về Idle/Walk. Đặt ở FA.Combat, chỉ dùng <see cref="ICombatContext"/> (Core) — KHÔNG ref Movement.
    ///
    /// QUY TẮC VÀNG (§1.1): ĐẾM frame Ở ĐÂY HỢP LỆ. Quy tắc vàng chỉ cấm đếm startup/active/recovery
    /// của ĐÒN MÌNH RA (= frame-data đòn tấn công, phải qua Animation Event). Hurtstun là thuộc tính
    /// của ĐÒN ĐỊCH (đọc <see cref="HitData.HitstunFrames"/> qua SetHit) — 1 clip Hurt dùng chung mọi
    /// đòn nên KHÔNG thể đặt RecoveryEnd riêng-từng-đòn trên clip. Đếm fixed-step → deterministic
    /// (cùng lý do Hitstop/Dash đếm timer được, research §3.3 verify).
    ///
    /// Re-hit trong hurtstun (COMBO): HitReactionController gọi lại <see cref="SetHit"/> → REFRESH
    /// _framesLeft mà KHÔNG Exit/Enter cứng (StateMachine.ChangeState bỏ-qua-self) → clip Hurt không
    /// giật lại, combo nối liền. Hitstop (P5) freeze SONG SONG: HurtState đếm bằng số FixedUpdate,
    /// nhưng HitstopController set body.linearVelocity=0 mỗi fixed-step trong hitstop nên pushback chỉ
    /// "ăn" sau khi hitstop hết — chấp nhận (hitstop 4–8f ⊂ hurtstun 12–20f, sai số nhỏ — CẦN TEST).
    ///
    /// P6 = pushback NHẸ ngang theo FacingSign (cảm giác va). Full knockback %-based (launch/juggle/
    /// tumble/bounce/DI) = P7 KnockbackResolver (subscribe OnHitConfirmed; HitData mang sẵn).
    /// </summary>
    public sealed class HurtState : IState
    {
        private const float MoveDeadzone = 0.1f;

        // Animator StateId cho clip Hurt (nối tiếp Attack_*=8/9/10; research §3.6). Literal trong
        // Combat — KHÔNG đụng enum CharacterStateId của Movement (cross-module).
        private const int HurtStateId = 11;

        private readonly ICombatContext _ctx;

        // Lực đẩy ngang NHẸ khi vào hurt (đơn vị/s). Hằng test — full kb để P7. CẦN TEST.
        private readonly float _hurtPushback;

        // Số clip Hurt variant (≥1). Mỗi lần ăn hit chọn ngẫu nhiên 1 → set param HurtVariant cho Animator.
        private readonly int _variantCount;

        // (P7) Resolver knockback cùng prefab — hỏi "còn knockback chờ áp?" để KHÔNG về Idle quá sớm.
        // Null ở prefab P5/P6 chưa có KnockbackResolver → giữ hành vi cũ (hết hurtstun là về Idle/Walk).
        private readonly IPendingKnockback _pendingKnockback;

        private int _framesLeft;     // số FixedUpdate hurtstun còn lại (đếm hợp lệ — đòn địch).
        private int _facingSign;     // hướng đẩy của đòn (= Facing attacker lúc trúng).
        private bool _pushPending;   // áp pushback 1 lần ở FixedTick kế (đảm bảo trong physics step).

        public HurtState(ICombatContext ctx, float hurtPushback, int variantCount = 1,
                         IPendingKnockback pendingKnockback = null)
        {
            _ctx = ctx;
            _hurtPushback = hurtPushback;
            _variantCount = Mathf.Max(1, variantCount);
            _pendingKnockback = pendingKnockback;
        }

        /// <summary>HitReactionController gọi TRƯỚC ChangeState(Hurt). Nếu ĐANG Hurt (re-hit combo) →
        /// chỉ refresh frames + pushback (ChangeState sẽ no-op vì self) → combo nối, không giật clip.</summary>
        public void SetHit(in HitData hit)
        {
            _framesLeft = Mathf.Max(1, hit.HitstunFrames);
            _facingSign = hit.FacingSign != 0 ? hit.FacingSign : _facingSign;
            _pushPending = true;

            // Chọn NGẪU NHIÊN 1 trong N clip Hurt mỗi lần ăn hit (cosmetic — không ảnh hưởng logic/hitbox,
            // nên Random không phá determinism gameplay). LUÔN set trong [0, count) — count=1 ⇒ luôn 0
            // (khớp Hurt_0) nên đổi count xuống 1 vẫn play; param không kẹt ở giá trị cũ không có state khớp.
            // Set TRƯỚC khi Enter set StateId=11 → Animator lọc đúng state Hurt_k. Re-hit (combo): đổi
            // variant → transition chéo sang clip mới; trùng variant → giữ (Can Transition To Self OFF).
            _ctx.Animator.SetInteger(CoreAnimatorParams.HurtVariant, Random.Range(0, _variantCount));
        }

        public void Enter()
        {
            // Cầu nối code↔Animator (§3.1): set StateId → transition Any State → Hurt cấu hình TRONG
            // cửa sổ Animator. KHÔNG Animator.Play. (Re-hit khi đang Hurt: Enter KHÔNG chạy lại — self.)
            _ctx.Animator.SetInteger(CoreAnimatorParams.StateId, HurtStateId);
            _ctx.SetHitstun(true); // báo "đang bị đánh" → Air Dodge bị chặn (chỉ Burst thoát juggle).
        }

        public void Tick(float dt)
        {
            // NUỐT input: KHÔNG chuyển state cho tới khi hết hurtstun. Chuyển state quyết ở FixedTick
            // (đếm theo fixed-step để khớp Hitstop/physics, deterministic).
        }

        public void FixedTick(float dt)
        {
            // Pushback nhẹ 1 lần khi mới trúng (đẩy ra-xa theo hướng đòn). Full kb %-based = P7.
            if (_pushPending)
            {
                _pushPending = false;
                if (_facingSign != 0)
                {
                    Vector2 v = _ctx.Body.linearVelocity;
                    v.x = _facingSign * _hurtPushback;
                    _ctx.Body.linearVelocity = v;
                }
            }

            if (_framesLeft > 0) _framesLeft--;
            if (_framesLeft > 0) return;

            // (P7 neutral-gap fix) hurtstun (≤20f) < knockbackReleaseFrames (26f): nếu resolver còn
            // knockback CHỜ áp trên victim này → Ở LẠI Hurt, KHÔNG về Idle (resolver sẽ bốc vào tumble).
            // Tránh victim về neutral ~6 frame rồi mới bị bốc bay. Prefab không có resolver (null) → bỏ qua.
            if (_pendingKnockback != null && _pendingKnockback.HasPendingKnockback) return;

            // Hết hurtstun → về Idle/Walk theo ý định di chuyển hiện tại (đứng đất). Trên không vẫn
            // về ground-state (P6 không có hurt-air riêng; tumble/air-knockback = P7).
            bool moving = Mathf.Abs(_ctx.MoveX) > MoveDeadzone;
            _ctx.Machine.ChangeState(moving ? _ctx.WalkState() : _ctx.IdleState());
        }

        public void Exit()
        {
            _ctx.SetHitstun(false); // hết hurt → mở lại Air Dodge cho air-neutral.
        }
    }
}
