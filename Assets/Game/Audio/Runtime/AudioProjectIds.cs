namespace MSC.Audio
{
    /// <summary>
    /// Project-owned stable IDs. Vendor object names are data in maps, never
    /// dependencies of gameplay code.
    /// </summary>
    public static class AudioProjectIds
    {
        public static class Events
        {
            public static readonly AudioEventId VehicleStarterEngaged = new AudioEventId("audio.event.vehicle.starter.engaged");
            public static readonly AudioEventId VehicleStarterDisengaged = new AudioEventId("audio.event.vehicle.starter.disengaged");
            public static readonly AudioEventId VehicleEngineStarted = new AudioEventId("audio.event.vehicle.engine.started");
            public static readonly AudioEventId VehicleEngineStopped = new AudioEventId("audio.event.vehicle.engine.stopped");
            public static readonly AudioEventId VehicleEngineStalled = new AudioEventId("audio.event.vehicle.engine.stalled");
            public static readonly AudioEventId VehicleReset = new AudioEventId("audio.event.vehicle.reset");
            public static readonly AudioEventId VehicleEngineIntake = new AudioEventId("audio.event.vehicle.engine.intake");
            public static readonly AudioEventId VehicleEngineExhaust = new AudioEventId("audio.event.vehicle.engine.exhaust");
            public static readonly AudioEventId VehicleEngineMechanical = new AudioEventId("audio.event.vehicle.engine.mechanical");
            public static readonly AudioEventId VehicleTransmission = new AudioEventId("audio.event.vehicle.transmission.loop");
            public static readonly AudioEventId VehicleBodyRattle = new AudioEventId("audio.event.vehicle.body.rattle");
            public static readonly AudioEventId VehicleSuspensionImpact = new AudioEventId("audio.event.vehicle.suspension.impact");
            public static readonly AudioEventId VehicleTireRoll = new AudioEventId("audio.event.vehicle.tire.roll");
            public static readonly AudioEventId VehicleTireSkid = new AudioEventId("audio.event.vehicle.tire.skid");
            public static readonly AudioEventId WeatherRainExterior = new AudioEventId("audio.event.weather.rain.exterior");
            public static readonly AudioEventId WeatherRainSheltered = new AudioEventId("audio.event.weather.rain.sheltered");
            public static readonly AudioEventId WeatherRainInterior = new AudioEventId("audio.event.weather.rain.interior");
            public static readonly AudioEventId WeatherRainLoop = WeatherRainExterior;
            public static readonly AudioEventId WeatherWind = new AudioEventId("audio.event.weather.wind");
            public static readonly AudioEventId WeatherWindLoop = WeatherWind;
            public static readonly AudioEventId WeatherThunder = new AudioEventId("audio.event.weather.thunder");
            public static readonly AudioEventId WeatherThunderDistant = new AudioEventId("audio.event.weather.thunder.distant");
            public static readonly AudioEventId WeatherThunderStrike = new AudioEventId("audio.event.weather.thunder.strike");
            public static readonly AudioEventId WorldForestAmbience = new AudioEventId("audio.event.world.forest.ambience");
            public static readonly AudioEventId WorldLakeAmbience = new AudioEventId("audio.event.world.lake.ambience");
            public static readonly AudioEventId WorldGarageRoomTone = new AudioEventId("audio.event.world.garage.roomtone");
            public static readonly AudioEventId WorldInteriorRoomTone = new AudioEventId("audio.event.world.interior.roomtone");
            public static readonly AudioEventId WorldDistantTraffic = new AudioEventId("audio.event.world.distant_traffic");
            public static readonly AudioEventId WorldBirds = new AudioEventId("audio.event.world.birds");
            public static readonly AudioEventId WorldBirdsMorning = new AudioEventId("audio.event.world.birds.morning");
            public static readonly AudioEventId WorldBirdsDay = new AudioEventId("audio.event.world.birds.day");
            public static readonly AudioEventId WorldBirdsEvening = new AudioEventId("audio.event.world.birds.evening");
            public static readonly AudioEventId WorldBirdsNight = new AudioEventId("audio.event.world.birds.night");
            public static readonly AudioEventId WorldBirdsSwamp = new AudioEventId("audio.event.world.birds.swamp");
            public static readonly AudioEventId WorldMeadow = new AudioEventId("audio.event.world.meadow");
            public static readonly AudioEventId WorldDog = new AudioEventId("audio.event.world.dog");
            public static readonly AudioEventId WorldChainsaw = new AudioEventId("audio.event.world.chainsaw");
            public static readonly AudioEventId WorldInsects = new AudioEventId("audio.event.world.insects");
            public static readonly AudioEventId WorldWindChime = new AudioEventId("audio.event.world.wind_chime");
            public static readonly AudioEventId WorldMosquito = new AudioEventId("audio.event.world.mosquito");
            public static readonly AudioEventId WorldFly = new AudioEventId("audio.event.world.fly");
            public static readonly AudioEventId WorldFlyVariant = new AudioEventId("audio.event.world.fly.variant");
            public static readonly AudioEventId WorldWasp = new AudioEventId("audio.event.world.wasp");
            public static readonly AudioEventId InteractionPickup = new AudioEventId("audio.event.interaction.pickup");
            public static readonly AudioEventId InteractionDrop = new AudioEventId("audio.event.interaction.drop");
            public static readonly AudioEventId InteractionThrow = new AudioEventId("audio.event.interaction.throw");
            public static readonly AudioEventId InteractionPlace = new AudioEventId("audio.event.interaction.place");
            public static readonly AudioEventId InteractionMountHandoff = new AudioEventId("audio.event.interaction.mount_handoff");
            public static readonly AudioEventId InteractionImpact = new AudioEventId("audio.event.interaction.impact");
            public static readonly AudioEventId InteractionToolUse = new AudioEventId("audio.event.interaction.tool.use");
            public static readonly AudioEventId InteractionFastenerInsert = new AudioEventId("audio.event.interaction.fastener.insert");
            public static readonly AudioEventId InteractionFastenerTighten = new AudioEventId("audio.event.interaction.fastener.tighten");
            public static readonly AudioEventId InteractionFastenerLoosen = new AudioEventId("audio.event.interaction.fastener.loosen");
            public static readonly AudioEventId InteractionPartInstall = new AudioEventId("audio.event.interaction.part.install");
            public static readonly AudioEventId InteractionPartRemove = new AudioEventId("audio.event.interaction.part.remove");
            public static readonly AudioEventId InteractionDoorOpen = new AudioEventId("audio.event.interaction.door.open");
            public static readonly AudioEventId InteractionDoorClose = new AudioEventId("audio.event.interaction.door.close");
            public static readonly AudioEventId VehicleBodyImpactLow01 = new AudioEventId("audio.event.vehicle.body.impact.low.01");
            public static readonly AudioEventId VehicleBodyImpactLow02 = new AudioEventId("audio.event.vehicle.body.impact.low.02");
            public static readonly AudioEventId VehicleBodyImpactHigh01 = new AudioEventId("audio.event.vehicle.body.impact.high.01");
            public static readonly AudioEventId VehicleBodyImpactHigh02 = new AudioEventId("audio.event.vehicle.body.impact.high.02");
            public static readonly AudioEventId InteractionGateOpen = new AudioEventId("audio.event.interaction.gate.open");
            public static readonly AudioEventId InteractionGateClose = new AudioEventId("audio.event.interaction.gate.close");
            public static readonly AudioEventId InteractionWindowOpen = new AudioEventId("audio.event.interaction.window.open");
            public static readonly AudioEventId InteractionWindowClose = new AudioEventId("audio.event.interaction.window.close");
            public static readonly AudioEventId BusDriverStuckCurse = new AudioEventId("audio.event.npc.latanen.bus-stuck-curse");
            public static readonly AudioEventId PlayerFootstep = new AudioEventId("audio.event.player.footstep");
            private static readonly AudioEventId[] PlayerSwearVariants =
            {
                new AudioEventId("audio.event.player.swear.01"),
                new AudioEventId("audio.event.player.swear.02"),
                new AudioEventId("audio.event.player.swear.03"),
                new AudioEventId("audio.event.player.swear.04"),
                new AudioEventId("audio.event.player.swear.05"),
                new AudioEventId("audio.event.player.swear.06"),
                new AudioEventId("audio.event.player.swear.07"),
                new AudioEventId("audio.event.player.swear.08"),
                new AudioEventId("audio.event.player.swear.09"),
                new AudioEventId("audio.event.player.swear.10"),
                new AudioEventId("audio.event.player.swear.11"),
                new AudioEventId("audio.event.player.swear.12"),
                new AudioEventId("audio.event.player.swear.13"),
                new AudioEventId("audio.event.player.swear.14"),
                new AudioEventId("audio.event.player.swear.15"),
                new AudioEventId("audio.event.player.swear.16"),
            };
            private static readonly AudioEventId[] PlayerMiddleFingerVariants =
            {
                new AudioEventId("audio.event.player.finger.01"),
                new AudioEventId("audio.event.player.finger.02"),
                new AudioEventId("audio.event.player.finger.03"),
                new AudioEventId("audio.event.player.finger.04"),
                new AudioEventId("audio.event.player.finger.05"),
                new AudioEventId("audio.event.player.finger.06"),
                new AudioEventId("audio.event.player.finger.07"),
                new AudioEventId("audio.event.player.finger.08"),
                new AudioEventId("audio.event.player.finger.09"),
                new AudioEventId("audio.event.player.finger.10"),
                new AudioEventId("audio.event.player.finger.11"),
            };
            public static readonly AudioEventId UiNavigate = new AudioEventId("audio.event.ui.navigate");
            public static readonly AudioEventId UiConfirm = new AudioEventId("audio.event.ui.confirm");
            public static readonly AudioEventId UiCancel = new AudioEventId("audio.event.ui.cancel");
            public static readonly AudioEventId UiSaveFeedback = new AudioEventId("audio.event.ui.save.feedback");
            public static readonly AudioEventId UiLoadFeedback = new AudioEventId("audio.event.ui.load.feedback");

            public static int PlayerSwearVariantCount =>
                PlayerSwearVariants.Length;

            public static AudioEventId GetPlayerSwearVariant(int zeroBasedIndex)
            {
                if (zeroBasedIndex < 0 ||
                    zeroBasedIndex >= PlayerSwearVariants.Length)
                {
                    throw new System.ArgumentOutOfRangeException(
                        nameof(zeroBasedIndex));
                }

                return PlayerSwearVariants[zeroBasedIndex];
            }

            public static int PlayerMiddleFingerVariantCount =>
                PlayerMiddleFingerVariants.Length;

            public static AudioEventId GetPlayerMiddleFingerVariant(
                int zeroBasedIndex)
            {
                if (zeroBasedIndex < 0 ||
                    zeroBasedIndex >= PlayerMiddleFingerVariants.Length)
                {
                    throw new System.ArgumentOutOfRangeException(
                        nameof(zeroBasedIndex));
                }

                return PlayerMiddleFingerVariants[zeroBasedIndex];
            }
        }

        public static class Parameters
        {
            public static readonly AudioParameterId Master = new AudioParameterId("audio.parameter.mixer.master");
            public static readonly AudioParameterId Vehicle = new AudioParameterId("audio.parameter.mixer.vehicle");
            public static readonly AudioParameterId Effects = new AudioParameterId("audio.parameter.mixer.effects");
            public static readonly AudioParameterId Ambience = new AudioParameterId("audio.parameter.mixer.ambience");
            public static readonly AudioParameterId Music = new AudioParameterId("audio.parameter.mixer.music");
            public static readonly AudioParameterId Ui = new AudioParameterId("audio.parameter.mixer.ui");
            public static readonly AudioParameterId VehicleRpm = new AudioParameterId("audio.parameter.vehicle.rpm");
            public static readonly AudioParameterId VehicleRpmNormalized = new AudioParameterId("audio.parameter.vehicle.rpm.normalized");
            public static readonly AudioParameterId VehicleEngineLoad = new AudioParameterId("audio.parameter.vehicle.engine_load");
            public static readonly AudioParameterId VehicleThrottle = new AudioParameterId("audio.parameter.vehicle.throttle");
            public static readonly AudioParameterId VehicleGear = new AudioParameterId("audio.parameter.vehicle.gear");
            public static readonly AudioParameterId VehicleClutchSlipRpm = new AudioParameterId("audio.parameter.vehicle.clutch_slip");
            public static readonly AudioParameterId VehicleSpeed = new AudioParameterId("audio.parameter.vehicle.speed");
            public static readonly AudioParameterId VehicleWheelSpeed = new AudioParameterId("audio.parameter.vehicle.wheel_speed_radps");
            public static readonly AudioParameterId VehicleWheelSlip = new AudioParameterId("audio.parameter.vehicle.wheel_slip");
            public static readonly AudioParameterId VehicleBrake = new AudioParameterId("audio.parameter.vehicle.brake");
            public static readonly AudioParameterId VehicleSuspensionImpact = new AudioParameterId("audio.parameter.vehicle.suspension_impact");
            public static readonly AudioParameterId VehicleBatteryVoltage = new AudioParameterId("audio.parameter.vehicle.battery_voltage");
            public static readonly AudioParameterId VehicleEngineTemperature = new AudioParameterId("audio.parameter.vehicle.engine_temperature");
            public static readonly AudioParameterId VehicleDamage = new AudioParameterId("audio.parameter.vehicle.damage");
            public static readonly AudioParameterId VehicleInteriorBlend = new AudioParameterId("audio.parameter.vehicle.interior_blend");
            public static readonly AudioParameterId VehicleDoorOpenness = new AudioParameterId("audio.parameter.vehicle.door_openness");
            public static readonly AudioParameterId VehicleWindowOpenness = new AudioParameterId("audio.parameter.vehicle.window_openness");
            public static readonly AudioParameterId WeatherPrecipitation = new AudioParameterId("audio.parameter.weather.precipitation");
            public static readonly AudioParameterId WeatherWind = new AudioParameterId("audio.parameter.weather.wind");
            public static readonly AudioParameterId WeatherThunderRisk = new AudioParameterId("audio.parameter.weather.thunder_risk");
            public static readonly AudioParameterId EnvironmentShelter = new AudioParameterId("audio.parameter.environment.shelter");
            public static readonly AudioParameterId EnvironmentTimeOfDay = new AudioParameterId("audio.parameter.environment.time_of_day");
            public static readonly AudioParameterId LightningIntensity = new AudioParameterId("audio.parameter.lightning.intensity");
            public static readonly AudioParameterId LightningDistance = new AudioParameterId("audio.parameter.lightning.distance");
            public static readonly AudioParameterId LightningDelay = new AudioParameterId("audio.parameter.lightning.delay");
            public static readonly AudioParameterId InteractionImpactIntensity = new AudioParameterId("audio.parameter.interaction.impact_intensity");
        }

        public static class Switches
        {
            public static readonly AudioSwitchId SurfaceGroup = new AudioSwitchId("audio.switch.vehicle.surface");
            public static readonly AudioSwitchId SurfaceUnknown = new AudioSwitchId("audio.switch.vehicle.surface.unknown");
            public static readonly AudioSwitchId SurfacePaved = new AudioSwitchId("audio.switch.vehicle.surface.paved");
            public static readonly AudioSwitchId SurfaceGravel = new AudioSwitchId("audio.switch.vehicle.surface.gravel");
            public static readonly AudioSwitchId SurfaceDirt = new AudioSwitchId("audio.switch.vehicle.surface.dirt");
            public static readonly AudioSwitchId SurfaceGrass = new AudioSwitchId("audio.switch.vehicle.surface.grass");
            public static readonly AudioSwitchId SurfaceMudWet = new AudioSwitchId("audio.switch.vehicle.surface.mud_wet");
            public static readonly AudioSwitchId WeatherPrecipitationGroup = new AudioSwitchId("audio.switch.weather.precipitation_type");
            public static readonly AudioSwitchId WeatherPrecipitationNone = new AudioSwitchId("audio.switch.weather.precipitation_type.none");
            public static readonly AudioSwitchId WeatherPrecipitationDrizzle = new AudioSwitchId("audio.switch.weather.precipitation_type.drizzle");
            public static readonly AudioSwitchId WeatherPrecipitationRain = new AudioSwitchId("audio.switch.weather.precipitation_type.rain");
            public static readonly AudioSwitchId WeatherPrecipitationSnow = new AudioSwitchId("audio.switch.weather.precipitation_type.snow");
            public static readonly AudioSwitchId InteractionMaterialGroup = new AudioSwitchId("audio.switch.interaction.material");
            public static readonly AudioSwitchId InteractionMaterialUnknown = new AudioSwitchId("audio.switch.interaction.material.unknown");
            public static readonly AudioSwitchId InteractionMaterialMetal = new AudioSwitchId("audio.switch.interaction.material.metal");
            public static readonly AudioSwitchId InteractionMaterialWood = new AudioSwitchId("audio.switch.interaction.material.wood");
            public static readonly AudioSwitchId InteractionMaterialPlastic = new AudioSwitchId("audio.switch.interaction.material.plastic");
            public static readonly AudioSwitchId InteractionMaterialGlass = new AudioSwitchId("audio.switch.interaction.material.glass");
            public static readonly AudioSwitchId InteractionMaterialConcrete = new AudioSwitchId("audio.switch.interaction.material.concrete");
            public static readonly AudioSwitchId InteractionMaterialFabric = new AudioSwitchId("audio.switch.interaction.material.fabric");
            public static readonly AudioSwitchId FootstepSurfaceGroup = new AudioSwitchId("audio.switch.footstep.surface");
            public static readonly AudioSwitchId FootstepSurfaceUnknown = new AudioSwitchId("audio.switch.footstep.surface.unknown");
            public static readonly AudioSwitchId FootstepSurfacePaved = new AudioSwitchId("audio.switch.footstep.surface.paved");
            public static readonly AudioSwitchId FootstepSurfaceGravel = new AudioSwitchId("audio.switch.footstep.surface.gravel");
            public static readonly AudioSwitchId FootstepSurfaceDirt = new AudioSwitchId("audio.switch.footstep.surface.dirt");
            public static readonly AudioSwitchId FootstepSurfaceGrass = new AudioSwitchId("audio.switch.footstep.surface.grass");
            public static readonly AudioSwitchId FootstepSurfaceWood = new AudioSwitchId("audio.switch.footstep.surface.wood");
            public static readonly AudioSwitchId FootstepSurfaceConcrete = new AudioSwitchId("audio.switch.footstep.surface.concrete");
            public static readonly AudioSwitchId FootstepSurfaceMetal = new AudioSwitchId("audio.switch.footstep.surface.metal");
            public static readonly AudioSwitchId FootstepSurfaceWet = new AudioSwitchId("audio.switch.footstep.surface.wet");
        }

        public static class States
        {
            public static readonly AudioStateId EnvironmentGroup = new AudioStateId("audio.state.environment");
            public static readonly AudioStateId EnvironmentExterior = new AudioStateId("audio.state.environment.exterior");
            public static readonly AudioStateId EnvironmentSheltered = new AudioStateId("audio.state.environment.sheltered");
            public static readonly AudioStateId EnvironmentInterior = new AudioStateId("audio.state.environment.interior");
            public static readonly AudioStateId EnvironmentVehicleInterior = new AudioStateId("audio.state.environment.vehicle_interior");
            public static readonly AudioStateId DayPhaseGroup = new AudioStateId("audio.state.day_phase");
            public static readonly AudioStateId DayPhaseDawn = new AudioStateId("audio.state.day_phase.dawn");
            public static readonly AudioStateId DayPhaseDay = new AudioStateId("audio.state.day_phase.day");
            public static readonly AudioStateId DayPhaseEvening = new AudioStateId("audio.state.day_phase.evening");
            public static readonly AudioStateId DayPhaseNight = new AudioStateId("audio.state.day_phase.night");
            public static readonly AudioStateId VehicleEngineGroup = new AudioStateId("audio.state.vehicle.engine");
            public static readonly AudioStateId VehicleEngineOff = new AudioStateId("audio.state.vehicle.engine.off");
            public static readonly AudioStateId VehicleEngineCranking = new AudioStateId("audio.state.vehicle.engine.cranking");
            public static readonly AudioStateId VehicleEngineRunning = new AudioStateId("audio.state.vehicle.engine.running");
            public static readonly AudioStateId VehicleEngineStalled = new AudioStateId("audio.state.vehicle.engine.stalled");
        }
    }
}
