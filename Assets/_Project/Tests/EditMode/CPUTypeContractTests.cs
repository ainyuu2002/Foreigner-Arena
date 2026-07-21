using System;
using NUnit.Framework;
using FA.AI;

namespace FA.AI.Tests
{
    public sealed class CPUTypeContractTests
    {
        [TestCase("FA.AI.CPUConfigSO")]
        [TestCase("FA.AI.CPUOpponentModel")]
        [TestCase("FA.AI.CPUUtilityBrain")]
        [TestCase("FA.AI.CPUActionExecutor")]
        public void AiModule_ExposesFocusedDecisionTypes(string typeName)
        {
            Type type = Type.GetType($"{typeName}, FA.AI");
            Assert.That(type, Is.Not.Null, typeName);
        }

        [Test]
        public void DecisionTypes_ExposeTestablePureApis()
        {
            Type[] configArgument = { typeof(CPUConfigSO) };

            Assert.That(typeof(CPUOpponentModel).GetConstructor(configArgument), Is.Not.Null);
            Assert.That(typeof(CPUOpponentModel).GetMethod("Observe"), Is.Not.Null);
            Assert.That(typeof(CPUOpponentModel).GetProperty("Profile"), Is.Not.Null);
            Assert.That(typeof(CPUUtilityBrain).GetConstructor(configArgument), Is.Not.Null);
            Assert.That(typeof(CPUUtilityBrain).GetMethod("Decide"), Is.Not.Null);
            Assert.That(typeof(CPUActionExecutor).GetConstructor(configArgument), Is.Not.Null);
            Assert.That(typeof(CPUActionExecutor).GetMethod("Tick"), Is.Not.Null);
        }
    }
}
