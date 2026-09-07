using System.Text.Json.Nodes;

namespace MySummerRemake.SaveMaster.Core;

public enum ValidationSeverity { Warning, Error }

public sealed record ValidationIssue(ValidationSeverity Severity, string DomainId, string JsonPointer, string Message);
public sealed record SaveDomain(string Id, int SchemaVersion, bool Required, JsonNode Payload);
public sealed record SaveChange(string DomainId, string JsonPointer, string Before, string After);
public sealed record SaveEdit(string DomainId, string JsonPointer, string Text);
public sealed record SaveWriteResult(string Path, string? BackupPath, string Sha256);

public sealed class SaveConflictException : IOException
{
    public SaveConflictException(string message) : base(message) { }
}

public static class SaveMasterLimits
{
    public const int MaximumDocumentBytes = 16 * 1024 * 1024;
    public const int MaximumDomainPayloadBytes = 2 * 1024 * 1024;
    public const int MaximumDomainCount = 512;
    public const int MaximumJsonDepth = 64;
}
