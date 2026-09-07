using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MSC.Bootstrap;
using MSC.Player;
using MSC.Vehicle;
using MSC.Vehicle.NWH;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed partial class CanonicalConsumableNativeSaveTests
    {
        // Separate process from isolated native fixtures: this is the real
        // populated Bootstrap, not an empty graphics-free physics benchmark.
        public static IEnumerator RunNativeRoadRenderAudit()
        {
            string[] args = Environment.GetCommandLineArgs();
            int arg = Array.IndexOf(args, "-liveEngineSavePath");
            if (arg < 0) Assert.Ignore("Opt-in rendered native audit requires -liveEngineSavePath.");
            string sourcePath = Path.GetFullPath(args[arg + 1]);
            byte[] source = File.ReadAllBytes(sourcePath);
            SaveDocument native = new SaveDocumentCodec().Deserialize(System.Text.Encoding.UTF8.GetString(source), true);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            int oldVsync = QualitySettings.vSyncCount, oldCap = Application.targetFrameRate;
            float oldTimeScale = Time.timeScale;
            RenderTexture target = null;
            Camera camera = null;
            var rows = new List<string> { "case,speedKph,frameMs,cpuMainMs,cpuRenderMs,gpuMs,physicsMs,poseSyncMs,scenes,streaming" };
            try
            {
                Time.timeScale = 1;
                yield return SceneManager.LoadSceneAsync("Assets/Game/Bootstrap/Bootstrap.unity", LoadSceneMode.Single);
                yield return null; // Bootstrap Start must construct its service bindings first.
                var installer = Object.FindAnyObjectByType<ProductionWorldStreamingInstaller>();
                Assert.That(installer, Is.Not.Null);
                var save = installer.NativeSaveSession;
                // Run the actual pending-load boot order (world support,
                // native restore, weather reveal), with storage replaced by a
                // read-only in-memory snapshot. No user slot can be written.
                typeof(SaveCoordinator).GetField("storage", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(save.SaveService, new RoadAuditReadOnlyStorage(native));
                save.GetType().GetField("pendingRestoreSlotId", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(save, native.Header.SlotId);
                Assert.That(installer.TryBeginGameplayPreparation(out string failure), Is.True, failure);
                float deadline = Time.realtimeSinceStartup + 180;
                while (!installer.IsGameplayPrepared && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(installer.IsGameplayPrepared, Is.True, installer.LastGameplayPreparationFailure);
                Assert.That(save.HasRestoredSave, Is.True, save.LastLoadFailure);
                Assert.That(installer.TryActivateGameplay(out failure), Is.True, failure);
                yield return null;
                var ui = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .First(component => component != null && component.GetType().FullName == "MSC.UI.Presentation.GameUiRoot");
                ui.GetType().GetMethod("EnterGameplay", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
                Time.timeScale = 0;
                var session = installer.SpawnedPlayer.GetComponent<SatsumaDrivingSessionController>();
                var body = session.Station.VehicleBody;
                var backend = body.GetComponent<NwhWheelPhysicsBackend>();
                var input = installer.SpawnedPlayer.GetComponent<PlayerInputRouter>();
                input.SetGameplayInputEnabled(false);
                Assert.That(session.TryRestoreDrivingState(PlayerDrivingSaveDto.Create(session.Station.StationId, 0,
                    new Vector3(-.282f, -.19307387f, -.06712156f), PlayerPosture.Crouch), out failure), Is.True, failure);
                body.GetComponent<VehicleSimulationHost>().enabled = false;
                body.GetComponent<SatsumaHandbrakeController>().TryRestore(new SatsumaHandbrakeSaveDto(), out _);
                foreach (var wheel in backend.Wheels) { wheel.MotorTorque = 0; wheel.BrakeTorque = 0; }
                camera = installer.SpawnedPlayer.GetComponentInChildren<Camera>(true);
                foreach (var other in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude))
                    if (other != camera && other.cameraType == CameraType.Game) other.enabled = false;
                target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.DefaultHDR);
                Assert.That(target.Create(), Is.True);
                camera.targetTexture = target; camera.enabled = true;
                QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
                Time.timeScale = 1;
                TestContext.WriteLine("ROAD_RENDER_CONTEXT GPU=" + SystemInfo.graphicsDeviceName + " quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] +
                    " nativeWrites=false populatedBootstrap=true render=1920x1080 engineDrive=false");
                using var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
                using var render = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);
                using var gpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GPU Frame Time", 1);
                using var physics = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics.Simulate", 1);
                using var poses = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "MSC.Vehicle.InstalledPoseSync", 1);
                foreach (float speed in new[] { 0f, 8.333f, 16.667f, 30.556f })
                {
                    Time.timeScale = 0;
                    // Same recorded, straight highway interval used by the
                    // contact audit; only this test owns the relocation.
                    Vector3 start = new Vector3(678.865051f, -1.052318f, -1095.95447f);
                    Vector3 forward = new Vector3(.5855997f, 0, -.8106004f);
                    RelocateRoadProbe(body, start + Vector3.up * .85f, Quaternion.LookRotation(forward));
                    body.GetComponent<MSC.Vehicle.Assembly.VehicleAssemblyController>().SynchronizeInstalledParts();
                    foreach (var wheel in backend.Wheels) { wheel.wheel.angularVelocity = 0; wheel.Initialize(); }
                    Physics.SyncTransforms(); session.RefreshSeatedPose();
                    yield return installer.WorldStreaming.RefreshNow();
                    Time.timeScale = 1;
                    for (int step = 0; step < 70; step++) yield return new WaitForFixedUpdate();
                    // Warm HDRP history/shaders with the full world visible.
                    for (int frame = 0; frame < 30; frame++) yield return null;
                    SetRoadCoastVelocity(body, forward * speed);
                    foreach (var wheel in backend.Wheels) wheel.wheel.angularVelocity = speed / wheel.Radius;
                    var frames = new List<float>();
                    double end = Time.timeAsDouble + 1.5;
                    while (Time.timeAsDouble < end)
                    {
                        yield return null;
                        frames.Add(Time.unscaledDeltaTime * 1000);
                        string Metric(ProfilerRecorder recorder, bool zeroUnavailable = false) => recorder.Valid && (!zeroUnavailable || recorder.LastValue > 0)
                            ? (recorder.LastValue * .000001).ToString("F4", CultureInfo.InvariantCulture) : "";
                        rows.Add(string.Join(",", "highway-" + speed.ToString("F3", CultureInfo.InvariantCulture),
                            (body.linearVelocity.magnitude * 3.6f).ToString("F3", CultureInfo.InvariantCulture),
                            (Time.unscaledDeltaTime * 1000).ToString("F3", CultureInfo.InvariantCulture), Metric(main), Metric(render), Metric(gpu, true), Metric(physics), Metric(poses),
                            installer.WorldStreaming.OwnedLoadedSceneCount, installer.WorldStreaming.IsStreaming));
                    }
                    frames.Sort();
                    TestContext.WriteLine("ROAD_RENDER_BAND initialKph=" + speed * 3.6f + " samples=" + frames.Count + " medianMs=" + frames[frames.Count / 2] +
                        " p95Ms=" + frames[Math.Min(frames.Count - 1, (int)(frames.Count * .95))] + " maxMs=" + frames.Last() +
                        " activeScenes=" + installer.WorldStreaming.OwnedLoadedSceneCount + " reportedStreamingSpeed=" + installer.WorldStreaming.ReportedFocusSpeedMetersPerSecond);
                }
                Directory.CreateDirectory("Logs/road-render-audit");
                File.WriteAllLines("Logs/road-render-audit/render-counters.csv", rows);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var screenshot = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); screenshot.Apply();
                File.WriteAllBytes("Logs/road-render-audit/highway.png", screenshot.EncodeToPNG());
                Object.Destroy(screenshot); RenderTexture.active = previous;
            }
            finally
            {
                if (camera != null) camera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                QualitySettings.vSyncCount = oldVsync; Application.targetFrameRate = oldCap; Time.timeScale = oldTimeScale;
                Assert.That(File.ReadAllBytes(sourcePath), Is.EqualTo(source), "Native source remains byte-for-byte untouched.");
            }
        }

        private sealed class RoadAuditReadOnlyStorage : ISaveStorage
        {
            private readonly SaveDocument snapshot;
            public RoadAuditReadOnlyStorage(SaveDocument document) => snapshot = document.DeepClone();
            public SaveWriteResult Write(string slotId, SaveDocument document) => throw new InvalidOperationException("Road audit storage is strictly read-only.");
            public SaveReadResult Read(string slotId, bool allowRecovery = true) =>
                new SaveReadResult(slotId, SaveReadStatus.Loaded, snapshot.DeepClone(), "road-audit-memory", Array.Empty<string>(), string.Empty);
            public IReadOnlyList<SaveSlotSummary> EnumerateSlots() => new[]
            {
                new SaveSlotSummary(snapshot.Header.SlotId, snapshot.Metadata, snapshot.Header.UpdatedUtc, true, "Read-only road audit snapshot")
            };
        }
    }
}
