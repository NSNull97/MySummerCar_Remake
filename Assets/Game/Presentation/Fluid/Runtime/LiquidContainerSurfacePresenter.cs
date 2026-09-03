using System;
using MSC.Interaction.Capabilities;
using MSC.Items;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Presentation.Fluid
{
    /// <summary>
    /// Replaceable project-owned water surface driven by authoritative item
    /// litres. It also disables the static donor Water insert that previously
    /// required a donor FSM to position/toggle it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LiquidContainerSurfacePresenter : MonoBehaviour
    {
        public const string SaunaBucketDefinitionId =
            LiquidContainerTiltSpiller.SaunaBucketDefinitionId;
        public const string SaunaDipperDefinitionId =
            LiquidContainerTiltSpiller.SaunaDipperDefinitionId;

        private WorldItemInstance item;
        private GameObject surfaceObject;
        private Mesh surfaceMesh;
        private Material surfaceMaterial;
        private LiquidContainerTiltSpiller tiltSpiller;
        private GameObject pourObject;
        private ProceduralFluidStreamPresenter pourStream;
        private Vector3 surfaceCenter;
        private Vector3 openAxis;
        private float emptyAxisOffset;
        private float fullAxisOffset;
        private float surfaceRadius;
        private bool configured;
        private bool hasGeometry;

        public bool IsConfigured => configured;
        public bool IsSurfaceVisible =>
            surfaceObject != null && surfaceObject.activeSelf;
        public Material SurfaceMaterial => surfaceMaterial;
        public Transform SurfaceTransform => surfaceObject != null
            ? surfaceObject.transform
            : null;
        public Vector3 LocalOpenAxis => openAxis;

        public static bool SupportsDefinition(string definitionId) =>
            string.Equals(
                definitionId,
                SaunaBucketDefinitionId,
                StringComparison.Ordinal) ||
            string.Equals(
                definitionId,
                SaunaDipperDefinitionId,
                StringComparison.Ordinal);

        public void Configure(
            WorldItemInstance configuredItem,
            GameObject presentationRoot)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "Liquid container surface is already configured.");
            }

            item = configuredItem ??
                throw new ArgumentNullException(nameof(configuredItem));
            if (!SupportsDefinition(item.DefinitionId))
            {
                throw new ArgumentException(
                    $"Item '{item.DefinitionId}' has no liquid surface profile.",
                    nameof(configuredItem));
            }

            surfaceMesh = BuildSurfaceMesh();
            surfaceMaterial = BuildWaterMaterial();
            surfaceObject = new GameObject("Dynamic container water surface");
            surfaceObject.transform.SetParent(transform, false);
            surfaceObject.AddComponent<MeshFilter>().sharedMesh = surfaceMesh;
            MeshRenderer renderer = surfaceObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = surfaceMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            tiltSpiller = item.GetComponent<LiquidContainerTiltSpiller>();
            pourObject = new GameObject("Dynamic container pour stream")
            {
                hideFlags = HideFlags.DontSave,
            };
            pourObject.transform.SetParent(transform, false);
            pourStream =
                pourObject.AddComponent<ProceduralFluidStreamPresenter>();
            pourStream.Configure(
                FluidStreamProfile.Faucet,
                Vector3.zero,
                Vector3.down,
                ~0);
            item.StatusChanged += HandleItemStatusChanged;
            configured = true;
            BindPresentation(presentationRoot);
            RefreshPresentation();
        }

        public bool BindPresentation(GameObject presentationRoot)
        {
            if (!configured || presentationRoot == null)
            {
                return false;
            }

            MeshFilter[] filters =
                presentationRoot.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0)
            {
                hasGeometry = false;
                RefreshPresentation();
                return false;
            }

            bool bucket = string.Equals(
                item.DefinitionId,
                SaunaBucketDefinitionId,
                StringComparison.Ordinal);
            bool hasBounds = false;
            Bounds localBounds = default;
            bool hasInsertCalibration = false;
            Vector3 insertCenter = Vector3.zero;
            Vector3 insertNormal = Vector3.back;
            float insertRadius = 0f;
            for (int index = 0; index < filters.Length; index++)
            {
                MeshFilter filter = filters[index];
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                {
                    continue;
                }

                bool flatInsert = bucket && IsFlatInsert(mesh.bounds.size);
                if (flatInsert)
                {
                    if (TryReadFlatInsertCalibration(
                            filter,
                            out Vector3 candidateCenter,
                            out Vector3 candidateNormal,
                            out float candidateRadius) &&
                        candidateRadius > insertRadius)
                    {
                        hasInsertCalibration = true;
                        insertCenter = candidateCenter;
                        insertNormal = candidateNormal;
                        insertRadius = candidateRadius;
                    }

                    MeshRenderer flatRenderer =
                        filter.GetComponent<MeshRenderer>();
                    if (flatRenderer != null)
                    {
                        flatRenderer.enabled = false;
                    }

                    continue;
                }

                EncapsulateMeshBounds(
                    transform,
                    filter.transform,
                    mesh.bounds,
                    ref localBounds,
                    ref hasBounds);
            }

            if (!hasBounds)
            {
                hasGeometry = false;
                RefreshPresentation();
                return false;
            }

            ConfigureProfile(
                filters,
                localBounds,
                bucket,
                hasInsertCalibration,
                insertCenter,
                insertNormal,
                insertRadius);
            hasGeometry = true;
            RefreshPresentation();
            return true;
        }

        public void RefreshPresentation()
        {
            if (!configured || surfaceObject == null || item == null ||
                !hasGeometry)
            {
                surfaceObject?.SetActive(false);
                return;
            }

            bool hasWater = item.LiquidAmountLitres > 0.0001f &&
                string.Equals(
                    item.LiquidId,
                    LiquidTypeIds.Water,
                    StringComparison.Ordinal);
            bool visible = hasWater &&
                (tiltSpiller == null || !tiltSpiller.IsSpilling);
            surfaceObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            float ratio = Mathf.Clamp01(
                item.LiquidAmountLitres /
                Mathf.Max(0.0001f, item.Definition.MaximumContent));
            float axisOffset = Mathf.Lerp(
                emptyAxisOffset,
                fullAxisOffset,
                ratio);
            surfaceObject.transform.localPosition =
                surfaceCenter + openAxis * axisOffset;
            surfaceObject.transform.localRotation =
                Quaternion.FromToRotation(Vector3.forward, openAxis);
            float radius = surfaceRadius * Mathf.Lerp(0.82f, 1f, ratio);
            float filledDepth = Mathf.Max(
                0.004f,
                axisOffset - emptyAxisOffset);
            surfaceObject.transform.localScale =
                new Vector3(radius, radius, filledDepth);
        }

        private void ConfigureProfile(
            MeshFilter[] filters,
            Bounds bounds,
            bool bucket,
            bool hasInsertCalibration,
            Vector3 insertCenter,
            Vector3 insertNormal,
            float insertRadius)
        {
            // The former donor Water insert is presentation evidence for the
            // real mouth plane. Its rendered normal is stable even when the
            // saved bucket happens to be lying down during materialization.
            openAxis = hasInsertCalibration
                ? insertNormal.normalized
                : tiltSpiller != null
                    ? tiltSpiller.LocalOpenAxis
                    : Vector3.back;
            ProjectBounds(bounds, openAxis, out float boundsMinimum,
                out float boundsMaximum);

            if (bucket)
            {
                if (hasInsertCalibration)
                {
                    float insertProjection =
                        Vector3.Dot(insertCenter, openAxis);
                    surfaceCenter = insertCenter -
                        openAxis * insertProjection;
                    fullAxisOffset = Mathf.Clamp(
                        insertProjection - 0.004f,
                        boundsMinimum + 0.025f,
                        boundsMaximum - 0.002f);
                    surfaceRadius = Mathf.Max(
                        0.025f,
                        insertRadius * 0.93f);
                }
                else
                {
                    surfaceCenter = bounds.center - openAxis *
                        Vector3.Dot(bounds.center, openAxis);
                    fullAxisOffset = boundsMaximum -
                        (boundsMaximum - boundsMinimum) * 0.08f;
                    surfaceRadius = Mathf.Max(
                        0.025f,
                        Mathf.Min(bounds.size.x, bounds.size.y) * 0.39f);
                }

                float usableDepth = Mathf.Max(
                    0.035f,
                    fullAxisOffset - boundsMinimum);
                emptyAxisOffset = fullAxisOffset - usableDepth * 0.88f;
                return;
            }

            surfaceCenter = FindDipperBowlCenter(filters, bounds.center);
            surfaceRadius = Mathf.Max(
                0.018f,
                Mathf.Min(bounds.size.x * 0.43f, 0.046f));
            fullAxisOffset = boundsMaximum -
                (boundsMaximum - boundsMinimum) * 0.12f;
            emptyAxisOffset = Mathf.Lerp(
                boundsMinimum,
                fullAxisOffset,
                0.18f);
        }

        private bool TryReadFlatInsertCalibration(
            MeshFilter filter,
            out Vector3 center,
            out Vector3 normal,
            out float radius)
        {
            center = Vector3.zero;
            normal = Vector3.zero;
            radius = 0f;
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
            {
                return false;
            }

            center = transform.InverseTransformPoint(
                filter.transform.TransformPoint(mesh.bounds.center));
            if (mesh.isReadable)
            {
                Vector3[] normals = mesh.normals;
                for (int index = 0; index < normals.Length; index++)
                {
                    normal += transform.InverseTransformDirection(
                        filter.transform.TransformDirection(normals[index]));
                }

                Vector3[] vertices = mesh.vertices;
                for (int index = 0; index < vertices.Length; index++)
                {
                    Vector3 local = transform.InverseTransformPoint(
                        filter.transform.TransformPoint(vertices[index]));
                    Vector3 radial = local - center;
                    if (normal.sqrMagnitude > 0.001f)
                    {
                        radial = Vector3.ProjectOnPlane(radial, normal);
                    }

                    radius = Mathf.Max(radius, radial.magnitude);
                }
            }

            if (normal.sqrMagnitude < 0.001f)
            {
                normal = tiltSpiller != null
                    ? tiltSpiller.LocalOpenAxis
                    : Vector3.back;
            }

            normal.Normalize();
            if (radius <= 0.001f)
            {
                radius = Mathf.Max(mesh.bounds.extents.x,
                    mesh.bounds.extents.y);
            }

            return true;
        }

        private Vector3 FindDipperBowlCenter(
            MeshFilter[] filters,
            Vector3 fallback)
        {
            float widestX = 0f;
            float selectedY = fallback.y;
            float selectedX = fallback.x;
            for (int filterIndex = 0;
                 filterIndex < filters.Length;
                 filterIndex++)
            {
                MeshFilter filter = filters[filterIndex];
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || !mesh.isReadable)
                {
                    continue;
                }

                Vector3[] vertices = mesh.vertices;
                for (int index = 0; index < vertices.Length; index++)
                {
                    Vector3 local = transform.InverseTransformPoint(
                        filter.transform.TransformPoint(vertices[index]));
                    float width = Mathf.Abs(local.x - fallback.x);
                    if (width > widestX)
                    {
                        widestX = width;
                        selectedX = local.x;
                        selectedY = local.y;
                    }
                }
            }

            return new Vector3(
                Mathf.Lerp(fallback.x, selectedX, 0.18f),
                selectedY,
                fallback.z);
        }

        private void HandleItemStatusChanged(IItemStatusSource source)
        {
            RefreshPresentation();
        }

        private void LateUpdate()
        {
            if (!configured)
            {
                return;
            }

            RefreshPresentation();
            UpdatePourPresentation();
        }

        private void UpdatePourPresentation()
        {
            if (pourStream == null)
            {
                return;
            }

            bool pouring = hasGeometry && tiltSpiller != null &&
                tiltSpiller.IsSpilling &&
                item != null && item.LiquidAmountLitres > 0.0001f &&
                string.Equals(
                    item.LiquidId,
                    LiquidTypeIds.Water,
                    StringComparison.Ordinal);
            pourStream.SetFlowing(pouring);
            if (!pouring)
            {
                return;
            }

            Vector3 openingCenter =
                surfaceCenter + openAxis * fullAxisOffset;
            Vector3 worldOpenAxis =
                transform.TransformDirection(openAxis).normalized;
            Vector3 downhill = Vector3.ProjectOnPlane(
                Vector3.down,
                worldOpenAxis);
            Vector3 rimOffset = downhill.sqrMagnitude > 0.001f
                ? transform.InverseTransformDirection(
                      downhill.normalized) * surfaceRadius * 0.86f
                : Vector3.zero;
            pourObject.transform.localPosition = openingCenter + rimOffset;
            pourObject.transform.rotation = Quaternion.LookRotation(
                Vector3.down,
                Vector3.forward);
            pourStream.SetIntensity(
                Mathf.Lerp(0.35f, 1.25f,
                    tiltSpiller.SpillIntensity01));
        }

        private static bool IsFlatInsert(Vector3 size)
        {
            float minimum = Mathf.Min(size.x, size.y, size.z);
            float maximum = Mathf.Max(size.x, size.y, size.z);
            return minimum < 0.001f && maximum > 0.05f;
        }

        private static void EncapsulateMeshBounds(
            Transform root,
            Transform meshTransform,
            Bounds meshBounds,
            ref Bounds result,
            ref bool hasBounds)
        {
            Vector3 minimum = meshBounds.min;
            Vector3 maximum = meshBounds.max;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 localCorner = new Vector3(
                    (corner & 1) == 0 ? minimum.x : maximum.x,
                    (corner & 2) == 0 ? minimum.y : maximum.y,
                    (corner & 4) == 0 ? minimum.z : maximum.z);
                Vector3 rootLocal = root.InverseTransformPoint(
                    meshTransform.TransformPoint(localCorner));
                if (!hasBounds)
                {
                    result = new Bounds(rootLocal, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    result.Encapsulate(rootLocal);
                }
            }
        }

        private static void ProjectBounds(
            Bounds bounds,
            Vector3 axis,
            out float minimum,
            out float maximum)
        {
            minimum = float.PositiveInfinity;
            maximum = float.NegativeInfinity;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                float projection = Vector3.Dot(point, axis);
                minimum = Mathf.Min(minimum, projection);
                maximum = Mathf.Max(maximum, projection);
            }
        }

        private static Mesh BuildSurfaceMesh()
        {
            const int Segments = 28;
            var vertices = new Vector3[Segments * 2 + 2];
            var triangles = new int[Segments * 12];
            int topCenter = Segments * 2;
            int bottomCenter = topCenter + 1;
            vertices[topCenter] = Vector3.zero;
            vertices[bottomCenter] = Vector3.back;
            for (int index = 0; index < Segments; index++)
            {
                float angle = index * Mathf.PI * 2f / Segments;
                float variation = 0.96f +
                    Mathf.Sin(index * 2.37f) * 0.025f;
                vertices[index] = new Vector3(
                    Mathf.Cos(angle) * variation,
                    Mathf.Sin(angle) * variation,
                    0f);
                vertices[Segments + index] = new Vector3(
                    vertices[index].x,
                    vertices[index].y,
                    -1f);
                int next = (index + 1) % Segments;
                int triangle = index * 12;
                triangles[triangle] = topCenter;
                triangles[triangle + 1] = index;
                triangles[triangle + 2] = next;
                triangles[triangle + 3] = bottomCenter;
                triangles[triangle + 4] = Segments + next;
                triangles[triangle + 5] = Segments + index;
                triangles[triangle + 6] = index;
                triangles[triangle + 7] = Segments + index;
                triangles[triangle + 8] = Segments + next;
                triangles[triangle + 9] = index;
                triangles[triangle + 10] = Segments + next;
                triangles[triangle + 11] = next;
            }

            var mesh = new Mesh
            {
                name = "MSC Dynamic Container Water",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                triangles = triangles,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material BuildWaterMaterial()
        {
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "HDRP/Lit is required for container water.");
            }

            var material = new Material(shader)
            {
                name = "MSC Dynamic Container Water",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent + 6,
            };
            material.SetColor(
                "_BaseColor",
                new Color(0.12f, 0.18f, 0.2f, 0.5f));
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.97f);
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
                    "Container water material failed HDRP validation.");
            }

            return material;
        }

        private void OnDestroy()
        {
            if (item != null)
            {
                item.StatusChanged -= HandleItemStatusChanged;
            }

            DestroyRuntimeObject(surfaceMaterial);
            DestroyRuntimeObject(surfaceMesh);
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
    }
}
