using System;
using System.Collections.Generic;
using MSC.Characters;
using MSC.LegacyImport;
using MSC.Weather.Production;
using UnityEngine;

namespace MSC.NPC
{
    /// <summary>
    /// Binds Teimo's project-owned schedule to the temporary donor shop bicycle
    /// and service-door geometry. Stable world IDs are the only world lookup
    /// contract; donor hierarchy names never become runtime authority.
    /// </summary>
    internal sealed class TeimoShopWorldPresentationController : IDisposable
    {
        internal const string ShopArrivalScheduleId =
            "schedule.teimo.shop-arrival";
        internal const string ShopWaitScheduleId =
            "schedule.teimo.shop-wait";
        internal const string ShopScheduleId = "schedule.teimo.shop";
        internal const string StoreToPubScheduleId =
            "schedule.teimo.store-to-pub";
        internal const string PubScheduleId = "schedule.teimo.pub";

        private const string ServiceDoorStableId =
            "41889effb8941e0ef1aa015c802f648d";
        private const float ArrivalDurationRealSeconds = 26.1f;
        private const float DoorOpenEventRealSeconds = 15f;
        private const float DoorCloseEventRealSeconds = 16f;
        private const float DoorCloseDurationRealSeconds = 1.5666667f;
        private const float DoorOpenAngleDegrees = 85f;
        private const string ShopWeatherZoneStableId =
            "weather.zone.cell_-3_0.teimo.shop.v1";
        private const string ServiceDoorWeatherPortalStableId =
            "weather.portal.teimo.service_door.v1";

        private static readonly string[] ParkedBicycleStableIds =
        {
            "8f7341e40b01815fe359e33b864df56b",
            "7f09935ceb4f54431a23c37b8ba23e1e",
            "8c8312dfdcb7b77a47c1be818bc685cd",
            "d69b1be98ee0cb4496f5fa5a6265317f",
            "7ab57ce5c27ac32d14359c4b86a7d581",
            "4af3a8e8fa1fb94fa0021430ab9d800e",
        };

        private static readonly HashSet<string> BicycleVisibleSchedules =
            new HashSet<string>(StringComparer.Ordinal)
            {
                ShopArrivalScheduleId,
                ShopWaitScheduleId,
                ShopScheduleId,
                StoreToPubScheduleId,
                PubScheduleId,
            };

        private static readonly Vector3 ServiceDoorPivotPosition =
            new Vector3(-1384.022627f, 6.2509976f, 143.8757373f);
        private static readonly Quaternion ServiceDoorPivotRotation =
            new Quaternion(
                -0.62024206f,
                0.33955848f,
                0.33955857f,
                0.6202417f);

        private readonly CharacterInstance teimo;
        private readonly List<Renderer> bicycleRenderers =
            new List<Renderer>(ParkedBicycleStableIds.Length);

        private Transform serviceDoorMesh;
        private Transform serviceDoorOriginalParent;
        private Transform serviceDoorPivot;
        private Quaternion serviceDoorClosedLocalRotation;
        private TransformAngleWeatherPortalStateProvider weatherStateProvider;
        private WeatherPortal weatherPortal;
        private bool bindingsDirty = true;

        public TeimoShopWorldPresentationController(CharacterInstance instance)
        {
            teimo = instance ?? throw new ArgumentNullException(nameof(instance));
        }

        internal int BoundBicycleRendererCount => bicycleRenderers.Count;

        public void MarkBindingsDirty()
        {
            bindingsDirty = true;
        }

        public void Reconcile()
        {
            EnsureBindings();

            bool bicycleVisible = BicycleVisibleSchedules.Contains(
                teimo.ActiveScheduleBlockId);
            for (int index = 0; index < bicycleRenderers.Count; index++)
            {
                Renderer renderer = bicycleRenderers[index];
                if (renderer != null)
                {
                    renderer.enabled = bicycleVisible;
                }
            }

            if (serviceDoorPivot == null)
            {
                return;
            }

            EnsureWeatherPortal();

            float angle = string.Equals(
                teimo.ActiveScheduleBlockId,
                ShopArrivalScheduleId,
                StringComparison.Ordinal)
                ? EvaluateServiceDoorAngle(
                    (float)teimo.RouteProgress01 *
                    ArrivalDurationRealSeconds)
                : 0f;
            serviceDoorPivot.localRotation =
                serviceDoorClosedLocalRotation *
                Quaternion.AngleAxis(angle, Vector3.forward);
        }

        public void Dispose()
        {
            for (int index = 0; index < bicycleRenderers.Count; index++)
            {
                Renderer renderer = bicycleRenderers[index];
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }

            if (serviceDoorPivot != null)
            {
                serviceDoorPivot.gameObject.SetActive(false);
                serviceDoorPivot.localRotation =
                    serviceDoorClosedLocalRotation;
                if (serviceDoorMesh != null)
                {
                    serviceDoorMesh.SetParent(
                        serviceDoorOriginalParent,
                        worldPositionStays: true);
                }

                DestroyObject(serviceDoorPivot.gameObject);
            }

            serviceDoorMesh = null;
            serviceDoorOriginalParent = null;
            serviceDoorPivot = null;
            weatherStateProvider = null;
            weatherPortal = null;
            bicycleRenderers.Clear();
            bindingsDirty = true;
        }

        internal static float EvaluateServiceDoorAngle(float arrivalSeconds)
        {
            if (!float.IsFinite(arrivalSeconds) ||
                arrivalSeconds <= DoorOpenEventRealSeconds)
            {
                return 0f;
            }

            if (arrivalSeconds < DoorCloseEventRealSeconds)
            {
                return DoorOpenAngleDegrees * Mathf.InverseLerp(
                    DoorOpenEventRealSeconds,
                    DoorCloseEventRealSeconds,
                    arrivalSeconds);
            }

            return DoorOpenAngleDegrees * (1f - Mathf.InverseLerp(
                DoorCloseEventRealSeconds,
                DoorCloseEventRealSeconds +
                DoorCloseDurationRealSeconds,
                arrivalSeconds));
        }

        private void EnsureBindings()
        {
            if (!bindingsDirty &&
                bicycleRenderers.TrueForAll(renderer => renderer != null) &&
                serviceDoorMesh != null)
            {
                return;
            }

            Dispose();
            DonorWorldBaselineEntityMetadata[] entities =
                UnityEngine.Object.FindObjectsByType<
                    DonorWorldBaselineEntityMetadata>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            var bicycleIds = new HashSet<string>(
                ParkedBicycleStableIds,
                StringComparer.Ordinal);
            for (int index = 0; index < entities.Length; index++)
            {
                DonorWorldBaselineEntityMetadata entity = entities[index];
                if (entity == null)
                {
                    continue;
                }

                if (bicycleIds.Contains(entity.StableId))
                {
                    Renderer renderer = entity.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        bicycleRenderers.Add(renderer);
                    }
                }
                else if (string.Equals(
                    entity.StableId,
                    ServiceDoorStableId,
                    StringComparison.Ordinal))
                {
                    serviceDoorMesh = entity.transform;
                }
            }

            if (serviceDoorMesh != null)
            {
                serviceDoorOriginalParent = serviceDoorMesh.parent;
                var pivotObject = new GameObject(
                    "Teimo_ServiceDoor_ProjectOwnedPivot");
                serviceDoorPivot = pivotObject.transform;
                serviceDoorPivot.SetParent(
                    serviceDoorOriginalParent,
                    worldPositionStays: false);
                serviceDoorPivot.SetPositionAndRotation(
                    ServiceDoorPivotPosition,
                    ServiceDoorPivotRotation);
                serviceDoorClosedLocalRotation =
                    serviceDoorPivot.localRotation;
                serviceDoorMesh.SetParent(
                    serviceDoorPivot,
                    worldPositionStays: true);
            }

            bindingsDirty = false;
        }

        private void EnsureWeatherPortal()
        {
            if (weatherPortal != null || serviceDoorPivot == null ||
                WeatherZoneRegistry.Active == null ||
                !WeatherZoneRegistry.Active.TryGetZone(
                    ShopWeatherZoneStableId,
                    out WeatherZone shopZone))
            {
                return;
            }

            weatherStateProvider = serviceDoorPivot.gameObject.AddComponent<
                TransformAngleWeatherPortalStateProvider>();
            float closedAngle = serviceDoorClosedLocalRotation.eulerAngles.z;
            weatherStateProvider.Configure(
                serviceDoorPivot,
                PortalRotationAxis.Z,
                closedAngle,
                closedAngle + DoorOpenAngleDegrees);
            weatherPortal = serviceDoorPivot.gameObject.AddComponent<WeatherPortal>();
            weatherPortal.Configure(
                ServiceDoorWeatherPortalStableId,
                WeatherPortalKind.Door,
                shopZone,
                weatherStateProvider,
                serviceDoorPivot,
                new Vector2(1f, 2f),
                5f);
        }

        private static void DestroyObject(GameObject owner)
        {
            if (owner == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(owner);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }
}
