namespace MSC.LegacyImport.Editor.Ledger
{
    public sealed class PortingLedgerUpdatePlan
    {
        public PortingLedgerUpdatePlan(string updatedCsv, int addedCount, int updatedCount)
        {
            UpdatedCsv = updatedCsv;
            AddedCount = addedCount;
            UpdatedCount = updatedCount;
        }

        public string UpdatedCsv { get; }
        public int AddedCount { get; }
        public int UpdatedCount { get; }
        public bool HasChanges => AddedCount > 0 || UpdatedCount > 0;
    }
}
