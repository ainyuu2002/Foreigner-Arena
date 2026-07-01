using UnityEngine;
using UnityEngine.Serialization;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// BỘ ÁP LỰC knockback (P7 + P16) trên VICTIM. GDD §3.6: trong combo CHỈ tích % + hurtstun nhẹ
    /// (HurtState giữ nguyên P6), lực bay xa ở hit CUỐI. Cơ chế per-victim (P16 — VĂNG-TẠI-KNOCKBACKPOINT):
    ///
    ///  1. MỘT animation có thể NHIỀU hit. Mỗi OnHitConfirmed (mình là victim) → CHỈ LƯU pending knockback
    ///     từ hit MỚI NHẤT (override hit trước — đòn cuối quyết hướng/lực), KHÔNG áp ngay. Hit ĐẦU của đòn
    ///     2-hit KHÔNG văng (văng thì hit sau trượt). Đòn THƯỜNG chờ Animation Event <c>KnockbackPoint</c>
    ///     (frame hit cuối) → attacker raise <c>onKnockbackLaunch</c> (nếu KHÔNG nối) → mới <c>ScheduleApply()</c>.
    ///  2. Spike (<see cref="HitData.IsSpike"/>) / launcher (<see cref="HitData.CausesLaunch"/>): VĂNG NGAY
    ///     trên hit (<c>ScheduleApply</c> trong OnHitConfirmed) — mở juggle / đập xuống, KHÔNG cần
    ///     KnockbackPoint, KHÔNG bao giờ hoãn.
    ///  3. Văng áp tại <c>_applyDelay = hitstop+1</c> → bay đúng lúc hết hitstop (KHÔNG bị HitstopController
    ///     nuốt). Attacker đang nối (latch follow-up hợp lệ P15) → KHÔNG raise launch ở KnockbackPoint →
    ///     đòn đó KHÔNG văng (combo tiếp).
    ///
    /// P16 BỎ HẲN release-timer: KHÔNG còn <c>_framesSinceLastHit</c> / <c>knockbackReleaseFrames</c> (P7
    /// cũ chờ 26 fixed-step "combo coi như kết thúc" → game feel khựng ~0.43s). Thay bằng: văng tại
    /// <c>KnockbackPoint</c> (Animation Event = frame hit cuối, §1.1) trừ khi đang nối (INPUT-LATCH P15,
    /// intent attacker), KHÔNG timer mới. Xem [[phase16-knockback]].
    ///
    /// Formula Smash-lite (research §2): <c>mag = (base + growth×%victim) × refWeight/mass</c>; hướng theo
    /// facing + launchAngle; áp DI ±diMaxDegrees (digital, 1 lần lúc apply); set <see cref="Rigidbody2D.linearVelocity"/>.
    /// Apply SAU hitstop: <c>_applyDelay = hitstop+1</c> chờ qua hết hitstop của hit cuối (research §4 cạm bẫy 2)
    /// — "khoảng nhỏ" còn lại = chính hitstop impact (CHỦ Ý), KHÔNG phải timer nhân tạo.
    ///
    /// Đặt FA.Combat, chỉ ref Core. Đọc % từ KnockbackPercent (cùng module), weight từ Rigidbody2D.mass
    /// (KHÔNG đọc CharacterData → cross-module §1.3). Vào tumble qua KnockbackController. Kênh launch =
    /// GameObjectEventChannelSO (Core) → attacker (FA.Combat/FA.Specials) raise mà KHÔNG ref class chéo (§1.3).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KnockbackResolver : MonoBehaviour, IPendingKnockback, IKnockbackCanceler
    {
        [Header("Kênh sự kiện")]
        [Tooltip("OnHitConfirmed (HitEventChannelSO) — CÙNG asset HitReactionController raise (trúng-không-block).")]
        [SerializeField] private HitEventChannelSO onHitConfirmed;

        [Tooltip("(P16) OnKnockbackLaunch (GameObjectEventChannelSO) — CÙNG asset attacker state raise tại " +
                 "Animation Event KnockbackPoint (frame hit cuối) khi KHÔNG đang nối. Nhận attacker root: " +
                 "nếu == attacker của pending → ScheduleApply (văng tại hitstop+1). Bỏ trống = đòn thường " +
                 "KHÔNG bao giờ văng (chỉ spike/launcher văng).")]
        [FormerlySerializedAs("onComboContinues")]
        [SerializeField] private GameObjectEventChannelSO onKnockbackLaunch;

        [Header("Tham chiếu (cùng prefab)")]
        [Tooltip("Config knockback. BẮT BUỘC gán.")]
        [SerializeField] private KnockbackConfigSO config;

        [Tooltip("Thanh % của victim (đọc Current). BẮT BUỘC gán.")]
        [SerializeField] private KnockbackPercent percent;

        [Tooltip("KnockbackController (vào tumble + material nảy). BẮT BUỘC gán.")]
        [SerializeField] private KnockbackController knockback;

        [Tooltip("Root để so 'mình có phải victim không'. Bỏ trống = transform.root.")]
        [SerializeField] private Transform root;

        private ICombatContext _ctx;
        private Rigidbody2D _body;

        // Pending knockback từ hit mới nhất.
        private bool _hasPending;
        private float _pendingLaunchAngle;
        private float _pendingBaseKnockback;
        private float _pendingGrowth;
        private int _pendingFacing;
        private int _pendingHitstop;   // hitstop của hit cuối → delay apply để không bị hitstop nuốt.
        // (P16) Attacker root của pending — so với attacker trong OnKnockbackLaunch để CHỈ văng khi đúng
        // attacker của pending raise (không bị launch của trận/cú khác kích nhầm).
        private GameObject _pendingAttacker;

        /// <summary>True khi còn knockback CHỜ áp trên victim này (đã ăn hit nhưng chưa bốc vào tumble).
        /// HurtState hỏi cờ này để KHÔNG tự về Idle trước khi bị bốc vào tumble (neutral-gap fix). P16:
        /// <c>_hasPending</c> true từ lúc ăn hit tới khi văng (KnockbackPoint→ScheduleApply) hoặc tới khi
        /// đòn nối (không raise launch → pending bị hit kế override, hoặc hurtstun hết → HurtState về
        /// neutral). Khi đã lên lịch, <c>_applyDelay >= 0</c> giữ true tới lúc văng.</summary>
        public bool HasPendingKnockback => _hasPending || _applyDelay >= 0;

        // Lên lịch áp velocity: chờ _applyDelay fixed-step (≈ hitstop của hit cuối + 1) rồi mới set, để
        // HitstopController KHÔNG zero mất cú launch (research §4 cạm bẫy 2). -1 = không có lịch.
        private int _applyDelay = -1;
        private Vector2 _applyVelocity;

        private void Awake()
        {
            if (root == null) root = transform.root;
            _ctx = GetComponent<ICombatContext>();
            _body = root != null ? root.GetComponent<Rigidbody2D>() : GetComponent<Rigidbody2D>();
            if (_ctx == null) Debug.LogError("[KnockbackResolver] Thiếu ICombatContext.", this);
            if (config == null) Debug.LogError("[KnockbackResolver] Thiếu KnockbackConfigSO.", this);
        }

        private void OnEnable()
        {
            if (onHitConfirmed != null) onHitConfirmed.OnEventRaised += OnHitConfirmed;
            if (onKnockbackLaunch != null) onKnockbackLaunch.OnEventRaised += OnKnockbackLaunch;
        }

        private void OnDisable()
        {
            if (onHitConfirmed != null) onHitConfirmed.OnEventRaised -= OnHitConfirmed;
            if (onKnockbackLaunch != null) onKnockbackLaunch.OnEventRaised -= OnKnockbackLaunch;
        }

        /// <summary>(P10 — IKnockbackCanceler) Hủy MỌI knockback đang chờ trên victim này. BurstState gọi
        /// khi thoát combo bằng burst (research §4 cạm bẫy 3). CHỈ reset cờ pending/lịch (idempotent) —
        /// KHÔNG đụng logic resolve. (P16: không còn dùng cho suppress-by-continue; văng do KnockbackPoint.)</summary>
        public void CancelPending()
        {
            _hasPending = false;
            _applyDelay = -1;
            _pendingAttacker = null;
        }

        /// <summary>(P16) Attacker raise tại Animation Event <c>KnockbackPoint</c> (frame hit cuối) khi
        /// KHÔNG đang nối → VĂNG pending của victim. CHỈ áp nếu attacker == attacker của pending HIỆN HÀNH
        /// (không bị launch của trận/cú khác kích nhầm) và còn pending CHƯA lên lịch (<c>_hasPending</c>).
        /// Idempotent: đã lên lịch (<c>_applyDelay >= 0</c>) thì bỏ qua — KHÔNG lên lịch chồng.</summary>
        private void OnKnockbackLaunch(GameObject attacker)
        {
            if (!_hasPending) return;                                     // không có pending → bỏ qua.
            if (_applyDelay >= 0) return;                                 // đã lên lịch → idempotent, không chồng.
            if (!ReferenceEquals(attacker, _pendingAttacker)) return;     // không phải attacker của pending này.
            ScheduleApply();
        }

        private void OnHitConfirmed(HitData hit)
        {
            GameObject me = root != null ? root.gameObject : gameObject;
            if (!ReferenceEquals(hit.Victim, me)) return;

            // Lưu pending từ hit MỚI NHẤT (override — đòn cuối quyết hướng/lực). Lưu attacker để so với
            // OnKnockbackLaunch (chỉ attacker này raise KnockbackPoint mới văng pending này).
            _pendingLaunchAngle = hit.LaunchAngle;
            _pendingBaseKnockback = hit.BaseKnockback;
            _pendingGrowth = hit.KnockbackGrowth;
            _pendingFacing = hit.FacingSign != 0 ? hit.FacingSign : (_ctx != null ? _ctx.Facing * -1 : 1);
            _pendingHitstop = Mathf.Max(0, hit.HitstopFrames);
            _pendingAttacker = hit.Attacker;
            _hasPending = true;  // có pending CHƯA lên lịch — chờ KnockbackPoint (đòn thường) raise launch.

            // (P16) MỘT animation có thể NHIỀU hit → KHÔNG văng ngay (hit đầu văng thì hit sau trượt).
            // Đòn THƯỜNG chờ Animation Event KnockbackPoint (frame hit cuối) → attacker raise
            // OnKnockbackLaunch (nếu KHÔNG nối) → mới ScheduleApply. NGOẠI LỆ: spike/launcher VĂNG NGAY
            // trên hit (mở juggle / đập xuống, không cần KnockbackPoint, không bao giờ hoãn).
            if (hit.IsSpike || hit.CausesLaunch)
                ScheduleApply();
        }

        /// <summary>Tính knockback + lên lịch áp sau (hitstop+1) fixed-step để hitstop không nuốt cú văng.</summary>
        private void ScheduleApply()
        {
            _applyVelocity = ComputeKnockback();
            _applyDelay = _pendingHitstop + 1;   // chờ qua hết hitstop của hit cuối.
            _hasPending = false;
        }

        private void FixedUpdate()
        {
            // (P16) CHỈ còn 1 nhánh: lịch áp velocity → đếm ngược qua hết hitstop rồi set + vào tumble.
            // Đã bỏ nhánh đếm release-timer (P7) — văng tại KnockbackPoint (Animation Event) thay thế.
            if (_applyDelay < 0) return;
            if (_applyDelay > 0) { _applyDelay--; return; }
            _applyDelay = -1;
            if (_body != null) _body.linearVelocity = _applyVelocity;
            if (knockback != null) knockback.EnterTumble();
        }

        /// <summary>Smash-lite formula + DI. mag = (base + growth×%) × refWeight/mass; hướng theo facing+góc.</summary>
        private Vector2 ComputeKnockback()
        {
            float pct = percent != null ? percent.Current : 0f;
            float mag = _pendingBaseKnockback + _pendingGrowth * pct;

            float refWeight = config != null ? config.referenceWeight : 1f;
            float mass = _body != null ? Mathf.Max(0.1f, _body.mass) : 1f; // clamp mass≥0.1 (chia 0).
            mag *= refWeight / mass;

            // Hướng cơ sở theo facing + launchAngle (độ). facing +1 phải / -1 trái.
            float rad = _pendingLaunchAngle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad) * Mathf.Sign(_pendingFacing == 0 ? 1 : _pendingFacing),
                                      Mathf.Sin(rad));

            dir = ApplyDI(dir.normalized);
            return dir * mag;
        }

        /// <summary>DI digital (research §3.5): input victim xoay vector knockback ±diMaxDegrees, 1 lần.
        /// Input cùng phía vector → xoay nhẹ về phía đó (đổi quỹ đạo để thoát/sống).</summary>
        private Vector2 ApplyDI(Vector2 dir)
        {
            if (_ctx == null || config == null || config.diMaxDegrees <= 0f) return dir;

            Vector2 input = new Vector2(_ctx.MoveX, _ctx.MoveY);
            if (input.sqrMagnitude < 0.04f) return dir; // không ép hướng → không DI.
            input.Normalize();

            // Thành phần input VUÔNG GÓC với vector kb quyết chiều xoay (giống Smash: nghiêng ⟂).
            Vector2 perp = new Vector2(-dir.y, dir.x);          // pháp tuyến trái của dir.
            float side = Mathf.Sign(Vector2.Dot(input, perp));  // input lệch về bên nào của vector.
            float strength = Mathf.Clamp01(Mathf.Abs(Vector2.Dot(input, perp))); // 0..1 độ ⟂.

            float deg = side * config.diMaxDegrees * strength;
            return Rotate(dir, deg);
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
