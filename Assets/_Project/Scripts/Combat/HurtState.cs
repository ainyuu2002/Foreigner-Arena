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
    /// giật lại, combo nối liền. Hitstop (P5) freeze SONG SONG nhưng đồng hồ hurtstun ĐỨNG YÊN trong
    /// hitstop (gate IsFrozen ở FixedTick): nếu vẫn đếm thì hurt chỉ hiển thị hitstun−hitstop frame
    /// (vừa vào đã thoát → giật) và pushback bị hitstop zero-velocity nuốt. Hitstun chạy SAU hitstop
    /// (chuẩn fighting game — attacker cũng freeze cùng lượng nên frame advantage không đổi).
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

        // Số clip Hurt variant (≥1). Mỗi hit chọn 1 variant KHÁC variant đang hiển thị (xem PickNextVariant).
        private readonly int _variantCount;

        // (P7) Resolver knockback cùng prefab — hỏi "còn knockback chờ áp?" để KHÔNG về Idle quá sớm.
        // Null ở prefab P5/P6 chưa có KnockbackResolver → giữ hành vi cũ (hết hurtstun là về Idle/Walk).
        private readonly IPendingKnockback _pendingKnockback;

        // Hitstop cùng prefab (cùng module Combat) — hỏi IsFrozen để ĐỨNG đồng hồ hurtstun + hoãn
        // pushback trong hitstop. Null (prefab/test thiếu) → đếm liên tục như cũ (no-regression).
        private readonly HitstopController _hitstop;

        private int _framesLeft;     // số FixedUpdate hurtstun còn lại (đếm hợp lệ — đòn địch).
        private int _facingSign;     // hướng đẩy của đòn (= Facing attacker lúc trúng).
        private bool _pushPending;   // áp pushback 1 lần ở FixedTick kế (đảm bảo trong physics step).

        public HurtState(ICombatContext ctx, float hurtPushback, int variantCount = 1,
                         IPendingKnockback pendingKnockback = null, HitstopController hitstop = null)
        {
            _ctx = ctx;
            _hurtPushback = hurtPushback;
            _variantCount = Mathf.Max(1, variantCount);
            _pendingKnockback = pendingKnockback;
            _hitstop = hitstop;
        }

        /// <summary>HitReactionController gọi TRƯỚC ChangeState(Hurt). Nếu ĐANG Hurt (re-hit combo) →
        /// chỉ refresh frames + pushback (ChangeState sẽ no-op vì self) → combo nối, không giật clip.</summary>
        public void SetHit(in HitData hit)
        {
            _framesLeft = Mathf.Max(1, hit.HitstunFrames);
            _facingSign = hit.FacingSign != 0 ? hit.FacingSign : _facingSign;
            _pushPending = true;

            // Chọn variant KHÁC variant đang hiển thị (random-exclude-current, cosmetic — không ảnh
            // hưởng logic/hitbox nên Random không phá determinism gameplay). BẮT BUỘC khác: re-hit khi
            // đang Hurt (combo / đòn multi-hit) mà bốc trùng variant ⇒ cả StateId lẫn HurtVariant đều
            // không đổi ⇒ KHÔNG transition nào bắn (Can Transition To Self OFF) ⇒ clip không restart —
            // nhìn như "trúng đòn không có animation". Đổi variant mỗi hit ⇒ transition chéo Hurt_a→Hurt_b
            // LUÔN bắn, mỗi hit (kể cả từng HitboxOn window trong 1 đòn multi-hit) thấy 1 clip Hurt khác.
            // count=1 ⇒ luôn 0 (khớp Hurt_0; re-hit không đổi clip — cần ≥2 variant để thấy đổi).
            // Set TRƯỚC khi Enter set StateId=11 → Animator lọc đúng state Hurt_k.
            _ctx.Animator.SetInteger(CoreAnimatorParams.HurtVariant, PickNextVariant());
        }

        /// <summary>Variant kế: random trong [0, count) TRỪ variant đang set trên Animator. Đọc param
        /// hiện tại từ Animator (nguồn chân lý hình ảnh) thay vì cache — an toàn khi param bị đổi ngoài
        /// state này. Param ngoài [0, count) (vd giảm count trong Inspector) → random tự do (re-sync).</summary>
        private int PickNextVariant()
        {
            if (_variantCount <= 1) return 0;
            int current = _ctx.Animator.GetInteger(CoreAnimatorParams.HurtVariant);
            if (current < 0 || current >= _variantCount) return Random.Range(0, _variantCount);
            int next = Random.Range(0, _variantCount - 1); // bốc trong N-1 giá trị ≠ current
            return next >= current ? next + 1 : next;      // dồn khoảng [current..] lên 1 để né current
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
            // ĐỨNG YÊN trong hitstop: không đếm hurtstun, không áp pushback (hitstop đang zero-velocity
            // mỗi fixed-step — áp bây giờ là bị nuốt). Hết freeze mới chạy → hurt hiển thị ĐỦ hitstun.
            if (_hitstop != null && _hitstop.IsFrozen) return;

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

            // (P7/P16 neutral-gap fix) Nếu resolver còn knockback CHỜ áp trên victim này → Ở LẠI Hurt,
            // KHÔNG về Idle (resolver sẽ bốc vào tumble). P16 bỏ release-timer 26f: pending áp tại
            // hitstop+1 (thường < hurtstun) ⇒ neutral-gap gần như biến mất → ổn hơn P7. Khi attacker nối
            // (CancelPending) → HasPendingKnockback=false → về neutral như mọi hit-trong-combo. Resolver
            // null (prefab cũ) → bỏ qua nhánh này.
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
