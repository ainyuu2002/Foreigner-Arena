using UnityEngine;
using UnityEngine.Serialization;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// ENTRY-POINT combat (Phương án B, research §3.3) — MonoBehaviour TÁCH KHỎI CharacterMotor để
    /// FA.Combat KHÔNG reference FA.Movement (§1.3). Mỗi Update hỏi parser (qua ICombatContext, một
    /// interface ở Core) xem nút L/M/H vừa nhấn chưa; nếu được phép đánh → set MoveData hiện hành vào
    /// AttackState + HitboxController rồi ChangeState sang AttackState. Song song "Any State → Attack"
    /// của Motor, KHÔNG nằm trong Motor.Update.
    ///
    /// P6 — SỞ HỮU CHUNG các state combat: AttackState (P5) + HurtState + BlockState. Cùng module
    /// FA.Combat → HitReactionController/FacingController lấy state qua getter này (ref trực tiếp HỢP
    /// LỆ — §1.3 chỉ cấm CROSS-module). Expose cờ IsAttacking/IsHurt/IsBlocking/BlockIsLow cho
    /// FacingController gate auto-face.
    ///
    /// BLOCK = REACTIVE (kiểu SF, chốt khi test P6): giữ lùi KHÔNG tự vào BlockState ở đây (giữ lùi =
    /// đi lùi bình thường). Chỉ khi ĐỐI THỦ RA ĐÒN TRÚNG lúc victim đang giữ lùi (away) thì
    /// HitReactionController mới đẩy victim vào BlockState (Low nếu giữ thêm Down). Lý do: block chủ động
    /// (giữ-lùi-để-thủ) làm (1) nuốt đòn khi giữ-lùi+đánh và (2) chập chờn đi-lùi↔block. Vào HurtState
    /// cũng do HitReactionController kích (không ở đây).
    ///
    /// Map nút→MoveData: 3 ref nL/nM/nH gán Inspector (FA.Combat tự chứa). GATE đánh (P5): chỉ khi
    /// GROUNDED & state hiện tại là Idle/Walk → KHÔNG đánh khi đang đánh/dash/nhảy/hurt/block. Aerial = P7.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatController : MonoBehaviour
    {
        [Header("Tham chiếu (cùng prefab)")]
        [Tooltip("AnimationEventRelay cùng object Animator — AttackState subscribe RecoveryEnd/CancelWindow.")]
        [SerializeField] private AnimationEventRelay relay;

        [Tooltip("HitboxController cùng prefab — set MoveData hiện hành khi kích đòn (trước HitboxOn).")]
        [SerializeField] private HitboxController hitboxController;

        [Tooltip("(P16) OnKnockbackLaunch (GameObjectEventChannelSO) — AttackState RAISE tại Animation Event " +
                 "KnockbackPoint (frame hit cuối) khi KHÔNG nối → victim VĂNG pending. CÙNG asset gán vào " +
                 "KnockbackResolver (listen). Bỏ trống = đòn thường KHÔNG văng (chỉ spike/launcher văng).")]
        [FormerlySerializedAs("onComboContinues")]
        [SerializeField] private GameObjectEventChannelSO onKnockbackLaunch;

        [Header("Bảng đòn thường (button → MoveData). Thêm đòn = thêm asset (extensible).")]
        [Tooltip("Đòn nút L (Light).")] [SerializeField] private MoveData nL;
        [Tooltip("Đòn nút M (Medium).")] [SerializeField] private MoveData nM;
        [Tooltip("Đòn nút H (Heavy).")] [SerializeField] private MoveData nH;

        [Header("Hit Reaction (P6) — pushback nhẹ, full kb để P7")]
        [Tooltip("Lực đẩy ngang NHẸ khi vào HurtState (đơn vị/s). Full launch %-based = P7. CẦN TEST.")]
        [SerializeField] private float hurtPushback = 2.5f;

        [Tooltip("Số clip Hurt (variant biểu cảm trúng đòn). Mỗi lần ăn hit chọn NGẪU NHIÊN 1 trong N. " +
                 "Phải KHỚP số state Hurt_0..Hurt_(N-1) dựng trong Animator (nếu nhân vật chỉ 1 clip → để 1).")]
        [SerializeField] private int hurtVariantCount = 3;

        // ICombatContext lấy từ cùng prefab (CharacterMotor implement). Combat phụ thuộc Core interface.
        private ICombatContext _ctx;
        private AttackState _attackState;
        private HurtState _hurtState;
        private BlockState _blockState;

        private void Awake()
        {
            _ctx = GetComponent<ICombatContext>();
            if (relay == null) relay = GetComponentInChildren<AnimationEventRelay>();
            if (hitboxController == null) hitboxController = GetComponent<HitboxController>();

            if (_ctx == null)
            {
                Debug.LogError("[CombatController] Thiếu ICombatContext (CharacterMotor) trên prefab.", this);
                enabled = false;
                return;
            }
            // (P7) Resolver knockback cùng prefab (qua Core interface — KHÔNG ref class cứng). Null ở
            // prefab P5/P6 chưa gắn → HurtState giữ hành vi cũ. Truyền vào để HurtState không về Idle
            // sớm khi còn knockback chờ áp (neutral-gap).
            var pendingKnockback = GetComponent<IPendingKnockback>();
            // (P14) MoveMotionController cùng prefab (Core) — uỷ quyền vx cho đòn CÓ chuyển động. Null ở
            // prefab cũ → AttackState giữ hành vi đứng yên (no-regression).
            var motion = GetComponent<MoveMotionController>();
            // (P15) HitConfirmTracker cùng prefab (Core) — cho follow-up điều kiện OnHit (block không tính).
            // Null ở prefab cũ → follow-up OnHit luôn coi như chưa-trúng (an toàn — không nối bậy).
            var hitConfirm = GetComponent<HitConfirmTracker>();
            // (P16) Root attacker truyền vào AttackState để raise OnKnockbackLaunch (so HitData.Attacker bên victim).
            GameObject attackerRoot = transform.root.gameObject;
            _attackState = new AttackState(_ctx, relay, motion, hitboxController, hitConfirm,
                                           onKnockbackLaunch, attackerRoot);
            _hurtState = new HurtState(_ctx, hurtPushback, hurtVariantCount, pendingKnockback);
            _blockState = new BlockState(_ctx);
        }

        // ===== Getter state combat (cùng module — HitReaction/Facing dùng) =====
        public HurtState HurtState => _hurtState;
        public BlockState BlockState => _blockState;

        // ===== Cờ trạng thái (FacingController gate auto-face) =====
        public bool IsAttacking => ReferenceEquals(_ctx?.Machine.CurrentState, _attackState);
        public bool IsHurt => ReferenceEquals(_ctx?.Machine.CurrentState, _hurtState);
        public bool IsBlocking => ReferenceEquals(_ctx?.Machine.CurrentState, _blockState);
        public bool BlockIsLow => _blockState != null && _blockState.IsLow;

        private void Update()
        {
            if (_ctx == null || _ctx.Parser == null) return;

            // BLOCK reactive: KHÔNG vào block từ input ở đây (giữ lùi = đi lùi). HitReactionController
            // đẩy vào BlockState khi bị đòn trúng lúc đang giữ lùi (away). Frame này chỉ xét attack.
            if (!CanAttackNow()) return;

            // Ưu tiên H > M > L nếu nhấn nhiều nút cùng frame (đòn nặng ưu tiên — cảm giác fighting game).
            if (TryStart(InputButton.H, nH)) return;
            if (TryStart(InputButton.M, nM)) return;
            TryStart(InputButton.L, nL);
        }

        /// <summary>Chỉ đánh khi GROUNDED & đang ở Idle/Walk (so ref qua ctx — không ref class Movement).</summary>
        private bool CanAttackNow()
        {
            if (!_ctx.IsGrounded) return false;
            return IsGroundNeutral();
        }

        /// <summary>State hiện tại là Idle/Walk (ground-neutral) — nơi cho phép vào attack/block.</summary>
        private bool IsGroundNeutral()
        {
            IState s = _ctx.Machine.CurrentState;
            return ReferenceEquals(s, _ctx.IdleState()) || ReferenceEquals(s, _ctx.WalkState());
        }

        /// <summary>Nếu nút b vừa nhấn (edge trong window) & có MoveData → tiêu thụ edge + vào AttackState.</summary>
        private bool TryStart(InputButton b, MoveData move)
        {
            if (move == null) return false;
            int win = _ctx.EdgeWindow;
            if (!_ctx.Parser.WasPressedWithin(b, win)) return false;

            _ctx.Parser.ConsumePress(b, win); // 1 lần nhấn = 1 đòn (không re-trigger suốt window).

            // (P15) Set MoveData đòn ĐẦU chuỗi; AttackState.BeginMove (trong Enter) lo re-point HitboxController
            // (+ MoveMotionController + reset hit-confirm) → HitDetection đọc đúng tham số đòn trước HitboxOn.
            _attackState.SetMove(move);
            _ctx.Machine.ChangeState(_attackState);
            return true;
        }
    }
}
