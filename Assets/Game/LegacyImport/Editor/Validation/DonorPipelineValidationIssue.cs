namespace MSC.LegacyImport.Editor.Validation
{
    public enum DonorPipelineValidationSeverity
    {
        Warning,
        Error
    }

    public sealed class DonorPipelineValidationIssue
    {
        public DonorPipelineValidationIssue(
            DonorPipelineValidationSeverity severity,
            string code,
            string message,
            string assetPath = "")
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
        }

        public DonorPipelineValidationSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
        public string AssetPath { get; }
    }
}
