using System;
using UnityEngine;

namespace MSC.Services.Presentation
{
    public enum ServiceInteractionKind
    {
        StoreBasket = 0,
        StoreCheckout = 1,
        PubPurchase = 2,
        FuelDispense = 3,
        WorkshopOrder = 4,
        VehicleInspection = 5,
        WorkshopSelection = 6,
    }

    /// <summary>
    /// Presentation hint consumed by the already established status-message
    /// and UI confirm/cancel audio surfaces. It is not transaction authority.
    /// </summary>
    public enum ServiceInteractionFeedbackCue
    {
        None = 0,
        Confirm = 1,
        Reject = 2,
    }

    public readonly struct ServiceInteractionFeedback
    {
        public ServiceInteractionFeedback(
            ServiceInteractionKind kind,
            string locationId,
            string offerId,
            ServiceResult result,
            string fallbackMessage,
            Vector3 worldPosition,
            ServiceInteractionFeedbackCue cue)
        {
            Kind = kind;
            LocationId = locationId ?? string.Empty;
            OfferId = offerId ?? string.Empty;
            Result = result;
            FallbackMessage = fallbackMessage ?? string.Empty;
            WorldPosition = worldPosition;
            Cue = cue;
        }

        public ServiceInteractionKind Kind { get; }
        public string LocationId { get; }
        public string OfferId { get; }
        public ServiceResult Result { get; }
        public string FallbackMessage { get; }
        public Vector3 WorldPosition { get; }
        public ServiceInteractionFeedbackCue Cue { get; }
    }

    public delegate void ServiceInteractionFeedbackHandler(
        ServiceInteractionFeedback feedback);
}
