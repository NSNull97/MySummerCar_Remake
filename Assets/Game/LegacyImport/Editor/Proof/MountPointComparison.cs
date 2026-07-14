using System;
using UnityEngine;

namespace MSC.LegacyImport.Editor.Proof
{
    [Serializable]
    public sealed class MountPointComparison
    {
        [SerializeField] private string name = string.Empty;
        [SerializeField] private Vector3 referenceLocalPositionMeters = Vector3.zero;
        [SerializeField] private Vector3 productionLocalPositionMeters = Vector3.zero;

        public string Name => name;
        public Vector3 ReferenceLocalPositionMeters => referenceLocalPositionMeters;
        public Vector3 ProductionLocalPositionMeters => productionLocalPositionMeters;
        public float DeltaMeters => Vector3.Distance(
            referenceLocalPositionMeters,
            productionLocalPositionMeters);
    }
}
