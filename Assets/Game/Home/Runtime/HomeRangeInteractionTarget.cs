using System;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Home
{
    /// <summary>
    /// Scene-facing adapter for the donor-evidenced sauna Range controls.
    /// The authored transform follows authoritative state and never owns it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeRangeInteractionTarget :
        MonoBehaviour,
        IDirectionalIncrementalInteractionTarget
    {
        [SerializeField] private string stableActionId =
            HomeActionIds.ElectricSaunaPowerToggle;
        [SerializeField] private HomeActionKind action =
            HomeActionKind.ElectricSaunaPowerToggle;
        [SerializeField] private Vector3 localRotationAxis = Vector3.up;

        private HomeSystemRuntime runtime;
        private Quaternion neutralLocalRotation;

        public string StableActionId => stableActionId;
        public HomeActionKind Action => action;
        public string AdjustmentPrompt =>
            runtime != null
                ? runtime.GetAdjustmentPrompt(action)
                : string.Empty;

        public void Configure(
            string configuredStableActionId,
            HomeActionKind configuredAction,
            HomeSystemRuntime configuredRuntime,
            Vector3 configuredLocalRotationAxis)
        {
            if (string.IsNullOrWhiteSpace(configuredStableActionId))
            {
                throw new ArgumentException(
                    "Home range interaction requires a stable action ID.",
                    nameof(configuredStableActionId));
            }

            if (!HomeActionIds.Matches(
                    configuredStableActionId,
                    configuredAction))
            {
                throw new ArgumentException(
                    "Home range action ID does not match its action.",
                    nameof(configuredStableActionId));
            }

            if (configuredAction !=
                    HomeActionKind.ElectricSaunaPowerToggle &&
                configuredAction !=
                    HomeActionKind.ElectricSaunaTimerCycle)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredAction),
                    configuredAction,
                    "Only sauna Range controls are supported.");
            }

            if (!float.IsFinite(configuredLocalRotationAxis.x) ||
                !float.IsFinite(configuredLocalRotationAxis.y) ||
                !float.IsFinite(configuredLocalRotationAxis.z) ||
                configuredLocalRotationAxis.sqrMagnitude < 0.001f)
            {
                throw new ArgumentException(
                    "Home range rotation axis is invalid.",
                    nameof(configuredLocalRotationAxis));
            }

            stableActionId = configuredStableActionId;
            action = configuredAction;
            runtime = configuredRuntime ??
                throw new ArgumentNullException(nameof(configuredRuntime));
            localRotationAxis =
                configuredLocalRotationAxis.normalized;
            neutralLocalRotation = transform.localRotation;
            runtime.StateChanged += HandleStateChanged;
            ApplyState(runtime.Snapshot);
        }

        public bool CanAdjust(in InteractionContext context) =>
            enabled &&
            gameObject.activeInHierarchy &&
            runtime != null &&
            runtime.CanAdjustAction(stableActionId, action);

        public bool TryAdjust(
            in InteractionContext context,
            float signedNotches)
        {
            return CanAdjust(context) &&
                   runtime.TryAdjustAction(
                       stableActionId,
                       action,
                       signedNotches,
                       out _);
        }

        public bool CanAdjust(
            in InteractionContext context,
            InteractionScrollDirection direction)
        {
            if (!CanAdjust(context))
            {
                return false;
            }

            HomeStateSnapshot state = runtime.Snapshot;
            if (action == HomeActionKind.ElectricSaunaPowerToggle)
            {
                return direction == InteractionScrollDirection.Positive
                    ? state.ElectricSaunaHeatKnobDegrees <
                      HomeSaunaControlRange.MaximumHeatDegrees - 0.001f
                    : state.ElectricSaunaHeatKnobDegrees >
                      HomeSaunaControlRange.MinimumHeatDegrees + 0.001f;
            }

            return direction == InteractionScrollDirection.Positive
                ? state.ElectricSaunaTimerKnobDegrees <
                  HomeSaunaControlRange.MaximumTimerDegrees - 0.001f
                : state.ElectricSaunaTimerKnobDegrees >
                  HomeSaunaControlRange.MinimumTimerDegrees + 0.001f;
        }

        public string GetAdjustmentPrompt(
            InteractionScrollDirection direction) =>
            direction == InteractionScrollDirection.Positive
                ? "УВЕЛИЧИТЬ"
                : "УМЕНЬШИТЬ";

        private void HandleStateChanged(HomeStateSnapshot state)
        {
            ApplyState(state);
        }

        private void ApplyState(HomeStateSnapshot state)
        {
            float degrees = action ==
                HomeActionKind.ElectricSaunaPowerToggle
                    ? state.ElectricSaunaHeatKnobDegrees
                    : state.ElectricSaunaTimerKnobDegrees;
            transform.localRotation =
                neutralLocalRotation *
                Quaternion.AngleAxis(degrees, localRotationAxis);
        }

        private void OnDestroy()
        {
            if (runtime != null)
            {
                runtime.StateChanged -= HandleStateChanged;
            }
        }
    }
}
