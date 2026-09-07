namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Bounded pass entry point; never rebuilds the car or canonical map.</summary>
    public static class SatsumaLiveEnginePassAuthoring
    {
        public static void BuildAudioAndBelt()
        {
            Phase1SatsumaEngineAudioImporter.Build();
            Phase1UserSelectedAudioImporter.Build();
            Phase1SatsumaInstalledBeltAssets.RepairMissingCanonicalTexture();
        }
    }
}
