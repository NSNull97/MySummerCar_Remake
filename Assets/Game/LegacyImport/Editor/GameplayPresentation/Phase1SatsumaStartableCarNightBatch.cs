using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Core.Identity;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Existing-prefab-only night packet. Purchased item presentation is a separate Items.Editor step.</summary>
    public static class Phase1SatsumaStartableCarNightBatch
    {
        public const string GeneratedRoot = Phase1SatsumaConsumableMountAuthoring.CanonicalRoot;
        // Exact accepted V66 rosters before this packet; ordinal IDs joined with LF, UTF-8, no final LF.
        private const string BaseMountHash = "9B98558B6733842418C22944CCA3699DB19B905DC6115C2A2108F63F6A35C0FE";
        private const string BaseFastenerHash = "9974560CA1688C47BC783C5BB554DE350F6621A943EDFD2B0982D5B93AD2DE75";

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Startable Car Night Packet Only")]
        public static void RefreshStartableCarNightBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before the scoped night refresh.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing == null || !File.Exists(path))
                throw new InvalidDataException("The existing canonical Satsuma is required; this command never rebuilds it.");
            VehicleAssemblyController authored = existing.GetComponent<VehicleAssemblyController>();
            ValidateTopology(authored, false);
            string backup = CreateBackup(authored, path);
            GameObject contents = null;
            bool prefabSaved = false;
            try
            {
                // Each importer hash-checks its entire frozen source packet before its first import.
                // They are intentionally outside the loaded-prefab pass and do not touch Items scenes.
                Phase1SatsumaInstalledBeltAssets.ImportReviewedAssets();
                Phase1SatsumaEngineAudioImporter.Build();
                contents = PrefabUtility.LoadPrefabContents(path);
                var assembly = contents.GetComponent<VehicleAssemblyController>();
                int changed = ApplyToInstance(assembly);
                int repeated = ApplyToInstance(assembly);
                if (repeated != 0)
                    throw new InvalidDataException("Night packet is not idempotent: second pass changed " + repeated + " bindings.");
                NightTopology topology = ValidateTopology(assembly, true);
                if (changed > 0)
                {
                    if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                        throw new IOException("Could not save the scoped Satsuma night prefab.");
                    prefabSaved = true;
                }
                SaveDefinitionAssets(assembly);
                Debug.Log($"SATSUMA_STARTABLE_CAR_NIGHT_OK changed={changed} repeat={repeated} " +
                    $"parts={topology.PartCount} mounts={topology.MountCount} fasteners={topology.FastenerCount} " +
                    $"consumableMounts={topology.ConsumableMountCount} fullRebuild=false nativeSaveWrites=false backup={backup}");
            }
            catch (Exception failure)
            {
                // Some scoped authorers persist new definitions immediately. Do not imply
                // atomic rollback or silently save/discard a partially changed asset graph.
                Debug.LogError("SATSUMA_STARTABLE_CAR_NIGHT_FAILED backup=" + backup +
                    " prefabSaved=" + prefabSaved + " generated assets may be partially updated or dirty; " +
                    "no automatic rollback and no native save writes. Inspect the failure before retrying. " + failure.Message);
                throw;
            }
            finally
            {
                if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>Authoring only: no scene loads, imports, full rebuild, graph initialization or native storage.</summary>
        public static int ApplyToInstance(VehicleAssemblyController assembly, string generatedRoot = null)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Night authoring cannot run in Play.");
            ValidateTopology(assembly, false);
            string root = generatedRoot ?? GeneratedRoot;
            PartInstance[] originalParts = (PartInstance[])assembly.Parts.Clone();
            string[] originalPartIds = originalParts.Select(part => part.StableId.Value).ToArray();
            ToolDefinition screwdriver = Phase1SatsumaEngineScrewdriverAuthoring.GetOrCreateScrewdriver(root + "/ToolDefinitions");
            int changed = Phase1SatsumaEngineScrewdriverAuthoring.Configure(assembly, screwdriver);
            changed += Phase1SatsumaHoseClampAuthoring.ApplyToInstance(assembly, root);
            changed += Phase1SatsumaStockMountFastenerAuthoring.ApplyToInstance(assembly, root);
            changed += Phase1SatsumaFuelLineConnectionAuthoring.ApplyToInstance(assembly, root);
            changed += Phase1SatsumaHeadlightFastenerAuthoring.ApplyToInstance(assembly, root);
            changed += Phase1SatsumaMountFrameRepairAuthoring.ApplyToInstance(assembly);
            changed += Phase1SatsumaCabinControlFrameAuthoring.ApplyToInstance(assembly, root);
            changed += Phase1SatsumaDoorFastenerVisibilityAuthoring.ApplyToInstance(assembly);
            changed += Phase1SatsumaElectricalTerminalPoseAuthoring.ApplyToInstance(assembly.GetComponent<SatsumaElectricalSystem>());
            Phase1SatsumaConsumableMountAuthoring.DefinitionSet consumables =
                Phase1SatsumaConsumableMountAuthoring.GetOrCreateDefinitions(assembly, root);
            changed += Phase1SatsumaConsumableMountAuthoring.ApplyToInstance(assembly, consumables);
            // Lighting explicitly requires the two purchased-bulb sockets above.
            changed += Phase1SatsumaDashboardControlsAuthoring.ApplyToInstance(assembly, root);
            changed += Phase1SatsumaStartReadinessAuthoring.ApplyToInstance(assembly);
            changed += Phase1SatsumaEngineFeedbackAuthoring.ApplyToInstance(assembly);
            if (!assembly.Parts.SequenceEqual(originalParts) ||
                !assembly.Parts.Select(part => part.StableId.Value).SequenceEqual(originalPartIds))
                throw new InvalidDataException("The night packet changed the fixed part roster or stable identities.");
            ValidateTopology(assembly, true);
            return changed;
        }

        public static NightTopology ValidateTopology(VehicleAssemblyController assembly, bool requireComplete)
        {
            if (assembly == null || assembly.GetComponents<VehicleAssemblyController>().Length != 1 ||
                assembly.GetComponent<StableEntityIdAuthoring>()?.SerializedId != Phase1SatsumaBaselineBuilder.StableVehicleId)
                throw new InvalidDataException("Expected the existing canonical stable Satsuma root.");
            PartInstance[] parts = assembly.Parts;
            if (parts == null || parts.Length != 126 || parts.Any(part => part == null || part.Definition == null) ||
                parts.Distinct().Count() != parts.Length ||
                parts.Select(part => part.StableId.Value).Any(string.IsNullOrWhiteSpace) ||
                parts.Select(part => part.StableId.Value).Distinct(StringComparer.Ordinal).Count() != parts.Length)
                throw new InvalidDataException("The night packet preserves exactly 126 unique fixed parts.");
            MountPointAuthoring[] mounts = assembly.MountPoints;
            if (mounts == null || mounts.Any(mount => mount == null || mount.Definition == null ||
                    string.IsNullOrWhiteSpace(mount.MountId) || mount.Definition.DefinitionId != mount.MountId) ||
                mounts.Select(mount => mount.MountId).Distinct(StringComparer.Ordinal).Count() != mounts.Length)
                throw new InvalidDataException("Mount identities must be unique and explicitly bound.");
            string[] consumableIds = Phase1SatsumaConsumableMountAuthoring.MountIds;
            int consumableCount = mounts.Count(mount => consumableIds.Contains(mount.MountId));
            string[] baseMounts = mounts.Select(mount => mount.MountId).Except(consumableIds, StringComparer.Ordinal).ToArray();
            if (consumableCount is not (0 or 7) || baseMounts.Length != 117 || IdHash(baseMounts) != BaseMountHash)
                throw new InvalidDataException("Only the exact original 117 mounts plus the complete seven consumable sockets are supported.");
            AssemblyFastenerInteractionTarget[] targets = assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true);
            if (targets.Any(target => target.Controller != assembly || string.IsNullOrWhiteSpace(target.FastenerDefinitionId)) ||
                targets.Select(target => target.FastenerDefinitionId).Distinct(StringComparer.Ordinal).Count() != targets.Length)
                throw new InvalidDataException("Fastener targets must have unique reviewed identities and the correct aggregate.");
            string[] stockIds = Phase1SatsumaStockMountFastenerAuthoring.GetBindings().Select(binding => binding.FastenerId).ToArray();
            string[] headlightIds = Phase1SatsumaHeadlightFastenerAuthoring.GetBindings().Select(binding => binding.FastenerId).ToArray();
            string[] plugIds = Enumerable.Range(1, 4).Select(SatsumaConsumableAssemblyRules.SparkPlugFastenerId).ToArray();
            int stockCount = targets.Count(target => stockIds.Contains(target.FastenerDefinitionId));
            int headlightCount = targets.Count(target => headlightIds.Contains(target.FastenerDefinitionId));
            int plugCount = targets.Count(target => plugIds.Contains(target.FastenerDefinitionId));
            string[] baseTargets = targets.Select(target => target.FastenerDefinitionId)
                .Except(stockIds, StringComparer.Ordinal).Except(headlightIds, StringComparer.Ordinal)
                .Except(plugIds, StringComparer.Ordinal).ToArray();
            // The live-engine packet retired exactly eight valve pseudo-bolts.
            // Reconstruct only that reviewed cohort for the frozen identity hash;
            // accepting a smaller count alone would permit unrelated missing bolts.
            bool valvesRetired = SatsumaRockerShaftFastenerMigration.IsCanonicalShape(targets
                .Where(target => target.MountId == SatsumaRockerShaftFastenerMigration.MountId)
                .Select(target => target.FastenerDefinitionId).ToArray());
            int retiredCount = valvesRetired ? 8 : 0;
            IEnumerable<string> historicalBase = valvesRetired
                ? baseTargets.Concat(SatsumaRockerShaftFastenerMigration.RetiredIds) : baseTargets;
            if (stockCount is not (0 or 21) || headlightCount is not (0 or 4) ||
                (headlightCount == 4 && stockCount != 21) || plugCount != (consumableCount == 7 ? 4 : 0) ||
                baseTargets.Length != 273 - retiredCount || IdHash(historicalBase) != BaseFastenerHash)
                throw new InvalidDataException("Only exact Stock21, optional whole Headlight4 after Stock21, +4 purchased-plug threads and the exact eight-valve retirement may change the 273-target roster.");
            var declared = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (MountPointAuthoring mount in mounts)
                for (int index = 0; index < mount.Definition.Fasteners.Length; index++)
                {
                    FastenerDefinition fastener = mount.Definition.Fasteners[index];
                    if (fastener == null || string.IsNullOrWhiteSpace(fastener.DefinitionId))
                        throw new InvalidDataException("Missing declared fastener definition: mount=" + mount.MountId +
                            " index=" + index + " asset=" + AssetDatabase.GetAssetPath(mount.Definition) +
                            " groupIds=" + string.Join(",", mount.Definition.FastenerGroup?.FastenerDefinitionIds ?? Array.Empty<string>()));
                    if (!declared.TryAdd(fastener.DefinitionId, mount.MountId))
                        throw new InvalidDataException("Duplicate declared fastener definition: mount=" + mount.MountId +
                            " index=" + index + " fastener=" + fastener.DefinitionId + " firstMount=" + declared[fastener.DefinitionId]);
                }
            if (declared.Count != targets.Length || targets.Any(target =>
                !declared.TryGetValue(target.FastenerDefinitionId, out string mountId) || mountId != target.MountId))
                throw new InvalidDataException("Declared fasteners and interaction targets do not form the same exact mount-owned roster.");
            if (requireComplete && (stockCount != 21 || headlightCount != 4 || consumableCount != 7 || targets.Length != 302 - retiredCount))
                throw new InvalidDataException("The complete night topology must be 126 parts / 124 mounts / 302 historical or 294 reviewed post-valve fasteners.");
            return new NightTopology(parts.Length, mounts.Length, targets.Length, consumableCount);
        }

        private static string IdHash(IEnumerable<string> ids)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                string.Join("\n", ids.OrderBy(id => id, StringComparer.Ordinal))))).Replace("-", "");
        }

        private static void SaveDefinitionAssets(VehicleAssemblyController assembly)
        {
            foreach (MountPointAuthoring mount in assembly.MountPoints)
            {
                AssetDatabase.SaveAssetIfDirty(mount.Definition);
                foreach (FastenerDefinition fastener in mount.Definition.Fasteners) AssetDatabase.SaveAssetIfDirty(fastener);
            }
            foreach (ToolDefinition tool in assembly.Tools) if (tool != null) AssetDatabase.SaveAssetIfDirty(tool);
        }

        private static string CreateBackup(VehicleAssemblyController assembly, string prefabPath)
        {
            string backup = "Logs/satsuma-startable-night-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
            var paths = new HashSet<string>(StringComparer.Ordinal) { prefabPath };
            foreach (MountPointAuthoring mount in assembly.MountPoints)
            {
                AddAssetPath(paths, mount.Definition);
                foreach (FastenerDefinition fastener in mount.Definition.Fasteners) AddAssetPath(paths, fastener);
            }
            foreach (ToolDefinition tool in assembly.Tools) AddAssetPath(paths, tool);
            foreach (string folder in new[] { GeneratedRoot + "/LoosePartDefinitions", GeneratedRoot + "/FastenerDefinitions",
                GeneratedRoot + "/MountDefinitions", GeneratedRoot + "/ToolDefinitions", Phase1SatsumaEngineAudioImporter.OutputRoot })
                if (Directory.Exists(folder))
                    foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)) paths.Add(path.Replace('\\', '/'));
            foreach (string path in new[] { Phase1SatsumaInstalledBeltAssets.MeshPath, Phase1SatsumaInstalledBeltAssets.MaterialPath,
                GeneratedRoot + "/Textures/" + Phase1SatsumaInstalledBeltAssets.TextureGuid + ".png",
                GeneratedRoot + "/InstalledAlternatorBeltPresentation.prefab", GeneratedRoot + "/VehicleItemPartCatalog.asset" })
                paths.Add(path);
            Directory.CreateDirectory(backup);
            foreach (string path in paths.ToArray()) if (File.Exists(path + ".meta")) paths.Add(path + ".meta");
            foreach (string path in paths.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!File.Exists(path)) continue;
                if (!path.StartsWith(GeneratedRoot + "/", StringComparison.Ordinal) &&
                    !path.StartsWith(Phase1SatsumaEngineAudioImporter.OutputRoot + "/", StringComparison.Ordinal))
                    throw new InvalidDataException("Refusing an out-of-scope night backup asset: " + path);
                string destination = backup + "/" + path;
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(path, destination, false);
            }
            Debug.Log("SATSUMA_STARTABLE_CAR_NIGHT_BACKUP " + backup);
            return backup;
        }

        private static void AddAssetPath(HashSet<string> paths, UnityEngine.Object asset)
        {
            if (asset == null) return;
            string path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path)) paths.Add(path);
        }

        public readonly struct NightTopology
        {
            public NightTopology(int parts, int mounts, int fasteners, int consumableMounts)
            { PartCount = parts; MountCount = mounts; FastenerCount = fasteners; ConsumableMountCount = consumableMounts; }
            public int PartCount { get; }
            public int MountCount { get; }
            public int FastenerCount { get; }
            public int ConsumableMountCount { get; }
        }
    }
}
