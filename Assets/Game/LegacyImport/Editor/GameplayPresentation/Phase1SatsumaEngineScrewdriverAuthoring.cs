using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Explicit tool bindings for three donor-proved mounting screws, not tuning screws.</summary>
    public static class Phase1SatsumaEngineScrewdriverAuthoring
    {
        private const string CanonicalToolFolder =
            "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/ToolDefinitions";

        internal static readonly string[] MountIds =
        {
            "mount.satsuma.engine-block.alternator",
            "mount.satsuma.engine-block.distributor",
            "mount.satsuma.engine-block.radiator-hose2",
        };

        internal static readonly string[] FastenerIds =
        {
            "fastener.satsuma.engine-block-alternator.boltpm-3",
            "fastener.satsuma.engine-block-distributor.boltpm-1",
            "fastener.satsuma.engine-block-radiator-hose2.boltpm-1",
        };

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Engine Screwdriver Bindings")]
        public static void RefreshEngineScrewdriverBatch()
        {
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            GameObject contents = PrefabUtility.LoadPrefabContents(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            try
            {
                VehicleAssemblyController assembly = contents.GetComponent<VehicleAssemblyController>();
                ToolDefinition screwdriver = GetOrCreateScrewdriver(CanonicalToolFolder);
                int changes = Configure(assembly, screwdriver);
                foreach (MountPointAuthoring mount in assembly.MountPoints)
                {
                    foreach (FastenerDefinition fastener in mount.Definition.Fasteners)
                    {
                        if (Array.IndexOf(FastenerIds, fastener.DefinitionId) >= 0)
                        {
                            AssetDatabase.SaveAssetIfDirty(fastener);
                        }
                    }
                }
                if (changes > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents,
                        Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
                }
                Debug.Log("SATSUMA_ENGINE_SCREWDRIVER_REFRESH_OK changes=" + changes +
                    " reviewedClamps=3 sparkPlugBridge=false");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // The full builder passes its current ToolDefinitionRoot so transactional
        // staging never acquires references to the previous canonical generation.
        public static ToolDefinition GetOrCreateScrewdriver(string toolDefinitionFolder)
        {
            if (string.IsNullOrWhiteSpace(toolDefinitionFolder) ||
                !AssetDatabase.IsValidFolder(toolDefinitionFolder))
            {
                throw new InvalidDataException("An existing generated tool-definition folder is required.");
            }
            string path = toolDefinitionFolder.TrimEnd('/') + "/screwdriver.asset";
            ToolDefinition tool = AssetDatabase.LoadAssetAtPath<ToolDefinition>(path);
            if (tool == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    throw new InvalidDataException("Screwdriver asset path contains a different asset type.");
                }
                tool = SatsumaAuxiliaryAssemblyTools.CreateScrewdriver();
                AssetDatabase.CreateAsset(tool, path);
            }
            ValidateTool(tool);
            return tool;
        }

        /// <returns>Count of changed definitions, target bindings and tool registrations.</returns>
        public static int Configure(VehicleAssemblyController assembly, ToolDefinition screwdriver)
        {
            ValidateTool(screwdriver);
            if (assembly == null)
            {
                throw new InvalidDataException("Engine screwdriver requires an authored assembly.");
            }

            var fasteners = new List<FastenerDefinition>();
            var targets = new List<AssemblyFastenerInteractionTarget>();
            AssemblyFastenerInteractionTarget[] allTargets =
                assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true);
            // Resolve every exact binding before changing shared definition assets.
            for (int index = 0; index < MountIds.Length; index++)
            {
                MountPointAuthoring[] matchingMounts = assembly.MountPoints.Where(mount =>
                    mount?.Definition != null && mount.MountId == MountIds[index]).ToArray();
                if (matchingMounts.Length != 1)
                {
                    throw new InvalidDataException("Expected one reviewed screw mount: " + MountIds[index]);
                }
                FastenerDefinition[] matchingFasteners = matchingMounts[0].Definition.Fasteners
                    .Where(fastener => fastener != null &&
                        fastener.DefinitionId == FastenerIds[index]).ToArray();
                AssemblyFastenerInteractionTarget[] matchingTargets = allTargets.Where(target =>
                    target.Controller == assembly && target.MountId == MountIds[index] &&
                    target.FastenerDefinitionId == FastenerIds[index]).ToArray();
                if (matchingFasteners.Length != 1 || matchingTargets.Length != 1)
                {
                    throw new InvalidDataException("Expected one reviewed screw and interaction: " +
                        FastenerIds[index]);
                }
                FastenerDefinition reviewed = matchingFasteners[0];
                bool legacyRule = reviewed.ToolRule.ToolType == "Wrench" &&
                    reviewed.ToolRule.FastenerSize == FastenerSize.Millimeter6;
                bool currentRule = reviewed.ToolRule.ToolType == SatsumaAuxiliaryAssemblyTools.ScrewdriverType &&
                    reviewed.ToolRule.FastenerSize == FastenerSize.None;
                if (reviewed.Size != FastenerSize.Millimeter6 ||
                    (!legacyRule && !currentRule))
                {
                    throw new InvalidDataException("Reviewed mounting-screw contract drifted: " + reviewed.DefinitionId);
                }
                fasteners.Add(matchingFasteners[0]);
                targets.Add(matchingTargets[0]);
            }
            ToolDefinition[] registered = assembly.Tools.Where(tool => tool != null &&
                tool.DefinitionId == screwdriver.DefinitionId).ToArray();
            if (registered.Length > 1)
            {
                throw new InvalidDataException("Duplicate screwdriver stable tool definition.");
            }

            int changes = 0;
            foreach (FastenerDefinition fastener in fasteners)
            {
                if (fastener.ToolRule.ToolType == SatsumaAuxiliaryAssemblyTools.ScrewdriverType &&
                    fastener.ToolRule.FastenerSize == FastenerSize.None)
                {
                    continue;
                }
                // Size=6 is preserved as legacy authored presentation metadata;
                // only the explicit semantic tool rule changes to Screwdriver/0.
                fastener.Configure(fastener.DefinitionId, fastener.DisplayName, fastener.Size,
                    fastener.MaximumStage, fastener.TighteningDirection, fastener.InsertedOnInstall,
                    fastener.RequiredForRemoval, SatsumaAuxiliaryAssemblyTools.ScrewdriverRule());
                EditorUtility.SetDirty(fastener);
                changes++;
            }
            foreach (AssemblyFastenerInteractionTarget target in targets)
            {
                var serialized = new SerializedObject(target);
                SerializedProperty toolReference = serialized.FindProperty("tool");
                if (toolReference.objectReferenceValue == screwdriver)
                {
                    continue;
                }
                // Recalling Configure would recapture the moving presentation's
                // base pose. Change only the tool reference instead.
                toolReference.objectReferenceValue = screwdriver;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changes++;
            }
            if (registered.Length == 0 || registered[0] != screwdriver)
            {
                var serialized = new SerializedObject(assembly);
                SerializedProperty tools = serialized.FindProperty("tools");
                int index = registered.Length == 0 ? tools.arraySize :
                    Array.IndexOf(assembly.Tools, registered[0]);
                if (index == tools.arraySize)
                {
                    tools.arraySize++;
                }
                tools.GetArrayElementAtIndex(index).objectReferenceValue = screwdriver;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changes++;
            }
            return changes;
        }

        private static void ValidateTool(ToolDefinition tool)
        {
            if (tool == null || tool.DefinitionId != SatsumaAuxiliaryAssemblyTools.ScrewdriverDefinitionId ||
                tool.ToolType != SatsumaAuxiliaryAssemblyTools.ScrewdriverType || tool.Size != FastenerSize.None)
            {
                throw new InvalidDataException("Expected the dedicated unsized screwdriver definition.");
            }
        }
    }
}
