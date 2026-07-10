using System.Collections.Generic;
using UnityEngine;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// Phát hiện va chạm hitbox→hurtbox & RAISE <see cref="HitData"/> (§3.5). Đặt trên CHILD "Hitbox"
    /// (cùng object với BoxCollider2D isTrigger, Layer Hitbox). Lọc va chạm do PHYSICS2D COLLISION
    /// MATRIX (Project Settings) → OnTriggerEnter2D chỉ kêu khi chạm Hurtbox; KHÔNG "if(layer==...)",
    /// KHÔNG tự tính overlap (§1.1). Chỉ guard self-root (P1 không tự trúng mình) + chống double-hit.
    ///
    /// KHÔNG apply damage/knockback ở đây — chỉ RAISE HitData qua kênh; listener (HitstopController P5,
    /// KnockbackResolver P7, Mana/Rage P8) tự xử (§3.3 decoupling). MoveData hiện hành do
    /// HitboxController.SetCurrentMove cấp (set TRƯỚC HitboxOn) → biết damage%/knockback/hitstop.
    ///
    /// Collider enable/disable do HitboxController (theo Animation Event) — KHÔNG đếm frame ở đây.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class HitboxAuthoring : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [Tooltip("Root GameObject NGƯỜI ĐÁNH (để guard self-hit + gán vào HitData.Attacker). " +
                 "Bỏ trống = lấy transform.root.")]
        [SerializeField] private Transform attackerRoot;

        [Tooltip("Kênh raise khi trúng (HitEventChannelSO 'OnHitLandedChannel'). BẮT BUỘC gán asset.")]
        [SerializeField] private HitEventChannelSO hitChannel;

        [Tooltip("BoxCollider2D của hitbox (cùng object). Bỏ trống = lấy trên object này.")]
        [SerializeField] private BoxCollider2D box;

        [Tooltip("(Tùy chọn) MoveData RIÊNG cho hitbox này — GHI ĐÈ move hiện hành (SetCurrentMove). Dùng " +
                 "cho đòn NHIỀU HIT có hit kết tham số khác: hit giữa = Hitbox0 (move của đòn, carry nhẹ), " +
                 "hit kết = Hitbox1 với override (ender văng theo %). Clip gọi HitboxOn(1) cho hit kết. " +
                 "Để trống = dùng move hiện hành (hành vi cũ, mọi hitbox chung tham số).")]
        [SerializeField] private MoveData moveOverride;

        // MoveData đòn hiện hành (HitboxController set) — nguồn tham số damage/knockback/hitstop.
        private MoveData _move;

        // Move THỰC DÙNG khi va chạm: override (per-hitbox) thắng move hiện hành.
        private MoveData ActiveMove => moveOverride != null ? moveOverride : _move;

        // (P10 ADD) Buff context của ATTACKER (Rage Mode) — null-safe ×1.0 nếu prefab không gắn. Resolve
        // LAZY ở hit-time từ attackerRoot (attacker có thể đổi qua SetAttacker P9 cho projectile). ADD nhỏ
        // giống SetAttacker — KHÔNG đổi logic raise P5/P7.
        private IBuffContext _attackerBuff;
        private Transform _buffCachedRoot;

        private IBuffContext AttackerBuff(Transform atkRoot)
        {
            if (!ReferenceEquals(_buffCachedRoot, atkRoot))
            {
                _buffCachedRoot = atkRoot;
                _attackerBuff = atkRoot != null ? atkRoot.GetComponent<IBuffContext>() : null;
            }
            return _attackerBuff;
        }

        // Ignore-set chống double-hit trong 1 active-window. Reset mỗi HitboxOn (HitboxController gọi).
        private readonly HashSet<GameObject> _ignore = new HashSet<GameObject>();

        private void Awake()
        {
            if (box == null) box = GetComponent<BoxCollider2D>();
            if (attackerRoot == null) attackerRoot = transform.root;
            box.isTrigger = true;   // bảo hiểm: hitbox luôn là trigger (Unity-native overlap).
            box.enabled = false;    // mặc định TẮT — bật bởi Animation Event HitboxOn (§3.5).
        }

        /// <summary>HitboxController cấp MoveData đòn hiện hành (trước HitboxOn).</summary>
        public void SetMove(MoveData move) => _move = move;

        /// <summary>
        /// (P9) Gán lại NGƯỜI ĐÁNH cho hitbox PROJECTILE. Chưởng là object RỜI (transform.root = chính
        /// chưởng) → mặc định Attacker sẽ là chưởng, sai attribution (mana/combo không cộng người bắn) +
        /// self-hit guard sai. ProjectileSpawner gọi cái này NGAY sau Instantiate, truyền root NGƯỜI BẮN.
        /// Chỉ THÊM method (không phá hành vi P5: nhân vật thường vẫn để attackerRoot=transform.root).
        /// </summary>
        public void SetAttacker(Transform shooterRoot) => attackerRoot = shooterRoot;

        /// <summary>HitboxController bật/tắt collider theo HitboxOn/Off (Animation Event).</summary>
        public void SetEnabled(bool on) { if (box != null) box.enabled = on; }

        // Debug multi-hit: log 1 lần/cửa sổ khi physics THẤY overlap (tách "event không bắn" khỏi
        // "physics không thấy chồng nhau" khi chẩn đoán đòn nhiều hit).
        private bool _dbgOverlapLogged;

        /// <summary>HitboxController gọi mỗi HitboxOn → 1 active-window = 1 hit/victim (§3.6).</summary>
        public void ResetIgnore()
        {
            _ignore.Clear();
            _dbgOverlapLogged = false;
        }

        private void OnTriggerEnter2D(Collider2D other) => HandleOverlap(other);

        // (Fix multi-hit) Re-enable collider khi 2 bên VẪN đang chồng nhau không sinh Enter mới đáng tin
        // → đòn nhiều hit với hitbox to (victim không thoát khỏi hộp giữa 2 cửa sổ) chỉ ăn hit đầu.
        // Stay + ignore-set = polling chuẩn fighting game: mỗi HitboxOn ResetIgnore → Stay của physics
        // step kế tiếp đăng ký hit mới; trong cùng cửa sổ ignore-set chặn double-hit như cũ.
        private void OnTriggerStay2D(Collider2D other) => HandleOverlap(other);

        private void HandleOverlap(Collider2D other)
        {
            if (!_dbgOverlapLogged)
            {
                _dbgOverlapLogged = true;
                Debug.Log($"[HBX] {name}: overlap {other.transform.root.name} " +
                          $"(move={( _move != null ? _move.moveId : "NULL")}, override={(moveOverride != null ? moveOverride.moveId : "-")})", this);
            }

            MoveData move = ActiveMove;
            if (move == null || hitChannel == null) return;

            // 'other' luôn là Hurtbox (Matrix đã lọc). Tìm root người nhận.
            GameObject victimRoot = other.transform.root.gameObject;
            GameObject atkRoot = attackerRoot != null ? attackerRoot.gameObject : gameObject;

            // Guard self-hit (fallback nếu Matrix Player1↔Player1 chưa tách) — P1 không tự trúng mình.
            if (ReferenceEquals(victimRoot, atkRoot)) return;

            // Chống double-hit: 1 victim/active-window đúng 1 lần.
            if (!_ignore.Add(victimRoot)) return;

            int facing = transform.lossyScale.x >= 0f ? 1 : -1;
            Vector2 contact = other.bounds.ClosestPoint(box.bounds.center);

            // (P10 ADD) Multiplier Rage Mode của attacker (null-safe ×1.0). Áp ở ĐIỂM RAISE → KHÔNG sửa
            // MoveData/KnockbackResolver (research §3.4). DMG nhân vào %; knockback nhân base + growth.
            IBuffContext buff = AttackerBuff(attackerRoot);
            float dmgMult = buff != null ? buff.DamageDealtMult : 1f;
            float kbMult = buff != null ? buff.KnockbackDealtMult : 1f;

            var hit = new HitData(
                attacker: atkRoot,
                victim: victimRoot,
                damagePercent: move.baseDamagePercent * dmgMult,
                launchAngle: move.launchAngle,
                baseKnockback: move.baseKnockback * kbMult,
                knockbackGrowth: move.knockbackGrowth * kbMult,
                hitstopFrames: move.hitstopFrames,
                facingSign: facing,
                isSpike: move.isSpike,
                causesLaunch: move.causesLaunch,
                hitsLow: move.hitsLow,
                hitsHigh: move.hitsHigh,
                hitstunFrames: move.hitstunFrames,   // P6: hurtstun đòn này gây cho victim.
                contactPoint: contact,
                role: move.role,                     // P10: BlockBreak (Ultimate) cho HitReactionController.
                hitEffect: move.hitEffect,           // VFX/SFX per-move (spark + hitsound riêng từng đòn).
                blockEffect: move.blockEffect,
                manaGainMult: move.manaGainMultiplier); // Balance: kinh tế mana per-move (ManaBar nhân).

            hitChannel.RaiseEvent(hit);
        }
    }
}
