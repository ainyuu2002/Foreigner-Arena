using FA.Core;
using UnityEngine;

namespace FA.AI
{
    public enum CPUIntent
    {
        Wait,
        Approach,
        KeepDistance,
        AttackLight,
        AttackMedium,
        AttackHeavy,
        AntiAir,
        Defend,
        EscapeImmediateDanger,
        RecoverToStage
    }

    public readonly struct CPUPerceptionFrame
    {
        public readonly Vector2 SelfPosition;
        public readonly Vector2 OpponentPosition;
        public readonly Vector2 SelfVelocity;
        public readonly Vector2 OpponentVelocity;
        public readonly float StageCenterX;
        public readonly float ThreatVisibleFor;
        public readonly bool SelfGrounded;
        public readonly bool SelfInHitstun;
        public readonly bool OpponentGrounded;
        public readonly bool OpponentCommitted;
        public readonly bool HasSafeGroundBelow;
        public readonly bool ImmediateDanger;

        public CPUPerceptionFrame(
            Vector2 selfPosition, Vector2 opponentPosition,
            Vector2 selfVelocity, Vector2 opponentVelocity,
            float stageCenterX, float threatVisibleFor,
            bool selfGrounded, bool selfInHitstun,
            bool opponentGrounded, bool opponentCommitted,
            bool hasSafeGroundBelow, bool immediateDanger)
        {
            SelfPosition = selfPosition;
            OpponentPosition = opponentPosition;
            SelfVelocity = selfVelocity;
            OpponentVelocity = opponentVelocity;
            StageCenterX = stageCenterX;
            ThreatVisibleFor = threatVisibleFor;
            SelfGrounded = selfGrounded;
            SelfInHitstun = selfInHitstun;
            OpponentGrounded = opponentGrounded;
            OpponentCommitted = opponentCommitted;
            HasSafeGroundBelow = hasSafeGroundBelow;
            ImmediateDanger = immediateDanger;
        }

        public float HorizontalDelta => OpponentPosition.x - SelfPosition.x;
        public float Distance => Vector2.Distance(SelfPosition, OpponentPosition);
        public int DirectionToOpponent => HorizontalDelta >= 0f ? 1 : -1;
        public int DirectionToStage => StageCenterX >= SelfPosition.x ? 1 : -1;
        public bool OpponentMovingTowardSelf => OpponentVelocity.x * HorizontalDelta < 0f;
        public bool OpponentPassive => OpponentGrounded && Mathf.Abs(OpponentVelocity.x) < 0.1f && !OpponentCommitted;
    }

    public readonly struct CPUOpponentProfile
    {
        public static readonly CPUOpponentProfile Neutral = new CPUOpponentProfile(0f, 0f, 0f, 0f);

        public readonly float JumpModifier;
        public readonly float ApproachModifier;
        public readonly float PassiveModifier;
        public readonly float SpacingModifier;

        public CPUOpponentProfile(float jump, float approach, float passive, float spacing)
        {
            JumpModifier = jump;
            ApproachModifier = approach;
            PassiveModifier = passive;
            SpacingModifier = spacing;
        }
    }

    public readonly struct CPUHeldInput
    {
        public static readonly CPUHeldInput Neutral = new CPUHeldInput(0, 0, false, false, false, false, false);

        public readonly sbyte DirX;
        public readonly sbyte DirY;
        public readonly bool Jump;
        public readonly bool Dash;
        public readonly bool L;
        public readonly bool M;
        public readonly bool H;

        public CPUHeldInput(sbyte dirX, sbyte dirY, bool jump, bool dash, bool l, bool m, bool h)
        {
            DirX = dirX;
            DirY = dirY;
            Jump = jump;
            Dash = dash;
            L = l;
            M = m;
            H = h;
        }

        public InputSnapshot ToSnapshot(int frame) =>
            new InputSnapshot(DirX, DirY, Jump, Dash, L, M, H, false, false, false, frame);
    }
}
