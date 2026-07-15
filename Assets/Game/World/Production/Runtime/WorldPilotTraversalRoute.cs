using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MSC.World.Remaster
{
    [Serializable]
    public struct WorldPilotTraversalWaypoint
    {
        [SerializeField] private string waypointId;
        [SerializeField] private Vector3 localPosition;
        [SerializeField, Min(0.05f)] private float reachToleranceMeters;
        [SerializeField] private bool requireGrounded;
        [SerializeField] private bool requireCrouching;

        public WorldPilotTraversalWaypoint(
            string id,
            Vector3 position,
            float reachTolerance,
            bool grounded,
            bool crouching)
        {
            waypointId = id ?? string.Empty;
            localPosition = position;
            reachToleranceMeters = Mathf.Max(0.05f, reachTolerance);
            requireGrounded = grounded;
            requireCrouching = crouching;
        }

        public string WaypointId => waypointId;
        public Vector3 LocalPosition => localPosition;
        public float ReachToleranceMeters => reachToleranceMeters;
        public float VerticalReachToleranceMeters => Mathf.Max(0.3f, reachToleranceMeters);
        public bool RequireGrounded => requireGrounded;
        public bool RequireCrouching => requireCrouching;
    }

    [DisallowMultipleComponent]
    public sealed class WorldPilotTraversalRoute : MonoBehaviour
    {
        public const int CurrentSchemaVersion = 1;
        public const string CurrentRouteId = "m05b1-home-garage-house";
        public const string AcceptedRouteFingerprint =
            "f8c915039e48f3c5f8f8fe1a2a8f75b84bca614c01f8505720a69cd402e2e1da";

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string routeId = CurrentRouteId;
        [SerializeField] private string zoneId = string.Empty;
        [SerializeField] private string playerPrefabGuid = string.Empty;
        [SerializeField, HideInInspector] private string dependencyFingerprint = string.Empty;
        [SerializeField] private Transform referenceFrame;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private CharacterController playerController;
        [SerializeField] private WorldHingedArchitecture[] doorsToOpen = Array.Empty<WorldHingedArchitecture>();
        [SerializeField] private WorldPilotTraversalWaypoint[] waypoints = Array.Empty<WorldPilotTraversalWaypoint>();

        public int SchemaVersion => schemaVersion;
        public string RouteId => routeId;
        public string ZoneId => zoneId;
        public string PlayerPrefabGuid => playerPrefabGuid;
        public string DependencyFingerprint => dependencyFingerprint;
        public Transform ReferenceFrame => referenceFrame;
        public Transform PlayerRoot => playerRoot;
        public CharacterController PlayerController => playerController;
        public IReadOnlyList<WorldHingedArchitecture> DoorsToOpen => doorsToOpen;
        public IReadOnlyList<WorldPilotTraversalWaypoint> Waypoints => waypoints;

        public void Configure(
            string routeZoneId,
            string sourcePlayerPrefabGuid,
            Transform routeReferenceFrame,
            Transform routePlayerRoot,
            CharacterController routePlayerController,
            WorldHingedArchitecture[] requiredOpenDoors,
            WorldPilotTraversalWaypoint[] routeWaypoints)
        {
            schemaVersion = CurrentSchemaVersion;
            routeId = CurrentRouteId;
            zoneId = routeZoneId ?? string.Empty;
            playerPrefabGuid = sourcePlayerPrefabGuid ?? string.Empty;
            dependencyFingerprint = string.Empty;
            referenceFrame = routeReferenceFrame;
            playerRoot = routePlayerRoot;
            playerController = routePlayerController;
            doorsToOpen = requiredOpenDoors ?? Array.Empty<WorldHingedArchitecture>();
            waypoints = routeWaypoints ?? Array.Empty<WorldPilotTraversalWaypoint>();
        }

#if UNITY_EDITOR
        public void ConfigureDependencyFingerprintForAuthoring(string fingerprint)
        {
            dependencyFingerprint = fingerprint ?? string.Empty;
        }
#endif

        public Vector3 GetWorldPosition(int waypointIndex)
        {
            if (referenceFrame == null)
            {
                throw new InvalidOperationException("Traversal route reference frame is missing.");
            }

            if (waypointIndex < 0 || waypointIndex >= waypoints.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(waypointIndex));
            }

            return referenceFrame.TransformPoint(waypoints[waypointIndex].LocalPosition);
        }

        public string CalculateFingerprint()
        {
            var canonical = new StringBuilder();
            canonical.Append(schemaVersion).Append('|')
                .Append(routeId).Append('|')
                .Append(zoneId).Append('|')
                .Append(playerPrefabGuid).Append('|');

            foreach (WorldHingedArchitecture door in doorsToOpen)
            {
                canonical.Append(door != null ? door.name : "<missing>").Append('|');
            }

            foreach (WorldPilotTraversalWaypoint waypoint in waypoints)
            {
                Vector3 position = waypoint.LocalPosition;
                canonical.Append(waypoint.WaypointId).Append(':')
                    .Append(position.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(position.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(position.z.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                    .Append(waypoint.ReachToleranceMeters.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                    .Append(waypoint.RequireGrounded ? '1' : '0').Append(':')
                    .Append(waypoint.RequireCrouching ? '1' : '0').Append('|');
            }

            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
            var result = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
            {
                result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return result.ToString();
        }
    }
}
