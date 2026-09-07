using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MSC.Core.Time;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed partial class CanonicalConsumableNativeSaveTests
    {
        [Test]
        public void ReadOnlyNativeCarHdrpVisualPacketCloseups()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Actual HDRP required.");
            WithReadOnlyNativeEngine(document =>
            {
                using var f = new Fixture("item.spark-plug", "mount.satsuma.cylinder-head.spark-plug-1",
                    usePreviewScene: false, allEngineConsumables: true, useCurrentEditorScene: true);
                RestoreEngineDocument(f, document); f.Assembly.transform.parent.gameObject.SetActive(true);
                var owned = new List<Object>(); RenderTexture prior = RenderTexture.active;
                string folder = "Logs/visual-packet-20260907/native-graphics"; Directory.CreateDirectory(folder);
                try
                {
                    var root = f.Assembly.transform;
                    foreach (var belt in f.Assembly.AllRuntimeParts.Select(p => p.GetComponent<AssemblyAlternatorBeltPresentation>()).Where(b => b != null)) belt.RefreshPresentation();
                    PartInstance Part(string suffix) => f.Assembly.Parts.Single(p => p.Definition.DefinitionId == "vehicle.satsuma.part." + suffix);
                    foreach (var part in f.Assembly.AllRuntimeParts.Where(p => !p.IsInstalled || p == Part("hood")))
                        foreach (var renderer in part.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                    GameObject New(string name)
                    { var go = new GameObject("TEST ONLY " + name); owned.Add(go); SceneManager.MoveGameObjectToScene(go, f.Scene); return go; }
                    var camera = New("native audit camera").AddComponent<Camera>(); camera.gameObject.AddComponent<HDAdditionalCameraData>();
                    camera.orthographic = true; camera.nearClipPlane = .005f; camera.farClipPlane = 5f;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f,.045f,.055f);
                    var volume = New("native audit exposure").AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 20000f;
                    var profile = ScriptableObject.CreateInstance<VolumeProfile>(); owned.Add(profile); volume.sharedProfile = profile;
                    var exposure = profile.Add<Exposure>(); exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(12f);
                    var indirect = profile.Add<IndirectLightingController>();
                    var lights = new List<Light>();
                    foreach (var euler in new[]{new Vector3(50,-20,0),new Vector3(65,145,0)})
                    { var light = New("native audit sun").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 18000f;
                        light.transform.rotation = root.rotation * Quaternion.Euler(euler); lights.Add(light); }
                    var target = new RenderTexture(1440,960,24,RenderTextureFormat.ARGB32); owned.Add(target); target.Create(); camera.targetTexture = target;
                    var image = new Texture2D(1440,960,TextureFormat.RGBA32,false); owned.Add(image);
                    void Frame(string name, Vector3 center, Vector3 offset, float size, Vector3 up)
                    {
                        camera.transform.position = center + root.TransformDirection(offset);
                        camera.transform.rotation = Quaternion.LookRotation(center - camera.transform.position,root.TransformDirection(up)); camera.orthographicSize = size;
                        for(int i=0;i<4;i++) camera.Render(); RenderTexture.active=target;
                        image.ReadPixels(new Rect(0,0,1440,960),0,0); image.Apply(); File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());
                    }
                    var flexible = f.Assembly.GetComponent<SatsumaFlexibleConnectionPresenter>(); flexible.RefreshOutputs();
                    Assert.That(flexible.OutOfRangeConnections,Is.Zero);
                    Frame("fuel-pump-filter",Part("fuel-pump").transform.position+root.up*.045f,new Vector3(.2f,.50f,.20f),.12f,Vector3.forward);
                    Frame("pump-belt-hoses",Part("water-pump-pulley").transform.position+root.up*.035f,new Vector3(-.32f,.6f,.13f),.225f,Vector3.up);
                    var junction=flexible.Bindings[2];
                    Frame("lower-hose-junction",junction.Target.TransformPoint(junction.TargetPoint),new Vector3(0,.4f,.15f),.095f,Vector3.up);
                    var instruments=f.Assembly.GetComponent<SatsumaInstrumentPresenter>(); var clock=new GameTimeService(); instruments.BindGameTime(clock); instruments.RefreshOutputs();
                    Vector3 cockpit=Vector3.zero; foreach(var n in instruments.Needles.Take(5)) cockpit+=n.Leaf.position*.2f;
                    Frame("cockpit-day",cockpit,new Vector3(0,.03f,-.16f),.15f,Vector3.up);
                    var controls=f.Assembly.GetComponent<SatsumaDashboardControlsController>();
                    if(controls.HeadlightsMode==SatsumaHeadlightsMode.Off) controls.TryCycleLights();
                    instruments.RefreshOutputs(); foreach(var light in lights) light.enabled=false;
                    exposure.fixedExposure.Override(4f); indirect.indirectDiffuseLightingMultiplier.Override(0); indirect.reflectionLightingMultiplier.Override(0);
                    Frame("cockpit-night",cockpit,new Vector3(0,.03f,-.16f),.15f,Vector3.up);
                    Debug.Log("SATSUMA_NATIVE_VISUAL_PACKET_GRAPHICS_OK frames=5 purchases=9 nativeWrites=false");
                    camera.targetTexture=null;
                }
                finally
                {
                    f.Assembly.GetComponent<SatsumaFlexibleConnectionPresenter>().ResetPresentation();
                    RenderTexture.active=prior;
                    foreach(var value in owned.AsEnumerable().Reverse()) if(value!=null)
                    { if(value is RenderTexture rt) rt.Release(); Object.DestroyImmediate(value); }
                }
            });
        }

        [Test]
        public void NativeOdometerSurvivesUnavailableOwnerAndFreshPresentationWithoutWritingPlayerSave()
        {
            WithReadOnlyNativeEngine(document =>
            {
                var codec = new SaveDocumentCodec(); SaveDocument captured;
                using (var source = EngineFixture())
                {
                    RestoreEngineDocument(source, document);
                    var host = source.Assembly.GetComponent<VehicleSimulationHost>();
                    Assert.That(host.State.SatsumaOperating.OdometerTenKilometerUnits, Is.EqualTo(10000), "Old native save default.");
                    var dto = host.State.CaptureDto(); dto.satsumaOperatingState.hasOdometerState = true;
                    dto.satsumaOperatingState.odometerTenKilometerUnits = 23456;
                    dto.satsumaOperatingState.odometerPartialMeters = 7890.125;
                    Assert.That(host.TryRestoreSimulationState(dto, out _), Is.True);
                    captured = codec.Deserialize(codec.Serialize(NewDocument(source.Registry.CaptureDomains())), true);
                }
                using (var deferred = EngineFixture(deferVehicle: true))
                {
                    RestoreEngineDocument(deferred, captured);
                    captured = codec.Deserialize(codec.Serialize(NewDocument(deferred.Registry.CaptureDomains())), true);
                    Assert.That(EngineVehicle(captured).simulation.satsumaOperatingState.odometerPartialMeters, Is.EqualTo(7890.125));
                }
                using var restored = EngineFixture(); RestoreEngineDocument(restored, captured);
                var state = restored.Assembly.GetComponent<VehicleSimulationHost>().State.SatsumaOperating;
                Assert.That(state.OdometerTenKilometerUnits, Is.EqualTo(23456));
                Assert.That(state.OdometerPartialMeters, Is.EqualTo(7890.125));
                Assert.That(restored.Assembly.GetComponent<SatsumaInstrumentPresenter>(), Is.Not.Null);
                Assert.That(restored.Assembly.GetComponent<SatsumaFlexibleConnectionPresenter>().Bindings, Has.Length.EqualTo(3));
            });
        }

        [Test]
        public void ReadOnlyCurrentNativeIdleTraceAndActualPurchasedVisualBindings()
        {
            WithReadOnlyNativeEngine(document =>
            {
                using var f = EngineFixture(); RestoreEngineDocument(f, document);
                f.Assembly.transform.parent.gameObject.SetActive(true);
                var host = f.Assembly.GetComponent<VehicleSimulationHost>();
                var vibration = f.Assembly.GetComponent<SatsumaEngineVisualVibration>();
                string assemblyBefore = JsonUtility.ToJson(f.Assembly.CaptureSaveData());
                var plug = f.Assembly.AllRuntimeParts.First(p => p.Definition.DefinitionId == SatsumaConsumableAssemblyRules.SparkPlugPartId);
                var renderer = plug.GetComponentsInChildren<Renderer>(true).First(r => r.gameObject.activeInHierarchy);
                var belt = f.Assembly.AllRuntimeParts.Select(p => p.GetComponent<AssemblyAlternatorBeltPresentation>()).First(b => b != null);
                belt.RefreshPresentation(); vibration.RebuildBindings();
                Assert.That(belt.InstalledRig, Is.Not.Null);
                Vector3 rigRest = belt.InstalledRig.transform.position;
                Vector3 rest = renderer.transform.position;
                vibration.ApplyFrame(VehicleEngineStatus.Running, 900, 0, .016f);
                Assert.That(Vector3.Distance(rest, renderer.transform.position), Is.GreaterThan(.000001f));
                Assert.That(Vector3.Distance(rigRest, belt.InstalledRig.transform.position), Is.GreaterThan(.000001f), "Actual two-bone belt rig, not just its renderer, must move.");
                Assert.That(JsonUtility.ToJson(f.Assembly.CaptureSaveData()), Is.EqualTo(assemblyBefore));
                vibration.ApplyFrame(VehicleEngineStatus.Off, 0, 0, .016f);
                var report = new StringBuilder("seconds,rpm,filteredThrottle,status,afr,temperature,fan\n");
                float min = float.MaxValue, max = 0;
                for (int i = 0; i < 3000; i++)
                {
                    host.Root.Tick(.02f, VehicleInputState.Neutral(true));
                    if (i >= 500) { min = Mathf.Min(min, host.State.EngineRpm); max = Mathf.Max(max, host.State.EngineRpm); }
                    if (i % 5 == 0) report.AppendLine(FormattableString.Invariant($"{i * .02f:F2},{host.State.EngineRpm:F3},{host.State.FilteredThrottle01:F3},{host.State.EngineStatus},{host.Root.SatsumaOperatingModel.LastPoint.AirFuelRatio:F3},{host.State.EngineTemperatureCelsius:F3},{host.State.SatsumaOperating.RadiatorFanRunning}"));
                }
                Directory.CreateDirectory("Logs/visual-packet-20260907");
                File.WriteAllText("Logs/visual-packet-20260907/current-native-zero-pedal-rpm.csv", report.ToString());
                Debug.Log("SATSUMA_CURRENT_NATIVE_IDLE_AUDIT min=" + min + " max=" + max + " nativeWrites=false");
            });
        }
    }
}
