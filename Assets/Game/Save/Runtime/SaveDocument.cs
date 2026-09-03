using System;

namespace MSC.Save
{
    [Serializable]
    public sealed class SaveDocument
    {
        public const int CurrentDocumentVersion = 16;
        public SaveHeader Header = new SaveHeader();
        public SaveMetadata Metadata = new SaveMetadata();
        public SaveDomainEnvelope[] Domains = Array.Empty<SaveDomainEnvelope>();

        public SaveDocument DeepClone()
        {
            SaveDomainEnvelope[] domains = Domains ?? Array.Empty<SaveDomainEnvelope>();
            SaveDomainEnvelope[] copy = new SaveDomainEnvelope[domains.Length];
            for (int index = 0; index < domains.Length; index++)
            {
                copy[index] = domains[index]?.DeepClone();
            }

            return new SaveDocument
            {
                Header = Header?.DeepClone(),
                Metadata = Metadata?.DeepClone(),
                Domains = copy,
            };
        }
    }

    [Serializable]
    public sealed class SaveHeader
    {
        public const string CurrentFormatId = "msc.native-save";
        public string FormatId = CurrentFormatId;
        public int DocumentVersion = SaveDocument.CurrentDocumentVersion;
        public string SaveId = string.Empty;
        public string SlotId = string.Empty;
        public string BuildId = string.Empty;
        public string CreatedUtc = string.Empty;
        public string UpdatedUtc = string.Empty;
        public string IntegritySha256 = string.Empty;

        public SaveHeader DeepClone()
        {
            return (SaveHeader)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class SaveMetadata
    {
        public string DisplayName = string.Empty;
        public double PlayTimeSeconds;
        public string GameTimestamp = string.Empty;
        public string LocationStableId = string.Empty;

        public SaveMetadata DeepClone()
        {
            return (SaveMetadata)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class SaveDomainEnvelope
    {
        public string DomainId = string.Empty;
        public int SchemaVersion;
        public bool Required = true;
        public string PayloadJson = string.Empty;

        public SaveDomainEnvelope DeepClone()
        {
            return (SaveDomainEnvelope)MemberwiseClone();
        }
    }
}
