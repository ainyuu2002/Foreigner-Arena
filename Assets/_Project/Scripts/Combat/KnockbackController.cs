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

        // ICombatContext từ cùng prefab (CharacterMotor implement). Phụ thuộc Core interface.
        private ICombatContext _ctx;

        private LauncherState _tumble;
        private WallSplatState _wallSplat;
        private GroundBounceState _groundBounce;

        private PhysicsMaterial2D _defaultMaterial;   // material thường (gỡ bounce khi về ground).

        public KnockbackConfigSO Config => config;

        private void Awake()
        {
            _ctx = GetComponent<ICombatContext>();
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
            _groundBounce = new GroundBounceState(_ctx, _tumble);
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
            // Gỡ material nảy khi đã thoát mọi air-reaction state (về Idle/Walk/jump...) — tránh nảy lúc
            // di chuyển thường (hồi quy P1–P3). Kiểm tra ở Update (đổi state có thể từ chính các state lo).
            if (!IsTumbling && bodyCollider != null && bodyCollider.sharedMaterial == bounceMaterial)
                ApplyBounceMaterial(false);
        }

        private void ApplyBounceMaterial(bool on)
        {
            if (bodyCollider == null) return;
            bodyCollider.sharedMaterial = on ? bounceMaterial : _defaultMaterial;
        }
    }
}
