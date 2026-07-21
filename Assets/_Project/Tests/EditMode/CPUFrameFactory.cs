using FA.AI;
using UnityEngine;

namespace FA.AI.Tests
{
    internal static class CPUFrameFactory
    {
        public static CPUPerceptionFrame Create(
            Vector2? self = null,
            Vector2? opponent = null,
            Vector2? selfVelocity = null,
            Vector2? opponentVelocity = null,
            float stageCenterX = 0f,
            float threatVisibleFor = 0f,
            bool selfGrounded = true,
            bool selfInHitstun = false,
            bool opponentGrounded = true,
            bool opponentCommitted = false,
            bool hasSafeGroundBelow = true,
            bool immediateDanger = false)
        {
            return new CPUPerceptionFrame(
                self ?? Vector2.zero,
                opponent ?? new Vector2(2f, 0f),
                selfVelocity ?? Vector2.zero,
                opponentVelocity ?? Vector2.zero,
                stageCenterX,
                threatVisibleFor,
                selfGrounded,
                selfInHitstun,
                opponentGrounded,
                opponentCommitted,
                hasSafeGroundBelow,
                immediateDanger);
        }
    }
}
