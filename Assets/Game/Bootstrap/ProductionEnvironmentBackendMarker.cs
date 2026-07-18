using System;
using UnityEngine;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Project-owned identity for the generated production environment backend.
    /// The marker lets Editor rebuild tooling avoid hierarchy-name ownership.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionEnvironmentBackendMarker : MonoBehaviour
    {
        public const string StableOwnerId =
            "production.environment.backend.v1";

        [SerializeField] private string ownerId = StableOwnerId;

        public string OwnerId => ownerId;

        public bool IsValid =>
            string.Equals(ownerId, StableOwnerId, StringComparison.Ordinal);

#if UNITY_EDITOR
        public void ConfigureForAuthoring()
        {
            ownerId = StableOwnerId;
        }
#endif
    }
}
