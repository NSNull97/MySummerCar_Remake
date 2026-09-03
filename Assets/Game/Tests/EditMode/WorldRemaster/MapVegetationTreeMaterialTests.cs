using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Editor.Vegetation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class MapVegetationTreeMaterialTests
    {
        [Test]
        public void RetainedChernobylTrees_EveryRendererMaterialHasExplicitBinding()
        {
            var approved = new HashSet<string>(
                MapVegetationMaterialBindings.ApprovedTreeSourceGuids,
                StringComparer.Ordinal);
            Assert.That(approved, Has.Count.EqualTo(16));

            var encountered = new HashSet<string>(StringComparer.Ordinal);
            foreach (string species in new[] { "Pine", "Birch", "Aspen" })
            foreach (GameObject prefab in
                     MapVegetationTreePresentation.LoadSpeciesPrefabs(species))
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
            {
                Assert.That(material, Is.Not.Null,
                    species + " prefab contains a missing material slot.");
                string path = AssetDatabase.GetAssetPath(material);
                string guid = AssetDatabase.AssetPathToGUID(path);
                Assert.That(approved, Does.Contain(guid),
                    species + " renderer bypasses the project binding: " + path);
                Assert.That(
                    MapVegetationMaterialBindings.TryGetTreePolicy(guid, out _),
                    Is.True,
                    "No tree role/palette policy for " + path);
                encountered.Add(guid);
            }

            Assert.That(encountered, Is.EquivalentTo(approved),
                "The explicit binding table contains a stale or unexercised tree material.");
        }

        [Test]
        public void GeneratedChernobylBindings_AreMatteDielectricAndLowRisk()
        {
            int legacyMetallicBarkCount = 0;
            foreach (string guid in
                     MapVegetationMaterialBindings.ApprovedTreeSourceGuids)
            {
                string sourcePath = AssetDatabase.GUIDToAssetPath(guid);
                Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
                Assert.That(source, Is.Not.Null, "Missing source material " + sourcePath);
                Assert.That(
                    MapVegetationMaterialBindings.TryGetTreePolicy(guid, out var policy),
                    Is.True);
                if (policy.Role == MapVegetationTreeMaterialRole.Bark &&
                    source.HasProperty("_Metallic") &&
                    source.GetFloat("_Metallic") > 0.9f)
                {
                    legacyMetallicBarkCount++;
                }

                string outputPath =
                    MapVegetationMaterialBindings.BindingPathForTests(guid);
                Material generated =
                    AssetDatabase.LoadAssetAtPath<Material>(outputPath);
                Assert.That(generated, Is.Not.Null,
                    "Run the tree-material authoring step; missing " + outputPath);
                Assert.That(generated.shader, Is.Not.Null);
                Assert.That(generated.shader.name, Is.EqualTo("HDRP/Lit"));
                Assert.That(ShaderUtil.ShaderHasError(generated.shader), Is.False);
                Assert.That(generated.GetFloat("_Metallic"), Is.Zero.Within(0.0001f));
                Assert.That(generated.GetTexture("_MaskMap"), Is.Null);
                Assert.That(generated.GetFloat("_ReceivesSSR"), Is.Zero.Within(0.0001f));
                Assert.That(generated.GetFloat("_CoatMask"), Is.Zero.Within(0.0001f));
                Assert.That(generated.GetFloat("_SubsurfaceMask"), Is.Zero.Within(0.0001f));
                Assert.That(generated.GetColor("_EmissiveColor").maxColorComponent,
                    Is.Zero.Within(0.0001f));
                Assert.That(generated.GetFloat("_Smoothness"),
                    Is.EqualTo(policy.Smoothness).Within(0.0001f));
                Assert.That(policy.SunFacingWhiteningRisk, Is.LessThan(0.91f),
                    policy.Species + " " + policy.Role +
                    " exceeds the deterministic sun-facing whitening budget.");

                if (policy.Role == MapVegetationTreeMaterialRole.Bark)
                {
                    Assert.That(generated.GetFloat("_MaterialID"), Is.EqualTo(1f));
                    Assert.That(generated.IsKeywordEnabled(
                        "_MATERIAL_FEATURE_SPECULAR_COLOR"), Is.False);
                    Assert.That(generated.GetFloat("_CullMode"),
                        Is.EqualTo((float)CullMode.Back));
                    Assert.That(generated.GetFloat("_DoubleSidedEnable"), Is.Zero);
                }
                else
                {
                    Assert.That(generated.GetFloat("_MaterialID"), Is.EqualTo(4f));
                    Assert.That(generated.IsKeywordEnabled(
                        "_MATERIAL_FEATURE_SPECULAR_COLOR"), Is.True);
                    Color specular = generated.GetColor("_SpecularColor");
                    Assert.That(specular.maxColorComponent,
                        Is.LessThanOrEqualTo(
                            MapVegetationTreeMaterialPolicy.FoliageSpecularF0 + 0.0001f));
                    Assert.That(generated.GetFloat("_CullMode"),
                        Is.EqualTo((float)CullMode.Off),
                        "Two-sided cards must remain visible around the tree.");
                    Assert.That(generated.GetFloat("_DoubleSidedEnable"), Is.EqualTo(1f));
                    Assert.That(generated.GetVector("_DoubleSidedConstants"),
                        Is.EqualTo(new Vector4(-1f, -1f, -1f, 0f)));
                    Color tint = generated.GetColor("_BaseColor");
                    Assert.That(tint.g, Is.GreaterThan(tint.r));
                    Assert.That(tint.g, Is.GreaterThan(tint.b));
                }
            }

            Assert.That(legacyMetallicBarkCount, Is.GreaterThanOrEqualTo(4),
                "The regression fixture must retain evidence of the exact vendor bug: " +
                "tree bark was authored as metal before project binding.");
        }

        [Test]
        public void PineSelection_AuditsBoundCopyAndLeavesVendorMaterialsUntouched()
        {
            GameObject[] pines = null;
            Assert.DoesNotThrow(() => pines =
                MapVegetationTreePresentation.LoadSpeciesPrefabs("Pine"));
            Assert.That(pines, Has.Length.EqualTo(5));
            foreach (GameObject prefab in pines)
            {
                Material[] vendorMaterials = prefab
                    .GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Distinct().ToArray();
                Assert.That(vendorMaterials, Is.Not.Empty);
                var originalShaders = vendorMaterials.ToDictionary(
                    material => material,
                    material => material.shader);
                Assert.That(vendorMaterials.All(material =>
                        AssetDatabase.GetAssetPath(material).StartsWith(
                            "Assets/Chernobyl/", StringComparison.Ordinal)),
                    Is.True,
                    "The vendor prefab itself must remain read-only/un rebound.");

                GameObject instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    MapVegetationMaterialBindings.Apply(instance);
                    MapVegetationTreePresentation.Apply(instance, prefab);
                    Material[] bound = instance
                        .GetComponentsInChildren<Renderer>(true)
                        .SelectMany(renderer => renderer.sharedMaterials)
                        .Distinct().ToArray();
                    Assert.That(bound, Is.Not.Empty);
                    Assert.That(bound.All(material =>
                            material != null && material.shader != null &&
                            material.shader.name == "HDRP/Lit"), Is.True);
                    Assert.That(bound.All(material =>
                            AssetDatabase.GetAssetPath(material).StartsWith(
                                MapVegetationRebuildOptions.GeneratedRoot +
                                "/Materials/", StringComparison.Ordinal)),
                        Is.True,
                        "Every selected Pine slot must use its project-owned binding.");
                    Assert.DoesNotThrow(() =>
                        MapVegetationTreeAcceptance.AuditPrefab(instance,
                            "Pine", AssetDatabase.GetAssetPath(prefab)));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }

                foreach (KeyValuePair<Material, Shader> pair in originalShaders)
                    Assert.That(pair.Key.shader, Is.SameAs(pair.Value),
                        "Quality validation mutated a vendor material.");
            }
        }

        [Test]
        public void ActiveTreeInventory_RejectsBadAspenAndAuditsEveryRemainingVariant()
        {
            IReadOnlyList<MapVegetationTreePresentation.RejectedTreeVariant>
                rejected =
                MapVegetationTreePresentation.RejectedVariantsForTests;
            Assert.That(rejected.Count, Is.EqualTo(1));
            MapVegetationTreePresentation.RejectedTreeVariant aspen =
                rejected[0];
            Assert.That(aspen.species, Is.EqualTo("Aspen"));
            Assert.That(aspen.prefabGuid,
                Is.EqualTo("4778a89862ad262488d9d29b49559c22"));
            Assert.That(aspen.prefabPath,
                Is.EqualTo(
                    "Assets/Chernobyl/Prefabs/Vegetation/Tree/Aspen_02.prefab"));
            Assert.That(aspen.classification,
                Is.EqualTo("RejectedForFidelity"));
            Assert.That(aspen.reason, Does.Contain("LOD0"));
            Assert.That(aspen.reason, Does.Contain("rock/log"));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(
                aspen.prefabPath), Is.Not.Null,
                "The licensed vendor asset is retained read-only as provenance.");

            GameObject[] approved = null;
            Assert.DoesNotThrow(() => approved =
                MapVegetationTreePresentation.LoadAllApprovedPrefabs(),
                "The aggregate gate must report every active candidate in one pass.");
            Assert.That(approved, Has.Length.EqualTo(18));
            Assert.That(approved.Count(prefab =>
                    MapVegetationTreePresentation.GetSpecies(prefab) == "Aspen"),
                Is.EqualTo(4));
            Assert.That(approved.Any(prefab =>
                    AssetDatabase.AssetPathToGUID(
                        AssetDatabase.GetAssetPath(prefab)) ==
                    aspen.prefabGuid), Is.False);
            GameObject deterministicA = MapVegetationTreePresentation
                .SelectPrefab("Aspen", "rejected-aspen-regression",
                    20260831, 16f);
            GameObject deterministicB = MapVegetationTreePresentation
                .SelectPrefab("Aspen", "rejected-aspen-regression",
                    20260831, 16f);
            Assert.That(deterministicA, Is.SameAs(deterministicB));
            Assert.That(MapVegetationTreePresentation.GetSpecies(
                deterministicA), Is.EqualTo("Aspen"));
            Assert.That(AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(deterministicA)),
                Is.Not.EqualTo(aspen.prefabGuid));
        }

        [Test]
        public void GeneratedAlpSpruceBindings_UseTheSameBorealDielectricContract()
        {
            Shader foliageShader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/Game/Presentation/Shaders/Vegetation/MSC_SpruceWindHDRP.shader");
            Assert.That(foliageShader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(foliageShader), Is.False);

            foreach (string path in MapVegetationAlpSpruceBindings.OutputPrefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null,
                    "Run the ALP spruce authoring step; missing " + path);
                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers, Is.Not.Empty);
                foreach (Material material in renderers
                             .SelectMany(renderer => renderer.sharedMaterials)
                             .Distinct())
                {
                    Assert.That(material, Is.Not.Null);
                    Assert.That(material.GetFloat("_Metallic"),
                        Is.Zero.Within(0.0001f));
                    Assert.That(material.GetFloat("_Smoothness"),
                        Is.LessThanOrEqualTo(0.1201f));
                    Assert.That(material.GetColor("_BaseColor").g,
                        Is.GreaterThan(material.GetColor("_BaseColor").b));
                    Assert.That(material.GetColor("_EmissiveColor").maxColorComponent,
                        Is.Zero.Within(0.0001f));
                    if (material.shader.name == "HDRP/Lit")
                    {
                        Assert.That(material.GetFloat("_ReceivesSSR"), Is.Zero);
                        Assert.That(material.GetFloat("_CullMode"),
                            Is.EqualTo((float)CullMode.Back));
                    }
                    else
                    {
                        Assert.That(material.shader, Is.SameAs(foliageShader));
                        Assert.That(material.GetFloat("_Smoothness"),
                            Is.LessThanOrEqualTo(0.0451f));
                        Assert.That(material.GetColor("_SpecularColor").maxColorComponent,
                            Is.LessThanOrEqualTo(
                                MapVegetationTreeMaterialPolicy.FoliageSpecularF0 + 0.0001f));
                        Assert.That(material.GetFloat("_CullMode"),
                            Is.EqualTo((float)CullMode.Off));
                        Assert.That(material.GetVector("_DoubleSidedConstants"),
                            Is.EqualTo(new Vector4(-1f, -1f, -1f, 0f)));
                    }
                }
            }
        }

        [Test]
        public void AlpSpruceMaterialRoles_FinalLodUsesStricterBillboardBudget()
        {
            Assert.That(MapVegetationAlpSpruceBindings.ClassifyMaterialRole(
                    "Conifer Bark", 3, 4),
                Is.EqualTo(MapVegetationTreeMaterialRole.Bark));
            Assert.That(MapVegetationAlpSpruceBindings.ClassifyMaterialRole(
                    "Needle Cards", 1, 4),
                Is.EqualTo(MapVegetationTreeMaterialRole.Foliage));
            Assert.That(MapVegetationAlpSpruceBindings.ClassifyMaterialRole(
                    "ConiferTree Atlas", 1, 4),
                Is.EqualTo(MapVegetationTreeMaterialRole.Billboard));
            Assert.That(MapVegetationAlpSpruceBindings.ClassifyMaterialRole(
                    "Needle Cards", 3, 4),
                Is.EqualTo(MapVegetationTreeMaterialRole.Billboard),
                "A final card LOD must not retain the looser near-foliage F0.");

            foreach (string path in
                     MapVegetationAlpSpruceBindings.OutputPrefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    path);
                Assert.That(prefab, Is.Not.Null,
                    "Run the ALP spruce authoring step; missing " + path);
                LODGroup group = prefab.GetComponentInChildren<LODGroup>(true);
                Assert.That(group, Is.Not.Null, path);
                LOD[] lods = group.GetLODs();
                Assert.That(lods.Length, Is.GreaterThanOrEqualTo(2));
                Material[] finalMaterials = lods[^1].renderers
                    .Where(renderer => renderer != null)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null &&
                        material.shader.name == "MSC/HDRP/Spruce Wind")
                    .Distinct().ToArray();
                Assert.That(finalMaterials, Is.Not.Empty,
                    path + " has no final billboard material.");
                foreach (Material material in finalMaterials)
                {
                    Assert.That(AssetDatabase.GetAssetPath(material),
                        Does.Contain("_Billboard.mat"));
                    Assert.That(material.GetFloat("_Metallic"),
                        Is.Zero.Within(0.0001f));
                    Assert.That(material.GetFloat("_Smoothness"),
                        Is.EqualTo(0.02f).Within(0.0001f));
                    Assert.That(material.GetColor("_SpecularColor")
                            .maxColorComponent,
                        Is.EqualTo(MapVegetationTreeMaterialPolicy
                            .BillboardSpecularF0).Within(0.0001f));
                }
            }
        }

        [Test]
        public void ApprovedTreeVariants_EveryLodSlotAndNearTrunkPassAcceptance()
        {
            int prefabCount = 0;
            int materialSlotCount = 0;
            foreach (string species in new[] { "Spruce", "Pine", "Birch", "Aspen" })
            foreach (GameObject prefab in
                     MapVegetationTreePresentation.LoadSpeciesPrefabs(species))
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    MapVegetationMaterialBindings.Apply(instance);
                    MapVegetationTreePresentation.Apply(instance, prefab);
                    string path = AssetDatabase.GetAssetPath(prefab);
                    TreeAcceptanceEvidence evidence =
                        MapVegetationTreeAcceptance.AuditPrefab(
                            instance, species, path);

                    Assert.That(evidence.sourcePrefabPath, Is.EqualTo(path));
                    Assert.That(evidence.lods, Has.Count.EqualTo(evidence.lodCount));
                    Assert.That(evidence.materialSlots.Select(slot => slot.lodIndex)
                        .Distinct(), Is.EquivalentTo(
                            Enumerable.Range(0, evidence.lodCount)),
                        path + " did not exercise every LOD material set.");
                    Assert.That(evidence.materialSlots.All(slot =>
                        !string.IsNullOrWhiteSpace(slot.materialPath)), Is.True,
                        path + " contains a transient/untracked material slot.");
                    TreeLodGeometryEvidence near = evidence.lods[0];
                    Assert.That(near.nonFlatNearGeometry, Is.True, path);
                    Assert.That(near.hasVolumetricTrunk, Is.True, path);
                    Assert.That(near.hasBarkMaterialGeometry, Is.True, path);
                    Assert.That(near.hasFoliageGeometry, Is.True, path);
                    Assert.That(near.nearProxyRejected, Is.True, path);
                    Assert.That(near.billboardTriangleCount, Is.Zero, path);
                    prefabCount++;
                    materialSlotCount += evidence.materialSlots.Count;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            Assert.That(prefabCount, Is.EqualTo(18),
                "The approved tree variant inventory changed without updating acceptance.");
            Assert.That(materialSlotCount, Is.GreaterThan(prefabCount * 2),
                "The audit did not traverse all renderer material slots.");
        }

        [Test]
        public void SpruceShader_HardCodesDielectricSpecularAndBoundedWetGloss()
        {
            const string surfacePath =
                "Assets/Game/Presentation/Shaders/Vegetation/MSC_SpruceWindSurfaceData.hlsl";
            string source = File.ReadAllText(surfacePath);
            StringAssert.Contains("MATERIALFEATUREFLAGS_LIT_SPECULAR_COLOR", source);
            StringAssert.Contains("surfaceData.metallic = 0.0;", source);
            StringAssert.Contains("surfaceData.specularColor = _SpecularColor.rgb;", source);
            StringAssert.Contains("saturate(_Smoothness + 0.08)", source);
            StringAssert.DoesNotContain("saturate(_Smoothness + 0.2)", source);
        }
    }
}
