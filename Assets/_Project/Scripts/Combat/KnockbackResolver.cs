using UnityEngine;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// BỘ ÁP LỰC knockback (P7, research §3.2 — ĐIỂM KHÓ NHẤT) trên VICTIM. GDD §3.6: trong combo CHỈ
    /// tích % + hurtstun nhẹ (HurtState giữ nguyên P6), lực bay xa CHỈ ở CUỐI combo. Cơ chế per-victim:
    ///
    ///  1. Mỗi OnHitConfirmed (mình là victim) → LƯU pending knockback từ hit MỚI NHẤT (override hit trước
    ///     — đòn cuối quyết hướng/lực) + reset <c>_framesSinceLastHit</c>.
    ///  2. FixedUpdate đếm <c>_framesSinceLastHit</c>; ≥ <c>knockbackReleaseFrames</c> (≥ comboReset của
    ///     attacker) → "combo coi như kết thúc" → APPLY pending. Tách khỏi ComboCounter (ít coupling).
    ///  3. Nếu hit là SPIKE hoặc <see cref="HitData.CausesLaunch"/> (đòn KẾT/launcher, mang từ
    ///     <see cref="MoveData.causesLaunch"/>) → apply NGAY (không chờ release-timer).
    ///
    /// Formula Smash-lite (research §2): <c>mag = (base + growth×%victim) × refWeight/mass</c>; hướng theo
    /// facing + launchAngle; áp DI ±diMaxDegrees (digital, 1 lần lúc apply); set <see cref="Rigidbody2D.linearVelocity"/>.
    /// Apply SAU hitstop (research §4 cạm bẫy 2): release-timer ≥ comboReset luôn rơi sau hitstop; nhưng để
    /// chắc, dùng <c>_applyPending</c> áp ở FixedTick (giống HurtState) — KHÔNG set lúc còn hitstop.
    ///
    /// Đặt FA.Combat, chỉ ref Core. Đọc % từ KnockbackPercent (cùng module), weight từ Rigidbody2D.mass
    /// (KHÔNG đọc CharacterData → cross-module §1.3). Vào tumble qua KnockbackController.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KnockbackResolver : MonoBehaviour, IPendingKnockback, IKnockbackCanceler
    {
        [Header("Kênh sự kiện")]
        [Tooltip("OnHitConfirmed (HitEventChannelSO) — CÙNG asset HitReactionController raise (trúng-không-block).")]
        [SerializeField] private HitEventChannelSO onHitConfirmed;

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
        private int _framesSinceLastHit;
        private float _pendingLaunchAngle;
        private float _pendingBaseKnockback;
        private float _pendingGrowth;
        private int _pendingFacing;
        private bool _pendingSpike;
        private bool _pendingCausesLaunch;   // đòn KẾT/launcher (MoveData.causesLaunch) → bay NGAY.
        private int _pendingHitstop;   // hitstop của hit cuối → delay apply để không bị hitstop nuốt.

        /// <summary>True khi còn knockback CHỜ áp trên victim này (đã ăn hit nhưng release-timer/launch
        /// chưa apply). HurtState hỏi cờ này để KHÔNG tự về Idle trước khi bị bốc vào tumble
        /// (neutral-gap fix). Còn pending khi đang đếm release HOẶC đang chờ _applyDelay.</summary>
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
        }

        private void OnDisable()
        {
            if (onHitConfirmed != null) onHitConfirmed.OnEventRaised -= OnHitConfirmed;
        }

        /// <summary>(P10 ADD — IKnockbackCanceler) Hủy MỌI knockback đang chờ trên victim này. BurstState
        /// gọi khi thoát combo bằng burst → không bị bay theo cú đánh cuối (research §4 cạm bẫy 3).
        /// CHỈ reset cờ pending/lịch — KHÔNG đụng logic resolve (ADD tối thiểu, không phá P7).</summary>
        public void CancelPending()
        {
            _hasPending = false;
            _framesSinceLastHit = 0;
            _applyDelay = -1;
        }

        private void OnHitConfirmed(HitData hit)
        {
            GameObject me = root != null ? root.gameObject : gameObject;
            if (!ReferenceEquals(hit.Victim, me)) return;

            // Lưu pending từ hit MỚI NHẤT (override — đòn cuối quyết hướng/lực).
            _pendingLaunchAngle = hit.LaunchAngle;
            _pendingBaseKnockback = hit.BaseKnockback;
            _pendingGrowth = hit.KnockbackGrowth;
            _pendingFacing = hit.FacingSign != 0 ? hit.FacingSign : (_ctx != null ? _ctx.Facing * -1 : 1);
            _pendingSpike = hit.IsSpike;
            _pendingCausesLaunch = hit.CausesLaunch;
            _pendingHitstop = Mathf.Max(0, hit.HitstopFrames);
            _hasPending = true;
            _framesSinceLastHit = 0;
            _applyDelay = -1; // hit MỚI override: hủy lịch áp cũ (đòn cuối quyết) — tránh launch giữa combo.

            // LAUNCH NGAY (không chờ release-timer) nếu SPIKE hoặc đòn KẾT/launcher (MoveData.causesLaunch
            // → HitData.CausesLaunch). Spike: đập thẳng xuống cần văng victim xuống sàn ngay để
            // ground-bounce (research §3.6). Launcher: đòn-kết ép vào tumble NGAY (GDD §3.6 "đòn kết bốc
            // bay"). Đòn thường khác (cả 2 cờ false) → release-timer lo "bay sau combo".
            if (_pendingSpike || _pendingCausesLaunch) ScheduleApply();
        }

        /// <summary>Tính knockback + lên lịch áp sau (hitstop+1) fixed-step để hitstop không nuốt cú launch.</summary>
        private void ScheduleApply()
        {
            _applyVelocity = ComputeKnockback();
            _applyDelay = _pendingHitstop + 1;   // chờ qua hết hitstop của hit cuối.
            _hasPending = false;
            _framesSinceLastHit = 0;
        }

        private void FixedUpdate()
        {
            // Lịch áp velocity đang chạy → đếm ngược qua hết hitstop rồi set + vào tumble.
            if (_applyDelay >= 0)
            {
                if (_applyDelay > 0) { _applyDelay--; return; }
                _applyDelay = -1;
                if (_body != null) _body.linearVelocity = _applyVelocity;
                if (knockback != null) knockback.EnterTumble();
                return;
            }

            if (!_hasPending) return;

            _framesSinceLastHit++;
            int release = config != null ? config.knockbackReleaseFrames : 26;
            if (_framesSinceLastHit < release) return;

            // Combo coi như KẾT THÚC → tính & lên lịch áp lực (release-timer đã > hitstop, delay ~0 vẫn an toàn).
            ScheduleApply();
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
