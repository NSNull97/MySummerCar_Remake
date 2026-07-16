using System;
using System.Collections.Generic;
using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    public enum VehicleValidationRouteSectionKind
    {
        FlatAcceleration = 0,
        Braking = 1,
        SteeringSlalom = 2,
        SuspensionBump = 3,
        SurfaceComparison = 4,
        Slope = 5,
        GarageClearance = 6,
        CollisionTransition = 7
    }

    [Serializable]
    public sealed class VehicleValidationRouteSection
    {
        [SerializeField] private string sectionId = string.Empty;
        [SerializeField] private VehicleValidationRouteSectionKind kind;
        [SerializeField] private Transform spawnPose;
        [SerializeField] private VehicleSurfaceType surfaceType = VehicleSurfaceType.Unknown;
        [SerializeField, Min(0f)] private float lengthMeters;
        [SerializeField, Min(0f)] private float widthMeters;
        [SerializeField] private float slopeDegrees;
        [SerializeField, Min(0f)] private float bumpHeightMeters;
        [SerializeField, Min(0f)] private float garageOpeningWidthMeters;
        [SerializeField, Min(0f)] private float garageOpeningHeightMeters;
        [SerializeField, Min(0f)] private float maximumCollisionStepMeters;
        [SerializeField] private string evidenceBoundary = string.Empty;

        public string SectionId => sectionId;
        public VehicleValidationRouteSectionKind Kind => kind;
        public Transform SpawnPose => spawnPose;
        public VehicleSurfaceType SurfaceType => surfaceType;
        public float LengthMeters => lengthMeters;
        public float WidthMeters => widthMeters;
        public float SlopeDegrees => slopeDegrees;
        public float BumpHeightMeters => bumpHeightMeters;
        public float GarageOpeningWidthMeters => garageOpeningWidthMeters;
        public float GarageOpeningHeightMeters => garageOpeningHeightMeters;
        public float MaximumCollisionStepMeters => maximumCollisionStepMeters;
        public string EvidenceBoundary => evidenceBoundary;

        public void Configure(
            string id,
            VehicleValidationRouteSectionKind sectionKind,
            Transform configuredSpawnPose,
            VehicleSurfaceType configuredSurface,
            float configuredLengthMeters,
            float configuredWidthMeters,
            float configuredSlopeDegrees = 0f,
            float configuredBumpHeightMeters = 0f,
            float configuredGarageOpeningWidthMeters = 0f,
            float configuredGarageOpeningHeightMeters = 0f,
            float configuredMaximumCollisionStepMeters = 0f,
            string configuredEvidenceBoundary = "")
        {
            sectionId = id ?? string.Empty;
            kind = sectionKind;
            spawnPose = configuredSpawnPose;
            surfaceType = configuredSurface;
            lengthMeters = Mathf.Max(0f, configuredLengthMeters);
            widthMeters = Mathf.Max(0f, configuredWidthMeters);
            slopeDegrees = configuredSlopeDegrees;
            bumpHeightMeters = Mathf.Max(0f, configuredBumpHeightMeters);
            garageOpeningWidthMeters = Mathf.Max(0f, configuredGarageOpeningWidthMeters);
            garageOpeningHeightMeters = Mathf.Max(0f, configuredGarageOpeningHeightMeters);
            maximumCollisionStepMeters = Mathf.Max(0f, configuredMaximumCollisionStepMeters);
            evidenceBoundary = configuredEvidenceBoundary ?? string.Empty;
        }

        public bool Validate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(sectionId))
            {
                failure = "Validation route section ID is empty.";
                return false;
            }

            if (spawnPose == null)
            {
                failure = $"Validation route section '{sectionId}' has no spawn pose.";
                return false;
            }

            if (!VehicleSimulationMath.IsFinite(lengthMeters) ||
                !VehicleSimulationMath.IsFinite(widthMeters) ||
                !VehicleSimulationMath.IsFinite(slopeDegrees) ||
                !VehicleSimulationMath.IsFinite(bumpHeightMeters) ||
                !VehicleSimulationMath.IsFinite(garageOpeningWidthMeters) ||
                !VehicleSimulationMath.IsFinite(garageOpeningHeightMeters) ||
                !VehicleSimulationMath.IsFinite(maximumCollisionStepMeters) ||
                lengthMeters <= 0f || widthMeters <= 0f)
            {
                failure = $"Validation route section '{sectionId}' has invalid geometry.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [DisallowMultipleComponent]
    public sealed class VehicleValidationRouteAuthoring : MonoBehaviour
    {
        [SerializeField] private string routeId = "m06a.physics-validation-course.v1";
        [SerializeField] private VehicleValidationRouteSection[] sections =
            Array.Empty<VehicleValidationRouteSection>();

        public string RouteId => routeId;
        public IReadOnlyList<VehicleValidationRouteSection> Sections => sections;

        public void Configure(string configuredRouteId, VehicleValidationRouteSection[] configuredSections)
        {
            routeId = configuredRouteId ?? string.Empty;
            sections = configuredSections ?? Array.Empty<VehicleValidationRouteSection>();
        }

        public bool TryGetSection(string sectionId, out VehicleValidationRouteSection section)
        {
            if (!string.IsNullOrWhiteSpace(sectionId) && sections != null)
            {
                for (int index = 0; index < sections.Length; index++)
                {
                    VehicleValidationRouteSection candidate = sections[index];
                    if (candidate != null && string.Equals(
                            candidate.SectionId,
                            sectionId,
                            StringComparison.Ordinal))
                    {
                        section = candidate;
                        return true;
                    }
                }
            }

            section = null;
            return false;
        }

        public bool Validate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(routeId))
            {
                failure = "Validation route ID is empty.";
                return false;
            }

            int requiredKindMask = 0;
            for (int value = (int)VehicleValidationRouteSectionKind.FlatAcceleration;
                 value <= (int)VehicleValidationRouteSectionKind.CollisionTransition;
                 value++)
            {
                requiredKindMask |= 1 << value;
            }

            int actualKindMask = 0;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (sections != null)
            {
                for (int index = 0; index < sections.Length; index++)
                {
                    VehicleValidationRouteSection section = sections[index];
                    if (section == null)
                    {
                        failure = $"Validation route section at index {index} is null.";
                        return false;
                    }

                    if (!section.Validate(out failure))
                    {
                        return false;
                    }

                    if (!ids.Add(section.SectionId))
                    {
                        failure = $"Validation route section ID is duplicated: {section.SectionId}.";
                        return false;
                    }

                    actualKindMask |= 1 << (int)section.Kind;
                }
            }

            if (actualKindMask != requiredKindMask)
            {
                failure = "Validation route must contain acceleration, braking, slalom, bump, " +
                          "surface, slope, garage, and collision-transition sections.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }
}
