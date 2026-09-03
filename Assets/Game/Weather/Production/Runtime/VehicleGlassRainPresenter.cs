using System;
using System.Collections.Generic;
using MSC.Weather.Domain;
using UnityEngine;

namespace MSC.Weather.Production
{
    /// <summary>
    /// Project-owned replacement for the donor windshield component. The donor
    /// drives one 512 px rain atlas shared by the five Satsuma window renderers;
    /// this presenter keeps that contract without importing the old script or
    /// its obsolete shaders.
    /// </summary>
    [DefaultExecutionOrder(-320)]
    [DisallowMultipleComponent]
    public sealed class VehicleGlassRainPresenter : MonoBehaviour
    {
        public const int DonorTextureSize = 512;
        public const float DonorGravityMultiplier = 40f;
        public const float DonorRainTransitionSeconds = 6f;
        public const float DonorFrontRainOpacity = 0.5f;
        public const float DonorCabinRainOpacity = 0.2f;

        private const float SimulationIntervalSeconds = 1f / 12f;
        private const float RoofResolveIntervalSeconds = 0.2f;
        private const float OwnerResolveIntervalSeconds = 0.5f;
        private const float RoofProbeDistanceMeters = 40f;
        private const float RoofProbeSpreadMeters = 0.45f;
        private const float DonorVehicleSpeedMultiplier = 8f;

        private static readonly int DetailMapId =
            Shader.PropertyToID("_DetailMap");
        private static readonly int DetailAlbedoScaleId =
            Shader.PropertyToID("_DetailAlbedoScale");
        private static readonly int DetailNormalScaleId =
            Shader.PropertyToID("_DetailNormalScale");
        private static readonly int DetailSmoothnessScaleId =
            Shader.PropertyToID("_DetailSmoothnessScale");

        [Serializable]
        public struct RainType
        {
            [SerializeField, Min(0)] private int dropsPerFrameAt60Fps;
            [SerializeField, Min(0f)] private float dryingSpeed;
            [SerializeField, Min(0f)] private float dropSizePixels;

            public RainType(
                int configuredDropsPerFrameAt60Fps,
                float configuredDryingSpeed,
                float configuredDropSizePixels)
            {
                dropsPerFrameAt60Fps = configuredDropsPerFrameAt60Fps;
                dryingSpeed = configuredDryingSpeed;
                dropSizePixels = configuredDropSizePixels;
            }

            public int DropsPerFrameAt60Fps => dropsPerFrameAt60Fps;
            public float DryingSpeed => dryingSpeed;
            public float DropSizePixels => dropSizePixels;
        }

        [SerializeField] private Renderer[] windowRenderers =
            Array.Empty<Renderer>();
        [SerializeField] private float[] rainOpacityByRenderer =
            Array.Empty<float>();
        [SerializeField] private Rigidbody vehicleBody;
        [SerializeField] private int textureSize = DonorTextureSize;
        [SerializeField] private float gravityMultiplier =
            DonorGravityMultiplier;
        [SerializeField] private LayerMask roofLayers = ~0;
        [SerializeField] private RainType[] rainTypes =
        {
            new RainType(0, 2.45f, 1f),
            new RainType(450, 2f, 3f),
            new RainType(900, 2f, 4f),
        };

        private readonly RaycastHit[] roofHitBuffer = new RaycastHit[64];
        private readonly Vector3[] roofProbeOffsets =
        {
            Vector3.zero,
            Vector3.right * RoofProbeSpreadMeters,
            Vector3.left * RoofProbeSpreadMeters,
            Vector3.forward * RoofProbeSpreadMeters,
            Vector3.back * RoofProbeSpreadMeters,
        };

        private ProductionEnvironmentController environment;
        private MaterialPropertyBlock propertyBlock;
        private MaterialPropertyBlock[] originalPropertyBlocks =
            Array.Empty<MaterialPropertyBlock>();
        private bool[] originalPropertyBlockCaptured = Array.Empty<bool>();
        private Texture2D rainDetailTexture;
        private byte[] wetness;
        private byte[] nextWetness;
        private Color32[] detailPixels;
        private uint randomState = 0x9e3779b9U;
        private float weatherRainTarget01;
        private float weatherWindMetersPerSecond;
        private float currentRain01;
        private float roofExposure01 = 1f;
        private float simulationAccumulator;
        private float nextRoofResolveTime;
        private float nextOwnerResolveTime;

        public IReadOnlyList<Renderer> WindowRenderers =>
            windowRenderers ?? Array.Empty<Renderer>();
        public IReadOnlyList<float> RainOpacityByRenderer =>
            rainOpacityByRenderer ?? Array.Empty<float>();
        public IReadOnlyList<RainType> RainTypes =>
            rainTypes ?? Array.Empty<RainType>();
        public Rigidbody VehicleBody => vehicleBody;
        public int TextureSize => textureSize;
        public float GravityMultiplier => gravityMultiplier;
        public float CurrentRain01 => currentRain01;
        public float RoofExposure01 => roofExposure01;
        public int LiveWetPixelCount { get; private set; }
        public uint TextureUpdateCount { get; private set; }
        public Texture RainDetailTexture => rainDetailTexture;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            Renderer[] authoredWindowRenderers,
            float[] authoredRainOpacityByRenderer,
            Rigidbody authoredVehicleBody)
        {
            if (authoredWindowRenderers == null ||
                authoredRainOpacityByRenderer == null ||
                authoredWindowRenderers.Length == 0 ||
                authoredWindowRenderers.Length !=
                authoredRainOpacityByRenderer.Length)
            {
                throw new ArgumentException(
                    "Vehicle glass rain renderers and opacity values must be non-empty and aligned.");
            }

            for (int index = 0;
                 index < authoredWindowRenderers.Length;
                 index++)
            {
                if (authoredWindowRenderers[index] == null ||
                    !float.IsFinite(authoredRainOpacityByRenderer[index]) ||
                    authoredRainOpacityByRenderer[index] < 0f ||
                    authoredRainOpacityByRenderer[index] > 1f)
                {
                    throw new ArgumentException(
                        "Vehicle glass rain authoring contains an invalid renderer or opacity.");
                }
            }

            windowRenderers = (Renderer[])authoredWindowRenderers.Clone();
            rainOpacityByRenderer =
                (float[])authoredRainOpacityByRenderer.Clone();
            vehicleBody = authoredVehicleBody != null
                ? authoredVehicleBody
                : throw new ArgumentNullException(nameof(authoredVehicleBody));
            textureSize = DonorTextureSize;
            gravityMultiplier = DonorGravityMultiplier;
            rainTypes = new[]
            {
                new RainType(0, 2.45f, 1f),
                new RainType(450, 2f, 3f),
                new RainType(900, 2f, 4f),
            };
        }

        /// <summary>
        /// Editor-only deterministic step used to verify the generated HDRP
        /// atlas and shelter gate without booting the production weather loop.
        /// </summary>
        public void SimulateForValidation(
            float rain01,
            float windMetersPerSecond,
            float exposure01,
            float deltaSeconds)
        {
            if (!float.IsFinite(rain01) ||
                !float.IsFinite(windMetersPerSecond) ||
                !float.IsFinite(exposure01) ||
                !float.IsFinite(deltaSeconds) ||
                deltaSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(deltaSeconds),
                    "Rain validation inputs must be finite and delta time must be positive.");
            }

            weatherRainTarget01 = Mathf.Clamp01(rain01);
            weatherWindMetersPerSecond = Mathf.Max(
                0f,
                windMetersPerSecond);
            roofExposure01 = Mathf.Clamp01(exposure01);
            currentRain01 = weatherRainTarget01 * roofExposure01;
            EnsureRuntimeResources();
            AdvanceRainAtlas(deltaSeconds);
        }
#endif

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureRuntimeResources();
            ResolveEnvironmentOwner();
            ResolveRoofExposure();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureRuntimeResources();
            if (Time.unscaledTime >= nextOwnerResolveTime)
            {
                nextOwnerResolveTime =
                    Time.unscaledTime + OwnerResolveIntervalSeconds;
                ResolveEnvironmentOwner();
            }

            if (Time.unscaledTime >= nextRoofResolveTime)
            {
                nextRoofResolveTime =
                    Time.unscaledTime + RoofResolveIntervalSeconds;
                ResolveRoofExposure();
            }

            float effectiveTarget =
                weatherRainTarget01 * roofExposure01;
            float transitionRate = 1f / DonorRainTransitionSeconds;
            currentRain01 = Mathf.MoveTowards(
                currentRain01,
                effectiveTarget,
                Time.unscaledDeltaTime * transitionRate);

            simulationAccumulator = Mathf.Min(
                simulationAccumulator + Time.unscaledDeltaTime,
                SimulationIntervalSeconds * 2f);
            while (simulationAccumulator >= SimulationIntervalSeconds)
            {
                simulationAccumulator -= SimulationIntervalSeconds;
                AdvanceRainAtlas(SimulationIntervalSeconds);
            }
        }

        private void OnDisable()
        {
            UnbindEnvironmentOwner();
            ClearRendererOverrides();
            ReleaseRuntimeResources();
            weatherRainTarget01 = 0f;
            weatherWindMetersPerSecond = 0f;
            currentRain01 = 0f;
            roofExposure01 = 1f;
            simulationAccumulator = 0f;
            nextRoofResolveTime = 0f;
            nextOwnerResolveTime = 0f;
            LiveWetPixelCount = 0;
            TextureUpdateCount = 0U;
        }

        private void OnDestroy()
        {
            ReleaseRuntimeResources();
        }

        private void ResolveEnvironmentOwner()
        {
            ProductionEnvironmentController candidate =
                ProductionEnvironmentController.ActiveOwner;
            if (candidate == environment)
            {
                return;
            }

            UnbindEnvironmentOwner();
            environment = candidate;
            if (environment == null)
            {
                weatherRainTarget01 = 0f;
                weatherWindMetersPerSecond = 0f;
                return;
            }

            environment.EnvironmentOutputsChanged += HandleEnvironmentOutputs;
            if (environment.CurrentOutputs.LogicalRevision != 0U)
            {
                HandleEnvironmentOutputs(environment.CurrentOutputs);
            }
        }

        private void UnbindEnvironmentOwner()
        {
            if (environment != null)
            {
                environment.EnvironmentOutputsChanged -=
                    HandleEnvironmentOutputs;
                environment = null;
            }
        }

        private void HandleEnvironmentOutputs(
            WeatherEnvironmentOutputs outputs)
        {
            weatherRainTarget01 = outputs.Weather.PrecipitationType ==
                                  WeatherPrecipitationType.None
                ? 0f
                : Mathf.Clamp01(
                    outputs.Weather.PrecipitationIntensity01);
            weatherWindMetersPerSecond = Mathf.Max(
                0f,
                outputs.Weather.WindSpeedMetersPerSecond);
        }

        private void EnsureRuntimeResources()
        {
            int resolvedSize = Mathf.Clamp(textureSize, 64, 1024);
            if (rainDetailTexture != null &&
                rainDetailTexture.width == resolvedSize)
            {
                return;
            }

            ReleaseRuntimeResources();
            // All simulation loops index by the serialized size. Keep it in
            // lockstep with the validated allocation size so malformed legacy
            // authoring cannot index past the CPU atlas buffers.
            textureSize = resolvedSize;
            int pixelCount = resolvedSize * resolvedSize;
            wetness = new byte[pixelCount];
            nextWetness = new byte[pixelCount];
            detailPixels = new Color32[pixelCount];
            rainDetailTexture = new Texture2D(
                resolvedSize,
                resolvedSize,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: true)
            {
                name = "Satsuma Rain Detail Atlas",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1,
                hideFlags = HideFlags.DontSave,
            };
            WriteDetailPixels();
            ApplyRendererOverrides();
        }

        private void ReleaseRuntimeResources()
        {
            if (rainDetailTexture != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(rainDetailTexture);
                }
                else
                {
                    DestroyImmediate(rainDetailTexture);
                }

                rainDetailTexture = null;
            }

            wetness = null;
            nextWetness = null;
            detailPixels = null;
        }

        private void ApplyRendererOverrides()
        {
            if (rainDetailTexture == null || windowRenderers == null ||
                rainOpacityByRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            int count = Mathf.Min(
                windowRenderers.Length,
                rainOpacityByRenderer.Length);
            for (int index = 0; index < count; index++)
            {
                Renderer renderer = windowRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                CaptureOriginalPropertyBlock(index, renderer);
                renderer.GetPropertyBlock(propertyBlock);
                float opacity = Mathf.Clamp01(
                    rainOpacityByRenderer[index]);
                propertyBlock.SetTexture(DetailMapId, rainDetailTexture);
                propertyBlock.SetFloat(DetailAlbedoScaleId, 0f);
                propertyBlock.SetFloat(DetailNormalScaleId, opacity);
                propertyBlock.SetFloat(
                    DetailSmoothnessScaleId,
                    opacity);
                renderer.SetPropertyBlock(propertyBlock);
                propertyBlock.Clear();
            }
        }

        private void ClearRendererOverrides()
        {
            if (windowRenderers == null)
            {
                return;
            }

            for (int index = 0; index < windowRenderers.Length; index++)
            {
                Renderer renderer = windowRenderers[index];
                if (renderer == null || index >=
                        originalPropertyBlockCaptured.Length ||
                    !originalPropertyBlockCaptured[index])
                {
                    continue;
                }

                renderer.SetPropertyBlock(originalPropertyBlocks[index]);
            }

            originalPropertyBlocks = Array.Empty<MaterialPropertyBlock>();
            originalPropertyBlockCaptured = Array.Empty<bool>();
        }

        private void CaptureOriginalPropertyBlock(
            int index,
            Renderer renderer)
        {
            if (originalPropertyBlocks.Length != windowRenderers.Length)
            {
                originalPropertyBlocks = new MaterialPropertyBlock[
                    windowRenderers.Length];
                originalPropertyBlockCaptured = new bool[
                    windowRenderers.Length];
            }

            if (originalPropertyBlockCaptured[index])
            {
                return;
            }

            var original = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(original);
            originalPropertyBlocks[index] = original;
            originalPropertyBlockCaptured[index] = true;
        }

        private void ResolveRoofExposure()
        {
            Vector3 origin = transform.TransformPoint(
                new Vector3(0f, 1.15f, 0f));
            int coveredProbeCount = 0;
            for (int probeIndex = 0;
                 probeIndex < roofProbeOffsets.Length;
                 probeIndex++)
            {
                Vector3 probeOrigin = origin + transform.TransformVector(
                    roofProbeOffsets[probeIndex]);
                int hitCount = Physics.RaycastNonAlloc(
                    probeOrigin,
                    Vector3.up,
                    roofHitBuffer,
                    RoofProbeDistanceMeters,
                    roofLayers,
                    QueryTriggerInteraction.Ignore);
                bool covered = false;
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    Collider collider = roofHitBuffer[hitIndex].collider;
                    if (collider == null ||
                        collider.transform.IsChildOf(transform))
                    {
                        continue;
                    }

                    covered = true;
                    break;
                }

                if (covered)
                {
                    coveredProbeCount++;
                }
            }

            roofExposure01 = 1f -
                coveredProbeCount / (float)roofProbeOffsets.Length;
        }

        private void AdvanceRainAtlas(float deltaSeconds)
        {
            if (wetness == null || nextWetness == null ||
                detailPixels == null)
            {
                return;
            }

            EvaluateRainType(
                currentRain01,
                out float dropsPerFrame,
                out float dryingSpeed,
                out float dropSizePixels);
            FlowAndDry(
                deltaSeconds,
                dryingSpeed,
                ResolveHorizontalFlowPixels(deltaSeconds));

            int dropCount = Mathf.RoundToInt(
                dropsPerFrame * deltaSeconds * 60f);
            int radius = Mathf.Max(1, Mathf.RoundToInt(
                dropSizePixels * 0.5f));
            for (int index = 0; index < dropCount; index++)
            {
                StampDrop(
                    NextRandom(textureSize),
                    NextRandom(textureSize),
                    radius);
            }

            WriteDetailPixels();
            ApplyRendererOverrides();
        }

        private void EvaluateRainType(
            float rain01,
            out float dropsPerFrame,
            out float dryingSpeed,
            out float dropSizePixels)
        {
            if (rainTypes == null || rainTypes.Length < 2)
            {
                dropsPerFrame = 0f;
                dryingSpeed = 0f;
                dropSizePixels = 1f;
                return;
            }

            int last = rainTypes.Length - 1;
            float clamped = Mathf.Clamp01(rain01);
            int floorIndex = Mathf.FloorToInt(
                last * Mathf.Clamp01(clamped - 0.001f));
            floorIndex = Mathf.Clamp(floorIndex, 0, last - 1);
            float blend = Mathf.Repeat(clamped * last, 1f);
            RainType floor = rainTypes[floorIndex];
            RainType ceiling = rainTypes[floorIndex + 1];
            dropsPerFrame = Mathf.Lerp(
                floor.DropsPerFrameAt60Fps,
                ceiling.DropsPerFrameAt60Fps,
                blend);
            dryingSpeed = Mathf.Lerp(
                floor.DryingSpeed,
                ceiling.DryingSpeed,
                blend);
            dropSizePixels = Mathf.Lerp(
                floor.DropSizePixels,
                ceiling.DropSizePixels,
                blend);
        }

        private void FlowAndDry(
            float deltaSeconds,
            float dryingSpeed,
            int horizontalFlowPixels)
        {
            Array.Clear(nextWetness, 0, nextWetness.Length);
            int decay = Mathf.Max(1, Mathf.RoundToInt(
                dryingSpeed * deltaSeconds * 12f));
            int downwardFlowPixels = Mathf.Clamp(
                Mathf.RoundToInt(
                    gravityMultiplier * deltaSeconds * 0.3f),
                1,
                4);
            for (int y = 0; y < textureSize; y++)
            {
                int regionMinimumY = ResolveRegionMinimumY(y);
                for (int x = 0; x < textureSize; x++)
                {
                    int sourceIndex = y * textureSize + x;
                    int value = wetness[sourceIndex] - decay;
                    if (value <= 0)
                    {
                        continue;
                    }

                    byte retained = (byte)value;
                    if (retained > nextWetness[sourceIndex])
                    {
                        nextWetness[sourceIndex] = retained;
                    }

                    int destinationY = Mathf.Max(
                        regionMinimumY,
                        y - downwardFlowPixels);
                    int destinationX = PositiveModulo(
                        x + horizontalFlowPixels,
                        textureSize);
                    int destinationIndex =
                        destinationY * textureSize + destinationX;
                    byte flowed = (byte)Mathf.Max(0, value - 3);
                    if (flowed > nextWetness[destinationIndex])
                    {
                        nextWetness[destinationIndex] = flowed;
                    }
                }
            }

            (wetness, nextWetness) = (nextWetness, wetness);
        }

        private int ResolveRegionMinimumY(int y)
        {
            int frontEnd = Mathf.RoundToInt(textureSize * 0.4f);
            int sideEnd = Mathf.RoundToInt(textureSize * 0.7f);
            if (y < frontEnd)
            {
                return 0;
            }

            return y < sideEnd ? frontEnd : sideEnd;
        }

        private int ResolveHorizontalFlowPixels(float deltaSeconds)
        {
            float localVelocity = vehicleBody != null
                ? transform.InverseTransformDirection(
                    vehicleBody.linearVelocity).x
                : 0f;
            float flow = localVelocity * DonorVehicleSpeedMultiplier +
                         weatherWindMetersPerSecond * 0.15f;
            return Mathf.Clamp(
                Mathf.RoundToInt(flow * deltaSeconds * 0.1f),
                -4,
                4);
        }

        private void StampDrop(int centerX, int centerY, int radius)
        {
            int radiusSquared = radius * radius;
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                int y = Mathf.Clamp(
                    centerY + offsetY,
                    0,
                    textureSize - 1);
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    if (offsetX * offsetX + offsetY * offsetY >
                        radiusSquared)
                    {
                        continue;
                    }

                    int x = PositiveModulo(centerX + offsetX, textureSize);
                    wetness[y * textureSize + x] = 255;
                }
            }
        }

        private void WriteDetailPixels()
        {
            int wetPixelCount = 0;
            for (int y = 0; y < textureSize; y++)
            {
                int downY = Mathf.Max(0, y - 1);
                int upY = Mathf.Min(textureSize - 1, y + 1);
                for (int x = 0; x < textureSize; x++)
                {
                    int leftX = PositiveModulo(x - 1, textureSize);
                    int rightX = PositiveModulo(x + 1, textureSize);
                    int index = y * textureSize + x;
                    byte center = wetness[index];
                    if (center != 0)
                    {
                        wetPixelCount++;
                    }

                    int gradientX =
                        wetness[y * textureSize + rightX] -
                        wetness[y * textureSize + leftX];
                    int gradientY =
                        wetness[upY * textureSize + x] -
                        wetness[downY * textureSize + x];
                    byte normalX = (byte)Mathf.Clamp(
                        128 + gradientX / 4,
                        0,
                        255);
                    byte normalY = (byte)Mathf.Clamp(
                        128 + gradientY / 4,
                        0,
                        255);
                    byte smoothness = (byte)Mathf.Clamp(
                        128 + center / 2,
                        0,
                        255);
                    detailPixels[index] = new Color32(
                        128,
                        normalY,
                        smoothness,
                        normalX);
                }
            }

            rainDetailTexture.SetPixels32(detailPixels);
            rainDetailTexture.Apply(
                updateMipmaps: false,
                makeNoLongerReadable: false);
            LiveWetPixelCount = wetPixelCount;
            TextureUpdateCount++;
        }

        private int NextRandom(int exclusiveMaximum)
        {
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            return (int)(randomState % (uint)exclusiveMaximum);
        }

        private static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }
    }
}
