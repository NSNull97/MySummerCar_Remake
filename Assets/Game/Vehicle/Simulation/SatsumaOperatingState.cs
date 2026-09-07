using System;

namespace MSC.Vehicle.Simulation
{
    /// <summary>Mutable solver-owned state. DTOs are snapshots, never the live authority.</summary>
    public sealed class SatsumaOperatingState
    {
        public float BrakeFrontLiters { get; internal set; }
        public float BrakeRearLiters { get; internal set; }
        public float ClutchLiters { get; internal set; }
        public float OilContaminationPercent { get; internal set; }
        public float OilPressureBar { get; internal set; }
        public float CoolantPressurePsi { get; internal set; }
        public float CrankingSeconds { get; internal set; }
        public bool RadiatorFanRunning { get; internal set; }
        public int OdometerTenKilometerUnits { get; private set; } = 10000;
        public double OdometerPartialMeters { get; private set; }
        public double OdometerKilometers => OdometerTenKilometerUnits * 10d + OdometerPartialMeters / 1000d;

        internal void AccumulateTravel(double meters)
        {
            if (!double.IsFinite(meters) || meters < 0d) throw new ArgumentOutOfRangeException(nameof(meters));
            double total = OdometerPartialMeters + meters;
            double completed = Math.Floor(total / 10000d);
            OdometerTenKilometerUnits = (int)Math.Min(99999d, OdometerTenKilometerUnits + completed);
            OdometerPartialMeters = total % 10000d;
        }

        public SatsumaOperatingSaveDto Capture() => new()
        {
            brakeFrontLiters = BrakeFrontLiters, brakeRearLiters = BrakeRearLiters,
            clutchLiters = ClutchLiters, oilContaminationPercent = OilContaminationPercent,
            oilPressureBar = OilPressureBar, coolantPressurePsi = CoolantPressurePsi,
            crankingSeconds = CrankingSeconds, radiatorFanRunning = RadiatorFanRunning,
            hasOdometerState = true, odometerTenKilometerUnits = OdometerTenKilometerUnits,
            odometerPartialMeters = OdometerPartialMeters,
        };

        internal static SatsumaOperatingState Restore(SatsumaOperatingSaveDto dto)
        {
            if (dto == null) return new SatsumaOperatingState();
            if (!dto.IsValid) throw new ArgumentException("Invalid Satsuma operating state.", nameof(dto));
            return new SatsumaOperatingState
            {
                BrakeFrontLiters = dto.brakeFrontLiters, BrakeRearLiters = dto.brakeRearLiters,
                ClutchLiters = dto.clutchLiters, OilContaminationPercent = dto.oilContaminationPercent,
                OilPressureBar = dto.oilPressureBar, CoolantPressurePsi = dto.coolantPressurePsi,
                CrankingSeconds = dto.crankingSeconds, RadiatorFanRunning = dto.radiatorFanRunning,
                OdometerTenKilometerUnits = dto.hasOdometerState ? dto.odometerTenKilometerUnits : 10000,
                OdometerPartialMeters = dto.hasOdometerState ? dto.odometerPartialMeters : 0d,
            };
        }

        public bool IsFinite => float.IsFinite(BrakeFrontLiters) && float.IsFinite(BrakeRearLiters) &&
            float.IsFinite(ClutchLiters) && float.IsFinite(OilContaminationPercent) &&
            float.IsFinite(OilPressureBar) && float.IsFinite(CoolantPressurePsi) && float.IsFinite(CrankingSeconds) &&
            double.IsFinite(OdometerPartialMeters);
    }
}
