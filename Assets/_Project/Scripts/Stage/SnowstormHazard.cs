using System.Collections.Generic;
using UnityEngine;
using FA.Core;

namespace FA.Stage
{
    /// <summary>
    /// BÃO TUYẾT (P11, biome Ice, research §3.4) — hazard NGẪU NHIÊN: bật theo chu kỳ random; trong lúc bão
    /// (a) làm CHẬM vận tốc ngang + (b) ĐẨY xung lực ngang ngẫu nhiên trái/phải định kỳ lên CẢ 2 player
    /// ('đẩy random phá combo là CHỦ Ý' — GDD). Visual = ParticleSystem tuyết bật/tắt theo trạng thái bão.
    ///
    /// Unity-native lo: lực/vận tốc qua <c>Rigidbody2D.AddForce</c>/velocity (KHÔNG tự tích phân §1.1);
    /// hạt qua ParticleSystem. Code chỉ điều phối CHU KỲ + chọn target. KHÔNG ref module khác — chỉ chạm
    /// Rigidbody2D (Unity) tìm theo Layer Player (GameLayers — Core). Số liệu ở StageConfigSO (CẦN TEST).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)] // chạy SAU CharacterMotor (order 0): state ghi vx trước, gió cộng đè sau — nếu không gió bị motor nuốt ở state thường.
    public sealed class SnowstormHazard : MonoBehaviour
    {
        [Header("Config")]
        [Tooltip("StageConfigSO của map Ice — interval/duration/jitter/nudgeInterval/pushForce/slowFactor. BẮT BUỘC.")]
        [SerializeField] private StageConfigSO config;

        [Header("Targets")]
        [Tooltip("Rigidbody2D 2 player chịu bão. Bỏ trống = tự tìm theo playerLayers trong vùng overlapArea.")]
        [SerializeField] private List<Rigidbody2D> affectedBodies = new List<Rigidbody2D>();

        [Tooltip("Layer player để auto-tìm nếu affectedBodies trống.")]
        [SerializeField] private LayerMask playerLayers;

        [Tooltip("Vùng bão (BoxCollider2D trigger) — chỉ ảnh hưởng player trong vùng. Bỏ trống = toàn map (dùng affectedBodies).")]
        [SerializeField] private BoxCollider2D stormArea;

        [Header("Visual")]
        [Tooltip("ParticleSystem tuyết — Play khi bão, Stop khi tạnh. Tùy chọn.")]
        [SerializeField] private ParticleSystem snowParticles;

        [Tooltip("ParticleSystem tuyết RƠI NỀN (luôn bật) — bão tới thì hạt NGHIÊNG THEO GIÓ, tạnh thì " +
                 "rơi thẳng lại (feedback test: 'gió thổi nhưng tuyết sẵn có vẫn rơi thẳng'). Tùy chọn.")]
        [SerializeField] private ParticleSystem ambientSnow;

        [Tooltip("Vận tốc ngang hạt tuyết nền khi TRỜI LẶNG (world/giây — số visual, không phải gameplay).")]
        [SerializeField] private float ambientCalmVelX = -0.4f;

        [Tooltip("Vận tốc ngang hạt tuyết nền khi CÓ BÃO (min..max, nhân hướng gió — visual).")]
        [SerializeField] private Vector2 ambientStormVelX = new Vector2(4f, 7f);

        [Header("Trạng thái (đọc-only)")]
        [SerializeField] private bool stormActive;

        private float _phaseTimer;       // đếm tới lần đổi trạng thái kế (bão↔tạnh).
        private int _windDir = 1;         // hướng gió trận bão hiện tại: -1 trái / +1 phải (random mỗi trận).
        private readonly List<Rigidbody2D> _scratch = new List<Rigidbody2D>(4);

        public bool StormActive => stormActive;
        public int WindDirection => _windDir;

        private void Awake()
        {
            if (config == null)
                Debug.LogError("[SnowstormHazard] Thiếu StageConfigSO.", this);
            ScheduleNextPhase(false); // bắt đầu ở trạng thái tạnh, chờ tới interval.
            if (snowParticles != null) snowParticles.Stop();
        }

        private void Update()
        {
            if (config == null) return;

            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer <= 0f)
                ToggleStorm();
        }

        /// <summary>
        /// GIÓ CÓ HƯỚNG (feedback test): cộng windSpeed vào vx mỗi FixedUpdate. Motor ghi lại vx mỗi tick
        /// (Idle=0, Walk=±walkSpeed) nên hiệu quả ổn định: đứng yên TRÔI theo gió, đi ngược gió CHẬM đi,
        /// đi theo gió NHANH lên. Trần WindMaxCarry chặn cộng dồn vô hạn khi bay knockback (gió chỉ 'cuốn'
        /// tới trần rồi thôi).
        /// </summary>
        private void FixedUpdate()
        {
            // (Fix gió-đè-intro) CinematicGate đang mở (intro/chant/super-flash — RoundIntroController và
            // các cinematic khác Acquire) → mọi hazard môi trường đứng yên, không đè lực lên nhân vật đang
            // bị khoá diễn cảnh.
            if (config == null || !stormActive || CinematicGate.IsActive) return;
            foreach (var rb in ResolveBodies())
            {
                if (rb == null) continue;

                // BẤT TỬ hồi sinh (IInvulnerable — Core §1.3): môi trường bỏ qua hoàn toàn (feedback
                // test: vừa hồi sinh đã bị gió đẩy).
                var inv = rb.GetComponent<IInvulnerable>();
                if (inv != null && inv.IsInvulnerable) continue;

                // Victim đang bị knockback/hitstun (Hurt/tumble/splat đều set cờ này qua ICombatContext
                // — interface Core, không ref FA.Combat §1.3): gió nương tay theo KnockedFactor.
                // Feedback test: knock trong bão mà gió full lực = trượt thẳng khỏi map, chết chắc.
                float windScale = 1f;
                var ctx = rb.GetComponent<ICombatContext>();
                if (ctx != null && ctx.IsInHitstun)
                {
                    // ĐANG NẰM/TRƯỢT TRÊN ĐẤT (|vy| ~ 0): gió BỎ QUA hẳn — nếu không, gió giữ tốc độ
                    // trượt > tumbleEndSpeed khiến victim không bao giờ "hết đà" → thời gian nằm bị
                    // kéo dài tới trần thay vì dậy ngay như lúc lặng gió (feedback test).
                    if (Mathf.Abs(rb.linearVelocity.y) < 1f) continue;
                    windScale = config.snowstormWindKnockedFactor; // đang BAY: gió còn KnockedFactor.
                    if (windScale <= 0f) continue;
                }

                Vector2 v = rb.linearVelocity;
                if (_windDir * v.x < config.snowstormWindMaxCarry)
                    v.x += _windDir * config.snowstormWindSpeed * windScale;
                rb.linearVelocity = v;
            }
        }

        private void ToggleStorm()
        {
            stormActive = !stormActive;
            if (stormActive)
            {
                _windDir = Random.value < 0.5f ? -1 : 1; // hướng gió random mỗi trận bão.
                DebugLog.Log($"[SnowstormHazard] Bão tuyết BẮT ĐẦU — gió thổi sang {(_windDir > 0 ? "PHẢI" : "TRÁI")}.", this);
            }
            if (snowParticles != null)
            {
                if (stormActive)
                {
                    // Hạt tuyết bay theo hướng gió cho khớp cảm giác.
                    var vel = snowParticles.velocityOverLifetime;
                    vel.x = new ParticleSystem.MinMaxCurve(_windDir * 9f, _windDir * 15f);
                    snowParticles.Play();
                }
                else snowParticles.Stop();
            }
            if (ambientSnow != null)
            {
                // TUYẾT NỀN cũng nghiêng theo gió khi bão / rơi thẳng lại khi tạnh (feedback test).
                // Hạt cũ trên trời giữ vận tốc cũ, hạt mới sinh ăn vận tốc mới → chuyển mượt tự nhiên.
                // LƯU Ý: giữ mode TwoConstants cho cả 3 trục (Unity bắt cùng mode — lỗi cũ P11).
                var avel = ambientSnow.velocityOverLifetime;
                avel.x = stormActive
                    ? new ParticleSystem.MinMaxCurve(_windDir * ambientStormVelX.x, _windDir * ambientStormVelX.y)
                    : new ParticleSystem.MinMaxCurve(ambientCalmVelX, ambientCalmVelX);
            }
            ScheduleNextPhase(stormActive);
        }

        private void ScheduleNextPhase(bool currentlyActive)
        {
            float baseDur = currentlyActive ? config.snowstormDuration : config.snowstormInterval;
            float jitter = Random.Range(-config.snowstormRandomJitter, config.snowstormRandomJitter);
            _phaseTimer = Mathf.Max(0.5f, baseDur + jitter);
        }

        private IEnumerable<Rigidbody2D> ResolveBodies()
        {
            if (affectedBodies.Count > 0)
            {
                foreach (var rb in affectedBodies)
                    if (InArea(rb)) yield return rb;
                yield break;
            }

            // Auto-tìm theo layer trong stormArea (hoặc toàn map nếu không có area).
            _scratch.Clear();
            if (stormArea != null)
            {
                var filter = new ContactFilter2D { useLayerMask = true, layerMask = playerLayers, useTriggers = true };
                _overlapScratch.Clear(); // list CACHE — new List mỗi FixedUpdate là rác GC → giật định kỳ.
                stormArea.Overlap(filter, _overlapScratch);
                foreach (var c in _overlapScratch)
                    if (c.attachedRigidbody != null && !_scratch.Contains(c.attachedRigidbody))
                        _scratch.Add(c.attachedRigidbody);
            }
            foreach (var rb in _scratch) yield return rb;
        }

        private readonly List<Collider2D> _overlapScratch = new List<Collider2D>(8);

        private bool InArea(Rigidbody2D rb)
        {
            if (rb == null) return false;
            if (stormArea == null) return true;
            return stormArea.OverlapPoint(rb.position);
        }
    }
}
