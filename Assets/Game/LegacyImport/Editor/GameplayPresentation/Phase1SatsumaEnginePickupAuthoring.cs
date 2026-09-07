using System;
using System.Collections.Generic;
using System.IO;
using MSC.Interaction.Query;
using MSC.Interaction.Capabilities;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static class Phase1SatsumaEnginePickupAuthoring
    {
        private const string BlockId = "vehicle.satsuma.part.engine-block";

        public static void RefreshEnginePickupBatch()
        {
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int added = Configure(contents.GetComponent<VehicleAssemblyController>());
                if (added > 0)
                {
                    Directory.CreateDirectory("Logs");
                    File.Copy(path, "Logs/codex-engine-pickup-before-" +
                        DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".prefab", false);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                Debug.Log("SATSUMA_ENGINE_PICKUP_REFRESH_OK changedBindings=" + added);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        public static int Configure(VehicleAssemblyController assembly)
        {
            if (assembly == null)
            {
                throw new InvalidDataException("Engine pickup requires an authored assembly.");
            }

            var engineIds = new HashSet<string>(StringComparer.Ordinal) { BlockId };
            bool changed;
            do
            {
                changed = false;
                foreach (MountPointAuthoring mount in assembly.MountPoints)
                {
                    if (mount?.Definition == null ||
                        !engineIds.Contains(mount.Definition.OwnerPartDefinitionId))
                    {
                        continue;
                    }
                    foreach (string accepted in mount.Definition.AcceptedPartDefinitionIds)
                    {
                        changed |= engineIds.Add(accepted);
                    }
                }
            } while (changed);

            var targets = new List<PartInstance>();
            foreach (PartInstance part in assembly.Parts)
            {
                if (part?.Definition == null || !engineIds.Contains(part.Definition.DefinitionId))
                {
                    continue;
                }
                if (part.IsAssemblyRoot || part.PickupTarget == null ||
                    part.GetComponent<InteractionTargetHost>() == null)
                {
                    throw new InvalidDataException("Missing engine pickup binding: " +
                        part.Definition.DefinitionId);
                }
                targets.Add(part);
            }
            if (!targets.Exists(part => part.Definition.DefinitionId == BlockId))
            {
                throw new InvalidDataException("Engine block is missing from assembly.");
            }

            // Validate every socket before adding anything to the loaded prefab.
            foreach (MountPointAuthoring mount in assembly.MountPoints)
            {
                if (mount?.Definition != null &&
                    engineIds.Contains(mount.Definition.OwnerPartDefinitionId) &&
                    (mount.GetComponent<InteractionTargetHost>() == null ||
                     mount.GetComponent<AssemblyMountHandoffTarget>() == null))
                {
                    throw new InvalidDataException("Missing engine socket interaction: " + mount.MountId);
                }
            }

            int added = 0;
            foreach (PartInstance part in targets)
            {
                AssemblySubassemblyPickupTarget adapter =
                    part.GetComponent<AssemblySubassemblyPickupTarget>();
                InteractionTargetHost host = part.GetComponent<InteractionTargetHost>();
                if (adapter == null)
                {
                    adapter = part.gameObject.AddComponent<AssemblySubassemblyPickupTarget>();
                }
                if (adapter.Controller != assembly || adapter.SurfacePart != part)
                {
                    adapter.Configure(assembly, part);
                    added++;
                }
                if (!host.TryGetCapability(out IPickupTarget pickup) || !ReferenceEquals(pickup, adapter))
                {
                    host.AddCapabilityFirst(adapter);
                    added++;
                }
            }
            foreach (MountPointAuthoring mount in assembly.MountPoints)
            {
                if (mount?.Definition == null ||
                    !engineIds.Contains(mount.Definition.OwnerPartDefinitionId))
                {
                    continue;
                }
                InteractionTargetHost host = mount.GetComponent<InteractionTargetHost>();
                AssemblyMountHandoffTarget target = mount.GetComponent<AssemblyMountHandoffTarget>();
                if (host == null || target == null)
                {
                    throw new InvalidDataException("Missing engine socket interaction: " + mount.MountId);
                }
                AssemblyLooseOwnerMountOcclusionTarget adapter =
                    mount.GetComponent<AssemblyLooseOwnerMountOcclusionTarget>();
                if (adapter == null)
                {
                    adapter = mount.gameObject.AddComponent<AssemblyLooseOwnerMountOcclusionTarget>();
                }
                if (adapter.Controller != assembly || adapter.Mount != mount || adapter.OrdinaryTarget != target)
                {
                    adapter.Configure(assembly, mount, target);
                    added++;
                }
                if (!host.TryGetCapability(out IParentColliderOcclusionBypass bypass) || !ReferenceEquals(bypass, adapter))
                {
                    host.AddCapabilityFirst(adapter);
                    added++;
                }
            }
            return added;
        }
    }
}
