using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>
    /// Reads only the Unity YAML records required to reconstruct a sanitized
    /// skinned hand. Donor components, scripts and state machines are never
    /// instantiated or copied.
    /// </summary>
    internal sealed class DonorUnitySceneModel
    {
        private const int GameObjectClassId = 1;
        private const int TransformClassId = 4;
        private const int MeshRendererClassId = 23;
        private const int MeshFilterClassId = 33;
        private const int RigidbodyClassId = 54;
        private const int HingeJointClassId = 59;
        private const int MeshColliderClassId = 64;
        private const int BoxColliderClassId = 65;
        private const int SkinnedMeshRendererClassId = 137;
        private const int SphereColliderClassId = 135;
        private const int CapsuleColliderClassId = 136;
        private const int MonoBehaviourClassId = 114;

        private static readonly Regex HeaderPattern = new Regex(
            @"^--- !u!(?<classId>\d+) &(?<fileId>-?\d+)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly Dictionary<long, DonorGameObjectRecord> gameObjects =
            new Dictionary<long, DonorGameObjectRecord>();

        private readonly Dictionary<long, DonorTransformRecord> transforms =
            new Dictionary<long, DonorTransformRecord>();

        private readonly List<DonorSkinnedRendererRecord> renderers =
            new List<DonorSkinnedRendererRecord>();

        private readonly List<DonorStaticRendererSource> staticRendererSources =
            new List<DonorStaticRendererSource>();

        private readonly Dictionary<long, string> meshGuidByGameObject =
            new Dictionary<long, string>();

        private readonly Dictionary<long, long> transformByGameObject =
            new Dictionary<long, long>();

        private readonly Dictionary<long, DonorRigidbodyRecord> rigidbodiesByGameObject =
            new Dictionary<long, DonorRigidbodyRecord>();

        private readonly Dictionary<long, DonorRigidbodyRecord> rigidbodiesByComponent =
            new Dictionary<long, DonorRigidbodyRecord>();

        private readonly List<DonorHingeJointRecord> hingeJoints =
            new List<DonorHingeJointRecord>();

        private readonly List<DonorColliderRecord> colliders =
            new List<DonorColliderRecord>();

        private readonly List<DonorMonoBehaviourRecord> monoBehaviours =
            new List<DonorMonoBehaviourRecord>();

        public static DonorUnitySceneModel Parse(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new ArgumentException(
                    "Donor scene path must not be empty.",
                    nameof(scenePath));
            }

            if (!File.Exists(scenePath))
            {
                throw new FileNotFoundException(
                    "The locked donor scene does not exist.",
                    scenePath);
            }

            var model = new DonorUnitySceneModel();
            using (var reader = new StreamReader(scenePath))
            {
                int currentClassId = 0;
                long currentFileId = 0;
                StringBuilder block = null;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Match header = HeaderPattern.Match(line);
                    if (header.Success)
                    {
                        model.ParseBlock(
                            currentClassId,
                            currentFileId,
                            block?.ToString());
                        currentClassId = int.Parse(
                            header.Groups["classId"].Value,
                            CultureInfo.InvariantCulture);
                        currentFileId = long.Parse(
                            header.Groups["fileId"].Value,
                            CultureInfo.InvariantCulture);
                        block = IsRelevantClass(currentClassId)
                            ? new StringBuilder(1024)
                            : null;
                        continue;
                    }

                    block?.AppendLine(line);
                }

                model.ParseBlock(
                    currentClassId,
                    currentFileId,
                    block?.ToString());
            }

            model.BuildIndices();
            return model;
        }

        public DonorActionSlice CreateHandActionSlice(
            long actionRootTransformId,
            long animationTargetTransformId,
            string handMeshGuid,
            IReadOnlyList<long> rendererComponentIds,
            IReadOnlyList<long> staticRendererComponentIds = null)
        {
            RequireTransform(actionRootTransformId);
            RequireTransform(animationTargetTransformId);
            if (rendererComponentIds == null ||
                rendererComponentIds.Count == 0)
            {
                throw new ArgumentException(
                    "At least one locked donor renderer ID is required.",
                    nameof(rendererComponentIds));
            }

            DonorSkinnedRendererRecord[] actionRenderers = renderers
                .Where(candidate =>
                    string.Equals(
                        candidate.MeshGuid,
                        handMeshGuid,
                        StringComparison.OrdinalIgnoreCase))
                .Where(candidate =>
                    transformByGameObject.TryGetValue(
                        candidate.GameObjectId,
                        out long rendererTransformId) &&
                    IsDescendantOrSelf(
                        rendererTransformId,
                        actionRootTransformId))
                .Where(candidate =>
                    rendererComponentIds.Contains(candidate.ComponentId))
                .OrderBy(candidate => candidate.ComponentId)
                .ToArray();

            if (actionRenderers.Length != rendererComponentIds.Count)
            {
                throw new InvalidOperationException(
                    $"Expected {rendererComponentIds.Count} locked donor hand " +
                    $"renderer(s) below transform {actionRootTransformId}; " +
                    $"found {actionRenderers.Length}. Expected IDs: " +
                    $"{string.Join(", ", rendererComponentIds)}.");
            }

            IReadOnlyList<long> requestedStaticRendererIds =
                staticRendererComponentIds ?? Array.Empty<long>();
            DonorStaticRendererRecord[] actionStaticRenderers =
                staticRendererSources
                    .Where(candidate =>
                        requestedStaticRendererIds.Contains(candidate.ComponentId))
                    .Where(candidate =>
                        transformByGameObject.TryGetValue(
                            candidate.GameObjectId,
                            out long rendererTransformId) &&
                        IsDescendantOrSelf(
                            rendererTransformId,
                            actionRootTransformId))
                    .Select(candidate => new DonorStaticRendererRecord(
                        candidate.ComponentId,
                        candidate.GameObjectId,
                        meshGuidByGameObject.TryGetValue(
                            candidate.GameObjectId,
                            out string meshGuid)
                                ? meshGuid
                                : string.Empty,
                        candidate.MaterialGuids))
                    .OrderBy(candidate => candidate.ComponentId)
                    .ToArray();
            if (actionStaticRenderers.Length != requestedStaticRendererIds.Count ||
                actionStaticRenderers.Any(candidate =>
                    string.IsNullOrWhiteSpace(candidate.MeshGuid)))
            {
                throw new InvalidOperationException(
                    $"Expected {requestedStaticRendererIds.Count} locked donor accessory renderer(s) below transform {actionRootTransformId}; found {actionStaticRenderers.Length}. Expected IDs: {string.Join(", ", requestedStaticRendererIds)}.");
            }

            var required = new HashSet<long>();
            AddAncestors(
                actionRootTransformId,
                actionRootTransformId,
                required);
            AddAncestors(
                animationTargetTransformId,
                actionRootTransformId,
                required);
            foreach (DonorSkinnedRendererRecord renderer in actionRenderers)
            {
                AddAncestors(
                    transformByGameObject[renderer.GameObjectId],
                    actionRootTransformId,
                    required);
                if (renderer.RootBoneTransformId > 0L)
                {
                    AddAncestors(
                        renderer.RootBoneTransformId,
                        actionRootTransformId,
                        required);
                }
                foreach (long boneTransformId in renderer.BoneTransformIds)
                {
                    if (boneTransformId > 0L)
                    {
                        AddAncestors(
                            boneTransformId,
                            actionRootTransformId,
                            required);
                    }
                }
            }
            foreach (DonorStaticRendererRecord renderer in actionStaticRenderers)
            {
                AddAncestors(
                    transformByGameObject[renderer.GameObjectId],
                    actionRootTransformId,
                    required);
            }

            return new DonorActionSlice(
                actionRootTransformId,
                animationTargetTransformId,
                actionRenderers,
                actionStaticRenderers,
                required
                    .Select(RequireTransform)
                    .OrderBy(GetDepth)
                    .ThenBy(record => record.TransformId)
                    .ToArray());
        }

        public DonorActionSlice CreatePresentationSlice(
            long actionRootTransformId,
            long animationTargetTransformId,
            IReadOnlyList<long> rendererComponentIds,
            IReadOnlyList<long> staticRendererComponentIds,
            IReadOnlyList<long> requiredTransformIds = null)
        {
            RequireTransform(actionRootTransformId);
            RequireTransform(animationTargetTransformId);
            IReadOnlyList<long> requestedRendererIds =
                rendererComponentIds ?? Array.Empty<long>();

            DonorSkinnedRendererRecord[] selectedRenderers = renderers
                .Where(candidate =>
                    requestedRendererIds.Contains(candidate.ComponentId))
                .Where(candidate =>
                    transformByGameObject.TryGetValue(
                        candidate.GameObjectId,
                        out long rendererTransformId) &&
                    IsDescendantOrSelf(
                        rendererTransformId,
                        actionRootTransformId))
                .OrderBy(candidate => candidate.ComponentId)
                .ToArray();
            if (selectedRenderers.Length != requestedRendererIds.Count)
            {
                throw new InvalidOperationException(
                    $"Expected {requestedRendererIds.Count} locked donor " +
                    $"skinned renderer(s) below transform " +
                    $"{actionRootTransformId}; found " +
                    $"{selectedRenderers.Length}.");
            }

            IReadOnlyList<long> requestedStaticIds =
                staticRendererComponentIds ?? Array.Empty<long>();
            DonorStaticRendererRecord[] selectedStaticRenderers =
                staticRendererSources
                    .Where(candidate =>
                        requestedStaticIds.Contains(candidate.ComponentId))
                    .Where(candidate =>
                        transformByGameObject.TryGetValue(
                            candidate.GameObjectId,
                            out long rendererTransformId) &&
                        IsDescendantOrSelf(
                            rendererTransformId,
                            actionRootTransformId))
                    .Select(CreateStaticRendererRecord)
                    .OrderBy(candidate => candidate.ComponentId)
                    .ToArray();
            if (selectedStaticRenderers.Length != requestedStaticIds.Count ||
                selectedStaticRenderers.Any(candidate =>
                    string.IsNullOrWhiteSpace(candidate.MeshGuid)))
            {
                throw new InvalidOperationException(
                    $"Expected {requestedStaticIds.Count} locked donor static " +
                    $"renderer(s) below transform {actionRootTransformId}; " +
                    $"found {selectedStaticRenderers.Length}.");
            }

            var required = new HashSet<long>();
            AddAncestors(
                actionRootTransformId,
                actionRootTransformId,
                required);
            AddAncestors(
                animationTargetTransformId,
                actionRootTransformId,
                required);
            foreach (DonorSkinnedRendererRecord renderer in selectedRenderers)
            {
                AddAncestors(
                    transformByGameObject[renderer.GameObjectId],
                    actionRootTransformId,
                    required);
                if (renderer.RootBoneTransformId > 0L)
                {
                    AddAncestors(
                        renderer.RootBoneTransformId,
                        actionRootTransformId,
                        required);
                }
                foreach (long boneTransformId in renderer.BoneTransformIds)
                {
                    if (boneTransformId > 0L)
                    {
                        AddAncestors(
                            boneTransformId,
                            actionRootTransformId,
                            required);
                    }
                }
            }

            foreach (DonorStaticRendererRecord renderer in
                     selectedStaticRenderers)
            {
                AddAncestors(
                    transformByGameObject[renderer.GameObjectId],
                    actionRootTransformId,
                    required);
            }

            foreach (long transformId in
                     requiredTransformIds ?? Array.Empty<long>())
            {
                AddAncestors(
                    transformId,
                    actionRootTransformId,
                    required);
            }

            return new DonorActionSlice(
                actionRootTransformId,
                animationTargetTransformId,
                selectedRenderers,
                selectedStaticRenderers,
                required
                    .Select(RequireTransform)
                    .OrderBy(GetDepth)
                    .ThenBy(record => record.TransformId)
                    .ToArray());
        }

        public IReadOnlyList<DonorStaticRendererRecord>
            GetActiveStaticRenderersBelow(
                IReadOnlyList<long> rootTransformIds,
                bool treatSelectedRootsAsActive = false)
        {
            if (rootTransformIds == null || rootTransformIds.Count == 0)
            {
                return Array.Empty<DonorStaticRendererRecord>();
            }

            foreach (long transformId in rootTransformIds)
            {
                RequireTransform(transformId);
            }

            return staticRendererSources
                .Where(candidate =>
                    candidate.Enabled &&
                    transformByGameObject.TryGetValue(
                        candidate.GameObjectId,
                        out long rendererTransformId) &&
                    rootTransformIds.Any(rootId =>
                        IsDescendantOrSelf(rendererTransformId, rootId)) &&
                    IsActiveInHierarchy(
                        rendererTransformId,
                        rootTransformIds,
                        treatSelectedRootsAsActive))
                .Select(CreateStaticRendererRecord)
                .Where(candidate =>
                    !string.IsNullOrWhiteSpace(candidate.MeshGuid))
                .GroupBy(candidate => candidate.ComponentId)
                .Select(group => group.Single())
                .OrderBy(candidate => candidate.ComponentId)
                .ToArray();
        }

        public IReadOnlyList<DonorStaticRendererRecord>
            GetStaticRenderersBelowIncludingInactive(long rootTransformId)
        {
            RequireTransform(rootTransformId);
            return staticRendererSources
                .Where(candidate =>
                    candidate.Enabled &&
                    transformByGameObject.TryGetValue(
                        candidate.GameObjectId,
                        out long rendererTransformId) &&
                    IsDescendantOrSelf(rendererTransformId, rootTransformId))
                .Select(CreateStaticRendererRecord)
                .Where(candidate =>
                    !string.IsNullOrWhiteSpace(candidate.MeshGuid))
                .GroupBy(candidate => candidate.ComponentId)
                .Select(group => group.Single())
                .OrderBy(candidate => candidate.ComponentId)
                .ToArray();
        }

        public IReadOnlyList<DonorSkinnedRendererRecord>
            GetActiveSkinnedRenderersBelow(
                IReadOnlyList<long> rootTransformIds,
                bool treatSelectedRootsAsActive = false)
        {
            if (rootTransformIds == null || rootTransformIds.Count == 0)
            {
                return Array.Empty<DonorSkinnedRendererRecord>();
            }

            foreach (long transformId in rootTransformIds)
            {
                RequireTransform(transformId);
            }

            return renderers
                .Where(candidate =>
                    candidate.Enabled &&
                    transformByGameObject.TryGetValue(
                        candidate.GameObjectId,
                        out long rendererTransformId) &&
                    rootTransformIds.Any(rootId =>
                        IsDescendantOrSelf(rendererTransformId, rootId)) &&
                    IsActiveInHierarchy(
                        rendererTransformId,
                        rootTransformIds,
                        treatSelectedRootsAsActive))
                .GroupBy(candidate => candidate.ComponentId)
                .Select(group => group.Single())
                .OrderBy(candidate => candidate.ComponentId)
                .ToArray();
        }

        public DonorSkinnedRendererRecord GetSkinnedRenderer(
            long rendererComponentId)
        {
            DonorSkinnedRendererRecord renderer = renderers.SingleOrDefault(
                candidate => candidate.ComponentId == rendererComponentId);
            return renderer ?? throw new InvalidOperationException(
                $"Donor skinned renderer {rendererComponentId} was not found.");
        }

        public DonorStaticRendererRecord GetStaticRenderer(
            long rendererComponentId)
        {
            DonorStaticRendererSource renderer = staticRendererSources
                .SingleOrDefault(candidate =>
                    candidate.ComponentId == rendererComponentId);
            return renderer != null
                ? CreateStaticRendererRecord(renderer)
                : throw new InvalidOperationException(
                    $"Donor static renderer {rendererComponentId} was not found.");
        }

        public void GetStaticRendererTransformRelativeTo(
            long rendererComponentId,
            long referenceRootTransformId,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Vector3 localScale)
        {
            DonorStaticRendererRecord renderer = GetStaticRenderer(
                rendererComponentId);
            if (!transformByGameObject.TryGetValue(
                    renderer.GameObjectId,
                    out long rendererTransformId))
            {
                throw new InvalidOperationException(
                    $"Donor static renderer {rendererComponentId} has no Transform.");
            }

            Matrix4x4 referenceWorld = GetWorldMatrix(
                referenceRootTransformId,
                new HashSet<long>());
            Matrix4x4 rendererWorld = GetWorldMatrix(
                rendererTransformId,
                new HashSet<long>());
            Matrix4x4 relative = referenceWorld.inverse * rendererWorld;
            localPosition = relative.MultiplyPoint3x4(Vector3.zero);
            localRotation = relative.rotation;
            localScale = relative.lossyScale;
        }

        private DonorStaticRendererRecord CreateStaticRendererRecord(
            DonorStaticRendererSource candidate) =>
            new DonorStaticRendererRecord(
                candidate.ComponentId,
                candidate.GameObjectId,
                meshGuidByGameObject.TryGetValue(
                    candidate.GameObjectId,
                    out string meshGuid)
                        ? meshGuid
                        : string.Empty,
                candidate.MaterialGuids);

        private bool IsActiveInHierarchy(
            long transformId,
            IReadOnlyList<long> allowedRoots,
            bool treatSelectedRootsAsActive = false)
        {
            long current = transformId;
            var visited = new HashSet<long>();
            while (current != 0 && visited.Add(current))
            {
                DonorTransformRecord transform = RequireTransform(current);
                if (treatSelectedRootsAsActive &&
                    allowedRoots.Contains(current))
                {
                    return true;
                }

                if (!gameObjects.TryGetValue(
                        transform.GameObjectId,
                        out DonorGameObjectRecord gameObject) ||
                    !gameObject.ActiveSelf)
                {
                    return false;
                }

                if (allowedRoots.Contains(current))
                {
                    return true;
                }

                current = transform.FatherTransformId;
            }

            return false;
        }

        public string GetGameObjectName(long gameObjectId)
        {
            return gameObjects.TryGetValue(
                gameObjectId,
                out DonorGameObjectRecord record)
                ? record.Name
                : $"DonorObject_{gameObjectId}";
        }

        public int GetGameObjectLayer(long gameObjectId)
        {
            return gameObjects.TryGetValue(
                gameObjectId,
                out DonorGameObjectRecord record)
                ? record.Layer
                : 0;
        }

        public DonorTransformRecord GetTransform(long transformId)
        {
            return RequireTransform(transformId);
        }

        public long GetTransformIdForGameObject(long gameObjectId)
        {
            return transformByGameObject.TryGetValue(
                gameObjectId,
                out long transformId)
                ? transformId
                : throw new InvalidOperationException(
                    $"Donor GameObject {gameObjectId} has no Transform.");
        }

        public string GetHierarchyPath(long transformId)
        {
            var segments = new Stack<string>();
            long current = transformId;
            var visited = new HashSet<long>();
            while (current != 0L && visited.Add(current))
            {
                DonorTransformRecord transform = RequireTransform(current);
                segments.Push(GetGameObjectName(transform.GameObjectId));
                current = transform.FatherTransformId;
            }

            if (current != 0L)
            {
                throw new FormatException(
                    $"Cycle detected in donor transform ancestry at {current}.");
            }

            return string.Join("/", segments);
        }

        public void GetTransformRelativeTo(
            long transformId,
            long referenceRootTransformId,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Vector3 localScale)
        {
            Matrix4x4 referenceWorld = GetWorldMatrix(
                referenceRootTransformId,
                new HashSet<long>());
            Matrix4x4 targetWorld = GetWorldMatrix(
                transformId,
                new HashSet<long>());
            Matrix4x4 relative = referenceWorld.inverse * targetWorld;
            localPosition = relative.MultiplyPoint3x4(Vector3.zero);
            localRotation = relative.rotation;
            localScale = relative.lossyScale;
        }

        public IReadOnlyList<DonorTransformRecord> GetDirectChildren(
            long parentTransformId)
        {
            RequireTransform(parentTransformId);
            return transforms.Values
                .Where(candidate =>
                    candidate.FatherTransformId == parentTransformId)
                .OrderBy(candidate => candidate.TransformId)
                .ToArray();
        }

        public IReadOnlyList<DonorTransformRecord> GetDescendants(
            long rootTransformId,
            bool includeRoot = false)
        {
            RequireTransform(rootTransformId);
            return transforms.Values
                .Where(candidate =>
                    (includeRoot || candidate.TransformId != rootTransformId) &&
                    IsDescendantOrSelf(
                        candidate.TransformId,
                        rootTransformId))
                .OrderBy(GetDepth)
                .ThenBy(candidate => candidate.TransformId)
                .ToArray();
        }

        public int GetTransformDepth(long transformId) =>
            GetDepth(RequireTransform(transformId));

        public bool IsGameObjectActiveSelf(long transformId)
        {
            DonorTransformRecord transform = RequireTransform(transformId);
            return gameObjects.TryGetValue(
                       transform.GameObjectId,
                       out DonorGameObjectRecord gameObject) &&
                   gameObject.ActiveSelf;
        }

        public bool IsTransformActiveBelow(
            long transformId,
            long rootTransformId,
            bool treatRootAsActive = false)
        {
            RequireTransform(rootTransformId);
            if (!IsDescendantOrSelf(transformId, rootTransformId))
            {
                return false;
            }

            return IsActiveInHierarchy(
                transformId,
                new[] { rootTransformId },
                treatRootAsActive);
        }

        public bool TryGetRigidbody(
            long transformId,
            out DonorRigidbodyRecord rigidbody)
        {
            DonorTransformRecord transform = RequireTransform(transformId);
            return rigidbodiesByGameObject.TryGetValue(
                transform.GameObjectId,
                out rigidbody);
        }

        public bool TryGetRigidbodyByComponentId(
            long componentId,
            out DonorRigidbodyRecord rigidbody) =>
            rigidbodiesByComponent.TryGetValue(componentId, out rigidbody);

        public IReadOnlyList<DonorColliderRecord> GetActiveCollidersBelow(
            long rootTransformId,
            bool treatRootAsActive = false,
            bool includeTriggers = false)
        {
            RequireTransform(rootTransformId);
            var roots = new[] { rootTransformId };
            return colliders
                .Where(candidate =>
                    candidate.Enabled &&
                    (includeTriggers || !candidate.IsTrigger) &&
                    transformByGameObject.TryGetValue(
                        candidate.GameObjectId,
                        out long colliderTransformId) &&
                    IsDescendantOrSelf(colliderTransformId, rootTransformId) &&
                    IsActiveInHierarchy(
                        colliderTransformId,
                        roots,
                        treatRootAsActive))
                .OrderBy(candidate => candidate.ComponentId)
                .ToArray();
        }

        public IReadOnlyList<DonorColliderRecord>
            GetCollidersBelowIncludingInactive(
                long rootTransformId,
                bool includeTriggers = false)
        {
            RequireTransform(rootTransformId);
            return colliders
                .Where(candidate =>
                    candidate.Enabled &&
                    (includeTriggers || !candidate.IsTrigger) &&
                    transformByGameObject.TryGetValue(
                        candidate.GameObjectId,
                        out long colliderTransformId) &&
                    IsDescendantOrSelf(
                        colliderTransformId,
                        rootTransformId))
                .OrderBy(candidate => candidate.ComponentId)
                .ToArray();
        }

        public IReadOnlyList<DonorColliderRecord> GetCollidersForGameObject(
            long gameObjectId,
            bool includeDisabled = true,
            bool includeTriggers = true)
        {
            return colliders
                .Where(candidate =>
                    candidate.GameObjectId == gameObjectId &&
                    (includeDisabled || candidate.Enabled) &&
                    (includeTriggers || !candidate.IsTrigger))
                .OrderBy(candidate => candidate.ComponentId)
                .ToArray();
        }

        public DonorTransformRecord GetUniqueDirectChildByName(
            long parentTransformId,
            string childName)
        {
            string expectedName = childName?.Trim() ?? string.Empty;
            DonorTransformRecord[] matches = GetDirectChildren(parentTransformId)
                .Where(candidate => string.Equals(
                    GetGameObjectName(candidate.GameObjectId),
                    expectedName,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one direct child '{expectedName}' below transform {parentTransformId}; found {matches.Length}.");
            }

            return matches[0];
        }

        public DonorTransformRecord GetUniqueTransformByPath(
            params string[] hierarchyNames)
        {
            if (hierarchyNames == null || hierarchyNames.Length == 0 ||
                hierarchyNames.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException(
                    "A non-empty donor hierarchy path is required.",
                    nameof(hierarchyNames));
            }

            DonorTransformRecord[] candidates = transforms.Values
                .Where(candidate => candidate.FatherTransformId == 0L)
                .Where(candidate => string.Equals(
                    GetGameObjectName(candidate.GameObjectId),
                    hierarchyNames[0],
                    StringComparison.Ordinal))
                .ToArray();
            for (int index = 1; index < hierarchyNames.Length; index++)
            {
                string expectedName = hierarchyNames[index];
                var parentIds = new HashSet<long>(
                    candidates.Select(candidate => candidate.TransformId));
                candidates = transforms.Values
                    .Where(candidate =>
                        parentIds.Contains(candidate.FatherTransformId))
                    .Where(candidate => string.Equals(
                        GetGameObjectName(candidate.GameObjectId),
                        expectedName,
                        StringComparison.Ordinal))
                    .ToArray();
            }

            if (candidates.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one donor transform at " +
                    $"'{string.Join("/", hierarchyNames)}'; found " +
                    $"{candidates.Length}.");
            }

            return candidates[0];
        }

        public Vector3 GetWorldPosition(long transformId) =>
            GetWorldMatrix(transformId, new HashSet<long>())
                .MultiplyPoint3x4(Vector3.zero);

        public Quaternion GetWorldRotation(long transformId) =>
            GetWorldMatrix(transformId, new HashSet<long>()).rotation;

        public IReadOnlyList<DonorMonoBehaviourRecord> MonoBehaviours =>
            monoBehaviours;

        public IReadOnlyList<DonorHingeJointRecord> HingeJoints =>
            hingeJoints;

        public IReadOnlyList<DonorMonoBehaviourRecord> GetMonoBehaviours(
            long gameObjectId) => monoBehaviours
                .Where(value => value.GameObjectId == gameObjectId)
                .OrderBy(value => value.ComponentId)
                .ToArray();

        public bool TryGetTransformIdForGameObject(
            long gameObjectId,
            out long transformId) =>
            transformByGameObject.TryGetValue(gameObjectId, out transformId);

        private static bool IsRelevantClass(int classId)
        {
            return classId == GameObjectClassId ||
                   classId == TransformClassId ||
                   classId == MeshRendererClassId ||
                   classId == MeshFilterClassId ||
                   classId == RigidbodyClassId ||
                   classId == HingeJointClassId ||
                   classId == MeshColliderClassId ||
                   classId == BoxColliderClassId ||
                   classId == SphereColliderClassId ||
                   classId == CapsuleColliderClassId ||
                   classId == MonoBehaviourClassId ||
                   classId == SkinnedMeshRendererClassId;
        }

        private void ParseBlock(
            int classId,
            long fileId,
            string block)
        {
            if (string.IsNullOrEmpty(block))
            {
                return;
            }

            switch (classId)
            {
                case GameObjectClassId:
                    ParseGameObject(fileId, block);
                    break;

                case TransformClassId:
                    ParseTransform(fileId, block);
                    break;

                case MeshRendererClassId:
                    ParseStaticRenderer(fileId, block);
                    break;

                case MeshFilterClassId:
                    ParseMeshFilter(block);
                    break;

                case RigidbodyClassId:
                    ParseRigidbody(fileId, block);
                    break;

                case HingeJointClassId:
                    ParseHingeJoint(fileId, block);
                    break;

                case MeshColliderClassId:
                case BoxColliderClassId:
                case SphereColliderClassId:
                case CapsuleColliderClassId:
                    ParseCollider(classId, fileId, block);
                    break;

                case MonoBehaviourClassId:
                    ParseMonoBehaviour(fileId, block);
                    break;

                case SkinnedMeshRendererClassId:
                    ParseSkinnedRenderer(fileId, block);
                    break;
            }
        }

        private void ParseGameObject(long fileId, string block)
        {
            string name = ExtractScalar(block, "m_Name");
            gameObjects.Add(
                fileId,
                new DonorGameObjectRecord(
                    fileId,
                    string.IsNullOrWhiteSpace(name)
                        ? $"DonorObject_{fileId}"
                        : name,
                    ParseIntOrDefault(ExtractScalar(block, "m_Layer"), 0),
                    ExtractScalar(block, "m_IsActive") != "0"));
        }

        private void ParseTransform(long fileId, string block)
        {
            long gameObjectId = ExtractFileId(block, "m_GameObject");
            long fatherId = ExtractFileId(block, "m_Father");
            transforms.Add(
                fileId,
                new DonorTransformRecord(
                    fileId,
                    gameObjectId,
                    fatherId,
                    ExtractVector3(block, "m_LocalPosition"),
                    ExtractQuaternion(block, "m_LocalRotation"),
                    ExtractVector3(block, "m_LocalScale")));
        }

        private void ParseSkinnedRenderer(long fileId, string block)
        {
            long gameObjectId = ExtractFileId(block, "m_GameObject");
            string meshGuid = ExtractGuid(block, "m_Mesh");
            IReadOnlyList<string> materialGuids = ExtractGuidList(
                block,
                "m_Materials",
                "m_SubsetIndices");
            long rootBone = ExtractFileId(block, "m_RootBone");
            IReadOnlyList<long> bones = ExtractFileIdList(
                block,
                "m_Bones",
                "m_BlendShapeWeights");
            Vector3 center = ExtractVector3(block, "m_Center");
            Vector3 extent = ExtractVector3(block, "m_Extent");

            renderers.Add(
                new DonorSkinnedRendererRecord(
                    fileId,
                    gameObjectId,
                    ExtractScalar(block, "m_Enabled") != "0",
                    meshGuid,
                    materialGuids,
                    rootBone,
                    bones,
                    new Bounds(center, extent * 2f)));
        }

        private void ParseStaticRenderer(long fileId, string block)
        {
            staticRendererSources.Add(new DonorStaticRendererSource(
                fileId,
                ExtractFileId(block, "m_GameObject"),
                ExtractScalar(block, "m_Enabled") != "0",
                ExtractGuidList(
                    block,
                    "m_Materials",
                    "m_SubsetIndices")));
        }

        private void ParseMeshFilter(string block)
        {
            long gameObjectId = ExtractFileId(block, "m_GameObject");
            meshGuidByGameObject.Add(
                gameObjectId,
                ExtractGuid(block, "m_Mesh"));
        }

        private void ParseRigidbody(long fileId, string block)
        {
            long gameObjectId = ExtractFileId(block, "m_GameObject");
            var record = new DonorRigidbodyRecord(
                    fileId,
                    gameObjectId,
                    ParseFloatOrDefault(ExtractScalar(block, "m_Mass"), 1f),
                    ParseFloatOrDefault(ExtractScalar(block, "m_Drag"), 0f),
                    ParseFloatOrDefault(
                        ExtractScalar(block, "m_AngularDrag"),
                        0.05f),
                    ExtractScalar(block, "m_UseGravity") != "0",
                    ExtractScalar(block, "m_IsKinematic") != "0");
            rigidbodiesByGameObject.Add(gameObjectId, record);
            rigidbodiesByComponent.Add(fileId, record);
        }

        private void ParseHingeJoint(long fileId, string block)
        {
            hingeJoints.Add(new DonorHingeJointRecord(
                fileId,
                ExtractFileId(block, "m_GameObject"),
                ExtractFileId(block, "m_ConnectedBody"),
                ExtractVector3(block, "m_Anchor"),
                ExtractVector3(block, "m_Axis"),
                ExtractScalar(block, "m_AutoConfigureConnectedAnchor") != "0",
                ExtractVector3(block, "m_ConnectedAnchor"),
                ExtractScalar(block, "m_UseSpring") != "0",
                ExtractNestedFloat(block, "m_Spring", "spring"),
                ExtractNestedFloat(block, "m_Spring", "damper"),
                ExtractNestedFloat(block, "m_Spring", "targetPosition"),
                ExtractScalar(block, "m_UseLimits") != "0",
                ExtractNestedFloat(block, "m_Limits", "min"),
                ExtractNestedFloat(block, "m_Limits", "max"),
                ParseFloatOrInfinity(ExtractScalar(block, "m_BreakForce")),
                ParseFloatOrInfinity(ExtractScalar(block, "m_BreakTorque")),
                ExtractScalar(block, "m_EnableCollision") != "0"));
        }

        private void ParseCollider(int classId, long fileId, string block)
        {
            DonorColliderKind kind = classId switch
            {
                MeshColliderClassId => DonorColliderKind.Mesh,
                BoxColliderClassId => DonorColliderKind.Box,
                SphereColliderClassId => DonorColliderKind.Sphere,
                CapsuleColliderClassId => DonorColliderKind.Capsule,
                _ => throw new ArgumentOutOfRangeException(nameof(classId)),
            };
            colliders.Add(new DonorColliderRecord(
                fileId,
                ExtractFileId(block, "m_GameObject"),
                kind,
                ExtractScalar(block, "m_Enabled") != "0",
                ExtractScalar(block, "m_IsTrigger") != "0",
                ExtractScalar(block, "m_Convex") != "0",
                ExtractGuid(block, "m_Mesh"),
                ExtractVector3(block, "m_Center"),
                ExtractVector3(block, "m_Size"),
                ParseFloatOrDefault(ExtractScalar(block, "m_Radius"), 0f),
                ParseFloatOrDefault(ExtractScalar(block, "m_Height"), 0f),
                (int)ParseFloatOrDefault(
                    ExtractScalar(block, "m_Direction"),
                    1f)));
        }

        private void ParseMonoBehaviour(long fileId, string block)
        {
            long gameObjectId = ExtractFileId(block, "m_GameObject");
            if (gameObjectId == 0L)
            {
                return;
            }

            monoBehaviours.Add(new DonorMonoBehaviourRecord(
                fileId,
                gameObjectId,
                ExtractGuid(block, "m_Script"),
                block));
        }

        private void BuildIndices()
        {
            foreach (DonorTransformRecord transform in transforms.Values)
            {
                if (transformByGameObject.ContainsKey(transform.GameObjectId))
                {
                    throw new FormatException(
                        $"Donor GameObject {transform.GameObjectId} has more " +
                        "than one Transform record.");
                }

                transformByGameObject.Add(
                    transform.GameObjectId,
                    transform.TransformId);
            }
        }

        private DonorTransformRecord RequireTransform(long transformId)
        {
            if (!transforms.TryGetValue(
                    transformId,
                    out DonorTransformRecord record))
            {
                throw new FormatException(
                    $"Donor transform {transformId} was not found.");
            }

            return record;
        }

        private Matrix4x4 GetWorldMatrix(
            long transformId,
            ISet<long> visited)
        {
            if (!visited.Add(transformId))
            {
                throw new FormatException(
                    $"Cycle detected in donor transform ancestry at {transformId}.");
            }

            DonorTransformRecord transform = RequireTransform(transformId);
            Matrix4x4 local = Matrix4x4.TRS(
                transform.LocalPosition,
                transform.LocalRotation,
                transform.LocalScale);
            return transform.FatherTransformId == 0
                ? local
                : GetWorldMatrix(transform.FatherTransformId, visited) * local;
        }

        private void AddAncestors(
            long transformId,
            long actionRootTransformId,
            ISet<long> destination)
        {
            long current = transformId;
            var visited = new HashSet<long>();
            while (current != 0)
            {
                if (!visited.Add(current))
                {
                    throw new FormatException(
                        $"Cycle detected in donor transform ancestry at {current}.");
                }

                DonorTransformRecord record = RequireTransform(current);
                destination.Add(current);
                if (current == actionRootTransformId)
                {
                    return;
                }

                current = record.FatherTransformId;
            }

            throw new InvalidOperationException(
                $"Transform {transformId} is not below action root " +
                $"{actionRootTransformId}.");
        }

        private bool IsDescendantOrSelf(
            long transformId,
            long ancestorTransformId)
        {
            long current = transformId;
            var visited = new HashSet<long>();
            while (current != 0 && visited.Add(current))
            {
                if (current == ancestorTransformId)
                {
                    return true;
                }

                current = RequireTransform(current).FatherTransformId;
            }

            return false;
        }

        private int GetDepth(DonorTransformRecord record)
        {
            int depth = 0;
            long current = record.FatherTransformId;
            var visited = new HashSet<long>();
            while (current != 0 && visited.Add(current))
            {
                depth++;
                current = RequireTransform(current).FatherTransformId;
            }

            return depth;
        }

        private static string ExtractScalar(string block, string field)
        {
            Match match = Regex.Match(
                block,
                @"^\s*" + Regex.Escape(field) + @":\s*(?<value>.*)$",
                RegexOptions.Multiline | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return string.Empty;
            }

            string value = match.Groups["value"].Value.Trim();
            if (value.Length >= 2 &&
                value[0] == '"' &&
                value[value.Length - 1] == '"')
            {
                value = value.Substring(1, value.Length - 2)
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");
            }

            return value;
        }

        private static long ExtractFileId(string block, string field)
        {
            Match match = Regex.Match(
                block,
                @"^\s*" + Regex.Escape(field) +
                @":\s*\{fileID:\s*(?<value>-?\d+)",
                RegexOptions.Multiline | RegexOptions.CultureInvariant);
            return match.Success
                ? long.Parse(
                    match.Groups["value"].Value,
                    CultureInfo.InvariantCulture)
                : 0L;
        }

        private static string ExtractGuid(string block, string field)
        {
            Match match = Regex.Match(
                block,
                @"^\s*" + Regex.Escape(field) +
                @":\s*\{fileID:\s*-?\d+,\s*guid:\s*(?<value>[0-9a-fA-F]+)",
                RegexOptions.Multiline | RegexOptions.CultureInvariant);
            return match.Success
                ? match.Groups["value"].Value.ToLowerInvariant()
                : string.Empty;
        }

        private static IReadOnlyList<long> ExtractFileIdList(
            string block,
            string startField,
            string endField)
        {
            int start = block.IndexOf(
                "  " + startField + ":",
                StringComparison.Ordinal);
            if (start < 0)
            {
                return Array.Empty<long>();
            }

            int end = block.IndexOf(
                "  " + endField + ":",
                start,
                StringComparison.Ordinal);
            string section = end >= 0
                ? block.Substring(start, end - start)
                : block.Substring(start);
            MatchCollection matches = Regex.Matches(
                section,
                @"-\s*\{fileID:\s*(?<value>-?\d+)",
                RegexOptions.CultureInvariant);
            var values = new long[matches.Count];
            for (int index = 0; index < matches.Count; index++)
            {
                values[index] = long.Parse(
                    matches[index].Groups["value"].Value,
                    CultureInfo.InvariantCulture);
            }

            return values;
        }

        private static IReadOnlyList<string> ExtractGuidList(
            string block,
            string startField,
            string endField)
        {
            int start = block.IndexOf(
                "  " + startField + ":",
                StringComparison.Ordinal);
            if (start < 0)
            {
                return Array.Empty<string>();
            }

            int end = block.IndexOf(
                "  " + endField + ":",
                start,
                StringComparison.Ordinal);
            string section = end >= 0
                ? block.Substring(start, end - start)
                : block.Substring(start);
            return Regex.Matches(
                    section,
                    @"guid:\s*(?<value>[0-9a-fA-F]+)",
                    RegexOptions.CultureInvariant)
                .Cast<Match>()
                .Select(match =>
                    match.Groups["value"].Value.ToLowerInvariant())
                .ToArray();
        }

        private static Vector3 ExtractVector3(
            string block,
            string field)
        {
            Match match = Regex.Match(
                block,
                @"^\s*" + Regex.Escape(field) +
                @":\s*\{x:\s*(?<x>[^,]+),\s*y:\s*(?<y>[^,]+),\s*z:\s*(?<z>[^}]+)\}",
                RegexOptions.Multiline | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return Vector3.zero;
            }

            return new Vector3(
                ParseFloat(match.Groups["x"].Value),
                ParseFloat(match.Groups["y"].Value),
                ParseFloat(match.Groups["z"].Value));
        }

        private static Quaternion ExtractQuaternion(
            string block,
            string field)
        {
            Match match = Regex.Match(
                block,
                @"^\s*" + Regex.Escape(field) +
                @":\s*\{x:\s*(?<x>[^,]+),\s*y:\s*(?<y>[^,]+),\s*z:\s*(?<z>[^,]+),\s*w:\s*(?<w>[^}]+)\}",
                RegexOptions.Multiline | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return Quaternion.identity;
            }

            return new Quaternion(
                ParseFloat(match.Groups["x"].Value),
                ParseFloat(match.Groups["y"].Value),
                ParseFloat(match.Groups["z"].Value),
                ParseFloat(match.Groups["w"].Value));
        }

        private static float ParseFloat(string value)
        {
            return float.Parse(
                value.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture);
        }

        private static float ParseFloatOrDefault(
            string value,
            float fallback) =>
            float.TryParse(
                value?.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float parsed)
                ? parsed
                : fallback;

        private static int ParseIntOrDefault(
            string value,
            int fallback) =>
            int.TryParse(
                value?.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int parsed)
                ? parsed
                : fallback;

        private static float ExtractNestedFloat(
            string block,
            string containerField,
            string valueField)
        {
            Match match = Regex.Match(
                block,
                @"^\s*" + Regex.Escape(containerField) +
                @":\s*$[\s\S]*?^\s+" + Regex.Escape(valueField) +
                @":\s*(?<value>\S+)\s*$",
                RegexOptions.Multiline | RegexOptions.CultureInvariant);
            return match.Success
                ? ParseFloatOrDefault(match.Groups["value"].Value, 0f)
                : 0f;
        }

        private static float ParseFloatOrInfinity(string value)
        {
            if (string.Equals(
                    value?.Trim(),
                    "Infinity",
                    StringComparison.OrdinalIgnoreCase))
            {
                return float.PositiveInfinity;
            }

            if (string.Equals(
                    value?.Trim(),
                    "-Infinity",
                    StringComparison.OrdinalIgnoreCase))
            {
                return float.NegativeInfinity;
            }

            return ParseFloatOrDefault(value, 0f);
        }
    }

    internal sealed class DonorActionSlice
    {
        public DonorActionSlice(
            long rootTransformId,
            long animationTargetTransformId,
            IReadOnlyList<DonorSkinnedRendererRecord> renderers,
            IReadOnlyList<DonorStaticRendererRecord> staticRenderers,
            IReadOnlyList<DonorTransformRecord> transforms)
        {
            RootTransformId = rootTransformId;
            AnimationTargetTransformId = animationTargetTransformId;
            Renderers = renderers ??
                throw new ArgumentNullException(nameof(renderers));
            StaticRenderers = staticRenderers ??
                throw new ArgumentNullException(nameof(staticRenderers));
            Transforms = transforms ??
                throw new ArgumentNullException(nameof(transforms));
        }

        public long RootTransformId { get; }

        public long AnimationTargetTransformId { get; }

        public IReadOnlyList<DonorSkinnedRendererRecord> Renderers { get; }

        public IReadOnlyList<DonorStaticRendererRecord> StaticRenderers { get; }

        public IReadOnlyList<DonorTransformRecord> Transforms { get; }
    }

    internal sealed class DonorGameObjectRecord
    {
        public DonorGameObjectRecord(
            long gameObjectId,
            string name,
            int layer,
            bool activeSelf)
        {
            GameObjectId = gameObjectId;
            Name = name ?? string.Empty;
            Layer = layer;
            ActiveSelf = activeSelf;
        }

        public long GameObjectId { get; }

        public string Name { get; }

        public int Layer { get; }

        public bool ActiveSelf { get; }
    }

    internal sealed class DonorTransformRecord
    {
        public DonorTransformRecord(
            long transformId,
            long gameObjectId,
            long fatherTransformId,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            TransformId = transformId;
            GameObjectId = gameObjectId;
            FatherTransformId = fatherTransformId;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
        }

        public long TransformId { get; }

        public long GameObjectId { get; }

        public long FatherTransformId { get; }

        public Vector3 LocalPosition { get; }

        public Quaternion LocalRotation { get; }

        public Vector3 LocalScale { get; }
    }

    internal sealed class DonorSkinnedRendererRecord
    {
        public DonorSkinnedRendererRecord(
            long componentId,
            long gameObjectId,
            bool enabled,
            string meshGuid,
            IReadOnlyList<string> materialGuids,
            long rootBoneTransformId,
            IReadOnlyList<long> boneTransformIds,
            Bounds localBounds)
        {
            ComponentId = componentId;
            GameObjectId = gameObjectId;
            Enabled = enabled;
            MeshGuid = meshGuid ?? string.Empty;
            MaterialGuids = materialGuids ??
                throw new ArgumentNullException(nameof(materialGuids));
            RootBoneTransformId = rootBoneTransformId;
            BoneTransformIds = boneTransformIds ??
                throw new ArgumentNullException(nameof(boneTransformIds));
            LocalBounds = localBounds;
        }

        public long ComponentId { get; }

        public long GameObjectId { get; }

        public bool Enabled { get; }

        public string MeshGuid { get; }

        public IReadOnlyList<string> MaterialGuids { get; }

        public long RootBoneTransformId { get; }

        public IReadOnlyList<long> BoneTransformIds { get; }

        public Bounds LocalBounds { get; }
    }

    internal sealed class DonorStaticRendererRecord
    {
        public DonorStaticRendererRecord(
            long componentId,
            long gameObjectId,
            string meshGuid,
            IReadOnlyList<string> materialGuids)
        {
            ComponentId = componentId;
            GameObjectId = gameObjectId;
            MeshGuid = meshGuid ?? string.Empty;
            MaterialGuids = materialGuids ??
                throw new ArgumentNullException(nameof(materialGuids));
        }

        public long ComponentId { get; }

        public long GameObjectId { get; }

        public string MeshGuid { get; }

        public IReadOnlyList<string> MaterialGuids { get; }
    }

    internal sealed class DonorStaticRendererSource
    {
        public DonorStaticRendererSource(
            long componentId,
            long gameObjectId,
            bool enabled,
            IReadOnlyList<string> materialGuids)
        {
            ComponentId = componentId;
            GameObjectId = gameObjectId;
            Enabled = enabled;
            MaterialGuids = materialGuids ??
                throw new ArgumentNullException(nameof(materialGuids));
        }

        public long ComponentId { get; }

        public long GameObjectId { get; }

        public bool Enabled { get; }

        public IReadOnlyList<string> MaterialGuids { get; }
    }

    internal sealed class DonorMonoBehaviourRecord
    {
        public DonorMonoBehaviourRecord(
            long componentId,
            long gameObjectId,
            string scriptGuid,
            string serializedBody)
        {
            ComponentId = componentId;
            GameObjectId = gameObjectId;
            ScriptGuid = scriptGuid ?? string.Empty;
            SerializedBody = serializedBody ?? string.Empty;
        }

        public long ComponentId { get; }
        public long GameObjectId { get; }
        public string ScriptGuid { get; }
        public string SerializedBody { get; }
    }

    internal sealed class DonorRigidbodyRecord
    {
        public DonorRigidbodyRecord(
            long componentId,
            long gameObjectId,
            float massKilograms,
            float linearDamping,
            float angularDamping,
            bool useGravity,
            bool isKinematic)
        {
            ComponentId = componentId;
            GameObjectId = gameObjectId;
            MassKilograms = massKilograms;
            LinearDamping = linearDamping;
            AngularDamping = angularDamping;
            UseGravity = useGravity;
            IsKinematic = isKinematic;
        }

        public long ComponentId { get; }
        public long GameObjectId { get; }
        public float MassKilograms { get; }
        public float LinearDamping { get; }
        public float AngularDamping { get; }
        public bool UseGravity { get; }
        public bool IsKinematic { get; }
    }

    internal sealed class DonorHingeJointRecord
    {
        public DonorHingeJointRecord(
            long componentId,
            long gameObjectId,
            long connectedBodyComponentId,
            Vector3 anchor,
            Vector3 axis,
            bool autoConfigureConnectedAnchor,
            Vector3 connectedAnchor,
            bool useSpring,
            float spring,
            float damper,
            float targetPosition,
            bool useLimits,
            float minimumLimitDegrees,
            float maximumLimitDegrees,
            float breakForce,
            float breakTorque,
            bool enableCollision)
        {
            ComponentId = componentId;
            GameObjectId = gameObjectId;
            ConnectedBodyComponentId = connectedBodyComponentId;
            Anchor = anchor;
            Axis = axis;
            AutoConfigureConnectedAnchor = autoConfigureConnectedAnchor;
            ConnectedAnchor = connectedAnchor;
            UseSpring = useSpring;
            Spring = spring;
            Damper = damper;
            TargetPosition = targetPosition;
            UseLimits = useLimits;
            MinimumLimitDegrees = minimumLimitDegrees;
            MaximumLimitDegrees = maximumLimitDegrees;
            BreakForce = breakForce;
            BreakTorque = breakTorque;
            EnableCollision = enableCollision;
        }

        public long ComponentId { get; }
        public long GameObjectId { get; }
        public long ConnectedBodyComponentId { get; }
        public Vector3 Anchor { get; }
        public Vector3 Axis { get; }
        public bool AutoConfigureConnectedAnchor { get; }
        public Vector3 ConnectedAnchor { get; }
        public bool UseSpring { get; }
        public float Spring { get; }
        public float Damper { get; }
        public float TargetPosition { get; }
        public bool UseLimits { get; }
        public float MinimumLimitDegrees { get; }
        public float MaximumLimitDegrees { get; }
        public float BreakForce { get; }
        public float BreakTorque { get; }
        public bool EnableCollision { get; }
    }

    internal enum DonorColliderKind
    {
        Mesh = 0,
        Box = 1,
        Sphere = 2,
        Capsule = 3,
    }

    internal sealed class DonorColliderRecord
    {
        public DonorColliderRecord(
            long componentId,
            long gameObjectId,
            DonorColliderKind kind,
            bool enabled,
            bool isTrigger,
            bool convex,
            string meshGuid,
            Vector3 center,
            Vector3 size,
            float radius,
            float height,
            int direction)
        {
            ComponentId = componentId;
            GameObjectId = gameObjectId;
            Kind = kind;
            Enabled = enabled;
            IsTrigger = isTrigger;
            Convex = convex;
            MeshGuid = meshGuid ?? string.Empty;
            Center = center;
            Size = size;
            Radius = radius;
            Height = height;
            Direction = direction;
        }

        public long ComponentId { get; }
        public long GameObjectId { get; }
        public DonorColliderKind Kind { get; }
        public bool Enabled { get; }
        public bool IsTrigger { get; }
        public bool Convex { get; }
        public string MeshGuid { get; }
        public Vector3 Center { get; }
        public Vector3 Size { get; }
        public float Radius { get; }
        public float Height { get; }
        public int Direction { get; }
    }
}
