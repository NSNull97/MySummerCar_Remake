using System.Collections;
using MSC.Characters;
using MSC.Vehicle.NWH;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehicleNwhIntegration
{
    public sealed class StoryTrafficNwhPlayModeTests
    {
        [UnityTest]
        public IEnumerator GeneratedJani_AcceleratesThroughNwhWithoutPoseDragging()
        {
            yield return VerifyGeneratedCarAccelerates(
                "presentation.character.jani-car",
                "Jani",
                minimumSpeedMetersPerSecond: 2f,
                minimumForwardDistanceMeters: 8f,
                maximumSteps: 600);
        }

        [UnityTest]
        public IEnumerator GeneratedPetteri_AcceleratesThroughNwhWithoutPoseDragging()
        {
            yield return VerifyGeneratedCarAccelerates(
                "presentation.character.petteri-car",
                "Petteri",
                minimumSpeedMetersPerSecond: 2f,
                minimumForwardDistanceMeters: 8f,
                maximumSteps: 600);
        }

        [UnityTest]
        public IEnumerator GeneratedPerajarviFormation_BothCarsLaunchWithoutDeadlock()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "NWH_StoryTraffic_FormationGround";
            SetWorldSurfaceLayer(ground);
            ground.transform.SetPositionAndRotation(
                new Vector3(40f, -0.5f, -50f),
                Quaternion.identity);
            ground.transform.localScale = new Vector3(300f, 1f, 300f);

            CharacterPresentationCatalog catalog =
                Resources.Load<CharacterPresentationCatalog>(
                    "Phase1Characters/CharacterPresentationCatalog");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryGet(
                "presentation.character.jani-car",
                out CharacterPresentationCatalogEntry janiEntry), Is.True);
            Assert.That(catalog.TryGet(
                "presentation.character.petteri-car",
                out CharacterPresentationCatalogEntry petteriEntry), Is.True);

            GameObject jani = Object.Instantiate(janiEntry.WrapperPrefab);
            GameObject petteri = Object.Instantiate(petteriEntry.WrapperPrefab);
            Vector3 janiStart = Vector3.zero;
            Vector3 petteriStart = new Vector3(-3.0065f, 0f, 4.8629f);
            Vector3 janiDirection = new Vector3(
                2.957f,
                0f,
                -1.689942f).normalized;
            Vector3 petteriDirection = new Vector3(
                2.569f,
                0f,
                -3.92981f).normalized;
            Quaternion janiRotation = Quaternion.LookRotation(janiDirection);
            Quaternion petteriRotation = Quaternion.LookRotation(
                petteriDirection);
            jani.transform.SetPositionAndRotation(janiStart, janiRotation);
            petteri.transform.SetPositionAndRotation(
                petteriStart,
                petteriRotation);
            StoryTrafficVehiclePresentationBinding janiMotion =
                jani.GetComponent<StoryTrafficVehiclePresentationBinding>();
            StoryTrafficVehiclePresentationBinding petteriMotion =
                petteri.GetComponent<StoryTrafficVehiclePresentationBinding>();
            NwhStoryTrafficVehicleMotionBackend janiBackend =
                jani.GetComponent<NwhStoryTrafficVehicleMotionBackend>();
            NwhStoryTrafficVehicleMotionBackend petteriBackend =
                petteri.GetComponent<NwhStoryTrafficVehicleMotionBackend>();
            Assert.That(janiMotion, Is.Not.Null);
            Assert.That(petteriMotion, Is.Not.Null);
            Assert.That(janiBackend, Is.Not.Null);
            Assert.That(petteriBackend, Is.Not.Null);

            janiMotion.SetRoutePoseTarget(janiStart, janiRotation);
            petteriMotion.SetRoutePoseTarget(petteriStart, petteriRotation);
            yield return new WaitForFixedUpdate();
            bool bothMoving = false;
            for (int step = 0; step < 700; step++)
            {
                janiMotion.SetPhysicalRouteGuidanceTarget(
                    janiStart + janiDirection * 140f,
                    janiRotation,
                    step / 1400f);
                petteriMotion.SetPhysicalRouteGuidanceTarget(
                    petteriStart + petteriDirection * 140f,
                    petteriRotation,
                    step / 1400f);
                yield return new WaitForFixedUpdate();
                if (janiBackend.HasGroundContact &&
                    petteriBackend.HasGroundContact &&
                    janiBackend.SpeedMetersPerSecond > 2f &&
                    petteriBackend.SpeedMetersPerSecond > 2f &&
                    Vector3.Dot(
                        jani.transform.position - janiStart,
                        janiDirection) > 6f &&
                    Vector3.Dot(
                        petteri.transform.position - petteriStart,
                        petteriDirection) > 6f)
                {
                    bothMoving = true;
                    break;
                }
            }

            Debug.Log(
                $"NWH Perajarvi formation diagnostic: " +
                $"Jani={jani.transform.position}/{janiBackend.SpeedMetersPerSecond:F2}mps, " +
                $"Petteri={petteri.transform.position}/{petteriBackend.SpeedMetersPerSecond:F2}mps");
            Object.Destroy(jani);
            Object.Destroy(petteri);
            Object.Destroy(ground);
            yield return null;

            Assert.That(bothMoving, Is.True,
                "The exact two-car spacing deadlocked physical launch.");
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator GeneratedJani_AcceleratesOnRealPerajarviWorldSurface()
        {
            const string globalScenePath =
                "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/World_Global_Legacy.unity";
            const string perajarviScenePath =
                "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_-3_0_Legacy.unity";
            AsyncOperation globalLoad = SceneManager.LoadSceneAsync(
                globalScenePath,
                LoadSceneMode.Additive);
            Assert.That(globalLoad, Is.Not.Null);
            yield return globalLoad;
            AsyncOperation cellLoad = SceneManager.LoadSceneAsync(
                perajarviScenePath,
                LoadSceneMode.Additive);
            Assert.That(cellLoad, Is.Not.Null);
            yield return cellLoad;

            Vector3 start = new Vector3(
                -1173.1444f,
                3.38973f,
                123.312256f);
            Vector3 direction = new Vector3(
                2.957f,
                0f,
                -1.689942f).normalized;
            Quaternion rotation = Quaternion.LookRotation(direction);
            Physics.SyncTransforms();
            RaycastHit[] supportHits = Physics.RaycastAll(
                start + Vector3.up * 15f,
                Vector3.down,
                40f,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            string supportDiagnostic = string.Empty;
            for (int index = 0; index < supportHits.Length; index++)
            {
                RaycastHit hit = supportHits[index];
                supportDiagnostic +=
                    $" [{index}] {hit.collider.name}/" +
                    $"{LayerMask.LayerToName(hit.collider.gameObject.layer)} " +
                    $"{hit.collider.GetType().Name} y={hit.point.y:F3}";
            }

            CharacterPresentationCatalog catalog =
                Resources.Load<CharacterPresentationCatalog>(
                    "Phase1Characters/CharacterPresentationCatalog");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryGet(
                "presentation.character.jani-car",
                out CharacterPresentationCatalogEntry entry), Is.True);
            GameObject car = Object.Instantiate(entry.WrapperPrefab);
            car.name = "NWH_StoryTraffic_RealPerajarviJani";
            car.transform.SetPositionAndRotation(start, rotation);
            StoryTrafficVehiclePresentationBinding motion =
                car.GetComponent<StoryTrafficVehiclePresentationBinding>();
            NwhStoryTrafficVehicleMotionBackend backend =
                car.GetComponent<NwhStoryTrafficVehicleMotionBackend>();
            NwhWheelPhysicsBackend wheelBackend =
                car.GetComponent<NwhWheelPhysicsBackend>();
            Assert.That(motion, Is.Not.Null);
            Assert.That(backend, Is.Not.Null);
            Assert.That(wheelBackend, Is.Not.Null);

            motion.SetRoutePoseTarget(start, rotation);
            yield return new WaitForFixedUpdate();
            Vector3 launchPosition = car.transform.position;
            bool acceleratedWhileGrounded = false;
            for (int step = 0; step < 700; step++)
            {
                motion.SetPhysicalRouteGuidanceTarget(
                    start + direction * 140f,
                    rotation,
                    step / 1400f);
                yield return new WaitForFixedUpdate();
                if (backend.HasGroundContact &&
                    backend.SpeedMetersPerSecond > 2f &&
                    Vector3.Dot(
                        car.transform.position - launchPosition,
                        direction) > 6f)
                {
                    acceleratedWhileGrounded = true;
                    break;
                }
            }

            string wheelDiagnostic = string.Empty;
            for (int index = 0; index < wheelBackend.Wheels.Length; index++)
            {
                var wheel = wheelBackend.Wheels[index];
                Collider hit = wheel.HitCollider;
                wheelDiagnostic +=
                    $" [{index}] grounded={wheel.IsGrounded} " +
                    $"load={wheel.Load:F1} slip={wheel.LongitudinalSlip:F2} " +
                    $"omega={wheel.AngularVelocity:F2} " +
                    $"motor={wheel.MotorTorque:F1} brake={wheel.BrakeTorque:F1} " +
                    $"hit={(hit != null ? hit.name : "none")}/" +
                    $"{(hit != null ? LayerMask.LayerToName(hit.gameObject.layer) : "none")}";
            }
            Debug.Log(
                "NWH real Perajarvi support:" + supportDiagnostic +
                "\nNWH real Perajarvi car: " +
                $"position={car.transform.position}, " +
                $"speed={backend.SpeedMetersPerSecond:F3}, " +
                $"grounded={backend.HasGroundContact}, " +
                $"rpm={backend.EngineRpm:F0}, gear={backend.SelectedGear}, " +
                $"blocked={motion.IsObstacleBraking}; wheels:" +
                wheelDiagnostic);

            Assert.That(supportHits.Length, Is.GreaterThan(0),
                "No world collider exists below the locked Perajarvi formation.");
            Assert.That(acceleratedWhileGrounded, Is.True,
                "Jani did not transfer wheel torque into the real Perajarvi world surface.");

            Object.Destroy(car);
            yield return null;
            AsyncOperation cellUnload = SceneManager.UnloadSceneAsync(
                perajarviScenePath);
            if (cellUnload != null)
            {
                yield return cellUnload;
            }
            AsyncOperation globalUnload = SceneManager.UnloadSceneAsync(
                globalScenePath);
            if (globalUnload != null)
            {
                yield return globalUnload;
            }
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator GeneratedPerajarviFormation_BothCarsAccelerateOnRealWorldSurface()
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

            CharacterPresentationCatalog catalog =
                Resources.Load<CharacterPresentationCatalog>(
                    "Phase1Characters/CharacterPresentationCatalog");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryGet(
                "presentation.character.jani-car",
                out CharacterPresentationCatalogEntry janiEntry), Is.True);
            Assert.That(catalog.TryGet(
                "presentation.character.petteri-car",
                out CharacterPresentationCatalogEntry petteriEntry), Is.True);

            Vector3 janiStart = new Vector3(
                -1173.1444f,
                3.38973f,
                123.312256f);
            Vector3 petteriStart = new Vector3(
                -1176.1509f,
                3.464334f,
                128.17517f);
            Vector3 janiDirection = new Vector3(
                2.957f,
                0f,
                -1.689942f).normalized;
            Vector3 petteriDirection = new Vector3(
                2.569f,
                0f,
                -3.92981f).normalized;
            Quaternion janiRotation = Quaternion.LookRotation(janiDirection);
            Quaternion petteriRotation = Quaternion.LookRotation(
                petteriDirection);

            GameObject jani = Object.Instantiate(janiEntry.WrapperPrefab);
            GameObject petteri = Object.Instantiate(petteriEntry.WrapperPrefab);
            jani.name = "NWH_StoryTraffic_RealFormationJani";
            petteri.name = "NWH_StoryTraffic_RealFormationPetteri";
            jani.transform.SetPositionAndRotation(janiStart, janiRotation);
            petteri.transform.SetPositionAndRotation(
                petteriStart,
                petteriRotation);
            StoryTrafficVehiclePresentationBinding janiMotion =
                jani.GetComponent<StoryTrafficVehiclePresentationBinding>();
            StoryTrafficVehiclePresentationBinding petteriMotion =
                petteri.GetComponent<StoryTrafficVehiclePresentationBinding>();
            NwhStoryTrafficVehicleMotionBackend janiBackend =
                jani.GetComponent<NwhStoryTrafficVehicleMotionBackend>();
            NwhStoryTrafficVehicleMotionBackend petteriBackend =
                petteri.GetComponent<NwhStoryTrafficVehicleMotionBackend>();
            Assert.That(janiMotion, Is.Not.Null);
            Assert.That(petteriMotion, Is.Not.Null);
            Assert.That(janiBackend, Is.Not.Null);
            Assert.That(petteriBackend, Is.Not.Null);

            janiMotion.SetRoutePoseTarget(janiStart, janiRotation);
            petteriMotion.SetRoutePoseTarget(petteriStart, petteriRotation);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            bool bothMoved = false;
            for (int step = 0; step < 350; step++)
            {
                janiMotion.SetPhysicalRouteGuidanceTarget(
                    janiStart + janiDirection * 140f,
                    janiRotation,
                    step / 1400f);
                petteriMotion.SetPhysicalRouteGuidanceTarget(
                    petteriStart + petteriDirection * 140f,
                    petteriRotation,
                    step / 1400f);
                yield return new WaitForFixedUpdate();
                if (janiBackend.HasGroundContact &&
                    petteriBackend.HasGroundContact &&
                    janiBackend.SpeedMetersPerSecond > 2f &&
                    petteriBackend.SpeedMetersPerSecond > 2f &&
                    Vector3.Distance(janiStart, jani.transform.position) > 3f &&
                    Vector3.Distance(petteriStart, petteri.transform.position) > 3f)
                {
                    bothMoved = true;
                    break;
                }
            }

            Debug.Log(
                "NWH real Perajarvi formation diagnostic: " +
                $"Jani={jani.transform.position}/" +
                $"{janiBackend.SpeedMetersPerSecond:F2}mps/" +
                $"{janiMotion.ManeuverState}/" +
                $"blocked={janiMotion.IsObstacleBraking}; " +
                $"Petteri={petteri.transform.position}/" +
                $"{petteriBackend.SpeedMetersPerSecond:F2}mps/" +
                $"{petteriMotion.ManeuverState}/" +
                $"blocked={petteriMotion.IsObstacleBraking}");

            Object.Destroy(jani);
            Object.Destroy(petteri);
            yield return null;
            yield return SceneManager.UnloadSceneAsync(perajarviScenePath);
            yield return SceneManager.UnloadSceneAsync(globalScenePath);

            Assert.That(bothMoved, Is.True,
                "The exact production formation deadlocked on the real Perajarvi surface.");
        }

        [UnityTest]
        public IEnumerator GeneratedJani_ShiftsAndSustainsDonorHighwaySpeed()
        {
            yield return VerifyGeneratedCarAccelerates(
                "presentation.character.jani-car",
                "JaniHighway",
                minimumSpeedMetersPerSecond: 25f,
                minimumForwardDistanceMeters: 75f,
                maximumSteps: 1000);
        }

        [UnityTest]
        public IEnumerator GeneratedJani_RejoinsStraightRouteThroughPhysicalGuidance()
        {
            const float initialLateralOffsetMeters = 10f;
            const float acceptedRouteCorridorMeters = 4f;
            const float minimumForwardProgressMeters = 18f;
            const float maximumPhysicalStepMeters = 3f;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "NWH_StoryTraffic_RejoinGround";
            SetWorldSurfaceLayer(ground);
            ground.transform.SetPositionAndRotation(
                new Vector3(0f, -0.5f, 100f),
                Quaternion.identity);
            ground.transform.localScale = new Vector3(100f, 1f, 500f);

            CharacterPresentationCatalog catalog =
                Resources.Load<CharacterPresentationCatalog>(
                    "Phase1Characters/CharacterPresentationCatalog");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(
                catalog.TryGet(
                    "presentation.character.jani-car",
                    out CharacterPresentationCatalogEntry entry),
                Is.True);

            GameObject car = Object.Instantiate(entry.WrapperPrefab);
            car.name = "NWH_StoryTraffic_RejoinJani";
            Vector3 authoredStart = new Vector3(
                initialLateralOffsetMeters,
                0f,
                0f);
            car.transform.SetPositionAndRotation(
                authoredStart,
                Quaternion.identity);
            StoryTrafficVehiclePresentationBinding motion =
                car.GetComponent<StoryTrafficVehiclePresentationBinding>();
            NwhStoryTrafficVehicleMotionBackend backend =
                car.GetComponent<NwhStoryTrafficVehicleMotionBackend>();
            Rigidbody body = car.GetComponent<Rigidbody>();
            Assert.That(motion, Is.Not.Null);
            Assert.That(backend, Is.Not.Null);
            Assert.That(body, Is.Not.Null);
            Assert.That(body.isKinematic, Is.False);

            motion.SetRoutePoseTarget(authoredStart, Quaternion.identity);
            yield return new WaitForFixedUpdate();
            motion.SetRouteRejoinActive(true);

            Vector3 launchPosition = motion.PhysicalWorldPosition;
            Vector3 previousPosition = launchPosition;
            float maximumStepDistance = 0f;
            float maximumForwardProgress = 0f;
            bool observedGroundContact = backend.HasGroundContact;
            bool lostGroundContactAfterLaunch = false;
            bool rejoinedWhileDriving = false;
            for (int step = 0; step < 900; step++)
            {
                Vector3 currentPosition = motion.PhysicalWorldPosition;
                float guidanceZ = Mathf.Min(
                    220f,
                    Mathf.Max(30f, currentPosition.z + 28f));
                motion.SetPhysicalRouteGuidanceTarget(
                    new Vector3(0f, 0f, guidanceZ),
                    Quaternion.identity,
                    physicalRouteProgress01: step / 900f);
                yield return new WaitForFixedUpdate();

                currentPosition = motion.PhysicalWorldPosition;
                maximumStepDistance = Mathf.Max(
                    maximumStepDistance,
                    Vector3.Distance(previousPosition, currentPosition));
                previousPosition = currentPosition;
                maximumForwardProgress = Mathf.Max(
                    maximumForwardProgress,
                    currentPosition.z - launchPosition.z);
                if (backend.HasGroundContact)
                {
                    observedGroundContact = true;
                }
                else if (observedGroundContact)
                {
                    lostGroundContactAfterLaunch = true;
                }

                float lateralDistanceFromCenterline =
                    Mathf.Abs(currentPosition.x);
                if (lateralDistanceFromCenterline <=
                        acceptedRouteCorridorMeters &&
                    maximumForwardProgress >= minimumForwardProgressMeters &&
                    backend.HasGroundContact &&
                    backend.SpeedMetersPerSecond > 2f)
                {
                    rejoinedWhileDriving = true;
                    break;
                }
            }

            Vector3 finalPosition = motion.PhysicalWorldPosition;
            float finalLateralDistance = Mathf.Abs(finalPosition.x);
            Debug.Log(
                "NWH story traffic route-rejoin diagnostic: " +
                $"launch={launchPosition}, final={finalPosition}, " +
                $"lateral={finalLateralDistance:F2}, " +
                $"forward={maximumForwardProgress:F2}, " +
                $"speed={backend.SpeedMetersPerSecond:F2}, " +
                $"grounded={backend.HasGroundContact}, " +
                $"lostGround={lostGroundContactAfterLaunch}, " +
                $"maxStep={maximumStepDistance:F3}, " +
                $"state={motion.ManeuverState}");

            Assert.That(
                Mathf.Abs(launchPosition.x),
                Is.InRange(8f, 12f),
                "The generated Jani fixture did not begin off the route.");
            Assert.That(motion.IsRouteRejoinActive, Is.True);
            Assert.That(observedGroundContact, Is.True);
            Assert.That(lostGroundContactAfterLaunch, Is.False,
                "Route rejoin must keep the physical chassis supported.");
            Assert.That(maximumStepDistance, Is.LessThan(maximumPhysicalStepMeters),
                "Route rejoin must steer the Rigidbody, not teleport it.");
            Assert.That(maximumForwardProgress,
                Is.GreaterThanOrEqualTo(minimumForwardProgressMeters),
                "Jani stalled instead of advancing along the retained route.");
            Assert.That(finalLateralDistance,
                Is.LessThanOrEqualTo(acceptedRouteCorridorMeters));
            Assert.That(rejoinedWhileDriving, Is.True,
                "Jani did not physically regain the straight route corridor.");

            Object.Destroy(car);
            Object.Destroy(ground);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GeneratedJani_PassesRoadBlockWithoutPoseTeleport()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "NWH_StoryTraffic_ObstacleGround";
            SetWorldSurfaceLayer(ground);
            ground.transform.SetPositionAndRotation(
                new Vector3(0f, -0.5f, 100f),
                Quaternion.identity);
            ground.transform.localScale = new Vector3(80f, 1f, 500f);
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "NWH_StoryTraffic_RoadBlock";
            obstacle.transform.SetPositionAndRotation(
                new Vector3(0f, 1f, 22f),
                Quaternion.identity);
            obstacle.transform.localScale = new Vector3(3f, 2f, 3f);

            CharacterPresentationCatalog catalog =
                Resources.Load<CharacterPresentationCatalog>(
                    "Phase1Characters/CharacterPresentationCatalog");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(
                catalog.TryGet(
                    "presentation.character.jani-car",
                    out CharacterPresentationCatalogEntry entry),
                Is.True);
            GameObject car = Object.Instantiate(entry.WrapperPrefab);
            car.name = "NWH_StoryTraffic_ObstacleJani";
            car.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            StoryTrafficVehiclePresentationBinding motion =
                car.GetComponent<StoryTrafficVehiclePresentationBinding>();
            NwhStoryTrafficVehicleMotionBackend backend =
                car.GetComponent<NwhStoryTrafficVehicleMotionBackend>();
            Assert.That(motion, Is.Not.Null);
            Assert.That(backend, Is.Not.Null);

            motion.SetRoutePoseTarget(Vector3.zero, Quaternion.identity);
            yield return new WaitForFixedUpdate();
            bool enteredPassing = false;
            bool passedRoadBlock = false;
            float maximumStepDistance = 0f;
            Vector3 previousPosition = car.transform.position;
            for (int step = 0; step < 1000; step++)
            {
                float guidanceZ = Mathf.Min(
                    170f,
                    car.transform.position.z + Mathf.Clamp(
                        7f + backend.SpeedMetersPerSecond * 0.42f,
                        7f,
                        26f));
                motion.SetPhysicalRouteGuidanceTarget(
                    new Vector3(0f, 0f, guidanceZ),
                    Quaternion.identity,
                    physicalRouteProgress01: step / 2000f);
                yield return new WaitForFixedUpdate();

                enteredPassing |=
                    motion.ManeuverState == StoryTrafficManeuverState.PassingOut ||
                    motion.ManeuverState == StoryTrafficManeuverState.Passing ||
                    motion.ManeuverState == StoryTrafficManeuverState.Returning;
                maximumStepDistance = Mathf.Max(
                    maximumStepDistance,
                    Vector3.Distance(previousPosition, car.transform.position));
                previousPosition = car.transform.position;
                if (car.transform.position.z > 30f && backend.HasGroundContact)
                {
                    passedRoadBlock = true;
                    break;
                }
            }

            Debug.Log(
                $"NWH story traffic obstacle diagnostic: " +
                $"position={car.transform.position}, speed={backend.SpeedMetersPerSecond:F2}, " +
                $"state={motion.ManeuverState}, passed={passedRoadBlock}, " +
                $"maxStep={maximumStepDistance:F3}");
            Object.Destroy(car);
            Object.Destroy(obstacle);
            Object.Destroy(ground);
            yield return null;

            Assert.That(enteredPassing, Is.True);
            Assert.That(passedRoadBlock, Is.True);
            Assert.That(maximumStepDistance, Is.LessThan(3f),
                "Normal obstacle handling must not reposition the chassis.");
        }

        [UnityTest]
        public IEnumerator GeneratedJani_HandbrakeTurnPhysicallyReleasesRearGrip()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "NWH_StoryTraffic_DriftGround";
            SetWorldSurfaceLayer(ground);
            ground.transform.SetPositionAndRotation(
                new Vector3(0f, -0.5f, 100f),
                Quaternion.identity);
            ground.transform.localScale = new Vector3(180f, 1f, 500f);

            CharacterPresentationCatalog catalog =
                Resources.Load<CharacterPresentationCatalog>(
                    "Phase1Characters/CharacterPresentationCatalog");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(
                catalog.TryGet(
                    "presentation.character.jani-car",
                    out CharacterPresentationCatalogEntry entry),
                Is.True);

            GameObject car = Object.Instantiate(entry.WrapperPrefab);
            car.name = "NWH_StoryTraffic_PhysicalDriftJani";
            car.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            StoryTrafficVehiclePresentationBinding motion =
                car.GetComponent<StoryTrafficVehiclePresentationBinding>();
            NwhStoryTrafficVehicleMotionBackend backend =
                car.GetComponent<NwhStoryTrafficVehicleMotionBackend>();
            NwhWheelPhysicsBackend wheels =
                car.GetComponent<NwhWheelPhysicsBackend>();
            Rigidbody body = car.GetComponent<Rigidbody>();
            Assert.That(motion, Is.Not.Null);
            Assert.That(backend, Is.Not.Null);
            Assert.That(wheels, Is.Not.Null);
            Assert.That(body, Is.Not.Null);

            // This fixture drives the backend directly so the assertion covers
            // real NWH tire forces instead of merely observing the presenter
            // enter its audio/animation drift state.
            motion.enabled = false;
            yield return new WaitForFixedUpdate();
            for (int step = 0; step < 700 &&
                 backend.SpeedMetersPerSecond < 13f; step++)
            {
                backend.Step(
                    Time.fixedDeltaTime,
                    new StoryTrafficVehicleDriveCommand(
                        car.transform.position + Vector3.forward * 100f,
                        Vector3.forward,
                        18f,
                        reverse: false,
                        fullBrake: false,
                        handbrake: false));
                yield return new WaitForFixedUpdate();
            }

            Assert.That(backend.SpeedMetersPerSecond, Is.GreaterThanOrEqualTo(13f),
                "The generated car never reached the donor slide entry speed.");
            float authoredLeftRearGrip = wheels.Wheels[2].sideFriction.grip;
            float authoredRightRearGrip = wheels.Wheels[3].sideFriction.grip;
            float minimumRearGripScale = 1f;
            float maximumBodySlipDegrees = 0f;
            float accumulatedYawDegrees = 0f;
            Vector3 previousForward = car.transform.forward;
            // Runtime presenter keeps the donor handbrake request active for
            // 0.88 s; exercise the same duration against the real NWH tire
            // backend so this cannot regress into an audio-only skid event.
            for (int step = 0; step < 44; step++)
            {
                Vector3 turnDirection = Quaternion.AngleAxis(
                    38f,
                    Vector3.up) * Vector3.forward;
                backend.Step(
                    Time.fixedDeltaTime,
                    new StoryTrafficVehicleDriveCommand(
                        car.transform.position + turnDirection * 16f,
                        turnDirection,
                        15f,
                        reverse: false,
                        fullBrake: false,
                        handbrake: true));
                yield return new WaitForFixedUpdate();

                minimumRearGripScale = Mathf.Min(
                    minimumRearGripScale,
                    wheels.Wheels[2].sideFriction.grip /
                    Mathf.Max(0.001f, authoredLeftRearGrip),
                    wheels.Wheels[3].sideFriction.grip /
                    Mathf.Max(0.001f, authoredRightRearGrip));
                Vector3 planarVelocity = Vector3.ProjectOnPlane(
                    body.linearVelocity,
                    Vector3.up);
                Vector3 planarForward = Vector3.ProjectOnPlane(
                    car.transform.forward,
                    Vector3.up);
                if (planarVelocity.sqrMagnitude > 1f &&
                    planarForward.sqrMagnitude > 0.1f)
                {
                    maximumBodySlipDegrees = Mathf.Max(
                        maximumBodySlipDegrees,
                        Mathf.Abs(Vector3.SignedAngle(
                            planarForward,
                            planarVelocity,
                            Vector3.up)));
                }

                accumulatedYawDegrees += Mathf.Abs(Vector3.SignedAngle(
                    previousForward,
                    car.transform.forward,
                    Vector3.up));
                previousForward = car.transform.forward;
            }

            Debug.Log(
                "NWH physical handbrake drift diagnostic: " +
                $"speed={backend.SpeedMetersPerSecond:F2}, " +
                $"rearGripScale={minimumRearGripScale:F2}, " +
                $"bodySlip={maximumBodySlipDegrees:F2}deg, " +
                $"yaw={accumulatedYawDegrees:F2}deg, " +
                $"grounded={backend.HasGroundContact}");

            Assert.That(minimumRearGripScale, Is.LessThanOrEqualTo(0.55f),
                "The handbrake state did not release rear lateral tire grip.");
            Assert.That(maximumBodySlipDegrees, Is.GreaterThan(3f),
                "Handbrake produced an ordinary planted turn, not chassis slip.");
            Assert.That(accumulatedYawDegrees, Is.GreaterThan(8f),
                "The physical chassis did not rotate through the slide.");
            Assert.That(backend.HasGroundContact, Is.True);

            Object.Destroy(car);
            Object.Destroy(ground);
            yield return null;
        }

        private static IEnumerator VerifyGeneratedCarAccelerates(
            string presentationId,
            string diagnosticName,
            float minimumSpeedMetersPerSecond,
            float minimumForwardDistanceMeters,
            int maximumSteps)
        {
            GameObject ground = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            ground.name = $"NWH_StoryTraffic_{diagnosticName}_TestGround";
            SetWorldSurfaceLayer(ground);
            ground.transform.SetPositionAndRotation(
                new Vector3(0f, -0.5f, 100f),
                Quaternion.identity);
            ground.transform.localScale = new Vector3(80f, 1f, 500f);

            CharacterPresentationCatalog catalog =
                Resources.Load<CharacterPresentationCatalog>(
                    "Phase1Characters/CharacterPresentationCatalog");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(
                catalog.TryGet(
                    presentationId,
                    out CharacterPresentationCatalogEntry entry),
                Is.True);
            GameObject prefab = entry.WrapperPrefab;
            Assert.That(prefab, Is.Not.Null);
            GameObject car = Object.Instantiate(prefab);
            car.name = $"NWH_StoryTraffic_Test{diagnosticName}";
            car.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            StoryTrafficVehiclePresentationBinding motion =
                car.GetComponent<StoryTrafficVehiclePresentationBinding>();
            NwhStoryTrafficVehicleMotionBackend backend =
                car.GetComponent<NwhStoryTrafficVehicleMotionBackend>();
            NwhWheelPhysicsBackend wheelBackend =
                car.GetComponent<NwhWheelPhysicsBackend>();
            Rigidbody body = car.GetComponent<Rigidbody>();

            Assert.That(motion, Is.Not.Null);
            Assert.That(backend, Is.Not.Null);
            Assert.That(wheelBackend, Is.Not.Null);
            Assert.That(body, Is.Not.Null);
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.useGravity, Is.True);

            motion.SetRoutePoseTarget(Vector3.zero, Quaternion.identity);
            yield return new WaitForFixedUpdate();
            bool acceleratedWhileGrounded = false;
            float guidanceDistance = Mathf.Max(
                80f,
                minimumForwardDistanceMeters + 150f);
            for (int step = 0; step < maximumSteps; step++)
            {
                motion.SetPhysicalRouteGuidanceTarget(
                    new Vector3(0f, 0f, guidanceDistance),
                    Quaternion.identity,
                    physicalRouteProgress01: step / 1200f);
                yield return new WaitForFixedUpdate();
                if (step >= 50 && backend.HasGroundContact &&
                    backend.SpeedMetersPerSecond > minimumSpeedMetersPerSecond &&
                    car.transform.position.z > minimumForwardDistanceMeters)
                {
                    acceleratedWhileGrounded = true;
                    break;
                }
            }

            Debug.Log(
                $"NWH story traffic {diagnosticName} diagnostic: " +
                $"position={car.transform.position}, " +
                $"speed={backend.SpeedMetersPerSecond:F3}, rpm={backend.EngineRpm:F0}, " +
                $"gear={backend.SelectedGear}, state={motion.ManeuverState}, " +
                $"blocked={motion.IsObstacleBraking}, grounded={backend.HasGroundContact}, " +
                $"rearMotor=({wheelBackend.Wheels[2].MotorTorque:F1}," +
                $"{wheelBackend.Wheels[3].MotorTorque:F1}), " +
                $"rearBrake=({wheelBackend.Wheels[2].BrakeTorque:F1}," +
                $"{wheelBackend.Wheels[3].BrakeTorque:F1}), " +
                $"rearOmega=({wheelBackend.Wheels[2].AngularVelocity:F2}," +
                $"{wheelBackend.Wheels[3].AngularVelocity:F2})");

            Assert.That(acceleratedWhileGrounded, Is.True);
            Assert.That(backend.HasGroundContact, Is.True);
            Assert.That(backend.EngineRpm, Is.GreaterThan(650f));
            Assert.That(backend.SelectedGear, Is.GreaterThanOrEqualTo(1));
            if (minimumSpeedMetersPerSecond >= 25f)
            {
                Assert.That(backend.SelectedGear, Is.GreaterThanOrEqualTo(3));
            }
            Assert.That(backend.SpeedMetersPerSecond,
                Is.GreaterThan(minimumSpeedMetersPerSecond));
            Assert.That(car.transform.position.z,
                Is.GreaterThan(minimumForwardDistanceMeters));

            Object.Destroy(car);
            Object.Destroy(ground);
            yield return null;
        }

        private static void SetWorldSurfaceLayer(GameObject target)
        {
            int worldSurfaceLayer = LayerMask.NameToLayer("WorldSurface");
            Assert.That(worldSurfaceLayer, Is.GreaterThanOrEqualTo(0));
            target.layer = worldSurfaceLayer;
        }
    }
}
