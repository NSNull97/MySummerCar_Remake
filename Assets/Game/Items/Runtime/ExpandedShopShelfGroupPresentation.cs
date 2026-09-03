using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Items.Presentation
{
    /// <summary>
    /// Identifies the exact visual stock units inside one sanitized generated
    /// shelf-group prefab. It contains no purchasing or inventory authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExpandedShopShelfGroupPresentation : MonoBehaviour
    {
        [SerializeField] private GameObject[] stockUnits =
            Array.Empty<GameObject>();
        [SerializeField] private Renderer[] outlineRenderers =
            Array.Empty<Renderer>();

        public IReadOnlyList<GameObject> StockUnits => stockUnits;
        public IReadOnlyList<Renderer> OutlineRenderers => outlineRenderers;

        public bool TryValidate(out string failure)
        {
            failure = string.Empty;
            if (stockUnits == null || outlineRenderers == null)
            {
                failure = "Expanded Shop shelf presentation arrays are null.";
                return false;
            }

            var unitSet = new HashSet<GameObject>();
            for (int index = 0; index < stockUnits.Length; index++)
            {
                GameObject unit = stockUnits[index];
                if (unit == null || !unit.transform.IsChildOf(transform) ||
                    !unitSet.Add(unit))
                {
                    failure =
                        "Expanded Shop shelf presentation contains an invalid " +
                        "or duplicate stock unit.";
                    return false;
                }
            }

            for (int index = 0; index < outlineRenderers.Length; index++)
            {
                Renderer renderer = outlineRenderers[index];
                if (renderer == null ||
                    !renderer.transform.IsChildOf(transform))
                {
                    failure =
                        "Expanded Shop shelf presentation contains an invalid " +
                        "outline renderer.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        public void ConfigureForAuthoring(
            GameObject[] configuredStockUnits,
            Renderer[] configuredOutlineRenderers)
        {
            stockUnits = configuredStockUnits ?? Array.Empty<GameObject>();
            outlineRenderers = configuredOutlineRenderers ??
                Array.Empty<Renderer>();
        }
    }
}
