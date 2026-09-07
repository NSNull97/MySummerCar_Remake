using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Audio;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Explicit project-owned bindings; no donor-name lookup at runtime.</summary>
    public static class Phase1SatsumaEngineFeedbackAuthoring
    {
        public const string EngineEmitterId = "audio.emitter.vehicle.satsuma.engine";
        public const string KeyEmitterId = "audio.emitter.vehicle.satsuma.ignition";
        public const string ExhaustEmitterId = "audio.emitter.vehicle.satsuma.exhaust";
        public const string IntakeEmitterId = "audio.emitter.vehicle.satsuma.intake";
        public const string RadiatorEmitterId = "audio.emitter.vehicle.satsuma.radiator";

        // GAME FromEngine65270, FromHeaders58024, FromPipe43644, FromMuffler43806.
        // Their Exhaust54098 -> Satsuma systems71973 ancestors have identity TRS
        // under SATSUMA64200, so these are already chassis-local meters.
        public static Vector3[] ReviewedExhaustOutletPositions => new[]
        {
            new Vector3(.028999226f, .12f, 1.1820002f),
            new Vector3(.106008425f, -.014121518f, 1.1816006f),
            new Vector3(-.386f, -.231f, -1.342f),
            new Vector3(-.39942816f, -.24288762f, -1.6977237f),
        };

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Engine Feedback Only")]
        public static void RefreshEngineFeedbackBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before authoring engine feedback.");
            Phase1SatsumaEngineAudioImporter.Build();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = ApplyToInstance(contents.GetComponent<VehicleAssemblyController>());
                if (changed > 0)
                {
                    Directory.CreateDirectory("Logs");
                    File.Copy(path, "Logs/codex-engine-feedback-before-" +
                        DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".prefab", false);
                    if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                        throw new InvalidDataException("Could not save engine feedback bindings.");
                }
                Debug.Log("SATSUMA_ENGINE_FEEDBACK_REFRESH_OK changed=" + changed + " fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        public static int ApplyToInstance(VehicleAssemblyController assembly)
        {
            if (Application.isPlaying || assembly == null)
                throw new InvalidDataException("Engine feedback requires a non-playing authored assembly.");
            var host = assembly.GetComponent<VehicleSimulationHost>();
            var ignition = assembly.GetComponent<SatsumaIgnitionController>();
            PartInstance block = assembly.Parts.Single(part => part?.Definition != null &&
                part.Definition.DefinitionId == Phase1SatsumaEngineDockingAuthoring.BlockPartId);
            if (host == null || ignition == null || ignition.KeyPivot == null)
                throw new InvalidDataException("Author simulation and ignition before engine feedback.");
            int changed = 0;
            AudioEmitterAuthoring engine = EnsureEmitter(assembly, EngineEmitterId, block.transform, ref changed);
            AudioEmitterAuthoring key = EnsureEmitter(assembly, KeyEmitterId, ignition.KeyPivot, ref changed);
            AudioEmitterAuthoring intake = EnsureEmitter(assembly, IntakeEmitterId,
                RequirePart(assembly, "vehicle.satsuma.part.carburetor").transform, ref changed);
            AudioEmitterAuthoring radiator = EnsureEmitter(assembly, RadiatorEmitterId,
                RequirePart(assembly, "vehicle.satsuma.part.radiator").transform, ref changed);
            SatsumaEngineExhaustBinding exhaust = EnsureExhaust(assembly, ref changed);
            var presenter = assembly.GetComponent<SatsumaEngineFeedbackPresenter>();
            if (presenter == null) { presenter = assembly.gameObject.AddComponent<SatsumaEngineFeedbackPresenter>(); changed++; }
            if (presenter.ExhaustBinding != exhaust)
            { presenter.ConfigureExhaust(exhaust); EditorUtility.SetDirty(presenter); changed++; }
            if (presenter.IntakeEmitter != intake || presenter.RadiatorEmitter != radiator)
            { presenter.ConfigureSymptomEmitters(intake, radiator); EditorUtility.SetDirty(presenter); changed++; }
            if (presenter.Simulation != host || presenter.Ignition != ignition || presenter.EnginePart != block ||
                presenter.EngineEmitter != engine || presenter.KeyEmitter != key)
            { presenter.ConfigureReferences(host, ignition, block, engine, key); EditorUtility.SetDirty(presenter); changed++; }

            // Reuse the authored engine ownership graph, not broad Category or
            // hierarchy membership (which would include chassis accessories).
            var scope = new HashSet<string>(StringComparer.Ordinal) { block.Definition.DefinitionId };
            bool expanded;
            do
            {
                expanded = false;
                foreach (MountPointAuthoring mount in assembly.MountPoints)
                    if (mount?.Definition != null && scope.Contains(mount.Definition.OwnerPartDefinitionId))
                        foreach (string accepted in mount.Definition.AcceptedPartDefinitionIds) expanded |= scope.Add(accepted);
            } while (expanded);
            var leaves = new List<Transform>();
            var owners = new List<PartInstance>();
            foreach (PartInstance part in assembly.Parts)
            {
                if (part?.Definition == null || !scope.Contains(part.Definition.DefinitionId)) continue;
                foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.GetComponentInParent<PartInstance>(true) != part ||
                        !SatsumaEngineVisualVibration.IsSafeVisualLeaf(renderer.transform)) continue;
                    leaves.Add(renderer.transform); owners.Add(part);
                }
            }
            if (leaves.Count == 0 || !owners.Contains(block))
                throw new InvalidDataException("Engine vibration has no safe block geometry binding.");
            var vibration = assembly.GetComponent<SatsumaEngineVisualVibration>();
            if (vibration == null) { vibration = assembly.gameObject.AddComponent<SatsumaEngineVisualVibration>(); changed++; }
            if (vibration.Simulation != host || vibration.Block != block ||
                !vibration.VisualLeaves.SequenceEqual(leaves) || !vibration.Owners.SequenceEqual(owners))
            { vibration.Configure(host, block, leaves.ToArray(), owners.ToArray()); EditorUtility.SetDirty(vibration); changed++; }
            var transmitted = new[] { RequirePart(assembly, "vehicle.satsuma.part.exhaust-pipe"),
                RequirePart(assembly, "vehicle.satsuma.part.exhaust-muffler") };
            var gains = new[] { .32f, .18f };
            if (vibration.Assembly != assembly || !vibration.TransmittedOwners.SequenceEqual(transmitted) ||
                !vibration.TransmissionGains.SequenceEqual(gains))
            { vibration.ConfigureTransmission(assembly, transmitted, gains); EditorUtility.SetDirty(vibration); changed++; }
            return changed;
        }

        private static SatsumaEngineExhaustBinding EnsureExhaust(VehicleAssemblyController assembly, ref int changed)
        {
            PartInstance headers = RequirePart(assembly, "vehicle.satsuma.part.headers");
            PartInstance pipe = RequirePart(assembly, "vehicle.satsuma.part.exhaust-pipe");
            PartInstance muffler = RequirePart(assembly, "vehicle.satsuma.part.exhaust-muffler");
            // The dedicated audio-only transform follows the chosen outlet;
            // never reposition a physical part or the existing engine emitter.
            AudioEmitterAuthoring[] emitters = assembly.GetComponentsInChildren<AudioEmitterAuthoring>(true)
                .Where(value => value.StableId == ExhaustEmitterId).ToArray();
            if (emitters.Length > 1) throw new InvalidDataException("Duplicate Satsuma exhaust audio identity.");
            AudioEmitterAuthoring emitter = emitters.SingleOrDefault();
            if (emitter == null)
            {
                var go = new GameObject(ExhaustEmitterId);
                go.transform.SetParent(assembly.transform, false);
                emitter = go.AddComponent<AudioEmitterAuthoring>();
                emitter.Configure(ExhaustEmitterId, null, go.transform); changed++;
            }
            else if (emitter.AudioTransform != emitter.transform || emitter.transform.parent != assembly.transform)
                throw new InvalidDataException("Exhaust emitter must own its audio-only chassis-local transform.");
            var binding = assembly.GetComponent<SatsumaEngineExhaustBinding>();
            if (binding == null)
            {
                binding = assembly.gameObject.AddComponent<SatsumaEngineExhaustBinding>();
                binding.Configure(headers, pipe, muffler, emitter, ReviewedExhaustOutletPositions); changed++;
            }
            else if (binding.Headers != headers || binding.Pipe != pipe || binding.Muffler != muffler ||
                binding.Emitter != emitter || !binding.ChassisLocalOutletPositions.SequenceEqual(ReviewedExhaustOutletPositions))
                throw new InvalidDataException("Existing stock exhaust binding drifted; refusing silent retargeting.");
            return binding;
        }

        private static PartInstance RequirePart(VehicleAssemblyController assembly, string id) =>
            assembly.Parts.Single(part => part?.Definition != null && part.Definition.DefinitionId == id);

        private static AudioEmitterAuthoring EnsureEmitter(VehicleAssemblyController assembly,
            string id, Transform source, ref int changed)
        {
            AudioEmitterAuthoring[] existing = assembly.GetComponentsInChildren<AudioEmitterAuthoring>(true)
                .Where(emitter => emitter.StableId == id).ToArray();
            if (existing.Length > 1) throw new InvalidDataException("Duplicate engine audio emitter identity: " + id);
            AudioEmitterAuthoring result = existing.SingleOrDefault();
            if (result == null)
            {
                var go = new GameObject(id);
                go.transform.SetParent(assembly.transform, false);
                result = go.AddComponent<AudioEmitterAuthoring>();
                result.Configure(id, null, source); changed++;
            }
            else if (result.AudioTransform != source || new SerializedObject(result)
                .FindProperty("explicitBackendComponent").objectReferenceValue != null)
            { result.Configure(id, null, source); EditorUtility.SetDirty(result); changed++; }
            return result;
        }
    }
}
