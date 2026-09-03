using System.Collections.Generic;
using MSC.Audio;
using MSC.Bootstrap;
using MSC.Core.Time;
using MSC.Interaction;
using MSC.Interaction.Architecture;
using MSC.Interaction.Capabilities;
using MSC.Needs;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Home.Tests.EditMode
{
    public sealed class HomeSystemRuntimeTests
    {
        private GameObject fixtureObject;
        private HomeSystemRuntime runtime;

        [SetUp]
        public void SetUp()
        {
            fixtureObject = new GameObject("HomeSystemRuntimeFixture");
            runtime = fixtureObject.AddComponent<HomeSystemRuntime>();
            Initialize(runtime);
        }

        [TearDown]
        public void TearDown()
        {
            if (fixtureObject != null)
            {
                Object.DestroyImmediate(fixtureObject);
            }
        }

        [Test]
        public void FreshShower_SelectsHead_AndOpenValveFlowsOnlySelectedOutlet()
        {
            HomeStateSnapshot fresh = runtime.Snapshot;

            Assert.That(fresh.ShowerHeadSelected, Is.True);
            Assert.That(fresh.ShowerValveOpen, Is.False);
            Assert.That(fresh.ShowerHeadFlowing, Is.False);
            Assert.That(fresh.ShowerTapFlowing, Is.False);

            Assert.That(
                runtime.TryPerformAction(
                    HomeActionIds.ShowerValveToggle,
                    HomeActionKind.ShowerValveToggle,
                    out string valveFailure),
                Is.True,
                valveFailure);

            HomeStateSnapshot headFlow = runtime.Snapshot;
            Assert.That(headFlow.ShowerHeadFlowing, Is.True);
            Assert.That(headFlow.ShowerTapFlowing, Is.False);

            Assert.That(
                runtime.TryPerformAction(
                    HomeActionIds.ShowerSwitchToggle,
                    HomeActionKind.ShowerSwitchToggle,
                    out string selectorFailure),
                Is.True,
                selectorFailure);

            HomeStateSnapshot tapFlow = runtime.Snapshot;
            Assert.That(tapFlow.ShowerHeadSelected, Is.False);
            Assert.That(tapFlow.ShowerHeadFlowing, Is.False);
            Assert.That(tapFlow.ShowerTapFlowing, Is.True);
        }

        [Test]
        public void BathroomScale_UsesDonorWeightCalibrationAndBoundedFootprint()
        {
            Assert.That(
                HomeWeightScalePresenter.CalculateGaugeAngleDegrees(83f),
                Is.EqualTo(-230.74f).Within(0.001f));
            Assert.That(
                HomeWeightScalePresenter.CalculateGaugeAngleDegrees(96f),
                Is.EqualTo(-266.88f).Within(0.001f));

            var center = new Vector3(165.967f, 1.269f, -1029.393f);
            Assert.That(
                HomeWeightScalePresenter.IsPlayerWithinDetectionVolume(
                    center + new Vector3(0.15f, -0.18f, 0.1f),
                    center,
                    horizontalRadiusMeters: 0.24f,
                    verticalToleranceMeters: 0.45f),
                Is.True);
            Assert.That(
                HomeWeightScalePresenter.IsPlayerWithinDetectionVolume(
                    center + new Vector3(0.3f, -0.18f, 0f),
                    center,
                    horizontalRadiusMeters: 0.24f,
                    verticalToleranceMeters: 0.45f),
                Is.False);
        }

        [Test]
        public void KitchenTapProvidesWaterToHeldContainerOnlyWhileOpen()
        {
            var targetObject = new GameObject("Kitchen tap liquid target");
            try
            {
                HomeInteractionTarget target =
                    targetObject.AddComponent<HomeInteractionTarget>();
                target.Configure(
                    HomeActionIds.KitchenTapConsume,
                    "Пить воду",
                    HomeActionKind.KitchenTapConsume,
                    runtime);
                var context = new InteractionContext(
                    fixtureObject,
                    Vector3.zero,
                    Vector3.forward);

                Assert.That(
                    target.CanProvideLiquid(string.Empty, 0.5f, context),
                    Is.False);
                Perform(
                    HomeActionIds.KitchenTapToggle,
                    HomeActionKind.KitchenTapToggle);
                Assert.That(
                    target.TryProvideLiquid(
                        string.Empty,
                        0.5f,
                        context,
                        out string liquidId,
                        out float provided),
                    Is.True);
                Assert.That(liquidId, Is.EqualTo(LiquidTypeIds.Water));
                Assert.That(provided, Is.EqualTo(0.5f));
                Assert.That(
                    target.CanProvideLiquid("liquid.gasoline", 0.5f, context),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void SaunaHeatWheel_AdjustsByFifteenDegrees_AndClamps()
        {
            Assert.That(
                runtime.Snapshot.ElectricSaunaHeatKnobDegrees,
                Is.EqualTo(HomeSaunaControlRange.MinimumHeatDegrees));

            Adjust(
                HomeActionIds.ElectricSaunaPowerToggle,
                HomeActionKind.ElectricSaunaPowerToggle,
                1f);
            Assert.That(
                runtime.Snapshot.ElectricSaunaHeatKnobDegrees,
                Is.EqualTo(
                    HomeSaunaControlRange.MinimumHeatDegrees +
                    HomeSaunaControlRange.HeatStepDegrees));

            Adjust(
                HomeActionIds.ElectricSaunaPowerToggle,
                HomeActionKind.ElectricSaunaPowerToggle,
                -1f);
            Assert.That(
                runtime.Snapshot.ElectricSaunaHeatKnobDegrees,
                Is.EqualTo(HomeSaunaControlRange.MinimumHeatDegrees));

            Adjust(
                HomeActionIds.ElectricSaunaPowerToggle,
                HomeActionKind.ElectricSaunaPowerToggle,
                100f);
            Assert.That(
                runtime.Snapshot.ElectricSaunaHeatKnobDegrees,
                Is.EqualTo(HomeSaunaControlRange.MaximumHeatDegrees));

            Adjust(
                HomeActionIds.ElectricSaunaPowerToggle,
                HomeActionKind.ElectricSaunaPowerToggle,
                -100f);
            Assert.That(
                runtime.Snapshot.ElectricSaunaHeatKnobDegrees,
                Is.EqualTo(HomeSaunaControlRange.MinimumHeatDegrees));
        }

        [Test]
        public void SaunaTimerWheel_AdjustsByTenDegrees_Clamps_AndMapsToSeconds()
        {
            Assert.That(
                runtime.Snapshot.ElectricSaunaTimerKnobDegrees,
                Is.EqualTo(HomeSaunaControlRange.MinimumTimerDegrees));

            Adjust(
                HomeActionIds.ElectricSaunaTimerCycle,
                HomeActionKind.ElectricSaunaTimerCycle,
                1f);
            AssertTimerMatchesDegrees(
                HomeSaunaControlRange.MinimumTimerDegrees +
                HomeSaunaControlRange.TimerStepDegrees);

            Adjust(
                HomeActionIds.ElectricSaunaTimerCycle,
                HomeActionKind.ElectricSaunaTimerCycle,
                100f);
            AssertTimerMatchesDegrees(
                HomeSaunaControlRange.MaximumTimerDegrees);

            Adjust(
                HomeActionIds.ElectricSaunaTimerCycle,
                HomeActionKind.ElectricSaunaTimerCycle,
                -100f);
            AssertTimerMatchesDegrees(
                HomeSaunaControlRange.MinimumTimerDegrees);
        }

        [Test]
        public void SaunaDirectionalHint_HidesTheClampedWheelDirection()
        {
            var targetObject = new GameObject("Sauna heat direction target");
            try
            {
                HomeRangeInteractionTarget target =
                    targetObject.AddComponent<HomeRangeInteractionTarget>();
                target.Configure(
                    HomeActionIds.ElectricSaunaPowerToggle,
                    HomeActionKind.ElectricSaunaPowerToggle,
                    runtime,
                    Vector3.up);
                var context = new InteractionContext(
                    fixtureObject,
                    target.transform.position,
                    target.transform.forward);

                Assert.That(
                    target.CanAdjust(
                        context,
                        InteractionScrollDirection.Positive),
                    Is.True);
                Assert.That(
                    target.CanAdjust(
                        context,
                        InteractionScrollDirection.Negative),
                    Is.False);
                Assert.That(
                    target.GetAdjustmentPrompt(
                        InteractionScrollDirection.Positive),
                    Is.EqualTo("УВЕЛИЧИТЬ"));
                Assert.That(
                    target.GetAdjustmentPrompt(
                        InteractionScrollDirection.Negative),
                    Is.EqualTo("УМЕНЬШИТЬ"));

                Adjust(
                    HomeActionIds.ElectricSaunaPowerToggle,
                    HomeActionKind.ElectricSaunaPowerToggle,
                    100f);

                Assert.That(
                    target.CanAdjust(
                        context,
                        InteractionScrollDirection.Positive),
                    Is.False);
                Assert.That(
                    target.CanAdjust(
                        context,
                        InteractionScrollDirection.Negative),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void CaptureAndRestore_PreserveShowerSelectorValveAndSaunaKnobs()
        {
            Perform(
                HomeActionIds.ShowerValveToggle,
                HomeActionKind.ShowerValveToggle);
            Perform(
                HomeActionIds.ShowerSwitchToggle,
                HomeActionKind.ShowerSwitchToggle);
            Adjust(
                HomeActionIds.ElectricSaunaPowerToggle,
                HomeActionKind.ElectricSaunaPowerToggle,
                3f);
            Adjust(
                HomeActionIds.ElectricSaunaTimerCycle,
                HomeActionKind.ElectricSaunaTimerCycle,
                4f);

            HomeStateDto captured = runtime.CaptureDto();
            GameObject restoredObject =
                new GameObject("RestoredHomeSystemRuntimeFixture");

            try
            {
                HomeSystemRuntime restored =
                    restoredObject.AddComponent<HomeSystemRuntime>();
                Initialize(restored);

                Assert.That(
                    restored.TryRestoreDto(captured, out string failure),
                    Is.True,
                    failure);

                HomeStateSnapshot state = restored.Snapshot;
                Assert.That(state.ShowerHeadSelected, Is.False);
                Assert.That(state.ShowerValveOpen, Is.True);
                Assert.That(state.ShowerHeadFlowing, Is.False);
                Assert.That(state.ShowerTapFlowing, Is.True);
                Assert.That(
                    state.ElectricSaunaHeatKnobDegrees,
                    Is.EqualTo(captured.electricSaunaHeatKnobDegrees));
                Assert.That(
                    state.ElectricSaunaTimerKnobDegrees,
                    Is.EqualTo(captured.electricSaunaTimerKnobDegrees));
                Assert.That(
                    state.ElectricSaunaTimerSecondsRemaining,
                    Is.EqualTo(
                        state.ElectricSaunaTimerKnobDegrees *
                        HomeSaunaControlRange.TimerSecondsPerDegree));
            }
            finally
            {
                Object.DestroyImmediate(restoredObject);
            }
        }

        [Test]
        public void FridgeCoolingRequiresClosedDoorPowerAndElectricity()
        {
            Assert.That(runtime.Snapshot.FridgePowered, Is.True);
            Assert.That(runtime.Snapshot.FridgeDoorOpen, Is.False);
            Assert.That(runtime.FridgeCoolingActive, Is.True);

            runtime.SetFridgeDoorOpen(true);
            Assert.That(runtime.FridgeCoolingActive, Is.False);
            runtime.SetFridgeDoorOpen(false);
            Assert.That(runtime.FridgeCoolingActive, Is.True);

            runtime.SetElectricityAvailable(false);
            Assert.That(runtime.FridgeCoolingActive, Is.False);
            runtime.SetElectricityAvailable(true);
            Assert.That(runtime.FridgeCoolingActive, Is.True);

            Perform(
                HomeActionIds.FridgePowerToggle,
                HomeActionKind.FridgePowerToggle);
            Assert.That(runtime.Snapshot.FridgePowered, Is.False);
            Assert.That(runtime.FridgeCoolingActive, Is.False);
        }

        [Test]
        public void CaptureAndRestorePreserveFridgeDoorAndMangalState()
        {
            runtime.SetFridgeDoorOpen(true);
            Perform(
                HomeActionIds.MangalFireToggle,
                HomeActionKind.MangalFireToggle);
            HomeStateDto captured = runtime.CaptureDto();
            GameObject restoredObject =
                new GameObject("Restored food appliance home state");

            try
            {
                HomeSystemRuntime restored =
                    restoredObject.AddComponent<HomeSystemRuntime>();
                Initialize(restored);
                Assert.That(
                    restored.TryRestoreDto(captured, out string failure),
                    Is.True,
                    failure);
                Assert.That(restored.Snapshot.FridgeDoorOpen, Is.True);
                Assert.That(restored.Snapshot.MangalLit, Is.True);
                Assert.That(restored.MangalHeatingActive, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(restoredObject);
            }
        }

        [Test]
        public void FridgeDoorUsesOrdinaryInteractionLimitsAndAudioEventIds()
        {
            var doorObject = new GameObject("Fridge door interaction test");
            var audio = new RecordingAudioBackend();

            try
            {
                HingedDoorInteractionTarget hinge =
                    doorObject.AddComponent<HingedDoorInteractionTarget>();
                hinge.Configure(
                    doorObject.transform,
                    Quaternion.identity,
                    Vector3.forward,
                    ProductionFoodApplianceInstaller.FridgeDoorSwingDegrees,
                    ProductionFoodApplianceInstaller
                        .FridgeDoorAngularSpeedDegrees,
                    "Open fridge",
                    "Close fridge");
                FridgeDoorInteractionTarget target =
                    doorObject.AddComponent<FridgeDoorInteractionTarget>();
                target.Configure(hinge, runtime, audio);
                var context = new InteractionContext(
                    fixtureObject,
                    Vector3.zero,
                    Vector3.forward);

                Assert.That(target.CanInteract(context), Is.True);
                target.Interact(context);
                Assert.That(runtime.Snapshot.FridgeDoorOpen, Is.True);
                Assert.That(hinge.TargetOpen, Is.True);
                hinge.Advance(hinge.EstimatedTravelSeconds);
                Assert.That(
                    hinge.OpenNormalized,
                    Is.EqualTo(1f).Within(0.0001f));
                Assert.That(
                    audio.PostedEvents[0].EventId,
                    Is.EqualTo(AudioProjectIds.Events.InteractionDoorOpen));

                target.Interact(context);
                Assert.That(runtime.Snapshot.FridgeDoorOpen, Is.False);
                Assert.That(hinge.TargetOpen, Is.False);
                hinge.Advance(hinge.EstimatedTravelSeconds);
                Assert.That(
                    hinge.OpenNormalized,
                    Is.EqualTo(0f).Within(0.0001f));
                Assert.That(
                    audio.PostedEvents[1].EventId,
                    Is.EqualTo(AudioProjectIds.Events.InteractionDoorClose));
            }
            finally
            {
                Object.DestroyImmediate(doorObject);
            }
        }

        [Test]
        public void FlowVolumeFillsCompatibleContainerOnlyWhileWaterFlows()
        {
            var streamObject = new GameObject("Physical water stream test");
            GameObject containerObject =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                HomeLiquidStreamFillVolume stream =
                    streamObject.AddComponent<HomeLiquidStreamFillVolume>();
                stream.Configure(
                    configuredFillRateLitresPerSecond: 1.75f,
                    radiusMeters: 0.1f,
                    lengthMeters: 1.2f);
                LiquidContainerProbe container =
                    containerObject.AddComponent<LiquidContainerProbe>();
                Collider collider = containerObject.GetComponent<Collider>();

                Assert.That(stream.FillVolume.isTrigger, Is.True);
                Assert.That(stream.FillVolume.enabled, Is.False);
                Assert.That(stream.TryFill(collider, 0.5f), Is.False);

                stream.SetFlowing(true);
                Assert.That(stream.FillVolume.enabled, Is.True);
                Assert.That(stream.TryFill(collider, 0.5f), Is.True);
                Assert.That(container.LiquidId,
                    Is.EqualTo(LiquidTypeIds.Water));
                Assert.That(container.AmountLitres,
                    Is.EqualTo(0.875f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(containerObject);
                Object.DestroyImmediate(streamObject);
            }
        }

        private static void Initialize(HomeSystemRuntime target)
        {
            target.Initialize(
                new GameTimeService(),
                () => true,
                new NeedsEffectSinkStub(),
                TryStartUrination,
                () => Vector3.zero);
        }

        private static bool TryStartUrination(out string failure)
        {
            failure = string.Empty;
            return true;
        }

        private void Perform(
            string stableActionId,
            HomeActionKind action)
        {
            Assert.That(
                runtime.TryPerformAction(
                    stableActionId,
                    action,
                    out string failure),
                Is.True,
                failure);
        }

        private void Adjust(
            string stableActionId,
            HomeActionKind action,
            float scrollNotches)
        {
            Assert.That(
                runtime.TryAdjustAction(
                    stableActionId,
                    action,
                    scrollNotches,
                    out string failure),
                Is.True,
                failure);
        }

        private void AssertTimerMatchesDegrees(float expectedDegrees)
        {
            HomeStateSnapshot state = runtime.Snapshot;
            Assert.That(
                state.ElectricSaunaTimerKnobDegrees,
                Is.EqualTo(expectedDegrees));
            Assert.That(
                state.ElectricSaunaTimerSecondsRemaining,
                Is.EqualTo(
                    expectedDegrees *
                    HomeSaunaControlRange.TimerSecondsPerDegree));
        }

        private sealed class NeedsEffectSinkStub :
            IPlayerNeedsEffectSink
        {
            public bool TryApplyEffects(
                in PlayerNeedsEffectDelta immediateDelta,
                in PlayerNeedsEffectDelta delayedDelta,
                out string failure)
            {
                failure = string.Empty;
                return true;
            }

            public bool TryApplyEffectImmediately(
                in PlayerNeedsEffectDelta delta,
                out string failure)
            {
                failure = string.Empty;
                return true;
            }

            public bool TryQueueDelayedEffect(
                in PlayerNeedsEffectDelta delta,
                out string failure)
            {
                failure = string.Empty;
                return true;
            }
        }

        private sealed class LiquidContainerProbe :
            MonoBehaviour,
            ILiquidContainerTarget
        {
            public float AmountLitres { get; private set; }
            public string LiquidId { get; private set; } = string.Empty;

            public bool CanAcceptLiquid(
                string liquidId,
                float requestedLitres)
            {
                return requestedLitres > 0f &&
                    (string.IsNullOrEmpty(LiquidId) ||
                     LiquidId == liquidId);
            }

            public bool TryAcceptLiquid(
                string liquidId,
                float requestedLitres,
                out float acceptedLitres)
            {
                acceptedLitres = 0f;
                if (!CanAcceptLiquid(liquidId, requestedLitres))
                {
                    return false;
                }

                LiquidId = liquidId;
                AmountLitres += requestedLitres;
                acceptedLitres = requestedLitres;
                return true;
            }
        }

        private sealed class RecordingAudioBackend : IAudioBackend
        {
            public readonly List<AudioEventRequest> PostedEvents =
                new List<AudioEventRequest>();

            public string BackendId => "audio.backend.fridge-tests";
            public AudioBackendKind Kind => AudioBackendKind.Wwise;
            public bool IsReady => true;
            public string FailureReason => string.Empty;

            public bool RegisterEmitter(
                IAudioEmitter emitter,
                out string failure)
            {
                failure = string.Empty;
                return emitter != null;
            }

            public bool UnregisterEmitter(IAudioEmitter emitter) =>
                emitter != null;

            public IAudioEventHandle PostEvent(in AudioEventRequest request)
            {
                PostedEvents.Add(request);
                return null;
            }

            public bool SetParameter(
                AudioParameterId parameterId,
                float value,
                IAudioEmitter emitter = null) => true;

            public bool SetSwitch(
                AudioSwitchId switchGroupId,
                AudioSwitchId switchValueId,
                IAudioEmitter emitter = null) => true;

            public bool SetState(
                AudioStateId stateGroupId,
                AudioStateId stateValueId) => true;

            public void SetListenerContext(
                in AudioListenerContext listenerContext)
            {
            }

            public void ApplySettings(in AudioSettingsState settings)
            {
            }

            public void StopAll(float fadeSeconds = 0f)
            {
            }

            public AudioRuntimeSnapshot CaptureSnapshot() => default;
        }
    }
}
