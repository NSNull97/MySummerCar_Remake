using System;
using MSC.Audio;
using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>
    /// Read-only operating feedback. The containing presenter owns emitter
    /// registration; this object owns only its nine bounded playback handles.
    /// Cadence is integrated crank revolutions, never a random trouble timer.
    /// </summary>
    public sealed class SatsumaEngineSymptomAudio : IDisposable
    {
        private readonly IAudioBackend backend;
        private readonly IAudioEmitter engine, exhaust, intake, fan;
        private readonly IAudioEventHandle[] handles = new IAudioEventHandle[9];
        private readonly float[] retry = new float[9];
        private readonly float fanLeadSeconds;
        private double valvePhase, bearingPhase, intakePhase, exhaustPhase;
        private bool fanSeeded, previousFan;
        private float fanLeadRemaining;

        public SatsumaEngineSymptomAudio(IAudioBackend audio, IAudioEmitter engineSource,
            IAudioEmitter exhaustSource, IAudioEmitter intakeSource, IAudioEmitter fanSource)
        {
            backend = audio ?? throw new ArgumentNullException(nameof(audio));
            engine = engineSource ?? throw new ArgumentNullException(nameof(engineSource));
            exhaust = exhaustSource ?? engine;
            intake = intakeSource ?? engine;
            fan = fanSource ?? engine;
            fanLeadSeconds = backend is IAudioEventTimingSource timing &&
                timing.TryGetEventDurationSeconds(SatsumaEngineAudioIds.FanStarted, out float duration)
                ? duration : 2.52f; // Cooling106153, replaced by effective media timing when available.
        }

        public void Tick(in SatsumaEngineOperatingPoint point, VehicleEngineStatus status,
            bool blockInstalled, bool fanRunning, float rpm, float deltaSeconds)
        {
            if (!float.IsFinite(deltaSeconds) || deltaSeconds <= 0f || !float.IsFinite(rpm)) return;
            float dt = Mathf.Min(deltaSeconds, .25f);
            bool rotating = blockInstalled && rpm > 100f;
            bool firing = rotating && status == VehicleEngineStatus.Running && point.CombustionAllowed;
            float beltGain = rotating ? Mathf.Clamp01(point.BeltSqueal01) : 0f;
            float pingGain = firing ? Mathf.Clamp01(point.Knock01) : 0f;
            backend.SetParameter(SatsumaEngineAudioIds.BeltGain, beltGain, engine);
            backend.SetParameter(SatsumaEngineAudioIds.BeltPitch, 1f + Mathf.Max(0f, rpm) / 30000f, engine);
            backend.SetParameter(SatsumaEngineAudioIds.PingingGain, pingGain, engine);
            backend.SetParameter(SatsumaEngineAudioIds.PingingPitch, Mathf.Clamp(rpm / 4000f, .8f, 1.2f), engine);
            // One-shot pitch stays fixed in the library so its established
            // expiry contract remains correct; RPM controls impulse cadence.
            Loop(0, SatsumaEngineAudioIds.BeltSqueal, engine, beltGain > .001f, dt);
            Loop(1, SatsumaEngineAudioIds.Pinging, engine, pingGain > .001f, dt);

            double revolutions = firing ? Math.Max(0f, rpm) * dt / 60d : 0d;
            // One audible valve tick per four-stroke cycle. Pop/bearing sample
            // density is an explicit modern presentation calibration, not the
            // donor's randomized Wait loop and not a new simulation authority.
            Pulse(3, SatsumaEngineAudioIds.ValveTick, engine, ref valvePhase,
                revolutions / 2d, firing ? point.ValveNoise01 : 0f, dt);
            Pulse(4, SatsumaEngineAudioIds.BearingKnock, engine, ref bearingPhase,
                revolutions / 8d, firing ? point.BearingKnock01 : 0f, dt);
            Pulse(5, SatsumaEngineAudioIds.IntakeSpit, intake, ref intakePhase,
                revolutions / 24d, firing ? point.IntakeSpit01 : 0f, dt);
            Pulse(6, SatsumaEngineAudioIds.ExhaustBackfire, exhaust, ref exhaustPhase,
                revolutions / 24d, firing ? point.ExhaustBackfire01 : 0f, dt);

            if (!fanSeeded) { previousFan = fanRunning; fanSeeded = true; }
            else if (fanRunning != previousFan)
            {
                Stop(7); Stop(8);
                if (fanRunning)
                {
                    Play(7, SatsumaEngineAudioIds.FanStarted, fan, 1f);
                    fanLeadRemaining = fanLeadSeconds;
                }
                else
                {
                    Play(8, SatsumaEngineAudioIds.FanStopped, fan, 1f);
                    fanLeadRemaining = 0f;
                }
                previousFan = fanRunning;
            }
            fanLeadRemaining = Mathf.Max(0f, fanLeadRemaining - dt);
            Loop(2, SatsumaEngineAudioIds.FanLoop, fan, fanRunning && fanLeadRemaining <= 0f, dt);
            Reap(7); Reap(8);
        }

        private void Pulse(int slot, AudioEventId id, IAudioEmitter source, ref double phase,
            double cycles, float severity, float dt)
        {
            severity = Mathf.Clamp01(severity);
            if (!(severity > .001f)) { phase = 0d; retry[slot] = 0f; Stop(slot); return; }
            Reap(slot);
            retry[slot] = Mathf.Max(0f, retry[slot] - dt);
            phase += cycles * severity;
            if (phase < 1d) return;
            phase %= 1d; // No burst of deferred events after a slow frame or unavailable backend.
            if (retry[slot] > 0f) return;
            // A short impulse may replace its own preceding tail; never overlap
            // unbounded voices or retain completed per-revolution handles.
            Stop(slot);
            Play(slot, id, source, severity);
            if (handles[slot] == null || !handles[slot].IsValid) retry[slot] = 1f;
        }

        private void Loop(int slot, AudioEventId id, IAudioEmitter source, bool enabled, float dt)
        {
            if (!enabled) { Stop(slot); retry[slot] = 0f; return; }
            retry[slot] = Mathf.Max(0f, retry[slot] - dt);
            Reap(slot);
            if (handles[slot] != null || retry[slot] > 0f) return;
            Play(slot, id, source, 1f);
            retry[slot] = 1f;
        }

        private void Play(int slot, AudioEventId id, IAudioEmitter source, float gain)
        {
            if (!backend.IsReady) return;
            var request = new AudioEventRequest(id, source, volume01: gain, allowMultiple: false);
            handles[slot] = backend.PostEvent(in request) ?? AudioEventHandles.Invalid;
        }

        private void Reap(int slot)
        {
            if (handles[slot] != null && (!handles[slot].IsValid || !handles[slot].IsPlaying)) Stop(slot);
        }
        private void Stop(int slot)
        {
            handles[slot]?.Stop(); handles[slot]?.Dispose(); handles[slot] = null;
        }

        public void Reset()
        {
            for (int i = 0; i < handles.Length; i++) { Stop(i); retry[i] = 0f; }
            valvePhase = bearingPhase = intakePhase = exhaustPhase = 0d;
            fanSeeded = previousFan = false;
            fanLeadRemaining = 0f;
        }
        public void Dispose() => Reset();
    }
}
