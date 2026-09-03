using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Editor.Vegetation
{
    /// <summary>
    /// Explicit compatibility bindings on generated instances. Vendor assets
    /// remain read-only; all corrected HDRP materials live under the generated
    /// project-owned vegetation root.
    /// </summary>
    public static class MapVegetationMaterialBindings
    {
        private const float HdrpMaterialIdStandard = 1f;
        private const float HdrpMaterialIdSpecularColor = 4f;

        private static readonly SourceBindingSpec[] TreeBindingSpecs =
        {
            // Retained licensed Chernobyl pine presentation.
            Tree("207b92193dbd37d40bc438f9fe6e6365", "Pine", MapVegetationTreeMaterialRole.Foliage),
            Tree("637841121d2b67c458a8228dab13e87d", "Pine", MapVegetationTreeMaterialRole.Bark),
            Tree("ff458f8099508b746a3263f7212eb9f7", "Pine", MapVegetationTreeMaterialRole.Billboard),
            Tree("3ebf0d4ef6f0fda4591d517dce58ff74", "Pine", MapVegetationTreeMaterialRole.Billboard),
            Tree("25b73025770b840458ae37beac317390", "Pine", MapVegetationTreeMaterialRole.Billboard),

            // Retained licensed Chernobyl birch presentation.
            Tree("8076283726d85ba4aa3a53c50f3c2edf", "Birch", MapVegetationTreeMaterialRole.Foliage),
            Tree("af8881d5d953eab4b83718790551845b", "Birch", MapVegetationTreeMaterialRole.Bark),
            Tree("5a705fc62719c0d4c84aa45a0054f6f0", "Birch", MapVegetationTreeMaterialRole.Billboard),
            Tree("bf6e7978cb7abfc49ae019f5384cf379", "Birch", MapVegetationTreeMaterialRole.Billboard),

            // Retained licensed Chernobyl aspen presentation. Aspen_03 uses the
            // package's Maple_01_Bark material as an authored bark submesh.
            Tree("69b034c9a316fa84facd8de48bbca18d", "Aspen", MapVegetationTreeMaterialRole.Foliage),
            Tree("121a8f6a497e89d4fb2ec65bda8fef65", "Aspen", MapVegetationTreeMaterialRole.Bark),
            Tree("6470e42e3bab9ca45a9536081a4df712", "Aspen", MapVegetationTreeMaterialRole.Bark),
            Tree("4f2d97f4f555bbf48a66f3f49927b685", "Aspen", MapVegetationTreeMaterialRole.Bark),
            Tree("bc856ca852666114bb2a11f773fbf5c4", "Aspen", MapVegetationTreeMaterialRole.Bark),
            Tree("74a23dfc9056b244687da1fa834c6506", "Aspen", MapVegetationTreeMaterialRole.Billboard),
            Tree("fb9f8627aa0d0a543937d7e860ef32a1", "Aspen", MapVegetationTreeMaterialRole.Billboard)
        };

        // These two pre-existing compatibility bindings are intentionally left
        // on their established material policy. Grass/undergrowth is outside
        // this tree-lighting correction.
        private static readonly SourceBindingSpec[] UnderstoryBindingSpecs =
        {
            Understory("aa9c2f3b59cd2324bbbc5acb5f9becb3"), // Grass_01 used by fern prefabs
            Understory("53a8a91ebed514e4ba744e54c237edb0")  // Bushes_01
        };

        private static readonly SourceBindingSpec[] BindingSpecs =
            TreeBindingSpecs.Concat(UnderstoryBindingSpecs).ToArray();
        private static readonly Dictionary<string, SourceBindingSpec> SpecsByGuid =
            BindingSpecs.ToDictionary(spec => spec.Guid, StringComparer.Ordinal);
        private static readonly Dictionary<Material, Material> CachedBindings =
            new Dictionary<Material, Material>();
        private static readonly HashSet<Material> UnchangedMaterials =
            new HashSet<Material>();
        private static string MaterialRoot =>
            MapVegetationRebuildOptions.GeneratedRoot + "/Materials";

        public static Material[] BuildMaterials()
        {
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "HDRP/Lit is unavailable; vegetation materials were not prepared.");
            }

            EnsureFolder(MaterialRoot);
            var result = new Material[BindingSpecs.Length];
            var provenance = new BindingReport();
            CachedBindings.Clear();
            UnchangedMaterials.Clear();

            for (int index = 0; index < BindingSpecs.Length; index++)
            {
                SourceBindingSpec spec = BindingSpecs[index];
                string sourcePath = AssetDatabase.GUIDToAssetPath(spec.Guid);
                Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
                if (source == null)
                {
                    throw new FileNotFoundException(
                        "Approved Chernobyl source material is missing: " + spec.Guid,
                        sourcePath);
                }

                string albedoProperty = FindTextureProperty(
                    source,
                    "_Base_Color",
                    "_BaseColorMap",
                    "_MainTex");
                Texture albedo = string.IsNullOrEmpty(albedoProperty)
                    ? null
                    : source.GetTexture(albedoProperty);
                if (albedo == null)
                {
                    throw new InvalidDataException(
                        "Approved vegetation material has no active albedo texture: " +
                        sourcePath);
                }

                string normalProperty = FindTextureProperty(
                    source,
                    "_Normal",
                    "_NormalMap",
                    "_BumpMap");
                Texture normal = string.IsNullOrEmpty(normalProperty)
                    ? null
                    : source.GetTexture(normalProperty);
                Color sourceTint = ResolveSourceTint(source);
                float sourceMetallic = GetFloat(source, "_Metallic", 0f);
                float sourceSmoothness = GetFloat(source, "_Smoothness", 0f);

                string path = BindingPath(spec.Guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader)
                    {
                        name = source.name + " Map Vegetation HDRP"
                    };
                    AssetDatabase.CreateAsset(material, path);
                }

                material.shader = shader;
                material.shaderKeywords = Array.Empty<string>();
                material.enableInstancing = true;
                material.SetTexture("_BaseColorMap", albedo);
                material.SetTextureScale(
                    "_BaseColorMap",
                    source.GetTextureScale(albedoProperty));
                material.SetTextureOffset(
                    "_BaseColorMap",
                    source.GetTextureOffset(albedoProperty));
                material.SetTexture("_NormalMap", normal);
                if (normal != null)
                {
                    material.SetTextureScale(
                        "_NormalMap",
                        source.GetTextureScale(normalProperty));
                    material.SetTextureOffset(
                        "_NormalMap",
                        source.GetTextureOffset(normalProperty));
                }

                float cutoff = ResolveAlphaCutoff(source);
                MapVegetationTreeMaterialPolicy treePolicy = default;
                if (spec.IsTree)
                {
                    treePolicy = MapVegetationTreeMaterialPolicy.Create(
                        spec.Species,
                        spec.Role,
                        sourceTint);
                    ConfigureTreeMaterial(
                        material,
                        treePolicy,
                        normal != null,
                        cutoff);
                }
                else
                {
                    ConfigureEstablishedUnderstoryMaterial(
                        material,
                        source,
                        sourceTint,
                        normal != null,
                        cutoff);
                }

                if (!HDMaterial.ValidateMaterial(material))
                {
                    throw new InvalidDataException(
                        "HDRP rejected generated material: " + path);
                }
                EditorUtility.SetDirty(material);
                result[index] = material;
                CachedBindings[source] = material;
                provenance.bindings.Add(new BindingEvidence
                {
                    sourceMaterialGuid = spec.Guid,
                    sourceMaterialPath = sourcePath,
                    sourceMaterialSha256 = HashAsset(sourcePath),
                    sourceShader = source.shader != null
                        ? source.shader.name
                        : "missing",
                    species = spec.IsTree ? spec.Species : "Understory",
                    role = spec.IsTree ? spec.Role.ToString() : "EstablishedCompatibility",
                    sourceMetallic = sourceMetallic,
                    sourceSmoothness = sourceSmoothness,
                    generatedMaterialPath = path,
                    albedoPath = AssetDatabase.GetAssetPath(albedo),
                    normalPath = AssetDatabase.GetAssetPath(normal),
                    alphaCutoff = cutoff,
                    generatedTint = material.GetColor("_BaseColor"),
                    generatedMetallic = material.GetFloat("_Metallic"),
                    generatedSmoothness = material.GetFloat("_Smoothness"),
                    generatedReceivesSsr = material.GetFloat("_ReceivesSSR"),
                    generatedSpecularF0 = spec.IsTree && treePolicy.UsesSpecularColor
                        ? treePolicy.SpecularF0
                        : 0.04f,
                    sunFacingWhiteningRisk = spec.IsTree
                        ? treePolicy.SunFacingWhiteningRisk
                        : 0f
                });
            }

            AssetDatabase.SaveAssets();
            foreach (BindingEvidence evidence in provenance.bindings)
            {
                evidence.generatedMaterialGuid =
                    AssetDatabase.AssetPathToGUID(evidence.generatedMaterialPath);
                evidence.generatedMaterialSha256 =
                    HashAsset(evidence.generatedMaterialPath);
                evidence.albedoSha256 = HashAsset(evidence.albedoPath);
                evidence.normalSha256 = HashAsset(evidence.normalPath);
            }

            const string reportDirectory = "Artifacts/VegetationRebuild";
            Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(
                reportDirectory + "/material-bindings-provenance.json",
                JsonUtility.ToJson(provenance, true));
            return result;
        }

        public static void Apply(GameObject instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }
            foreach (Renderer renderer in
                     instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int index = 0; index < materials.Length; index++)
                {
                    Material source = materials[index];
                    if (source == null || UnchangedMaterials.Contains(source))
                    {
                        continue;
                    }
                    if (!CachedBindings.TryGetValue(source, out Material replacement) ||
                        replacement == null)
                    {
                        string guid = AssetDatabase.AssetPathToGUID(
                            AssetDatabase.GetAssetPath(source));
                        if (!SpecsByGuid.ContainsKey(guid))
                        {
                            if (source.shader != null &&
                                source.shader.name == "AE/Leaves")
                            {
                                throw new InvalidOperationException(
                                    "Unreviewed AE/Leaves material requires an " +
                                    "explicit HDRP binding: " +
                                    AssetDatabase.GetAssetPath(source));
                            }
                            UnchangedMaterials.Add(source);
                            continue;
                        }
                        replacement = AssetDatabase.LoadAssetAtPath<Material>(
                            BindingPath(guid));
                        if (replacement == null || replacement.shader == null ||
                            replacement.shader.name != "HDRP/Lit")
                        {
                            throw new InvalidOperationException(
                                "Prepare approved vegetation art before generation; " +
                                "material binding is missing for " + source.name);
                        }
                        CachedBindings[source] = replacement;
                    }
                    materials[index] = replacement;
                    changed = true;
                }
                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        internal static string[] ApprovedTreeSourceGuids =>
            TreeBindingSpecs.Select(spec => spec.Guid).ToArray();

        internal static bool TryGetTreePolicy(
            string sourceGuid,
            out MapVegetationTreeMaterialPolicy policy)
        {
            policy = default;
            if (!SpecsByGuid.TryGetValue(sourceGuid, out SourceBindingSpec spec) ||
                !spec.IsTree)
            {
                return false;
            }
            Material source = AssetDatabase.LoadAssetAtPath<Material>(
                AssetDatabase.GUIDToAssetPath(sourceGuid));
            if (source == null)
            {
                return false;
            }
            policy = MapVegetationTreeMaterialPolicy.Create(
                spec.Species,
                spec.Role,
                ResolveSourceTint(source));
            return true;
        }

        internal static string BindingPathForTests(string guid) =>
            BindingPath(guid);

        private static void ConfigureTreeMaterial(
            Material material,
            MapVegetationTreeMaterialPolicy policy,
            bool hasNormal,
            float cutoff)
        {
            material.SetColor("_BaseColor", policy.Tint);
            material.SetFloat("_NormalScale", hasNormal ? policy.NormalScale : 0f);
            material.SetFloat("_SurfaceType", 0f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_MetallicRemapMin", 0f);
            material.SetFloat("_MetallicRemapMax", 0f);
            material.SetFloat("_Smoothness", policy.Smoothness);
            material.SetTexture("_MaskMap", null);
            material.SetFloat("_CoatMask", 0f);
            material.SetTexture("_CoatMaskMap", null);
            material.SetFloat("_TransmissionEnable", 0f);
            material.SetFloat("_TransmissionMask", 0f);
            material.SetTexture("_TransmissionMaskMap", null);
            material.SetFloat("_SubsurfaceMask", 0f);
            material.SetFloat("_ReceivesSSR", 0f);
            material.SetFloat("_ReceivesSSRTransparent", 0f);
            material.SetFloat("_EnableGeometricSpecularAA", 1f);
            material.SetFloat("_SpecularAAScreenSpaceVariance", 0.1f);
            material.SetFloat("_SpecularAAThreshold", 0.2f);
            material.SetFloat("_SpecularHighlights", 1f);
            material.SetColor("_EmissiveColor", Color.black);
            material.SetColor("_EmissionColor", Color.black);

            if (policy.UsesSpecularColor)
            {
                material.SetFloat("_MaterialID", HdrpMaterialIdSpecularColor);
                material.SetColor(
                    "_SpecularColor",
                    new Color(
                        policy.SpecularF0,
                        policy.SpecularF0,
                        policy.SpecularF0,
                        1f));
            }
            else
            {
                material.SetFloat("_MaterialID", HdrpMaterialIdStandard);
                material.SetColor("_SpecularColor", Color.white);
            }

            material.SetFloat("_AlphaCutoffEnable", policy.AlphaClipped ? 1f : 0f);
            material.SetFloat("_AlphaCutoff", cutoff);
            material.SetFloat("_AlphaCutoffShadow", cutoff);
            material.SetFloat("_UseShadowThreshold", policy.AlphaClipped ? 1f : 0f);
            material.SetFloat("_DoubleSidedEnable", policy.DoubleSided ? 1f : 0f);
            // HDRP Flip mode gives the back of thin foliage a real opposite normal.
            // The previous Mirror mode made sun-facing card backs share highlights.
            material.SetFloat("_DoubleSidedNormalMode", 0f);
            material.SetVector(
                "_DoubleSidedConstants",
                policy.DoubleSided
                    ? new Vector4(-1f, -1f, -1f, 0f)
                    : new Vector4(1f, 1f, -1f, 0f));
            material.SetFloat(
                "_CullMode",
                policy.DoubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            material.SetFloat(
                "_CullModeForward",
                policy.DoubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            material.doubleSidedGI = policy.DoubleSided;
            material.renderQueue = policy.AlphaClipped
                ? (int)RenderQueue.AlphaTest
                : (int)RenderQueue.Geometry;
        }

        private static void ConfigureEstablishedUnderstoryMaterial(
            Material material,
            Material source,
            Color sourceTint,
            bool hasNormal,
            float cutoff)
        {
            sourceTint.a = 1f;
            material.SetColor("_BaseColor", sourceTint);
            material.SetFloat(
                "_NormalScale",
                hasNormal && source.HasProperty("_Normal_Power")
                    ? Mathf.Clamp(source.GetFloat("_Normal_Power"), 0f, 2f)
                    : hasNormal ? 1f : 0f);
            material.SetFloat("_SurfaceType", 0f);
            material.SetFloat("_AlphaCutoffEnable", 1f);
            material.SetFloat("_AlphaCutoff", cutoff);
            material.SetFloat("_AlphaCutoffShadow", cutoff);
            material.SetFloat("_UseShadowThreshold", 1f);
            material.SetFloat("_DoubleSidedEnable", 1f);
            material.SetFloat("_DoubleSidedNormalMode", 1f);
            material.SetVector(
                "_DoubleSidedConstants",
                new Vector4(1f, 1f, -1f, 0f));
            material.SetFloat("_CullMode", (float)CullMode.Off);
            material.SetFloat("_CullModeForward", (float)CullMode.Off);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.25f);
            material.SetTexture("_MaskMap", null);
            material.SetColor("_EmissiveColor", Color.black);
            material.doubleSidedGI = true;
            material.renderQueue = (int)RenderQueue.AlphaTest;
        }

        private static Color ResolveSourceTint(Material source)
        {
            Color tint;
            if (source.shader != null && source.shader.name == "AE/Leaves" &&
                source.HasProperty("_Color"))
            {
                tint = source.GetColor("_Color");
            }
            else if (source.HasProperty("_BaseColor"))
            {
                tint = source.GetColor("_BaseColor");
            }
            else if (source.HasProperty("_Color"))
            {
                tint = source.GetColor("_Color");
            }
            else
            {
                tint = Color.white;
            }
            tint.a = 1f;
            return tint;
        }

        private static float ResolveAlphaCutoff(Material source)
        {
            if (source.HasProperty("_AlphaCutoff"))
            {
                return Mathf.Clamp(source.GetFloat("_AlphaCutoff"), 0.05f, 0.95f);
            }
            if (source.HasProperty("_Cutoff"))
            {
                return Mathf.Clamp(source.GetFloat("_Cutoff"), 0.05f, 0.95f);
            }
            return 0.5f;
        }

        private static string FindTextureProperty(
            Material material,
            params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name) && material.GetTexture(name) != null)
                {
                    return name;
                }
            }
            return string.Empty;
        }

        private static float GetFloat(
            Material material,
            string property,
            float fallback) =>
            material.HasProperty(property)
                ? material.GetFloat(property)
                : fallback;

        private static SourceBindingSpec Tree(
            string guid,
            string species,
            MapVegetationTreeMaterialRole role) =>
            new SourceBindingSpec(guid, species, role, true);

        private static SourceBindingSpec Understory(string guid) =>
            new SourceBindingSpec(
                guid,
                string.Empty,
                MapVegetationTreeMaterialRole.Foliage,
                false);

        private static string BindingPath(string guid) =>
            MaterialRoot + "/Chernobyl_" + guid + "_HDRP.mat";

        private static string HashAsset(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return string.Empty;
            }
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        [Serializable]
        private sealed class BindingReport
        {
            public string generator = "msc.map-vegetation-material-bindings.v3";
            public string classification = "ReauthoredMaterial";
            public string source =
                "Existing licensed Chernobyl package, productId221608; not original My Summer Car art.";
            public string conversion =
                "All active retained pine/birch/aspen foliage, bark and billboard slots receive project-owned HDRP/Lit bindings. Legacy bark metallic=1/mask packing is rejected; tree outputs are metallic=0, no SSR/transmission/coat/emission, matte role-specific smoothness, low-F0 specular foliage, corrected double-sided normals and restrained boreal-summer tints. Existing grass/bush compatibility bindings retain their previous policy.";
            public List<BindingEvidence> bindings = new List<BindingEvidence>();
        }

        [Serializable]
        private sealed class BindingEvidence
        {
            public string sourceMaterialGuid;
            public string sourceMaterialPath;
            public string sourceMaterialSha256;
            public string sourceShader;
            public string species;
            public string role;
            public string generatedMaterialGuid;
            public string generatedMaterialPath;
            public string generatedMaterialSha256;
            public string albedoPath;
            public string albedoSha256;
            public string normalPath;
            public string normalSha256;
            public float alphaCutoff;
            public float sourceMetallic;
            public float sourceSmoothness;
            public Color generatedTint;
            public float generatedMetallic;
            public float generatedSmoothness;
            public float generatedReceivesSsr;
            public float generatedSpecularF0;
            public float sunFacingWhiteningRisk;
        }

        private readonly struct SourceBindingSpec
        {
            public SourceBindingSpec(
                string guid,
                string species,
                MapVegetationTreeMaterialRole role,
                bool isTree)
            {
                Guid = guid;
                Species = species;
                Role = role;
                IsTree = isTree;
            }

            public string Guid { get; }
            public string Species { get; }
            public MapVegetationTreeMaterialRole Role { get; }
            public bool IsTree { get; }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent))
            {
                throw new InvalidOperationException(
                    "Invalid owned material folder: " + path);
            }
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
