using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Interaction.Query;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Only the eight donor door bolts: mesh ownership/poses stay on their hinged parts.</summary>
    public static class Phase1SatsumaDoorFastenerVisibilityAuthoring
    {
        public static string[] DoorMountIds => new[] { "mount.satsuma.door-left", "mount.satsuma.door-right" };

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Door Bolt Visibility Only")]
        public static void RefreshDoorFastenerVisibilityBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before authoring door bolt visibility.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            if (!File.Exists(path)) throw new InvalidDataException("The existing canonical Satsuma prefab is required.");
            Directory.CreateDirectory("Logs");
            string backup = Path.Combine("Logs", "satsuma-door-bolt-visibility-before-" +
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N") + ".prefab");
            File.Copy(path, backup, overwrite: false);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = ApplyToInstance(contents.GetComponent<VehicleAssemblyController>());
                int repeated = ApplyToInstance(contents.GetComponent<VehicleAssemblyController>());
                if (repeated != 0) throw new InvalidDataException("Door bolt visibility refresh is not idempotent.");
                if (changed > 0 && PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                    throw new IOException("Could not save door bolt visibility.");
                Debug.Log($"SATSUMA_DOOR_BOLT_VISIBILITY_OK changed={changed} repeat={repeated} reviewed=8 fullRebuild=false backup={backup}");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        public static int ApplyToInstance(VehicleAssemblyController assembly)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Door visibility authoring cannot run in Play.");
            if (assembly == null) throw new InvalidDataException("Missing Satsuma assembly.");
            var reviewed = new List<(AssemblyFastenerInteractionTarget target, Renderer renderer)>(8);
            AssemblyFastenerInteractionTarget[] targets = assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true);
            foreach (string mountId in DoorMountIds)
            {
                string slug = mountId.Substring("mount.satsuma.".Length);
                string partId = "vehicle.satsuma.part." + slug;
                MountPointAuthoring[] mounts = assembly.MountPoints.Where(value => value != null && value.MountId == mountId).ToArray();
                PartInstance[] parts = assembly.Parts.Where(value => value != null && value.Definition != null &&
                    value.Definition.DefinitionId == partId).ToArray();
                if (mounts.Length != 1 || parts.Length != 1 || !string.IsNullOrEmpty(parts[0].InitialMountId))
                    throw new InvalidDataException("Expected exactly one initially loose door and its mount: " + mountId);
                MountPointDefinition definition = mounts[0].Definition;
                string[] expectedIds = Enumerable.Range(1, 4).Select(index => "fastener.satsuma." + slug + ".boltpm-" + index).ToArray();
                if (definition == null || definition.FastenerGroup == null || definition.OwnerPartDefinitionId != "vehicle.satsuma.part.body-shell" ||
                    !definition.AcceptedPartDefinitionIds.SequenceEqual(new[] { partId }) ||
                    definition.Fasteners.Length != 4 || definition.Fasteners.Any(value => value == null) ||
                    !definition.Fasteners.Select(value => value.DefinitionId).SequenceEqual(expectedIds) ||
                    definition.Fasteners.Any(value => value.MaximumStage != 8 || value.Size != FastenerSize.Millimeter10 ||
                        !value.InsertedOnInstall || !value.RequiredForRemoval) ||
                    definition.FastenerGroup.AggregateMaximumTightness != 32 || definition.FastenerGroup.BoltedOnThreshold != 28 ||
                    definition.FastenerGroup.BoltedOffThreshold != 0 ||
                    !definition.FastenerGroup.FastenerDefinitionIds.SequenceEqual(expectedIds))
                    throw new InvalidDataException("Door fastener definition differs from the reviewed four 10mm bolts: " + mountId);
                AssemblyFastenerInteractionTarget[] doorTargets = targets.Where(value => value.MountId == mountId).ToArray();
                if (doorTargets.Length != 4 || !doorTargets.Select(value => value.FastenerDefinitionId)
                    .OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(expectedIds))
                    throw new InvalidDataException("Missing or duplicate reviewed door fastener targets: " + mountId);
                foreach (AssemblyFastenerInteractionTarget target in doorTargets)
                {
                    InteractionTargetHost host = target.GetComponent<InteractionTargetHost>();
                    Renderer[] visuals = host?.OutlineRenderers?.ToArray() ?? Array.Empty<Renderer>();
                    Renderer renderer = visuals.Length == 1 ? visuals[0] : null;
                    var serialized = new SerializedObject(target);
                    if (target.Controller != assembly || renderer == null || renderer.transform.parent != parts[0].transform ||
                        serialized.FindProperty("fastenerPresentation").objectReferenceValue != renderer.transform ||
                        target.GetComponent<Collider>() == null || target.GetComponentsInChildren<Renderer>(true).Length != 0 ||
                        (target.InstalledOnlyExternalPresentationRenderer != null && target.InstalledOnlyExternalPresentationRenderer != renderer) ||
                        reviewed.Any(value => value.renderer == renderer))
                        throw new InvalidDataException("Door bolt must keep its one explicitly owned external renderer: " + target.FastenerDefinitionId);
                    reviewed.Add((target, renderer));
                }
            }
            // Validate both complete doors before the first mutation. A partial
            // old/new binding cohort is repairable; a foreign binding is not.
            int changed = 0;
            foreach (var binding in reviewed)
            {
                bool needsChange = binding.target.InstalledOnlyExternalPresentationRenderer != binding.renderer || binding.renderer.enabled;
                if (!needsChange) continue;
                binding.target.ConfigureInstalledOnlyExternalPresentationRenderer(binding.renderer);
                binding.renderer.enabled = false;
                EditorUtility.SetDirty(binding.target);
                EditorUtility.SetDirty(binding.renderer);
                changed++;
            }
            return changed;
        }
    }
}
