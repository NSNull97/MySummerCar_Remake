using System;
using System.Collections.Generic;

namespace MSC.Audio
{
    public enum AudioValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    public readonly struct AudioValidationIssue
    {
        public AudioValidationIssue(
            AudioValidationSeverity severity,
            string code,
            string message)
        {
            Severity = severity;
            Code = string.IsNullOrWhiteSpace(code) ? "AUDIO-UNKNOWN" : code.Trim();
            Message = message?.Trim() ?? string.Empty;
        }

        public AudioValidationSeverity Severity { get; }
        public string Code { get; }
        public string Message { get; }
    }

    public sealed class AudioValidationReport
    {
        private readonly List<AudioValidationIssue> issues =
            new List<AudioValidationIssue>();

        public IReadOnlyList<AudioValidationIssue> Issues => issues;
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public bool Passed => ErrorCount == 0;

        public void Add(
            AudioValidationSeverity severity,
            string code,
            string message)
        {
            issues.Add(new AudioValidationIssue(severity, code, message));
            if (severity == AudioValidationSeverity.Error)
            {
                ErrorCount++;
            }
            else if (severity == AudioValidationSeverity.Warning)
            {
                WarningCount++;
            }
        }
    }

    public static class AudioValidationService
    {
        public static AudioValidationReport Validate(
            AudioEventMap eventMap,
            AudioParameterMap parameterMap,
            IAudioBackend backend)
        {
            var report = new AudioValidationReport();
            ValidateEventMap(eventMap, report);
            ValidateParameterMap(parameterMap, report);
            ValidateBackend(backend, report);
            return report;
        }

        public static void ValidateEventMap(
            AudioEventMap eventMap,
            AudioValidationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (eventMap == null)
            {
                report.Add(
                    AudioValidationSeverity.Error,
                    "AUDIO-EVENT-MAP-MISSING",
                    "AudioEventMap is not assigned.");
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<AudioEventMapEntry> entries = eventMap.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                AudioEventMapEntry entry = entries[index];
                if (entry == null)
                {
                    report.Add(
                        AudioValidationSeverity.Error,
                        "AUDIO-EVENT-NULL",
                        $"Event map entry {index} is null.");
                    continue;
                }

                if (!entry.TryGetId(out _, out string idFailure))
                {
                    report.Add(
                        AudioValidationSeverity.Error,
                        "AUDIO-EVENT-ID",
                        $"Event map entry {index}: {idFailure}");
                    continue;
                }

                if (!ids.Add(entry.StableId))
                {
                    report.Add(
                        AudioValidationSeverity.Error,
                        "AUDIO-EVENT-DUPLICATE",
                        $"Duplicate AudioEventId '{entry.StableId}'.");
                }

                if (string.IsNullOrWhiteSpace(entry.BackendEventName))
                {
                    report.Add(
                        AudioValidationSeverity.Error,
                        "AUDIO-EVENT-BACKEND-NAME",
                        $"AudioEventId '{entry.StableId}' has no backend mapping.");
                }
            }
        }

        public static void ValidateParameterMap(
            AudioParameterMap parameterMap,
            AudioValidationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (parameterMap == null)
            {
                report.Add(
                    AudioValidationSeverity.Error,
                    "AUDIO-PARAMETER-MAP-MISSING",
                    "AudioParameterMap is not assigned.");
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyList<AudioParameterMapEntry> entries = parameterMap.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                AudioParameterMapEntry entry = entries[index];
                if (entry == null)
                {
                    report.Add(
                        AudioValidationSeverity.Error,
                        "AUDIO-PARAMETER-NULL",
                        $"Parameter map entry {index} is null.");
                    continue;
                }

                if (!entry.TryGetId(out _, out string idFailure))
                {
                    report.Add(
                        AudioValidationSeverity.Error,
                        "AUDIO-PARAMETER-ID",
                        $"Parameter map entry {index}: {idFailure}");
                    continue;
                }

                if (!ids.Add(entry.StableId))
                {
                    report.Add(
                        AudioValidationSeverity.Error,
                        "AUDIO-PARAMETER-DUPLICATE",
                        $"Duplicate AudioParameterId '{entry.StableId}'.");
                }

                if (string.IsNullOrWhiteSpace(entry.BackendParameterName))
                {
                    report.Add(
                        AudioValidationSeverity.Error,
                        "AUDIO-PARAMETER-BACKEND-NAME",
                        $"AudioParameterId '{entry.StableId}' has no backend mapping.");
                }

                if (!float.IsFinite(entry.MinimumValue) ||
                    !float.IsFinite(entry.MaximumValue) ||
                    entry.MaximumValue < entry.MinimumValue ||
                    entry.DefaultValue < entry.MinimumValue ||
                    entry.DefaultValue > entry.MaximumValue ||
                    !float.IsFinite(entry.UpdateDeadband) ||
                    entry.UpdateDeadband < 0f)
                {
                    report.Add(
                        AudioValidationSeverity.Error,
                        "AUDIO-PARAMETER-RANGE",
                        $"AudioParameterId '{entry.StableId}' has an invalid range/default/deadband.");
                }
            }
        }

        public static void ValidateBackend(
            IAudioBackend backend,
            AudioValidationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (backend == null)
            {
                report.Add(
                    AudioValidationSeverity.Error,
                    "AUDIO-BACKEND-MISSING",
                    "No audio backend is configured.");
                return;
            }

            if (!backend.IsReady)
            {
                report.Add(
                    AudioValidationSeverity.Error,
                    "AUDIO-BACKEND-NOT-READY",
                    string.IsNullOrWhiteSpace(backend.FailureReason)
                        ? $"Audio backend '{backend.BackendId}' is not ready."
                        : backend.FailureReason);
            }

            AudioRuntimeSnapshot snapshot = backend.CaptureSnapshot();
            for (int index = 0; index < snapshot.MissingBanks.Length; index++)
            {
                report.Add(
                    AudioValidationSeverity.Error,
                    "AUDIO-BANK-MISSING",
                    $"Required SoundBank is missing: {snapshot.MissingBanks[index]}.");
            }

            if (snapshot.IsFallback)
            {
                report.Add(
                    AudioValidationSeverity.Warning,
                    "AUDIO-FALLBACK-ACTIVE",
                    $"Fallback backend '{snapshot.BackendId}' is active.");
            }
        }
    }
}
