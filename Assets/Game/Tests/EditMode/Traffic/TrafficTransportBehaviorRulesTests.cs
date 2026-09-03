using MSC.Traffic;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.Traffic
{
    public sealed class TrafficTransportBehaviorRulesTests
    {
        [Test]
        public void BoatTwo_RequiresSundayDaylightAndPlayerResidency()
        {
            Vector3 boat = new(100f, 0f, 100f);

            Assert.That(TrafficTransportBehaviorRules.IsBoatTwoEligible(
                dayIndex: 5L,
                isDaylight: true,
                playerPosition: boat + Vector3.forward * 699f,
                boatPosition: boat), Is.True);
            Assert.That(TrafficTransportBehaviorRules.IsBoatTwoEligible(
                dayIndex: 5L,
                isDaylight: false,
                playerPosition: boat,
                boatPosition: boat), Is.False);
            Assert.That(TrafficTransportBehaviorRules.IsBoatTwoEligible(
                dayIndex: 4L,
                isDaylight: true,
                playerPosition: boat,
                boatPosition: boat), Is.False);
            Assert.That(TrafficTransportBehaviorRules.IsBoatTwoEligible(
                dayIndex: 5L,
                isDaylight: true,
                playerPosition: boat + Vector3.forward * 701f,
                boatPosition: boat), Is.False);
        }

        [Test]
        public void BoatTwo_StartPointSelection_IsDeterministicAndInRange()
        {
            int first = TrafficTransportBehaviorRules
                .ResolveBoatTwoStartPointIndex(5L, 0, 8);
            int repeated = TrafficTransportBehaviorRules
                .ResolveBoatTwoStartPointIndex(5L, 0, 8);

            Assert.That(first, Is.EqualTo(repeated));
            Assert.That(first, Is.InRange(0, 7));
            for (int activation = 0; activation < 64; activation++)
            {
                Assert.That(TrafficTransportBehaviorRules
                    .ResolveBoatTwoStartPointIndex(5L, activation, 8),
                    Is.InRange(0, 7));
            }
        }

        [Test]
        public void BusAbandonment_RequiresTravelThenTwentyFiveRealSecondsStuck()
        {
            var state = new BusTerminalAbandonmentState(
                BusTerminalAbandonmentPhase.Monitoring,
                monitorArmed: false,
                stuckRealSeconds: 0f,
                shutdownDelayRealSecondsRemaining: 0f);

            state = TrafficTransportBehaviorRules.AdvanceBusAbandonment(
                state,
                TrafficTransportBehaviorRules
                    .BusStuckMonitorArmSpeedMetersPerSecond,
                intentionalServiceStop: false,
                realDeltaSeconds: 30f);
            Assert.That(state.MonitorArmed, Is.False,
                "The donor comparison is strictly greater than five.");

            state = TrafficTransportBehaviorRules.AdvanceBusAbandonment(
                state,
                physicalSpeedMetersPerSecond: 5.01f,
                intentionalServiceStop: false,
                realDeltaSeconds: 0.02f);
            Assert.That(state.MonitorArmed, Is.True);

            state = TrafficTransportBehaviorRules.AdvanceBusAbandonment(
                state,
                physicalSpeedMetersPerSecond: 2f,
                intentionalServiceStop: false,
                realDeltaSeconds: 24.99f);
            Assert.That(state.Phase,
                Is.EqualTo(BusTerminalAbandonmentPhase.StuckConfirmation));

            state = TrafficTransportBehaviorRules.AdvanceBusAbandonment(
                state,
                physicalSpeedMetersPerSecond: 2f,
                intentionalServiceStop: false,
                realDeltaSeconds: 0.01f);
            Assert.That(state.Phase,
                Is.EqualTo(BusTerminalAbandonmentPhase.ShutdownDelay));
            Assert.That(state.ShutdownDelayRealSecondsRemaining,
                Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void BusAbandonment_ServiceDwellAndRecoveredMotionResetConfirmation()
        {
            var state = new BusTerminalAbandonmentState(
                BusTerminalAbandonmentPhase.StuckConfirmation,
                monitorArmed: true,
                stuckRealSeconds: 24f,
                shutdownDelayRealSecondsRemaining: 0f);

            state = TrafficTransportBehaviorRules.AdvanceBusAbandonment(
                state,
                physicalSpeedMetersPerSecond: 0f,
                intentionalServiceStop: true,
                realDeltaSeconds: 100f);
            Assert.That(state.Phase,
                Is.EqualTo(BusTerminalAbandonmentPhase.Monitoring));
            Assert.That(state.StuckRealSeconds, Is.Zero);
            Assert.That(state.MonitorArmed, Is.True);

            state = TrafficTransportBehaviorRules.AdvanceBusAbandonment(
                new BusTerminalAbandonmentState(
                    BusTerminalAbandonmentPhase.StuckConfirmation,
                    monitorArmed: true,
                    stuckRealSeconds: 24f,
                    shutdownDelayRealSecondsRemaining: 0f),
                physicalSpeedMetersPerSecond: 2.01f,
                intentionalServiceStop: false,
                realDeltaSeconds: 2f);
            Assert.That(state.Phase,
                Is.EqualTo(BusTerminalAbandonmentPhase.Monitoring));
            Assert.That(state.StuckRealSeconds, Is.Zero);
        }

        [Test]
        public void BusAbandonment_RecoveryMotionDoesNotFakeForwardRecovery()
        {
            var state = new BusTerminalAbandonmentState(
                BusTerminalAbandonmentPhase.StuckConfirmation,
                monitorArmed: true,
                stuckRealSeconds: 24f,
                shutdownDelayRealSecondsRemaining: 0f);

            state = TrafficTransportBehaviorRules.AdvanceBusAbandonment(
                state,
                physicalSpeedMetersPerSecond: 4f,
                intentionalServiceStop: false,
                realDeltaSeconds: 1f,
                meaningfulForwardRouteProgress: false,
                recoveryMotionWithoutForwardProgress: true);
            Assert.That(state.Phase,
                Is.EqualTo(BusTerminalAbandonmentPhase.ShutdownDelay),
                "Project reverse recovery cannot keep donor DrivingIssue alive forever.");

            state = TrafficTransportBehaviorRules.AdvanceBusAbandonment(
                new BusTerminalAbandonmentState(
                    BusTerminalAbandonmentPhase.StuckConfirmation,
                    monitorArmed: true,
                    stuckRealSeconds: 24f,
                    shutdownDelayRealSecondsRemaining: 0f),
                physicalSpeedMetersPerSecond: 0f,
                intentionalServiceStop: false,
                realDeltaSeconds: 10f,
                meaningfulForwardRouteProgress: true,
                recoveryMotionWithoutForwardProgress: true);
            Assert.That(state.Phase,
                Is.EqualTo(BusTerminalAbandonmentPhase.Monitoring));
            Assert.That(state.StuckRealSeconds, Is.Zero);
        }

        [Test]
        public void BusAbandonment_ForwardProgressAndRecoveryBudgetHandleLoopWrap()
        {
            Assert.That(TrafficTransportBehaviorRules
                    .ResolveBusForwardProgressMeters(
                        anchorProgress01: 0.99f,
                        anchorCompletedTrips: 2,
                        currentProgress01: 0.01f,
                        currentCompletedTrips: 3,
                        routeLengthMeters: 1000f),
                Is.EqualTo(20f).Within(0.001f));
            Assert.That(TrafficTransportBehaviorRules
                    .ResolveBusForwardProgressMeters(
                        anchorProgress01: 0.5f,
                        anchorCompletedTrips: 2,
                        currentProgress01: 0.45f,
                        currentCompletedTrips: 2,
                        routeLengthMeters: 1000f),
                Is.Zero,
                "Reverse travel is not meaningful forward route progress.");
            Assert.That(TrafficTransportBehaviorRules
                    .IsBusRecoveryExhausted(4, 6),
                Is.False);
            Assert.That(TrafficTransportBehaviorRules
                    .IsBusRecoveryExhausted(4, 7),
                Is.True);
        }

        [Test]
        public void BusAbandonment_ShutdownWaitsFourRealSecondsThenStaysTerminal()
        {
            var state = new BusTerminalAbandonmentState(
                BusTerminalAbandonmentPhase.ShutdownDelay,
                monitorArmed: true,
                stuckRealSeconds: 25f,
                shutdownDelayRealSecondsRemaining: 4f);

            state = TrafficTransportBehaviorRules.AdvanceBusAbandonment(
                state,
                physicalSpeedMetersPerSecond: 30f,
                intentionalServiceStop: false,
                realDeltaSeconds: 3.99f);
            Assert.That(state.Phase,
                Is.EqualTo(BusTerminalAbandonmentPhase.ShutdownDelay));

            state = TrafficTransportBehaviorRules.AdvanceBusAbandonment(
                state,
                physicalSpeedMetersPerSecond: 30f,
                intentionalServiceStop: false,
                realDeltaSeconds: 0.01f);
            Assert.That(state.Phase,
                Is.EqualTo(BusTerminalAbandonmentPhase.Abandoned));
            Assert.That(state.IsTerminal, Is.True);

            state = TrafficTransportBehaviorRules.AdvanceBusAbandonment(
                state,
                physicalSpeedMetersPerSecond: 100f,
                intentionalServiceStop: false,
                realDeltaSeconds: 500f);
            Assert.That(state.Phase,
                Is.EqualTo(BusTerminalAbandonmentPhase.Abandoned));
        }

        [Test]
        public void BusAbandonment_DoorUsesDonorOneSecondRealTimeCurve()
        {
            Assert.That(
                TrafficTransportBehaviorRules.ResolveBusServiceDoorOpen01(
                    BusTerminalAbandonmentPhase.StuckConfirmation,
                    shutdownDelayRealSecondsRemaining: 0f),
                Is.Zero);
            Assert.That(
                TrafficTransportBehaviorRules.ResolveBusServiceDoorOpen01(
                    BusTerminalAbandonmentPhase.ShutdownDelay,
                    shutdownDelayRealSecondsRemaining: 4f),
                Is.Zero);
            Assert.That(
                TrafficTransportBehaviorRules.ResolveBusServiceDoorOpen01(
                    BusTerminalAbandonmentPhase.ShutdownDelay,
                    shutdownDelayRealSecondsRemaining: 3.5f),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(
                TrafficTransportBehaviorRules.ResolveBusServiceDoorOpen01(
                    BusTerminalAbandonmentPhase.ShutdownDelay,
                    shutdownDelayRealSecondsRemaining: 3f),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                TrafficTransportBehaviorRules.ResolveBusServiceDoorOpen01(
                    BusTerminalAbandonmentPhase.Abandoned,
                    shutdownDelayRealSecondsRemaining: 0f),
                Is.EqualTo(1f));
        }

        [Test]
        public void BusAbandonment_NewDepartureResetsTerminalStateAndDisarms()
        {
            BusTerminalAbandonmentState state =
                TrafficTransportBehaviorRules
                    .ResetBusAbandonmentForNewDeparture();

            Assert.That(state.Phase,
                Is.EqualTo(BusTerminalAbandonmentPhase.Monitoring));
            Assert.That(state.MonitorArmed, Is.False);
            Assert.That(state.StuckRealSeconds, Is.Zero);
            Assert.That(state.ShutdownDelayRealSecondsRemaining, Is.Zero);
        }

        [Test]
        public void BusAbandonment_TerminalPhysicalPoseRemainsAuthoritative()
        {
            Assert.That(TrafficTransportBehaviorRules
                    .IsTerminalBusPhysicalPoseAuthoritative(
                        BusTerminalAbandonmentPhase.StuckConfirmation),
                Is.False);
            Assert.That(TrafficTransportBehaviorRules
                    .IsTerminalBusPhysicalPoseAuthoritative(
                        BusTerminalAbandonmentPhase.ShutdownDelay),
                Is.True);
            Assert.That(TrafficTransportBehaviorRules
                    .IsTerminalBusPhysicalPoseAuthoritative(
                        BusTerminalAbandonmentPhase.Abandoned),
                Is.True);
        }
    }
}
