using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MSC.LegacyImport.Editor.Configuration;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static class Phase1SatsumaDriverStationAuthoring
    {
        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Interior Driver Station Only")]
        public static void RefreshBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before authoring the driver station.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            Directory.CreateDirectory("Logs");
            File.Copy(path, "Logs/driver-station-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".prefab", false);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = ApplyToInstance(root.GetComponent<VehicleAssemblyController>());
                if (changed > 0 && PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new InvalidDataException("Could not save interior driver station.");
                Debug.Log("SATSUMA_DRIVER_STATION_REFRESH_OK changed=" + changed + " fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static int ApplyToInstance(VehicleAssemblyController assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            var old = assembly.GetComponent<SatsumaDriverStation>();
            if (old != null)
            {
                if (old.ActivationTrigger == null || !old.ActivationTrigger.isTrigger || old.DriverEyeAnchor == null ||
                    old.DriverSeat == null || old.DriverSeat.Definition == null || old.VehicleBody != assembly.GetComponent<Rigidbody>() || old.StationId != SatsumaDriverStation.StockStationId ||
                    old.DriverSeat.Definition.DefinitionId != "vehicle.satsuma.part.seat-driver" ||
                    old.ActivationTrigger.transform.parent != assembly.transform || old.DriverEyeAnchor.parent != assembly.transform ||
                    old.ActivationTrigger.center != Vector3.zero || old.ActivationTrigger.transform.localScale != Vector3.one ||
                    Vector3.Distance(old.DriverEyeAnchor.localPosition, new Vector3(-.3000002f,.5608089f,.02000036f)) > .00001f ||
                    Quaternion.Angle(old.DriverEyeAnchor.localRotation, Quaternion.identity) > .001f ||
                    Vector3.Distance(old.ActivationTrigger.transform.localPosition, new Vector3(-.282f,.05692613284f,-.06712156f)) > .00001f ||
                    old.ActivationTrigger.direction != 2 || Mathf.Abs(old.ActivationTrigger.radius - .03f) > .00001f ||
                    Mathf.Abs(old.ActivationTrigger.height - .3f) > .00001f)
                    throw new InvalidDataException("Existing driver station authoring drifted.");
                return 0;
            }
            var config = DonorPathConfiguration.LoadFromFile("Config/DonorPaths.local.json");
            string scenePath = Path.Combine(config.DonorStagingDirectory,
                "raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity");
            using (var sha = SHA256.Create())
            using (var input = File.OpenRead(scenePath))
                if (!BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").Equals(
                    Phase1SatsumaBaselineBuilder.LockedSceneSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Driver station source differs from the locked scene.");
            var scene = DonorUnitySceneModel.Parse(scenePath);
            scene.GetTransformRelativeTo(37601, 64200, out Vector3 position, out Quaternion rotation, out Vector3 scale);
            Vector3 center = position + rotation * Vector3.Scale(new Vector3(0,-.25f,0), scale);
            if (Vector3.Distance(center, new Vector3(-.282f,.05692613284f,-.06712156f)) > .00001f)
                throw new InvalidDataException("Normalized interior trigger source pose drifted.");
            PartInstance seat = assembly.Parts.Single(p => p.Definition.DefinitionId == "vehicle.satsuma.part.seat-driver");
            Rigidbody body = assembly.GetComponent<Rigidbody>() ?? throw new InvalidDataException("Missing canonical car body.");
            var metadata = assembly.GetComponent<LegacySatsumaBaselineMetadata>() ?? throw new InvalidDataException("Missing canonical provenance.");
            Transform trigger = new GameObject("interaction.satsuma.driver-station").transform;
            trigger.SetParent(assembly.transform, false);
            trigger.SetLocalPositionAndRotation(center, rotation);
            trigger.gameObject.layer = 2; // Ignore Raycast; this is body-overlap activation, not a clickable door.
            var capsule = trigger.gameObject.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true; capsule.direction = 2;
            capsule.radius = .03f * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            capsule.height = .3f * Mathf.Abs(scale.z);
            Transform eyes = new GameObject("anchor.satsuma.driver-eyes").transform;
            eyes.SetParent(assembly.transform, false);
            scene.GetTransformRelativeTo(48821, 64200, out Vector3 eyePosition, out _, out _);
            eyes.SetLocalPositionAndRotation(eyePosition, Quaternion.identity);
            var station = assembly.gameObject.AddComponent<SatsumaDriverStation>();
            station.Configure(metadata.StableVehicleId, capsule, eyes, seat, body);
            EditorUtility.SetDirty(station);
            return 1;
        }
    }
}
