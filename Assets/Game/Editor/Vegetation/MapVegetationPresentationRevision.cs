namespace MSC.Editor.Vegetation
{
    /// <summary>
    /// Compatibility entry points for the former prefab-instance presentation
    /// revision. Woody presentation is now generator-owned packed data, so a
    /// revision deterministically rebuilds the selected cell population instead
    /// of reading PrefabUtility links or walking one Transform per tree.
    /// </summary>
    public static class MapVegetationPresentationRevision
    {
        public const string ReportRoot =
            "Artifacts/VegetationRebuild/PresentationRevision";

        public static void PrepareBatch() =>
            MapVegetationRebuild.PrepareApprovedArtBatch();

        public static void RunPilotBatch() =>
            MapVegetationRebuild.RunPilotBatch();

        public static void RunAllBatch() =>
            MapVegetationRebuild.RunAllBatch();

        public static void ValidateAllBatch() =>
            MapVegetationRebuild.ValidateAllBatch();
    }
}
