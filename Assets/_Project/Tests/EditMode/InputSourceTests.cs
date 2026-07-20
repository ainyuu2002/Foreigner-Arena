using System;
using NUnit.Framework;
using FA.Core;

namespace FA.AI.Tests
{
    public sealed class InputSourceTests
    {
        [Test]
        public void AlternateInputContract_ExposesActivationMoveAndSnapshot()
        {
            Type contract = typeof(InputSnapshot).Assembly.GetType("FA.Core.IInputSnapshotSource");

            Assert.That(contract, Is.Not.Null);
            Assert.That(contract.GetProperty("IsInputActive"), Is.Not.Null);
            Assert.That(contract.GetProperty("Move"), Is.Not.Null);
            Assert.That(contract.GetMethod("ReadSnapshot"), Is.Not.Null);
        }

        [Test]
        public void CpuSnapshots_CreateOneLightPressEdge()
        {
            var buffer = new InputBuffer(8);
            var parser = new CommandParser(buffer);
            var neutral = new InputSnapshot(
                0, 0, false, false, false, false, false, false, false, false, 1);
            var light = new InputSnapshot(
                0, 0, false, false, true, false, false, false, false, false, 2);

            buffer.Push(in neutral);
            buffer.Push(in light);

            Assert.That(parser.WasPressedWithin(InputButton.L, 4), Is.True);
            parser.ConsumePress(InputButton.L, 4);
            Assert.That(parser.WasPressedWithin(InputButton.L, 4), Is.False);
        }
    }
}
