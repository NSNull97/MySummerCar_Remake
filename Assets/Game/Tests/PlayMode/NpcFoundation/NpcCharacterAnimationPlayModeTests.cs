using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MSC.Audio;
using MSC.Bootstrap;
using MSC.Characters;
using MSC.Core.Time;
using MSC.NPC;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Array = System.Array;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MSC.NPC.Tests.PlayMode
{
    public sealed class NpcCharacterAnimationPlayModeTests
    {
#if UNITY_EDITOR
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator FullBootstrap_StoryTrafficLeavesPerajarviFormation()
        {
            const string bootstrapScenePath =
                "Assets/Game/Bootstrap/Bootstrap.unity";
            var initialSceneHandles = new HashSet<SceneHandle>();
            for (int sceneIndex = 0;
                 sceneIndex < SceneManager.sceneCount;
                 sceneIndex++)
            {
                initialSceneHandles.Add(
                    SceneManager.GetSceneAt(sceneIndex).handle);
            }
            yield return SceneManager.LoadSceneAsync(
                bootstrapScenePath,
                LoadSceneMode.Additive);
            yield return null;

            ProductionWorldStreamingInstaller installer =
                Object.FindFirstObjectByType<
                    ProductionWorldStreamingInstaller>(
                    FindObjectsInactive.Include);
            Assert.That(installer, Is.Not.Null);
            Assert.That(installer.SpawnedPlayer, Is.Not.Null);
            installer.SpawnedPlayer.transform.position =
                new Vector3(-1230f, 4f, 85f);
            if (!installer.IsGameplayPrepared)
            {
                Assert.That(
                    installer.TryBeginGameplayPreparation(
                        out string preparationFailure),
                    Is.True,
                    preparationFailure);
                for (int frame = 0;
                     frame < 900 && !installer.IsGameplayPrepared;
                     frame++)
                {
                    yield return null;
                }
            }
            else
            {
                yield return installer.WorldStreaming.RefreshNow();
            }

            Assert.That(installer.IsGameplayPrepared, Is.True,
                installer.LastGameplayPreparationFailure);
            // Bootstrap opens on the main-menu surface, which intentionally
            // pauses Unity physics. The test enters the same unpaused state as
            // the running game before waiting for FixedUpdate.
            Time.timeScale = 1f;
            Assert.That(
                installer.Environment.DevTrySetDateAndTime(
                    new GameDate(1995, 8, 4),
                    16d * 3600d + 4d * 60d,
                    out string timeFailure),
                Is.True,
                timeFailure);
            yield return new WaitForFixedUpdate();

            StoryTrafficVehiclePresentationBinding[] traffic =
                Object.FindObjectsByType<
                    StoryTrafficVehiclePresentationBinding>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            StoryTrafficVehiclePresentationBinding jani = traffic.FirstOrDefault(
                candidate => string.Equals(
                    candidate.DriverFeatureId,
                    "P1.NPC.049",
                    System.StringComparison.Ordinal));
            StoryTrafficVehiclePresentationBinding petteri =
                traffic.FirstOrDefault(candidate => string.Equals(
                    candidate.DriverFeatureId,
                    "P1.NPC.050",
                    System.StringComparison.Ordinal));
            Assert.That(jani, Is.Not.Null);
            Assert.That(petteri, Is.Not.Null);
            Assert.That(jani.GetComponent<CapsuleCollider>(), Is.Null,
                "Dialogue setup must not add a wheel-lifting humanoid capsule " +
                "to Jani's physical chassis.");
            Assert.That(petteri.GetComponent<CapsuleCollider>(), Is.Null,
                "Dialogue setup must not add a wheel-lifting humanoid capsule " +
                "to Petteri's physical chassis.");
            Vector3 janiStart = jani.transform.position;
            Vector3 petteriStart = petteri.transform.position;
            Vector3 previousJaniPosition = janiStart;
            Vector3 previousPetteriPosition = petteriStart;
            float maximumJaniFixedStep = 0f;
            float maximumPetteriFixedStep = 0f;
            double previousJaniProgress = 0d;
            double previousPetteriProgress = 0d;
            NpcWorldRuntime npcRuntime = Object.FindFirstObjectByType<
                NpcWorldRuntime>(FindObjectsInactive.Exclude);
            Assert.That(npcRuntime, Is.Not.Null);

            for (int step = 0; step < 900; step++)
            {
                yield return new WaitForFixedUpdate();
                // Keep the test observer inside the production physical-
                // residency radius while the pair leaves Perajarvi. Streaming
                // is covered separately; this fixture measures continuous
                // wheel-driven travel and must not retain destroyed wrappers.
                installer.SpawnedPlayer.transform.position =
                    (jani.transform.position + petteri.transform.position) *
                    0.5f + Vector3.up * 2f;
                maximumJaniFixedStep = Mathf.Max(
                    maximumJaniFixedStep,
                    Vector3.Distance(previousJaniPosition,
                        jani.transform.position));
                maximumPetteriFixedStep = Mathf.Max(
                    maximumPetteriFixedStep,
                    Vector3.Distance(previousPetteriPosition,
                        petteri.transform.position));
                previousJaniPosition = jani.transform.position;
                previousPetteriPosition = petteri.transform.position;
                Assert.That(npcRuntime.TryGetInstance(
                    "character.jani", out CharacterInstance janiState), Is.True);
                Assert.That(npcRuntime.TryGetInstance(
                    "character.petteri", out CharacterInstance petteriState), Is.True);
                Assert.That(janiState.RouteProgress01 + 0.000001d,
                    Is.GreaterThanOrEqualTo(previousJaniProgress),
                    "Jani's physical route progress moved backwards.");
                Assert.That(petteriState.RouteProgress01 + 0.000001d,
                    Is.GreaterThanOrEqualTo(previousPetteriProgress),
                    "Petteri's physical route progress moved backwards.");
                previousJaniProgress = janiState.RouteProgress01;
                previousPetteriProgress = petteriState.RouteProgress01;
            }

            float janiDistance = Vector3.Distance(
                janiStart,
                jani.transform.position);
            float petteriDistance = Vector3.Distance(
                petteriStart,
                petteri.transform.position);
            StoryTrafficVehicleAudioPresenter[] audioOwners =
                Object.FindObjectsByType<StoryTrafficVehicleAudioPresenter>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Debug.Log(
                "Full Bootstrap story traffic diagnostic: " +
                $"Jani={jani.transform.position}/{janiDistance:F2}m/" +
                $"{jani.CurrentSpeedMetersPerSecond:F2}mps/" +
                $"desired={jani.LastDesiredSpeedMetersPerSecond:F2}/" +
                $"target={jani.LastPhysicalTargetDistanceMeters:F2}/" +
                $"grounded={jani.HasPhysicalGroundContact}/" +
                $"body={jani.PhysicalVelocityMetersPerSecond.magnitude:F2}/" +
                $"rpm={jani.PhysicalEngineRpm:F0}/" +
                $"gear={jani.PhysicalSelectedGear}/" +
                $"{jani.ManeuverState}/blocked={jani.IsObstacleBraking}/" +
                $"obstacle={jani.TrackedObstacleName}; " +
                $"Petteri={petteri.transform.position}/{petteriDistance:F2}m/" +
                $"{petteri.CurrentSpeedMetersPerSecond:F2}mps/" +
                $"desired={petteri.LastDesiredSpeedMetersPerSecond:F2}/" +
                $"target={petteri.LastPhysicalTargetDistanceMeters:F2}/" +
                $"grounded={petteri.HasPhysicalGroundContact}/" +
                $"body={petteri.PhysicalVelocityMetersPerSecond.magnitude:F2}/" +
                $"rpm={petteri.PhysicalEngineRpm:F0}/" +
                $"gear={petteri.PhysicalSelectedGear}/" +
                $"{petteri.ManeuverState}/blocked={petteri.IsObstacleBraking}/" +
                $"obstacle={petteri.TrackedObstacleName}; " +
                $"audioOwners={audioOwners.Length}/" +
                $"initialized={audioOwners.Count(owner => owner.IsInitialized)}");

            int initializedAudioOwnerCount = audioOwners.Count(
                owner => owner.IsInitialized);
            GameCompositionRoot compositionRoot =
                GameCompositionRoot.ActiveRoot;
            if (compositionRoot != null)
            {
                compositionRoot.SendMessage(
                    "EndSessionAndDestroy",
                    SendMessageOptions.RequireReceiver);
                yield return null;
            }

            var addedScenes = new List<Scene>();
            for (int sceneIndex = 0;
                 sceneIndex < SceneManager.sceneCount;
                 sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (scene.isLoaded &&
                    !initialSceneHandles.Contains(scene.handle))
                {
                    addedScenes.Add(scene);
                }
            }

            foreach (Scene scene in addedScenes)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null)
                {
                    yield return unload;
                }
            }

            Assert.That(janiDistance, Is.GreaterThan(20f));
            Assert.That(petteriDistance, Is.GreaterThan(20f));
            Assert.That(previousJaniProgress, Is.GreaterThan(0.001d));
            Assert.That(previousPetteriProgress, Is.GreaterThan(0.001d));
            Assert.That(maximumJaniFixedStep, Is.LessThan(5f),
                "Jani teleported during physical route driving.");
            Assert.That(maximumPetteriFixedStep, Is.LessThan(5f),
                "Petteri teleported during physical route driving.");
            Assert.That(initializedAudioOwnerCount,
                Is.GreaterThanOrEqualTo(2));
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator NpcRuntime_ContinuousClockTicksDriveRealPerajarviFormation()
        {
            const string globalScenePath =
                "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/World_Global_Legacy.unity";
            const string perajarviScenePath =
                "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_-3_0_Legacy.unity";
            yield return SceneManager.LoadSceneAsync(
                globalScenePath,
                LoadSceneMode.Additive);
            yield return SceneManager.LoadSceneAsync(
                perajarviScenePath,
                LoadSceneMode.Additive);

            CharacterDefinitionCatalog characters =
                AssetDatabase.LoadAssetAtPath<CharacterDefinitionCatalog>(
                    "Assets/Game/NPC/Content/Phase1Foundation/" +
                    "CharacterDefinitionCatalog.asset");
            NpcFoundationCatalog foundation =
                AssetDatabase.LoadAssetAtPath<NpcFoundationCatalog>(
                    "Assets/Game/NPC/Content/Phase1Foundation/" +
                    "NpcFoundationCatalog.asset");
            CharacterPresentationCatalog presentations =
                Resources.Load<CharacterPresentationCatalog>(
                    "Phase1Characters/CharacterPresentationCatalog");
            Assert.That(characters, Is.Not.Null);
            Assert.That(foundation, Is.Not.Null);
            Assert.That(presentations, Is.Not.Null);

            var root = new GameObject("NPC_Runtime_RealPerajarvi_Test");
            var player = new GameObject("NPC_Runtime_RealPerajarvi_Player");
            player.transform.position = new Vector3(-1230f, 4f, 85f);
            var gameTime = new GameTimeService();
            var cells = new AlwaysLoadedNpcCellAvailability();
            NpcWorldRuntime runtime = root.AddComponent<NpcWorldRuntime>();
            runtime.InitializeInternal(
                characters,
                foundation,
                presentations,
                gameTime,
                cells,
                new DirectNpcNavigationBackend(),
                player.transform);
            // The fixture clock begins shortly before the donor 16:00 Amis
            // activation. Entering it through the normal time publication is
            // important: this is the production reconciliation path that used
            // to overwrite physical guidance every tick.
            gameTime.Advance(1500d);
            yield return new WaitForFixedUpdate();

            Transform janiRoot = root.transform.Find(
                "NPC_Presentation_character.jani");
            Transform petteriRoot = root.transform.Find(
                "NPC_Presentation_character.petteri");
            Assert.That(janiRoot, Is.Not.Null);
            Assert.That(petteriRoot, Is.Not.Null);
            StoryTrafficVehiclePresentationBinding jani =
                janiRoot.GetComponent<StoryTrafficVehiclePresentationBinding>();
            StoryTrafficVehiclePresentationBinding petteri =
                petteriRoot.GetComponent<StoryTrafficVehiclePresentationBinding>();
            Assert.That(jani.HasPhysicalMotionBackend, Is.True);
            Assert.That(petteri.HasPhysicalMotionBackend, Is.True);
            Vector3 janiStart = jani.transform.position;
            Vector3 petteriStart = petteri.transform.position;

            bool bothMoved = false;
            for (int step = 0; step < 220; step++)
            {
                runtime.ReevaluateNow();
                yield return new WaitForFixedUpdate();
                if (Vector3.Distance(janiStart, jani.transform.position) > 3f &&
                    Vector3.Distance(petteriStart, petteri.transform.position) > 3f &&
                    jani.CurrentSpeedMetersPerSecond > 2f &&
                    petteri.CurrentSpeedMetersPerSecond > 2f)
                {
                    bothMoved = true;
                    break;
                }
            }

            Debug.Log(
                "NPC runtime real Perajarvi formation diagnostic: " +
                $"Jani={jani.transform.position}/" +
                $"{jani.CurrentSpeedMetersPerSecond:F2}mps/" +
                $"{jani.ManeuverState}/blocked={jani.IsObstacleBraking}; " +
                $"Petteri={petteri.transform.position}/" +
                $"{petteri.CurrentSpeedMetersPerSecond:F2}mps/" +
                $"{petteri.ManeuverState}/blocked={petteri.IsObstacleBraking}");

            Object.Destroy(root);
            Object.Destroy(player);
            yield return null;
            yield return SceneManager.UnloadSceneAsync(perajarviScenePath);
            yield return SceneManager.UnloadSceneAsync(globalScenePath);

            Assert.That(bothMoved, Is.True,
                "NpcWorldRuntime did not keep physical guidance authoritative " +
                "during continuous game-clock reconciliation.");
        }
#endif

        [UnityTest]
        public IEnumerator WalkingPose_ConformsToLoadedWorldSurface()
        {
            int worldSurfaceLayer = LayerMask.NameToLayer("WorldSurface");
            Assert.That(worldSurfaceLayer, Is.GreaterThanOrEqualTo(0));

            var surface = new GameObject("NPC_PlayMode_GroundSurface");
            var presentation = new GameObject("NPC_PlayMode_Walker");
            try
            {
                surface.layer = worldSurfaceLayer;
                surface.transform.position = new Vector3(7000f, 1.5f, 7000f);
                BoxCollider collider = surface.AddComponent<BoxCollider>();
                collider.size = new Vector3(10f, 1f, 10f);
                Physics.SyncTransforms();
                yield return null;

                var navigation = new DirectNpcNavigationBackend();
                navigation.ApplyPose(
                    presentation.transform,
                    new NpcPose(
                        new Vector3(7000f, 3.25f, 7000f),
                        Quaternion.identity,
                        "cell.fixture",
                        shouldConformToGround: true));

                Assert.That(
                    presentation.transform.position.y,
                    Is.EqualTo(2f).Within(0.0001f));
            }
            finally
            {
                Object.Destroy(surface);
                Object.Destroy(presentation);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator StoryTrafficRouteMotion_PassesObstacleWithoutTeleporting()
        {
            int worldSurfaceLayer = LayerMask.NameToLayer("WorldSurface");
            Assert.That(worldSurfaceLayer, Is.GreaterThanOrEqualTo(0));

            var ground = new GameObject("StoryTraffic_TestGround");
            var obstacle = new GameObject("StoryTraffic_TestObstacle");
            var vehicle = new GameObject("StoryTraffic_TestVehicle");
            try
            {
                Vector3 start = new Vector3(8000f, 0f, 8000f);
                ground.layer = worldSurfaceLayer;
                ground.transform.position = start + Vector3.down * 0.5f;
                BoxCollider groundCollider = ground.AddComponent<BoxCollider>();
                groundCollider.size = new Vector3(40f, 1f, 240f);

                obstacle.transform.position = start +
                                              new Vector3(0f, 0.75f, 4f);
                BoxCollider obstacleCollider =
                    obstacle.AddComponent<BoxCollider>();
                obstacleCollider.size = new Vector3(2f, 1.5f, 1f);

                Transform[] wheels = Enumerable.Range(0, 4)
                    .Select(index =>
                    {
                        var wheel = new GameObject("Wheel_" + index);
                        wheel.transform.SetParent(vehicle.transform, false);
                        return wheel.transform;
                    })
                    .ToArray();
                StoryTrafficVehiclePresentationBinding motion =
                    vehicle.AddComponent<
                        StoryTrafficVehiclePresentationBinding>();
                motion.ConfigureForAuthoring(
                    "P1.NPC.049",
                    System.Array.Empty<string>(),
                    System.Array.Empty<GameObject>(),
                    wheels,
                    configuredWheelDegreesPerMeter: 190f,
                    configuredGroundContactCalibrationMeters: 0.35f);
                int collisionEvents = 0;
                motion.CollisionIncident += _ => collisionEvents++;

                Physics.SyncTransforms();
                var setupHits = new RaycastHit[8];
                int setupHitCount = Physics.SphereCastNonAlloc(
                    start + Vector3.up * 0.65f,
                    0.65f,
                    Vector3.forward,
                    setupHits,
                    5f,
                    Physics.DefaultRaycastLayers &
                    ~(1 << worldSurfaceLayer),
                    QueryTriggerInteraction.Ignore);
                Assert.That(
                    setupHits.Take(setupHitCount)
                        .Any(hit => hit.collider == obstacleCollider),
                    Is.True,
                    "Story-traffic obstacle fixture must intersect the " +
                    "production probe volume.");
                motion.SetRoutePoseTarget(start, Quaternion.identity);
                yield return new WaitForFixedUpdate();
                Assert.That(
                    vehicle.transform.position.y,
                    Is.EqualTo(start.y).Within(0.02f),
                    "Importer wheel correction must not be added to the " +
                    "wrapper road height a second time.");
                // Production receives dense Highway samples from the game-time
                // route evaluation. Keep the fixture equally dense so the
                // lane-change target follows the road instead of cutting a
                // synthetic twenty-metre chord.
                for (int distance = 1; distance <= 120; distance++)
                {
                    motion.SetRoutePoseTarget(
                        start + Vector3.forward * distance,
                        Quaternion.identity);
                }

                bool enteredPassingManeuver = false;
                float maximumLaneOffset = 0f;
                float maximumFixedStep = 0f;
                Vector3 previous = vehicle.transform.position;
                // This fixture deliberately starts only four metres from a
                // stationary block. The bounded controller must first brake,
                // reverse for clearance, then pass; allow that complete
                // non-teleport recovery cycle rather than timing out midway.
                for (int index = 0; index < 480; index++)
                {
                    yield return new WaitForFixedUpdate();
                    enteredPassingManeuver |=
                        motion.ManeuverState ==
                            StoryTrafficManeuverState.PassingOut ||
                        motion.ManeuverState ==
                            StoryTrafficManeuverState.Passing ||
                        motion.ManeuverState ==
                            StoryTrafficManeuverState.Returning;
                    maximumLaneOffset = Mathf.Max(
                        maximumLaneOffset,
                        Mathf.Abs(motion.CurrentLaneOffsetMeters));
                    maximumFixedStep = Mathf.Max(
                        maximumFixedStep,
                        Vector3.Distance(previous, vehicle.transform.position));
                    previous = vehicle.transform.position;
                    if (vehicle.transform.position.z >= start.z + 14f &&
                        motion.ManeuverState ==
                            StoryTrafficManeuverState.Cruise)
                    {
                        break;
                    }
                }

                Assert.That(collisionEvents, Is.Zero,
                    "A forward obstacle probe is planning evidence, not a " +
                    "physical collision event.");
                Assert.That(enteredPassingManeuver, Is.True,
                    "Story traffic never entered its passing state.");
                Assert.That(maximumLaneOffset, Is.GreaterThan(1.5f),
                    "Story traffic did not leave its driving lane.");
                Assert.That(vehicle.transform.position.z,
                    Is.GreaterThan(start.z + 10f),
                    "Story traffic did not pass the persistent obstacle. " +
                    $"position={vehicle.transform.position}, " +
                    $"bodyPosition={vehicle.GetComponent<Rigidbody>().position}, " +
                    $"state={motion.ManeuverState}, " +
                    $"speed={motion.CurrentSpeedMetersPerSecond:F3}, " +
                    $"desired={motion.LastDesiredSpeedMetersPerSecond:F3}, " +
                    $"lane={motion.CurrentLaneOffsetMeters:F3}, " +
                    $"targetDistance={motion.LastPhysicalTargetDistanceMeters:F3}, " +
                    $"pending={motion.PendingRouteSampleCount}, " +
                    $"braking={motion.IsObstacleBraking}, " +
                    $"obstacle={motion.TrackedObstacleName}.");
                Assert.That(maximumFixedStep, Is.LessThan(1f),
                    "Story traffic teleported while passing the obstacle.");
            }
            finally
            {
                Object.Destroy(ground);
                Object.Destroy(obstacle);
                Object.Destroy(vehicle);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator StoryTrafficImpact_UsesRecoverableThresholdWithoutTerminalCrash()
        {
            int worldSurfaceLayer = LayerMask.NameToLayer("WorldSurface");
            Assert.That(worldSurfaceLayer, Is.GreaterThanOrEqualTo(0));

            var ground = new GameObject("StoryTraffic_ImpactGround");
            var impactFixture = new GameObject("StoryTraffic_ImpactFixture");
            var vehicle = new GameObject("StoryTraffic_ImpactVehicle");
            try
            {
                Vector3 start = new Vector3(8050f, 0f, 8050f);
                ground.layer = worldSurfaceLayer;
                ground.transform.position = start + Vector3.down * 0.5f;
                BoxCollider groundCollider = ground.AddComponent<BoxCollider>();
                groundCollider.size = new Vector3(30f, 1f, 180f);
                impactFixture.transform.position = start +
                                                   Vector3.right * 12f;
                BoxCollider impactCollider =
                    impactFixture.AddComponent<BoxCollider>();

                Transform[] wheels = Enumerable.Range(0, 4)
                    .Select(index =>
                    {
                        var wheel = new GameObject("ImpactWheel_" + index);
                        wheel.transform.SetParent(vehicle.transform, false);
                        return wheel.transform;
                    })
                    .ToArray();
                StoryTrafficVehiclePresentationBinding motion =
                    vehicle.AddComponent<
                        StoryTrafficVehiclePresentationBinding>();
                motion.ConfigureForAuthoring(
                    "P1.NPC.049",
                    Array.Empty<string>(),
                    Array.Empty<GameObject>(),
                    wheels,
                    configuredWheelDegreesPerMeter: 190f,
                    configuredGroundContactCalibrationMeters: 0.35f);

                Physics.SyncTransforms();
                motion.SetRoutePoseTarget(start, Quaternion.identity);
                for (int distance = 1; distance <= 120; distance++)
                {
                    motion.SetRoutePoseTarget(
                        start + Vector3.forward * distance,
                        Quaternion.identity);
                }

                for (int frame = 0; frame < 60; frame++)
                {
                    yield return new WaitForFixedUpdate();
                }

                int incidents = 0;
                int terminalIncidents = 0;
                StoryTrafficCollisionEvent lastIncident = default;
                motion.CollisionIncident += incident =>
                {
                    incidents++;
                    lastIncident = incident;
                };
                motion.TerminalCrash += _ => terminalIncidents++;
                Assert.That(
                    motion.ReportImpact(
                        impactCollider,
                        vehicle.transform.position,
                        4.99f),
                    Is.False,
                    "Sub-threshold contact must not enter recoverable crash state.");
                Assert.That(motion.IsCrashed, Is.False);
                Assert.That(lastIncident.IsCrash, Is.False);

                for (int frame = 0; frame < 20; frame++)
                {
                    yield return new WaitForFixedUpdate();
                }

                Vector3 beforeCrash = vehicle.transform.position;
                Assert.That(
                    motion.ReportImpact(
                        impactCollider,
                        beforeCrash,
                        5f),
                    Is.True,
                    "Recoverable contact threshold was not applied.");
                Assert.That(motion.IsCrashed, Is.True);
                Assert.That(lastIncident.IsCrash, Is.True);
                Assert.That(lastIncident.SpeedMetersPerSecond,
                    Is.EqualTo(5f).Within(0.001f));

                float maximumStep = 0f;
                Vector3 previous = vehicle.transform.position;
                for (int frame = 0; frame < 230; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    maximumStep = Mathf.Max(
                        maximumStep,
                        Vector3.Distance(previous, vehicle.transform.position));
                    previous = vehicle.transform.position;
                }

                Assert.That(incidents, Is.EqualTo(2));
                Assert.That(terminalIncidents, Is.Zero,
                    "An ordinary recoverable contact emitted a terminal crash.");
                Assert.That(motion.RecoveryCount, Is.EqualTo(1));
                Assert.That(motion.IsCrashed, Is.False);
                Assert.That(
                    motion.ManeuverState,
                    Is.Not.EqualTo(StoryTrafficManeuverState.Recovering));
                Assert.That(maximumStep, Is.LessThan(1f),
                    "Crash recovery teleported the story car.");

                Assert.That(
                    motion.ReportImpact(
                        impactCollider,
                        vehicle.transform.position,
                        12f),
                    Is.True);
                motion.SetStoryIncidentHold(true);
                int recoveryCountAtTerminalIncident =
                    motion.RecoveryCount;
                for (int frame = 0; frame < 180; frame++)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(motion.IsCrashed, Is.True,
                    "A terminal story incident recovered after its crash hold.");
                Assert.That(
                    motion.RecoveryCount,
                    Is.EqualTo(recoveryCountAtTerminalIncident));
                Assert.That(terminalIncidents, Is.Zero,
                    "A manual hold must not fabricate structural failure.");
            }
            finally
            {
                Object.Destroy(ground);
                Object.Destroy(impactFixture);
                Object.Destroy(vehicle);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator StoryTrafficHighSpeedCorner_EntersBoundedDrift()
        {
            int worldSurfaceLayer = LayerMask.NameToLayer("WorldSurface");
            Assert.That(worldSurfaceLayer, Is.GreaterThanOrEqualTo(0));

            var ground = new GameObject("StoryTraffic_DriftGround");
            var vehicle = new GameObject("StoryTraffic_DriftVehicle");
            try
            {
                Vector3 start = new Vector3(8300f, 0f, 8300f);
                ground.layer = worldSurfaceLayer;
                ground.transform.position = start +
                                            new Vector3(70f, -0.5f, 70f);
                BoxCollider groundCollider = ground.AddComponent<BoxCollider>();
                groundCollider.size = new Vector3(180f, 1f, 180f);

                Transform[] wheels = Enumerable.Range(0, 4)
                    .Select(index =>
                    {
                        var wheel = new GameObject("DriftWheel_" + index);
                        wheel.transform.SetParent(vehicle.transform, false);
                        return wheel.transform;
                    })
                    .ToArray();
                StoryTrafficVehiclePresentationBinding motion =
                    vehicle.AddComponent<
                        StoryTrafficVehiclePresentationBinding>();
                motion.ConfigureForAuthoring(
                    "P1.NPC.049",
                    Array.Empty<string>(),
                    Array.Empty<GameObject>(),
                    wheels,
                    configuredWheelDegreesPerMeter: 190f,
                    configuredGroundContactCalibrationMeters: 0.35f);
                motion.ConfigureDrivingProfile(
                    configuredMinimumCruiseSpeedMetersPerSecond: 115f / 3.6f,
                    configuredMaximumSpeedMetersPerSecond: 185f / 3.6f,
                    configuredAccelerationMetersPerSecond2: 8f,
                    configuredBrakingMetersPerSecond2: 20f,
                    configuredTurnRateDegreesPerSecond: 150f,
                    configuredPassingLaneOffsetMeters: -3.2f);

                Physics.SyncTransforms();
                motion.SetRoutePoseTarget(start, Quaternion.identity);
                motion.RestoreRuntimeState(new StoryTrafficMotionRuntimeState(
                    speedMetersPerSecond: 30f,
                    cruiseSpeedMetersPerSecond: 40f,
                    laneOffsetMeters: 0f,
                    maneuverState: StoryTrafficManeuverState.Cruise,
                    maneuverStateSeconds: 0f,
                    driftSlipDegrees: 0f,
                    recoveryCount: 0,
                    hasSafePose: true,
                    safePosition: start,
                    safeRotation: Quaternion.identity));
                for (int distance = 1; distance <= 20; distance++)
                {
                    motion.SetRoutePoseTarget(
                        start + Vector3.forward * distance,
                        Quaternion.identity);
                }

                for (int distance = 1; distance <= 40; distance++)
                {
                    motion.SetRoutePoseTarget(
                        start + Vector3.forward * 20f +
                        Vector3.right * distance,
                        Quaternion.LookRotation(Vector3.right));
                }

                bool drifted = false;
                float maximumSlip = 0f;
                for (int frame = 0; frame < 240; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    drifted |= motion.IsDrifting;
                    maximumSlip = Mathf.Max(maximumSlip, motion.Drift01);
                    if (drifted && !motion.IsDrifting &&
                        vehicle.transform.position.x > start.x + 2f)
                    {
                        break;
                    }
                }

                Assert.That(drifted, Is.True,
                    "A donor-style high-speed corner never entered drift.");
                Assert.That(maximumSlip, Is.GreaterThan(0.1f));
                Assert.That(maximumSlip, Is.LessThanOrEqualTo(1f));
            }
            finally
            {
                Object.Destroy(ground);
                Object.Destroy(vehicle);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator StoryTrafficPerajarviHandbrakeZone_TriggersAuthoredSlide()
        {
            int worldSurfaceLayer = LayerMask.NameToLayer("WorldSurface");
            Assert.That(worldSurfaceLayer, Is.GreaterThanOrEqualTo(0));

            var ground = new GameObject("StoryTraffic_HandbrakeZoneGround");
            var vehicle = new GameObject("StoryTraffic_HandbrakeZoneVehicle");
            try
            {
                Vector3 start = new Vector3(8400f, 0f, 8400f);
                ground.layer = worldSurfaceLayer;
                ground.transform.position = start + new Vector3(0f, -0.5f, 20f);
                BoxCollider groundCollider = ground.AddComponent<BoxCollider>();
                groundCollider.size = new Vector3(60f, 1f, 80f);

                Transform[] wheels = Enumerable.Range(0, 4)
                    .Select(index =>
                    {
                        var wheel = new GameObject("HandbrakeWheel_" + index);
                        wheel.transform.SetParent(vehicle.transform, false);
                        return wheel.transform;
                    })
                    .ToArray();
                StoryTrafficVehiclePresentationBinding motion =
                    vehicle.AddComponent<StoryTrafficVehiclePresentationBinding>();
                motion.ConfigureForAuthoring(
                    "P1.NPC.049",
                    Array.Empty<string>(),
                    Array.Empty<GameObject>(),
                    wheels,
                    configuredWheelDegreesPerMeter: 190f,
                    configuredGroundContactCalibrationMeters: 0.35f);
                motion.SetRoadBehaviorProfile(
                    StoryTrafficRoadBehaviorProfile.Perajarvi,
                    insideDonorHandbrakeZone: true);
                Physics.SyncTransforms();
                motion.SetRoutePoseTarget(start, Quaternion.identity);
                motion.RestoreRuntimeState(new StoryTrafficMotionRuntimeState(
                    speedMetersPerSecond: 12f,
                    cruiseSpeedMetersPerSecond: 18f,
                    laneOffsetMeters: 0f,
                    maneuverState: StoryTrafficManeuverState.Cruise,
                    maneuverStateSeconds: 0f,
                    driftSlipDegrees: 0f,
                    recoveryCount: 0,
                    hasSafePose: true,
                    safePosition: start,
                    safeRotation: Quaternion.identity));
                motion.SetPhysicalRouteGuidanceTarget(
                    start + Vector3.forward * 24f,
                    Quaternion.identity,
                    physicalRouteProgress01: 0.1f);

                bool enteredDrift = false;
                float physicalHandbrakeSeconds = 0f;
                bool releasedHandbrake = false;
                for (int frame = 0; frame < 90; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    enteredDrift |= motion.IsDrifting;
                    if (motion.HandbrakeActive)
                    {
                        physicalHandbrakeSeconds += Time.fixedDeltaTime;
                    }
                    else if (enteredDrift)
                    {
                        releasedHandbrake = true;
                    }
                }

                Assert.That(enteredDrift, Is.True,
                    "An authored donor HandbrakeZone must request its slide even when the chassis enters aligned with the route.");
                Assert.That(
                    physicalHandbrakeSeconds,
                    Is.InRange(0.8f, 0.94f),
                    "The authored slide must keep the physical rear-grip release active for the bounded 0.88 s pulse, not only post skid audio.");
                Assert.That(releasedHandbrake, Is.True,
                    "The physical handbrake pulse must release after the authored slide window.");
            }
            finally
            {
                Object.Destroy(ground);
                Object.Destroy(vehicle);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator StoryTrafficAudio_StreamingDoesNotReplayStarterAndRpmTracksSpeed()
        {
            int worldSurfaceLayer = LayerMask.NameToLayer("WorldSurface");
            Assert.That(worldSurfaceLayer, Is.GreaterThanOrEqualTo(0));

            var ground = new GameObject("StoryTraffic_AudioGround");
            var vehicle = new GameObject("StoryTraffic_AudioVehicle");
            var backendObject = new GameObject("StoryTraffic_AudioBackend");
            var audioOwner = new GameObject("StoryTraffic_AudioOwner");
            try
            {
                Vector3 start = new Vector3(8100f, 0f, 8100f);
                ground.layer = worldSurfaceLayer;
                ground.transform.position = start + Vector3.down * 0.5f;
                BoxCollider groundCollider = ground.AddComponent<BoxCollider>();
                groundCollider.size = new Vector3(20f, 1f, 80f);

                Transform[] wheels = Enumerable.Range(0, 4)
                    .Select(index =>
                    {
                        var wheel = new GameObject("AudioWheel_" + index);
                        wheel.transform.SetParent(vehicle.transform, false);
                        return wheel.transform;
                    })
                    .ToArray();
                StoryTrafficVehiclePresentationBinding motion =
                    vehicle.AddComponent<
                        StoryTrafficVehiclePresentationBinding>();
                motion.ConfigureForAuthoring(
                    "P1.NPC.049",
                    Array.Empty<string>(),
                    Array.Empty<GameObject>(),
                    wheels,
                    configuredWheelDegreesPerMeter: 190f,
                    configuredGroundContactCalibrationMeters: 0.35f);
                motion.ConfigureDrivingProfile(
                    configuredMinimumCruiseSpeedMetersPerSecond: 115f / 3.6f,
                    configuredMaximumSpeedMetersPerSecond: 185f / 3.6f,
                    configuredAccelerationMetersPerSecond2: 6.5f,
                    configuredBrakingMetersPerSecond2: 20f,
                    configuredTurnRateDegreesPerSecond: 150f,
                    configuredPassingLaneOffsetMeters: -3.2f);
                var backend = backendObject.AddComponent<
                    RecordingAudioBackend>();
                StoryTrafficVehicleAudioPresenter presenter =
                    audioOwner.AddComponent<StoryTrafficVehicleAudioPresenter>();

                Physics.SyncTransforms();
                motion.SetRoutePoseTarget(start, Quaternion.identity);
                presenter.ConfigurePersistent(
                    backend,
                    "character.jani",
                    configuredListener: null);
                presenter.BindMotion(motion);
                presenter.SetDrivingActive(true);
                yield return null;

                Assert.That(presenter.IsInitialized, Is.True);
                Assert.That(
                    backend.PostedEvents.Any(eventId => eventId.Equals(
                        AudioProjectIds.Events.VehicleEngineStarted)),
                    Is.False,
                    "Streaming materialization replayed the starter one-shot.");
                Assert.That(
                    backend.PostedEvents.Count(eventId => eventId ==
                        new AudioEventId(
                            "audio.event.traffic.jani.engine.loop")),
                    Is.EqualTo(1));
                Assert.That(
                    backend.PostedEvents.Count(eventId => eventId ==
                        new AudioEventId("audio.event.traffic.jani.music")),
                    Is.EqualTo(1));
                float idleRpm = presenter.SimulatedEngineRpm;

                motion.SetRoutePoseTarget(
                    start + Vector3.forward * 35f,
                    Quaternion.identity);
                for (int frame = 0; frame < 120; frame++)
                {
                    yield return new WaitForFixedUpdate();
                    yield return null;
                }

                Assert.That(presenter.SimulatedGear, Is.GreaterThan(1));
                Assert.That(
                    presenter.SimulatedEngineRpm,
                    Is.GreaterThan(idleRpm + 300f),
                    "Traffic engine RPM stayed flat while the car accelerated.");
                Assert.That(
                    backend.ParameterWrites.Any(write =>
                        write.ParameterId ==
                            AudioProjectIds.Parameters.VehicleRpm &&
                        ReferenceEquals(write.Emitter, presenter.Emitter) &&
                        write.Value > idleRpm + 300f),
                    Is.True,
                    "Traffic RPM was not written to its own emitter.");
                Assert.That(
                    backend.ParameterWrites.Any(write =>
                        write.ParameterId ==
                            AudioProjectIds.Parameters.VehicleEngineLoad &&
                        ReferenceEquals(write.Emitter, presenter.Emitter) &&
                        write.Value > 0f),
                    Is.True,
                    "Traffic engine load was not written to its own emitter.");
                Assert.That(
                    backend.ParameterWrites.Any(write =>
                        write.ParameterId ==
                            AudioProjectIds.Parameters.VehicleSpeed &&
                        ReferenceEquals(write.Emitter, presenter.Emitter) &&
                        write.Value > 0f),
                    Is.True,
                    "Traffic speed was not written to its own emitter.");
                Assert.That(
                    backend.PostedEvents.Any(eventId => eventId.Equals(
                        AudioProjectIds.Events.VehicleEngineStarted)),
                    Is.False);

                Vector3 logicalPose = vehicle.transform.position;
                Quaternion logicalRotation = vehicle.transform.rotation;
                presenter.UnbindMotion(motion);
                presenter.SetLogicalPose(
                    logicalPose,
                    logicalRotation,
                    activeDriving: true);
                Object.Destroy(vehicle);
                yield return null;

                var replacementVehicle = new GameObject(
                    "StoryTraffic_AudioReplacementVehicle");
                Transform[] replacementWheels = Enumerable.Range(0, 4)
                    .Select(index =>
                    {
                        var wheel = new GameObject(
                            "ReplacementAudioWheel_" + index);
                        wheel.transform.SetParent(
                            replacementVehicle.transform,
                            false);
                        return wheel.transform;
                    })
                    .ToArray();
                StoryTrafficVehiclePresentationBinding replacementMotion =
                    replacementVehicle.AddComponent<
                        StoryTrafficVehiclePresentationBinding>();
                replacementMotion.ConfigureForAuthoring(
                    "P1.NPC.049",
                    Array.Empty<string>(),
                    Array.Empty<GameObject>(),
                    replacementWheels,
                    configuredWheelDegreesPerMeter: 190f,
                    configuredGroundContactCalibrationMeters: 0.35f);
                replacementMotion.SetRoutePoseTarget(
                    logicalPose,
                    logicalRotation);
                presenter.BindMotion(replacementMotion);
                yield return null;
                Assert.That(
                    backend.PostedEvents.Any(eventId => eventId.Equals(
                        AudioProjectIds.Events.VehicleEngineStarted)),
                    Is.False,
                    "Streaming disable/enable replayed the starter one-shot.");
                Assert.That(
                    backend.PostedEvents.Count(eventId => eventId ==
                        new AudioEventId(
                            "audio.event.traffic.jani.engine.loop")),
                    Is.EqualTo(1),
                    "Streaming rematerialization restarted the engine loop.");
                Assert.That(
                    backend.PostedEvents.Count(eventId => eventId ==
                        new AudioEventId("audio.event.traffic.jani.music")),
                    Is.EqualTo(1),
                    "Streaming rematerialization changed/restarted Jani music.");

                presenter.SetTerminallyDisabled(true);
                Assert.That(presenter.IsTerminallyDisabled, Is.True);
                Assert.That(presenter.IsDrivingActive, Is.False);
                Assert.That(presenter.IsInitialized, Is.False);
                Assert.That(
                    backend.PostedEvents.Any(eventId => eventId.Equals(
                        AudioProjectIds.Events.VehicleReset)),
                    Is.False,
                    "Streaming/terminal teardown posted the fallback reset " +
                    "one-shot, which is currently a start-like diagnostic clip.");
                int postedAtTerminalDisable = backend.PostedEvents.Count;
                presenter.SetLogicalPose(
                    logicalPose,
                    logicalRotation,
                    activeDriving: true);
                presenter.BindMotion(replacementMotion);
                yield return null;
                Assert.That(presenter.IsDrivingActive, Is.False,
                    "A logical driving refresh restarted a terminal wreck.");
                Assert.That(presenter.IsInitialized, Is.False);
                Assert.That(
                    backend.PostedEvents.Count,
                    Is.EqualTo(postedAtTerminalDisable),
                    "A terminal wreck reposted engine or music audio.");

                Object.Destroy(replacementVehicle);
            }
            finally
            {
                Object.Destroy(ground);
                Object.Destroy(vehicle);
                Object.Destroy(backendObject);
                Object.Destroy(audioOwner);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator StoryTrafficAudio_OrdinaryImpactPostsOneSpatialOneShot()
        {
            var vehicle = new GameObject("StoryTraffic_ImpactAudioVehicle");
            var obstacle = new GameObject("StoryTraffic_ImpactAudioObstacle");
            var backendObject = new GameObject("StoryTraffic_ImpactAudioBackend");
            var audioOwner = new GameObject("StoryTraffic_ImpactAudioOwner");
            try
            {
                obstacle.transform.position = Vector3.right * 10f;
                StoryTrafficVehiclePresentationBinding motion =
                    vehicle.AddComponent<
                        StoryTrafficVehiclePresentationBinding>();
                BoxCollider obstacleCollider = obstacle.AddComponent<BoxCollider>();
                var backend = backendObject.AddComponent<RecordingAudioBackend>();
                StoryTrafficVehicleAudioPresenter presenter =
                    audioOwner.AddComponent<StoryTrafficVehicleAudioPresenter>();
                presenter.ConfigurePersistent(
                    backend,
                    "character.jani",
                    configuredListener: null);
                presenter.BindMotion(motion);
                presenter.SetDrivingActive(true);
                yield return null;

                Assert.That(
                    motion.ReportImpact(
                        obstacleCollider,
                        vehicle.transform.position + Vector3.forward,
                        impactSpeedMetersPerSecond: 2f),
                    Is.False,
                    "A 2 m/s ordinary contact must remain non-terminal.");
                yield return null;

                AudioEventId impactEvent = new AudioEventId(
                    "audio.event.traffic.jani.crash");
                AudioEventRequest[] impactRequests = backend.PostedRequests
                    .Where(request => request.EventId == impactEvent)
                    .ToArray();
                Assert.That(impactRequests, Has.Length.EqualTo(1));
                Assert.That(
                    impactRequests[0].Emitter,
                    Is.SameAs(presenter.Emitter),
                    "Traffic impact lost its registered 3D owner.");
                Assert.That(impactRequests[0].AllowMultiple, Is.True);
                Assert.That(
                    impactRequests[0].Volume01,
                    Is.EqualTo(0.4f).Within(0.001f));
                Assert.That(
                    backend.ParameterWrites.Any(write =>
                        write.ParameterId ==
                            AudioProjectIds.Parameters.VehicleSuspensionImpact &&
                        ReferenceEquals(write.Emitter, presenter.Emitter) &&
                        Mathf.Abs(write.Value - 0.1f) <= 0.001f),
                    Is.True,
                    "Impact strength was not scoped to the traffic emitter.");

                // The physical publisher owns the 0.35 s contact cooldown, so
                // a duplicate callback from the same contact manifold cannot
                // layer another one-shot in the same frame.
                motion.ReportImpact(
                    obstacleCollider,
                    vehicle.transform.position + Vector3.forward,
                    impactSpeedMetersPerSecond: 9f);
                yield return null;
                Assert.That(
                    backend.PostedRequests.Count(request =>
                        request.EventId == impactEvent),
                    Is.EqualTo(1));

                presenter.SetDrivingActive(false);
                presenter.SetDrivingActive(true);
                yield return null;
                Assert.That(
                    backend.PostedEvents.Any(eventId => eventId.Equals(
                        AudioProjectIds.Events.VehicleEngineStarted) ||
                        eventId.Equals(AudioProjectIds.Events.VehicleReset)),
                    Is.False,
                    "Residency pause/resume emitted a starter/reset chirp.");
            }
            finally
            {
                Object.Destroy(vehicle);
                Object.Destroy(obstacle);
                Object.Destroy(backendObject);
                Object.Destroy(audioOwner);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator GeneratedPhase1Clips_AdvanceVisibleSkeletonsOverFrames()
        {
            CharacterPresentationCatalog catalog =
                Resources.Load<CharacterPresentationCatalog>(
                    "Phase1Characters/CharacterPresentationCatalog");
            if (catalog == null)
            {
                Assert.Ignore(
                    "Private ignored Phase 1 character presentation has not been generated locally.");
            }

            yield return AssertClipAdvances(
                catalog,
                "presentation.character.fixture.stationary-service",
                CharacterActivityState.Working);
            yield return AssertClipAdvances(
                catalog,
                "presentation.character.teimo-bicycle",
                CharacterActivityState.Talking);
            yield return AssertClipAdvances(
                catalog,
                "presentation.character.fixture.scheduled-roaming",
                CharacterActivityState.Walking);
            yield return AssertClipAdvances(
                catalog,
                "presentation.character.fixture.vehicle-linked",
                CharacterActivityState.Walking);
            yield return AssertClipAdvances(
                catalog,
                "presentation.character.fleetari",
                CharacterActivityState.Working);
            yield return AssertClipAdvances(
                catalog,
                "presentation.character.farmer",
                CharacterActivityState.Walking);
            yield return AssertClipAdvances(
                catalog,
                "presentation.character.berryman",
                CharacterActivityState.Working);
        }

        [UnityTest]
        public IEnumerator GeneratedTeimoBicycle_TravelRotatesWheelsAndPedals()
        {
            CharacterPresentationCatalog catalog =
                Resources.Load<CharacterPresentationCatalog>(
                    "Phase1Characters/CharacterPresentationCatalog");
            if (catalog == null)
            {
                Assert.Ignore(
                    "Private ignored Phase 1 character presentation has not been generated locally.");
            }

            Assert.That(
                catalog.TryGet(
                    "presentation.character.teimo-bicycle",
                    out CharacterPresentationCatalogEntry entry),
                Is.True);
            GameObject instance = Object.Instantiate(entry.WrapperPrefab);
            try
            {
                LegacyCharacterPresentationBinding character =
                    instance.GetComponent<
                        LegacyCharacterPresentationBinding>();
                TeimoBicyclePresentationBinding bicycle =
                    instance.GetComponent<
                        TeimoBicyclePresentationBinding>();
                Assert.That(character.ApplyState(
                    CharacterActivityState.VehicleSeated), Is.False);
                Assert.That(bicycle, Is.Not.Null);

                yield return null;
                Quaternion frontBefore = bicycle.FrontWheel.localRotation;
                Quaternion rearBefore = bicycle.RearWheel.localRotation;
                Quaternion pedalsBefore = bicycle.Pedals.localRotation;
                Quaternion leftHipBefore = bicycle.LeftHip.rotation;
                Quaternion rightHipBefore = bicycle.RightHip.rotation;
                instance.transform.position += Vector3.forward * 0.1f;
                yield return null;

                Assert.That(
                    Quaternion.Angle(
                        frontBefore,
                        bicycle.FrontWheel.localRotation),
                    Is.EqualTo(17f).Within(0.05f));
                Assert.That(
                    Quaternion.Angle(
                        rearBefore,
                        bicycle.RearWheel.localRotation),
                    Is.EqualTo(17f).Within(0.05f));
                Assert.That(
                    Quaternion.Angle(
                        pedalsBefore,
                        bicycle.Pedals.localRotation),
                    Is.EqualTo(17f / 3.5f).Within(0.05f));
                Assert.That(
                    Quaternion.Angle(leftHipBefore, bicycle.LeftHip.rotation) +
                    Quaternion.Angle(rightHipBefore, bicycle.RightHip.rotation),
                    Is.GreaterThan(0.05f),
                    "Pedal rotation did not propagate to Teimo's legs.");
            }
            finally
            {
                Object.Destroy(instance);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator GeneratedTeimo_RootMotionClipsKeepVisualOnLogicalRoot()
        {
            CharacterPresentationCatalog catalog =
                Resources.Load<CharacterPresentationCatalog>(
                    "Phase1Characters/CharacterPresentationCatalog");
            if (catalog == null)
            {
                Assert.Ignore(
                    "Private ignored Phase 1 character presentation has not been generated locally.");
            }

            Assert.That(
                catalog.TryGet(
                    "presentation.character.fixture.stationary-service",
                    out CharacterPresentationCatalogEntry entry),
                Is.True);
            GameObject instance = Object.Instantiate(entry.WrapperPrefab);
            try
            {
                LegacyCharacterPresentationBinding binding =
                    instance.GetComponent<LegacyCharacterPresentationBinding>();
                Assert.That(binding, Is.Not.Null);
                Transform animationRoot = binding.LegacyAnimation.transform;
                Assert.That(animationRoot, Is.Not.SameAs(instance.transform));
                Vector3 authoredLocalPosition = animationRoot.localPosition;
                Quaternion authoredLocalRotation = animationRoot.localRotation;
                Vector3 authoredLocalScale = animationRoot.localScale;
                Vector3 logicalWorldPosition = instance.transform.position;
                Quaternion logicalWorldRotation = instance.transform.rotation;

                Assert.That(
                    binding.ApplyState(CharacterActivityState.Walking),
                    Is.True);
                for (int frame = 0; frame < 20; frame++)
                {
                    yield return null;
                }

                AssertPresentationRootPinned(
                    instance.transform,
                    animationRoot,
                    logicalWorldPosition,
                    logicalWorldRotation,
                    authoredLocalPosition,
                    authoredLocalRotation,
                    authoredLocalScale);

                Assert.That(
                    binding.TryPlayAction(
                        "action.character.teimo.move-kitchen-in",
                        resumeStateAfter: false),
                    Is.True);
                for (int frame = 0; frame < 20; frame++)
                {
                    yield return null;
                }

                AssertPresentationRootPinned(
                    instance.transform,
                    animationRoot,
                    logicalWorldPosition,
                    logicalWorldRotation,
                    authoredLocalPosition,
                    authoredLocalRotation,
                    authoredLocalScale);
            }
            finally
            {
                Object.Destroy(instance);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator GeneratedTeimo_ServiceWalkSurvivesWorkingTickAndCheckoutHoldsLeanEnd()
        {
            CharacterPresentationCatalog catalog =
                Resources.Load<CharacterPresentationCatalog>(
                    "Phase1Characters/CharacterPresentationCatalog");
            if (catalog == null)
            {
                Assert.Ignore(
                    "Private ignored Phase 1 character presentation has not been generated locally.");
            }

            Assert.That(
                catalog.TryGet(
                    "presentation.character.fixture.stationary-service",
                    out CharacterPresentationCatalogEntry entry),
                Is.True);
            GameObject instance = Object.Instantiate(entry.WrapperPrefab);
            try
            {
                LegacyCharacterPresentationBinding binding =
                    instance.GetComponent<LegacyCharacterPresentationBinding>();
                Assert.That(binding.ApplyState(CharacterActivityState.Working),
                    Is.True);
                Assert.That(
                    binding.TryPlayAction(
                        "action.character.teimo.service-walk",
                        resumeStateAfter: false),
                    Is.True);
                Transform leftThigh = instance
                    .GetComponentsInChildren<Transform>(true)
                    .First(value => value.name == "thig_left");
                Quaternion initialThighRotation = leftThigh.localRotation;
                float maximumLegMotion = 0f;
                for (int frame = 0; frame < 20; frame++)
                {
                    yield return null;
                    maximumLegMotion = Mathf.Max(
                        maximumLegMotion,
                        Quaternion.Angle(
                            initialThighRotation,
                            leftThigh.localRotation));
                }

                Assert.That(maximumLegMotion, Is.GreaterThan(0.5f),
                    "The service-walk action did not animate Teimo's legs.");
                Assert.That(binding.ApplyState(CharacterActivityState.Working),
                    Is.True);
                Assert.That(
                    binding.ActiveActionId,
                    Is.EqualTo("action.character.teimo.service-walk"),
                    "An unchanged authoritative Working tick replaced the presentation-only kitchen walk.");

                Assert.That(
                    binding.TryPlayAction(
                        "action.character.teimo.cash-register",
                        resumeStateAfter: true,
                        restartStateAnimationAfter: false),
                    Is.True);
                AnimationState checkout = binding.LegacyAnimation[
                    "teimo_cash_register"];
                float checkoutDeadline = Time.time + checkout.length + 1f;
                while (!string.IsNullOrEmpty(binding.ActiveActionId) &&
                       Time.time < checkoutDeadline)
                {
                    yield return null;
                }

                Assert.That(binding.ActiveActionId, Is.Empty);
                AnimationState heldLean = binding.LegacyAnimation
                    .Cast<AnimationState>()
                    .Single(state => state.enabled);
                Assert.That(heldLean.clip.name, Is.EqualTo("teimo_lean_table_in"));
                Assert.That(heldLean.speed, Is.Zero.Within(0.0001f));
                Assert.That(
                    heldLean.time,
                    Is.EqualTo(heldLean.length).Within(0.02f),
                    "Checkout restarted lean-in instead of holding the already established counter pose.");
            }
            finally
            {
                Object.Destroy(instance);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator GeneratedTeimo_UsesExactDonorServiceClipsAndTimings()
        {
            CharacterPresentationCatalog catalog =
                Resources.Load<CharacterPresentationCatalog>(
                    "Phase1Characters/CharacterPresentationCatalog");
            if (catalog == null)
            {
                Assert.Ignore(
                    "Private ignored Phase 1 character presentation has not been generated locally.");
            }

            Assert.That(
                catalog.TryGet(
                    "presentation.character.fixture.stationary-service",
                    out CharacterPresentationCatalogEntry entry),
                Is.True);
            GameObject teimoObject = Object.Instantiate(entry.WrapperPrefab);
            try
            {
                LegacyCharacterPresentationBinding binding =
                    teimoObject.GetComponent<
                        LegacyCharacterPresentationBinding>();
                AssertActionClip(binding,
                    "action.character.teimo.give-drink", 6f);
                AssertActionClip(binding,
                    "action.character.teimo.lean-table-out", 0.5f);
                AssertActionClip(binding,
                    "action.character.teimo.move-kitchen-in", 4f);
                AssertActionClip(binding,
                    "action.character.teimo.move-kitchen-out", 4f);
                AssertActionClip(binding,
                    "action.character.teimo.cook", 10f);
                AssertActionClip(binding,
                    "action.character.teimo.cook2", 5f);
                AssertActionClip(binding,
                    "action.character.teimo.microwave-door", 2f);
                AssertRootMotion(
                    binding,
                    "action.character.teimo.move-kitchen-in",
                    new Vector3(-6.5f, 0f, 0f),
                    new Vector3(-3.8f, 0f, 0.1f));
                AssertRootMotion(
                    binding,
                    "action.character.teimo.move-kitchen-out",
                    new Vector3(-3.8f, 0f, 0.1f),
                    new Vector3(-6.5f, 0f, 0f));

                AssertPrivateFloat(
                    "CounterPropRevealSeconds",
                    2.3f);
                AssertPrivateFloat(
                    "CounterPropReleaseSeconds",
                    5.3f);
                AssertPrivateFloat(
                    "FoodCounterReleaseSeconds",
                    0.4f);
                AssertPrivateFloat(
                    "MicrowaveSoundSequenceRemainderSeconds",
                    5.937641f);
            }
            finally
            {
                Object.Destroy(teimoObject);
            }

            yield return null;
        }

        private static void AssertActionClip(
            LegacyCharacterPresentationBinding binding,
            string actionId,
            float expectedDuration)
        {
            Assert.That(binding.TryGetActionClip(actionId, out AnimationClip clip),
                Is.True,
                actionId);
            Assert.That(clip.length,
                Is.EqualTo(expectedDuration).Within(0.02f),
                actionId);
        }

        private static void AssertPrivateFloat(string fieldName, float expected)
        {
            FieldInfo field = typeof(TeimoServicePresentationDirector).GetField(
                fieldName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            Assert.That((float)field.GetRawConstantValue(),
                Is.EqualTo(expected).Within(0.0001f),
                fieldName);
        }

        private static void AssertRootMotion(
            LegacyCharacterPresentationBinding binding,
            string actionId,
            Vector3 expectedStart,
            Vector3 expectedEnd)
        {
            Assert.That(binding.TryGetActionClip(actionId, out AnimationClip clip),
                Is.True,
                actionId);
            var sampler = new GameObject("Donor root-motion assertion");
            try
            {
                clip.SampleAnimation(sampler, 0f);
                Assert.That(Vector3.Distance(
                        sampler.transform.localPosition,
                        expectedStart),
                    Is.LessThan(0.002f),
                    actionId + " start");
                clip.SampleAnimation(sampler, clip.length);
                Assert.That(Vector3.Distance(
                        sampler.transform.localPosition,
                        expectedEnd),
                    Is.LessThan(0.002f),
                    actionId + " end");
            }
            finally
            {
                Object.DestroyImmediate(sampler);
            }
        }

        private static void AssertPresentationRootPinned(
            Transform logicalRoot,
            Transform animationRoot,
            Vector3 logicalWorldPosition,
            Quaternion logicalWorldRotation,
            Vector3 authoredLocalPosition,
            Quaternion authoredLocalRotation,
            Vector3 authoredLocalScale)
        {
            Assert.That(
                Vector3.Distance(logicalRoot.position, logicalWorldPosition),
                Is.LessThan(0.0001f));
            Assert.That(
                Quaternion.Angle(logicalRoot.rotation, logicalWorldRotation),
                Is.LessThan(0.01f));
            Assert.That(
                Vector3.Distance(animationRoot.localPosition, authoredLocalPosition),
                Is.LessThan(0.0001f));
            Assert.That(
                Quaternion.Angle(
                    animationRoot.localRotation,
                    authoredLocalRotation),
                Is.LessThan(0.01f));
            Assert.That(
                Vector3.Distance(animationRoot.localScale, authoredLocalScale),
                Is.LessThan(0.0001f));
        }

        [UnityTest]
        public IEnumerator GeneratedSewageClient_DrinkingLayerMovesAndRestarts()
        {
            CharacterPresentationCatalog catalog =
                Resources.Load<CharacterPresentationCatalog>(
                    "Phase1Characters/CharacterPresentationCatalog");
            if (catalog == null)
            {
                Assert.Ignore(
                    "Private ignored Phase 1 character presentation has not been generated locally.");
            }

            Assert.That(
                catalog.TryGet(
                    "presentation.character.sewage-client-1",
                    out CharacterPresentationCatalogEntry entry),
                Is.True);
            GameObject instance = Object.Instantiate(entry.WrapperPrefab);
            try
            {
                LegacyLoopingAnimationLayer layer = instance
                    .GetComponentInChildren<LegacyLoopingAnimationLayer>(true);
                Assert.That(layer, Is.Not.Null);
                Assert.That(layer.TryValidate(out string failure),
                    Is.True,
                    failure);
                yield return null;
                Assert.That(
                    layer.LegacyAnimation.IsPlaying(layer.Clip.name),
                    Is.True,
                    "The drinking layer did not start when the NPC materialized.");

                Transform[] animatedBones = layer.transform
                    .GetComponentsInChildren<Transform>(true);
                Quaternion[] initial = animatedBones
                    .Select(bone => bone.localRotation)
                    .ToArray();
                AnimationState state = layer.LegacyAnimation[layer.Clip.name];
                float maximumAngle = 0f;
                for (int sample = 1; sample <= 4; sample++)
                {
                    state.time = layer.Clip.length * sample / 5f;
                    layer.LegacyAnimation.Sample();
                    for (int index = 0; index < animatedBones.Length; index++)
                    {
                        maximumAngle = Mathf.Max(
                            maximumAngle,
                            Quaternion.Angle(
                                initial[index],
                                animatedBones[index].localRotation));
                    }
                }

                Assert.That(maximumAngle, Is.GreaterThan(0.5f),
                    "The drinking layer never raises or lowers the held beer hand.");

                instance.SetActive(false);
                yield return null;
                instance.SetActive(true);
                yield return null;
                Assert.That(
                    layer.LegacyAnimation.IsPlaying(layer.Clip.name),
                    Is.True,
                    "The drinking layer did not restart after a streaming-style disable/enable cycle.");
            }
            finally
            {
                Object.Destroy(instance);
            }

            yield return null;
        }

        private static IEnumerator AssertClipAdvances(
            CharacterPresentationCatalog catalog,
            string bindingId,
            CharacterActivityState activity)
        {
            Assert.That(catalog.TryGet(bindingId, out var entry),
                Is.True,
                bindingId);
            GameObject instance = Object.Instantiate(entry.WrapperPrefab);
            try
            {
                LegacyCharacterPresentationBinding binding =
                    instance.GetComponent<LegacyCharacterPresentationBinding>();
                Assert.That(binding, Is.Not.Null, bindingId);
                Assert.That(binding.TryValidate(out string failure),
                    Is.True,
                    failure);
                Assert.That(binding.ApplyState(activity), Is.True, bindingId);

                Animation animation = binding.LegacyAnimation;
                Transform[] bones = animation.transform
                    .GetComponentsInChildren<Transform>(true)
                    .ToArray();
                Assert.That(bones, Is.Not.Empty, bindingId);
                SkinnedMeshRenderer renderer =
                    binding.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Assert.That(renderer, Is.Not.Null, bindingId);
                Assert.That(
                    renderer.bones.Intersect(bones).Any(),
                    Is.True,
                    $"{bindingId} animation hierarchy has no renderer bone.");
                yield return null;

                Quaternion[] initialRotations = bones
                    .Select(bone => bone.localRotation)
                    .ToArray();
                bool moved = false;
                for (int frame = 0; frame < 60 && !moved; frame++)
                {
                    yield return null;
                    for (int index = 0; index < bones.Length; index++)
                    {
                        if (Quaternion.Angle(
                                initialRotations[index],
                                bones[index].localRotation) > 0.05f)
                        {
                            moved = true;
                            break;
                        }
                    }
                }

                Assert.That(animation.isPlaying, Is.True, bindingId);
                Assert.That(moved, Is.True,
                    $"{bindingId} remained a static mannequin for 60 rendered frames.");
            }
            finally
            {
                Object.Destroy(instance);
            }

            yield return null;
        }

        private sealed class RecordingAudioBackend : MonoBehaviour,
            IAudioBackend
        {
            private readonly HashSet<IAudioEmitter> emitters =
                new HashSet<IAudioEmitter>();

            public string BackendId => "audio.test.story_traffic";
            public AudioBackendKind Kind => AudioBackendKind.Unity;
            public bool IsReady => true;
            public string FailureReason => string.Empty;
            public List<AudioEventId> PostedEvents { get; } =
                new List<AudioEventId>();
            public List<AudioEventRequest> PostedRequests { get; } =
                new List<AudioEventRequest>();
            public List<ParameterWrite> ParameterWrites { get; } =
                new List<ParameterWrite>();

            public bool RegisterEmitter(
                IAudioEmitter audioEmitter,
                out string failure)
            {
                if (audioEmitter == null)
                {
                    failure = "missing";
                    return false;
                }

                emitters.Add(audioEmitter);
                failure = string.Empty;
                return true;
            }

            public bool UnregisterEmitter(IAudioEmitter audioEmitter) =>
                emitters.Remove(audioEmitter);

            public IAudioEventHandle PostEvent(in AudioEventRequest request)
            {
                PostedEvents.Add(request.EventId);
                PostedRequests.Add(request);
                return AudioEventHandles.Invalid;
            }

            public bool SetParameter(
                AudioParameterId parameterId,
                float value,
                IAudioEmitter audioEmitter = null)
            {
                ParameterWrites.Add(new ParameterWrite(
                    parameterId,
                    value,
                    audioEmitter));
                return true;
            }

            public bool SetSwitch(
                AudioSwitchId switchGroupId,
                AudioSwitchId switchValueId,
                IAudioEmitter audioEmitter = null) => true;

            public bool SetState(
                AudioStateId stateGroupId,
                AudioStateId stateValueId) => true;

            public void SetListenerContext(in AudioListenerContext context)
            {
            }

            public void ApplySettings(in AudioSettingsState settings)
            {
            }

            public void StopAll(float fadeSeconds = 0f)
            {
            }

            public AudioRuntimeSnapshot CaptureSnapshot() =>
                new AudioRuntimeSnapshot(
                    BackendId,
                    Kind,
                    true,
                    true,
                    emitters.Count,
                    0,
                    0,
                    Array.Empty<string>(),
                    default,
                    string.Empty);

            public readonly struct ParameterWrite
            {
                public ParameterWrite(
                    AudioParameterId parameterId,
                    float value,
                    IAudioEmitter emitter)
                {
                    ParameterId = parameterId;
                    Value = value;
                    Emitter = emitter;
                }

                public AudioParameterId ParameterId { get; }
                public float Value { get; }
                public IAudioEmitter Emitter { get; }
            }
        }

        private sealed class AlwaysLoadedNpcCellAvailability :
            INpcCellAvailability
        {
            public event System.Action<string, bool> AvailabilityChanged;

            public bool IsLoaded(string cellId) => true;

            public bool TryResolveCellId(
                Vector3 worldPosition,
                out string cellId)
            {
                cellId = "cell_-3_0";
                return true;
            }

            public void Dispose()
            {
                AvailabilityChanged = null;
            }
        }

    }
}
