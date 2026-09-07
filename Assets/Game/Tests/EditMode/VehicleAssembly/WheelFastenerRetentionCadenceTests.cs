using System;
using System.Linq;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class WheelFastenerRetentionCadenceTests
    {
        [Test]
        public void FiftyPhysicsTicksBeforeOneRealtimeSecondProduceOneAttempt()
        {
            using var fixture = new GroupFixture(16);
            int attempts = 0;
            for (int tick = 0; tick < 50; tick++)
            {
                if (fixture.Group.ShouldEvaluateSpeedRetention(50f, tick * 0.02)) attempts++;
            }
            Assert.That(attempts, Is.EqualTo(1), "The first eligible attempt is immediate.");
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 1.0), Is.True);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 1.0), Is.False);
        }

        [TestCase(0, 0f, false)]
        [TestCase(0, 5f, false)]
        [TestCase(0, 5.001f, true)]
        [TestCase(1, 5.001f, false)]
        [TestCase(1, 33f, false)]
        [TestCase(1, 33.001f, true)]
        [TestCase(32, 33f, false)]
        [TestCase(32, 33.001f, true)]
        public void FirstEligibilityUsesUnchangedStrictSpeedBranches(int tightness, float speed, bool expected)
        {
            using var fixture = new GroupFixture(tightness);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(speed, 0.0), Is.EqualTo(expected));
        }

        [Test]
        public void AllFourCornersHaveIndependentDeadlines()
        {
            using var fl = new GroupFixture(16);
            using var fr = new GroupFixture(16);
            using var rl = new GroupFixture(16);
            using var rr = new GroupFixture(16);
            GroupFixture[] corners = { fl, fr, rl, rr };
            for (int i = 0; i < corners.Length; i++)
                Assert.That(corners[i].Group.ShouldEvaluateSpeedRetention(50f, 10.0 + i * 0.25), Is.True);
            for (int i = 0; i < corners.Length; i++)
                Assert.That(corners[i].Group.ShouldEvaluateSpeedRetention(50f, 10.99), Is.False);
            for (int i = 0; i < corners.Length; i++)
            {
                double now = 11.0 + i * 0.25;
                for (int corner = 0; corner < corners.Length; corner++)
                    Assert.That(corners[corner].Group.ShouldEvaluateSpeedRetention(50f, now),
                        Is.EqualTo(corner == i), $"corner {corner}, deadline {now}");
            }
        }

        [Test]
        public void SpeedDipDoesNotCancelWaitOrStartAnotherDelayAfterExpiry()
        {
            using var fixture = new GroupFixture(16);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 10.0), Is.True);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(0f, 10.5), Is.False);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 10.9), Is.False);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(0f, 11.2), Is.False);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 11.3), Is.True);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 11.31), Is.False);
        }

        [Test]
        public void RealtimePauseGapProducesOneNewAttemptWithoutCatchup()
        {
            using var fixture = new GroupFixture(16);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 2.0), Is.True);
            // No scaled delta/tick count is involved, only the caller's realtime.
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 200.0), Is.True);
            for (int i = 0; i < 50; i++)
                Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 200.0 + i * 0.01), Is.False);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 201.0), Is.True);
        }

        [Test]
        public void ZeroTightnessCannotInterruptActiveWaitButOutsideWaitIsImmediate()
        {
            using var fixture = new GroupFixture(16);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 3.0), Is.True);
            fixture.SetTightness(0);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 3.9), Is.False);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(5f, 4.0), Is.False);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(6f, 4.01), Is.True);
            Assert.That(fixture.Group.ShouldBreak(6f, 0.999f), Is.True);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(6f, 4.01), Is.True,
                "OFF does not create a cooldown; its caller immediately detaches the wheel.");
        }

        [Test]
        public void FullyTightenedWheelStillVisitsZeroRiskChanceAndPreservesWaitWhenLoosened()
        {
            using var fixture = new GroupFixture(16);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 3.0), Is.True);
            fixture.SetTightness(32);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 3.9), Is.False);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 4.0), Is.True);
            Assert.That(fixture.Group.ShouldBreak(50f, 0f), Is.False);
            fixture.SetTightness(1);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 4.9), Is.False);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 5.0), Is.True);
        }

        [Test]
        public void ScheduleResetPreservesStagesAndLatchWhileGroupResetClearsLatch()
        {
            using var fixture = new GroupFixture(16);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 10.0), Is.True);
            fixture.Group.ResetSpeedRetentionSchedule();
            Assert.That(fixture.Group.Tightness, Is.EqualTo(16));
            Assert.That(fixture.Group.IsBolted, Is.True);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 10.1), Is.True);
            fixture.Group.Reset();
            Assert.That(fixture.Group.IsBolted, Is.False);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 10.2), Is.True);
        }

        [Test]
        public void InvalidClockAndSpeedDoNotPoisonNextValidAttempt()
        {
            using var fixture = new GroupFixture(16);
            foreach (double now in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1.0 })
                Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, now), Is.False);
            foreach (float speed in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(speed, 0.0), Is.False);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(50f, 0.0), Is.True);
        }

        [Test]
        public void GroupsWithoutWheelPolicyDoNotAcquireWheelCadence()
        {
            using var fixture = new GroupFixture(16, wheelPolicy: false);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(100f, 0.0), Is.False);
            Assert.That(fixture.Group.ShouldEvaluateSpeedRetention(100f, 10.0), Is.False);
        }

        private sealed class GroupFixture : IDisposable
        {
            private readonly FastenerDefinition[] definitions;
            private readonly FastenerInstance[] fasteners;

            public GroupFixture(int tightness, bool wheelPolicy = true)
            {
                definitions = new FastenerDefinition[4];
                for (int i = 0; i < definitions.Length; i++)
                {
                    definitions[i] = ScriptableObject.CreateInstance<FastenerDefinition>();
                    definitions[i].Configure("test.wheel.bolt." + i, "Wheel bolt", FastenerSize.Millimeter13,
                        8, FastenerDirection.ClockwiseToTighten, true, true,
                        ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter13));
                }
                fasteners = definitions.Select(value => new FastenerInstance(value)).ToArray();
                var definition = new FastenerGroupDefinition();
                definition.Configure(definitions.Select(value => value.DefinitionId).ToArray(), 32, 1, 0,
                    wheelPolicy ? FastenerSpeedRetentionPolicy.DonorWheelBoltCheck : FastenerSpeedRetentionPolicy.None,
                    5f, 33f, 100f, FastenerBreakAction.DetachInstalledPart);
                Group = new FastenerGroupState(definition, fasteners);
                SetTightness(tightness);
            }

            public FastenerGroupState Group { get; }

            public void SetTightness(int total)
            {
                int remaining = total;
                foreach (FastenerInstance fastener in fasteners)
                {
                    int stage = Mathf.Min(remaining, fastener.Definition.MaximumStage);
                    Assert.That(fastener.TryRestore(true, true, stage), Is.True);
                    remaining -= stage;
                }
                Assert.That(remaining, Is.Zero);
                Group.Reevaluate(true);
            }

            public void Dispose()
            {
                foreach (FastenerDefinition definition in definitions) Object.DestroyImmediate(definition);
            }
        }
    }
}
