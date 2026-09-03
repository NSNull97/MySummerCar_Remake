using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace MSC.Editor.Vegetation
{
    /// <summary>
    /// Builds project-owned HDRP wrapper prefabs around the reviewed local ALP
    /// spruce subset. Source package assets stay untouched and removable.
    /// </summary>
    public static class MapVegetationAlpSpruceBindings
    {
        public const string SourceRoot =
            "Assets/Game/Presentation/Vegetation/ThirdParty/ALPSpruceTreesPack/Source";
        public const string OutputRoot =
            MapVegetationRebuildOptions.GeneratedRoot + "/Art/Trees/ALPSpruce";
        public const string ReportPath =
            "Artifacts/VegetationRebuild/ALPSpruce/provenance.json";
        public const string PackageSha256 =
            "39d47d1a7a8424a3c1f39dd55dd64ed6cee42542a13466cc04fb40280126b827";

        private const string SourceTreeRoot =
            SourceRoot + "/_Models/OptimizedVegetation/";
        private const string SpruceShaderPath =
            "Assets/Game/Presentation/Shaders/Vegetation/MSC_SpruceWindHDRP.shader";
        private const string BillboardAtlasPath =
            SourceRoot + "/Textures/ConiferTreeBig01_B_Atlas.tif";

        private static readonly string[] VariantNames =
        {
            "ConiferTreeBig01_Optimized",
            "ConiferTreeBig02_Optimized",
            "ConiferTreeBig04_Optimized",
            "ConiferTreeSmall03_Optimized",
            "ConiferTreeSmall04_Optimized"
        };

        public static string[] SourcePrefabPaths => VariantNames
            .Select(name => SourceTreeRoot + name + ".prefab").ToArray();

        public static string[] OutputPrefabPaths => VariantNames
            .Select(name => OutputRoot + "/Prefabs/MapSpruce_" + name + ".prefab")
            .ToArray();

        public static GameObject[] BuildPrefabs()
        {
            Shader barkShader = Shader.Find("HDRP/Lit");
            Shader foliageShader = AssetDatabase.LoadAssetAtPath<Shader>(SpruceShaderPath);
            if (barkShader == null) throw new InvalidOperationException("HDRP/Lit is unavailable.");
            if (foliageShader == null || foliageShader.name != "MSC/HDRP/Spruce Wind")
                throw new InvalidOperationException("Project spruce wind shader is unavailable.");

            EnsureFolder(OutputRoot + "/Prefabs");
            EnsureFolder(OutputRoot + "/Materials");
            var report = new BindingReport();
            var results = new List<GameObject>(VariantNames.Length);

            for (int index = 0; index < VariantNames.Length; index++)
            {
                string sourcePath = SourcePrefabPaths[index];
                string outputPath = OutputPrefabPaths[index];
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                if (source == null) throw new FileNotFoundException(
                    "Reviewed ALP spruce source is missing. Run the selective package importer first.", sourcePath);

                GameObject generated = BuildOne(source, sourcePath, outputPath,
                    barkShader, foliageShader, report);
                results.Add(generated);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            foreach (PrefabReport prefab in report.prefabs)
            {
                prefab.outputGuid = AssetDatabase.AssetPathToGUID(prefab.outputPath);
                prefab.outputSha256 = HashFile(prefab.outputPath);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            Debug.Log("MAP_ALP_SPRUCE_BINDINGS_OK prefabs=" + results.Count + " report=" + ReportPath);
            return results.ToArray();
        }

        private static GameObject BuildOne(
            GameObject source,
            string sourcePath,
            string outputPath,
            Shader barkShader,
            Shader foliageShader,
            BindingReport report)
        {
            GameObject root = new GameObject("MapSpruce_" + source.name);
            GameObject visual = null;
            try
            {
                visual = PrefabUtility.InstantiatePrefab(source) as GameObject;
                if (visual == null) throw new InvalidOperationException("Could not instantiate " + sourcePath);
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
                visual.name = "Visuals";
                visual.transform.SetParent(root.transform, false);

                LODGroup[] groups = visual.GetComponentsInChildren<LODGroup>(true);
                if (groups.Length != 1) throw new InvalidDataException(
                    "Reviewed ALP spruce must contain exactly one LODGroup: " + sourcePath);
                LODGroup authoredGroup = groups[0];
                LOD[] authoredLods = authoredGroup.GetLODs();
                if (authoredLods.Length != 4 && authoredLods.Length != 5)
                    throw new InvalidDataException("Unexpected ALP spruce LOD count: " + sourcePath);

                Vector3 referenceWorld = authoredGroup.transform.TransformPoint(authoredGroup.localReferencePoint);
                float referenceSize = authoredGroup.size * MaximumAxis(authoredGroup.transform.lossyScale);
                var entry = new PrefabReport
                {
                    sourcePath = sourcePath,
                    sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath),
                    sourceSha256 = HashFile(sourcePath),
                    dependencyHash = AssetDatabase.GetAssetDependencyHash(sourcePath).ToString(),
                    outputPath = outputPath,
                    sourceLodCount = authoredLods.Length,
                    sourceRootScale = visual.transform.localScale,
                    sourceReferenceSize = referenceSize
                };

                var generatedLods = new LOD[authoredLods.Length];
                float[] thresholds = Thresholds(authoredLods.Length);
                for (int lodIndex = 0; lodIndex < authoredLods.Length; lodIndex++)
                {
                    Renderer[] renderers = authoredLods[lodIndex].renderers;
                    if (renderers == null || renderers.Length == 0 || renderers.Any(renderer => renderer == null))
                        throw new InvalidDataException("ALP spruce LOD has missing renderers: " + sourcePath);
                    foreach (Renderer renderer in renderers)
                    {
                        Material[] materials = renderer.sharedMaterials;
                        if (materials.Length == 0 || materials.Any(material => material == null))
                            throw new InvalidDataException("ALP spruce renderer has missing materials: " + sourcePath);
                        for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                        {
                            Material sourceMaterial = materials[materialIndex];
                            MapVegetationTreeMaterialRole role =
                                ClassifyMaterialRole(sourceMaterial.name,
                                    lodIndex, authoredLods.Length);
                            bool bark = role == MapVegetationTreeMaterialRole.Bark;
                            string roleSuffix = role ==
                                MapVegetationTreeMaterialRole.Bark
                                ? "_Bark.mat"
                                : role == MapVegetationTreeMaterialRole.Billboard
                                    ? "_Billboard.mat"
                                    : "_Foliage.mat";
                            string materialPath = OutputRoot + "/Materials/" + source.name +
                                "_LOD" + lodIndex + "_M" + materialIndex +
                                roleSuffix;
                            materials[materialIndex] = BuildMaterial(sourceMaterial,
                                materialPath, bark ? barkShader : foliageShader,
                                role);
                            entry.materials.Add(new MaterialReport
                            {
                                lod = lodIndex,
                                slot = materialIndex,
                                classification = role.ToString(),
                                sourceName = sourceMaterial.name,
                                outputPath = materialPath
                            });
                        }
                        renderer.sharedMaterials = materials;
                        renderer.shadowCastingMode = lodIndex <= 1
                            ? ShadowCastingMode.On : ShadowCastingMode.Off;
                        renderer.receiveShadows = lodIndex <= 2;
                        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                        renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                        renderer.motionVectorGenerationMode = lodIndex <= 1
                            ? MotionVectorGenerationMode.Object
                            : MotionVectorGenerationMode.ForceNoMotion;
                    }
                    generatedLods[lodIndex] = new LOD(thresholds[lodIndex], renderers)
                    {
                        fadeTransitionWidth = 0.12f
                    };
                }

                foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);
                foreach (Component component in visual.GetComponentsInChildren<Component>(true))
                    if (component != null && component.GetType().FullName == "UnityEngine.Tree")
                        Object.DestroyImmediate(component);
                Object.DestroyImmediate(authoredGroup);

                LODGroup group = root.AddComponent<LODGroup>();
                group.localReferencePoint = root.transform.InverseTransformPoint(referenceWorld);
                group.size = referenceSize;
                group.fadeMode = LODFadeMode.CrossFade;
                group.animateCrossFading = false;
                group.SetLODs(generatedLods);
                group.RecalculateBounds();

                Bounds bounds = RendererBounds(root);
                entry.outputHeightMeters = bounds.size.y;
                entry.outputBounds = bounds;
                entry.lodThresholds = thresholds;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, outputPath);
                if (saved == null) throw new IOException("Could not save generated spruce prefab: " + outputPath);
                report.prefabs.Add(entry);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Material BuildMaterial(Material source, string path,
            Shader shader, MapVegetationTreeMaterialRole role)
        {
            bool bark = role == MapVegetationTreeMaterialRole.Bark;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.shaderKeywords = Array.Empty<string>();
            material.name = Path.GetFileNameWithoutExtension(path);
            material.enableInstancing = true;

            string albedoProperty = FindTextureProperty(source,
                "_MainTex", "_BaseColorMap", "_BaseMap");
            Texture albedo = string.IsNullOrEmpty(albedoProperty) ? null : source.GetTexture(albedoProperty);
            // Big01 ships with one blank, already-converted billboard material,
            // while its two sibling billboard materials reference this same
            // authored atlas. Bind the package texture explicitly so Big01's
            // far LOD cannot turn into an untextured white card.
            if (albedo == null && source.name.StartsWith(
                    "ConiferTreeBig01_B_Atlas", StringComparison.Ordinal))
                albedo = AssetDatabase.LoadAssetAtPath<Texture>(BillboardAtlasPath);
            if (albedo == null) throw new InvalidDataException(
                "ALP spruce material has no albedo texture: " + source.name);
            string normalProperty = FindTextureProperty(source,
                "_BumpSpecMap", "_NormalMap", "_BumpMap");
            Texture normal = string.IsNullOrEmpty(normalProperty) ? null : source.GetTexture(normalProperty);
            Color tint = source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            tint.a = 1f;
            MapVegetationTreeMaterialPolicy policy =
                MapVegetationTreeMaterialPolicy.Create("Spruce", role, tint);
            float cutoff = source.HasProperty("_Cutoff")
                ? Mathf.Clamp(source.GetFloat("_Cutoff"), 0.05f, 0.95f) : 0.45f;

            material.SetTexture("_BaseColorMap", albedo);
            if (!string.IsNullOrEmpty(albedoProperty))
            {
                material.SetTextureScale("_BaseColorMap", source.GetTextureScale(albedoProperty));
                material.SetTextureOffset("_BaseColorMap", source.GetTextureOffset(albedoProperty));
            }
            else
            {
                material.SetTextureScale("_BaseColorMap", Vector2.one);
                material.SetTextureOffset("_BaseColorMap", Vector2.zero);
            }
            material.SetColor("_BaseColor", policy.Tint);
            material.SetTexture("_NormalMap", normal);
            if (normal != null)
            {
                material.SetTextureScale("_NormalMap", source.GetTextureScale(normalProperty));
                material.SetTextureOffset("_NormalMap", source.GetTextureOffset(normalProperty));
            }
            SetFloat(material, "_NormalScale", normal == null ? 0f : policy.NormalScale);
            SetFloat(material, "_Metallic", 0f);
            SetFloat(material, "_MetallicRemapMin", 0f);
            SetFloat(material, "_MetallicRemapMax", 0f);
            SetFloat(material, "_Smoothness", policy.Smoothness);
            SetFloat(material, "_SurfaceType", 0f);
            SetFloat(material, "_ReceivesSSR", 0f);
            SetFloat(material, "_ReceivesSSRTransparent", 0f);
            SetFloat(material, "_TransmissionEnable", 0f);
            SetFloat(material, "_TransmissionMask", 0f);
            SetFloat(material, "_CoatMask", 0f);
            SetFloat(material, "_SubsurfaceMask", 0f);
            SetColor(material, "_SpecularColor", new Color(
                policy.SpecularF0,
                policy.SpecularF0,
                policy.SpecularF0,
                1f));
            if (material.HasProperty("_MaskMap")) material.SetTexture("_MaskMap", null);
            if (material.HasProperty("_CoatMaskMap")) material.SetTexture("_CoatMaskMap", null);
            if (material.HasProperty("_TransmissionMaskMap"))
                material.SetTexture("_TransmissionMaskMap", null);
            SetColor(material, "_EmissiveColor", Color.black);
            SetColor(material, "_EmissionColor", Color.black);

            if (bark)
            {
                SetFloat(material, "_AlphaCutoffEnable", 0f);
                SetFloat(material, "_DoubleSidedEnable", 0f);
                SetFloat(material, "_CullMode", (float)CullMode.Back);
                SetFloat(material, "_CullModeForward", (float)CullMode.Back);
                material.doubleSidedGI = false;
                material.renderQueue = (int)RenderQueue.Geometry;
                if (!HDMaterial.ValidateMaterial(material))
                    throw new InvalidDataException("HDRP rejected generated ALP bark material: " + path);
            }
            else
            {
                SetFloat(material, "_AlphaCutoffEnable", 1f);
                SetFloat(material, "_AlphaCutoff", cutoff);
                SetFloat(material, "_DoubleSidedEnable", 1f);
                SetVector(material, "_DoubleSidedConstants", new Vector4(-1f, -1f, -1f, 0f));
                SetFloat(material, "_CullMode", (float)CullMode.Off);
                SetFloat(material, "_CullModeForward", (float)CullMode.Off);
                SetFloat(material, "_ZTestDepthEqualForOpaque", (float)CompareFunction.LessEqual);
                SetFloat(material, "_WindStrengthMeters", 0.30f);
                SetFloat(material, "_WindSpeed", 1.0f);
                SetFloat(material, "_WindFrequency", 0.026f);
                SetFloat(material, "_WindBaseHeightMeters", 0.7f);
                SetFloat(material, "_WindHeightMeters", 16f);
                SetFloat(material, "_MinimumWindIntensity", 0.035f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_DOUBLESIDED_ON");
                material.doubleSidedGI = true;
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        internal static float[] Thresholds(int lodCount)
        {
            if (lodCount == 5) return new[] { 0.22f, 0.10f, 0.045f, 0.015f, 0.0025f };
            if (lodCount == 4) return new[] { 0.22f, 0.08f, 0.025f, 0.004f };
            throw new ArgumentOutOfRangeException(nameof(lodCount), "ALP spruce requires four or five LODs.");
        }

        internal static MapVegetationTreeMaterialRole ClassifyMaterialRole(
            string materialName, int lodIndex, int lodCount)
        {
            if (string.IsNullOrWhiteSpace(materialName))
                throw new ArgumentException("Material name is required.",
                    nameof(materialName));
            if (lodCount < 2 || lodIndex < 0 || lodIndex >= lodCount)
                throw new ArgumentOutOfRangeException(nameof(lodIndex));
            if (materialName.IndexOf("bark",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                materialName.IndexOf("trunk",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return MapVegetationTreeMaterialRole.Bark;
            if (materialName.IndexOf("atlas",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                lodIndex == lodCount - 1)
                return MapVegetationTreeMaterialRole.Billboard;
            return MapVegetationTreeMaterialRole.Foliage;
        }

        private static string FindTextureProperty(Material material, params string[] names)
        {
            foreach (string name in names)
                if (material.HasProperty(name) && material.GetTexture(name) != null) return name;
            return string.Empty;
        }

        private static float MaximumAxis(Vector3 scale) =>
            Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

        private static Bounds RendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidDataException("Generated spruce has no renderers.");
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void SetVector(Material material, string property, Vector4 value)
        {
            if (material.HasProperty(property)) material.SetVector(property, value);
        }

        private static string HashFile(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) return string.Empty;
            using var stream = File.OpenRead(assetPath);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("Invalid folder: " + path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [Serializable]
        private sealed class BindingReport
        {
            public string generator = "msc.map-alp-spruce-bindings.v3";
            public string classification = "ThirdPartyPrivatePhase1Presentation";
            public bool productionReady;
            public string productionBlocker =
                "Purchase receipt or other licence evidence is not stored with the supplied archive.";
            public string sourcePackage = "Spruce Trees Pack v1.1";
            public string packageSha256 = PackageSha256;
            public string exclusions =
                "Small02 has unresolved textures; Big03 is a 58 m accent shape; Small01 duplicates a weak silhouette; vendor groups, scripts, sky, wind and demos are excluded.";
            public string materialCalibration =
                "Project-owned matte spruce palette; metallic0; low-F0 foliage/billboard specular; no SSR, transmission, coat or emission; HDRP Flip normals on double-sided foliage. Vendor materials remain unchanged.";
            public List<PrefabReport> prefabs = new List<PrefabReport>();
        }

        [Serializable]
        private sealed class PrefabReport
        {
            public string sourcePath, sourceGuid, sourceSha256, dependencyHash;
            public string outputPath, outputGuid, outputSha256;
            public int sourceLodCount;
            public Vector3 sourceRootScale;
            public float sourceReferenceSize, outputHeightMeters;
            public Bounds outputBounds;
            public float[] lodThresholds;
            public List<MaterialReport> materials = new List<MaterialReport>();
        }

        [Serializable]
        private sealed class MaterialReport
        {
            public int lod, slot;
            public string classification, sourceName, outputPath;
        }
    }
}
