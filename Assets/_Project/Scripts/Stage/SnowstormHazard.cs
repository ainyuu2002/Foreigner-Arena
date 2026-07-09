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
            if (config == null || !stormActive) return;
            foreach (var rb in ResolveBodies())
            {
                if (rb == null) continue;
                Vector2 v = rb.linearVelocity;
                if (_windDir * v.x < config.snowstormWindMaxCarry)
                    v.x += _windDir * config.snowstormWindSpeed;
                rb.linearVelocity = v;
            }
        }

        private void ToggleStorm()
        {
            stormActive = !stormActive;
            if (stormActive)
            {
                _windDir = Random.value < 0.5f ? -1 : 1; // hướng gió random mỗi trận bão.
                Debug.Log($"[SnowstormHazard] Bão tuyết BẮT ĐẦU — gió thổi sang {(_windDir > 0 ? "PHẢI" : "TRÁI")}.", this);
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
                var results = new List<Collider2D>(8);
                stormArea.Overlap(filter, results);
                foreach (var c in results)
                    if (c.attachedRigidbody != null && !_scratch.Contains(c.attachedRigidbody))
                        _scratch.Add(c.attachedRigidbody);
            }
            foreach (var rb in _scratch) yield return rb;
        }

        private bool InArea(Rigidbody2D rb)
        {
            if (rb == null) return false;
            if (stormArea == null) return true;
            return stormArea.OverlapPoint(rb.position);
        }
    }
}
