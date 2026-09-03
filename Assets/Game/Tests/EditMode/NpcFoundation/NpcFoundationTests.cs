using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MSC.Characters;
using MSC.Core.Time;
using MSC.LegacyImport;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.NPC.Tests.EditMode
{
    public sealed class NpcFoundationTests
    {
        private CharacterDefinitionCatalog characters;
        private NpcFoundationCatalog foundation;
        private NpcSimulation simulation;

        [Test]
        public void DialogueInteractionPrompt_UsesNaturalRussianInfinitive()
        {
            var targetObject = new GameObject("Dialogue prompt target");
            try
            {
                NpcDialogueInteractionTarget target =
                    targetObject.AddComponent<NpcDialogueInteractionTarget>();
                Assert.That(target.InteractionPrompt, Is.EqualTo("Поговорить"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [SetUp]
        public void SetUp()
        {
            characters = ScriptableObject.CreateInstance<
                CharacterDefinitionCatalog>();
            characters.ConfigureForAuthoring(
                "characters.test.foundation",
                new[]
                {
                    new CharacterDefinition(
                        "character.fixture.test",
                        "P1.NPC.TEST",
                        "test fixture",
                        "10a000000000000000000000000000ff",
                        "presentation.character.fixture.test",
                        "presentation.character.fixture.test",
                        "anchor.fixture.start",
                        "anchor.fixture.end",
                        CharacterFixturePattern.ScheduledRoaming,
                        isFrameworkFixtureOnly: true),
                });
            foundation = ScriptableObject.CreateInstance<NpcFoundationCatalog>();
            foundation.ConfigureForAuthoring(
                "npc.test.foundation",
                new[]
                {
                    new NpcAnchorDefinition(
                        "anchor.fixture.start",
                        "cell_0_0",
                        Vector3.zero,
                        Vector3.zero),
                    new NpcAnchorDefinition(
                        "anchor.fixture.end",
                        "cell_0_0",
                        new Vector3(10f, 0f, 0f),
                        new Vector3(0f, 90f, 0f)),
                },
                new[]
                {
                    new NpcRouteDefinition(
                        "route.fixture.test",
                        "anchor.fixture.start",
                        "anchor.fixture.end",
                        1800d,
                        NpcRouteTraversalMode.PingPong),
                },
                new[]
                {
                    new NpcScheduleBlock(
                        "schedule.fixture.test",
                        "character.fixture.test",
                        127,
                        0d,
                        86400d,
                        "anchor.fixture.start",
                        "route.fixture.test",
                        CharacterActivityState.Walking),
                });
            simulation = new NpcSimulation(characters, foundation);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(characters);
            UnityEngine.Object.DestroyImmediate(foundation);
        }

        [Test]
        public void StoryTrafficStateDto_ValidatesStableIncidentIdentities()
        {
            var dto = new StoryTrafficStateDto
            {
                drivers = new[]
                {
                    new StoryTrafficDriverStateDto
                    {
                        characterDefinitionId = "character.jani",
                        stableInstanceId =
                            "10b30000000000000000000000000049",
                        worldRotation = Quaternion.identity,
                    },
                    new StoryTrafficDriverStateDto
                    {
                        characterDefinitionId = "character.petteri",
                        stableInstanceId =
                            "10b30000000000000000000000000050",
                        worldRotation = Quaternion.identity,
                    },
                },
                suski = new SuskiRescueStateDto
                {
                    stableInstanceId =
                        "10b20000000000000000000000000006",
                    stage = SuskiRescueStage.RestingAtParentsBed,
                    worldRotation = Quaternion.identity,
                },
            };

            Assert.That(dto.TryValidate(out string failure), Is.True, failure);
            StoryTrafficStateDto clone = dto.DeepClone();
            clone.drivers[0].terminalCrash = true;
            clone.drivers[0].teimoSocialStopConsumed = true;
            clone.drivers[0].teimoSocialStopSecondsRemaining = 6f;
            clone.drivers[1].routeFullStopReached = true;
            Assert.That(dto.drivers[0].terminalCrash, Is.False);
            Assert.That(dto.drivers[0].teimoSocialStopConsumed, Is.False);
            Assert.That(dto.drivers[1].routeFullStopReached, Is.False);
            Assert.That(clone.TryValidate(out failure), Is.True, failure);
            clone.suski.stage = SuskiRescueStage.CrashedInCar;
            Assert.That(clone.TryValidate(out failure), Is.True, failure);

            clone.drivers[1].stableInstanceId =
                clone.drivers[0].stableInstanceId;
            Assert.That(clone.TryValidate(out _), Is.False);
        }

        [Test]
        public void Catalogs_UseProjectOwnedUniqueStableIdentity()
        {
            Assert.That(characters.ValidateConfiguration(), Is.Empty);
            Assert.That(foundation.ValidateConfiguration(characters), Is.Empty);
            Assert.That(
                characters.Definitions.Single().StableInstanceId,
                Has.Length.EqualTo(32));
        }

        [Test]
        public void PhysicalRouteAuthority_ProgressesFromChassisAndResumesScheduleOnlyAfterRelease()
        {
            simulation.Evaluate(0, 0d, 0d);
            Assert.That(
                simulation.TryGetInstance(
                    "character.fixture.test",
                    out CharacterInstance instance),
                Is.True);
            simulation.SetPhysicalRouteAuthority(
                instance.Definition.DefinitionId,
                true);

            Assert.That(
                simulation.TryUpdatePhysicalRoute(
                    instance,
                    new Vector3(4f, 0f, 0f),
                    2f,
                    out NpcPose guidance,
                    out float physicalProgress),
                Is.True);
            Assert.That(physicalProgress, Is.EqualTo(0.4f).Within(0.04f));
            Assert.That(guidance.Position.x, Is.GreaterThan(physicalProgress * 10f));

            double retainedProgress = instance.RouteProgress01;
            simulation.Evaluate(0, 1700d, 1700d);
            Assert.That(
                instance.RouteProgress01,
                Is.EqualTo(retainedProgress).Within(0.0001d),
                "An active physical car must not jump ahead with the schedule clock.");

            simulation.SetPhysicalRouteAuthority(
                instance.Definition.DefinitionId,
                false);
            simulation.Evaluate(0, 1700d, 0d);
            Assert.That(instance.RouteProgress01,
                Is.GreaterThan(retainedProgress),
                "Generic schedule motion must resume only after physical authority is explicitly released; the world runtime now keeps named story traffic under physical authority globally.");
        }

        [Test]
        public void PhysicalRouteAuthority_ProjectionToleranceNeverCommitsBackwardJitter()
        {
            simulation.Evaluate(0, 0d, 0d);
            CharacterInstance instance = simulation.Instances.Single();
            simulation.SetPhysicalRouteAuthority(
                instance.Definition.DefinitionId,
                true);

            Assert.That(
                simulation.TryUpdatePhysicalRoute(
                    instance,
                    new Vector3(4f, 0f, 0f),
                    2f,
                    out _,
                    out float forwardProgress),
                Is.True);
            Assert.That(
                simulation.TryUpdatePhysicalRoute(
                    instance,
                    new Vector3(3.995f, 0f, 0f),
                    2f,
                    out _,
                    out float jitteredProgress),
                Is.True);

            Assert.That(
                jitteredProgress,
                Is.EqualTo(forwardProgress).Within(0.000001f),
                "Projection tolerance may retain the sample hint, but must not move authoritative route progress backwards.");
            Assert.That(
                instance.RouteProgress01,
                Is.EqualTo(forwardProgress).Within(0.000001d));
        }

        [Test]
        public void StoryTrafficResidency_PinsActiveAndCrashedCarsPhysically()
        {
            Assert.That(
                NpcWorldRuntime.ShouldKeepStoryTrafficPhysical(
                    CharacterActivityState.VehicleSeated,
                    "route.story-traffic.jani-race",
                    terminalCrash: false),
                Is.True);
            Assert.That(
                NpcWorldRuntime.ShouldKeepStoryTrafficPhysical(
                    CharacterActivityState.Hidden,
                    string.Empty,
                    terminalCrash: true),
                Is.True);
            Assert.That(
                NpcWorldRuntime.ShouldKeepStoryTrafficPhysical(
                    CharacterActivityState.Hidden,
                    string.Empty,
                    terminalCrash: false),
                Is.False);
        }

        [Test]
        public void StoryTrafficLookAhead_IsShorterInPerajarviThanOnRoadRace()
        {
            const float speedMetersPerSecond = 30f;
            float town = NpcWorldRuntime.ResolveStoryTrafficLookAheadMeters(
                StoryTrafficRoadBehaviorProfile.Perajarvi,
                speedMetersPerSecond);
            float roadRace = NpcWorldRuntime.ResolveStoryTrafficLookAheadMeters(
                StoryTrafficRoadBehaviorProfile.RoadRace,
                speedMetersPerSecond);
            float trackfield = NpcWorldRuntime.ResolveStoryTrafficLookAheadMeters(
                StoryTrafficRoadBehaviorProfile.Gravel,
                speedMetersPerSecond);
            float handbrakeTurn =
                NpcWorldRuntime.ResolveStoryTrafficLookAheadMeters(
                    StoryTrafficRoadBehaviorProfile.Perajarvi,
                    speedMetersPerSecond,
                    insideDonorHandbrakeZone: true);
            float tightTownTurn =
                NpcWorldRuntime.ResolveStoryTrafficLookAheadMeters(
                    StoryTrafficRoadBehaviorProfile.Perajarvi,
                    speedMetersPerSecond,
                    insideDonorHandbrakeZone: false,
                    insideTightManeuverCorridor: true);

            Assert.That(town, Is.EqualTo(20.6f).Within(0.001f));
            Assert.That(roadRace, Is.EqualTo(43.5f).Within(0.001f));
            Assert.That(trackfield, Is.EqualTo(7.6f).Within(0.001f));
            Assert.That(town, Is.LessThan(roadRace));
            Assert.That(trackfield, Is.LessThan(town),
                "The compact Trackfield circuit needs a shorter pursuit chord so cars do not cut inside its roadway.");
            Assert.That(handbrakeTurn, Is.InRange(4f, 5f));
            Assert.That(tightTownTurn, Is.InRange(4.5f, 6f));
            Assert.That(handbrakeTurn, Is.LessThan(tightTownTurn),
                "The donor handbrake trigger needs a later, tighter target than its approach corridor.");
            Assert.That(tightTownTurn, Is.LessThan(trackfield),
                "Teimo's pass-the-pumps handbrake U-turn needs the shortest pursuit chord.");
        }

        [Test]
        public void StoryTrafficAuthoredStops_UseBoundedPhysicalApproachSpeed()
        {
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficStopApproachSpeedCap(
                    distanceMeters: 30f,
                    arrivalRadiusMeters: 2f,
                    maximumMetersPerSecond: 8f),
                Is.EqualTo(8f).Within(0.001f));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficStopApproachSpeedCap(
                    distanceMeters: 3f,
                    arrivalRadiusMeters: 2f,
                    maximumMetersPerSecond: 8f),
                Is.EqualTo(Mathf.Sqrt(10f)).Within(0.001f));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficStopApproachSpeedCap(
                    distanceMeters: 2f,
                    arrivalRadiusMeters: 2f,
                    maximumMetersPerSecond: 8f),
                Is.EqualTo(1.2f).Within(0.001f));
        }

        [Test]
        public void PhysicalRouteDriver_FirstEvaluationInsideWindowStartsAtFormation()
        {
            simulation.RegisterPhysicalRouteDriver(
                "character.fixture.test",
                elapsedGameSeconds: 1700d);

            simulation.Evaluate(
                dayIndex: 0,
                secondsOfDay: 1700d,
                elapsedGameSeconds: 1700d);
            CharacterInstance instance = simulation.Instances.Single();
            Assert.That(
                instance.RouteProgress01,
                Is.Zero,
                "Starting inside an active time window must not derive a physical car position from the schedule clock.");

            simulation.Evaluate(
                dayIndex: 0,
                secondsOfDay: 1710d,
                elapsedGameSeconds: 1710d);
            Assert.That(
                instance.RouteProgress01,
                Is.Zero,
                "An unloaded donor physical driver must retain its waypoint while disabled.");

            simulation.SetPhysicalRouteAuthority(
                instance.Definition.DefinitionId,
                true,
                elapsedGameSeconds: 1710d);
            Assert.That(
                simulation.TryUpdatePhysicalRoute(
                    instance,
                    new Vector3(4f, 0f, 0f),
                    2f,
                    out _,
                    out float physicalProgress),
                Is.True);
            simulation.Evaluate(
                dayIndex: 0,
                secondsOfDay: 2000d,
                elapsedGameSeconds: 2000d);
            Assert.That(
                instance.RouteProgress01,
                Is.EqualTo(physicalProgress).Within(0.0001d),
                "A loaded physical driver must remain chassis-authoritative.");
        }

        [Test]
        public void PhysicalRouteDriver_BlankScheduleRoutePreservesScenarioRoute()
        {
            foundation.ConfigureForAuthoring(
                "npc.test.foundation",
                new[]
                {
                    new NpcAnchorDefinition(
                        "anchor.fixture.start",
                        "cell_0_0",
                        Vector3.zero,
                        Vector3.zero),
                    new NpcAnchorDefinition(
                        "anchor.fixture.end",
                        "cell_0_0",
                        new Vector3(10f, 0f, 0f),
                        new Vector3(0f, 90f, 0f)),
                },
                new[]
                {
                    new NpcRouteDefinition(
                        "route.fixture.test",
                        "anchor.fixture.start",
                        "anchor.fixture.end",
                        1800d,
                        NpcRouteTraversalMode.PingPong),
                },
                new[]
                {
                    new NpcScheduleBlock(
                        "schedule.fixture.test",
                        "character.fixture.test",
                        127,
                        0d,
                        86400d,
                        "anchor.fixture.start",
                        configuredRouteId: string.Empty,
                        activity: CharacterActivityState.VehicleSeated),
                });
            simulation = new NpcSimulation(characters, foundation);
            simulation.RegisterPhysicalRouteDriver(
                "character.fixture.test",
                elapsedGameSeconds: 0d);
            simulation.Evaluate(0, 0d, 0d);
            CharacterInstance instance = simulation.Instances.Single();
            instance.ApplyScheduleState(
                "schedule.fixture.test",
                "anchor.fixture.start",
                "route.fixture.test",
                0.42d,
                CharacterActivityState.VehicleSeated);

            simulation.Evaluate(0, 10d, 10d);

            Assert.That(instance.CurrentRouteId,
                Is.EqualTo("route.fixture.test"));
            Assert.That(instance.RouteProgress01,
                Is.EqualTo(0.42d).Within(0.0001d),
                "A blank availability schedule must not erase the active story route or reset it to formation.");
        }

        [Test]
        public void PhysicalRouteAuthority_RouteChangeDiscardsPreviousProjectionHint()
        {
            var routeAnchors = new List<NpcAnchorDefinition>();
            var routeAIds = new List<string>();
            var routeBIds = new List<string>();
            for (int index = 0; index <= 100; index++)
            {
                string aId = $"anchor.fixture.route-a.{index:D3}";
                string bId = $"anchor.fixture.route-b.{index:D3}";
                routeAIds.Add(aId);
                routeBIds.Add(bId);
                routeAnchors.Add(new NpcAnchorDefinition(
                    aId,
                    "cell_0_0",
                    new Vector3(index, 0f, 0f),
                    Vector3.zero));
                routeAnchors.Add(new NpcAnchorDefinition(
                    bId,
                    "cell_0_0",
                    new Vector3(index, 100f, 0f),
                    Vector3.zero));
            }

            foundation.ConfigureForAuthoring(
                "npc.test.route-change",
                routeAnchors,
                new[]
                {
                    new NpcRouteDefinition(
                        "route.fixture.route-a",
                        routeAIds,
                        100d),
                    new NpcRouteDefinition(
                        "route.fixture.route-b",
                        routeBIds,
                        100d),
                },
                Array.Empty<NpcScheduleBlock>());
            simulation = new NpcSimulation(characters, foundation);
            CharacterInstance instance = simulation.Instances.Single();
            simulation.SetPhysicalRouteAuthority(
                instance.Definition.DefinitionId,
                true);
            instance.ApplyScheduleState(
                string.Empty,
                routeAIds[0],
                "route.fixture.route-a",
                0.99d,
                CharacterActivityState.VehicleSeated);

            Assert.That(
                simulation.TryUpdatePhysicalRoute(
                    instance,
                    new Vector3(99f, 0f, 0f),
                    2f,
                    out _,
                    out float routeAProgress),
                Is.True);
            Assert.That(routeAProgress, Is.GreaterThan(0.95f));

            instance.ApplyScheduleState(
                string.Empty,
                routeBIds[0],
                "route.fixture.route-b",
                0d,
                CharacterActivityState.VehicleSeated);
            Assert.That(
                simulation.TryUpdatePhysicalRoute(
                    instance,
                    new Vector3(0f, 100f, 0f),
                    2f,
                    out _,
                    out float routeBProgress),
                Is.True);
            Assert.That(
                routeBProgress,
                Is.LessThan(0.05f),
                "A new scenario route must not inherit the sample hint from the previous geometry.");
        }

        [Test]
        public void PhysicalRouteAuthority_ReverseRecoveryDoesNotChasePassedRoad()
        {
            simulation.Evaluate(0, 0d, 0d);
            CharacterInstance instance = simulation.Instances.Single();
            simulation.SetPhysicalRouteAuthority(
                instance.Definition.DefinitionId,
                true);

            Assert.That(
                simulation.TryUpdatePhysicalRoute(
                    instance,
                    new Vector3(8f, 0f, 0f),
                    1.5f,
                    out _,
                    out float forwardProgress),
                Is.True);
            Assert.That(forwardProgress, Is.GreaterThan(0.7f));

            Assert.That(
                simulation.TryUpdatePhysicalRoute(
                    instance,
                    new Vector3(2f, 0f, 0f),
                    1.5f,
                    out NpcPose recoveryGuidance,
                    out float retainedProgress),
                Is.True);
            Assert.That(retainedProgress,
                Is.EqualTo(forwardProgress).Within(0.0001f));
            Assert.That(recoveryGuidance.Position.x,
                Is.GreaterThan(8f),
                "After a reverse recovery the guidance target must remain ahead of retained progress instead of walking the route hint backwards.");
        }

        [Test]
        public void PhysicalRouteAuthority_OffRoadCarReacquiresNearbyForwardSegment()
        {
            var anchors = new List<NpcAnchorDefinition>();
            var waypointIds = new List<string>();
            for (int index = 0; index <= 60; index++)
            {
                string anchorId = $"anchor.fixture.recovery.{index:D3}";
                waypointIds.Add(anchorId);
                anchors.Add(new NpcAnchorDefinition(
                    anchorId,
                    "cell_0_0",
                    new Vector3(index, 0f, 0f),
                    Vector3.zero));
            }

            foundation.ConfigureForAuthoring(
                "npc.test.route-recovery",
                anchors,
                new[]
                {
                    new NpcRouteDefinition(
                        "route.fixture.recovery",
                        waypointIds,
                        60d,
                        NpcRouteTraversalMode.Once,
                        NpcRouteInterpolationMode.CatmullRom),
                },
                Array.Empty<NpcScheduleBlock>());
            simulation = new NpcSimulation(characters, foundation);
            CharacterInstance instance = simulation.Instances.Single();
            simulation.SetPhysicalRouteAuthority(
                instance.Definition.DefinitionId,
                true);
            instance.ApplyScheduleState(
                string.Empty,
                waypointIds[0],
                "route.fixture.recovery",
                0d,
                CharacterActivityState.VehicleSeated);

            Assert.That(
                simulation.TryUpdatePhysicalRoute(
                    instance,
                    new Vector3(9f, 0f, 0f),
                    5f,
                    out _,
                    out _),
                Is.True);
            Assert.That(
                simulation.TryUpdatePhysicalRoute(
                    instance,
                    new Vector3(25f, 0f, 15f),
                    5f,
                    out NpcPose guidance,
                    out float frozenProgress),
                Is.True);

            Assert.That(frozenProgress,
                Is.LessThan(0.2f),
                "Progress remains frozen until the chassis returns to the 12 m route corridor.");
            Assert.That(guidance.Position.x,
                Is.GreaterThan(25f),
                "Bounded route recovery must reacquire the nearby forward segment instead of steering towards the stale four-span window.");
        }

        [Test]
        public void PhysicalRouteAuthority_WideRecoveryKeepsRetainedDirectedBranch()
        {
            var anchors = new List<NpcAnchorDefinition>();
            var waypointIds = new List<string>();
            for (int index = 0; index <= 20; index++)
            {
                AddRouteAnchor(index, 0f);
            }

            AddRouteAnchor(20f, 10f);
            for (int index = 19; index >= 0; index--)
            {
                AddRouteAnchor(index, 10f);
            }

            foundation.ConfigureForAuthoring(
                "npc.test.parallel-route-recovery",
                anchors,
                new[]
                {
                    new NpcRouteDefinition(
                        "route.fixture.parallel-recovery",
                        waypointIds,
                        120d,
                        NpcRouteTraversalMode.Once,
                        NpcRouteInterpolationMode.CatmullRom),
                },
                Array.Empty<NpcScheduleBlock>());
            simulation = new NpcSimulation(characters, foundation);
            CharacterInstance instance = simulation.Instances.Single();
            simulation.SetPhysicalRouteAuthority(
                instance.Definition.DefinitionId,
                true);
            instance.ApplyScheduleState(
                string.Empty,
                waypointIds[0],
                "route.fixture.parallel-recovery",
                0d,
                CharacterActivityState.VehicleSeated);

            Assert.That(
                simulation.TryUpdatePhysicalRoute(
                    instance,
                    new Vector3(5f, 0f, 0f),
                    Vector3.right,
                    8f,
                    out _,
                    out float retainedProgress,
                    out _),
                Is.True);
            Assert.That(
                simulation.TryUpdatePhysicalRoute(
                    instance,
                    new Vector3(5f, 0f, 10f),
                    Vector3.zero,
                    24f,
                    out NpcPose guidance,
                    out float frozenProgress,
                    out float deviationMeters),
                Is.True);

            Assert.That(deviationMeters, Is.GreaterThan(8f));
            Assert.That(frozenProgress,
                Is.EqualTo(retainedProgress).Within(0.0001f));
            Assert.That(guidance.Position.z,
                Is.LessThan(3f),
                "A stationary off-route car must keep the retained outbound branch instead of selecting the spatially closer opposing leg.");
            Assert.That(guidance.Position.x,
                Is.GreaterThan(5f),
                "Recovery guidance must remain ahead on the retained branch.");

            void AddRouteAnchor(float x, float z)
            {
                int anchorIndex = waypointIds.Count;
                string anchorId =
                    $"anchor.fixture.parallel-recovery.{anchorIndex:D3}";
                waypointIds.Add(anchorId);
                anchors.Add(new NpcAnchorDefinition(
                    anchorId,
                    "cell_0_0",
                    new Vector3(x, 0f, z),
                    Vector3.zero));
            }
        }

        [Test]
        public void OvernightSchedule_UsesTheDayOnWhichTheBlockStarts()
        {
            var saturdayPub = new NpcScheduleBlock(
                "schedule.fixture.overnight",
                "character.fixture.test",
                1 << 5,
                72000d,
                7200d,
                "anchor.fixture.start",
                string.Empty,
                CharacterActivityState.Working);

            Assert.That(saturdayPub.IsActive(5, 75600d), Is.True);
            Assert.That(
                saturdayPub.IsActive(6, 3600d),
                Is.True,
                "Saturday service must remain active after midnight on Sunday.");
            Assert.That(
                saturdayPub.IsActive(0, 3600d),
                Is.False,
                "Monday 01:00 belongs to the excluded Sunday-night shift.");
            Assert.That(saturdayPub.IsActive(6, 7200d), Is.False);
        }

        [Test]
        public void GeneratedTeimoSchedule_UsesExactStoreAndPubEndpoints()
        {
            CharacterDefinitionCatalog generatedCharacters =
                AssetDatabase.LoadAssetAtPath<CharacterDefinitionCatalog>(
                    "Assets/Game/NPC/Content/Phase1Foundation/" +
                    "CharacterDefinitionCatalog.asset");
            NpcFoundationCatalog generatedFoundation =
                AssetDatabase.LoadAssetAtPath<NpcFoundationCatalog>(
                    "Assets/Game/NPC/Content/Phase1Foundation/" +
                    "NpcFoundationCatalog.asset");

            Assert.That(generatedCharacters, Is.Not.Null);
            Assert.That(generatedFoundation, Is.Not.Null);
            Assert.That(
                generatedFoundation.TryGetAnchor(
                    "anchor.fixture.stationary-service",
                    out NpcAnchorDefinition shop),
                Is.True);
            Assert.That(
                generatedFoundation.TryGetAnchor(
                    "anchor.teimo.pub",
                    out NpcAnchorDefinition pub),
                Is.True);
            Assert.That(
                Vector3.Distance(
                    shop.Position,
                    new Vector3(-1381.5231f, 5.959f, 142.64197f)),
                Is.LessThan(0.0002f));
            Assert.That(
                Vector3.Distance(
                    pub.Position,
                    new Vector3(-1376.0472f, 5.9590025f, 146.14417f)),
                Is.LessThan(0.0002f));
            Assert.That(
                Vector3.Distance(shop.Position, pub.Position),
                Is.EqualTo(6.5f).Within(0.0002f),
                "The donor move clips separate the two endpoints by 6.5 metres.");
            Assert.That(
                generatedFoundation.TryGetRoute(
                    "route.teimo.store-to-pub",
                    out NpcRouteDefinition storeToPub),
                Is.True);
            Assert.That(
                storeToPub.WaypointAnchorIds,
                Is.EqualTo(new[]
                {
                    "anchor.fixture.stationary-service",
                    "anchor.teimo.store-to-pub.turn",
                    "anchor.teimo.store-to-pub.pub-door",
                    "anchor.teimo.pub",
                }));
            Assert.That(
                storeToPub.TraversalGameSeconds,
                Is.EqualTo(57d).Within(0.001d));
            Assert.That(
                storeToPub.WaypointProgress01,
                Is.EqualTo(new[]
                {
                    0d,
                    0.48333332d / 4.75d,
                    4d / 4.75d,
                    1d,
                }).Within(0.000001d));
            Assert.That(
                storeToPub.SurfaceMode,
                Is.EqualTo(NpcRouteSurfaceMode.AuthoredHeight),
                "The audited indoor route must not snap Teimo to an under-floor collider.");
            Assert.That(
                generatedFoundation.TryGetAnchor(
                    "anchor.teimo.store-to-pub.turn",
                    out NpcAnchorDefinition turn),
                Is.True);
            Assert.That(
                generatedFoundation.TryGetAnchor(
                    "anchor.teimo.store-to-pub.pub-door",
                    out NpcAnchorDefinition pubDoor),
                Is.True);
            Assert.That(turn.Position.y,
                Is.EqualTo(shop.Position.y).Within(0.00001f));
            Assert.That(pubDoor.Position.y,
                Is.EqualTo(shop.Position.y).Within(0.00001f));
            Assert.That(
                Vector3.Distance(turn.Position, shop.Position),
                Is.EqualTo(0.3589f).Within(0.001f));
            Assert.That(
                Vector3.Distance(pubDoor.Position, pub.Position),
                Is.EqualTo(0.6f).Within(0.002f));
            Assert.That(
                generatedFoundation.TryGetScheduleBlock(
                    "schedule.teimo.store-to-pub",
                    out NpcScheduleBlock storeToPubSchedule),
                Is.True);
            Assert.That(storeToPubSchedule.StartSecondsOfDay,
                Is.EqualTo(72000d));
            Assert.That(storeToPubSchedule.EndSecondsOfDay,
                Is.EqualTo(72057d).Within(0.001d));
            Assert.That(storeToPubSchedule.ActivityState,
                Is.EqualTo(CharacterActivityState.Walking));

            var generatedSimulation = new NpcSimulation(
                generatedCharacters,
                generatedFoundation);
            Assert.That(
                generatedSimulation.TryGetInstance(
                    "character.fixture.stationary-service",
                    out CharacterInstance teimo),
                Is.True);

            generatedSimulation.Evaluate(2, 54000d, 0d);
            Assert.That(
                teimo.CurrentAnchorId,
                Is.EqualTo("anchor.fixture.stationary-service"));
            generatedSimulation.Evaluate(2, 72030d, 0d);
            Assert.That(teimo.ActiveScheduleBlockId,
                Is.EqualTo("schedule.teimo.store-to-pub"));
            Assert.That(teimo.CurrentRouteId,
                Is.EqualTo("route.teimo.store-to-pub"));
            Assert.That(teimo.ActivityState,
                Is.EqualTo(CharacterActivityState.Walking));
            Assert.That(
                generatedSimulation.TryResolvePose(teimo, out NpcPose walkingPose),
                Is.True);
            Assert.That(walkingPose.ShouldConformToGround, Is.False);
            Assert.That(
                walkingPose.Position.y,
                Is.EqualTo(shop.Position.y).Within(0.00001f));
            generatedSimulation.Evaluate(2, 75600d, 0d);
            Assert.That(teimo.CurrentAnchorId, Is.EqualTo("anchor.teimo.pub"));
            generatedSimulation.Evaluate(6, 3600d, 0d);
            Assert.That(
                teimo.CurrentAnchorId,
                Is.EqualTo("anchor.teimo.pub"),
                "Saturday pub service must continue into Sunday morning.");
            generatedSimulation.Evaluate(0, 3600d, 0d);
            Assert.That(teimo.ActivityState, Is.EqualTo(CharacterActivityState.Hidden));
        }

        [Test]
        public void GeneratedStateOnlyCharacters_HaveNoMaterializingSchedules()
        {
            CharacterDefinitionCatalog generatedCharacters =
                AssetDatabase.LoadAssetAtPath<CharacterDefinitionCatalog>(
                    "Assets/Game/NPC/Content/Phase1Foundation/" +
                    "CharacterDefinitionCatalog.asset");
            NpcFoundationCatalog generatedFoundation =
                AssetDatabase.LoadAssetAtPath<NpcFoundationCatalog>(
                    "Assets/Game/NPC/Content/Phase1Foundation/" +
                    "NpcFoundationCatalog.asset");

            Assert.That(generatedCharacters, Is.Not.Null);
            Assert.That(generatedFoundation, Is.Not.Null);
            Assert.That(
                generatedCharacters.TryGet(
                    "character.fixture.vehicle-linked",
                    out CharacterDefinition latanen),
                Is.True);
            Assert.That(latanen.StateOnly, Is.True);
            Assert.That(
                generatedFoundation.GetSchedule(latanen.DefinitionId),
                Is.Empty,
                "The bus-owned Latanen presenter must not also be materialized " +
                "by the NPC schedule runtime.");
            Assert.That(
                generatedFoundation.ValidateConfiguration(generatedCharacters),
                Is.Empty,
                "Generated NPC catalogs must never schedule a state-only character.");
        }

        [Test]
        public void GeneratedTeimoBicycleRoutes_PreserveDonorTimingDaysAndSaveState()
        {
            CharacterDefinitionCatalog generatedCharacters =
                AssetDatabase.LoadAssetAtPath<CharacterDefinitionCatalog>(
                    "Assets/Game/NPC/Content/Phase1Foundation/" +
                    "CharacterDefinitionCatalog.asset");
            NpcFoundationCatalog generatedFoundation =
                AssetDatabase.LoadAssetAtPath<NpcFoundationCatalog>(
                    "Assets/Game/NPC/Content/Phase1Foundation/" +
                    "NpcFoundationCatalog.asset");
            ProductionWorldStreamingManifest streamingManifest =
                AssetDatabase.LoadAssetAtPath<ProductionWorldStreamingManifest>(
                    "Assets/Game/World/Content/Streaming/" +
                    "ProductionWorldStreamingManifest.asset");

            Assert.That(generatedCharacters, Is.Not.Null);
            Assert.That(generatedFoundation, Is.Not.Null);
            Assert.That(streamingManifest, Is.Not.Null);
            Assert.That(
                generatedFoundation.TryGetRoute(
                    "route.teimo.bicycle-to-store",
                    out NpcRouteDefinition toStore),
                Is.True);
            Assert.That(
                generatedFoundation.TryGetRoute(
                    "route.teimo.bicycle-to-home",
                    out NpcRouteDefinition toHome),
                Is.True);
            Assert.That(toStore.WaypointAnchorIds.Count, Is.EqualTo(37));
            Assert.That(toHome.WaypointAnchorIds.Count, Is.EqualTo(37));
            Assert.That(toStore.TraversalMode,
                Is.EqualTo(NpcRouteTraversalMode.Once));
            Assert.That(toHome.TraversalMode,
                Is.EqualTo(NpcRouteTraversalMode.Once));
            Assert.That(toStore.InterpolationMode,
                Is.EqualTo(NpcRouteInterpolationMode.CatmullRom));
            Assert.That(toHome.InterpolationMode,
                Is.EqualTo(NpcRouteInterpolationMode.CatmullRom));
            Assert.That(toStore.TraversalGameSeconds,
                Is.EqualTo(2331.0705d).Within(0.05d));
            Assert.That(toHome.TraversalGameSeconds,
                Is.EqualTo(2334.983d).Within(0.05d));
            Assert.That(
                generatedFoundation.TryGetAnchor(
                    toStore.WaypointAnchorIds[0],
                    out NpcAnchorDefinition homeEndpoint),
                Is.True);
            Assert.That(
                Vector3.Distance(
                    homeEndpoint.Position,
                    new Vector3(-705.2809f, 9.75299f, 202.3601f)),
                Is.LessThan(0.0002f));
            Assert.That(homeEndpoint.CellId, Is.EqualTo("cell_-2_0"));
            Assert.That(
                generatedFoundation.TryGetAnchor(
                    toStore.WaypointAnchorIds[^1],
                    out NpcAnchorDefinition storeEndpoint),
                Is.True);
            Assert.That(
                Vector3.Distance(
                    storeEndpoint.Position,
                    new Vector3(-1376.3809f, 4.4929934f, 147.18005f)),
                Is.LessThan(0.0002f));
            Assert.That(storeEndpoint.CellId, Is.EqualTo("cell_-3_0"));

            Assert.That(
                generatedFoundation.TryGetScheduleBlock(
                    "schedule.teimo.bicycle-to-store",
                    out NpcScheduleBlock toStoreSchedule),
                Is.True);
            Assert.That(toStoreSchedule.DayMask, Is.EqualTo(63));
            Assert.That(toStoreSchedule.StartSecondsOfDay,
                Is.EqualTo(28800d));
            Assert.That(toStoreSchedule.PresentationBindingIdOverride,
                Is.EqualTo("presentation.character.teimo-bicycle"));
            Assert.That(
                generatedFoundation.TryGetScheduleBlock(
                    "schedule.teimo.bicycle-to-home",
                    out NpcScheduleBlock toHomeSchedule),
                Is.True);
            Assert.That(toHomeSchedule.DayMask, Is.EqualTo(126));
            Assert.That(toHomeSchedule.StartSecondsOfDay,
                Is.EqualTo(7200d));
            Assert.That(toHomeSchedule.PresentationBindingIdOverride,
                Is.EqualTo("presentation.character.teimo-bicycle"));
            Assert.That(
                generatedFoundation.TryGetRoute(
                    "route.teimo.shop-arrival",
                    out NpcRouteDefinition shopArrival),
                Is.True);
            Assert.That(shopArrival.WaypointAnchorIds.Count, Is.EqualTo(17));
            Assert.That(shopArrival.WaypointProgress01.Count, Is.EqualTo(17));
            Assert.That(shopArrival.TraversalGameSeconds,
                Is.EqualTo(313.2d).Within(0.01d));
            Assert.That(shopArrival.WaypointProgress01[10],
                Is.EqualTo(15.133333d / 26.1d).Within(0.000001d));

            var generatedSimulation = new NpcSimulation(
                generatedCharacters,
                generatedFoundation);
            Assert.That(
                generatedSimulation.TryGetInstance(
                    "character.fixture.stationary-service",
                    out CharacterInstance teimo),
                Is.True);

            generatedSimulation.Evaluate(2, 29400d, 29400d);
            Assert.That(teimo.ActiveScheduleBlockId,
                Is.EqualTo("schedule.teimo.bicycle-to-store"));
            Assert.That(teimo.CurrentRouteId,
                Is.EqualTo("route.teimo.bicycle-to-store"));
            Assert.That(teimo.ActivityState,
                Is.EqualTo(CharacterActivityState.VehicleSeated));
            Assert.That(teimo.RouteProgress01,
                Is.EqualTo(600d / toStore.TraversalGameSeconds)
                    .Within(0.000001d));
            Assert.That(
                generatedSimulation.TryResolvePose(teimo, out NpcPose pose),
                Is.True);
            Assert.That(pose.CellId, Is.EqualTo("cell_-2_0"));
            Assert.That(pose.ShouldConformToGround, Is.True);

            foreach (string anchorId in toStore.WaypointAnchorIds.Concat(
                         toHome.WaypointAnchorIds))
            {
                Assert.That(
                    generatedFoundation.TryGetAnchor(
                        anchorId,
                        out NpcAnchorDefinition routeAnchor),
                    Is.True,
                    anchorId);
                bool resolvesToCell = streamingManifest.TryGetCell(
                    routeAnchor.CellId,
                    out _);
                bool resolvesToGlobalScene = streamingManifest.GlobalScenes.Any(
                    scene => string.Equals(
                        scene.SceneId,
                        routeAnchor.CellId,
                        StringComparison.Ordinal));
                Assert.That(
                    resolvesToCell || resolvesToGlobalScene,
                    Is.True,
                    $"Route anchor '{anchorId}' references unavailable " +
                    $"streaming owner '{routeAnchor.CellId}'.");
            }

            NpcStateDto midRouteSave = generatedSimulation.CaptureDto();
            generatedSimulation.Evaluate(2, 32400d, 32400d);
            Assert.That(teimo.ActiveScheduleBlockId,
                Is.EqualTo("schedule.teimo.shop-wait"));
            Assert.That(teimo.ActivityState,
                Is.EqualTo(CharacterActivityState.Idle),
                "Teimo must remain at the shop after entering and before opening time.");
            Assert.That(
                generatedSimulation.TryRestoreDto(
                    midRouteSave,
                    out string failure),
                Is.True,
                failure);
            Assert.That(teimo.ActiveScheduleBlockId,
                Is.EqualTo("schedule.teimo.bicycle-to-store"));
            Assert.That(teimo.RouteProgress01,
                Is.EqualTo(600d / toStore.TraversalGameSeconds)
                    .Within(0.000001d));

            generatedSimulation.Evaluate(2, 36000d, 36000d);
            Assert.That(teimo.ActiveScheduleBlockId,
                Is.EqualTo("schedule.teimo.shop"));
            Assert.That(teimo.ActivityState,
                Is.EqualTo(CharacterActivityState.Working));

            generatedSimulation.Evaluate(3, 7800d, 7800d);
            Assert.That(teimo.ActiveScheduleBlockId,
                Is.EqualTo("schedule.teimo.bicycle-to-home"));
            Assert.That(teimo.CurrentRouteId,
                Is.EqualTo("route.teimo.bicycle-to-home"));
            generatedSimulation.Evaluate(6, 7800d, 7800d);
            Assert.That(teimo.ActiveScheduleBlockId,
                Is.EqualTo("schedule.teimo.bicycle-to-home"));
            generatedSimulation.Evaluate(0, 7800d, 7800d);
            Assert.That(teimo.ActivityState,
                Is.EqualTo(CharacterActivityState.Hidden),
                "There is no Sunday service shift whose ride home would occur on Monday morning.");
        }

        [Test]
        public void TeimoShopWorldPresentation_FollowsScheduleAndDoorTimeline()
        {
            CharacterInstance teimo = new CharacterInstance(
                characters.Definitions.Single());
            var created = new List<GameObject>();
            try
            {
                string[] bicycleIds =
                {
                    "8f7341e40b01815fe359e33b864df56b",
                    "7f09935ceb4f54431a23c37b8ba23e1e",
                    "8c8312dfdcb7b77a47c1be818bc685cd",
                    "d69b1be98ee0cb4496f5fa5a6265317f",
                    "7ab57ce5c27ac32d14359c4b86a7d581",
                    "4af3a8e8fa1fb94fa0021430ab9d800e",
                };
                var bicycleRenderers = new List<MeshRenderer>();
                foreach (string stableId in bicycleIds)
                {
                    GameObject owner = CreateWorldEntity(stableId, created);
                    bicycleRenderers.Add(owner.AddComponent<MeshRenderer>());
                }

                GameObject door = CreateWorldEntity(
                    "41889effb8941e0ef1aa015c802f648d",
                    created);
                door.AddComponent<MeshRenderer>();

                using var controller =
                    new TeimoShopWorldPresentationController(teimo);
                teimo.ApplyScheduleState(
                    "schedule.teimo.bicycle-to-store",
                    "anchor.fixture.start",
                    "route.fixture.test",
                    0.5d,
                    CharacterActivityState.VehicleSeated);
                controller.Reconcile();
                Assert.That(bicycleRenderers.All(renderer => !renderer.enabled),
                    Is.True);
                Assert.That(controller.BoundBicycleRendererCount, Is.EqualTo(6));

                teimo.ApplyScheduleState(
                    TeimoShopWorldPresentationController.ShopArrivalScheduleId,
                    "anchor.fixture.start",
                    "route.fixture.test",
                    15.5d / 26.1d,
                    CharacterActivityState.Walking);
                controller.Reconcile();
                Assert.That(bicycleRenderers.All(renderer => renderer.enabled),
                    Is.True);
                Assert.That(
                    TeimoShopWorldPresentationController
                        .EvaluateServiceDoorAngle(15.5f),
                    Is.EqualTo(42.5f).Within(0.001f));
                Assert.That(
                    TeimoShopWorldPresentationController
                        .EvaluateServiceDoorAngle(18f),
                    Is.Zero.Within(0.001f));
            }
            finally
            {
                foreach (GameObject owner in created)
                {
                    UnityEngine.Object.DestroyImmediate(owner);
                }
            }
        }

        [Test]
        public void OffscreenSchedule_AdvancesRouteDeterministically()
        {
            simulation.Evaluate(0, 900d, 900d);
            CharacterInstance instance = simulation.Instances.Single();

            Assert.That(instance.ActivityState, Is.EqualTo(CharacterActivityState.Walking));
            Assert.That(instance.RouteProgress01, Is.EqualTo(0.5d).Within(0.000001d));
            Assert.That(simulation.TryResolvePose(instance, out NpcPose pose), Is.True);
            Assert.That(pose.Position.x, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(pose.ShouldConformToGround, Is.True);
        }

        [Test]
        public void OpenCatmullRoute_StartsTowardItsFirstWaypoint()
        {
            foundation.ConfigureForAuthoring(
                "npc.test.catmull-start",
                new[]
                {
                    new NpcAnchorDefinition(
                        "anchor.fixture.start",
                        "cell_0_0",
                        Vector3.zero,
                        new Vector3(0f, 180f, 0f)),
                    new NpcAnchorDefinition(
                        "anchor.fixture.middle",
                        "cell_0_0",
                        new Vector3(10f, 0f, 0f),
                        Vector3.zero),
                    new NpcAnchorDefinition(
                        "anchor.fixture.end",
                        "cell_0_0",
                        new Vector3(10f, 0f, 10f),
                        Vector3.zero),
                },
                new[]
                {
                    new NpcRouteDefinition(
                        "route.fixture.catmull-start",
                        new[]
                        {
                            "anchor.fixture.start",
                            "anchor.fixture.middle",
                            "anchor.fixture.end",
                        },
                        100d,
                        NpcRouteTraversalMode.Once,
                        NpcRouteInterpolationMode.CatmullRom),
                },
                new[]
                {
                    new NpcScheduleBlock(
                        "schedule.fixture.catmull-start",
                        "character.fixture.test",
                        127,
                        0d,
                        86400d,
                        "anchor.fixture.start",
                        "route.fixture.catmull-start",
                        CharacterActivityState.Walking),
                });
            simulation = new NpcSimulation(characters, foundation);
            simulation.Evaluate(0, 1d, 1d);

            Assert.That(
                simulation.TryResolvePose(
                    simulation.Instances.Single(),
                    out NpcPose pose),
                Is.True);
            Assert.That(pose.Position.x, Is.GreaterThan(0f));
            Assert.That(
                Vector3.Dot(
                    pose.Rotation * Vector3.forward,
                    Vector3.right),
                Is.GreaterThan(0.95f),
                "The first open Catmull segment inherited the anchor yaw " +
                "instead of pointing at its first waypoint.");
        }

        [Test]
        public void DirectNavigation_ConformsOnlyRequestedPosesToWorldGround()
        {
            int worldSurfaceLayer = LayerMask.NameToLayer("WorldSurface");
            Assert.That(worldSurfaceLayer, Is.GreaterThanOrEqualTo(0));

            var surface = new GameObject("NPC_GroundProbe_Surface");
            var groundedPresentation = new GameObject(
                "NPC_GroundProbe_Grounded");
            var authoredPresentation = new GameObject(
                "NPC_GroundProbe_Authored");
            try
            {
                surface.layer = worldSurfaceLayer;
                surface.transform.position = new Vector3(7000f, 1.5f, 7000f);
                BoxCollider collider = surface.AddComponent<BoxCollider>();
                collider.size = new Vector3(10f, 1f, 10f);
                Physics.SyncTransforms();

                var navigation = new DirectNpcNavigationBackend();
                navigation.ApplyPose(
                    groundedPresentation.transform,
                    new NpcPose(
                        new Vector3(7000f, 3.25f, 7000f),
                        Quaternion.identity,
                        "cell.fixture",
                        shouldConformToGround: true));
                navigation.ApplyPose(
                    authoredPresentation.transform,
                    new NpcPose(
                        new Vector3(7001f, 3.25f, 7000f),
                        Quaternion.identity,
                        "cell.fixture"));

                Assert.That(
                    groundedPresentation.transform.position.y,
                    Is.EqualTo(2f).Within(0.0001f));
                Assert.That(
                    authoredPresentation.transform.position.y,
                    Is.EqualTo(3.25f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(surface);
                UnityEngine.Object.DestroyImmediate(groundedPresentation);
                UnityEngine.Object.DestroyImmediate(authoredPresentation);
            }
        }

        [Test]
        public void PingPongRoute_StillMovesAtDefaultNoonStart()
        {
            simulation.Evaluate(0, 43200d, 0d);
            CharacterInstance instance = simulation.Instances.Single();
            double initialProgress = instance.RouteProgress01;

            simulation.Evaluate(0, 43212d, 12d);

            Assert.That(initialProgress, Is.EqualTo(0d).Within(0.000001d));
            Assert.That(instance.RouteProgress01, Is.GreaterThan(initialProgress));

            simulation.Evaluate(0, 45200d, 2000d);
            Assert.That(simulation.TryResolvePose(instance, out NpcPose reversePose),
                Is.True);
            Assert.That(
                (reversePose.Rotation * Vector3.forward).x,
                Is.LessThan(-0.9f));
        }

        [Test]
        public void MultiWaypointLoop_InterpolatesClosingSegmentWithoutTeleport()
        {
            foundation.ConfigureForAuthoring(
                "npc.test.loop",
                new[]
                {
                    new NpcAnchorDefinition(
                        "anchor.fixture.start",
                        "cell_0_0",
                        Vector3.zero,
                        Vector3.zero),
                    new NpcAnchorDefinition(
                        "anchor.fixture.middle",
                        "cell_0_0",
                        new Vector3(10f, 0f, 0f),
                        Vector3.zero),
                    new NpcAnchorDefinition(
                        "anchor.fixture.end",
                        "cell_0_0",
                        new Vector3(10f, 0f, 10f),
                        Vector3.zero),
                },
                new[]
                {
                    new NpcRouteDefinition(
                        "route.fixture.loop",
                        new[]
                        {
                            "anchor.fixture.start",
                            "anchor.fixture.middle",
                            "anchor.fixture.end",
                        },
                        120d,
                        NpcRouteTraversalMode.Loop),
                },
                new[]
                {
                    new NpcScheduleBlock(
                        "schedule.fixture.loop",
                        "character.fixture.test",
                        127,
                        0d,
                        86400d,
                        "anchor.fixture.start",
                        "route.fixture.loop",
                        CharacterActivityState.Walking),
                });
            simulation = new NpcSimulation(characters, foundation);

            simulation.Evaluate(0, 90d, 90d);

            Assert.That(
                simulation.TryResolvePose(
                    simulation.Instances.Single(),
                    out NpcPose pose),
                Is.True);
            Assert.That(pose.Position.x, Is.EqualTo(6.035534f).Within(0.0001f));
            Assert.That(pose.Position.z, Is.EqualTo(6.035534f).Within(0.0001f));
        }

        [Test]
        public void PhysicalRouteAuthority_LoopSeamCommitsForwardWrap()
        {
            foundation.ConfigureForAuthoring(
                "npc.test.physical-loop",
                new[]
                {
                    new NpcAnchorDefinition(
                        "anchor.fixture.start",
                        "cell_0_0",
                        Vector3.zero,
                        Vector3.zero),
                    new NpcAnchorDefinition(
                        "anchor.fixture.middle",
                        "cell_0_0",
                        new Vector3(10f, 0f, 0f),
                        Vector3.zero),
                    new NpcAnchorDefinition(
                        "anchor.fixture.end",
                        "cell_0_0",
                        new Vector3(10f, 0f, 10f),
                        Vector3.zero),
                },
                new[]
                {
                    new NpcRouteDefinition(
                        "route.fixture.physical-loop",
                        new[]
                        {
                            "anchor.fixture.start",
                            "anchor.fixture.middle",
                            "anchor.fixture.end",
                        },
                        120d,
                        NpcRouteTraversalMode.Loop),
                },
                Array.Empty<NpcScheduleBlock>());
            simulation = new NpcSimulation(characters, foundation);
            CharacterInstance instance = simulation.Instances.Single();
            instance.ApplyScheduleState(
                string.Empty,
                "anchor.fixture.start",
                "route.fixture.physical-loop",
                0.99d,
                CharacterActivityState.VehicleSeated);
            simulation.SetPhysicalRouteAuthority(
                instance.Definition.DefinitionId,
                true);

            Assert.That(
                simulation.TryUpdatePhysicalRoute(
                    instance,
                    new Vector3(0.5f, 0f, 0f),
                    2f,
                    out _,
                    out float wrappedProgress),
                Is.True);
            Assert.That(
                wrappedProgress,
                Is.LessThan(0.1f),
                "A genuine forward loop seam must not be clamped to the retained near-one progress.");
            Assert.That(
                instance.RouteProgress01,
                Is.EqualTo(wrappedProgress).Within(0.000001d));
        }

        [Test]
        public void MultiWaypointPingPong_UsesDistanceWeightedReturnPath()
        {
            foundation.ConfigureForAuthoring(
                "npc.test.ping-pong",
                new[]
                {
                    new NpcAnchorDefinition(
                        "anchor.fixture.start",
                        "cell_0_0",
                        Vector3.zero,
                        Vector3.zero),
                    new NpcAnchorDefinition(
                        "anchor.fixture.middle",
                        "cell_0_0",
                        new Vector3(10f, 0f, 0f),
                        Vector3.zero),
                    new NpcAnchorDefinition(
                        "anchor.fixture.end",
                        "cell_0_0",
                        new Vector3(10f, 0f, 20f),
                        Vector3.zero),
                },
                new[]
                {
                    new NpcRouteDefinition(
                        "route.fixture.ping-pong",
                        new[]
                        {
                            "anchor.fixture.start",
                            "anchor.fixture.middle",
                            "anchor.fixture.end",
                        },
                        120d,
                        NpcRouteTraversalMode.PingPong),
                },
                new[]
                {
                    new NpcScheduleBlock(
                        "schedule.fixture.ping-pong",
                        "character.fixture.test",
                        127,
                        0d,
                        86400d,
                        "anchor.fixture.start",
                        "route.fixture.ping-pong",
                        CharacterActivityState.Walking),
                });
            simulation = new NpcSimulation(characters, foundation);

            simulation.Evaluate(0, 60d, 60d);
            Assert.That(
                simulation.TryResolvePose(
                    simulation.Instances.Single(),
                    out NpcPose outwardPose),
                Is.True);
            Assert.That(outwardPose.Position.x,
                Is.EqualTo(10f).Within(0.0001f));
            Assert.That(outwardPose.Position.z,
                Is.EqualTo(5f).Within(0.0001f),
                "Half the route time must cover half its physical length, not half its segment count.");

            simulation.Evaluate(0, 150d, 90d);
            Assert.That(
                simulation.TryResolvePose(
                    simulation.Instances.Single(),
                    out NpcPose returnPose),
                Is.True);
            Assert.That(returnPose.Position.x,
                Is.EqualTo(10f).Within(0.0001f));
            Assert.That(returnPose.Position.z,
                Is.EqualTo(12.5f).Within(0.0001f),
                "The return leg must retrace the waypoint chain instead of closing directly to the start.");
            Assert.That(
                (returnPose.Rotation * Vector3.forward).z,
                Is.LessThan(-0.9f));
        }

        [Test]
        public void StreamingUnloadReload_DoesNotResetLogicalState()
        {
            simulation.Evaluate(0, 900d, 900d);
            CharacterInstance instance = simulation.Instances.Single();
            NpcStateDto beforeUnload = simulation.CaptureDto();

            Assert.That(
                NpcPresentationPolicy.ShouldMaterialize(instance.ActivityState, true),
                Is.True);
            Assert.That(
                NpcPresentationPolicy.ShouldMaterialize(instance.ActivityState, false),
                Is.False);
            Assert.That(simulation.CaptureDto().characters[0].routeProgress01,
                Is.EqualTo(beforeUnload.characters[0].routeProgress01));
            Assert.That(
                NpcPresentationPolicy.ShouldMaterialize(instance.ActivityState, true),
                Is.True);
        }

        [Test]
        public void SaveRoundTrip_RestoresScheduleFlagsAndRelationships()
        {
            simulation.Evaluate(0, 1200d, 1200d);
            CharacterInstance instance = simulation.Instances.Single();
            instance.SetFlag("flag.fixture.met", true);
            instance.SetRelationship("relationship.fixture.player", 42);
            instance.RecordDialogueLine(
                "line.fixture.saved",
                elapsedGameSeconds: 1200d,
                cooldownGameSeconds: 60d);
            NpcStateDto saved = simulation.CaptureDto();

            simulation.Evaluate(0, 4000d, 4000d);
            instance.SetFlag("flag.fixture.met", false);
            instance.SetRelationship("relationship.fixture.player", -5);

            Assert.That(simulation.TryRestoreDto(saved, out string failure),
                Is.True, failure);
            Assert.That(instance.ActivityState, Is.EqualTo(CharacterActivityState.Walking));
            Assert.That(instance.RouteProgress01, Is.EqualTo(2d / 3d).Within(0.000001d));
            Assert.That(instance.GetFlag("flag.fixture.met"), Is.True);
            Assert.That(instance.GetRelationship("relationship.fixture.player"), Is.EqualTo(42));
            Assert.That(
                instance.IsDialogueLineEligible(
                    "line.fixture.saved",
                    elapsedGameSeconds: 1259d),
                Is.False);
            Assert.That(
                instance.IsDialogueLineEligible(
                    "line.fixture.saved",
                    elapsedGameSeconds: 1260d),
                Is.True);
        }

        [Test]
        public void SavePreflight_RejectsRosterMismatchWithoutMutation()
        {
            simulation.Evaluate(0, 900d, 900d);
            NpcStateDto checkpoint = simulation.CaptureDto();
            var malformed = new NpcStateDto();

            Assert.That(simulation.TryRestoreDto(malformed, out _), Is.False);
            Assert.That(
                simulation.CaptureDto().characters[0].routeProgress01,
                Is.EqualTo(checkpoint.characters[0].routeProgress01));
        }

        [Test]
        public void Dialogue_UsesExplicitConditionsAndDomainEventHook()
        {
            CharacterInstance instance = simulation.Instances.Single();
            instance.SetFlag("flag.fixture.met", true);
            instance.SetRelationship("relationship.fixture.player", 20);
            var sink = new RecordingSink();
            var runtime = new NpcDialogueRuntime(
                new ExternalConditions(),
                sink);
            var definition = new NpcDialogueDefinition(
                "dialogue.fixture.test",
                "character.fixture.test",
                new[]
                {
                    new NpcDialogueLine(
                        "line.fixture.test",
                        "npc.fixture.test",
                        "Test fallback subtitle",
                        "audio.npc.fixture.test",
                        configuredCooldownGameSeconds: 30d,
                        new[]
                        {
                            new NpcDialogueCondition(
                                NpcDialogueConditionKind.CharacterFlag,
                                "flag.fixture.met"),
                            new NpcDialogueCondition(
                                NpcDialogueConditionKind.RelationshipAtLeast,
                                "relationship.fixture.player",
                                configuredThreshold: 10),
                            new NpcDialogueCondition(
                                NpcDialogueConditionKind.ExternalFlag,
                                "flag.external.job-ready"),
                        },
                        "event.npc.fixture.selected"),
                });

            Assert.That(
                runtime.TrySelectLine(
                    definition,
                    instance,
                    elapsedGameSeconds: 100d,
                    out var line),
                Is.True);
            runtime.CommitLine(line, instance, elapsedGameSeconds: 100d);
            Assert.That(
                runtime.TrySelectLine(
                    definition,
                    instance,
                    elapsedGameSeconds: 129d,
                    out _),
                Is.False);
            Assert.That(
                runtime.TrySelectLine(
                    definition,
                    instance,
                    elapsedGameSeconds: 130d,
                    out _),
                Is.True);
            Assert.That(sink.Events, Is.EqualTo(new[]
            {
                "event.npc.fixture.selected:character.fixture.test",
            }));
        }

        [Test]
        public void Dialogue_RotatesEligibleLinesAndFiltersByActiveScheduleBlock()
        {
            CharacterInstance instance = simulation.Instances.Single();
            instance.ApplyScheduleState(
                "schedule.fixture.shop",
                instance.Definition.HomeAnchorId,
                string.Empty,
                0d,
                CharacterActivityState.Working);
            var runtime = new NpcDialogueRuntime();
            var shopCondition = new NpcDialogueCondition(
                NpcDialogueConditionKind.ActiveScheduleBlock,
                "schedule.fixture.shop");
            var pubCondition = new NpcDialogueCondition(
                NpcDialogueConditionKind.ActiveScheduleBlock,
                "schedule.fixture.pub");
            var definition = new NpcDialogueDefinition(
                "dialogue.fixture.rotation",
                "character.fixture.test",
                new[]
                {
                    DialogueLine("line.fixture.shop-1", shopCondition),
                    DialogueLine("line.fixture.shop-2", shopCondition),
                    DialogueLine("line.fixture.pub-1", pubCondition),
                });

            Assert.That(
                runtime.TrySelectLine(definition, instance, 100d, out var first),
                Is.True);
            Assert.That(first.LineId, Is.EqualTo("line.fixture.shop-1"));
            runtime.CommitLine(first, instance, 100d);
            Assert.That(
                runtime.TrySelectLine(definition, instance, 100d, out var second),
                Is.True);
            Assert.That(second.LineId, Is.EqualTo("line.fixture.shop-2"));
            runtime.CommitLine(second, instance, 100d);

            instance.ApplyScheduleState(
                "schedule.fixture.pub",
                instance.Definition.HomeAnchorId,
                string.Empty,
                0d,
                CharacterActivityState.Working);
            Assert.That(
                runtime.TrySelectLine(definition, instance, 100d, out var pub),
                Is.True);
            Assert.That(pub.LineId, Is.EqualTo("line.fixture.pub-1"));
        }

        [Test]
        public void GeneratedStoryTraffic_UsesLockedDonorRouteSequenceAndDonorDayWindow()
        {
            NpcFoundationCatalog generated =
                AssetDatabase.LoadAssetAtPath<NpcFoundationCatalog>(
                    "Assets/Game/NPC/Content/Phase1Foundation/" +
                    "NpcFoundationCatalog.asset");
            Assert.That(generated, Is.Not.Null);
            Assert.That(
                generated.TryGetRoute(
                    "route.story-traffic.jani-race",
                    out NpcRouteDefinition jani),
                Is.True);
            Assert.That(
                generated.TryGetRoute(
                    "route.story-traffic.petteri-race",
                    out NpcRouteDefinition petteri),
                Is.True);
            Assert.That(
                generated.TryGetRoute(
                    "route.story-traffic.jani-dancehall-cycle",
                    out NpcRouteDefinition dancehall),
                Is.True);
            Assert.That(
                generated.TryGetRoute(
                    "route.story-traffic.jani-perajarvi-departure",
                    out NpcRouteDefinition janiDeparture),
                Is.True);
            Assert.That(
                generated.TryGetRoute(
                    "route.story-traffic.petteri-perajarvi-departure",
                    out NpcRouteDefinition petteriDeparture),
                Is.True);
            Assert.That(janiDeparture.WaypointAnchorIds.Count,
                Is.EqualTo(477));
            Assert.That(petteriDeparture.WaypointAnchorIds.Count,
                Is.EqualTo(478));
            Assert.That(
                janiDeparture.WaypointAnchorIds[0],
                Is.EqualTo("anchor.story-traffic.jani.000"));
            Assert.That(
                janiDeparture.WaypointAnchorIds[1],
                Is.EqualTo("anchor.traffic.village.0121"));
            Assert.That(
                petteriDeparture.WaypointAnchorIds[0],
                Is.EqualTo("anchor.story-traffic.petteri.000"));
            Assert.That(
                petteriDeparture.WaypointAnchorIds[1],
                Is.EqualTo("anchor.traffic.village.0122"));
            Assert.That(
                janiDeparture.WaypointAnchorIds,
                Does.Contain("anchor.traffic.village.0022"));
            Assert.That(
                janiDeparture.WaypointAnchorIds,
                Does.Contain("anchor.traffic.road-race.0622"));
            Assert.That(
                janiDeparture.WaypointAnchorIds,
                Does.Contain("anchor.traffic.road-race.0248"));
            Assert.That(
                janiDeparture.WaypointAnchorIds[^1],
                Is.EqualTo("anchor.traffic.highway.0464"));
            Assert.That(janiDeparture.TraversalMode,
                Is.EqualTo(NpcRouteTraversalMode.Once));
            Assert.That(jani.WaypointAnchorIds.Count, Is.EqualTo(2088));
            Assert.That(petteri.WaypointAnchorIds.Count, Is.EqualTo(2088));
            Assert.That(
                jani.WaypointAnchorIds[0],
                Is.EqualTo("anchor.story-traffic.jani.000"));
            Assert.That(
                jani.WaypointAnchorIds[1],
                Is.EqualTo("anchor.traffic.track-field.0228"));
            Assert.That(
                petteri.WaypointAnchorIds[0],
                Is.EqualTo("anchor.story-traffic.petteri.000"));
            Assert.That(
                petteri.WaypointAnchorIds[1],
                Is.EqualTo("anchor.traffic.track-field.0228"));
            Assert.That(jani.WaypointAnchorIds[63],
                Is.EqualTo("anchor.traffic.track-field.0290"));
            Assert.That(jani.WaypointAnchorIds[64],
                Is.EqualTo("anchor.traffic.village.0000"));
            Assert.That(jani.WaypointAnchorIds[359],
                Is.EqualTo("anchor.traffic.road-race.0000"));
            Assert.That(jani.WaypointAnchorIds[982],
                Is.EqualTo("anchor.traffic.road-race.0623"));
            Assert.That(jani.WaypointAnchorIds[983],
                Is.EqualTo("anchor.traffic.track-field.0000"));
            Assert.That(jani.WaypointAnchorIds[94],
                Is.EqualTo("anchor.traffic.village.0030"));
            Assert.That(jani.WaypointAnchorIds[96],
                Is.EqualTo("anchor.traffic.village.0032"));
            Assert.That(jani.WaypointAnchorIds[1028],
                Is.EqualTo("anchor.traffic.track-field.0045"));
            Assert.That(jani.WaypointAnchorIds[1056],
                Is.EqualTo("anchor.traffic.track-field.0073"));
            Assert.That(jani.WaypointAnchorIds[1184],
                Is.EqualTo("anchor.traffic.track-field.0201"));
            Assert.That(jani.WaypointAnchorIds[1185],
                Is.EqualTo("anchor.traffic.track-field.0073"));
            Assert.That(jani.WaypointAnchorIds[2085],
                Is.EqualTo("anchor.traffic.track-field.0199"));
            Assert.That(jani.WaypointAnchorIds[2087],
                Is.EqualTo("anchor.traffic.track-field.0201"));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficTeimoStopWaypointIndex(
                    "character.jani"),
                Is.EqualTo(96));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficTeimoStopWaypointIndex(
                    "character.petteri"),
                Is.EqualTo(94));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficTerminalStopWaypointIndex(
                    "character.jani"),
                Is.EqualTo(2087));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficTerminalStopWaypointIndex(
                    "character.petteri"),
                Is.EqualTo(2085));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRoadBehaviorProfile(
                        generated,
                        NpcWorldRuntime.JaniRaceRouteId,
                        (float)jani.WaypointProgress01[358]),
                Is.EqualTo(StoryTrafficRoadBehaviorProfile.Perajarvi));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRoadBehaviorProfile(
                        generated,
                        NpcWorldRuntime.JaniRaceRouteId,
                        (float)jani.WaypointProgress01[359]),
                Is.EqualTo(StoryTrafficRoadBehaviorProfile.RoadRace));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRoadBehaviorProfile(
                        generated,
                        NpcWorldRuntime.JaniRaceRouteId,
                        (float)jani.WaypointProgress01[982]),
                Is.EqualTo(StoryTrafficRoadBehaviorProfile.RoadRace));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRoadBehaviorProfile(
                        generated,
                        NpcWorldRuntime.JaniRaceRouteId,
                        (float)jani.WaypointProgress01[983]),
                Is.EqualTo(StoryTrafficRoadBehaviorProfile.Gravel));
            float roadRaceExit = (float)(
                jani.WaypointProgress01[982] +
                (jani.WaypointProgress01[983] -
                 jani.WaypointProgress01[982]) * 0.5d);
            float beforePerajarviInboundBrake =
                NpcWorldRuntime.ResolveStoryTrafficRouteSpeedCap(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[938]);
            float enteringPerajarviInboundBrake =
                NpcWorldRuntime.ResolveStoryTrafficRouteSpeedCap(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[939]);
            float atPerajarviInboundApex =
                NpcWorldRuntime.ResolveStoryTrafficRouteSpeedCap(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[957]);
            float leavingPerajarviInboundBrake =
                NpcWorldRuntime.ResolveStoryTrafficRouteSpeedCap(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[975]);
            Assert.That(beforePerajarviInboundBrake,
                Is.EqualTo(float.PositiveInfinity));
            Assert.That(enteringPerajarviInboundBrake,
                Is.EqualTo(185f / 3.6f).Within(0.001f));
            Assert.That(atPerajarviInboundApex,
                Is.EqualTo(43f / 3.6f).Within(0.001f));
            Assert.That(leavingPerajarviInboundBrake,
                Is.EqualTo(68f / 3.6f).Within(0.001f));
            Assert.That(atPerajarviInboundApex,
                Is.LessThan(enteringPerajarviInboundBrake),
                "RoadRace cars must brake before reaching the compact Perajarvi return bend.");
            Assert.That(leavingPerajarviInboundBrake,
                Is.GreaterThan(atPerajarviInboundApex),
                "The inbound cap must release progressively only after the directed bend is complete.");
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRouteSpeedCap(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    roadRaceExit + 0.001f),
                Is.EqualTo(float.PositiveInfinity),
                "The Gravel profile owns speed after the route boundary.");
            Assert.That(
                NpcWorldRuntime.IsInsideStoryTrafficTightManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[78]),
                Is.True,
                "Teimo's pass-the-pumps U-turn needs sequential short-preview guidance before its apex.");
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRouteSpeedCap(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[78]),
                Is.EqualTo(60f / 3.6f).Within(0.001f),
                "Teimo's physical handbrake turn needs a bounded donor-village entry speed.");
            Assert.That(jani.WaypointAnchorIds[78],
                Is.EqualTo("anchor.traffic.village.0014"));
            Assert.That(jani.WaypointAnchorIds[79],
                Is.EqualTo("anchor.traffic.village.0015"),
                "The runtime tune must preserve the measured donor apex instead of replacing its waypoints.");
            Assert.That(
                NpcWorldRuntime.IsInsideStoryTrafficTightManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[320]),
                Is.True,
                "The inspection drift and Perajarvi exit must not be cut by a highway pursuit chord.");
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficDirectedManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[320]),
                Is.EqualTo(NpcWorldRuntime
                    .StoryTrafficDirectedManeuverCorridor.InspectionH2));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRouteSpeedCap(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[320]),
                Is.EqualTo(38f / 3.6f).Within(0.001f),
                "Inspection H2 must be slow enough to follow the donor arc without contacting the inspection building.");
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRouteLookAheadMeters(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[320],
                    StoryTrafficRoadBehaviorProfile.Perajarvi,
                    20f,
                    insideDonorHandbrakeZone: false),
                Is.EqualTo(4.95f).Within(0.001f));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficDirectedManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[337]),
                Is.EqualTo(NpcWorldRuntime
                    .StoryTrafficDirectedManeuverCorridor.InspectionH3));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRouteSpeedCap(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[337]),
                Is.EqualTo(38f / 3.6f).Within(0.001f));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRouteLookAheadMeters(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[337],
                    StoryTrafficRoadBehaviorProfile.Perajarvi,
                    20f,
                    insideDonorHandbrakeZone: false),
                Is.EqualTo(4.7f).Within(0.001f));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRouteSpeedCap(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[369]),
                Is.EqualTo(90f / 3.6f).Within(0.001f));
            float outboundBrakeMidpoint =
                NpcWorldRuntime.ResolveStoryTrafficRouteSpeedCap(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[375]);
            Assert.That(outboundBrakeMidpoint,
                Is.GreaterThan(44f / 3.6f)
                    .And.LessThan(90f / 3.6f));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRouteSpeedCap(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[381]),
                Is.EqualTo(44f / 3.6f).Within(0.001f));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRouteSpeedCap(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[390]),
                Is.EqualTo(44f / 3.6f).Within(0.001f));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRouteSpeedCap(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[398]),
                Is.EqualTo(90f / 3.6f).Within(0.001f));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficDirectedManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[384]),
                Is.EqualTo(NpcWorldRuntime
                    .StoryTrafficDirectedManeuverCorridor.PerajarviOutbound),
                "The town-to-highway branch needs its own progress-directed preview even where it overlaps the inbound road.");
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficDirectedManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[371]),
                Is.EqualTo(NpcWorldRuntime
                    .StoryTrafficDirectedManeuverCorridor.None));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficDirectedManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[372]),
                Is.EqualTo(NpcWorldRuntime
                    .StoryTrafficDirectedManeuverCorridor.PerajarviOutbound));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficDirectedManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[393]),
                Is.EqualTo(NpcWorldRuntime
                    .StoryTrafficDirectedManeuverCorridor.PerajarviOutbound));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficDirectedManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[394]),
                Is.EqualTo(NpcWorldRuntime
                    .StoryTrafficDirectedManeuverCorridor.None));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRouteLookAheadMeters(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[384],
                    StoryTrafficRoadBehaviorProfile.RoadRace,
                    30f,
                    insideDonorHandbrakeZone: false),
                Is.EqualTo(11.4f).Within(0.001f));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRouteSpeedCap(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[953]),
                Is.EqualTo(43f / 3.6f).Within(0.001f));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRouteSpeedCap(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[963]),
                Is.EqualTo(43f / 3.6f).Within(0.001f));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficDirectedManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[957]),
                Is.EqualTo(NpcWorldRuntime
                    .StoryTrafficDirectedManeuverCorridor.PerajarviInbound),
                "The highway-to-town branch must brake and preview independently from the spatially overlapping outbound leg.");
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficDirectedManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[945]),
                Is.EqualTo(NpcWorldRuntime
                    .StoryTrafficDirectedManeuverCorridor.None));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficDirectedManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[946]),
                Is.EqualTo(NpcWorldRuntime
                    .StoryTrafficDirectedManeuverCorridor.PerajarviInbound));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficDirectedManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[965]),
                Is.EqualTo(NpcWorldRuntime
                    .StoryTrafficDirectedManeuverCorridor.PerajarviInbound));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficDirectedManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[966]),
                Is.EqualTo(NpcWorldRuntime
                    .StoryTrafficDirectedManeuverCorridor.None));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRouteLookAheadMeters(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[957],
                    StoryTrafficRoadBehaviorProfile.RoadRace,
                    30f,
                    insideDonorHandbrakeZone: false),
                Is.EqualTo(11.4f).Within(0.001f));
            Assert.That(
                NpcWorldRuntime.IsInsideStoryTrafficTightManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[200]),
                Is.False);
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRouteSpeedCap(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[1056]),
                Is.EqualTo(50f / 3.6f).Within(0.001f),
                "Trackfield laps need a bounded speed compatible with the authored oval radius.");
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficDirectedManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[1028]),
                Is.EqualTo(NpcWorldRuntime
                    .StoryTrafficDirectedManeuverCorridor.TrackfieldApproach));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRouteLookAheadMeters(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[1028],
                    StoryTrafficRoadBehaviorProfile.Gravel,
                    20f,
                    insideDonorHandbrakeZone: false),
                Is.EqualTo(4.95f).Within(0.001f));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficDirectedManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[1064]),
                Is.EqualTo(NpcWorldRuntime
                    .StoryTrafficDirectedManeuverCorridor.TrackfieldOval));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficDirectedManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[1184]),
                Is.EqualTo(NpcWorldRuntime
                    .StoryTrafficDirectedManeuverCorridor
                    .TrackfieldLoopClosure));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficDirectedManeuverCorridor(
                    generated,
                    NpcWorldRuntime.JaniRaceRouteId,
                    (float)jani.WaypointProgress01[2078]),
                Is.EqualTo(NpcWorldRuntime
                    .StoryTrafficDirectedManeuverCorridor.TerminalApproach));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficRoadBehaviorProfile(
                        generated,
                        NpcWorldRuntime.JaniDancehallRouteId,
                        0.5f),
                Is.EqualTo(StoryTrafficRoadBehaviorProfile.Gravel));
            Assert.That(
                NpcWorldRuntime.IsInsideDonorPerajarviHandbrakeZone(
                    new Vector3(-1408.82f, 20f, 129.075f)),
                Is.True,
                "Perajarvi's donor handbrake zones are horizontal 12 m triggers.");
            Assert.That(
                NpcWorldRuntime.IsInsideDonorPerajarviHandbrakeZone(
                    new Vector3(-1200f, 4f, 129f)),
                Is.False);
            Assert.That(
                jani.WaypointAnchorIds.Count(value =>
                    value == "anchor.traffic.track-field.0201"),
                Is.EqualTo(8),
                "The locked donor navigation performs eight track-field circuits.");
            Assert.That(jani.WaypointAnchorIds[^1],
                Is.EqualTo("anchor.traffic.track-field.0201"));
            Assert.That(dancehall.WaypointAnchorIds.Count, Is.EqualTo(537));
            Assert.That(dancehall.WaypointAnchorIds[1],
                Is.EqualTo("anchor.traffic.dancehall.0000"));
            Assert.That(dancehall.WaypointAnchorIds[^1],
                Is.EqualTo("anchor.traffic.dancehall.0001"));
            Assert.That(
                jani.WaypointAnchorIds.Any(value =>
                    value.StartsWith(
                        "anchor.teimo.",
                        StringComparison.Ordinal)),
                Is.False,
                "Story cars must never reuse Teimo's bicycle route.");
            Assert.That(jani.TraversalMode,
                Is.EqualTo(NpcRouteTraversalMode.Once));
            Assert.That(jani.InterpolationMode,
                Is.EqualTo(NpcRouteInterpolationMode.Linear),
                "Dense donor road samples must not be re-fit through a curve that cuts inside the authored lane.");
            Assert.That(petteri.InterpolationMode,
                Is.EqualTo(NpcRouteInterpolationMode.Linear));
            Assert.That(jani.TraversalGameSeconds, Is.GreaterThan(1000d));
            Assert.That(
                generated.TryGetAnchor(
                    "anchor.story-traffic.jani.000",
                    out NpcAnchorDefinition janiSpawn),
                Is.True);
            Assert.That(janiSpawn.Position.x,
                Is.EqualTo(-1173.1444f).Within(0.01f));
            Assert.That(janiSpawn.Position.y,
                Is.EqualTo(3.38973f).Within(0.01f));
            Assert.That(janiSpawn.Position.z,
                Is.EqualTo(123.312256f).Within(0.01f));
            Assert.That(
                generated.TryGetAnchor(
                    "anchor.story-traffic.petteri.000",
                    out NpcAnchorDefinition petteriSpawn),
                Is.True);
            Assert.That(petteriSpawn.Position.x,
                Is.EqualTo(-1176.1509f).Within(0.01f));
            Assert.That(petteriSpawn.Position.y,
                Is.EqualTo(3.464334f).Within(0.01f));
            Assert.That(petteriSpawn.Position.z,
                Is.EqualTo(128.17517f).Within(0.01f));

            Assert.That(
                generated.TryGetScheduleBlock(
                    "schedule.story-traffic.jani-perajarvi-departure",
                    out NpcScheduleBlock departure),
                Is.True);
            Assert.That(
                generated.TryGetScheduleBlock(
                    "schedule.story-traffic.jani-review",
                    out NpcScheduleBlock driving),
                Is.True);
            Assert.That(
                generated.TryGetScheduleBlock(
                    "schedule.story-traffic.jani-hidden",
                    out NpcScheduleBlock hidden),
                Is.True);
            Assert.That(
                generated.TryGetScheduleBlock(
                    "schedule.story-traffic.jani-inactive-days",
                    out NpcScheduleBlock inactiveDays),
                Is.True);
            Assert.That(
                generated.TryGetScheduleBlock(
                    "schedule.story-traffic.jani-inactive-early",
                    out NpcScheduleBlock inactiveEarly),
                Is.True);
            Assert.That(departure.DayMask, Is.EqualTo(29));
            Assert.That(driving.DayMask, Is.EqualTo(29));
            Assert.That(hidden.DayMask, Is.EqualTo(29));
            Assert.That(inactiveDays.DayMask, Is.EqualTo(98));
            Assert.That(inactiveEarly.DayMask, Is.EqualTo(64));
            Assert.That(departure.StartSecondsOfDay, Is.EqualTo(57600d));
            Assert.That(departure.EndSecondsOfDay,
                Is.GreaterThan(57600d).And.LessThan(61200d));
            Assert.That(driving.StartSecondsOfDay,
                Is.EqualTo(departure.EndSecondsOfDay));
            Assert.That(driving.EndSecondsOfDay, Is.EqualTo(7200d));
            Assert.That(driving.IsActive(0L, 43200d), Is.False);
            Assert.That(hidden.IsActive(0L, 43200d), Is.True);
            Assert.That(departure.IsActive(0L, 57600d), Is.True);
            Assert.That(driving.IsActive(0L, 57600d), Is.False);
            Assert.That(driving.IsActive(0L, 61200d), Is.True);
            Assert.That(hidden.IsActive(0L, 61200d), Is.False);
            Assert.That(driving.IsActive(1L, 3600d), Is.True);
            Assert.That(inactiveDays.IsActive(1L, 43200d), Is.True);
            Assert.That(inactiveDays.IsActive(1L, 3600d), Is.False);
            Assert.That(inactiveEarly.IsActive(6L, 3600d), Is.True);
            Assert.That(departure.IsActive(1L, 57600d), Is.False);
        }

        [Test]
        public void GeneratedStoryTraffic_OverlappingPerajarviBranchesKeepDirectedProgress()
        {
            CharacterDefinitionCatalog generatedCharacters =
                AssetDatabase.LoadAssetAtPath<CharacterDefinitionCatalog>(
                    "Assets/Game/NPC/Content/Phase1Foundation/" +
                    "CharacterDefinitionCatalog.asset");
            NpcFoundationCatalog generatedFoundation =
                AssetDatabase.LoadAssetAtPath<NpcFoundationCatalog>(
                    "Assets/Game/NPC/Content/Phase1Foundation/" +
                    "NpcFoundationCatalog.asset");

            Assert.That(generatedCharacters, Is.Not.Null);
            Assert.That(generatedFoundation, Is.Not.Null);
            Assert.That(
                generatedFoundation.TryGetRoute(
                    NpcWorldRuntime.JaniRaceRouteId,
                    out NpcRouteDefinition janiRoute),
                Is.True);
            Assert.That(
                generatedFoundation.TryGetRoute(
                    NpcWorldRuntime.PetteriRaceRouteId,
                    out NpcRouteDefinition petteriRoute),
                Is.True);
            Assert.That(
                generatedFoundation.TryGetAnchor(
                    janiRoute.WaypointAnchorIds[379],
                    out NpcAnchorDefinition outboundAnchor),
                Is.True);
            Assert.That(
                generatedFoundation.TryGetAnchor(
                    janiRoute.WaypointAnchorIds[380],
                    out NpcAnchorDefinition outboundNextAnchor),
                Is.True);
            Assert.That(
                generatedFoundation.TryGetAnchor(
                    petteriRoute.WaypointAnchorIds[963],
                    out NpcAnchorDefinition inboundAnchor),
                Is.True);
            Assert.That(
                generatedFoundation.TryGetAnchor(
                    petteriRoute.WaypointAnchorIds[964],
                    out NpcAnchorDefinition inboundNextAnchor),
                Is.True);

            Assert.That(
                Vector3.Distance(
                    outboundAnchor.Position,
                    inboundAnchor.Position),
                Is.LessThan(1f),
                "The donor branches intentionally occupy the same junction; world-space corridor triggers cannot distinguish their directions.");
            Vector3 outboundForward =
                (outboundNextAnchor.Position - outboundAnchor.Position)
                .normalized;
            Vector3 inboundForward =
                (inboundNextAnchor.Position - inboundAnchor.Position)
                .normalized;
            Assert.That(
                Vector3.Dot(outboundForward, inboundForward),
                Is.LessThan(-0.9f));

            var generatedSimulation = new NpcSimulation(
                generatedCharacters,
                generatedFoundation);
            Assert.That(
                generatedSimulation.TryGetInstance(
                    "character.jani",
                    out CharacterInstance janiInstance),
                Is.True);
            Assert.That(
                generatedSimulation.TryGetInstance(
                    "character.petteri",
                    out CharacterInstance petteriInstance),
                Is.True);
            janiInstance.ApplyScheduleState(
                string.Empty,
                janiRoute.WaypointAnchorIds[379],
                NpcWorldRuntime.JaniRaceRouteId,
                janiRoute.WaypointProgress01[379],
                CharacterActivityState.VehicleSeated);
            petteriInstance.ApplyScheduleState(
                string.Empty,
                petteriRoute.WaypointAnchorIds[963],
                NpcWorldRuntime.PetteriRaceRouteId,
                petteriRoute.WaypointProgress01[963],
                CharacterActivityState.VehicleSeated);
            generatedSimulation.SetPhysicalRouteAuthority(
                janiInstance.Definition.DefinitionId,
                true);
            generatedSimulation.SetPhysicalRouteAuthority(
                petteriInstance.Definition.DefinitionId,
                true);

            Vector3 sharedJunctionPosition = Vector3.Lerp(
                outboundAnchor.Position,
                inboundAnchor.Position,
                0.5f);
            Assert.That(
                generatedSimulation.TryUpdatePhysicalRoute(
                    janiInstance,
                    sharedJunctionPosition,
                    outboundForward,
                    10f,
                    out _,
                    out float outboundProgress,
                    out float outboundDeviation),
                Is.True);
            Assert.That(
                generatedSimulation.TryUpdatePhysicalRoute(
                    petteriInstance,
                    sharedJunctionPosition,
                    inboundForward,
                    10f,
                    out _,
                    out float inboundProgress,
                    out float inboundDeviation),
                Is.True);

            Assert.That(outboundDeviation, Is.LessThan(1f));
            Assert.That(inboundDeviation, Is.LessThan(1f));
            Assert.That(
                outboundProgress,
                Is.InRange(
                    (float)janiRoute.WaypointProgress01[377],
                    (float)janiRoute.WaypointProgress01[381]),
                "The outbound projection must stay on story379 instead of jumping to the spatially coincident inbound branch.");
            Assert.That(
                inboundProgress,
                Is.InRange(
                    (float)petteriRoute.WaypointProgress01[961],
                    (float)petteriRoute.WaypointProgress01[965]),
                "The inbound projection must stay on story963 instead of jumping to the spatially coincident outbound branch.");
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficDirectedManeuverCorridor(
                    generatedFoundation,
                    NpcWorldRuntime.JaniRaceRouteId,
                    outboundProgress),
                Is.EqualTo(NpcWorldRuntime
                    .StoryTrafficDirectedManeuverCorridor.PerajarviOutbound));
            Assert.That(
                NpcWorldRuntime.ResolveStoryTrafficDirectedManeuverCorridor(
                    generatedFoundation,
                    NpcWorldRuntime.PetteriRaceRouteId,
                    inboundProgress),
                Is.EqualTo(NpcWorldRuntime
                    .StoryTrafficDirectedManeuverCorridor.PerajarviInbound));
        }

        [Test]
        public void GeneratedPresentation_UnloadReloadReconcilesWithoutStateLoss()
        {
            CharacterDefinitionCatalog generatedCharacters =
                AssetDatabase.LoadAssetAtPath<CharacterDefinitionCatalog>(
                    "Assets/Game/NPC/Content/Phase1Foundation/" +
                    "CharacterDefinitionCatalog.asset");
            NpcFoundationCatalog generatedFoundation =
                AssetDatabase.LoadAssetAtPath<NpcFoundationCatalog>(
                    "Assets/Game/NPC/Content/Phase1Foundation/" +
                    "NpcFoundationCatalog.asset");
            NpcDialogueCatalog generatedDialogue =
                AssetDatabase.LoadAssetAtPath<NpcDialogueCatalog>(
                    "Assets/Game/NPC/Content/Phase1Foundation/" +
                    "NpcDialogueCatalog.asset");
            CharacterPresentationCatalog presentation =
                AssetDatabase.LoadAssetAtPath<CharacterPresentationCatalog>(
                    "Assets/Game/LegacyImport/RuntimeBaseline/Characters/" +
                    "Resources/Phase1Characters/" +
                    "CharacterPresentationCatalog.asset");
            if (presentation == null)
            {
                Assert.Ignore(
                    "Private ignored Phase 1 donor presentation has not been generated locally.");
            }

            AssertGeneratedPresentationCalibration(presentation);

            Assert.That(generatedCharacters.Definitions.Count, Is.EqualTo(22));
            Assert.That(
                generatedDialogue.ValidateConfiguration(generatedCharacters),
                Is.Empty);
            Assert.That(
                generatedCharacters.Definitions.Select(value => value.FeatureId),
                Does.Contain("P1.NPC.001"));
            Assert.That(
                generatedCharacters.Definitions.Select(value => value.FeatureId),
                Does.Contain("P1.NPC.002"));
            Assert.That(
                generatedCharacters.Definitions.Select(value => value.FeatureId),
                Does.Contain("P1.NPC.007"));
            Assert.That(
                generatedCharacters.Definitions.Select(value => value.FeatureId),
                Does.Contain("P1.NPC.008"));
            Assert.That(
                generatedCharacters.Definitions.Select(value => value.FeatureId),
                Does.Contain("P1.NPC.003"));
            Assert.That(
                generatedCharacters.Definitions.Select(value => value.FeatureId),
                Does.Contain("P1.NPC.017"));
            Assert.That(
                generatedCharacters.Definitions.Select(value => value.FeatureId),
                Does.Contain("P1.NPC.101"));
            Assert.That(
                generatedCharacters.Definitions.Select(value => value.FeatureId),
                Does.Contain("P1.NPC.049"));
            Assert.That(
                generatedCharacters.Definitions.Select(value => value.FeatureId),
                Does.Contain("P1.NPC.050"));

            var root = new GameObject("10B-R3 NPC runtime test");
            var player = new GameObject("10B-R3 test player");
            try
            {
                var cells = new FakeCellAvailability(initiallyLoaded: true);
                NpcWorldRuntime runtime = root.AddComponent<NpcWorldRuntime>();
                var gameTime = new GameTimeService();
                runtime.ConfigureDialogue(generatedDialogue, null);
                runtime.InitializeInternal(
                    generatedCharacters,
                    generatedFoundation,
                    presentation,
                    gameTime,
                    cells,
                    new DirectNpcNavigationBackend(),
                    player.transform);
                int expectedAtNoon = runtime.Simulation.Instances.Count(instance =>
                    instance.ActivityState != CharacterActivityState.Hidden &&
                    instance.ActivityState != CharacterActivityState.Disabled);
                Assert.That(
                    expectedAtNoon,
                    Is.EqualTo(15),
                    "The state-only Latanen vehicle fixture must not materialize as a standalone scheduled NPC.");
                Assert.That(
                    runtime.MaterializedPresentationCount,
                    Is.EqualTo(expectedAtNoon));
                foreach (LegacyCharacterPresentationBinding binding in
                         root.GetComponentsInChildren<
                             LegacyCharacterPresentationBinding>(true))
                {
                    if (binding.GetComponent<
                            StoryTrafficVehiclePresentationBinding>() == null)
                    {
                        AssertPresentationClipMovesBones(binding);
                    }
                    AssertDialogueInteractionComponents(binding);
                }

                Assert.That(
                    root.transform.Find(
                        "NPC_Presentation_character.suski"),
                    Is.Null,
                    "Suski must initially exist only as Jani's passenger.");

                // Donor Amis Setup enables both cars on the even-hour
                // afternoon/night window (16:00 through 02:00), not all day.
                gameTime.Advance(1500d);
                int expectedWithStoryTraffic = runtime.Simulation.Instances
                    .Count(instance =>
                        instance.ActivityState !=
                        CharacterActivityState.Hidden &&
                        instance.ActivityState !=
                        CharacterActivityState.Disabled);
                Assert.That(
                    expectedWithStoryTraffic,
                    Is.GreaterThanOrEqualTo(expectedAtNoon));
                Assert.That(
                    runtime.MaterializedPresentationCount,
                    Is.EqualTo(expectedWithStoryTraffic));
                StoryTrafficVehiclePresentationBinding janiCar =
                    root.transform.Find("NPC_Presentation_character.jani")
                        .GetComponent<
                            StoryTrafficVehiclePresentationBinding>();
                StoryTrafficVehiclePresentationBinding petteriCar =
                    root.transform.Find("NPC_Presentation_character.petteri")
                        .GetComponent<
                            StoryTrafficVehiclePresentationBinding>();
                Assert.That(
                    runtime.TryGetInstance(
                        "character.jani",
                        out CharacterInstance spawnedJani),
                    Is.True);
                Assert.That(
                    runtime.TryGetInstance(
                        "character.petteri",
                        out CharacterInstance spawnedPetteri),
                    Is.True);
                Assert.That(spawnedJani.RouteProgress01,
                    Is.EqualTo(0d).Within(0.000001d),
                    "Jani started part-way around the route when the active window was entered.");
                Assert.That(spawnedPetteri.RouteProgress01,
                    Is.EqualTo(0d).Within(0.000001d),
                    "Petteri started part-way around the route when the active window was entered.");
                Assert.That(
                    generatedFoundation.TryGetAnchor(
                        "anchor.story-traffic.jani.000",
                        out NpcAnchorDefinition janiFormation),
                    Is.True);
                Assert.That(
                    generatedFoundation.TryGetAnchor(
                        "anchor.story-traffic.petteri.000",
                        out NpcAnchorDefinition petteriFormation),
                    Is.True);
                Assert.That(
                    Vector2.Distance(
                        new Vector2(
                            janiCar.transform.position.x,
                            janiCar.transform.position.z),
                        new Vector2(
                            janiFormation.Position.x,
                            janiFormation.Position.z)),
                    Is.LessThan(0.25f));
                Assert.That(
                    Vector2.Distance(
                        new Vector2(
                            petteriCar.transform.position.x,
                            petteriCar.transform.position.z),
                        new Vector2(
                            petteriFormation.Position.x,
                            petteriFormation.Position.z)),
                    Is.LessThan(0.25f));
                Assert.That(janiCar.TryValidate(out string janiFailure),
                    Is.True, janiFailure);
                Assert.That(petteriCar.TryValidate(out string petteriFailure),
                    Is.True, petteriFailure);

                // The runtime publishes game-clock snapshots continuously. A
                // reconciliation must not enqueue the logical pose behind a
                // live physical look-ahead target; doing so leaves the NWH
                // driven wheels spinning while desired road speed collapses.
                var operationalMotion = janiCar.gameObject.AddComponent<
                    OperationalStoryTrafficMotionBackend>();
                janiCar.ConfigureMotionBackendForAuthoring(operationalMotion);
                Assert.That(janiCar.HasPhysicalMotionBackend, Is.True);
                runtime.ReevaluateNow();
                Vector3 physicalGuidancePosition =
                    janiCar.transform.position +
                    janiCar.transform.forward * 10f;
                janiCar.SetPhysicalRouteGuidanceTarget(
                    physicalGuidancePosition,
                    janiCar.transform.rotation,
                    0.001f);
                Assert.That(janiCar.PendingRouteSampleCount, Is.Zero);
                runtime.ReevaluateNow();
                Assert.That(
                    janiCar.PendingRouteSampleCount,
                    Is.Zero,
                    "A game-time reconciliation overwrote the physical " +
                    "look-ahead with a queued logical route pose.");
                Assert.That(janiCar.GroundContactCalibrationMeters,
                    Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
                Assert.That(petteriCar.GroundContactCalibrationMeters,
                    Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
                Assert.That(janiCar.PassengerFeatureIds,
                    Does.Contain("P1.NPC.006"));
                Assert.That(
                    janiCar.TryGetPassengerVisibility(
                        "P1.NPC.006",
                        out bool passengerInitiallyVisible),
                    Is.True);
                Assert.That(passengerInitiallyVisible, Is.True);
                Assert.That(petteriCar.PassengerFeatureIds, Is.Empty);

                Assert.That(
                    runtime.TryGetInstance(
                        "character.jani",
                        out CharacterInstance janiInstance),
                    Is.True);
                Assert.That(
                    runtime.Simulation.TryResolvePose(
                        janiInstance,
                        out NpcPose janiLogicalPose),
                    Is.True);
                double retainedProgressBeforeOffRouteUpdate =
                    janiInstance.RouteProgress01;
                janiCar.transform.SetPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                MethodInfo fixedUpdate = typeof(NpcWorldRuntime).GetMethod(
                    "FixedUpdate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(fixedUpdate, Is.Not.Null);
                fixedUpdate.Invoke(runtime, null);
                Assert.That(
                    janiCar.transform.position,
                    Is.EqualTo(Vector3.zero),
                    "Live road AI teleported an off-route physical vehicle " +
                    "instead of steering/recovering it through physics.");
                Assert.That(
                    janiInstance.RouteProgress01,
                    Is.EqualTo(retainedProgressBeforeOffRouteUpdate)
                        .Within(0.000001d),
                    "An off-route physical projection regressed logical " +
                    "route progress.");
                janiCar.SnapToRoutePoseTarget(
                    janiLogicalPose.Position,
                    janiLogicalPose.Rotation);
                const string physicalTrafficCell =
                    "cell.story-traffic.physical-fixture";
                cells.SetResolvedCell(physicalTrafficCell);
                cells.SetCellLoaded(physicalTrafficCell, true);
                cells.SetCellLoaded(janiLogicalPose.CellId, false);
                runtime.ReevaluateNow();
                Assert.That(
                    root.transform.Find(
                        "NPC_Presentation_character.jani"),
                    Is.Not.Null,
                    "A story car in a loaded physical cell was removed " +
                    "because its off-screen logical route entered an " +
                    "unloaded cell.");
                cells.SetCellLoaded(janiLogicalPose.CellId, true);
                cells.ClearResolvedCell();

                Transform janiPassenger = RequireNamedTransform(
                    janiCar.gameObject,
                    "Passenger");
                Assert.That(janiPassenger.childCount, Is.EqualTo(1),
                    "Jani's car must contain only the BetterMSC Suski passenger root.");
                Assert.That(
                    janiPassenger.GetComponentsInChildren<
                        SkinnedMeshRenderer>(true),
                    Has.Length.EqualTo(1),
                    "The BetterMSC Suski body must be the only passenger skin.");
                SkinnedMeshRenderer suskiRenderer = janiPassenger
                    .GetComponentInChildren<SkinnedMeshRenderer>(true);
                Assert.That(suskiRenderer.updateWhenOffscreen, Is.True);
                Assert.That(
                    janiPassenger.GetComponentsInChildren<MeshRenderer>(true),
                    Is.Empty,
                    "Original Suski tail and cigarette renderers must not survive the BetterMSC replacement.");
                LegacyCharacterPresentationBinding janiCharacter =
                    janiCar.GetComponent<LegacyCharacterPresentationBinding>();
                AnimationClip suskiSitClip = janiCharacter.LegacyAnimation
                    .Cast<AnimationState>()
                    .Single()
                    .clip;
                Assert.That(
                    AnimationUtility.GetCurveBindings(suskiSitClip).Any(binding =>
                        string.IsNullOrEmpty(binding.path) &&
                        string.Equals(
                            binding.propertyName,
                            "m_LocalPosition.y",
                            StringComparison.Ordinal)),
                    Is.True,
                    "The baked BetterMSC seated pose must retain its model-root placement.");
                Renderer[] vehicleRenderers = janiCar
                    .GetComponentsInChildren<Renderer>(true)
                    .Where(renderer =>
                        !renderer.transform.IsChildOf(janiPassenger))
                    .ToArray();
                Bounds vehicleBounds = vehicleRenderers[0].bounds;
                foreach (Renderer renderer in vehicleRenderers.Skip(1))
                {
                    vehicleBounds.Encapsulate(renderer.bounds);
                }

                Transform suskiHead = RequireNamedTransform(
                    janiPassenger.gameObject,
                    "Bip01 Head");
                Transform suskiNeck = RequireNamedTransform(
                    janiPassenger.gameObject,
                    "Bip01 Neck");
                Transform suskiHeadPivot = RequireNamedTransform(
                    janiPassenger.gameObject,
                    "HeadPivot");
                Transform suskiLeftHand = RequireNamedTransform(
                    janiPassenger.gameObject,
                    "Bip01 L Hand");
                Transform suskiRightHand = RequireNamedTransform(
                    janiPassenger.gameObject,
                    "Bip01 R Hand");
                Assert.That(suskiHead.position.y,
                    Is.LessThan(vehicleBounds.max.y - 0.05f)
                        .And.GreaterThan(vehicleBounds.center.y),
                    "BetterMSC Suski's head must remain inside Jani's cabin rather than crossing the roof.");
                foreach (Transform hand in new[]
                         {
                             suskiLeftHand,
                             suskiRightHand,
                         })
                {
                    Assert.That(hand.position.x,
                        Is.InRange(vehicleBounds.min.x, vehicleBounds.max.x));
                    Assert.That(hand.position.y,
                        Is.InRange(vehicleBounds.min.y, vehicleBounds.max.y));
                    Assert.That(hand.position.z,
                        Is.InRange(vehicleBounds.min.z, vehicleBounds.max.z));
                }

                Transform suskiPelvis = RequireNamedTransform(
                    janiPassenger.gameObject,
                    "Bip01 Pelvis");
                Transform suskiLeftUpperArm = RequireNamedTransform(
                    janiPassenger.gameObject,
                    "Bip01 L UpperArm");
                Transform suskiRightUpperArm = RequireNamedTransform(
                    janiPassenger.gameObject,
                    "Bip01 R UpperArm");
                Transform suskiLeftForearm = RequireNamedTransform(
                    janiPassenger.gameObject,
                    "Bip01 L Forearm");
                Transform suskiRightForearm = RequireNamedTransform(
                    janiPassenger.gameObject,
                    "Bip01 R Forearm");
                Transform suskiLeftThigh = RequireNamedTransform(
                    janiPassenger.gameObject,
                    "Bip01 L Thigh");
                Transform suskiRightThigh = RequireNamedTransform(
                    janiPassenger.gameObject,
                    "Bip01 R Thigh");
                Transform suskiLeftCalf = RequireNamedTransform(
                    janiPassenger.gameObject,
                    "Bip01 L Calf");
                Transform suskiRightCalf = RequireNamedTransform(
                    janiPassenger.gameObject,
                    "Bip01 R Calf");
                Transform suskiLeftFoot = RequireNamedTransform(
                    janiPassenger.gameObject,
                    "Bip01 L Foot");
                Transform suskiRightFoot = RequireNamedTransform(
                    janiPassenger.gameObject,
                    "Bip01 R Foot");
                Vector3 PassengerLocal(Transform value) =>
                    janiPassenger.InverseTransformPoint(value.position);
                Vector3 pelvis = PassengerLocal(suskiPelvis);
                Vector3 head = PassengerLocal(suskiHead);
                Vector3 leftUpperArm = PassengerLocal(suskiLeftUpperArm);
                Vector3 rightUpperArm = PassengerLocal(suskiRightUpperArm);
                Vector3 leftForearm = PassengerLocal(suskiLeftForearm);
                Vector3 rightForearm = PassengerLocal(suskiRightForearm);
                Vector3 leftHand = PassengerLocal(suskiLeftHand);
                Vector3 rightHand = PassengerLocal(suskiRightHand);
                Vector3 leftThigh = PassengerLocal(suskiLeftThigh);
                Vector3 rightThigh = PassengerLocal(suskiRightThigh);
                Vector3 leftCalf = PassengerLocal(suskiLeftCalf);
                Vector3 rightCalf = PassengerLocal(suskiRightCalf);
                Vector3 leftFoot = PassengerLocal(suskiLeftFoot);
                Vector3 rightFoot = PassengerLocal(suskiRightFoot);
                Assert.That(head.y, Is.GreaterThan(pelvis.y + 0.45f),
                    "Suski's torso must remain upright in the passenger seat.");
                Vector3 headOffset = head - pelvis;
                headOffset.y = 0f;
                Assert.That(headOffset.magnitude, Is.LessThan(0.35f),
                    "Suski's head must stay above her pelvis instead of leaning across the cabin.");
                Assert.That(suskiHeadPivot.parent, Is.SameAs(suskiNeck),
                    "BetterMSC's humanoid head must remain on HeadPivot.");
                Assert.That(suskiHead.parent, Is.SameAs(suskiHeadPivot),
                    "Bip01 Head must remain the fixed mesh bone below HeadPivot.");
                Assert.That(suskiHeadPivot.localPosition.magnitude,
                    Is.InRange(0.09f, 0.11f));
                Assert.That(suskiHead.localPosition.magnitude,
                    Is.LessThan(0.001f),
                    "Applying the humanoid head pose directly to Bip01 Head stretches the neck.");
                Vector3 shoulderSpan = rightUpperArm - leftUpperArm;
                shoulderSpan.y = 0f;
                Assert.That(shoulderSpan.magnitude,
                    Is.GreaterThan(0.20f));
                Assert.That(Mathf.Abs(leftUpperArm.y - rightUpperArm.y),
                    Is.LessThan(0.08f),
                    "Suski's shoulders must remain symmetric rather than twisted.");
                Vector3 CarLocal(Transform value) =>
                    janiCar.transform.InverseTransformPoint(value.position);
                Vector3 carPelvis = CarLocal(suskiPelvis);
                Vector3 carLeftForearm = CarLocal(suskiLeftForearm);
                Vector3 carRightForearm = CarLocal(suskiRightForearm);
                Vector3 carLeftHand = CarLocal(suskiLeftHand);
                Vector3 carRightHand = CarLocal(suskiRightHand);
                Vector3 carLeftThigh = CarLocal(suskiLeftThigh);
                Vector3 carRightThigh = CarLocal(suskiRightThigh);
                Vector3 carLeftCalf = CarLocal(suskiLeftCalf);
                Vector3 carRightCalf = CarLocal(suskiRightCalf);
                Vector3 carLeftFoot = CarLocal(suskiLeftFoot);
                Vector3 carRightFoot = CarLocal(suskiRightFoot);
                Assert.That(pelvis.y, Is.InRange(-0.04f, 0.01f),
                    "Suski's pelvis must remain on the donor passenger-seat height.");
                Assert.That(leftForearm.y,
                    Is.LessThan(leftUpperArm.y - 0.10f));
                Assert.That(rightForearm.y,
                    Is.LessThan(rightUpperArm.y - 0.10f));
                Assert.That(carLeftHand.z,
                    Is.GreaterThan(carLeftForearm.z + 0.10f));
                Assert.That(carRightHand.z,
                    Is.GreaterThan(carRightForearm.z + 0.10f));
                Assert.That(Mathf.Abs(leftHand.y - rightHand.y),
                    Is.LessThan(0.08f),
                    "Suski's hands must remain paired in the seated pose.");
                Assert.That(carLeftCalf.z,
                    Is.GreaterThan(carLeftThigh.z + 0.20f));
                Assert.That(carRightCalf.z,
                    Is.GreaterThan(carRightThigh.z + 0.20f));
                Assert.That(carLeftFoot.z,
                    Is.GreaterThan(carLeftCalf.z + 0.25f));
                Assert.That(carRightFoot.z,
                    Is.GreaterThan(carRightCalf.z + 0.25f));
                Assert.That(leftFoot.y,
                    Is.LessThan(leftCalf.y - 0.10f));
                Assert.That(rightFoot.y,
                    Is.LessThan(rightCalf.y - 0.10f),
                    "Suski's legs must extend into the passenger footwell.");
                Vector3 carForward =
                    ((carLeftFoot + carRightFoot) * 0.5f - carPelvis);
                carForward.y = 0f;
                Vector3 passengerForward =
                    ((leftFoot + rightFoot) * 0.5f - pelvis);
                passengerForward.y = 0f;
                Assert.That(passengerForward.normalized.x,
                    Is.GreaterThan(0.95f),
                    "Suski must retain the locked passenger-skeleton forward axis.");
                Assert.That(carForward.normalized.z,
                    Is.GreaterThan(0.95f),
                    "Suski must face along Jani's car instead of across the cabin.");

                StoryTrafficStateDto trafficBeforeRagdoll =
                    runtime.CaptureTrafficDto();
                StoryTrafficStateDto impossibleInCarIncident =
                    trafficBeforeRagdoll.DeepClone();
                impossibleInCarIncident.suski.stage =
                    SuskiRescueStage.CrashedInCar;
                Assert.That(
                    runtime.TryValidateTrafficDto(
                        impossibleInCarIncident,
                        out _),
                    Is.False,
                    "CrashedInCar requires Jani's terminal wreck authority.");
                StoryTrafficStateDto crashedInCarIncident =
                    trafficBeforeRagdoll.DeepClone();
                StoryTrafficDriverStateDto janiIncident =
                    crashedInCarIncident.drivers.Single(driver =>
                        string.Equals(
                            driver.characterDefinitionId,
                            "character.jani",
                            StringComparison.Ordinal));
                janiIncident.terminalCrash = true;
                janiIncident.worldPosition = janiCar.transform.position;
                janiIncident.worldRotation = janiCar.transform.rotation;
                crashedInCarIncident.suski.stage =
                    SuskiRescueStage.CrashedInCar;
                crashedInCarIncident.suski.worldPosition =
                    janiPassenger.position;
                crashedInCarIncident.suski.worldRotation =
                    janiPassenger.rotation;
                Assert.That(
                    runtime.TryRestoreTrafficDto(
                        crashedInCarIncident,
                        out string crashedInCarRestoreFailure),
                    Is.True,
                    crashedInCarRestoreFailure);
                Assert.That(
                    runtime.CaptureTrafficDto().suski.stage,
                    Is.EqualTo(SuskiRescueStage.CrashedInCar));
                Assert.That(
                    root.transform.Find(
                        "NPC_Presentation_character.suski"),
                    Is.Null,
                    "A terminal joint break must not eject or duplicate Suski.");
                Assert.That(
                    janiCar.TryGetPassengerVisibility(
                        "P1.NPC.006",
                        out bool crashedPassengerVisible),
                    Is.True);
                Assert.That(crashedPassengerVisible, Is.True,
                    "Suski remains owned by the terminal wreck until extraction.");
                Assert.That(runtime.CanExtractSuskiFromCrashedCar(), Is.True);

                StoryTrafficStateDto persistedInCarIncident =
                    runtime.CaptureTrafficDto();
                Assert.That(
                    runtime.TryRestoreTrafficDto(
                        persistedInCarIncident,
                        out string persistedInCarFailure),
                    Is.True,
                    persistedInCarFailure);
                Assert.That(runtime.TryExtractSuskiFromCrashedCar(), Is.True);
                Assert.That(runtime.TryExtractSuskiFromCrashedCar(), Is.False,
                    "Extraction must be a one-way explicit transition.");
                Assert.That(
                    runtime.CaptureTrafficDto().suski.stage,
                    Is.EqualTo(SuskiRescueStage.AwaitingPickup));
                Assert.That(
                    janiCar.TryGetPassengerVisibility(
                        "P1.NPC.006",
                        out bool passengerAfterExtraction),
                    Is.True);
                Assert.That(passengerAfterExtraction, Is.False);
                Transform looseSuski = root.transform.Find(
                    "NPC_Presentation_character.suski");
                Assert.That(looseSuski, Is.Not.Null);
                LegacyCharacterPresentationBinding looseSuskiBinding =
                    looseSuski.GetComponent<
                        LegacyCharacterPresentationBinding>();
                Assert.That(
                    looseSuskiBinding.BindingId,
                    Is.EqualTo(
                        NpcWorldRuntime.SuskiRescuePresentationBindingId));
                Assert.That(
                    looseSuski.GetComponentsInChildren<
                        SkinnedMeshRenderer>(true),
                    Has.Length.EqualTo(1),
                    "Only the BetterMSC Suski skin may be used after extraction.");
                Assert.That(
                    looseSuski.GetComponentsInChildren<MeshRenderer>(true),
                    Is.Empty,
                    "The removed donor Suski tail/cigarette must not return after extraction.");
                SuskiRescueRagdoll ragdoll = looseSuski.GetComponent<
                    SuskiRescueRagdoll>();
                Assert.That(ragdoll, Is.Not.Null);
                Assert.That(ragdoll.IsInitialized, Is.True);
                Assert.That(
                    looseSuski.GetComponentsInChildren<Rigidbody>(true),
                    Has.Length.EqualTo(11),
                    "Suski must be an articulated body rather than one rigid pickup capsule.");
                Assert.That(
                    looseSuski.GetComponentsInChildren<CharacterJoint>(true),
                    Has.Length.EqualTo(10));
                Assert.That(looseSuski.GetComponent<Rigidbody>(), Is.Null);
                Assert.That(
                    runtime.TryRestoreTrafficDto(
                        trafficBeforeRagdoll,
                        out string clearIncidentFailure),
                    Is.True,
                    clearIncidentFailure);
                Assert.That(
                    root.transform.Find(
                        "NPC_Presentation_character.suski"),
                    Is.Null,
                    "Clearing the incident must return Suski to Jani's passenger binding.");

                StoryTrafficStateDto corruptOriginIncident =
                    trafficBeforeRagdoll.DeepClone();
                StoryTrafficDriverStateDto corruptJani =
                    corruptOriginIncident.drivers.Single(driver =>
                        string.Equals(
                            driver.characterDefinitionId,
                            "character.jani",
                            StringComparison.Ordinal));
                corruptJani.terminalCrash = true;
                corruptJani.worldPosition = Vector3.zero;
                corruptJani.worldRotation = Quaternion.identity;
                corruptOriginIncident.suski.stage =
                    SuskiRescueStage.AwaitingPickup;
                corruptOriginIncident.suski.worldPosition = Vector3.zero;
                Assert.That(
                    runtime.TryRestoreTrafficDto(
                        corruptOriginIncident,
                        out string corruptOriginFailure),
                    Is.True,
                    corruptOriginFailure);
                StoryTrafficStateDto repairedOrigin =
                    runtime.CaptureTrafficDto();
                Assert.That(
                    repairedOrigin.drivers.Single(driver =>
                        string.Equals(
                            driver.characterDefinitionId,
                            "character.jani",
                            StringComparison.Ordinal)).terminalCrash,
                    Is.False,
                    "Legacy origin-corrupted crash state was not repaired.");
                Assert.That(
                    repairedOrigin.suski.stage,
                    Is.EqualTo(SuskiRescueStage.Passenger));

                Transform grandmotherPresentation = root.transform.Find(
                    "NPC_Presentation_character.grandmother");
                CharacterFlagPresentationBinding conditional =
                    grandmotherPresentation.GetComponent<
                        CharacterFlagPresentationBinding>();
                Assert.That(conditional, Is.Not.Null);
                Assert.That(conditional.TryValidate(
                    out string conditionalFailure), Is.True,
                    conditionalFailure);
                Transform orderedProducts = RequireNamedTransform(
                    grandmotherPresentation.gameObject,
                    "PotatoBox");
                Assert.That(orderedProducts.gameObject.activeSelf, Is.False);
                Assert.That(runtime.TryGetInstance(
                    "character.grandmother",
                    out CharacterInstance grandmotherState), Is.True);
                grandmotherState.SetFlag(
                    "flag.grandmother.products-ordered",
                    true);
                runtime.ReevaluateNow();
                Assert.That(orderedProducts.gameObject.activeSelf, Is.True);

                runtime.SetSuskiRescuedFromJaniCrash(true);
                Assert.That(
                    root.transform.Find(
                        "NPC_Presentation_character.suski"),
                    Is.Not.Null,
                    "Rescue state must materialize Suski at her store anchor.");
                Assert.That(
                    janiCar.TryGetPassengerVisibility(
                        "P1.NPC.006",
                        out bool passengerAfterRescue),
                    Is.True);
                Assert.That(passengerAfterRescue, Is.False);
                NpcStateDto rescuedState = runtime.CaptureDto();
                runtime.SetSuskiRescuedFromJaniCrash(false);
                Assert.That(runtime.TryRestoreDto(
                    rescuedState,
                    out string rescueRestoreFailure),
                    Is.True,
                    rescueRestoreFailure);
                Assert.That(
                    runtime.TryGetInstance(
                        NpcWorldRuntime.SuskiDefinitionId,
                        out CharacterInstance restoredSuski),
                    Is.True);
                Assert.That(
                    restoredSuski.GetFlag(
                        NpcWorldRuntime.SuskiRescuedFromJaniCrashFlagId),
                    Is.True);
                runtime.SetSuskiRescuedFromJaniCrash(false);
                Assert.That(runtime.MaterializedPresentationCount,
                    Is.EqualTo(expectedWithStoryTraffic));

                runtime.ConfigureDialogue(generatedDialogue, null);
                foreach (LegacyCharacterPresentationBinding binding in
                         root.GetComponentsInChildren<
                             LegacyCharacterPresentationBinding>(true))
                {
                    AssertDialogueInteractionComponents(binding);
                }

                Transform roaming = root.transform.Find(
                    "NPC_Presentation_character.fixture.scheduled-roaming");
                Assert.That(roaming, Is.Not.Null);
                Vector3 initialPosition = roaming.position;
                LegacyCharacterPresentationBinding roamingBinding =
                    roaming.GetComponent<LegacyCharacterPresentationBinding>();
                AnimationState animationState = roamingBinding.LegacyAnimation
                    .Cast<AnimationState>()
                    .First(state => state.enabled);
                animationState.time = Math.Min(0.25f, animationState.length * 0.5f);
                float animationTimeBeforeTick = animationState.time;

                gameTime.Advance(1d);

                Assert.That(
                    Vector3.Distance(initialPosition, roaming.position),
                    Is.GreaterThan(0.1f));
                Assert.That(
                    animationState.time,
                    Is.EqualTo(animationTimeBeforeTick).Within(0.0001f),
                    "An unchanged activity must not restart its clip every GameTime tick.");
                NpcStateDto beforeUnload = runtime.CaptureDto();
                cells.SetLoaded("cell_-3_0", false);
                runtime.ReevaluateNow();
                Assert.That(runtime.MaterializedPresentationCount,
                    Is.EqualTo(2),
                    "Jani and Petteri remain physical outside streamed cells so an off-screen race or crash cannot be skipped.");
                Assert.That(root.transform.Find(
                    "NPC_Presentation_character.jani"), Is.Not.Null);
                Assert.That(root.transform.Find(
                    "NPC_Presentation_character.petteri"), Is.Not.Null);
                Assert.That(
                    runtime.CaptureDto().characters.Select(value => value.routeProgress01),
                    Is.EqualTo(beforeUnload.characters.Select(value => value.routeProgress01)));

                cells.SetLoaded("cell_-3_0", true);
                runtime.ReevaluateNow();
                Assert.That(
                    runtime.MaterializedPresentationCount,
                    Is.EqualTo(expectedWithStoryTraffic));
                foreach (LegacyCharacterPresentationBinding binding in
                         root.GetComponentsInChildren<
                             LegacyCharacterPresentationBinding>(true))
                {
                    AssertDialogueInteractionComponents(binding);
                }

                gameTime.Advance(4549d);
                Transform teimoOnRoad = root.transform.Find(
                    "NPC_Presentation_character.fixture.stationary-service");
                Assert.That(teimoOnRoad, Is.Not.Null);
                LegacyCharacterPresentationBinding bicycleBinding =
                    teimoOnRoad.GetComponent<LegacyCharacterPresentationBinding>();
                Assert.That(
                    bicycleBinding.BindingId,
                    Is.EqualTo("presentation.character.teimo-bicycle"));
                Assert.That(
                    runtime.Simulation.Instances.Single(instance =>
                        instance.Definition.DefinitionId ==
                        "character.fixture.stationary-service")
                        .ActiveScheduleBlockId,
                    Is.EqualTo("schedule.teimo.bicycle-to-store"));

                TeimoBicyclePresentationBinding bicycleMotion =
                    teimoOnRoad.GetComponent<
                        TeimoBicyclePresentationBinding>();
                Assert.That(bicycleMotion, Is.Not.Null);
                Quaternion frontBefore = bicycleMotion.FrontWheel.localRotation;
                Quaternion rearBefore = bicycleMotion.RearWheel.localRotation;
                Quaternion pedalsBefore = bicycleMotion.Pedals.localRotation;
                Quaternion leftHipBefore = bicycleMotion.LeftHip.rotation;
                Quaternion rightHipBefore = bicycleMotion.RightHip.rotation;
                bicycleMotion.PresentTravelDistance(1f);
                Assert.That(
                    Quaternion.Angle(
                        frontBefore,
                        bicycleMotion.FrontWheel.localRotation),
                    Is.EqualTo(170f).Within(0.01f));
                Assert.That(
                    Quaternion.Angle(
                        rearBefore,
                        bicycleMotion.RearWheel.localRotation),
                    Is.EqualTo(170f).Within(0.01f));
                Assert.That(
                    Quaternion.Angle(
                        pedalsBefore,
                        bicycleMotion.Pedals.localRotation),
                    Is.EqualTo(170f / 3.5f).Within(0.01f));
                Assert.That(
                    Quaternion.Angle(leftHipBefore, bicycleMotion.LeftHip.rotation) +
                    Quaternion.Angle(rightHipBefore, bicycleMotion.RightHip.rotation),
                    Is.GreaterThan(0.1f),
                    "The crank moved but Teimo's legs remained static.");

                player.transform.position =
                    teimoOnRoad.position + teimoOnRoad.right * 4f;
                Assert.That(bicycleMotion.TryPresentGreeting(), Is.True);
                Assert.That(
                    bicycleBinding.LegacyAnimation.IsPlaying(
                        "teimo_bicycle_waving_hello"),
                    Is.True);
                runtime.ReevaluateNow();
                Assert.That(
                    bicycleBinding.LegacyAnimation.IsPlaying(
                        "teimo_bicycle_waving_hello"),
                    Is.True,
                    "An unchanged VehicleSeated state stopped the greeting one-shot.");
                Assert.That(
                    bicycleMotion.TryPresentGreeting(),
                    Is.False,
                    "Teimo greeted more than once during the same game day.");
                CharacterInstance teimoInstance = runtime.Simulation.Instances
                    .Single(instance => instance.Definition.DefinitionId ==
                        "character.fixture.stationary-service");
                Assert.That(
                    teimoInstance.IsDialogueLineEligible(
                        TeimoBicyclePresentationBinding.GreetingCooldownLineId,
                        gameTime.Snapshot.ElapsedGameSeconds),
                    Is.False);

                NpcStateDto bicycleStateBeforeUnload = runtime.CaptureDto();
                Assert.That(
                    bicycleStateBeforeUnload.characters.Single(snapshot =>
                            snapshot.definitionId ==
                            "character.fixture.stationary-service")
                        .dialogueCooldowns.Select(cooldown => cooldown.lineId),
                    Does.Contain(
                        TeimoBicyclePresentationBinding.GreetingCooldownLineId));
                cells.SetLoaded("cell_-2_0", false);
                runtime.ReevaluateNow();
                Assert.That(runtime.MaterializedPresentationCount, Is.Zero);
                cells.SetLoaded("cell_-2_0", true);
                runtime.ReevaluateNow();
                Assert.That(
                    runtime.CaptureDto().characters.Select(value =>
                        value.routeProgress01),
                    Is.EqualTo(bicycleStateBeforeUnload.characters.Select(value =>
                        value.routeProgress01)));
                Assert.That(
                    root.transform.Find(
                            "NPC_Presentation_character.fixture.stationary-service")
                        .GetComponent<LegacyCharacterPresentationBinding>()
                        .BindingId,
                    Is.EqualTo("presentation.character.teimo-bicycle"));
                Assert.That(
                    root.transform.Find(
                            "NPC_Presentation_character.fixture.stationary-service")
                        .GetComponent<TeimoBicyclePresentationBinding>()
                        .TryPresentGreeting(),
                    Is.False,
                    "Streaming reload lost the once-per-day greeting gate.");

                gameTime.Advance(550d);
                Transform teimoAtShop = root.transform.Find(
                    "NPC_Presentation_character.fixture.stationary-service");
                Assert.That(teimoAtShop, Is.Not.Null);
                Assert.That(
                    teimoAtShop.GetComponent<LegacyCharacterPresentationBinding>()
                        .BindingId,
                    Is.EqualTo(
                        "presentation.character.fixture.stationary-service"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AssertDialogueInteractionComponents(
            LegacyCharacterPresentationBinding binding)
        {
            if (binding.GetComponent<
                    StoryTrafficVehiclePresentationBinding>() != null)
            {
                Assert.That(
                    binding.GetComponents<CapsuleCollider>().Length,
                    Is.Zero,
                    $"{binding.BindingId} must not receive a humanoid dialogue " +
                    "capsule that can support its physical chassis above the road.");
                Assert.That(
                    binding.GetComponents<Collider>().Length,
                    Is.GreaterThanOrEqualTo(1),
                    $"{binding.BindingId} must retain its authored chassis collider " +
                    "for dialogue interaction.");
            }
            else
            {
                Assert.That(
                    binding.GetComponents<CapsuleCollider>().Length,
                    Is.EqualTo(1),
                    $"{binding.BindingId} must have exactly one dialogue collider.");
            }

            Assert.That(
                binding.GetComponents<NpcDialogueInteractionTarget>().Length,
                Is.EqualTo(1),
                $"{binding.BindingId} must have exactly one dialogue target.");
        }

        private static NpcDialogueLine DialogueLine(
            string lineId,
            NpcDialogueCondition condition) =>
            new NpcDialogueLine(
                lineId,
                "npc.fixture.rotation",
                "Fallback",
                "audio.npc.fixture.rotation",
                configuredCooldownGameSeconds: 0d,
                new[] { condition },
                string.Empty);

        private static void AssertGeneratedPresentationCalibration(
            CharacterPresentationCatalog presentation)
        {
            GameObject teimo = RequirePresentationPrefab(
                presentation,
                "presentation.character.fixture.stationary-service");
            AssertNamedMeshRenderer(teimo, "teimo_hat");
            AssertGlassesMaterialClosure(teimo);
            Assert.That(
                RequireNamedTransform(teimo, "Teimo").localPosition.y,
                Is.EqualTo(0.201f).Within(0.0001f),
                "Teimo's donor visual origin must be raised above the logical collider anchor.");
            LegacyCharacterPresentationBinding teimoBinding =
                teimo.GetComponent<LegacyCharacterPresentationBinding>();
            Assert.That(teimoBinding, Is.Not.Null);
            Assert.That(
                teimoBinding.HasActionBinding(
                    "action.character.teimo.cash-register"),
                Is.True,
                "The cash-register clip must be an explicit purchase action, not Teimo's permanent working pose.");
            Assert.That(
                teimoBinding.HasActionBinding("action.character.teimo.angry"),
                Is.True);
            Assert.That(
                teimoBinding.HasActionBinding(
                    "action.character.teimo.bring-food"),
                Is.True);
            Assert.That(
                teimoBinding.HasActionBinding(
                    "action.character.teimo.service-idle"),
                Is.True,
                "Kitchen waits need a standing loop instead of a frozen walk/cook frame.");
            Assert.That(
                teimoBinding.HasActionBinding(
                    "action.character.teimo.service-walk"),
                Is.True,
                "Kitchen navigation needs a presentation action so the authoritative Working state cannot replace fat_walk every tick.");
            FieldInfo stateBindingsField = typeof(
                    LegacyCharacterPresentationBinding)
                .GetField(
                    "stateBindings",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(stateBindingsField, Is.Not.Null);
            CharacterAnimationBinding[] teimoStates =
                (CharacterAnimationBinding[])stateBindingsField.GetValue(
                    teimoBinding);
            CharacterAnimationBinding teimoWorking = teimoStates.Single(
                value => value.State == CharacterActivityState.Working);
            Assert.That(
                teimoWorking.Clip.name,
                Is.EqualTo("teimo_lean_table_in"),
                "Outside a purchase action Teimo must hold the donor counter-lean pose.");
            Assert.That(
                teimoWorking.Loop,
                Is.False,
                "The lean-in clip must clamp its final pose instead of swaying forever.");
            FieldInfo actionBindingsField = typeof(
                    LegacyCharacterPresentationBinding)
                .GetField(
                    "actionBindings",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(actionBindingsField, Is.Not.Null);
            CharacterActionAnimationBinding[] teimoActions =
                (CharacterActionAnimationBinding[])actionBindingsField.GetValue(
                    teimoBinding);
            CharacterActionAnimationBinding serviceWalk = teimoActions.Single(
                value => value.ActionId ==
                    "action.character.teimo.service-walk");
            CharacterActionAnimationBinding serviceIdle = teimoActions.Single(
                value => value.ActionId ==
                    "action.character.teimo.service-idle");
            CharacterActionAnimationBinding carryWalk = teimoActions.Single(
                value => value.ActionId ==
                    "action.character.teimo.walk-hand-out");
            Assert.That(serviceWalk.Clip.name, Is.EqualTo("fat_walk"));
            Assert.That(serviceWalk.Loop, Is.True);
            Assert.That(serviceIdle.Clip.name, Is.EqualTo("fat_standing"));
            Assert.That(serviceIdle.Loop, Is.True);
            Assert.That(carryWalk.Loop, Is.True);

            GameObject teimoBicycle = RequirePresentationPrefab(
                presentation,
                "presentation.character.teimo-bicycle");
            AssertTeimoBicycleClosure(teimoBicycle);

            GameObject latanen = RequirePresentationPrefab(
                presentation,
                "presentation.character.fixture.vehicle-linked");
            AssertNamedMeshRenderer(latanen, "hat_busdriver");
            LegacyCharacterPresentationBinding latanenBinding =
                latanen.GetComponent<LegacyCharacterPresentationBinding>();
            Assert.That(latanenBinding, Is.Not.Null);
            Assert.That(
                latanenBinding.LegacyAnimation.transform.name,
                Is.EqualTo("skeleton"),
                "The detached bus-driver walker clips address pelvis and thigh " +
                "bones from the donor skeleton root, not the seated cash-loop " +
                "target at spine_middle.");
            Assert.That(
                latanenBinding.HasStateBinding(CharacterActivityState.Idle),
                Is.True);
            Assert.That(
                latanenBinding.HasStateBinding(CharacterActivityState.Walking),
                Is.True);
            Assert.That(
                latanenBinding.HasStateBinding(
                    CharacterActivityState.VehicleSeated),
                Is.False,
                "The seated bus driver is authored by the transport wrapper; " +
                "the state-only Latanen wrapper must remain a dedicated walker.");
            Assert.That(
                latanenBinding.LegacyAnimation.Cast<AnimationState>()
                    .Select(state => state.clip.name),
                Is.EquivalentTo(new[] { "fat_standing", "fat_walk" }));

            GameObject alpo = RequirePresentationPrefab(
                presentation,
                "presentation.character.fixture.scheduled-roaming");
            Assert.That(
                Vector3.Distance(
                    RequireNamedTransform(alpo, "Char").localPosition,
                    Vector3.zero),
                Is.LessThan(0.0001f),
                "Alpo's authored route anchor already contains the donor root elevation.");

            GameObject fleetari = RequirePresentationPrefab(
                presentation,
                "presentation.character.fleetari");
            Transform fleetariRoot = RequireNamedTransform(
                fleetari,
                "Neighbour 2");
            Assert.That(
                Vector3.Distance(
                    fleetariRoot.localPosition,
                    new Vector3(-0.0202864f, 0.47984046f, -0.1293867f)),
                Is.LessThan(0.0001f),
                "Fleetari's visual root must compensate the donor chair-parent tilt at the upright logical anchor.");
            Assert.That(
                Quaternion.Angle(
                    fleetariRoot.localRotation,
                    new Quaternion(
                        -0.18221372f,
                        -0.7523851f,
                        -0.17260018f,
                        -0.6090358f)),
                Is.LessThan(0.01f),
                "Fleetari's compensated root must reproduce the donor chair-relative world pose.");
            AssertGlassesMaterialClosure(fleetari);

            GameObject farmer = RequirePresentationPrefab(
                presentation,
                "presentation.character.farmer");
            AssertFarmerAccessoryClosure(farmer);

            GameObject berryman = RequirePresentationPrefab(
                presentation,
                "presentation.character.berryman");
            AssertNamedMeshRenderer(berryman, "Latsa");
            LegacyCharacterPresentationBinding berrymanBinding =
                berryman.GetComponent<LegacyCharacterPresentationBinding>();
            Assert.That(berrymanBinding.LegacyAnimation.transform.name,
                Is.EqualTo("hand_left"));
            Assert.That(
                berrymanBinding.LegacyAnimation.Cast<AnimationState>()
                    .Select(state => state.clip.name),
                Is.All.EqualTo("strawberryman_left_hand_loop"),
                "The seated berry picker must retain his donor hand loop; the missing tent is world content.");

            string[] r2PhysicalBindings =
            {
                "presentation.character.uncle-kesseli",
                "presentation.character.grandmother",
                "presentation.character.jokke",
                "presentation.character.suski",
                "presentation.character.sewage-client-1",
                "presentation.character.sewage-client-2",
                "presentation.character.sewage-client-3",
                "presentation.character.sewage-client-4",
                "presentation.character.sewage-client-5",
                "presentation.character.firewood-customer",
                "presentation.character.inspection-officer",
                "presentation.character.wastewater-attendant",
                "presentation.character.ventti-pigman",
            };
            foreach (string bindingId in r2PhysicalBindings)
            {
                RequirePresentationPrefab(presentation, bindingId);
            }

            GameObject suski = RequirePresentationPrefab(
                presentation,
                "presentation.character.suski");
            Assert.That(
                suski.GetComponentsInChildren<SkinnedMeshRenderer>(true),
                Has.Length.EqualTo(1),
                "Suski's normal presentation must use only the BetterMSC skin.");
            Assert.That(
                suski.GetComponentsInChildren<MeshRenderer>(true),
                Is.Empty,
                "The old donor Suski tail and cigarette must not remain in " +
                "the normal presentation.");
            Assert.That(
                RequireNamedTransform(suski, "BetterMSC Suski Model"),
                Is.Not.Null);

            GameObject uncle = RequirePresentationPrefab(
                presentation,
                "presentation.character.uncle-kesseli");
            AssertNamedMeshRenderer(uncle, "mesh");
            AssertGlassesMaterialClosure(uncle);

            GameObject grandmother = RequirePresentationPrefab(
                presentation,
                "presentation.character.grandmother");
            AssertNamedMeshRenderer(grandmother, "hat_granny");
            AssertNamedMeshRenderer(grandmother, "coffee_cup");
            AssertNamedMeshRenderer(grandmother, "coffee_plate");
            AssertNamedMeshRenderer(grandmother, "PotatoBox");
            AssertNamedMeshRenderer(grandmother, "garden_chair Context");
            AssertNamedMeshRenderer(grandmother, "garden_table Context");
            Assert.That(
                grandmother.transform.GetChild(0).localRotation,
                Is.Not.EqualTo(Quaternion.identity),
                "Grandmother must retain the donor seated lean relative to her project anchor.");
            Assert.That(
                grandmother.GetComponent<CharacterFlagPresentationBinding>(),
                Is.Not.Null,
                "The ordered-products tray must be driven by a project-owned flag.");

            GameObject jokke = RequirePresentationPrefab(
                presentation,
                "presentation.character.jokke");
            AssertNamedMeshRenderer(jokke, "ChairPlastic");
            AssertNamedMeshRenderer(jokke, "TablePlastic Context");
            AssertNamedMeshRenderer(jokke, "terrace_shade Context");
            Assert.That(
                jokke.transform.GetChild(0).localRotation,
                Is.Not.EqualTo(Quaternion.identity),
                "Jokke must retain the donor seated lean relative to his project anchor.");

            for (int index = 1; index <= 5; index++)
            {
                GameObject client = RequirePresentationPrefab(
                    presentation,
                    $"presentation.character.sewage-client-{index}");
                Assert.That(
                    client.GetComponentsInChildren<MeshRenderer>(true),
                    Has.Length.EqualTo(2),
                    $"Sewage client {index} requires one chair and one held beer bottle.");
                LegacyCharacterPresentationBinding clientBinding =
                    client.GetComponent<LegacyCharacterPresentationBinding>();
                Animation[] animationLayers =
                    client.GetComponentsInChildren<Animation>(true);
                Assert.That(animationLayers, Has.Length.EqualTo(2),
                    $"Sewage client {index} requires breath and right-hand drinking animation layers.");
                Animation drinkLayer = animationLayers.Single(animation =>
                    animation != clientBinding.LegacyAnimation);
                Assert.That(drinkLayer.transform.name,
                    Is.EqualTo("collar_right"));
                Assert.That(drinkLayer.clip, Is.Not.Null);
                Assert.That(drinkLayer.clip.name,
                    Is.EqualTo("fat_handsright_drink"));
                Assert.That(drinkLayer.playAutomatically, Is.False,
                    "The project-owned looping layer must own restarts across streaming transitions.");
                LegacyLoopingAnimationLayer loopingLayer =
                    drinkLayer.GetComponent<LegacyLoopingAnimationLayer>();
                Assert.That(loopingLayer, Is.Not.Null);
                Assert.That(loopingLayer.TryValidate(
                    out string loopingFailure), Is.True, loopingFailure);
                Assert.That(loopingLayer.LegacyAnimation,
                    Is.SameAs(drinkLayer));
                Assert.That(loopingLayer.Clip,
                    Is.SameAs(drinkLayer.clip));
                Assert.That(loopingLayer.StartTimeSeconds,
                    Is.GreaterThan(0f).And.LessThan(drinkLayer.clip.length));
            }

            GameObject firewoodCustomer = RequirePresentationPrefab(
                presentation,
                "presentation.character.firewood-customer");
            Assert.That(
                firewoodCustomer.GetComponentsInChildren<MeshRenderer>(true),
                Has.Length.EqualTo(1),
                "Livaloinen requires his held vodka bottle.");

            GameObject wastewaterAttendant = RequirePresentationPrefab(
                presentation,
                "presentation.character.wastewater-attendant");
            AssertGlassesMaterialClosure(wastewaterAttendant);

            GameObject ventti = RequirePresentationPrefab(
                presentation,
                "presentation.character.ventti-pigman");
            AssertNamedMeshRenderer(ventti, "latsa");
            AssertNamedMeshRenderer(ventti, "eye_glasses_dark");
        }

        private static GameObject RequirePresentationPrefab(
            CharacterPresentationCatalog presentation,
            string bindingId)
        {
            Assert.That(presentation.TryGet(bindingId, out var entry),
                Is.True,
                bindingId);
            Assert.That(entry.WrapperPrefab, Is.Not.Null, bindingId);
            return entry.WrapperPrefab;
        }

        private static Transform RequireNamedTransform(
            GameObject prefab,
            string name)
        {
            Transform result = prefab.GetComponentsInChildren<Transform>(true)
                .SingleOrDefault(candidate =>
                    string.Equals(candidate.name, name, StringComparison.Ordinal));
            Assert.That(result, Is.Not.Null,
                $"{prefab.name} lacks donor transform '{name}'.");
            return result;
        }

        private static void AssertNamedMeshRenderer(
            GameObject prefab,
            string name)
        {
            Transform transform = RequireNamedTransform(prefab, name);
            MeshFilter filter = transform.GetComponent<MeshFilter>();
            MeshRenderer renderer = transform.GetComponent<MeshRenderer>();
            Assert.That(filter, Is.Not.Null, name);
            Assert.That(filter.sharedMesh, Is.Not.Null, name);
            Assert.That(renderer, Is.Not.Null, name);
            Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(
                filter.sharedMesh.subMeshCount), name);
            Assert.That(renderer.sharedMaterials,
                Is.All.Not.Null,
                name);
        }

        private static void AssertGlassesMaterialClosure(GameObject prefab)
        {
            Transform glasses = RequireNamedTransform(
                prefab,
                "eye_glasses_regular");
            MeshFilter filter = glasses.GetComponent<MeshFilter>();
            MeshRenderer renderer = glasses.GetComponent<MeshRenderer>();
            Assert.That(filter, Is.Not.Null);
            Assert.That(filter.sharedMesh, Is.Not.Null);
            Assert.That(filter.sharedMesh.name, Is.EqualTo("eye_glasses"));
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(2));

            Material glass = renderer.sharedMaterials[0];
            Material metal = renderer.sharedMaterials[1];
            Assert.That(
                AssetDatabase.GetAssetPath(glass),
                Does.EndWith("/material_char_glass.mat"));
            Assert.That(glass.GetTexture("_BaseColorMap"), Is.Not.Null);
            Assert.That(glass.GetFloat("_SurfaceType"), Is.EqualTo(1f));
            Assert.That(glass.renderQueue,
                Is.GreaterThanOrEqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
            Assert.That(glass.GetFloat("_DstBlend"), Is.EqualTo(10f));
            Assert.That(
                AssetDatabase.GetAssetPath(metal),
                Does.EndWith("/material_char_metal-shiny.mat"));
            Assert.That(metal.GetFloat("_SurfaceType"), Is.EqualTo(0f));
            Assert.That(metal.GetFloat("_Metallic"),
                Is.EqualTo(0.406f).Within(0.0001f));
            Assert.That(metal.GetFloat("_Smoothness"),
                Is.EqualTo(0.802f).Within(0.0001f));
        }

        private static void AssertFarmerAccessoryClosure(GameObject prefab)
        {
            Transform hat = RequireNamedTransform(prefab, "mesh");
            MeshFilter hatFilter = hat.GetComponent<MeshFilter>();
            MeshRenderer hatRenderer = hat.GetComponent<MeshRenderer>();
            Assert.That(hatFilter, Is.Not.Null);
            Assert.That(hatFilter.sharedMesh, Is.Not.Null);
            Assert.That(hatFilter.sharedMesh.name, Is.EqualTo("gifu_hat"));
            Assert.That(hatRenderer, Is.Not.Null);
            Assert.That(hatRenderer.sharedMaterials, Has.Length.EqualTo(1));
            Assert.That(
                AssetDatabase.GetAssetPath(hatRenderer.sharedMaterials[0]),
                Does.EndWith("/material_char_shirt03.mat"));

            Transform glasses = RequireNamedTransform(
                prefab,
                "eye_glasses_dark");
            MeshFilter glassesFilter = glasses.GetComponent<MeshFilter>();
            MeshRenderer glassesRenderer = glasses.GetComponent<MeshRenderer>();
            Assert.That(glassesFilter, Is.Not.Null);
            Assert.That(glassesFilter.sharedMesh, Is.Not.Null);
            Assert.That(glassesFilter.sharedMesh.name,
                Is.EqualTo("eye_glasses2"));
            Assert.That(glassesRenderer, Is.Not.Null);
            Assert.That(glassesRenderer.sharedMaterials,
                Has.Length.EqualTo(2));
            Assert.That(
                AssetDatabase.GetAssetPath(glassesRenderer.sharedMaterials[0]),
                Does.EndWith("/material_char_glass.mat"));

            Material dark = glassesRenderer.sharedMaterials[1];
            Assert.That(
                AssetDatabase.GetAssetPath(dark),
                Does.EndWith("/material_char_glasses-dark.mat"));
            Assert.That(dark.GetTexture("_BaseColorMap"), Is.Not.Null);
            Assert.That(dark.GetFloat("_SurfaceType"), Is.EqualTo(0f));
            Assert.That(dark.GetFloat("_Metallic"),
                Is.EqualTo(0.08f).Within(0.0001f));
            Assert.That(dark.GetFloat("_Smoothness"),
                Is.EqualTo(0.68f).Within(0.0001f));
        }

        private static void AssertTeimoBicycleClosure(GameObject prefab)
        {
            AssertNamedMeshRenderer(prefab, "teimo_hat");
            AssertGlassesMaterialClosure(prefab);

            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            Assert.That(filters.Count(filter =>
                    filter.sharedMesh != null &&
                    string.Equals(filter.sharedMesh.name, "bicycle", StringComparison.Ordinal)),
                Is.EqualTo(1));
            Assert.That(filters.Count(filter =>
                    filter.sharedMesh != null &&
                    string.Equals(filter.sharedMesh.name, "bicycle_pedals", StringComparison.Ordinal)),
                Is.EqualTo(1));
            Assert.That(filters.Count(filter =>
                    filter.sharedMesh != null &&
                    string.Equals(filter.sharedMesh.name, "bicycle_tire", StringComparison.Ordinal)),
                Is.EqualTo(2));
            Assert.That(filters.Count(filter =>
                    filter.sharedMesh != null &&
                    string.Equals(filter.sharedMesh.name, "bicycle_rim", StringComparison.Ordinal)),
                Is.EqualTo(2));

            Material bicycleMaterial = filters
                .Where(filter => filter.sharedMesh != null &&
                    filter.sharedMesh.name.StartsWith(
                        "bicycle",
                        StringComparison.Ordinal))
                .SelectMany(filter =>
                    filter.GetComponent<MeshRenderer>().sharedMaterials)
                .Distinct()
                .Single();
            Assert.That(
                AssetDatabase.GetAssetPath(bicycleMaterial),
                Does.EndWith("/material_vehicle_teimo-bicycle.mat"));
            Assert.That(bicycleMaterial.GetTexture("_BaseColorMap"), Is.Not.Null);
            Assert.That(bicycleMaterial.GetFloat("_Metallic"),
                Is.EqualTo(0.49f).Within(0.0001f));
            Assert.That(bicycleMaterial.GetFloat("_Smoothness"),
                Is.EqualTo(0.38f).Within(0.0001f));

            LegacyCharacterPresentationBinding binding =
                prefab.GetComponent<LegacyCharacterPresentationBinding>();
            Assert.That(binding, Is.Not.Null);
            Assert.That(binding.LegacyAnimation.transform.name,
                Is.EqualTo("collar_right"));
            Assert.That(
                binding.LegacyAnimation.Cast<AnimationState>()
                    .Select(state => state.clip.name),
                Is.EqualTo(new[] { "teimo_bicycle_waving_hello" }));

            TeimoBicyclePresentationBinding motion =
                prefab.GetComponent<TeimoBicyclePresentationBinding>();
            Assert.That(motion, Is.Not.Null);
            Assert.That(motion.TryValidate(out string failure), Is.True,
                failure);
            Assert.That(motion.WheelDegreesPerMeter,
                Is.EqualTo(170f).Within(0.0001f));
            Assert.That(motion.PedalSpeedDivisor,
                Is.EqualTo(3.5f).Within(0.0001f));
            Assert.That(motion.PedalHalfWidthMeters,
                Is.EqualTo(0.158972f).Within(0.000001f));
            Assert.That(motion.PedalRadiusMeters,
                Is.EqualTo(0.180836f).Within(0.000001f));
            Assert.That(motion.LeftKnee.IsChildOf(motion.LeftHip), Is.True);
            Assert.That(motion.LeftAnkle.IsChildOf(motion.LeftKnee), Is.True);
            Assert.That(motion.RightKnee.IsChildOf(motion.RightHip), Is.True);
            Assert.That(motion.RightAnkle.IsChildOf(motion.RightKnee), Is.True);
            Assert.That(motion.GreetingDistanceMeters,
                Is.EqualTo(5f).Within(0.0001f));
            Assert.That(motion.GroundContactCalibrationMeters,
                Is.EqualTo(0.184355f).Within(0.001f));
            Assert.That(
                CalculateLowestLocalMeshPointY(
                    prefab.transform,
                    motion.FrontWheel,
                    motion.RearWheel),
                Is.EqualTo(0f).Within(0.001f));
        }

        private static GameObject CreateWorldEntity(
            string stableId,
            ICollection<GameObject> created)
        {
            var owner = new GameObject("world-entity-" + stableId);
            created.Add(owner);
            DonorWorldBaselineEntityMetadata metadata =
                owner.AddComponent<DonorWorldBaselineEntityMetadata>();
            metadata.Configure(
                stableId,
                1L,
                string.Empty,
                "test/entity",
                string.Empty,
                "cell.test",
                "Test",
                "4;23",
                "RendererAccepted",
                "test",
                true,
                true,
                true);
            return owner;
        }

        private static float CalculateLowestLocalMeshPointY(
            Transform root,
            params Transform[] meshTransforms)
        {
            float minimumY = float.PositiveInfinity;
            foreach (Transform meshTransform in meshTransforms)
            {
                Bounds bounds = meshTransform.GetComponent<MeshFilter>()
                    .sharedMesh.bounds;
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 corner = bounds.center + Vector3.Scale(
                                bounds.extents,
                                new Vector3(x, y, z));
                            minimumY = Mathf.Min(
                                minimumY,
                                root.InverseTransformPoint(
                                    meshTransform.TransformPoint(corner)).y);
                        }
                    }
                }
            }

            return minimumY;
        }

        private static void AssertPresentationClipMovesBones(
            LegacyCharacterPresentationBinding binding)
        {
            Animation animation = binding.LegacyAnimation;
            Assert.That(animation.cullingType,
                Is.EqualTo(AnimationCullingType.AlwaysAnimate),
                binding.BindingId);
            AnimationState state = animation.Cast<AnimationState>().First();
            SkinnedMeshRenderer renderer =
                binding.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(renderer, Is.Not.Null, binding.BindingId);
            Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(3),
                binding.BindingId);
            Assert.That(
                renderer.sharedMaterials.All(material =>
                    material != null && material.GetTexture("_BaseColorMap") != null),
                Is.True,
                binding.BindingId);
            AnimationClip clip = state.clip;
            Transform[] targets = AnimationUtility.GetCurveBindings(clip)
                .Select(curve => string.IsNullOrEmpty(curve.path)
                    ? animation.transform
                    : animation.transform.Find(curve.path))
                .Where(target => target != null)
                .Distinct()
                .ToArray();
            Assert.That(targets, Is.Not.Empty, binding.BindingId);

            state.enabled = true;
            state.weight = 1f;
            state.time = 0f;
            animation.Sample();
            Quaternion[] initialRotations = targets
                .Select(target => target.localRotation)
                .ToArray();
            bool moved = false;
            foreach (float normalizedTime in new[] { 0.25f, 0.5f, 0.75f, 1f })
            {
                state.time = clip.length * normalizedTime;
                animation.Sample();
                for (int index = 0; index < targets.Length; index++)
                {
                    if (Quaternion.Angle(
                            initialRotations[index],
                            targets[index].localRotation) > 0.05f)
                    {
                        moved = true;
                        break;
                    }
                }

                if (moved)
                {
                    break;
                }
            }

            Assert.That(moved, Is.True,
                $"{binding.BindingId} clip '{clip.name}' did not change any bound bone.");
        }

        private sealed class ExternalConditions : INpcExternalConditionSource
        {
            public bool GetFlag(string flagId) =>
                string.Equals(
                    flagId,
                    "flag.external.job-ready",
                    StringComparison.Ordinal);
        }

        private sealed class RecordingSink : INpcDomainEventSink
        {
            public List<string> Events { get; } = new List<string>();

            public void Emit(string eventId, string characterDefinitionId)
            {
                Events.Add(eventId + ":" + characterDefinitionId);
            }
        }

        private sealed class OperationalStoryTrafficMotionBackend :
            MonoBehaviour,
            IStoryTrafficVehicleMotionBackend
        {
            public bool IsOperational => true;
            public float SpeedMetersPerSecond => 0f;
            public Vector3 VelocityMetersPerSecond => Vector3.zero;
            public bool HasGroundContact => true;
            public float EngineRpm => 900f;
            public float EngineRedlineRpm => 6000f;
            public float EngineLoad01 => 0f;
            public int SelectedGear => 1;

            public void Step(
                float fixedDeltaSeconds,
                in StoryTrafficVehicleDriveCommand command)
            {
            }

            public void SnapToPose(
                Vector3 position,
                Quaternion rotation,
                float forwardSpeedMetersPerSecond)
            {
                transform.SetPositionAndRotation(position, rotation);
            }

            public void ResetMotion()
            {
            }

            public bool TryGetWheelVisualState(
                int wheelIndex,
                out float steeringAngleDegrees,
                out float axleAngleDegrees)
            {
                steeringAngleDegrees = 0f;
                axleAngleDegrees = 0f;
                return false;
            }
        }

        private sealed class FakeCellAvailability : INpcCellAvailability
        {
            private bool loaded;
            private readonly Dictionary<string, bool> loadedByCell =
                new Dictionary<string, bool>(StringComparer.Ordinal);
            private string resolvedCellId = string.Empty;

            public FakeCellAvailability(bool initiallyLoaded)
            {
                loaded = initiallyLoaded;
            }

            public event Action<string, bool> AvailabilityChanged;

            public bool IsLoaded(string cellId) =>
                loadedByCell.TryGetValue(
                    cellId ?? string.Empty,
                    out bool cellLoaded)
                    ? cellLoaded
                    : loaded;

            public bool TryResolveCellId(
                Vector3 worldPosition,
                out string cellId)
            {
                cellId = resolvedCellId;
                return !string.IsNullOrEmpty(cellId);
            }

            public void SetResolvedCell(string cellId)
            {
                resolvedCellId = cellId ?? string.Empty;
            }

            public void ClearResolvedCell()
            {
                resolvedCellId = string.Empty;
            }

            public void SetCellLoaded(string cellId, bool value)
            {
                loadedByCell[cellId ?? string.Empty] = value;
                AvailabilityChanged?.Invoke(cellId ?? string.Empty, value);
            }

            public void SetLoaded(string cellId, bool value)
            {
                loaded = value;
                loadedByCell.Clear();
                AvailabilityChanged?.Invoke(cellId, value);
            }

            public void Dispose()
            {
            }
        }
    }
}
