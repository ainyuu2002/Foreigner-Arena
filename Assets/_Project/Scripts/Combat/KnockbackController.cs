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

        [Header("Relay (đòn xuyên người per-move)")]
        [Tooltip("AnimationEventRelay cùng object Animator — nghe PassThroughOn/Off (cửa sổ đòn lướt " +
                 "XUYÊN thân đối thủ, đặt trên clip). Bỏ trống = tự tìm trong children; không có = " +
                 "tính năng xuyên người tắt, hành vi cũ giữ nguyên.")]
        [SerializeField] private AnimationEventRelay relay;

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
        private bool _ignoringOpponent;               // cờ pushbox đang tắt (mình là bên đang giữ tắt).
        private bool _passThrough;                    // cửa sổ xuyên thân per-move (PassThroughOn/Off trên clip).

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
            if (relay == null) relay = GetComponentInChildren<AnimationEventRelay>(); // null-safe: không có = không xuyên.
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

        // Cửa sổ xuyên thân đến từ Animation Event trên clip đòn (§1.1) — subscribe per-instance,
        // đối xứng OnEnable/OnDisable (chống double-fire, pattern MoveMotionController).
        private void OnEnable()
        {
            if (relay == null) return;
            relay.PassThroughOnEvent += OnPassThroughOn;
            relay.PassThroughOffEvent += OnPassThroughOff;
            // Lưới an toàn: RecoveryEnd (cuối MỌI clip đòn) tự đóng cửa sổ — clip quên PassThroughOff
            // cũng không kẹt xuyên-người sang neutral. Cửa sổ không bao giờ sống lâu hơn đòn.
            relay.RecoveryEndEvent += OnPassThroughOff;
        }

        private void OnPassThroughOn() => _passThrough = true;

        private void OnPassThroughOff() => _passThrough = false;

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

            // Cửa sổ xuyên thân per-move: nếu đòn bị NGẮT (mình dính đòn/bị văng) trước khi clip kịp
            // chạy tới PassThroughOff thì event đó không bao giờ bắn — tự đóng ở đây để không kẹt
            // xuyên-người vĩnh viễn. (Tumbling thì pushbox đã tắt theo nhánh P7 rồi.)
            if (_passThrough && (tumbling || (_combat != null && _combat.IsHurt)))
                _passThrough = false;

            // PUSHBOX 2 người: TẮT khi MÌNH đang bay knockback (tumble/splat/bounce) — xác bay xuyên
            // người attacker (Smash-style): không bị thân địch chặn cú văng (nhất là bật tường bay
            // ngược lại), không truyền động lượng xô attacker, không nảy bounce-material vào người.
            // — HOẶC khi đòn đang mở cửa sổ xuyên thân (PassThroughOn/Off trên clip, per-move).
            // Hurt/Block mặt đất GIỮ pushbox (spacing kiểu SF — quyết định người dùng, case 1).
            SetIgnoreOpponent(tumbling || _passThrough);
        }

        private void OnDisable()
        {
            // Trả pushbox khi bị disable giữa tumble (chết/respawn/đổi trận) — IgnoreCollision là trạng
            // thái persistent của Physics2D, không tự hồi.
            _passThrough = false;
            SetIgnoreOpponent(false);
            if (relay == null) return;
            relay.PassThroughOnEvent -= OnPassThroughOn;
            relay.PassThroughOffEvent -= OnPassThroughOff;
            relay.RecoveryEndEvent -= OnPassThroughOff;
        }

        private void SetIgnoreOpponent(bool ignore)
        {
            if (bodyCollider == null || opponentBodyCollider == null) return;
            // ignore=true RE-ASSERT MỖI FRAME: IgnoreCollision là trạng thái CHUNG của cặp collider mà
            // 2 controller (mỗi fighter 1 cái) cùng ghi — bên kia vừa hết tumble sẽ trả pushbox đúng lúc
            // mình còn cần xuyên; gọi lại mỗi frame giữ đúng ngữ nghĩa "tắt khi CÒN MỘT bên cần".
            // ignore=false chỉ gọi lúc chuyển trạng thái (không ghi đè bên kia đang cần tắt).
            if (ignore)
            {
                Physics2D.IgnoreCollision(bodyCollider, opponentBodyCollider, true);
                _ignoringOpponent = true;
            }
            else if (_ignoringOpponent)
            {
                Physics2D.IgnoreCollision(bodyCollider, opponentBodyCollider, false);
                _ignoringOpponent = false;
            }
        }

        private void ApplyBounceMaterial(bool on)
        {
            if (bodyCollider == null) return;
            bodyCollider.sharedMaterial = on ? bounceMaterial : _defaultMaterial;
        }
    }
}
