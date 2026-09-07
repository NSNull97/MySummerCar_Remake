namespace MSC.Audio
{
    /// <summary>Stable, replacement-safe Satsuma engine feedback identity.</summary>
    public static class SatsumaEngineAudioIds
    {
        public static readonly AudioEventId KeyInserted = new("audio.event.vehicle.satsuma.key.inserted");
        public static readonly AudioEventId KeyRemoved = new("audio.event.vehicle.satsuma.key.removed");
        public static readonly AudioEventId StarterEngaged = new("audio.event.vehicle.satsuma.starter.engaged");
        public static readonly AudioEventId StarterLoop = new("audio.event.vehicle.satsuma.starter.loop");
        public static readonly AudioEventId EngineCaught = new("audio.event.vehicle.satsuma.engine.caught");
        public static readonly AudioEventId EngineThrottleLoop = new("audio.event.vehicle.satsuma.engine.throttle.loop");
        public static readonly AudioEventId EngineCoastLoop = new("audio.event.vehicle.satsuma.engine.coast.loop");
        public static readonly AudioEventId ExhaustLoop = new("audio.event.vehicle.satsuma.exhaust.loop");
        public static readonly AudioEventId BeltSqueal = new("audio.event.vehicle.satsuma.belt.squeal.loop");
        public static readonly AudioEventId Pinging = new("audio.event.vehicle.satsuma.engine.pinging.loop");
        public static readonly AudioEventId ValveTick = new("audio.event.vehicle.satsuma.valve.tick");
        public static readonly AudioEventId BearingKnock = new("audio.event.vehicle.satsuma.bearing.knock");
        public static readonly AudioEventId IntakeSpit = new("audio.event.vehicle.satsuma.intake.spit");
        public static readonly AudioEventId ExhaustBackfire = new("audio.event.vehicle.satsuma.exhaust.backfire");
        public static readonly AudioEventId FanStarted = new("audio.event.vehicle.satsuma.fan.started");
        public static readonly AudioEventId FanLoop = new("audio.event.vehicle.satsuma.fan.loop");
        public static readonly AudioEventId FanStopped = new("audio.event.vehicle.satsuma.fan.stopped");
        public static readonly AudioParameterId ThrottleGain = new("audio.parameter.vehicle.satsuma.throttle.gain");
        public static readonly AudioParameterId ThrottlePitch = new("audio.parameter.vehicle.satsuma.throttle.pitch");
        public static readonly AudioParameterId CoastGain = new("audio.parameter.vehicle.satsuma.coast.gain");
        public static readonly AudioParameterId CoastPitch = new("audio.parameter.vehicle.satsuma.coast.pitch");
        public static readonly AudioParameterId ExhaustGain = new("audio.parameter.vehicle.satsuma.exhaust.gain");
        public static readonly AudioParameterId ExhaustPitch = new("audio.parameter.vehicle.satsuma.exhaust.pitch");
        public static readonly AudioParameterId BeltGain = new("audio.parameter.vehicle.satsuma.belt.gain");
        public static readonly AudioParameterId BeltPitch = new("audio.parameter.vehicle.satsuma.belt.pitch");
        public static readonly AudioParameterId PingingGain = new("audio.parameter.vehicle.satsuma.pinging.gain");
        public static readonly AudioParameterId PingingPitch = new("audio.parameter.vehicle.satsuma.pinging.pitch");
    }
}
