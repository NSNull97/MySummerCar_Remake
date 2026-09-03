using System;
using UnityEngine;

namespace MSC.World.Presentation
{
    public sealed class Phase1GrassFieldData : ScriptableObject
    {
        [SerializeField] private Vector3[] localPositions =
            Array.Empty<Vector3>();
        [SerializeField] private Bounds localBounds;

        public Vector3[] LocalPositions => localPositions;
        public Bounds LocalBounds => localBounds;

#if UNITY_EDITOR
        public void SetGeneratedData(
            Vector3[] positions,
            Bounds bounds)
        {
            localPositions = positions ??
                             throw new ArgumentNullException(
                                 nameof(positions));
            localBounds = bounds;
        }
#endif
    }
}
