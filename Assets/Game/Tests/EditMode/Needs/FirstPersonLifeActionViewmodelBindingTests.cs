using System.Collections.Generic;
using System.Linq;
using MSC.Needs;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Needs.Tests.EditMode
{
    public sealed class FirstPersonLifeActionViewmodelBindingTests
    {
        private GameObject root;
        private readonly List<AnimationClip> clips = new List<AnimationClip>();

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Viewmodel Binding Test");
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }

            foreach (AnimationClip clip in clips)
            {
                if (clip != null)
                {
                    Object.DestroyImmediate(clip);
                }
            }

            clips.Clear();
        }

        [Test]
        public void ConfiguredBinding_PlaysAndResetsReviewedVisuals()
        {
            FirstPersonLifeActionViewmodelBinding binding =
                root.AddComponent<FirstPersonLifeActionViewmodelBinding>();
            binding.ConfigureMetadata(
                FirstPersonLifeActionViewmodelBinding.ExpectedBindingId,
                FirstPersonLifeActionViewmodelBinding.ExpectedReplacementKey);

            (GameObject drinkRoot, Animation drinkAnimation) =
                CreateAnimatedRoot("Drink");
            AnimationClip drink = CreateClip("drink", 0.5f);
            Transform drinkGrip = new GameObject("Drink Grip").transform;
            drinkGrip.SetParent(drinkRoot.transform, false);
            binding.ConfigureDrink(
                drinkRoot,
                drinkAnimation,
                drink,
                CreateClip("drink-short", 0.2f),
                CreateClip("drink-throw", 0.2f),
                CreateClip("drink-spray", 0.2f),
                drinkGrip);

            (GameObject smokeRoot, Animation smokeAnimation) =
                CreateAnimatedRoot("Smoke");
            Transform cigaretteGrip = new GameObject("Cigarette Grip").transform;
            cigaretteGrip.SetParent(smokeRoot.transform, false);
            binding.ConfigureSmoke(
                smokeRoot,
                smokeAnimation,
                CreateClip("smoke-in", 0.1f),
                CreateClip("smoke-light", 0.1f),
                CreateClip("smoke-out", 0.1f),
                CreateClip("smoke-put-off", 0.1f),
                CreateClip("smoke-reset", 0.1f),
                cigaretteGrip);

            (GameObject helloRoot, Animation helloAnimation) =
                CreateAnimatedRoot("Hello");
            binding.ConfigureHello(
                helloRoot,
                helloAnimation,
                CreateClip("hello", 0.1f));

            (GameObject middleFingerRoot, Animation middleFingerAnimation) =
                CreateAnimatedRoot("Middle Finger");
            binding.ConfigureMiddleFinger(
                middleFingerRoot,
                middleFingerAnimation,
                CreateClip("middle-finger", 0.1f));

            (GameObject pushRoot, Animation pushAnimation) =
                CreateAnimatedRoot("Push");
            binding.ConfigurePush(
                pushRoot,
                pushAnimation,
                CreateClip("push-on", 0.1f),
                CreateClip("push-off", 0.1f));

            (GameObject fistRoot, Animation fistAnimation) =
                CreateAnimatedRoot("Fist");
            binding.ConfigureFist(
                fistRoot,
                fistAnimation,
                CreateClip("fist", 0.1f));

            Assert.That(binding.IsConfigured, Is.True);
            Assert.That(binding.DrinkGripAnchor, Is.SameAs(drinkGrip));
            Assert.That(
                binding.CigaretteGripAnchor,
                Is.SameAs(cigaretteGrip));
            Assert.That(binding.ShowDrinkReady(), Is.True);
            Assert.That(binding.ActiveVisual,
                Is.EqualTo(FirstPersonLifeActionVisual.Drink));
            Assert.That(drinkRoot.activeSelf, Is.True);

            Assert.That(
                binding.Play(FirstPersonLifeActionVisual.Drink),
                Is.True);
            Assert.That(binding.ActiveVisual,
                Is.EqualTo(FirstPersonLifeActionVisual.Drink));
            Assert.That(drinkRoot.activeSelf, Is.True);

            Assert.That(
                binding.Play(FirstPersonLifeActionVisual.Smoke),
                Is.True);
            Assert.That(binding.ActiveVisual,
                Is.EqualTo(FirstPersonLifeActionVisual.Smoke));
            Assert.That(drinkRoot.activeSelf, Is.False);
            Assert.That(smokeRoot.activeSelf, Is.True);

            Assert.That(
                binding.Play(FirstPersonLifeActionVisual.Hello),
                Is.True);
            Assert.That(binding.ActiveVisual,
                Is.EqualTo(FirstPersonLifeActionVisual.Hello));
            Assert.That(smokeRoot.activeSelf, Is.False);
            Assert.That(helloRoot.activeSelf, Is.True);

            Assert.That(
                binding.Play(FirstPersonLifeActionVisual.MiddleFinger),
                Is.True);
            Assert.That(binding.ActiveVisual,
                Is.EqualTo(FirstPersonLifeActionVisual.MiddleFinger));
            Assert.That(helloRoot.activeSelf, Is.False);
            Assert.That(middleFingerRoot.activeSelf, Is.True);

            Assert.That(
                binding.Play(FirstPersonLifeActionVisual.Push),
                Is.True);
            Assert.That(binding.ActiveVisual,
                Is.EqualTo(FirstPersonLifeActionVisual.Push));
            Assert.That(middleFingerRoot.activeSelf, Is.False);
            Assert.That(pushRoot.activeSelf, Is.True);

            Assert.That(
                binding.Play(FirstPersonLifeActionVisual.Fist),
                Is.True);
            Assert.That(binding.ActiveVisual,
                Is.EqualTo(FirstPersonLifeActionVisual.Fist));
            Assert.That(pushRoot.activeSelf, Is.False);
            Assert.That(fistRoot.activeSelf, Is.True);

            binding.Stop();

            Assert.That(binding.ActiveVisual,
                Is.EqualTo(FirstPersonLifeActionVisual.None));
            Assert.That(fistRoot.activeSelf, Is.False);
        }

        [Test]
        public void GeneratedPrefab_CameraLocalPlaybackActivatesOnlyOneIntactAction()
        {
            const string prefabPath =
                "Assets/Game/LegacyImport/RuntimeBaseline/" +
                "GameplayPresentation/PlayerViewmodel/Resources/" +
                "Phase1PlayerViewmodel/" +
                "Phase1PlayerViewmodelBinding.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);

            var cameraObject = new GameObject("Viewmodel Camera");
            GameObject generated = null;
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                generated = Object.Instantiate(
                    prefab,
                    camera.transform,
                    worldPositionStays: false);
                FirstPersonLifeActionViewmodelBinding binding = generated
                    .GetComponent<FirstPersonLifeActionViewmodelBinding>();
                Assert.That(binding, Is.Not.Null);
                Assert.That(binding.IsConfigured, Is.True);
                Assert.That(
                    generated.transform.localPosition,
                    Is.EqualTo(Vector3.zero));
                Assert.That(
                    Quaternion.Angle(
                        generated.transform.localRotation,
                        Quaternion.identity),
                    Is.LessThan(0.001f));
                Assert.That(
                    generated.transform.localScale,
                    Is.EqualTo(Vector3.one));

                var expectations = new[]
                {
                    (FirstPersonLifeActionVisual.Drink, "Drink"),
                    (FirstPersonLifeActionVisual.Smoke, "Smoke"),
                    (FirstPersonLifeActionVisual.Hello, "Hello"),
                    (FirstPersonLifeActionVisual.MiddleFinger, "MiddleFinger"),
                    (FirstPersonLifeActionVisual.Push, "Push"),
                    (FirstPersonLifeActionVisual.Fist, "Fist"),
                };
                foreach ((FirstPersonLifeActionVisual visual,
                             string expectedRoot) in expectations)
                {
                    Assert.That(binding.Play(visual), Is.True, visual.ToString());
                    Transform[] activeActionRoots = generated.transform
                        .Cast<Transform>()
                        .Where(candidate => candidate.gameObject.activeSelf)
                        .ToArray();
                    Assert.That(
                        activeActionRoots.Select(candidate => candidate.name),
                        Is.EqualTo(new[] { expectedRoot }),
                        $"{visual} left another gesture root active.");

                    SkinnedMeshRenderer[] renderers = activeActionRoots[0]
                        .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .Where(renderer => renderer.gameObject.activeInHierarchy)
                        .ToArray();
                    Assert.That(
                        renderers,
                        Is.Not.Empty,
                        $"{visual} has no active hand renderer.");
                    foreach (SkinnedMeshRenderer renderer in renderers)
                    {
                        var baked = new Mesh();
                        try
                        {
                            renderer.BakeMesh(baked, useScale: true);
                            Assert.That(
                                baked.vertexCount,
                                Is.GreaterThan(0),
                                $"{visual}/{renderer.name}");
                            Assert.That(
                                baked.vertices.All(IsFinite),
                                Is.True,
                                $"{visual}/{renderer.name} baked non-finite " +
                                "vertices.");
                            Bounds usedBounds = CalculateUsedBounds(
                                baked,
                                renderer.transform,
                                camera.transform);
                            float maximumEdge = CalculateMaximumTriangleEdge(
                                baked,
                                renderer.transform);
                            Assert.That(
                                usedBounds.size.x,
                                Is.LessThan(1.25f),
                                $"{visual}/{renderer.name} spans " +
                                $"{usedBounds.size} in camera space.");
                            Assert.That(
                                usedBounds.size.y,
                                Is.LessThan(1.25f),
                                $"{visual}/{renderer.name} spans " +
                                $"{usedBounds.size} in camera space.");
                            Assert.That(
                                usedBounds.size.z,
                                Is.LessThan(1.25f),
                                $"{visual}/{renderer.name} spans " +
                                $"{usedBounds.size} in camera space.");
                            Assert.That(
                                maximumEdge,
                                Is.LessThan(0.5f),
                                $"{visual}/{renderer.name} contains a " +
                                $"{maximumEdge:0.000}m triangle edge.");
                            TestContext.WriteLine(
                                $"{visual}/{renderer.name}: center=" +
                                $"{usedBounds.center}, size={usedBounds.size}, " +
                                $"maxEdge={maximumEdge:0.0000}m");
                        }
                        finally
                        {
                            Object.DestroyImmediate(baked);
                        }
                    }
                }

                binding.Stop();
                Assert.That(
                    generated.transform.Cast<Transform>()
                        .Any(candidate => candidate.gameObject.activeSelf),
                    Is.False);
            }
            finally
            {
                if (generated != null)
                {
                    Object.DestroyImmediate(generated);
                }

                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void MissingReviewedVisual_IsNotConfigured()
        {
            FirstPersonLifeActionViewmodelBinding binding =
                root.AddComponent<FirstPersonLifeActionViewmodelBinding>();
            binding.ConfigureMetadata(
                FirstPersonLifeActionViewmodelBinding.ExpectedBindingId,
                FirstPersonLifeActionViewmodelBinding.ExpectedReplacementKey);

            (GameObject drinkRoot, Animation drinkAnimation) =
                CreateAnimatedRoot("Drink");
            Transform drinkGrip = new GameObject("Drink Grip").transform;
            drinkGrip.SetParent(drinkRoot.transform, false);
            binding.ConfigureDrink(
                drinkRoot,
                drinkAnimation,
                CreateClip("drink", 0.5f),
                CreateClip("drink-short", 0.2f),
                CreateClip("drink-throw", 0.2f),
                CreateClip("drink-spray", 0.2f),
                drinkGrip);

            Assert.That(binding.IsConfigured, Is.False);
        }

        [Test]
        public void DrinkReady_DirectlySamplesRaisedFrameBeforeHolding()
        {
            FirstPersonLifeActionViewmodelBinding binding =
                root.AddComponent<FirstPersonLifeActionViewmodelBinding>();
            (GameObject drinkRoot, Animation drinkAnimation) =
                CreateAnimatedRoot("Drink");
            AnimationClip drink = new AnimationClip
            {
                name = "drink-ready-sample",
                legacy = true,
                frameRate = 60f,
            };
            drink.SetCurve(
                string.Empty,
                typeof(Transform),
                "localPosition.y",
                new AnimationCurve(
                    new Keyframe(0f, -0.4f),
                    new Keyframe(0.5f, -0.2f),
                    new Keyframe(1f, -0.15f)));
            clips.Add(drink);
            Transform drinkGrip = new GameObject("Drink Grip").transform;
            drinkGrip.SetParent(drinkRoot.transform, false);
            binding.ConfigureDrink(
                drinkRoot,
                drinkAnimation,
                drink,
                CreateClip("drink-short", 0.2f),
                CreateClip("drink-throw", 0.2f),
                CreateClip("drink-spray", 0.2f),
                drinkGrip);

            Assert.That(binding.ShowDrinkReady(), Is.True);
            Assert.That(
                drinkRoot.transform.localPosition.y,
                Is.EqualTo(-0.2f).Within(0.001f));

            Assert.That(
                binding.Play(FirstPersonLifeActionVisual.Drink),
                Is.True);
            Assert.That(
                drinkRoot.transform.localPosition.y,
                Is.EqualTo(-0.2f).Within(0.001f));
        }

        private (GameObject Root, Animation Animation) CreateAnimatedRoot(
            string name)
        {
            var instance = new GameObject(name);
            instance.transform.SetParent(root.transform, false);
            return (instance, instance.AddComponent<Animation>());
        }

        private AnimationClip CreateClip(string name, float duration)
        {
            var clip = new AnimationClip
            {
                name = name,
                legacy = true,
                frameRate = 60f,
            };
            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "localPosition.x",
                AnimationCurve.Linear(0f, 0f, duration, 0.1f));
            clips.Add(clip);
            return clip;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static Bounds CalculateUsedBounds(
            Mesh mesh,
            Transform rendererTransform,
            Transform cameraTransform)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Assert.That(triangles, Is.Not.Empty, mesh.name);
            Vector3 first = cameraTransform.InverseTransformPoint(
                rendererTransform.TransformPoint(vertices[triangles[0]]));
            var bounds = new Bounds(first, Vector3.zero);
            foreach (int vertexIndex in triangles)
            {
                bounds.Encapsulate(cameraTransform.InverseTransformPoint(
                    rendererTransform.TransformPoint(vertices[vertexIndex])));
            }

            return bounds;
        }

        private static float CalculateMaximumTriangleEdge(
            Mesh mesh,
            Transform rendererTransform)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            float maximum = 0f;
            for (int index = 0; index + 2 < triangles.Length; index += 3)
            {
                Vector3 a = rendererTransform.TransformPoint(
                    vertices[triangles[index]]);
                Vector3 b = rendererTransform.TransformPoint(
                    vertices[triangles[index + 1]]);
                Vector3 c = rendererTransform.TransformPoint(
                    vertices[triangles[index + 2]]);
                maximum = Mathf.Max(
                    maximum,
                    Vector3.Distance(a, b),
                    Vector3.Distance(b, c),
                    Vector3.Distance(c, a));
            }

            return maximum;
        }
    }
}
