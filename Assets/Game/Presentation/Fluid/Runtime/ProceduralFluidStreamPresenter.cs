using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Presentation.Fluid
{
    public enum FluidStreamProfile
    {
        Urine = 0,
        Faucet = 1,
        Shower = 2,
    }

    /// <summary>
    /// Lightweight world-space particle stream for first-person and fixture
    /// liquids. Gameplay state remains external; this component only renders
    /// emission, gravity, collision and short impact splashes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralFluidStreamPresenter : MonoBehaviour
    {
        private const int MaximumCollisionEvents = 32;

        private readonly List<ParticleCollisionEvent> collisionEvents =
            new List<ParticleCollisionEvent>(MaximumCollisionEvents);

        private ParticleSystem stream;
        private ParticleSystem splash;
        private Material fluidMaterial;
        private Texture2D fluidTexture;
        private float baseEmissionRate;
        private float currentIntensity = 1f;
        private bool configured;
        private bool flowing;

        public event Action<GameObject> Impacted;

        public bool IsFlowing => flowing;

        public void Configure(
            FluidStreamProfile profile,
            Vector3 localPosition,
            Vector3 localDirection,
            LayerMask collisionMask)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "Fluid stream presenter is already configured.");
            }

            if (!IsFinite(localPosition) ||
                !IsFinite(localDirection) ||
                localDirection.sqrMagnitude < 0.001f)
            {
                throw new ArgumentException(
                    "Fluid stream transform is invalid.");
            }

            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.LookRotation(
                localDirection.normalized,
                Mathf.Abs(Vector3.Dot(
                    localDirection.normalized,
                    Vector3.up)) > 0.98f
                        ? Vector3.forward
                        : Vector3.up);

            Color start;
            Color middle;
            Color end;
            float lifetime;
            float speed;
            float minimumSize;
            float maximumSize;
            float coneAngle;
            float coneRadius;
            float noiseStrength;
            float rendererVelocityScale;
            float rendererLengthScale;
            int maximumParticles;
            switch (profile)
            {
                case FluidStreamProfile.Shower:
                    // A dense fan of short translucent droplets. Long stretched
                    // cards read as black needles at grazing angles in HDRP.
                    start = new Color(0.9f, 0.97f, 1f, 0.46f);
                    middle = new Color(0.78f, 0.92f, 1f, 0.32f);
                    end = new Color(0.72f, 0.88f, 1f, 0f);
                    lifetime = 0.72f;
                    speed = 4.65f;
                    minimumSize = 0.006f;
                    maximumSize = 0.011f;
                    coneAngle = 16f;
                    coneRadius = 0.038f;
                    noiseStrength = 0.055f;
                    rendererVelocityScale = 0.018f;
                    rendererLengthScale = 0.32f;
                    baseEmissionRate = 760f;
                    maximumParticles = 720;
                    break;

                case FluidStreamProfile.Faucet:
                    start = new Color(0.88f, 0.96f, 1f, 0.6f);
                    middle = new Color(0.76f, 0.9f, 1f, 0.45f);
                    end = new Color(0.68f, 0.84f, 1f, 0f);
                    lifetime = 1.05f;
                    speed = 3.55f;
                    minimumSize = 0.009f;
                    maximumSize = 0.014f;
                    coneAngle = 0.8f;
                    coneRadius = 0.005f;
                    noiseStrength = 0.008f;
                    rendererVelocityScale = 0.014f;
                    rendererLengthScale = 0.48f;
                    baseEmissionRate = 420f;
                    maximumParticles = 480;
                    break;

                default:
                    // Based on the locked donor particle evidence: 0.015 m,
                    // two-second, gravity-driven yellow stream.
                    start = new Color32(235, 213, 105, 190);
                    middle = new Color32(238, 225, 168, 145);
                    end = new Color32(231, 222, 175, 0);
                    lifetime = 1.7f;
                    speed = 4.6f;
                    minimumSize = 0.011f;
                    maximumSize = 0.015f;
                    coneAngle = 0.45f;
                    coneRadius = 0.003f;
                    noiseStrength = 0.004f;
                    rendererVelocityScale = 0.014f;
                    rendererLengthScale = 0.44f;
                    baseEmissionRate = 380f;
                    maximumParticles = 520;
                    break;
            }

            fluidTexture = BuildFluidTexture(
                $"MSC {profile} Fluid Shape");
            fluidMaterial = BuildFluidMaterial(
                $"MSC {profile} Fluid",
                start,
                fluidTexture);
            stream = BuildStream(
                start,
                middle,
                end,
                lifetime,
                speed,
                minimumSize,
                maximumSize,
                coneAngle,
                coneRadius,
                noiseStrength,
                rendererVelocityScale,
                rendererLengthScale,
                maximumParticles,
                collisionMask);
            splash = BuildSplash(start, middle, end);
            configured = true;
            SetFlowing(false, clearExisting: true);
        }

        public void SetFlowing(
            bool value,
            bool clearExisting = false)
        {
            if (!configured)
            {
                return;
            }

            flowing = value;
            ParticleSystem.EmissionModule emission = stream.emission;
            emission.enabled = value;
            emission.rateOverTime =
                baseEmissionRate * currentIntensity;
            if (value)
            {
                if (!stream.isPlaying)
                {
                    stream.Play(withChildren: false);
                }
            }
            else
            {
                stream.Stop(
                    withChildren: false,
                    clearExisting
                        ? ParticleSystemStopBehavior.StopEmittingAndClear
                        : ParticleSystemStopBehavior.StopEmitting);
            }

            if (clearExisting)
            {
                splash.Clear(withChildren: false);
            }
        }

        public void SetIntensity(float normalized)
        {
            currentIntensity = Mathf.Clamp(normalized, 0.08f, 1.5f);
            if (stream == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = stream.emission;
            emission.rateOverTime =
                baseEmissionRate * currentIntensity;
        }

        private ParticleSystem BuildStream(
            Color start,
            Color middle,
            Color end,
            float lifetime,
            float speed,
            float minimumSize,
            float maximumSize,
            float coneAngle,
            float coneRadius,
            float noiseStrength,
            float rendererVelocityScale,
            float rendererLengthScale,
            int maximumParticles,
            LayerMask collisionMask)
        {
            ParticleSystem result =
                gameObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = result.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = new ParticleSystem.MinMaxCurve(
                minimumSize,
                maximumSize);
            main.maxParticles = maximumParticles;
            main.gravityModifier = 1f;
            main.startColor = start;

            ParticleSystem.EmissionModule emission = result.emission;
            emission.enabled = false;
            emission.rateOverTime = baseEmissionRate;

            ParticleSystem.ShapeModule shape = result.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = coneRadius;
            shape.radiusThickness = 1f;

            ParticleSystem.ColorOverLifetimeModule color =
                result.colorOverLifetime;
            color.enabled = true;
            color.color = BuildGradient(start, middle, end);

            ParticleSystem.NoiseModule noise = result.noise;
            noise.enabled = true;
            noise.strength = noiseStrength;
            noise.frequency = 0.55f;
            noise.scrollSpeed = 0.18f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            ParticleSystem.SizeOverLifetimeModule size = result.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.72f),
                    new Keyframe(0.08f, 1f),
                    new Keyframe(0.82f, 0.92f),
                    new Keyframe(1f, 0.58f)));

            ParticleSystem.CollisionModule collision = result.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.collidesWith = collisionMask;
            collision.dampen = 0.25f;
            collision.bounce = 0.03f;
            collision.lifetimeLoss = 0.85f;
            collision.sendCollisionMessages = true;

            ParticleSystemRenderer renderer =
                result.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode =
                ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = rendererVelocityScale;
            renderer.lengthScale = rendererLengthScale;
            renderer.cameraVelocityScale = 0f;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = fluidMaterial;
            return result;
        }

        private ParticleSystem BuildSplash(
            Color start,
            Color middle,
            Color end)
        {
            var splashObject = new GameObject("Impact Splash")
            {
                hideFlags = HideFlags.DontSave,
            };
            splashObject.transform.SetParent(transform, false);
            ParticleSystem result =
                splashObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = result.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.02f);
            main.maxParticles = 96;
            main.gravityModifier = 0.55f;
            main.startColor = start;

            ParticleSystem.EmissionModule emission = result.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = result.shape;
            shape.enabled = false;
            ParticleSystem.ColorOverLifetimeModule color =
                result.colorOverLifetime;
            color.enabled = true;
            color.color = BuildGradient(start, middle, end);

            ParticleSystemRenderer renderer =
                result.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = fluidMaterial;
            return result;
        }

        private void OnParticleCollision(GameObject other)
        {
            if (stream == null || splash == null || other == null)
            {
                return;
            }

            int count = stream.GetCollisionEvents(other, collisionEvents);
            if (count > 0)
            {
                Impacted?.Invoke(other);
            }

            int emitCount = Mathf.Min(count, 3);
            for (int i = 0; i < emitCount; i++)
            {
                ParticleCollisionEvent collision = collisionEvents[i];
                var emit = new ParticleSystem.EmitParams
                {
                    position = collision.intersection +
                               collision.normal * 0.003f,
                    velocity = collision.normal * 0.24f +
                               Vector3.ProjectOnPlane(
                                   collision.velocity,
                                   collision.normal) * 0.05f,
                    startLifetime = 0.2f,
                    startSize = 0.012f,
                };
                splash.Emit(emit, 2);
            }
        }

        private static ParticleSystem.MinMaxGradient BuildGradient(
            Color start,
            Color middle,
            Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(middle, 0.48f),
                    new GradientColorKey(end, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(start.a, 0f),
                    new GradientAlphaKey(middle.a, 0.58f),
                    new GradientAlphaKey(0f, 1f),
                });
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        private static Material BuildFluidMaterial(
            string materialName,
            Color color,
            Texture texture)
        {
            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "The HDRP/Unlit shader required by fluid presentation " +
                    "is unavailable.");
            }

            var material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent,
            };
            material.SetOverrideTag("RenderType", "Transparent");
            SetColorIfPresent(material, "_BaseColor", color);
            SetColorIfPresent(material, "_UnlitColor", color);
            SetColorIfPresent(material, "_Color", color);
            SetTextureIfPresent(material, "_BaseColorMap", texture);
            SetTextureIfPresent(material, "_UnlitColorMap", texture);
            SetTextureIfPresent(material, "_MainTex", texture);
            SetFloatIfPresent(material, "_SurfaceType", 1f);
            SetFloatIfPresent(material, "_BlendMode", 0f);
            SetFloatIfPresent(material, "_TransparentZWrite", 0f);
            SetFloatIfPresent(material, "_ZWrite", 0f);
            SetFloatIfPresent(material, "_EnableFogOnTransparent", 1f);
            SetFloatIfPresent(material, "_DoubleSidedEnable", 1f);

            // HDRP derives blend factors, culling, render tags, passes and
            // keywords from the authored properties. Merely setting the
            // properties leaves a runtime material at opaque One/Zero
            // blending, which turns transparent particle cards into black
            // rectangles.
            HDMaterial.SetSurfaceType(material, transparent: true);
            HDMaterial.SetRenderingPass(
                material,
                HDMaterial.RenderingPass.Default);
            if (!HDMaterial.ValidateMaterial(material))
            {
                throw new InvalidOperationException(
                    "Fluid presentation material failed HDRP validation.");
            }

            return material;
        }

        private static Texture2D BuildFluidTexture(string textureName)
        {
            const int Size = 32;
            var texture = new Texture2D(
                Size,
                Size,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: true)
            {
                name = textureName,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                float normalizedY =
                    (y + 0.5f) / Size * 2f - 1f;
                for (int x = 0; x < Size; x++)
                {
                    float normalizedX =
                        (x + 0.5f) / Size * 2f - 1f;
                    float radius = Mathf.Sqrt(
                        normalizedX * normalizedX +
                        normalizedY * normalizedY);
                    float alpha = 1f -
                        Mathf.SmoothStep(0.28f, 1f, radius);
                    pixels[y * Size + x] =
                        new Color32(
                            255,
                            255,
                            255,
                            (byte)Mathf.RoundToInt(
                                Mathf.Clamp01(alpha) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return texture;
        }

        private static void SetColorIfPresent(
            Material material,
            string property,
            Color value)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, value);
            }
        }

        private static void SetTextureIfPresent(
            Material material,
            string property,
            Texture value)
        {
            if (value != null && material.HasProperty(property))
            {
                material.SetTexture(property, value);
            }
        }

        private static void SetFloatIfPresent(
            Material material,
            string property,
            float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private void OnDestroy()
        {
            if (fluidMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(fluidMaterial);
                }
                else
                {
                    DestroyImmediate(fluidMaterial);
                }
            }

            if (fluidTexture != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(fluidTexture);
                }
                else
                {
                    DestroyImmediate(fluidTexture);
                }
            }
        }
    }
}
