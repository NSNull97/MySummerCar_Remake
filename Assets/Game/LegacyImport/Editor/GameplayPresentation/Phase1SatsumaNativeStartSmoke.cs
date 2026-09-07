using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Core.Identity;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Diagnostics;
using MSC.Vehicle.Simulation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Read-only native snapshot, then explicitly synthetic in-memory start checks. Never saves assets or native files.</summary>
    public static class Phase1SatsumaNativeStartSmoke
    {
        public static void ValidateNativeStartSmokeBatch()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-engineSavePath");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Pass -engineSavePath with the native current.save.json path.");
            Report report = RunFile(args[index + 1]);
            Debug.Log("SATSUMA_NATIVE_START_SMOKE " + JsonUtility.ToJson(report));
            if (!report.reachedRunning || !report.observedCranking || !report.fuelPumpLossStalled || !report.emptyEngineCannotRun)
                throw new InvalidDataException("Native start smoke did not pass all scenarios. Missing prerequisites are reported; no stock parts or wires were fabricated.");
        }

        public static Report RunFile(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run this isolated Editor smoke outside Play.");
            byte[] bytes = File.ReadAllBytes(path); // One immutable source snapshot.
            string hash = Hash(bytes);
            string date = File.GetLastWriteTimeUtc(path).ToString("O");
            Debug.Log("SATSUMA_NATIVE_START_SMOKE_SOURCE sha256=" + hash + " lastWriteUtc=" + date);
            try
            {
                Report report = RunSnapshot(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
                report.sourceSha256 = hash; report.sourceLastWriteUtc = date;
                return report;
            }
            finally
            {
                if (Hash(File.ReadAllBytes(path)) != hash)
                    throw new InvalidDataException("The native source changed during the read-only smoke (possibly an external save). No native writer is used here.");
            }
        }

        public static VehicleSaveRecordDto ReadVehicleRecord(string json)
        {
            NativeProjection document = JsonUtility.FromJson<NativeProjection>(json);
            DomainProjection[] domains = document?.Domains?.Where(domain => domain?.DomainId == "vehicle.satsuma").ToArray();
            if (domains == null || domains.Length != 1) throw new InvalidDataException("Expected exactly one vehicle.satsuma domain.");
            VehicleDomainSaveDto vehicles = JsonUtility.FromJson<VehicleDomainSaveDto>(domains[0].PayloadJson);
            VehicleSaveRecordDto[] matches = vehicles?.vehicles?.Where(record => record?.stableVehicleId == Phase1SatsumaBaselineBuilder.StableVehicleId).ToArray();
            if (matches == null || matches.Length != 1 || matches[0].assembly == null || matches[0].simulation == null)
                throw new InvalidDataException("Expected one complete canonical Satsuma record.");
            return matches[0];
        }

        public static Report RunSnapshot(string json)
        {
            VehicleSaveRecordDto source = ReadVehicleRecord(json);
            string originalDto = JsonUtility.ToJson(source);
            if ((source.assembly.dynamicParts?.Length ?? 0) != 0)
                throw new InvalidDataException("This bounded smoke requires a pre-bridge snapshot with no dynamic wrappers; restore real Items conditions before extending it.");
            GameObject root = PrefabUtility.LoadPrefabContents(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            var created = new List<GameObject>();
            try
            {
                var assembly = root.GetComponent<VehicleAssemblyController>();
                Phase1SatsumaStartableCarNightBatch.ValidateTopology(assembly, true);
                Require(assembly.ValidateSaveDataForRestore(source.assembly), "Original assembly validation");
                Require(assembly.RestoreSaveData(source.assembly), "Original assembly restore");
                VehicleAssemblySaveData migrated = assembly.CaptureSaveData();
                Require(assembly.ValidateSaveDataForRestore(migrated), "Migrated graph round-trip validation");
                var report = new Report { originalRestorePassed = true, sourceDtoUnchanged = true,
                    oldFasteners = source.assembly.fasteners.Length, migratedFasteners = migrated.fasteners.Length };
                VehicleAssemblySaveData tightened = CreateTightenedCopy(migrated, assembly.MountPoints);
                Require(assembly.RestoreSaveData(tightened), "Synthetic tightened graph restore");

                var host = root.GetComponent<VehicleSimulationHost>();
                var prerequisites = root.GetComponent<AssemblyVehiclePrerequisiteAdapter>();
                var electrical = root.GetComponent<SatsumaElectricalSystem>();
                if (host == null || prerequisites == null || electrical == null || !prerequisites.UsesSatsumaAssemblyRequirements)
                    throw new InvalidDataException("The scoped night start bindings have not been authored.");
                var backend = root.AddComponent<SatsumaNativeSmokeWheelBackend>();
                if (backend == null || host.Config == null)
                    throw new InvalidDataException("The isolated smoke backend or authored simulation config is unavailable.");
                backend.Configure(host.Config.WheelCount);
                host.Configure(host.Config, backend, prerequisites, null);
                if (!host.TryInitialize(out string failure)) throw new InvalidDataException(failure);
                VehicleSimulationStateDto parked = Clone(source.simulation);
                parked.engineStatus = VehicleEngineStatus.Off; parked.engineRpm = 0; parked.selectedGear = 0;
                parked.filteredThrottle01 = 0; parked.engineTorqueNewtonMeters = 0; parked.stallTimerSeconds = 0;
                if (!host.TryRestoreSimulationState(parked, out failure)) throw new InvalidDataException("Saved simulation: " + failure);
                SatsumaElectricalSaveDto wires = source.electrical == null ? new SatsumaElectricalSaveDto() : Clone(source.electrical);
                wires.starterCableStage = SatsumaElectricalSystem.FastenerMaximumStage;
                if (!electrical.TryRestore(wires, out failure)) throw new InvalidDataException("Saved electrical state: " + failure);
                report.missingInstalledStockParts = MissingStock(assembly.Graph);
                report.missingStarterWires = new[] { SatsumaElectricalConnection.BatteryHarness, SatsumaElectricalConnection.GroundBattery,
                    SatsumaElectricalConnection.Ignition, SatsumaElectricalConnection.Starter }
                    .Where(connection => !electrical.IsConnectionInstalled(connection)).Select(connection => connection.ToString()).ToArray();
                Debug.Log("SATSUMA_NATIVE_START_SMOKE_PRECONDITIONS " + JsonUtility.ToJson(report));

                for (int plug = 1; plug <= 4; plug++) AddSyntheticPlug(assembly, plug, created);
                report.syntheticPlugs = created.Count;
                report.firingCylinderMask = SatsumaEngineAssemblyReadiness.EvaluateFiringCylinderMask(assembly.Graph);
                // The real F10 seam: do not fill tanks, change configuration or bypass missing parts/wires.
                prerequisites.SetFluidReadinessTestOverride(true);
                var crank = new VehicleInputState(0f, 1f, 0f, 0f, true, true, true, 0);
                VehicleSimulationPrerequisites readiness = default;
                prerequisites.Evaluate(crank, ref readiness);
                report.initialTestFailures = readiness.FailureFlags.ToString();
                for (int tick = 0; tick < 1000 && !report.reachedRunning; tick++)
                {
                    host.Root.Tick(.02f, crank);
                    report.observedCranking |= host.State.EngineStatus == VehicleEngineStatus.Cranking;
                    report.reachedRunning |= host.State.EngineStatus == VehicleEngineStatus.Running;
                }
                report.startFinalFailures = host.Root.Prerequisites.FailureFlags.ToString();
                if (report.reachedRunning)
                {
                    PartInstance pump = assembly.AllRuntimeParts.Single(part => part.Definition.DefinitionId == "vehicle.satsuma.part.fuel-pump");
                    Require(assembly.TryBreakInstalledPart(pump), "Test-only fuel-pump loss");
                    host.Root.Tick(.02f, VehicleInputState.Neutral(true));
                    bool fuelDeliveryBlockedImmediately = !host.Root.Prerequisites.CanRun;
                    for (int tick = 0; tick < 1000 && host.State.EngineStatus != VehicleEngineStatus.Stalled; tick++)
                        host.Root.Tick(.02f, VehicleInputState.Neutral(true));
                    report.fuelPumpLossStalled = host.State.EngineStatus == VehicleEngineStatus.Stalled &&
                        fuelDeliveryBlockedImmediately && !host.Root.Prerequisites.CanRun;
                }
                var descendants = EngineDescendantDefinitions(assembly);
                foreach (PartInstance part in assembly.AllRuntimeParts.ToArray())
                    if (descendants.Contains(part.Definition.DefinitionId) && part.IsInstalled)
                        Require(assembly.TryBreakInstalledPart(part), "Test-only empty-engine teardown: " + part.Definition.DefinitionId);
                if (assembly.AllRuntimeParts.Any(part => descendants.Contains(part.Definition.DefinitionId) && part.IsInstalled))
                    throw new InvalidDataException("The empty-engine test still contains installed engine descendants.");
                report.emptyEngineCannotRun = true;
                for (int tick = 0; tick < 100; tick++)
                {
                    host.Root.Tick(.02f, crank);
                    report.emptyEngineCannotRun &= host.State.EngineStatus != VehicleEngineStatus.Running && !host.Root.Prerequisites.CanRun;
                }
                report.emptyEngineFailures = host.Root.Prerequisites.FailureFlags.ToString();
                return report;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                foreach (GameObject value in created) if (value != null) Object.DestroyImmediate(value);
                if (originalDto != JsonUtility.ToJson(source)) throw new InvalidDataException("Read-only source DTO was mutated.");
            }
        }

        public static VehicleAssemblySaveData CreateTightenedCopy(VehicleAssemblySaveData source, IReadOnlyList<MountPointAuthoring> mounts)
        {
            VehicleAssemblySaveData copy = Clone(source);
            var byId = mounts.ToDictionary(mount => mount.MountId, StringComparer.Ordinal);
            var occupied = new HashSet<string>(copy.mounts.Where(mount => !string.IsNullOrEmpty(mount.installedPartStableEntityId)).Select(mount => mount.mountId), StringComparer.Ordinal);
            foreach (FastenerSaveDto fastener in copy.fasteners)
            {
                if (!occupied.Contains(fastener.mountId)) continue;
                FastenerDefinition definition = byId[fastener.mountId].Definition.Fasteners.Single(value => value.DefinitionId == fastener.fastenerDefinitionId);
                fastener.inserted = true; fastener.seated = true; fastener.stage = definition.MaximumStage;
            }
            foreach (FastenerGroupSaveDto group in copy.fastenerGroups)
                if (occupied.Contains(group.mountId) && byId[group.mountId].Definition.FastenerGroup.HasFasteners)
                    group.isBolted = true;
            return copy;
        }

        private static void AddSyntheticPlug(VehicleAssemblyController assembly, int index, List<GameObject> created)
        {
            if (!assembly.TryGetDynamicPartDefinition(SatsumaConsumableAssemblyRules.SparkPlugPartId, out PartDefinition definition))
                throw new InvalidDataException("Purchased-plug dynamic definition was not registered by the official bridge authoring.");
            MountPointAuthoring mount = assembly.MountPoints.Single(value => value.MountId == SatsumaConsumableAssemblyRules.SparkPlugMountId(index));
            if (assembly.ResolveMount(mount).IsOccupied) throw new InvalidDataException("Synthetic smoke refuses to replace an existing spark plug.");
            var go = new GameObject("TEST ONLY synthetic native-smoke spark plug " + index);
            created.Add(go); go.transform.SetParent(assembly.transform, false);
            Rigidbody body = go.AddComponent<Rigidbody>(); body.useGravity = false;
            var identity = go.AddComponent<StableEntityIdAuthoring>(); identity.InitializeExplicitRuntimeId(StableEntityId.New());
            PartInstance part = go.AddComponent<PartInstance>(); part.Configure(definition, identity, body, null, false, string.Empty);
            go.AddComponent<SatsumaNativeSmokePlugCondition>();
            if (!assembly.TryRegisterDynamicPart(part, "item.spark-plug", out string failure)) throw new InvalidDataException(failure);
            go.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
            Require(assembly.TryInstall(part, mount), "Synthetic plug install " + index);
            ToolDefinition tool = assembly.Tools.Single(value => value.DefinitionId == SatsumaAuxiliaryAssemblyTools.SparkPlugWrenchDefinitionId);
            for (int stage = 0; stage < SatsumaConsumableAssemblyRules.SparkPlugMaximumStage; stage++)
                Require(assembly.TryTurnFastener(mount.MountId, SatsumaConsumableAssemblyRules.SparkPlugFastenerId(index),
                    tool, FastenerRotationDirection.Clockwise), "Synthetic plug tightening " + index);
        }

        private static HashSet<string> EngineDescendantDefinitions(VehicleAssemblyController assembly)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal) { SatsumaEngineAssemblyReadiness.BlockId };
            bool changed;
            do
            {
                changed = false;
                foreach (MountPointAuthoring mount in assembly.MountPoints)
                    if (ids.Contains(mount.Definition.OwnerPartDefinitionId))
                        foreach (string id in mount.Definition.AcceptedPartDefinitionIds) changed |= ids.Add(id);
            } while (changed);
            ids.Remove(SatsumaEngineAssemblyReadiness.BlockId);
            return ids;
        }

        private static string[] MissingStock(AssemblyGraph graph) => new[]
        {
            "engine-block", "starter", "battery", "fuel-tank", "fuel-strainer", "fuel-pump", "carburetor",
            "crankshaft", "camshaft", "timing-chain", "rocker-shaft", "cylinder-head", "flywheel", "electrics", "distributor",
            "piston1", "piston2", "piston3", "piston4",
        }.Select(slug => "vehicle.satsuma.part." + slug).Where(id => !graph.IsPartDefinitionInstalled(id)).ToArray();
        private static T Clone<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        private static void Require(AssemblyOperationResult result, string context)
        { if (!result.Succeeded) throw new InvalidDataException(context + ": " + result.Message); }
        private static string Hash(byte[] bytes)
        { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }

        [Serializable] public sealed class Report
        {
            public string sourceSha256, sourceLastWriteUtc, initialTestFailures, startFinalFailures, emptyEngineFailures;
            public bool originalRestorePassed, sourceDtoUnchanged, observedCranking, reachedRunning, fuelPumpLossStalled, emptyEngineCannotRun;
            public int oldFasteners, migratedFasteners, syntheticPlugs, firingCylinderMask;
            public string[] missingInstalledStockParts, missingStarterWires;
        }
        [Serializable] private sealed class NativeProjection { public DomainProjection[] Domains; }
        [Serializable] private sealed class DomainProjection { public string DomainId; public string PayloadJson; }
    }
}
