using NUnit.Framework;
using UnityEngine;
using FA.AI;

namespace FA.AI.Tests
{
    public sealed class CPUOpponentModelTests
    {
        [Test]
        public void Observe_ClampsAdaptiveModifiers()
        {
            CPUConfigSO config = ScriptableObject.CreateInstance<CPUConfigSO>();
            config.observationRate = 10f;
            config.observationDecayPerSecond = 0f;
            config.adaptationStrength = 1f;
            config.adaptationClamp = 0.2f;
            var model = new CPUOpponentModel(config);
            CPUPerceptionFrame frame = CPUFrameFactory.Create(
                opponent: new Vector2(5f, 2f),
                opponentVelocity: new Vector2(-3f, 0f),
                opponentGrounded: false);

            for (int i = 0; i < 20; i++) model.Observe(frame, 0.1f);

            CPUOpponentProfile profile = model.Profile;
            Assert.That(profile.JumpModifier, Is.InRange(0.01f, 0.2f));
            Assert.That(profile.ApproachModifier, Is.InRange(0.01f, 0.2f));
            Assert.That(profile.SpacingModifier, Is.InRange(0.01f, 0.2f));
            Object.DestroyImmediate(config);
        }

        [Test]
        public void NewModel_StartsWithNeutralProfile()
        {
            CPUConfigSO config = ScriptableObject.CreateInstance<CPUConfigSO>();
            var model = new CPUOpponentModel(config);

            CPUOpponentProfile profile = model.Profile;

            Assert.That(profile.JumpModifier, Is.Zero);
            Assert.That(profile.ApproachModifier, Is.Zero);
            Assert.That(profile.PassiveModifier, Is.Zero);
            Assert.That(profile.SpacingModifier, Is.Zero);
            Object.DestroyImmediate(config);
        }
    }
}
