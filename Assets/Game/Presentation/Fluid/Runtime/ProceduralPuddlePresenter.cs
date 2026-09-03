using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Presentation.Fluid
{
    /// <summary>
    /// Small pooled HDRP puddle surface. Global rain accumulation controls a
    /// deterministic ring around the player; gameplay spills add short-lived
    /// local surfaces at their actual world position.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralPuddlePresenter : MonoBehaviour
    {
        private const int RainPuddleCount = 14;
        private const int LocalPuddleCount = 12;
        private const float RainRefreshSeconds = 2f;
        private const float RainRelocationDistance = 85f;
        private const float RainMinimumSpawnDistance = 30f;
        private const float RainMaximumSpawnDistance = 55f;
        private const float RainFadeInSeconds = 4f;

        private static readonly Vector2[] RainOffsets =
        {
            new Vector2(-7.1f, 4.2f), new Vector2(5.8f, 6.4f),
            new Vector2(9.5f, -2.8f), new Vector2(-4.6f, -8.7f),
            new Vector2(2.4f, -5.1f), new Vector2(-10.3f, -1.7f),
            new Vector2(12.1f, 7.5f), new Vector2(-13.4f, 8.1f),
            new Vector2(6.9f, -12.2f), new Vector2(-7.8f, 13.5f),
            new Vector2(15.2f, -6.4f), new Vector2(-15.8f, -7.7f),
            new Vector2(1.8f, 14.4f), new Vector2(11.7f, 12.8f),
        };

        private sealed class PuddleSlot
        {
            public GameObject Object;
            public MeshRenderer Renderer;
            public MaterialPropertyBlock Properties;
            public float ExpiresAt;
            public float Radius;
            public float TargetAlpha;
            public float FadeStartedAt;
            public bool HasWorldAnchor;
        }

        private Transform followTarget;
        private LayerMask groundMask;
        private Mesh puddleMesh;
        private Material puddleMaterial;
        private Texture2D puddleTexture;
        private PuddleSlot[] rainPuddles;
        private PuddleSlot[] localPuddles;
        private float rainAmount01;
        private float nextRainRefreshAt;
        private int nextLocalSlot;
        private bool configured;

        public float RainAmount01 => rainAmount01;

        public void Configure(Transform configuredFollowTarget, LayerMask mask)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "Puddle presenter is already configured.");
            }

            followTarget = configuredFollowTarget ??
                throw new ArgumentNullException(nameof(configuredFollowTarget));
            groundMask = mask;
            puddleMesh = BuildPuddleMesh();
            puddleTexture = BuildPuddleTexture();
            puddleMaterial = BuildPuddleMaterial(puddleTexture);
            rainPuddles = BuildPool("Rain puddle", RainPuddleCount);
            localPuddles = BuildPool("Local spill puddle", LocalPuddleCount);
            configured = true;
            nextRainRefreshAt = Time.unscaledTime;
        }

        public void SetRainAmount(float amount01)
        {
            rainAmount01 = Mathf.Clamp01(amount01);
            nextRainRefreshAt = Time.unscaledTime;
        }

        public bool AddLocalPuddle(
            Vector3 spillPosition,
            float waterLitres,
            Transform ignoredRoot = null)
        {
            if (!configured || !IsFinite(spillPosition) ||
                !float.IsFinite(waterLitres) || waterLitres <= 0f ||
                !TryFindGround(
                    spillPosition,
                    5f,
                    ignoredRoot,
                    out RaycastHit hit))
            {
                return false;
            }

            PuddleSlot slot = localPuddles[nextLocalSlot];
            nextLocalSlot = (nextLocalSlot + 1) % localPuddles.Length;
            slot.Radius = Mathf.Clamp(
                0.18f + Mathf.Sqrt(waterLitres) * 0.5f,
                0.22f,
                1.2f);
            slot.ExpiresAt = Time.unscaledTime +
                Mathf.Lerp(150f, 360f, Mathf.Clamp01(waterLitres / 2f));
            Place(slot, hit, slot.Radius, 0.2f, 0f);
            return true;
        }

        private void Update()
        {
            if (!configured || followTarget == null ||
                rainPuddles == null || localPuddles == null)
            {
                return;
            }

            UpdateLocalPuddles();
            if (Time.unscaledTime >= nextRainRefreshAt)
            {
                nextRainRefreshAt = Time.unscaledTime + RainRefreshSeconds;
                RefreshRainPuddles();
            }

            UpdateRainPuddleAppearance();
        }

        private void UpdateLocalPuddles()
        {
            float now = Time.unscaledTime;
            for (int index = 0; index < localPuddles.Length; index++)
            {
                PuddleSlot slot = localPuddles[index];
                if (!slot.Object.activeSelf)
                {
                    continue;
                }

                float remaining = slot.ExpiresAt - now;
                if (remaining <= 0f)
                {
                    slot.Object.SetActive(false);
                    continue;
                }

                if (remaining < 30f)
                {
                    SetAppearance(
                        slot,
                        Mathf.Lerp(0f, 0.2f, remaining / 30f));
                }
            }
        }

        private void RefreshRainPuddles()
        {
            int visibleCount = rainAmount01 < 0.035f
                ? 0
                : Mathf.Clamp(
                    Mathf.CeilToInt(rainAmount01 * RainPuddleCount),
                    1,
                    RainPuddleCount);
            float radius = Mathf.Lerp(0.24f, 0.95f, rainAmount01);
            float alpha = Mathf.Lerp(0.075f, 0.19f, rainAmount01);

            for (int index = 0; index < rainPuddles.Length; index++)
            {
                PuddleSlot slot = rainPuddles[index];
                if (index >= visibleCount)
                {
                    slot.Object.SetActive(false);
                    slot.HasWorldAnchor = false;
                    slot.TargetAlpha = 0f;
                    continue;
                }

                Vector3 horizontalDelta = slot.Object.transform.position -
                    followTarget.position;
                horizontalDelta.y = 0f;
                bool mustRelocate = !slot.HasWorldAnchor ||
                    horizontalDelta.sqrMagnitude >
                    RainRelocationDistance * RainRelocationDistance;
                float sizeVariation = 0.82f +
                    ((index * 37) % 29) / 100f;
                if (!mustRelocate)
                {
                    slot.Object.SetActive(true);
                    slot.Object.transform.localScale = new Vector3(
                        radius * sizeVariation,
                        1f,
                        radius * sizeVariation * 0.74f);
                    slot.TargetAlpha = alpha;
                    continue;
                }

                Vector2 direction = RainOffsets[index].normalized;
                float spawnDistance = Mathf.Lerp(
                    RainMinimumSpawnDistance,
                    RainMaximumSpawnDistance,
                    ((index * 37) % 31) / 30f);
                Vector2 offset = direction * spawnDistance;
                Vector3 candidate = followTarget.position +
                    new Vector3(offset.x, 2.5f, offset.y);
                if (!TryFindGround(candidate, 12f, out RaycastHit hit))
                {
                    slot.Object.SetActive(false);
                    slot.HasWorldAnchor = false;
                    continue;
                }

                Place(
                    slot,
                    hit,
                    radius * sizeVariation,
                    0f,
                    (index * 47) % 360);
                slot.HasWorldAnchor = true;
                slot.TargetAlpha = alpha;
                slot.FadeStartedAt = Time.unscaledTime;
            }
        }

        private void UpdateRainPuddleAppearance()
        {
            float now = Time.unscaledTime;
            for (int index = 0; index < rainPuddles.Length; index++)
            {
                PuddleSlot slot = rainPuddles[index];
                if (!slot.Object.activeSelf)
                {
                    continue;
                }

                float fade01 = Mathf.Clamp01(
                    (now - slot.FadeStartedAt) / RainFadeInSeconds);
                SetAppearance(
                    slot,
                    slot.TargetAlpha * Mathf.SmoothStep(0f, 1f, fade01));
            }
        }

        private bool TryFindGround(
            Vector3 origin,
            float distance,
            out RaycastHit hit)
        {
            return TryFindGround(
                origin,
                distance,
                ignoredRoot: null,
                out hit);
        }

        private bool TryFindGround(
            Vector3 origin,
            float distance,
            Transform ignoredRoot,
            out RaycastHit hit)
        {
            Vector3 rayOrigin = origin + Vector3.up * 1.5f;
            if (ignoredRoot == null && Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out hit,
                    distance,
                    groundMask,
                    QueryTriggerInteraction.Ignore) &&
                hit.normal.y >= 0.65f)
            {
                return true;
            }

            if (ignoredRoot != null)
            {
                RaycastHit[] candidates = Physics.RaycastAll(
                    rayOrigin,
                    Vector3.down,
                    distance,
                    groundMask,
                    QueryTriggerInteraction.Ignore);
                float nearestDistance = float.PositiveInfinity;
                bool found = false;
                hit = default;
                for (int index = 0; index < candidates.Length; index++)
                {
                    RaycastHit candidate = candidates[index];
                    if (candidate.collider == null ||
                        candidate.normal.y < 0.65f ||
                        candidate.collider.transform.IsChildOf(ignoredRoot) ||
                        candidate.distance >= nearestDistance)
                    {
                        continue;
                    }

                    hit = candidate;
                    nearestDistance = candidate.distance;
                    found = true;
                }

                return found;
            }

            hit = default;
            return false;
        }

        private void Place(
            PuddleSlot slot,
            in RaycastHit hit,
            float radius,
            float alpha,
            float rotationDegrees)
        {
            slot.Object.SetActive(true);
            slot.Object.transform.SetPositionAndRotation(
                hit.point + hit.normal * 0.012f,
                Quaternion.FromToRotation(Vector3.up, hit.normal) *
                Quaternion.AngleAxis(rotationDegrees, Vector3.up));
            slot.Object.transform.localScale =
                new Vector3(radius, 1f, radius * 0.74f);
            SetAppearance(slot, alpha);
        }

        private static void SetAppearance(PuddleSlot slot, float alpha)
        {
            MaterialPropertyBlock properties = slot.Properties;
            Color color = new Color(0.018f, 0.018f, 0.018f, alpha);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            slot.Renderer.SetPropertyBlock(properties);
        }

        private PuddleSlot[] BuildPool(string name, int count)
        {
            var result = new PuddleSlot[count];
            for (int index = 0; index < count; index++)
            {
                var puddleObject = new GameObject($"{name} {index + 1}");
                puddleObject.transform.SetParent(transform, false);
                MeshFilter filter = puddleObject.AddComponent<MeshFilter>();
                filter.sharedMesh = puddleMesh;
                MeshRenderer renderer =
                    puddleObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = puddleMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                puddleObject.SetActive(false);
                result[index] = new PuddleSlot
                {
                    Object = puddleObject,
                    Renderer = renderer,
                    Properties = new MaterialPropertyBlock(),
                };
            }

            return result;
        }

        private static Mesh BuildPuddleMesh()
        {
            const int Segments = 20;
            var vertices = new Vector3[Segments + 1];
            var triangles = new int[Segments * 3];
            var uv = new Vector2[Segments + 1];
            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (int index = 0; index < Segments; index++)
            {
                float angle = index * Mathf.PI * 2f / Segments;
                float variation = 0.88f +
                    ((index * 17) % 9) / 50f;
                vertices[index + 1] = new Vector3(
                    Mathf.Cos(angle) * variation,
                    0f,
                    Mathf.Sin(angle) * variation);
                uv[index + 1] = new Vector2(
                    vertices[index + 1].x * 0.5f + 0.5f,
                    vertices[index + 1].z * 0.5f + 0.5f);
                int triangle = index * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = index + 1;
                triangles[triangle + 2] = (index + 1) % Segments + 1;
            }

            var mesh = new Mesh
            {
                name = "MSC Procedural Puddle",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                triangles = triangles,
                uv = uv,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material BuildPuddleMaterial(Texture texture)
        {
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "HDRP/Lit is required for puddle presentation.");
            }

            var material = new Material(shader)
            {
                name = "MSC Procedural Puddle",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent + 8,
            };
            material.SetColor(
                "_BaseColor",
                new Color(0.018f, 0.018f, 0.018f, 0.16f));
            if (material.HasProperty("_BaseColorMap"))
            {
                material.SetTexture("_BaseColorMap", texture);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.96f);
            material.SetFloat("_SurfaceType", 1f);
            material.SetFloat("_BlendMode", 0f);
            material.SetFloat("_TransparentZWrite", 0f);
            material.SetFloat("_DoubleSidedEnable", 1f);
            HDMaterial.SetSurfaceType(material, transparent: true);
            HDMaterial.SetRenderingPass(
                material,
                HDMaterial.RenderingPass.Default);
            if (!HDMaterial.ValidateMaterial(material))
            {
                throw new InvalidOperationException(
                    "Puddle material failed HDRP validation.");
            }

            return material;
        }

        private static Texture2D BuildPuddleTexture()
        {
            const int Size = 64;
            var texture = new Texture2D(
                Size,
                Size,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: true)
            {
                name = "MSC Soft Irregular Puddle Mask",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                float py = (y + 0.5f) / Size * 2f - 1f;
                for (int x = 0; x < Size; x++)
                {
                    float px = (x + 0.5f) / Size * 2f - 1f;
                    float radius = Mathf.Sqrt(px * px + py * py);
                    float angle = Mathf.Atan2(py, px);
                    float edge = 0.9f +
                        Mathf.Sin(angle * 3f + 0.7f) * 0.045f +
                        Mathf.Sin(angle * 7f - 0.4f) * 0.025f;
                    float alpha = 1f - Mathf.SmoothStep(
                        edge - 0.2f,
                        edge,
                        radius);
                    pixels[y * Size + x] = new Color32(
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

        private void OnDestroy()
        {
            DestroyRuntimeObject(puddleMaterial);
            DestroyRuntimeObject(puddleTexture);
            DestroyRuntimeObject(puddleMesh);
        }

        private static void DestroyRuntimeObject(UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }
}
