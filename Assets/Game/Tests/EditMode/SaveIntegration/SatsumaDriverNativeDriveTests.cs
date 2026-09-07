using System;
using System.Collections;
using System.IO;
using System.Linq;
using MSC.Player;
using MSC.Vehicle;
using MSC.Vehicle.NWH;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed partial class CanonicalConsumableNativeSaveTests
    {
        // Shared Editor-native fixture invoked by the real PlayMode runner.
        public static IEnumerator RunNativeDriverDriveFlow()
        {
            SaveDocument native=null;
            WithReadOnlyNativeEngine(document=>native=document);
            float oldScale=Time.timeScale; Time.timeScale=1f;
            var oldUpdate=InputSystem.settings.updateMode;
            var oldBackground=InputSystem.settings.backgroundBehavior;
            var oldRouting=InputSystem.settings.editorInputBehaviorInPlayMode;
            Keyboard oldKeyboard=Keyboard.current; Mouse oldMouse=Mouse.current;
            InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            // A batch Editor is unfocused. Set routing before creating devices;
            // changing IgnoreFocus later does not revive an already-disabled keyboard.
            var keyboard=InputSystem.AddDevice<Keyboard>(); var mouse=InputSystem.AddDevice<Mouse>();
            Fixture f=null; DriverPlayerFixture player=null; GameObject floor=null;
            InputActionAsset playerActions=null,carActions=null;
            try
            {
                f=new Fixture("item.spark-plug","mount.satsuma.cylinder-head.spark-plug-1",usePreviewScene:false,allEngineConsumables:true);
                // Production restores into an already active canonical car.
                // Native joint actors must exist before the pose synchronizer.
                f.Assembly.transform.parent.gameObject.SetActive(true);
                RestoreEngineDocument(f,native);
                player=new DriverPlayerFixture(f);
                var host=f.Assembly.GetComponent<VehicleSimulationHost>();
                var router=f.Assembly.GetComponent<VehicleInputRouter>();
                var adapter=f.Assembly.GetComponent<SatsumaIgnitionInputAdapter>();
                var backend=f.Assembly.GetComponent<NwhWheelPhysicsBackend>();
                Rigidbody body=backend.Chassis;
                Assert.That(host.InputSource,Is.SameAs(adapter));
                Assert.That(host.State.FuelLiters,Is.GreaterThan(10),"Use native fuel; no fuel readiness override.");
                TestContext.WriteLine("FIRST_DRIVE_NATIVE_HYDRAULICS="+host.State.SatsumaOperating.BrakeFrontLiters+"/"+
                    host.State.SatsumaOperating.BrakeRearLiters+"/"+host.State.SatsumaOperating.ClutchLiters);
                Assert.That(host.State.SatsumaOperating.BrakeFrontLiters,Is.GreaterThan(.9f),"Prepare the selected native car's front hydraulics before this drive test.");
                Assert.That(host.State.SatsumaOperating.BrakeRearLiters,Is.GreaterThan(.9f));
                Assert.That(host.State.SatsumaOperating.ClutchLiters,Is.GreaterThan(.45f));
                TestContext.WriteLine("FIRST_DRIVE_FIXTURE nativeFuel="+host.State.FuelLiters+" nativeHydraulicsUsed=true addedFluid=0 nativeWrites=false");
                // Flat test pad under the saved car. No imposed body motion,
                // wheel velocity, torque source or auto-repair simulation flag.
                float floorY=backend.Wheels.Min(w=>w.transform.position.y-w.Radius-w.spring.maxLength)-.05f;
                floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(floor,f.Scene);
                floor.name="TEST ONLY first-drive flat pad";
                floor.transform.position=new Vector3(body.position.x,floorY-.5f,body.position.z);
                floor.transform.localScale=new Vector3(500,1,500);
                TestContext.WriteLine("FIRST_DRIVE_INITIAL body="+body.position+" floor="+floorY);
                foreach(var part in f.Assembly.AllRuntimeParts.Where(p=>!p.IsInstalled && !p.IsAssemblyRoot)) part.gameObject.SetActive(false);
                var input=player.Player.GetComponent<PlayerInputRouter>();
                playerActions=InputActionAsset.FromJson(input.InputActions.ToJson());
                carActions=InputActionAsset.FromJson(router.InputActions.ToJson());
                playerActions.devices=new InputDevice[]{keyboard,mouse}; carActions.devices=new InputDevice[]{keyboard,mouse};
                input.Configure(playerActions,player.Motor,player.Look,player.Player.GetComponent<PlayerInteractionController>());
                router.Configure(carActions);
                f.Assembly.transform.parent.gameObject.SetActive(true);
                player.Camera.enabled=false;
                var handbrake=f.Assembly.GetComponent<SatsumaHandbrakeController>();
                Assert.That(handbrake.TryRestore(new SatsumaHandbrakeSaveDto(),out _),Is.True);
                for(int i=0;i<60;i++) yield return new WaitForFixedUpdate();
                TestContext.WriteLine("FIRST_DRIVE_SETTLED contacts="+backend.Wheels.Count(w=>w.IsGrounded)+" speed="+body.linearVelocity.magnitude+" rpm="+host.State.EngineRpm+" body="+body.position+" floor="+floorY);
                foreach(var wheel in backend.Wheels) TestContext.WriteLine("FIRST_DRIVE_WHEEL enabled="+wheel.isActiveAndEnabled+" pos="+wheel.transform.position+" contactPos="+wheel.WheelPosition+" radius="+wheel.Radius+" spring="+wheel.SpringLength);
                Assert.That(backend.Wheels.Count(w=>w.IsGrounded),Is.EqualTo(4));
                player.Player.SetActive(true);
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(player.Motor.TryRestoreWorldPose(body.transform.TransformPoint(new Vector3(-.282f,-.19307387f,-.06712156f)),body.rotation,out _),Is.True);
                int enteredEdges=0; input.DrivingModeRequested+=()=>enteredEdges++;
                var drivingAction=playerActions.FindAction("Player/DrivingMode",true);
                TestContext.WriteLine("FIRST_DRIVE_ENTRY before can="+player.Session.CanEnter+" gate="+input.IsGameplayInputEnabled+" action="+drivingAction.enabled+" bindings="+drivingAction.bindings.Count+" inside="+player.Session.Station.ContainsPlayer(player.Capsule)+
                    " frame="+Time.frameCount+" keyboardAdded="+keyboard.added+" enabled="+keyboard.enabled+" current="+(keyboard==Keyboard.current)+" mode="+InputSystem.settings.updateMode+" background="+InputSystem.settings.backgroundBehavior+" routing="+InputSystem.settings.editorInputBehaviorInPlayMode);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(player.Session.IsDriving,Is.True,player.Session.LastFailure+" edges="+enteredEdges+" pressed="+keyboard.enterKey.isPressed+" action="+drivingAction.ReadValue<float>()+" gate="+input.IsGameplayInputEnabled+" can="+player.Session.CanEnter+" frame="+Time.frameCount+" added="+keyboard.added+" enabled="+keyboard.enabled);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                yield return new WaitForSecondsRealtime(.08f);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift,Key.W,Key.E));
                yield return new WaitForSecondsRealtime(.08f);
                for(int i=0;i<15;i++) yield return new WaitForFixedUpdate();
                Assert.That(host.State.SelectedGear,Is.EqualTo(1));
                Assert.That(host.LastInput.ClutchPedal01,Is.EqualTo(1));
                Assert.That(host.State.ClutchEngagement01,Is.LessThan(.01f));
                Vector3 start=body.position; Vector3 forward=Vector3.ProjectOnPlane(body.transform.forward,Vector3.up).normalized;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
                yield return new WaitForSecondsRealtime(.08f);
                for(int i=0;i<175;i++) yield return new WaitForFixedUpdate();
                float travelled=Vector3.Dot(body.position-start,forward); float speed=body.linearVelocity.magnitude;
                TestContext.WriteLine("FIRST_DRIVE_FORWARD metres="+travelled+" speed="+speed+" rpm="+host.State.EngineRpm+" torque="+host.State.DifferentialTorqueNewtonMeters);
                Assert.That(travelled,Is.GreaterThan(2f),"The native NWH car must actually move, not just show a selected gear.");
                Assert.That(speed,Is.GreaterThan(1f));
                Assert.That(host.LastInput.Throttle01,Is.EqualTo(1));
                Assert.That(player.Session.TryExitDriving(out _),Is.False);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift,Key.E));
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(host.State.SelectedGear,Is.EqualTo(2));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
                yield return new WaitForSecondsRealtime(.8f);
                Assert.That(host.State.EngineStatus,Is.EqualTo(VehicleEngineStatus.Running));
                TestContext.WriteLine("FIRST_DRIVE_SECOND speed="+body.linearVelocity.magnitude+" rpm="+host.State.EngineRpm);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.D));
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(backend.Wheels.Take(2).Any(w=>Mathf.Abs(w.SteerAngle)>1f),Is.True,"Steering intent must reach actual front wheel authority.");
                // Inspect the completed visual frame before Update removes
                // presentation offsets for the next assembly frame.
                yield return new WaitForFixedUpdate();
                Assert.That(f.Assembly.GetComponent<SatsumaCockpitSteeringPresenter>().AppliedAngleDegrees,Is.EqualTo(450f));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.S,Key.LeftShift));
                yield return new WaitForSecondsRealtime(.08f);
                for(int i=0;i<150;i++) yield return new WaitForFixedUpdate();
                TestContext.WriteLine("FIRST_DRIVE_BRAKED speed="+body.linearVelocity.magnitude+" brakeTorque="+host.State.BrakeTorqueNewtonMeters);
                Assert.That(backend.Wheels.All(w=>w.BrakeTorque>0),Is.True,"Both hydraulic circuits must reach actual wheel commands.");
                Assert.That(body.linearVelocity.magnitude,Is.LessThan(.15f));
                for(int gear=1;gear>=-1;gear--)
                {
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.S,Key.LeftShift,Key.Q));
                    yield return new WaitForSecondsRealtime(.08f);
                    Assert.That(host.State.SelectedGear,Is.EqualTo(gear));
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.S,Key.LeftShift));
                    yield return new WaitForSecondsRealtime(.08f);
                }
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift));
                yield return new WaitForSecondsRealtime(.3f);
                Vector3 reverseStart=body.position;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
                yield return new WaitForSecondsRealtime(1.5f);
                float reverseMetres=Vector3.Dot(body.position-reverseStart,forward);
                TestContext.WriteLine("FIRST_DRIVE_REVERSE signedMetres="+reverseMetres+" speed="+body.linearVelocity.magnitude);
                Assert.That(reverseMetres,Is.LessThan(-1f));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.S,Key.LeftShift));
                yield return new WaitForSecondsRealtime(2f);
                Assert.That(body.linearVelocity.magnitude,Is.LessThan(.15f));
                // Exercise the existing physical lever's hold contract. Its
                // mouse ray is covered by the independent cockpit tests.
                Assert.That(handbrake.TrySetHeldDirection(1,1f),Is.True);
                yield return new WaitForSecondsRealtime(.3f);
                handbrake.ReleaseHold();
                Assert.That(handbrake.BrakeInput01,Is.GreaterThan(.99f));
                // Hold the service brake on the driven front wheels, then dump
                // the clutch with reverse still selected. A rear-only parking
                // brake is not a reliable stationary load on this FWD car.
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.S));
                yield return new WaitForSecondsRealtime(3f);
                TestContext.WriteLine("FIRST_DRIVE_LOADED_STALL rpm="+host.State.EngineRpm+
                    " gear="+host.State.SelectedGear+" clutch="+host.LastInput.ClutchPedal01+
                    " brake="+host.LastInput.Brake01+" status="+host.State.EngineStatus);
                Assert.That(host.State.EngineStatus,Is.EqualTo(VehicleEngineStatus.Stalled));
                Assert.That(player.Session.TryExitDriving(out string failure),Is.True,failure);
                Assert.That(player.Motor.enabled && player.Capsule.enabled,Is.True);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                // Paused restore uses the real four-domain native registry;
                // the transaction must preserve the in-cabin walking release.
                input.SetGameplayInputEnabled(false);router.SetGameplayInputEnabled(false);
                Time.timeScale=0f;
                var registry=DriverRegistry(f,player.Participant);
                var codec=new SaveDocumentCodec();var parked=codec.Deserialize(codec.Serialize(NewDocument(registry.CaptureDomains())),true);
                var report=new UnresolvedContentReport();var prepared=registry.PrepareRestore(parked,report,f.Deferred);
                registry.ApplyRestore(prepared,report,f.Deferred);
                Assert.That(player.Session.IsDriving,Is.False);
                Assert.That(handbrake.BrakeInput01,Is.GreaterThan(.99f));
                Assert.That(router.IsDriverSessionActive,Is.False);
                Assert.That(f.Runtime.LoadedInstances.Count(),Is.EqualTo(9));
                TestContext.WriteLine("FIRST_DRIVE_ROUTE_OK forward=true gear2=true brake=true reverse=true handbrake=true releaseInCabin=true nativeRoundtrip=true nativeWrites=false");
                // Restoring must not resurrect the stalled engine.
                Time.timeScale=1f;
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(host.State.EngineStatus,Is.EqualTo(VehicleEngineStatus.Stalled));
                TestContext.WriteLine("FIRST_DRIVE_LOADED_STALL_OK rpm="+host.State.EngineRpm);
            }
            finally
            {
                player?.Dispose(); if(floor!=null) Object.DestroyImmediate(floor); f?.Dispose();
                if(playerActions!=null) Object.DestroyImmediate(playerActions); if(carActions!=null) Object.DestroyImmediate(carActions);
                InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
                if(oldKeyboard!=null && oldKeyboard.added) oldKeyboard.MakeCurrent(); if(oldMouse!=null && oldMouse.added) oldMouse.MakeCurrent();
                InputSystem.settings.updateMode=oldUpdate; InputSystem.settings.backgroundBehavior=oldBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode=oldRouting; Time.timeScale=oldScale;
            }
        }
    }

}
