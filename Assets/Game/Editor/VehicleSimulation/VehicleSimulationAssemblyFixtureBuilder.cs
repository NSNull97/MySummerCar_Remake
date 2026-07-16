using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.VehicleSimulation
{
    internal static class VehicleSimulationAssemblyFixtureBuilder
    {
        private sealed class PartSpec
        {
            public PartSpec(
                string id,
                string displayName,
                PartCategory category,
                float massKilograms,
                string ownerId,
                Vector3 mountPosition,
                Vector3 visualScale,
                PrimitiveType visualPrimitive = PrimitiveType.Cube)
            {
                Id = id;
                DisplayName = displayName;
                Category = category;
                MassKilograms = massKilograms;
                OwnerId = ownerId;
                MountPosition = mountPosition;
                VisualScale = visualScale;
                VisualPrimitive = visualPrimitive;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public PartCategory Category { get; }
            public float MassKilograms { get; }
            public string OwnerId { get; }
            public Vector3 MountPosition { get; }
            public Vector3 VisualScale { get; }
            public PrimitiveType VisualPrimitive { get; }
            public bool IsRoot => string.IsNullOrEmpty(OwnerId);
            public string Suffix => Id.Substring("m06.".Length);
            public string SocketId => "m06.socket." + Suffix;
            public string MountId => "m06.mount." + Suffix;
            public string FastenerId => "m06.fastener." + Suffix;
        }

        private static readonly PartSpec[] Specs =
        {
            new PartSpec("m06.chassis", "M06 logical chassis", PartCategory.Structural, 620f, "", Vector3.zero, new Vector3(1.7f, 0.28f, 3.7f)),
            new PartSpec("m06.engine", "M06 engine", PartCategory.Engine, 92f, "m06.chassis", new Vector3(0f, 0.42f, 0.85f), new Vector3(0.75f, 0.6f, 0.85f)),
            new PartSpec("m06.starter", "M06 starter", PartCategory.Electrical, 4.2f, "m06.engine", new Vector3(0.48f, 0.25f, 0.72f), new Vector3(0.18f, 0.18f, 0.4f), PrimitiveType.Cylinder),
            new PartSpec("m06.battery", "M06 battery", PartCategory.Electrical, 11.5f, "m06.chassis", new Vector3(-0.55f, 0.38f, 1.05f), new Vector3(0.38f, 0.3f, 0.55f)),
            new PartSpec("m06.fuel_tank", "M06 fuel tank", PartCategory.Structural, 28f, "m06.chassis", new Vector3(0f, 0.25f, -1.25f), new Vector3(0.8f, 0.25f, 0.65f)),
            new PartSpec("m06.clutch", "M06 clutch", PartCategory.Engine, 7f, "m06.engine", new Vector3(0f, 0.35f, 0.2f), new Vector3(0.28f, 0.28f, 0.12f), PrimitiveType.Cylinder),
            new PartSpec("m06.gearbox", "M06 gearbox", PartCategory.Engine, 31f, "m06.clutch", new Vector3(0f, 0.32f, -0.25f), new Vector3(0.45f, 0.4f, 0.72f)),
            new PartSpec("m06.differential", "M06 differential", PartCategory.Engine, 18f, "m06.gearbox", new Vector3(0f, 0.25f, -1.05f), new Vector3(0.55f, 0.28f, 0.32f)),
            new PartSpec("m06.wheel.fl", "M06 wheel front left", PartCategory.Wheel, 15f, "m06.chassis", new Vector3(-0.6299995f, 0f, 1.1669996f), new Vector3(0.62f, 0.22f, 0.62f), PrimitiveType.Cylinder),
            new PartSpec("m06.wheel.fr", "M06 wheel front right", PartCategory.Wheel, 15f, "m06.chassis", new Vector3(0.6300007f, 0f, 1.1669996f), new Vector3(0.62f, 0.22f, 0.62f), PrimitiveType.Cylinder),
            new PartSpec("m06.wheel.rl", "M06 wheel rear left", PartCategory.Wheel, 15f, "m06.chassis", new Vector3(-0.6029993f, 0f, -1.167f), new Vector3(0.62f, 0.22f, 0.62f), PrimitiveType.Cylinder),
            new PartSpec("m06.wheel.rr", "M06 wheel rear right", PartCategory.Wheel, 15f, "m06.chassis", new Vector3(0.603001f, 0f, -1.1669996f), new Vector3(0.62f, 0.22f, 0.62f), PrimitiveType.Cylinder)
        };

        public static VehicleAssemblyController Build(Transform parent)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            EnsureFolders();
            ToolDefinition tool = BuildTool();
            Dictionary<string, PartDefinition> parts = BuildPartDefinitions();
            Dictionary<string, MountPointDefinition> mounts = BuildMountDefinitions();

            GameObject fixture = new GameObject("M06_LogicalAssemblyFixture");
            fixture.transform.SetParent(parent, false);
            VehicleAssemblyController controller = fixture.AddComponent<VehicleAssemblyController>();
            var instances = new List<PartInstance>(Specs.Length);
            var authorings = new List<MountPointAuthoring>(Specs.Length - 1);

            for (int index = 0; index < Specs.Length; index++)
            {
                PartSpec spec = Specs[index];
                GameObject partObject = new GameObject(spec.DisplayName);
                partObject.transform.SetParent(fixture.transform, false);
                partObject.transform.localPosition = spec.MountPosition;

                StableEntityIdAuthoring identity = partObject.AddComponent<StableEntityIdAuthoring>();
                SetStableId(identity, Hash128.Compute("m06.logical.instance." + spec.Id).ToString());
                PartInstance instance = partObject.AddComponent<PartInstance>();
                instance.Configure(
                    parts[spec.Id],
                    identity,
                    body: null,
                    pickup: null,
                    isRoot: spec.IsRoot,
                    mountedAtStart: spec.IsRoot ? string.Empty : spec.MountId);
                instances.Add(instance);
            }

            for (int index = 0; index < Specs.Length; index++)
            {
                PartSpec spec = Specs[index];
                if (spec.IsRoot)
                {
                    continue;
                }

                GameObject mountObject = new GameObject("Mount_" + spec.Suffix);
                mountObject.transform.SetParent(fixture.transform, false);
                mountObject.transform.localPosition = spec.MountPosition;
                MountPointAuthoring authoring = mountObject.AddComponent<MountPointAuthoring>();
                authoring.Configure(mounts[spec.Id], spec.MountId, mountObject.transform, 0);
                authorings.Add(authoring);
            }

            AssemblyDependency[] dependencies = Specs
                .Where(spec => !spec.IsRoot)
                .Select(spec => AssemblyDependency.Create(
                    spec.Id,
                    spec.OwnerId,
                    AssemblyDependencyKind.InstallRequiresInstalled))
                .ToArray();
            controller.Configure(
                instances.ToArray(),
                authorings.ToArray(),
                dependencies,
                new[] { tool },
                detachedPartsRoot: fixture.transform);

            PrototypeAssemblyStateInitializer initializer =
                fixture.AddComponent<PrototypeAssemblyStateInitializer>();
            initializer.Configure(controller);
            return controller;
        }

        private static ToolDefinition BuildTool()
        {
            const string path = VehicleSimulationPrototypePaths.ToolDefinitions + "/M06_Wrench_10.asset";
            ToolDefinition tool = VehicleSimulationEditorUtility.LoadOrCreate<ToolDefinition>(path);
            tool.Configure("m06.tool.wrench.10", "M06 logical wrench 10", "Wrench", FastenerSize.Millimeter10);
            EditorUtility.SetDirty(tool);
            return tool;
        }

        private static Dictionary<string, PartDefinition> BuildPartDefinitions()
        {
            var result = new Dictionary<string, PartDefinition>(StringComparer.Ordinal);
            for (int index = 0; index < Specs.Length; index++)
            {
                PartSpec spec = Specs[index];
                GameObject prefab = BuildVisualPrefab(spec);
                string path = VehicleSimulationPrototypePaths.PartDefinitions + "/" +
                              FileSafe(spec.Id) + ".asset";
                PartDefinition definition = VehicleSimulationEditorUtility.LoadOrCreate<PartDefinition>(path);
                PartCompatibilityRule[] rules = spec.IsRoot
                    ? Array.Empty<PartCompatibilityRule>()
                    : new[] { PartCompatibilityRule.Create(spec.SocketId, spec.OwnerId) };
                definition.Configure(
                    spec.Id,
                    spec.DisplayName,
                    spec.Category,
                    spec.MassKilograms,
                    prefab,
                    rules);
                EditorUtility.SetDirty(definition);
                result.Add(spec.Id, definition);
            }

            return result;
        }

        private static Dictionary<string, MountPointDefinition> BuildMountDefinitions()
        {
            var result = new Dictionary<string, MountPointDefinition>(StringComparer.Ordinal);
            for (int index = 0; index < Specs.Length; index++)
            {
                PartSpec spec = Specs[index];
                if (spec.IsRoot)
                {
                    continue;
                }

                string fastenerPath = VehicleSimulationPrototypePaths.FastenerDefinitions + "/" +
                                      FileSafe(spec.FastenerId) + ".asset";
                FastenerDefinition fastener =
                    VehicleSimulationEditorUtility.LoadOrCreate<FastenerDefinition>(fastenerPath);
                fastener.Configure(
                    spec.FastenerId,
                    "M06 required fastener: " + spec.DisplayName,
                    FastenerSize.Millimeter10,
                    3,
                    FastenerDirection.ClockwiseToTighten,
                    true,
                    true,
                    ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter10));
                EditorUtility.SetDirty(fastener);

                string mountPath = VehicleSimulationPrototypePaths.MountDefinitions + "/" +
                                   FileSafe(spec.MountId) + ".asset";
                MountPointDefinition mount =
                    VehicleSimulationEditorUtility.LoadOrCreate<MountPointDefinition>(mountPath);
                mount.Configure(
                    spec.MountId,
                    "M06 logical mount: " + spec.DisplayName,
                    spec.SocketId,
                    spec.OwnerId,
                    new[] { spec.Id },
                    new MountConstraint(0.01f, 1f, 0.1f, 0f),
                    0f,
                    new[] { fastener });
                EditorUtility.SetDirty(mount);
                result.Add(spec.Id, mount);
            }

            return result;
        }

        private static GameObject BuildVisualPrefab(PartSpec spec)
        {
            string path = VehicleSimulationPrototypePaths.AssemblyPrefabs + "/" + FileSafe(spec.Id) + ".prefab";
            GameObject root = GameObject.CreatePrimitive(spec.VisualPrimitive);
            root.name = spec.DisplayName + " [project-authored logical proxy]";
            root.transform.localScale = spec.VisualScale;
            Collider collider = root.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            try
            {
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void SetStableId(StableEntityIdAuthoring authoring, string stableId)
        {
            if (!StableEntityId.TryParse(stableId, out _))
            {
                throw new InvalidOperationException("Generated M06 stable ID is not canonical: " + stableId);
            }

            var serialized = new SerializedObject(authoring);
            serialized.FindProperty("stableId").stringValue = stableId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolders()
        {
            VehicleSimulationEditorUtility.EnsureFolder(VehicleSimulationPrototypePaths.PartDefinitions);
            VehicleSimulationEditorUtility.EnsureFolder(VehicleSimulationPrototypePaths.MountDefinitions);
            VehicleSimulationEditorUtility.EnsureFolder(VehicleSimulationPrototypePaths.FastenerDefinitions);
            VehicleSimulationEditorUtility.EnsureFolder(VehicleSimulationPrototypePaths.ToolDefinitions);
            VehicleSimulationEditorUtility.EnsureFolder(VehicleSimulationPrototypePaths.AssemblyPrefabs);
        }

        private static string FileSafe(string id)
        {
            return id.Replace('.', '_');
        }
    }
}
