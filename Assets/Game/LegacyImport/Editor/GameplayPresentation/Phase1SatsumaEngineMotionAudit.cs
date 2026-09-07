using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Read-only geometry inventory before explicit render-leaf motion binding.</summary>
    public static class Phase1SatsumaEngineMotionAudit
    {
        private static readonly string[] Suffixes = { "engine-block", "crankshaft", "crankshaft-pulley", "flywheel",
            "camshaft", "camshaft-gear", "timing-chain", "piston1", "piston2", "piston3", "piston4",
            "rocker-shaft", "cylinder-head", "water-pump", "water-pump-pulley", "alternator", "radiator", "distributor", "starter" };

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Audit Engine Motion Geometry Only")]
        public static void AuditBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before the read-only motion inventory.");
            GameObject root = PrefabUtility.LoadPrefabContents(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            try
            {
                var rows = new List<Leaf>();
                var topology = new List<Topology>();
                var inspectedMeshes = new HashSet<Mesh>();
                var assembly = root.GetComponent<VehicleAssemblyController>();
                var crank = assembly.Parts.Single(p => p.Definition.DefinitionId == "vehicle.satsuma.part.crankshaft");
                Mesh crankMesh = crank.GetComponentsInChildren<MeshFilter>(true).Single(f =>
                    AssetDatabase.GetAssetPath(f.sharedMesh).EndsWith("/cef82582b4c013a4eab6bf26895cc151.asset", StringComparison.Ordinal)).sharedMesh;
                var sections = crankMesh.vertices.GroupBy(v => Mathf.RoundToInt(v.x * 10000f)).OrderBy(g => g.Key)
                    .Select(g => new Section { x = g.Key / 10000f, count = g.Count(),
                        minimum = new Vector2(g.Min(v => v.y), g.Min(v => v.z)),
                        maximum = new Vector2(g.Max(v => v.y), g.Max(v => v.z)) }).ToArray();
                var mounts = assembly.MountPoints.Where(m => m.Definition.OwnerPartDefinitionId == "vehicle.satsuma.part.engine-block" ||
                        m.Definition.OwnerPartDefinitionId == "vehicle.satsuma.part.crankshaft")
                    .Select(m => { PartInstance owner = assembly.Parts.Single(p => p.Definition.DefinitionId == m.Definition.OwnerPartDefinitionId);
                        return new Mount { id = m.MountId, owner = owner.Definition.DefinitionId,
                            position = owner.transform.InverseTransformPoint(m.Pose.position),
                            rotation = Quaternion.Inverse(owner.transform.rotation) * m.Pose.rotation }; }).ToArray();
                foreach (string suffix in Suffixes)
                {
                    PartInstance part = assembly.Parts.Single(p => p.Definition.DefinitionId == "vehicle.satsuma.part." + suffix);
                    foreach (MeshFilter filter in part.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (filter.GetComponentInParent<PartInstance>(true) != part) continue;
                        Mesh mesh = filter.sharedMesh;
                        if ((suffix.StartsWith("piston", StringComparison.Ordinal) || suffix == "rocker-shaft") && mesh != null && inspectedMeshes.Add(mesh))
                            topology.Add(InspectIslands(mesh));
                        rows.Add(new Leaf
                        {
                            partId = part.Definition.DefinitionId, visualLabel = filter.name,
                            meshPath = AssetDatabase.GetAssetPath(mesh),
                            position = part.transform.InverseTransformPoint(filter.transform.position),
                            rotation = Quaternion.Inverse(part.transform.rotation) * filter.transform.rotation,
                            scale = filter.transform.lossyScale, boundsCenter = mesh != null ? mesh.bounds.center : default,
                            boundsSize = mesh != null ? mesh.bounds.size : default, vertexCount = mesh != null ? mesh.vertexCount : 0,
                            childCount = filter.transform.childCount, hasCollider = filter.GetComponent<Collider>() != null,
                            fastenerOwner = filter.GetComponentInParent<AssemblyFastenerInteractionTarget>() != null,
                        });
                    }
                }
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/satsuma-engine-motion-geometry-20260906.json", JsonUtility.ToJson(
                    new Report { leaves = rows.ToArray(), topology = topology.ToArray(), crankSections = sections, mounts = mounts }, true));
                Debug.Log("SATSUMA_ENGINE_MOTION_AUDIT_OK parts=" + Suffixes.Length + " leaves=" + rows.Count + " assetsChanged=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Topology InspectIslands(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            int[] groups = Enumerable.Range(0, vertices.Length).ToArray();
            var welded = new Dictionary<Vector3Int, int>();
            int Root(int i) { while (groups[i] != i) { groups[i] = groups[groups[i]]; i = groups[i]; } return i; }
            void Union(int a, int b) { groups[Root(b)] = Root(a); }
            for (int i = 0; i < vertices.Length; i++)
            {
                var key = Vector3Int.RoundToInt(vertices[i] * 200000f); // Five-micron seam welding, audit only.
                if (welded.TryGetValue(key, out int prior)) Union(prior, i); else welded.Add(key, i);
            }
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                if (mesh.GetTopology(sub) != MeshTopology.Triangles) continue;
                int[] indices = mesh.GetIndices(sub);
                for (int i = 0; i < indices.Length; i += 3) { Union(indices[i], indices[i + 1]); Union(indices[i], indices[i + 2]); }
            }
            var islands = new Dictionary<int, Bounds>();
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                int id = Root(i);
                if (!islands.TryGetValue(id, out var bounds)) bounds = new Bounds(vertices[i], Vector3.zero);
                bounds.Encapsulate(vertices[i]); islands[id] = bounds;
                counts[id] = counts.TryGetValue(id, out int count) ? count + 1 : 1;
            }
            return new Topology { meshPath = AssetDatabase.GetAssetPath(mesh), islands = islands.OrderBy(pair => pair.Value.center.x)
                .Select(pair => new Island { center = pair.Value.center, size = pair.Value.size, vertices = counts[pair.Key] }).ToArray() };
        }

        [Serializable] private sealed class Report { public Leaf[] leaves; public Topology[] topology; public Section[] crankSections; public Mount[] mounts; }
        [Serializable] private sealed class Section { public float x; public int count; public Vector2 minimum, maximum; }
        [Serializable] private sealed class Mount { public string id, owner; public Vector3 position; public Quaternion rotation; }
        [Serializable] private sealed class Topology { public string meshPath; public Island[] islands; }
        [Serializable] private sealed class Island { public Vector3 center, size; public int vertices; }
        [Serializable] private sealed class Leaf
        {
            public string partId, visualLabel, meshPath;
            public Vector3 position, scale, boundsCenter, boundsSize;
            public Quaternion rotation;
            public int vertexCount, childCount;
            public bool hasCollider, fastenerOwner;
        }
    }
}
