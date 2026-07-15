using System;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    public enum PartCategory
    {
        Structural = 0,
        Suspension = 1,
        Brake = 2,
        Wheel = 3,
        Engine = 4,
        Electrical = 5,
        Cooling = 6,
        Body = 7,
        Interior = 8,
        Exhaust = 9
    }

    public enum FastenerSize
    {
        None = 0,
        Millimeter8 = 8,
        Millimeter9 = 9,
        Millimeter10 = 10,
        Millimeter11 = 11,
        Millimeter12 = 12,
        Millimeter13 = 13,
        Millimeter14 = 14,
        Millimeter17 = 17
    }

    public enum FastenerDirection
    {
        ClockwiseToTighten = 0,
        CounterClockwiseToTighten = 1
    }

    public enum FastenerRotationDirection
    {
        Clockwise = 0,
        CounterClockwise = 1
    }

    [Serializable]
    public sealed class PartCompatibilityRule
    {
        [SerializeField]
        private string mountSocketType = string.Empty;

        [SerializeField]
        private string requiredOwnerPartDefinitionId = string.Empty;

        public string MountSocketType => mountSocketType;

        public string RequiredOwnerPartDefinitionId => requiredOwnerPartDefinitionId;

        public bool Matches(MountPointDefinition mount)
        {
            if (mount == null || !string.Equals(
                    mountSocketType,
                    mount.SocketType,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return string.IsNullOrEmpty(requiredOwnerPartDefinitionId) || string.Equals(
                requiredOwnerPartDefinitionId,
                mount.OwnerPartDefinitionId,
                StringComparison.Ordinal);
        }

        public static PartCompatibilityRule Create(string socketType, string requiredOwnerId = "")
        {
            return new PartCompatibilityRule
            {
                mountSocketType = socketType ?? string.Empty,
                requiredOwnerPartDefinitionId = requiredOwnerId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class ToolCompatibilityRule
    {
        [SerializeField]
        private string toolType = "Wrench";

        [SerializeField]
        private FastenerSize fastenerSize = FastenerSize.None;

        public string ToolType => toolType;

        public FastenerSize FastenerSize => fastenerSize;

        public bool Matches(ToolDefinition tool)
        {
            return tool != null &&
                string.Equals(toolType, tool.ToolType, StringComparison.Ordinal) &&
                (fastenerSize == FastenerSize.None || fastenerSize == tool.Size);
        }

        public static ToolCompatibilityRule Create(string type, FastenerSize size)
        {
            return new ToolCompatibilityRule
            {
                toolType = type ?? string.Empty,
                fastenerSize = size
            };
        }
    }

    [Serializable]
    public struct MountConstraint
    {
        [SerializeField, Min(0.001f)]
        private float positionToleranceMeters;

        [SerializeField, Range(0f, 180f)]
        private float angularToleranceDegrees;

        [SerializeField, Min(0.01f)]
        private float previewDistanceMeters;

        [SerializeField, Min(0f)]
        private float obstructionRadiusMeters;

        public MountConstraint(
            float positionToleranceMeters,
            float angularToleranceDegrees,
            float previewDistanceMeters,
            float obstructionRadiusMeters)
        {
            this.positionToleranceMeters = Mathf.Max(0.001f, positionToleranceMeters);
            this.angularToleranceDegrees = Mathf.Clamp(angularToleranceDegrees, 0f, 180f);
            this.previewDistanceMeters = Mathf.Max(0.01f, previewDistanceMeters);
            this.obstructionRadiusMeters = Mathf.Max(0f, obstructionRadiusMeters);
        }

        public float PositionToleranceMeters => positionToleranceMeters;

        public float AngularToleranceDegrees => angularToleranceDegrees;

        public float PreviewDistanceMeters => previewDistanceMeters;

        public float ObstructionRadiusMeters => obstructionRadiusMeters;
    }

    [Serializable]
    public struct MountPose
    {
        [SerializeField]
        private Vector3 localPosition;

        [SerializeField]
        private Vector3 localEulerAngles;

        public MountPose(Vector3 localPosition, Vector3 localEulerAngles)
        {
            this.localPosition = localPosition;
            this.localEulerAngles = localEulerAngles;
        }

        public Vector3 LocalPosition => localPosition;

        public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);

        public static MountPose Capture(Transform transform)
        {
            return transform == null
                ? new MountPose(Vector3.zero, Vector3.zero)
                : new MountPose(transform.localPosition, transform.localEulerAngles);
        }
    }
}
