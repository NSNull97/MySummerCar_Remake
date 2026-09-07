using System;
using System.Collections.Generic;
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
    public static class Phase1SatsumaCockpitSteeringAuthoring
    {
        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Cockpit Steering Presentation Only")]
        public static void RefreshBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = ApplyToInstance(root.GetComponent<VehicleAssemblyController>());
                if (changed > 0)
                {
                    Directory.CreateDirectory("Logs");
                    File.Copy(path,"Logs/cockpit-steering-before-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+".prefab",false);
                    if (PrefabUtility.SaveAsPrefabAsset(root,path) == null) throw new InvalidDataException("Could not save cockpit steering.");
                }
                Debug.Log("SATSUMA_COCKPIT_STEERING_REFRESH_OK changed="+changed+" fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        public static int ApplyToInstance(VehicleAssemblyController assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            var visuals = new List<SatsumaCockpitSteeringBinding>();
            PartInstance Part(string suffix) => assembly.Parts.Single(p => p.Definition.DefinitionId == "vehicle.satsuma.part."+suffix);
            foreach (string suffix in new[] {"steering-column","stock-steering-wheel","gt-gt-steering-wheel"})
            {
                PartInstance owner = Part(suffix);
                foreach (var renderer in owner.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.GetComponentInParent<PartInstance>(true) != owner) continue;
                    if (!SatsumaEngineVisualVibration.IsSafeVisualLeaf(renderer.transform))
                        throw new InvalidDataException("Unsafe steering render leaf: "+renderer.name);
                    visuals.Add(new SatsumaCockpitSteeringBinding(owner,renderer.transform));
                }
            }
            // The installed wheel nut is a separate presentation leaf; its
            // interaction frame and tightening state remain assembly-owned.
            foreach (var target in assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true).Where(t => t.MountId == "mount.satsuma.steering-wheel"))
                foreach (var renderer in target.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!SatsumaEngineVisualVibration.IsSafeVisualLeaf(renderer.transform)) throw new InvalidDataException("Unsafe wheel nut renderer.");
                    visuals.Add(new SatsumaCockpitSteeringBinding(Part("stock-steering-wheel"),renderer.transform,Part("gt-gt-steering-wheel")));
                }
            var existing = assembly.GetComponent<SatsumaCockpitSteeringPresenter>();
            if (existing != null)
            {
                if (existing.Bindings.Length != visuals.Count || !existing.Bindings.Select((b,i) => b.Owner == visuals[i].Owner && b.AlternativeOwner == visuals[i].AlternativeOwner && b.Leaf == visuals[i].Leaf).All(v=>v))
                    throw new InvalidDataException("Existing cockpit steering bindings drifted.");
                return 0;
            }
            var config = DonorPathConfiguration.LoadFromFile("Config/DonorPaths.local.json");
            string source = Path.Combine(config.DonorStagingDirectory,"raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity");
            using(var sha=SHA256.Create()) using(var stream=File.OpenRead(source))
                if (!BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").Equals(Phase1SatsumaBaselineBuilder.LockedSceneSha256,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Cockpit steering source hash drift.");
            var scene = DonorUnitySceneModel.Parse(source);
            scene.GetTransformRelativeTo(67214,64200,out Vector3 pivot,out Quaternion rotation,out _);
            existing = assembly.gameObject.AddComponent<SatsumaCockpitSteeringPresenter>();
            existing.Configure(assembly.GetComponent<VehicleInputRouter>(),pivot,rotation*Vector3.forward,visuals.ToArray());
            EditorUtility.SetDirty(existing);
            Debug.Log("SATSUMA_COCKPIT_STEERING_BINDINGS leaves="+visuals.Count+" pivot="+pivot.ToString("F7")+" axis="+(rotation*Vector3.forward).ToString("F7"));
            return 1;
        }
    }
}
