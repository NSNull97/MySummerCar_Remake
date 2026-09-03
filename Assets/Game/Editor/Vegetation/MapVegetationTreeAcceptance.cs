using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.World.Vegetation;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.Editor.Vegetation
{
    /// <summary>
    /// Shared structural/material acceptance contract for approved tree prefabs
    /// and their packed GPU prototypes. It reads imported non-readable meshes
    /// through MeshData and never changes importer settings.
    /// </summary>
    internal static class MapVegetationTreeAcceptance
    {
        public const int MinimumNearTriangles = 64;
        public const int MinimumNearBarkTriangles = 8;
        public const int MinimumNearFoliageTriangles = 24;

        internal static TreeAcceptanceEvidence AuditPrefab(
            GameObject instance,
            string species,
            string sourcePrefabPath)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (string.IsNullOrWhiteSpace(species))
                throw new ArgumentException("Tree species is required.", nameof(species));
            LODGroup[] groups = instance.GetComponentsInChildren<LODGroup>(true);
            if (groups.Length != 1)
                throw new InvalidDataException(
                    "Approved tree must contain exactly one LODGroup: " +
                    sourcePrefabPath);
            LOD[] lods = groups[0].GetLODs();
            if (lods.Length < 2)
                throw new InvalidDataException(
                    "Approved tree requires distinct near and far LODs: " +
                    sourcePrefabPath);

            var evidence = NewEvidence(
                species,
                sourcePrefabPath,
                AssetDatabase.AssetPathToGUID(sourcePrefabPath),
                instance.name,
                false,
                lods.Length);
            for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
            {
                Renderer[] renderers = lods[lodIndex].renderers ??
                    Array.Empty<Renderer>();
                if (renderers.Length == 0 || renderers.Any(renderer => renderer == null))
                    throw new InvalidDataException(
                        Describe(evidence) + " LOD" + lodIndex +
                        " contains no valid renderers.");
                evidence.lods.Add(AuditRendererSet(
                    instance.transform,
                    renderers,
                    species,
                    lodIndex,
                    lods.Length,
                    evidence.materialSlots));
            }
            ValidateNearGeometry(evidence);
            return evidence;
        }

        internal static TreeAcceptanceEvidence AuditPackedPrototype(
            PackedWoodyPrototypeAsset prototype)
        {
            if (prototype == null) throw new ArgumentNullException(nameof(prototype));
            if (prototype.Species < PackedWoodySpecies.Spruce ||
                prototype.Species > PackedWoodySpecies.Aspen)
                throw new ArgumentException(
                    "Only packed tree prototypes use the tree acceptance gate.",
                    nameof(prototype));
            string species = prototype.Species.ToString();
            IReadOnlyList<PackedWoodyLod> lods = prototype.Lods;
            if (lods == null || lods.Count < 2)
                throw new InvalidDataException(
                    "Packed tree prototype requires distinct near and far LODs: " +
                    prototype.name);
            string sourcePath = AssetDatabase.GUIDToAssetPath(
                prototype.SourceAssetGuid);
            var evidence = NewEvidence(
                species,
                sourcePath,
                prototype.SourceAssetGuid,
                prototype.name,
                true,
                lods.Count);
            for (int lodIndex = 0; lodIndex < lods.Count; lodIndex++)
            {
                PackedWoodyLod lod = lods[lodIndex];
                if (lod == null || lod.DrawParts.Count == 0)
                    throw new InvalidDataException(
                        Describe(evidence) + " LOD" + lodIndex +
                        " contains no packed draw parts.");
                var geometry = new TreeLodGeometryEvidence
                {
                    lodIndex = lodIndex,
                    rendererCount = lod.DrawParts.Count
                };
                for (int partIndex = 0; partIndex < lod.DrawParts.Count; partIndex++)
                {
                    PackedWoodyDrawPart part = lod.DrawParts[partIndex];
                    if (part == null || part.Mesh == null || part.Material == null)
                        throw new InvalidDataException(
                            Describe(evidence) + " LOD" + lodIndex +
                            " contains an incomplete packed draw part.");
                    if (part.SubMeshIndex < 0 ||
                        part.SubMeshIndex >= part.Mesh.subMeshCount)
                        throw new InvalidDataException(
                            Describe(evidence) + " LOD" + lodIndex +
                            " contains an invalid packed submesh index.");
                    MapVegetationTreeMaterialRole role = ResolveRole(
                        part.Material,
                        species,
                        lodIndex,
                        lods.Count);
                    evidence.materialSlots.Add(AuditMaterial(
                        part.Material,
                        species,
                        role,
                        lodIndex,
                        "PackedDrawPart[" + partIndex + "]",
                        part.SubMeshIndex));
                    SubMeshGeometry subMesh = ReadSubMeshGeometry(
                        part.Mesh,
                        part.SubMeshIndex,
                        Matrix4x4.identity);
                    AddGeometry(geometry, subMesh, role);
                }
                FinalizeGeometry(geometry, evidence);
                evidence.lods.Add(geometry);
            }
            ValidateNearGeometry(evidence);
            if (AssetDatabase.Contains(prototype))
            {
                if (string.IsNullOrWhiteSpace(sourcePath))
                    Reject(evidence,
                        "generated packed prototype has no resolvable source prefab.");
                if (evidence.materialSlots.Any(slot =>
                        string.IsNullOrWhiteSpace(slot.materialPath)))
                    Reject(evidence,
                        "generated packed prototype contains a transient material.");
            }
            return evidence;
        }

        private static TreeAcceptanceEvidence NewEvidence(
            string species,
            string sourcePath,
            string sourceGuid,
            string name,
            bool packed,
            int lodCount) =>
            new TreeAcceptanceEvidence
            {
                species = species,
                sourcePrefabPath = sourcePath,
                sourcePrefabGuid = sourceGuid,
                displayName = name,
                packedPrototype = packed,
                lodCount = lodCount
            };

        private static TreeLodGeometryEvidence AuditRendererSet(
            Transform root,
            Renderer[] renderers,
            string species,
            int lodIndex,
            int lodCount,
            List<TreeMaterialSlotEvidence> materialSlots)
        {
            var geometry = new TreeLodGeometryEvidence
            {
                lodIndex = lodIndex,
                rendererCount = renderers.Length
            };
            foreach (Renderer renderer in renderers)
            {
                if (!(renderer is MeshRenderer))
                    throw new InvalidDataException(
                        "Approved static tree LOD uses a non-MeshRenderer: " +
                        renderer.name);
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                    throw new InvalidDataException(
                        "Approved tree renderer has no mesh: " + renderer.name);
                Material[] materials = renderer.sharedMaterials;
                if (materials.Length == 0 || materials.Any(material => material == null))
                    throw new InvalidDataException(
                        "Approved tree renderer has a missing material slot: " +
                        renderer.name);
                if (materials.Length < mesh.subMeshCount)
                    throw new InvalidDataException(
                        "Approved tree renderer has fewer materials than submeshes: " +
                        renderer.name);
                string rendererPath = RelativePath(root, renderer.transform);
                var roles = new MapVegetationTreeMaterialRole[materials.Length];
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    roles[slot] = ResolveRole(
                        materials[slot], species, lodIndex, lodCount);
                    materialSlots.Add(AuditMaterial(
                        materials[slot],
                        species,
                        roles[slot],
                        lodIndex,
                        rendererPath,
                        slot < mesh.subMeshCount ? slot : -1));
                }
                Matrix4x4 toTree = root.worldToLocalMatrix *
                    renderer.transform.localToWorldMatrix;
                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    SubMeshGeometry subGeometry = ReadSubMeshGeometry(
                        mesh, subMesh, toTree);
                    AddGeometry(geometry, subGeometry, roles[subMesh]);
                }
            }
            FinalizeGeometry(geometry, null);
            return geometry;
        }

        private static void FinalizeGeometry(
            TreeLodGeometryEvidence geometry,
            TreeAcceptanceEvidence owner)
        {
            if (!geometry.hasBounds || geometry.triangleCount <= 0)
                throw new InvalidDataException(
                    (owner != null ? Describe(owner) : "Approved tree") +
                    " LOD" + geometry.lodIndex + " has no triangle geometry.");
            geometry.boundsSize = geometry.bounds.size;
            geometry.barkBoundsSize = geometry.hasBarkBounds
                ? geometry.barkBounds.size : Vector3.zero;
            geometry.foliageBoundsSize = geometry.hasFoliageBounds
                ? geometry.foliageBounds.size : Vector3.zero;
        }

        private static void AddGeometry(
            TreeLodGeometryEvidence target,
            SubMeshGeometry source,
            MapVegetationTreeMaterialRole role)
        {
            Encapsulate(ref target.bounds, ref target.hasBounds, source.Bounds);
            target.triangleCount += source.TriangleCount;
            if (role == MapVegetationTreeMaterialRole.Bark)
            {
                target.barkTriangleCount += source.TriangleCount;
                Encapsulate(
                    ref target.barkBounds,
                    ref target.hasBarkBounds,
                    source.Bounds);
            }
            else
            {
                target.foliageTriangleCount += source.TriangleCount;
                Encapsulate(
                    ref target.foliageBounds,
                    ref target.hasFoliageBounds,
                    source.Bounds);
                if (role == MapVegetationTreeMaterialRole.Billboard)
                    target.billboardTriangleCount += source.TriangleCount;
            }
        }

        private static void ValidateNearGeometry(TreeAcceptanceEvidence evidence)
        {
            TreeLodGeometryEvidence near = evidence.lods[0];
            float height = near.boundsSize.y;
            if (!float.IsFinite(height) || height <= 0.25f)
                Reject(evidence, "LOD0 height is invalid.");
            float minimumCrownDepth = Mathf.Max(0.02f, height * 0.02f);
            if (near.boundsSize.x < minimumCrownDepth ||
                near.boundsSize.z < minimumCrownDepth)
                Reject(evidence,
                    "LOD0 is flat in a horizontal axis; crossed cards or a " +
                    "single billboard are not accepted as near geometry.");
            if (near.triangleCount < MinimumNearTriangles)
                Reject(evidence,
                    "LOD0 triangle count is too small for a real near tree (" +
                    near.triangleCount + " < " + MinimumNearTriangles + ").");
            if (!near.hasBarkBounds ||
                near.barkTriangleCount < MinimumNearBarkTriangles)
                Reject(evidence,
                    "LOD0 has no independently rendered bark/trunk material geometry.");
            if (!near.hasFoliageBounds ||
                near.foliageTriangleCount < MinimumNearFoliageTriangles)
                Reject(evidence,
                    "LOD0 has no substantial foliage geometry; a log proxy is forbidden.");
            if (near.billboardTriangleCount > 0)
                Reject(evidence,
                    "LOD0 contains billboard-classified geometry.");

            Vector3 trunk = near.barkBoundsSize;
            float minimumTrunkDepth = Mathf.Max(0.002f, height * 0.0005f);
            if (trunk.x < minimumTrunkDepth || trunk.z < minimumTrunkDepth ||
                trunk.y < height * 0.20f)
                Reject(evidence,
                    "LOD0 bark does not prove a vertically extensive 3D trunk volume.");
            if (trunk.y <= Mathf.Max(trunk.x, trunk.z) * 1.25f)
                Reject(evidence,
                    "LOD0 bark bounds describe a rock/log-like volume, not an upright trunk.");
            near.nonFlatNearGeometry = true;
            near.hasVolumetricTrunk = true;
            near.hasBarkMaterialGeometry = true;
            near.hasFoliageGeometry = true;
            near.nearProxyRejected = true;
        }

        private static TreeMaterialSlotEvidence AuditMaterial(
            Material material,
            string expectedSpecies,
            MapVegetationTreeMaterialRole role,
            int lodIndex,
            string rendererPath,
            int subMeshIndex)
        {
            if (material == null || material.shader == null)
                throw new InvalidDataException(
                    "Tree material slot has a missing material/shader.");
            string shaderName = material.shader.name;
            if (shaderName != "HDRP/Lit" && shaderName != "MSC/HDRP/Spruce Wind")
                throw new InvalidDataException(
                    "Tree material bypasses the reviewed HDRP shaders: " +
                    AssetDatabase.GetAssetPath(material));
            RequireProperty(material, "_BaseColor");
            RequireProperty(material, "_Metallic");
            RequireProperty(material, "_Smoothness");
            Color tint = material.GetColor("_BaseColor");
            float metallic = material.GetFloat("_Metallic");
            float smoothness = material.GetFloat("_Smoothness");
            Color.RGBToHSV(new Color(
                Mathf.Clamp01(tint.r),
                Mathf.Clamp01(tint.g),
                Mathf.Clamp01(tint.b)),
                out float hue,
                out float saturation,
                out _);
            float luma = RelativeLuminance(tint);
            float specularF0 = role == MapVegetationTreeMaterialRole.Bark
                ? 0.04f
                : material.HasProperty("_SpecularColor")
                    ? Mathf.Max(
                        material.GetColor("_SpecularColor").r,
                        Mathf.Max(
                            material.GetColor("_SpecularColor").g,
                            material.GetColor("_SpecularColor").b))
                    : float.PositiveInfinity;

            if (!float.IsFinite(metallic) || Mathf.Abs(metallic) > 0.0001f)
                RejectMaterial(material, "metallic must be exactly zero");
            float maximumSmoothness = role == MapVegetationTreeMaterialRole.Bark
                ? 0.1201f : 0.046f;
            if (!float.IsFinite(smoothness) || smoothness < 0f ||
                smoothness > maximumSmoothness)
                RejectMaterial(material,
                    "smoothness exceeds its matte tree-role budget");
            if (!float.IsFinite(specularF0) || specularF0 < 0f ||
                specularF0 > (role == MapVegetationTreeMaterialRole.Billboard
                    ? MapVegetationTreeMaterialPolicy.BillboardSpecularF0 + 0.0001f
                    : role == MapVegetationTreeMaterialRole.Foliage
                        ? MapVegetationTreeMaterialPolicy.FoliageSpecularF0 + 0.0001f
                        : 0.0401f))
                RejectMaterial(material, "specular F0 exceeds its tree-role budget");
            if (material.HasProperty("_ReceivesSSR") &&
                material.GetFloat("_ReceivesSSR") > 0.0001f)
                RejectMaterial(material, "screen-space reflections must be disabled");
            if (material.HasProperty("_CoatMask") &&
                material.GetFloat("_CoatMask") > 0.0001f)
                RejectMaterial(material, "coat must be disabled");
            if (material.HasProperty("_SubsurfaceMask") &&
                material.GetFloat("_SubsurfaceMask") > 0.0001f)
                RejectMaterial(material, "subsurface must be disabled");
            if (material.HasProperty("_EmissiveColor") &&
                material.GetColor("_EmissiveColor").maxColorComponent > 0.0001f)
                RejectMaterial(material, "emission must be disabled");

            if (role == MapVegetationTreeMaterialRole.Bark)
            {
                if (luma < 0.025f || luma > 0.93f)
                    RejectMaterial(material,
                        "bark base-colour luminance is outside the natural range");
            }
            else
            {
                float hueDegrees = hue * 360f;
                if (hueDegrees < 55f || hueDegrees > 165f ||
                    saturation < 0.055f)
                    RejectMaterial(material,
                        "foliage hue/saturation is outside the boreal green range");
                if (luma < 0.38f || luma > 0.86f)
                    RejectMaterial(material,
                        "foliage base-colour luminance is outside the boreal range");
                if (!(tint.g > tint.r * 1.005f && tint.g > tint.b * 1.035f))
                    RejectMaterial(material,
                        "foliage base colour is not green-dominant");
            }

            return new TreeMaterialSlotEvidence
            {
                lodIndex = lodIndex,
                rendererPath = rendererPath,
                subMeshIndex = subMeshIndex,
                materialPath = AssetDatabase.GetAssetPath(material),
                materialName = material.name,
                shaderName = shaderName,
                role = role.ToString(),
                expectedSpecies = expectedSpecies,
                metallic = metallic,
                smoothness = smoothness,
                specularF0 = specularF0,
                baseColor = tint,
                baseColorHueDegrees = hue * 360f,
                baseColorSaturation = saturation,
                baseColorLuminance = luma
            };
        }

        private static MapVegetationTreeMaterialRole ResolveRole(
            Material material,
            string expectedSpecies,
            int lodIndex,
            int lodCount)
        {
            string materialPath = AssetDatabase.GetAssetPath(material);
            foreach (string sourceGuid in
                     MapVegetationMaterialBindings.ApprovedTreeSourceGuids)
            {
                if (!string.Equals(
                        materialPath,
                        MapVegetationMaterialBindings.BindingPathForTests(sourceGuid),
                        StringComparison.Ordinal))
                    continue;
                if (!MapVegetationMaterialBindings.TryGetTreePolicy(
                        sourceGuid, out MapVegetationTreeMaterialPolicy policy))
                    throw new InvalidDataException(
                        "Generated tree binding lost its role policy: " +
                        materialPath);
                if (!string.Equals(
                        policy.Species, expectedSpecies,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        "Tree material species does not match its prefab: " +
                        materialPath);
                return policy.Role;
            }

            string identity = (material.name + " " + materialPath).ToLowerInvariant();
            if (identity.Contains("bark") || identity.Contains("trunk"))
                return MapVegetationTreeMaterialRole.Bark;
            if (identity.Contains("billboard") || identity.Contains("atlas") ||
                lodIndex == lodCount - 1)
                return MapVegetationTreeMaterialRole.Billboard;
            return MapVegetationTreeMaterialRole.Foliage;
        }

        private static SubMeshGeometry ReadSubMeshGeometry(
            Mesh mesh,
            int subMesh,
            Matrix4x4 transform)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            using Mesh.MeshDataArray data = MeshUtility.AcquireReadOnlyMeshData(mesh);
            Mesh.MeshData read = data[0];
            if (subMesh < 0 || subMesh >= read.subMeshCount)
                throw new ArgumentOutOfRangeException(nameof(subMesh));
            SubMeshDescriptor descriptor = read.GetSubMesh(subMesh);
            if (descriptor.topology != MeshTopology.Triangles ||
                descriptor.indexCount < 3 || descriptor.indexCount % 3 != 0)
                throw new InvalidDataException(
                    "Tree draw geometry must use non-empty triangles: " + mesh.name);
            using var vertices = new NativeArray<Vector3>(
                read.vertexCount, Allocator.Temp);
            read.GetVertices(vertices);
            using var indices = new NativeArray<int>(
                descriptor.indexCount, Allocator.Temp);
            read.GetIndices(indices, subMesh, true);
            if (indices.Length == 0)
                throw new InvalidDataException(
                    "Tree submesh contains no indices: " + mesh.name);
            if (indices[0] < 0 || indices[0] >= vertices.Length)
                throw new InvalidDataException(
                    "Tree submesh index exceeds its vertex buffer: " + mesh.name);
            Vector3 first = transform.MultiplyPoint3x4(vertices[indices[0]]);
            var bounds = new Bounds(first, Vector3.zero);
            for (int index = 1; index < indices.Length; index++)
            {
                int vertex = indices[index];
                if (vertex < 0 || vertex >= vertices.Length)
                    throw new InvalidDataException(
                        "Tree submesh index exceeds its vertex buffer: " +
                        mesh.name);
                bounds.Encapsulate(transform.MultiplyPoint3x4(vertices[vertex]));
            }
            return new SubMeshGeometry(bounds, indices.Length / 3);
        }

        private static void Encapsulate(
            ref Bounds destination,
            ref bool initialized,
            Bounds source)
        {
            if (!initialized)
            {
                destination = source;
                initialized = true;
            }
            else destination.Encapsulate(source);
        }

        private static string RelativePath(Transform root, Transform child)
        {
            var parts = new Stack<string>();
            while (child != root)
            {
                if (child == null) return "<outside-tree-root>";
                parts.Push(child.name);
                child = child.parent;
            }
            return parts.Count == 0 ? root.name : string.Join("/", parts);
        }

        private static float RelativeLuminance(Color value) =>
            value.r * 0.2126f + value.g * 0.7152f + value.b * 0.0722f;

        private static void RequireProperty(Material material, string property)
        {
            if (!material.HasProperty(property))
                RejectMaterial(material, "required property is missing: " + property);
        }

        private static void RejectMaterial(Material material, string reason)
        {
            string path = AssetDatabase.GetAssetPath(material);
            throw new InvalidDataException(
                "Tree material acceptance failed (" + reason + "): " +
                (!string.IsNullOrEmpty(path) ? path : material.name));
        }

        private static void Reject(
            TreeAcceptanceEvidence evidence,
            string reason) =>
            throw new InvalidDataException(
                "Tree geometry acceptance failed for " + Describe(evidence) +
                ": " + reason);

        private static string Describe(TreeAcceptanceEvidence evidence) =>
            evidence.species + " " +
            (!string.IsNullOrWhiteSpace(evidence.sourcePrefabPath)
                ? evidence.sourcePrefabPath
                : evidence.displayName);

        private readonly struct SubMeshGeometry
        {
            public SubMeshGeometry(Bounds bounds, int triangleCount)
            {
                Bounds = bounds;
                TriangleCount = triangleCount;
            }

            public Bounds Bounds { get; }
            public int TriangleCount { get; }
        }
    }

    [Serializable]
    internal sealed class TreeAcceptanceEvidence
    {
        public string species;
        public string sourcePrefabPath;
        public string sourcePrefabGuid;
        public string displayName;
        public bool packedPrototype;
        public int lodCount;
        public List<TreeLodGeometryEvidence> lods =
            new List<TreeLodGeometryEvidence>();
        public List<TreeMaterialSlotEvidence> materialSlots =
            new List<TreeMaterialSlotEvidence>();
    }

    [Serializable]
    internal sealed class TreeLodGeometryEvidence
    {
        public int lodIndex;
        public int rendererCount;
        public int triangleCount;
        public int barkTriangleCount;
        public int foliageTriangleCount;
        public int billboardTriangleCount;
        public Bounds bounds;
        public Bounds barkBounds;
        public Bounds foliageBounds;
        public Vector3 boundsSize;
        public Vector3 barkBoundsSize;
        public Vector3 foliageBoundsSize;
        public bool hasBounds;
        public bool hasBarkBounds;
        public bool hasFoliageBounds;
        public bool nonFlatNearGeometry;
        public bool hasVolumetricTrunk;
        public bool hasBarkMaterialGeometry;
        public bool hasFoliageGeometry;
        public bool nearProxyRejected;
    }

    [Serializable]
    internal sealed class TreeMaterialSlotEvidence
    {
        public int lodIndex;
        public string rendererPath;
        public int subMeshIndex;
        public string materialPath;
        public string materialName;
        public string shaderName;
        public string role;
        public string expectedSpecies;
        public float metallic;
        public float smoothness;
        public float specularF0;
        public Color baseColor;
        public float baseColorHueDegrees;
        public float baseColorSaturation;
        public float baseColorLuminance;
    }
}
