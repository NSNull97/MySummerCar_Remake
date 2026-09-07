using System;
using System.IO;
using System.Linq;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.NWH;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Bounded, idempotent upgrade of the existing canonical chassis.</summary>
    public static class Phase1SatsumaRoadPhysicsAuthoring
    {
        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Road Collision Policy Only")]
        public static void RefreshBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before refreshing road collision policy.");
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = ApplyToInstance(root.GetComponent<VehicleAssemblyController>());
                if (changed > 0)
                {
                    Directory.CreateDirectory("Logs");
                    File.Copy(path, "Logs/road-physics-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".prefab", false);
                    if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                        throw new InvalidDataException("Could not save road collision policy.");
                }
                Debug.Log("SATSUMA_ROAD_COLLISION_REFRESH_OK changed=" + changed + " fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static int ApplyToInstance(VehicleAssemblyController assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            if (assembly.GetComponent<LegacySatsumaBaselineMetadata>() == null)
                throw new InvalidDataException("Canonical Satsuma provenance is required.");
            Rigidbody body = assembly.GetComponent<Rigidbody>() ?? throw new InvalidDataException("Missing chassis body.");
            if (body.collisionDetectionMode != CollisionDetectionMode.ContinuousSpeculative &&
                body.collisionDetectionMode != CollisionDetectionMode.ContinuousDynamic)
                throw new InvalidDataException("Unexpected collision policy; inspect authored drift before replacing it.");
            int changed = body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic ? 0 : 1;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            EditorUtility.SetDirty(body);
            var backend = assembly.GetComponent<NwhWheelPhysicsBackend>() ?? throw new InvalidDataException("Missing NWH backend.");
            if (backend.Wheels.Length != 4 || backend.Wheels.Any(w => w == null))
                throw new InvalidDataException("Expected four explicit NWH wheel references.");
            foreach (var wheel in backend.Wheels)
                if (wheel.useContactModification) { wheel.useContactModification = false; EditorUtility.SetDirty(wheel); changed++; }
            var contactPolicy = assembly.GetComponent<SatsumaWheelContactPolicy>();
            if (contactPolicy == null)
            {
                contactPolicy = assembly.gameObject.AddComponent<SatsumaWheelContactPolicy>();
                contactPolicy.Configure(backend); EditorUtility.SetDirty(contactPolicy); changed++;
            }
            else if (contactPolicy.Backend != backend)
                throw new InvalidDataException("Wheel contact policy binding drift; inspect before replacing.");
            var front = assembly.GetComponent<SatsumaFrontSuspensionController>() ?? throw new InvalidDataException("Missing front bindings.");
            var playerShapes = front.ChassisColliders.Where(c => c != null &&
                c.excludeLayers.value == ~(1 << LayerMask.NameToLayer("Player"))).ToArray();
            var proxies = playerShapes.Select(c => c.attachedRigidbody).Distinct().ToArray();
            if (playerShapes.Length != 4 || proxies.Length != 1 || proxies[0] == null || !proxies[0].isKinematic)
                throw new InvalidDataException("Expected four player-only shapes on one isolated kinematic actor.");
            var follower = assembly.GetComponent<VehiclePlayerCollisionFollower>();
            if (follower == null)
            {
                follower = assembly.gameObject.AddComponent<VehiclePlayerCollisionFollower>();
                follower.Configure(body, proxies[0]);
                EditorUtility.SetDirty(follower); EditorUtility.SetDirty(proxies[0]); changed++;
            }
            else if (follower.Chassis != body || follower.PlayerCollisionBody != proxies[0])
                throw new InvalidDataException("Player collision follower binding drift; inspect before replacing.");
            return changed;
        }
    }
}
