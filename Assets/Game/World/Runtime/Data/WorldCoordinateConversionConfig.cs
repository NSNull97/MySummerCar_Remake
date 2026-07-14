using System;
using UnityEngine;

namespace MSC.World.Data
{
    [Serializable]
    public sealed class WorldCoordinateConversionConfig
    {
        [SerializeField] private float sourceUnitScale = 1f;
        [SerializeField] private float destinationUnitScale = 1f;
        [SerializeField] private string axisMapping = "X,Y,Z -> X,Y,Z";
        [SerializeField] private string handednessConversion = "Unity left-handed Y-up retained";
        [SerializeField] private Vector3 globalTranslation;
        [SerializeField] private Quaternion globalRotation = Quaternion.identity;
        [SerializeField] private Vector3 globalScale = Vector3.one;
        [SerializeField] private string pivotPolicy = "Preserve donor hierarchy pivots";
        [SerializeField] private string negativeScaleHandlingPolicy = "Preserve and flag";
        [SerializeField] private string floatingOriginRecommendation = "Not required for reference generation";
        [SerializeField] private string precisionNotes = "Single precision is adequate for the audited approximately 6.5 km by 5.9 km layout";

        public float SourceUnitScale => sourceUnitScale;
        public float DestinationUnitScale => destinationUnitScale;
        public string AxisMapping => axisMapping;
        public string HandednessConversion => handednessConversion;
        public Vector3 GlobalTranslation => globalTranslation;
        public Quaternion GlobalRotation => globalRotation;
        public Vector3 GlobalScale => globalScale;
        public string PivotPolicy => pivotPolicy;
        public string NegativeScaleHandlingPolicy => negativeScaleHandlingPolicy;
        public string FloatingOriginRecommendation => floatingOriginRecommendation;
        public string PrecisionNotes => precisionNotes;

        public static WorldCoordinateConversionConfig CreateGarageAnchored(Vector3 donorGarageAnchorMeters)
        {
            return new WorldCoordinateConversionConfig
            {
                globalTranslation = -donorGarageAnchorMeters
            };
        }

        public Vector3 ConvertPoint(Vector3 sourcePoint)
        {
            float unitRatio = sourceUnitScale / destinationUnitScale;
            Vector3 scaled = Vector3.Scale(sourcePoint * unitRatio, globalScale);
            return globalRotation * scaled + globalTranslation;
        }

        public Vector3 ConvertDirection(Vector3 sourceDirection)
        {
            return globalRotation * Vector3.Scale(sourceDirection, globalScale).normalized;
        }

        public Quaternion ConvertRotation(Quaternion sourceRotation)
        {
            return globalRotation * sourceRotation;
        }

        public Vector3 ConvertScale(Vector3 sourceScale)
        {
            return Vector3.Scale(sourceScale * (sourceUnitScale / destinationUnitScale), globalScale);
        }

        public Vector3 InverseConvertPoint(Vector3 convertedPoint)
        {
            Vector3 unrotated = Quaternion.Inverse(globalRotation) * (convertedPoint - globalTranslation);
            Vector3 unscaled = new Vector3(
                SafeDivide(unrotated.x, globalScale.x),
                SafeDivide(unrotated.y, globalScale.y),
                SafeDivide(unrotated.z, globalScale.z));
            return unscaled * (destinationUnitScale / sourceUnitScale);
        }

        private static float SafeDivide(float value, float divisor)
        {
            if (Mathf.Approximately(divisor, 0f))
            {
                throw new InvalidOperationException("World coordinate conversion scale contains a zero component.");
            }

            return value / divisor;
        }
    }

    public static class WorldTransformHierarchyUtility
    {
        public static Matrix4x4 ComposeWorldMatrix(
            Matrix4x4 parentWorld,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            return parentWorld * Matrix4x4.TRS(localPosition, localRotation, localScale);
        }

        public static Vector3 ExtractPosition(Matrix4x4 worldMatrix)
        {
            return worldMatrix.MultiplyPoint3x4(Vector3.zero);
        }
    }
}
