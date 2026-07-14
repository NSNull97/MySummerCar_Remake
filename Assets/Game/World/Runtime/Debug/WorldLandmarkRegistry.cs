using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.World.Debugging
{
    [Serializable]
    public struct WorldLandmarkEntry
    {
        [SerializeField] private string stableId;
        [SerializeField] private string landmarkName;
        [SerializeField] private Vector3 position;

        public WorldLandmarkEntry(string id, string name, Vector3 worldPosition)
        {
            stableId = id;
            landmarkName = name;
            position = worldPosition;
        }

        public string StableId => stableId;
        public string LandmarkName => landmarkName;
        public Vector3 Position => position;
    }

    [DisallowMultipleComponent]
    public sealed class WorldLandmarkRegistry : MonoBehaviour
    {
        [SerializeField] private WorldLandmarkEntry[] landmarks = Array.Empty<WorldLandmarkEntry>();

        public IReadOnlyList<WorldLandmarkEntry> Landmarks => landmarks;

        public void Configure(WorldLandmarkEntry[] entries)
        {
            landmarks = entries ?? Array.Empty<WorldLandmarkEntry>();
        }

        public bool TryResolve(string stableIdOrName, out WorldLandmarkEntry landmark)
        {
            for (int index = 0; index < landmarks.Length; index++)
            {
                WorldLandmarkEntry candidate = landmarks[index];
                if (string.Equals(candidate.StableId, stableIdOrName, StringComparison.Ordinal) ||
                    string.Equals(candidate.LandmarkName, stableIdOrName, StringComparison.OrdinalIgnoreCase))
                {
                    landmark = candidate;
                    return true;
                }
            }

            landmark = default;
            return false;
        }
    }
}
