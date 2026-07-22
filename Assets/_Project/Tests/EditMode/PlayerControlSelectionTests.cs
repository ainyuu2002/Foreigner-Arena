using System;
using System.Reflection;
using NUnit.Framework;
using FA.Core;
using FA.GameFlow;

namespace FA.AI.Tests
{
    public sealed class PlayerControlSelectionTests
    {
        [TestCase(PlayerSlot.P1, false, false)]
        [TestCase(PlayerSlot.P2, true, false)]
        [TestCase(PlayerSlot.P2, false, true)]
        public void CpuSelection_IsOnlyP2WithoutGamepad(
            PlayerSlot slot, bool hasGamepad, bool expected)
        {
            MethodInfo resolver = typeof(PlayerSpawner).GetMethod(
                "ShouldEnableCpu", BindingFlags.Public | BindingFlags.Static);

            Assert.That(resolver, Is.Not.Null);
            bool actual = (bool)resolver.Invoke(null, new object[] { slot, hasGamepad });
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void CoreOwnsGamepadAvailabilityQuery()
        {
            Type availability = typeof(InputSnapshot).Assembly.GetType("FA.Core.LocalInputDevices");

            Assert.That(availability, Is.Not.Null);
            Assert.That(availability.GetProperty("HasGamepad"), Is.Not.Null);
        }
    }
}
