using System;
using MSC.Audio;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    public enum SatsumaExhaustOutlet { Engine, Headers, Pipe, Muffler }

    /// <summary>Measured stock exhaust chain; no fastening or simulation authority.</summary>
    public static class SatsumaExhaustFeedbackRules
    {
        // Logic109210 reads Data.Installed, never Bolted. A downstream loose or
        // disconnected component cannot move the effective exhaust outlet.
        public static SatsumaExhaustOutlet SelectStock(bool headers, bool pipe, bool muffler) =>
            !headers ? SatsumaExhaustOutlet.Engine : !pipe ? SatsumaExhaustOutlet.Headers :
            !muffler ? SatsumaExhaustOutlet.Pipe : SatsumaExhaustOutlet.Muffler;

        public static float ThrottleVolume(SatsumaExhaustOutlet outlet) => outlet switch
        {
            SatsumaExhaustOutlet.Muffler => .5f,
            SatsumaExhaustOutlet.Pipe => .9f,
            _ => 1.5f,
        };
        public static float CoastVolume(SatsumaExhaustOutlet outlet) => outlet switch
        {
            SatsumaExhaustOutlet.Muffler => .5f,
            SatsumaExhaustOutlet.Pipe => .7f,
            _ => 1f,
        };
        public static float OutletVolume(SatsumaExhaustOutlet outlet) => outlet switch
        {
            SatsumaExhaustOutlet.Muffler => .2f,
            SatsumaExhaustOutlet.Pipe => .8f,
            _ => 1f,
        };
        public static float Pitch(float rpm) => .55f +
            (float.IsFinite(rpm) ? Mathf.Clamp(rpm, 0f, 16000f) : 0f) / 10000f;

        public static bool IsAudible(VehicleEngineStatus status, float rpm, bool installed,
            bool combustionRundownActive = true) =>
            installed && float.IsFinite(rpm) && rpm > 0f &&
            (status == VehicleEngineStatus.Running ||
             // Starter106807 enables Exhaust at successful catch/push-start,
             // disables in Wait after Stall engine's RPM100 +/-10 comparison.
             combustionRundownActive && (status == VehicleEngineStatus.Off || status == VehicleEngineStatus.Stalled) && rpm > 110f);
    }

    [DisallowMultipleComponent]
    public sealed class SatsumaEngineExhaustBinding : MonoBehaviour
    {
        [SerializeField] private PartInstance headers;
        [SerializeField] private PartInstance pipe;
        [SerializeField] private PartInstance muffler;
        [SerializeField] private AudioEmitterAuthoring emitter;
        [SerializeField] private Vector3[] chassisLocalOutletPositions = Array.Empty<Vector3>();
        private int installedMask = -1;
        public PartInstance Headers => headers;
        public PartInstance Pipe => pipe;
        public PartInstance Muffler => muffler;
        public AudioEmitterAuthoring Emitter => emitter;
        public Vector3[] ChassisLocalOutletPositions => chassisLocalOutletPositions;
        public SatsumaExhaustOutlet CurrentOutlet { get; private set; }
        public bool IsConfigured => headers != null && pipe != null && muffler != null &&
            emitter != null && emitter.AudioTransform != null && chassisLocalOutletPositions?.Length == 4;

        public void Configure(PartInstance stockHeaders, PartInstance stockPipe, PartInstance stockMuffler,
            AudioEmitterAuthoring outletEmitter, Vector3[] localOutletPositions)
        {
            if (stockHeaders == null || stockPipe == null || stockMuffler == null ||
                outletEmitter == null || outletEmitter.AudioTransform == null || localOutletPositions?.Length != 4)
                throw new ArgumentException("Explicit stock exhaust parts, emitter and four chassis-local outlets are required.");
            foreach (Vector3 point in localOutletPositions)
                if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))
                    throw new ArgumentException("Exhaust outlet positions must be finite.");
            headers = stockHeaders; pipe = stockPipe; muffler = stockMuffler; emitter = outletEmitter;
            chassisLocalOutletPositions = (Vector3[])localOutletPositions.Clone();
            Invalidate();
            RefreshConfiguration();
        }

        public void Invalidate() => installedMask = -1;

        public void RefreshConfiguration()
        {
            if (!IsConfigured) return;
            // Three cached explicit references, no hierarchy scans or allocation.
            // Also catches native restore and forced detach without event ordering assumptions.
            int mask = (headers.IsInstalled ? 1 : 0) | (pipe.IsInstalled ? 2 : 0) | (muffler.IsInstalled ? 4 : 0);
            if (mask == installedMask) return;
            installedMask = mask;
            CurrentOutlet = SatsumaExhaustFeedbackRules.SelectStock((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0);
            emitter.AudioTransform.localPosition = chassisLocalOutletPositions[(int)CurrentOutlet];
        }
    }
}
