using UnityEngine;

namespace FA.Items
{
    /// <summary>Điều khiển aura, phản hồi trúng đòn và hiệu ứng vỡ của Rage Orb.</summary>
    [DisallowMultipleComponent]
    public sealed class RageOrbVfx : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer glow;
        [SerializeField] private ParticleSystem ambientAura;
        [SerializeField] private ParticleSystem orbitingSparks;
        [SerializeField] private ParticleSystem hitBurst;
        [SerializeField] private ParticleSystem breakBurst;

        private float _progress;
        private Vector3 _glowBaseScale;

        private void Awake()
        {
            if (glow != null) _glowBaseScale = glow.transform.localScale;
        }

        private void Update()
        {
            if (glow == null) return;
            float speed = Mathf.Lerp(2.8f, 6.5f, _progress);
            float amount = Mathf.Lerp(0.04f, 0.11f, _progress);
            glow.transform.localScale = _glowBaseScale * (1f + Mathf.Sin(Time.time * speed) * amount);
            Color color = Color.Lerp(new Color(0.55f, 0.12f, 1f, 0.18f),
                                     new Color(1f, 0.16f, 0.05f, 0.38f), _progress);
            glow.color = color;
        }

        public void SetProgress(float progress)
        {
            _progress = Mathf.Clamp01(progress);
            if (ambientAura != null)
            {
                var emission = ambientAura.emission;
                emission.rateOverTime = Mathf.Lerp(14f, 34f, _progress);
                var main = ambientAura.main;
                main.startColor = Color.Lerp(new Color(0.5f, 0.08f, 1f, 0.62f),
                                             new Color(1f, 0.12f, 0.03f, 0.9f), _progress);
            }
            if (orbitingSparks != null)
            {
                var emission = orbitingSparks.emission;
                emission.rateOverTime = Mathf.Lerp(5f, 16f, _progress);
            }
        }

        public void PlayHit(float progress)
        {
            SetProgress(progress);
            if (hitBurst != null) hitBurst.Emit(Mathf.RoundToInt(Mathf.Lerp(7f, 15f, _progress)));
        }

        public void PlayBreak()
        {
            if (ambientAura != null) ambientAura.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (orbitingSparks != null) orbitingSparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (hitBurst != null) hitBurst.Emit(18);
            if (breakBurst != null) breakBurst.Emit(42);
            if (glow != null) glow.color = new Color(1f, 0.35f, 0.08f, 0.7f);
        }

        public void ResetVfx()
        {
            _progress = 0f;
            RestartLoop(ambientAura);
            RestartLoop(orbitingSparks);
            Clear(hitBurst);
            Clear(breakBurst);
            if (glow != null)
            {
                glow.transform.localScale = _glowBaseScale;
                glow.color = new Color(0.55f, 0.12f, 1f, 0.18f);
            }
            SetProgress(0f);
        }

        public void StopVfx()
        {
            Stop(ambientAura);
            Stop(orbitingSparks);
            Stop(hitBurst);
            Stop(breakBurst);
        }

        private static void RestartLoop(ParticleSystem particles)
        {
            if (particles == null) return;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(true);
        }

        private static void Clear(ParticleSystem particles)
        {
            if (particles == null) return;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void Stop(ParticleSystem particles)
        {
            if (particles == null) return;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
