using UnityEngine;
using FA.Core;

namespace FA.Specials
{
    /// <summary>
    /// SPAWN chưởng theo Animation Event (P9, research §3.4). MonoBehaviour trên prefab nhân vật, nghe
    /// C# event PER-INSTANCE <see cref="AnimationEventRelay.SpawnProjectileEvent"/> (đặt event
    /// "SpawnProjectile" trên clip special tại frame muốn bắn — frame-precise, KHÔNG đếm frame §1.1).
    ///
    /// Mỗi spawn:
    ///  1) Instantiate prefab tại MUZZLE (offset theo Facing) — KHÔNG dời transform sau đó (vật lý lo).
    ///  2) Set hướng nhìn chưởng + <see cref="Rigidbody2D.linearVelocity"/> = facing × speed (Unity-native).
    ///  3) Gán NGƯỜI BẮN (attackerRoot) + kênh hit (OnHitLandedChannel) cho HitboxAuthoring con của chưởng
    ///     (qua SendMessage → FA.Specials KHÔNG ref FA.Combat §1.3) + cho Projectile.Init (lọc despawn-on-hit).
    ///
    /// variantId của Animation Event (SpawnProjectile(int)) chọn 1 trong N prefab (vd chưởng thường vs
    /// charged to hơn). Cùng list prefab nên thêm chưởng = thêm phần tử (extensible).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProjectileSpawner : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [Tooltip("AnimationEventRelay cùng object Animator. Bỏ trống = tìm trong children.")]
        [SerializeField] private AnimationEventRelay relay;

        [Tooltip("Root NGƯỜI BẮN (gán vào HitData.Attacker để mana/combo cộng đúng + guard self-hit). " +
                 "Bỏ trống = transform (root prefab nhân vật).")]
        [SerializeField] private Transform shooterRoot;

        [Tooltip("Điểm bắn (muzzle). Vị trí chưởng spawn. Bỏ trống = vị trí prefab này. X-offset tự lật " +
                 "theo Facing để chưởng ra TRƯỚC mặt.")]
        [SerializeField] private Transform muzzle;

        [Tooltip("Kênh hit (OnHitLandedChannel) — gán cho HitboxAuthoring chưởng + Projectile (despawn-on-hit). " +
                 "CÙNG asset với nhân vật bắn (per-player).")]
        [SerializeField] private HitEventChannelSO hitChannel;

        [Header("Prefab chưởng (index = variantId của Animation Event SpawnProjectile(int))")]
        [Tooltip("Danh sách prefab chưởng. variantId 0 = phần tử 0. Thêm chưởng = thêm phần tử.")]
        [SerializeField] private Projectile[] projectilePrefabs;

        [Tooltip("Tốc độ bay (units/s). Bay theo hướng nhìn × speed. gravityScale của prefab quyết bay " +
                 "thẳng (0) hay theo cung (>0). CẦN TEST.")]
        [Min(0f)] [SerializeField] private float projectileSpeed = 10f;

        // ICombatContext cấp Facing (qua Core interface — KHÔNG ref Movement §1.3).
        private ICombatContext _ctx;

        private void Awake()
        {
            _ctx = GetComponent<ICombatContext>();
            if (relay == null) relay = GetComponentInChildren<AnimationEventRelay>();
            if (shooterRoot == null) shooterRoot = transform;
            if (muzzle == null) muzzle = transform;
        }

        private void OnEnable()
        {
            if (relay != null) relay.SpawnProjectileEvent += OnSpawnProjectile;
        }

        private void OnDisable()
        {
            if (relay != null) relay.SpawnProjectileEvent -= OnSpawnProjectile;
        }

        private void OnSpawnProjectile(int variantId)
        {
            Projectile prefab = GetPrefab(variantId);
            if (prefab == null)
            {
                Debug.LogWarning($"[ProjectileSpawner] Không có prefab cho variantId={variantId}.", this);
                return;
            }

            int facing = _ctx != null ? _ctx.Facing : (transform.lossyScale.x >= 0f ? 1 : -1);

            // Vị trí spawn: muzzle, đối xứng X theo facing để chưởng ra TRƯỚC mặt cả 2 hướng.
            Vector3 muzzleLocal = muzzle.localPosition;
            Vector3 spawnPos = transform.TransformPoint(new Vector3(muzzleLocal.x, muzzleLocal.y, muzzleLocal.z));
            // (transform.TransformPoint đã tính localScale.x âm khi lật facing → muzzle tự sang đúng phía.)

            Projectile proj = Instantiate(prefab, spawnPos, Quaternion.identity);

            // Lật hướng nhìn chưởng theo facing người bắn (hitbox/visual đúng phía).
            Vector3 s = proj.transform.localScale;
            float mag = Mathf.Abs(s.x);
            s.x = facing > 0 ? mag : -mag;
            proj.transform.localScale = s;

            // MOVEMENT Unity-native: set linearVelocity 1 lần. gravityScale prefab quyết bay thẳng/cung.
            var rb = proj.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = new Vector2(facing * projectileSpeed, rb.linearVelocity.y);

            // Gán NGƯỜI BẮN + kênh hit cho HitboxAuthoring con của chưởng (SendMessage → không ref FA.Combat).
            proj.BroadcastMessage("SetAttacker", shooterRoot, SendMessageOptions.DontRequireReceiver);

            // Projectile tự lo lifetime/despawn + lọc despawn-on-hit theo owner.
            proj.Init(shooterRoot != null ? shooterRoot.gameObject : gameObject, hitChannel);
        }

        private Projectile GetPrefab(int variantId)
        {
            if (projectilePrefabs == null || projectilePrefabs.Length == 0) return null;
            if (variantId < 0 || variantId >= projectilePrefabs.Length) variantId = 0;
            return projectilePrefabs[variantId];
        }
    }
}
