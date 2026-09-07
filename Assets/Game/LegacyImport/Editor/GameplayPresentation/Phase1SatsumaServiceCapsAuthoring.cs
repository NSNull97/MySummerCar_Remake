using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Interaction.Query;
using MSC.LegacyImport.Editor.Configuration;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static class Phase1SatsumaServiceCapsAuthoring
    {
        private const string OilMesh = "7c0509b632ed0fd48a51297f7d78e393";
        private const string RadiatorMesh = "22c31f3bc669f0f4faafbb39a6c896c1";
        private const string HydraulicMesh = "7619b6ed3381ffe4baa418b26282c26b";
        private static readonly Profile[] Profiles =
        {
            new("rocker-cover", SatsumaServiceCapKind.MotorOil, OilMesh, new(.1117f, .0062f, .0389f)),
            new("radiator", SatsumaServiceCapKind.Coolant, RadiatorMesh, new(.10730009f, .00829966f, .20180258f)),
            new("brake-master-cylinder", SatsumaServiceCapKind.BrakeFront, HydraulicMesh, new(.0009f, -.0378f, .0807f)),
            new("brake-master-cylinder", SatsumaServiceCapKind.BrakeRear, HydraulicMesh, new(.0008996926f, .021700017f, .08069999f)),
            new("clutch-master-cylinder", SatsumaServiceCapKind.Clutch, HydraulicMesh, new(.0021000481f, -.00940026f, .08069999f)),
        };

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Service Caps Only")]
        public static void RefreshBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before cap authoring.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Directory.CreateDirectory("Logs");
                File.Copy(path, "Logs/codex-service-caps-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".prefab", false);
                int changes = ApplyToInstance(contents.GetComponent<VehicleAssemblyController>());
                if (changes > 0 && PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                    throw new InvalidDataException("Could not save cap/receiver bindings.");
                Debug.Log("SATSUMA_SERVICE_CAPS_OK changed=" + changes + " stockCaps=5 fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        public static int ApplyToInstance(VehicleAssemblyController assembly, string generatedRoot = null)
        {
            if (assembly == null || Application.isPlaying) throw new ArgumentException("Stopped editor and explicit assembly required.");
            string output = generatedRoot ?? Phase1SatsumaEngineAdjustmentsAuthoring.GeneratedRoot;
            ImportMissingRadiatorCap(output);
            var host = assembly.GetComponent<VehicleSimulationHost>();
            if (host == null) throw new InvalidDataException("Missing existing simulation host.");
            var resolved = new List<(Profile profile, PartInstance part, Renderer renderer)>();
            int existingCount = 0;
            foreach (Profile profile in Profiles)
            {
                PartInstance part = assembly.Parts.Single(value => value?.Definition?.DefinitionId == "vehicle.satsuma.part." + profile.Part);
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(output + "/Meshes/" + profile.Mesh + ".asset");
                if (mesh == null) throw new InvalidDataException("Missing reviewed cap mesh " + profile.Mesh);
                MeshFilter[] matches = part.GetComponentsInChildren<MeshFilter>(true).Where(value => value.sharedMesh == mesh &&
                    value.GetComponentInParent<PartInstance>() == part &&
                    Vector3.Distance(part.transform.InverseTransformPoint(value.transform.position), profile.Position) < .004f).ToArray();
                bool missingRadiatorCap = profile.Kind == SatsumaServiceCapKind.Coolant && matches.Length == 0;
                if (!missingRadiatorCap && (matches.Length != 1 || matches[0].GetComponent<Renderer>() == null || matches[0].GetComponents<Collider>().Length != 0))
                    throw new InvalidDataException("Ambiguous/missing separate cap geometry " + profile.Kind + "; expected " + profile.Position);
                Renderer renderer = missingRadiatorCap ? null : matches[0].GetComponent<Renderer>();
                AssemblyServiceCapState state = part.GetComponent<AssemblyServiceCapState>();
                AssemblyServiceCapTarget[] targets = part.GetComponentsInChildren<AssemblyServiceCapTarget>(true);
                if (state != null)
                {
                    AssemblyServiceCapTarget[] exact = targets.Where(value => value.State == state && value.State.Kind(value.CapIndex) == profile.Kind).ToArray();
                    if (renderer == null || state.Part != part || exact.Length != 1 || exact[0].CapRenderer != renderer ||
                        exact[0].GetComponent<InteractionTargetHost>() == null || exact[0].GetComponent<Collider>() == null ||
                        exact[0].GetComponent<SatsumaServiceFluidReceiver>()?.Caps != state)
                        throw new InvalidDataException("Existing cap binding drifted " + profile.Kind);
                    existingCount++;
                }
                else if (targets.Length != 0) throw new InvalidDataException("Cap target without part state.");
                resolved.Add((profile, part, renderer));
            }
            if (existingCount == Profiles.Length) return 0;
            if (existingCount != 0) throw new InvalidDataException("Partial cap authoring requires explicit repair, not silent overwrite.");
            // Source geometry and all five old/new targets validated before mutation.
            foreach (var group in resolved.GroupBy(value => value.part))
            {
                var state = group.Key.gameObject.AddComponent<AssemblyServiceCapState>();
                state.Configure(group.Key, group.Select(value => value.profile.Kind).ToArray());
                int index = 0;
                foreach (var entry in group)
                {
                    Renderer capRenderer = entry.renderer;
                    if (capRenderer == null)
                    {
                        // The frozen loose radiator has the same body mesh as the installed
                        // radiator but omits its separate cap. Add only that reviewed leaf.
                        var cap = new GameObject("TemporaryDirectImport radiator cap");
                        cap.transform.SetParent(group.Key.transform, false);
                        cap.transform.localPosition = entry.profile.Position;
                        cap.transform.localRotation = Quaternion.AngleAxis(22f, Vector3.forward);
                        cap.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(output + "/Meshes/" + RadiatorMesh + ".asset");
                        capRenderer = cap.AddComponent<MeshRenderer>();
                        capRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(output + "/Materials/ad2f7b6e8cc080845a7a7fd4264fbb83.mat");
                    }
                    var control = new GameObject("Project-owned service cap " + entry.profile.Kind) { layer = group.Key.gameObject.layer };
                    control.transform.SetParent(group.Key.transform, false);
                    control.transform.localPosition = entry.profile.Position;
                    SphereCollider collider = control.AddComponent<SphereCollider>(); collider.radius = .025f; collider.isTrigger = true;
                    var target = control.AddComponent<AssemblyServiceCapTarget>(); target.Configure(state, index, capRenderer);
                    var receiver = control.AddComponent<SatsumaServiceFluidReceiver>(); receiver.Configure(host, state, index);
                    var interaction = control.AddComponent<InteractionTargetHost>(); interaction.Configure(target);
                    interaction.ConfigureSelectionPriority(46); interaction.ConfigureOutlineRenderers(capRenderer);
                    index++;
                }
            }
            return Profiles.Length;
        }

        private static void ImportMissingRadiatorCap(string output)
        {
            string canonical = Phase1SatsumaEngineAdjustmentsAuthoring.GeneratedRoot;
            if (output != canonical && output != canonical + "_Staging") throw new InvalidDataException("Unexpected private cap output.");
            string path = output + "/Meshes/" + RadiatorMesh + ".asset";
            var configuration = DonorPathConfiguration.LoadFromFile("Config/DonorPaths.local.json");
            string source = Path.Combine(configuration.DonorStagingDirectory,
                "raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/Mesh/motor_radiator_cap.asset");
            const string hash = "249536A66A0F6E8E906CE7E97F75D5E104FD6A9998BAA4D9F95122A690AB857F";
            if (Hash(source) != hash || !File.ReadAllText(source + ".meta").Contains("guid: " + RadiatorMesh) ||
                File.Exists(path) && Hash(path) != hash ||
                AssetDatabase.LoadAssetAtPath<Material>(output + "/Materials/ad2f7b6e8cc080845a7a7fd4264fbb83.mat") == null)
                throw new InvalidDataException("Reviewed radiator cap source, generated mesh or existing material drifted.");
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.Copy(source, path, false);
                using var sha = SHA256.Create();
                string guid = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes("satsuma-service-cap:" + output + ":" + RadiatorMesh)))
                    .Replace("-", "").Substring(0, 32).ToLowerInvariant();
                File.WriteAllText(path + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\n", new UTF8Encoding(false));
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        private static string Hash(string path)
        { using var stream = File.OpenRead(path); using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }

        private readonly struct Profile
        {
            public Profile(string part, SatsumaServiceCapKind kind, string mesh, Vector3 position)
            { Part = part; Kind = kind; Mesh = mesh; Position = position; }
            public string Part { get; }
            public SatsumaServiceCapKind Kind { get; }
            public string Mesh { get; }
            public Vector3 Position { get; }
        }
    }
}
