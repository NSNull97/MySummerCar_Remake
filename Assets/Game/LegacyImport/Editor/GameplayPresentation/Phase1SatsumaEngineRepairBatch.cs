using System;
using System.IO;
using System.Linq;
using MSC.Vehicle.Assembly;
using MSC.Vehicle;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static class Phase1SatsumaEngineRepairBatch
    {
        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Engine Repair Packet Only")]
        public static void RefreshEngineRepairBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before engine authoring.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            Phase1SatsumaEngineFastenerTravel.ValidateFrozenSource();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            const string generatedRoot = "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                VehicleAssemblyController assembly = root.GetComponent<VehicleAssemblyController>();
                string backup = "Logs/engine-repair-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
                Directory.CreateDirectory(backup);
                File.Copy(path, backup + "/Satsuma.prefab");
                foreach (MountPointDefinition definition in assembly.MountPoints.Select(value => value.Definition).Distinct())
                {
                    string source = AssetDatabase.GetAssetPath(definition);
                    File.Copy(source, backup + "/" + Path.GetFileName(source));
                }
                int changes = Apply(root, assembly, generatedRoot);
                int repeatChanges = Apply(root, assembly, generatedRoot);
                if (repeatChanges != 0) throw new InvalidDataException("Engine packet is not idempotent: " + repeatChanges);
                if (changes > 0)
                {
                    if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                        throw new InvalidDataException("Engine repair prefab save failed.");
                    foreach (MountPointAuthoring mount in assembly.MountPoints)
                        if (mount.MountId == SatsumaRockerCoverFastenerMigration.MountId ||
                            mount.MountId == SatsumaCarburetorFastenerMigration.MountId ||
                            mount.MountId == "mount.satsuma.engine-assembly")
                            AssetDatabase.SaveAssetIfDirty(mount.Definition);
                }
                Debug.Log("SATSUMA_ENGINE_REPAIR_PACKET_OK changed=" + changes + " repeat=" + repeatChanges +
                    " targets=" + root.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true).Length +
                    " compoundParts=" + root.GetComponent<AssemblyLooseCompoundPhysics>().Bindings.Length +
                    " fullRebuild=false backup=" + backup);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static int Apply(GameObject root, VehicleAssemblyController assembly, string generatedRoot)
        {
            int changed = Phase1SatsumaEngineFastenerPresentation.ApplyReviewedMeshes(root, generatedRoot);
            changed += Phase1SatsumaEngineAdditionalFastenerPresentation.ApplyReviewedMeshes(root, generatedRoot);
            changed += Phase1SatsumaEngineCapAndCoverAuthoring.Configure(root, generatedRoot);
            changed += Phase1SatsumaEngineAdjustmentsAuthoring.ApplyToInstance(assembly, generatedRoot);
            changed += Phase1SatsumaEngineFastenerTravel.ApplyReviewedTravel(root);
            changed += Phase1SatsumaEngineCompoundPhysicsAuthoring.Configure(assembly);
            changed += Phase1SatsumaEngineDockingAuthoring.ApplyToInstance(assembly);
            return changed;
        }

        // Deliberately read-only with respect to native storage. The explicit
        // CLI argument is local test input, never a runtime machine path.
        public static void ValidateNativeEngineSaveBatch()
        {
            string[] args = Environment.GetCommandLineArgs();
            int argument = Array.IndexOf(args, "-engineSavePath");
            if (argument < 0 || argument + 1 >= args.Length) throw new ArgumentException("Missing -engineSavePath.");
            string json = File.ReadAllText(args[argument + 1]);
            NativeProjection document = JsonUtility.FromJson<NativeProjection>(json);
            DomainProjection domain = document.Domains.Single(value => value.DomainId == "vehicle.satsuma");
            VehicleDomainSaveDto vehicles = JsonUtility.FromJson<VehicleDomainSaveDto>(domain.PayloadJson);
            VehicleSaveRecordDto record = vehicles.vehicles.Single(value => value.stableVehicleId ==
                Phase1SatsumaBaselineBuilder.StableVehicleId);
            GameObject instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath));
            try
            {
                VehicleAssemblyController assembly = instance.GetComponent<VehicleAssemblyController>();
                string before = JsonUtility.ToJson(record.assembly);
                AssemblyOperationResult valid = assembly.ValidateSaveDataForRestore(record.assembly);
                if (!valid.Succeeded) throw new InvalidDataException("Native engine save validation: " + valid.Message);
                AssemblyOperationResult restored = assembly.RestoreSaveData(record.assembly);
                if (!restored.Succeeded) throw new InvalidDataException("Native engine save restore: " + restored.Message);
                if (before != JsonUtility.ToJson(record.assembly)) throw new InvalidDataException("Source DTO was mutated.");
                VehicleAssemblySaveData captured = assembly.CaptureSaveData();
                if (!assembly.ValidateSaveDataForRestore(captured).Succeeded)
                    throw new InvalidDataException("Migrated native engine graph cannot round-trip.");
                Debug.Log("SATSUMA_NATIVE_ENGINE_SAVE_READONLY_OK oldFasteners=" + record.assembly.fasteners.Length +
                    " currentFasteners=" + captured.fasteners.Length + " parts=" + captured.parts.Length +
                    " sourceDtoUnchanged=true nativeFileWritten=false");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        [Serializable] private sealed class NativeProjection { public DomainProjection[] Domains = Array.Empty<DomainProjection>(); }
        [Serializable] private sealed class DomainProjection
        { public string DomainId = string.Empty; public string PayloadJson = string.Empty; }
    }
}
