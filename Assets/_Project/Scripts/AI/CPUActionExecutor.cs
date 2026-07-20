namespace FA.AI
{
    public sealed class CPUActionExecutor
    {
        private readonly CPUConfigSO config;
        private float attackCooldown;
        private float jumpCooldown;
        private float dashCooldown;

        public CPUActionExecutor(CPUConfigSO config)
        {
            this.config = config;
        }

        public CPUHeldInput Tick(CPUIntent intent, in CPUPerceptionFrame frame, float dt)
        {
            attackCooldown = UnityEngine.Mathf.Max(0f, attackCooldown - dt);
            jumpCooldown = UnityEngine.Mathf.Max(0f, jumpCooldown - dt);
            dashCooldown = UnityEngine.Mathf.Max(0f, dashCooldown - dt);

            switch (intent)
            {
                case CPUIntent.Approach:
                    return Pursue(frame);
                case CPUIntent.KeepDistance:
                case CPUIntent.Defend:
                    return Direction(-frame.DirectionToOpponent);
                case CPUIntent.AttackLight:
                    return Attack(frame, InputKind.Light);
                case CPUIntent.AttackMedium:
                    return Attack(frame, InputKind.Medium);
                case CPUIntent.AttackHeavy:
                    return Attack(frame, InputKind.Heavy);
                case CPUIntent.AntiAir:
                    return ShouldPursueVertically(frame) ? Pursue(frame) : Attack(frame, InputKind.Heavy);
                case CPUIntent.RecoverToStage:
                    return Recover(frame);
                case CPUIntent.EscapeImmediateDanger:
                    return Escape(frame);
                default:
                    return CPUHeldInput.Neutral;
            }
        }

        private CPUHeldInput Attack(in CPUPerceptionFrame frame, InputKind kind)
        {
            if (attackCooldown > 0f) return CPUHeldInput.Neutral;
            attackCooldown = config != null ? config.commitmentDuration : 0.28f;
            return new CPUHeldInput(0, 0, false, false,
                kind == InputKind.Light, kind == InputKind.Medium, kind == InputKind.Heavy);
        }

        private CPUHeldInput Recover(in CPUPerceptionFrame frame)
        {
            sbyte direction = Sign(frame.DirectionToStage);
            bool jump = false;
            bool dash = false;

            if (!frame.SelfGrounded && jumpCooldown <= 0f)
            {
                jump = true;
                jumpCooldown = config != null ? config.recoveryJumpCooldown : 0.28f;
            }
            else if (!frame.SelfGrounded && frame.Distance > (config != null ? config.preferredRange : 2.4f) &&
                     dashCooldown <= 0f)
            {
                dash = true;
                dashCooldown = config != null ? config.recoveryDashCooldown : 0.42f;
            }

            return new CPUHeldInput(direction, 0, jump, dash, false, false, false);
        }

        private CPUHeldInput Pursue(in CPUPerceptionFrame frame)
        {
            bool jump = ShouldPursueVertically(frame) && jumpCooldown <= 0f;
            if (jump)
                jumpCooldown = config != null ? config.pursuitJumpCooldown : 0.35f;
            return new CPUHeldInput(
                Sign(frame.DirectionToOpponent), 0, jump, false, false, false, false);
        }

        private bool ShouldPursueVertically(in CPUPerceptionFrame frame)
        {
            float threshold = config != null ? config.verticalPursuitHeight : 1.4f;
            return frame.OpponentPosition.y - frame.SelfPosition.y >= threshold;
        }

        private CPUHeldInput Escape(in CPUPerceptionFrame frame)
        {
            bool jump = jumpCooldown <= 0f;
            if (jump) jumpCooldown = config != null ? config.recoveryJumpCooldown : 0.28f;
            return new CPUHeldInput(Sign(frame.DirectionToStage), 0, jump, false, false, false, false);
        }

        private static CPUHeldInput Direction(int direction) =>
            new CPUHeldInput(Sign(direction), 0, false, false, false, false, false);

        private static sbyte Sign(int direction) => (sbyte)(direction >= 0 ? 1 : -1);

        private enum InputKind { Light, Medium, Heavy }
    }
}
