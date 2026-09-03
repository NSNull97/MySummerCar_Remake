using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MSC.Bootstrap;
using MSC.Editor.WorldTransfer;
using MSC.Items.Presentation;
using MSC.LegacyImport.Editor.Configuration;
using MSC.Vehicle.ItemsIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MSC.Items.Editor
{
    internal sealed class ItemLegacyPresentationBuildResult
    {
        public ItemLegacyPresentationBuildResult(
            ItemLegacyPresentationPlan plan,
            IReadOnlyList<ItemLegacyPresentationBuildEntry> entries,
            IReadOnlyCollection<string> fallbackMaterialGuids,
            int installedBindingCount)
        {
            Plan = plan;
            Entries = entries;
            FallbackMaterialGuids = fallbackMaterialGuids;
            InstalledBindingCount = installedBindingCount;
        }

        public ItemLegacyPresentationPlan Plan { get; }
        public IReadOnlyList<ItemLegacyPresentationBuildEntry> Entries { get; }
        public IReadOnlyCollection<string> FallbackMaterialGuids { get; }
        public int InstalledBindingCount { get; }
    }

    internal sealed class ItemLegacyPresentationBuildEntry
    {
        public ItemLegacyPresentationBuildEntry(
            ItemLegacyPresentationPlanEntry planEntry,
            string prefabPath)
        {
            PlanEntry = planEntry;
            PrefabPath = prefabPath;
        }

        public ItemLegacyPresentationPlanEntry PlanEntry { get; }
        public string PrefabPath { get; }
    }

    public static class ItemLegacyPresentationPipeline
    {
        private const string ValidatePlanMenu =
            "MSC/Phase 1/09B/Validate Sanitized Item Presentation Plan";
        private const string BuildMenu =
            "MSC/Phase 1/09B/Build Sanitized Item Presentation";
        private const string ProviderRootName =
            "__MSC_PHASE1_ITEM_PRESENTATION";
        private const string FallbackMaterialAssetName =
            "LegacyItemMaterial_Fallback.mat";
        private const string ConfigurationPath =
            "Config/DonorPaths.local.json";
        private const string HelmetDefinitionId = "item.helmet";
        private const string HelmetShellStableId =
            "0791b6e176aedef248f846715808af21";

        [MenuItem(ValidatePlanMenu)]
        public static void ValidatePlanFromMenu()
        {
            ItemLegacyPresentationPlan plan =
                ItemLegacyPresentationEvidence.CreatePlan();
            ItemLegacyPresentationReport.WritePlanReport(plan);
            Debug.Log(
                "ITEM_PRESENTATION_PLAN_OK " +
                $"bindings={plan.Entries.Count} " +
                $"meshGuids={plan.MeshGuids.Count} " +
                $"revision={plan.DonorRevision}");
        }

        [MenuItem(BuildMenu)]
        public static void BuildFromMenu()
        {
            ItemLegacyPresentationBuildResult result = Build();
            ItemLegacyPresentationValidator.ValidateGenerated(result.Plan);
            ItemLegacyPresentationReport.WriteBuildReport(result);
            Debug.Log(
                "ITEM_PRESENTATION_BUILD_OK " +
                $"bindings={result.InstalledBindingCount} " +
                $"fallbackMaterials={result.FallbackMaterialGuids.Count}");
        }

        internal static ItemLegacyPresentationBuildResult Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Item presentation generation is disabled during Play Mode.");
            }

            ItemLegacyPresentationPlan plan =
                ItemLegacyPresentationEvidence.CreatePlan();
            EnsureGeneratedFolders();

            WorldReferenceMeshSyncResult sync =
                WorldReferenceMeshLibrarySync.SynchronizeGuids(
                    plan.WorldMeshGuids);
            if (sync.MissingGuids.Count > 0 ||
                sync.ResolvedAssetCount != sync.RequestedGuidCount)
            {
                throw new InvalidDataException(
                    "Frozen item mesh synchronization is incomplete. Missing: " +
                    string.Join(", ", sync.MissingGuids));
            }

            ImportDirectDonorMeshes(plan);
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            Dictionary<string, Mesh> meshes = BuildRuntimeMeshes(
                plan.MeshGuids);
            Dictionary<string, Material> materials =
                BuildReviewedSurfaceMaterials(out Font homeOrderFont);
            var fallbackMaterialGuids = new HashSet<string>(
                StringComparer.Ordinal);
            Material fallback = GetOrCreateFallbackMaterial();
            var builtEntries = new List<ItemLegacyPresentationBuildEntry>();
            var bindings = new List<ItemPresentationBinding>();

            foreach (ItemLegacyPresentationPlanEntry entry in plan.Entries)
            {
                string prefabPath = BuildVisualPrefab(
                    entry,
                    meshes,
                    materials,
                    fallback,
                    fallbackMaterialGuids);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath) ?? throw new InvalidDataException(
                    "Generated item visual prefab could not be loaded: " +
                    prefabPath);
                var binding = new ItemPresentationBinding();
                binding.Configure(
                    entry.Definition.DefinitionId,
                    entry.Definition.ReplacementKey,
                    prefab,
                    entry.Source.VariantIndex);
                bindings.Add(binding);
                builtEntries.Add(new ItemLegacyPresentationBuildEntry(
                    entry,
                    prefabPath));
            }

            bindings.AddRange(
                ExpandedShopPresentationImporter.Build(plan.Definitions));

            AssetDatabase.SaveAssets();
            InstallProvider(
                plan.DonorRevision,
                bindings.ToArray(),
                materials,
                homeOrderFont);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            return new ItemLegacyPresentationBuildResult(
                plan,
                builtEntries,
                fallbackMaterialGuids
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                bindings.Count);
        }

        private static Dictionary<string, Mesh> BuildRuntimeMeshes(
            IEnumerable<string> meshGuids)
        {
            var result = new Dictionary<string, Mesh>(StringComparer.Ordinal);
            foreach (string meshGuid in meshGuids.OrderBy(
                         value => value,
                         StringComparer.Ordinal))
            {
                string referencePath = AssetDatabase.GUIDToAssetPath(meshGuid);
                if (!WorldReferenceMeshLibrarySync.IsBelowReferenceMeshRoot(
                        referencePath) &&
                    !IsBelowGeneratedDirectMeshRoot(referencePath))
                {
                    throw new InvalidDataException(
                        "Synchronized mesh did not resolve below the reviewed " +
                        "ReferenceOnly mesh library: " + meshGuid);
                }

                Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(referencePath) ??
                              throw new InvalidDataException(
                                  "Synchronized item mesh is not a Mesh: " +
                                  meshGuid);
                string targetPath =
                    ItemLegacyPresentationPaths.GeneratedMeshRoot +
                    "/LegacyItemMesh_" + meshGuid + ".asset";
                Mesh target = AssetDatabase.LoadAssetAtPath<Mesh>(targetPath);
                if (target == null)
                {
                    target = UnityEngine.Object.Instantiate(source);
                    target.name = "LegacyItemMesh_" + meshGuid;
                    AssetDatabase.CreateAsset(target, targetPath);
                }
                else
                {
                    EditorUtility.CopySerialized(source, target);
                    target.name = "LegacyItemMesh_" + meshGuid;
                    EditorUtility.SetDirty(target);
                }

                result.Add(meshGuid, target);
            }

            result.Add(
                ItemLegacyPresentationEvidence.BuiltInMeshGuid,
                GetOrCreateReviewedBuiltInCube());

            return result;
        }

        private static void ImportDirectDonorMeshes(
            ItemLegacyPresentationPlan plan)
        {
            ItemLegacyPresentationMeshSource[] sources = plan.Entries
                .Where(entry => entry.Source.IsDirectDonorPrefab)
                .SelectMany(entry => entry.Source.DirectMeshes)
                .GroupBy(value => value.MeshGuid, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(value => value.MeshGuid, StringComparer.Ordinal)
                .ToArray();
            foreach (ItemLegacyPresentationMeshSource source in sources)
            {
                string existing = AssetDatabase.GUIDToAssetPath(
                    source.MeshGuid);
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    continue;
                }

                string sourcePath = ItemLegacyPresentationEvidence
                    .ResolveDirectSourcePath(source.AssetRelativePath);
                string sourceMetaPath = sourcePath + ".meta";
                if (!File.Exists(sourceMetaPath))
                {
                    throw new FileNotFoundException(
                        "Direct donor mesh metadata is missing.",
                        sourceMetaPath);
                }

                string meta = File.ReadAllText(sourceMetaPath);
                if (!meta.Contains(
                        "guid: " + source.MeshGuid,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Direct donor mesh metadata GUID differs from the " +
                        "locked source: " + source.AssetRelativePath);
                }

                string destinationAssetPath =
                    ItemLegacyPresentationPaths.GeneratedDonorMeshSourceRoot +
                    "/" + Path.GetFileName(source.AssetRelativePath);
                string destinationPath = ItemLegacyPresentationEvidence
                    .ToAbsoluteProjectPath(destinationAssetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(
                    destinationPath));
                File.Copy(sourcePath, destinationPath, overwrite: true);
                File.Copy(
                    sourceMetaPath,
                    destinationPath + ".meta",
                    overwrite: true);
            }
        }

        private static bool IsBelowGeneratedDirectMeshRoot(
            string assetPath)
        {
            string prefix =
                ItemLegacyPresentationPaths.GeneratedDonorMeshSourceRoot +
                "/";
            return !string.IsNullOrWhiteSpace(assetPath) &&
                   assetPath.StartsWith(prefix, StringComparison.Ordinal);
        }

        private static Mesh GetOrCreateReviewedBuiltInCube()
        {
            string targetPath =
                ItemLegacyPresentationPaths.GeneratedMeshRoot +
                "/LegacyItemMesh_ProjectCube.asset";
            Mesh target = AssetDatabase.LoadAssetAtPath<Mesh>(targetPath);
            if (target != null)
            {
                return target;
            }

            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Mesh source = primitive.GetComponent<MeshFilter>()?.sharedMesh ??
                              throw new InvalidOperationException(
                                  "Unity built-in cube mesh is unavailable.");
                target = UnityEngine.Object.Instantiate(source);
                target.name = "LegacyItemMesh_ProjectCube";
                AssetDatabase.CreateAsset(target, targetPath);
                return target;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(primitive);
            }
        }

        private static string BuildVisualPrefab(
            ItemLegacyPresentationPlanEntry entry,
            IReadOnlyDictionary<string, Mesh> meshes,
            IDictionary<string, Material> materialCache,
            Material fallbackMaterial,
            ISet<string> fallbackMaterialGuids)
        {
            string sanitizedId = SanitizeAssetName(
                entry.Definition.DefinitionId);
            string variantSuffix = entry.Source.VariantIndex >= 0
                ? $"_variant_{entry.Source.VariantIndex:D2}"
                : string.Empty;
            string prefabPath =
                ItemLegacyPresentationPaths.GeneratedPrefabRoot +
                "/LegacyItemVisual_" + sanitizedId + variantSuffix +
                ".prefab";
            var root = new GameObject(
                "LegacyItemVisual_" + sanitizedId + variantSuffix);
            try
            {
                root.transform.localRotation =
                    entry.Source.PresentationRotation;
                var renderersByEvidence =
                    new Dictionary<ItemLegacyEvidenceRecord, Renderer>();
                foreach (ItemLegacyEvidenceRecord record in entry.Geometry)
                {
                    if (!meshes.TryGetValue(record.MeshGuid, out Mesh mesh) ||
                        mesh == null)
                    {
                        throw new InvalidDataException(
                            "Generated mesh registry lacks " + record.MeshGuid);
                    }

                    var visual = new GameObject(
                        "mesh_" + ShortStableId(record.StableId));
                    visual.transform.SetParent(root.transform, false);
                    ApplyNormalizedTransform(
                        visual.transform,
                        entry.SourceRoot,
                        record);
                    visual.AddComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
                    renderersByEvidence.Add(record, renderer);
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                    renderer.sharedMaterials = ResolveMaterials(
                        record,
                        mesh,
                        materialCache,
                        fallbackMaterial,
                        fallbackMaterialGuids);
                    if (string.Equals(
                            entry.Definition.DefinitionId,
                            HelmetDefinitionId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            record.StableId,
                            HelmetShellStableId,
                            StringComparison.Ordinal))
                    {
                        HelmetPaintPresentation paint =
                            visual.AddComponent<HelmetPaintPresentation>();
                        paint.ConfigureForAuthoring(renderer);
                    }
                }

                ConfigureSpannerSetPresentation(
                    entry,
                    root,
                    renderersByEvidence);
                ConfigureContainerContentsPresentation(
                    entry,
                    root,
                    renderersByEvidence);
                ConfigureHingedCoverPresentation(
                    entry,
                    root,
                    renderersByEvidence);
                ConfigureJackPresentation(
                    entry,
                    root,
                    renderersByEvidence);

                bool success;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out success);
                if (!success)
                {
                    throw new IOException(
                        "Unity failed to save sanitized item prefab: " +
                        prefabPath);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            return prefabPath;
        }

        private static void ConfigureJackPresentation(
            ItemLegacyPresentationPlanEntry entry,
            GameObject root,
            IReadOnlyDictionary<ItemLegacyEvidenceRecord, Renderer>
                renderersByEvidence)
        {
            string definitionId = entry.Definition.DefinitionId;
            string sourceRoot = entry.Source.SourceHierarchyRoot;
            string contactPath;
            float maximumLiftMeters;
            float liftStepMeters;
            float loweringSpeedMetersPerSecond;
            bool supportsGroundDrag;
            if (string.Equals(
                    definitionId,
                    VehicleJackInteractionController.CarJackDefinitionId,
                    StringComparison.Ordinal))
            {
                contactPath = sourceRoot +
                    "/Lift/lift/car_jack_stand";
                maximumLiftMeters = 0.48f;
                liftStepMeters = 0.04f;
                loweringSpeedMetersPerSecond = 0.24f;
                supportsGroundDrag = false;
            }
            else if (string.Equals(
                         definitionId,
                         VehicleJackInteractionController.FloorJackDefinitionId,
                         StringComparison.Ordinal))
            {
                contactPath = sourceRoot +
                    "/Lifter/lift/floor_jack_step";
                maximumLiftMeters = 0.38f;
                liftStepMeters = 0.015f;
                loweringSpeedMetersPerSecond = 0.32f;
                supportsGroundDrag = true;
            }
            else
            {
                return;
            }

            Renderer contactRenderer = FindReviewedRenderer(
                renderersByEvidence,
                contactPath);
            Transform liftHeadParent = root.transform;
            if (supportsGroundDrag)
            {
                var lifterObject = new GameObject(
                    "Project-owned floor jack lifter");
                liftHeadParent = lifterObject.transform;
                liftHeadParent.SetParent(root.transform, false);
                liftHeadParent.localPosition = new Vector3(
                    0f,
                    -0.07f,
                    -0.248f);
                liftHeadParent.localRotation = Quaternion.identity;
                liftHeadParent.localScale = Vector3.one;
            }

            var liftHeadObject = new GameObject(
                "Project-owned jack lift head");
            Transform liftHead = liftHeadObject.transform;
            liftHead.SetParent(liftHeadParent, false);
            liftHead.localPosition = supportsGroundDrag
                ? Vector3.zero
                : contactRenderer.transform.localPosition;
            liftHead.localRotation = Quaternion.identity;
            liftHead.localScale = Vector3.one;
            if (supportsGroundDrag)
            {
                // Keep the physical plate square and apply the accepted
                // playtest alignment only to presentation. This turns the
                // saddle fork toward the arm without changing contact physics.
                var saddleVisualPivotObject = new GameObject(
                    "Project-owned floor jack saddle visual pivot");
                Transform saddleVisualPivot =
                    saddleVisualPivotObject.transform;
                saddleVisualPivot.SetParent(liftHead, false);
                saddleVisualPivot.localPosition = Vector3.zero;
                saddleVisualPivot.localRotation = Quaternion.identity;
                saddleVisualPivot.localScale = Vector3.one;
                contactRenderer.transform.SetParent(
                    saddleVisualPivot,
                    true);
                saddleVisualPivot.localRotation =
                    Quaternion.Euler(0f, 90f, 0f);
            }
            else
            {
                contactRenderer.transform.SetParent(liftHead, true);
            }

            if (supportsGroundDrag)
            {
                Rigidbody liftBody = liftHeadObject.AddComponent<Rigidbody>();
                liftBody.mass = 0.0000001f;
                liftBody.useGravity = false;
                liftBody.isKinematic = true;
                liftBody.interpolation = RigidbodyInterpolation.None;
                liftBody.collisionDetectionMode =
                    CollisionDetectionMode.ContinuousSpeculative;

                BoxCollider liftCollider =
                    liftHeadObject.AddComponent<BoxCollider>();
                liftCollider.center = Vector3.zero;
                liftCollider.size = new Vector3(0.1f, 0.09f, 0.1f);
                liftCollider.isTrigger = false;
            }

            VehicleJackInteractionController controller =
                root.AddComponent<VehicleJackInteractionController>();
            controller.ConfigureForAuthoring(
                definitionId,
                liftHead,
                maximumLiftMeters,
                liftStepMeters,
                loweringSpeedMetersPerSecond,
                supportsGroundDrag);

            if (!supportsGroundDrag)
            {
                Transform lowerRight = FindReviewedRenderer(
                    renderersByEvidence,
                    sourceRoot +
                    "/LowerRight1/car_jack_lower 1").transform;
                Transform lowerLeft = FindReviewedRenderer(
                    renderersByEvidence,
                    sourceRoot +
                    "/LowerLeft1/car_jack_lower 1").transform;
                Transform upperLeft = FindReviewedRenderer(
                    renderersByEvidence,
                    sourceRoot +
                    "/Lift/lift/UpperLeft1/car_jack_upper 1").transform;
                Transform upperRight = FindReviewedRenderer(
                    renderersByEvidence,
                    sourceRoot +
                    "/Lift/lift/UpperRight1/car_jack_upper 2").transform;
                controller.ConfigureArticulationForAuthoring(
                    new[]
                    {
                        lowerRight,
                        lowerLeft,
                        upperLeft,
                        upperRight,
                    },
                    new[]
                    {
                        new Vector3(62f, 0f, 0f),
                        new Vector3(-62f, 0f, 0f),
                        new Vector3(62f, 0f, 0f),
                        new Vector3(-62f, 0f, 0f),
                    },
                    new[]
                    {
                        Vector3.up * 0.5f,
                        Vector3.up * 0.5f,
                        Vector3.up * 0.5f,
                        Vector3.up * 0.5f,
                    });
            }
            else
            {
                Renderer liftArmRenderer = FindReviewedRenderer(
                    renderersByEvidence,
                    sourceRoot +
                    "/Pivot/Move/Arm/Base/floor_jack_arm");
                Renderer movingBaseRenderer = FindReviewedRenderer(
                    renderersByEvidence,
                    sourceRoot + "/Pivot/Move/floor_jack");
                Renderer leverRenderer = FindReviewedRenderer(
                    renderersByEvidence,
                    sourceRoot +
                    "/Pivot/Move/Pivot/floor_jack_lever");

                var mechanismPivotObject = new GameObject(
                    "Project-owned floor jack mechanism pivot");
                Transform mechanismPivot = mechanismPivotObject.transform;
                mechanismPivot.SetParent(root.transform, false);
                mechanismPivot.localPosition = new Vector3(
                    0f,
                    0.009000003f,
                    -0.248000145f);
                mechanismPivot.localRotation = new Quaternion(
                    0f,
                    0.707093954f,
                    0f,
                    0.7071197f);

                var mechanismMoveObject = new GameObject(
                    "Project-owned floor jack mechanism travel");
                Transform mechanismMove = mechanismMoveObject.transform;
                mechanismMove.SetParent(mechanismPivot, false);

                var armObject = new GameObject(
                    "Project-owned floor jack arm");
                Transform arm = armObject.transform;
                arm.SetParent(mechanismMove, false);
                arm.localPosition = new Vector3(
                    -0.433298171f,
                    -0.0214604735f,
                    0.0000161416119f);
                arm.localRotation = new Quaternion(
                    -0.0466433465f,
                    -0.705553353f,
                    -0.0466416553f,
                    0.7055803f);

                BoxCollider armCollider = armObject.AddComponent<BoxCollider>();
                armCollider.center = new Vector3(0f, -0.006f, -0.16f);
                armCollider.size = new Vector3(0.128f, 0.045f, 0.368f);
                // The moving arm is presentation/query geometry. Leaving it as
                // a solid child collider makes it part of the dynamic base's
                // compound shape and can launch the jack when the arm animates
                // through a vehicle. The separate saddle collider carries load.
                armCollider.isTrigger = true;

                var armBaseObject = new GameObject(
                    "Project-owned floor jack arm base");
                Transform armBase = armBaseObject.transform;
                armBase.SetParent(arm, false);
                armBase.localPosition = new Vector3(0f, 0f, -0.2f);
                // The flattened source renderer predates the donor IK lower
                // pose. Preserving its world transform while reparenting bakes
                // a hidden -7.564 degree compensation into the mesh, so the
                // arm misses the saddle. Restore the donor leaf pose exactly.
                liftArmRenderer.transform.SetParent(armBase, false);
                liftArmRenderer.transform.localPosition =
                    new Vector3(0f, 0f, 0.2f);
                liftArmRenderer.transform.localRotation = new Quaternion(
                    0.000000145778557f,
                    0.7071066f,
                    0.707107f,
                    -0.000000476837158f);
                liftArmRenderer.transform.localScale = Vector3.one;

                var leverPivotObject = new GameObject(
                    "Project-owned floor jack lever pivot");
                Transform leverPivot = leverPivotObject.transform;
                leverPivot.SetParent(mechanismMove, false);
                leverPivot.localPosition = new Vector3(
                    -0.516298354f,
                    -0.0244604349f,
                    0.00001923361f);
                leverRenderer.transform.SetParent(leverPivot, true);
                movingBaseRenderer.transform.SetParent(mechanismMove, true);

                var handleInteractionObject = new GameObject(
                    "Project-owned floor jack handle interaction");
                Transform handleInteraction =
                    handleInteractionObject.transform;
                handleInteraction.SetParent(root.transform, false);
                handleInteraction.localPosition = new Vector3(
                    0f,
                    0.563f,
                    0.395f);
                handleInteraction.localRotation = new Quaternion(
                    0.110360853f,
                    0.0000000291476354f,
                    -0.00000000170811754f,
                    0.9938916f);
                CapsuleCollider handleCollider =
                    handleInteractionObject.AddComponent<CapsuleCollider>();
                handleCollider.radius = 0.05f;
                handleCollider.height = 1.2f;
                handleCollider.direction = 1;
                handleCollider.isTrigger = true;

                controller.ConfigureArticulationForAuthoring(
                    new[] { mechanismMove, arm },
                    new[]
                    {
                        Vector3.zero,
                        new Vector3(55.13667f, 0f, 0f),
                    },
                    new[]
                    {
                        Vector3.right / 2.7f,
                        Vector3.zero,
                    });
                controller.ConfigurePumpLeverForAuthoring(leverPivot);
            }
        }

        private static void ConfigureContainerContentsPresentation(
            ItemLegacyPresentationPlanEntry entry,
            GameObject root,
            IReadOnlyDictionary<ItemLegacyEvidenceRecord, Renderer>
                renderersByEvidence)
        {
            string definitionId = entry.Definition.DefinitionId;
            string sourceRoot = entry.Source.SourceHierarchyRoot;
            if (string.Equals(
                    definitionId,
                    HingedItemCoverPresentationController
                        .PortableGrillDefinitionId,
                    StringComparison.Ordinal))
            {
                Renderer charcoal = FindReviewedRenderer(
                    renderersByEvidence,
                    sourceRoot +
                    "/Fireplace/HiillosPivot/Pivot/HiillosMesh");
                ItemContentsPresentationController grillController =
                    root.AddComponent<ItemContentsPresentationController>();
                grillController.ConfigureForAuthoring(
                    definitionId,
                    new[] { charcoal },
                    Array.Empty<ItemScalarSurfaceBinding>());
                return;
            }

            if (string.Equals(
                    definitionId,
                    HingedItemCoverPresentationController
                        .KiljuBucketDefinitionId,
                    StringComparison.Ordinal))
            {
                Renderer water = FindReviewedRenderer(
                    renderersByEvidence,
                    sourceRoot + "/Water/Surface");
                Renderer sugar = FindReviewedRenderer(
                    renderersByEvidence,
                    sourceRoot + "/SugarSurface");
                Renderer yeast = FindReviewedRenderer(
                    renderersByEvidence,
                    sourceRoot + "/YeastSurface");
                var sugarBinding = new ItemScalarSurfaceBinding();
                sugarBinding.ConfigureForAuthoring("sugar", 0.0001f, sugar);
                var yeastBinding = new ItemScalarSurfaceBinding();
                yeastBinding.ConfigureForAuthoring("yeast", 0.0001f, yeast);
                ItemContentsPresentationController controller =
                    root.AddComponent<ItemContentsPresentationController>();
                controller.ConfigureForAuthoring(
                    definitionId,
                    new[] { water },
                    new[] { sugarBinding, yeastBinding });
                return;
            }

            if (!string.Equals(
                    definitionId,
                    "item.sauna-bucket",
                    StringComparison.Ordinal))
            {
                return;
            }

            Renderer saunaWater = FindReviewedRenderer(
                renderersByEvidence,
                sourceRoot + "/Water/Surface");
            ItemContentsPresentationController saunaController =
                root.AddComponent<ItemContentsPresentationController>();
            saunaController.ConfigureForAuthoring(
                definitionId,
                new[] { saunaWater },
                Array.Empty<ItemScalarSurfaceBinding>());
        }

        private static void ConfigureHingedCoverPresentation(
            ItemLegacyPresentationPlanEntry entry,
            GameObject root,
            IReadOnlyDictionary<ItemLegacyEvidenceRecord, Renderer>
                renderersByEvidence)
        {
            string definitionId = entry.Definition.DefinitionId;
            if (string.Equals(
                    definitionId,
                    HingedItemCoverPresentationController
                        .PortableGrillDefinitionId,
                    StringComparison.Ordinal))
            {
                string sourceRoot = entry.Source.SourceHierarchyRoot;
                Renderer cover = FindReviewedRenderer(
                    renderersByEvidence,
                    sourceRoot + "/CoverPivot/grill_cover");
                var hingeObject = new GameObject(
                    "Project-owned portable grill cover hinge");
                Transform hinge = hingeObject.transform;
                hinge.SetParent(root.transform, false);
                // Exact donor CoverPivot transform and child pose from the
                // locked GAME.unity. Only the 105 degree interaction travel
                // is provisional project-owned behavior.
                hinge.localPosition = new Vector3(
                    0.2045002f,
                    -0.000000007581667f,
                    0.063599624f);
                hinge.localRotation = Quaternion.identity;
                hinge.localScale = Vector3.one;
                cover.transform.SetParent(hinge, false);
                cover.transform.localPosition = new Vector3(
                    -0.20072365f,
                    -0.00093651755f,
                    0.07892335f);
                cover.transform.localRotation = Quaternion.identity;
                cover.transform.localScale = Vector3.one;
                BoxCollider interactionCollider =
                    hingeObject.AddComponent<BoxCollider>();
                interactionCollider.isTrigger = true;
                HingedItemCoverPresentationController controller =
                    root.AddComponent<
                        HingedItemCoverPresentationController>();
                controller.ConfigureForAuthoring(
                    definitionId,
                    "Крышка гриля",
                    hinge,
                    Quaternion.identity,
                    Vector3.up,
                    105f,
                    220f,
                    new[] { cover },
                    interactionCollider,
                    string.Empty,
                    string.Empty,
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one);
                return;
            }

            if (!string.Equals(
                    definitionId,
                    HingedItemCoverPresentationController
                        .KiljuBucketDefinitionId,
                    StringComparison.Ordinal))
            {
                return;
            }

            var bucketHingeObject = new GameObject(
                "Project-owned kilju bucket lid hinge");
            Transform bucketHinge = bucketHingeObject.transform;
            bucketHinge.SetParent(root.transform, false);
            // The donor lid is a separate physical item. The reviewed mesh
            // bounds place its underside at the bucket rim; the hinge/travel
            // are an explicit user-requested Reimplemented presentation.
            bucketHinge.localPosition = new Vector3(0.205f, 0f, 0.2226f);
            bucketHinge.localRotation = Quaternion.identity;
            bucketHinge.localScale = Vector3.one;
            BoxCollider bucketInteractionCollider =
                bucketHingeObject.AddComponent<BoxCollider>();
            bucketInteractionCollider.center = new Vector3(-0.205f, 0f, 0f);
            bucketInteractionCollider.size = new Vector3(0.44f, 0.44f, 0.05f);
            bucketInteractionCollider.isTrigger = true;
            HingedItemCoverPresentationController bucketController =
                root.AddComponent<HingedItemCoverPresentationController>();
            bucketController.ConfigureForAuthoring(
                definitionId,
                "Крышка ведра",
                bucketHinge,
                Quaternion.identity,
                Vector3.up,
                105f,
                220f,
                Array.Empty<Renderer>(),
                bucketInteractionCollider,
                HingedItemCoverPresentationController
                    .KiljuLidStableEntityId,
                HingedItemCoverPresentationController.KiljuLidDefinitionId,
                new Vector3(-0.205f, 0f, 0f),
                Quaternion.identity,
                Vector3.one);
        }

        private static Renderer FindReviewedRenderer(
            IReadOnlyDictionary<ItemLegacyEvidenceRecord, Renderer>
                renderersByEvidence,
            string hierarchyPath)
        {
            Renderer[] matches = renderersByEvidence
                .Where(pair => string.Equals(
                    pair.Key.HierarchyPath,
                    hierarchyPath,
                    StringComparison.Ordinal))
                .Select(pair => pair.Value)
                .ToArray();
            if (matches.Length != 1 || matches[0] == null)
            {
                throw new InvalidDataException(
                    "Reviewed item renderer is missing or ambiguous: " +
                    hierarchyPath);
            }

            return matches[0];
        }

        private static void ConfigureSpannerSetPresentation(
            ItemLegacyPresentationPlanEntry entry,
            GameObject root,
            IReadOnlyDictionary<ItemLegacyEvidenceRecord, Renderer>
                renderersByEvidence)
        {
            if (!string.Equals(
                    entry.Definition.DefinitionId,
                    SpannerSetPresentationController.DefinitionId,
                    StringComparison.Ordinal))
            {
                return;
            }

            string sourceRoot = entry.Source.SourceHierarchyRoot;
            Renderer lid = renderersByEvidence
                .Where(pair => string.Equals(
                    pair.Key.HierarchyPath,
                    sourceRoot + "/Pivot/top",
                    StringComparison.Ordinal))
                .Select(pair => pair.Value)
                .Single();
            string spannerPattern = "^" + Regex.Escape(sourceRoot) +
                                    @"/Tools/spanner\((\d+)\)\(Clone\)/mesh$";
            var spanners = renderersByEvidence
                .Select(pair => new
                {
                    Renderer = pair.Value,
                    Match = Regex.Match(
                        pair.Key.HierarchyPath,
                        spannerPattern,
                        RegexOptions.CultureInvariant),
                })
                .Where(value => value.Match.Success)
                .Select(value => new
                {
                    value.Renderer,
                    Size = int.Parse(
                        value.Match.Groups[1].Value,
                        CultureInfo.InvariantCulture),
                })
                .OrderBy(value => value.Size)
                .ToArray();
            if (spanners.Length != 11 || spanners[0].Size != 5 ||
                spanners[^1].Size != 15)
            {
                throw new InvalidDataException(
                    "Reviewed spanner set must contain donor sizes 5..15.");
            }

            Renderer[] auxiliaryTools = renderersByEvidence
                .Where(pair => pair.Key.HierarchyPath.StartsWith(
                                   sourceRoot + "/Tools/",
                                   StringComparison.Ordinal) &&
                               !Regex.IsMatch(
                                   pair.Key.HierarchyPath,
                                   spannerPattern,
                                   RegexOptions.CultureInvariant))
                .Select(pair => pair.Value)
                .ToArray();
            SpannerSetPresentationController controller =
                root.AddComponent<SpannerSetPresentationController>();
            controller.ConfigureForAuthoring(
                lid,
                spanners.Select(value => value.Renderer).ToArray(),
                spanners.Select(value => value.Size.ToString(
                    CultureInfo.InvariantCulture)).ToArray(),
                auxiliaryTools);
        }

        private static Material[] ResolveMaterials(
            ItemLegacyEvidenceRecord record,
            Mesh mesh,
            IDictionary<string, Material> materialCache,
            Material fallbackMaterial,
            ISet<string> fallbackMaterialGuids)
        {
            int slotCount = Math.Max(1, mesh.subMeshCount);
            var result = new Material[slotCount];
            for (int index = 0; index < slotCount; index++)
            {
                if (index >= record.MaterialGuids.Count)
                {
                    result[index] = fallbackMaterial;
                    fallbackMaterialGuids.Add("<missing-slot>");
                    continue;
                }

                string materialGuid = record.MaterialGuids[index];
                if (!materialCache.TryGetValue(
                        materialGuid,
                        out Material material))
                {
                    material = CopyReviewedMaterial(materialGuid);
                    materialCache.Add(materialGuid, material);
                }

                if (material == null)
                {
                    result[index] = fallbackMaterial;
                    fallbackMaterialGuids.Add(materialGuid);
                }
                else
                {
                    result[index] = material;
                }
            }

            return result;
        }

        private static Material CopyReviewedMaterial(string materialGuid)
        {
            string sourcePath =
                ItemLegacyPresentationPaths.ExistingWorldMaterialRoot +
                "/M06B2_" + materialGuid + ".mat";
            Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (source == null)
            {
                return null;
            }

            string targetPath =
                ItemLegacyPresentationPaths.GeneratedMaterialRoot +
                "/LegacyItemMaterial_" + materialGuid + ".mat";
            Material target = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
            if (target == null)
            {
                target = new Material(source)
                {
                    name = "LegacyItemMaterial_" + materialGuid,
                };
                AssetDatabase.CreateAsset(target, targetPath);
            }
            else
            {
                EditorUtility.CopySerialized(source, target);
                target.name = "LegacyItemMaterial_" + materialGuid;
                EditorUtility.SetDirty(target);
            }

            return target;
        }

        private static Dictionary<string, Material>
            BuildReviewedSurfaceMaterials(out Font homeOrderFont)
        {
            string manifestFile = ItemLegacyPresentationEvidence
                .ToAbsoluteProjectPath(
                    ItemLegacyPresentationPaths.SurfaceManifestPath);
            if (!File.Exists(manifestFile))
            {
                throw new FileNotFoundException(
                    "Reviewed item surface manifest is missing.",
                    manifestFile);
            }

            ItemSurfaceManifest manifest = JsonUtility.FromJson<
                ItemSurfaceManifest>(File.ReadAllText(manifestFile));
            if (manifest == null || manifest.schemaVersion != 1 ||
                !string.Equals(
                    manifest.classification,
                    "TemporaryDirectImport",
                    StringComparison.Ordinal) ||
                manifest.source == null ||
                string.IsNullOrWhiteSpace(
                    manifest.source.stagingRootRelativePath) ||
                manifest.assets == null || manifest.assets.Length == 0 ||
                manifest.materials == null || manifest.materials.Length == 0 ||
                manifest.homeOrderFont == null)
            {
                throw new InvalidDataException(
                    "Reviewed item surface manifest is invalid.");
            }

            DonorPathConfiguration configuration =
                DonorPathConfiguration.LoadFromFile(ConfigurationPath);
            string donorAssetsRoot = Path.Combine(
                configuration.DonorStagingDirectory,
                manifest.source.stagingRootRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            var pathsByRole = new Dictionary<string, string>(
                StringComparer.Ordinal);
            foreach (ItemSurfaceAssetSpec asset in manifest.assets)
            {
                if (asset == null || string.IsNullOrWhiteSpace(asset.role) ||
                    string.IsNullOrWhiteSpace(asset.relativePath) ||
                    string.IsNullOrWhiteSpace(asset.sha256) ||
                    string.IsNullOrWhiteSpace(asset.sourceGuid) ||
                    string.IsNullOrWhiteSpace(asset.generatedGuid) ||
                    !pathsByRole.TryAdd(
                        asset.role,
                        ItemLegacyPresentationPaths.GeneratedTextureRoot +
                        "/" + Path.GetFileName(asset.relativePath)))
                {
                    throw new InvalidDataException(
                        "Reviewed item surface asset identity is invalid.");
                }

                string source = Path.Combine(
                    donorAssetsRoot,
                    asset.relativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar));
                RequireHash(source, asset.sha256, asset.role);
                CopyAssetAndMeta(
                    source,
                    pathsByRole[asset.role],
                    asset.sourceGuid,
                    asset.generatedGuid);
            }

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            foreach (ItemSurfaceAssetSpec asset in manifest.assets)
            {
                ConfigureSurfaceTexture(
                    pathsByRole[asset.role],
                    asset.normalMap,
                    asset.clamp,
                    asset.alphaIsTransparency);
            }

            Shader shader = Shader.Find("HDRP/Lit") ??
                throw new InvalidOperationException(
                    "HDRP/Lit shader is unavailable for reviewed item surfaces.");
            var result = new Dictionary<string, Material>(
                StringComparer.Ordinal);
            foreach (ItemSurfaceMaterialSpec spec in manifest.materials)
            {
                if (spec == null ||
                    string.IsNullOrWhiteSpace(spec.sourceMaterialGuid) ||
                    string.IsNullOrWhiteSpace(spec.albedoRole) ||
                    !pathsByRole.TryGetValue(
                        spec.albedoRole,
                        out string albedoPath) ||
                    (!string.IsNullOrWhiteSpace(spec.normalRole) &&
                     !pathsByRole.ContainsKey(spec.normalRole)) ||
                    !result.TryAdd(spec.sourceMaterialGuid, null))
                {
                    throw new InvalidDataException(
                        "Reviewed item surface material identity is invalid.");
                }

                string targetPath =
                    ItemLegacyPresentationPaths.GeneratedMaterialRoot +
                    "/LegacyItemMaterial_" + spec.sourceMaterialGuid + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(
                    targetPath);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, targetPath);
                }

                material.name =
                    "LegacyItemMaterial_" + spec.sourceMaterialGuid;
                material.shader = shader;
                material.enableInstancing = true;
                material.SetTexture(
                    "_BaseColorMap",
                    AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath));
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Metallic", Mathf.Clamp01(spec.metallic));
                material.SetFloat(
                    "_Smoothness",
                    Mathf.Clamp01(spec.smoothness));
                ConfigureSurfaceMode(material, spec.transparent);
                if (string.IsNullOrWhiteSpace(spec.normalRole))
                {
                    material.SetTexture("_NormalMap", null);
                    material.DisableKeyword("_NORMALMAP_TANGENT_SPACE");
                }
                else
                {
                    material.SetTexture(
                        "_NormalMap",
                        AssetDatabase.LoadAssetAtPath<Texture2D>(
                            pathsByRole[spec.normalRole]));
                    material.SetFloat("_NormalScale", spec.normalScale);
                    material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
                }

                EditorUtility.SetDirty(material);
                result[spec.sourceMaterialGuid] = material;
            }

            homeOrderFont = ImportReviewedFont(
                donorAssetsRoot,
                manifest.homeOrderFont);
            return result;
        }

        private static void ConfigureSurfaceTexture(
            string assetPath,
            bool normalMap,
            bool clamp,
            bool alphaIsTransparency)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as
                TextureImporter ?? throw new InvalidOperationException(
                    "Reviewed item texture importer is unavailable: " +
                    assetPath);
            importer.textureType = normalMap
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.wrapMode = clamp
                ? TextureWrapMode.Clamp
                : TextureWrapMode.Repeat;
            importer.alphaIsTransparency = alphaIsTransparency;
            importer.textureCompression =
                TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void ConfigureSurfaceMode(
            Material material,
            bool transparent)
        {
            material.SetFloat("_SurfaceType", 0f);
            material.SetFloat("_ZWrite", 1f);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (transparent)
            {
                material.SetFloat("_AlphaCutoffEnable", 1f);
                material.SetFloat("_AlphaCutoff", 0.1f);
                material.SetFloat("_DoubleSidedEnable", 1f);
                material.SetFloat("_CullMode", 0f);
                material.doubleSidedGI = true;
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
                material.EnableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_DOUBLESIDED_ON");
            }
            else
            {
                material.SetFloat("_AlphaCutoffEnable", 0f);
                material.SetFloat("_DoubleSidedEnable", 0f);
                material.SetFloat("_CullMode", 2f);
                material.doubleSidedGI = false;
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = -1;
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_DOUBLESIDED_ON");
            }
        }

        private static Font ImportReviewedFont(
            string donorAssetsRoot,
            ItemSurfaceFontSpec spec)
        {
            if (spec == null || !spec.IsComplete)
            {
                throw new InvalidDataException(
                    "Reviewed home order font identity is invalid.");
            }

            string sourceFont = Path.Combine(
                donorAssetsRoot,
                spec.fontRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            string sourceMaterial = Path.Combine(
                donorAssetsRoot,
                spec.materialRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            string sourceTexture = Path.Combine(
                donorAssetsRoot,
                spec.textureRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            RequireHash(sourceFont, spec.fontSha256, "parts-magazine.order-font");
            RequireHash(
                sourceMaterial,
                spec.materialSha256,
                "parts-magazine.order-font-material");
            RequireHash(
                sourceTexture,
                spec.textureSha256,
                "parts-magazine.order-font-texture");

            string targetFont = ItemLegacyPresentationPaths.GeneratedFontRoot +
                                "/" + Path.GetFileName(spec.fontRelativePath);
            string targetMaterial = ItemLegacyPresentationPaths
                                        .GeneratedNativeMaterialRoot +
                                    "/" +
                                    Path.GetFileName(spec.materialRelativePath);
            string targetTexture = ItemLegacyPresentationPaths
                                       .GeneratedTextureRoot +
                                   "/" +
                                   Path.GetFileName(spec.textureRelativePath);
            CopyAssetAndMeta(
                sourceTexture,
                targetTexture,
                spec.textureSourceGuid,
                spec.textureGeneratedGuid);
            CopyAssetAndMeta(
                sourceMaterial,
                targetMaterial,
                spec.materialSourceGuid,
                spec.materialGeneratedGuid);
            CopyAssetAndMeta(
                sourceFont,
                targetFont,
                spec.fontSourceGuid,
                spec.fontGeneratedGuid);
            ReplaceGeneratedGuidReferences(
                targetMaterial,
                (spec.textureSourceGuid, spec.textureGeneratedGuid));
            ReplaceGeneratedGuidReferences(
                targetFont,
                (spec.materialSourceGuid, spec.materialGeneratedGuid),
                (spec.textureSourceGuid, spec.textureGeneratedGuid));

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Font>(targetFont) ??
                   throw new InvalidDataException(
                       "Reviewed home order font could not be loaded: " +
                       targetFont);
        }

        private static void ReplaceGeneratedGuidReferences(
            string destinationAssetPath,
            params (string Source, string Generated)[] replacements)
        {
            string destination = ItemLegacyPresentationEvidence
                .ToAbsoluteProjectPath(destinationAssetPath);
            string contents = File.ReadAllText(destination);
            foreach ((string source, string generated) in replacements)
            {
                contents = contents.Replace(
                    "guid: " + source,
                    "guid: " + generated,
                    StringComparison.Ordinal);
            }

            File.WriteAllText(
                destination,
                contents,
                new UTF8Encoding(false));
        }

        private static void CopyAssetAndMeta(
            string source,
            string destinationAssetPath,
            string sourceGuid,
            string generatedGuid)
        {
            if (!File.Exists(source) || !File.Exists(source + ".meta"))
            {
                throw new FileNotFoundException(
                    "Locked donor item surface or metadata is missing.",
                    source);
            }

            string destination = ItemLegacyPresentationEvidence
                .ToAbsoluteProjectPath(destinationAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, overwrite: true);
            string meta = File.ReadAllText(source + ".meta");
            string sourceLine = "guid: " + sourceGuid;
            if (!meta.Contains(sourceLine, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Donor metadata for '{source}' lacks locked GUID {sourceGuid}.");
            }

            File.WriteAllText(
                destination + ".meta",
                meta.Replace(
                    sourceLine,
                    "guid: " + generatedGuid,
                    StringComparison.Ordinal),
                new UTF8Encoding(false));
        }

        private static void RequireHash(
            string path,
            string expected,
            string label)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Locked donor item surface '{label}' is missing.",
                    path);
            }

            using FileStream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            string actual = string.Concat(sha.ComputeHash(stream).Select(
                value => value.ToString("x2")));
            if (!string.Equals(
                    actual,
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Locked item surface '{label}' hash mismatch. " +
                    $"Expected {expected}, got {actual}.");
            }
        }

        private static Material GetOrCreateFallbackMaterial()
        {
            string path =
                ItemLegacyPresentationPaths.GeneratedMaterialRoot + "/" +
                FallbackMaterialAssetName;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("HDRP/Lit");
                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "HDRP/Lit shader is unavailable for the sanitized " +
                        "item presentation fallback material.");
                }

                material = new Material(shader)
                {
                    name = "LegacyItemMaterial_Fallback",
                };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", new Color(0.32f, 0.34f, 0.36f, 1f));
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.18f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ApplyNormalizedTransform(
            Transform target,
            ItemLegacyEvidenceRecord sourceRoot,
            ItemLegacyEvidenceRecord sourceGeometry)
        {
            Matrix4x4 rootMatrix = Matrix4x4.TRS(
                sourceRoot.SourcePosition,
                sourceRoot.SourceRotation,
                sourceRoot.SourceScale);
            Matrix4x4 geometryMatrix = Matrix4x4.TRS(
                sourceGeometry.SourcePosition,
                sourceGeometry.SourceRotation,
                sourceGeometry.SourceScale);
            Matrix4x4 relative = rootMatrix.inverse * geometryMatrix;
            target.localPosition = relative.GetColumn(3);
            target.localRotation = relative.rotation;
            target.localScale = relative.lossyScale;
        }

        private static void InstallProvider(
            string donorRevision,
            ItemPresentationBinding[] bindings,
            IReadOnlyDictionary<string, Material> reviewedMaterials,
            Font homeOrderFont)
        {
            EnsureNoDirtyScenes();
            if (!File.Exists(ItemLegacyPresentationEvidence.ToAbsoluteProjectPath(
                    ItemLegacyPresentationPaths.GlobalLegacyScene)))
            {
                throw new FileNotFoundException(
                    "Ignored World_Global_Legacy scene is unavailable. Build " +
                    "the accepted world baseline before item presentation.",
                    ItemLegacyPresentationPaths.GlobalLegacyScene);
            }

            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            bool canRestoreSetup = setup.Any(entry =>
                entry.isLoaded && entry.isActive);
            try
            {
                Scene scene = EditorSceneManager.OpenScene(
                    ItemLegacyPresentationPaths.GlobalLegacyScene,
                    OpenSceneMode.Single);
                ItemPresentationProvider[] providers = scene
                    .GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<
                        ItemPresentationProvider>(true))
                    .ToArray();
                if (providers.Length > 1)
                {
                    throw new InvalidDataException(
                        "World_Global_Legacy contains multiple item " +
                        "presentation providers.");
                }

                ItemPresentationProvider provider;
                if (providers.Length == 1)
                {
                    provider = providers[0];
                }
                else
                {
                    var providerRoot = new GameObject(ProviderRootName);
                    provider = providerRoot.AddComponent<
                        ItemPresentationProvider>();
                }

                provider.Configure(donorRevision, bindings);
                Material homeCatalogMaterial = RequireReviewedMaterial(
                    reviewedMaterials,
                    "e97e1bb757f448c42ba6febd9093d97b");
                Material fleetariCatalogMaterial = RequireReviewedMaterial(
                    reviewedMaterials,
                    "ab474223bc939c949abeb2dd2282c31e");
                Material[] homePages =
                {
                    RequireReviewedMaterial(reviewedMaterials, "e68a1ff1e69b08146b1bba0d34759b5d"),
                    RequireReviewedMaterial(reviewedMaterials, "dee048d02c564064aaa30a3c00f608d9"),
                    RequireReviewedMaterial(reviewedMaterials, "ab38e828c7985334598eb0338158c5b4"),
                    RequireReviewedMaterial(reviewedMaterials, "f909787131e71cc41b82e81933788652"),
                    RequireReviewedMaterial(reviewedMaterials, "2c4e800a883a4cf4fa61599870311790"),
                    RequireReviewedMaterial(reviewedMaterials, "adb9f4747417c34419bd10a516e01601"),
                    RequireReviewedMaterial(reviewedMaterials, "8142a30cdfa068b43877a5e392f183c5"),
                    RequireReviewedMaterial(reviewedMaterials, "0367950d11c1aad4397de3d749dcee8d"),
                };
                Material homeOrder = RequireReviewedMaterial(
                    reviewedMaterials,
                    "1b94212a6192d75498f7eeb067cf128e");
                Material homeSelectionMark = RequireReviewedMaterial(
                    reviewedMaterials,
                    "1365f5837af5d3e4c9aff55dfc1fb1d3");
                Material[] fleetariPages =
                {
                    RequireReviewedMaterial(reviewedMaterials, "f802287c31806ba4ab513afc17fb6ac2"),
                    RequireReviewedMaterial(reviewedMaterials, "6607163ad205bf44f80e6e25dbcdc24e"),
                    RequireReviewedMaterial(reviewedMaterials, "3b17378927266e443bd7052a4c5a953f"),
                    RequireReviewedMaterial(reviewedMaterials, "98cc377ed41acfc4ebea28ddd1609788"),
                    RequireReviewedMaterial(reviewedMaterials, "daae3bb0264fb0044b3f4c7cca017cee"),
                    RequireReviewedMaterial(reviewedMaterials, "63e559f856bafbf458126e90ca53d083"),
                };
                Material fleetariOrder = RequireReviewedMaterial(
                    reviewedMaterials,
                    "bc6139561a896d54782f54521399b566");

                LegacyServiceCatalogPresentationProvider catalogProvider =
                    provider.GetComponent<
                        LegacyServiceCatalogPresentationProvider>() ??
                    provider.gameObject.AddComponent<
                        LegacyServiceCatalogPresentationProvider>();
                catalogProvider.ConfigureForAuthoring(
                    homeCatalogMaterial,
                    fleetariCatalogMaterial,
                    homePages,
                    homeOrder,
                    fleetariPages,
                    fleetariOrder,
                    homeSelectionMark,
                    homeOrderFont);
                EditorUtility.SetDirty(provider);
                EditorUtility.SetDirty(catalogProvider);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(
                        scene,
                        ItemLegacyPresentationPaths.GlobalLegacyScene))
                {
                    throw new IOException(
                        "Failed to save item provider into ignored global " +
                        "legacy scene.");
                }
            }
            finally
            {
                if (canRestoreSetup)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
            }
        }

        private static Material RequireReviewedMaterial(
            IReadOnlyDictionary<string, Material> reviewedMaterials,
            string sourceMaterialGuid)
        {
            if (!reviewedMaterials.TryGetValue(
                    sourceMaterialGuid,
                    out Material material) ||
                material == null)
            {
                throw new InvalidDataException(
                    "Reviewed service catalog material is missing: " +
                    sourceMaterialGuid);
            }

            return material;
        }

        private static void EnsureNoDirtyScenes()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty)
                {
                    throw new InvalidOperationException(
                        "Save open scenes before installing the generated item " +
                        "presentation provider: " + scene.path);
                }
            }
        }

        private static void EnsureGeneratedFolders()
        {
            EnsureAssetFolder(ItemLegacyPresentationPaths.GeneratedRoot);
            EnsureAssetFolder(ItemLegacyPresentationPaths.GeneratedMeshRoot);
            EnsureAssetFolder(ItemLegacyPresentationPaths.GeneratedMaterialRoot);
            EnsureAssetFolder(ItemLegacyPresentationPaths.GeneratedPrefabRoot);
            EnsureAssetFolder(ItemLegacyPresentationPaths.GeneratedSourceRoot);
            EnsureAssetFolder(ItemLegacyPresentationPaths.GeneratedTextureRoot);
            EnsureAssetFolder(ItemLegacyPresentationPaths.GeneratedFontRoot);
            EnsureAssetFolder(
                ItemLegacyPresentationPaths.GeneratedNativeMaterialRoot);
            EnsureAssetFolder(
                ItemLegacyPresentationPaths.GeneratedDonorMeshSourceRoot);
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string[] parts = assetPath.Split('/');
            if (parts.Length == 0 ||
                !string.Equals(parts[0], "Assets", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Generated item folder must remain under Assets: " +
                    assetPath);
            }

            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static string SanitizeAssetName(string value)
        {
            char[] characters = value.Select(character =>
                    char.IsLetterOrDigit(character) ||
                    character is '-' or '_'
                        ? character
                        : '_')
                .ToArray();
            return new string(characters);
        }

        private static string ShortStableId(string stableId) =>
            !string.IsNullOrEmpty(stableId) && stableId.Length > 12
                ? stableId.Substring(0, 12)
                : stableId ?? "missing";

        [Serializable]
        private sealed class ItemSurfaceManifest
        {
            public int schemaVersion;
            public string manifestId;
            public string classification;
            public ItemSurfaceSourceSpec source;
            public ItemSurfaceAssetSpec[] assets;
            public ItemSurfaceMaterialSpec[] materials;
            public ItemSurfaceFontSpec homeOrderFont;
        }

        [Serializable]
        private sealed class ItemSurfaceSourceSpec
        {
            public string stagingRootRelativePath;
        }

        [Serializable]
        private sealed class ItemSurfaceAssetSpec
        {
            public string role;
            public string relativePath;
            public string sha256;
            public string sourceGuid;
            public string generatedGuid;
            public bool normalMap;
            public bool clamp;
            public bool alphaIsTransparency;
        }

        [Serializable]
        private sealed class ItemSurfaceMaterialSpec
        {
            public string sourceMaterialGuid;
            public string albedoRole;
            public string normalRole;
            public float metallic;
            public float smoothness;
            public float normalScale = 1f;
            public bool transparent;
        }

        [Serializable]
        private sealed class ItemSurfaceFontSpec
        {
            public string fontRelativePath;
            public string fontSha256;
            public string fontSourceGuid;
            public string fontGeneratedGuid;
            public string materialRelativePath;
            public string materialSha256;
            public string materialSourceGuid;
            public string materialGeneratedGuid;
            public string textureRelativePath;
            public string textureSha256;
            public string textureSourceGuid;
            public string textureGeneratedGuid;

            public bool IsComplete =>
                !string.IsNullOrWhiteSpace(fontRelativePath) &&
                !string.IsNullOrWhiteSpace(fontSha256) &&
                !string.IsNullOrWhiteSpace(fontSourceGuid) &&
                !string.IsNullOrWhiteSpace(fontGeneratedGuid) &&
                !string.IsNullOrWhiteSpace(materialRelativePath) &&
                !string.IsNullOrWhiteSpace(materialSha256) &&
                !string.IsNullOrWhiteSpace(materialSourceGuid) &&
                !string.IsNullOrWhiteSpace(materialGeneratedGuid) &&
                !string.IsNullOrWhiteSpace(textureRelativePath) &&
                !string.IsNullOrWhiteSpace(textureSha256) &&
                !string.IsNullOrWhiteSpace(textureSourceGuid) &&
                !string.IsNullOrWhiteSpace(textureGeneratedGuid);
        }
    }
}
