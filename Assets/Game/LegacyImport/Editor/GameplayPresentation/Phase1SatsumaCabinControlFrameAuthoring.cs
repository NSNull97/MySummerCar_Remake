using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Interaction.Query;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static class Phase1SatsumaCabinControlFrameAuthoring
    {
        public const string HandleMeshGuid = "1b88f46a46e9c8e44b9040dd4972d6a6";
        public static readonly Quaternion WiperRestRotation =
            new(-0.1560646f, 1.0121124e-7f, -6.454299e-8f, 0.9877469f);
        public static readonly Vector3 HandlePosition = new(0.5443001f, -0.01560072f, -0.13640001f);
        public static readonly Quaternion HandleRotation = new(0.17081988f, -3.8543052e-7f, -1.3644272e-6f, 0.98530227f);
        public static readonly Vector3 TriggerPosition = new(0.5443f, -0.0493f, -0.1484f);
        public static readonly Quaternion TriggerRotation = new(-0.17081985f, 3.8563905e-7f, 1.3625023e-6f, -0.9853023f);
        public static readonly Vector3 TriggerCenter = new(1.6093254e-6f, -0.000094999734f, 0.001113f);

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Cabin Control Frames")]
        public static void RefreshCabinControlFramesBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before refreshing cabin controls.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = ApplyToInstance(contents.GetComponent<VehicleAssemblyController>());
                if (changed > 0)
                {
                    Directory.CreateDirectory("Logs");
                    File.Copy(path, "Logs/codex-cabin-frames-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".prefab", false);
                    if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                        throw new InvalidDataException("Could not save scoped cabin controls.");
                }
                Debug.Log("SATSUMA_CABIN_FRAME_REFRESH_OK changed=" + changed + " fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        public static int ApplyToInstance(VehicleAssemblyController assembly, string generatedRoot = null)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            var writes = new List<Action>();
            PlanWiper(assembly, writes);
            PlanHood(assembly, writes, generatedRoot ?? Phase1SatsumaCamshaftTimingAuthoring.GeneratedRoot);
            foreach (Action write in writes) write();
            return writes.Count;
        }

        private static void PlanWiper(VehicleAssemblyController assembly, List<Action> writes)
        {
            SatsumaWiperController wiper = assembly.GetComponent<SatsumaWiperController>();
            if (wiper == null || wiper.SwitchKnob == null)
                throw new InvalidDataException("Missing existing wiper knob binding.");
            Transform knob = wiper.SwitchKnob;
            PartInstance meters = knob.GetComponentInParent<PartInstance>(true);
            if (meters?.Definition == null || meters.Definition.DefinitionId != "vehicle.satsuma.part.dashboard-meters" ||
                knob.GetComponent<SatsumaWiperSwitchInteractionTarget>() == null ||
                Vector3.Distance(knob.localPosition, new Vector3(.1619731f, .040598895f, -.030620733f)) > .00001f)
                throw new InvalidDataException("Wiper knob owner or source pose drifted.");
            Quaternion modeRotation = Quaternion.AngleAxis(-45f * (int)wiper.Mode, Vector3.up);
            if (wiper.HasSwitchKnobBaseLocalRotation)
            {
                if (!SameRotation(wiper.SwitchKnobBaseLocalRotation, WiperRestRotation) ||
                    !SameRotation(knob.localRotation, WiperRestRotation * modeRotation))
                    throw new InvalidDataException("Reviewed wiper rest basis drifted.");
            }
            else
            {
                if (!SameRotation(knob.localRotation, modeRotation) &&
                    !SameRotation(knob.localRotation, WiperRestRotation * modeRotation))
                    throw new InvalidDataException("Wiper knob differs from both legacy and reviewed frames.");
                writes.Add(() =>
                {
                    wiper.ConfigureSwitchKnobRestRotation(WiperRestRotation);
                    EditorUtility.SetDirty(wiper);
                    EditorUtility.SetDirty(knob);
                });
            }
        }

        private static void PlanHood(VehicleAssemblyController assembly, List<Action> writes, string generatedRoot)
        {
            var releases = assembly.Parts.Where(value => value?.Definition != null &&
                    value.Definition.DefinitionId == "vehicle.satsuma.part.dashboard")
                .SelectMany(value => value.GetComponentsInChildren<AssemblyHoodReleaseInteractionTarget>(true)).ToArray();
            if (releases.Length != 1) throw new InvalidDataException("Expected one existing dashboard hood-release binding.");
            AssemblyHoodReleaseInteractionTarget release = releases[0];
            Mesh handleMesh = AssetDatabase.LoadAssetAtPath<Mesh>(generatedRoot + "/Meshes/" + HandleMeshGuid + ".asset");
            MeshFilter[] candidates = release.DashboardPart.GetComponentsInChildren<MeshFilter>(true)
                .Where(value => value.sharedMesh == handleMesh).ToArray();
            if (handleMesh == null || candidates.Length != 1)
                throw new InvalidDataException("The reviewed GO13396 handle mesh is missing/ambiguous.");
            Transform handle = candidates[0].transform;
            Renderer renderer = handle.GetComponent<Renderer>();
            SphereCollider shape = release.GetComponent<SphereCollider>();
            var host = release.GetComponent<InteractionTargetHost>();
            if (handle.parent != release.transform.parent || renderer == null || shape == null || !shape.isTrigger ||
                Mathf.Abs(shape.radius - .03f) > .000001f || host == null || release.Hood == null ||
                Vector3.Distance(handle.localPosition, HandlePosition) > .00001f || !SameRotation(handle.localRotation, HandleRotation))
                throw new InvalidDataException("Reviewed hood handle/trigger hierarchy drifted.");
            // Both imported meshes share the flattened dashboard-presentation
            // parent. Keep the established target component/host references;
            // only its ray sphere and explicit animated mesh binding change.
            Vector3 desiredCenter = Matrix4x4.TRS(release.transform.localPosition,
                release.transform.localRotation, release.transform.localScale).inverse.MultiplyPoint3x4(
                TriggerPosition + TriggerRotation * TriggerCenter);
            if (release.UsesReviewedHandlePull)
            {
                if (release.LeverVisual != handle || Vector3.Distance(shape.center, desiredCenter) > .00001f ||
                    host.OutlineRenderers.Count != 1 || host.OutlineRenderers[0] != renderer)
                    throw new InvalidDataException("Existing reviewed hood-release binding drifted.");
            }
            else
            {
                MeshFilter oldMesh = release.GetComponent<MeshFilter>();
                if (release.LeverVisual != release.transform || oldMesh?.sharedMesh == null ||
                    Vector3.Distance(shape.center, Vector3.zero) > .00001f &&
                    Vector3.Distance(shape.center, oldMesh.sharedMesh.bounds.center) > .00001f)
                    throw new InvalidDataException("Unexpected legacy hood release; refusing silent retargeting.");
                writes.Add(() =>
                {
                    release.ConfigureReviewedHandlePull(handle);
                    shape.center = desiredCenter;
                    host.ConfigureOutlineRenderers(renderer);
                    EditorUtility.SetDirty(release);
                    EditorUtility.SetDirty(shape);
                    EditorUtility.SetDirty(host);
                });
            }
        }

        private static bool SameRotation(Quaternion a, Quaternion b) =>
            Mathf.Abs(Quaternion.Dot(a.normalized, b.normalized)) > .999999f;
    }
}
