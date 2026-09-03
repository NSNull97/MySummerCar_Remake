using System;
using System.IO;
using MSC.Items.Presentation;
using UnityEditor;
using UnityEngine;

namespace MSC.Items.Editor
{
    /// <summary>
    /// Isolated repeatable build entry for the user-supplied Expanded Shop
    /// presentation. It avoids rebuilding unrelated donor item content when
    /// only the extension evidence changed.
    /// </summary>
    public static class ExpandedShopPresentationBuildCommand
    {
        private const string MenuPath =
            "MSC/Phase 1/Items/Rebuild Exact Expanded Shop Presentation";

        [MenuItem(MenuPath)]
        public static void Build()
        {
            ItemLegacyPresentationPlan plan =
                ItemLegacyPresentationEvidence.CreatePlan();
            int bindingCount = ExpandedShopPresentationImporter
                .Build(plan.Definitions).Count;
            ExpandedShopShelfLayoutCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    ExpandedShopShelfLayoutCatalog>(
                    ExpandedShopPresentationImporter
                        .GeneratedShelfCatalogPath) ??
                throw new FileNotFoundException(
                    "Generated Expanded Shop shelf catalog is missing.",
                    ExpandedShopPresentationImporter
                        .GeneratedShelfCatalogPath);
            string failure = string.Empty;
            if (bindingCount !=
                    ExpandedShopPresentationImporter.ExpectedBindingCount ||
                !catalog.TryValidate(out failure))
            {
                throw new InvalidOperationException(
                    string.IsNullOrEmpty(failure)
                        ? "Expanded Shop presentation binding count mismatch."
                        : failure);
            }

            Debug.Log(
                "EXPANDED_SHOP_PRESENTATION_BUILD_OK " +
                $"bindings={bindingCount} " +
                $"physicalGroups={catalog.Groups.Count}");
        }
    }
}
