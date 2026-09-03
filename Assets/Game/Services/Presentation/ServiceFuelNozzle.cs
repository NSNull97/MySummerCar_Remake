using System;
using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using UnityEngine;

namespace MSC.Services.Presentation
{
    /// <summary>
    /// Project-owned physical fuel nozzle. Fuel crosses the service/item
    /// boundary only while the carried nozzle trigger overlaps an open,
    /// compatible liquid container.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(StableEntityIdAuthoring))]
    public sealed class ServiceFuelNozzle :
        ServiceOfferInteractionTargetBase,
        IPickupTarget
    {
        [SerializeField] private Rigidbody targetBody;
        [SerializeField] private StableEntityIdAuthoring stableIdAuthoring;
        [SerializeField] private ServiceFuelNozzleFillVolume fillVolume;
        [SerializeField] private LineRenderer hoseRenderer;
        [SerializeField] private Vector3 cradleWorldPosition;
        [SerializeField] private Quaternion cradleWorldRotation = Quaternion.identity;
        [SerializeField, Min(0.5f)] private float maximumHoseLengthMeters = 4.5f;
        [SerializeField, Min(0.01f)] private float flowLitresPerSecond = 0.8f;

        private bool isCarried;
        private bool dispensedSincePickup;
        private float nextFeedbackTime;
        private Material runtimeHoseMaterial;

        protected override ServiceLocationKind ExpectedLocationKind =>
            ServiceLocationKind.FuelStation;
        protected override ServiceOfferKind ExpectedOfferKind =>
            ServiceOfferKind.Fuel;

        public override string InteractionPrompt => string.Empty;
        public override string InteractionDisplayName
        {
            get
            {
                if (!TryResolveOffer(
                        ExpectedOfferKind,
                        out _,
                        out ServiceOfferDefinition offer,
                        out _))
                {
                    return string.Empty;
                }

                long minor = Runtime.GetFuelPriceMinorUnitsPerLiter(
                    offer.FuelGrade);
                return $"{offer.DisplayName} — {minor / 100L},{Math.Abs(minor % 100L):00} MK/л";
            }
        }

        public string PickupPrompt => "Взять топливный пистолет";
        public Rigidbody Body => targetBody;
        public StableEntityId StableId =>
            stableIdAuthoring != null &&
            stableIdAuthoring.TryGetStableId(out StableEntityId id)
                ? id
                : default;
        public bool IsCarried => isCarried;
        public bool HasActiveFuelSession => dispensedSincePickup;
        public ServiceFuelNozzleFillVolume FillVolume => fillVolume;

        // Pickup is the only direct interaction; dispensing is physical.
        public override bool CanInteract(in InteractionContext context) => false;
        public override void Interact(in InteractionContext context)
        {
        }

        public bool CanPickup(in InteractionContext context) =>
            enabled && gameObject.activeInHierarchy && !isCarried &&
            targetBody != null && !targetBody.isKinematic && StableId.IsValid &&
            TryResolveOffer(
                ExpectedOfferKind,
                out ServiceLocationDefinition location,
                out _,
                out _) &&
            Runtime.IsLocationOpen(location.LocationId);

        public void NotifyPickedUp(in InteractionContext context)
        {
            isCarried = true;
            dispensedSincePickup = false;
            fillVolume?.SetFlowing(true);
        }

        public void NotifyReleased(PickupReleaseReason reason)
        {
            isCarried = false;
            fillVolume?.SetFlowing(false);

            if (dispensedSincePickup &&
                TryResolveOffer(
                    ExpectedOfferKind,
                    out _,
                    out ServiceOfferDefinition offer,
                    out _))
            {
                ServiceResult result = Runtime.TryReleaseFuelNozzle(
                    offer.FuelGrade);
                Publish(
                    ServiceInteractionKind.FuelDispense,
                    offer.OfferId,
                    result,
                    MessageFor(result, "Топливный пистолет возвращён"));
            }

            dispensedSincePickup = false;
            ReturnToCradle();
        }

        public void ConfigureForAuthoring(
            string configuredLocationId,
            string configuredInteractionAnchorId,
            string configuredOfferId,
            string configuredStableId,
            Vector3 configuredCradleWorldPosition,
            Quaternion configuredCradleWorldRotation,
            float configuredMaximumHoseLengthMeters = 4.5f,
            float configuredFlowLitresPerSecond = 0.8f)
        {
            if (!StableEntityId.TryParse(
                    configuredStableId,
                    out StableEntityId stableId))
            {
                throw new ArgumentException(
                    "Fuel nozzle stable ID must be canonical.",
                    nameof(configuredStableId));
            }

            ConfigureBindingForAuthoring(
                configuredLocationId,
                configuredInteractionAnchorId);
            ConfigureOfferForAuthoring(configuredOfferId);
            targetBody = GetComponent<Rigidbody>();
            stableIdAuthoring = GetComponent<StableEntityIdAuthoring>();
            stableIdAuthoring.InitializeExplicitRuntimeId(stableId);
            cradleWorldPosition = configuredCradleWorldPosition;
            cradleWorldRotation = configuredCradleWorldRotation;
            maximumHoseLengthMeters = Mathf.Max(
                0.5f,
                configuredMaximumHoseLengthMeters);
            flowLitresPerSecond = Mathf.Max(
                0.01f,
                configuredFlowLitresPerSecond);
            fillVolume = GetComponentInChildren<ServiceFuelNozzleFillVolume>(
                includeInactive: true);
            if (fillVolume == null)
            {
                var volumeObject = new GameObject("Fuel Stream Fill Volume");
                volumeObject.transform.SetParent(transform, false);
                volumeObject.transform.localPosition = new Vector3(0f, -0.16f, 0.22f);
                volumeObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                fillVolume = volumeObject.AddComponent<
                    ServiceFuelNozzleFillVolume>();
            }

            fillVolume.Configure(this, 0.065f, 0.32f);
            ConfigureHoseRenderer();
            ReturnToCradle();
        }

        public void Bind(
            ServiceRuntime configuredRuntime,
            ServiceInteractionFeedbackHandler feedback = null) =>
            BindRuntime(configuredRuntime, feedback);

        internal bool TryDispenseInto(
            Collider other,
            float elapsedSeconds)
        {
            if (!isCarried || other == null ||
                !float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0f ||
                !TryResolveOffer(
                    ExpectedOfferKind,
                    out ServiceLocationDefinition location,
                    out ServiceOfferDefinition offer,
                    out _) ||
                !Runtime.IsLocationOpen(location.LocationId))
            {
                return false;
            }

            float requestedLitres = flowLitresPerSecond * elapsedSeconds;
            if (!TryConvertLitresToMillilitres(
                    requestedLitres,
                    out long requestedMillilitres))
            {
                return false;
            }

            ServiceResult preflight = Runtime.TryPreflightAcceptedFuel(
                offer.FuelGrade,
                requestedMillilitres);
            if (!preflight.Succeeded)
            {
                return false;
            }

            MonoBehaviour[] candidates =
                other.GetComponentsInParent<MonoBehaviour>(
                    includeInactive: false);
            string liquidId = ServiceFuelDispenseInteractionTarget
                .FuelLiquidId(offer.FuelGrade);
            for (int index = 0; index < candidates.Length; index++)
            {
                if (!(candidates[index] is ILiquidContainerTarget container) ||
                    !container.CanAcceptLiquid(liquidId, requestedLitres) ||
                    !container.TryAcceptLiquid(
                        liquidId,
                        requestedLitres,
                        out float acceptedLitres) ||
                    !TryConvertLitresToMillilitres(
                        acceptedLitres,
                        out long acceptedMillilitres))
                {
                    continue;
                }

                ServiceResult recorded = Runtime.TryRecordAcceptedFuel(
                    offer.FuelGrade,
                    acceptedMillilitres);
                if (!recorded.Succeeded)
                {
                    return false;
                }

                dispensedSincePickup = true;
                if (Time.unscaledTime >= nextFeedbackTime)
                {
                    nextFeedbackTime = Time.unscaledTime + 0.25f;
                    Publish(
                        ServiceInteractionKind.FuelDispense,
                        offer.OfferId,
                        recorded,
                        $"Заправлено: {acceptedLitres:0.###} л");
                }

                return true;
            }

            return false;
        }

        private void FixedUpdate()
        {
            if (!isCarried || targetBody == null)
            {
                return;
            }

            Vector3 offset = targetBody.position - cradleWorldPosition;
            if (offset.sqrMagnitude <=
                maximumHoseLengthMeters * maximumHoseLengthMeters)
            {
                return;
            }

            targetBody.position = cradleWorldPosition +
                offset.normalized * maximumHoseLengthMeters;
            targetBody.linearVelocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (hoseRenderer == null)
            {
                return;
            }

            hoseRenderer.SetPosition(0, cradleWorldPosition);
            hoseRenderer.SetPosition(1, transform.position);
        }

        private void ReturnToCradle()
        {
            if (targetBody == null)
            {
                return;
            }

            targetBody.linearVelocity = Vector3.zero;
            targetBody.angularVelocity = Vector3.zero;
            targetBody.position = cradleWorldPosition;
            targetBody.rotation = cradleWorldRotation;
        }

        private void ConfigureHoseRenderer()
        {
            hoseRenderer = GetComponent<LineRenderer>();
            if (hoseRenderer == null)
            {
                hoseRenderer = gameObject.AddComponent<LineRenderer>();
            }

            hoseRenderer.useWorldSpace = true;
            hoseRenderer.positionCount = 2;
            hoseRenderer.startWidth = 0.025f;
            hoseRenderer.endWidth = 0.025f;
            hoseRenderer.numCapVertices = 3;
            Shader shader = Shader.Find("HDRP/Unlit") ??
                            Shader.Find("Sprites/Default");
            if (shader != null)
            {
                runtimeHoseMaterial = new Material(shader)
                {
                    name = "Runtime Fuel Hose",
                    color = new Color(0.025f, 0.025f, 0.025f, 1f),
                };
                hoseRenderer.sharedMaterial = runtimeHoseMaterial;
            }
        }

        private void OnDestroy()
        {
            if (runtimeHoseMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(runtimeHoseMaterial);
                }
                else
                {
                    DestroyImmediate(runtimeHoseMaterial);
                }
            }
        }

        private static bool TryConvertLitresToMillilitres(
            float litres,
            out long millilitres)
        {
            millilitres = 0;
            if (!float.IsFinite(litres) || litres <= 0f)
            {
                return false;
            }

            try
            {
                millilitres = Math.Max(
                    1L,
                    checked((long)Math.Round(
                        (double)litres * 1000d,
                        MidpointRounding.AwayFromZero)));
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class ServiceFuelNozzleFillVolume : MonoBehaviour
    {
        private ServiceFuelNozzle nozzle;
        private CapsuleCollider trigger;
        private bool configured;

        public CapsuleCollider Trigger => trigger;

        public void Configure(
            ServiceFuelNozzle configuredNozzle,
            float radiusMeters,
            float lengthMeters)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "Fuel nozzle fill volume is already configured.");
            }

            nozzle = configuredNozzle ??
                throw new ArgumentNullException(nameof(configuredNozzle));
            trigger = gameObject.AddComponent<CapsuleCollider>();
            trigger.isTrigger = true;
            trigger.direction = 2;
            trigger.radius = radiusMeters;
            trigger.height = lengthMeters;
            trigger.center = Vector3.forward * (lengthMeters * 0.5f);
            trigger.enabled = false;
            configured = true;
        }

        public void SetFlowing(bool value)
        {
            if (trigger != null)
            {
                trigger.enabled = configured && value;
            }
        }

        public bool TryFill(Collider other, float elapsedSeconds) =>
            nozzle != null && nozzle.TryDispenseInto(other, elapsedSeconds);

        private void OnTriggerStay(Collider other) =>
            TryFill(other, Time.fixedDeltaTime);
    }
}
