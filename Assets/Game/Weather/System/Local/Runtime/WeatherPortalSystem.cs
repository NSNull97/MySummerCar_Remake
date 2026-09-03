using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace MSC.Weather.System.Local
{
    public readonly struct PortalTransmission : IEquatable<PortalTransmission>
    {
        public PortalTransmission(
            float visual01,
            float audio01,
            float wind01,
            float fog01,
            float precipitation01,
            float thunder01)
        {
            Visual01 = Mathf.Clamp01(visual01);
            Audio01 = Mathf.Clamp01(audio01);
            Wind01 = Mathf.Clamp01(wind01);
            Fog01 = Mathf.Clamp01(fog01);
            Precipitation01 = Mathf.Clamp01(precipitation01);
            Thunder01 = Mathf.Clamp01(thunder01);
        }

        public float Visual01 { get; }
        public float Audio01 { get; }
        public float Wind01 { get; }
        public float Fog01 { get; }
        public float Precipitation01 { get; }
        public float Thunder01 { get; }

        public static PortalTransmission Blocked => default;
        public static PortalTransmission Open =>
            new PortalTransmission(1f, 1f, 1f, 1f, 1f, 1f);

        public static PortalTransmission Multiply(
            in PortalTransmission left,
            in PortalTransmission right) => new PortalTransmission(
                left.Visual01 * right.Visual01,
                left.Audio01 * right.Audio01,
                left.Wind01 * right.Wind01,
                left.Fog01 * right.Fog01,
                left.Precipitation01 * right.Precipitation01,
                left.Thunder01 * right.Thunder01);

        public static PortalTransmission Max(
            in PortalTransmission left,
            in PortalTransmission right) => new PortalTransmission(
                Mathf.Max(left.Visual01, right.Visual01),
                Mathf.Max(left.Audio01, right.Audio01),
                Mathf.Max(left.Wind01, right.Wind01),
                Mathf.Max(left.Fog01, right.Fog01),
                Mathf.Max(left.Precipitation01, right.Precipitation01),
                Mathf.Max(left.Thunder01, right.Thunder01));

        public bool Equals(PortalTransmission other) =>
            Visual01.Equals(other.Visual01) &&
            Audio01.Equals(other.Audio01) &&
            Wind01.Equals(other.Wind01) &&
            Fog01.Equals(other.Fog01) &&
            Precipitation01.Equals(other.Precipitation01) &&
            Thunder01.Equals(other.Thunder01);

        public override bool Equals(object obj) =>
            obj is PortalTransmission other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Visual01.GetHashCode();
                hash = (hash * 397) ^ Audio01.GetHashCode();
                hash = (hash * 397) ^ Wind01.GetHashCode();
                hash = (hash * 397) ^ Fog01.GetHashCode();
                hash = (hash * 397) ^ Precipitation01.GetHashCode();
                hash = (hash * 397) ^ Thunder01.GetHashCode();
                return hash;
            }
        }
    }

    public interface IEnvironmentPortal
    {
        string StableId { get; }
        IInteriorZone ZoneA { get; }
        IInteriorZone ZoneB { get; }
        Transform OpeningTransform { get; }
        Vector2 OpeningSizeMeters { get; }
        float Openness01 { get; }
        PortalTransmission Transmission { get; }
        bool IsAvailable { get; }
        event Action<IEnvironmentPortal> TransmissionChanged;
    }

    /// <summary>
    /// Event-driven, streaming-safe strongest-path graph rooted at Outdoor.
    /// Independent channels may select different paths through the same zones.
    /// </summary>
    [DefaultExecutionOrder(-450)]
    [DisallowMultipleComponent]
    public sealed class WeatherPortalSystem : MonoBehaviour
    {
        public const string OutdoorZoneId = "environment.zone.outdoor";

        private static readonly ProfilerMarker GraphMarker =
            new ProfilerMarker("Weather.PortalGraph");

        private readonly List<IInteriorZone> zones = new List<IInteriorZone>(32);
        private readonly List<IEnvironmentPortal> portals =
            new List<IEnvironmentPortal>(48);
        private readonly Dictionary<string, IInteriorZone> zonesById =
            new Dictionary<string, IInteriorZone>(StringComparer.Ordinal);
        private readonly Dictionary<string, IEnvironmentPortal> portalsById =
            new Dictionary<string, IEnvironmentPortal>(StringComparer.Ordinal);
        private readonly Dictionary<IInteriorZone, int> zoneIndices =
            new Dictionary<IInteriorZone, int>();

        private PortalTransmission[] strongest =
            new PortalTransmission[16];
        private IEnvironmentPortal[] strongestVisualPortal =
            new IEnvironmentPortal[16];
        private bool graphDirty = true;
        private uint graphRevision;
        private int graphZoneCount;
        private bool subscribedToOwnershipChanges;

        public static WeatherPortalSystem Active { get; private set; }
        public static event Action<WeatherPortalSystem> ActiveChanged;

        public IReadOnlyList<IInteriorZone> Zones => zones;
        public IReadOnlyList<IEnvironmentPortal> Portals => portals;
        public uint GraphRevision => graphRevision;
        public bool IsGraphDirty => graphDirty;

        private void Awake()
        {
            if (!subscribedToOwnershipChanges)
            {
                ActiveChanged += HandleActiveChanged;
                subscribedToOwnershipChanges = true;
            }

            TryClaimOwnership();
        }

        private void OnEnable()
        {
            TryClaimOwnership();
        }

        private void OnDisable()
        {
            ReleaseOwnership();
        }

        private void LateUpdate()
        {
            if (Active != this)
            {
                return;
            }

            EnsureGraph();
        }

        private void OnDestroy()
        {
            if (subscribedToOwnershipChanges)
            {
                ActiveChanged -= HandleActiveChanged;
                subscribedToOwnershipChanges = false;
            }

            ReleaseOwnership();
        }

        private void TryClaimOwnership()
        {
            if (!isActiveAndEnabled || Active == this || Active != null)
            {
                return;
            }

            Active = this;
            graphDirty = true;
            ActiveChanged?.Invoke(this);
        }

        private void ReleaseOwnership()
        {
            if (Active != this)
            {
                return;
            }

            Active = null;
            ActiveChanged?.Invoke(null);
        }

        private void HandleActiveChanged(WeatherPortalSystem owner)
        {
            if (owner == null)
            {
                TryClaimOwnership();
            }
        }

        public bool RegisterZone(IInteriorZone zone)
        {
            if (!IsAlive(zone) || string.IsNullOrWhiteSpace(zone.StableId))
            {
                return false;
            }

            if (zonesById.TryGetValue(zone.StableId, out IInteriorZone existing))
            {
                if (ReferenceEquals(existing, zone))
                {
                    return true;
                }

                Debug.LogError(
                    $"Duplicate interior weather zone ID '{zone.StableId}'.",
                    zone as UnityEngine.Object);
                return false;
            }

            zones.Add(zone);
            zonesById.Add(zone.StableId, zone);
            MarkGraphDirty();
            return true;
        }

        public bool UnregisterZone(IInteriorZone zone)
        {
            if (zone == null)
            {
                return false;
            }

            bool removed = zones.Remove(zone);
            if (removed && zonesById.TryGetValue(zone.StableId, out IInteriorZone existing) &&
                ReferenceEquals(existing, zone))
            {
                zonesById.Remove(zone.StableId);
            }

            if (removed)
            {
                MarkGraphDirty();
            }

            return removed;
        }

        public bool RegisterPortal(IEnvironmentPortal portal)
        {
            if (!IsAlive(portal) || string.IsNullOrWhiteSpace(portal.StableId))
            {
                return false;
            }

            if (portalsById.TryGetValue(
                    portal.StableId,
                    out IEnvironmentPortal existing))
            {
                if (ReferenceEquals(existing, portal))
                {
                    return true;
                }

                Debug.LogError(
                    $"Duplicate environment portal ID '{portal.StableId}'.",
                    portal as UnityEngine.Object);
                return false;
            }

            portals.Add(portal);
            portalsById.Add(portal.StableId, portal);
            portal.TransmissionChanged += HandlePortalChanged;
            MarkGraphDirty();
            return true;
        }

        public bool UnregisterPortal(IEnvironmentPortal portal)
        {
            if (portal == null)
            {
                return false;
            }

            portal.TransmissionChanged -= HandlePortalChanged;
            bool removed = portals.Remove(portal);
            if (removed && portalsById.TryGetValue(
                    portal.StableId,
                    out IEnvironmentPortal existing) &&
                ReferenceEquals(existing, portal))
            {
                portalsById.Remove(portal.StableId);
            }

            if (removed)
            {
                MarkGraphDirty();
            }

            return removed;
        }

        public bool TryResolveOutdoorTransmission(
            IInteriorZone zone,
            out PortalTransmission result)
        {
            EnsureGraph();
            if (zone == null)
            {
                result = PortalTransmission.Open;
                return true;
            }

            if (!zoneIndices.TryGetValue(zone, out int index) ||
                index < 1 || index >= graphZoneCount)
            {
                result = PortalTransmission.Blocked;
                return false;
            }

            result = strongest[index];
            return true;
        }

        public bool TryGetStrongestVisualPortal(
            IInteriorZone zone,
            out IEnvironmentPortal portal)
        {
            EnsureGraph();
            if (zone != null && zoneIndices.TryGetValue(zone, out int index) &&
                index >= 1 && index < graphZoneCount)
            {
                portal = strongestVisualPortal[index];
                return IsAlive(portal);
            }

            portal = null;
            return false;
        }

        /// <summary>
        /// Writes the strongest visual path from a zone toward Outdoor into a
        /// caller-owned buffer. The buffer is reused by debug tooling.
        /// </summary>
        public bool TryBuildStrongestVisualPath(
            IInteriorZone zone,
            IList<IEnvironmentPortal> output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            output.Clear();
            EnsureGraph();
            IInteriorZone current = zone;
            for (int step = 0; current != null && step < graphZoneCount; step++)
            {
                if (!zoneIndices.TryGetValue(current, out int index) ||
                    index < 1 || index >= graphZoneCount)
                {
                    output.Clear();
                    return false;
                }

                IEnvironmentPortal portal = strongestVisualPortal[index];
                if (!IsAlive(portal) || output.Contains(portal))
                {
                    output.Clear();
                    return false;
                }

                output.Add(portal);
                current = ReferenceEquals(portal.ZoneA, current)
                    ? portal.ZoneB
                    : ReferenceEquals(portal.ZoneB, current)
                        ? portal.ZoneA
                        : null;
            }

            return current == null && output.Count > 0;
        }

        public bool TryGetNearestPortal(
            IInteriorZone zone,
            Vector3 worldPosition,
            out IEnvironmentPortal nearest,
            out float distanceMeters)
        {
            nearest = null;
            distanceMeters = float.PositiveInfinity;
            if (zone == null)
            {
                return false;
            }

            for (int index = 0; index < portals.Count; index++)
            {
                IEnvironmentPortal portal = portals[index];
                if (!IsAlive(portal) || !portal.IsAvailable ||
                    (!ReferenceEquals(portal.ZoneA, zone) &&
                     !ReferenceEquals(portal.ZoneB, zone)) ||
                    portal.OpeningTransform == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(
                    worldPosition,
                    portal.OpeningTransform.position);
                if (distance < distanceMeters)
                {
                    distanceMeters = distance;
                    nearest = portal;
                }
            }

            return nearest != null;
        }

        public void MarkGraphDirty()
        {
            graphDirty = true;
        }

        private void HandlePortalChanged(IEnvironmentPortal portal)
        {
            MarkGraphDirty();
        }

        private void EnsureGraph()
        {
            if (!graphDirty)
            {
                return;
            }

            using (GraphMarker.Auto())
            {
                zoneIndices.Clear();
                int count = 1;
                for (int index = 0; index < zones.Count; index++)
                {
                    IInteriorZone zone = zones[index];
                    if (!IsAlive(zone) || !zone.IsAvailable)
                    {
                        continue;
                    }

                    zoneIndices[zone] = count++;
                }

                EnsureCapacity(count);
                graphZoneCount = count;
                for (int index = 0; index < count; index++)
                {
                    strongest[index] = PortalTransmission.Blocked;
                    strongestVisualPortal[index] = null;
                }

                strongest[0] = PortalTransmission.Open;
                for (int pass = 0; pass < count - 1; pass++)
                {
                    bool changed = false;
                    for (int portalIndex = 0;
                         portalIndex < portals.Count;
                         portalIndex++)
                    {
                        IEnvironmentPortal portal = portals[portalIndex];
                        if (!IsAlive(portal) || !portal.IsAvailable ||
                            !TryResolveIndex(portal.ZoneA, out int a) ||
                            !TryResolveIndex(portal.ZoneB, out int b) ||
                            a == b)
                        {
                            continue;
                        }

                        PortalTransmission edge = portal.Transmission;
                        changed |= Relax(a, b, edge, portal);
                        changed |= Relax(b, a, edge, portal);
                    }

                    if (!changed)
                    {
                        break;
                    }
                }

                graphDirty = false;
                graphRevision = graphRevision == uint.MaxValue
                    ? 1U
                    : graphRevision + 1U;
            }
        }

        private bool Relax(
            int source,
            int destination,
            in PortalTransmission edge,
            IEnvironmentPortal portal)
        {
            PortalTransmission candidate = PortalTransmission.Multiply(
                strongest[source],
                edge);
            PortalTransmission previous = strongest[destination];
            PortalTransmission combined = PortalTransmission.Max(previous, candidate);
            if (combined.Equals(previous))
            {
                return false;
            }

            if (candidate.Visual01 > previous.Visual01)
            {
                strongestVisualPortal[destination] = portal;
            }

            strongest[destination] = combined;
            return true;
        }

        private bool TryResolveIndex(IInteriorZone zone, out int index)
        {
            if (zone == null)
            {
                index = 0;
                return true;
            }

            return zoneIndices.TryGetValue(zone, out index);
        }

        private void EnsureCapacity(int count)
        {
            if (strongest.Length >= count)
            {
                return;
            }

            int capacity = Mathf.NextPowerOfTwo(count);
            Array.Resize(ref strongest, capacity);
            Array.Resize(ref strongestVisualPortal, capacity);
        }

        private static bool IsAlive(object value)
        {
            if (value == null)
            {
                return false;
            }

            return !(value is UnityEngine.Object unityObject) || unityObject != null;
        }
    }
}
