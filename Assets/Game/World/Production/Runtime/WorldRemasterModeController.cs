using UnityEngine;

namespace MSC.World.Remaster
{
    [DisallowMultipleComponent]
    public sealed class WorldRemasterModeController : MonoBehaviour
    {
        [SerializeField] private GameObject productionRoot;
        [SerializeField] private GameObject referenceRoot;
        [SerializeField] private WorldComparisonMode mode = WorldComparisonMode.ProductionOnly;

        public WorldComparisonMode Mode => mode;
        public GameObject ProductionRoot => productionRoot;
        public GameObject ReferenceRoot => referenceRoot;

        private void Awake() => ApplyMode(mode);

        public void Configure(GameObject production, GameObject reference, WorldComparisonMode initialMode)
        {
            productionRoot = production;
            referenceRoot = reference;
            ApplyMode(initialMode);
        }

        public void ApplyMode(WorldComparisonMode nextMode)
        {
            mode = nextMode;
            if (productionRoot != null)
            {
                productionRoot.SetActive(mode != WorldComparisonMode.ReferenceOnly);
            }

            if (referenceRoot != null)
            {
                referenceRoot.SetActive(mode != WorldComparisonMode.ProductionOnly);
            }
        }
    }
}
