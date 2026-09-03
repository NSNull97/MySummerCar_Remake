using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MSC.Needs;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>
    /// Integrates the locally licensed AXIS Neutral Arms source into the
    /// project-owned Phase 1 viewmodel wrapper. The FBX contains presentation
    /// only; gameplay state and bottle physics remain project-owned.
    /// </summary>
    public static class LicensedAxisNeutralArmsImporter
    {
        public const string SourceBlendSha256 =
            "9a978c3040b665bafb4f6e64779f6ad1" +
            "cce47f7d3d498430687cc8336056934f";

        public const int LicensedSourceAssetCount = 4;

        internal const float DrinkReadyPoseTimeSeconds = 0f;

        private const string SourceRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/" +
            "GameplayPresentation/LicensedAxisNeutralArms/Source";

        public const string ModelAssetPath =
            SourceRoot + "/AxisNeutralArms.fbx";

        private const string AlbedoAssetPath =
            SourceRoot + "/AxisNeutralArms_Diffuse.png";

        private const string NormalAssetPath =
            SourceRoot + "/AxisNeutralArms_Normal.png";

        private const string RoughnessAssetPath =
            SourceRoot + "/AxisNeutralArms_Roughness.png";

        private const string GeneratedRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/" +
            "GameplayPresentation/PlayerViewmodel/Generated/" +
            "LicensedAxisNeutralArms";

        private const string ViewmodelPrefabAssetPath =
            "Assets/Game/LegacyImport/RuntimeBaseline/" +
            "GameplayPresentation/PlayerViewmodel/Resources/" +
            "Phase1PlayerViewmodel/Phase1PlayerViewmodelBinding.prefab";

        private const string BeerBottleMeshAssetPath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items/" +
            "Meshes/LegacyItemMesh_5103d9d7418206a4da09260262c716a0.asset";

        private const string BeerBottleMaterialAssetPath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items/" +
            "Materials/LegacyItemMaterial_505c0da5d59fb1a4fbc38445d0494ef3.mat";

        private const string MaterialAssetPath =
            GeneratedRoot + "/AxisNeutralArms_HDRP.mat";

        private const string RightArmMeshAssetPath =
            GeneratedRoot + "/AxisNeutralArms_RightArm.asset";

        private const string LeftArmMeshAssetPath =
            GeneratedRoot + "/AxisNeutralArms_LeftArm.asset";

        private const string DrinkClipAssetPath =
            GeneratedRoot + "/AxisArms_Drink.anim";

        private const string DrinkShortClipAssetPath =
            GeneratedRoot + "/AxisArms_DrinkShort.anim";

        private const string DrinkThrowClipAssetPath =
            GeneratedRoot + "/AxisArms_DrinkThrow.anim";

        private const string DrinkSprayClipAssetPath =
            GeneratedRoot + "/AxisArms_DrinkSpray.anim";

        private const string WaveClipAssetPath =
            GeneratedRoot + "/AxisArms_Wave.anim";

        private const string MiddleFingerClipAssetPath =
            GeneratedRoot + "/AxisArms_MiddleFinger.anim";

        private const string SmokeInClipAssetPath =
            GeneratedRoot + "/AxisArms_SmokeIn.anim";

        private const string SmokeLightUpClipAssetPath =
            GeneratedRoot + "/AxisArms_SmokeLightUp.anim";

        private const string SmokeOutClipAssetPath =
            GeneratedRoot + "/AxisArms_SmokeOut.anim";

        private const string SmokePutOffClipAssetPath =
            GeneratedRoot + "/AxisArms_SmokePutOff.anim";

        private const string SmokeResetClipAssetPath =
            GeneratedRoot + "/AxisArms_SmokeReset.anim";

        private const string PushOnClipAssetPath =
            GeneratedRoot + "/AxisArms_PushOn.anim";

        private const string PushOffClipAssetPath =
            GeneratedRoot + "/AxisArms_PushOff.anim";

        private const string ThumbUpClipAssetPath =
            GeneratedRoot + "/AxisArms_ThumbUp.anim";

        private const float BottlePalmOffset = 0.0593870527f;
        private const float BottleCameraFacingClearance = 0.006f;
        private const float BottleAxialOffset = 0.0212357036f;
        private const float BottleTiltDegrees = 4f;
        private const float BottleTiltForward = -0.3513697f;
        private const float BottleTiltNormal = -0.9362368f;
        private const float ViewmodelDistalBoneWeightThreshold = 0.001f;

        private static readonly SourceSpec[] SourceSpecs =
        {
            new SourceSpec(
                "model",
                ModelAssetPath,
                "dc03305d8d63e59e28edfc44f81906fa" +
                "d047814dde170dda4bce9cac12e30d43"),
            new SourceSpec(
                "albedo",
                AlbedoAssetPath,
                "be0ad1e8e8c1e543c8b39c9cb52f809" +
                "792cca377b8e125769fab809d27bf1cc6"),
            new SourceSpec(
                "normal",
                NormalAssetPath,
                "49721e8bb24f10a4cc122548cfa31dcd" +
                "d82c662b89306925d91ce4b407bdf88d"),
            new SourceSpec(
                "roughness",
                RoughnessAssetPath,
                "9bc9ac0424a63723a282f38b9d0daf41" +
                "be3f6f085e4e77cd41dd620af95ef1db"),
        };

        private static readonly ClipSpec[] ClipSpecs =
        {
            new ClipSpec("AxisArms_Drink", DrinkClipAssetPath, 11f),
            new ClipSpec(
                "AxisArms_DrinkShort",
                DrinkShortClipAssetPath,
                1.95f),
            new ClipSpec(
                "AxisArms_DrinkThrow",
                DrinkThrowClipAssetPath,
                44f / 60f),
            new ClipSpec(
                "AxisArms_DrinkSpray",
                DrinkSprayClipAssetPath,
                4f),
            new ClipSpec("AxisArms_Wave", WaveClipAssetPath, 2.8f),
            new ClipSpec(
                "AxisArms_MiddleFinger",
                MiddleFingerClipAssetPath,
                1.4f),
            new ClipSpec("AxisArms_SmokeIn", SmokeInClipAssetPath, 0.5f),
            new ClipSpec(
                "AxisArms_SmokeLightUp",
                SmokeLightUpClipAssetPath,
                0.5f),
            new ClipSpec("AxisArms_SmokeOut", SmokeOutClipAssetPath, 0.5f),
            new ClipSpec(
                "AxisArms_SmokePutOff",
                SmokePutOffClipAssetPath,
                0.91683334f),
            new ClipSpec(
                "AxisArms_SmokeReset",
                SmokeResetClipAssetPath,
                1f / 60f),
            new ClipSpec("AxisArms_PushOn", PushOnClipAssetPath, 0.4f),
            new ClipSpec(
                "AxisArms_PushOff",
                PushOffClipAssetPath,
                0.41666666f),
            new ClipSpec("AxisArms_ThumbUp", ThumbUpClipAssetPath, 0.8f),
        };

        public static IReadOnlyList<string> HashedAssetPaths { get; } =
            SourceSpecs.Select(spec => spec.AssetPath)
                .Concat(new[]
                {
                    MaterialAssetPath,
                    RightArmMeshAssetPath,
                    LeftArmMeshAssetPath,
                    DrinkClipAssetPath,
                    DrinkShortClipAssetPath,
                    DrinkThrowClipAssetPath,
                    DrinkSprayClipAssetPath,
                    WaveClipAssetPath,
                    MiddleFingerClipAssetPath,
                    SmokeInClipAssetPath,
                    SmokeLightUpClipAssetPath,
                    SmokeOutClipAssetPath,
                    SmokePutOffClipAssetPath,
                    SmokeResetClipAssetPath,
                    PushOnClipAssetPath,
                    PushOffClipAssetPath,
                    ThumbUpClipAssetPath,
                })
                .ToArray();

        internal static AxisHandsBuildResult BuildViewmodelActions(
            Transform prefabRoot)
        {
            if (prefabRoot == null)
            {
                throw new ArgumentNullException(nameof(prefabRoot));
            }

            ValidateInstalledSource();
            ConfigureSourceImporters();
            EnsureAssetFolder(GeneratedRoot);
            DeleteGeneratedAssets();

            Material material = CreateHandMaterial();
            GameObject sourceModel = RequireAsset<GameObject>(ModelAssetPath);
            Mesh rightArmMesh = CreateSideMesh(
                sourceModel,
                "R",
                RightArmMeshAssetPath);
            Mesh leftArmMesh = CreateSideMesh(
                sourceModel,
                "L",
                LeftArmMeshAssetPath);
            IReadOnlyDictionary<string, AnimationClip> clips =
                ExtractLegacyClips();

            AxisAction drink = CreateAction(
                "Drink",
                prefabRoot,
                sourceModel,
                material,
                rightArmMesh);
            AxisAction wave = CreateAction(
                "Hello",
                prefabRoot,
                sourceModel,
                material,
                rightArmMesh);
            AxisAction middleFinger = CreateAction(
                "MiddleFinger",
                prefabRoot,
                sourceModel,
                material,
                leftArmMesh);
            AxisAction smoke = CreateAction(
                "Smoke",
                prefabRoot,
                sourceModel,
                material,
                rightArmMesh);
            AxisAction push = CreateAction(
                "Push",
                prefabRoot,
                sourceModel,
                material,
                rightArmMesh);
            AxisAction thumbUp = CreateAction(
                "Fist",
                prefabRoot,
                sourceModel,
                material,
                leftArmMesh);

            AnimationClip drinkClip = clips["AxisArms_Drink"];
            drinkClip.SampleAnimation(drink.Model.gameObject, 0f);
            Transform grip = CreatePalmLocalBottleGrip(drink);
            clips["AxisArms_SmokeIn"].SampleAnimation(
                smoke.Model.gameObject,
                0.5f);
            Transform cigaretteGrip = CreateCigaretteGrip(smoke);
            drink.Root.SetActive(false);
            wave.Root.SetActive(false);
            middleFinger.Root.SetActive(false);
            smoke.Root.SetActive(false);
            push.Root.SetActive(false);
            thumbUp.Root.SetActive(false);
            return new AxisHandsBuildResult(
                drink,
                drinkClip,
                clips["AxisArms_DrinkShort"],
                clips["AxisArms_DrinkThrow"],
                clips["AxisArms_DrinkSpray"],
                grip,
                wave,
                clips["AxisArms_Wave"],
                middleFinger,
                clips["AxisArms_MiddleFinger"],
                smoke,
                clips["AxisArms_SmokeIn"],
                clips["AxisArms_SmokeLightUp"],
                clips["AxisArms_SmokeOut"],
                clips["AxisArms_SmokePutOff"],
                clips["AxisArms_SmokeReset"],
                cigaretteGrip,
                push,
                clips["AxisArms_PushOn"],
                clips["AxisArms_PushOff"],
                thumbUp,
                clips["AxisArms_ThumbUp"]);
        }

        public static void ValidateGeneratedSourceAndAssets()
        {
            ValidateInstalledSource();
            foreach (string assetPath in HashedAssetPaths)
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
                {
                    throw new InvalidOperationException(
                        $"Licensed AXIS generated asset is missing: " +
                        $"'{assetPath}'. Rebuild the Phase 1 player " +
                        "viewmodel.");
                }
            }
        }

        public static Mesh RequireImportedHandMesh()
        {
            return RequireAsset<Mesh>(RightArmMeshAssetPath);
        }

        public static Mesh RequireImportedLeftHandMesh()
        {
            return RequireAsset<Mesh>(LeftArmMeshAssetPath);
        }

        /// <summary>
        /// Batch entry point that renders the generated prefab exactly as a
        /// camera-local viewmodel at the gameplay camera's 120 degree
        /// horizontal FOV (88.507 degrees vertically at 16:9).
        /// Unlike the Blender pose sheets, this audit includes Unity FBX
        /// conversion, skinning, blend shapes and the real donor beer mesh.
        /// </summary>
        public static void RenderUnityCameraAuditFromBatch()
        {
            GameObject prefab = RequireAsset<GameObject>(
                ViewmodelPrefabAssetPath);
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.name = "AXIS Unity Camera Audit";
            instance.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            instance.SetActive(true);

            var cameraObject = new GameObject("AXIS Audit Camera");
            var bottle = new GameObject("AXIS Audit Beer Bottle");
            GameObject cigarette = GameObject.CreatePrimitive(
                PrimitiveType.Cylinder);
            cigarette.name = "AXIS Audit Cigarette";
            UnityEngine.Object.DestroyImmediate(
                cigarette.GetComponent<Collider>());
            var volumeObject = new GameObject("AXIS Audit Exposure");
            var keyLightObject = new GameObject("AXIS Audit Key Light");
            var fillLightObject = new GameObject("AXIS Audit Fill Light");
            Material handMaterial = null;
            Material bottleMaterial = null;
            Material cigaretteMaterial = null;
            VolumeProfile volumeProfile = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                // Use the exact generated HDRP material.  A flat unlit audit
                // hides knuckle direction, finger intersections and wrist
                // twisting -- precisely the defects this capture must expose.
                handMaterial = new Material(
                    RequireAsset<Material>(MaterialAssetPath))
                {
                    name = "AXIS Audit Hand Material",
                };
                foreach (SkinnedMeshRenderer renderer in instance
                             .GetComponentsInChildren<
                                 SkinnedMeshRenderer>(true))
                {
                    if (renderer.sharedMesh == null ||
                        !renderer.sharedMesh.name.StartsWith(
                            "AXIS Neutral Arms",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    renderer.sharedMaterial = handMaterial;
                    renderer.updateWhenOffscreen = true;
                    renderer.forceMatrixRecalculationPerRender = true;
                    renderer.quality = SkinQuality.Auto;
                }

                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<HDAdditionalCameraData>();
                camera.transform.SetPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.055f, 0.065f, 0.08f);
                camera.fieldOfView = Camera.HorizontalToVerticalFieldOfView(
                    120f,
                    16f / 9f);
                camera.nearClipPlane = 0.03f;
                camera.farClipPlane = 5f;
                camera.allowHDR = true;
                camera.allowMSAA = true;

                ConfigureAuditDirectionalLight(
                    keyLightObject,
                    new Vector3(42f, -32f, 0f),
                    100000f,
                    new Color(1f, 0.94f, 0.87f),
                    castShadows: true);
                ConfigureAuditDirectionalLight(
                    fillLightObject,
                    new Vector3(325f, 148f, 0f),
                    28000f,
                    new Color(0.56f, 0.72f, 1f),
                    castShadows: false);

                Volume volume = volumeObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 100000f;
                volumeProfile =
                    ScriptableObject.CreateInstance<VolumeProfile>();
                Exposure exposure = volumeProfile.Add<Exposure>();
                exposure.mode.Override(ExposureMode.Fixed);
                exposure.fixedExposure.Override(14f);
                exposure.compensation.Override(0f);
                Bloom bloom = volumeProfile.Add<Bloom>();
                bloom.active = false;
                volume.sharedProfile = volumeProfile;
                target = new RenderTexture(
                    1280,
                    720,
                    24,
                    RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 2,
                };
                target.Create();
                camera.targetTexture = target;
                pixels = new Texture2D(
                    target.width,
                    target.height,
                    TextureFormat.RGB24,
                    mipChain: false);

                Mesh bottleMesh = RequireAsset<Mesh>(
                    BeerBottleMeshAssetPath);
                bottle.AddComponent<MeshFilter>().sharedMesh = bottleMesh;
                MeshRenderer bottleRenderer =
                    bottle.AddComponent<MeshRenderer>();
                Material sourceBottleMaterial = RequireAsset<Material>(
                    BeerBottleMaterialAssetPath);
                bottleMaterial = new Material(sourceBottleMaterial)
                {
                    name = "AXIS Audit Beer Material",
                };
                bottleRenderer.sharedMaterial = bottleMaterial;

                Transform drinkGrip = instance
                    .GetComponentsInChildren<Transform>(true)
                    .Single(candidate => string.Equals(
                        candidate.name,
                        "Beer Bottle Grip Anchor",
                        StringComparison.Ordinal));
                Transform cigaretteGrip = instance
                    .GetComponentsInChildren<Transform>(true)
                    .Single(candidate => string.Equals(
                        candidate.name,
                        "Cigarette Grip Anchor",
                        StringComparison.Ordinal));
                cigarette.transform.SetParent(
                    cigaretteGrip,
                    worldPositionStays: false);
                cigarette.transform.SetLocalPositionAndRotation(
                    new Vector3(0f, 0f, -0.028f),
                    Quaternion.Euler(90f, 0f, 0f));
                cigarette.transform.localScale =
                    new Vector3(0.008f, 0.045f, 0.008f);
                cigaretteMaterial = new Material(
                    Shader.Find("HDRP/Unlit") ??
                    Shader.Find("HDRP/Lit") ??
                    Shader.Find("Unlit/Color"))
                {
                    name = "AXIS Audit Cigarette Material",
                    color = new Color(0.91f, 0.88f, 0.80f, 1f),
                };
                if (cigaretteMaterial.HasProperty("_BaseColor"))
                {
                    cigaretteMaterial.SetColor(
                        "_BaseColor",
                        cigaretteMaterial.color);
                }

                cigarette.GetComponent<MeshRenderer>().sharedMaterial =
                    cigaretteMaterial;
                var reviews = new[]
                {
                    new AuditReview(
                        "Drink",
                        "Drink",
                        DrinkClipAssetPath,
                        new[]
                        {
                            0f,
                            DrinkReadyPoseTimeSeconds,
                            0.73f,
                            2.52f,
                            6.52f,
                            8.52f,
                            11f,
                        },
                        showBottle: true,
                        showCigarette: false),
                    new AuditReview(
                        "DrinkShort",
                        "Drink",
                        DrinkShortClipAssetPath,
                        new[] { 0f, 0.55f, 1.2f, 1.95f },
                        showBottle: true,
                        showCigarette: false),
                    new AuditReview(
                        "DrinkSpray",
                        "Drink",
                        DrinkSprayClipAssetPath,
                        new[] { 0f, 0.7f, 2f, 4f },
                        showBottle: true,
                        showCigarette: false),
                    new AuditReview(
                        "DrinkThrow",
                        "Drink",
                        DrinkThrowClipAssetPath,
                        new[] { 0f, 0.36f, 0.733333f },
                        showBottle: true,
                        showCigarette: false),
                    new AuditReview(
                        "SmokeIn",
                        "Smoke",
                        SmokeInClipAssetPath,
                        new[] { 0.25f, 0.5f },
                        showBottle: false,
                        showCigarette: true),
                    new AuditReview(
                        "SmokeLightUp",
                        "Smoke",
                        SmokeLightUpClipAssetPath,
                        new[] { 0f, 0.25f, 0.5f },
                        showBottle: false,
                        showCigarette: true),
                    new AuditReview(
                        "Hello",
                        "Hello",
                        WaveClipAssetPath,
                        new[] { 0.48f, 0.8f, 1.4f, 2.2f },
                        showBottle: false,
                        showCigarette: false),
                    new AuditReview(
                        "MiddleFinger",
                        "MiddleFinger",
                        MiddleFingerClipAssetPath,
                        new[] { 0.2f, 0.45f, 0.7f, 1.0f },
                        showBottle: false,
                        showCigarette: false),
                    new AuditReview(
                        "Push",
                        "Push",
                        PushOnClipAssetPath,
                        new[] { 0.2f, 0.4f },
                        showBottle: false,
                        showCigarette: false),
                    new AuditReview(
                        "ThumbUp",
                        "Fist",
                        ThumbUpClipAssetPath,
                        new[] { 0.3f, 0.6f },
                        showBottle: false,
                        showCigarette: false),
                };
                Transform[] roots = reviews
                    .Select(review => instance.transform.Find(
                        review.ActionRootName) ??
                        throw new InvalidOperationException(
                            $"AXIS audit action '{review.ActionRootName}' " +
                            "is missing."))
                    .Distinct()
                    .ToArray();
                string output = Path.Combine(
                    Directory.GetParent(Application.dataPath)?.FullName ??
                    throw new InvalidOperationException(
                        "Unity project root is unavailable."),
                    "Logs",
                    "AxisHandsUnityCameraAudit");
                Directory.CreateDirectory(output);
                var metrics = new List<string>
                {
                    "review,time,palm_x,palm_y,palm_z,bounds_center_x," +
                    "bounds_center_y,bounds_center_z,bounds_size_x," +
                    "bounds_size_y,bounds_size_z,max_triangle_edge",
                };
                foreach (AuditReview review in reviews)
                {
                    foreach (Transform root in roots)
                    {
                        root.gameObject.SetActive(
                            string.Equals(
                                root.name,
                                review.ActionRootName,
                                StringComparison.Ordinal));
                    }

                    Transform actionRoot = instance.transform.Find(
                        review.ActionRootName);
                    Animation animation = actionRoot
                        .GetComponentInChildren<Animation>(true);
                    GameObject animationTarget = animation.gameObject;
                    AnimationClip clip = RequireAsset<AnimationClip>(
                        review.ClipAssetPath);
                    Transform palm = RequireDescendant(
                        animationTarget.transform,
                        review.ActionRootName == "MiddleFinger" ||
                        review.ActionRootName == "Fist"
                            ? "DEF-hand.L"
                            : "DEF-hand.R");
                    SkinnedMeshRenderer renderer = actionRoot
                        .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                        .Single(candidate => candidate.sharedMesh != null);
                    bottle.SetActive(review.ShowBottle);
                    cigarette.SetActive(review.ShowCigarette);
                    for (int index = 0;
                         index < review.Times.Length;
                         index++)
                    {
                        float time = review.Times[index];
                        clip.SampleAnimation(animationTarget, time);
                        if (review.ShowBottle)
                        {
                            bottle.transform.SetPositionAndRotation(
                                drinkGrip.position,
                                drinkGrip.rotation);
                            bottle.transform.localScale = Vector3.one;
                        }

                        if (review.ShowCigarette)
                        {
                            Vector3 cigaretteLocalPosition =
                                new Vector3(0f, 0f, -0.028f);
                            Quaternion cigaretteLocalRotation =
                                Quaternion.Euler(90f, 0f, 0f);
                            cigarette.transform.SetPositionAndRotation(
                                cigaretteGrip.TransformPoint(
                                    cigaretteLocalPosition),
                                cigaretteGrip.rotation *
                                cigaretteLocalRotation);
                        }

                        // HDRP's first explicit render after constructing a
                        // camera/volume can be an all-black warm-up frame.
                        // Render twice so frame zero is audited just like every
                        // later sample instead of being mistaken for a hidden
                        // ready pose.
                        camera.Render();
                        camera.Render();
                        RenderTexture previous = RenderTexture.active;
                        RenderTexture.active = target;
                        pixels.ReadPixels(
                            new Rect(0f, 0f, target.width, target.height),
                            0,
                            0,
                            recalculateMipMaps: false);
                        pixels.Apply(updateMipmaps: false);
                        RenderTexture.active = previous;
                        string timeLabel = time.ToString(
                            "0.00",
                            CultureInfo.InvariantCulture);
                        File.WriteAllBytes(
                            Path.Combine(
                                output,
                                $"{review.Name}_{index:00}_{timeLabel}s.png"),
                            pixels.EncodeToPNG());

                        var baked = new Mesh();
                        try
                        {
                            renderer.BakeMesh(baked, useScale: true);
                            Bounds worldBounds = CalculateWorldBounds(
                                baked,
                                renderer.transform);
                            float maxEdge = CalculateMaximumTriangleEdge(
                                baked,
                                renderer.transform);
                            metrics.Add(string.Join(
                                ",",
                                review.Name,
                                timeLabel,
                                Format(palm.position.x),
                                Format(palm.position.y),
                                Format(palm.position.z),
                                Format(worldBounds.center.x),
                                Format(worldBounds.center.y),
                                Format(worldBounds.center.z),
                                Format(worldBounds.size.x),
                                Format(worldBounds.size.y),
                                Format(worldBounds.size.z),
                                Format(maxEdge)));
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(baked);
                        }
                    }
                }

                File.WriteAllLines(
                    Path.Combine(output, "metrics.csv"),
                    metrics);
                Debug.Log(
                    $"AXIS Unity camera audit written to '{output}'.");
            }
            finally
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                if (camera != null)
                {
                    camera.targetTexture = null;
                }

                if (target != null)
                {
                    target.Release();
                }

                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(handMaterial);
                UnityEngine.Object.DestroyImmediate(bottleMaterial);
                UnityEngine.Object.DestroyImmediate(cigaretteMaterial);
                UnityEngine.Object.DestroyImmediate(volumeProfile);
                UnityEngine.Object.DestroyImmediate(volumeObject);
                UnityEngine.Object.DestroyImmediate(keyLightObject);
                UnityEngine.Object.DestroyImmediate(fillLightObject);
                UnityEngine.Object.DestroyImmediate(bottle);
                UnityEngine.Object.DestroyImmediate(cigarette);
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [MenuItem(
            "Tools/My Summer Car/Legacy Import/" +
            "Capture Active AXIS Runtime Audit %&a")]
        public static void CaptureActiveRuntimeAudit()
        {
            FirstPersonLifeActionViewmodelBinding[] bindings =
                UnityEngine.Object.FindObjectsByType<
                    FirstPersonLifeActionViewmodelBinding>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            FirstPersonLifeActionViewmodelBinding binding = bindings
                .FirstOrDefault(candidate =>
                    candidate.gameObject.scene.IsValid() &&
                    candidate.transform.GetComponentInParent<Camera>(true) !=
                    null);
            if (binding == null)
            {
                throw new InvalidOperationException(
                    "No live Phase 1 viewmodel binding under a camera was " +
                    "found. Enter Play Mode and reproduce the broken gesture " +
                    "before running this audit.");
            }

            Camera camera = binding.transform.GetComponentInParent<Camera>(
                true);
            string projectRoot = Directory
                .GetParent(Application.dataPath)?.FullName ??
                throw new InvalidOperationException(
                    "Unity project root is unavailable.");
            string outputRoot = Path.Combine(
                projectRoot,
                "Logs",
                "AxisHandsActiveRuntimeAudit");
            Directory.CreateDirectory(outputRoot);
            var report = new List<string>
            {
                $"unity_playing={EditorApplication.isPlaying}",
                $"binding={GetHierarchyPath(binding.transform)}",
                $"binding_active={binding.gameObject.activeInHierarchy}",
                $"binding_local_position={Format(binding.transform.localPosition)}",
                $"binding_local_rotation={Format(binding.transform.localRotation.eulerAngles)}",
                $"binding_lossy_scale={Format(binding.transform.lossyScale)}",
                $"camera={GetHierarchyPath(camera.transform)}",
                $"camera_local_position={Format(camera.transform.localPosition)}",
                $"camera_local_rotation={Format(camera.transform.localRotation.eulerAngles)}",
                $"camera_lossy_scale={Format(camera.transform.lossyScale)}",
                $"camera_near_clip={Format(camera.nearClipPlane)}",
                $"camera_fov={camera.fieldOfView.ToString(CultureInfo.InvariantCulture)}",
                $"camera_aspect={camera.aspect.ToString(CultureInfo.InvariantCulture)}",
                $"active_visual={binding.ActiveVisual}",
            };

            foreach (Transform actionRoot in binding.transform)
            {
                report.Add(
                    $"action={actionRoot.name}|active_self=" +
                    $"{actionRoot.gameObject.activeSelf}|active_hierarchy=" +
                    $"{actionRoot.gameObject.activeInHierarchy}|local_position=" +
                    $"{Format(actionRoot.localPosition)}|local_rotation=" +
                    $"{Format(actionRoot.localRotation.eulerAngles)}|scale=" +
                    $"{Format(actionRoot.lossyScale)}");
            }

            report.Add("camera_descendant_renderers_begin");
            foreach (Renderer renderer in camera
                         .GetComponentsInChildren<Renderer>(true)
                         .Where(candidate => candidate.gameObject.scene.IsValid()))
            {
                Bounds bounds = renderer.bounds;
                Vector3 cameraLocalCenter = camera.transform
                    .InverseTransformPoint(bounds.center);
                string meshName = renderer switch
                {
                    SkinnedMeshRenderer skinned =>
                        skinned.sharedMesh != null
                            ? skinned.sharedMesh.name
                            : "<none>",
                    MeshRenderer meshRenderer =>
                        meshRenderer.GetComponent<MeshFilter>()?.sharedMesh != null
                            ? meshRenderer.GetComponent<MeshFilter>().sharedMesh.name
                            : "<none>",
                    _ => "<not-mesh>",
                };
                report.Add(
                    $"camera_renderer={GetHierarchyPath(renderer.transform)}|" +
                    $"type={renderer.GetType().Name}|active_self=" +
                    $"{renderer.gameObject.activeSelf}|active_hierarchy=" +
                    $"{renderer.gameObject.activeInHierarchy}|enabled=" +
                    $"{renderer.enabled}|mesh={meshName}|local_position=" +
                    $"{Format(renderer.transform.localPosition)}|lossy_scale=" +
                    $"{Format(renderer.transform.lossyScale)}|bounds_world=" +
                    $"{Format(bounds.center)}/{Format(bounds.size)}|" +
                    $"center_camera={Format(cameraLocalCenter)}");
            }

            report.Add("camera_descendant_renderers_end");

            foreach (SkinnedMeshRenderer renderer in binding
                         .GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var baked = new Mesh();
                try
                {
                    renderer.BakeMesh(baked, useScale: true);
                    Bounds worldBounds = CalculateWorldBounds(
                        baked,
                        renderer.transform);
                    Vector3 cameraLocalCenter = camera.transform
                        .InverseTransformPoint(worldBounds.center);
                    report.Add(
                        $"renderer={GetHierarchyPath(renderer.transform)}|" +
                        $"mesh={renderer.sharedMesh?.name}|quality=" +
                        $"{renderer.quality}|bones={renderer.bones.Length}|" +
                        $"root_bone={renderer.rootBone?.name}|used_bounds_world=" +
                        $"{Format(worldBounds.center)}/{Format(worldBounds.size)}|" +
                        $"used_center_camera={Format(cameraLocalCenter)}|" +
                        $"max_edge={Format(CalculateMaximumTriangleEdge(baked, renderer.transform))}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(baked);
                }
            }

            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var target = new RenderTexture(
                1280,
                720,
                24,
                RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(
                target.width,
                target.height,
                TextureFormat.RGBA32,
                mipChain: false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(
                    new Rect(0f, 0f, target.width, target.height),
                    0,
                    0,
                    recalculateMipMaps: false);
                pixels.Apply(updateMipmaps: false);
                File.WriteAllBytes(
                    Path.Combine(outputRoot, "GameCamera.png"),
                    pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }

            string reportPath = Path.Combine(outputRoot, "runtime.txt");
            File.WriteAllLines(reportPath, report);
            Debug.Log(
                $"Active AXIS runtime audit written to '{outputRoot}'.",
                binding);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var segments = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                segments.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", segments);
        }

        private static void ConfigureAuditDirectionalLight(
            GameObject lightObject,
            Vector3 eulerAngles,
            float intensityLux,
            Color color,
            bool castShadows)
        {
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.lightUnit = LightUnit.Lux;
            light.intensity = intensityLux;
            light.color = color;
            light.shadows = castShadows
                ? LightShadows.Soft
                : LightShadows.None;
            lightObject.AddComponent<HDAdditionalLightData>();
            lightObject.transform.rotation = Quaternion.Euler(eulerAngles);
        }

        private static Bounds CalculateWorldBounds(
            Mesh mesh,
            Transform transform)
        {
            Vector3[] vertices = mesh.vertices;
            int firstVertex = -1;
            for (int subMesh = 0;
                 subMesh < mesh.subMeshCount && firstVertex < 0;
                 subMesh++)
            {
                int[] indices = mesh.GetTriangles(subMesh);
                if (indices.Length > 0)
                {
                    firstVertex = indices[0];
                }
            }

            if (firstVertex < 0)
            {
                return new Bounds();
            }

            var bounds = new Bounds(
                transform.TransformPoint(vertices[firstVertex]),
                Vector3.zero);
            // The side-specific mesh deliberately keeps the FBX vertex and
            // bind-pose arrays stable while replacing its index buffers.  A
            // raw vertex scan therefore includes unused opposite-side and
            // shoulder vertices and reports nonsense metre-wide bounds.  Only
            // triangles that can actually render belong in the audit.
            for (int subMesh = 0;
                 subMesh < mesh.subMeshCount;
                 subMesh++)
            {
                int[] indices = mesh.GetTriangles(subMesh);
                foreach (int index in indices)
                {
                    bounds.Encapsulate(
                        transform.TransformPoint(vertices[index]));
                }
            }

            return bounds;
        }

        private static float CalculateMaximumTriangleEdge(
            Mesh mesh,
            Transform transform)
        {
            Vector3[] vertices = mesh.vertices;
            float maximum = 0f;
            for (int subMesh = 0;
                 subMesh < mesh.subMeshCount;
                 subMesh++)
            {
                int[] triangles = mesh.GetTriangles(subMesh);
                for (int index = 0;
                     index < triangles.Length;
                     index += 3)
                {
                    Vector3 a = transform.TransformPoint(
                        vertices[triangles[index]]);
                    Vector3 b = transform.TransformPoint(
                        vertices[triangles[index + 1]]);
                    Vector3 c = transform.TransformPoint(
                        vertices[triangles[index + 2]]);
                    maximum = Mathf.Max(
                        maximum,
                        Vector3.Distance(a, b),
                        Vector3.Distance(b, c),
                        Vector3.Distance(c, a));
                }
            }

            return maximum;
        }

        private static string Format(float value) => value.ToString(
            "0.000000",
            CultureInfo.InvariantCulture);

        private static string Format(Vector3 value) =>
            $"({Format(value.x)}, {Format(value.y)}, {Format(value.z)})";

        private static void ValidateInstalledSource()
        {
            foreach (SourceSpec spec in SourceSpecs)
            {
                string filePath = ToFileSystemPath(spec.AssetPath);
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException(
                        $"Licensed AXIS source '{spec.Role}' is missing.",
                        filePath);
                }

                string actual = ComputeSha256(filePath);
                if (!string.Equals(
                        actual,
                        spec.Sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Licensed AXIS source hash mismatch for " +
                        $"'{spec.Role}'. Re-run the deterministic Blender " +
                        "export before rebuilding Unity presentation.");
                }
            }
        }

        private static void ConfigureSourceImporters()
        {
            if (!(AssetImporter.GetAtPath(ModelAssetPath) is
                    ModelImporter importer))
            {
                throw new InvalidOperationException(
                    "Licensed AXIS model importer is unavailable.");
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode =
                ModelImporterMaterialImportMode.None;
            importer.globalScale = 1f;
            importer.isReadable = true;
            // AXIS ships driver-authored corrective shape keys for the full
            // two-arm rig. Blender's FBX exporter bakes every driver into the
            // clips. After Unity swaps the full mesh for one extracted arm,
            // those curves are no longer a compatible deformation stack:
            // several reach 80-100% and pull detached palms/fingers through
            // the camera. The approved first-person poses are already baked
            // into the exported bone animation, so keep this viewmodel
            // strictly bone-skinned and reject blend-shape curves below.
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            // The licensed AXIS mesh genuinely uses up to eight influences on
            // 1,308 vertices.  Unity's four-weight default tears the fingers
            // and forearm when the corrective hand poses are sampled.
            importer.skinWeights = ModelImporterSkinWeights.Custom;
            importer.maxBonesPerVertex = 8;
            importer.minBoneWeight = 0f;
            importer.optimizeGameObjects = false;
            importer.animationCompression =
                ModelImporterAnimationCompression.Off;
            importer.resampleCurves = false;
            importer.SaveAndReimport();

            ConfigureTexture(
                AlbedoAssetPath,
                TextureImporterType.Default,
                sRgb: true,
                maxSize: 4096);
            ConfigureTexture(
                NormalAssetPath,
                TextureImporterType.NormalMap,
                sRgb: false,
                maxSize: 4096);
            ConfigureTexture(
                RoughnessAssetPath,
                TextureImporterType.Default,
                sRgb: false,
                maxSize: 4096);
        }

        private static void ConfigureTexture(
            string path,
            TextureImporterType type,
            bool sRgb,
            int maxSize)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                throw new InvalidOperationException(
                    $"Texture importer is unavailable for '{path}'.");
            }

            importer.textureType = type;
            importer.sRGBTexture = sRgb;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = maxSize;
            importer.textureCompression =
                TextureImporterCompression.CompressedHQ;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static void DeleteGeneratedAssets()
        {
            foreach (string path in HashedAssetPaths)
            {
                if (path.StartsWith(
                        GeneratedRoot,
                        StringComparison.Ordinal) &&
                    AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        private static Material CreateHandMaterial()
        {
            Shader shader = Shader.Find("HDRP/Lit") ??
                throw new InvalidOperationException(
                    "HDRP/Lit shader is unavailable for AXIS hands.");
            var material = new Material(shader)
            {
                name = "Licensed AXIS Neutral Arms HDRP",
                enableInstancing = true,
            };
            material.SetTexture(
                "_BaseColorMap",
                RequireAsset<Texture2D>(AlbedoAssetPath));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture(
                "_NormalMap",
                RequireAsset<Texture2D>(NormalAssetPath));
            material.SetFloat("_NormalScale", 0.72f);
            material.SetTexture(
                "_SpecularColorMap",
                RequireAsset<Texture2D>(RoughnessAssetPath));
            material.SetColor("_SpecularColor", new Color(0.04f, 0.04f, 0.04f));
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.32f);
            material.SetFloat("_SmoothnessRemapMin", 0.08f);
            material.SetFloat("_SmoothnessRemapMax", 0.46f);
            material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
            material.EnableKeyword("_MATERIAL_FEATURE_SPECULAR_COLOR");
            AssetDatabase.CreateAsset(material, MaterialAssetPath);
            return material;
        }

        private static Mesh CreateSideMesh(
            GameObject sourceModel,
            string side,
            string assetPath)
        {
            GameObject instance = UnityEngine.Object.Instantiate(sourceModel);
            try
            {
                SkinnedMeshRenderer renderer = instance
                    .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(candidate => candidate.sharedMesh != null);
                Mesh source = renderer.sharedMesh;
                NativeArray<byte> bonesPerVertex =
                    source.GetBonesPerVertex();
                NativeArray<BoneWeight1> allWeights =
                    source.GetAllBoneWeights();
                if (!bonesPerVertex.IsCreated ||
                    bonesPerVertex.Length != source.vertexCount ||
                    !allWeights.IsCreated)
                {
                    if (bonesPerVertex.IsCreated)
                    {
                        bonesPerVertex.Dispose();
                    }

                    if (allWeights.IsCreated)
                    {
                        allWeights.Dispose();
                    }

                    throw new InvalidOperationException(
                        "Licensed AXIS mesh has no compatible skin weights.");
                }

                string selectedSuffix = "." + side;
                string rejectedSuffix = side == "R" ? ".L" : ".R";
                var selectedBones = new HashSet<int>();
                var rejectedBones = new HashSet<int>();
                for (int index = 0; index < renderer.bones.Length; index++)
                {
                    Transform bone = renderer.bones[index];
                    if (bone == null)
                    {
                        continue;
                    }

                    if (bone.name.EndsWith(
                            selectedSuffix,
                            StringComparison.Ordinal) &&
                        IsViewmodelDistalBone(bone.name))
                    {
                        selectedBones.Add(index);
                    }
                    else if (bone.name.EndsWith(
                                 rejectedSuffix,
                                 StringComparison.Ordinal))
                    {
                        rejectedBones.Add(index);
                    }
                }

                if (selectedBones.Count == 0 || rejectedBones.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Licensed AXIS {side}-arm bone classification " +
                        "failed.");
                }

                float[] selected = new float[source.vertexCount];
                float[] rejected = new float[source.vertexCount];
                int weightIndex = 0;
                for (int vertex = 0;
                     vertex < source.vertexCount;
                     vertex++)
                {
                    int influenceCount = bonesPerVertex[vertex];
                    for (int influence = 0;
                         influence < influenceCount;
                         influence++)
                    {
                        BoneWeight1 weight = allWeights[weightIndex++];
                        Accumulate(
                            weight.boneIndex,
                            weight.weight,
                            selectedBones,
                            rejectedBones,
                            ref selected[vertex],
                            ref rejected[vertex]);
                    }
                }

                bonesPerVertex.Dispose();
                allWeights.Dispose();

                Mesh result = UnityEngine.Object.Instantiate(source);
                result.name = $"AXIS Neutral Arms {side} Viewmodel";
                int retainedTriangles = 0;
                for (int subMesh = 0;
                     subMesh < source.subMeshCount;
                     subMesh++)
                {
                    int[] sourceTriangles = source.GetTriangles(subMesh);
                    var triangles = new List<int>(sourceTriangles.Length / 2);
                    for (int triangle = 0;
                         triangle < sourceTriangles.Length;
                         triangle += 3)
                    {
                        int a = sourceTriangles[triangle];
                        int b = sourceTriangles[triangle + 1];
                        int c = sourceTriangles[triangle + 2];
                        // A summed vote can retain a triangle with one vertex
                        // from the requested arm and two from the opposite arm,
                        // creating the long spikes visible in Scene View.  All
                        // three vertices must belong to the requested side.
                        if (!BelongsToSide(a, selected, rejected) ||
                            !BelongsToSide(b, selected, rejected) ||
                            !BelongsToSide(c, selected, rejected))
                        {
                            continue;
                        }

                        triangles.Add(a);
                        triangles.Add(b);
                        triangles.Add(c);
                        retainedTriangles++;
                    }

                    result.SetTriangles(
                        triangles,
                        subMesh,
                        calculateBounds: false);
                }

                if (retainedTriangles == 0)
                {
                    UnityEngine.Object.DestroyImmediate(result);
                    throw new InvalidOperationException(
                        $"Licensed AXIS {side}-arm extraction produced " +
                        "no triangles.");
                }

                result.RecalculateBounds();
                AssetDatabase.CreateAsset(result, assetPath);
                return result;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static bool BelongsToSide(
            int vertex,
            IReadOnlyList<float> selected,
            IReadOnlyList<float> rejected)
        {
            return selected[vertex] >
                    ViewmodelDistalBoneWeightThreshold &&
                selected[vertex] > rejected[vertex];
        }

        private static bool IsViewmodelDistalBone(string boneName)
        {
            // The licensed source contains the complete shoulder cap.  It is
            // anatomically useful in a third-person body, but from a camera-
            // local viewmodel it sits inside the near plane and becomes the
            // giant crescent seen in Game View.  Retain the hand and both
            // forearm twist segments; their weighted transition creates one
            // clean elbow-side cut that remains below the gameplay frame.
            return boneName.IndexOf(
                       "forearm",
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                boneName.IndexOf(
                    "hand",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                boneName.IndexOf(
                    "palm",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                boneName.IndexOf(
                    "thumb",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                boneName.IndexOf(
                    "f_index",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                boneName.IndexOf(
                    "f_middle",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                boneName.IndexOf(
                    "f_ring",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                boneName.IndexOf(
                    "f_pinky",
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void Accumulate(
            int boneIndex,
            float weight,
            HashSet<int> selectedBones,
            HashSet<int> rejectedBones,
            ref float selected,
            ref float rejected)
        {
            if (selectedBones.Contains(boneIndex))
            {
                selected += weight;
            }
            else if (rejectedBones.Contains(boneIndex))
            {
                rejected += weight;
            }
        }

        private static IReadOnlyDictionary<string, AnimationClip>
            ExtractLegacyClips()
        {
            AnimationClip[] imported = AssetDatabase
                .LoadAllAssetsAtPath(ModelAssetPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith(
                    "__preview__",
                    StringComparison.Ordinal))
                .ToArray();
            AnimationClip compactFistReference = imported.SingleOrDefault(
                clip => clip.name.EndsWith(
                    "AxisArms_ThumbUp",
                    StringComparison.Ordinal));
            if (compactFistReference == null)
            {
                throw new InvalidOperationException(
                    "Licensed AXIS compact-fist reference clip is missing.");
            }

            var clips = new Dictionary<string, AnimationClip>(
                StringComparer.Ordinal);
            foreach (ClipSpec spec in ClipSpecs)
            {
                AnimationClip source = imported.SingleOrDefault(clip =>
                    clip.name.EndsWith(spec.SourceName, StringComparison.Ordinal));
                if (source == null)
                {
                    throw new InvalidOperationException(
                        $"Licensed AXIS FBX clip '{spec.SourceName}' is " +
                        "missing.");
                }

                AnimationClip copy = UnityEngine.Object.Instantiate(source);
                copy.name = spec.SourceName;
                copy.legacy = true;
                copy.frameRate = 60f;
                copy.wrapMode = WrapMode.Once;
                RemoveBlendShapeCurves(copy);
                if (string.Equals(
                        spec.SourceName,
                        "AxisArms_MiddleFinger",
                        StringComparison.Ordinal))
                {
                    ApplyCompactMiddleFingerCurl(
                        copy,
                        compactFistReference);
                    RaiseMiddleFingerThumb(
                        copy,
                        compactFistReference);
                }
                AnimationUtility.SetAnimationEvents(
                    copy,
                    Array.Empty<AnimationEvent>());
                AssetDatabase.CreateAsset(copy, spec.AssetPath);
                AnimationClip saved = RequireAsset<AnimationClip>(
                    spec.AssetPath);
                if (Mathf.Abs(saved.length - spec.ExpectedLength) > 0.02f)
                {
                    throw new InvalidOperationException(
                        $"Licensed AXIS clip '{spec.SourceName}' has " +
                        $"unexpected length {saved.length:0.000}s; expected " +
                        $"{spec.ExpectedLength:0.000}s.");
                }

                clips.Add(spec.SourceName, saved);
            }

            return clips;
        }

        private static void RemoveBlendShapeCurves(AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type == typeof(SkinnedMeshRenderer) &&
                    binding.propertyName.StartsWith(
                        "blendShape.",
                        StringComparison.Ordinal))
                {
                    AnimationUtility.SetEditorCurve(clip, binding, null);
                }
            }
        }

        private static void ApplyCompactMiddleFingerCurl(
            AnimationClip clip,
            AnimationClip compactFistReference)
        {
            // The purchased rig's photo-reference pose was authored with the
            // MCP joints almost fully closed.  On this mesh that makes the
            // phalanges read like three separate cylinders.  Reuse the same
            // rig's already-validated compact curl from thumbs-up for only the
            // index, ring and pinky.  The extended middle finger, photographed
            // thumb, wrist and full arm motion remain untouched.
            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Transform) ||
                    !binding.propertyName.StartsWith(
                        "localEulerAnglesRaw.",
                        StringComparison.Ordinal) ||
                    (!binding.path.Contains(
                         "/DEF-f_index.",
                         StringComparison.Ordinal) &&
                     !binding.path.Contains(
                         "/DEF-f_ring.",
                         StringComparison.Ordinal) &&
                     !binding.path.Contains(
                         "/DEF-f_pinky.",
                         StringComparison.Ordinal)))
                {
                    continue;
                }

                AnimationCurve referenceCurve =
                    AnimationUtility.GetEditorCurve(
                        compactFistReference,
                        binding);
                if (referenceCurve == null)
                {
                    throw new InvalidOperationException(
                        $"Compact-fist curve '{binding.path}/" +
                        $"{binding.propertyName}' is missing.");
                }

                float value = referenceCurve.Evaluate(
                    Mathf.Min(0.4f, compactFistReference.length * 0.5f));
                var compactCurve = AnimationCurve.Linear(
                    0f,
                    value,
                    clip.length,
                    value);
                AnimationUtility.SetEditorCurve(
                    clip,
                    binding,
                    compactCurve);
            }
        }

        private static void RaiseMiddleFingerThumb(
            AnimationClip clip,
            AnimationClip thumbUpReference)
        {
            // The user's left-hand reference has the thumb raised diagonally,
            // not stretched horizontally like a barrier arm. Blend only its
            // local pose partway toward the already-approved thumbs-up while
            // keeping the middle-finger thumb visibly open.
            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Transform) ||
                    !binding.propertyName.StartsWith(
                        "localEulerAnglesRaw.",
                        StringComparison.Ordinal) ||
                    !binding.path.Contains(
                        "/DEF-thumb.",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                AnimationCurve source = AnimationUtility.GetEditorCurve(
                    clip,
                    binding);
                AnimationCurve reference = AnimationUtility.GetEditorCurve(
                    thumbUpReference,
                    binding);
                if (source == null || reference == null)
                {
                    throw new InvalidOperationException(
                        $"Middle-finger thumb curve '{binding.path}/" +
                        $"{binding.propertyName}' is missing.");
                }

                float sourceValue = source.Evaluate(0.45f);
                float raisedValue = reference.Evaluate(
                    Mathf.Min(0.4f, thumbUpReference.length * 0.5f));
                float value = Mathf.LerpAngle(
                    sourceValue,
                    raisedValue,
                    1f);
                AnimationUtility.SetEditorCurve(
                    clip,
                    binding,
                    AnimationCurve.Linear(0f, value, clip.length, value));
            }

            // The fist reference supplies a sound thumb curl, but its whole
            // hand is rolled for the thumbs-up composition.  The middle-finger
            // palm stays camera-facing, so add the small root yaw seen in the
            // user's left-hand photo instead of rotating the whole wrist.
            EditorCurveBinding thumbRootYaw = AnimationUtility
                .GetCurveBindings(clip)
                .Single(binding =>
                    binding.type == typeof(Transform) &&
                    binding.path.EndsWith(
                        "/DEF-thumb.01.L",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        binding.propertyName,
                        "localEulerAnglesRaw.z",
                        StringComparison.Ordinal));
            AnimationCurve rootYawCurve = AnimationUtility.GetEditorCurve(
                clip,
                thumbRootYaw);
            float rootYaw = rootYawCurve.Evaluate(0.45f) + 22f;
            AnimationUtility.SetEditorCurve(
                clip,
                thumbRootYaw,
                AnimationCurve.Linear(0f, rootYaw, clip.length, rootYaw));
        }

        private static AxisAction CreateAction(
            string name,
            Transform prefabRoot,
            GameObject sourceModel,
            Material material,
            Mesh sideMesh)
        {
            var root = new GameObject(name);
            root.transform.SetParent(prefabRoot, false);
            GameObject model = UnityEngine.Object.Instantiate(sourceModel);
            model.name = "Licensed AXIS Neutral Arms";
            model.transform.SetParent(root.transform, false);
            model.transform.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);

            foreach (Animator animator in
                     model.GetComponentsInChildren<Animator>(true))
            {
                UnityEngine.Object.DestroyImmediate(animator);
            }

            SkinnedMeshRenderer[] renderers =
                model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer primary = renderers.Single(renderer =>
                renderer.sharedMesh != null);
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                if (renderer != primary)
                {
                    UnityEngine.Object.DestroyImmediate(renderer.gameObject);
                }
            }

            primary.sharedMesh = sideMesh;
            primary.sharedMaterial = material;
            for (int index = 0;
                 index < primary.sharedMesh.blendShapeCount;
                 index++)
            {
                primary.SetBlendShapeWeight(index, 0f);
            }
            // First-person hands sit centimetres from the lens.  Realtime
            // self-shadow maps at world-camera bias values draw black seams
            // across knuckles and make intact phalanges look torn.  Keep HDRP
            // direct/ambient lighting and the normal map, but do not let this
            // presentation-only mesh cast or receive world shadows.
            primary.shadowCastingMode = ShadowCastingMode.Off;
            primary.receiveShadows = false;
            primary.updateWhenOffscreen = true;
            primary.forceMatrixRecalculationPerRender = true;
            primary.skinnedMotionVectors = false;
            primary.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            primary.quality = SkinQuality.Auto;

            Animation animation = model.AddComponent<Animation>();
            animation.playAutomatically = false;
            animation.animatePhysics = false;
            animation.cullingType = AnimationCullingType.AlwaysAnimate;
            return new AxisAction(root, animation, model.transform, primary);
        }

        private static Transform CreatePalmLocalBottleGrip(AxisAction action)
        {
            Transform palm = RequireDescendant(action.Model, "DEF-hand.R");
            Transform indexBase = RequireDescendant(
                action.Model,
                "DEF-f_index.01.R");
            Transform middleBase = RequireDescendant(
                action.Model,
                "DEF-f_middle.01.R");
            Transform ringBase = RequireDescendant(
                action.Model,
                "DEF-f_ring.01.R");
            Transform pinkyBase = RequireDescendant(
                action.Model,
                "DEF-f_pinky.01.R");
            Vector3 forward = (
                (indexBase.position - palm.position) +
                (middleBase.position - palm.position) +
                (ringBase.position - palm.position) +
                (pinkyBase.position - palm.position))
                .normalized;
            Vector3 across = (pinkyBase.position - indexBase.position)
                .normalized;
            Vector3 normal = Vector3.Cross(forward, across).normalized;
            // FBX coordinate conversion can preserve the palm basis while
            // reversing which of its two normals Cross() returns.  The old
            // result put the bottle behind the back of the hand.  This is a
            // camera-local viewmodel, so choose the normal facing the camera
            // for the positional clearance while retaining the approved
            // bottle rotation below.
            Vector3 positionNormal = normal;
            Vector3 towardCamera = -palm.position.normalized;
            if (Vector3.Dot(positionNormal, towardCamera) < 0f)
            {
                positionNormal = -positionNormal;
            }
            Vector3 bottleAxis = -Vector3.Cross(normal, forward).normalized;
            if (bottleAxis.z < 0f)
            {
                bottleAxis = -bottleAxis;
            }

            Vector3 tiltDirection =
                (forward * BottleTiltForward + normal * BottleTiltNormal)
                .normalized;
            bottleAxis = Quaternion.AngleAxis(
                    BottleTiltDegrees,
                    Vector3.Cross(bottleAxis, tiltDirection).normalized) *
                bottleAxis;
            Vector3 position =
                palm.position +
                forward * BottlePalmOffset +
                // The approved Blender proxy uses the full bottle-radius
                // normal offset. With the real near-camera mesh that depth
                // produces visible palm/prop parallax while drinking, so keep
                // only a small camera-facing clearance here.
                positionNormal * BottleCameraFacingClearance +
                bottleAxis * BottleAxialOffset;
            var gripObject = new GameObject("Beer Bottle Grip Anchor");
            Transform grip = gripObject.transform;
            Vector3 bottleUp = Vector3.ProjectOnPlane(normal, bottleAxis)
                .normalized;
            grip.SetPositionAndRotation(
                position,
                // The imported bottle mesh has its neck on local -Z.  The
                // hand-space axis points toward the mouth, so local -Z must
                // follow it. Mapping +Z here aimed the recessed base at the
                // camera and sent the neck over the player's shoulder.
                Quaternion.LookRotation(-bottleAxis, bottleUp));
            grip.SetParent(palm, worldPositionStays: true);
            return grip;
        }

        private static Transform CreateCigaretteGrip(AxisAction action)
        {
            Transform indexTip = RequireDescendant(
                action.Model,
                "DEF-f_index.03.R");
            Transform middleTip = RequireDescendant(
                action.Model,
                "DEF-f_middle.03.R");
            Transform indexMiddle = RequireDescendant(
                action.Model,
                "DEF-f_index.02.R");
            Transform middleMiddle = RequireDescendant(
                action.Model,
                "DEF-f_middle.02.R");
            Vector3 position = Vector3.Lerp(
                Vector3.Lerp(indexMiddle.position, indexTip.position, 0.38f),
                Vector3.Lerp(middleMiddle.position, middleTip.position, 0.38f),
                0.5f);
            Vector3 fingerForward = (
                    (indexTip.position - indexMiddle.position) +
                    (middleTip.position - middleMiddle.position))
                .normalized;
            Vector3 fingerAcross = (
                    middleMiddle.position - indexMiddle.position)
                .normalized;
            Vector3 up = Vector3.Cross(fingerForward, fingerAcross).normalized;
            var gripObject = new GameObject("Cigarette Grip Anchor");
            Transform grip = gripObject.transform;
            grip.SetPositionAndRotation(
                position,
                // A cigarette crosses the gap between the two fingers; it
                // does not run lengthwise down either phalanx.
                Quaternion.LookRotation(fingerAcross, up));
            // The finger curl is identical across the smoke clips, so keeping
            // the prop on the index middle phalanx makes it follow both the
            // wrist motion and the grip without introducing runtime IK.
            grip.SetParent(indexMiddle, worldPositionStays: true);
            return grip;
        }

        private static Transform RequireDescendant(
            Transform root,
            string name)
        {
            Transform match = root
                .GetComponentsInChildren<Transform>(true)
                .SingleOrDefault(candidate => string.Equals(
                    candidate.name,
                    name,
                    StringComparison.Ordinal));
            return match ?? throw new InvalidOperationException(
                $"Licensed AXIS rig transform '{name}' is missing.");
        }

        private static void EnsureAssetFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static T RequireAsset<T>(string path)
            where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path) ??
                throw new InvalidOperationException(
                    $"Required AXIS asset '{path}' is unavailable.");
        }

        private static string ToFileSystemPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?
                .FullName ?? throw new InvalidOperationException(
                    "Unity project root is unavailable.");
            return Path.GetFullPath(Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ComputeSha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            return string.Concat(
                sha256.ComputeHash(stream)
                    .Select(value => value.ToString("x2")));
        }

        public sealed class AxisHandsBuildResult
        {
            internal AxisHandsBuildResult(
                AxisAction drink,
                AnimationClip drinkClip,
                AnimationClip drinkShortClip,
                AnimationClip drinkThrowClip,
                AnimationClip drinkSprayClip,
                Transform drinkGripAnchor,
                AxisAction wave,
                AnimationClip waveClip,
                AxisAction middleFinger,
                AnimationClip middleFingerClip,
                AxisAction smoke,
                AnimationClip smokeInClip,
                AnimationClip smokeLightUpClip,
                AnimationClip smokeOutClip,
                AnimationClip smokePutOffClip,
                AnimationClip smokeResetClip,
                Transform cigaretteGripAnchor,
                AxisAction push,
                AnimationClip pushOnClip,
                AnimationClip pushOffClip,
                AxisAction thumbUp,
                AnimationClip thumbUpClip)
            {
                DrinkRoot = drink.Root;
                DrinkAnimation = drink.Animation;
                DrinkClip = drinkClip;
                DrinkShortClip = drinkShortClip;
                DrinkThrowClip = drinkThrowClip;
                DrinkSprayClip = drinkSprayClip;
                DrinkGripAnchor = drinkGripAnchor;
                WaveRoot = wave.Root;
                WaveAnimation = wave.Animation;
                WaveClip = waveClip;
                MiddleFingerRoot = middleFinger.Root;
                MiddleFingerAnimation = middleFinger.Animation;
                MiddleFingerClip = middleFingerClip;
                SmokeRoot = smoke.Root;
                SmokeAnimation = smoke.Animation;
                SmokeInClip = smokeInClip;
                SmokeLightUpClip = smokeLightUpClip;
                SmokeOutClip = smokeOutClip;
                SmokePutOffClip = smokePutOffClip;
                SmokeResetClip = smokeResetClip;
                CigaretteGripAnchor = cigaretteGripAnchor;
                PushRoot = push.Root;
                PushAnimation = push.Animation;
                PushOnClip = pushOnClip;
                PushOffClip = pushOffClip;
                ThumbUpRoot = thumbUp.Root;
                ThumbUpAnimation = thumbUp.Animation;
                ThumbUpClip = thumbUpClip;
                HandMesh = drink.Renderer.sharedMesh;
            }

            public GameObject DrinkRoot { get; }

            public Animation DrinkAnimation { get; }

            public AnimationClip DrinkClip { get; }

            public AnimationClip DrinkShortClip { get; }

            public AnimationClip DrinkThrowClip { get; }

            public AnimationClip DrinkSprayClip { get; }

            public Transform DrinkGripAnchor { get; }

            public GameObject WaveRoot { get; }

            public Animation WaveAnimation { get; }

            public AnimationClip WaveClip { get; }

            public GameObject MiddleFingerRoot { get; }

            public Animation MiddleFingerAnimation { get; }

            public AnimationClip MiddleFingerClip { get; }

            public GameObject SmokeRoot { get; }

            public Animation SmokeAnimation { get; }

            public AnimationClip SmokeInClip { get; }

            public AnimationClip SmokeLightUpClip { get; }

            public AnimationClip SmokeOutClip { get; }

            public AnimationClip SmokePutOffClip { get; }

            public AnimationClip SmokeResetClip { get; }

            public Transform CigaretteGripAnchor { get; }

            public GameObject PushRoot { get; }

            public Animation PushAnimation { get; }

            public AnimationClip PushOnClip { get; }

            public AnimationClip PushOffClip { get; }

            public GameObject ThumbUpRoot { get; }

            public Animation ThumbUpAnimation { get; }

            public AnimationClip ThumbUpClip { get; }

            public Mesh HandMesh { get; }
        }

        internal sealed class AxisAction
        {
            public AxisAction(
                GameObject root,
                Animation animation,
                Transform model,
                SkinnedMeshRenderer renderer)
            {
                Root = root;
                Animation = animation;
                Model = model;
                Renderer = renderer;
            }

            public GameObject Root { get; }

            public Animation Animation { get; }

            public Transform Model { get; }

            public SkinnedMeshRenderer Renderer { get; }
        }

        private sealed class SourceSpec
        {
            public SourceSpec(string role, string assetPath, string sha256)
            {
                Role = role;
                AssetPath = assetPath;
                Sha256 = sha256;
            }

            public string Role { get; }

            public string AssetPath { get; }

            public string Sha256 { get; }
        }

        private sealed class ClipSpec
        {
            public ClipSpec(
                string sourceName,
                string assetPath,
                float expectedLength)
            {
                SourceName = sourceName;
                AssetPath = assetPath;
                ExpectedLength = expectedLength;
            }

            public string SourceName { get; }

            public string AssetPath { get; }

            public float ExpectedLength { get; }
        }

        private sealed class AuditReview
        {
            public AuditReview(
                string name,
                string actionRootName,
                string clipAssetPath,
                float[] times,
                bool showBottle,
                bool showCigarette)
            {
                Name = name;
                ActionRootName = actionRootName;
                ClipAssetPath = clipAssetPath;
                Times = times;
                ShowBottle = showBottle;
                ShowCigarette = showCigarette;
            }

            public string Name { get; }

            public string ActionRootName { get; }

            public string ClipAssetPath { get; }

            public float[] Times { get; }

            public bool ShowBottle { get; }

            public bool ShowCigarette { get; }
        }
    }
}
