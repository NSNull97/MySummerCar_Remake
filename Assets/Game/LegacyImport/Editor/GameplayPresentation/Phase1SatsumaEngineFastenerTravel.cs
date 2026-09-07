using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MSC.Core.Identity;
using MSC.LegacyImport.Editor.Configuration;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>
    /// Restores the missing parent-Z scale in the existing engine screw travel
    /// formula. This does not change rest poses, rotation phase, IDs or state.
    /// </summary>
    internal static class Phase1SatsumaEngineFastenerTravel
    {
        internal const string FrozenSceneHash =
            "c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4";
        internal const string TravelProperty = "fastenerPresentationStageTravelScale";
        internal const float LocalStageTravelMeters = 0.0025f;
        private const float Tolerance = 0.00001f;
        private static readonly IReadOnlyList<Binding> Bindings = CreateBindings();

        internal static IReadOnlyList<Binding> ReviewedBindings => Bindings;

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Engine Fastener Travel")]
        public static void RefreshEngineFastenerTravelBatch()
        {
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            ValidateFrozenSource();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = ApplyReviewedTravel(root);
                if (changed > 0)
                {
                    Directory.CreateDirectory("Logs");
                    File.Copy(path, "Logs/codex-engine-travel-before-" +
                        DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".prefab", false);
                    if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                        throw new InvalidDataException("Engine travel refresh could not save the prefab.");
                }
                Debug.Log("PHASE1_SATSUMA_ENGINE_FASTENER_TRAVEL_REFRESH_OK reviewedTargets=115 " +
                    "changedTravelScales=" + changed + " posesUnchanged=true definitionsUnchanged=true fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        internal static int ApplyReviewedTravel(GameObject root, bool dryRun = false)
        {
            if (root == null) throw new InvalidDataException("Engine travel requires a vehicle root.");
            StableEntityIdAuthoring[] ids = root.GetComponents<StableEntityIdAuthoring>();
            VehicleAssemblyController[] controllers = root.GetComponents<VehicleAssemblyController>();
            if (ids.Length != 1 || ids[0].SerializedId != Phase1SatsumaBaselineBuilder.StableVehicleId || controllers.Length != 1)
                throw new InvalidDataException("Engine travel refuses a foreign or ambiguous vehicle root.");
            VehicleAssemblyController controller = controllers[0];
            var targets = new Dictionary<string, AssemblyFastenerInteractionTarget>(StringComparer.Ordinal);
            foreach (AssemblyFastenerInteractionTarget target in root.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true))
            {
                if (string.IsNullOrWhiteSpace(target.FastenerDefinitionId) || !targets.TryAdd(target.FastenerDefinitionId, target))
                    throw new InvalidDataException("Engine travel refuses ambiguous target identities.");
            }
            var mounts = new Dictionary<string, MountPointAuthoring>(StringComparer.Ordinal);
            foreach (MountPointAuthoring mount in controller.MountPoints)
            {
                if (mount == null || string.IsNullOrEmpty(mount.MountId) || !mounts.TryAdd(mount.MountId, mount))
                    throw new InvalidDataException("Engine travel refuses ambiguous mount identities.");
            }

            IReadOnlyList<Binding> activeBindings = ResolveActiveBindings(targets, mounts);

            var changes = new List<(AssemblyFastenerInteractionTarget target, float travel)>();
            var presenters = new HashSet<Transform>();
            foreach (Binding binding in activeBindings)
            {
                if (!targets.TryGetValue(binding.FastenerId, out AssemblyFastenerInteractionTarget target) ||
                    !mounts.TryGetValue(binding.MountId, out MountPointAuthoring mount) || mount.Definition == null ||
                    target.Controller != controller || target.MountId != binding.MountId ||
                    target.transform.parent != mount.transform || target.transform.name != binding.FastenerId ||
                    Vector3.Distance(target.transform.localScale, Vector3.one) > Tolerance)
                    throw new InvalidDataException("Engine travel binding/marker drifted: " + binding.FastenerId);
                FastenerDefinition[] definitions = mount.Definition.Fasteners.Where(value =>
                    value != null && value.DefinitionId == binding.FastenerId).ToArray();
                if (definitions.Length != 1 || definitions[0].MaximumStage != 8)
                    throw new InvalidDataException("Engine travel requires the reviewed eight-stage definition: " + binding.FastenerId);

                var serialized = new SerializedObject(target);
                Transform presentation = serialized.FindProperty("fastenerPresentation")?.objectReferenceValue as Transform;
                SerializedProperty travel = serialized.FindProperty(TravelProperty);
                if (presentation == null || presentation.parent != target.transform || !presenters.Add(presentation) ||
                    presentation.childCount != 0 || presentation.GetComponent<MeshFilter>() == null ||
                    Vector3.Distance(presentation.localScale, binding.PresentationScale) > Tolerance ||
                    !IsFinite(target.transform.localPosition) || !IsFinite(target.transform.localRotation) ||
                    !NearZero(presentation.localPosition) || !NearIdentity(presentation.localRotation) ||
                    !NearZero(serialized.FindProperty("fastenerPresentationBaseLocalPosition").vector3Value) ||
                    !NearIdentity(serialized.FindProperty("fastenerPresentationBaseLocalRotation").quaternionValue) ||
                    travel == null || !IsLegacyOrReviewedScale(binding, travel.floatValue))
                    throw new InvalidDataException("Engine travel refuses changed presentation/axis/scale: " + binding.FastenerId);
                if (Mathf.Abs(travel.floatValue - binding.TravelScale) > Tolerance)
                    changes.Add((target, binding.TravelScale));
            }

            // Do not call Configure: it would initialize graph/presentation state.
            // Validate the entire cohort before changing even one serialized field.
            foreach ((AssemblyFastenerInteractionTarget target, float travel) in changes)
            {
                if (dryRun) continue;
                var serialized = new SerializedObject(target);
                serialized.FindProperty(TravelProperty).floatValue = travel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }
            return changes.Count;
        }

        internal static IReadOnlyList<Binding> ResolveActiveBindings(GameObject root)
        {
            return ResolveActiveBindings(root.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                    .ToDictionary(value => value.FastenerDefinitionId, StringComparer.Ordinal),
                root.GetComponent<VehicleAssemblyController>().MountPoints.ToDictionary(value => value.MountId, StringComparer.Ordinal));
        }

        private static IReadOnlyList<Binding> ResolveActiveBindings(
            IReadOnlyDictionary<string, AssemblyFastenerInteractionTarget> targets,
            IReadOnlyDictionary<string, MountPointAuthoring> mounts)
        {
            const string coverMount = "mount.satsuma.cylinder-head.rocker-cover";
            Binding[] old = Bindings.Where(value => value.MountId == coverMount).ToArray();
            int[] retainedIndices = { 2, 4, 8, 9, 10, 12 };
            var canonical = new HashSet<string>(retainedIndices.Select(index =>
                "fastener.satsuma.cylinder-head-rocker-cover.boltpm-" + index), StringComparer.Ordinal);
            var oldIds = new HashSet<string>(old.Select(value => value.FastenerId), StringComparer.Ordinal);
            if (!mounts.TryGetValue(coverMount, out MountPointAuthoring mount) || mount.Definition == null)
                throw new InvalidDataException("Engine travel requires the reviewed rocker-cover mount.");
            var actualDefinitions = new HashSet<string>(mount.Definition.Fasteners.Select(value => value?.DefinitionId), StringComparer.Ordinal);
            var actualTargets = new HashSet<string>(targets.Values.Where(value => value.MountId == coverMount)
                .Select(value => value.FastenerDefinitionId), StringComparer.Ordinal);
            if (actualDefinitions.SetEquals(oldIds) && actualTargets.SetEquals(oldIds) && mount.Definition.Fasteners.Length == 12)
                return Bindings;
            if (actualDefinitions.SetEquals(canonical) && actualTargets.SetEquals(canonical) && mount.Definition.Fasteners.Length == 6 &&
                oldIds.Except(canonical).All(value => !targets.ContainsKey(value)))
                return Bindings.Where(value => value.MountId != coverMount || canonical.Contains(value.FastenerId)).ToArray();
            throw new InvalidDataException("Engine travel accepts only exact pre-retirement12 or canonical6 cover identities.");
        }

        internal static bool IsLegacyOrReviewedScale(string fastenerId, float scale)
        {
            foreach (Binding binding in Bindings)
                if (binding.FastenerId == fastenerId) return IsLegacyOrReviewedScale(binding, scale);
            return false;
        }

        private static bool IsLegacyOrReviewedScale(Binding binding, float scale) =>
            float.IsFinite(scale) && (Mathf.Abs(scale - 1f) <= Tolerance || Mathf.Abs(scale - binding.TravelScale) <= Tolerance);

        internal static void ValidateDonorBinding(DonorUnitySceneModel scene, Binding binding)
        {
            DonorTransformRecord marker = scene.GetTransform(binding.MarkerTransformId);
            if (Vector3.Distance(marker.LocalScale, binding.PresentationScale) > Tolerance)
                throw new InvalidDataException("Donor engine travel marker scale drifted: " + binding.FastenerId);
            DonorMonoBehaviourRecord[] screws = scene.GetMonoBehaviours(marker.GameObjectId)
                .Where(value => Regex.IsMatch(value.SerializedBody, @"(?m)^    name: Screw\r?$")).ToArray();
            if (screws.Length != 1) throw new InvalidDataException("Expected exactly one donor Screw: " + binding.FastenerId);
            ValidateStageContract(screws[0].SerializedBody, binding.FastenerId);
        }

        internal static IReadOnlyDictionary<long, SourceEvidence> ValidateFrozenSource()
        {
            DonorPathConfiguration paths = DonorPathConfiguration.LoadFromFile("Config/DonorPaths.local.json");
            return ReadFrozenSource(Path.Combine(paths.DonorStagingDirectory,
                "raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity"));
        }

        // A bounded streaming reader avoids retaining the complete 400+ MB FSM
        // scene just to check 115 marker records and their staged actions.
        internal static IReadOnlyDictionary<long, SourceEvidence> ReadFrozenSource(string path)
        {
            using (SHA256 hash = SHA256.Create())
            using (FileStream input = File.OpenRead(path))
            {
                string actual = BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
                if (actual != FrozenSceneHash) throw new InvalidDataException("Engine travel requires the frozen donor scene.");
            }
            var bindings = Bindings.ToDictionary(value => value.MarkerTransformId);
            var source = new Dictionary<long, SourceEvidence>();
            var markerByGo = new Dictionary<long, long>();
            using (var reader = new StreamReader(path))
            {
                int type = 0;
                long id = 0;
                StringBuilder block = null;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("--- !u!", StringComparison.Ordinal))
                    {
                        InspectBlock(type, id, block, bindings, source, markerByGo);
                        Match header = Regex.Match(line, @"!u!(\d+) &(\d+)");
                        type = int.Parse(header.Groups[1].Value, CultureInfo.InvariantCulture);
                        id = long.Parse(header.Groups[2].Value, CultureInfo.InvariantCulture);
                        block = type == 114 || type == 4 && bindings.ContainsKey(id) ? new StringBuilder() : null;
                    }
                    else block?.AppendLine(line);
                }
                InspectBlock(type, id, block, bindings, source, markerByGo);
            }
            if (source.Count != Bindings.Count || source.Values.Any(value => value.ScrewComponentId == 0))
                throw new InvalidDataException("The frozen engine travel evidence is incomplete.");
            return source;
        }

        private static void InspectBlock(int type, long id, StringBuilder block,
            IReadOnlyDictionary<long, Binding> bindings, Dictionary<long, SourceEvidence> source,
            Dictionary<long, long> markerByGo)
        {
            if (block == null) return;
            string body = block.ToString();
            Match owner = Regex.Match(body, @"m_GameObject: \{fileID: (\d+)\}");
            if (!owner.Success) return;
            long go = long.Parse(owner.Groups[1].Value, CultureInfo.InvariantCulture);
            if (type == 4)
            {
                Vector3 scale = ParseVector(body, "m_LocalScale");
                Match q = Regex.Match(body, @"m_LocalRotation: \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}");
                var rotation = new Quaternion(Float(q.Groups[1].Value), Float(q.Groups[2].Value), Float(q.Groups[3].Value), Float(q.Groups[4].Value));
                if (Vector3.Distance(scale, bindings[id].PresentationScale) > Tolerance || !IsFinite(rotation))
                    throw new InvalidDataException("Frozen marker scale/rotation differs: " + id);
                source.Add(id, new SourceEvidence(scale, rotation));
                markerByGo.Add(go, id);
            }
            else if (type == 114 && markerByGo.TryGetValue(go, out long marker) && Regex.IsMatch(body, @"(?m)^    name: Screw\r?$"))
            {
                if (source[marker].ScrewComponentId != 0) throw new InvalidDataException("Duplicate donor Screw: " + marker);
                ValidateStageContract(body, bindings[marker].FastenerId);
                source[marker].ScrewComponentId = id;
            }
        }

        internal static void ValidateStageContract(string body, string label)
        {
            if (!Regex.IsMatch(body, @"(?m)^    startState: Setup 2\r?$"))
                throw new InvalidDataException("Non-mounting or changed donor Screw initialization: " + label);
            Match[] states = Regex.Matches(body, @"(?ms)^    - name: ([^\r\n]+).*?(?=^    - name:|^    events:)")
                .Cast<Match>().Where(value => Regex.IsMatch(value.Groups[1].Value.Trim(), @"^[0-8] 2$")).ToArray();
            if (states.Length != 9 || states.Select(value => value.Groups[1].Value.Trim()).Distinct().Count() != 9)
                throw new InvalidDataException("Expected all nine donor travel states: " + label);
            foreach (Match state in states)
            {
                string s = state.Value;
                int stage = state.Groups[1].Value[0] - '0';
                string actions = Regex.Match(s, @"(?ms)^        actionNames:\r?\n(.*?)^        customNames:").Groups[1].Value;
                string[] actualActions = Regex.Matches(actions, @"(?m)^        - (.*)").Cast<Match>().Select(value => value.Groups[1].Value.Trim()).ToArray();
                bool drain = label == "fastener.satsuma.engine-block-oilpan.boltpm-3";
                bool rotationOnly = label == "fastener.satsuma.engine-block-radiator-hose2.boltpm-1";
                bool alternatorClamp = label == "fastener.satsuma.engine-block-alternator.boltpm-3";
                string[] expectedActions = drain
                    ? new[] { "HutongGames.PlayMaker.Actions.SetPosition", "HutongGames.PlayMaker.Actions.SetRotation", "HutongGames.PlayMaker.Actions.ActivateGameObject" }
                    : alternatorClamp
                        ? new[] { "HutongGames.PlayMaker.Actions.EnableFSM", "HutongGames.PlayMaker.Actions.SetPosition", "HutongGames.PlayMaker.Actions.SetRotation" }
                        : new[] { "HutongGames.PlayMaker.Actions.SetPosition", "HutongGames.PlayMaker.Actions.SetRotation" };
                string offsets = Scalar(s, "paramDataPos");
                string expectedOffsets = "00000000000000000d00000012000000170000001c000000200000002100000001000000220000003300000040000000450000004a0000004f0000005300000054000000" +
                    (drain ? "020000005500000057000000590000005a000000" : string.Empty);
                if (alternatorClamp) expectedOffsets = "00000000000000000000000002000000010000000400000011000000160000001b00000020000000240000002500000002000000260000003700000044000000490000004e000000530000005700000058000000";
                if (!actualActions.SequenceEqual(expectedActions) || Scalar(s, "actionEnabled") != (drain || alternatorClamp ? "010101" : rotationOnly ? "0001" : "0101") || offsets != expectedOffsets)
                    throw new InvalidDataException("Donor staged action layout changed: " + label);
                // Exact layout includes the None flags for X/Y, Self spaces,
                // non-repeating actions, ThisBolt position owner slot0 and
                // marker rotation owner slot1. Only the two stage floats vary.
                byte[] expected = Hex("00000000000000000000000001000000000100000000010000000000010000000000000000000000000000000000000000000100000000000000000000000001000000000100000000010000000000010000000000");
                int translationOffset = alternatorClamp ? 27 : 23;
                int rotationOffset = alternatorClamp ? 78 : 74;
                if (alternatorClamp)
                {
                    byte[] withPrefix = new byte[expected.Length + 4];
                    Buffer.BlockCopy(expected, 0, withPrefix, 4, expected.Length);
                    withPrefix[0] = stage < 8 ? (byte)1 : (byte)0;
                    expected = withPrefix;
                }
                if (drain)
                {
                    // Explicit original oil-pour activation tail, not a third
                    // motion owner. Its gameplay remains outside this packet.
                    Array.Resize(ref expected, 91);
                    expected[85] = stage < 6 ? (byte)1 : (byte)0;
                }
                Buffer.BlockCopy(BitConverter.GetBytes(-LocalStageTravelMeters * stage), 0, expected, translationOffset, 4);
                if (stage == 0) Buffer.BlockCopy(BitConverter.GetBytes(0f), 0, expected, translationOffset, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(45f * stage), 0, expected, rotationOffset, 4);
                byte[] actual = Hex(Scalar(s, "byteData"));
                // Some stages serialize decimal literals with the adjacent
                // float32 representation; accept only that numeric tolerance.
                bool same = actual.Length == expected.Length;
                for (int index = 0; same && index < actual.Length; index++)
                    if ((index < translationOffset || index >= translationOffset + 4) &&
                        (index < rotationOffset || index >= rotationOffset + 4) && actual[index] != expected[index]) same = false;
                if (!same || !float.IsFinite(BitConverter.ToSingle(actual, translationOffset)) || !float.IsFinite(BitConverter.ToSingle(actual, rotationOffset)) ||
                    Mathf.Abs(BitConverter.ToSingle(actual, translationOffset) + LocalStageTravelMeters * stage) > 0.00000001f ||
                    Mathf.Abs(BitConverter.ToSingle(actual, rotationOffset) - 45f * stage) > Tolerance ||
                    !Regex.IsMatch(s, @"(?s)- ownerOption: 1\s*gameObject:\s*useVariable: 1\s*name: ThisBolt\b.*?- ownerOption: 0\s*gameObject:\s*useVariable: 0\s*name:\s*tooltip:"))
                    throw new InvalidDataException("Donor stage displacement/space/owner changed: " + label + "/" + stage);
            }
        }

        private static IReadOnlyList<Binding> CreateBindings()
        {
            var values = Phase1SatsumaEngineFastenerPresentation.ReviewedBindings.Select(value =>
                new Binding(value.MountId, value.FastenerId, value.DonorMarkerTransformId, value.ExpectedPresentationScale)).ToList();
            values.AddRange(Phase1SatsumaEngineAdditionalFastenerPresentation.ReviewedBindings
                .Where(value => value.MarkerTransformId != 52523) // Mixture adjustment, not a staged mounting screw.
                .Select(value => new Binding(value.MountId, value.FastenerId, value.MarkerTransformId, value.PresentationScale)));
            if (values.Count != 115 || values.Select(value => value.FastenerId).Distinct().Count() != 115 ||
                values.Select(value => value.MarkerTransformId).Distinct().Count() != 115)
                throw new InvalidDataException("The reviewed engine travel cohort changed.");
            return values.AsReadOnly();
        }

        private static string Scalar(string body, string key) => Regex.Match(body, @"(?m)^\s*" + key + @": ([^\r\n]+)").Groups[1].Value;
        private static byte[] Hex(string text) => Enumerable.Range(0, text.Length / 2).Select(index => Convert.ToByte(text.Substring(index * 2, 2), 16)).ToArray();
        private static float Float(string text) => float.Parse(text, CultureInfo.InvariantCulture);
        private static Vector3 ParseVector(string body, string key)
        {
            Match value = Regex.Match(body, key + @": \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}");
            return new Vector3(Float(value.Groups[1].Value), Float(value.Groups[2].Value), Float(value.Groups[3].Value));
        }
        private static bool IsFinite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        private static bool IsFinite(Quaternion q) => float.IsFinite(q.x) && float.IsFinite(q.y) && float.IsFinite(q.z) && float.IsFinite(q.w);
        private static bool NearZero(Vector3 v) => IsFinite(v) && v.sqrMagnitude <= Tolerance * Tolerance;
        private static bool NearIdentity(Quaternion q) => IsFinite(q) && Quaternion.Angle(q, Quaternion.identity) <= 0.001f;

        internal readonly struct Binding
        {
            public Binding(string mountId, string fastenerId, long marker, Vector3 scale)
            { MountId = mountId; FastenerId = fastenerId; MarkerTransformId = marker; PresentationScale = scale; }
            public string MountId { get; }
            public string FastenerId { get; }
            public long MarkerTransformId { get; }
            public Vector3 PresentationScale { get; }
            // The hose clamp disables SetPosition in all nine donor states.
            public float TravelScale => MarkerTransformId == 55496 ? 0f : PresentationScale.z;
        }

        internal sealed class SourceEvidence
        {
            public SourceEvidence(Vector3 scale, Quaternion rotation) { MarkerScale = scale; MarkerRotation = rotation; }
            public Vector3 MarkerScale { get; }
            public Quaternion MarkerRotation { get; }
            public long ScrewComponentId { get; set; }
        }
    }
}
