using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static class Phase1SatsumaFlexibleConnectionsAuthoring
    {
        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Flexible Connections Only")]
        public static void RefreshBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before authoring.");
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            Directory.CreateDirectory("Logs"); File.Copy(path, "Logs/connections-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".prefab", false);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = ApplyToInstance(root.GetComponent<VehicleAssemblyController>());
                if (changed > 0 && PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new InvalidDataException("Could not save connection bindings.");
                Debug.Log("SATSUMA_FLEXIBLE_CONNECTIONS_REFRESH_OK changed=" + changed + " physicalPosesChanged=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static int ApplyToInstance(VehicleAssemblyController assembly, string generatedRoot = null)
        {
            string root = generatedRoot ?? Phase1SatsumaInstalledBeltAssets.Root;
            if (root != Phase1SatsumaInstalledBeltAssets.Root && root != Phase1SatsumaInstalledBeltAssets.Root + "_Staging")
                throw new InvalidDataException("Unreviewed flexible-connection output root.");
            PartInstance Part(string suffix) => assembly.Parts.Single(p => p.Definition.DefinitionId == "vehicle.satsuma.part." + suffix);
            MeshFilter Leaf(string part, string guid, string hash)
            {
                string path = root + "/Meshes/" + guid + ".asset";
                using var file = File.OpenRead(path); using var sha = SHA256.Create();
                if (!BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").Equals(hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Reviewed connection mesh changed: " + part);
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                var leaf = Part(part).GetComponentsInChildren<MeshFilter>(true).Single(m => m.sharedMesh == mesh);
                Vector3 scale = leaf.transform.lossyScale;
                if ((scale - Vector3.one).sqrMagnitude > 1e-6f) throw new InvalidDataException("Connector expects canonical metre-scale meshes.");
                return leaf;
            }
            var filter = Leaf("fuel-strainer", "67e772cf7ce18d941bca6f8fae39e4f2", "D00E1D106E1F794D2B29443DB137498E017CCCF379D120E761BE1D096434E9A9");
            var pump = Leaf("fuel-pump", "3dd12b73d7fbe0a4ba5d4d6b5df4e0f7", "D4577292EDAB75075B9DFF646C0E616503D1A12C963305A18B4B372453AC4860");
            var upper = Leaf("radiator-hose1", "7001391f8a7e9fe4e90aadc1d015fb85", "AF5E13CA9952ED5B3C2071A4BD709A4065A19F2BD171E09CD1A00EC3D433F50A");
            var head = Leaf("cylinder-head", "ac00296d52fa86d4cabf01eda3ee32fd", "2DD075CC76C8B3AF99D208FE97DEABA8B7EF741D304A9C5793C9ADE64D82425B");
            var lower = Leaf("radiator-hose3", "3efa24ddd1161634e934510ba20870be", "7FB8F0B4DD6422F41376AA7969AB709F118A05CC273F3D6C9265781B82C426BE");
            var engineHose = Leaf("radiator-hose2", "df5343e740b0ddc48b99316ad74d124a", "4C4CD4F52720F6EFB84791CEA64C10A499EB7A75A39993D72FDA2B933D8F868D");
            // Ring indices and outward face triangles are reviewed dimensional
            // metadata, selected on hash-locked meshes (see the 2026-09-07 audit).
            Vector3 Center(MeshFilter mesh, int[] indices)
            { var vertices = mesh.sharedMesh.vertices; Vector3 sum = Vector3.zero; foreach (int i in indices) sum += vertices[i]; return sum / indices.Length; }
            Vector3 Inset(MeshFilter mesh, int[] indices, int a, int b, int c, float metres)
            {
                var v = mesh.sharedMesh.vertices;
                return Center(mesh, indices) - Vector3.Cross(v[b] - v[a], v[c] - v[a]).normalized * metres;
            }
            var bindings = new[]
            {
                new SatsumaFlexibleConnectionBinding(Part("fuel-strainer"), Part("fuel-pump"), filter, pump.transform,
                    Center(filter, new[]{339,340,320,391,318,954,389,317,363,321,364}),
                    Inset(pump, new[]{1198,1200,1194,1202,1206,1196,1216,1213,1208,1210},1314,1316,1315,.003f), .022f,.08f),
                new SatsumaFlexibleConnectionBinding(Part("radiator-hose1"), Part("cylinder-head"), upper, head.transform,
                    Center(upper,new[]{302,305,318,316,299,308,320,313,296,321,314,264,319,312,293,263,317,315,268,266}),
                    Inset(head,new[]{1370,1367,1349,1376,1379,1374,1364,1347,1381,3856,1361,3846,3833,1344,3844,3835,1358,1343,3842,3838,3840,1356,1351,1354},3835,3837,3838,.003f), .028f,.085f),
                new SatsumaFlexibleConnectionBinding(Part("radiator-hose3"), Part("radiator-hose2"), lower, engineHose.transform,
                    Center(lower,new[]{253,249,248,233,246,235,244,238,242,240}),
                    Inset(engineHose,new[]{83,81,411,402,85,413,79,400,91,422,397,77,420,93,399,86,418,416,95,88},422,421,420,.003f), .032f,.11f)
            };
            var presenter = assembly.GetComponent<SatsumaFlexibleConnectionPresenter>();
            if (presenter != null)
            {
                if (JsonUtility.ToJson(new BindingSet { values = presenter.Bindings }) != JsonUtility.ToJson(new BindingSet { values = bindings }))
                    throw new InvalidDataException("Existing flexible connection bindings drifted.");
                return 0;
            }
            assembly.gameObject.AddComponent<SatsumaFlexibleConnectionPresenter>().Configure(bindings); return 1;
        }
        [Serializable] private sealed class BindingSet { public SatsumaFlexibleConnectionBinding[] values; }
    }
}
