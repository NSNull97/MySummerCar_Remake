using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Interaction.Query;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static class Phase1SatsumaDashboardControlsAuthoring
    {
        public const string MetersId = "vehicle.satsuma.part.dashboard-meters";
        // Frozen GAME MeshFilters84283/88289, renderers75171/80274.
        public const string HeadlightLensMeshSourceGuid = "85ee7b66475902b4187d62d12fad42cd";
        public const float HeadlightLensEmission = 40f; // Restrained HDRP presentation calibration, not a donor photometric value.
        public static readonly string[] KnobMeshSourceGuids =
        {
            "d1e33ce86b3d5154fbf7274bfb73d57e", // GO16738 / renderer76824
            "a33e2f4b5288f4d44875c1a0e7945fb4", // GO30726 / renderer80567
            "5af90b41049184c4e8382b7cf0ce4f1d", // GO26594 / renderer79478
        };
        public static int ApplyToInstance(VehicleAssemblyController assembly, string generatedRoot = null)
        {
            if (Application.isPlaying || assembly == null) throw new InvalidDataException("Author dashboard controls outside Play.");
            generatedRoot ??= Phase1SatsumaCamshaftTimingAuthoring.GeneratedRoot;
            PartInstance meters = RequirePart(assembly, MetersId);
            var electrical = assembly.GetComponent<SatsumaElectricalSystem>() ??
                throw new InvalidDataException("Author Satsuma electrical state before dashboard controls.");
            var knobs = new MeshFilter[3];
            for (int i = 0; i < knobs.Length; i++)
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(generatedRoot +
                    "/Meshes/" + KnobMeshSourceGuids[i] + ".asset");
                knobs[i] = meters.GetComponentsInChildren<MeshFilter>(true).SingleOrDefault(filter => filter.sharedMesh == mesh);
                if (mesh == null || knobs[i] == null || knobs[i].GetComponent<Renderer>() == null)
                    throw new InvalidDataException("Missing/ambiguous reviewed dashboard knob " + i);
            }
            int changed = 0;
            var controller = assembly.GetComponent<SatsumaDashboardControlsController>();
            if (controller == null)
            {
                controller = assembly.gameObject.AddComponent<SatsumaDashboardControlsController>();
                controller.Configure(electrical, knobs[0].transform, knobs[1].transform, knobs[2].transform); changed++;
            }
            else if (controller.Electrical != electrical || controller.ChokeKnob != knobs[0].transform ||
                controller.HazardKnob != knobs[1].transform || controller.LightsKnob != knobs[2].transform)
                throw new InvalidDataException("Existing dashboard controller bindings drifted.");
            for (int i = 0; i < knobs.Length; i++) changed += EnsureTarget(controller, meters, knobs[i], (SatsumaDashboardControlKind)i);
            changed += AuthorLighting(assembly, controller, generatedRoot);
            return changed;
        }

        private static int EnsureTarget(SatsumaDashboardControlsController controller, PartInstance meters,
            MeshFilter knob, SatsumaDashboardControlKind kind)
        {
            var matches = meters.GetComponentsInChildren<SatsumaDashboardControlInteractionTarget>(true)
                .Where(target => target.Kind == kind).ToArray();
            if (matches.Length > 1) throw new InvalidDataException("Duplicate dashboard interaction identity.");
            if (matches.Length == 1)
            {
                if (matches[0].Controller != controller || matches[0].GetComponent<InteractionTargetHost>() == null)
                    throw new InvalidDataException("Existing dashboard target drifted.");
                return 0;
            }
            // Keep the interaction sphere fixed while the separate choke mesh
            // translates, as the original trigger does.
            var go = new GameObject("Project-owned dashboard " + kind);
            go.layer = meters.gameObject.layer;
            go.transform.SetParent(knob.transform.parent, false);
            go.transform.localPosition = knob.transform.localPosition;
            go.transform.localRotation = knob.transform.localRotation;
            if (kind == SatsumaDashboardControlKind.Lights)
            {
                // Original trigger52097 differs slightly from mesh parent67205.
                go.transform.localPosition += new Vector3(-.00000194f, -.0000039f, -.00070667f);
                go.transform.localRotation = new Quaternion(-.015450702f, -4.378124e-8f, -1.4468722e-7f, .9998807f);
            }
            SphereCollider sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true; sphere.radius = .02f; sphere.center = new Vector3(0, -.03f, 0);
            var target = go.AddComponent<SatsumaDashboardControlInteractionTarget>();
            Renderer renderer = knob.GetComponent<Renderer>();
            target.Configure(controller, kind, meters, renderer);
            var host = go.AddComponent<InteractionTargetHost>();
            host.Configure(target); host.ConfigureOutlineRenderers(renderer); host.ConfigureSelectionPriority(45);
            return 1;
        }

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Dashboard Lighting Only")]
        public static void RefreshDashboardLightingBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before refreshing dashboard lighting.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = ApplyToInstance(contents.GetComponent<VehicleAssemblyController>());
                if (changed > 0)
                {
                    Directory.CreateDirectory("Logs");
                    File.Copy(path, "Logs/satsuma-lighting-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".prefab", false);
                    if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                        throw new InvalidDataException("Could not save scoped dashboard lighting.");
                }
                Debug.Log("SATSUMA_DASHBOARD_LIGHTING_REFRESH_OK changed=" + changed + " fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static int AuthorLighting(VehicleAssemblyController assembly, SatsumaDashboardControlsController controller, string generatedRoot)
        {
            var specs = CreateLampSpecs();
            var bindings = new List<SatsumaDashboardLampBinding>();
            var existing = assembly.GetComponent<SatsumaDashboardLightingPresenter>();
            int changed = 0;
            foreach (LampSpec spec in specs)
            {
                PartInstance owner = assembly.Parts.SingleOrDefault(part => part?.Definition?.DefinitionId == spec.PartId);
                // Optional aftermarket marker parts are not present in every
                // baseline. Never substitute headlamp beams for missing markers.
                if (owner == null && spec.OptionalMarker) continue;
                if (owner == null) throw new InvalidDataException("Missing required lamp owner " + spec.PartId);
                MountPointAuthoring ownerMount = assembly.MountPoints.Single(mount => mount.Definition != null &&
                    mount.Definition.AcceptedPartDefinitionIds.Contains(spec.PartId));
                if (!string.IsNullOrEmpty(spec.BulbMount) && !assembly.MountPoints.Any(mount => mount.MountId == spec.BulbMount))
                    throw new InvalidDataException("Author purchased bulb sockets before dashboard lights.");
                SatsumaDashboardLampBinding prior = existing?.Lamps.SingleOrDefault(binding => binding.StableLampId == spec.Id);
                Light light = prior?.Light;
                if (light == null)
                {
                    var go = new GameObject(spec.Id);
                    go.transform.SetParent(owner.transform, false);
                    // The source light frame is SATSUMA-local. Loose storage
                    // transforms must not contaminate the assembled light pose.
                    go.transform.localPosition = ownerMount.Pose.InverseTransformPoint(assembly.transform.TransformPoint(spec.Position));
                    go.transform.localRotation = Quaternion.Inverse(ownerMount.Pose.rotation) * assembly.transform.rotation * spec.Rotation;
                    light = go.AddComponent<Light>();
                    var hd = go.GetComponent<HDAdditionalLightData>() ?? go.AddComponent<HDAdditionalLightData>();
                    hd.affectsVolumetric = false;
                    light.type = spec.Spot ? LightType.Spot : LightType.Point;
                    light.color = spec.Color; light.range = spec.Range; light.spotAngle = 50;
                    light.shadows = LightShadows.None; light.lightUnit = LightUnit.Candela;
                    // HDRP calibration, not a claimed one-to-one conversion of
                    // original legacy intensity3/2 to photometric units.
                    light.intensity = spec.Spot ? 3000f : 20f; light.enabled = false;
                    changed++;
                }
                Renderer lens = null;
                Color emission = Color.black;
                if (spec.Function == SatsumaDashboardLampFunction.Headlights)
                {
                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(generatedRoot + "/Meshes/" + HeadlightLensMeshSourceGuid + ".asset");
                    MeshFilter leaf = owner.GetComponentsInChildren<MeshFilter>(true).SingleOrDefault(value => value.sharedMesh == mesh);
                    lens = leaf != null ? leaf.GetComponent<Renderer>() : null;
                    if (mesh == null || lens == null || lens.sharedMaterials.Length != 1 || lens.sharedMaterial == null ||
                        !lens.sharedMaterial.HasProperty("_EmissiveColor"))
                        throw new InvalidDataException("Missing/ambiguous reviewed HDRP headlight lens: " + spec.Id);
                    emission = spec.Color.linear * HeadlightLensEmission;
                    emission.a = 1f;
                }
                var binding = new SatsumaDashboardLampBinding(spec.Id, spec.Function, owner,
                    ownerMount.MountId, spec.Bolted, spec.BulbMount, spec.Wires, light, lens, emission);
                // The only accepted old shape is a missing optional lens binding.
                // Existing lamp identity, pose, bulb/wire/bolt rules stay intact.
                if (prior != null && (!SameCoreBinding(prior, binding) ||
                    prior.LensRenderer != null && prior.LensRenderer != lens))
                    throw new InvalidDataException("Existing lamp requirements drifted: " + spec.Id);
                bindings.Add(binding);
            }
            if (existing == null) { existing = assembly.gameObject.AddComponent<SatsumaDashboardLightingPresenter>(); changed++; }
            if (existing.Assembly != assembly || existing.Controls != controller || existing.Lamps.Length != bindings.Count ||
                !existing.Lamps.Zip(bindings, SameBinding).All(same => same))
            { existing.Configure(assembly, controller, bindings.ToArray()); EditorUtility.SetDirty(existing); changed++; }
            return changed;
        }
        private static bool SameBinding(SatsumaDashboardLampBinding a, SatsumaDashboardLampBinding b) =>
            SameCoreBinding(a, b) && a.LensRenderer == b.LensRenderer && a.LensEmissionColor == b.LensEmissionColor;
        private static bool SameCoreBinding(SatsumaDashboardLampBinding a, SatsumaDashboardLampBinding b) =>
            a.StableLampId == b.StableLampId && a.Function == b.Function && a.Owner == b.Owner && a.Light == b.Light &&
            a.OwnerMountId == b.OwnerMountId && a.BulbMountId == b.BulbMountId &&
            a.RequiresBoltedOwner == b.RequiresBoltedOwner && a.RequiredConnections.SequenceEqual(b.RequiredConnections);
        private static PartInstance RequirePart(VehicleAssemblyController assembly, string id) =>
            assembly.Parts.Single(part => part?.Definition?.DefinitionId == id);
        private static LampSpec[] CreateLampSpecs()
        {
            var result = new List<LampSpec>();
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0; string suffix = left ? "left" : "right"; float sign = left ? -1 : 1;
                var headWire = left ? SatsumaElectricalConnection.HeadlightLeft : SatsumaElectricalConnection.HeadlightRight;
                var rearWire = left ? SatsumaElectricalConnection.RearlightLeft : SatsumaElectricalConnection.RearlightRight;
                string headPart = "vehicle.satsuma.part.headlight-" + suffix;
                string rearPart = "vehicle.satsuma.part.rear-light-" + suffix;
                Color amber = new(1f, .5019608f, 0f, 1f);
                result.Add(new LampSpec("presentation.satsuma.headlight." + suffix, SatsumaDashboardLampFunction.Headlights,
                    headPart, new Vector3(sign * .51f, .13f, 1.75f), Quaternion.Euler(10, 0, 0),
                    new Color(1f, .90172416f, .5808823f), 50, true, true,
                    "mount.satsuma.headlight-" + suffix + ".light-bulb",
                    new[] { headWire, SatsumaElectricalConnection.FrontLightsHarness, SatsumaElectricalConnection.SwitchLights }));
                result.Add(new LampSpec("presentation.satsuma.parking." + suffix, SatsumaDashboardLampFunction.Parking,
                    "vehicle.satsuma.part.marker-light-" + suffix, new Vector3(sign * .38f, -.1156f, 1.749f), Quaternion.identity,
                    Color.white, 1, false, true, "", new[] { left ? SatsumaElectricalConnection.MarkerLeft : SatsumaElectricalConnection.MarkerRight,
                        SatsumaElectricalConnection.FrontLightsHarness, SatsumaElectricalConnection.SwitchLights }, true));
                result.Add(new LampSpec("presentation.satsuma.tail." + suffix, SatsumaDashboardLampFunction.RearTail,
                    rearPart, new Vector3(left ? -.5280116f : .5280766f, .21380886f, -1.856006f), Quaternion.identity,
                    Color.red, 1f, false, false, "", new[] { rearWire }));
                result.Add(new LampSpec("presentation.satsuma.hazard.front." + suffix, SatsumaDashboardLampFunction.Hazard,
                    "vehicle.satsuma.part.fender-" + suffix, new Vector3(sign * .75f, .19f, 1.5684154f), Quaternion.identity,
                    amber, .5f, false, false, "", new[] { headWire, SatsumaElectricalConnection.FrontLightsHarness }));
                result.Add(new LampSpec("presentation.satsuma.hazard.rear." + suffix, SatsumaDashboardLampFunction.Hazard,
                    rearPart, new Vector3(sign * .5820312f, .10880884f, -1.8560181f), Quaternion.identity,
                    amber, .5f, false, false, "", new[] { rearWire }));
            }
            return result.ToArray();
        }
        private sealed class LampSpec
        {
            public readonly string Id, PartId, BulbMount;
            public readonly SatsumaDashboardLampFunction Function;
            public readonly Vector3 Position; public readonly Quaternion Rotation; public readonly Color Color;
            public readonly float Range; public readonly bool Spot, Bolted, OptionalMarker;
            public readonly SatsumaElectricalConnection[] Wires;
            public LampSpec(string id, SatsumaDashboardLampFunction function, string part, Vector3 position,
                Quaternion rotation, Color color, float range, bool spot, bool bolted, string bulb,
                SatsumaElectricalConnection[] wires, bool optional = false)
            { Id=id; Function=function; PartId=part; Position=position; Rotation=rotation; Color=color;
                Range=range; Spot=spot; Bolted=bolted; BulbMount=bulb; Wires=wires; OptionalMarker=optional; }
        }
    }
}
