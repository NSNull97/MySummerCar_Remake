using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [CreateAssetMenu(menuName = "MSC/Vehicle Assembly/Tool Definition")]
    public sealed class ToolDefinition : ScriptableObject
    {
        [SerializeField]
        private string definitionId = string.Empty;

        [SerializeField]
        private string displayName = string.Empty;

        [SerializeField]
        private string toolType = "Wrench";

        [SerializeField]
        private FastenerSize size = FastenerSize.None;

        public string DefinitionId => definitionId;
        public string DisplayName => displayName;
        public string ToolType => toolType;
        public FastenerSize Size => size;

        public void Configure(string id, string name, string type, FastenerSize toolSize)
        {
            definitionId = id ?? string.Empty;
            displayName = name ?? string.Empty;
            toolType = type ?? string.Empty;
            size = toolSize;
        }
    }
}
