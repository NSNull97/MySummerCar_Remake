using System;
using UnityEngine;

namespace MSC.Core.ReferenceCapture
{
    public enum ReferenceUnit
    {
        Unknown,
        Unitless,
        Millimeter,
        Centimeter,
        Meter,
        Kilometer,
        MeterPerUnityUnit,
        MeterPerSecond,
        KilometerPerHour,
        MeterPerSecondSquared,
        Gram,
        Kilogram,
        Second,
        Minute,
        Hour,
        Degree,
        Radian,
        RevolutionsPerMinute,
        Ratio,
        Decibel,
        Hertz,
        Liter,
        Celsius
    }

    public enum ReferenceConfidence { Unknown, Low, Medium, High, Exact }
    public enum ReferencePriority { P0, P1, P2, P3 }
    public enum ReferenceValidationStatus { Validated, NeedsReview, Missing, Rejected }
    public enum ReferenceRequirementStatus { Covered, Partial, Missing, Blocked }
    public enum ReferenceFixtureStatus { Ready, Partial, Missing, Blocked }

    public enum ReferenceCategory
    {
        WorldLandmarks,
        PlayerInteraction,
        VehicleGeometryAssembly,
        PowertrainVehicleBehavior,
        TimeWeatherEnvironment,
        Audio,
        UIState
    }

    public enum ReferenceCaptureMethod
    {
        SerializedDonorData,
        DecompiledConstant,
        DecompiledFormula,
        SceneTransform,
        AssetMetadata,
        ManualMeasurement,
        VideoTiming,
        ScreenshotMeasurement,
        RuntimeObservation,
        DerivedCalculation,
        Approximation,
        Unknown
    }

    public enum ReferenceValueKind { Numeric, Vector3, Curve, Enum, String, Observation }

    public enum ReferenceCoordinateSpace
    {
        NotApplicable,
        DonorWorld,
        RemakeWorld,
        DonorMeshLocal,
        RemakeMeshLocal,
        VehicleLocal,
        PlayerLocal,
        ScreenPixels,
        Normalized,
        Unknown
    }

    [Serializable]
    public sealed class ReferenceDatasetVersion
    {
        [SerializeField] private int schemaVersion = ReferenceCaptureDatabaseVersion.CurrentSchemaVersion;
        [SerializeField] private string datasetVersion = ReferenceCaptureDatabaseVersion.CurrentDatasetVersion;
        [SerializeField] private string createdUtc = string.Empty;
        [SerializeField] private string generator = string.Empty;

        public int SchemaVersion => schemaVersion;
        public string DatasetVersion => datasetVersion ?? string.Empty;
        public string CreatedUtc => createdUtc ?? string.Empty;
        public string Generator => generator ?? string.Empty;

        public static ReferenceDatasetVersion Create(int schema, string dataset, string created, string generatorId) =>
            new ReferenceDatasetVersion
            {
                schemaVersion = schema,
                datasetVersion = dataset ?? string.Empty,
                createdUtc = created ?? string.Empty,
                generator = generatorId ?? string.Empty
            };
    }

    [Serializable]
    public sealed class ReferenceCurveSample
    {
        [SerializeField] private float input;
        [SerializeField] private float output;

        public float Input => input;
        public float Output => output;
    }

    [Serializable]
    public sealed class ReferenceValue
    {
        [SerializeField] private string kind = nameof(ReferenceValueKind.Observation);
        [SerializeField] private float numericValue;
        [SerializeField] private Vector3 vectorValue;
        [SerializeField] private string textValue = string.Empty;
        [SerializeField] private ReferenceCurveSample[] curveSamples = Array.Empty<ReferenceCurveSample>();

        public string KindName => kind ?? string.Empty;
        public ReferenceValueKind Kind => ParseEnum(kind, ReferenceValueKind.Observation);
        public float NumericValue => numericValue;
        public Vector3 VectorValue => vectorValue;
        public string TextValue => textValue ?? string.Empty;
        public ReferenceCurveSample[] CurveSamples => curveSamples ?? Array.Empty<ReferenceCurveSample>();

        public static ReferenceValue Numeric(float value) => new ReferenceValue
        {
            kind = nameof(ReferenceValueKind.Numeric),
            numericValue = value
        };

        public static ReferenceValue Vector(Vector3 value) => new ReferenceValue
        {
            kind = nameof(ReferenceValueKind.Vector3),
            vectorValue = value
        };

        public static ReferenceValue Text(string value, ReferenceValueKind valueKind = ReferenceValueKind.String) =>
            new ReferenceValue { kind = valueKind.ToString(), textValue = value ?? string.Empty };

        private static T ParseEnum<T>(string value, T fallback) where T : struct =>
            Enum.TryParse(value, true, out T parsed) ? parsed : fallback;
    }

    [Serializable]
    public sealed class ReferenceTolerance
    {
        [SerializeField] private float absolute;
        [SerializeField] private float relativePercent;
        [SerializeField] private string unit = nameof(ReferenceUnit.Unitless);
        [SerializeField] private string notes = string.Empty;

        public float Absolute => absolute;
        public float RelativePercent => relativePercent;
        public string UnitName => unit ?? string.Empty;
        public ReferenceUnit Unit => Enum.TryParse(unit, true, out ReferenceUnit parsed) ? parsed : ReferenceUnit.Unknown;
        public string Notes => notes ?? string.Empty;

        public static ReferenceTolerance Create(float absoluteValue, float relative, ReferenceUnit toleranceUnit, string toleranceNotes = "") =>
            new ReferenceTolerance
            {
                absolute = absoluteValue,
                relativePercent = relative,
                unit = toleranceUnit.ToString(),
                notes = toleranceNotes ?? string.Empty
            };
    }

    [Serializable]
    public sealed class ReferenceRecord
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string category = nameof(ReferenceCategory.WorldLandmarks);
        [SerializeField] private string subcategory = string.Empty;
        [SerializeField] private string name = string.Empty;
        [SerializeField] private string priority = nameof(ReferencePriority.P2);
        [SerializeField] private string unit = nameof(ReferenceUnit.Unknown);
        [SerializeField] private string coordinateSpace = nameof(ReferenceCoordinateSpace.NotApplicable);
        [SerializeField] private string sourceId = string.Empty;
        [SerializeField] private string sourceLocator = string.Empty;
        [SerializeField] private string captureMethod = nameof(ReferenceCaptureMethod.Unknown);
        [SerializeField] private string captureDateUtc = string.Empty;
        [SerializeField] private string confidence = nameof(ReferenceConfidence.Unknown);
        [SerializeField] private ReferenceTolerance tolerance = new ReferenceTolerance();
        [SerializeField] private string rawObservation = string.Empty;
        [SerializeField] private ReferenceValue normalizedValue = new ReferenceValue();
        [SerializeField] private string notes = string.Empty;
        [SerializeField] private string[] evidenceIds = Array.Empty<string>();
        [SerializeField] private string[] dependencyRecordIds = Array.Empty<string>();
        [SerializeField] private string validationStatus = nameof(ReferenceValidationStatus.NeedsReview);

        public string StableId => stableId ?? string.Empty;
        public ReferenceCategory Category => ParseEnum(category, ReferenceCategory.WorldLandmarks);
        public string CategoryName => category ?? string.Empty;
        public string Subcategory => subcategory ?? string.Empty;
        public string Name => name ?? string.Empty;
        public ReferencePriority Priority => ParseEnum(priority, ReferencePriority.P2);
        public string PriorityName => priority ?? string.Empty;
        public ReferenceUnit Unit => ParseEnum(unit, ReferenceUnit.Unknown);
        public string UnitName => unit ?? string.Empty;
        public ReferenceCoordinateSpace CoordinateSpace => ParseEnum(coordinateSpace, ReferenceCoordinateSpace.Unknown);
        public string CoordinateSpaceName => coordinateSpace ?? string.Empty;
        public string SourceId => sourceId ?? string.Empty;
        public string SourceLocator => sourceLocator ?? string.Empty;
        public ReferenceCaptureMethod CaptureMethod => ParseEnum(captureMethod, ReferenceCaptureMethod.Unknown);
        public string CaptureMethodName => captureMethod ?? string.Empty;
        public string CaptureDateUtc => captureDateUtc ?? string.Empty;
        public ReferenceConfidence Confidence => ParseEnum(confidence, ReferenceConfidence.Unknown);
        public string ConfidenceName => confidence ?? string.Empty;
        public ReferenceTolerance Tolerance => tolerance ?? new ReferenceTolerance();
        public string RawObservation => rawObservation ?? string.Empty;
        public ReferenceValue NormalizedValue => normalizedValue ?? new ReferenceValue();
        public string Notes => notes ?? string.Empty;
        public string[] EvidenceIds => evidenceIds ?? Array.Empty<string>();
        public string[] DependencyRecordIds => dependencyRecordIds ?? Array.Empty<string>();
        public ReferenceValidationStatus ValidationStatus => ParseEnum(validationStatus, ReferenceValidationStatus.NeedsReview);
        public string ValidationStatusName => validationStatus ?? string.Empty;

        public static ReferenceRecord Create(
            string id,
            ReferenceCategory recordCategory,
            string recordSubcategory,
            string recordName,
            ReferencePriority recordPriority,
            ReferenceUnit recordUnit,
            ReferenceCoordinateSpace space,
            string recordSourceId,
            string locator,
            ReferenceCaptureMethod method,
            string dateUtc,
            ReferenceConfidence recordConfidence,
            ReferenceTolerance recordTolerance,
            string observation,
            ReferenceValue value,
            string recordNotes,
            string[] recordEvidenceIds,
            string[] dependencies,
            ReferenceValidationStatus status) => new ReferenceRecord
        {
            stableId = id ?? string.Empty,
            category = recordCategory.ToString(),
            subcategory = recordSubcategory ?? string.Empty,
            name = recordName ?? string.Empty,
            priority = recordPriority.ToString(),
            unit = recordUnit.ToString(),
            coordinateSpace = space.ToString(),
            sourceId = recordSourceId ?? string.Empty,
            sourceLocator = locator ?? string.Empty,
            captureMethod = method.ToString(),
            captureDateUtc = dateUtc ?? string.Empty,
            confidence = recordConfidence.ToString(),
            tolerance = recordTolerance ?? new ReferenceTolerance(),
            rawObservation = observation ?? string.Empty,
            normalizedValue = value ?? new ReferenceValue(),
            notes = recordNotes ?? string.Empty,
            evidenceIds = recordEvidenceIds ?? Array.Empty<string>(),
            dependencyRecordIds = dependencies ?? Array.Empty<string>(),
            validationStatus = status.ToString()
        };

        private static T ParseEnum<T>(string value, T fallback) where T : struct =>
            Enum.TryParse(value, true, out T parsed) ? parsed : fallback;
    }

    [Serializable]
    public sealed class MeasurementRecord
    {
        [SerializeField] private ReferenceRecord record = new ReferenceRecord();
        public ReferenceRecord Record => record ?? new ReferenceRecord();
        public static MeasurementRecord Create(ReferenceRecord commonRecord) => new MeasurementRecord { record = commonRecord };
    }

    [Serializable]
    public sealed class BehaviorFixture
    {
        [SerializeField] private ReferenceRecord record = new ReferenceRecord();
        [SerializeField] private string fixtureId = string.Empty;
        [SerializeField] private string status = nameof(ReferenceFixtureStatus.Missing);
        [SerializeField] private string[] preconditions = Array.Empty<string>();
        [SerializeField] private string[] inputConditions = Array.Empty<string>();
        [SerializeField] private string[] expectedObservations = Array.Empty<string>();
        [SerializeField] private string[] relatedRecordIds = Array.Empty<string>();
        [SerializeField] private string[] missingRequirementIds = Array.Empty<string>();
        [SerializeField] private int repeatedTrialsRequired;

        public ReferenceRecord Record => record ?? new ReferenceRecord();
        public string FixtureId => fixtureId ?? string.Empty;
        public ReferenceFixtureStatus Status => Enum.TryParse(status, true, out ReferenceFixtureStatus parsed) ? parsed : ReferenceFixtureStatus.Missing;
        public string[] Preconditions => preconditions ?? Array.Empty<string>();
        public string[] InputConditions => inputConditions ?? Array.Empty<string>();
        public string[] ExpectedObservations => expectedObservations ?? Array.Empty<string>();
        public string[] RelatedRecordIds => relatedRecordIds ?? Array.Empty<string>();
        public string[] MissingRequirementIds => missingRequirementIds ?? Array.Empty<string>();
        public int RepeatedTrialsRequired => repeatedTrialsRequired;
    }

    [Serializable]
    public sealed class ReferenceSourceRecord
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string sourceKind = string.Empty;
        [SerializeField] private string logicalPath = string.Empty;
        [SerializeField] private string sha256 = string.Empty;
        [SerializeField] private string donorBuildId = string.Empty;
        [SerializeField] private string donorUnityVersion = string.Empty;
        [SerializeField] private string notes = string.Empty;

        public string StableId => stableId ?? string.Empty;
        public string SourceKind => sourceKind ?? string.Empty;
        public string LogicalPath => logicalPath ?? string.Empty;
        public string Sha256 => sha256 ?? string.Empty;
        public string DonorBuildId => donorBuildId ?? string.Empty;
        public string DonorUnityVersion => donorUnityVersion ?? string.Empty;
        public string Notes => notes ?? string.Empty;
    }

    [Serializable]
    public sealed class ReferenceEvidenceRecord
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string sourceId = string.Empty;
        [SerializeField] private string rootKind = string.Empty;
        [SerializeField] private string logicalPath = string.Empty;
        [SerializeField] private string evidenceType = string.Empty;
        [SerializeField] private string sha256 = string.Empty;
        [SerializeField] private bool externalToGit;
        [SerializeField] private string notes = string.Empty;

        public string StableId => stableId ?? string.Empty;
        public string SourceId => sourceId ?? string.Empty;
        public string RootKind => rootKind ?? string.Empty;
        public string LogicalPath => logicalPath ?? string.Empty;
        public string EvidenceType => evidenceType ?? string.Empty;
        public string Sha256 => sha256 ?? string.Empty;
        public bool ExternalToGit => externalToGit;
        public string Notes => notes ?? string.Empty;

        public static ReferenceEvidenceRecord Create(
            string id,
            string recordSourceId,
            string evidenceRootKind,
            string path,
            string type,
            string hash,
            bool external,
            string evidenceNotes) => new ReferenceEvidenceRecord
        {
            stableId = id ?? string.Empty,
            sourceId = recordSourceId ?? string.Empty,
            rootKind = evidenceRootKind ?? string.Empty,
            logicalPath = path ?? string.Empty,
            evidenceType = type ?? string.Empty,
            sha256 = hash ?? string.Empty,
            externalToGit = external,
            notes = evidenceNotes ?? string.Empty
        };
    }

    [Serializable]
    public sealed class ReferenceRequirementRecord
    {
        [SerializeField] private string requirementId = string.Empty;
        [SerializeField] private string category = nameof(ReferenceCategory.WorldLandmarks);
        [SerializeField] private string name = string.Empty;
        [SerializeField] private string priority = nameof(ReferencePriority.P2);
        [SerializeField] private string fixtureId = string.Empty;
        [SerializeField] private string status = nameof(ReferenceRequirementStatus.Missing);
        [SerializeField] private string[] coveredByRecordIds = Array.Empty<string>();
        [SerializeField] private string notes = string.Empty;

        public string RequirementId => requirementId ?? string.Empty;
        public ReferenceCategory Category => Enum.TryParse(category, true, out ReferenceCategory parsed) ? parsed : ReferenceCategory.WorldLandmarks;
        public string CategoryName => category ?? string.Empty;
        public string Name => name ?? string.Empty;
        public ReferencePriority Priority => Enum.TryParse(priority, true, out ReferencePriority parsed) ? parsed : ReferencePriority.P2;
        public string PriorityName => priority ?? string.Empty;
        public string FixtureId => fixtureId ?? string.Empty;
        public ReferenceRequirementStatus Status => Enum.TryParse(status, true, out ReferenceRequirementStatus parsed) ? parsed : ReferenceRequirementStatus.Missing;
        public string StatusName => status ?? string.Empty;
        public string[] CoveredByRecordIds => coveredByRecordIds ?? Array.Empty<string>();
        public string Notes => notes ?? string.Empty;
    }

    [Serializable]
    public sealed class ReferenceTuningOverride
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string measuredRecordId = string.Empty;
        [SerializeField] private ReferenceValue tunedValue = new ReferenceValue();
        [SerializeField] private string unit = nameof(ReferenceUnit.Unknown);
        [SerializeField] private string rationale = string.Empty;
        [SerializeField] private string authoringSource = string.Empty;
        [SerializeField] private string updatedUtc = string.Empty;

        public string StableId => stableId ?? string.Empty;
        public string MeasuredRecordId => measuredRecordId ?? string.Empty;
        public ReferenceValue TunedValue => tunedValue ?? new ReferenceValue();
        public ReferenceUnit Unit => Enum.TryParse(unit, true, out ReferenceUnit parsed) ? parsed : ReferenceUnit.Unknown;
        public string UnitName => unit ?? string.Empty;
        public string Rationale => rationale ?? string.Empty;
        public string AuthoringSource => authoringSource ?? string.Empty;
        public string UpdatedUtc => updatedUtc ?? string.Empty;
    }
}
