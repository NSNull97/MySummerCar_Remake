using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MSC.Core.Time;
using MSC.Weather.Domain;
using MSC.Weather.Lightning;
using MSC.Weather.Presentation;
using MSC.Weather.Production;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MSC.Tests.EditMode.WeatherProduction
{
    public sealed class ProductionEnvironmentDevApiTests
    {
        private GameObject root;
        private FakePresentationAdapter adapter;
        private ProductionEnvironmentController controller;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ProductionEnvironmentDevApiTests");
            root.SetActive(false);
            adapter = root.AddComponent<FakePresentationAdapter>();
            controller = root.AddComponent<ProductionEnvironmentController>();
            controller.ConfigureForAuthoring(
                adapter,
                null,
                EnvironmentQualityTier.Medium,
                42UL);
            controller.InitializeForEditorValidation();
            root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LogicalOverride_UsesKnownIdAndRequestedTransition()
        {
            CompleteInitialization(controller);

            Assert.That(
                controller.DevTryApplyWeatherOverride(
                    "weather.heavy_rain",
                    42f,
                    out string failure),
                Is.True,
                failure);
            Assert.That(
                controller.AuthoritativeWeather.CurrentState.Id.Value,
                Is.EqualTo("weather.heavy_rain"));
            Assert.That(controller.DevManualWeatherOverrideActive, Is.True);
            Assert.That(
                adapter.LastFrame.TransitionDurationSeconds,
                Is.EqualTo(42f));

            controller.DevRefreshPresentation();
            Assert.That(
                adapter.LastFrame.TransitionDurationSeconds,
                Is.Zero);
            Assert.That(controller.DevRemoveWeatherOverride(), Is.True);
            Assert.That(controller.DevManualWeatherOverrideActive, Is.False);
            Assert.That(
                adapter.LastFrame.TransitionDurationSeconds,
                Is.EqualTo(0.25f));
        }

        [Test]
        public void ScheduledBinding_UsesRemainingFrontInSimulationSeconds()
        {
            CompleteInitialization(controller);
            Assert.That(
                adapter.LastFrame.TransitionDurationSeconds,
                Is.Zero,
                "The initial/reveal frame must be coherent and instant.");

            WeatherTransition transition =
                controller.AuthoritativeWeather.Timeline.Transition;
            double elapsedGameSeconds =
                transition.Front.TransitionDurationSeconds * 0.75d;
            controller.AuthoritativeWeather.Advance(elapsedGameSeconds);
            controller.DevRefreshPresentation();

            double gameSecondsPerSimulationSecond =
                86400d /
                controller.AuthoritativeGameTime.Config
                    .DayLengthSimulationSeconds *
                controller.AuthoritativeGameTime.Snapshot.TimeScale;
            float expected = (float)(
                (transition.Front.TransitionDurationSeconds -
                 elapsedGameSeconds) /
                gameSecondsPerSimulationSecond);
            Assert.That(
                adapter.LastFrame.TransitionDurationSeconds,
                Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void EnvironmentRefresh_IsThresholdedByBindingTimeAndListener()
        {
            GameObject cameraObject = new GameObject("CadenceCamera");
            cameraObject.transform.SetParent(root.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            Assert.That(controller.BindPresentationCamera(camera), Is.True);
            CompleteInitialization(controller);
            Assert.That(
                adapter.LastFrame.EnvironmentRefresh.Targets,
                Is.EqualTo(
                    EnvironmentRefreshTarget.Sky |
                    EnvironmentRefreshTarget.Ambient |
                    EnvironmentRefreshTarget.Reflections));

            controller.DevRefreshPresentation();
            Assert.That(
                adapter.LastFrame.EnvironmentRefresh.Targets,
                Is.EqualTo(EnvironmentRefreshTarget.None));

            Assert.That(
                controller.DevTryApplyWeatherOverride(
                    WeatherStateIds.PartlyCloudy.Value,
                    2f,
                    out string overrideFailure),
                Is.True,
                overrideFailure);
            Assert.That(
                adapter.LastFrame.EnvironmentRefresh.Targets,
                Is.EqualTo(
                    EnvironmentRefreshTarget.Sky |
                    EnvironmentRefreshTarget.Ambient |
                    EnvironmentRefreshTarget.Reflections));

            Assert.That(
                controller.DevTryAdvanceGameSeconds(
                    599d,
                    out string firstAdvanceFailure),
                Is.True,
                firstAdvanceFailure);
            Assert.That(
                adapter.LastFrame.EnvironmentRefresh.Targets,
                Is.EqualTo(EnvironmentRefreshTarget.None));
            Assert.That(
                controller.DevTryAdvanceGameSeconds(
                    1d,
                    out string secondAdvanceFailure),
                Is.True,
                secondAdvanceFailure);
            Assert.That(
                adapter.LastFrame.EnvironmentRefresh.Targets,
                Is.EqualTo(
                    EnvironmentRefreshTarget.Sky |
                    EnvironmentRefreshTarget.Ambient |
                    EnvironmentRefreshTarget.Reflections));

            cameraObject.transform.position = new Vector3(25.1f, 0f, 0f);
            controller.DevRefreshPresentation();
            Assert.That(
                adapter.LastFrame.EnvironmentRefresh.Targets,
                Is.EqualTo(
                    EnvironmentRefreshTarget.Ambient |
                    EnvironmentRefreshTarget.Reflections));
            controller.DevRefreshPresentation();
            Assert.That(
                adapter.LastFrame.EnvironmentRefresh.Targets,
                Is.EqualTo(EnvironmentRefreshTarget.None));
        }

        [Test]
        public void EnvironmentOutputsChanged_IsCoherentAndIsolatesSubscribers()
        {
            int successfulSubscriberCalls = 0;
            WeatherEnvironmentOutputs received = default;
            controller.EnvironmentOutputsChanged += _ =>
                throw new InvalidOperationException("intentional test failure");
            controller.EnvironmentOutputsChanged += outputs =>
            {
                successfulSubscriberCalls++;
                received = outputs;
            };
            LogAssert.Expect(
                LogType.Error,
                "Production environment subscriber failed for " +
                "EnvironmentOutputsChanged: intentional test failure");

            CompleteInitialization(controller);

            Assert.That(successfulSubscriberCalls, Is.EqualTo(1));
            Assert.That(received.IsValid, Is.True);
            Assert.That(
                received.LogicalRevision,
                Is.EqualTo(controller.CurrentOutputs.LogicalRevision));
            Assert.That(
                received.Weather.Id,
                Is.EqualTo(controller.CurrentOutputs.Weather.Id));
        }

        [Test]
        public void AutomaticStormLightning_IsDirectorOwnedGraceBoundedAndNonLethal()
        {
            GameObject cameraObject = new GameObject("LightningCamera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(10f, 2f, 20f);
            Camera camera = cameraObject.AddComponent<Camera>();
            Assert.That(controller.BindPresentationCamera(camera), Is.True);
            CompleteInitialization(controller);
            var strikes = new List<LightningStrikeEvent>();
            var thunder = new List<ThunderAudioRequest>();
            controller.LightningOccurred += strikes.Add;
            controller.ThunderRequested += thunder.Add;
            Assert.That(
                controller.DevTryApplyWeatherOverride(
                    WeatherStateIds.Thunderstorm.Value,
                    0f,
                    out string overrideFailure),
                Is.True,
                overrideFailure);
            Assert.That(adapter.LastFrame.LightningVisual.IsRequested, Is.False);

            Assert.That(
                controller.DevTryAdvanceGameSeconds(
                    46d,
                    out string graceFailure),
                Is.True,
                graceFailure);
            Assert.That(
                adapter.LastFrame.LightningVisual.IsRequested,
                Is.False,
                "Leaving spawn grace schedules a bounded strike; it must not flash immediately.");

            Assert.That(
                controller.DevTryAdvanceGameSeconds(
                    241d,
                    out string dueFailure),
                Is.True,
                dueFailure);
            Assert.That(adapter.LastFrame.LightningVisual.IsRequested, Is.True);
            Vector3 offset =
                adapter.LastFrame.LightningVisual.WorldPosition -
                cameraObject.transform.position;
            float horizontalDistance =
                new Vector2(offset.x, offset.z).magnitude;
            Assert.That(horizontalDistance, Is.InRange(180f, 450f));
            Assert.That(offset.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(strikes, Has.Count.EqualTo(1));
            Assert.That(thunder, Has.Count.EqualTo(1));
            Assert.That(
                thunder[0].Sequence,
                Is.EqualTo(strikes[0].Sequence));
            Assert.That(
                thunder[0].DelaySeconds,
                Is.EqualTo(offset.magnitude / 343d).Within(0.0001d));
            Assert.That(controller.AuthoritativeLightning.NonLethalMode, Is.True);
            Assert.That(
                controller.AuthoritativeLightning.CaptureSnapshot()
                    .CandidateStates,
                Is.Empty,
                "Ambient presentation must not fabricate gameplay strike candidates/effects.");

            controller.DevRefreshPresentation();
            Assert.That(adapter.LastFrame.LightningVisual.IsRequested, Is.False);
            Assert.That(
                controller.DevTryAdvanceGameSeconds(
                    89d,
                    out string cooldownFailure),
                Is.True,
                cooldownFailure);
            Assert.That(
                adapter.LastFrame.LightningVisual.IsRequested,
                Is.False,
                "Automatic ambient lightning must honor its minimum cooldown.");
        }

        [Test]
        public void AdditiveSceneOwnershipFault_StopsSimulationFailClosed()
        {
            CompleteInitialization(controller);
            controller.BeginSimulationAfterWorldReveal();
            adapter.ForceOwnershipFault = true;
            LogAssert.Expect(
                LogType.Error,
                "Production environment ownership revalidation failed after " +
                "an additive scene topology change.");

            MethodInfo callback = typeof(ProductionEnvironmentController)
                .GetMethod(
                    "OnSceneLoaded",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(callback, Is.Not.Null);
            callback.Invoke(
                controller,
                new object[]
                {
                    SceneManager.GetActiveScene(),
                    LoadSceneMode.Additive,
                });

            Assert.That(adapter.OwnershipRevalidationCount, Is.EqualTo(1));
            Assert.That(controller.IsSimulationActive, Is.False);
            Assert.That(
                controller.LastFailure,
                Does.Contain("ownership revalidation failed"));
        }

        [Test]
        public void TimeAdvanceAndWetnessOverrides_AreValidatedAndCoherent()
        {
            var date = new GameDate(1995, 8, 3);
            Assert.That(
                controller.DevTrySetDateAndTime(
                    date,
                    18d * 3600d + 30d * 60d,
                    out string setFailure),
                Is.True,
                setFailure);
            Assert.That(
                controller.DevTryAdvanceGameSeconds(
                    3600d,
                    out string advanceFailure),
                Is.True,
                advanceFailure);
            Assert.That(
                controller.AuthoritativeGameTime.Snapshot.Date,
                Is.EqualTo(date));
            Assert.That(
                controller.AuthoritativeGameTime.Snapshot.SecondsOfDay,
                Is.EqualTo(19d * 3600d + 30d * 60d).Within(0.001d));

            Assert.That(
                controller.DevTrySetWetness(
                    0.1f,
                    0.2f,
                    0.3f,
                    0.4f,
                    out string wetnessFailure),
                Is.True,
                wetnessFailure);
            Assert.That(
                controller.AuthoritativeWetness.State.PuddleAmount01,
                Is.EqualTo(0.3f));
            Assert.That(
                controller.DevTrySetWetness(
                    1.1f,
                    0.2f,
                    0.3f,
                    0.4f,
                    out _),
                Is.False);
            Assert.That(
                controller.AuthoritativeWetness.State.GroundWetness01,
                Is.EqualTo(0.1f));
        }

        [Test]
        public void SeedReset_PreservesFrozenScheduleAndVisibleState()
        {
            Assert.That(
                controller.DevTryApplyWeatherOverride(
                    "weather.thunderstorm",
                    0f,
                    out string overrideFailure),
                Is.True,
                overrideFailure);
            controller.DevSetScheduleFrozen(true);

            Assert.That(
                controller.DevTryResetWeatherSeed(
                    987654321UL,
                    out string seedFailure),
                Is.True,
                seedFailure);
            Assert.That(
                controller.DevWeatherSeed,
                Is.EqualTo(987654321UL));
            Assert.That(controller.DevIsScheduleFrozen, Is.True);
            Assert.That(controller.DevManualWeatherOverrideActive, Is.True);
            Assert.That(
                controller.AuthoritativeWeather.CurrentState.Id.Value,
                Is.EqualTo("weather.thunderstorm"));
        }

        [Test]
        public void AmbientLightning_UsesExplicitBoundListener()
        {
            var cameraObject = new GameObject("PresentationCamera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(3f, 4f, 5f);
            Camera camera = cameraObject.AddComponent<Camera>();

            Assert.That(controller.BindPresentationCamera(camera), Is.True);
            CompleteInitialization(controller);
            var strikes = new List<LightningStrikeEvent>();
            var thunder = new List<ThunderAudioRequest>();
            controller.LightningOccurred += strikes.Add;
            controller.ThunderRequested += thunder.Add;
            Assert.That(
                controller.DevTryTriggerAmbientLightningAtListener(
                    out EnvironmentPresentationStatus status,
                    out string failure),
                Is.True,
                failure);
            Assert.That(status.IsOperational, Is.True);
            Assert.That(adapter.LastFrame.LightningVisual.IsRequested, Is.True);
            Assert.That(
                adapter.LastFrame.LightningVisual.WorldPosition,
                Is.EqualTo(cameraObject.transform.position));
            Assert.That(strikes, Has.Count.EqualTo(1));
            Assert.That(thunder, Has.Count.EqualTo(1));
            Assert.That(strikes[0].Kind, Is.EqualTo(LightningEventKind.AmbientVisual));
            Assert.That(thunder[0].Sequence, Is.EqualTo(strikes[0].Sequence));
            Assert.That(thunder[0].DelaySeconds, Is.Zero.Within(0.0001d));
        }

        [Test]
        public void AmbientLightning_WithoutBoundListener_PublishesNoThunder()
        {
            CompleteInitialization(controller);
            var strikes = new List<LightningStrikeEvent>();
            var thunder = new List<ThunderAudioRequest>();
            controller.LightningOccurred += strikes.Add;
            controller.ThunderRequested += thunder.Add;

            controller.TriggerAmbientLightning(
                new Vector3(343f, 0f, 0f),
                0.75f);

            Assert.That(strikes, Has.Count.EqualTo(1));
            Assert.That(strikes[0].Intensity01, Is.EqualTo(0.75f));
            Assert.That(thunder, Is.Empty);
        }

        [Test]
        public void GameplayLightning_PublishesExistingStrikeAndThunderOnce()
        {
            CompleteInitialization(controller);
            controller.AuthoritativeLightning.Advance(21d);
            var strikes = new List<LightningStrikeEvent>();
            var thunder = new List<ThunderAudioRequest>();
            controller.LightningOccurred += strikes.Add;
            controller.ThunderRequested += thunder.Add;
            var candidates = new[]
            {
                new LightningStrikeCandidate(
                    "weather.lightning.audio-test",
                    new Vector3(343f, 0f, 0f),
                    1f,
                    10f,
                    0f,
                    isProtected: false),
            };

            Assert.That(
                controller.TryCreateGameplayLightning(
                    candidates,
                    Array.Empty<LightningProtectionVolume>(),
                    Vector3.zero,
                    0.8f,
                    out GameplayLightningResult result),
                Is.True);

            Assert.That(strikes, Has.Count.EqualTo(1));
            Assert.That(thunder, Has.Count.EqualTo(1));
            Assert.That(strikes[0].Sequence, Is.EqualTo(result.StrikeEvent.Sequence));
            Assert.That(thunder[0].Sequence, Is.EqualTo(result.Thunder.Sequence));
            Assert.That(
                thunder[0].DelaySeconds,
                Is.EqualTo(1d).Within(0.0001d));
        }

        private static void CompleteInitialization(
            ProductionEnvironmentController target)
        {
            IEnumerator routine = target.InitializeBeforeWorldReveal();
            int safety = 0;
            while (routine.MoveNext())
            {
                safety++;
                if (safety > 20)
                {
                    Assert.Fail("Production initialization did not complete.");
                }
            }

            Assert.That(target.IsWorldRevealReady, Is.True);
        }

        private sealed class FakePresentationAdapter :
            MonoBehaviour,
            IEnvironmentPresentationAdapter,
            IEnvironmentPresentationCameraTarget,
            IEnvironmentPresentationSceneOwnershipGuard
        {
            private static readonly IReadOnlyList<
                EnvironmentPresentationDiagnostic> EmptyDiagnostics =
                Array.Empty<EnvironmentPresentationDiagnostic>();

            private bool attached;
            private EnvironmentPresentationStatus status =
                EnvironmentPresentationStatus.Detached;

            public EnvironmentPresentationFrame LastFrame { get; private set; }

            public bool ForceOwnershipFault { get; set; }

            public int OwnershipRevalidationCount { get; private set; }

            public bool IsAttached => attached;

            public EnvironmentPresentationCapabilities Capabilities =>
                EnvironmentPresentationCapabilities.TimeOfDay |
                EnvironmentPresentationCapabilities.Sky |
                EnvironmentPresentationCapabilities.SunMoonLighting |
                EnvironmentPresentationCapabilities.Clouds |
                EnvironmentPresentationCapabilities.Precipitation |
                EnvironmentPresentationCapabilities.Fog |
                EnvironmentPresentationCapabilities.Wind |
                EnvironmentPresentationCapabilities.LightningVisual |
                EnvironmentPresentationCapabilities.EnvironmentRefresh |
                EnvironmentPresentationCapabilities.QualityTiers;

            public EnvironmentPresentationStatus Status => status;

            public IReadOnlyList<EnvironmentPresentationDiagnostic>
                Diagnostics => EmptyDiagnostics;

            public EnvironmentPresentationStatus Attach()
            {
                attached = true;
                status = CreateReadyStatus(0UL);
                return status;
            }

            public EnvironmentPresentationStatus Present(
                in EnvironmentPresentationFrame frame)
            {
                LastFrame = frame;
                status = CreateReadyStatus(frame.Revision);
                return status;
            }

            public void Detach()
            {
                attached = false;
                status = EnvironmentPresentationStatus.Detached;
            }

            public bool TrySetPresentationCamera(Camera camera) =>
                camera != null;

            public EnvironmentPresentationStatus RevalidateSceneOwnership()
            {
                OwnershipRevalidationCount++;
                if (!ForceOwnershipFault)
                {
                    return status;
                }

                attached = false;
                status = new EnvironmentPresentationStatus(
                    EnvironmentPresentationState.Faulted,
                    EnvironmentPresentationCapabilities.None,
                    status.LastAppliedRevision,
                    0,
                    1);
                return status;
            }

            private EnvironmentPresentationStatus CreateReadyStatus(
                ulong revision) =>
                new EnvironmentPresentationStatus(
                    EnvironmentPresentationState.Ready,
                    Capabilities,
                    revision,
                    0,
                    0);
        }
    }
}
