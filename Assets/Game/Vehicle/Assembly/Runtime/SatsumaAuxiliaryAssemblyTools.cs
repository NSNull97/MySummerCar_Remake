using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// Project-owned unsized tool definitions for the special toolbox tools.
    /// An importer must explicitly bind the matching rule to a reviewed screw
    /// or spark-plug target; these definitions never act as universal wrenches.
    /// </summary>
    public static class SatsumaAuxiliaryAssemblyTools
    {
        public const string ScrewdriverType = "Screwdriver";
        public const string SparkPlugWrenchType = "SparkPlugWrench";
        public const string ScrewdriverDefinitionId = "tool.satsuma.screwdriver";
        public const string SparkPlugWrenchDefinitionId = "tool.satsuma.spark-plug-wrench";

        public static ToolDefinition CreateScrewdriver() => Create(
            ScrewdriverDefinitionId, "Отвёртка", ScrewdriverType);

        public static ToolDefinition CreateSparkPlugWrench() => Create(
            SparkPlugWrenchDefinitionId, "Свечной ключ", SparkPlugWrenchType);

        public static ToolCompatibilityRule ScrewdriverRule() =>
            ToolCompatibilityRule.Create(ScrewdriverType, FastenerSize.None);

        public static ToolCompatibilityRule SparkPlugWrenchRule() =>
            ToolCompatibilityRule.Create(SparkPlugWrenchType, FastenerSize.None);

        private static ToolDefinition Create(string id, string name, string type)
        {
            ToolDefinition tool = ScriptableObject.CreateInstance<ToolDefinition>();
            tool.Configure(id, name, type, FastenerSize.None);
            return tool;
        }
    }
}
