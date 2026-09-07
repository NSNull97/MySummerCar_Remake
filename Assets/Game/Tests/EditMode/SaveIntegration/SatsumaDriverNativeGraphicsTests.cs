using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Player;
using MSC.Vehicle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed partial class CanonicalConsumableNativeSaveTests
    {
        [Test]
        public void NativeDriverEyeAnchorRendersActualStockCabinAndSteeredWheelInHdrp()
        {
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            WithReadOnlyNativeEngine(document=>
            {
                using var f=new Fixture("item.spark-plug","mount.satsuma.cylinder-head.spark-plug-1",usePreviewScene:false,allEngineConsumables:true,useCurrentEditorScene:true);
                RestoreEngineDocument(f,document);
                using var player=new DriverPlayerFixture(f);
                Assert.That(player.Session.TryRestoreDrivingState(PlayerDrivingSaveDto.Create(player.Session.Station.StationId,0,
                    new Vector3(-.282f,-.19307387f,-.06712156f),PlayerPosture.Crouch),out string failure),Is.True,failure);
                f.Assembly.transform.parent.gameObject.SetActive(true);
                var owned=new List<Object>(); var prior=RenderTexture.active;
                string folder="Logs/driver-station-20260907";Directory.CreateDirectory(folder);
                try
                {
                    foreach(var part in f.Assembly.AllRuntimeParts.Where(p=>!p.IsInstalled && !p.IsAssemblyRoot))
                        foreach(var renderer in part.GetComponentsInChildren<Renderer>(true)) renderer.enabled=false;
                    GameObject New(string label) {var go=new GameObject("TEST ONLY driver capture "+label);owned.Add(go);SceneManager.MoveGameObjectToScene(go,f.Scene);return go;}
                    var camera=New("camera").AddComponent<Camera>(); camera.CopyFrom(player.Camera);
                    camera.gameObject.AddComponent<HDAdditionalCameraData>(); camera.enabled=false;
                    camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.14f,.17f,.19f);
                    var volume=New("exposure").AddComponent<Volume>();volume.isGlobal=true;volume.priority=20000f;
                    var profile=ScriptableObject.CreateInstance<VolumeProfile>();owned.Add(profile);volume.sharedProfile=profile;
                    var exposure=profile.Add<Exposure>();exposure.mode.Override(ExposureMode.Fixed);exposure.fixedExposure.Override(11f);
                    foreach(var euler in new[]{new Vector3(40,-30,0),new Vector3(60,135,0)})
                    {var light=New("sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=16000f;light.transform.rotation=f.Assembly.transform.rotation*Quaternion.Euler(euler);}
                    var rt=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);owned.Add(rt);rt.Create();camera.targetTexture=rt;
                    var image=new Texture2D(1600,1000,TextureFormat.RGBA32,false);owned.Add(image);
                    var wheel=f.Assembly.GetComponent<SatsumaCockpitSteeringPresenter>();
                    void Frame(string name,float steer,float pitch)
                    {
                        Assert.That(player.Look.TryRestoreSaveState(FirstPersonLookSaveDto.Create(pitch),out _),Is.True);
                        player.Session.RefreshSeatedPose();
                        camera.transform.SetPositionAndRotation(player.Camera.transform.position,player.Camera.transform.rotation);
                        wheel.ApplyFrame(steer);
                        for(int i=0;i<4;i++) camera.Render();RenderTexture.active=rt;
                        image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());
                    }
                    Frame("driver-forward",0,0);
                    Frame("driver-dashboard",0,24);
                    Frame("driver-steered",.5f,24);
                    TestContext.WriteLine("DRIVER_HDRP_CAPTURE eye="+f.Assembly.transform.InverseTransformPoint(camera.transform.position)+" fov="+camera.fieldOfView+" near="+camera.nearClipPlane);
                    camera.targetTexture=null;
                }
                finally
                {
                    f.Assembly.GetComponent<SatsumaCockpitSteeringPresenter>().RestoreVisuals();RenderTexture.active=prior;
                    foreach(var value in owned.AsEnumerable().Reverse()) if(value!=null){if(value is RenderTexture rt)rt.Release();Object.DestroyImmediate(value);}
                }
            });
        }
    }
}
