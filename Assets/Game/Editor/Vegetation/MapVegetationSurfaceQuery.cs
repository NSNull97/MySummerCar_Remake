using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MSC.Interaction.Capabilities;
using MSC.LegacyImport;
using MSC.Traffic;
using MSC.World.Vegetation;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    public readonly struct MapVegetationSurfaceHit
    {
        public Vector3 Position { get; }
        public Vector3 Normal { get; }
        public string Source { get; }
        public Material Material { get; }
        public string SurfaceCategory { get; }
        public int Layer { get; }
        private readonly int allowedChannels;
        public bool AllowsChannel(VegetationDensityChannel channel) => (allowedChannels & (1 << (int)channel)) != 0;
        internal MapVegetationSurfaceHit(Vector3 position, Vector3 normal, SurfaceInfo info)
        { Position = position; Normal = normal; Source = info.Source; Material = info.Material; SurfaceCategory = info.Category; Layer = info.Layer; allowedChannels = info.DensityChannels; }
    }

    public readonly struct MapVegetationExclusionDebugBounds
    {
        public Bounds Bounds { get; }
        public MapVegetationExclusionKind Category { get; }
        public string Source { get; }
        public float MaximumMargin { get; }
        internal MapVegetationExclusionDebugBounds(Bounds bounds, MapVegetationExclusionKind category, string source, float margin)
        { Bounds = bounds; Category = category; Source = source; MaximumMargin = margin; }
    }

    internal readonly struct SurfaceInfo
    {
        public readonly string Source, Category, MaterialIdentity;
        public readonly Material Material;
        public readonly int Layer, Channels, DensityChannels;
        public readonly float MaximumSlope;
        public readonly MapVegetationGrassTextureMask.Binding GrassTexture;
        public SurfaceInfo(string source, string category, Material material, int layer, VegetationSurface marker,
            MapVegetationGrassTextureMask.Binding grassTexture = null)
        {
            Source = source; Category = category; Material = material; Layer = layer;
            GrassTexture = grassTexture;
            MaterialIdentity = string.Empty;
            if (material != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(material);
                MaterialIdentity = !string.IsNullOrEmpty(assetPath) && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out string guid, out long localId)
                    ? guid + ":" + localId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + AssetDatabase.GetAssetDependencyHash(assetPath)
                    : "nonpersistent:" + material.name + ":" + (material.shader != null ? material.shader.name : string.Empty);
            }
            DensityChannels = marker == null ? -1 :
                (marker.Allows(VegetationDensityChannel.ShortGrass) ? 1 : 0) |
                (marker.Allows(VegetationDensityChannel.MeadowGrass) ? 2 : 0) |
                (marker.Allows(VegetationDensityChannel.TallGrass) ? 4 : 0) |
                (marker.Allows(VegetationDensityChannel.Decorative) ? 8 : 0);
            Channels = ((DensityChannels & 8) != 0 ? 3 : 0) | ((DensityChannels & 7) != 0 ? 4 : 0);
            MaximumSlope = marker == null ? 89f : marker.SlopeOverrideDegrees;
        }
    }

    /// <summary>
    /// Editor-only downward mesh ray query and exclusion footprint index.
    /// All scene geometry is copied, so callers may unload each input cell after AddRoots.
    /// Ground opt-in and exclusion provenance are independent; an opt-in never overrides a road or roof.
    /// </summary>
    public sealed class MapVegetationSurfaceQuery
    {
        private readonly MapVegetationPlacementSettings settings;
        private readonly MapVegetationGrassTextureMask grassTextureMask;
        private readonly float[,] capturedMargins;
        private readonly float[] maximumMargins;
        private readonly Dictionary<Vector2Int, List<GroundTriangle>> ground = new Dictionary<Vector2Int, List<GroundTriangle>>();
        private readonly Dictionary<Vector2Int, List<Footprint>> exclusions = new Dictionary<Vector2Int, List<Footprint>>();
        private readonly List<GroundTriangle> wideGround = new List<GroundTriangle>();
        private readonly List<Footprint> wideExclusions = new List<Footprint>();
        private readonly List<GroundTriangle> groundPrimitives = new List<GroundTriangle>();
        private readonly List<Footprint> exclusionPrimitives = new List<Footprint>();
        private readonly List<TerrainSnapshot> terrains = new List<TerrainSnapshot>();
        private readonly List<VolumeSnapshot> volumes = new List<VolumeSnapshot>();
        private readonly List<Bounds> groundBounds = new List<Bounds>();
        private readonly List<MapVegetationExclusionDebugBounds> debugBounds = new List<MapVegetationExclusionDebugBounds>();
        private readonly List<string> warnings = new List<string>();
        private ulong waterVisitStamp;
        private long resolveCalls, waterChecks, exposureChecks, waterTriangleVisits, waterTriangleDuplicates;
        private const int MaximumBucketsPerPrimitive = 4096;
        private const float WaterSurfaceToleranceMeters = 0.02f;
        private static readonly int[] BoxTriangleIndices =
        { 2, 6, 7, 2, 7, 3, 0, 1, 5, 0, 5, 4, 0, 2, 3, 0, 3, 1, 4, 5, 7, 4, 7, 6, 0, 4, 6, 0, 6, 2, 1, 3, 7, 1, 7, 5 };

        public IReadOnlyList<Bounds> GroundBounds => groundBounds;
        public IReadOnlyList<MapVegetationExclusionDebugBounds> ExclusionDebugBounds => debugBounds;
        public IReadOnlyList<string> Warnings => warnings;
        public int GroundTriangleCount { get; private set; }
        public int SurfaceCount => groundBounds.Count;
        public int ExclusionCount { get; private set; }
        public long ResolveCallCount => resolveCalls;
        public string GrassTextureMaskDiagnosticSummary => grassTextureMask.DiagnosticSummary;
        public IReadOnlyList<string> GrassTextureMaskCoverage => grassTextureMask.Coverage;
        public string DiagnosticSummary => $"ground={groundPrimitives.Count} wideGround={wideGround.Count} groundBuckets={ground.Count} exclusions={exclusionPrimitives.Count} wideExclusions={wideExclusions.Count} resolveCalls={resolveCalls} waterChecks={waterChecks} exposureChecks={exposureChecks} waterTriangleVisits={waterTriangleVisits} waterTriangleDuplicates={waterTriangleDuplicates} {GrassTextureMaskDiagnosticSummary}";

        private MapVegetationSurfaceQuery(MapVegetationPlacementSettings configuredSettings)
        {
            settings = configuredSettings != null ? configuredSettings : throw new ArgumentNullException(nameof(configuredSettings));
            grassTextureMask = new MapVegetationGrassTextureMask(settings, warnings);
            int exclusionCount = Enum.GetValues(typeof(MapVegetationExclusionKind)).Length;
            capturedMargins = new float[Enum.GetValues(typeof(MapVegetationKind)).Length, exclusionCount];
            maximumMargins = new float[exclusionCount];
            foreach (MapVegetationKind kind in Enum.GetValues(typeof(MapVegetationKind)))
                foreach (MapVegetationExclusionKind exclusion in Enum.GetValues(typeof(MapVegetationExclusionKind)))
                {
                    float margin = settings.GetMargin(kind, exclusion);
                    capturedMargins[(int)kind, (int)exclusion] = margin;
                    maximumMargins[(int)exclusion] = Mathf.Max(maximumMargins[(int)exclusion], margin);
                }
            foreach (MapVegetationAuthoredExclusion volume in settings.AuthoredExclusions)
                if (volume != null) AddBounds(volume.WorldBounds, volume.Category, "Authored: " + volume.Label);
            AddTrafficRoutes(settings.TrafficNetwork);
        }

        public static MapVegetationSurfaceQuery Build(IEnumerable<GameObject> explicitRoots, MapVegetationPlacementSettings settings)
        {
            var result = new MapVegetationSurfaceQuery(settings);
            result.AddRoots(explicitRoots);
            return result;
        }

        /// <summary>
        /// Fingerprints the accepted source geometry and query semantics after all AddRoots calls.
        /// Scene traversal order, native object IDs and lazily populated shoreline caches are excluded.
        /// </summary>
        public string ComputeGeometryFingerprint()
        {
            var sortedGround = new List<GroundTriangle>(groundPrimitives);
            sortedGround.Sort(GroundTriangle.Compare);
            var sortedExclusions = new List<Footprint>(exclusionPrimitives);
            sortedExclusions.Sort(Footprint.Compare);
            // Terrain arrays can be large; hash them directly into child digests rather
            // than constructing a second serialized copy or retaining native assets.
            var terrainHashes = new List<string>(terrains.Count);
            foreach (TerrainSnapshot terrain in terrains) terrainHashes.Add(Hash(terrain.WriteFingerprint));
            terrainHashes.Sort(StringComparer.Ordinal);
            var volumeHashes = new List<string>(volumes.Count);
            foreach (VolumeSnapshot volume in volumes) volumeHashes.Add(Hash(volume.WriteFingerprint));
            volumeHashes.Sort(StringComparer.Ordinal);
            return Hash(writer =>
            {
                writer.Write("msc-map-vegetation-surfaces-v3");
                writer.Write(grassTextureMask.SettingsFingerprint);
                writer.Write(WaterSurfaceToleranceMeters);
                writer.Write(settings.WorldHeightRange.x); writer.Write(settings.WorldHeightRange.y);
                foreach (MapVegetationKind kind in Enum.GetValues(typeof(MapVegetationKind)))
                {
                    writer.Write((int)kind);
                    writer.Write(settings.Category(kind).MaximumSlopeDegrees);
                    writer.Write(settings.Category(kind).SurfaceOffsetMeters);
                    foreach (MapVegetationExclusionKind exclusion in Enum.GetValues(typeof(MapVegetationExclusionKind)))
                    { writer.Write((int)exclusion); writer.Write(capturedMargins[(int)kind, (int)exclusion]); }
                }
                writer.Write(sortedGround.Count);
                foreach (GroundTriangle triangle in sortedGround) triangle.WriteFingerprint(writer);
                writer.Write(sortedExclusions.Count);
                foreach (Footprint exclusion in sortedExclusions) exclusion.WriteFingerprint(writer);
                writer.Write(terrainHashes.Count);
                foreach (string hash in terrainHashes) writer.Write(hash);
                writer.Write(volumeHashes.Count);
                foreach (string hash in volumeHashes) writer.Write(hash);
            });
        }

        private static string Hash(Action<BinaryWriter> write)
        {
            using (SHA256 hash = SHA256.Create())
            using (var stream = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                write(writer); writer.Flush(); stream.FlushFinalBlock();
                return BitConverter.ToString(hash.Hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static void WriteVector(BinaryWriter writer, Vector3 value)
        { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }

        private static void WriteMatrix(BinaryWriter writer, Matrix4x4 value)
        { for (int i = 0; i < 16; i++) writer.Write(value[i]); }

        private static void WriteSurface(BinaryWriter writer, SurfaceInfo info)
        {
            writer.Write(info.Source ?? string.Empty); writer.Write(info.Category ?? string.Empty);
            writer.Write(info.MaterialIdentity ?? string.Empty); writer.Write(info.Layer);
            writer.Write(info.Channels); writer.Write(info.DensityChannels); writer.Write(info.MaximumSlope);
            writer.Write(info.GrassTexture?.Fingerprint ?? string.Empty);
        }

        private static int CompareFloat(float a, float b) => BitConverter.SingleToInt32Bits(a).CompareTo(BitConverter.SingleToInt32Bits(b));
        private static int CompareVector(Vector3 a, Vector3 b)
        {
            int order = CompareFloat(a.x, b.x); if (order != 0) return order;
            order = CompareFloat(a.y, b.y); return order != 0 ? order : CompareFloat(a.z, b.z);
        }

        private static int CompareSurface(SurfaceInfo a, SurfaceInfo b)
        {
            int order = string.CompareOrdinal(a.Source, b.Source); if (order != 0) return order;
            order = string.CompareOrdinal(a.Category, b.Category); if (order != 0) return order;
            order = string.CompareOrdinal(a.MaterialIdentity, b.MaterialIdentity); if (order != 0) return order;
            order = a.Layer.CompareTo(b.Layer); if (order != 0) return order;
            order = a.Channels.CompareTo(b.Channels); if (order != 0) return order;
            order = a.DensityChannels.CompareTo(b.DensityChannels);
            if (order != 0) return order;
            order = CompareFloat(a.MaximumSlope, b.MaximumSlope);
            return order != 0 ? order : string.CompareOrdinal(a.GrassTexture?.Fingerprint, b.GrassTexture?.Fingerprint);
        }

        public void AddRoots(IEnumerable<GameObject> explicitRoots)
        {
            if (explicitRoots == null) throw new ArgumentNullException(nameof(explicitRoots));
            var visited = new HashSet<Transform>();
            foreach (GameObject root in explicitRoots)
            {
                if (root == null) continue;
                foreach (Transform target in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!visited.Add(target) || !target.gameObject.activeInHierarchy) continue;
                    var volume = target.GetComponent<VegetationExclusionVolume>();
                    if (volume != null && volume.enabled) volumes.Add(new VolumeSnapshot(volume));
                    var terrain = target.GetComponent<Terrain>();
                    if (terrain != null && terrain.enabled && terrain.terrainData != null) AddTerrain(terrain);
                    var filter = target.GetComponent<MeshFilter>();
                    var meshCollider = target.GetComponent<MeshCollider>();
                    Mesh mesh = filter != null ? filter.sharedMesh : meshCollider != null && meshCollider.enabled ? meshCollider.sharedMesh : null;
                    Renderer renderer = target.GetComponent<Renderer>();
                    // Hidden donor geometry and generated tree renderers are never reinterpreted as ground.
                    if (mesh != null && (renderer == null || renderer.enabled || meshCollider != null && meshCollider.enabled))
                        AddMesh(target, mesh, renderer != null ? renderer.sharedMaterials : Array.Empty<Material>());
                    else if (mesh == null)
                    {
                        Collider collider = target.GetComponent<Collider>();
                        if (collider != null && collider.enabled && !collider.isTrigger && !(collider is TerrainCollider)) AddCollider(collider);
                    }
                }
            }
        }

        public bool TryResolve(Vector3 sourceWorldPosition, MapVegetationKind kind, out MapVegetationSurfaceHit hit, out string reason)
        {
            resolveCalls++;
            hit = default;
            if (!Finite(sourceWorldPosition)) { reason = "NonFinitePosition"; return false; }
            Vector2 point = Xz(sourceWorldPosition);
            if (!TryGround(point, out Vector3 position, out Vector3 normal, out SurfaceInfo info, out GroundTriangle triangle))
            { reason = "NoAllowedGround"; return false; }
            hit = new MapVegetationSurfaceHit(position, normal, info);
            if ((info.Channels & (1 << (int)kind)) == 0) { reason = "SurfaceChannelDisabled:" + info.Source; return false; }
            if (TryGetExclusion(position, kind, out MapVegetationExclusionKind exclusion, out string source))
            { reason = exclusion + ":" + source; return false; }
            foreach (VolumeSnapshot volume in volumes)
                if (volume.Contains(position, kind)) { reason = "ExclusionVolume:" + volume.Source; return false; }
            float maximumSlope = Mathf.Min(settings.Category(kind).MaximumSlopeDegrees, info.MaximumSlope);
            if (Vector3.Angle(Vector3.up, normal) > maximumSlope + 0.001f)
            { reason = "ExcessiveSlope:" + info.Source; return false; }
            // Colour only rejects grass after the existing ground and exclusion
            // rules. A brown upper surface never reveals a lower green surface.
            if (kind == MapVegetationKind.Grass && !grassTextureMask.Allows(info.GrassTexture,
                triangle != null ? triangle.UvAt(point) : default, info.Source, out reason)) return false;
            position.y += settings.Category(kind).SurfaceOffsetMeters;
            hit = new MapVegetationSurfaceHit(position, normal, info);
            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// Evaluates authored exclusion footprints/volumes without requiring an
        /// opted-in ground triangle. Collisionless skyline continuation uses it
        /// only when no outer-map ground exists; roads, water and structures are
        /// still authoritative where their evidence reaches beyond the wall.
        /// </summary>
        public bool IsExcludedWithoutGround(Vector3 position,
            MapVegetationKind kind, out string reason)
        {
            if (!Finite(position))
            {
                reason = "NonFinitePosition";
                return true;
            }
            if (TryGetExclusion(position, kind,
                    out MapVegetationExclusionKind exclusion,
                    out string source))
            {
                reason = exclusion + ":" + source;
                return true;
            }
            foreach (VolumeSnapshot volume in volumes)
                if (volume.Contains(position, kind))
                {
                    reason = "ExclusionVolume:" + volume.Source;
                    return true;
                }
            reason = string.Empty;
            return false;
        }

        /// <summary>
        /// Fast texture-only migration filter for PREVIOUSLY validated grass records.
        /// Uses the highest approved ground at XZ, but deliberately does not repeat
        /// road/water/building/volume/slope checks. Never use as a placement query.
        /// </summary>
        public bool AllowsGrassGroundTexture(Vector3 worldPosition, out string reason)
        {
            reason = string.Empty;
            if (!Finite(worldPosition)) { reason = "NonFinitePosition"; return false; }
            if (!grassTextureMask.Enabled) return true;
            Vector2 point = Xz(worldPosition);
            if (!TryGround(point, out _, out _, out SurfaceInfo info, out GroundTriangle triangle))
            { reason = "NoAllowedGround"; return false; }
            return grassTextureMask.Allows(info.GrassTexture, triangle != null ? triangle.UvAt(point) : default, info.Source, out reason);
        }

        private bool TryGround(Vector2 point, out Vector3 position, out Vector3 normal, out SurfaceInfo info, out GroundTriangle triangle)
        {
            bool found = false;
            position = default; normal = default; info = default; triangle = null;
            if (ground.TryGetValue(Key(point), out List<GroundTriangle> candidates))
                FindHighest(candidates, point, ref found, ref position, ref normal, ref info, ref triangle);
            FindHighest(wideGround, point, ref found, ref position, ref normal, ref info, ref triangle);
            foreach (TerrainSnapshot terrain in terrains)
            {
                if (terrain.TryRaycast(point, out Vector3 candidate, out Vector3 terrainNormal) &&
                    candidate.y >= settings.WorldHeightRange.x && candidate.y <= settings.WorldHeightRange.y &&
                    (!found || candidate.y > position.y))
                { found = true; position = candidate; normal = terrainNormal; info = terrain.Info; triangle = null; }
            }
            return found;
        }

        public bool TryGetExclusion(Vector3 point, MapVegetationKind vegetation, out MapVegetationExclusionKind category, out string source)
        {
            if (exclusions.TryGetValue(Key(Xz(point)), out List<Footprint> candidates) && Blocks(candidates, point, vegetation, out category, out source)) return true;
            return Blocks(wideExclusions, point, vegetation, out category, out source);
        }

        public bool TryResolveGrass(Vector3 position, VegetationDensityChannel channel, out MapVegetationSurfaceHit hit, out string reason)
        {
            if (!TryResolve(position, MapVegetationKind.Grass, out hit, out reason)) return false;
            return AllowsGrassChannel(hit, channel, out reason);
        }

        /// <summary>Checks a profile channel on a successful Grass TryResolve hit without repeating geometry queries.</summary>
        public bool AllowsGrassChannel(MapVegetationSurfaceHit validatedHit, VegetationDensityChannel channel, out string reason)
        {
            if (!validatedHit.AllowsChannel(channel)) { reason = "SurfaceChannelDisabled:" + validatedHit.Source; return false; }
            // Resolve the profile's channel only after choosing its density profile.
            // A meadow-only exclusion must not remove valid short-grass candidates.
            Vector3 groundPosition = validatedHit.Position - Vector3.up * settings.Category(MapVegetationKind.Grass).SurfaceOffsetMeters;
            foreach (VolumeSnapshot volume in volumes)
                if (volume.ContainsChannel(groundPosition, channel)) { reason = "ExclusionVolume:" + volume.Source; return false; }
            reason = string.Empty;
            return true;
        }

        private bool Blocks(List<Footprint> candidates, Vector3 point, MapVegetationKind vegetation, out MapVegetationExclusionKind category, out string source)
        {
            foreach (Footprint candidate in candidates)
            {
                if (candidate.Category == MapVegetationExclusionKind.OpenSpace && vegetation == MapVegetationKind.Grass) continue;
                float margin = capturedMargins[(int)vegetation, (int)candidate.Category];
                if (candidate.Contains(Xz(point), margin) &&
                    (candidate.Category != MapVegetationExclusionKind.Water || WaterBlocks(candidate, point, margin)))
                { category = candidate.Category; source = candidate.Source; return true; }
            }
            category = MapVegetationExclusionKind.None; source = string.Empty; return false;
        }

        private bool WaterBlocks(Footprint water, Vector3 point, float margin)
        {
            waterChecks++;
            Vector2 xz = Xz(point);
            // Real lake tiles extend underneath raised land. Their projected
            // rectangle is not a shoreline and must not block all dry land above it.
            if (water.Contains(xz, 0f) && water.TryHeight(xz, out float waterY) &&
                point.y <= waterY + WaterSurfaceToleranceMeters) return true;
            if (margin <= 0f) return false;
            if (TouchesExposedWaterEdge(xz, water.A, water.B, water, margin) ||
                TouchesExposedWaterEdge(xz, water.B, water.C, water, margin) ||
                TouchesExposedWaterEdge(xz, water.C, water.A, water, margin)) return true;
            ulong visit = ++waterVisitStamp;
            Vector2Int min = Key(xz - Vector2.one * margin), max = Key(xz + Vector2.one * margin);
            for (int x = min.x; x <= max.x; x++) for (int z = min.y; z <= max.y; z++)
            {
                if (!ground.TryGetValue(new Vector2Int(x, z), out List<GroundTriangle> nearby)) continue;
                foreach (GroundTriangle triangle in nearby)
                {
                    if (triangle.WaterVisitStamp == visit) { waterTriangleDuplicates++; continue; }
                    triangle.WaterVisitStamp = visit;
                    if (TouchesWetGround(water, triangle, xz, margin)) return true;
                }
            }
            foreach (GroundTriangle triangle in wideGround)
                if (TouchesWetGround(water, triangle, xz, margin)) return true;
            foreach (TerrainSnapshot terrain in terrains)
                foreach (GroundTriangle triangle in terrain.NearbyTriangles(xz, margin))
                    if (TouchesWetGround(water, triangle, xz, margin)) return true;
            return false;
        }

        private bool TouchesWetGround(Footprint water, GroundTriangle triangle, Vector2 point, float margin)
        {
            waterTriangleVisits++;
            if (point.x < triangle.Bounds.min.x - margin || point.x > triangle.Bounds.max.x + margin ||
                point.y < triangle.Bounds.min.z - margin || point.y > triangle.Bounds.max.z + margin) return false;
            // A steep bank may terminate above water without a whitelisted bed.
            // Check both sides of nearby ground edges against the actual highest
            // ground; ordinary internal triangulation edges have dry support on both sides.
            if (TouchesExposedWaterEdge(point, Xz(triangle.A), Xz(triangle.B), water, margin) ||
                TouchesExposedWaterEdge(point, Xz(triangle.B), Xz(triangle.C), water, margin) ||
                TouchesExposedWaterEdge(point, Xz(triangle.C), Xz(triangle.A), water, margin)) return true;
            Vector2[] wet = water.WetGroundPolygon(triangle);
            if (wet.Length == 0) return false;
            float radiusSquared = margin * margin;
            for (int i = 0; i < wet.Length; i++)
            {
                Vector2 a = wet[i], b = wet[(i + 1) % wet.Length], delta = b - a;
                float t = delta.sqrMagnitude > 0.000001f ? Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude) : 0f;
                Vector2 closest = a + delta * t;
                if ((point - closest).sqrMagnitude <= radiusSquared && IsExposedWater(closest, water)) return true;
            }
            return false;
        }

        private bool TouchesExposedWaterEdge(Vector2 point, Vector2 a, Vector2 b, Footprint water, float margin)
        {
            Vector2 delta = b - a;
            float t = delta.sqrMagnitude > 0.000001f ? Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude) : 0f;
            Vector2 closest = a + delta * t;
            if ((point - closest).sqrMagnitude > margin * margin) return false;
            if (IsExposedWater(closest, water)) return true;
            Vector2 side = delta.sqrMagnitude > 0.000001f ? new Vector2(-delta.y, delta.x).normalized * 0.025f : Vector2.zero;
            return IsExposedWater(closest + side, water) || IsExposedWater(closest - side, water);
        }

        private bool IsExposedWater(Vector2 point, Footprint water)
        {
            exposureChecks++;
            if (!water.Contains(point, 0f) || !water.TryHeight(point, out float waterY)) return false;
            bool found = false;
            Vector3 position = default, normal = default;
            SurfaceInfo info = default;
            if (ground.TryGetValue(Key(point), out List<GroundTriangle> candidates))
                FindHighest(candidates, point, ref found, ref position, ref normal, ref info);
            FindHighest(wideGround, point, ref found, ref position, ref normal, ref info);
            foreach (TerrainSnapshot terrain in terrains)
                if (terrain.TryRaycast(point, out Vector3 candidate, out _) && (!found || candidate.y > position.y))
                { found = true; position = candidate; }
            // Do not derive a shoreline from a submerged lower mesh when a higher,
            // dry approved ground surface covers it.
            return !found || position.y <= waterY + WaterSurfaceToleranceMeters + 0.001f;
        }

        private void FindHighest(List<GroundTriangle> candidates, Vector2 point, ref bool found, ref Vector3 position, ref Vector3 normal, ref SurfaceInfo info)
        {
            GroundTriangle ignored = null;
            FindHighest(candidates, point, ref found, ref position, ref normal, ref info, ref ignored);
        }

        private void FindHighest(List<GroundTriangle> candidates, Vector2 point, ref bool found, ref Vector3 position, ref Vector3 normal, ref SurfaceInfo info, ref GroundTriangle selected)
        {
            foreach (GroundTriangle triangle in candidates)
            {
                if (!triangle.TryRaycast(point, out float y) || y < settings.WorldHeightRange.x || y > settings.WorldHeightRange.y || found && y <= position.y) continue;
                found = true; position = new Vector3(point.x, y, point.y); normal = triangle.Normal; info = triangle.Info;
                selected = triangle;
            }
        }

        private void AddMesh(Transform target, Mesh mesh, Material[] materials)
        {
            GetMetadata(target, out string category, out string path, out string source);
            VegetationSurface marker = target.GetComponentInParent<VegetationSurface>();
            VegetationBlocker blocker = target.GetComponentInParent<VegetationBlocker>();
            bool generatedForest = string.Equals(path, "Phase1/ForestRemediation", StringComparison.Ordinal);
            bool interactive = !generatedForest && IsInteractive(target);
            var submeshExclusions = new MapVegetationExclusionKind[mesh.subMeshCount];
            var allowedSubmeshes = new bool[mesh.subMeshCount];
            bool hasRelevantGeometry = false;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                Material material = submesh < materials.Length ? materials[submesh] : null;
                MapVegetationExclusionKind exclusion = ResolveExclusion(target.gameObject, material, category, path, blocker, interactive);
                bool allowed = !generatedForest && exclusion == MapVegetationExclusionKind.None &&
                    (marker != null && marker.enabled || settings.IsAllowedGround(target.gameObject, material, path));
                submeshExclusions[submesh] = exclusion; allowedSubmeshes[submesh] = allowed;
                hasRelevantGeometry |= allowed || exclusion != MapVegetationExclusionKind.None;
            }
            // Old generated forest contains tens of thousands of shared high-detail meshes.
            // Classification must precede any CPU vertex access, including unreadable meshes.
            if (!hasRelevantGeometry) return;
            Vector3[] vertices;
            try { vertices = mesh.vertices; }
            catch (UnityException ex)
            {
                warnings.Add("Unreadable mesh: " + source + ": " + ex.Message);
                Renderer renderer = target.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var indexed = new HashSet<MapVegetationExclusionKind>();
                    foreach (MapVegetationExclusionKind exclusion in submeshExclusions)
                        if (exclusion != MapVegetationExclusionKind.None && indexed.Add(exclusion))
                            AddBounds(renderer.bounds, exclusion, source + " [conservative bounds: unreadable mesh]");
                }
                return;
            }
            Matrix4x4 matrix = target.localToWorldMatrix;
            // Mesh data is already readable here. Capture UVs while its owning
            // scene is loaded; no Unity mesh access is needed during migration.
            bool needsGroundUv = false;
            foreach (bool allowed in allowedSubmeshes) needsGroundUv |= allowed;
            Vector2[] uv = needsGroundUv ? mesh.uv : Array.Empty<Vector2>();
            bool hasUv = uv != null && uv.Length == vertices.Length;
            for (int vertex = 0; vertex < vertices.Length; vertex++) vertices[vertex] = matrix.MultiplyPoint3x4(vertices[vertex]);
            bool addedGround = false;
            Bounds meshBounds = default;
            if (vertices.Length > 0) { meshBounds = new Bounds(vertices[0], Vector3.zero); foreach (Vector3 vertex in vertices) meshBounds.Encapsulate(vertex); }
            var recordedDebug = new HashSet<MapVegetationExclusionKind>();
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles) continue;
                Material material = submesh < materials.Length ? materials[submesh] : null;
                MapVegetationExclusionKind exclusion = submeshExclusions[submesh];
                bool allowed = allowedSubmeshes[submesh];
                if (!allowed && exclusion == MapVegetationExclusionKind.None) continue;
                int[] indices;
                try { indices = mesh.GetTriangles(submesh); }
                catch (UnityException ex) { warnings.Add("Unreadable triangles: " + source + ": " + ex.Message); continue; }
                SurfaceInfo info = allowed ? new SurfaceInfo(source, string.IsNullOrEmpty(category) ? "ExplicitGround" : category, material, target.gameObject.layer, marker,
                    grassTextureMask.Capture(material, path, source, hasUv)) : default;
                for (int index = 0; index + 2 < indices.Length; index += 3)
                {
                    Vector3 a = vertices[indices[index]], b = vertices[indices[index + 1]], c = vertices[indices[index + 2]];
                    if (!Finite(a) || !Finite(b) || !Finite(c)) continue;
                    if (exclusion != MapVegetationExclusionKind.None) AddFootprint(new Footprint(a, b, c, exclusion, source));
                    else if (Mathf.Abs(Cross(Xz(b - a), Xz(c - a))) > 0.00001f)
                    {
                        var triangle = new GroundTriangle(a, b, c, info,
                            hasUv ? uv[indices[index]] : default, hasUv ? uv[indices[index + 1]] : default,
                            hasUv ? uv[indices[index + 2]] : default);
                        // Mirrored canonical transforms may reverse triangle winding. The
                        // explicitly approved ground is sampled from above as a two-sided surface.
                        Index(ground, wideGround, triangle, triangle.Bounds, 0f);
                        groundPrimitives.Add(triangle);
                        GroundTriangleCount++; addedGround = true;
                    }
                }
                if (exclusion != MapVegetationExclusionKind.None && recordedDebug.Add(exclusion))
                    debugBounds.Add(new MapVegetationExclusionDebugBounds(meshBounds, exclusion, source, maximumMargins[(int)exclusion]));
            }
            if (addedGround) groundBounds.Add(meshBounds);
        }

        private void AddCollider(Collider collider)
        {
            Transform target = collider.transform;
            GetMetadata(target, out string category, out string path, out string source);
            var marker = target.GetComponentInParent<VegetationSurface>();
            var blocker = target.GetComponentInParent<VegetationBlocker>();
            MapVegetationExclusionKind exclusion = ResolveExclusion(target.gameObject, null, category, path, blocker, IsInteractive(target));
            if (exclusion != MapVegetationExclusionKind.None)
            {
                if (collider is BoxCollider excludedBox)
                {
                    // A world AABB turns a long diagonal rail into kilometres of
                    // forbidden land. Project the actual oriented box faces instead.
                    Vector3[] corners = BoxCorners(excludedBox);
                    for (int i = 0; i < BoxTriangleIndices.Length; i += 3)
                        AddFootprint(new Footprint(corners[BoxTriangleIndices[i]], corners[BoxTriangleIndices[i + 1]],
                            corners[BoxTriangleIndices[i + 2]], exclusion, source));
                    debugBounds.Add(new MapVegetationExclusionDebugBounds(collider.bounds, exclusion, source, maximumMargins[(int)exclusion]));
                }
                else AddBounds(collider.bounds, exclusion, source);
                return;
            }
            if (marker == null && !settings.IsAllowedGround(target.gameObject, null, path)) return;
            if (collider is BoxCollider box)
            {
                Vector3[] vertices = BoxCorners(box);
                int[] indices = BoxTriangleIndices;
                var info = new SurfaceInfo(source, "ExplicitColliderGround", null, target.gameObject.layer, marker,
                    grassTextureMask.Capture(null, path, source, false));
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var triangle = new GroundTriangle(vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]], info);
                    if (Mathf.Abs(Cross(Xz(triangle.B - triangle.A), Xz(triangle.C - triangle.A))) <= 0.00001f) continue;
                    Index(ground, wideGround, triangle, triangle.Bounds, 0f); groundPrimitives.Add(triangle); GroundTriangleCount++;
                }
                groundBounds.Add(collider.bounds);
            }
            else warnings.Add("Unsupported explicit ground collider (use a mesh or Terrain): " + source + " " + collider.GetType().Name);
        }

        private static Vector3[] BoxCorners(BoxCollider box)
        {
            Vector3 center = box.center, half = box.size * 0.5f;
            var vertices = new Vector3[8];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = box.transform.TransformPoint(center + new Vector3(
                    (i & 1) == 0 ? -half.x : half.x, (i & 2) == 0 ? -half.y : half.y, (i & 4) == 0 ? -half.z : half.z));
            return vertices;
        }

        private void AddTerrain(Terrain terrain)
        {
            GetMetadata(terrain.transform, out string category, out string path, out string source);
            var marker = terrain.GetComponentInParent<VegetationSurface>();
            MapVegetationExclusionKind exclusion = ResolveExclusion(terrain.gameObject, terrain.materialTemplate, category, path, terrain.GetComponentInParent<VegetationBlocker>(), false);
            Bounds bounds = TransformBounds(terrain.terrainData.bounds, terrain.transform.localToWorldMatrix);
            if (exclusion != MapVegetationExclusionKind.None) { AddBounds(bounds, exclusion, source); return; }
            if (!settings.AllowTerrainSurfaces && marker == null && !settings.IsAllowedGround(terrain.gameObject, terrain.materialTemplate, path)) return;
            if (Quaternion.Angle(terrain.transform.rotation, Quaternion.identity) > 0.01f || terrain.transform.lossyScale.x <= 0f || terrain.transform.lossyScale.z <= 0f)
            { warnings.Add("Rotated/mirrored Terrain must be explicitly converted to mesh before placement: " + source); return; }
            terrains.Add(new TerrainSnapshot(terrain, new SurfaceInfo(source, "Terrain", terrain.materialTemplate, terrain.gameObject.layer, marker,
                grassTextureMask.Capture(terrain.materialTemplate, path, source, false))));
            groundBounds.Add(bounds);
        }

        private MapVegetationExclusionKind ResolveExclusion(GameObject target, Material material, string category, string path, VegetationBlocker blocker, bool interactive)
        {
            var configured = settings.ConfiguredExclusion(target, material);
            if (configured != MapVegetationExclusionKind.None) return configured;
            if (blocker != null && blocker.enabled)
            {
                switch (blocker.Kind)
                {
                    case VegetationBlockerKind.Water: return MapVegetationExclusionKind.Water;
                    case VegetationBlockerKind.GroundRoad: return MapVegetationExclusionKind.DirtRoad;
                    case VegetationBlockerKind.BridgeDeck:
                    case VegetationBlockerKind.BridgePillar: return MapVegetationExclusionKind.Bridge;
                    case VegetationBlockerKind.Building:
                    case VegetationBlockerKind.Foundation: return MapVegetationExclusionKind.Building;
                    default: return MapVegetationExclusionKind.ArtificialStructure;
                }
            }
            if (interactive) return MapVegetationExclusionKind.InteractiveObject;
            return ClassifyCanonical(category, path);
        }

        /// <summary>Donor names are provenance-only here; runtime never invokes this classification.</summary>
        public static MapVegetationExclusionKind ClassifyCanonical(string category, string path)
        {
            category = category ?? string.Empty; path = path ?? string.Empty;
            string leaf = path.Substring(path.LastIndexOf('/') + 1);
            // Under-map water colour/occlusion helpers project over dry land. They are
            // never accepted as ground, and true water-surface geometry owns exclusion.
            if (Contains(path, "WATERUNDER", "WATERCOLOR", "LAKEBED", "LAKESMALLBOTTOM", "LAKE_VEGETATION", "/FOLIAGE/")) return MapVegetationExclusionKind.None;
            if (Contains(category, "Water") || Contains(path, "/LAKEWATER", "/RIVER", "MAP/LakeSimple/Tile", "MAP/LakeNice/Lake/Tile")) return MapVegetationExclusionKind.Water;
            if (string.Equals(category, "Field", StringComparison.OrdinalIgnoreCase) ||
                Contains(category, "Agricultural", "Crop", "Ploughed", "Farmland") ||
                Contains(path, "/TERRAIN_OBJ/FIELDS", "/MESH/TRACKFIELD", "/STRAWBERRYFIELD/", "/CROPS/", "/FARMLAND/", "/PLOWED", "/PLOUGHED")) return MapVegetationExclusionKind.AgriculturalField;
            if (Contains(category, "Rail") || Contains(path, "/RAILROAD", "/RAILWAY")) return MapVegetationExclusionKind.Railway;
            if (Contains(category, "Bridge", "Tunnel") || Contains(path, "/BRIDGE", "/TUNNEL")) return MapVegetationExclusionKind.Bridge;
            if (Contains(category, "GarageOpening") || Contains(leaf, "garagedoor", "garage_door", "garageopening")) return MapVegetationExclusionKind.GarageOpening;
            if (Contains(category, "Door") || Contains(leaf, "door")) return MapVegetationExclusionKind.Door;
            if (Contains(category, "Gate") || Contains(leaf, "gate")) return MapVegetationExclusionKind.Gate;
            if (Contains(category, "Driveway") || Contains(path, "/DRIVEWAY", "/DRIVE_WAY")) return MapVegetationExclusionKind.Driveway;
            if (Contains(category, "DirtRoad", "ForestRoad", "Trail") || Contains(path, "/DIRTROAD", "/GRAVELROAD", "/FORESTROAD", "/TRAIL")) return MapVegetationExclusionKind.DirtRoad;
            if (Contains(category, "Road", "Parking") || Contains(path, "/TERRAIN_OBJ/ROAD", "/ASPHALT", "/PARKING")) return MapVegetationExclusionKind.AsphaltRoad;
            if (Contains(category, "OpenSpace", "Meadow", "Clearing", "Yard", "Hayfield")) return MapVegetationExclusionKind.OpenSpace;
            if (Contains(category, "VehicleRoute", "VehicleSpawn", "GameplayZone")) return MapVegetationExclusionKind.VehicleRoute;
            if (Contains(category, "Roof", "Floor", "Window", "Building", "Foundation", "Wall", "Interior")) return MapVegetationExclusionKind.Building;
            if (Contains(leaf, "house", "garage", "shed", "barn", "cottage", "building", "roof", "foundation", "stairs", "terrace", "pier", "jetty", "platform", "busstop")) return MapVegetationExclusionKind.ArtificialStructure;
            return MapVegetationExclusionKind.None;
        }

        private static bool Contains(string value, params string[] tokens)
        {
            foreach (string token in tokens) if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static void GetMetadata(Transform target, out string category, out string path, out string source)
        {
            var baseline = target.GetComponentInParent<DonorWorldBaselineEntityMetadata>();
            var supplemental = target.GetComponentInParent<DonorWorldSupplementalEntityMetadata>();
            if (supplemental != null && (baseline == null || supplemental.transform.IsChildOf(baseline.transform)))
            { category = string.Empty; path = supplemental.SourceHierarchyPath; source = supplemental.StableId + "|" + path; return; }
            if (baseline != null) { category = baseline.SemanticCategory; path = baseline.SourceHierarchyPath; source = baseline.StableId + "|" + path; return; }
            category = string.Empty; path = string.Empty; source = target.gameObject.scene.path + ":" + target.name;
        }

        private static bool IsInteractive(Transform target)
        {
            foreach (MonoBehaviour component in target.GetComponentsInParent<MonoBehaviour>(true))
                if (component is IContextInteractionTarget || component is IInteractionDisplayTarget) return true;
            return false;
        }

        private void AddTrafficRoutes(TrafficRoadNetworkCatalog catalog)
        {
            if (catalog == null) return;
            foreach (TrafficRouteDefinition route in catalog.Routes)
            {
                if (route == null || route.WorldPoints.Count < 2) continue;
                int count = route.ClosesLoop ? route.WorldPoints.Count : route.WorldPoints.Count - 1;
                for (int i = 0; i < count; i++)
                {
                    Vector3 a = route.WorldPoints[i], b = route.WorldPoints[(i + 1) % route.WorldPoints.Count];
                    AddFootprint(new Footprint(a, b, b, MapVegetationExclusionKind.VehicleRoute, route.RouteId, settings.TrafficRouteHalfWidthMeters));
                }
            }
        }

        private void AddBounds(Bounds bounds, MapVegetationExclusionKind category, string source)
        {
            if (category == MapVegetationExclusionKind.None || !Finite(bounds.min) || !Finite(bounds.max)) return;
            Vector3 a = bounds.min, b = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z), c = bounds.max, d = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
            if (category == MapVegetationExclusionKind.Water)
            { a.y = bounds.max.y; b.y = bounds.max.y; d.y = bounds.max.y; }
            AddFootprint(new Footprint(a, b, c, category, source)); AddFootprint(new Footprint(a, c, d, category, source));
            debugBounds.Add(new MapVegetationExclusionDebugBounds(bounds, category, source, maximumMargins[(int)category]));
        }

        private void AddFootprint(Footprint footprint)
        {
            Index(exclusions, wideExclusions, footprint, footprint.Bounds, maximumMargins[(int)footprint.Category] + footprint.ExtraMargin);
            exclusionPrimitives.Add(footprint);
            ExclusionCount++;
        }

        private void Index<T>(Dictionary<Vector2Int, List<T>> buckets, List<T> wide, T value, Bounds bounds, float margin)
        {
            Vector2Int min = Key(Xz(bounds.min) - Vector2.one * margin), max = Key(Xz(bounds.max) + Vector2.one * margin);
            long bucketCount = ((long)max.x - min.x + 1) * ((long)max.y - min.y + 1);
            if (bucketCount > MaximumBucketsPerPrimitive) { wide.Add(value); return; }
            for (int x = min.x; x <= max.x; x++) for (int z = min.y; z <= max.y; z++)
            {
                var key = new Vector2Int(x, z);
                if (!buckets.TryGetValue(key, out List<T> values)) { values = new List<T>(); buckets.Add(key, values); }
                values.Add(value);
            }
        }

        private Vector2Int Key(Vector2 point) => new Vector2Int(Mathf.FloorToInt(point.x / settings.SpatialIndexCellMeters), Mathf.FloorToInt(point.y / settings.SpatialIndexCellMeters));
        private static Vector2 Xz(Vector3 point) => new Vector2(point.x, point.z);
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        private static Bounds TriangleBounds(Vector3 a, Vector3 b, Vector3 c) { var value = new Bounds(a, Vector3.zero); value.Encapsulate(b); value.Encapsulate(c); return value; }
        private static Bounds TransformBounds(Bounds local, Matrix4x4 matrix)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++) result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? local.min.x : local.max.x, (i & 2) == 0 ? local.min.y : local.max.y, (i & 4) == 0 ? local.min.z : local.max.z)));
            return result;
        }

        private sealed class GroundTriangle
        {
            // Query-local visitation only; never part of source identity or the geometry fingerprint.
            public ulong WaterVisitStamp;
            public readonly Vector3 A, B, C, Normal;
            public readonly Bounds Bounds;
            public readonly SurfaceInfo Info;
            private readonly Vector2 uvA, uvB, uvC;
            public GroundTriangle(Vector3 a, Vector3 b, Vector3 c, SurfaceInfo info, Vector2 uvA = default, Vector2 uvB = default, Vector2 uvC = default)
            { A = a; B = b; C = c; Info = info; this.uvA = uvA; this.uvB = uvB; this.uvC = uvC; Bounds = TriangleBounds(a, b, c); Vector3 n = Vector3.Cross(b - a, c - a).normalized; Normal = n.y < 0f ? -n : n; }
            public void WriteFingerprint(BinaryWriter writer)
            {
                WriteVector(writer, A); WriteVector(writer, B); WriteVector(writer, C); WriteSurface(writer, Info);
                WriteVector(writer, uvA); WriteVector(writer, uvB); WriteVector(writer, uvC);
            }
            public static int Compare(GroundTriangle a, GroundTriangle b)
            {
                int order = CompareSurface(a.Info, b.Info); if (order != 0) return order;
                order = CompareVector(a.A, b.A); if (order != 0) return order;
                order = CompareVector(a.B, b.B); if (order != 0) return order;
                order = CompareVector(a.C, b.C); if (order != 0) return order;
                order = CompareVector(a.uvA, b.uvA); if (order != 0) return order;
                order = CompareVector(a.uvB, b.uvB); return order != 0 ? order : CompareVector(a.uvC, b.uvC);
            }
            public Vector2 UvAt(Vector2 point)
            {
                Vector2 ab = Xz(B - A), ac = Xz(C - A), ap = point - Xz(A);
                float determinant = Cross(ab, ac);
                return uvA + (uvB - uvA) * (Cross(ap, ac) / determinant) + (uvC - uvA) * (Cross(ab, ap) / determinant);
            }
            public bool TryRaycast(Vector2 p, out float y)
            {
                Vector2 a = Xz(A), ab = Xz(B) - a, ac = Xz(C) - a, ap = p - a;
                float determinant = Cross(ab, ac);
                if (Mathf.Abs(determinant) <= 0.00001f) { y = 0f; return false; }
                float u = Cross(ap, ac) / determinant, v = Cross(ab, ap) / determinant;
                y = A.y + u * (B.y - A.y) + v * (C.y - A.y);
                return u >= -0.00001f && v >= -0.00001f && u + v <= 1.00001f;
            }
            public float HeightOnPlane(Vector2 p)
            {
                Vector2 ab = Xz(B - A), ac = Xz(C - A), ap = p - Xz(A);
                float determinant = Cross(ab, ac);
                return Mathf.Abs(determinant) <= 0.00001f ? float.NaN :
                    A.y + Cross(ap, ac) / determinant * (B.y - A.y) + Cross(ab, ap) / determinant * (C.y - A.y);
            }
        }

        private sealed class Footprint
        {
            private readonly Vector2 a, b, c;
            private readonly Vector2 minimum, maximum;
            private readonly Vector3 first, second, third;
            private Dictionary<GroundTriangle, Vector2[]> wetPolygons;
            public Vector2 A => a;
            public Vector2 B => b;
            public Vector2 C => c;
            public readonly Bounds Bounds;
            public readonly MapVegetationExclusionKind Category;
            public readonly string Source;
            public readonly float ExtraMargin;
            public Footprint(Vector3 first, Vector3 second, Vector3 third, MapVegetationExclusionKind category, string source, float extraMargin = 0f)
            {
                this.first = first; this.second = second; this.third = third;
                a = Xz(first); b = Xz(second); c = Xz(third);
                minimum = Vector2.Min(a, Vector2.Min(b, c)); maximum = Vector2.Max(a, Vector2.Max(b, c));
                Category = category; Source = source; Bounds = TriangleBounds(first, second, third); ExtraMargin = extraMargin;
            }
            public void WriteFingerprint(BinaryWriter writer)
            {
                writer.Write((int)Category); writer.Write(Source ?? string.Empty); writer.Write(ExtraMargin);
                WriteVector(writer, first); WriteVector(writer, second); WriteVector(writer, third);
            }
            public static int Compare(Footprint a, Footprint b)
            {
                int order = a.Category.CompareTo(b.Category); if (order != 0) return order;
                order = string.CompareOrdinal(a.Source, b.Source); if (order != 0) return order;
                order = CompareFloat(a.ExtraMargin, b.ExtraMargin); if (order != 0) return order;
                order = CompareVector(a.first, b.first); if (order != 0) return order;
                order = CompareVector(a.second, b.second); return order != 0 ? order : CompareVector(a.third, b.third);
            }
            public bool TryHeight(Vector2 p, out float y)
            {
                float determinant = Cross(b - a, c - a);
                if (Mathf.Abs(determinant) <= 0.00001f) { y = 0f; return false; }
                y = first.y + Cross(p - a, c - a) / determinant * (second.y - first.y) +
                    Cross(b - a, p - a) / determinant * (third.y - first.y);
                return float.IsFinite(y);
            }
            public Vector2[] WetGroundPolygon(GroundTriangle triangle)
            {
                wetPolygons ??= new Dictionary<GroundTriangle, Vector2[]>();
                if (wetPolygons.TryGetValue(triangle, out Vector2[] result)) return result;
                float determinant = Cross(b - a, c - a);
                if (Mathf.Abs(determinant) <= 0.00001f || triangle.Bounds.min.y > Bounds.max.y + WaterSurfaceToleranceMeters)
                    result = Array.Empty<Vector2>();
                else
                {
                    var polygon = new List<Vector2> { Xz(triangle.A), Xz(triangle.B), Xz(triangle.C) };
                    float direction = Mathf.Sign(determinant);
                    polygon = Clip(polygon, p => direction * Cross(b - a, p - a));
                    polygon = Clip(polygon, p => direction * Cross(c - b, p - b));
                    polygon = Clip(polygon, p => direction * Cross(a - c, p - c));
                    polygon = Clip(polygon, p => TryHeight(p, out float height) ? height + WaterSurfaceToleranceMeters - triangle.HeightOnPlane(p) : -1f);
                    result = polygon.Count >= 3 ? polygon.ToArray() : Array.Empty<Vector2>();
                }
                wetPolygons.Add(triangle, result);
                return result;
            }
            private static List<Vector2> Clip(List<Vector2> input, Func<Vector2, float> signedDistance)
            {
                if (input.Count == 0) return input;
                var result = new List<Vector2>(input.Count + 1);
                Vector2 previous = input[input.Count - 1];
                float previousDistance = signedDistance(previous);
                foreach (Vector2 current in input)
                {
                    float distance = signedDistance(current);
                    bool previousInside = previousDistance >= 0f, currentInside = distance >= 0f;
                    if (previousInside != currentInside)
                    {
                        float t = previousDistance / (previousDistance - distance);
                        result.Add(Vector2.LerpUnclamped(previous, current, t));
                    }
                    if (currentInside) result.Add(current);
                    previous = current; previousDistance = distance;
                }
                return result;
            }
            public bool Contains(Vector2 p, float margin)
            {
                float r = margin + ExtraMargin;
                // Buckets deliberately over-select triangles; reject their expanded
                // bounds before barycentric and three point-to-segment calculations.
                if (p.x < minimum.x - r || p.x > maximum.x + r || p.y < minimum.y - r || p.y > maximum.y + r) return false;
                float d = Cross(b - a, c - a);
                if (Mathf.Abs(d) > 0.00001f)
                {
                    float u = Cross(p - a, c - a) / d, v = Cross(b - a, p - a) / d;
                    if (u >= 0f && v >= 0f && u + v <= 1f) return true;
                }
                float r2 = r * r;
                return SegmentDistanceSquared(p, a, b) <= r2 || SegmentDistanceSquared(p, b, c) <= r2 || SegmentDistanceSquared(p, c, a) <= r2;
            }
            private static float SegmentDistanceSquared(Vector2 p, Vector2 a, Vector2 b)
            { Vector2 delta = b - a; float t = delta.sqrMagnitude > 0.000001f ? Mathf.Clamp01(Vector2.Dot(p - a, delta) / delta.sqrMagnitude) : 0f; return (p - a - delta * t).sqrMagnitude; }
        }

        private sealed class TerrainSnapshot
        {
            private readonly float[,] heights;
            private readonly bool[,] surfaceExists;
            private readonly Vector3 size;
            private readonly int resolution, holesResolution;
            private readonly Matrix4x4 matrix, inverse;
            private readonly Dictionary<Vector2Int, GroundTriangle[]> waterGroundCells = new Dictionary<Vector2Int, GroundTriangle[]>();
            public readonly SurfaceInfo Info;
            public TerrainSnapshot(Terrain terrain, SurfaceInfo info)
            {
                TerrainData data = terrain.terrainData;
                size = data.size; resolution = data.heightmapResolution; holesResolution = data.holesResolution;
                heights = data.GetHeights(0, 0, resolution, resolution);
                surfaceExists = holesResolution > 0 ? data.GetHoles(0, 0, holesResolution, holesResolution) : new bool[0, 0];
                matrix = terrain.transform.localToWorldMatrix; inverse = matrix.inverse; Info = info;
            }
            public void WriteFingerprint(BinaryWriter writer)
            {
                WriteSurface(writer, Info); WriteMatrix(writer, matrix); WriteVector(writer, size);
                writer.Write(resolution); writer.Write(holesResolution);
                for (int z = 0; z < resolution; z++) for (int x = 0; x < resolution; x++) writer.Write(heights[z, x]);
                for (int z = 0; z < holesResolution; z++) for (int x = 0; x < holesResolution; x++) writer.Write(surfaceExists[z, x]);
            }
            public bool TryRaycast(Vector2 point, out Vector3 position, out Vector3 normal)
            {
                position = default; normal = Vector3.up;
                Vector3 local = inverse.MultiplyPoint3x4(new Vector3(point.x, 0f, point.y));
                float u = local.x / size.x, v = local.z / size.z;
                if (u < 0f || v < 0f || u > 1f || v > 1f) return false;
                if (holesResolution > 0 && !surfaceExists[Mathf.Min(holesResolution - 1, Mathf.FloorToInt(v * holesResolution)), Mathf.Min(holesResolution - 1, Mathf.FloorToInt(u * holesResolution))]) return false;
                int cells = resolution - 1;
                float sampleX = u * cells, sampleZ = v * cells;
                int x = Mathf.Min(cells - 1, Mathf.FloorToInt(sampleX)), z = Mathf.Min(cells - 1, Mathf.FloorToInt(sampleZ));
                float tx = sampleX - x, tz = sampleZ - z;
                float a = heights[z, x], b = heights[z, x + 1], c = heights[z + 1, x], d = heights[z + 1, x + 1];
                float height = Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz) * size.y;
                float slopeX = Mathf.Lerp(b - a, d - c, tz) * size.y * cells / size.x;
                float slopeZ = Mathf.Lerp(c - a, d - b, tx) * size.y * cells / size.z;
                position = matrix.MultiplyPoint3x4(new Vector3(local.x, height, local.z));
                normal = inverse.transpose.MultiplyVector(new Vector3(-slopeX, 1f, -slopeZ)).normalized;
                return true;
            }
            public IEnumerable<GroundTriangle> NearbyTriangles(Vector2 point, float radius)
            {
                Vector3 min = inverse.MultiplyPoint3x4(new Vector3(point.x - radius, 0f, point.y - radius));
                Vector3 max = inverse.MultiplyPoint3x4(new Vector3(point.x + radius, 0f, point.y + radius));
                if (min.x > size.x || max.x < 0f || min.z > size.z || max.z < 0f) yield break;
                int cells = resolution - 1;
                int minX = Mathf.Clamp(Mathf.FloorToInt(min.x / size.x * cells), 0, cells - 1);
                int maxX = Mathf.Clamp(Mathf.FloorToInt(max.x / size.x * cells), 0, cells - 1);
                int minZ = Mathf.Clamp(Mathf.FloorToInt(min.z / size.z * cells), 0, cells - 1);
                int maxZ = Mathf.Clamp(Mathf.FloorToInt(max.z / size.z * cells), 0, cells - 1);
                for (int z = minZ; z <= maxZ; z++) for (int x = minX; x <= maxX; x++)
                {
                    if (holesResolution > 0 && !surfaceExists[Mathf.Min(holesResolution - 1, z), Mathf.Min(holesResolution - 1, x)]) continue;
                    var key = new Vector2Int(x, z);
                    if (!waterGroundCells.TryGetValue(key, out GroundTriangle[] triangles))
                    {
                        Vector3 a = TerrainVertex(x, z, cells), b = TerrainVertex(x + 1, z, cells);
                        Vector3 c = TerrainVertex(x, z + 1, cells), d = TerrainVertex(x + 1, z + 1, cells);
                        triangles = new[] { new GroundTriangle(a, c, b, Info), new GroundTriangle(b, c, d, Info) };
                        waterGroundCells.Add(key, triangles);
                    }
                    foreach (GroundTriangle triangle in triangles) yield return triangle;
                }
            }
            private Vector3 TerrainVertex(int x, int z, int cells) => matrix.MultiplyPoint3x4(
                new Vector3(x / (float)cells * size.x, heights[z, x] * size.y, z / (float)cells * size.z));
        }

        private readonly struct VolumeSnapshot
        {
            private readonly Matrix4x4 inverse;
            private readonly Vector3 size;
            private readonly float radius;
            private readonly int channels;
            private readonly bool sphere;
            public readonly string Source;
            public VolumeSnapshot(VegetationExclusionVolume volume)
            {
                // Copy existing marker serialization without changing its accepted runtime API.
                var serialized = new SerializedObject(volume);
                inverse = volume.transform.worldToLocalMatrix; size = serialized.FindProperty("size").vector3Value;
                radius = serialized.FindProperty("radius").floatValue; channels = serialized.FindProperty("channelMask").intValue;
                sphere = serialized.FindProperty("shape").enumValueIndex == (int)VegetationExclusionShape.Sphere;
                Source = volume.gameObject.scene.path + ":" + volume.name;
            }
            public void WriteFingerprint(BinaryWriter writer)
            {
                writer.Write(Source ?? string.Empty); WriteMatrix(writer, inverse); WriteVector(writer, size);
                writer.Write(radius); writer.Write(channels); writer.Write(sphere);
            }
            public bool Contains(Vector3 position, MapVegetationKind kind)
            {
                if (kind == MapVegetationKind.Grass && (channels & 7) != 7) return false;
                return ContainsChannel(position, kind == MapVegetationKind.Grass ? VegetationDensityChannel.ShortGrass : VegetationDensityChannel.Decorative);
            }
            public bool ContainsChannel(Vector3 position, VegetationDensityChannel channel)
            {
                if ((channels & (1 << (int)channel)) == 0) return false;
                Vector3 local = inverse.MultiplyPoint3x4(position);
                return sphere ? local.sqrMagnitude <= radius * radius : Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.y) <= size.y * 0.5f && Mathf.Abs(local.z) <= size.z * 0.5f;
            }
        }
    }
}
