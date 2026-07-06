using UnityEngine;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// CHỦ SỞ HỮU các air-reaction state P7 (LauncherState/WallSplatState/GroundBounceState) — đối xứng
    /// CombatController sở hữu AttackState/HurtState/BlockState (research §3.3). MonoBehaviour per-fighter,
    /// dựng state với <see cref="ICombatContext"/> (Core) → KHÔNG ref Movement (§1.3). KnockbackResolver /
    /// KnockbackCollisionRelay (cùng module) gọi các entry method này để chuyển victim vào tumble/splat/bounce.
    ///
    /// QUẢN LÝ PhysicsMaterial2D bounce (research §3.4): SWAP material lên CapsuleCollider2D nhân vật khi
    /// vào tumble (để nảy tường/sàn), GỠ về material thường khi về ground (tránh nảy lúc đi/nhảy bình
    /// thường — hồi quy P1–P3, research §4 cạm bẫy 7). Nảy vật lý do Rigidbody2D + material (Unity-native),
    /// KHÔNG tự code phản xạ vector (§1.1).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KnockbackController : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("KnockbackConfigSO — hằng tumble/splat/bounce. BẮT BUỘC gán.")]
        [SerializeField] private KnockbackConfigSO config;

        [Header("Tham chiếu (cùng prefab)")]
        [Tooltip("CapsuleCollider2D THÂN nhân vật — swap PhysicsMaterial2D bounce khi tumble. " +
                 "Bỏ trống = lấy CapsuleCollider2D đầu tiên trên/ trong object.")]
        [SerializeField] private Collider2D bodyCollider;

        [Header("PhysicsMaterial2D (gán asset tay)")]
        [Tooltip("Material BOUNCE gán lên thân khi đang tumble (bounciness wall/ground). Bỏ trống = không nảy.")]
        [SerializeField] private PhysicsMaterial2D bounceMaterial;

        [Header("Đối thủ (gán chéo tay trong scene — như FacingController.opponent)")]
        [Tooltip("Collider2D THÂN đối thủ (kéo root đối thủ vào — capsule nằm trên root). Khi MÌNH đang " +
                 "tumble/splat/bounce → IgnoreCollision với thân địch: xác bay XUYÊN người (không bị thân " +
                 "attacker chặn cú knockback, không xô attacker, không nảy vào người). Null = giữ pushbox.")]
        [SerializeField] private Collider2D opponentBodyCollider;

        // ICombatContext từ cùng prefab (CharacterMotor implement). Phụ thuộc Core interface.
        private ICombatContext _ctx;

        // CombatController cùng prefab (CÙNG module — ref trực tiếp hợp lệ §1.3): hỏi IsHurt để giữ
        // material NẢY cả khi đang Hurt giữa combo — quyết định người dùng 2026-07-02: đòn giữa chuỗi
        // đẩy địch vào tường → địch BẬT RA (bounciness), không bị ép dính tường.
        private CombatController _combat;

        private LauncherState _tumble;
        private WallSplatState _wallSplat;
        private GroundBounceState _groundBounce;

        private PhysicsMaterial2D _defaultMaterial;   // material thường (gỡ bounce khi về ground).
        private bool _ignoringOpponent;               // cờ pushbox đang tắt (đổi mới gọi IgnoreCollision).

        public KnockbackConfigSO Config => config;

        /// <summary>Cấp collider thân đối thủ runtime (P12 spawner/scene setup) — như FacingController.SetOpponent.</summary>
        public void SetOpponentCollider(Collider2D c)
        {
            if (_ignoringOpponent) SetIgnoreOpponent(false); // gỡ ignore trên đối thủ CŨ trước khi đổi.
            opponentBodyCollider = c;
        }

        private void Awake()
        {
            _ctx = GetComponent<ICombatContext>();
            _combat = GetComponent<CombatController>(); // null-safe (prefab thiếu → chỉ nảy khi tumble như cũ).
            if (bodyCollider == null) bodyCollider = GetComponentInChildren<CapsuleCollider2D>();
            if (config == null)
                Debug.LogError("[KnockbackController] Thiếu KnockbackConfigSO.", this);
            if (_ctx == null)
            {
                Debug.LogError("[KnockbackController] Thiếu ICombatContext (CharacterMotor).", this);
                enabled = false;
                return;
            }

            if (bodyCollider != null) _defaultMaterial = bodyCollider.sharedMaterial;

            _tumble = new LauncherState(_ctx, config);
            _wallSplat = new WallSplatState(_ctx, config, _tumble);
            _groundBounce = new GroundBounceState(_ctx, config, _tumble);
        }

        /// <summary>Đang ở 1 trong các air-reaction state (relay chỉ đổi splat/bounce khi đang tumble).</summary>
        public bool IsTumbling
        {
            get
            {
                IState s = _ctx?.Machine.CurrentState;
                return ReferenceEquals(s, _tumble)
                    || ReferenceEquals(s, _wallSplat)
                    || ReferenceEquals(s, _groundBounce);
            }
        }

        /// <summary>KnockbackResolver gọi SAU khi set velocity knockback → vào tumble + bật material nảy.</summary>
        public void EnterTumble()
        {
            ApplyBounceMaterial(true);
            _ctx.Machine.ChangeState(_tumble);
        }

        /// <summary>KnockbackCollisionRelay gọi khi va tường tốc-độ-cao → dán tường (stun) rồi rơi tiếp.</summary>
        public void EnterWallSplat()
        {
            _ctx.Machine.ChangeState(_wallSplat);
        }

        /// <summary>KnockbackCollisionRelay gọi khi va sàn tốc-độ-cao → nảy lên (material lo), combo tiếp.</summary>
        public void EnterGroundBounce()
        {
            _ctx.Machine.ChangeState(_groundBounce);
        }

        private void Update()
        {
            // Material NẢY khi đang ở HIT-REACTION: tumble (P7) HOẶC Hurt giữa combo (quyết định người
            // dùng: pushback đẩy địch vào tường → BẬT RA như bóng — bounciness lo, không code phản xạ).
            // Gỡ khi về neutral — tránh nảy lúc đi/nhảy thường (hồi quy P1–P3). Quản 2 chiều ở Update
            // (đổi state có thể từ nhiều nơi: HitReaction/resolver/chính các state).
            if (bodyCollider == null) return;
            bool tumbling = IsTumbling;
            bool wantBounce = tumbling || (_combat != null && _combat.IsHurt);
            if (wantBounce && bodyCollider.sharedMaterial != bounceMaterial)
                ApplyBounceMaterial(true);
            else if (!wantBounce && bodyCollider.sharedMaterial == bounceMaterial)
                ApplyBounceMaterial(false);

            // PUSHBOX 2 người: TẮT khi MÌNH đang bay knockback (tumble/splat/bounce) — xác bay xuyên
            // người attacker (Smash-style): không bị thân địch chặn cú văng (nhất là bật tường bay
            // ngược lại), không truyền động lượng xô attacker, không nảy bounce-material vào người.
            // Hurt/Block mặt đất GIỮ pushbox (spacing kiểu SF — quyết định người dùng, case 1).
            // Cờ _ignoringOpponent: chỉ gọi IgnoreCollision khi TRẠNG THÁI MÌNH đổi → 2 controller
            // (mỗi bên 1 cái) không ghi đè nhau trên cùng cặp collider.
            SetIgnoreOpponent(tumbling);
        }

        private void OnDisable()
        {
            // Trả pushbox khi bị disable giữa tumble (chết/respawn/đổi trận) — IgnoreCollision là trạng
            // thái persistent của Physics2D, không tự hồi.
            SetIgnoreOpponent(false);
        }

        private void SetIgnoreOpponent(bool ignore)
        {
            if (_ignoringOpponent == ignore) return;
            if (bodyCollider == null || opponentBodyCollider == null) return;
            Physics2D.IgnoreCollision(bodyCollider, opponentBodyCollider, ignore);
            _ignoringOpponent = ignore;
        }

        private void ApplyBounceMaterial(bool on)
        {
            if (bodyCollider == null) return;
            bodyCollider.sharedMaterial = on ? bounceMaterial : _defaultMaterial;
        }
    }
}
