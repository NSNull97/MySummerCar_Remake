using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.LegacyImport.Editor.Configuration;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static class Phase1SatsumaInstrumentAuthoring
    {
        private const string EmissionGuid = "8e5238185de786e4fbda3e5d974b8e02";
        private const string EmissionHash = "936EC6816C68F1B381F57B6024CCE36320FADCD1BE55B2C5E48D512F1C8C7266";
        private const string Root = Phase1SatsumaInstalledBeltAssets.Root;

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Refresh Stock Instruments Only")]
        public static void RefreshBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before authoring instruments.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            Directory.CreateDirectory("Logs"); File.Copy(path, "Logs/instruments-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".prefab", false);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = ApplyToInstance(root.GetComponent<VehicleAssemblyController>());
                if (changed > 0 && PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new InvalidDataException("Could not save instrument bindings.");
                Debug.Log("SATSUMA_INSTRUMENT_REFRESH_OK changed=" + changed + " fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static int ApplyToInstance(VehicleAssemblyController assembly, string generatedRoot = null)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            string root = generatedRoot ?? Root;
            if (root != Root && root != Root + "_Staging") throw new InvalidDataException("Unreviewed instrument output root.");
            var old = assembly.GetComponent<SatsumaInstrumentPresenter>();
            if (old != null)
            {
                if (old.Needles.Length != 11 || old.Illumination.Length != 13 || old.Warnings.Length != 5 ||
                    old.Needles.Any(n => n.Leaf == null || n.Owner == null) || old.Illumination.Any(l => l.Renderer == null) ||
                    old.Warnings.Any(w => w.Visual == null)) throw new InvalidDataException("Existing instrument bindings drifted.");
                ValidateEmission(root); return EnsureWarningQuads(old);
            }
            var config = DonorPathConfiguration.LoadFromFile("Config/DonorPaths.local.json");
            string assets = Path.Combine(config.DonorStagingDirectory, "raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets");
            string sourceScene = Path.Combine(assets, "_Scenes/GAME.unity");
            if (!Hash(sourceScene).Equals(Phase1SatsumaBaselineBuilder.LockedSceneSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Instrument evidence differs from the locked scene.");
            var scene = DonorUnitySceneModel.Parse(sourceScene);
            Texture2D emit = ImportEmission(assets, root);
            Material face = MaterialVariant(root, "StockInstrumentFace", "c1430bf5323813a4bae1fa52be66bf68", emit);
            Material needleMaterial = MaterialVariant(root, "StockInstrumentNeedle", "90d61f2c2ba45fe449ec424594535d62", null);
            PartInstance Part(string suffix) => assembly.Parts.Single(p => p.Definition.DefinitionId == "vehicle.satsuma.part." + suffix);
            PartInstance meters = Part("dashboard-meters"), clock = Part("clock-gauge"), dashboard = Part("dashboard");
            var needles = new List<SatsumaInstrumentNeedle>(); var illumination = new List<SatsumaInstrumentIllumination>();
            Renderer Leaf(PartInstance owner, long rendererId)
            {
                // Names are frozen source selectors only in this Editor pass;
                // the runtime receives explicit object references and typed kinds.
                Renderer result = owner.GetComponentsInChildren<Renderer>(true).Single(r => r.name.EndsWith("_" + rendererId, StringComparison.Ordinal));
                var source = scene.GetStaticRenderer(rendererId);
                if (result.GetComponent<MeshFilter>()?.sharedMesh != AssetDatabase.LoadAssetAtPath<Mesh>(root + "/Meshes/" + source.MeshGuid + ".asset"))
                    throw new InvalidDataException("Instrument source mesh drift: " + rendererId);
                return result;
            }
            void Add(SatsumaInstrumentKind kind, PartInstance owner, long ownerSource, long rendererId, int power = 0)
            {
                Renderer visual = Leaf(owner, rendererId); var record = scene.GetStaticRenderer(rendererId);
                var source = scene.GetTransform(scene.GetTransformIdForGameObject(record.GameObjectId));
                scene.GetStaticRendererTransformRelativeTo(rendererId, ownerSource, out _, out Quaternion rotation, out _);
                Quaternion zero = rotation * Quaternion.Inverse(source.LocalRotation);
                needles.Add(new SatsumaInstrumentNeedle(kind, owner, visual.transform, zero, power));
                bool digit = kind == SatsumaInstrumentKind.Odometer;
                visual.sharedMaterial = digit ? face : needleMaterial;
                illumination.Add(new SatsumaInstrumentIllumination(owner, visual, digit
                    ? new Color(.10780333f, .7647059f, .084342554f) * 8f : new Color(.8f, .25f, .075f) * 8f));
            }
            Add(SatsumaInstrumentKind.Speed, meters, 52392, 78391);
            Add(SatsumaInstrumentKind.Coolant, meters, 52392, 81896);
            Add(SatsumaInstrumentKind.Fuel, meters, 52392, 78836);
            Add(SatsumaInstrumentKind.ClockHour, clock, 60864, 74302);
            Add(SatsumaInstrumentKind.ClockMinute, clock, 60864, 77848);
            long[] digits = { 81199, 79802, 80199, 75079, 79716, 79944 };
            for (int i = 0; i < digits.Length; i++) Add(SatsumaInstrumentKind.Odometer, meters, 52392, digits[i], i);
            foreach (var item in new[] { (meters, 80132L), (clock, 78621L) })
            {
                Renderer visual = Leaf(item.Item1, item.Item2); visual.sharedMaterial = face;
                illumination.Add(new SatsumaInstrumentIllumination(item.Item1, visual, new Color(.10780333f, .7647059f, .084342554f) * 8f));
            }
            var warnings = new List<SatsumaInstrumentWarningBinding>();
            foreach (var item in new[] { (SatsumaInstrumentWarning.OilPressure,71749L,Color.red),
                (SatsumaInstrumentWarning.LeftIndicator,50404L,Color.green), (SatsumaInstrumentWarning.RightIndicator,48632L,Color.green),
                (SatsumaInstrumentWarning.HighBeam,70045L,Color.blue), (SatsumaInstrumentWarning.Charging,70287L,Color.red) })
            {
                GameObject visual;
                if (item.Item1 == SatsumaInstrumentWarning.Charging)
                {
                    visual = new GameObject(); var light = visual.AddComponent<Light>(); light.type = LightType.Point;
                    visual.AddComponent<HDAdditionalLightData>(); light.range = .025f; light.intensity = .5f; light.color = Color.red;
                    light.shadows = LightShadows.None;
                }
                else
                {
                    // Source MeshFilter 85096/84757/89576 uses builtin 10210
                    // (Quad), not Cube. Depth is not part of the light aperture.
                    // Unity-owned primitive replacing the omitted donor builtin,
                    // with the reviewed presentation-only source pose, no collider.
                    visual = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
                    visual.GetComponent<Renderer>().sharedMaterial = WarningMaterial(root, item.Item1, item.Item3);
                    visual.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                visual.name = "presentation.satsuma.instrument." + item.Item1.ToString().ToLowerInvariant();
                scene.GetTransformRelativeTo(item.Item2, 52392, out Vector3 position, out Quaternion rotation, out Vector3 scale);
                visual.transform.SetParent(meters.transform, false); visual.transform.SetLocalPositionAndRotation(position, rotation); visual.transform.localScale = scale;
                visual.SetActive(false); warnings.Add(new SatsumaInstrumentWarningBinding(item.Item1, visual));
            }
            var presenter = assembly.gameObject.AddComponent<SatsumaInstrumentPresenter>();
            presenter.Configure(assembly.GetComponent<VehicleSimulationHost>(), assembly, assembly.GetComponent<SatsumaDashboardControlsController>(),
                assembly.GetComponent<SatsumaIgnitionController>(), dashboard, meters, clock, needles.ToArray(), illumination.ToArray(), warnings.ToArray());
            EditorUtility.SetDirty(presenter); return 1;
        }

        private static int EnsureWarningQuads(SatsumaInstrumentPresenter presenter)
        {
            GameObject reference = GameObject.CreatePrimitive(PrimitiveType.Quad);
            try
            {
                Mesh quad = reference.GetComponent<MeshFilter>().sharedMesh; int changed = 0;
                foreach (var warning in presenter.Warnings)
                {
                    if (warning.Kind == SatsumaInstrumentWarning.Charging) continue;
                    MeshFilter filter = warning.Visual.GetComponent<MeshFilter>();
                    if (filter == null) throw new InvalidDataException("Missing instrument warning mesh.");
                    if (filter.sharedMesh == quad) continue;
                    filter.sharedMesh = quad; EditorUtility.SetDirty(filter); changed++;
                }
                return changed;
            }
            finally { UnityEngine.Object.DestroyImmediate(reference); }
        }

        private static Texture2D ImportEmission(string sourceRoot, string root)
        {
            string source = Path.Combine(sourceRoot, "Texture2D/satsuma_dash_gauges_emit.png");
            if (Hash(source) != EmissionHash || Hash(Path.Combine(sourceRoot, "Material/satsuma_gauges_lit.mat")) !=
                "22ED8062B729C1AF8345EE692B10C2FF1BA975B79F29FBB11B04A12695EFEF4D") throw new InvalidDataException("Instrument illumination source drift.");
            string path = root + "/Textures/" + EmissionGuid + ".png";
            if (!File.Exists(path))
            {
                File.Copy(source, path, false);
                string guid;
                using (var sha = SHA256.Create()) guid = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes("msc.instruments:" + path))).Replace("-", "").Substring(0,32).ToLowerInvariant();
                File.WriteAllText(path + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\nTextureImporter:\n  serializedVersion: 13\n  textureShape: 1\n", new UTF8Encoding(false));
            }
            if (Hash(path) != EmissionHash) throw new InvalidDataException("Instrument illumination target drift.");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureShape != TextureImporterShape.Texture2D || !importer.sRGBTexture)
            { importer.textureShape = TextureImporterShape.Texture2D; importer.sRGBTexture = true; importer.SaveAndReimport(); }
            return ValidateEmission(root);
        }
        private static Texture2D ValidateEmission(string root)
        {
            string path = root + "/Textures/" + EmissionGuid + ".png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || Hash(path) != EmissionHash) throw new InvalidDataException("Missing reviewed instrument emission map.");
            return texture;
        }
        private static Material MaterialVariant(string root, string name, string sourceGuid, Texture2D emission)
        {
            string path = root + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var source = AssetDatabase.LoadAssetAtPath<Material>(root + "/Materials/" + sourceGuid + ".mat");
            if (source == null) throw new InvalidDataException("Missing source instrument material.");
            material = new Material(source) { name = name + " TemporaryDirectImport" };
            material.SetTexture("_EmissiveColorMap", emission); material.SetColor("_EmissiveColor", Color.black);
            material.SetFloat("_AlbedoAffectEmissive", 0f); material.SetFloat("_UseEmissiveIntensity", 0f);
            HDMaterial.ValidateMaterial(material); AssetDatabase.CreateAsset(material, path); return material;
        }
        private static Material WarningMaterial(string root, SatsumaInstrumentWarning kind, Color color)
        {
            string path = root + "/Materials/InstrumentWarning-" + kind + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material != null) return material;
            material = new Material(Shader.Find("HDRP/Unlit")) { name = "Instrument warning " + kind };
            material.SetColor("_UnlitColor", color); material.SetColor("_EmissiveColor", color * 8f);
            HDMaterial.ValidateMaterial(material); AssetDatabase.CreateAsset(material, path); return material;
        }
        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path); using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }
}
