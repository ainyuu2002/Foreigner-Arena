using NUnit.Framework;
using UnityEngine;
using FA.AI;

namespace FA.AI.Tests
{
    public sealed class CPUUtilityBrainTests
    {
        private CPUConfigSO config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<CPUConfigSO>();
            config.mistakeChance = 0f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(config);

        [Test]
        public void RecoveryOverridesAttackAndMistakes()
        {
            config.mistakeChance = 1f;
            var brain = new CPUUtilityBrain(config);
            CPUPerceptionFrame frame = CPUFrameFactory.Create(
                self: new Vector2(10f, -3f),
                opponent: Vector2.zero,
                selfGrounded: false,
                hasSafeGroundBelow: false,
                opponentCommitted: true,
                threatVisibleFor: 2f);

            Assert.That(brain.Decide(frame, CPUOpponentProfile.Neutral, 0f),
                Is.EqualTo(CPUIntent.RecoverToStage));
        }

        [Test]
        public void DefenseWaitsForReactionDelay()
        {
            var brain = new CPUUtilityBrain(config);
            CPUPerceptionFrame early = CPUFrameFactory.Create(
                opponent: new Vector2(1f, 0f), opponentCommitted: true, threatVisibleFor: 0.1f);
            CPUPerceptionFrame reacted = CPUFrameFactory.Create(
                opponent: new Vector2(1f, 0f), opponentCommitted: true, threatVisibleFor: 0.3f);

            Assert.That(brain.Decide(early, CPUOpponentProfile.Neutral, 0.5f),
                Is.Not.EqualTo(CPUIntent.Defend));
            Assert.That(brain.Decide(reacted, CPUOpponentProfile.Neutral, 0.5f),
                Is.EqualTo(CPUIntent.Defend));
        }

        [Test]
        public void AirborneOpponentInRangeSelectsAntiAir()
        {
            var brain = new CPUUtilityBrain(config);
            CPUPerceptionFrame frame = CPUFrameFactory.Create(
                opponent: new Vector2(1.4f, 1.5f), opponentGrounded: false);

            Assert.That(brain.Decide(frame, CPUOpponentProfile.Neutral, 0.5f),
                Is.EqualTo(CPUIntent.AntiAir));
        }

        [Test]
        public void ExecutorPulsesNormalsAndNeverEmitsForbiddenButtons()
        {
            var executor = new CPUActionExecutor(config);
            CPUPerceptionFrame frame = CPUFrameFactory.Create();

            CPUHeldInput first = executor.Tick(CPUIntent.AttackHeavy, frame, 0.02f);
            CPUHeldInput second = executor.Tick(CPUIntent.AttackHeavy, frame, 0.02f);
            FA.Core.InputSnapshot snapshot = first.ToSnapshot(1);

            Assert.That(first.H, Is.True);
            Assert.That(second.H, Is.False);
            Assert.That(snapshot.Super1 || snapshot.Super2 || snapshot.Rage, Is.False);
        }

        [Test]
        public void DefendHoldsAwayFromOpponent()
        {
            var executor = new CPUActionExecutor(config);
            CPUPerceptionFrame frame = CPUFrameFactory.Create(opponent: new Vector2(2f, 0f));

            CPUHeldInput input = executor.Tick(CPUIntent.Defend, frame, 0.02f);

            Assert.That(input.DirX, Is.EqualTo(-1));
        }
    }
}
