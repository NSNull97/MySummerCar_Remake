namespace MSC.Weather.Presentation
{
    public readonly struct EnvironmentPresentationDiagnostic
    {
        public EnvironmentPresentationDiagnostic(
            EnvironmentDiagnosticSeverity severity,
            string code,
            string message,
            EnvironmentBindingId bindingId = default)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            BindingId = bindingId;
        }

        public EnvironmentDiagnosticSeverity Severity { get; }

        public string Code { get; }

        public string Message { get; }

        public EnvironmentBindingId BindingId { get; }

        public override string ToString()
        {
            string binding = BindingId.IsValid ? " [" + BindingId.Value + "]" : string.Empty;
            return Severity + " " + Code + binding + ": " + Message;
        }
    }

    public static class EnvironmentPresentationDiagnosticCodes
    {
        public const string InvalidBindingId = "ENV-FRAME-001";
        public const string InvalidRevision = "ENV-FRAME-002";
        public const string InvalidDate = "ENV-FRAME-003";
        public const string InvalidTime = "ENV-FRAME-004";
        public const string InvalidClouds = "ENV-FRAME-005";
        public const string InvalidPrecipitation = "ENV-FRAME-006";
        public const string InvalidFog = "ENV-FRAME-007";
        public const string InvalidWind = "ENV-FRAME-008";
        public const string InvalidTransition = "ENV-FRAME-009";
        public const string InvalidQualityTier = "ENV-FRAME-010";
        public const string InvalidLightningRequest = "ENV-FRAME-011";
        public const string InvalidRefreshRequest = "ENV-FRAME-012";
        public const string DisabledFrameRequest = "ENV-FRAME-013";
        public const string InvalidExposure = "ENV-FRAME-014";
        public const string InvalidBindingDefinition = "ENV-BINDING-001";
        public const string DuplicateBindingDefinition = "ENV-BINDING-002";
        public const string InvalidPresetKind = "ENV-BINDING-003";
        public const string InvalidCapabilities = "ENV-BINDING-004";
    }
}
