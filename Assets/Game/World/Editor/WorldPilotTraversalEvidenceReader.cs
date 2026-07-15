using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.World.Remaster.Editor
{
    public sealed class WorldPilotTraversalEvidenceReadResult
    {
        internal WorldPilotTraversalEvidenceReadResult(
            bool executed,
            bool passed,
            string evidence,
            IEnumerable<string> errors,
            IEnumerable<string> warnings)
        {
            Executed = executed;
            Passed = passed;
            Evidence = evidence ?? string.Empty;
            Errors = (errors ?? Array.Empty<string>()).ToArray();
            Warnings = (warnings ?? Array.Empty<string>()).ToArray();
        }

        public bool Executed { get; }
        public bool Passed { get; }
        public string Evidence { get; }
        public string[] Errors { get; }
        public string[] Warnings { get; }
    }

    public static class WorldPilotTraversalEvidenceReader
    {
        private const int CurrentEvidenceSchemaVersion = 2;
        private const float MinimumCumulativeDistanceMeters = 50f;
        private const float MaximumAcceptedFrameDisplacementMeters = 0.35f;
        private const float MaximumAcceptedVerticalDeviationMeters = 0.35f;
        private const float MinimumRouteExtentXMeters = 16f;
        private const float MinimumRouteExtentZMeters = 10f;

        public const string ValidatorId = "m4-character-controller-traversal";
        public const string EvidencePath =
            "Docs/WorldValidation/M05B1_M4_CHARACTER_CONTROLLER_TRAVERSAL.json";

        public static WorldPilotTraversalEvidenceReadResult ReadAndValidate()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            string absoluteEvidencePath = Path.GetFullPath(EvidencePath);
            if (!File.Exists(absoluteEvidencePath))
            {
                errors.Add("Traversal evidence has not been produced by the real M4 PlayMode test: " + EvidencePath);
                return Result(executed: false, errors, warnings, "Evidence file is missing.");
            }

            WorldPilotTraversalEvidenceDto evidence;
            try
            {
                evidence = JsonUtility.FromJson<WorldPilotTraversalEvidenceDto>(
                    File.ReadAllText(absoluteEvidencePath));
            }
            catch (Exception exception)
            {
                errors.Add("Traversal evidence JSON cannot be read: " + exception.Message);
                return Result(executed: true, errors, warnings, "Evidence JSON is invalid.");
            }

            if (evidence == null)
            {
                errors.Add("Traversal evidence JSON is empty.");
                return Result(executed: true, errors, warnings, "Evidence JSON is empty.");
            }

            ValidateEvidenceHeader(evidence, errors);
            ValidateCurrentAuthoring(evidence, errors);
            string summary = errors.Count == 0
                ? $"Real M4 CharacterController completed {evidence.completedWaypointIds?.Length ?? 0} authored waypoints; " +
                  $"route fingerprint {evidence.routeFingerprint}."
                : "Traversal evidence is stale or inconsistent with current authoring.";
            return Result(executed: true, errors, warnings, summary);
        }

        private static void ValidateEvidenceHeader(
            WorldPilotTraversalEvidenceDto evidence,
            ICollection<string> errors)
        {
            if (evidence.schemaVersion != CurrentEvidenceSchemaVersion)
            {
                errors.Add($"Traversal evidence schema must be {CurrentEvidenceSchemaVersion}.");
            }

            if (!string.Equals(evidence.validatorId, ValidatorId, StringComparison.Ordinal) || !evidence.passed)
            {
                errors.Add("Traversal evidence does not contain a passing m4-character-controller-traversal result.");
            }

            if (!string.Equals(evidence.scenePath, WorldRemasterPaths.PilotPlaytestScene, StringComparison.Ordinal))
            {
                errors.Add("Traversal evidence points at an unexpected scene: " + evidence.scenePath);
            }

            if (!string.Equals(evidence.routeId, WorldPilotTraversalRoute.CurrentRouteId, StringComparison.Ordinal))
            {
                errors.Add("Traversal evidence route ID is not the current bounded 05B.1 route.");
            }

            if (!string.Equals(
                    evidence.routeFingerprint,
                    WorldPilotTraversalRoute.AcceptedRouteFingerprint,
                    StringComparison.Ordinal))
            {
                errors.Add("Traversal evidence route fingerprint is not the independently accepted 05B.1 contract.");
            }

            if (string.IsNullOrWhiteSpace(evidence.dependencyFingerprint))
            {
                errors.Add("Traversal evidence has no recursive production dependency fingerprint.");
            }

            if (!string.Equals(evidence.motorType, typeof(FirstPersonMotor).FullName, StringComparison.Ordinal) ||
                !string.Equals(evidence.controllerType, typeof(CharacterController).FullName, StringComparison.Ordinal))
            {
                errors.Add("Traversal evidence was not produced with the real M4 motor and CharacterController types.");
            }

            ValidateFileHash(
                WorldRemasterPaths.PilotPlaytestScene,
                evidence.pilotSceneSha256,
                "pilot scene",
                errors);
            ValidateFileHash(
                WorldRemasterPaths.PlayerPrefab,
                evidence.playerPrefabSha256,
                "M4 player prefab",
                errors);

            if (!evidence.noTeleportVerified || !evidence.stallGuardVerified ||
                !evidence.crouchPassageVerified || !evidence.returnToStartVerified ||
                !evidence.verticalReachVerified || evidence.traversalFrameCount <= 0 ||
                !float.IsFinite(evidence.cumulativeHorizontalDistanceMeters) ||
                evidence.cumulativeHorizontalDistanceMeters < MinimumCumulativeDistanceMeters ||
                !float.IsFinite(evidence.maximumFrameHorizontalDisplacementMeters) ||
                evidence.maximumFrameHorizontalDisplacementMeters <= 0f ||
                evidence.maximumFrameHorizontalDisplacementMeters > MaximumAcceptedFrameDisplacementMeters ||
                !float.IsFinite(evidence.maximumWaypointVerticalDeviationMeters) ||
                evidence.maximumWaypointVerticalDeviationMeters < 0f ||
                evidence.maximumWaypointVerticalDeviationMeters > MaximumAcceptedVerticalDeviationMeters ||
                !float.IsFinite(evidence.routeExtentXMeters) ||
                evidence.routeExtentXMeters < MinimumRouteExtentXMeters ||
                !float.IsFinite(evidence.routeExtentZMeters) ||
                evidence.routeExtentZMeters < MinimumRouteExtentZMeters)
            {
                errors.Add(
                    "Traversal evidence lacks bounded no-teleport, distance, vertical-level, extent, stall, or return proof.");
            }
        }

        private static void ValidateCurrentAuthoring(
            WorldPilotTraversalEvidenceDto evidence,
            ICollection<string> errors)
        {
            Scene scene = default;
            bool closeWhenDone = false;
            try
            {
                scene = SceneManager.GetSceneByPath(WorldRemasterPaths.PilotPlaytestScene);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(
                        WorldRemasterPaths.PilotPlaytestScene,
                        OpenSceneMode.Additive);
                    closeWhenDone = true;
                }

                WorldPilotTraversalRoute[] routes = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<WorldPilotTraversalRoute>(true))
                    .ToArray();
                if (routes.Length != 1)
                {
                    errors.Add($"Pilot scene must contain exactly one authored traversal route; found {routes.Length}.");
                    return;
                }

                WorldPilotTraversalRoute route = routes[0];
                if (route.SchemaVersion != WorldPilotTraversalRoute.CurrentSchemaVersion ||
                    route.RouteId != WorldPilotTraversalRoute.CurrentRouteId ||
                    route.ZoneId != WorldRemasterPaths.PilotZoneId)
                {
                    errors.Add("Current pilot traversal route identity/schema is inconsistent.");
                }

                string currentRouteFingerprint = route.CalculateFingerprint();
                if (!string.Equals(
                        currentRouteFingerprint,
                        WorldPilotTraversalRoute.AcceptedRouteFingerprint,
                        StringComparison.Ordinal))
                {
                    errors.Add("Current pilot traversal route drifted from the accepted 05B.1 fingerprint.");
                }

                string expectedPlayerGuid = AssetDatabase.AssetPathToGUID(WorldRemasterPaths.PlayerPrefab);
                if (route.PlayerPrefabGuid != expectedPlayerGuid || evidence.playerPrefabGuid != expectedPlayerGuid)
                {
                    errors.Add("Traversal player prefab GUID drifted from the M4 player prefab.");
                }

                if (route.ReferenceFrame == null || route.PlayerRoot == null || route.PlayerController == null ||
                    route.PlayerRoot.GetComponent<FirstPersonMotor>() == null ||
                    route.PlayerRoot.GetComponent<CharacterController>() != route.PlayerController)
                {
                    errors.Add("Traversal route is not wired to the real scene M4 motor/CharacterController.");
                }

                string[] expectedDoors = { "GarageDoorLeft", "GarageDoorRight", "HouseFrontDoor" };
                string[] currentDoors = route.DoorsToOpen
                    .Select(door => door != null ? door.name : string.Empty)
                    .ToArray();
                if (!currentDoors.SequenceEqual(expectedDoors, StringComparer.Ordinal))
                {
                    errors.Add("Traversal route door references are missing or reordered.");
                }

                string[] currentWaypointIds = route.Waypoints.Select(waypoint => waypoint.WaypointId).ToArray();
                if (currentWaypointIds.Length < 10 ||
                    currentWaypointIds.Distinct(StringComparer.Ordinal).Count() != currentWaypointIds.Length ||
                    !currentWaypointIds.SequenceEqual(evidence.completedWaypointIds ?? Array.Empty<string>(), StringComparer.Ordinal))
                {
                    errors.Add("Traversal evidence does not cover every unique current authored waypoint in order.");
                }


                if (!currentWaypointIds.Contains("house_exit", StringComparer.Ordinal) ||
                    !currentWaypointIds.Contains("garage_return_start", StringComparer.Ordinal))
                {
                    errors.Add("Traversal route must leave the house and finish back at the exterior start.");
                }

                if (route.Waypoints.Count(waypoint => waypoint.RequireCrouching) < 4)
                {
                    errors.Add("Traversal route does not preserve the validated crouched doorway passage in both directions.");
                }

                float minimumX = route.Waypoints.Min(waypoint => waypoint.LocalPosition.x);
                float maximumX = route.Waypoints.Max(waypoint => waypoint.LocalPosition.x);
                float minimumZ = route.Waypoints.Min(waypoint => waypoint.LocalPosition.z);
                float maximumZ = route.Waypoints.Max(waypoint => waypoint.LocalPosition.z);
                float currentExtentX = maximumX - minimumX;
                float currentExtentZ = maximumZ - minimumZ;
                if (currentExtentX < MinimumRouteExtentXMeters || currentExtentZ < MinimumRouteExtentZMeters ||
                    Mathf.Abs(currentExtentX - evidence.routeExtentXMeters) > 0.001f ||
                    Mathf.Abs(currentExtentZ - evidence.routeExtentZMeters) > 0.001f)
                {
                    errors.Add("Traversal route/evidence does not preserve the accepted exterior/interior XZ extent.");
                }

                if (!string.Equals(currentRouteFingerprint, evidence.routeFingerprint, StringComparison.Ordinal))
                {
                    errors.Add("Traversal evidence fingerprint is stale for the current authored route.");
                }

                WorldPilotDependencyFingerprint currentDependencies =
                    WorldPilotDependencyFingerprintUtility.CalculateCurrent();
                if (string.IsNullOrWhiteSpace(route.DependencyFingerprint) ||
                    !string.Equals(
                        route.DependencyFingerprint,
                        currentDependencies.Value,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        evidence.dependencyFingerprint,
                        currentDependencies.Value,
                        StringComparison.Ordinal))
                {
                    errors.Add(
                        "Traversal evidence is stale for the recursive playtest, production-cell, player, or script dependency closure.");
                }
            }
            catch (Exception exception)
            {
                errors.Add("Current traversal authoring cannot be inspected: " + exception.Message);
            }
            finally
            {
                if (closeWhenDone && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }

        private static WorldPilotTraversalEvidenceReadResult Result(
            bool executed,
            ICollection<string> errors,
            ICollection<string> warnings,
            string evidence) =>
            new WorldPilotTraversalEvidenceReadResult(
                executed,
                executed && errors.Count == 0,
                evidence,
                errors,
                warnings);

        private static void ValidateFileHash(
            string relativePath,
            string recordedHash,
            string label,
            ICollection<string> errors)
        {
            string absolutePath = Path.GetFullPath(relativePath);
            if (!File.Exists(absolutePath))
            {
                errors.Add("Current " + label + " file is missing: " + relativePath);
                return;
            }

            string currentHash = CalculateFileSha256(absolutePath);
            if (!string.Equals(currentHash, recordedHash, StringComparison.Ordinal))
            {
                errors.Add("Traversal evidence SHA-256 is stale for the current " + label + ".");
            }
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

        [Serializable]
        private sealed class WorldPilotTraversalEvidenceDto
        {
            public int schemaVersion = 0;
            public string validatorId = string.Empty;
            public bool passed = false;
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
            public bool noTeleportVerified = false;
            public bool stallGuardVerified = false;
            public bool crouchPassageVerified = false;
            public bool returnToStartVerified = false;
            public bool verticalReachVerified = false;
            public int traversalFrameCount = 0;
            public float cumulativeHorizontalDistanceMeters = 0f;
            public float maximumFrameHorizontalDisplacementMeters = 0f;
            public float maximumWaypointVerticalDeviationMeters = 0f;
            public float routeExtentXMeters = 0f;
            public float routeExtentZMeters = 0f;
        }
    }
}
