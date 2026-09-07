using System;

namespace MSC.Vehicle
{
    [Serializable]
    public sealed class SatsumaFuelLineConnectionSaveDto
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public int stage;
        public bool isBolted;

        public bool TryValidate(out string failure)
        {
            // Intermediate stages retain the history of reaching either end.
            if (schemaVersion != CurrentSchemaVersion || stage < 0 ||
                stage > SatsumaFuelLineConnection.MaximumStage ||
                stage == 0 && isBolted ||
                stage == SatsumaFuelLineConnection.MaximumStage && !isBolted)
            {
                failure = "Satsuma fuel-line connection payload is invalid.";
                return false;
            }
            failure = string.Empty;
            return true;
        }
    }
}
