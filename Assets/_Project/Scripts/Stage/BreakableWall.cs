using UnityEngine;
using FA.Core;

namespace FA.Stage
{
    /// <summary>
    /// TƯỜNG VỠ ĐƯỢC (P11, research §3.3) — break-stat tích theo TỔNG LỰC KNOCKBACK đập vào, hồi sau ~5s
    /// không bị đập, vỡ → mở death-zone con (Unity-native, KHÔNG ref FA.Combat). Đo lực bằng VA CHẠM VẬT LÝ
    /// Unity: <see cref="OnCollisionEnter2D"/> đọc <c>collision.relativeVelocity.magnitude</c> (giống hệt
    /// KnockbackCollisionRelay P7 nhưng đặt ở tường, gate ngưỡng tốc độ để LOẠI va đi-bộ thường — không
    /// cần biết Combat/IsTumbling).
    ///
    /// Unity-native lo:
    /// - Va chạm + relativeVelocity (collider VẬT LÝ non-trigger của tường).
    /// - Nứt dần = set Animator float param "WallDamage01" (0..1) → artist làm transition/sprite-swap; KHÔNG
    ///   tween toạ độ.
    /// - Vỡ = tắt wallCollider (vật lý) + bật deathZoneObject con (BoxCollider2D trigger Layer DeathZone) +
    ///   (tùy) bật crackedHitbox child khi tới ngưỡng nứt.
    /// Code chỉ điều phối break-stat (cộng/hồi/so ngưỡng). Chỉ ref Core (StageConfigSO, GameLayers).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BreakableWall : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("StageConfigSO của map — breakThreshold, wallImpactMinSpeed, recoverDelay/PerSec, wallRecovers. BẮT BUỘC.")]
        [SerializeField] private StageConfigSO config;

        [Header("Va chạm")]
        [Tooltip("Layer được tính là 'vật đập tường' (Player1|Player2). Va từ layer khác bỏ qua.")]
        [SerializeField] private LayerMask impactLayers;

        [Tooltip("Collider VẬT LÝ của tường (non-trigger) — tắt khi vỡ. Bỏ trống = lấy Collider2D non-trigger trên object này.")]
        [SerializeField] private Collider2D wallCollider;

        [Header("Vỡ → mở death zone")]
        [Tooltip("GameObject death-zone con ĐỨNG SAU tường (BoxCollider2D trigger Layer DeathZone + DeathZone.cs). " +
                 "BẬT khi vỡ. Bỏ trống = không mở zone (chỉ tắt tường).")]
        [SerializeField] private GameObject deathZoneObject;

        [Header("Nứt dần (Unity-native visual)")]
        [Tooltip("Animator của tường — set float 'WallDamage01' (0..1) để chuyển sprite/transition nứt. Tùy chọn.")]
        [SerializeField] private Animator animator;

        [Tooltip("Tên param float (0..1) cho mức nứt. Khớp Animator. Mặc định 'WallDamage01'.")]
        [SerializeField] private string damageParam = "WallDamage01";

        [Tooltip("Ngưỡng [0..1] mà tới đó bật crackedHitbox (mảnh tường gây dmg nhẹ). Mặc định 0.66 (tier nứt nặng).")]
        [Range(0f, 1f)] [SerializeField] private float crackedHitboxAt = 0.66f;

        [Tooltip("Child hitbox (BoxCollider2D isTrigger Layer Hitbox) bật khi nứt nặng. Tùy chọn (chờ sprite §6).")]
        [SerializeField] private GameObject crackedHitbox;

        private float _breakStat;
        private float _timeSinceLastImpact;
        private bool _broken;
        private int _damageParamHash;

        /// <summary>Mức nứt hiện tại [0..1] (debug/visual).</summary>
        public float Damage01 => config != null && config.breakThreshold > 0f
            ? Mathf.Clamp01(_breakStat / config.breakThreshold) : 0f;

        /// <summary>True khi tường đã vỡ.</summary>
        public bool IsBroken => _broken;

        private void Awake()
        {
            if (config == null)
                Debug.LogError("[BreakableWall] Thiếu StageConfigSO.", this);
            if (wallCollider == null)
            {
                foreach (var c in GetComponents<Collider2D>())
                    if (!c.isTrigger) { wallCollider = c; break; }
            }
            _damageParamHash = Animator.StringToHash(damageParam);
            if (deathZoneObject != null) deathZoneObject.SetActive(false);
            if (crackedHitbox != null) crackedHitbox.SetActive(false);
            _timeSinceLastImpact = float.MaxValue;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_broken || config == null) return;
            if (impactLayers.value != 0 && !IsInMask(collision.collider.gameObject.layer, impactLayers))
                return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < config.wallImpactMinSpeed) return; // gate: loại va đi-bộ thường.

            _breakStat += speed;                            // tích TỔNG lực knockback (research §3.3).
            _timeSinceLastImpact = 0f;
            ApplyVisual();

            if (_breakStat >= config.breakThreshold)
                Break();
        }

        private void Update()
        {
            if (_broken || config == null) return;

            _timeSinceLastImpact += Time.deltaTime;

            // Hồi dần sau recoverDelay không bị đập (nếu map cho hồi).
            if (config.wallRecovers && _timeSinceLastImpact >= config.wallRecoverDelay && _breakStat > 0f)
            {
                _breakStat = Mathf.Max(0f, _breakStat - config.wallRecoverPerSec * Time.deltaTime);
                ApplyVisual();
            }
        }

        private void ApplyVisual()
        {
            float d = Damage01;
            if (animator != null) animator.SetFloat(_damageParamHash, d);
            if (crackedHitbox != null) crackedHitbox.SetActive(d >= crackedHitboxAt);
        }

        private void Break()
        {
            _broken = true;
            if (wallCollider != null) wallCollider.enabled = false; // hết "tường vô hình".
            if (animator != null) animator.SetFloat(_damageParamHash, 1f);
            if (crackedHitbox != null) crackedHitbox.SetActive(false);
            if (deathZoneObject != null) deathZoneObject.SetActive(true); // mở vùng chết sau tường.
            DebugLog.Log($"[BreakableWall] '{name}' VỠ → mở death zone.", this);
        }

        /// <summary>Reset tường về nguyên vẹn (debug/test, hoặc P12 đầu trận). </summary>
        public void ResetWall()
        {
            _broken = false;
            _breakStat = 0f;
            _timeSinceLastImpact = float.MaxValue;
            if (wallCollider != null) wallCollider.enabled = true;
            if (deathZoneObject != null) deathZoneObject.SetActive(false);
            if (crackedHitbox != null) crackedHitbox.SetActive(false);
            ApplyVisual();
        }

        private static bool IsInMask(int layer, LayerMask mask) => (mask.value & (1 << layer)) != 0;
    }
}
