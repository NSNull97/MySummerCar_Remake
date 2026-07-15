using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Player;
using MSC.World.Remaster;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.WorldRemaster
{
    public sealed class WorldPilotTraversalPlayModeTests
    {
        private const int EvidenceSchemaVersion = 2;
        private const string PilotSceneName = "WorldRemasterPilotPlaytest";
        private const string PilotScenePath =
            "Assets/Game/World/Production/Scenes/WorldRemasterPilotPlaytest.unity";
        private const string EvidenceRelativePath =
            "Docs/WorldValidation/M05B1_M4_CHARACTER_CONTROLLER_TRAVERSAL.json";
        private const string PlayerPrefabPath =
            "Assets/Game/Player/Content/Prefabs/M4_FirstPersonPlayer.prefab";
        private const float WaypointTimeoutSeconds = 12f;
        private const float M4WalkingSpeedMetersPerSecond = 4.2f;
        private const float FrameDisplacementEpsilonMeters = 0.035f;
        private const float StallTimeoutSeconds = 1.5f;
        private const float MinimumCumulativeDistanceMeters = 50f;
        private const float MinimumRouteExtentXMeters = 16f;
        private const float MinimumRouteExtentZMeters = 10f;

        [UnityTest]
        public IEnumerator RealM4CharacterController_CompletesAuthoredGarageHouseTraversal()
        {
            DeletePreviousEvidence();
            AsyncOperation load = SceneManager.LoadSceneAsync(PilotSceneName, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, "Pilot scene is missing from Build Settings.");
            yield return load;
            yield return null;

            WorldPilotTraversalRoute route = UnityEngine.Object.FindFirstObjectByType<WorldPilotTraversalRoute>(
                FindObjectsInactive.Include);
            Assert.That(route, Is.Not.Null, "Pilot scene has no bounded 05B.1 traversal authoring.");
            Assert.That(route.RouteId, Is.EqualTo(WorldPilotTraversalRoute.CurrentRouteId));
            Assert.That(route.ZoneId, Is.EqualTo("cell_0_-3"));
            Assert.That(route.Waypoints.Count, Is.GreaterThanOrEqualTo(7));
            Assert.That(route.PlayerRoot, Is.Not.Null);
            Assert.That(route.PlayerController, Is.Not.Null);
            Assert.That(route.PlayerController.enabled, Is.True);
            Assert.That(
                route.CalculateFingerprint(),
                Is.EqualTo(WorldPilotTraversalRoute.AcceptedRouteFingerprint),
                "Authored route drifted from the independently accepted 05B.1 traversal contract.");
            Assert.That(route.DependencyFingerprint, Is.Not.Empty);

            (float routeExtentX, float routeExtentZ) = CalculateRouteExtents(route);
            Assert.That(routeExtentX, Is.GreaterThanOrEqualTo(MinimumRouteExtentXMeters));
            Assert.That(routeExtentZ, Is.GreaterThanOrEqualTo(MinimumRouteExtentZMeters));

            FirstPersonMotor motor = route.PlayerRoot.GetComponent<FirstPersonMotor>();
            PlayerInputRouter inputRouter = route.PlayerRoot.GetComponent<PlayerInputRouter>();
            Assert.That(motor, Is.Not.Null, "Authored route is not bound to the real M4 FirstPersonMotor.");
            Assert.That(inputRouter, Is.Not.Null, "M4 PlayerInputRouter architecture is missing.");
            inputRouter.enabled = false;
            motor.ResetInputIntent();

            foreach (WorldHingedArchitecture door in route.DoorsToOpen)
            {
                Assert.That(door, Is.Not.Null);
                door.SetOpen(true, immediate: true);
                Assert.That(door.OpenNormalized, Is.EqualTo(1f));
            }

            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();

            var metrics = new TraversalMetrics();
            string[] completedWaypointIds = new string[route.Waypoints.Count];
            for (int index = 0; index < route.Waypoints.Count; index++)
            {
                WorldPilotTraversalWaypoint waypoint = route.Waypoints[index];
                yield return TraverseTo(route, motor, waypoint, index, metrics);
                completedWaypointIds[index] = waypoint.WaypointId;
            }

            motor.ResetInputIntent();
            Assert.That(route.PlayerController.enabled, Is.True);
            Assert.That(completedWaypointIds[completedWaypointIds.Length - 1], Is.EqualTo("garage_return_start"));
            float returnDistance = HorizontalDistance(
                route.PlayerRoot.position,
                route.GetWorldPosition(route.Waypoints.Count - 1));
            Assert.That(
                returnDistance,
                Is.LessThanOrEqualTo(route.Waypoints[route.Waypoints.Count - 1].ReachToleranceMeters));
            Assert.That(
                metrics.CumulativeHorizontalDistanceMeters,
                Is.GreaterThanOrEqualTo(MinimumCumulativeDistanceMeters));
            WritePassingEvidence(route, completedWaypointIds, metrics);
        }

        private static IEnumerator TraverseTo(
            WorldPilotTraversalRoute route,
            FirstPersonMotor motor,
            WorldPilotTraversalWaypoint waypoint,
            int waypointIndex,
            TraversalMetrics metrics)
        {
            float startedAt = Time.time;
            float lastProgressAt = startedAt;
            float previousSimulationTime = startedAt;
            Vector3 target = route.GetWorldPosition(waypointIndex);
            Assert.That(
                float.IsFinite(target.x) && float.IsFinite(target.y) && float.IsFinite(target.z),
                Is.True,
                $"Traversal waypoint '{waypoint.WaypointId}' has a non-finite world position.");
            Vector3 previousPosition = route.PlayerRoot.position;
            motor.SetCrouchRequested(waypoint.RequireCrouching);
            if (waypoint.RequireCrouching)
            {
                float crouchStartedAt = Time.time;
                while (route.PlayerController.height > 1.2f && Time.time - crouchStartedAt < 1f)
                {
                    motor.SetCrouchRequested(true);
                    yield return null;
                }

                Assert.That(
                    route.PlayerController.height,
                    Is.LessThanOrEqualTo(1.2f),
                    $"M4 controller did not enter its authored crouch state before '{waypoint.WaypointId}'.");
                previousPosition = route.PlayerRoot.position;
                previousSimulationTime = Time.time;
                lastProgressAt = Time.time;
            }

            while (Time.time - startedAt < WaypointTimeoutSeconds)
            {
                Vector3 horizontalDelta = target - route.PlayerRoot.position;
                horizontalDelta.y = 0f;
                if (horizontalDelta.magnitude <= waypoint.ReachToleranceMeters)
                {
                    motor.SetMoveInput(Vector2.zero);
                    break;
                }

                route.PlayerRoot.rotation = Quaternion.LookRotation(horizontalDelta.normalized, Vector3.up);
                motor.SetMoveInput(Vector2.up);
                motor.SetCrouchRequested(waypoint.RequireCrouching);
                float commandedFrameDeltaTime = Time.deltaTime;
                yield return null;

                float simulationDeltaTime = Mathf.Max(0f, Time.time - previousSimulationTime);
                float frameDisplacement = HorizontalDistance(previousPosition, route.PlayerRoot.position);
                float movementDeltaTime = Mathf.Max(commandedFrameDeltaTime, simulationDeltaTime);
                float maximumAllowed = M4WalkingSpeedMetersPerSecond * movementDeltaTime +
                                       FrameDisplacementEpsilonMeters;
                Assert.That(
                    frameDisplacement,
                    Is.LessThanOrEqualTo(maximumAllowed),
                    $"M4 traversal teleported {frameDisplacement:0.###}m in one frame at " +
                    $"'{waypoint.WaypointId}' (allowed {maximumAllowed:0.###}m).");

                metrics.TraversalFrameCount++;
                metrics.CumulativeHorizontalDistanceMeters += frameDisplacement;
                metrics.MaximumFrameHorizontalDisplacementMeters = Mathf.Max(
                    metrics.MaximumFrameHorizontalDisplacementMeters,
                    frameDisplacement);
                if (frameDisplacement > 0.0001f)
                {
                    lastProgressAt = Time.time;
                }

                float stalledSeconds = Time.time - lastProgressAt;
                Assert.That(
                    stalledSeconds,
                    Is.LessThan(StallTimeoutSeconds),
                    $"M4 traversal stalled before '{waypoint.WaypointId}' for {stalledSeconds:0.###}s; " +
                    $"player={route.PlayerRoot.position}, target={target}, " +
                    $"remaining={HorizontalDistance(route.PlayerRoot.position, target):0.###}m, " +
                    $"grounded={route.PlayerController.isGrounded}, " +
                    BuildCollisionDiagnostics(route, target));
                previousPosition = route.PlayerRoot.position;
                previousSimulationTime = Time.time;
            }

            motor.SetMoveInput(Vector2.zero);
            Vector3 remaining = target - route.PlayerRoot.position;
            float horizontalRemaining = new Vector2(remaining.x, remaining.z).magnitude;
            Assert.That(
                horizontalRemaining,
                Is.LessThanOrEqualTo(waypoint.ReachToleranceMeters),
                $"Real M4 CharacterController could not reach '{waypoint.WaypointId}' in " +
                $"{WaypointTimeoutSeconds:0.#}s; remaining horizontal distance {horizontalRemaining:0.###}m, " +
                $"player={route.PlayerRoot.position}, target={target}; " +
                BuildCollisionDiagnostics(route, target));

            if (waypoint.RequireGrounded)
            {
                for (int frame = 0; frame < 5 && !route.PlayerController.isGrounded; frame++)
                {
                    yield return null;
                }

                Assert.That(
                    route.PlayerController.isGrounded,
                    Is.True,
                    $"M4 CharacterController is not grounded at '{waypoint.WaypointId}'.");
            }

            float verticalRemaining = Mathf.Abs(target.y - route.PlayerRoot.position.y);
            Assert.That(
                float.IsFinite(verticalRemaining),
                Is.True,
                $"M4 CharacterController produced a non-finite height at '{waypoint.WaypointId}'.");
            Assert.That(
                verticalRemaining,
                Is.LessThanOrEqualTo(waypoint.VerticalReachToleranceMeters),
                $"M4 CharacterController reached '{waypoint.WaypointId}' on the wrong vertical level: " +
                $"deviation={verticalRemaining:0.###}m, allowed={waypoint.VerticalReachToleranceMeters:0.###}m.");
            metrics.MaximumWaypointVerticalDeviationMeters = Mathf.Max(
                metrics.MaximumWaypointVerticalDeviationMeters,
                verticalRemaining);
        }

        private static void DeletePreviousEvidence()
        {
            string path = GetEvidenceAbsolutePath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void WritePassingEvidence(
            WorldPilotTraversalRoute route,
            string[] completedWaypointIds,
            TraversalMetrics metrics)
        {
            string projectRoot = GetProjectRoot();
            (float routeExtentX, float routeExtentZ) = CalculateRouteExtents(route);
            var evidence = new WorldPilotTraversalEvidenceDto
            {
                schemaVersion = EvidenceSchemaVersion,
                validatorId = "m4-character-controller-traversal",
                passed = true,
                scenePath = PilotScenePath,
                routeId = route.RouteId,
                routeFingerprint = route.CalculateFingerprint(),
                dependencyFingerprint = route.DependencyFingerprint,
                playerPrefabGuid = route.PlayerPrefabGuid,
                motorType = typeof(FirstPersonMotor).FullName,
                controllerType = typeof(CharacterController).FullName,
                completedWaypointIds = completedWaypointIds.ToArray(),
                pilotSceneSha256 = CalculateFileSha256(Path.Combine(projectRoot, PilotScenePath)),
                playerPrefabSha256 = CalculateFileSha256(Path.Combine(projectRoot, PlayerPrefabPath)),
                noTeleportVerified = true,
                stallGuardVerified = true,
                crouchPassageVerified = true,
                returnToStartVerified = true,
                verticalReachVerified = true,
                traversalFrameCount = metrics.TraversalFrameCount,
                cumulativeHorizontalDistanceMeters = metrics.CumulativeHorizontalDistanceMeters,
                maximumFrameHorizontalDisplacementMeters = metrics.MaximumFrameHorizontalDisplacementMeters,
                maximumWaypointVerticalDeviationMeters = metrics.MaximumWaypointVerticalDeviationMeters,
                routeExtentXMeters = routeExtentX,
                routeExtentZMeters = routeExtentZ
            };

            string path = GetEvidenceAbsolutePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException());
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(evidence, prettyPrint: true) + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (File.Exists(path))
            {
                File.Replace(temporary, path, null);
            }
            else
            {
                File.Move(temporary, path);
            }
        }

        private static string GetEvidenceAbsolutePath()
        {
            string projectRoot = GetProjectRoot();
            return Path.Combine(projectRoot, EvidenceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string GetProjectRoot() =>
            Directory.GetParent(Application.dataPath)?.FullName ??
            throw new InvalidOperationException("Unity project root cannot be resolved.");

        private static float HorizontalDistance(Vector3 from, Vector3 to) =>
            new Vector2(to.x - from.x, to.z - from.z).magnitude;

        private static (float extentX, float extentZ) CalculateRouteExtents(WorldPilotTraversalRoute route)
        {
            float minimumX = route.Waypoints.Min(waypoint => waypoint.LocalPosition.x);
            float maximumX = route.Waypoints.Max(waypoint => waypoint.LocalPosition.x);
            float minimumZ = route.Waypoints.Min(waypoint => waypoint.LocalPosition.z);
            float maximumZ = route.Waypoints.Max(waypoint => waypoint.LocalPosition.z);
            return (maximumX - minimumX, maximumZ - minimumZ);
        }

        private static string BuildCollisionDiagnostics(
            WorldPilotTraversalRoute route,
            Vector3 target)
        {
            CharacterController controller = route.PlayerController;
            Transform player = route.PlayerRoot;
            Vector3 center = player.TransformPoint(controller.center);
            float radius = controller.radius * 0.95f;
            float halfSegment = Mathf.Max(0f, controller.height * 0.5f - radius);
            Vector3 top = center + Vector3.up * halfSegment;
            Vector3 bottom = center - Vector3.up * halfSegment;
            Vector3 direction = target - player.position;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0f ? direction.normalized : player.forward;

            string overlaps = string.Join(",", Physics.OverlapCapsule(
                    bottom,
                    top,
                    radius,
                    Physics.AllLayers,
                    QueryTriggerInteraction.Ignore)
                .Where(collider => collider != controller && !collider.transform.IsChildOf(player))
                .Select(collider => collider.name)
                .Distinct()
                .OrderBy(name => name, StringComparer.Ordinal));
            string casts = string.Join(",", Physics.CapsuleCastAll(
                    bottom,
                    top,
                    radius,
                    direction,
                    0.75f,
                    Physics.AllLayers,
                    QueryTriggerInteraction.Ignore)
                .Where(hit => hit.collider != controller && !hit.collider.transform.IsChildOf(player))
                .OrderBy(hit => hit.distance)
                .Select(hit => $"{hit.collider.name}@{hit.distance:0.###}/n={hit.normal}"));
            string groundSamples = string.Join(",", Enumerable.Range(0, 31).Select(sampleIndex =>
            {
                float localZ = -5.2f + sampleIndex * 0.1f;
                Vector3 rayOrigin = route.ReferenceFrame.TransformPoint(new Vector3(0f, 4f, localZ));
                RaycastHit hit = Physics.RaycastAll(
                        rayOrigin,
                        Vector3.down,
                        10f,
                        Physics.AllLayers,
                        QueryTriggerInteraction.Ignore)
                    .Where(candidate => candidate.collider != controller &&
                                        !candidate.collider.transform.IsChildOf(player))
                    .OrderByDescending(candidate => candidate.point.y)
                    .FirstOrDefault();
                return hit.collider != null
                    ? $"{localZ:0.0}:{hit.collider.name}@{hit.point.y:0.###}"
                    : $"{localZ:0.0}:<none>";
            }));
            string nearby = string.Join(",", UnityEngine.Object.FindObjectsByType<Collider>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where(collider => collider != controller && !collider.transform.IsChildOf(player))
                .Select(collider => new
                {
                    Collider = collider,
                    Distance = Vector3.Distance(collider.bounds.ClosestPoint(center), center)
                })
                .Where(candidate => candidate.Distance <= 2f)
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Collider.name, StringComparer.Ordinal)
                .Take(12)
                .Select(candidate =>
                    $"{candidate.Collider.name}@{candidate.Distance:0.###}/bounds={candidate.Collider.bounds}"));
            FirstPersonMotor motor = player.GetComponent<FirstPersonMotor>();
            PlayerInputRouter inputRouter = player.GetComponent<PlayerInputRouter>();
            return $"worldEuler={player.eulerAngles}, localEuler={player.localEulerAngles}, " +
                   $"radius={controller.radius:0.###}, height={controller.height:0.###}, " +
                   $"step={controller.stepOffset:0.###}, motorActive={motor != null && motor.isActiveAndEnabled}, " +
                   $"inputRouterEnabled={inputRouter != null && inputRouter.enabled}, " +
                   $"timeScale={Time.timeScale:0.###}, velocity={controller.velocity}, " +
                   $"collisionFlags={controller.collisionFlags}, overlaps=[{overlaps}], casts=[{casts}], " +
                   $"nearby=[{nearby}], ground=[{groundSamples}].";
        }

        private static string CalculateFileSha256(string absolutePath)
        {
            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(absolutePath);
            byte[] hash = sha256.ComputeHash(stream);
            var output = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
            {
                output.Append(value.ToString("x2"));
            }

            return output.ToString();
        }

        private sealed class TraversalMetrics
        {
            public int TraversalFrameCount { get; set; }
            public float CumulativeHorizontalDistanceMeters { get; set; }
            public float MaximumFrameHorizontalDisplacementMeters { get; set; }
            public float MaximumWaypointVerticalDeviationMeters { get; set; }
        }

        [Serializable]
        private sealed class WorldPilotTraversalEvidenceDto
        {
            public int schemaVersion;
            public string validatorId = string.Empty;
            public bool passed;
            public string scenePath = string.Empty;
            public string routeId = string.Empty;
            public string routeFingerprint = string.Empty;
            public string dependencyFingerprint = string.Empty;
            public string playerPrefabGuid = string.Empty;
            public string motorType = string.Empty;
            public string controllerType = string.Empty;
            public string[] completedWaypointIds = Array.Empty<string>();
            public string pilotSceneSha256 = string.Empty;
            public string playerPrefabSha256 = string.Empty;
            public bool noTeleportVerified;
            public bool stallGuardVerified;
            public bool crouchPassageVerified;
            public bool returnToStartVerified;
            public bool verticalReachVerified;
            public int traversalFrameCount;
            public float cumulativeHorizontalDistanceMeters;
            public float maximumFrameHorizontalDisplacementMeters;
            public float maximumWaypointVerticalDeviationMeters;
            public float routeExtentXMeters;
            public float routeExtentZMeters;
        }
    }
}
