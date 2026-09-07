using System.Collections;
using System.IO;
using MSC.Bootstrap;
using MSC.Player;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.VehicleAssembly
{
    public sealed class SatsumaDrivingSessionPlayModeTests
    {
        private GameObject player, car, floor;
        private FirstPersonMotor motor;
        private FirstPersonLook look;
        private PlayerInputRouter input;
        private VehicleInputRouter pedals;
        private SatsumaDrivingSessionController session;
        private SatsumaDriverStation station;
        private CharacterController capsule;
        private Camera camera;
        private Rigidbody body;
        private InputActionAsset playerActions, carActions;
        private Keyboard keyboard, oldKeyboard;
        private Mouse mouse, oldMouse;
        private InputSettings.UpdateMode oldUpdate;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldRouting;
        private float oldTimeScale;

        [SetUp]
        public void CreateFixture()
        {
            oldTimeScale = Time.timeScale; Time.timeScale = 1f;
            oldUpdate = InputSystem.settings.updateMode;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            oldKeyboard = Keyboard.current; oldMouse = Mouse.current;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            playerActions = InputActionAsset.FromJson(File.ReadAllText(Path.Combine(Application.dataPath,"Game/Player/Content/Input/M4_Player.inputactions")));
            carActions = InputActionAsset.FromJson(File.ReadAllText(Path.Combine(Application.dataPath,"Game/Vehicle/Content/Simulation/Input/M06_Vehicle.inputactions")));
            playerActions.devices = new InputDevice[] { keyboard, mouse };
            carActions.devices = new InputDevice[] { keyboard, mouse };
            car = new GameObject("TEST ONLY interior station car");
            body = car.AddComponent<Rigidbody>(); body.useGravity = false;
            var seat = Child(car.transform,"Seat",Vector3.zero).gameObject.AddComponent<PartInstance>();
            seat.RuntimeState.SetInstalled(SatsumaDriverStation.StockSeatMountId,false);
            var trigger = Child(car.transform,"Trigger",new Vector3(-.282f,.05692613f,-.06712156f)).gameObject.AddComponent<CapsuleCollider>();
            trigger.isTrigger = true; trigger.direction = 2; trigger.radius = .03f; trigger.height = .3f;
            Transform eyes = Child(car.transform,"Eyes",new Vector3(-.3000002f,.5608089f,.02000036f));
            station = car.AddComponent<SatsumaDriverStation>();
            station.Configure("323d9fece916469ea30c705ebfcf68df",trigger,eyes,seat,body);
            pedals = car.AddComponent<VehicleInputRouter>(); pedals.Configure(carActions);
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TEST ONLY cabin floor"; floor.transform.position = new Vector3(0,-.4f,0);
            floor.transform.localScale = new Vector3(10,.2f,10);
            player = new GameObject("TEST ONLY actual input player"); player.SetActive(false);
            capsule = player.AddComponent<CharacterController>();
            var lean = Child(player.transform,"Lean",Vector3.zero);
            var pitch = Child(lean,"Pitch",Vector3.up*.7f);
            camera = Child(pitch,"Camera",Vector3.zero).gameObject.AddComponent<Camera>(); camera.enabled = false;
            motor = player.AddComponent<FirstPersonMotor>(); motor.Configure(capsule,lean,pitch,null);
            look = player.AddComponent<FirstPersonLook>(); look.Configure(player.transform,pitch,false);
            var interaction = player.AddComponent<PlayerInteractionController>();
            input = player.AddComponent<PlayerInputRouter>(); input.Configure(playerActions,motor,look,interaction);
            session = player.AddComponent<SatsumaDrivingSessionController>();
            session.Initialize(input,motor,look,interaction,camera,station,pedals);
            player.SetActive(true);
            PlaceInside();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            Object.Destroy(player); yield return null;
            Object.Destroy(car); Object.Destroy(floor);
            Object.Destroy(playerActions); Object.Destroy(carActions);
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            if (oldKeyboard != null && oldKeyboard.added) oldKeyboard.MakeCurrent();
            if (oldMouse != null && oldMouse.added) oldMouse.MakeCurrent();
            InputSystem.settings.updateMode = oldUpdate;
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldRouting;
            Time.timeScale = oldTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator WalkingWCannotDrive_EnterInsideOnly_HeldEnterDoesNotBounce()
        {
            player.transform.position = new Vector3(-2,0,0);
            yield return Keys(Key.W,Key.Enter);
            Assert.That(session.IsDriving,Is.False);
            Assert.That(pedals.ConsumeFixedInput(0).Throttle01,Is.Zero);
            Assert.That(motor.enabled,Is.True);
            yield return Keys(Key.W);
            PlaceInside();
            yield return Keys(Key.W,Key.Enter);
            Assert.That(session.IsDriving,Is.True,session.LastFailure);
            Assert.That(motor.enabled || capsule.enabled || input.IsLocomotionInputEnabled,Is.False);
            yield return Keys(Key.W,Key.Enter);
            Assert.That(session.IsDriving,Is.True,"A held Enter is not repeated boarding/unboarding.");
            Assert.That(pedals.IsWaitingForNeutralControls,Is.True);
            Assert.That(pedals.ConsumeFixedInput(0).Throttle01,Is.Zero);
            yield return Keys();
            yield return Keys(Key.W,Key.LeftShift,Key.D,Key.E);
            var actual = pedals.ConsumeFixedInput(0);
            Assert.That(actual.Throttle01,Is.EqualTo(1));
            Assert.That(actual.ClutchPedal01,Is.EqualTo(1));
            Assert.That(actual.SteeringMinusOneToOne,Is.EqualTo(1));
            Assert.That(actual.GearChangeRequested,Is.True);
            Assert.That(actual.RequestedGear,Is.EqualTo(1));
            Assert.That(pedals.ConsumeFixedInput(1).GearChangeRequested,Is.False,"The gear edge is consumed once.");
            yield return Keys();
            yield return Keys(Key.S,Key.Q,Key.I,Key.Backspace);
            actual = pedals.ConsumeFixedInput(0);
            Assert.That(actual.Brake01,Is.EqualTo(1));
            Assert.That(actual.RequestedGear,Is.EqualTo(-1));
            Assert.That(actual.IgnitionOn || actual.StarterRequested || pedals.ConsumeResetRequest(),Is.False);
        }

        [UnityTest]
        public IEnumerator DebugExternalCameraDoesNotMovePlayerOrChangeDrivingSaveAndResetsOnExit()
        {
            yield return Keys(); PlaceInside();
            Assert.That(session.TryEnterDriving(out _), Is.True);
            var debugView = player.GetComponent<MSC.Bootstrap.Development.SatsumaDrivingDebugView>();
            Assert.That(debugView, Is.Not.Null);
            session.RefreshSeatedPose();
            Vector3 playerPosition = player.transform.position;
            Vector3 cameraLocal = camera.transform.localPosition;
            string saved = JsonUtility.ToJson(session.CaptureDrivingState());
            Vector3 carPosition = body.position;
            yield return Keys(Key.F9);
            Assert.That(debugView.ViewMode, Is.EqualTo(1));
            debugView.ApplyCameraPose();
            Assert.That(Vector3.Distance(camera.transform.position, station.DriverEyeAnchor.position), Is.GreaterThan(3));
            Assert.That(Vector3.Distance(player.transform.position, playerPosition), Is.LessThan(.0001f));
            Assert.That(body.position, Is.EqualTo(carPosition));
            Assert.That(JsonUtility.ToJson(session.CaptureDrivingState()), Is.EqualTo(saved));
            Assert.That(motor.enabled || capsule.enabled, Is.False);
            session.RefreshSeatedPose();
            Assert.That(Vector3.Distance(camera.transform.position, station.DriverEyeAnchor.position), Is.LessThan(.0001f));
            Assert.That(camera.transform.localPosition, Is.EqualTo(cameraLocal));
            debugView.SetViewMode(2); debugView.ApplyCameraPose();
            Assert.That(session.TryExitDriving(out string failure), Is.True, failure);
            Assert.That(debugView.IsCameraOverridden, Is.False);
            Assert.That(camera.transform.localPosition, Is.EqualTo(cameraLocal));
            yield return Keys();
            Assert.That(debugView.ViewMode, Is.Zero);
            Assert.That(motor.enabled && capsule.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator LookAndAnchorFollowMovingCar_ExitOnlyStoppedAndStillInsideCabin()
        {
            yield return Keys(); PlaceInside();
            Vector3 release = car.transform.InverseTransformPoint(player.transform.position);
            Assert.That(session.TryEnterDriving(out string failure),Is.True,failure);
            InputSystem.QueueStateEvent(mouse,new MouseState { delta = new Vector2(100,30) });
            yield return null; yield return null;
            Assert.That(Mathf.Abs(session.LocalYawDegrees),Is.GreaterThan(1));
            Assert.That(Mathf.Abs(look.PitchDegrees),Is.GreaterThan(1));
            body.position = new Vector3(5,2,7); body.rotation = Quaternion.Euler(10,80,5);
            body.linearVelocity = new Vector3(.2f,0,0);
            yield return null;
            session.RefreshSeatedPose();
            Assert.That(Vector3.Distance(camera.transform.position,station.DriverEyeAnchor.position),Is.LessThan(.0001f));
            Assert.That(session.TryExitDriving(out _),Is.False);
            body.linearVelocity = Vector3.zero;
            Assert.That(session.TryExitDriving(out failure),Is.True,failure);
            Assert.That(Vector3.Distance(player.transform.position,car.transform.TransformPoint(release)),Is.LessThan(.0001f));
            Assert.That(Vector3.Dot(player.transform.up,Vector3.up),Is.GreaterThan(.9999f));
            Assert.That(motor.enabled && capsule.enabled && input.IsLocomotionInputEnabled,Is.True);
            Assert.That(pedals.IsDriverSessionActive,Is.False);
        }

        [UnityTest]
        public IEnumerator PauseAndBothGateRestoreOrdersRequireNeutralBeforeResuming()
        {
            yield return Keys(); PlaceInside();
            Assert.That(session.TryEnterDriving(out _),Is.True);
            yield return Keys(); yield return Keys(Key.W);
            Assert.That(pedals.ConsumeFixedInput(0).Throttle01,Is.EqualTo(1));
            Time.timeScale = 0f;
            yield return Keys(Key.W,Key.Enter);
            Assert.That(session.IsDriving,Is.True);
            Assert.That(pedals.ConsumeFixedInput(0).Throttle01,Is.Zero);
            Time.timeScale = 1f;
            yield return Keys(Key.W);
            Assert.That(pedals.ConsumeFixedInput(0).Throttle01,Is.Zero);
            foreach (bool playerFirst in new[] {true,false})
            {
                input.SetGameplayInputEnabled(false); pedals.SetGameplayInputEnabled(false);
                yield return Keys(Key.W);
                if (playerFirst) input.SetGameplayInputEnabled(true); else pedals.SetGameplayInputEnabled(true);
                yield return Keys(Key.W);
                Assert.That(pedals.ConsumeFixedInput(0).Throttle01,Is.Zero);
                if (playerFirst) pedals.SetGameplayInputEnabled(true); else input.SetGameplayInputEnabled(true);
                yield return Keys(Key.W);
                Assert.That(pedals.ConsumeFixedInput(0).Throttle01,Is.Zero);
                yield return Keys(); yield return Keys(Key.W);
                Assert.That(pedals.ConsumeFixedInput(0).Throttle01,Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator SeatLossAndComponentDisableReleasePoseAndClearPedals()
        {
            yield return Keys(); PlaceInside();
            Assert.That(session.TryEnterDriving(out _),Is.True);
            yield return Keys(); yield return Keys(Key.W);
            station.DriverSeat.RuntimeState.SetLoose(Vector3.zero,Quaternion.identity);
            yield return null; yield return null;
            Assert.That(session.IsDriving,Is.False);
            Assert.That(motor.enabled && capsule.enabled,Is.True);
            Assert.That(pedals.ConsumeFixedInput(0).Throttle01,Is.Zero);
            station.DriverSeat.RuntimeState.SetInstalled(SatsumaDriverStation.StockSeatMountId,false);
            PlaceInside(); Assert.That(session.TryEnterDriving(out _),Is.True);
            session.enabled = false;
            Assert.That(session.IsDriving || pedals.IsDriverSessionActive,Is.False);
            Assert.That(motor.enabled && capsule.enabled && input.IsLocomotionInputEnabled,Is.True);
        }

        [UnityTest]
        public IEnumerator OldWalkingSaveReleasesDriver_NewDrivingSaveRestoresPoseWithoutHeldInputs()
        {
            yield return Keys(); PlaceInside();
            PlayerSaveDto walking = PlayerPersistence.Capture(motor,look);
            Assert.That(session.TryEnterDriving(out _),Is.True);
            look.ApplyRotationDegrees(new Vector2(45,12)); session.RefreshSeatedPose();
            PlayerSaveDto seated = JsonUtility.FromJson<PlayerSaveDto>(JsonUtility.ToJson(PlayerPersistence.Capture(motor,look)));
            yield return Keys(); yield return Keys(Key.W);
            Assert.That(PlayerPersistence.TryRestore(walking,motor,look,out string failure),Is.True,failure);
            Assert.That(session.IsDriving,Is.False);
            Assert.That(motor.enabled && capsule.enabled,Is.True);
            Assert.That(PlayerPersistence.TryRestore(seated,motor,look,out failure),Is.True,failure);
            Assert.That(session.IsDriving,Is.True);
            Assert.That(session.LocalYawDegrees,Is.EqualTo(45).Within(.001f));
            Assert.That(look.PitchDegrees,Is.EqualTo(-12).Within(.001f));
            Assert.That(Vector3.Distance(camera.transform.position,station.DriverEyeAnchor.position),Is.LessThan(.0001f));
            yield return Keys(Key.W);
            Assert.That(pedals.ConsumeFixedInput(0).Throttle01,Is.Zero);
            var invalid = PlayerDrivingSaveDto.Create("vehicle.satsuma.driver-station.unknown",0,Vector3.zero,PlayerPosture.Crouch);
            var invalidPlayer = PlayerSaveDto.Create(Vector3.one*50,Quaternion.identity,walking.Motor,walking.Look,invalid);
            Vector3 before = player.transform.position;
            Assert.That(PlayerPersistence.TryRestore(invalidPlayer,motor,look,out _),Is.False);
            Assert.That(session.IsDriving,Is.True);
            Assert.That(Vector3.Distance(player.transform.position,before),Is.LessThan(.0001f));
        }

        private void PlaceInside()
        {
            Assert.That(motor.TryRestoreWorldPose(car.transform.TransformPoint(new Vector3(-.282f,-.19307387f,-.06712156f)),car.transform.rotation,out _),Is.True);
            Assert.That(station.ContainsPlayer(capsule),Is.True);
        }
        private IEnumerator Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
            yield return null;
            yield return null;
        }
        private static Transform Child(Transform parent,string name,Vector3 position)
        {
            var child = new GameObject(name).transform; child.SetParent(parent,false); child.localPosition = position; return child;
        }
    }
}
