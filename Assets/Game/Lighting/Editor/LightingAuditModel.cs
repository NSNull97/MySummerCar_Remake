using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Lighting.Editor
{
    [Serializable]
    public sealed class LightingAuditRecord
    {
        public string assetPath = string.Empty;
        public string scene = string.Empty;
        public string prefabGuid = string.Empty;
        public string hierarchyPath = string.Empty;
        public Vector3 worldPosition;
        public Vector3 localPosition;
        public string inferredType = string.Empty;
        public bool existingLight;
        public bool existingEmissiveRenderer;
        public bool existingSwitch;
        public string inferredCircuit = string.Empty;
        public string classification = string.Empty;
        public float confidence;
        public bool lightPlacementApplied;
        public bool manualReviewRequired;
        public string manualReviewReason = string.Empty;
    }

    [Serializable]
    public sealed class LightingAuditDocument
    {
        public string generatedUtc = string.Empty;
        public string unityVersion = string.Empty;
        public int scenesScanned;
        public int prefabsScanned;
        public int candidates;
        public int configuredFixtures;
        public int manualReview;
        public List<LightingAuditRecord> records = new();
    }
}
