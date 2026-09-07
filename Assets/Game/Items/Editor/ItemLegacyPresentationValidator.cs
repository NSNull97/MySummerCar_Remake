using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MSC.Bootstrap;
using MSC.Items.Presentation;
using MSC.Vehicle.ItemsIntegration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Items.Editor
{
    public static class ItemLegacyPresentationValidator
    {
        private const string ValidateGeneratedMenu =
            "MSC/Phase 1/09B/Validate Generated Item Presentation";

        private static readonly HashSet<Type> AllowedPrefabComponents = new()
        {
            typeof(Transform),
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(BoxCollider),
            typeof(CapsuleCollider),
            typeof(Rigidbody),
            typeof(HelmetPaintPresentation),
            typeof(SpannerSetPresentationController),
            typeof(ItemContentsPresentationController),
            typeof(HingedItemCoverPresentationController),
            typeof(VehicleJackInteractionController),
        };

        [MenuItem(ValidateGeneratedMenu)]
        public static void ValidateGeneratedFromMenu()
        {
            ItemLegacyPresentationPlan plan =
                ItemLegacyPresentationEvidence.CreatePlan();
            ValidateGenerated(plan);
            ItemLegacyPresentationReport.WritePlanReport(
                plan,
                "GeneratedValidated");
            Debug.Log(
                "ITEM_PRESENTATION_VALIDATION_OK " +
                $"bindings={plan.Entries.Count + ExpandedShopPresentationImporter.ExpectedBindingCount} " +
                $"revision={plan.DonorRevision}");
        }

        internal static void ValidateGenerated(
            ItemLegacyPresentationPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            string sceneAbsolute = ItemLegacyPresentationEvidence
                .ToAbsoluteProjectPath(
                    ItemLegacyPresentationPaths.GlobalLegacyScene);
            if (!File.Exists(sceneAbsolute))
            {
                throw new FileNotFoundException(
                    "Generated legacy global scene is missing.",
                    sceneAbsolute);
            }

            EnsureNoDirtyScenes();
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
                if (providers.Length != 1)
                {
                    throw new InvalidDataException(
                        $"Expected one ignored item presentation provider, " +
                        $"found {providers.Length}.");
                }

                ValidateProvider(plan, providers[0]);
            }
            finally
            {
                if (canRestoreSetup)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
            }
        }

        private static void ValidateProvider(
            ItemLegacyPresentationPlan plan,
            ItemPresentationProvider provider)
        {
            if (!string.Equals(
                    provider.DonorRevision,
                    plan.DonorRevision,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    provider.Classification,
                    "TemporaryDirectImport",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Generated provider provenance does not match the locked " +
                    "09B plan.");
            }

            int expectedBindingCount = plan.Entries.Count +
                ExpandedShopPresentationImporter.ExpectedBindingCount;
            if (provider.Bindings.Count != expectedBindingCount)
            {
                throw new InvalidDataException(
                    $"Provider has {provider.Bindings.Count} bindings; " +
                    $"expected {expectedBindingCount}.");
            }

            LegacyServiceCatalogPresentationProvider catalogProvider =
                provider.GetComponent<
                    LegacyServiceCatalogPresentationProvider>();
            if (catalogProvider == null ||
                catalogProvider.HomePartsCover == null ||
                catalogProvider.FleetariServicesCover == null ||
                catalogProvider.HomePartsSelectionMark == null ||
                catalogProvider.HomePartsOrderFont == null)
            {
                throw new InvalidDataException(
                    "Reviewed home/Fleetari catalog presentation is missing.");
            }

            Dictionary<string, ItemLegacyPresentationPlanEntry> expected =
                plan.Entries.ToDictionary(
                    entry => BindingIdentity(
                        entry.Definition.DefinitionId,
                        entry.Source.VariantIndex),
                    StringComparer.Ordinal);
            var expectedExpanded = new Dictionary<
                string,
                ItemDefinitionRecord>(StringComparer.Ordinal);
            foreach (string definitionId in
                     ExpandedShopPresentationImporter
                         .GetExpectedDefinitionIds())
            {
                if (!plan.Definitions.TryGet(
                        definitionId,
                        out ItemDefinitionRecord definition))
                {
                    throw new InvalidDataException(
                        "Expanded Shop presentation definition is missing: " +
                        definitionId);
                }

                expectedExpanded.Add(definitionId, definition);
            }

            var replacementKeyOwners = new Dictionary<string, string>(
                StringComparer.Ordinal);
            var seenBindings = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemPresentationBinding binding in provider.Bindings)
            {
                string bindingIdentity = binding != null
                    ? BindingIdentity(
                        binding.DefinitionId,
                        binding.VariantIndex)
                    : string.Empty;
                bool replacementKeyConflict = binding != null &&
                    replacementKeyOwners.TryGetValue(
                        binding.ReplacementKey,
                        out string replacementKeyOwner) &&
                    !string.Equals(
                        replacementKeyOwner,
                        binding.DefinitionId,
                        StringComparison.Ordinal);
                if (binding == null || replacementKeyConflict ||
                    binding.VisualPrefab == null ||
                    !seenBindings.Add(bindingIdentity))
                {
                    throw new InvalidDataException(
                        "Provider contains a missing, duplicate, or unplanned " +
                        "project-owned item presentation binding.");
                }

                replacementKeyOwners[binding.ReplacementKey] =
                    binding.DefinitionId;
                if (expected.TryGetValue(
                        bindingIdentity,
                        out ItemLegacyPresentationPlanEntry planEntry))
                {
                    if (!string.Equals(
                            binding.ReplacementKey,
                            planEntry.Definition.ReplacementKey,
                            StringComparison.Ordinal) ||
                        binding.VariantIndex !=
                            planEntry.Source.VariantIndex)
                    {
                        throw new InvalidDataException(
                            "Legacy item presentation binding identity " +
                            "does not match its definition.");
                    }

                    ValidatePrefab(planEntry, binding.VisualPrefab);
                    continue;
                }

                if (!expectedExpanded.TryGetValue(
                        binding.DefinitionId,
                        out ItemDefinitionRecord expandedDefinition) ||
                    binding.VariantIndex != -1 ||
                    !string.Equals(
                        binding.ReplacementKey,
                        expandedDefinition.ReplacementKey,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Provider contains an unplanned Expanded Shop " +
                        "presentation binding: " + binding.DefinitionId);
                }

                ValidateExpandedShopPrefab(
                    binding.DefinitionId,
                    binding.VisualPrefab);
            }
        }

        private static string BindingIdentity(
            string definitionId,
            int variantIndex) =>
            (definitionId ?? string.Empty) + "\n" + variantIndex;

        private static void ValidatePrefab(
            ItemLegacyPresentationPlanEntry planEntry,
            GameObject prefab)
        {
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (!IsBelow(
                    prefabPath,
                    ItemLegacyPresentationPaths.GeneratedPrefabRoot) ||
                PrefabUtility.GetPrefabAssetType(prefab) ==
                PrefabAssetType.NotAPrefab)
            {
                throw new InvalidDataException(
                    "Item presentation binding does not reference an ignored " +
                    "generated prefab: " + prefabPath);
            }

            Component[] components = prefab.GetComponentsInChildren<Component>(
                true);
            foreach (Component component in components)
            {
                if (component == null ||
                    !AllowedPrefabComponents.Contains(component.GetType()))
                {
                    throw new InvalidDataException(
                        "Sanitized item prefab contains a forbidden component " +
                        $"'{component?.GetType().FullName ?? "MissingScript"}' " +
                        "at " + prefabPath);
                }
            }

            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(
                true);
            MeshRenderer[] renderers =
                prefab.GetComponentsInChildren<MeshRenderer>(true);
            if (filters.Length != planEntry.Geometry.Count ||
                renderers.Length != planEntry.Geometry.Count)
            {
                throw new InvalidDataException(
                    $"Sanitized prefab '{prefabPath}' has {filters.Length} " +
                    $"mesh filters and {renderers.Length} renderers; expected " +
                    planEntry.Geometry.Count + ".");
            }

            foreach (MeshFilter filter in filters)
            {
                string meshPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
                if (filter.sharedMesh == null ||
                    !IsBelow(
                        meshPath,
                        ItemLegacyPresentationPaths.GeneratedMeshRoot))
                {
                    throw new InvalidDataException(
                        "Sanitized item prefab has a mesh outside its ignored " +
                        "generated item closure: " + prefabPath);
                }
            }

            foreach (MeshRenderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    string materialPath = AssetDatabase.GetAssetPath(material);
                    if (material == null ||
                        !IsBelow(
                            materialPath,
                            ItemLegacyPresentationPaths.GeneratedMaterialRoot))
                    {
                        throw new InvalidDataException(
                            "Sanitized item prefab has a material outside its " +
                            "ignored generated item closure: " + prefabPath);
                    }
                }
            }

            HelmetPaintPresentation[] paintPresentations =
                prefab.GetComponentsInChildren<HelmetPaintPresentation>(true);
            bool isHelmet = string.Equals(
                planEntry.Definition.DefinitionId,
                "item.helmet",
                StringComparison.Ordinal);
            if (paintPresentations.Length != (isHelmet ? 1 : 0) ||
                isHelmet && paintPresentations[0].PaintSurface == null)
            {
                throw new InvalidDataException(
                    "Generated helmet paint binding is missing or appears on " +
                    "a non-paintable item: " + prefabPath);
            }

            SpannerSetPresentationController[] spannerSets =
                prefab.GetComponentsInChildren<
                    SpannerSetPresentationController>(true);
            bool isSpannerSet = string.Equals(
                planEntry.Definition.DefinitionId,
                SpannerSetPresentationController.DefinitionId,
                StringComparison.Ordinal);
            if (spannerSets.Length != (isSpannerSet ? 1 : 0) ||
                isSpannerSet &&
                (spannerSets[0].LidRenderer == null ||
                 spannerSets[0].SpannerRenderers.Count != 11 ||
                 spannerSets[0].SpannerSizes.Count != 11 ||
                 spannerSets[0].AuxiliaryToolRenderers.Count != 3 ||
                 spannerSets[0].AuxiliaryToolKinds.Count != 3 ||
                 spannerSets[0].AuxiliaryToolKinds.Contains(SpannerSetAuxiliaryToolKind.Unbound) ||
                 spannerSets[0].AuxiliaryToolKinds.Distinct().Count() != 3 ||
                 !spannerSets[0].SpannerSizes.SequenceEqual(
                     Enumerable.Range(5, 11).Select(value =>
                         value.ToString()))))
            {
                throw new InvalidDataException(
                    "Generated spanner-set binding is incomplete or appears " +
                    "on another item: " + prefabPath);
            }

            string definitionId = planEntry.Definition.DefinitionId;
            bool expectsContents = string.Equals(
                    definitionId,
                    HingedItemCoverPresentationController
                        .KiljuBucketDefinitionId,
                    StringComparison.Ordinal) ||
                string.Equals(
                    definitionId,
                    "item.sauna-bucket",
                    StringComparison.Ordinal) ||
                string.Equals(
                    definitionId,
                    HingedItemCoverPresentationController
                        .PortableGrillDefinitionId,
                    StringComparison.Ordinal);
            ItemContentsPresentationController[] contents =
                prefab.GetComponentsInChildren<
                    ItemContentsPresentationController>(true);
            if (contents.Length != (expectsContents ? 1 : 0) ||
                expectsContents &&
                (!string.Equals(
                     contents[0].ExpectedDefinitionId,
                     definitionId,
                     StringComparison.Ordinal) ||
                 contents[0].LiquidSurfaceRenderers.Count == 0))
            {
                throw new InvalidDataException(
                    "Generated container-content binding is incomplete or " +
                    "appears on another item: " + prefabPath);
            }

            bool expectsHinge = string.Equals(
                    definitionId,
                    HingedItemCoverPresentationController
                        .KiljuBucketDefinitionId,
                    StringComparison.Ordinal) ||
                string.Equals(
                    definitionId,
                    HingedItemCoverPresentationController
                        .PortableGrillDefinitionId,
                    StringComparison.Ordinal);
            HingedItemCoverPresentationController[] hinges =
                prefab.GetComponentsInChildren<
                    HingedItemCoverPresentationController>(true);
            BoxCollider[] authoredColliders =
                prefab.GetComponentsInChildren<BoxCollider>(true);
            CapsuleCollider[] authoredCapsuleColliders =
                prefab.GetComponentsInChildren<CapsuleCollider>(true);
            Rigidbody[] authoredRigidbodies =
                prefab.GetComponentsInChildren<Rigidbody>(true);
            bool expectsFloorJackPhysics = string.Equals(
                definitionId,
                VehicleJackInteractionController.FloorJackDefinitionId,
                StringComparison.Ordinal);
            int expectedAuthoredBoxColliderCount = expectsHinge
                ? 1
                : expectsFloorJackPhysics
                    ? 2
                    : 0;
            if (hinges.Length != (expectsHinge ? 1 : 0) ||
                authoredColliders.Length !=
                    expectedAuthoredBoxColliderCount ||
                authoredCapsuleColliders.Length !=
                    (expectsFloorJackPhysics ? 1 : 0) ||
                authoredRigidbodies.Length !=
                    (expectsFloorJackPhysics ? 1 : 0) ||
                expectsHinge &&
                (!string.Equals(
                     hinges[0].ExpectedDefinitionId,
                     definitionId,
                     StringComparison.Ordinal) ||
                 hinges[0].HingePivot == null ||
                 hinges[0].InteractionCollider == null ||
                 hinges[0].InteractionCollider != authoredColliders[0] ||
                 !hinges[0].InteractionCollider.isTrigger))
            {
                throw new InvalidDataException(
                    "Generated hinged-cover binding is incomplete or " +
                    "appears on another item: " + prefabPath);
            }

            bool expectsJack = string.Equals(
                    definitionId,
                    VehicleJackInteractionController.CarJackDefinitionId,
                    StringComparison.Ordinal) ||
                string.Equals(
                    definitionId,
                    VehicleJackInteractionController.FloorJackDefinitionId,
                    StringComparison.Ordinal);
            VehicleJackInteractionController[] jacks =
                prefab.GetComponentsInChildren<
                    VehicleJackInteractionController>(true);
            bool expectsGroundDrag = string.Equals(
                definitionId,
                VehicleJackInteractionController.FloorJackDefinitionId,
                StringComparison.Ordinal);
            if (jacks.Length != (expectsJack ? 1 : 0) ||
                expectsJack &&
                (!string.Equals(
                     jacks[0].DefinitionId,
                     definitionId,
                     StringComparison.Ordinal) ||
                 jacks[0].LiftHead == null ||
                 jacks[0].SupportsGroundDrag != expectsGroundDrag ||
                 expectsGroundDrag &&
                 (jacks[0].LiftHeadBody == null ||
                  !jacks[0].LiftHeadBody.isKinematic ||
                  jacks[0].LiftHeadBody != authoredRigidbodies[0] ||
                  jacks[0].LiftHeadCollider == null ||
                  jacks[0].LiftHeadCollider.isTrigger ||
                  Vector3.Distance(
                      jacks[0].LiftHeadCollider.center,
                      Vector3.zero) > 0.0001f ||
                  Vector3.Distance(
                      jacks[0].LiftHeadCollider.size,
                      new Vector3(0.1f, 0.09f, 0.1f)) > 0.0001f ||
                  !authoredCapsuleColliders[0].isTrigger ||
                  Mathf.Abs(authoredCapsuleColliders[0].radius - 0.05f) >
                      0.0001f ||
                  Mathf.Abs(authoredCapsuleColliders[0].height - 1.2f) >
                      0.0001f ||
                  authoredCapsuleColliders[0].direction != 1)))
            {
                throw new InvalidDataException(
                    "Generated jack binding is incomplete or appears on " +
                    "another item: " + prefabPath);
            }

            foreach (string dependency in AssetDatabase.GetDependencies(
                         prefabPath,
                         true))
            {
                if (IsBelow(
                        dependency,
                        "Assets/Game/LegacyImport/ReferenceOnly"))
                {
                    throw new InvalidDataException(
                        "RuntimeBaseline item prefab depends on ReferenceOnly: " +
                        dependency);
                }
            }
        }

        private static void ValidateExpandedShopPrefab(
            string definitionId,
            GameObject prefab)
        {
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (!IsBelow(
                    prefabPath,
                    ItemLegacyPresentationPaths.GeneratedPrefabRoot) ||
                !Path.GetFileNameWithoutExtension(prefabPath).StartsWith(
                    "ExpandedShopItemVisual_",
                    StringComparison.Ordinal) ||
                PrefabUtility.GetPrefabAssetType(prefab) ==
                    PrefabAssetType.NotAPrefab)
            {
                throw new InvalidDataException(
                    "Expanded Shop binding does not reference a sanitized " +
                    "generated prefab: " + definitionId);
            }

            Component[] components = prefab.GetComponentsInChildren<Component>(
                true);
            foreach (Component component in components)
            {
                if (component == null ||
                    component is not Transform &&
                    component is not MeshFilter &&
                    component is not MeshRenderer)
                {
                    throw new InvalidDataException(
                        "Expanded Shop sanitized prefab contains forbidden " +
                        $"component '{component?.GetType().FullName ?? "MissingScript"}': " +
                        prefabPath);
                }
            }

            MeshFilter[] filters =
                prefab.GetComponentsInChildren<MeshFilter>(true);
            MeshRenderer[] renderers =
                prefab.GetComponentsInChildren<MeshRenderer>(true);
            if (filters.Length == 0 || filters.Length != renderers.Length ||
                Quaternion.Angle(
                    prefab.transform.localRotation,
                    Quaternion.Euler(-90f, 0f, 0f)) > 0.1f)
            {
                throw new InvalidDataException(
                    "Expanded Shop prefab lacks its reviewed meshes or Z-up " +
                    "normalization: " + prefabPath);
            }

            foreach (MeshFilter filter in filters)
            {
                string meshPath = AssetDatabase.GetAssetPath(
                    filter.sharedMesh);
                if (filter.sharedMesh == null ||
                    !IsBelow(
                        meshPath,
                        ExpandedShopPresentationImporter.GeneratedSourceRoot +
                        "/Mesh"))
                {
                    throw new InvalidDataException(
                        "Expanded Shop prefab has a mesh outside its ignored " +
                        "source closure: " + prefabPath);
                }
            }

            foreach (MeshRenderer renderer in renderers)
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    string materialPath = AssetDatabase.GetAssetPath(material);
                    if (material == null ||
                        !IsBelow(
                            materialPath,
                            ItemLegacyPresentationPaths.GeneratedMaterialRoot) ||
                        !material.name.StartsWith(
                            "ExpandedShopMaterial_",
                            StringComparison.Ordinal) ||
                        material.shader == null ||
                        !string.Equals(
                            material.shader.name,
                            "HDRP/Lit",
                            StringComparison.Ordinal) ||
                        material.GetTexture("_BaseColorMap") == null)
                    {
                        throw new InvalidDataException(
                            "Expanded Shop prefab has an unsanitized or " +
                            "untextured material: " + prefabPath);
                    }
                }
            }

            foreach (string dependency in AssetDatabase.GetDependencies(
                         prefabPath,
                         true))
            {
                if (IsBelow(
                        dependency,
                        "Assets/Game/LegacyImport/ReferenceOnly"))
                {
                    throw new InvalidDataException(
                        "Expanded Shop runtime prefab depends on " +
                        "ReferenceOnly: " + dependency);
                }
            }
        }

        private static bool IsBelow(string path, string root) =>
            !string.IsNullOrWhiteSpace(path) &&
            (string.Equals(path, root, StringComparison.Ordinal) ||
             path.StartsWith(root + "/", StringComparison.Ordinal));

        private static void EnsureNoDirtyScenes()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty)
                {
                    throw new InvalidOperationException(
                        "Save open scenes before validating generated item " +
                        "presentation: " + scene.path);
                }
            }
        }
    }

    internal static class ItemLegacyPresentationReport
    {
        public static void WritePlanReport(
            ItemLegacyPresentationPlan plan,
            string status = "PlanValidated")
        {
            Write(
                plan,
                plan.Entries.Select(entry => new ReportRow(
                    entry,
                    string.Empty,
                    status)),
                Array.Empty<string>());
        }

        public static void WriteBuildReport(
            ItemLegacyPresentationBuildResult result)
        {
            Write(
                result.Plan,
                result.Entries.Select(entry => new ReportRow(
                    entry.PlanEntry,
                    entry.PrefabPath,
                    "GeneratedValidated")),
                result.FallbackMaterialGuids);
        }

        private static void Write(
            ItemLegacyPresentationPlan plan,
            IEnumerable<ReportRow> reportRows,
            IReadOnlyCollection<string> fallbackMaterialGuids)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            ReportRow[] rows = reportRows.ToArray();
            string markdownAbsolute = ItemLegacyPresentationEvidence
                .ToAbsoluteProjectPath(
                    ItemLegacyPresentationPaths.MarkdownReportPath);
            string csvAbsolute = ItemLegacyPresentationEvidence
                .ToAbsoluteProjectPath(
                    ItemLegacyPresentationPaths.CsvReportPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(markdownAbsolute) ??
                throw new InvalidOperationException(
                    "Item presentation report directory is unavailable."));

            var markdown = new StringBuilder();
            markdown.AppendLine("# 09B sanitized item presentation report");
            markdown.AppendLine();
            markdown.AppendLine(
                "- Classification: `TemporaryDirectImport`");
            markdown.AppendLine(
                "- Entity table SHA-256: `" +
                plan.EntityTableSha256 + "`");
            markdown.AppendLine(
                "- Donor revision: `" + plan.DonorRevision + "`");
            markdown.AppendLine(
                "- Reviewed bindings: " + rows.Length);
            markdown.AppendLine(
                "- Unique synchronized meshes: " + plan.MeshGuids.Count);
            markdown.AppendLine(
                "- Fallback material GUIDs/slots: " +
                fallbackMaterialGuids.Count);
            markdown.AppendLine();
            markdown.AppendLine(
                "Generated wrappers contain only reviewed mesh presentation " +
                "plus bounded project-owned paint, spanner-set, container " +
                "content, and hinged-cover bindings. Donor scripts, FSMs, " +
                "physics and filename-driven runtime lookup are excluded.");
            markdown.AppendLine();
            markdown.AppendLine(
                "| FeatureId | DefinitionId | Variant | Source root | Mesh nodes | Status |");
            markdown.AppendLine("|---|---|---:|---|---:|---|");
            foreach (ReportRow row in rows)
            {
                markdown.Append("| ")
                    .Append(EscapeMarkdown(row.Entry.Source.FeatureId))
                    .Append(" | ")
                    .Append(EscapeMarkdown(
                        row.Entry.Definition.DefinitionId))
                    .Append(" | ")
                    .Append(row.Entry.Source.VariantIndex)
                    .Append(" | `")
                    .Append(EscapeMarkdown(
                        row.Entry.Source.EvidencePath))
                    .Append("` | ")
                    .Append(row.Entry.Geometry.Count)
                    .Append(" | ")
                    .Append(EscapeMarkdown(row.Status))
                    .AppendLine(" |");
            }

            if (fallbackMaterialGuids.Count > 0)
            {
                markdown.AppendLine();
                markdown.AppendLine("## Temporary material fallbacks");
                markdown.AppendLine();
                foreach (string guid in fallbackMaterialGuids.OrderBy(
                             value => value,
                             StringComparer.Ordinal))
                {
                    markdown.Append("- `").Append(guid).AppendLine("`");
                }
            }

            var csv = new StringBuilder();
            csv.AppendLine(
                "FeatureId,DefinitionId,VariantIndex,ReplacementKey," +
                "SourceHierarchyRoot," +
                "SourceRootStableId,GeometryCount,PrefabPath," +
                "Classification,Status");
            foreach (ReportRow row in rows)
            {
                csv.Append(Csv(row.Entry.Source.FeatureId)).Append(',')
                    .Append(Csv(row.Entry.Definition.DefinitionId)).Append(',')
                    .Append(row.Entry.Source.VariantIndex).Append(',')
                    .Append(Csv(row.Entry.Definition.ReplacementKey)).Append(',')
                    .Append(Csv(row.Entry.Source.EvidencePath)).Append(',')
                    .Append(Csv(row.Entry.SourceRoot.StableId)).Append(',')
                    .Append(row.Entry.Geometry.Count).Append(',')
                    .Append(Csv(row.PrefabPath)).Append(',')
                    .Append("TemporaryDirectImport,")
                    .Append(Csv(row.Status)).AppendLine();
            }

            File.WriteAllText(markdownAbsolute, markdown.ToString(),
                new UTF8Encoding(false));
            File.WriteAllText(csvAbsolute, csv.ToString(),
                new UTF8Encoding(false));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static string Csv(string value)
        {
            string normalized = value ?? string.Empty;
            return "\"" + normalized.Replace("\"", "\"\"") + "\"";
        }

        private static string EscapeMarkdown(string value) =>
            (value ?? string.Empty).Replace("|", "\\|");

        private readonly struct ReportRow
        {
            public ReportRow(
                ItemLegacyPresentationPlanEntry entry,
                string prefabPath,
                string status)
            {
                Entry = entry;
                PrefabPath = prefabPath ?? string.Empty;
                Status = status ?? string.Empty;
            }

            public ItemLegacyPresentationPlanEntry Entry { get; }
            public string PrefabPath { get; }
            public string Status { get; }
        }
    }
}
