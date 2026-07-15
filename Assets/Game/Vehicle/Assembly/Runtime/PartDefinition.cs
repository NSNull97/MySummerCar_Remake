using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [CreateAssetMenu(menuName = "MSC/Vehicle Assembly/Part Definition")]
    public sealed class PartDefinition : ScriptableObject
    {
        [SerializeField]
        private string definitionId = string.Empty;

        [SerializeField]
        private string displayName = string.Empty;

        [SerializeField]
        private PartCategory category;

        [SerializeField, Min(0.01f)]
        private float massKilograms = 1f;

        [SerializeField]
        private GameObject visualPrefab;

        [SerializeField]
        private PartCompatibilityRule[] compatibilityRules = Array.Empty<PartCompatibilityRule>();

        public string DefinitionId => definitionId;
        public string DisplayName => displayName;
        public PartCategory Category => category;
        public float MassKilograms => massKilograms;
        public GameObject VisualPrefab => visualPrefab;
        public PartCompatibilityRule[] CompatibilityRules => compatibilityRules;

        public bool IsCompatibleWith(MountPointDefinition mount)
        {
            if (mount == null || !mount.AcceptsPart(definitionId))
            {
                return false;
            }

            for (int i = 0; i < compatibilityRules.Length; i++)
            {
                PartCompatibilityRule rule = compatibilityRules[i];
                if (rule != null && rule.Matches(mount))
                {
                    return true;
                }
            }

            return false;
        }

        public void Configure(
            string id,
            string name,
            PartCategory partCategory,
            float mass,
            GameObject prefab,
            PartCompatibilityRule[] rules)
        {
            definitionId = id ?? string.Empty;
            displayName = name ?? string.Empty;
            category = partCategory;
            massKilograms = Mathf.Max(0.01f, mass);
            visualPrefab = prefab;
            compatibilityRules = rules ?? Array.Empty<PartCompatibilityRule>();
        }
    }
}
