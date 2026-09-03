using UnityEngine;

namespace MSC.LegacyImport
{
    /// <summary>
    /// Generated loose parts are authored inside the vehicle prefab so their
    /// references remain deterministic. At runtime they become composition-root
    /// siblings and therefore never follow the chassis after it starts moving.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class LegacySatsumaLoosePartsRoot : MonoBehaviour
    {
        [SerializeField] private Transform loosePartsRoot;

        public Transform LoosePartsRoot => loosePartsRoot;

        public void Configure(Transform configuredRoot)
        {
            loosePartsRoot = configuredRoot;
        }

        private void Awake()
        {
            if (loosePartsRoot == null || loosePartsRoot.parent != transform)
            {
                return;
            }

            loosePartsRoot.SetParent(transform.parent, true);
        }
    }
}
