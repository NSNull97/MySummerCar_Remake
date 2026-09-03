using System;
using System.Collections.Generic;
using System.IO;
using MSC.LegacyImport;
using MSC.World.Vegetation;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    /// <summary>Read-only evidence for a loaded capture; writes a report, never changes or saves scene objects.</summary>
    public static class MapVegetationRoadVisualDiagnostics
    {
        public static void WriteForLoadedRoots(IEnumerable<GameObject> sourceRoots, IEnumerable<GameObject> pilotRoots,
            MapVegetationPlacementSettings settings, Vector3 cameraPosition, string outputPath)
        {
            var roots = new List<GameObject>(sourceRoots);
            var report = new Report { cameraPosition = cameraPosition };
            var points = new List<PointReport> { new PointReport { label = "camera-ground-query", position = cameraPosition } };
            var profiles = new HashSet<VegetationProfile>();
            foreach (GameObject root in pilotRoots)
            foreach (VegetationWorldRenderer renderer in root.GetComponentsInChildren<VegetationWorldRenderer>(true))
            {
                if (renderer.Catalog == null) continue;
                foreach (VegetationCellAsset cell in renderer.Catalog.Cells)
                foreach (VegetationTileRecord tile in cell.Tiles)
                foreach (VegetationProfileTileInstances batch in tile.ProfileInstances)
                {
                    VegetationProfile profile = renderer.Catalog.Profiles[batch.ProfileIndex];
                    if (profile == null) continue;
                    if (profiles.Add(profile)) report.profiles.Add(ProfileEvidence(profile));
                    foreach (VegetationInstanceRecord instance in batch.Instances)
                    {
                        float distance = (Xz(instance.WorldPosition) - Xz(cameraPosition)).sqrMagnitude;
                        if (points.Count == 13 && distance >= points[points.Count - 1].distanceSquared) continue;
                        var point = new PointReport { label = "saved-grass", position = instance.WorldPosition,
                            profile = profile.ProfileId, channel = profile.DensityChannel, scale = instance.UniformScale,
                            distanceSquared = distance, surfaceNormal = instance.SurfaceNormal };
                        int index = 1;
                        while (index < points.Count && points[index].distanceSquared <= distance) index++;
                        points.Insert(index, point);
                        if (points.Count > 13) points.RemoveAt(points.Count - 1);
                    }
                }
            }
            MapVegetationSurfaceQuery query = MapVegetationSurfaceQuery.Build(roots, settings);
            foreach (PointReport point in points)
            {
                MapVegetationSurfaceHit hit;
                point.acceptedByCurrentQuery = point.label == "saved-grass"
                    ? query.TryResolveGrass(point.position, point.channel, out hit, out point.rejection)
                    : query.TryResolve(point.position, MapVegetationKind.Grass, out hit, out point.rejection);
                point.resolvedPosition = hit.Position; point.resolvedNormal = hit.Normal;
                point.groundSource = hit.Source; point.groundMaterial = hit.Material != null ? AssetDatabase.GetAssetPath(hit.Material) : string.Empty;
            }
            report.points = points;
            var visited = new HashSet<MeshFilter>();
            foreach (GameObject root in roots)
            foreach (DonorWorldBaselineEntityMetadata entity in root.GetComponentsInChildren<DonorWorldBaselineEntityMetadata>(true))
            {
                string path = entity.SourceHierarchyPath ?? string.Empty;
                // Match the capture's candidate selector, including Roadside, so
                // its accidental or inactive selection can be identified explicitly.
                if (path.IndexOf("/TERRAIN_OBJ/ROAD", StringComparison.OrdinalIgnoreCase) < 0 &&
                    path.IndexOf("/TERRAIN_OBJ/DIRTROAD", StringComparison.OrdinalIgnoreCase) < 0) continue;
                foreach (MeshFilter filter in entity.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!visited.Add(filter) || filter.sharedMesh == null) continue;
                    Renderer renderer = filter.GetComponent<Renderer>();
                    var evidence = new MeshEvidence { source = entity.StableId + "|" + path,
                        category = entity.SemanticCategory, objectName = filter.name,
                        activeInHierarchy = filter.gameObject.activeInHierarchy,
                        rendererExists = renderer != null, rendererEnabled = renderer != null && renderer.enabled,
                        forceRenderingOff = renderer != null && renderer.forceRenderingOff,
                        meshPath = AssetDatabase.GetAssetPath(filter.sharedMesh), meshName = filter.sharedMesh.name,
                        readable = filter.sharedMesh.isReadable };
                    foreach (Collider collider in filter.GetComponentsInChildren<Collider>(true))
                        evidence.colliders.Add(collider.GetType().Name + " enabled=" + collider.enabled +
                            " active=" + collider.gameObject.activeInHierarchy + " trigger=" + collider.isTrigger);
                    if (renderer != null)
                        foreach (Material material in renderer.sharedMaterials)
                            evidence.materials.Add(material == null ? "Missing" : AssetDatabase.GetAssetPath(material) + " shader=" + material.shader.name);
                    report.roadMeshes.Add(evidence);
                    if (!evidence.readable) { evidence.note = "Unreadable mesh: exact triangle evidence unavailable."; continue; }
                    Vector3[] vertices = filter.sharedMesh.vertices;
                    Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                    for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                    int[] triangles = filter.sharedMesh.triangles;
                    evidence.triangles = triangles.Length / 3;
                    evidence.nearestCentroidDistance = float.MaxValue;
                    foreach (PointReport point in points)
                        evidence.samples.Add(new RoadSample { label = point.label, position = point.position, distanceXZ = float.MaxValue });
                    for (int triangle = 0; triangle + 2 < triangles.Length; triangle += 3)
                    {
                        Vector3 a = vertices[triangles[triangle]], b = vertices[triangles[triangle + 1]], c = vertices[triangles[triangle + 2]];
                        Vector3 centroid = (a + b + c) / 3f;
                        float centroidDistance = (Xz(centroid) - Xz(cameraPosition)).magnitude;
                        if (centroidDistance < evidence.nearestCentroidDistance)
                        {
                            evidence.nearestCentroidDistance = centroidDistance;
                            evidence.nearestCentroid = centroid; evidence.nearestCentroidTriangle = triangle / 3;
                            evidence.nearestCentroidUpDot = Mathf.Abs(Vector3.Dot(Vector3.Cross(b - a, c - a).normalized, Vector3.up));
                        }
                        foreach (RoadSample sample in evidence.samples)
                        {
                            Vector3 nearest = NearestProjected(sample.position, a, b, c);
                            float distance = (Xz(nearest) - Xz(sample.position)).magnitude;
                            if (distance > sample.distanceXZ || distance == sample.distanceXZ && nearest.y <= sample.closestRoadPosition.y) continue;
                            sample.distanceXZ = distance; sample.closestRoadPosition = nearest; sample.triangle = triangle / 3;
                            sample.verticalDifference = sample.position.y - nearest.y;
                        }
                    }
                }
            }
            report.queryDiagnostics = query.DiagnosticSummary;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
            File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
            Debug.Log("MAP_VEGETATION_ROAD_VISUAL_DIAGNOSTICS " + outputPath + " samples=" + points.Count + " roadMeshes=" + report.roadMeshes.Count);
        }

        private static ProfileReport ProfileEvidence(VegetationProfile profile)
        {
            Material material = profile.Material;
            var result = new ProfileReport { id = profile.ProfileId, asset = AssetDatabase.GetAssetPath(profile),
                material = material != null ? AssetDatabase.GetAssetPath(material) : string.Empty,
                cutoff = material != null && material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : -1f,
                scaleRange = profile.UniformScaleRange, normalAlignment = profile.SurfaceNormalAlignment,
                castsShadows = profile.ShadowCasting.ToString() };
            for (int lod = 0; lod < 3; lod++)
            {
                Mesh mesh = profile.GetLodMesh(lod);
                if (mesh != null) result.lods.Add(new LodReport { index = lod, mesh = AssetDatabase.GetAssetPath(mesh), bounds = mesh.bounds,
                    minimumYAtMinScale = mesh.bounds.min.y * profile.UniformScaleRange.x,
                    minimumYAtMaxScale = mesh.bounds.min.y * profile.UniformScaleRange.y });
            }
            return result;
        }

        private static Vector3 NearestProjected(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector2 p = Xz(point), ab = Xz(b - a), ac = Xz(c - a), ap = p - Xz(a);
            float determinant = Cross(ab, ac);
            if (Mathf.Abs(determinant) > 0.00001f)
            {
                float u = Cross(ap, ac) / determinant, v = Cross(ab, ap) / determinant;
                if (u >= 0f && v >= 0f && u + v <= 1f) return a + (b - a) * u + (c - a) * v;
            }
            Vector3 first = NearestSegment(p, a, b), second = NearestSegment(p, b, c), third = NearestSegment(p, c, a);
            if ((Xz(second) - p).sqrMagnitude < (Xz(first) - p).sqrMagnitude) first = second;
            return (Xz(third) - p).sqrMagnitude < (Xz(first) - p).sqrMagnitude ? third : first;
        }
        private static Vector3 NearestSegment(Vector2 point, Vector3 a, Vector3 b)
        {
            Vector2 delta = Xz(b - a);
            float t = delta.sqrMagnitude > 0.000001f ? Mathf.Clamp01(Vector2.Dot(point - Xz(a), delta) / delta.sqrMagnitude) : 0f;
            return Vector3.LerpUnclamped(a, b, t);
        }
        private static Vector2 Xz(Vector3 value) => new Vector2(value.x, value.z);
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        [Serializable] private sealed class Report
        {
            public Vector3 cameraPosition;
            public string queryDiagnostics;
            public List<PointReport> points;
            public List<MeshEvidence> roadMeshes = new List<MeshEvidence>();
            public List<ProfileReport> profiles = new List<ProfileReport>();
        }
        [Serializable] private sealed class PointReport
        {
            public string label, profile, rejection, groundSource, groundMaterial;
            public Vector3 position, resolvedPosition, resolvedNormal, surfaceNormal;
            public VegetationDensityChannel channel;
            public float scale, distanceSquared;
            public bool acceptedByCurrentQuery;
        }
        [Serializable] private sealed class MeshEvidence
        {
            public string source, category, objectName, meshPath, meshName, note;
            public bool activeInHierarchy, rendererExists, rendererEnabled, forceRenderingOff, readable;
            public int triangles, nearestCentroidTriangle;
            public float nearestCentroidDistance, nearestCentroidUpDot;
            public Vector3 nearestCentroid;
            public List<string> materials = new List<string>(), colliders = new List<string>();
            public List<RoadSample> samples = new List<RoadSample>();
        }
        [Serializable] private sealed class RoadSample
        {
            public string label;
            public Vector3 position, closestRoadPosition;
            public float distanceXZ, verticalDifference;
            public int triangle;
        }
        [Serializable] private sealed class ProfileReport
        {
            public string id, asset, material, castsShadows;
            public float cutoff, normalAlignment;
            public Vector2 scaleRange;
            public List<LodReport> lods = new List<LodReport>();
        }
        [Serializable] private sealed class LodReport
        {
            public int index;
            public string mesh;
            public Bounds bounds;
            public float minimumYAtMinScale, minimumYAtMaxScale;
        }
    }
}
