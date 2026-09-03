using System;
using MSC.Items;
using UnityEngine;

namespace MSC.Services.Presentation
{
    /// <summary>
    /// Stable placement policy for physical service handoffs. Teimo pub points
    /// are exact donor item pivots; the general service anchor remains a
    /// project-owned fallback for other locations.
    /// </summary>
    public static class ServiceHandoffPlacementPolicy
    {
        private const string TeimoPubLocationId =
            "service.location.teimo-pub";
        private const string PreparedMealDefinitionId =
            "item.sausage-and-potatoes-meal";
        private static readonly Quaternion DonorDrinkSpawnRotation =
            new(0.22377104f, 0.6889811f, 0.6330759f, -0.27284887f);
        private static readonly Quaternion DonorFoodSpawnRotation =
            new(-0.30169034f, 0.64968413f, 0.48720184f, 0.49952763f);
        private static readonly Vector3 DonorDrinkSpawnPosition =
            new(-1375.5087f, 6.305682f, 145.8352f);
        private static readonly Vector3 DonorFoodSpawnPosition =
            new(-1375.6229f, 6.333628f, 145.78503f);

        public static Vector3 GetCounterPosition(
            string locationId,
            string definitionId,
            Vector3 fallbackPosition)
        {
            if (!string.Equals(
                    locationId,
                    TeimoPubLocationId,
                    StringComparison.Ordinal))
            {
                return fallbackPosition;
            }

            return string.Equals(
                definitionId,
                PreparedMealDefinitionId,
                StringComparison.Ordinal)
                    ? DonorFoodSpawnPosition
                    : DonorDrinkSpawnPosition;
        }

        public static Quaternion GetCounterRotation(
            string locationId,
            string definitionId)
        {
            if (string.Equals(
                    locationId,
                    TeimoPubLocationId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    definitionId,
                    PreparedMealDefinitionId,
                    StringComparison.Ordinal))
            {
                return DonorFoodSpawnRotation;
            }

            if (string.Equals(
                    locationId,
                    TeimoPubLocationId,
                    StringComparison.Ordinal))
            {
                // The donor FSM creates every under-counter product at one
                // DrinkSpawnPoint. Per-item guesses made bottles and cups fall
                // sideways after handoff.
                return DonorDrinkSpawnRotation;
            }

            return Quaternion.identity;
        }

        public static float CalculateSurfaceLift(
            ItemDefinitionRecord definition,
            Quaternion rotation,
            float clearance = 0.015f)
        {
            if (definition == null)
            {
                return Mathf.Max(0f, clearance);
            }

            Vector3 half = definition.ProxySize * 0.5f;
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;
            float verticalHalfExtent =
                Mathf.Abs(right.y) * half.x +
                Mathf.Abs(up.y) * half.y +
                Mathf.Abs(forward.y) * half.z;
            return Mathf.Max(0.01f, verticalHalfExtent) +
                   Mathf.Max(0f, clearance);
        }
    }
}
