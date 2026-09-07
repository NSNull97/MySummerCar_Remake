using System;
using System.IO;
using System.Linq;
using MSC.Vehicle;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static class Phase1SatsumaElectricalTerminalPoseAuthoring
    {
        public static Binding[] GetBindings() => new[]
        {
            new Binding(SatsumaElectricalFastener.BatteryPositiveTerminal, 60044, 53709, 55114,
                new Vector3(-.31310377f, 1.4375995f, -.026502978f),
                new Quaternion(.34747246f, -.61580443f, -.347506f, -.6158632f),
                new Vector3(0f, .0006f, -.0118f),
                new Quaternion(-1f, -4.0111846e-7f, 1.411083e-7f, -4.470348e-7f), .8f),
            new Binding(SatsumaElectricalFastener.BatteryNegativeTerminal, 65291, 63573, 44832,
                new Vector3(-.2988996f, 1.620501f, -.026501168f),
                new Quaternion(-.025019635f, -.7066301f, .02502175f, -.7066979f),
                new Vector3(0f, 0f, .016f),
                new Quaternion(-2.9802315e-8f, -4.4703473e-8f, 1.4901158e-8f, 1f), .8f),
            new Binding(SatsumaElectricalFastener.StarterCable, 67843, 66724, 48986,
                Vector3.zero, Quaternion.identity,
                new Vector3(-.10559f, 1.44987f, -.35653f),
                new Quaternion(-6.2333314e-8f, .7071069f, -5.9824806e-8f, .70710677f), .5f),
        };

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Electrical Terminal Rest Poses Only")]
        public static void RefreshElectricalTerminalRestPosesBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before authoring terminal rest poses.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = ApplyToInstance(root.GetComponent<SatsumaElectricalSystem>());
                if (changed > 0 && PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new InvalidDataException("Could not save terminal rest poses.");
                Debug.Log("SATSUMA_ELECTRICAL_TERMINAL_REST_POSES_REFRESH_OK changed=" + changed + " reviewed=3 fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static int ApplyToInstance(SatsumaElectricalSystem system)
        {
            if (system == null) throw new InvalidDataException("Missing Satsuma electrical system.");
            var targets = system.GetComponentsInChildren<SatsumaElectricalTerminalFastenerInteractionTarget>(true);
            if (targets.Length != 3) throw new InvalidDataException("Expected exactly three electrical terminal targets.");
            Binding[] bindings = GetBindings();
            var reviewed = new SatsumaElectricalTerminalFastenerInteractionTarget[3];
            var systemData = new SerializedObject(system);
            SerializedProperty presentations = systemData.FindProperty("bindings");
            for (int i = 0; i < bindings.Length; i++)
            {
                Binding b = bindings[i];
                var candidates = targets.Where(t =>
                    new SerializedObject(t).FindProperty("fastener").enumValueIndex == (int)b.Fastener).ToArray();
                if (candidates.Length != 1) throw new InvalidDataException("Duplicate/missing electrical fastener " + b.Fastener);
                var target = candidates[0];
                var data = new SerializedObject(target);
                Transform presentation = data.FindProperty("fastenerPresentation").objectReferenceValue as Transform;
                Renderer renderer = data.FindProperty("fastenerRenderer").objectReferenceValue as Renderer;
                GameObject parent = null;
                for (int j = 0; j < presentations.arraySize; j++)
                {
                    SerializedProperty row = presentations.GetArrayElementAtIndex(j);
                    int rule = row.FindPropertyRelative("rule").enumValueIndex;
                    int connection = row.FindPropertyRelative("connection").enumValueIndex;
                    bool matches = b.Fastener switch
                    {
                        SatsumaElectricalFastener.BatteryPositiveTerminal => rule == (int)SatsumaElectricalPresentationRule.BatteryPositiveShoe,
                        SatsumaElectricalFastener.BatteryNegativeTerminal => rule == (int)SatsumaElectricalPresentationRule.BatteryNegativeShoe,
                        _ => rule == (int)SatsumaElectricalPresentationRule.Connection && connection == (int)SatsumaElectricalConnection.Starter,
                    };
                    if (!matches) continue;
                    if (parent != null) throw new InvalidDataException("Duplicate terminal presentation binding.");
                    parent = row.FindPropertyRelative("installedPresentation").objectReferenceValue as GameObject;
                }
                Quaternion livePose = b.Pose.rotation * Quaternion.AngleAxis(
                    -SatsumaElectricalTerminalFastenerInteractionTarget.TurnDegrees * system.GetFastenerStage(b.Fastener),
                    Vector3.forward);
                bool oldIdentityBug = presentation != null && Quaternion.Angle(presentation.localRotation,
                    Quaternion.AngleAxis(-SatsumaElectricalTerminalFastenerInteractionTarget.TurnDegrees *
                        system.GetFastenerStage(b.Fastener), Vector3.forward)) < .05f;
                if (parent == null || presentation != target.transform || presentation.parent != parent.transform ||
                    data.FindProperty("electricalSystem").objectReferenceValue != system || renderer == null ||
                    renderer.transform.parent != presentation ||
                    Vector3.Distance(presentation.localPosition, b.Pose.position) > .0001f ||
                    Vector3.Distance(presentation.localScale, Vector3.one * b.Scale) > .0001f ||
                    (!oldIdentityBug && Quaternion.Angle(presentation.localRotation, livePose) > .05f))
                    throw new InvalidDataException("Electrical terminal differs from reviewed donor frames: " + b.Fastener);
                reviewed[i] = target;
            }
            int changed = 0;
            for (int i = 0; i < bindings.Length; i++)
            {
                var data = new SerializedObject(reviewed[i]);
                if (data.FindProperty("hasAuthoredBaseRotation").boolValue &&
                    Quaternion.Angle(data.FindProperty("baseRotation").quaternionValue, bindings[i].Pose.rotation) < .05f)
                    continue;
                reviewed[i].ConfigureAuthoredBaseRotation(bindings[i].Pose.rotation);
                EditorUtility.SetDirty(reviewed[i]); changed++;
            }
            return changed;
        }

        public readonly struct Binding
        {
            public Binding(SatsumaElectricalFastener fastener, long parentId, long markerId, long rendererId,
                Vector3 intermediatePosition, Quaternion intermediateRotation, Vector3 markerPosition,
                Quaternion markerRotation, float scale)
            { Fastener = fastener; ParentId = parentId; MarkerId = markerId; RendererId = rendererId;
                Pose = new Pose(intermediatePosition + intermediateRotation * markerPosition,
                    (intermediateRotation * markerRotation).normalized); Scale = scale; }
            public SatsumaElectricalFastener Fastener { get; }
            public long ParentId { get; }
            public long MarkerId { get; }
            public long RendererId { get; }
            public Pose Pose { get; }
            public float Scale { get; }
        }
    }
}
