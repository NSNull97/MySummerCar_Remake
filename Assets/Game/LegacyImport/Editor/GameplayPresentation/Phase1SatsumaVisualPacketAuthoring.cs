using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.LegacyImport.Editor.Configuration;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static class Phase1SatsumaVisualPacketAuthoring
    {
        public static void RefreshAudioAndConnectionsBatch()
        {
            Phase1SatsumaEngineAudioImporter.Build();
            Phase1UserSelectedAudioImporter.Build();
            Phase1SatsumaFlexibleConnectionsAuthoring.RefreshBatch();
            Phase1SatsumaInstrumentAuthoring.RefreshBatch();
        }

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Visual Motion And Belt Only")]
        public static void RefreshMotionAndBeltBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before authoring.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            string backup = "Logs/visual-packet-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
            Directory.CreateDirectory(backup);
            File.Copy(path, backup + "/Satsuma.prefab", false);
            File.Copy(Phase1SatsumaInstalledBeltAssets.MaterialPath, backup + "/InstalledBelt.mat", false);
            Phase1SatsumaInstalledBeltAssets.RepairMissingCanonicalTexture();
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var assembly = root.GetComponent<VehicleAssemblyController>();
                int changes = Phase1SatsumaEngineFeedbackAuthoring.ApplyToInstance(assembly) +
                    Phase1SatsumaEngineMotionAuthoring.ApplyToInstance(assembly);
                if (changes > 0 && PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new InvalidDataException("Could not save scoped visual bindings.");
                Debug.Log("SATSUMA_VISUAL_PACKET_MOTION_REFRESH_OK changes=" + changes + " fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AuditDonorEvidenceBatch();
        }

        // Reviewed source selectors are Editor-only provenance, never gameplay lookups.
        public static void AuditDonorEvidenceBatch()
        {
            var config = DonorPathConfiguration.LoadFromFile("Config/DonorPaths.local.json");
            string source = Path.Combine(config.DonorStagingDirectory,
                "raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity");
            using (var stream = File.OpenRead(source))
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() !=
                    Phase1SatsumaBaselineBuilder.LockedSceneSha256)
                    throw new InvalidDataException("Visual audit source drifted from the version lock.");
            var scene = DonorUnitySceneModel.Parse(source);
            string output = Path.Combine(config.DonorStagingDirectory, "runtime-inspection/visual-packet-20260907");
            Directory.CreateDirectory(output);
            foreach (long root in new long[] { 37889, 52392, 60864, 42583, 38339, 45342, 46587, 61313 })
            {
                var report = new StringBuilder();
                var renderers = scene.GetStaticRenderersBelowIncludingInactive(root);
                foreach (var t in scene.GetDescendants(root, true))
                {
                    report.AppendLine("TRANSFORM " + t.TransformId + " gameObject=" + t.GameObjectId + " parent=" + t.FatherTransformId +
                        " name=" + scene.GetGameObjectName(t.GameObjectId) + " local=" + t.LocalPosition.ToString("F7") +
                        " rotation=" + t.LocalRotation.eulerAngles.ToString("F5") + " scale=" + t.LocalScale.ToString("F5"));
                    foreach (var r in renderers.Where(r => r.GameObjectId == t.GameObjectId))
                        report.AppendLine("RENDERER " + r.ComponentId + " mesh=" + r.MeshGuid + " materials=" + string.Join(",", r.MaterialGuids));
                    foreach (var mono in scene.GetMonoBehaviours(t.GameObjectId))
                    {
                        report.AppendLine("EVIDENCE_COMPONENT " + mono.ComponentId + " script=" + mono.ScriptGuid);
                        File.WriteAllText(Path.Combine(output, "component-" + mono.ComponentId + ".txt"), mono.SerializedBody);
                    }
                }
                File.WriteAllText(Path.Combine(output, "tree-" + root + ".txt"), report.ToString());
            }
            var related = new StringBuilder();
            foreach (var mono in scene.MonoBehaviours)
            {
                string body = mono.SerializedBody;
                bool clock = body.IndexOf("TimeRotation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    body.IndexOf("54696d65526f746174696f6e", StringComparison.OrdinalIgnoreCase) >= 0;
                bool lamps = new long[] { 35702, 33992, 12588, 14347, 34235, 29015, 23513 }.Any(id =>
                    body.Contains("{fileID: " + id + "}"));
                if (!clock && !lamps) continue;
                related.AppendLine(mono.ComponentId + " GO=" + mono.GameObjectId + " name=" + scene.GetGameObjectName(mono.GameObjectId) +
                    " clock=" + clock + " lamps=" + lamps);
                File.WriteAllText(Path.Combine(output, "related-" + mono.ComponentId + ".txt"), body);
            }
            File.WriteAllText(Path.Combine(output, "related-components.txt"), related.ToString());
            Debug.Log("SATSUMA_VISUAL_PACKET_DONOR_AUDIT_OK roots=8 originalInstallationUnchanged=true");
        }
    }
}
