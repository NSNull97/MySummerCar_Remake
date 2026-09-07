using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static class Phase1SatsumaEngineDockingAuthoring
    {
        public const string MountId = "mount.satsuma.engine-assembly";
        public const string BlockPartId = "vehicle.satsuma.part.engine-block";
        public const string BodyPartId = "vehicle.satsuma.part.body-shell";
        public const string EngineBoltMeshSourceGuid = "aec6c756751308a4d830708366ad5cdb";
        public const float EngineBoltPresentationScale = 1.1f;
        public const float EngineBoltTravelScale = 1.1f;
        public static string[] FastenerIds => new[]
        {
            "fastener.satsuma.engine-assembly.boltpm-1",
            "fastener.satsuma.engine-assembly.boltpm-2",
            "fastener.satsuma.engine-assembly.boltpm-3",
        };

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Engine Docking Only")]
        public static void RefreshEngineDockingBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before authoring physical engine docking.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var assembly = contents.GetComponent<VehicleAssemblyController>();
                int changed = ApplyToInstance(assembly);
                if (changed > 0)
                {
                    Directory.CreateDirectory("Logs");
                    File.Copy(path, "Logs/codex-engine-docking-before-" +
                        DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".prefab", false);
                    if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                        throw new InvalidDataException("Could not save scoped engine docking bindings.");
                    AssetDatabase.SaveAssetIfDirty(RequireMount(assembly).Definition);
                }
                Debug.Log("SATSUMA_ENGINE_DOCKING_REFRESH_OK changed=" + changed + " fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        // Runs after ordinary fasteners are authored. CSV points are relative
        // to donor block root and donor Satsuma root respectively; neither is
        // the visually moving engine mount marker or subframe root.
        public static int ApplyToInstance(VehicleAssemblyController assembly)
        {
            MountPointAuthoring mount = RequireMount(assembly);
            PartInstance block = assembly.Parts.Single(value => value?.Definition != null &&
                value.Definition.DefinitionId == BlockPartId);
            ReadFrozenPoints(out Vector3[] blockPoints, out Vector3[] chassisPoints);
            int changed = 0;
            var state = block.GetComponent<AssemblyEngineDockingState>();
            if (state == null)
            {
                state = block.gameObject.AddComponent<AssemblyEngineDockingState>();
                state.Configure(assembly, block, mount, assembly.transform, blockPoints, chassisPoints, FastenerIds);
                changed++;
            }
            else
            {
                var data = new SerializedObject(state);
                if (state.Block != block || state.Mount != mount ||
                    data.FindProperty("assembly").objectReferenceValue != assembly ||
                    data.FindProperty("chassisFrame").objectReferenceValue != assembly.transform ||
                    !state.FastenerIds.SequenceEqual(FastenerIds) ||
                    !MatchVectors(data.FindProperty("blockPoints"), blockPoints) ||
                    !MatchVectors(data.FindProperty("chassisPoints"), chassisPoints) ||
                    Mathf.Abs(state.DistanceToleranceMeters - 0.1f) > 0.000001f)
                    throw new InvalidDataException("Existing engine docking bindings drifted; refusing silent retargeting.");
            }
            if (mount.GetComponent<AssemblyPhysicalDockingOnly>() == null)
            { mount.gameObject.AddComponent<AssemblyPhysicalDockingOnly>(); changed++; }
            AssemblyFastenerInteractionTarget[] targets = assembly
                .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                .Where(value => value.Controller == assembly && value.MountId == MountId).ToArray();
            if (targets.Length != 3 || !targets.Select(value => value.FastenerDefinitionId)
                    .OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(FastenerIds))
                throw new InvalidDataException("Expected the three existing engine mounting bolt targets.");
            changed += ApplyMountBoltPresentation(targets, mount, assembly.transform);
            foreach (AssemblyFastenerInteractionTarget target in targets)
            {
                if (target.PendingDocking != null && target.PendingDocking != state)
                    throw new InvalidDataException("Engine mounting bolt already belongs to another docking state.");
                if (target.PendingDocking != state)
                { target.ConfigurePendingDocking(state); changed++; }
            }
            FastenerGroupDefinition old = mount.Definition.FastenerGroup;
            if (old.BoltedOnThreshold != 2 || old.BoltedOffThreshold != 0)
            {
                var group = new FastenerGroupDefinition();
                group.Configure(old.FastenerDefinitionIds, old.AggregateMaximumTightness, 2, 0,
                    old.SpeedRetentionPolicy, old.LooseBreakSpeedKph, old.PartialCheckSpeedKph,
                    old.ChanceDivisor, old.BreakAction);
                mount.Definition.ConfigureFastenerGroup(group);
                EditorUtility.SetDirty(mount.Definition);
                changed++;
            }
            return changed;
        }

        // Frozen GAME Transform chain: markers 59373/70667/52875 -> _Motor43037
        // -> subframe47402 -> Chassis38351 -> SATSUMA64200. These poses are
        // SATSUMA-local, NOT relative to the solved engine-block mounting pose.
        public static Pose[] GetReviewedBoltChassisPoses() => new[]
        {
            new Pose(new Vector3(-0.22499907f, -0.21237408f, 1.502543f),
                new Quaternion(-2.6290722e-14f, -0.2588179f, 0.9659262f, -1.0194491e-14f)),
            new Pose(new Vector3(0.22499979f, -0.21237443f, 1.5025431f),
                new Quaternion(-4.216669e-8f, -0.25881746f, 0.9659263f, -1.573694e-7f)),
            new Pose(new Vector3(-4.501711e-7f, -0.23743555f, 1.0483661f),
                new Quaternion(0.991445f, 6.6440134e-7f, -1.0875442e-6f, 0.1305254f)),
        };

        private static int ApplyMountBoltPresentation(AssemblyFastenerInteractionTarget[] targets,
            MountPointAuthoring mount, Transform chassisFrame)
        {
            const float tolerance = 0.00001f;
            string meshRoot = Phase1SatsumaCamshaftTimingAuthoring.GeneratedRoot + "/Meshes/";
            Mesh expected = AssetDatabase.LoadAssetAtPath<Mesh>(meshRoot + EngineBoltMeshSourceGuid + ".asset");
            Mesh legacyNut = AssetDatabase.LoadAssetAtPath<Mesh>(meshRoot +
                Phase1SatsumaEngineFastenerPresentation.DefaultNutMeshSourceGuid + ".asset");
            if (expected == null || expected.name != "bolt" || legacyNut == null)
                throw new InvalidDataException("Reviewed engine mounting bolt meshes are missing.");
            Pose[] chassisPoses = GetReviewedBoltChassisPoses();
            var changes = new List<(AssemblyFastenerInteractionTarget target, MeshFilter filter, Pose localPose)>();
            var presentations = new HashSet<Transform>();
            foreach (AssemblyFastenerInteractionTarget target in targets)
            {
                var serialized = new SerializedObject(target);
                Transform presentation = serialized.FindProperty("fastenerPresentation")?.objectReferenceValue as Transform;
                MeshFilter filter = presentation != null ? presentation.GetComponent<MeshFilter>() : null;
                SerializedProperty travel = serialized.FindProperty("fastenerPresentationStageTravelScale");
                int index = Array.IndexOf(FastenerIds, target.FastenerDefinitionId);
                Pose chassisPose = chassisPoses[index];
                var correctedPose = new Pose(
                    mount.transform.InverseTransformPoint(chassisFrame.TransformPoint(chassisPose.position)),
                    Quaternion.Inverse(mount.transform.rotation) * chassisFrame.rotation * chassisPose.rotation);
                bool correctedFrame = MatchesPose(target.transform, correctedPose);
                // Accept only the measured legacy frame bug or the corrected
                // mount-relative frame. Unknown user/authoring offsets are not
                // guessed away. All three are validated before any pose write.
                if (!correctedFrame && !MatchesPose(target.transform, chassisPose))
                    throw new InvalidDataException("Engine mounting bolt pose differs from both reviewed frames: " +
                        target.FastenerDefinitionId);
                // The importer flattens donor marker scale1.1 onto the visible
                // child. Keep marker unit scale: scaling both would produce1.21.
                if (target.transform.parent != mount.transform ||
                    Vector3.Distance(target.transform.localScale, Vector3.one) > tolerance ||
                    presentation == null || presentation.parent != target.transform ||
                    !presentations.Add(presentation) || presentation.childCount != 0 ||
                    Vector3.Distance(presentation.localScale, Vector3.one * EngineBoltPresentationScale) > tolerance ||
                    presentation.localPosition.sqrMagnitude > tolerance * tolerance ||
                    Quaternion.Angle(presentation.localRotation, Quaternion.identity) > tolerance ||
                    serialized.FindProperty("fastenerPresentationBaseLocalPosition").vector3Value.sqrMagnitude > tolerance * tolerance ||
                    Quaternion.Angle(serialized.FindProperty("fastenerPresentationBaseLocalRotation").quaternionValue,
                        Quaternion.identity) > tolerance || filter == null ||
                    filter.sharedMesh != expected && filter.sharedMesh != legacyNut ||
                    travel == null || !float.IsFinite(travel.floatValue) ||
                    Mathf.Abs(travel.floatValue - 1f) > tolerance &&
                    Mathf.Abs(travel.floatValue - EngineBoltTravelScale) > tolerance)
                    throw new InvalidDataException("Engine mounting bolt frame/mesh/travel drifted: " + target.FastenerDefinitionId);
                if (!correctedFrame || filter.sharedMesh != expected ||
                    Mathf.Abs(travel.floatValue - EngineBoltTravelScale) > tolerance)
                    changes.Add((target, filter, correctedPose));
            }
            // Preflight all three before writing. Do not call target.Configure:
            // it initializes graph state. Only the marker frame is corrected;
            // the visible child's zero rest pose and stage translation stay intact.
            foreach ((AssemblyFastenerInteractionTarget target, MeshFilter filter, Pose localPose) in changes)
            {
                target.transform.SetLocalPositionAndRotation(localPose.position, localPose.rotation);
                filter.sharedMesh = expected;
                var serialized = new SerializedObject(target);
                serialized.FindProperty("fastenerPresentationStageTravelScale").floatValue = EngineBoltTravelScale;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(filter);
                EditorUtility.SetDirty(target.transform);
                EditorUtility.SetDirty(target);
            }
            return changes.Count;
        }

        private static bool MatchesPose(Transform marker, Pose pose) =>
            Vector3.Distance(marker.localPosition, pose.position) <= 0.00001f &&
            Quaternion.Angle(marker.localRotation, pose.rotation) <= 0.05f;

        public static void ReadFrozenPoints(out Vector3[] blockPoints, out Vector3[] chassisPoints)
        {
            string[] lines = File.ReadAllLines(Phase1SatsumaBaselineBuilder.EngineMountTriangulationAuditPath);
            if (lines.Length != 4 || !lines[0].StartsWith("AssemblyComponentId,ChassisTriggerTransformId,", StringComparison.Ordinal))
                throw new InvalidDataException("Frozen engine triangulation audit shape drifted.");
            string[] assemblyIds = { "104917", "111211", "107768" };
            string[] chassisIds = { "38857", "61244", "48877" };
            string[] blockIds = { "51236", "54063", "44595" };
            blockPoints = new Vector3[3];
            chassisPoints = new Vector3[3];
            for (int index = 0; index < 3; index++)
            {
                string[] cells = ReadCsvLine(lines[index + 1]);
                if (cells.Length != 11 || cells[0] != assemblyIds[index] ||
                    cells[1] != chassisIds[index] || cells[3] != blockIds[index] ||
                    cells[10] != "ConfigurationTransferred" ||
                    !float.TryParse(cells[9], NumberStyles.Float, CultureInfo.InvariantCulture, out float residual) ||
                    !float.IsFinite(residual) || residual < 0f || residual > 0.01f)
                    throw new InvalidDataException("Frozen engine triangulation row identity/residual drifted.");
                blockPoints[index] = ParseVector(cells[5]);
                chassisPoints[index] = ParseVector(cells[6]);
            }
        }

        private static MountPointAuthoring RequireMount(VehicleAssemblyController assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            MountPointAuthoring mount = assembly.MountPoints.Single(value => value != null && value.MountId == MountId);
            MountPointDefinition definition = mount.Definition;
            if (definition == null || definition.DefinitionId != MountId ||
                definition.OwnerPartDefinitionId != BodyPartId ||
                !definition.AcceptedPartDefinitionIds.SequenceEqual(new[] { BlockPartId }) ||
                definition.Fasteners.Length != 3 || definition.Fasteners.Any(value => value == null ||
                    value.Size != FastenerSize.Millimeter11 || value.MaximumStage != 8 ||
                    value.ToolRule.ToolType != "Wrench") ||
                !definition.Fasteners.Select(value => value.DefinitionId).SequenceEqual(FastenerIds) ||
                definition.FastenerGroup == null || definition.FastenerGroup.AggregateMaximumTightness != 24 ||
                !definition.FastenerGroup.FastenerDefinitionIds.SequenceEqual(FastenerIds))
                throw new InvalidDataException("Engine docking mount identity/fastener ownership drifted.");
            return mount;
        }

        private static bool MatchVectors(SerializedProperty array, Vector3[] expected)
        {
            if (array == null || !array.isArray || array.arraySize != expected.Length) return false;
            for (int index = 0; index < expected.Length; index++)
                if (Vector3.Distance(array.GetArrayElementAtIndex(index).vector3Value, expected[index]) > 0.000001f)
                    return false;
            return true;
        }

        private static Vector3 ParseVector(string text)
        {
            string[] values = text.Split(';');
            if (values.Length != 3) throw new InvalidDataException("Invalid engine anchor vector.");
            var vector = new Vector3();
            for (int axis = 0; axis < 3; axis++)
            {
                if (!float.TryParse(values[axis], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ||
                    !float.IsFinite(value)) throw new InvalidDataException("Non-finite engine anchor.");
                vector[axis] = value;
            }
            return vector;
        }

        private static string[] ReadCsvLine(string line)
        {
            var values = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            for (int index = 0; index < line.Length; index++)
            {
                char value = line[index];
                if (value == '"')
                {
                    if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                    { cell.Append('"'); index++; }
                    else quoted = !quoted;
                }
                else if (value == ',' && !quoted) { values.Add(cell.ToString()); cell.Clear(); }
                else cell.Append(value);
            }
            if (quoted) throw new InvalidDataException("Unclosed quote in engine triangulation audit.");
            values.Add(cell.ToString());
            return values.ToArray();
        }
    }
}
