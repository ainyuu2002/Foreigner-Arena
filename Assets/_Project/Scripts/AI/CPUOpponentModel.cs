namespace FA.AI
{
    public sealed class CPUOpponentModel
    {
        private readonly CPUConfigSO config;
        private float jumpSample = 0.5f;
        private float approachSample = 0.5f;
        private float passiveSample = 0.5f;
        private float spacingSample = 0.5f;

        public CPUOpponentModel(CPUConfigSO config)
        {
            this.config = config;
        }

        public CPUOpponentProfile Profile => new CPUOpponentProfile(
            Modifier(jumpSample), Modifier(approachSample),
            Modifier(passiveSample), Modifier(spacingSample));

        public void Observe(in CPUPerceptionFrame frame, float dt)
        {
            if (config == null || dt <= 0f) return;

            float decay = config.observationDecayPerSecond * dt;
            jumpSample = Decay(jumpSample, decay);
            approachSample = Decay(approachSample, decay);
            passiveSample = Decay(passiveSample, decay);
            spacingSample = Decay(spacingSample, decay);

            float blend = UnityEngine.Mathf.Clamp01(config.observationRate * dt);
            jumpSample = UnityEngine.Mathf.Lerp(jumpSample, frame.OpponentGrounded ? 0f : 1f, blend);
            approachSample = UnityEngine.Mathf.Lerp(
                approachSample, frame.OpponentMovingTowardSelf ? 1f : 0f, blend);
            passiveSample = UnityEngine.Mathf.Lerp(passiveSample, frame.OpponentPassive ? 1f : 0f, blend);
            spacingSample = UnityEngine.Mathf.Lerp(
                spacingSample, frame.Distance > config.preferredRange ? 1f : 0f, blend);
        }

        private static float Decay(float sample, float amount) =>
            UnityEngine.Mathf.MoveTowards(sample, 0.5f, amount);

        private float Modifier(float sample)
        {
            float strength = config != null ? config.adaptationStrength : 0f;
            float limit = config != null ? config.adaptationClamp : 0f;
            return UnityEngine.Mathf.Clamp((sample - 0.5f) * 2f * strength, -limit, limit);
        }
    }
}
