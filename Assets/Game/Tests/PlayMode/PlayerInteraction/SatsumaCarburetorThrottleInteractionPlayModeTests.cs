using System;
using System.Collections;
using MSC.Core.Identity;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using MSC.Player;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.PlayerInteraction
{
    public sealed class SatsumaCarburetorThrottleInteractionPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator InstalledThrottleReceivesMousePressHoldReleaseThroughPlayerRouter()
        {
            using var rig = new Rig(installed: true);
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            yield return null;
            rig.RefreshAim();
            Assert.That(rig.Interaction.CurrentActionSnapshot.First.Binding,
                Is.EqualTo(InteractionActionBinding.Interact));
            Assert.That(rig.Interaction.CurrentActionSnapshot.First.Label,
                Is.EqualTo(rig.Throttle.InteractionPrompt));

            Press(mouse.leftButton);
            yield return null;
            rig.AssertOpen(expectedThrottle: 1f);
            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
                rig.AssertOpen(expectedThrottle: 1f);
            }

            Release(mouse.leftButton);
            yield return null;
            rig.AssertClosed();
        }

        [UnityTest]
        public IEnumerator LooseThrottleMovesThroughMouseInputWithoutRequestingEnginePower()
        {
            using var rig = new Rig(installed: false);
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            yield return null;
            rig.RefreshAim();
            Press(mouse.leftButton);
            yield return null;
            rig.AssertOpen(expectedThrottle: 0f);
            Release(mouse.leftButton);
            yield return null;
            rig.AssertClosed();
        }

        [UnityTest]
        public IEnumerator SecondaryButtonToolKeyAndScrollDoNotOpenThrottle()
        {
            using var rig = new Rig(installed: true);
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            yield return null;
            rig.RefreshAim();

            Press(mouse.rightButton);
            yield return null;
            rig.AssertClosed();
            Release(mouse.rightButton);
            Press(keyboard.fKey);
            yield return null;
            rig.AssertClosed();
            Release(keyboard.fKey);
            Set(mouse.scroll, new Vector2(0f, 120f));
            yield return null;
            rig.AssertClosed();

            // A selected wrench/screwdriver uses this existing dedicated ray
            // mode. The bare-hand throttle must not be a tool-mode target.
            rig.Query.SetScrollToolTargetsOnly(true);
            Assert.That(rig.Query.Query().IsValid, Is.False);
        }

        [UnityTest]
        public IEnumerator InstalledThrottleCancelsOnDistanceAndLifecycleLossWhileMouseRemainsHeld()
        {
            using var rig = new Rig(installed: true);
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            yield return null;
            rig.RefreshAim();
            Press(mouse.leftButton);
            yield return null;
            rig.AssertOpen(expectedThrottle: 1f);

            rig.Player.transform.position += Vector3.back * 3f;
            yield return null;
            rig.AssertClosed();

            Release(mouse.leftButton);
            yield return null;
            rig.Player.transform.localPosition = Vector3.zero;
            rig.RefreshAim();
            Press(mouse.leftButton);
            yield return null;
            rig.AssertOpen(expectedThrottle: 1f);
            rig.Part.RuntimeState.SetLoose(rig.Part.transform.position, rig.Part.transform.rotation);
            // The input consumer must cancel even before the next player Update.
            Assert.That(rig.Input.ConsumeFixedInput(0).Throttle01, Is.Zero);
            rig.AssertClosed();
            yield return null;
            rig.AssertClosed();
            Release(mouse.leftButton);
        }

        private sealed class Rig : IDisposable
        {
            private readonly GameObject root = new("Carburetor throttle mouse fixture");
            private readonly PartDefinition definition;
            private readonly InputActionAsset actions;
            private readonly Vector3 pivot = new(-0.05525875f, -0.021597866f, 0.012377917f);
            private readonly Vector3 restPosition = new(0f, -0.010466572f, -0.011340024f);
            private readonly Quaternion restRotation = new Quaternion(-0.31894323f, 0f, 0f, 0.9477738f).normalized;
            private readonly Renderer closedButterfly;
            private readonly Renderer openButterfly;
            public GameObject Player { get; }
            public PartInstance Part { get; }
            public AssemblyCarburetorThrottleTarget Throttle { get; }
            public PlayerInteractionController Interaction { get; }
            public RaycastInteractionCandidateSource Query { get; }
            public SatsumaIgnitionInputAdapter Input { get; }

            public Rig(bool installed)
            {
                root.SetActive(false);
                root.transform.SetPositionAndRotation(new Vector3(10f, 2f, 4f), Quaternion.Euler(0f, 23f, 0f));
                Player = Child("Player", root.transform);
                Transform anchor = Child("Carry anchor", Player.transform).transform;
                anchor.localPosition = Vector3.forward;
                var carry = Player.AddComponent<PhysicalCarryController>();
                carry.Configure(anchor, null);
                Query = Player.AddComponent<RaycastInteractionCandidateSource>();
                Query.Configure(Player.transform, 2.25f, ~0);
                Interaction = Player.AddComponent<PlayerInteractionController>();
                Interaction.Configure(Query, carry, Player.transform, ~0);

                GameObject partObject = Child("Carburetor", root.transform);
                partObject.transform.localPosition = Vector3.forward;
                partObject.transform.localRotation = Quaternion.Euler(12f, 35f, 5f);
                definition = ScriptableObject.CreateInstance<PartDefinition>();
                definition.Configure("vehicle.satsuma.part.carburetor", "Carburetor", PartCategory.Engine,
                    1f, null, Array.Empty<PartCompatibilityRule>());
                var identity = partObject.AddComponent<StableEntityIdAuthoring>();
                identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var body = partObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                var pickup = partObject.AddComponent<PhysicsPickupTarget>();
                pickup.Configure(body, identity, "Carburetor", 35f);
                Part = partObject.AddComponent<PartInstance>();
                Part.Configure(definition, identity, body, pickup, false, string.Empty);
                if (installed) Part.RuntimeState.SetInstalled("mount.satsuma.cylinder-head.carburetor", false);

                Transform linkage = Child("Explicit flattened linkage", partObject.transform).transform;
                linkage.localPosition = restPosition;
                linkage.localRotation = restRotation;
                linkage.gameObject.AddComponent<MeshRenderer>();
                closedButterfly = Child("Closed butterfly", partObject.transform).AddComponent<MeshRenderer>();
                openButterfly = Child("Open butterfly", partObject.transform).AddComponent<MeshRenderer>();
                GameObject control = Child("Throttle control", partObject.transform);
                var sphere = control.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.center = new Vector3(-0.06f, -0.02f, 0.012f);
                sphere.radius = 0.03f;
                Throttle = control.AddComponent<AssemblyCarburetorThrottleTarget>();
                Throttle.Configure(Part, linkage, closedButterfly, openButterfly, pivot, restPosition, restRotation);
                var host = control.AddComponent<InteractionTargetHost>();
                host.Configure(Throttle);
                host.ConfigureOutlineRenderers(linkage.GetComponent<Renderer>());
                host.ConfigureSelectionPriority(36);
                Input = root.AddComponent<SatsumaIgnitionInputAdapter>();
                Input.ConfigureCarburetorThrottle(Throttle);

                actions = ScriptableObject.CreateInstance<InputActionAsset>();
                var map = new InputActionMap("Player");
                actions.AddActionMap(map);
                map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
                map.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
                map.AddAction("RotateModifier", InputActionType.Value, "<Mouse>/scroll/y");
                string[] buttons = { "Crouch", "Run", "Jump", "Zoom", "ForwardLean", "Interact", "Drop",
                    "Place", "Throw", "RotateAxis", "ToolActivate", "Urinate", "Wave", "MiddleFinger",
                    "Swear", "AlternativeActions" };
                foreach (string button in buttons) map.AddAction(button, InputActionType.Button);
                map.FindAction("Interact").AddBinding("<Mouse>/leftButton");
                map.FindAction("Throw").AddBinding("<Mouse>/rightButton");
                map.FindAction("ToolActivate").AddBinding("<Keyboard>/f");
                var router = Player.AddComponent<PlayerInputRouter>();
                router.Configure(actions, null, null, Interaction);
                root.SetActive(true);
                RefreshAim();
            }

            public void RefreshAim()
            {
                SphereCollider sphere = Throttle.GetComponent<SphereCollider>();
                Player.transform.LookAt(sphere.transform.TransformPoint(sphere.center));
                Physics.SyncTransforms();
                Interaction.RefreshCandidate();
                Assert.That(Interaction.CurrentCandidate.TryGetCapability(out IContinuousContextInteractionTarget target), Is.True);
                Assert.That(target, Is.SameAs(Throttle));
            }

            public void AssertOpen(float expectedThrottle)
            {
                Assert.That(Throttle.IsHeld, Is.True);
                Quaternion delta = Quaternion.Euler(40f, 0f, 0f);
                Assert.That(Quaternion.Angle(Throttle.Linkage.rotation,
                    Part.transform.rotation * delta * restRotation), Is.LessThan(0.05f));
                Assert.That(Vector3.Distance(Throttle.Linkage.position,
                    Part.transform.TransformPoint(pivot + delta * (restPosition - pivot))), Is.LessThan(0.00001f));
                Assert.That(closedButterfly.enabled, Is.False);
                Assert.That(openButterfly.enabled, Is.True);
                Assert.That(Input.ConsumeFixedInput(0).Throttle01, Is.EqualTo(expectedThrottle));
                Assert.That(Input.ConsumeFixedInput(0).StarterRequested, Is.False);
            }

            public void AssertClosed()
            {
                Assert.That(Throttle.IsHeld, Is.False);
                Assert.That(Quaternion.Angle(Throttle.Linkage.rotation,
                    Part.transform.rotation * restRotation), Is.LessThan(0.05f));
                Assert.That(Vector3.Distance(Throttle.Linkage.position,
                    Part.transform.TransformPoint(restPosition)), Is.LessThan(0.00001f));
                Assert.That(closedButterfly.enabled, Is.True);
                Assert.That(openButterfly.enabled, Is.False);
                Assert.That(Input.ConsumeFixedInput(0).Throttle01, Is.Zero);
            }

            private static GameObject Child(string name, Transform parent)
            {
                var child = new GameObject(name);
                child.transform.SetParent(parent, false);
                return child;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(actions);
                Object.DestroyImmediate(definition);
            }
        }
    }
}
