#if UNITY_EDITOR
using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle.Diagnostics
{
    /// <summary>Editor smoke only: isolates ignition/simulation from NWH and scene physics. Never authored into a prefab.</summary>
    public sealed class SatsumaNativeSmokeWheelBackend : MonoBehaviour, IWheelPhysicsBackend
    {
        public int WheelCount { get; private set; }
        public float VehicleSpeedMetersPerSecond => 0f;
        public void Configure(int count) => WheelCount = count;
        public void Sample(float seconds, WheelPhysicsSample[] destination)
        { for (int i = 0; i < destination.Length; i++) destination[i] = WheelPhysicsSample.NoContact; }
        public void Apply(float seconds, WheelPhysicsCommand[] commands) { }
        public void Reset() { }
    }
}
#endif
