namespace FA.AI
{
    public sealed class CPUUtilityBrain
    {
        private readonly CPUConfigSO config;

        public CPUUtilityBrain(CPUConfigSO config)
        {
            this.config = config;
        }

        public CPUIntent Decide(in CPUPerceptionFrame frame, CPUOpponentProfile profile, float random01)
        {
            if (config == null || frame.SelfInHitstun) return CPUIntent.Wait;
            if (NeedsRecovery(frame)) return CPUIntent.RecoverToStage;
            if (frame.ImmediateDanger) return CPUIntent.EscapeImmediateDanger;
            if (frame.OpponentCommitted && frame.ThreatVisibleFor >= config.reactionDelay &&
                frame.Distance <= config.heavyRange)
                return CPUIntent.Defend;

            if (random01 < config.mistakeChance) return CPUIntent.Wait;

            float antiAirReach = config.mediumRange * (1f + profile.JumpModifier);
            if (!frame.OpponentGrounded && frame.Distance <= antiAirReach)
                return CPUIntent.AntiAir;

            float approachScore = UnityEngine.Mathf.InverseLerp(
                config.preferredRange, config.heavyRange * 2f, frame.Distance) +
                profile.PassiveModifier + profile.SpacingModifier;
            float keepDistanceScore = UnityEngine.Mathf.InverseLerp(
                config.preferredRange, 0f, frame.Distance) + profile.ApproachModifier;
            float lightScore = RangeScore(frame.Distance, config.lightRange * 0.75f, config.lightRange);
            float mediumScore = RangeScore(frame.Distance, config.mediumRange * 0.8f, config.mediumRange);
            float heavyScore = RangeScore(frame.Distance, config.heavyRange * 0.85f, config.heavyRange);

            CPUIntent best = CPUIntent.Wait;
            float bestScore = 0.12f;
            Pick(CPUIntent.Approach, approachScore, ref best, ref bestScore);
            Pick(CPUIntent.KeepDistance, keepDistanceScore, ref best, ref bestScore);
            Pick(CPUIntent.AttackLight, lightScore, ref best, ref bestScore);
            Pick(CPUIntent.AttackMedium, mediumScore, ref best, ref bestScore);
            Pick(CPUIntent.AttackHeavy, heavyScore, ref best, ref bestScore);
            return best;
        }

        private bool NeedsRecovery(in CPUPerceptionFrame frame)
        {
            float horizontal = UnityEngine.Mathf.Abs(frame.SelfPosition.x - frame.StageCenterX);
            bool tooFarSideways = horizontal >= config.recoveryHorizontalDistance;
            bool tooLow = frame.SelfPosition.y < frame.OpponentPosition.y - config.recoveryVerticalOffset;
            return !frame.SelfGrounded && (tooFarSideways || tooLow || !frame.HasSafeGroundBelow);
        }

        private static float RangeScore(float distance, float ideal, float max)
        {
            if (distance > max || max <= 0f) return 0f;
            return 1f - UnityEngine.Mathf.Abs(distance - ideal) / max;
        }

        private static void Pick(CPUIntent candidate, float score, ref CPUIntent best, ref float bestScore)
        {
            if (score <= bestScore) return;
            best = candidate;
            bestScore = score;
        }
    }
}
