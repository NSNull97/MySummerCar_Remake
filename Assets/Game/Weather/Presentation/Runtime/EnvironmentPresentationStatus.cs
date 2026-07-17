using System;

namespace MSC.Weather.Presentation
{
    public readonly struct EnvironmentPresentationStatus
    {
        public EnvironmentPresentationStatus(
            EnvironmentPresentationState state,
            EnvironmentPresentationCapabilities capabilities,
            ulong lastAppliedRevision,
            int warningCount,
            int errorCount)
        {
            State = state;
            Capabilities = capabilities;
            LastAppliedRevision = lastAppliedRevision;
            WarningCount = Math.Max(0, warningCount);
            ErrorCount = Math.Max(0, errorCount);
        }

        public EnvironmentPresentationState State { get; }

        public EnvironmentPresentationCapabilities Capabilities { get; }

        public ulong LastAppliedRevision { get; }

        public int WarningCount { get; }

        public int ErrorCount { get; }

        public bool IsOperational =>
            ErrorCount == 0 &&
            (State == EnvironmentPresentationState.Ready ||
             State == EnvironmentPresentationState.Degraded);

        public static EnvironmentPresentationStatus Detached => new EnvironmentPresentationStatus(
            EnvironmentPresentationState.Detached,
            EnvironmentPresentationCapabilities.None,
            0,
            0,
            0);
    }
}
