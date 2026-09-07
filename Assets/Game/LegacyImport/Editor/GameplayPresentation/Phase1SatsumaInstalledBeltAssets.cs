using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MSC.LegacyImport.Editor.Configuration;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Reviewed private Phase 1 mesh/texture only; retains the belt's two skin bind poses.</summary>
    public static class Phase1SatsumaInstalledBeltAssets
    {
        public const string MeshGuid = "7dab2df4b56946b4c9f8892f57e8e978";
        public const string MaterialGuid = "1dc990c84250ce844b24b6439ec55445";
        public const string TextureGuid = "92213420fc2d0b34f938d80a92971def";
        public const string Root = "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma";
        public const string MeshPath = Root + "/Meshes/" + MeshGuid + ".asset";
        public const string MaterialPath = Root + "/Materials/" + MaterialGuid + ".mat";
        private const string TexturePath = Root + "/Textures/" + TextureGuid + ".png";

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Repair Missing Installed Belt Texture Only")]
        public static void RepairMissingCanonicalTexture() => ImportReviewedAssets(null, true);

        public static void ImportReviewedAssets(string generatedRoot = null, bool repairKnownMissingTexture = false)
        {
            string outputRoot = generatedRoot ?? Root;
            if (outputRoot != Root && outputRoot != Root + "_Staging")
                throw new InvalidDataException("Installed belt output must be the canonical or reviewed staging Satsuma root.");
            string meshPath = outputRoot + "/Meshes/" + MeshGuid + ".asset";
            string materialPath = outputRoot + "/Materials/" + MaterialGuid + ".mat";
            string texturePath = outputRoot + "/Textures/" + TextureGuid + ".png";
            DonorPathConfiguration configuration = DonorPathConfiguration.LoadFromFile("Config/DonorPaths.local.json");
            string sourceRoot = Path.Combine(configuration.DonorStagingDirectory,
                "raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets");
            var assets = new[]
            {
                (source: "Mesh/motor_fanbelt_001.asset", target: meshPath, guid: MeshGuid,
                    hash: "0BD2CE79A63CEE86005541B3808C204D0C53F8DF29944A8EE5F0D780379A30FE"),
                (source: "Texture2D/fanbelt.png", target: texturePath, guid: TextureGuid,
                    hash: "9FD61B8CAD32B3774742218E047CC5263A8A2104970C70C976A52A36F581F7DB"),
            };
            if (Hash(Path.Combine(sourceRoot, "Material/fanbelt.mat")) !=
                "CA16FAA76E71AD33BC928FE8D0DEAEAE09C27A153261BB95B73ABFBD1FD8ADDF")
                throw new InvalidDataException("Installed belt material evidence drifted.");
            foreach (var asset in assets)
            {
                string source = Path.Combine(sourceRoot, asset.source);
                if (Hash(source) != asset.hash ||
                    !File.ReadAllText(source + ".meta").Contains("guid: " + asset.guid) ||
                    File.Exists(asset.target) && Hash(asset.target) != asset.hash)
                    throw new InvalidDataException("Installed belt source/target drift: " + asset.source);
            }
            foreach (var asset in assets)
            {
                if (!File.Exists(asset.target))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(asset.target));
                    File.Copy(Path.Combine(sourceRoot, asset.source), asset.target, false);
                    // Project-owned GUID; no donor prefab/script references are copied.
                    using var sha = SHA256.Create();
                    string guid = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                        "satsuma-belt-presentation:" + outputRoot + ":" + asset.guid))).Replace("-", "").Substring(0, 32).ToLowerInvariant();
                    // The pinned Unity 6.6 importer rejects a GUID-only texture
                    // meta as obsolete version 1. Seed its reviewed native v13 header.
                    string importer = asset.target.EndsWith(".png", StringComparison.Ordinal)
                        ? "TextureImporter:\n  serializedVersion: 13\n" : string.Empty;
                    File.WriteAllText(asset.target + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\n" + importer,
                        new UTF8Encoding(false));
                }
                AssetDatabase.ImportAsset(asset.target, ImportAssetOptions.ForceSynchronousImport);
            }
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null || mesh.bindposes.Length != 2 || mesh.boneWeights.Length != mesh.vertexCount)
                throw new InvalidDataException("Installed belt mesh lost its reviewed two-bone skinning.");
            var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (textureImporter == null) throw new InvalidDataException("Installed belt texture importer is missing.");
            if (!textureImporter.sRGBTexture || textureImporter.textureType != TextureImporterType.Default ||
                textureImporter.textureShape != TextureImporterShape.Texture2D)
            {
                textureImporter.sRGBTexture = true;
                textureImporter.textureType = TextureImporterType.Default;
                textureImporter.textureShape = TextureImporterShape.Texture2D;
                textureImporter.SaveAndReimport();
            }
            // Unity 6.6 can keep a TextureImporter for the old GUID-only meta
            // while returning no Texture2D (serializedVersion 1 is unsupported).
            // Persist native importer settings to upgrade that generated meta;
            // never let null==null masquerade as a validated texture binding.
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                EditorUtility.SetDirty(textureImporter);
                textureImporter.SaveAndReimport();
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            }
            if (texture == null) throw new InvalidDataException("Installed belt texture did not import as a Texture2D.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (repairKnownMissingTexture && outputRoot == Root && material != null &&
                material.shader != null && material.shader.name == "HDRP/Lit" &&
                AssetDatabase.AssetPathToGUID(materialPath) == "55c3a1cb13000a74e8a9b59fa206138f" &&
                material.GetTexture("_BaseColorMap") == null && material.GetTexture("_MainTex") == null &&
                material.GetColor("_BaseColor") == Color.white &&
                Mathf.Approximately(material.GetFloat("_Metallic"), .49f) &&
                Mathf.Approximately(material.GetFloat("_Smoothness"), .38f))
            {
                // Exact audited missing-map defect only. The original material
                // GUID, mesh, skin bindposes and all prefab references survive.
                material.SetTexture("_BaseColorMap", texture);
                material.SetTextureOffset("_BaseColorMap", new Vector2(0f, -1f));
                HDMaterial.ValidateMaterial(material);
                EditorUtility.SetDirty(material);
            }
            if (material == null)
            {
                Shader shader = Shader.Find("HDRP/Lit") ?? throw new InvalidDataException("HDRP/Lit unavailable.");
                material = new Material(shader) { name = "TemporaryDirectImport_SatsumaInstalledBelt" };
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseColorMap", texture);
                material.SetTextureOffset("_BaseColorMap", new Vector2(0f, -1f));
                material.SetFloat("_Metallic", .49f);
                material.SetFloat("_Smoothness", .38f);
                HDMaterial.ValidateMaterial(material);
                Directory.CreateDirectory(Path.GetDirectoryName(materialPath));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (material.shader == null || material.shader.name != "HDRP/Lit" ||
                material.GetTexture("_BaseColorMap") != texture)
                throw new InvalidDataException("Existing installed belt material has an unreviewed binding.");
            AssetDatabase.SaveAssetIfDirty(material);
            Debug.Log("SATSUMA_INSTALLED_BELT_ASSETS_OK mesh=1 bones=2 texture=1 classification=TemporaryDirectImport");
        }

        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }
}
