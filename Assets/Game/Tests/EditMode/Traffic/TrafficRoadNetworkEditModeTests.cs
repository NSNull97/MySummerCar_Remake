using System;
using System.Linq;
using System.Reflection;
using MSC.Characters;
using MSC.Core.Time;
using MSC.NPC;
using MSC.Traffic;
using MSC.Vehicle.NWH;
using MSC.World.Streaming;
using NUnit.Framework;
using NWH.WheelController3D;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.Traffic
{
    public sealed class TrafficRoadNetworkEditModeTests
    {
        private const string CatalogPath =
            "Assets/Game/Traffic/Content/Phase1/" +
            "TrafficRoadNetworkCatalog.asset";
        private const string PresentationCatalogPath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Characters/" +
            "StoryTraffic/Generated/Resources/Phase1Traffic/" +
            "TrafficPresentationCatalog.asset";

        [Test]
        public void BusHeadingRecoveryPolicy_CanonicalizesWrongWayPoseAndUsesHysteresis()
        {
            Quaternion routeRotation = Quaternion.Euler(4f, 37f, -2f);
            Quaternion wrongWayRotation = routeRotation *
                Quaternion.Euler(0f, 180f, 0f);
            Quaternion alignedSavedRotation = routeRotation *
                Quaternion.Euler(0f, 25f, 0f);

            Assert.That(Quaternion.Angle(
                    TrafficWorldRuntime.ResolveBusMaterializationRotation(
                        wrongWayRotation,
                        routeRotation),
                    routeRotation),
                Is.LessThan(0.001f),
                "A valid near-route bus pose facing against BusRoute must " +
                "keep its position but restore the authored forward heading.");
            Assert.That(Quaternion.Angle(
                    TrafficWorldRuntime.ResolveBusMaterializationRotation(
                        alignedSavedRotation,
                        routeRotation),
                    alignedSavedRotation),
                Is.LessThan(0.001f),
                "An aligned saved bus pose must retain its physical rotation.");

            Assert.That(TrafficWorldRuntime.ResolveBusRouteRejoinActive(
                wasActive: false,
                routeSeparationMeters:
                    TrafficWorldRuntime.BusRouteCommitCorridorMeters,
                horizontalHeadingDot:
                    TrafficWorldRuntime.BusRouteRejoinEnterHeadingDot),
                Is.False,
                "The inclusive route corridor and enter threshold must not " +
                "chatter into recovery.");
            Assert.That(TrafficWorldRuntime.ResolveBusRouteRejoinActive(
                wasActive: false,
                routeSeparationMeters:
                    TrafficWorldRuntime.BusRouteCommitCorridorMeters + 0.01f,
                horizontalHeadingDot: 1f), Is.True);
            Assert.That(TrafficWorldRuntime.ResolveBusRouteRejoinActive(
                wasActive: false,
                routeSeparationMeters: 0f,
                horizontalHeadingDot:
                    TrafficWorldRuntime.BusRouteRejoinEnterHeadingDot - 0.01f),
                Is.True);
            Assert.That(TrafficWorldRuntime.ResolveBusRouteRejoinActive(
                wasActive: true,
                routeSeparationMeters:
                    TrafficWorldRuntime.BusRouteCommitCorridorMeters,
                horizontalHeadingDot:
                    TrafficWorldRuntime.BusRouteRejoinExitHeadingDot),
                Is.True,
                "Recovery must remain active at the exit heading threshold.");
            Assert.That(TrafficWorldRuntime.ResolveBusRouteRejoinActive(
                wasActive: true,
                routeSeparationMeters:
                    TrafficWorldRuntime.BusRouteCommitCorridorMeters,
                horizontalHeadingDot:
                    TrafficWorldRuntime.BusRouteRejoinExitHeadingDot + 0.01f),
                Is.False,
                "Recovery exits only after position and heading are both valid.");
            Assert.That(TrafficWorldRuntime
                    .ResolveBusRouteSpeedCapMetersPerSecond(
                        segmentIndex: 0,
                        routeSeparationMeters: 0f,
                        routeRejoinActive: true),
                Is.EqualTo(6.5f).Within(0.001f));
        }

        [Test]
        public void GeneratedCatalog_PreservesLockedRoadGraphAndMultiplicity()
        {
            TrafficRoadNetworkCatalog catalog = AssetDatabase.LoadAssetAtPath<
                TrafficRoadNetworkCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryValidate(out string failure), Is.True,
                failure);
            Assert.That(catalog.SourceSceneSha256, Is.EqualTo(
                "c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4"));
            Assert.That(catalog.Routes.Count, Is.EqualTo(16));
            Assert.That(catalog.Routes.Sum(route => route.WorldPoints.Count),
                Is.EqualTo(9320));
            Assert.That(catalog.Connections.Count, Is.EqualTo(48));
            Assert.That(catalog.Actors.Count, Is.EqualTo(11));
            Assert.That(catalog.Actors.Count(actor =>
                    actor.PresentationId == "presentation.traffic.victro"),
                Is.EqualTo(3));
            Assert.That(catalog.Actors.Count(actor =>
                    actor.PresentationId == "presentation.traffic.lamore"),
                Is.EqualTo(2));
            Assert.That(catalog.Actors.Select(actor => actor.StableInstanceId)
                    .Distinct().Count(),
                Is.EqualTo(11));
            Assert.That(catalog.Transports.Count, Is.EqualTo(4));
            Assert.That(catalog.Transports.Select(actor => actor.StableInstanceId)
                    .Distinct().Count(),
                Is.EqualTo(4));

            TrafficActorDefinition beerTruck = catalog.Actors.Single(actor =>
                actor.PresentationId == "presentation.traffic.truck");
            Assert.That(beerTruck.RoutePolicy,
                Is.EqualTo(TrafficRoutePolicy.LoopOnly),
                "TrafficCarExpansion excludes the Gifu beer truck from its town branch.");
            Assert.That(beerTruck.MinimumSpeedMetersPerSecond,
                Is.EqualTo(95f / 3.6f).Within(0.001f));
            Assert.That(beerTruck.MaximumSpeedMetersPerSecond,
                Is.EqualTo(105f / 3.6f).Within(0.001f));
            Assert.That(beerTruck.BaseLaneOffsetMeters,
                Is.EqualTo(2f).Within(0.001f));
            Assert.That(beerTruck.PassingLaneOffsetMeters,
                Is.EqualTo(-2f).Within(0.001f));

            TrafficActorDefinition pena = catalog.Actors.Single(actor =>
                actor.ActorId == "traffic.ambient.dirt-road.pena");
            Assert.That(pena.PresentationId,
                Is.EqualTo(TrafficCousinBehaviorRules.OrdinaryPresentationId));
            Assert.That(pena.VehicleFeatureId,
                Is.EqualTo("P1.VEHICLE.112"));
            Assert.That(pena.RouteId,
                Is.EqualTo("route.traffic.dirt-road"));
            Assert.That(pena.DrivingProfile,
                Is.EqualTo(TrafficDrivingProfile.DrunkCousin));
            Assert.That(pena.RoutePolicy,
                Is.EqualTo(TrafficRoutePolicy.LoopOnly));
            Assert.That(pena.BaseLaneOffsetMeters,
                Is.EqualTo(0f).Within(0.001f));
            Assert.That(pena.PassingLaneOffsetMeters,
                Is.EqualTo(-2f).Within(0.001f));
            Assert.That(pena.MinimumSpeedMetersPerSecond,
                Is.EqualTo(95f / 3.6f).Within(0.001f));
            Assert.That(pena.MaximumSpeedMetersPerSecond,
                Is.EqualTo(105f / 3.6f).Within(0.001f));

            Assert.That(catalog.Routes.Single(route =>
                    route.RouteId == "route.traffic.mod-town-entry")
                .WorldPoints.Count, Is.EqualTo(68));
            Assert.That(catalog.Routes.Single(route =>
                    route.RouteId == "route.traffic.mod-town-loop")
                .WorldPoints.Count, Is.EqualTo(321));
            Assert.That(catalog.Routes.Single(route =>
                    route.RouteId == "route.traffic.mod-gas-pump")
                .WorldPoints.Count, Is.EqualTo(112));
        }

        [Test]
        public void GeneratedCatalog_PreservesGreenMenaceProbabilityPolicy()
        {
            TrafficRoadNetworkCatalog catalog = AssetDatabase.LoadAssetAtPath<
                TrafficRoadNetworkCatalog>(CatalogPath);
            TrafficActorDefinition menace = catalog.Actors.Single(actor =>
                actor.PresentationId == "presentation.traffic.menace");
            Assert.That(menace.WeekdaySpawnProbability01,
                Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(menace.WeekendSpawnProbability01,
                Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(menace.InactiveRetryGameSeconds,
                Is.EqualTo(60f).Within(0.0001f));
            Assert.That(menace.MinimumSpeedMetersPerSecond,
                Is.EqualTo(115f / 3.6f).Within(0.001f));
            Assert.That(menace.MaximumSpeedMetersPerSecond,
                Is.EqualTo(185f / 3.6f).Within(0.001f));
        }

        [Test]
        public void GameClockAdvance_DoesNotMoveAmbientOrTransportActors()
        {
            TrafficRoadNetworkCatalog catalog = AssetDatabase.LoadAssetAtPath<
                TrafficRoadNetworkCatalog>(CatalogPath);
            TrafficPresentationCatalog presentation =
                AssetDatabase.LoadAssetAtPath<TrafficPresentationCatalog>(
                    PresentationCatalogPath);
            var clock = new GameTimeService();
            var runtimeObject = new GameObject("TrafficClockInvariantRuntime");
            var player = new GameObject("TrafficClockInvariantPlayer");
            player.transform.position =
                new Vector3(50000f, 50000f, 50000f);
            try
            {
                TrafficWorldRuntime runtime =
                    runtimeObject.AddComponent<TrafficWorldRuntime>();
                runtime.Initialize(catalog, presentation, clock, player.transform);
                AmbientTrafficActorStateDto[] ambientBefore =
                    runtime.CaptureAmbientActors();
                TransportTrafficActorStateDto[] transportBefore =
                    runtime.CaptureTransportActors();

                clock.Advance(900d);

                AmbientTrafficActorStateDto[] ambientAfter =
                    runtime.CaptureAmbientActors();
                TransportTrafficActorStateDto[] transportAfter =
                    runtime.CaptureTransportActors();
                foreach (AmbientTrafficActorStateDto before in ambientBefore)
                {
                    AmbientTrafficActorStateDto after = ambientAfter.Single(
                        value => value.actorId == before.actorId);
                    Assert.That(after.routeId, Is.EqualTo(before.routeId));
                    Assert.That(after.routeProgress01,
                        Is.EqualTo(before.routeProgress01).Within(0.000001f));
                    Assert.That(after.completedCircuits,
                        Is.EqualTo(before.completedCircuits));
                    Assert.That(after.spawnAttemptCount,
                        Is.EqualTo(before.spawnAttemptCount),
                        "Clock advance rerolled an ambient setup spawn.");
                    Assert.That(after.townDecisionGameSecondsRemaining,
                        Is.EqualTo(before.townDecisionGameSecondsRemaining)
                            .Within(0.000001f));
                }

                foreach (TransportTrafficActorStateDto before in transportBefore)
                {
                    TransportTrafficActorStateDto after = transportAfter.Single(
                        value => value.transportId == before.transportId);
                    Assert.That(after.routeId, Is.EqualTo(before.routeId));
                    Assert.That(after.routeProgress01,
                        Is.EqualTo(before.routeProgress01).Within(0.000001f));
                    Assert.That(after.completedTrips,
                        Is.EqualTo(before.completedTrips));
                    Assert.That(after.dwellGameSecondsRemaining,
                        Is.EqualTo(before.dwellGameSecondsRemaining)
                            .Within(0.000001f));
                }
            }
            finally
            {
                Object.DestroyImmediate(runtimeObject);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void RealTimeLogicalStep_AdvancesUnmaterializedSelectedRoot()
        {
            TrafficRoadNetworkCatalog catalog = AssetDatabase.LoadAssetAtPath<
                TrafficRoadNetworkCatalog>(CatalogPath);
            TrafficPresentationCatalog presentation =
                AssetDatabase.LoadAssetAtPath<TrafficPresentationCatalog>(
                    PresentationCatalogPath);
            var clock = new GameTimeService();
            var runtimeObject = new GameObject("TrafficLogicalStepRuntime");
            var player = new GameObject("TrafficLogicalStepPlayer");
            player.transform.position =
                new Vector3(50000f, 50000f, 50000f);
            try
            {
                TrafficWorldRuntime runtime =
                    runtimeObject.AddComponent<TrafficWorldRuntime>();
                runtime.Initialize(catalog, presentation, clock, player.transform);
                Assert.That(runtime.MaterializedActorCount, Is.Zero,
                    "EditMode must exercise logical traffic without prefabs.");

                TrafficActorDefinition truckDefinition = catalog.Actors.Single(
                    value => value.PresentationId ==
                             "presentation.traffic.truck");
                AmbientTrafficActorStateDto before = runtime
                    .CaptureAmbientActors()
                    .Single(value => value.actorId == truckDefinition.ActorId);
                AmbientTrafficActorStateDto penaBefore = runtime
                    .CaptureAmbientActors()
                    .Single(value => value.actorId ==
                        TrafficCousinBehaviorRules.ActorId);
                TrafficRouteDefinition route = catalog.Routes.Single(value =>
                    value.RouteId == truckDefinition.RouteId);
                var geometry = new TrafficRouteGeometry(route);
                float expected = geometry.AdvanceProgress(
                    before.routeProgress01,
                    before.travelsForward,
                    before.desiredSpeedMetersPerSecond,
                    out _);

                runtime.AdvanceLogicalAmbientActors(1f);

                AmbientTrafficActorStateDto after = runtime
                    .CaptureAmbientActors()
                    .Single(value => value.actorId == truckDefinition.ActorId);
                AmbientTrafficActorStateDto penaAfter = runtime
                    .CaptureAmbientActors()
                    .Single(value => value.actorId ==
                        TrafficCousinBehaviorRules.ActorId);
                Assert.That(after.routeProgress01,
                    Is.EqualTo(expected).Within(0.000001f));
                Assert.That(after.hasPhysicalPose, Is.False);
                Assert.That(after.currentSpeedMetersPerSecond,
                    Is.EqualTo(before.desiredSpeedMetersPerSecond)
                        .Within(0.000001f));
                Assert.That(penaAfter.routeProgress01,
                    Is.EqualTo(penaBefore.routeProgress01).Within(0.000001f),
                    "Named Pena traffic must remain physical authority.");
            }
            finally
            {
                Object.DestroyImmediate(runtimeObject);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void BusBootstrapUsesCurrentDonorPhaseWithoutClockCatchup()
        {
            TrafficRoadNetworkCatalog catalog = AssetDatabase.LoadAssetAtPath<
                TrafficRoadNetworkCatalog>(CatalogPath);
            TrafficPresentationCatalog presentation =
                AssetDatabase.LoadAssetAtPath<TrafficPresentationCatalog>(
                    PresentationCatalogPath);
            var reconstructedClock = new GameTimeService();
            var phaseClock = new GameTimeService();
            const double elapsedSimulationSeconds = 525d;
            reconstructedClock.Advance(elapsedSimulationSeconds);
            // Donor BUS Setup reads the discrete even-hour Time value and
            // places the bus directly on that phase index. It does not advance
            // route distance for minutes elapsed within the phase.
            phaseClock.Advance(450d);
            var reconstructedObject = new GameObject(
                "TrafficReconstructedRuntimeTest");
            var phaseObject = new GameObject(
                "TrafficPhaseRuntimeTest");
            var reconstructedPlayer = new GameObject(
                "TrafficReconstructedPlayerTest");
            var phasePlayer = new GameObject(
                "TrafficPhasePlayerTest");
            reconstructedPlayer.transform.position =
                new Vector3(50000f, 50000f, 50000f);
            phasePlayer.transform.position =
                reconstructedPlayer.transform.position;
            try
            {
                TrafficWorldRuntime reconstructed =
                    reconstructedObject.AddComponent<TrafficWorldRuntime>();
                TrafficWorldRuntime phase =
                    phaseObject.AddComponent<TrafficWorldRuntime>();
                reconstructed.Initialize(
                    catalog,
                    presentation,
                    reconstructedClock,
                    reconstructedPlayer.transform);
                phase.Initialize(
                    catalog,
                    presentation,
                    phaseClock,
                    phasePlayer.transform);

                AssertTransportStateEquivalent(
                    reconstructed,
                    phase,
                    "traffic.transport.bus");
            }
            finally
            {
                Object.DestroyImmediate(reconstructedObject);
                Object.DestroyImmediate(phaseObject);
                Object.DestroyImmediate(reconstructedPlayer);
                Object.DestroyImmediate(phasePlayer);
            }
        }

        [Test]
        public void BusCrossingNextPhaseHour_DoesNotMoveOrResetContinuousRoute()
        {
            TrafficRoadNetworkCatalog catalog = AssetDatabase.LoadAssetAtPath<
                TrafficRoadNetworkCatalog>(CatalogPath);
            TrafficPresentationCatalog presentation =
                AssetDatabase.LoadAssetAtPath<TrafficPresentationCatalog>(
                    PresentationCatalogPath);
            var singleClock = new GameTimeService();
            var splitClock = new GameTimeService();
            var singleObject = new GameObject("TrafficSingleBusAdvanceTest");
            var splitObject = new GameObject("TrafficSplitBusAdvanceTest");
            var singlePlayer = new GameObject("TrafficSingleBusPlayerTest");
            var splitPlayer = new GameObject("TrafficSplitBusPlayerTest");
            singlePlayer.transform.position =
                new Vector3(50000f, 50000f, 50000f);
            splitPlayer.transform.position = singlePlayer.transform.position;
            try
            {
                TrafficWorldRuntime single =
                    singleObject.AddComponent<TrafficWorldRuntime>();
                TrafficWorldRuntime split =
                    splitObject.AddComponent<TrafficWorldRuntime>();
                single.Initialize(catalog, presentation, singleClock,
                    singlePlayer.transform);
                split.Initialize(catalog, presentation, splitClock,
                    splitPlayer.transform);

                Assert.That(single.TryGetTransportState(
                    "traffic.transport.bus",
                    out TransportTrafficActorStateDto before), Is.True);

                // 750 simulation seconds crosses the next two-hour donor
                // phase boundary from the default 12:00 bootstrap. This is a
                // clock-only operation and therefore cannot move the bus.
                singleClock.Advance(750d);
                splitClock.Advance(449d);
                splitClock.Advance(301d);

                AssertTransportStateEquivalent(
                    single,
                    split,
                    "traffic.transport.bus");
                Assert.That(single.TryGetTransportState(
                    "traffic.transport.bus",
                    out TransportTrafficActorStateDto bus), Is.True);
                Assert.That(bus.active, Is.True,
                    "The donor bus loops continuously after bootstrap.");
                Assert.That(bus.completedTrips, Is.EqualTo(before.completedTrips));
                Assert.That(bus.routeProgress01,
                    Is.EqualTo(before.routeProgress01).Within(0.000001f));
            }
            finally
            {
                Object.DestroyImmediate(singleObject);
                Object.DestroyImmediate(splitObject);
                Object.DestroyImmediate(singlePlayer);
                Object.DestroyImmediate(splitPlayer);
            }
        }

        [Test]
        public void DonorTrafficGates_SelectRootsAndUseThirtyFiveMeterRearm()
        {
            Type rootType = typeof(TrafficWorldRuntime).GetNestedType(
                "AmbientTrafficRoot",
                BindingFlags.NonPublic);
            Assert.That(rootType, Is.Not.Null);
            object none = Enum.Parse(rootType, "None");
            object highway = Enum.Parse(rootType, "Highway");
            object dirtRoad = Enum.Parse(rootType, "DirtRoad");

            MethodInfo initialRoot = PrivateStatic(
                "ResolveInitialAmbientRoot");
            Assert.That(initialRoot.Invoke(null, null).ToString(),
                Is.EqualTo("Highway"),
                "A fresh/load session must not remain with both traffic " +
                "roots dormant.");

            MethodInfo discontinuousRecovery = PrivateStatic(
                "ResolveAmbientRootAfterDiscontinuousMove");
            Assert.That(discontinuousRecovery.Invoke(
                    null, new[] { none }).ToString(),
                Is.EqualTo("Highway"),
                "A legacy None root must recover after a load/teleport.");
            Assert.That(discontinuousRecovery.Invoke(
                    null, new[] { highway }).ToString(),
                Is.EqualTo("Highway"));
            Assert.That(discontinuousRecovery.Invoke(
                    null, new[] { dirtRoad }).ToString(),
                Is.EqualTo("DirtRoad"),
                "A teleport cannot invent a gate crossing once root " +
                "ownership is known.");

            MethodInfo resolver = PrivateStatic(
                "TryResolveAmbientRootTransition");
            Vector3 translation =
                TrafficWorldRuntime.SourceToProjectTrafficTranslation;
            var outCenter = new Vector3(1584f, 10.6f, 882.5f) +
                            translation;
            var inCenter = new Vector3(1585.34f, 10.6f, 888.04f) +
                           translation;
            Vector3 normal = Quaternion.Euler(0f, -166.408f, 0f) *
                             Vector3.forward;

            object[] outArguments =
            {
                outCenter - normal,
                outCenter + normal,
                null,
                -1,
                Vector3.zero,
            };
            Assert.That((bool)resolver.Invoke(null, outArguments), Is.True);
            Assert.That(outArguments[2].ToString(), Is.EqualTo("Highway"));
            Assert.That((int)outArguments[3], Is.EqualTo(0));
            Assert.That((Vector3)outArguments[4], Is.EqualTo(outCenter));

            object[] inArguments =
            {
                inCenter - normal,
                inCenter + normal,
                null,
                -1,
                Vector3.zero,
            };
            Assert.That((bool)resolver.Invoke(null, inArguments), Is.True);
            Assert.That(inArguments[2].ToString(), Is.EqualTo("DirtRoad"));

            MethodInfo rearm = PrivateStatic("IsInsideGateRearmDistance");
            Assert.That((bool)rearm.Invoke(null, new object[]
            {
                outCenter + Vector3.right * 34.99f,
                outCenter,
            }), Is.True);
            Assert.That((bool)rearm.Invoke(null, new object[]
            {
                outCenter + Vector3.right * 35.01f,
                outCenter,
            }), Is.False);
        }

        [Test]
        public void DonorTrafficRoots_PreserveMembershipWithBoundedResidency()
        {
            TrafficRoadNetworkCatalog catalog = AssetDatabase.LoadAssetAtPath<
                TrafficRoadNetworkCatalog>(CatalogPath);
            Type rootType = typeof(TrafficWorldRuntime).GetNestedType(
                "AmbientTrafficRoot",
                BindingFlags.NonPublic);
            Assert.That(rootType, Is.Not.Null);
            object none = Enum.Parse(rootType, "None");
            object highway = Enum.Parse(rootType, "Highway");
            object dirtRoad = Enum.Parse(rootType, "DirtRoad");
            MethodInfo membership = PrivateStatic("IsActorMemberOfRoot");

            Assert.That(catalog.Actors.Count(definition =>
                (bool)membership.Invoke(null, new[] { definition, highway })),
                Is.EqualTo(10));
            Assert.That(catalog.Actors.Count(definition =>
                (bool)membership.Invoke(null, new[] { definition, dirtRoad })),
                Is.EqualTo(1));
            Assert.That(catalog.Actors.Any(definition =>
                (bool)membership.Invoke(null, new[] { definition, none })),
                Is.False);
            Assert.That(catalog.Actors.Single(definition =>
                    (bool)membership.Invoke(null,
                        new[] { definition, dirtRoad })).ActorId,
                Is.EqualTo("traffic.ambient.dirt-road.pena"));
            Assert.That(
                TrafficWorldRuntime.IsPersistentNamedTrafficActor(
                    catalog.Actors.Single(definition =>
                        definition.ActorId ==
                        TrafficCousinBehaviorRules.ActorId)),
                Is.True,
                "Pena is named story traffic and must not freeze with a " +
                "player-distance/root LOD transition.");
            Assert.That(catalog.Actors.Count(
                    TrafficWorldRuntime.IsPersistentNamedTrafficActor),
                Is.EqualTo(1));
            Assert.That(TrafficWorldRuntime.AmbientMaterializeDistanceMeters,
                Is.EqualTo(440f));
            Assert.That(TrafficWorldRuntime.AmbientDematerializeDistanceMeters,
                Is.EqualTo(520f));
            Assert.That(
                TrafficWorldRuntime.MaximumMaterializedOrdinaryAmbientActors,
                Is.EqualTo(6));
        }

        [Test]
        public void SavedUnderMapPhysicalPose_IsRejectedBeforeMaterialization()
        {
            MethodInfo compatible = PrivateStatic(
                "IsPhysicalPoseCompatibleWithRoute");
            var logical = new Vector3(2126f, 5.7f, -1021f);

            Assert.That((bool)compatible.Invoke(null, new object[]
            {
                new Vector3(2126f, -42280f, -1021f),
                logical,
            }), Is.False,
                "A finite saved Rigidbody pose far below its route must not " +
                "be restored.");
            Assert.That((bool)compatible.Invoke(null, new object[]
            {
                logical + new Vector3(4f, -0.5f, 3f),
                logical,
            }), Is.True,
                "A nearby supported physical pose must remain restorable.");
        }

        [Test]
        public void PhysicalMaterializationBarrier_RequiresCurrentCellAndAllGlobals()
        {
            MethodInfo ready = PrivateStatic(
                "IsPhysicalTrafficMaterializationReady");
            var globals = new[]
            {
                new ProductionWorldGlobalScene(
                    "global-world",
                    1,
                    "Assets/Test/GlobalWorld.unity"),
                new ProductionWorldGlobalScene(
                    "global-support",
                    2,
                    "Assets/Test/GlobalSupport.unity"),
            };
            Func<string, bool> allGlobalsLoaded = _ => true;
            Func<string, bool> oneGlobalMissing = id =>
                id == "global-world";

            Assert.That((bool)ready.Invoke(null, new object[]
            {
                false,
                globals,
                allGlobalsLoaded,
            }), Is.False,
                "Global collision cannot substitute for an unloaded current " +
                "cell.");
            Assert.That((bool)ready.Invoke(null, new object[]
            {
                true,
                globals,
                oneGlobalMissing,
            }), Is.False,
                "Materialization must wait for every manifest-declared global " +
                "support scene.");
            Assert.That((bool)ready.Invoke(null, new object[]
            {
                true,
                globals,
                allGlobalsLoaded,
            }), Is.True);
        }

        [Test]
        public void TownExcursionCountdown_ConsumesRealSecondsDirectly()
        {
            MethodInfo countdown = PrivateStatic("AdvanceRealTimeCountdown");
            Assert.That((float)countdown.Invoke(null, new object[] { 90f, 1f }),
                Is.EqualTo(89f).Within(0.000001f));
            Assert.That((float)countdown.Invoke(null, new object[] { 1f, 2f }),
                Is.Zero);
            Assert.That((float)countdown.Invoke(null, new object[] { 45f, -2f }),
                Is.EqualTo(45f).Within(0.000001f));
        }

        private static MethodInfo PrivateStatic(string name)
        {
            MethodInfo method = typeof(TrafficWorldRuntime).GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, name);
            return method;
        }

        private static void AssertTransportStateEquivalent(
            TrafficWorldRuntime expectedRuntime,
            TrafficWorldRuntime actualRuntime,
            string transportId)
        {
            Assert.That(expectedRuntime.TryGetTransportState(
                transportId,
                out TransportTrafficActorStateDto expected), Is.True);
            Assert.That(actualRuntime.TryGetTransportState(
                transportId,
                out TransportTrafficActorStateDto actual), Is.True);
            Assert.That(actual.routeId, Is.EqualTo(expected.routeId));
            Assert.That(actual.active, Is.EqualTo(expected.active));
            Assert.That(actual.usingSecondaryRoute,
                Is.EqualTo(expected.usingSecondaryRoute));
            Assert.That(actual.completedTrips,
                Is.EqualTo(expected.completedTrips));
            Assert.That(actual.lastDepartureAbsoluteHour,
                Is.EqualTo(expected.lastDepartureAbsoluteHour));
            Assert.That(actual.nextStopIndex,
                Is.EqualTo(expected.nextStopIndex));
            Assert.That(actual.routeProgress01,
                Is.EqualTo(expected.routeProgress01).Within(0.0001f));
            Assert.That(actual.dwellGameSecondsRemaining,
                Is.EqualTo(expected.dwellGameSecondsRemaining).Within(0.01f));
        }

        private static int FindGasPumpAttempt(int seed, long dayIndex)
        {
            for (int attempt = 0; attempt < 256; attempt++)
            {
                if (ResolveExcursionRoll(
                        seed,
                        completedExcursions: 0,
                        attempt,
                        dayIndex,
                        salt: 0x713) >= 0.65f)
                {
                    return attempt;
                }
            }

            Assert.Fail("No deterministic gas-pump branch was found.");
            return 0;
        }

        private static float ResolveExcursionRoll(
            int seed,
            int completedExcursions,
            int attempt,
            long dayIndex,
            int salt)
        {
            unchecked
            {
                uint value = (uint)(seed ^ salt);
                value ^= (uint)completedExcursions * 0x9e3779b9u;
                value ^= (uint)attempt * 0x85ebca6bu;
                value ^= (uint)dayIndex * 0xc2b2ae35u;
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                value ^= value >> 16;
                return (value & 0x00ffffffu) / 16777215f;
            }
        }

        [Test]
        public void GeneratedPresentationCatalog_HasRoadAndTransportWrappers()
        {
            TrafficPresentationCatalog catalog = AssetDatabase.LoadAssetAtPath<
                TrafficPresentationCatalog>(PresentationCatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryValidate(out string failure), Is.True,
                failure);
            string[] expectedPresentationIds =
            {
                "presentation.traffic.victro",
                "presentation.traffic.lamore",
                "presentation.traffic.truck",
                "presentation.traffic.polsa",
                "presentation.traffic.fittan",
                "presentation.traffic.pena-fittan",
                "presentation.traffic.svoboda",
                "presentation.traffic.menace",
                "presentation.traffic.kuski",
                "presentation.traffic.event.rally-car-1",
                "presentation.traffic.event.rally-car-2",
                "presentation.traffic.event.rally-car-3",
                "presentation.traffic.event.drag-car-1",
                "presentation.traffic.event.drag-car-2",
                "presentation.traffic.event.police-car-1",
                "presentation.traffic.event.police-car-2",
                "presentation.traffic.bus",
                "presentation.traffic.train",
                "presentation.traffic.boat-1",
                "presentation.traffic.boat-2",
            };
            CollectionAssert.AreEquivalent(
                expectedPresentationIds,
                catalog.Entries.Select(value => value.PresentationId));
            foreach (TrafficPresentationCatalogEntry entry in catalog.Entries
                         .Where(value =>
                             value.PresentationId != "presentation.traffic.bus" &&
                             value.PresentationId != "presentation.traffic.train" &&
                             !value.PresentationId.StartsWith(
                                 "presentation.traffic.boat-")))
            {
                GameObject wrapper = entry.WrapperPrefab;
                StoryTrafficVehiclePresentationBinding motion = wrapper
                    .GetComponent<StoryTrafficVehiclePresentationBinding>();
                Assert.That(motion, Is.Not.Null, entry.PresentationId);
                Assert.That(motion.TryValidate(out failure), Is.True, failure);
                Assert.That(wrapper.GetComponent<Rigidbody>(), Is.Not.Null);
                Assert.That(wrapper.GetComponent<BoxCollider>(), Is.Not.Null);
                Assert.That(wrapper.GetComponent<NwhWheelPhysicsBackend>(),
                    Is.Not.Null);
                Assert.That(wrapper.GetComponent<
                    NwhStoryTrafficVehicleMotionBackend>(), Is.Not.Null);
                bool isCousinPresentation = entry.PresentationId ==
                        TrafficCousinBehaviorRules.OrdinaryPresentationId ||
                    entry.PresentationId ==
                        TrafficCousinBehaviorRules.SaturdayPresentationId;
                float expectedLane = isCousinPresentation ? 1.1f : 2f;
                Assert.That(motion.BaseLaneOffsetMeters,
                    Is.EqualTo(expectedLane).Within(0.001f));
                Assert.That(wrapper.GetComponentsInChildren<WheelController>(
                    true).Length, Is.EqualTo(4));
            }

            foreach (string id in new[]
                     {
                         "presentation.traffic.bus",
                         "presentation.traffic.train",
                         "presentation.traffic.boat-1",
                         "presentation.traffic.boat-2",
                     })
            {
                TrafficPresentationCatalogEntry entry = catalog.Entries.Single(
                    value => value.PresentationId == id);
                TrafficTransportPresentationBinding transport = entry
                    .WrapperPrefab.GetComponent<
                        TrafficTransportPresentationBinding>();
                Assert.That(transport, Is.Not.Null, id);
                Assert.That(transport.TryValidate(out failure), Is.True, failure);
                Assert.That(entry.WrapperPrefab.GetComponent<Rigidbody>(),
                    Is.Not.Null, id);
                Assert.That(entry.WrapperPrefab.GetComponent<BoxCollider>(),
                    Is.Not.Null, id);
                Assert.That(entry.WrapperPrefab.GetComponentsInChildren<
                    MonoBehaviour>(true).Any(component => component == null),
                    Is.False, $"{id} contains a missing script reference.");

                if (id == "presentation.traffic.bus")
                {
                    Assert.That(
                        transport.SupportsBusTerminalAbandonment,
                        Is.True,
                        "The generated bus must author the explicit terminal-" +
                        "abandonment presentation closure.");
                    Assert.That(entry.WrapperPrefab.GetComponent<
                        TrafficBusPassengerInteractionTarget>(), Is.Not.Null,
                        "The bus wrapper must expose the project-owned boarding target.");
                }
                else
                {
                    Assert.That(
                        transport.SupportsBusTerminalAbandonment,
                        Is.False,
                        $"{id} must import without bus-only door/driver data.");
                }
            }
        }

        [Test]
        public void GeneratedTransportCatalog_PreservesBusTrainAndBoatEvidence()
        {
            TrafficRoadNetworkCatalog catalog = AssetDatabase.LoadAssetAtPath<
                TrafficRoadNetworkCatalog>(CatalogPath);
            TrafficTransportDefinition bus = catalog.Transports.Single(value =>
                value.Kind == TrafficTransportKind.Bus);
            CollectionAssert.AreEqual(
                new[] { 0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22 },
                bus.BusDepartures.Select(value => value.Hour).ToArray());
            CollectionAssert.AreEqual(
                new[] { 3, 890, 482, 3, 890, 482, 3, 890, 482, 3, 890, 482 },
                bus.BusDepartures.Select(value =>
                    value.RouteStartPointIndex).ToArray());
            Assert.That(bus.SpeedMetersPerSecond,
                Is.EqualTo(82.5f / 3.6f).Within(0.0001f));
            float[] expectedStops = { 5207.049f, 7774.381f, 12027.058f };
            Assert.That(bus.RouteStops.Count, Is.EqualTo(expectedStops.Length));
            for (int index = 0; index < expectedStops.Length; index++)
            {
                Assert.That(bus.RouteStops[index].RouteDistanceMeters,
                    Is.EqualTo(expectedStops[index]).Within(0.001f));
                Assert.That(bus.RouteStops[index].DwellSimulationSeconds,
                    Is.EqualTo(25f).Within(0.001f));
            }

            TrafficTransportDefinition train = catalog.Transports.Single(value =>
                value.Kind == TrafficTransportKind.Train);
            Assert.That(train.SpeedMetersPerSecond,
                Is.EqualTo(30f).Within(0.0001f));
            Assert.That(train.EndpointDelaySimulationSeconds,
                Is.EqualTo(250f).Within(0.0001f));
            Assert.That(train.SecondaryRouteId,
                Is.EqualTo("route.traffic.train-west-to-east"));

            Assert.That(catalog.Transports.Count(value =>
                value.Kind == TrafficTransportKind.Boat), Is.EqualTo(2));
            Assert.That(catalog.Routes.Single(value =>
                    value.RouteId == "route.traffic.boat-1").WorldPoints.Count,
                Is.EqualTo(8));
            Assert.That(catalog.Routes.Single(value =>
                    value.RouteId == "route.traffic.boat-2").WorldPoints.Count,
                Is.EqualTo(8));
            TrafficTransportDefinition boatTwo = catalog.Transports.Single(
                value => value.TransportId == "traffic.transport.boat-2");
            Assert.That(boatTwo.PrimaryRouteId,
                Is.EqualTo("route.traffic.boat-2"));
            Assert.That(boatTwo.SecondaryRouteId,
                Is.EqualTo("route.traffic.boat-1"),
                "Boat 2 spawns on Waypoints2 and then joins Waypoints1.");
        }

        [Test]
        public void RouteGeometry_ProjectsAndAdvancesWithoutSplineShortcut()
        {
            var route = new TrafficRouteDefinition(
                "route.traffic.test-loop",
                "test/loop",
                configuredClosesLoop: true,
                TrafficRouteSurface.Paved,
                configuredNominalRoadWidthMeters: 6f,
                new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(20f, 0f, 0f),
                    new Vector3(20f, 0f, 20f),
                    new Vector3(0f, 0f, 20f),
                });
            var geometry = new TrafficRouteGeometry(route);
            int hint = -1;
            bool projected = geometry.TryProjectAndResolveAhead(
                new Vector3(9f, 0f, 1f),
                retainedProgress01: 0.1f,
                forward: true,
                lookAheadMeters: 8f,
                ref hint,
                out TrafficRouteSample onRoute,
                out TrafficRouteSample guidance);
            Assert.That(projected, Is.True);
            Assert.That(onRoute.Position.z, Is.EqualTo(0f).Within(0.01f));
            Assert.That(guidance.Position.x, Is.GreaterThan(onRoute.Position.x));
            float wrapped = geometry.AdvanceProgress(
                0.98f,
                forward: true,
                distanceMeters: 4f,
                out bool completed);
            Assert.That(completed, Is.True);
            Assert.That(wrapped, Is.LessThan(0.1f));
            Assert.That(
                geometry.CanCommitPhysicalProjection(
                    retainedProgress01: 0.4f,
                    projectedProgress01: 0.2f,
                    forward: true),
                Is.False,
                "A reverse recovery must not rewind persistent route state.");
            Assert.That(
                geometry.CanCommitPhysicalProjection(
                    retainedProgress01: 0.98f,
                    projectedProgress01: 0.02f,
                    forward: true),
                Is.True,
                "A forward loop wrap must remain a valid projection.");
        }

        [Test]
        public void RouteGeometry_AlignedProjectionSelectsMatchingOverlapBranch()
        {
            var route = new TrafficRouteDefinition(
                "route.traffic.test-overlap",
                "test/overlap",
                configuredClosesLoop: true,
                TrafficRouteSurface.Gravel,
                configuredNominalRoadWidthMeters: 5.6f,
                new[]
                {
                    new Vector3(-20f, 0f, 0f),
                    new Vector3(0f, 0f, 0f),
                    new Vector3(20f, 0f, 0f),
                    new Vector3(20f, 0f, 1f),
                    new Vector3(0f, 0f, 1f),
                    new Vector3(-20f, 0f, 1f),
                });
            var geometry = new TrafficRouteGeometry(route);
            Vector3 position = new(1f, 0f, 0.8f);

            TrafficRouteSample nearest = geometry.ProjectNearest(
                position,
                forward: true);
            Assert.That(
                Vector3.Dot(
                    nearest.Rotation * Vector3.forward,
                    Vector3.right),
                Is.LessThan(-0.9f),
                "The geometrically nearest branch should be the opposing " +
                "return branch in this regression fixture.");

            Assert.That(geometry.TryProjectNearestAligned(
                position,
                Vector3.right,
                forward: true,
                maximumDistanceMeters: 2f,
                minimumAlignment: 0.2f,
                out TrafficRouteSample aligned), Is.True);
            Assert.That(aligned.Position.z, Is.EqualTo(0f).Within(0.01f));
            Assert.That(
                Vector3.Dot(
                    aligned.Rotation * Vector3.forward,
                    Vector3.right),
                Is.GreaterThan(0.9f),
                "Recovery must select the nearby branch that continues in " +
                "the physical vehicle's heading.");
        }

        [Test]
        public void RouteGeometry_ClosedPointRangeSkipsExcludedRouteClosure()
        {
            var route = new TrafficRouteDefinition(
                "route.traffic.test-embedded-loop",
                "test/embedded-loop",
                configuredClosesLoop: true,
                TrafficRouteSurface.Gravel,
                configuredNominalRoadWidthMeters: 5.6f,
                new[]
                {
                    new Vector3(-100f, 0f, 0f),
                    new Vector3(-50f, 0f, 0f),
                    new Vector3(0f, 0f, 0f),
                    new Vector3(10f, 0f, 0f),
                    new Vector3(10f, 0f, 10f),
                    new Vector3(0f, 0f, 10f),
                });
            var geometry = new TrafficRouteGeometry(route);
            int hint = 5;

            Assert.That(
                geometry.TryProjectAndResolveAheadWithinClosedPointRange(
                    new Vector3(0.25f, 0f, 8f),
                    geometry.ProgressAtPointIndex(5),
                    forward: true,
                    lookAheadMeters: 4f,
                    firstPointIndex: 2,
                    lastPointIndex: 5,
                    ref hint,
                    out TrafficRouteSample projection,
                    out TrafficRouteSample guidance),
                Is.True);
            Assert.That(projection.SegmentIndex, Is.EqualTo(5));
            Assert.That(guidance.Position.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(guidance.Position.z, Is.LessThan(8f));
            Assert.That(
                Vector3.Dot(
                    guidance.Rotation * Vector3.forward,
                    Vector3.back),
                Is.GreaterThan(0.9f),
                "The embedded loop must close from point 5 to point 2, not " +
                "through excluded points 0 and 1.");

            Assert.That(
                geometry.TryResolveAheadWithinClosedPointRange(
                    geometry.ProgressAtPointIndex(5),
                    forward: true,
                    distanceMeters: 12f,
                    firstPointIndex: 2,
                    lastPointIndex: 5,
                    out TrafficRouteSample wrappedAhead),
                Is.True);
            Assert.That(wrappedAhead.Position.x, Is.GreaterThan(1.5f));
            Assert.That(wrappedAhead.Position.x, Is.LessThan(3f));
            Assert.That(wrappedAhead.Position.z, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void ReverseEscape_UsesBoundsForNonConvexSupportMesh()
        {
            var supportObject = new GameObject(
                "Traffic_NonConvexReverseSupport");
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-2f, 0f, -2f),
                    new Vector3(2f, 0f, -2f),
                    new Vector3(-2f, 0f, 2f),
                    new Vector3(2f, 0f, 2f),
                },
                triangles = new[] { 0, 2, 1, 1, 2, 3 },
            };
            mesh.RecalculateNormals();
            var collider = supportObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = false;
            MethodInfo method = typeof(
                    StoryTrafficVehiclePresentationBinding)
                .GetMethod(
                    "ResolveSafeClosestPoint",
                    BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null);
            var result = (Vector3)method.Invoke(
                null,
                new object[] { collider, new Vector3(3f, 1f, 0f) });
            Assert.That(float.IsFinite(result.x), Is.True);
            Assert.That(float.IsFinite(result.y), Is.True);
            Assert.That(float.IsFinite(result.z), Is.True);
            Assert.That(result.x, Is.EqualTo(2f).Within(0.01f));

            Object.DestroyImmediate(supportObject);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void TrafficStateDto_DeepClonesAdditiveAmbientPayload()
        {
            var dto = new StoryTrafficStateDto
            {
                drivers = new[]
                {
                    Driver("character.jani",
                        "10000000000000000000000000000001"),
                    Driver("character.petteri",
                        "10000000000000000000000000000002"),
                },
                suski = new SuskiRescueStateDto
                {
                    stableInstanceId =
                        "10000000000000000000000000000003",
                },
                ambientActors = new[]
                {
                    new AmbientTrafficActorStateDto
                    {
                        actorId = "traffic.ambient.highway.victro.01",
                        stableInstanceId =
                            "10000000000000000000000000000004",
                        routeId = "route.traffic.highway",
                        routeProgress01 = 0.42f,
                        desiredSpeedMetersPerSecond = 24f,
                        cruiseSpeedMetersPerSecond = 24f,
                        townExcursionPending = true,
                        townDecisionGameSecondsRemaining = 45f,
                        townDecisionAttemptCount = 3,
                        completedTownExcursions = 2,
                        townExcursionDestination = 2,
                    },
                },
                transportActors = new[]
                {
                    new TransportTrafficActorStateDto
                    {
                        transportId = "traffic.transport.bus",
                        stableInstanceId =
                            "10000000000000000000000000000005",
                        kind = (int)TrafficTransportKind.Bus,
                        routeId = "route.traffic.bus",
                        routeProgress01 = 0.25f,
                        active = true,
                        busAbandonmentPhase =
                            (int)BusTerminalAbandonmentPhase.ShutdownDelay,
                        busStallMonitorArmed = true,
                        busStuckRealSeconds = 25f,
                        busShutdownDelayRealSecondsRemaining = 2.5f,
                        busHasForwardProgressAnchor = true,
                        busForwardProgressAnchor01 = 0.2f,
                        busForwardProgressAnchorCompletedTrips = 2,
                        busRecoveryCountAtForwardProgress = 4,
                        busRecoveryEpisodeActive = true,
                        busRecoveryExhausted = true,
                        hasBusDriverPose = false,
                        busDriverCurseCooldownRealSeconds = 12f,
                    },
                },
            };
            Assert.That(dto.TryValidate(out string failure), Is.True, failure);
            StoryTrafficStateDto clone = dto.DeepClone();
            clone.ambientActors[0].routeProgress01 = 0.8f;
            clone.ambientActors[0].completedTownExcursions = 5;
            clone.transportActors[0].routeProgress01 = 0.9f;
            Assert.That(dto.ambientActors[0].routeProgress01,
                Is.EqualTo(0.42f).Within(0.0001f));
            Assert.That(dto.ambientActors[0].completedTownExcursions,
                Is.EqualTo(2));
            Assert.That(dto.ambientActors[0].townExcursionDestination,
                Is.EqualTo(2));
            Assert.That(dto.transportActors[0].routeProgress01,
                Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(clone.transportActors[0].busAbandonmentPhase,
                Is.EqualTo((int)BusTerminalAbandonmentPhase.ShutdownDelay));
            Assert.That(clone.transportActors[0]
                    .busShutdownDelayRealSecondsRemaining,
                Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(clone.transportActors[0]
                    .busHasForwardProgressAnchor,
                Is.True);
            Assert.That(clone.transportActors[0]
                    .busForwardProgressAnchor01,
                Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(clone.transportActors[0]
                    .busForwardProgressAnchorCompletedTrips,
                Is.EqualTo(2));
            Assert.That(clone.transportActors[0]
                    .busRecoveryCountAtForwardProgress,
                Is.EqualTo(4));
            Assert.That(clone.transportActors[0]
                    .busRecoveryEpisodeActive,
                Is.True);
            Assert.That(clone.transportActors[0]
                    .busRecoveryExhausted,
                Is.True);
            Assert.That(clone.transportActors[0].hasBusDriverPose, Is.False);
            Assert.That(clone.transportActors[0]
                    .busDriverCurseCooldownRealSeconds,
                Is.EqualTo(12f).Within(0.0001f));
            Assert.That(clone.TryValidate(out failure), Is.True, failure);

            static StoryTrafficDriverStateDto Driver(
                string definitionId,
                string stableId) => new StoryTrafficDriverStateDto
                {
                    characterDefinitionId = definitionId,
                    stableInstanceId = stableId,
                };
        }

        [Test]
        public void TrafficStateDto_PreAbandonmentTransportDefaultsRemainValid()
        {
            const string legacyTransportJson =
                "{\"transportId\":\"traffic.transport.bus\"," +
                "\"stableInstanceId\":\"20000000000000000000000000000004\"," +
                "\"kind\":0,\"routeId\":\"route.traffic.bus\"," +
                "\"active\":true}";
            TransportTrafficActorStateDto restoredLegacy =
                JsonUtility.FromJson<TransportTrafficActorStateDto>(
                    legacyTransportJson);
            var dto = new StoryTrafficStateDto
            {
                drivers = new[]
                {
                    new StoryTrafficDriverStateDto
                    {
                        characterDefinitionId = "character.jani",
                        stableInstanceId =
                            "20000000000000000000000000000001",
                    },
                    new StoryTrafficDriverStateDto
                    {
                        characterDefinitionId = "character.petteri",
                        stableInstanceId =
                            "20000000000000000000000000000002",
                    },
                },
                suski = new SuskiRescueStateDto
                {
                    stableInstanceId =
                        "20000000000000000000000000000003",
                },
                transportActors = new[]
                {
                    restoredLegacy,
                },
            };

            Assert.That(dto.TryValidate(out string failure), Is.True, failure);
            Assert.That(dto.transportActors[0].busAbandonmentPhase,
                Is.EqualTo((int)BusTerminalAbandonmentPhase.Monitoring));
            Assert.That(dto.transportActors[0].busStallMonitorArmed, Is.False);
            Assert.That(dto.transportActors[0].busStuckRealSeconds, Is.Zero);
            Assert.That(dto.transportActors[0]
                    .busShutdownDelayRealSecondsRemaining,
                Is.Zero);
            Assert.That(dto.transportActors[0]
                    .busHasForwardProgressAnchor,
                Is.False);
            Assert.That(dto.transportActors[0]
                    .busForwardProgressAnchor01,
                Is.Zero);
            Assert.That(dto.transportActors[0]
                    .busRecoveryCountAtForwardProgress,
                Is.Zero);
            Assert.That(dto.transportActors[0]
                    .busRecoveryExhausted,
                Is.False);
            Assert.That(dto.transportActors[0].hasBusDriverPose, Is.False);
            Assert.That(dto.transportActors[0].busDriverWorldRotation,
                Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void TrafficStateDto_BusAbandonmentIntermediateStatesRoundTrip()
        {
            TransportTrafficActorStateDto[] expected =
            {
                new()
                {
                    transportId = "traffic.transport.bus",
                    stableInstanceId =
                        "30000000000000000000000000000004",
                    kind = (int)TrafficTransportKind.Bus,
                    routeId = "route.traffic.bus",
                    active = true,
                    busAbandonmentPhase =
                        (int)BusTerminalAbandonmentPhase.StuckConfirmation,
                    busStallMonitorArmed = true,
                    busStuckRealSeconds = 12.5f,
                },
                new()
                {
                    transportId = "traffic.transport.bus",
                    stableInstanceId =
                        "40000000000000000000000000000004",
                    kind = (int)TrafficTransportKind.Bus,
                    routeId = "route.traffic.bus",
                    active = true,
                    busAbandonmentPhase =
                        (int)BusTerminalAbandonmentPhase.ShutdownDelay,
                    busStallMonitorArmed = true,
                    busStuckRealSeconds = 25f,
                    busShutdownDelayRealSecondsRemaining = 2.25f,
                },
                new()
                {
                    transportId = "traffic.transport.bus",
                    stableInstanceId =
                        "50000000000000000000000000000004",
                    kind = (int)TrafficTransportKind.Bus,
                    routeId = "route.traffic.bus",
                    active = true,
                    busAbandonmentPhase =
                        (int)BusTerminalAbandonmentPhase.Abandoned,
                    busStallMonitorArmed = true,
                    busStuckRealSeconds = 25f,
                    hasBusDriverPose = true,
                    busDriverWorldPosition = new Vector3(4f, 5f, 6f),
                    busDriverWorldRotation = Quaternion.Euler(0f, 37f, 0f),
                    busDriverCurseCooldownRealSeconds = 19.5f,
                },
            };

            for (int index = 0; index < expected.Length; index++)
            {
                TransportTrafficActorStateDto restored = JsonUtility.FromJson<
                    TransportTrafficActorStateDto>(
                    JsonUtility.ToJson(expected[index]));
                StoryTrafficStateDto document = CreateStoryState(restored);
                Assert.That(document.TryValidate(out string failure), Is.True,
                    $"row {index}: {failure}");
                Assert.That(restored.busAbandonmentPhase,
                    Is.EqualTo(expected[index].busAbandonmentPhase));
                Assert.That(restored.busStuckRealSeconds,
                    Is.EqualTo(expected[index].busStuckRealSeconds)
                        .Within(0.0001f));
                Assert.That(restored
                        .busShutdownDelayRealSecondsRemaining,
                    Is.EqualTo(expected[index]
                            .busShutdownDelayRealSecondsRemaining)
                        .Within(0.0001f));
                Assert.That(restored.hasBusDriverPose,
                    Is.EqualTo(expected[index].hasBusDriverPose));
                Assert.That(restored.busDriverCurseCooldownRealSeconds,
                    Is.EqualTo(expected[index]
                            .busDriverCurseCooldownRealSeconds)
                        .Within(0.0001f));
            }

            static StoryTrafficStateDto CreateStoryState(
                TransportTrafficActorStateDto transport) => new()
            {
                drivers = new[]
                {
                    new StoryTrafficDriverStateDto
                    {
                        characterDefinitionId = "character.jani",
                        stableInstanceId =
                            "60000000000000000000000000000001",
                    },
                    new StoryTrafficDriverStateDto
                    {
                        characterDefinitionId = "character.petteri",
                        stableInstanceId =
                            "60000000000000000000000000000002",
                    },
                },
                suski = new SuskiRescueStateDto
                {
                    stableInstanceId =
                        "60000000000000000000000000000003",
                },
                transportActors = new[] { transport },
            };
        }

        [Test]
        public void TrafficStateDto_RejectsBusPayloadOnNonBusAndEarlyDriverPose()
        {
            var nonBus = new TransportTrafficActorStateDto
            {
                transportId = "traffic.transport.train",
                stableInstanceId = "70000000000000000000000000000001",
                kind = (int)TrafficTransportKind.Train,
                routeId = "route.traffic.train",
                busStallMonitorArmed = true,
            };
            var earlyPose = new TransportTrafficActorStateDto
            {
                transportId = "traffic.transport.bus",
                stableInstanceId = "70000000000000000000000000000002",
                kind = (int)TrafficTransportKind.Bus,
                routeId = "route.traffic.bus",
                busAbandonmentPhase =
                    (int)BusTerminalAbandonmentPhase.ShutdownDelay,
                busStallMonitorArmed = true,
                busStuckRealSeconds = 25f,
                busShutdownDelayRealSecondsRemaining = 2f,
                hasBusDriverPose = true,
            };

            Assert.That(Story(nonBus).TryValidate(out _), Is.False);
            Assert.That(Story(earlyPose).TryValidate(out _), Is.False);

            static StoryTrafficStateDto Story(
                TransportTrafficActorStateDto transport) => new()
            {
                drivers = new[]
                {
                    new StoryTrafficDriverStateDto
                    {
                        characterDefinitionId = "character.jani",
                        stableInstanceId =
                            "80000000000000000000000000000001",
                    },
                    new StoryTrafficDriverStateDto
                    {
                        characterDefinitionId = "character.petteri",
                        stableInstanceId =
                            "80000000000000000000000000000002",
                    },
                },
                suski = new SuskiRescueStateDto
                {
                    stableInstanceId =
                        "80000000000000000000000000000003",
                },
                transportActors = new[] { transport },
            };
        }
    }
}
