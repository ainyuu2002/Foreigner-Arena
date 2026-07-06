using UnityEngine;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// NƠI DUY NHẤT quyết HURT vs BLOCK (P6, research §3.5) — thay <see cref="HitReceiverDebug"/> (P5).
    /// MonoBehaviour trên VICTIM, subscribe OnHitLanded (kênh va-chạm-thô P5). Khi mình là Victim:
    ///  - Đang BlockState & loại khớp (block-High đỡ HitsHigh / block-Low đỡ HitsLow) → BLOCK: giảm
    ///    dmg×mult + pushback NHẸ ra-xa (đẩy dần ra rìa), Ở LẠI BlockState, raise OnHitBlocked (tùy),
    ///    KHÔNG combo.
    ///  - Ngược lại (không block / sai loại) → HURT: HurtState.SetHit(hit) (hurtstun từ
    ///    HitData.HitstunFrames) + ChangeState(Hurt) (re-hit khi đang Hurt → refresh frames, combo nối)
    ///    + raise OnHitConfirmed → ComboCounter/Knockback(P7)/Mana(P8) nghe.
    ///
    /// Tách 2 kênh (research §3.4): OnHitLanded = "va chạm vật lý thô" (HitstopController cũng nghe);
    /// OnHitConfirmed = "trúng THẬT không-block" (ComboCounter nghe → block KHÔNG tăng combo). Đặt ở
    /// FA.Combat, chỉ ref Core (HitData/HitEventChannelSO) + cùng-module CombatController (state combat).
    /// In debug damage/total/hitstun ra console (giữ tiện ích test của HitReceiverDebug cũ).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitReactionController : MonoBehaviour
    {
        [Header("Kênh sự kiện")]
        [Tooltip("OnHitLanded (HitEventChannelSO) — CÙNG asset HitboxAuthoring raise. Subscribe để nhận.")]
        [SerializeField] private HitEventChannelSO onHitLanded;

        [Tooltip("OnHitConfirmed (HitEventChannelSO mới) — raise khi TRÚNG THẬT không-block. " +
                 "ComboCounter/Knockback(P7)/Mana(P8) nghe.")]
        [SerializeField] private HitEventChannelSO onHitConfirmed;

        [Tooltip("(Tùy) OnHitBlocked (HitEventChannelSO) — raise khi ĐỠ ĐÚNG loại. Cho mana-block-ít P8/UI.")]
        [SerializeField] private HitEventChannelSO onHitBlocked;

        [Header("Tham chiếu")]
        [Tooltip("Root để so 'mình có phải victim không'. Bỏ trống = lấy transform.root.")]
        [SerializeField] private Transform root;

        [Tooltip("CombatController cùng prefab — lấy HurtState/BlockState + cờ block. Bỏ trống = lấy trên object.")]
        [SerializeField] private CombatController combat;

        [Header("Block (P6) — REACTIVE, giá trị test, đưa SO/MoveData sau nếu cần balance")]
        [Tooltip("Hệ số dmg KHI BLOCK đúng loại (0.25 = ăn 25%). Giảm KHÔNG triệt tiêu (chip). CẦN TEST.")]
        [SerializeField] private float blockDamageMult = 0.25f;

        [Tooltip("Lực đẩy ngang NHẸ ra-xa-địch mỗi hit khi block (đẩy dần ra rìa). CẦN TEST.")]
        [SerializeField] private float blockPushback = 2.0f;

        [Tooltip("Số frame BLOCKSTUN khi đỡ trúng (victim đứng thủ rồi mới cử động lại được). CẦN TEST.")]
        [SerializeField] private int blockstunFrames = 12;

        [Tooltip("Deadzone trục NGANG để nhận 'đang giữ lùi (away)'. CẦN TEST.")]
        [SerializeField] private float backDeadzone = 0.3f;

        [Tooltip("Deadzone trục DỌC để nhận 'đang giữ Down' → block-Low. CẦN TEST.")]
        [SerializeField] private float blockLowDeadzone = 0.5f;

        private ICombatContext _ctx;
        // (P10 ADD) Buff context của CHÍNH MÌNH (victim) — Rage Mode giảm hurtstun nhận (null-safe ×1.0).
        private IBuffContext _selfBuff;
        // % cộng dồn nay do KnockbackPercent (P7) quản lý thật (subscribe OnHitConfirmed/OnHitBlocked) —
        // KHÔNG còn _accumulatedPercent debug ở đây (research §4 cạm bẫy 4: tránh cộng đôi với thanh % thật).

        private void Awake()
        {
            if (root == null) root = transform.root;
            if (combat == null) combat = GetComponent<CombatController>();
            _ctx = GetComponent<ICombatContext>();
            _selfBuff = GetComponent<IBuffContext>(); // P10: null nếu prefab chưa có RageModeController.
            if (_ctx == null)
                Debug.LogError("[HitReactionController] Thiếu ICombatContext (CharacterMotor).", this);
        }

        private void OnEnable()
        {
            if (onHitLanded != null) onHitLanded.OnEventRaised += OnHitLanded;
        }

        private void OnDisable()
        {
            if (onHitLanded != null) onHitLanded.OnEventRaised -= OnHitLanded;
        }

        private void OnHitLanded(HitData hit)
        {
            GameObject me = root != null ? root.gameObject : gameObject;
            if (!ReferenceEquals(hit.Victim, me)) return;
            if (_ctx == null || combat == null) return;

            // 1) BLOCK reactive: chỉ đỡ khi — TẠI THỜI ĐIỂM TRÚNG — victim đang GIỮ LÙI (away khỏi địch)
            //    + ĐANG Ở TRẠNG THÁI CÓ THỂ THỦ (đứng đất + Idle/Walk/đang-Block; KHÔNG đỡ được khi đang
            //    hurt/attack/trên không → combo không bị "block lậu"). Low = giữ thêm Down. Loại phải khớp
            //    HitsHigh/HitsLow. Giữ lùi away = dấu MoveX trùng FacingSign của đòn (FacingSign = hướng
            //    từ attacker → victim, cũng là hướng victim cần ép để lùi).
            // (P10) Ultimate phá block hoàn toàn: role=BlockBreak → BỎ nhánh block, LUÔN Hurt (research §3.5).
            if (hit.Role != MoveRole.BlockBreak &&
                CanGuardNow() && IsHoldingBackAway(hit) && GuardTypeMatches(hit, out bool guardLow))
            {
                // Pushback ra-xa-địch giao BlockState áp PENDING sau hitstop (set thẳng velocity ở đây
                // bị hitstop zero-velocity nuốt tùy thứ tự subscriber — không deterministic).
                combat.BlockState.SetBlock(guardLow, blockstunFrames, hit.FacingSign * blockPushback);
                _ctx.Machine.ChangeState(combat.BlockState);
                SnapAnimatorNow(); // hiện pose Block NGAY frame trúng (trước khi hitstop freeze "nhìn thấy").

                float blocked = hit.DamagePercent * blockDamageMult;

                Debug.Log($"[BLOCK] {hit.Attacker.name} → {me.name}  " +
                          $"chip +{blocked:0.#}% (full {hit.DamagePercent:0.#}%)  " +
                          $"type={(guardLow ? "Low" : "High")}", me);

                if (onHitBlocked != null) onHitBlocked.RaiseEvent(hit);
                return; // KHÔNG vào Hurt, KHÔNG raise Confirmed → combo không tăng.
            }

            // 2) Không giữ lùi / sai loại / không thủ được → HURT (full dmg). SetHit TRƯỚC ChangeState →
            //    re-hit khi đang Hurt: ChangeState no-op (self) nhưng frames đã refresh trong SetHit → combo nối.
            //    % thật do KnockbackPercent (P7) cộng qua OnHitConfirmed — KHÔNG cộng ở đây nữa.
            // (P10) Rage Mode giảm HURTSTUN NHẬN (HitstunTakenMult): dựng bản HitData scaled cho SetHit khi
            //    mult ≠ 1 (null-safe; KHÔNG sửa HurtState). % và knockback raise NGUYÊN qua onHitConfirmed.
            HitData hurtHit = ScaleHurtstun(hit);
            combat.HurtState.SetHit(hurtHit);
            _ctx.Machine.ChangeState(combat.HurtState);
            SnapAnimatorNow(); // hiện pose Hurt_k NGAY frame trúng — cả re-hit đổi variant giữa hitstop.

            Debug.Log($"[HIT] {hit.Attacker.name} → {me.name}  +{hit.DamagePercent:0.#}%  " +
                      $"hitstun={hit.HitstunFrames}f  hitstop={hit.HitstopFrames}f  facing={hit.FacingSign}", me);

            // "Trúng thật không-block" → ComboCounter/Knockback(P7)/Mana(P8) nghe (tách khỏi OnHitLanded).
            if (onHitConfirmed != null) onHitConfirmed.RaiseEvent(hit);
        }

        /// <summary>(Fix mượt combat) Ép Animator EVALUATE NGAY trong frame trúng đòn. Vấn đề: Animator
        /// chỉ xét transition trong update của nó, mà HitstopController freeze (speed=0) CÙNG frame →
        /// StateId/HurtVariant vừa set nhưng transition KHÔNG bắn → victim đứng nguyên pose TRƯỚC-trúng
        /// suốt hitstop, hết freeze mới hiện Hurt (nhìn như "hurt chạy trễ"); hit chồng trong freeze thì
        /// variant giữa bị nuốt. Snap = mở speed tạm + Update(0) → transition duration-0 ăn liền, victim
        /// đóng băng Ở pose Hurt/Block frame 0 (chuẩn fighting game). Trả lại speed cũ → hitstop giữ
        /// nguyên bất kể thứ tự subscriber (freeze trước hay sau handler này đều đúng).</summary>
        private void SnapAnimatorNow()
        {
            Animator anim = _ctx.Animator;
            if (anim == null) return;
            float saved = anim.speed;
            anim.speed = 1f;
            anim.Update(0f);
            anim.speed = saved;
        }

        /// <summary>(P10) Nhân hurtstun nhận theo Rage Mode (HitstunTakenMult). Mult=1 (không buff/null) →
        /// trả NGUYÊN hit (không cấp phát). Mult≠1 → dựng HitData mới chỉ đổi HitstunFrames (giữ % để
        /// onHitConfirmed cộng % ĐẦY ĐỦ). KHÔNG sửa HurtState (ADD tối thiểu, research §3.4).</summary>
        private HitData ScaleHurtstun(in HitData hit)
        {
            float mult = _selfBuff != null ? _selfBuff.HitstunTakenMult : 1f;
            if (Mathf.Approximately(mult, 1f)) return hit;
            int scaled = Mathf.Max(1, Mathf.RoundToInt(hit.HitstunFrames * mult));
            return new HitData(
                hit.Attacker, hit.Victim, hit.DamagePercent, hit.LaunchAngle, hit.BaseKnockback,
                hit.KnockbackGrowth, hit.HitstopFrames, hit.FacingSign, hit.IsSpike, hit.CausesLaunch,
                hit.HitsLow, hit.HitsHigh, scaled, hit.ContactPoint, hit.Role);
        }

        /// <summary>Chỉ thủ được khi đứng đất + đang Idle/Walk hoặc đang Block (đỡ tiếp). KHÔNG thủ được
        /// khi đang hurt/attack/trên không → combo không bị block lậu (không "guard out of hitstun").</summary>
        private bool CanGuardNow()
        {
            if (!_ctx.IsGrounded) return false;
            IState s = _ctx.Machine.CurrentState;
            return ReferenceEquals(s, _ctx.IdleState())
                || ReferenceEquals(s, _ctx.WalkState())
                || combat.IsBlocking;
        }

        /// <summary>Đang ÉP input lùi (away khỏi địch) tại frame trúng. FacingSign = hướng attacker→victim
        /// = hướng victim phải ép để lùi → dấu MoveX trùng FacingSign nghĩa là đang giữ lùi.</summary>
        private bool IsHoldingBackAway(in HitData hit)
        {
            if (hit.FacingSign == 0 || Mathf.Abs(_ctx.MoveX) < backDeadzone) return false;
            return (_ctx.MoveX > 0f ? 1 : -1) == hit.FacingSign;
        }

        /// <summary>Block-Low (giữ Down) đỡ đòn HitsLow; block-High (không Down) đỡ đòn HitsHigh. Đòn cả
        /// high+low → loại nào cũng đỡ. Trả guardLow để set stance + log.</summary>
        private bool GuardTypeMatches(in HitData hit, out bool guardLow)
        {
            guardLow = _ctx.MoveY < -blockLowDeadzone;
            return guardLow ? hit.HitsLow : hit.HitsHigh;
        }
    }
}
