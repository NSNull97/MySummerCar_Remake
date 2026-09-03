using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.Tests.EditMode.LegacyImport
{
    /// <summary>
    /// Retains the historical fixture name so existing filtered test commands
    /// keep working while validating the current AXIS replacement payload.
    /// </summary>
    public sealed class LicensedRealisticFpsHandsAuthoringTests
    {
        private const string PrefabPath =
            "Assets/Game/LegacyImport/RuntimeBaseline/" +
            "GameplayPresentation/PlayerViewmodel/Resources/" +
            "Phase1PlayerViewmodel/Phase1PlayerViewmodelBinding.prefab";

        private const string GeneratedRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/" +
            "GameplayPresentation/PlayerViewmodel/Generated/" +
            "LicensedAxisNeutralArms";

        private GameObject instance;

        [TearDown]
        public void TearDown()
        {
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void Drink_UsesApprovedElevenSecondSixtyHertzCycle()
        {
            Transform drink = InstantiateAction("Drink");
            GameObject animationTarget = RequireAnimationTarget(drink);
            AnimationClip clip = RequireClip("AxisArms_Drink.anim");
            Assert.That(clip.length, Is.EqualTo(11f).Within(0.02f));
            Assert.That(clip.frameRate, Is.EqualTo(60f).Within(0.01f));
            Assert.That(
                AnimationUtility.GetAnimationEvents(clip),
                Is.Empty,
                "Presentation clips must not own gameplay events.");

            Transform palm = RequireTransform(drink, "DEF-hand.R");
            Transform grip = RequireTransform(
                drink,
                "Beer Bottle Grip Anchor");
            Assert.That(grip.parent, Is.SameAs(palm));
            Vector3 localPosition = grip.localPosition;
            Quaternion localRotation = grip.localRotation;
            foreach (float time in new[]
                     {
                         0f,
                         0.73f,
                         2.52f,
                         4.52f,
                         6.52f,
                         8.52f,
                         11f,
                     })
            {
                clip.SampleAnimation(animationTarget, time);
                Assert.That(
                    Vector3.Distance(grip.localPosition, localPosition),
                    Is.LessThan(0.00001f),
                    $"Palm-local grip moved at {time:0.00}s.");
                Assert.That(
                    Quaternion.Angle(grip.localRotation, localRotation),
                    Is.LessThan(0.01f),
                    $"Palm-local grip rotated independently at " +
                    $"{time:0.00}s.");
            }

            clip.SampleAnimation(animationTarget, 0f);
            Vector3 readyPalm = palm.position;
            Vector3 readyAxis = -grip.forward;
            clip.SampleAnimation(animationTarget, 0.73f);
            Vector3 mouthPalm = palm.position;
            Vector3 mouthAxis = -grip.forward;
            Assert.That(
                Vector3.Distance(readyPalm, mouthPalm),
                Is.GreaterThan(0.08f),
                "Fast lift never brings the bottle toward the mouth.");
            Assert.That(
                Vector3.Angle(readyAxis, mouthAxis),
                Is.GreaterThan(20f),
                "Bottle never changes from ready to drinking direction.");

            clip.SampleAnimation(animationTarget, 2.52f);
            Vector3 firstSipAxis = -grip.forward;
            clip.SampleAnimation(animationTarget, 4.52f);
            Vector3 secondSipAxis = -grip.forward;
            clip.SampleAnimation(animationTarget, 6.52f);
            Vector3 thirdSipAxis = -grip.forward;
            clip.SampleAnimation(animationTarget, 8.52f);
            Vector3 fourthSipAxis = -grip.forward;
            AssertCumulativeLift(mouthAxis, firstSipAxis, "first");
            AssertCumulativeLift(firstSipAxis, secondSipAxis, "second");
            AssertCumulativeLift(secondSipAxis, thirdSipAxis, "third");
            AssertCumulativeLift(thirdSipAxis, fourthSipAxis, "fourth");

            clip.SampleAnimation(animationTarget, 11f);
            Assert.That(
                Vector3.Distance(palm.position, readyPalm),
                Is.LessThan(0.003f),
                "Drink does not finish at the exact ready pose.");
            Assert.That(
                Vector3.Angle(-grip.forward, readyAxis),
                Is.LessThan(0.25f),
                "Bottle does not finish at its exact ready orientation.");
        }

        [Test]
        public void Drink_BottleAnchorUsesCameraFacingSideOfPalm()
        {
            Transform drink = InstantiateAction("Drink");
            GameObject animationTarget = RequireAnimationTarget(drink);
            AnimationClip clip = RequireClip("AxisArms_Drink.anim");
            Transform palm = RequireTransform(drink, "DEF-hand.R");
            Transform grip = RequireTransform(
                drink,
                "Beer Bottle Grip Anchor");

            clip.SampleAnimation(animationTarget, 0.5f);
            Vector3 cameraDirection = -palm.position.normalized;
            Assert.That(
                Vector3.Dot(grip.position - palm.position, cameraDirection),
                Is.GreaterThan(0.001f),
                "Bottle anchor is still placed behind the back of the hand.");
        }

        [Test]
        public void MiddleFinger_UsesValidatedCompactFingerCurl()
        {
            AnimationClip clip = RequireClip("AxisArms_MiddleFinger.anim");
            AnimationClip reference = RequireClip("AxisArms_ThumbUp.anim");
            string[] curledDigits =
            {
                "/DEF-f_index.",
                "/DEF-f_ring.",
                "/DEF-f_pinky.",
            };
            EditorCurveBinding[] bindings = AnimationUtility
                .GetCurveBindings(clip)
                .Where(binding =>
                    binding.type == typeof(Transform) &&
                    binding.propertyName.StartsWith(
                        "localEulerAnglesRaw.",
                        StringComparison.Ordinal) &&
                    binding.path.Contains(
                        "/ORG-shoulder.L/",
                        StringComparison.Ordinal) &&
                    curledDigits.Any(digit => binding.path.Contains(
                        digit,
                        StringComparison.Ordinal)))
                .ToArray();
            Assert.That(bindings, Has.Length.EqualTo(27));
            foreach (EditorCurveBinding binding in bindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(
                    clip,
                    binding);
                AnimationCurve referenceCurve = AnimationUtility
                    .GetEditorCurve(reference, binding);
                Assert.That(curve, Is.Not.Null, binding.path);
                Assert.That(referenceCurve, Is.Not.Null, binding.path);
                float expected = referenceCurve.Evaluate(0.4f);
                Assert.That(
                    curve.Evaluate(0.45f),
                    Is.EqualTo(expected).Within(0.01f),
                    $"{binding.path}/{binding.propertyName} does not use " +
                    "the validated compact curl.");
            }
        }

        [Test]
        public void MiddleFinger_ThumbUsesRaisedPhotoReferencePose()
        {
            AnimationClip clip = RequireClip("AxisArms_MiddleFinger.anim");
            AnimationClip reference = RequireClip("AxisArms_ThumbUp.anim");
            EditorCurveBinding[] bindings = AnimationUtility
                .GetCurveBindings(clip)
                .Where(binding =>
                    binding.type == typeof(Transform) &&
                    binding.propertyName.StartsWith(
                        "localEulerAnglesRaw.",
                        StringComparison.Ordinal) &&
                    binding.path.Contains(
                        "/ORG-shoulder.L/",
                        StringComparison.Ordinal) &&
                    binding.path.Contains(
                        "/DEF-thumb.",
                        StringComparison.Ordinal))
                .ToArray();
            Assert.That(bindings, Has.Length.EqualTo(9));
            foreach (EditorCurveBinding binding in bindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(
                    clip,
                    binding);
                AnimationCurve referenceCurve = AnimationUtility
                    .GetEditorCurve(reference, binding);
                Assert.That(curve, Is.Not.Null, binding.path);
                Assert.That(referenceCurve, Is.Not.Null, binding.path);

                float actual = curve.Evaluate(0.45f);
                float raised = referenceCurve.Evaluate(0.4f);
                if (binding.path.EndsWith(
                        "/DEF-thumb.01.L",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        binding.propertyName,
                        "localEulerAnglesRaw.z",
                        StringComparison.Ordinal))
                {
                    raised += 22f;
                }
                Assert.That(
                    Mathf.Abs(Mathf.DeltaAngle(actual, raised)),
                    Is.LessThan(6f),
                    $"{binding.path}/{binding.propertyName} leaves the " +
                    "middle-finger thumb spread sideways.");
            }
        }

        [Test]
        public void Drink_IsContinuousAtEveryBakedFrame()
        {
            Transform drink = InstantiateAction("Drink");
            GameObject animationTarget = RequireAnimationTarget(drink);
            AnimationClip clip = RequireClip("AxisArms_Drink.anim");
            Transform palm = RequireTransform(drink, "DEF-hand.R");
            Quaternion previousRotation = Quaternion.identity;
            Vector3 previousPosition = Vector3.zero;
            bool hasPrevious = false;
            int frameCount = Mathf.RoundToInt(clip.length * 60f);
            for (int frame = 0; frame <= frameCount; frame++)
            {
                clip.SampleAnimation(animationTarget, frame / 60f);
                if (hasPrevious)
                {
                    Assert.That(
                        Vector3.Distance(previousPosition, palm.position),
                        Is.LessThan(0.025f),
                        $"Drink wrist jumped around frame {frame}.");
                    Assert.That(
                        Quaternion.Angle(previousRotation, palm.rotation),
                        Is.LessThan(8f),
                        $"Drink wrist flipped around frame {frame}.");
                }

                previousPosition = palm.position;
                previousRotation = palm.rotation;
                hasPrevious = true;
            }
        }

        [Test]
        public void Actions_UseCorrectAnatomicalSidesAndNoMotionVectors()
        {
            Transform drink = InstantiateAction("Drink");
            AssertSideRenderer(drink, "R");
            Assert.That(
                drink.GetComponentInChildren<Animator>(true),
                Is.Null);

            Transform hello = RequireAction("Hello");
            AssertSideRenderer(hello, "R");
            RequireTransform(hello, "DEF-hand.R");

            Transform middleFinger = RequireAction("MiddleFinger");
            AssertSideRenderer(middleFinger, "L");
            RequireTransform(middleFinger, "DEF-hand.L");

            Transform smoke = RequireAction("Smoke");
            AssertSideRenderer(smoke, "R");
            Transform cigaretteGrip = RequireTransform(
                smoke,
                "Cigarette Grip Anchor");
            Assert.That(
                cigaretteGrip.parent.name,
                Is.EqualTo("DEF-f_index.02.R"));

            Transform push = RequireAction("Push");
            AssertSideRenderer(push, "R");
            RequireTransform(push, "DEF-hand.R");

            Transform thumbUp = RequireAction("Fist");
            AssertSideRenderer(thumbUp, "L");
            RequireTransform(thumbUp, "DEF-hand.L");
        }

        [Test]
        public void Actions_StayInCameraLocalEnvelopeWithoutTornTriangles()
        {
            Transform drink = InstantiateAction("Drink");
            AssertActionSamples(
                drink,
                "AxisArms_Drink.anim",
                "DEF-hand.R",
                new[] { 0.5f, 0.73f, 2.52f, 6.52f, 8.52f });
            AssertActionSamples(
                RequireAction("Hello"),
                "AxisArms_Wave.anim",
                "DEF-hand.R",
                new[] { 0.48f, 0.8f, 1.4f, 2.2f });
            AssertActionSamples(
                RequireAction("MiddleFinger"),
                "AxisArms_MiddleFinger.anim",
                "DEF-hand.L",
                new[] { 0.2f, 0.45f, 0.7f, 1f });
            AssertActionSamples(
                RequireAction("Smoke"),
                "AxisArms_SmokeIn.anim",
                "DEF-hand.R",
                new[] { 0.25f, 0.5f });
            AssertActionSamples(
                RequireAction("Push"),
                "AxisArms_PushOn.anim",
                "DEF-hand.R",
                new[] { 0.2f, 0.4f });
            AssertActionSamples(
                RequireAction("Fist"),
                "AxisArms_ThumbUp.anim",
                "DEF-hand.L",
                new[] { 0.3f, 0.6f });
        }

        [Test]
        public void AxisMeshes_RetainEightWeightSkinningAndViewmodelShadowsOff()
        {
            Transform drink = InstantiateAction("Drink");
            foreach (Transform action in new[]
                     {
                         drink,
                         RequireAction("Hello"),
                         RequireAction("MiddleFinger"),
                         RequireAction("Smoke"),
                         RequireAction("Push"),
                         RequireAction("Fist"),
                     })
            {
                SkinnedMeshRenderer renderer = action
                    .GetComponentInChildren<SkinnedMeshRenderer>(true);
                Assert.That(renderer, Is.Not.Null, action.name);
                Assert.That(
                    renderer.shadowCastingMode,
                    Is.EqualTo(ShadowCastingMode.Off),
                    action.name);
                Assert.That(renderer.receiveShadows, Is.False, action.name);
                Assert.That(
                    renderer.sharedMesh.blendShapeCount,
                    Is.Zero,
                    $"{action.name} retained AXIS full-rig corrective " +
                    "blend shapes after the single-arm extraction.");
                Assert.That(
                    renderer.sharedMesh.vertexCount,
                    Is.LessThan(10000),
                    $"{action.name} unexpectedly retained the uncut " +
                    "two-arm mesh.");

                var bonesPerVertex = renderer.sharedMesh.GetBonesPerVertex();
                try
                {
                    int maximumBonesPerVertex = 0;
                    for (int index = 0; index < bonesPerVertex.Length; index++)
                    {
                        maximumBonesPerVertex = Mathf.Max(
                            maximumBonesPerVertex,
                            bonesPerVertex[index]);
                    }

                    Assert.That(
                        maximumBonesPerVertex,
                        Is.InRange(5, 8),
                        $"{action.name} lost the licensed mesh's >4-weight " +
                        "finger/forearm skinning.");
                }
                finally
                {
                    bonesPerVertex.Dispose();
                }
            }
        }

        [Test]
        public void AllAxisClips_AreLegacyEventFreeAndAtSixtyHertz()
        {
            foreach (string fileName in new[]
                     {
                         "AxisArms_Drink.anim",
                         "AxisArms_DrinkShort.anim",
                         "AxisArms_DrinkSpray.anim",
                         "AxisArms_DrinkThrow.anim",
                         "AxisArms_Wave.anim",
                         "AxisArms_MiddleFinger.anim",
                         "AxisArms_SmokeIn.anim",
                         "AxisArms_SmokeLightUp.anim",
                         "AxisArms_SmokeOut.anim",
                         "AxisArms_SmokePutOff.anim",
                         "AxisArms_SmokeReset.anim",
                         "AxisArms_PushOn.anim",
                         "AxisArms_PushOff.anim",
                         "AxisArms_ThumbUp.anim",
                     })
            {
                AnimationClip clip = RequireClip(fileName);
                Assert.That(clip.legacy, Is.True, fileName);
                Assert.That(
                    clip.frameRate,
                    Is.EqualTo(60f).Within(0.01f),
                    fileName);
                Assert.That(
                    AnimationUtility.GetAnimationEvents(clip),
                    Is.Empty,
                    fileName);
                Assert.That(
                    AnimationUtility.GetCurveBindings(clip).Any(binding =>
                        binding.type == typeof(SkinnedMeshRenderer) &&
                        binding.propertyName.StartsWith(
                            "blendShape.",
                            StringComparison.Ordinal)),
                    Is.False,
                    $"{fileName} retained incompatible AXIS full-rig " +
                    "blend-shape driver curves.");
            }
        }

        private static void AssertCumulativeLift(
            Vector3 previous,
            Vector3 current,
            string ordinal)
        {
            Assert.That(
                Vector3.Angle(previous, current),
                Is.InRange(0.45f, 3.5f),
                $"The {ordinal} finishing sip is missing or rocks backward.");
        }

        private static void AssertSideRenderer(
            Transform action,
            string side)
        {
            SkinnedMeshRenderer[] renderers = action
                .GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(renderers, Has.Length.EqualTo(1));
            SkinnedMeshRenderer renderer = renderers[0];
            string expected = side == "R"
                ? GeneratedRoot + "/AxisNeutralArms_RightArm.asset"
                : GeneratedRoot + "/AxisNeutralArms_LeftArm.asset";
            Assert.That(
                AssetDatabase.GetAssetPath(renderer.sharedMesh),
                Is.EqualTo(expected));
            Assert.That(
                renderer.motionVectorGenerationMode,
                Is.EqualTo(MotionVectorGenerationMode.ForceNoMotion));
        }

        private static void AssertActionSamples(
            Transform action,
            string clipFileName,
            string palmName,
            float[] times)
        {
            GameObject target = RequireAnimationTarget(action);
            AnimationClip clip = RequireClip(clipFileName);
            Transform palm = RequireTransform(action, palmName);
            SkinnedMeshRenderer renderer = action
                .GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(renderer, Is.Not.Null, action.name);
            var baked = new Mesh();
            try
            {
                foreach (float time in times)
                {
                    clip.SampleAnimation(target, time);
                    Vector3 cameraLocalPalm = palm.position;
                    Assert.That(
                        cameraLocalPalm.z,
                        Is.InRange(0.08f, 0.35f),
                        $"{action.name} palm escaped camera depth at " +
                        $"{time:0.00}s: {cameraLocalPalm}.");
                    Assert.That(
                        Mathf.Abs(cameraLocalPalm.x),
                        Is.LessThan(0.3f),
                        $"{action.name} palm escaped horizontally at " +
                        $"{time:0.00}s: {cameraLocalPalm}.");
                    Assert.That(
                        cameraLocalPalm.y,
                        Is.InRange(-0.35f, 0.15f),
                        $"{action.name} palm escaped vertically at " +
                        $"{time:0.00}s: {cameraLocalPalm}.");

                    renderer.BakeMesh(baked, useScale: true);
                    Assert.That(
                        MaximumUsedTriangleEdge(baked),
                        Is.LessThan(0.04f),
                        $"{action.name} contains a torn/spiked triangle at " +
                        $"{time:0.00}s.");
                    baked.Clear();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baked);
            }
        }

        private static float MaximumUsedTriangleEdge(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            float maximum = 0f;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                int[] triangles = mesh.GetTriangles(subMesh);
                for (int index = 0; index < triangles.Length; index += 3)
                {
                    Vector3 a = vertices[triangles[index]];
                    Vector3 b = vertices[triangles[index + 1]];
                    Vector3 c = vertices[triangles[index + 2]];
                    maximum = Mathf.Max(
                        maximum,
                        Vector3.Distance(a, b),
                        Vector3.Distance(b, c),
                        Vector3.Distance(c, a));
                }
            }

            return maximum;
        }

        private Transform InstantiateAction(string actionName)
        {
            GameObject prefab = RequireAsset<GameObject>(PrefabPath);
            instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = "AXIS hands authoring test";
            return RequireAction(actionName);
        }

        private Transform RequireAction(string actionName)
        {
            Transform action = instance.transform.Find(actionName);
            return action != null
                ? action
                : throw new InvalidOperationException(
                    $"Generated action '{actionName}' is missing.");
        }

        private static GameObject RequireAnimationTarget(Transform action)
        {
            Animation animation = action.GetComponentInChildren<Animation>(
                includeInactive: true);
            return animation != null
                ? animation.gameObject
                : throw new InvalidOperationException(
                    $"Generated action '{action.name}' has no Animation.");
        }

        private static AnimationClip RequireClip(string fileName) =>
            RequireAsset<AnimationClip>(GeneratedRoot + "/" + fileName);

        private static Transform RequireTransform(
            Transform root,
            string name)
        {
            Transform found = root.GetComponentsInChildren<Transform>(true)
                .SingleOrDefault(candidate => string.Equals(
                    candidate.name,
                    name,
                    StringComparison.Ordinal));
            return found != null
                ? found
                : throw new InvalidOperationException(
                    $"Generated AXIS transform '{name}' is missing.");
        }

        private static T RequireAsset<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            return asset != null
                ? asset
                : throw new InvalidOperationException(
                    $"Required generated asset '{path}' is missing.");
        }
    }
}
