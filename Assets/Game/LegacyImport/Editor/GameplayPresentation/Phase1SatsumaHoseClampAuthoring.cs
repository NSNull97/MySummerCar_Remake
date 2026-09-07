using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Four frozen stock hose clamps omitted by the engine-only screw pass.</summary>
    public static class Phase1SatsumaHoseClampAuthoring
    {
        public const string ScrewMeshGuid = "d42cbf361095bad4f9091a43a07fe91a";
        public const string NutMeshGuid = "e711c8a15b1135c4089caad19b8f56e8";
        public const float PresentationScale = 0.52f; // marker .65 * child .8
        private const float Tolerance = 0.00001f;

        public static Binding[] GetBindings() => new[]
        {
            new Binding("radiator-hose1", 1, 40414, 105316, 6,
                new Vector3(.05401f, .15575f, .06374f),
                new Quaternion(7.7600527e-7f, .48557273f, 2.592924e-8f, .8741963f)),
            new Binding("radiator-hose1", 2, 52612, 108796, 6,
                new Vector3(-.0266f, -.0536f, -.026f),
                new Quaternion(.024677955f, .7066761f, -.024677392f, .706676f)),
            new Binding("radiator-hose3", 1, 39723, 105152, 6,
                new Vector3(-.00075f, .34737f, .04242f),
                new Quaternion(-2.6928038e-7f, -.49197975f, -9.925079e-8f, .87060666f)),
            new Binding("radiator-hose3", 2, 68964, 113546, 7,
                new Vector3(-.03601f, -.14041f, .04106f),
                new Quaternion(-.478132f, .5209503f, .47813278f, .52095073f)),
        };

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Stock Hose Clamps Only")]
        public static void RefreshHoseClampsBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before authoring hose clamps.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = ApplyToInstance(root.GetComponent<VehicleAssemblyController>());
                if (changed > 0 && PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new InvalidDataException("Could not save hose clamp bindings.");
                foreach (Binding binding in GetBindings())
                    AssetDatabase.SaveAssetIfDirty(root.GetComponent<VehicleAssemblyController>().MountPoints
                        .Single(m => m.MountId == binding.MountId).Definition.Fasteners
                        .Single(f => f.DefinitionId == binding.FastenerId));
                Debug.Log("SATSUMA_HOSE_CLAMPS_REFRESH_OK changed=" + changed + " reviewed=4 fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static int ApplyToInstance(VehicleAssemblyController assembly, string generatedRoot = null)
        {
            string root = generatedRoot ?? Phase1SatsumaCamshaftTimingAuthoring.GeneratedRoot;
            Mesh screw = AssetDatabase.LoadAssetAtPath<Mesh>(root + "/Meshes/" + ScrewMeshGuid + ".asset");
            Mesh nut = AssetDatabase.LoadAssetAtPath<Mesh>(root + "/Meshes/" + NutMeshGuid + ".asset");
            ToolDefinition tool = assembly?.Tools.SingleOrDefault(t => t != null &&
                t.DefinitionId == SatsumaAuxiliaryAssemblyTools.ScrewdriverDefinitionId);
            return Configure(assembly, screw, nut, tool);
        }

        // Separate typed assets make guard/idempotence tests independent of generated content.
        public static int Configure(VehicleAssemblyController assembly, Mesh screw, Mesh legacyNut,
            ToolDefinition screwdriver)
        {
            if (assembly == null || screw == null || legacyNut == null || screw == legacyNut ||
                screwdriver == null || screwdriver.ToolType != SatsumaAuxiliaryAssemblyTools.ScrewdriverType ||
                screwdriver.Size != FastenerSize.None || !assembly.Tools.Contains(screwdriver))
                throw new InvalidDataException("Hose clamps require distinct reviewed meshes and the registered screwdriver.");
            AssemblyFastenerInteractionTarget[] all = assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true);
            var work = new List<(FastenerDefinition definition, AssemblyFastenerInteractionTarget target, Transform visual, MeshFilter filter)>();
            foreach (Binding binding in GetBindings())
            {
                MountPointAuthoring[] mounts = assembly.MountPoints.Where(m => m != null && m.MountId == binding.MountId).ToArray();
                if (mounts.Length != 1 || mounts[0].Definition == null || mounts[0].Definition.Fasteners.Length != 2)
                    throw new InvalidDataException("Expected exact two-clamp stock hose: " + binding.MountId);
                FastenerDefinition[] definitions = mounts[0].Definition.Fasteners.Where(f => f != null && f.DefinitionId == binding.FastenerId).ToArray();
                AssemblyFastenerInteractionTarget[] targets = all.Where(t => t.Controller == assembly &&
                    t.MountId == binding.MountId && t.FastenerDefinitionId == binding.FastenerId).ToArray();
                if (definitions.Length != 1 || targets.Length != 1)
                    throw new InvalidDataException("Missing or duplicate hose clamp: " + binding.FastenerId);
                FastenerDefinition definition = definitions[0];
                AssemblyFastenerInteractionTarget target = targets[0];
                bool oldRule = definition.ToolRule.ToolType == "Wrench" &&
                    definition.ToolRule.FastenerSize == (FastenerSize)binding.LegacySize;
                bool newRule = definition.ToolRule.ToolType == SatsumaAuxiliaryAssemblyTools.ScrewdriverType &&
                    definition.ToolRule.FastenerSize == FastenerSize.None;
                var data = new SerializedObject(target);
                Transform visual = data.FindProperty("fastenerPresentation").objectReferenceValue as Transform;
                MeshFilter filter = visual != null ? visual.GetComponent<MeshFilter>() : null;
                ToolDefinition oldTool = data.FindProperty("tool").objectReferenceValue as ToolDefinition;
                bool validTool = oldTool == screwdriver || oldTool != null && oldTool.ToolType == "Wrench" &&
                    oldTool.Size == (FastenerSize)binding.LegacySize;
                float travel = target.FastenerPresentationStageTravelScale;
                if (definition.Size != (FastenerSize)binding.LegacySize || definition.MaximumStage != 8 ||
                    !definition.InsertedOnInstall || !definition.RequiredForRemoval || (!oldRule && !newRule) ||
                    !validTool || target.transform.parent != mounts[0].transform ||
                    Vector3.Distance(target.transform.localPosition, binding.Pose.position) > Tolerance ||
                    Quaternion.Angle(target.transform.localRotation, binding.Pose.rotation) > .05f ||
                    Vector3.Distance(target.transform.localScale, Vector3.one) > Tolerance ||
                    visual == null || visual.parent != target.transform || filter == null ||
                    (filter.sharedMesh != legacyNut && filter.sharedMesh != screw) ||
                    visual.localPosition.sqrMagnitude > Tolerance * Tolerance ||
                    Quaternion.Angle(visual.localRotation, Quaternion.identity) > .05f ||
                    (Vector3.Distance(visual.localScale, Vector3.one * .65f) > Tolerance &&
                     Vector3.Distance(visual.localScale, Vector3.one * PresentationScale) > Tolerance) ||
                    (Mathf.Abs(travel - 1f) > Tolerance && Mathf.Abs(travel) > Tolerance))
                    throw new InvalidDataException("Hose clamp differs from both reviewed authoring shapes: " + binding.FastenerId);
                work.Add((definition, target, visual, filter));
            }
            // Full preflight before the first definition or prefab mutation.
            int changed = 0;
            foreach (var item in work)
            {
                if (item.definition.ToolRule.ToolType != SatsumaAuxiliaryAssemblyTools.ScrewdriverType)
                {
                    FastenerDefinition f = item.definition;
                    f.Configure(f.DefinitionId, f.DisplayName, f.Size, f.MaximumStage, f.TighteningDirection,
                        f.InsertedOnInstall, f.RequiredForRemoval, SatsumaAuxiliaryAssemblyTools.ScrewdriverRule());
                    EditorUtility.SetDirty(f); changed++;
                }
                var data = new SerializedObject(item.target);
                if (data.FindProperty("tool").objectReferenceValue != screwdriver ||
                    item.target.FastenerPresentationStageTravelScale != 0f)
                {
                    data.FindProperty("tool").objectReferenceValue = screwdriver;
                    // Original Screw FSMs 105316/108796/105152/113546 disable SetPosition
                    // in EVERY stage 0..8 (actionEnabled 0001). This is rotation only.
                    data.FindProperty("fastenerPresentationStageTravelScale").floatValue = 0f;
                    data.ApplyModifiedPropertiesWithoutUndo(); changed++;
                }
                if (item.filter.sharedMesh != screw) { item.filter.sharedMesh = screw; changed++; }
                Vector3 scale = Vector3.one * PresentationScale;
                if (Vector3.Distance(item.visual.localScale, scale) > Tolerance)
                { item.visual.localScale = scale; changed++; }
            }
            return changed;
        }

        public readonly struct Binding
        {
            public Binding(string slug, int number, long marker, long screw, int legacySize, Vector3 position, Quaternion rotation)
            { MountId = "mount.satsuma." + slug; FastenerId = "fastener.satsuma." + slug + ".boltpm-" + number;
                MarkerId = marker; ScrewId = screw; LegacySize = legacySize; Pose = new Pose(position, rotation); }
            public string MountId { get; }
            public string FastenerId { get; }
            public long MarkerId { get; }
            public long ScrewId { get; }
            public int LegacySize { get; }
            public Pose Pose { get; }
        }
    }
}
