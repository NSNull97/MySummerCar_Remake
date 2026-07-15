using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [CreateAssetMenu(menuName = "MSC/Vehicle Assembly/Fastener Definition")]
    public sealed class FastenerDefinition : ScriptableObject
    {
        [SerializeField]
        private string definitionId = string.Empty;

        [SerializeField]
        private string displayName = string.Empty;

        [SerializeField]
        private FastenerSize size = FastenerSize.Millimeter10;

        [SerializeField, Min(1)]
        private int maximumStage = 8;

        [SerializeField]
        private FastenerDirection tighteningDirection = FastenerDirection.ClockwiseToTighten;

        [SerializeField]
        private bool insertedOnInstall = true;

        [SerializeField]
        private bool requiredForRemoval = true;

        [SerializeField]
        private ToolCompatibilityRule toolRule = new ToolCompatibilityRule();

        public string DefinitionId => definitionId;
        public string DisplayName => displayName;
        public FastenerSize Size => size;
        public int MaximumStage => maximumStage;
        public FastenerDirection TighteningDirection => tighteningDirection;
        public bool InsertedOnInstall => insertedOnInstall;
        public bool RequiredForRemoval => requiredForRemoval;
        public ToolCompatibilityRule ToolRule => toolRule;

        public void Configure(
            string id,
            string name,
            FastenerSize fastenerSize,
            int stages,
            FastenerDirection direction,
            bool insertOnInstall,
            bool blocksRemoval,
            ToolCompatibilityRule compatibility)
        {
            definitionId = id ?? string.Empty;
            displayName = name ?? string.Empty;
            size = fastenerSize;
            maximumStage = Mathf.Max(1, stages);
            tighteningDirection = direction;
            insertedOnInstall = insertOnInstall;
            requiredForRemoval = blocksRemoval;
            toolRule = compatibility ?? new ToolCompatibilityRule();
        }
    }
}
