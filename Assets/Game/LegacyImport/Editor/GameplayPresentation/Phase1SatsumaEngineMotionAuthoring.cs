using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Exact source mesh identities are authoring/provenance only, never runtime discovery.</summary>
    public static class Phase1SatsumaEngineMotionAuthoring
    {
        private const string MeshRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/Meshes/";
        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Bind Engine Mechanical Motion Only")]
        public static void RefreshBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before motion authoring.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changes = ApplyToInstance(root.GetComponent<VehicleAssemblyController>());
                if (changes > 0)
                {
                    Directory.CreateDirectory("Logs");
                    File.Copy(path, "Logs/codex-mechanical-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".prefab", false);
                    if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new InvalidDataException("Could not save engine mechanical bindings.");
                }
                Debug.Log("SATSUMA_ENGINE_MECHANICAL_OK changed=" + changes + " rotating=9 reciprocating=32 fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static int ApplyToInstance(VehicleAssemblyController assembly)
        {
            if (assembly == null || Application.isPlaying) throw new ArgumentException("Explicit stopped-editor assembly required.");
            PartInstance Part(string suffix) => assembly.Parts.Single(p => p?.Definition?.DefinitionId == "vehicle.satsuma.part." + suffix);
            var host = assembly.GetComponent<VehicleSimulationHost>();
            if (host == null || host.SatsumaOperatingSourceComponent is not SatsumaEngineOperatingSource)
                throw new InvalidDataException("Bind the real Satsuma operating source before mechanical motion.");
            var shafts = new List<SatsumaShaftVisualBinding>();
            void Shaft(string suffix, string meshId, SatsumaMechanicalDrive drive, Vector3 axis)
            { PartInstance owner = Part(suffix); shafts.Add(new SatsumaShaftVisualBinding(owner, Mesh(owner, meshId).transform, axis, drive)); }
            Shaft("crankshaft", "cef82582b4c013a4eab6bf26895cc151", SatsumaMechanicalDrive.Crankshaft, Vector3.right);
            Shaft("crankshaft", "aaac33e10b0c32845ba22270f0c52d9e", SatsumaMechanicalDrive.Crankshaft, Vector3.right);
            Shaft("crankshaft-pulley", "a54fb151e9a80804bb2e89d7e8d31aac", SatsumaMechanicalDrive.Crankshaft, Vector3.right);
            Shaft("flywheel", "5a88cb2b8c931ab4a9d7ca02589b2cd5", SatsumaMechanicalDrive.Crankshaft, Vector3.right);
            Shaft("camshaft", "b00300b34e8994f4d8e60d88008438a7", SatsumaMechanicalDrive.Camshaft, Vector3.right);
            Shaft("camshaft-gear", "f846a4e5b79ba9c4ea14694fae7bb75b", SatsumaMechanicalDrive.Camshaft, Vector3.right);
            Shaft("water-pump-pulley", "e6ebe1eb94eeb304a98894dde1823c01", SatsumaMechanicalDrive.WaterPump, Vector3.right);
            Shaft("alternator", "f8771cd5897dcc94abbc279a97615b05", SatsumaMechanicalDrive.Alternator, Vector3.right);
            Shaft("radiator", "c64d7a595fa4aef4bb3a4ff380dc10de", SatsumaMechanicalDrive.RadiatorFan, Vector3.up);

            var moving = new List<SatsumaReciprocatingVisualBinding>();
            for (int cylinder = 0; cylinder < 4; cylinder++)
            {
                PartInstance owner = Part("piston" + (cylinder + 1));
                // Damaged2/3/4 are not the normal disconnected seven-island
                // topology. Do not tear arbitrary triangles to manufacture a rig.
                foreach (string meshId in cylinder == 0 ? new[] { "d915bf4de02b02045b3a48956fe9f783", "d72c77f82d261f140a07f962d6ab6d6b" } :
                    new[] { "d915bf4de02b02045b3a48956fe9f783" })
                {
                    MeshFilter filter = Mesh(owner, meshId);
                    var groups = Groups(filter.sharedMesh, island => island.bounds.center.z < -.04f ? 1 : 0, out int islands);
                    if (islands != 7) throw new InvalidDataException("Expected seven disconnected piston/rod islands: " + meshId + " actual=" + islands);
                    moving.Add(new(owner, filter, SatsumaReciprocatingKind.Piston, cylinder, groups,
                        Vector3.forward * SatsumaReciprocatingMesh.PistonPinZ));
                }
                foreach (string meshId in new[] { "f48a6a1b2ea4b164d9d920ab78966d86", "397d8da54d674e84e951c7d66a897a73",
                    "e52ec1b2bf6e76146a2bffa9c6d20e94", "6b163f67173264f41a783f4e4c9fd949" })
                {
                    MeshFilter filter = Mesh(owner, meshId);
                    int group = meshId == "f48a6a1b2ea4b164d9d920ab78966d86" ? 1 : 0;
                    moving.Add(new(owner, filter, SatsumaReciprocatingKind.Piston, cylinder,
                        Enumerable.Repeat(group, filter.sharedMesh.vertexCount).ToArray(), Vector3.forward * SatsumaReciprocatingMesh.PistonPinZ));
                }
            }
            PartInstance rocker = Part("rocker-shaft");
            AssemblyValveAdjustmentTarget[] valves = rocker.GetComponentsInChildren<AssemblyValveAdjustmentTarget>(true).OrderBy(v => v.ValveIndex).ToArray();
            if (valves.Length != 8 || valves.Where((v, i) => v.ValveIndex != i).Any()) throw new InvalidDataException("Bind eight reviewed valve settings first.");
            Vector3 rockerPivot = new(0f, .02469825f, .01505745f);
            foreach (string meshId in new[] { "9b7151d6e619efc4fbc9b1023cdd3ebb", "c1ee47cccc43cb342966aa0bb836011b", "b931fd1883690fb4984a9f10666460bc" })
            {
                MeshFilter filter = Mesh(rocker, meshId);
                bool valveBodies = meshId == "b931fd1883690fb4984a9f10666460bc";
                int parity = meshId == "9b7151d6e619efc4fbc9b1023cdd3ebb" ? 0 : 1;
                Matrix4x4 toPart = rocker.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var groups = Groups(filter.sharedMesh, island =>
                {
                    if (valveBodies && island.count != 53) return -1; // Five pedestal islands and fixed shaft stay put.
                    float x = toPart.MultiplyPoint3x4(island.bounds.center).x;
                    var closest = valves.Where(v => valveBodies || v.ValveIndex % 2 == parity)
                        .OrderBy(v => Mathf.Abs(rocker.transform.InverseTransformPoint(v.ScrewRenderer.transform.position).x - x)).First();
                    if (Mathf.Abs(rocker.transform.InverseTransformPoint(closest.ScrewRenderer.transform.position).x - x) > .002f)
                        throw new InvalidDataException("Rocker island is not aligned with its existing valve identity.");
                    return closest.ValveIndex;
                }, out int islands);
                if (islands != (valveBodies ? 14 : 8)) throw new InvalidDataException("Rocker topology drifted.");
                moving.Add(new(rocker, filter, valveBodies ? SatsumaReciprocatingKind.Valves : SatsumaReciprocatingKind.Rockers, 0, groups, rockerPivot));
            }
            foreach (var valve in valves)
            {
                MeshFilter filter = valve.ScrewRenderer.GetComponent<MeshFilter>();
                moving.Add(new(rocker, filter, SatsumaReciprocatingKind.ValveScrew, valve.ValveIndex / 2,
                    Enumerable.Repeat(valve.ValveIndex, filter.sharedMesh.vertexCount).ToArray(), rockerPivot));
            }
            foreach (var binding in moving) binding.Validate();
            var existing = assembly.GetComponent<SatsumaEngineMechanicalMotion>();
            if (existing != null)
            {
                bool same = existing.Simulation == host && existing.Shafts.Length == shafts.Count && existing.Reciprocating.Length == moving.Count;
                if (same) for (int i = 0; i < shafts.Count; i++)
                    same &= existing.Shafts[i].Owner == shafts[i].Owner && existing.Shafts[i].Leaf == shafts[i].Leaf &&
                        existing.Shafts[i].Drive == shafts[i].Drive && existing.Shafts[i].LocalAxis == shafts[i].LocalAxis;
                if (same) for (int i = 0; i < moving.Count; i++)
                    same &= existing.Reciprocating[i].Owner == moving[i].Owner && existing.Reciprocating[i].Filter == moving[i].Filter &&
                        existing.Reciprocating[i].Kind == moving[i].Kind && existing.Reciprocating[i].Cylinder == moving[i].Cylinder &&
                        existing.Reciprocating[i].Pivot == moving[i].Pivot && existing.Reciprocating[i].VertexGroups.SequenceEqual(moving[i].VertexGroups);
                if (!same) throw new InvalidDataException("Existing mechanical bindings drifted; refusing silent replacement.");
                return BindRotatingFasteners(assembly, existing);
            }
            existing = assembly.gameObject.AddComponent<SatsumaEngineMechanicalMotion>();
            existing.Configure(host, assembly, Part("engine-block"), Part("crankshaft"), Part("camshaft"), Part("timing-chain"),
                Part("camshaft-gear").GetComponent<AssemblyCamshaftTimingState>(), Part("alternator").GetComponent<AssemblyEngineAdjustmentState>(),
                shafts.ToArray(), moving.ToArray());
            return 1 + BindRotatingFasteners(assembly, existing);
        }

        private static int BindRotatingFasteners(VehicleAssemblyController assembly, SatsumaEngineMechanicalMotion motion)
        {
            var bindings = new List<SatsumaShaftFastenerBinding>();
            // Whole-part rotational leaves only. Alternator housing bolts and
            // radiator frame bolts must not orbit an internal pulley/fan.
            for (int i = 0; i < motion.Shafts.Length; i++)
            {
                var shaft = motion.Shafts[i];
                string id = shaft.Owner.Definition.DefinitionId;
                if (id != "vehicle.satsuma.part.water-pump-pulley" && id != "vehicle.satsuma.part.crankshaft-pulley" &&
                    id != "vehicle.satsuma.part.flywheel" && id != "vehicle.satsuma.part.camshaft-gear") continue;
                var mount = assembly.MountPoints.Single(m => m.Definition.AcceptedPartDefinitionIds.Contains(id));
                foreach (var target in assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                    .Where(t => t.MountId == mount.MountId).OrderBy(t => t.FastenerDefinitionId, StringComparer.Ordinal))
                    foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!SatsumaEngineVisualVibration.IsSafeVisualLeaf(renderer.transform))
                            throw new InvalidDataException("A rotating fastener is not a safe render leaf.");
                        bindings.Add(new SatsumaShaftFastenerBinding(i, renderer.transform));
                    }
            }
            if (motion.Fasteners.Length == bindings.Count && motion.Fasteners.Select((b, i) =>
                b.ShaftIndex == bindings[i].ShaftIndex && b.Leaf == bindings[i].Leaf).All(equal => equal)) return 0;
            motion.ConfigureFasteners(bindings.ToArray()); EditorUtility.SetDirty(motion); return 1;
        }

        private static MeshFilter Mesh(PartInstance owner, string id)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshRoot + id + ".asset");
            if (mesh == null) throw new InvalidDataException("Missing reviewed engine mesh " + id);
            MeshFilter[] candidates = owner.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh == mesh && f.GetComponentInParent<PartInstance>(true) == owner).ToArray();
            if (candidates.Length != 1) throw new InvalidDataException("Expected one mesh " + id + " on " + owner.Definition.DefinitionId + ", actual=" + candidates.Length);
            MeshFilter filter = candidates[0];
            if (!SatsumaEngineVisualVibration.IsSafeVisualLeaf(filter.transform)) throw new InvalidDataException("Unsafe mechanical renderer binding.");
            return filter;
        }

        private readonly struct Island
        {
            public readonly Bounds bounds; public readonly int count;
            public Island(Bounds value, int vertices) { bounds = value; count = vertices; }
        }
        private static int[] Groups(Mesh mesh, Func<Island, int> classify, out int islandCount)
        {
            Vector3[] vertices = mesh.vertices; int[] parent = Enumerable.Range(0, vertices.Length).ToArray();
            int Root(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
            void Union(int a, int b) { parent[Root(b)] = Root(a); }
            var welded = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var key = Vector3Int.RoundToInt(vertices[i] * 200000f);
                if (welded.TryGetValue(key, out int previous)) Union(previous, i); else welded.Add(key, i);
            }
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                if (mesh.GetTopology(sub) != MeshTopology.Triangles) throw new InvalidDataException("Mechanical mesh topology must be triangles.");
                int[] indices = mesh.GetIndices(sub);
                for (int i = 0; i < indices.Length; i += 3) { Union(indices[i], indices[i + 1]); Union(indices[i], indices[i + 2]); }
            }
            var islands = Enumerable.Range(0, vertices.Length).GroupBy(Root).ToArray(); islandCount = islands.Length;
            var groups = new int[vertices.Length];
            foreach (var island in islands)
            {
                Bounds bounds = new(vertices[island.First()], Vector3.zero);
                foreach (int index in island) bounds.Encapsulate(vertices[index]);
                int group = classify(new Island(bounds, island.Count()));
                foreach (int index in island) groups[index] = group;
            }
            return groups;
        }
    }
}
