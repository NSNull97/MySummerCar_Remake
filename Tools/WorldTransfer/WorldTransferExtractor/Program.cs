using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class Program
{
    private const string GeneratorId = "msc-world-transfer-extractor";
    private const string GeneratorVersion = "1.0.0";
    private const string BuiltInGuid = "0000000000000000e000000000000000";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static int Main(string[] args)
    {
        try
        {
            Options options = Options.Parse(args);
            Directory.CreateDirectory(options.ExternalOutput);
            Directory.CreateDirectory(options.ProjectOutput);

            SceneData scene = ParseScene(options.ScenePath);
            Dictionary<string, MeshInfo> meshes = ParseMeshes(options.MeshRoot);
            BuildDerivedData(scene, meshes, options);
            WriteOutputs(scene, meshes, options);

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                generator = $"{GeneratorId}/{GeneratorVersion}",
                sceneObjects = scene.GameObjects.Count,
                transforms = scene.Transforms.Count,
                geometryEntities = scene.Entities.Count,
                meshesReferenced = scene.Entities.Select(e => e.MeshGuid).Where(g => !string.IsNullOrEmpty(g)).Distinct().Count(),
                colliders = scene.Colliders.Count,
                missingReferences = scene.MissingReferences.Count,
                unsupportedClassIds = scene.UnsupportedTypeCounts.Count,
                cells = scene.Cells.Count,
                landmarks = scene.Landmarks.Count,
                sourceBoundsMin = Vector(scene.SourceBoundsMin),
                sourceBoundsMax = Vector(scene.SourceBoundsMax),
                convertedBoundsMin = Vector(scene.ConvertedBoundsMin),
                convertedBoundsMax = Vector(scene.ConvertedBoundsMax)
            }, JsonOptions));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static SceneData ParseScene(string path)
    {
        var data = new SceneData();
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1 << 20);
        Block? block = null;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            Match header = HeaderRegex().Match(line);
            if (header.Success)
            {
                if (block != null)
                {
                    FinalizeBlock(block, data);
                }

                block = new Block(int.Parse(header.Groups[1].Value, Invariant), long.Parse(header.Groups[2].Value, Invariant));
                data.SerializedTypeCounts[block.ClassId] = data.SerializedTypeCounts.GetValueOrDefault(block.ClassId) + 1;
                continue;
            }

            if (block == null)
            {
                continue;
            }

            ParseBlockLine(block, line);
        }

        if (block != null)
        {
            FinalizeBlock(block, data);
        }

        return data;
    }

    private static void ParseBlockLine(Block block, string line)
    {
        string trimmed = line.Trim();
        if (trimmed.StartsWith("m_GameObject:", StringComparison.Ordinal))
        {
            block.GameObjectId = ParseFileId(trimmed);
        }
        else if (trimmed.StartsWith("m_Name:", StringComparison.Ordinal) && block.ClassId == 1)
        {
            block.Name = Unquote(ValueAfterColon(trimmed));
        }
        else if (trimmed.StartsWith("m_Layer:", StringComparison.Ordinal) && block.ClassId == 1)
        {
            block.Layer = ParseInt(ValueAfterColon(trimmed));
        }
        else if (trimmed.StartsWith("m_StaticEditorFlags:", StringComparison.Ordinal) && block.ClassId == 1)
        {
            block.StaticFlags = ParseLong(ValueAfterColon(trimmed));
        }
        else if (trimmed.StartsWith("m_IsActive:", StringComparison.Ordinal) && block.ClassId == 1)
        {
            block.Active = ParseInt(ValueAfterColon(trimmed)) != 0;
        }
        else if (trimmed.StartsWith("m_LocalPosition:", StringComparison.Ordinal) && block.ClassId == 4)
        {
            block.Position = ParseVector3(trimmed);
        }
        else if (trimmed.StartsWith("m_LocalRotation:", StringComparison.Ordinal) && block.ClassId == 4)
        {
            block.Rotation = ParseQuaternion(trimmed);
        }
        else if (trimmed.StartsWith("m_LocalScale:", StringComparison.Ordinal) && block.ClassId == 4)
        {
            block.Scale = ParseVector3(trimmed);
        }
        else if (trimmed.StartsWith("m_Father:", StringComparison.Ordinal) && block.ClassId == 4)
        {
            block.FatherId = ParseFileId(trimmed);
        }
        else if (trimmed.StartsWith("m_Mesh:", StringComparison.Ordinal) && block.ClassId is 33 or 64 or 137)
        {
            (block.MeshFileId, block.MeshGuid) = ParseReference(trimmed);
        }
        else if (trimmed.StartsWith("m_Enabled:", StringComparison.Ordinal))
        {
            block.Enabled = ParseInt(ValueAfterColon(trimmed)) != 0;
        }
        else if (trimmed.StartsWith("m_IsTrigger:", StringComparison.Ordinal))
        {
            block.Trigger = ParseInt(ValueAfterColon(trimmed)) != 0;
        }
        else if (trimmed.StartsWith("m_Convex:", StringComparison.Ordinal))
        {
            block.Convex = ParseInt(ValueAfterColon(trimmed)) != 0;
        }
        else if (trimmed.StartsWith("m_Size:", StringComparison.Ordinal))
        {
            block.Size = ParseVector3(trimmed);
        }
        else if (trimmed.StartsWith("m_Center:", StringComparison.Ordinal))
        {
            block.Center = ParseVector3(trimmed);
        }
        else if (trimmed.StartsWith("m_Radius:", StringComparison.Ordinal))
        {
            block.Radius = ParseFloat(ValueAfterColon(trimmed));
        }
        else if (trimmed.StartsWith("m_Height:", StringComparison.Ordinal))
        {
            block.Height = ParseFloat(ValueAfterColon(trimmed));
        }
        else if (trimmed.StartsWith("m_Direction:", StringComparison.Ordinal))
        {
            block.Direction = ParseInt(ValueAfterColon(trimmed));
        }
        else if ((block.ClassId is 23 or 137) && trimmed.StartsWith("- {fileID:", StringComparison.Ordinal))
        {
            (_, string guid) = ParseReference(trimmed);
            if (!string.IsNullOrEmpty(guid))
            {
                block.MaterialGuids.Add(guid);
            }
        }
    }

    private static void FinalizeBlock(Block block, SceneData data)
    {
        switch (block.ClassId)
        {
            case 1:
                data.GameObjects[block.Id] = new GameObjectInfo(block.Id, block.Name, block.Layer, block.StaticFlags, block.Active);
                break;
            case 4:
                data.Transforms[block.Id] = new TransformInfo(block.Id, block.GameObjectId, block.FatherId, block.Position, block.Rotation, block.Scale);
                data.TransformByGameObject[block.GameObjectId] = block.Id;
                break;
            case 33:
                data.MeshByGameObject[block.GameObjectId] = new MeshReference(block.MeshFileId, block.MeshGuid);
                break;
            case 23:
                data.RenderersByGameObject[block.GameObjectId] = new RendererInfo(block.Enabled, block.MaterialGuids);
                break;
            case 137:
                data.RenderersByGameObject[block.GameObjectId] = new RendererInfo(block.Enabled, block.MaterialGuids);
                data.MeshByGameObject[block.GameObjectId] = new MeshReference(block.MeshFileId, block.MeshGuid);
                break;
            case 64:
            case 65:
            case 135:
            case 136:
                data.Colliders.Add(new ColliderInfo(block.Id, block.ClassId, block.GameObjectId, block.Enabled, block.Trigger,
                    block.Convex, block.MeshGuid, block.MeshFileId, block.Center, block.Size, block.Radius, block.Height, block.Direction));
                break;
        }

        if (block.GameObjectId != 0 && block.ClassId != 1)
        {
            if (!data.ComponentTypesByGameObject.TryGetValue(block.GameObjectId, out List<int>? types))
            {
                types = new List<int>();
                data.ComponentTypesByGameObject.Add(block.GameObjectId, types);
            }

            types.Add(block.ClassId);
        }
    }

    private static Dictionary<string, MeshInfo> ParseMeshes(string root)
    {
        var result = new Dictionary<string, MeshInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (string metaPath in Directory.EnumerateFiles(root, "*.asset.meta", SearchOption.AllDirectories))
        {
            string guid = string.Empty;
            using (var metaReader = new StreamReader(metaPath))
            {
                string? line;
                while ((line = metaReader.ReadLine()) != null)
                {
                    if (line.StartsWith("guid: ", StringComparison.Ordinal))
                    {
                        guid = line[6..].Trim();
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(guid))
            {
                continue;
            }

            string assetPath = metaPath[..^5];
            string relativePath = Path.GetRelativePath(Directory.GetParent(root)!.FullName, assetPath).Replace('\\', '/');
            var mesh = new MeshInfo(guid, relativePath, Path.GetFileNameWithoutExtension(assetPath));
            ParseMeshHeader(assetPath, mesh);
            result[guid] = mesh;
        }

        return result;
    }

    private static void ParseMeshHeader(string path, MeshInfo mesh)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, true, 1 << 16);
        bool inAabb = false;
        Vector3? center = null;
        Vector3? extent = null;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("m_Name:", StringComparison.Ordinal))
            {
                mesh.Name = Unquote(ValueAfterColon(trimmed));
            }
            else if (trimmed.StartsWith("indexCount:", StringComparison.Ordinal))
            {
                mesh.IndexCount += ParseInt(ValueAfterColon(trimmed));
            }
            else if (trimmed.StartsWith("vertexCount:", StringComparison.Ordinal))
            {
                mesh.VertexCount += ParseInt(ValueAfterColon(trimmed));
            }
            else if (trimmed == "localAABB:")
            {
                inAabb = true;
                center = null;
                extent = null;
            }
            else if (inAabb && trimmed.StartsWith("m_Center:", StringComparison.Ordinal))
            {
                center = ParseVector3(trimmed);
            }
            else if (inAabb && trimmed.StartsWith("m_Extent:", StringComparison.Ordinal))
            {
                extent = ParseVector3(trimmed);
                if (center.HasValue)
                {
                    mesh.IncludeBounds(center.Value - extent.Value, center.Value + extent.Value);
                }

                inAabb = false;
            }
            else if (trimmed.StartsWith("m_IndexBuffer:", StringComparison.Ordinal))
            {
                break;
            }
        }
    }

    private static void BuildDerivedData(SceneData data, Dictionary<string, MeshInfo> meshes, Options options)
    {
        var worldCache = new Dictionary<long, WorldTransform>();
        var pathCache = new Dictionary<long, string>();
        Dictionary<long, List<ColliderInfo>> collidersByObject = data.Colliders.GroupBy(c => c.GameObjectId).ToDictionary(g => g.Key, g => g.ToList());

        foreach ((long gameObjectId, GameObjectInfo gameObject) in data.GameObjects.OrderBy(pair => pair.Key))
        {
            if (!data.TransformByGameObject.TryGetValue(gameObjectId, out long transformId) ||
                !data.Transforms.TryGetValue(transformId, out TransformInfo? transform))
            {
                continue;
            }

            WorldTransform world = ResolveWorldTransform(transformId, data, worldCache, new HashSet<long>());
            string hierarchyPath = ResolveHierarchyPath(transformId, data, pathCache, new HashSet<long>());
            MeshReference meshReference = data.MeshByGameObject.GetValueOrDefault(gameObjectId) ?? MeshReference.Empty;
            data.RenderersByGameObject.TryGetValue(gameObjectId, out RendererInfo? renderer);
            collidersByObject.TryGetValue(gameObjectId, out List<ColliderInfo>? colliders);
            colliders ??= new List<ColliderInfo>();

            bool hasGeometry = !string.IsNullOrEmpty(meshReference.Guid) || renderer != null || colliders.Count > 0;
            bool referenceWorldEligible = hasGeometry && IsReferenceWorldObject(hierarchyPath, options.Rules);
            string category = Classify(gameObject.Name, hierarchyPath, gameObject, meshReference, renderer, colliders, options.Rules);
            string stableId = StableId(options.SourceId, options.SceneName, gameObjectId, transformId, hierarchyPath, "entity");
            long parentObjectId = 0;
            if (transform.FatherId != 0 && data.Transforms.TryGetValue(transform.FatherId, out TransformInfo? parent))
            {
                parentObjectId = parent.GameObjectId;
            }

            string parentStableId = parentObjectId != 0 && data.TransformByGameObject.TryGetValue(parentObjectId, out long parentTransformId)
                ? StableId(options.SourceId, options.SceneName, parentObjectId, parentTransformId,
                    ResolveHierarchyPath(parentTransformId, data, pathCache, new HashSet<long>()), "entity")
                : string.Empty;

            Vector3 boundsMin = world.Position;
            Vector3 boundsMax = world.Position;
            MeshInfo? mesh = null;
            bool missingMesh = false;
            bool rejectedBounds = false;
            if (!string.IsNullOrEmpty(meshReference.Guid) && meshReference.Guid != BuiltInGuid)
            {
                if (meshes.TryGetValue(meshReference.Guid, out mesh) && mesh.HasBounds)
                {
                    (boundsMin, boundsMax) = TransformBounds(mesh.BoundsMin, mesh.BoundsMax, world.Matrix);
                    Vector3 boundsSize = boundsMax - boundsMin;
                    if (mesh.Name.Contains("Combined Mesh (root: scene)", StringComparison.OrdinalIgnoreCase) ||
                        boundsSize.X > 20000f || boundsSize.Y > 20000f || boundsSize.Z > 20000f ||
                        MaxAbs(boundsMin) > 100000f || MaxAbs(boundsMax) > 100000f)
                    {
                        rejectedBounds = true;
                        boundsMin = world.Position;
                        boundsMax = world.Position;
                        data.MissingReferences.Add(new MissingReference(stableId, gameObjectId, hierarchyPath, "BoundsMetadata",
                            meshReference.Guid, "AssetRipper combined/static mesh bounds cannot be attributed safely to this separated placement and were replaced by the audited placement point."));
                    }
                }
                else
                {
                    missingMesh = true;
                    data.MissingReferences.Add(new MissingReference(stableId, gameObjectId, hierarchyPath, "Mesh",
                        meshReference.Guid, "Referenced mesh GUID was not found in the AssetRipper Mesh export."));
                }
            }

            Vector3 convertedPosition = world.Position - options.Origin;
            Vector3 convertedBoundsMin = boundsMin - options.Origin;
            Vector3 convertedBoundsMax = boundsMax - options.Origin;
            string cellId = referenceWorldEligible ? AssignCell(convertedBoundsMin, convertedBoundsMax, category, options.CellSize) : "excluded";
            string landmarkTag = LandmarkTag(gameObject.Name, hierarchyPath, category);
            string transferStatus = missingMesh ? "MissingReference" : rejectedBounds ? "ExtractedBoundsNeedsReview" :
                referenceWorldEligible ? "Extracted" : hasGeometry ? "ClassifiedNonWorld" : "Scanned";
            string replacementStatus = hasGeometry ? "DonorReference" : "ReplacementPlanned";
            string interiorExterior = IsInterior(gameObject.Name, hierarchyPath, category) ? "Interior" : "Exterior";
            var entity = new EntityRecord(stableId, options.SourceId, gameObjectId, transformId, parentObjectId, parentStableId,
                hierarchyPath, gameObject.Name, NormalizeName(gameObject.Name), category, transform.Position, transform.Rotation,
                transform.Scale, world.Position, world.Rotation, world.Scale, convertedPosition, boundsMin, boundsMax,
                convertedBoundsMin, convertedBoundsMax, meshReference.Guid, mesh?.Name ?? string.Empty,
                mesh?.AssetPath ?? string.Empty, renderer?.MaterialGuids ?? Array.Empty<string>(), colliders.Select(c => c.Id).ToArray(),
                gameObject.StaticFlags, gameObject.Active, cellId, interiorExterior, landmarkTag, replacementStatus,
                transferStatus, options.SourceSha256, hasGeometry, referenceWorldEligible,
                data.ComponentTypesByGameObject.GetValueOrDefault(gameObjectId) ?? new List<int>());

            data.AllPlacements.Add(entity);
            if (hasGeometry)
            {
                data.Entities.Add(entity);
            }

            if (referenceWorldEligible)
            {
                data.ReferenceEntities.Add(entity);
                data.SourceBoundsMin = Vector3.Min(data.SourceBoundsMin, boundsMin);
                data.SourceBoundsMax = Vector3.Max(data.SourceBoundsMax, boundsMax);
                data.ConvertedBoundsMin = Vector3.Min(data.ConvertedBoundsMin, convertedBoundsMin);
                data.ConvertedBoundsMax = Vector3.Max(data.ConvertedBoundsMax, convertedBoundsMax);
            }

            if (!string.IsNullOrEmpty(landmarkTag) && referenceWorldEligible)
            {
                data.Landmarks.Add(entity);
            }
        }

        EntityRecord[] landmarkRepresentatives = data.Landmarks
            .GroupBy(e => $"{e.LandmarkTag}|{MathF.Floor(e.ConvertedPosition.X / 128f)}|{MathF.Floor(e.ConvertedPosition.Z / 128f)}")
            .Select(group => group.OrderBy(e => e.HierarchyPath.Count(ch => ch == '/')).ThenBy(e => e.SourceObjectId).First())
            .OrderBy(e => e.LandmarkTag).ThenBy(e => e.SourceObjectId).ToArray();
        data.Landmarks.Clear();
        data.Landmarks.AddRange(landmarkRepresentatives);

        foreach (IGrouping<string, EntityRecord> group in data.ReferenceEntities.Where(e => e.CellId != "global").GroupBy(e => e.CellId).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            (int x, int z) = ParseCellId(group.Key);
            data.Cells.Add(new CellRecord(group.Key, x, z, options.CellSize,
                new Vector3(x * options.CellSize, data.ConvertedBoundsMin.Y, z * options.CellSize),
                new Vector3((x + 1) * options.CellSize, data.ConvertedBoundsMax.Y, (z + 1) * options.CellSize),
                group.Count(), group.Count(e => e.InteriorExterior == "Interior")));
        }

        HashSet<int> supported = new() { 1, 4, 23, 33, 64, 65, 135, 136, 137 };
        foreach ((int type, int count) in data.SerializedTypeCounts.Where(pair => !supported.Contains(pair.Key)).OrderBy(pair => pair.Key))
        {
            data.UnsupportedTypeCounts[type] = count;
        }

        foreach (ColliderInfo collider in data.Colliders)
        {
            if (!data.GameObjects.TryGetValue(collider.GameObjectId, out GameObjectInfo? gameObject) ||
                !data.TransformByGameObject.TryGetValue(collider.GameObjectId, out long transformId))
            {
                continue;
            }

            string path = ResolveHierarchyPath(transformId, data, pathCache, new HashSet<long>());
            collider.StableId = StableId(options.SourceId, options.SceneName, collider.GameObjectId, transformId, path, $"collider:{collider.Id}");
            collider.EntityStableId = StableId(options.SourceId, options.SceneName, collider.GameObjectId, transformId, path, "entity");
            collider.HierarchyPath = path;
            collider.ObjectName = gameObject.Name;
            collider.ReplacementStatus = "DonorReference";
        }
    }

    private static void WriteOutputs(SceneData data, Dictionary<string, MeshInfo> meshes, Options options)
    {
        string external = options.ExternalOutput;
        string project = options.ProjectOutput;
        string generatedUtc = DateTime.UtcNow.ToString("O", Invariant);

        WriteEntityCsv(Path.Combine(external, "WorldObjectPlacements.csv"), data.AllPlacements);
        WriteEntityCsv(Path.Combine(project, "M04A1_WorldEntities.csv"), data.Entities);
        WriteMeshCsv(Path.Combine(external, "WorldMeshManifest.csv"), data.Entities, meshes);
        WriteColliderCsv(Path.Combine(external, "WorldColliderManifest.csv"), data.Colliders);
        WriteColliderCsv(Path.Combine(project, "M04A1_WorldColliders.csv"), data.Colliders);
        WriteEntitySubsetCsv(Path.Combine(external, "WorldVegetationManifest.csv"), data.ReferenceEntities.Where(e => e.Category.StartsWith("Vegetation", StringComparison.Ordinal)));
        WriteEntitySubsetCsv(Path.Combine(external, "WorldInteriorManifest.csv"), data.ReferenceEntities.Where(e => e.InteriorExterior == "Interior"));
        WriteEntitySubsetCsv(Path.Combine(external, "WorldLandmarkManifest.csv"), data.Landmarks);
        WriteEntitySubsetCsv(Path.Combine(project, "M04A1_WorldLandmarks.csv"), data.Landmarks);
        WriteMissingCsv(Path.Combine(external, "WorldMissingReferences.csv"), data.MissingReferences);
        WriteUnsupportedCsv(Path.Combine(external, "WorldUnsupportedObjects.csv"), data.UnsupportedTypeCounts);
        WriteJson(Path.Combine(external, "WorldTerrainManifest.json"), SubsetManifest("Terrain", data.ReferenceEntities.Where(e => e.Category is "Terrain" or "GroundMesh" or "Field"), options));
        WriteJson(Path.Combine(external, "WorldRoadManifest.json"), SubsetManifest("Road", data.ReferenceEntities.Where(e => e.Category is "Road" or "RoadShoulder" or "Ditch" or "Bridge" or "Culvert" or "Driveway"), options));
        WriteJson(Path.Combine(external, "WorldWaterManifest.json"), SubsetManifest("Water", data.ReferenceEntities.Where(e => e.Category is "Water" or "Shoreline"), options));

        var categoryCounts = data.ReferenceEntities.GroupBy(e => e.Category).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count());
        var source = new
        {
            sourceId = options.SourceId,
            sceneName = options.SceneName,
            donorRelativePath = options.DonorRelativePath,
            sha256 = options.SourceSha256,
            extractedSceneSha256 = options.ExtractedSceneSha256,
            extractor = "AssetRipper",
            extractorVersion = options.AssetRipperVersion,
            extractionStatus = "ExtractedExternal"
        };
        var sources = new[]
        {
            source,
            new
            {
                sourceId = "msc-donor-sharedassets3-1e956c8a", sceneName = string.Empty,
                donorRelativePath = "mysummercar_Data/sharedassets3.assets", sha256 = options.AssetsSourceSha256,
                extractedSceneSha256 = string.Empty, extractor = "AssetRipper", extractorVersion = options.AssetRipperVersion,
                extractionStatus = "ExtractedExternal"
            },
            new
            {
                sourceId = "msc-donor-sharedassets3-resource-19797fa0", sceneName = string.Empty,
                donorRelativePath = "mysummercar_Data/sharedassets3.resource", sha256 = options.ResourceSourceSha256,
                extractedSceneSha256 = string.Empty, extractor = "AssetRipper", extractorVersion = options.AssetRipperVersion,
                extractionStatus = "ExtractedExternal"
            }
        };
        var coordinate = new
        {
            sourceUnitScale = 1.0,
            destinationUnitScale = 1.0,
            axisMapping = "X,Y,Z -> X,Y,Z",
            handedness = "Unity left-handed Y-up retained",
            globalTranslation = new[] { -options.Origin.X, -options.Origin.Y, -options.Origin.Z },
            globalRotation = new[] { 0.0, 0.0, 0.0, 1.0 },
            globalScale = new[] { 1.0, 1.0, 1.0 },
            pivotPolicy = "Preserve donor hierarchy pivots; project coordinates are donor world minus the audited garage anchor",
            negativeScalePolicy = "Preserve and flag for validation",
            floatingOriginRecommendation = "Not required for reference generation; reassess for runtime streaming"
        };
        var partition = new
        {
            cellSizeMeters = options.CellSize,
            origin = new[] { 0.0, 0.0, 0.0 },
            boundsMin = Vector(data.ConvertedBoundsMin),
            boundsMax = Vector(data.ConvertedBoundsMax),
            loadingRadiusCells = 1,
            unloadingRadiusCells = 2,
            largeObjectThresholdMeters = options.CellSize * 0.8,
            largeObjectPolicy = "global reference scene",
            crossCellParentPolicy = "entity remains in centroid cell; original parent stable ID retained as metadata",
            crossCellColliderPolicy = "collider remains with source entity; large collider routes to global"
        };
        var database = new
        {
            schema = "msc.world-geometry-database",
            schemaVersion = 1,
            databaseVersion = "04A1.1",
            generatorId = GeneratorId,
            generatorVersion = GeneratorVersion,
            generatedUtc,
            source,
            sources,
            classificationRulesSha256 = options.RulesSha256,
            coordinateConversion = coordinate,
            partition,
            tables = new
            {
                entities = "M04A1_WorldEntities.csv",
                colliders = "M04A1_WorldColliders.csv",
                landmarks = "M04A1_WorldLandmarks.csv"
            },
            counts = new
            {
                discoveredObjects = data.GameObjects.Count,
                placementRecords = data.AllPlacements.Count,
                geometryEntities = data.Entities.Count,
                referenceWorldEntities = data.ReferenceEntities.Count,
                classifiedNonWorldEntities = data.Entities.Count - data.ReferenceEntities.Count,
                colliders = data.Colliders.Count,
                resolvedMeshReferences = data.Entities.Count(e => !string.IsNullOrEmpty(e.MeshGuid) && !string.IsNullOrEmpty(e.MeshAssetPath)),
                missingReferences = data.MissingReferences.Count,
                unsupportedSerializedTypes = data.UnsupportedTypeCounts.Count,
                cells = data.Cells.Count,
                globalEntities = data.ReferenceEntities.Count(e => e.CellId == "global"),
                landmarks = data.Landmarks.Count
            },
            sourceBounds = new { min = Vector(data.SourceBoundsMin), max = Vector(data.SourceBoundsMax) },
            convertedBounds = new { min = Vector(data.ConvertedBoundsMin), max = Vector(data.ConvertedBoundsMax) },
            categoryCounts,
            cells = data.Cells.Select(c => new { c.CellId, c.X, c.Z, c.CellSizeMeters, min = Vector(c.Min), max = Vector(c.Max), c.EntityCount, c.InteriorCount }).ToArray(),
            landmarks = data.Landmarks.Select(e => new { e.StableId, e.OriginalName, e.NormalizedName, e.LandmarkTag, position = Vector(e.ConvertedPosition), boundsMin = Vector(e.ConvertedBoundsMin), boundsMax = Vector(e.ConvertedBoundsMax), e.CellId }).ToArray(),
            limitations = new[]
            {
                "The installed donor is mod-contaminated; provenance is bound to exact local source hashes.",
                "AssetRipper reported one unreadable Texture2D in sharedassets3.assets; geometry and placement export completed.",
                "Reference geometry remains external/ignored and is not production art.",
                "Semantic classification is deterministic but requires manual review for Unknown and InteractivePropCandidate records."
            }
        };
        string databasePath = Path.Combine(project, "M04A1_WorldGeometryDatabase.json");
        WriteJson(databasePath, database);

        var manifest = new
        {
            schema = "msc.world-geometry-manifest",
            schemaVersion = 1,
            generatedUtc,
            generatorId = GeneratorId,
            generatorVersion = GeneratorVersion,
            source,
            sources,
            coordinateConversion = coordinate,
            partition,
            counts = database.counts,
            categoryCounts,
            sourceBounds = database.sourceBounds,
            convertedBounds = database.convertedBounds,
            outputs = Directory.EnumerateFiles(external).Select(Path.GetFileName).OrderBy(name => name).ToArray(),
            projectDatabase = new
            {
                fileName = Path.GetFileName(databasePath),
                sha256 = FileSha256(databasePath),
                entityTableSha256 = FileSha256(Path.Combine(project, "M04A1_WorldEntities.csv")),
                colliderTableSha256 = FileSha256(Path.Combine(project, "M04A1_WorldColliders.csv")),
                landmarkTableSha256 = FileSha256(Path.Combine(project, "M04A1_WorldLandmarks.csv"))
            }
        };
        WriteJson(Path.Combine(external, "WorldGeometryManifest.json"), manifest);
    }

    private static object SubsetManifest(string kind, IEnumerable<EntityRecord> records, Options options)
    {
        EntityRecord[] array = records.OrderBy(e => e.StableId).ToArray();
        return new
        {
            schemaVersion = 1,
            kind,
            sourceId = options.SourceId,
            count = array.Length,
            records = array.Select(e => new { e.StableId, e.SourceObjectId, e.HierarchyPath, e.OriginalName, e.Category, position = Vector(e.ConvertedPosition), boundsMin = Vector(e.ConvertedBoundsMin), boundsMax = Vector(e.ConvertedBoundsMax), e.MeshGuid, e.CellId, e.TransferStatus }).ToArray()
        };
    }

    private static void WriteEntityCsv(string path, IEnumerable<EntityRecord> records)
    {
        using var writer = NewCsv(path);
        writer.WriteLine("StableId,SourceId,SourceObjectId,TransformId,ParentObjectId,ParentStableId,HierarchyPath,OriginalName,NormalizedName,SemanticCategory,LocalPositionX,LocalPositionY,LocalPositionZ,LocalRotationX,LocalRotationY,LocalRotationZ,LocalRotationW,LocalScaleX,LocalScaleY,LocalScaleZ,SourcePositionX,SourcePositionY,SourcePositionZ,SourceRotationX,SourceRotationY,SourceRotationZ,SourceRotationW,SourceScaleX,SourceScaleY,SourceScaleZ,ConvertedPositionX,ConvertedPositionY,ConvertedPositionZ,SourceBoundsMinX,SourceBoundsMinY,SourceBoundsMinZ,SourceBoundsMaxX,SourceBoundsMaxY,SourceBoundsMaxZ,ConvertedBoundsMinX,ConvertedBoundsMinY,ConvertedBoundsMinZ,ConvertedBoundsMaxX,ConvertedBoundsMaxY,ConvertedBoundsMaxZ,MeshGuid,MeshName,MeshAssetPath,MaterialGuids,ColliderIds,StaticFlags,Active,CellId,InteriorExterior,LandmarkTag,ReplacementStatus,TransferStatus,SourceSha256,HasGeometry,ReferenceWorldEligible,ComponentClassIds");
        foreach (EntityRecord e in records.OrderBy(record => record.SourceObjectId))
        {
            WriteCsvRow(writer, e.StableId, e.SourceId, e.SourceObjectId, e.TransformId, e.ParentObjectId, e.ParentStableId,
                e.HierarchyPath, e.OriginalName, e.NormalizedName, e.Category, F(e.LocalPosition.X), F(e.LocalPosition.Y), F(e.LocalPosition.Z),
                F(e.LocalRotation.X), F(e.LocalRotation.Y), F(e.LocalRotation.Z), F(e.LocalRotation.W), F(e.LocalScale.X), F(e.LocalScale.Y), F(e.LocalScale.Z),
                F(e.SourcePosition.X), F(e.SourcePosition.Y), F(e.SourcePosition.Z), F(e.SourceRotation.X), F(e.SourceRotation.Y), F(e.SourceRotation.Z), F(e.SourceRotation.W),
                F(e.SourceScale.X), F(e.SourceScale.Y), F(e.SourceScale.Z), F(e.ConvertedPosition.X), F(e.ConvertedPosition.Y), F(e.ConvertedPosition.Z),
                F(e.SourceBoundsMin.X), F(e.SourceBoundsMin.Y), F(e.SourceBoundsMin.Z), F(e.SourceBoundsMax.X), F(e.SourceBoundsMax.Y), F(e.SourceBoundsMax.Z),
                F(e.ConvertedBoundsMin.X), F(e.ConvertedBoundsMin.Y), F(e.ConvertedBoundsMin.Z), F(e.ConvertedBoundsMax.X), F(e.ConvertedBoundsMax.Y), F(e.ConvertedBoundsMax.Z),
                e.MeshGuid, e.MeshName, e.MeshAssetPath, string.Join(';', e.MaterialGuids), string.Join(';', e.ColliderIds), e.StaticFlags,
                e.Active ? 1 : 0, e.CellId, e.InteriorExterior, e.LandmarkTag, e.ReplacementStatus, e.TransferStatus, e.SourceSha256,
                e.HasGeometry ? 1 : 0, e.ReferenceWorldEligible ? 1 : 0, string.Join(';', e.ComponentClassIds.OrderBy(value => value)));
        }
    }

    private static void WriteEntitySubsetCsv(string path, IEnumerable<EntityRecord> records)
    {
        using var writer = NewCsv(path);
        writer.WriteLine("StableId,SourceObjectId,HierarchyPath,OriginalName,SemanticCategory,LandmarkTag,PositionX,PositionY,PositionZ,BoundsMinX,BoundsMinY,BoundsMinZ,BoundsMaxX,BoundsMaxY,BoundsMaxZ,MeshGuid,CellId,ReplacementStatus,TransferStatus");
        foreach (EntityRecord e in records.OrderBy(record => record.StableId))
        {
            WriteCsvRow(writer, e.StableId, e.SourceObjectId, e.HierarchyPath, e.OriginalName, e.Category, e.LandmarkTag,
                F(e.ConvertedPosition.X), F(e.ConvertedPosition.Y), F(e.ConvertedPosition.Z), F(e.ConvertedBoundsMin.X), F(e.ConvertedBoundsMin.Y), F(e.ConvertedBoundsMin.Z),
                F(e.ConvertedBoundsMax.X), F(e.ConvertedBoundsMax.Y), F(e.ConvertedBoundsMax.Z), e.MeshGuid, e.CellId, e.ReplacementStatus, e.TransferStatus);
        }
    }

    private static void WriteMeshCsv(string path, IEnumerable<EntityRecord> entities, Dictionary<string, MeshInfo> meshes)
    {
        using var writer = NewCsv(path);
        writer.WriteLine("MeshGuid,MeshName,AssetPath,Resolved,VertexCount,IndexCount,BoundsMinX,BoundsMinY,BoundsMinZ,BoundsMaxX,BoundsMaxY,BoundsMaxZ,PlacementCount");
        foreach (IGrouping<string, EntityRecord> group in entities.Where(e => !string.IsNullOrEmpty(e.MeshGuid)).GroupBy(e => e.MeshGuid).OrderBy(g => g.Key))
        {
            meshes.TryGetValue(group.Key, out MeshInfo? mesh);
            WriteCsvRow(writer, group.Key, mesh?.Name ?? string.Empty, mesh?.AssetPath ?? string.Empty, mesh != null ? 1 : 0,
                mesh?.VertexCount ?? 0, mesh?.IndexCount ?? 0, F(mesh?.BoundsMin.X ?? 0), F(mesh?.BoundsMin.Y ?? 0), F(mesh?.BoundsMin.Z ?? 0),
                F(mesh?.BoundsMax.X ?? 0), F(mesh?.BoundsMax.Y ?? 0), F(mesh?.BoundsMax.Z ?? 0), group.Count());
        }
    }

    private static void WriteColliderCsv(string path, IEnumerable<ColliderInfo> colliders)
    {
        using var writer = NewCsv(path);
        writer.WriteLine("StableId,EntityStableId,ComponentId,SourceObjectId,HierarchyPath,ObjectName,ColliderType,Enabled,IsTrigger,Convex,MeshGuid,MeshFileId,CenterX,CenterY,CenterZ,SizeX,SizeY,SizeZ,Radius,Height,Direction,ReplacementStatus");
        foreach (ColliderInfo c in colliders.OrderBy(collider => collider.Id))
        {
            WriteCsvRow(writer, c.StableId, c.EntityStableId, c.Id, c.GameObjectId, c.HierarchyPath, c.ObjectName, ColliderType(c.ClassId),
                c.Enabled ? 1 : 0, c.Trigger ? 1 : 0, c.Convex ? 1 : 0, c.MeshGuid, c.MeshFileId,
                F(c.Center.X), F(c.Center.Y), F(c.Center.Z), F(c.Size.X), F(c.Size.Y), F(c.Size.Z), F(c.Radius), F(c.Height), c.Direction, c.ReplacementStatus);
        }
    }

    private static void WriteMissingCsv(string path, IEnumerable<MissingReference> records)
    {
        using var writer = NewCsv(path);
        writer.WriteLine("EntityStableId,SourceObjectId,HierarchyPath,ReferenceType,ReferenceId,Reason");
        foreach (MissingReference record in records.OrderBy(record => record.EntityStableId))
        {
            WriteCsvRow(writer, record.EntityStableId, record.SourceObjectId, record.HierarchyPath, record.ReferenceType, record.ReferenceId, record.Reason);
        }
    }

    private static void WriteUnsupportedCsv(string path, IReadOnlyDictionary<int, int> counts)
    {
        using var writer = NewCsv(path);
        writer.WriteLine("SerializedClassId,Count,Status,Notes");
        foreach ((int classId, int count) in counts.OrderBy(pair => pair.Key))
        {
            WriteCsvRow(writer, classId, count, "MetadataOnly", "Serialized component type is retained in ComponentClassIds but has no dedicated 04A geometry processor.");
        }
    }

    private static WorldTransform ResolveWorldTransform(long transformId, SceneData data, Dictionary<long, WorldTransform> cache, HashSet<long> stack)
    {
        if (cache.TryGetValue(transformId, out WorldTransform cached))
        {
            return cached;
        }

        if (!stack.Add(transformId) || !data.Transforms.TryGetValue(transformId, out TransformInfo? transform))
        {
            return WorldTransform.Identity;
        }

        Matrix4x4 local = Matrix4x4.CreateScale(transform.Scale) * Matrix4x4.CreateFromQuaternion(transform.Rotation) * Matrix4x4.CreateTranslation(transform.Position);
        Matrix4x4 worldMatrix = local;
        if (transform.FatherId != 0 && data.Transforms.ContainsKey(transform.FatherId))
        {
            worldMatrix *= ResolveWorldTransform(transform.FatherId, data, cache, stack).Matrix;
        }

        stack.Remove(transformId);
        if (!Matrix4x4.Decompose(worldMatrix, out Vector3 scale, out Quaternion rotation, out Vector3 position))
        {
            scale = transform.Scale;
            rotation = transform.Rotation;
            position = Vector3.Transform(Vector3.Zero, worldMatrix);
        }

        var result = new WorldTransform(worldMatrix, position, Quaternion.Normalize(rotation), scale);
        cache[transformId] = result;
        return result;
    }

    private static string ResolveHierarchyPath(long transformId, SceneData data, Dictionary<long, string> cache, HashSet<long> stack)
    {
        if (cache.TryGetValue(transformId, out string? cached))
        {
            return cached;
        }

        if (!stack.Add(transformId) || !data.Transforms.TryGetValue(transformId, out TransformInfo? transform))
        {
            return "<invalid-hierarchy>";
        }

        string name = data.GameObjects.TryGetValue(transform.GameObjectId, out GameObjectInfo? gameObject) ? gameObject.Name : $"Object_{transform.GameObjectId}";
        string path = transform.FatherId != 0 && data.Transforms.ContainsKey(transform.FatherId)
            ? $"{ResolveHierarchyPath(transform.FatherId, data, cache, stack)}/{name}"
            : name;
        stack.Remove(transformId);
        cache[transformId] = path;
        return path;
    }

    private static (Vector3 Min, Vector3 Max) TransformBounds(Vector3 min, Vector3 max, Matrix4x4 matrix)
    {
        Vector3 resultMin = new(float.PositiveInfinity);
        Vector3 resultMax = new(float.NegativeInfinity);
        for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
        for (int z = 0; z < 2; z++)
        {
            Vector3 corner = new(x == 0 ? min.X : max.X, y == 0 ? min.Y : max.Y, z == 0 ? min.Z : max.Z);
            Vector3 transformed = Vector3.Transform(corner, matrix);
            resultMin = Vector3.Min(resultMin, transformed);
            resultMax = Vector3.Max(resultMax, transformed);
        }

        return (resultMin, resultMax);
    }

    private static string Classify(string name, string path, GameObjectInfo gameObject, MeshReference mesh, RendererInfo? renderer, IReadOnlyCollection<ColliderInfo> colliders, ClassificationRules rules)
    {
        string objectText = name.ToLowerInvariant();
        string hierarchyText = path.ToLowerInvariant();
        bool mapGeometry = hierarchyText.StartsWith("map/", StringComparison.Ordinal);
        string configuredCategory = rules.Match(objectText, hierarchyText);
        if (!string.IsNullOrEmpty(configuredCategory)) return configuredCategory;
        if (ContainsAny(objectText, "spawn", "teleport", "respawn")) return "SpawnMarker";
        if (ContainsAny(objectText, "interior portal", "interiorportal", "doorway")) return "InteriorPortal";
        if (mapGeometry && ContainsAny(objectText, "terrain")) return "Terrain";
        if (mapGeometry && ContainsAny(objectText, "ground", "maasto")) return "GroundMesh";
        if (mapGeometry && ContainsAny(objectText, "shoulder", "piennar")) return "RoadShoulder";
        if (mapGeometry && ContainsAny(objectText, "ditch", "oja")) return "Ditch";
        if (mapGeometry && ContainsAny(objectText, "bridge", "silta")) return "Bridge";
        if (mapGeometry && ContainsAny(objectText, "culvert", "rumpu")) return "Culvert";
        if (mapGeometry && ContainsAny(objectText, "driveway")) return "Driveway";
        if (mapGeometry && ContainsAny(objectText, "dirtroad", "road", "highway", "street")) return "Road";
        if (mapGeometry && ContainsAny(objectText, "shore", "coast", "ranta")) return "Shoreline";
        if (mapGeometry && ContainsAny(objectText, "water", "lake", "pond", "sea")) return "Water";
        if (mapGeometry && ContainsAny(objectText, "grass", "hein")) return "VegetationGrass";
        if (mapGeometry && ContainsAny(objectText, "bush", "shrub", "pensas")) return "VegetationBush";
        if (mapGeometry && ContainsAny(objectText, "tree", "spruce", "pine", "birch", "kuusi", "manty", "mänty", "koivu")) return "VegetationTree";
        if (mapGeometry && ContainsAny(objectText, "rock", "stone", "kivi")) return "Rock";
        if (ContainsAny(objectText, "roof", "katto")) return "Roof";
        if (ContainsAny(objectText, "floor", "lattia")) return "Floor";
        if (ContainsAny(objectText, "window", "ikkuna")) return "Window";
        if (ContainsAny(objectText, "gate", "portti")) return "Gate";
        if (ContainsAny(objectText, "door", "ovi")) return "Door";
        if (ContainsAny(objectText, "fence", "aita")) return "Fence";
        if (ContainsAny(objectText, "powerpole", "utilitypole", "electric pole", "telephone pole", "lightpole")) return "UtilityPole";
        if (ContainsAny(objectText, "wire", "cable", "johto")) return "Wire";
        if (ContainsAny(objectText, "roadsign", "road sign", "sign_", "traffic sign")) return "RoadSign";
        if (ContainsAny(objectText, "field", "pelto")) return "Field";
        if (ContainsAny(objectText, "yard", "piha")) return "Yard";
        if (ContainsAny(objectText, "garage", "cabin", "cottage", "house", "shop", "store", "station", "church", "shed", "barn", "pub", "repair", "inspection", "sauna"))
            return IsInterior(name, path, string.Empty) ? "BuildingInterior" : "BuildingExterior";
        if (ContainsAny(objectText, "fleetari", "teimo", "landfill", "ski hill", "dance pavilion", "airfield")) return "Landmark";
        if (string.IsNullOrEmpty(mesh.Guid) && renderer == null && colliders.Count > 0) return "ColliderOnly";
        if (ContainsAny(objectText, "trigger", "checkpoint", "route", "waypoint")) return "GameplayMarker";
        if (IsInterior(name, path, string.Empty)) return "BuildingInterior";
        if (IsBuildingLocationRoot(path)) return "BuildingExterior";
        if (gameObject.StaticFlags != 0 || hierarchyText.StartsWith("map/", StringComparison.Ordinal)) return "StaticProp";
        if (!string.IsNullOrEmpty(mesh.Guid) || renderer != null) return "InteractivePropCandidate";
        return "Unknown";
    }

    private static bool IsReferenceWorldObject(string hierarchyPath, ClassificationRules rules)
    {
        if (rules.ExcludedHierarchyContains.Any(value => hierarchyPath.Contains(value, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        string root = hierarchyPath.Split('/')[0];
        return rules.WorldRootAllowList.Contains(root, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsBuildingLocationRoot(string hierarchyPath)
    {
        string root = hierarchyPath.Split('/')[0];
        return root is "YARD" or "STORE" or "PERAJARVI" or "REPAIRSHOP" or "DANCEHALL" or "COTTAGE" or "CABIN" or
            "INSPECTION" or "WATERFACILITY" or "LANDFILL" or "RYKIPOHJA" or "THEATRE" or "JAIL";
    }

    private static float MaxAbs(Vector3 value) => MathF.Max(MathF.Abs(value.X), MathF.Max(MathF.Abs(value.Y), MathF.Abs(value.Z)));

    private static bool IsInterior(string name, string path, string category)
    {
        string text = $"{name} {path} {category}".ToLowerInvariant();
        return ContainsAny(text, "interior", "inside", "indoor", "kitchen", "livingroom", "bedroom", "bathroom", "sauna room", "buildinginterior");
    }

    private static string LandmarkTag(string name, string path, string category)
    {
        string text = $"{name} {path}".ToLowerInvariant();
        string root = path.Split('/')[0];
        if (root == "CABIN" && ContainsAny(text, "garage", "shed")) return "PrimaryHomeGarage";
        if (root == "REPAIRSHOP") return "RepairWorkshop";
        if (root is "STORE" or "PERAJARVI" && ContainsAny(text, "teimo", "shop", "store", "pier")) return "TownService";
        if (root == "PERAJARVI" && text.Contains("church", StringComparison.Ordinal)) return "Church";
        if (root == "INSPECTION") return "VehicleInspection";
        if (root == "LANDFILL") return "Landfill";
        if (root == "COTTAGE") return "IslandCottage";
        if (root == "MAP" && (text.Contains("bridge", StringComparison.Ordinal) || text.Contains("silta", StringComparison.Ordinal))) return "Bridge";
        if (root == "MAP" && category == "Water" && ContainsAny(text, "lake", "jarvi", "järvi")) return "MajorWater";
        if (category == "Landmark") return "UniqueStructure";
        return string.Empty;
    }

    private static string AssignCell(Vector3 min, Vector3 max, string category, float cellSize)
    {
        Vector3 size = max - min;
        if (size.X > cellSize * 0.8f || size.Z > cellSize * 0.8f || category == "Terrain")
        {
            return "global";
        }

        Vector3 center = (min + max) * 0.5f;
        int x = (int)MathF.Floor(center.X / cellSize);
        int z = (int)MathF.Floor(center.Z / cellSize);
        return $"cell_{x}_{z}";
    }

    private static (int X, int Z) ParseCellId(string id)
    {
        string[] parts = id.Split('_');
        return (int.Parse(parts[1], Invariant), int.Parse(parts[2], Invariant));
    }

    private static string StableId(string sourceId, string sceneName, long objectId, long transformId, string hierarchyPath, string role)
    {
        string canonical = $"{sourceId.Trim().ToLowerInvariant()}|{sceneName.Trim().ToLowerInvariant()}|{objectId}|{transformId}|{hierarchyPath.Trim().ToLowerInvariant()}|{role.Trim().ToLowerInvariant()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant()[..32];
    }

    private static string NormalizeName(string value)
    {
        string normalized = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(normalized) ? "unnamed" : normalized;
    }

    private static string FileSha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static bool ContainsAny(string value, params string[] candidates) => candidates.Any(candidate => value.Contains(candidate, StringComparison.Ordinal));
    private static string ColliderType(int classId) => classId switch { 64 => "MeshCollider", 65 => "BoxCollider", 135 => "SphereCollider", 136 => "CapsuleCollider", _ => $"ClassId{classId}" };
    private static string ValueAfterColon(string value) => value[(value.IndexOf(':') + 1)..].Trim();
    private static string Unquote(string value) => value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')) ? value[1..^1] : value;
    private static int ParseInt(string value) => int.Parse(value, NumberStyles.Integer, Invariant);
    private static long ParseLong(string value) => long.Parse(value, NumberStyles.Integer, Invariant);
    private static float ParseFloat(string value) => float.Parse(value, NumberStyles.Float, Invariant);
    private static long ParseFileId(string line) => long.TryParse(FileIdRegex().Match(line).Groups[1].Value, NumberStyles.Integer, Invariant, out long value) ? value : 0;
    private static (long FileId, string Guid) ParseReference(string line)
    {
        Match match = ReferenceRegex().Match(line);
        return match.Success ? (long.Parse(match.Groups[1].Value, Invariant), match.Groups[2].Success ? match.Groups[2].Value : string.Empty) : (0, string.Empty);
    }
    private static Vector3 ParseVector3(string line)
    {
        Match match = Vector3Regex().Match(line);
        return match.Success ? new Vector3(ParseFloat(match.Groups[1].Value), ParseFloat(match.Groups[2].Value), ParseFloat(match.Groups[3].Value)) : Vector3.Zero;
    }
    private static Quaternion ParseQuaternion(string line)
    {
        Match match = QuaternionRegex().Match(line);
        return match.Success ? new Quaternion(ParseFloat(match.Groups[1].Value), ParseFloat(match.Groups[2].Value), ParseFloat(match.Groups[3].Value), ParseFloat(match.Groups[4].Value)) : Quaternion.Identity;
    }
    private static double[] Vector(Vector3 value) => new[] { (double)value.X, value.Y, value.Z };
    private static string F(float value) => value.ToString("R", Invariant);
    private static StreamWriter NewCsv(string path) => new(path, false, new UTF8Encoding(false), 1 << 16);
    private static void WriteCsvRow(TextWriter writer, params object?[] values) => writer.WriteLine(string.Join(',', values.Select(Csv)));
    private static string Csv(object? value)
    {
        string text = Convert.ToString(value, Invariant) ?? string.Empty;
        return text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }
    private static void WriteJson(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions), new UTF8Encoding(false));
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [GeneratedRegex("^--- !u!(\\d+) &(-?\\d+)")]
    private static partial Regex HeaderRegex();
    [GeneratedRegex("fileID: (-?\\d+)")]
    private static partial Regex FileIdRegex();
    [GeneratedRegex("fileID: (-?\\d+)(?:, guid: ([0-9a-fA-F]+))?")]
    private static partial Regex ReferenceRegex();
    [GeneratedRegex(@"\{x: ([^,]+), y: ([^,]+), z: ([^}\s]+)\}")]
    private static partial Regex Vector3Regex();
    [GeneratedRegex(@"\{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}\s]+)\}")]
    private static partial Regex QuaternionRegex();

    private sealed class Options
    {
        public required string ScenePath { get; init; }
        public required string MeshRoot { get; init; }
        public required string ExternalOutput { get; init; }
        public required string ProjectOutput { get; init; }
        public required string SourceId { get; init; }
        public required string SourceSha256 { get; init; }
        public required string AssetsSourceSha256 { get; init; }
        public required string ResourceSourceSha256 { get; init; }
        public required string ExtractedSceneSha256 { get; init; }
        public required string DonorRelativePath { get; init; }
        public required string SceneName { get; init; }
        public required string AssetRipperVersion { get; init; }
        public required ClassificationRules Rules { get; init; }
        public required string RulesSha256 { get; init; }
        public required Vector3 Origin { get; init; }
        public required float CellSize { get; init; }

        public static Options Parse(string[] args)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i += 2)
            {
                if (i + 1 >= args.Length || !args[i].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException("Arguments must be --name value pairs.");
                values[args[i][2..]] = args[i + 1];
            }

            string Required(string name) => values.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException($"Missing --{name}.");
            float Number(string name) => float.Parse(Required(name), NumberStyles.Float, Invariant);
            string rulesPath = Path.GetFullPath(Required("rules"));
            ClassificationRules rules = JsonSerializer.Deserialize<ClassificationRules>(File.ReadAllText(rulesPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new FormatException("World transfer rules could not be parsed.");
            if (rules.SchemaVersion != 2 || rules.WorldRootAllowList.Length == 0 || rules.Rules.Length == 0)
                throw new FormatException("World transfer rules are incomplete or use an unsupported schema.");
            return new Options
            {
                ScenePath = Path.GetFullPath(Required("scene")), MeshRoot = Path.GetFullPath(Required("mesh-root")),
                ExternalOutput = Path.GetFullPath(Required("external-output")), ProjectOutput = Path.GetFullPath(Required("project-output")),
                SourceId = Required("source-id"), SourceSha256 = Required("source-sha256").ToLowerInvariant(),
                AssetsSourceSha256 = Required("assets-source-sha256").ToLowerInvariant(), ResourceSourceSha256 = Required("resource-source-sha256").ToLowerInvariant(),
                ExtractedSceneSha256 = Required("extracted-scene-sha256").ToLowerInvariant(),
                DonorRelativePath = Required("donor-relative-path"), SceneName = Required("scene-name"), AssetRipperVersion = Required("assetripper-version"),
                Origin = new Vector3(Number("origin-x"), Number("origin-y"), Number("origin-z")), CellSize = Number("cell-size"),
                Rules = rules, RulesSha256 = FileSha256(rulesPath)
            };
        }
    }

    private sealed class ClassificationRules
    {
        public int SchemaVersion { get; set; }
        public string[] WorldRootAllowList { get; set; } = Array.Empty<string>();
        public string[] ExcludedHierarchyContains { get; set; } = Array.Empty<string>();
        public ClassificationRule[] Rules { get; set; } = Array.Empty<ClassificationRule>();

        public string Match(string name, string hierarchy)
        {
            foreach (ClassificationRule rule in Rules.OrderByDescending(rule => rule.Priority).ThenBy(rule => rule.Category, StringComparer.Ordinal))
            {
                if (rule.HierarchyStartsWith.Length > 0 &&
                    !rule.HierarchyStartsWith.Any(value => hierarchy.StartsWith(value, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                if (rule.RequiredHierarchyContains.Length > 0 &&
                    !rule.RequiredHierarchyContains.All(value => hierarchy.Contains(value, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                if (rule.ExcludedHierarchyContains.Any(value => hierarchy.Contains(value, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                string candidate = string.Equals(rule.MatchField, "Hierarchy", StringComparison.OrdinalIgnoreCase) ? hierarchy :
                    string.Equals(rule.MatchField, "Both", StringComparison.OrdinalIgnoreCase) ? $"{name} {hierarchy}" : name;
                if (rule.Contains.Any(value => candidate.Contains(value, StringComparison.OrdinalIgnoreCase)))
                {
                    return rule.Category;
                }
            }

            return string.Empty;
        }
    }

    private sealed class ClassificationRule
    {
        public string Category { get; set; } = string.Empty;
        public int Priority { get; set; }
        public string MatchField { get; set; } = "Name";
        public string[] Contains { get; set; } = Array.Empty<string>();
        public string[] HierarchyStartsWith { get; set; } = Array.Empty<string>();
        public string[] RequiredHierarchyContains { get; set; } = Array.Empty<string>();
        public string[] ExcludedHierarchyContains { get; set; } = Array.Empty<string>();
    }

    private sealed class SceneData
    {
        public Dictionary<long, GameObjectInfo> GameObjects { get; } = new();
        public Dictionary<long, TransformInfo> Transforms { get; } = new();
        public Dictionary<long, long> TransformByGameObject { get; } = new();
        public Dictionary<long, MeshReference> MeshByGameObject { get; } = new();
        public Dictionary<long, RendererInfo> RenderersByGameObject { get; } = new();
        public Dictionary<long, List<int>> ComponentTypesByGameObject { get; } = new();
        public Dictionary<int, int> SerializedTypeCounts { get; } = new();
        public Dictionary<int, int> UnsupportedTypeCounts { get; } = new();
        public List<ColliderInfo> Colliders { get; } = new();
        public List<EntityRecord> AllPlacements { get; } = new();
        public List<EntityRecord> Entities { get; } = new();
        public List<EntityRecord> ReferenceEntities { get; } = new();
        public List<EntityRecord> Landmarks { get; } = new();
        public List<MissingReference> MissingReferences { get; } = new();
        public List<CellRecord> Cells { get; } = new();
        public Vector3 SourceBoundsMin { get; set; } = new(float.PositiveInfinity);
        public Vector3 SourceBoundsMax { get; set; } = new(float.NegativeInfinity);
        public Vector3 ConvertedBoundsMin { get; set; } = new(float.PositiveInfinity);
        public Vector3 ConvertedBoundsMax { get; set; } = new(float.NegativeInfinity);
    }

    private sealed class Block(int classId, long id)
    {
        public int ClassId { get; } = classId; public long Id { get; } = id; public long GameObjectId { get; set; }
        public long FatherId { get; set; } public string Name { get; set; } = string.Empty; public int Layer { get; set; }
        public long StaticFlags { get; set; } public bool Active { get; set; } = true; public bool Enabled { get; set; } = true;
        public bool Trigger { get; set; } public bool Convex { get; set; } public long MeshFileId { get; set; }
        public string MeshGuid { get; set; } = string.Empty; public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; } = Quaternion.Identity; public Vector3 Scale { get; set; } = Vector3.One;
        public Vector3 Center { get; set; } public Vector3 Size { get; set; } public float Radius { get; set; }
        public float Height { get; set; } public int Direction { get; set; } public List<string> MaterialGuids { get; } = new();
    }

    private sealed record GameObjectInfo(long Id, string Name, int Layer, long StaticFlags, bool Active);
    private sealed record TransformInfo(long Id, long GameObjectId, long FatherId, Vector3 Position, Quaternion Rotation, Vector3 Scale);
    private sealed record MeshReference(long FileId, string Guid) { public static MeshReference Empty { get; } = new(0, string.Empty); }
    private sealed record RendererInfo(bool Enabled, IReadOnlyList<string> MaterialGuids);
    private readonly record struct WorldTransform(Matrix4x4 Matrix, Vector3 Position, Quaternion Rotation, Vector3 Scale)
    { public static WorldTransform Identity { get; } = new(Matrix4x4.Identity, Vector3.Zero, Quaternion.Identity, Vector3.One); }
    private sealed record MissingReference(string EntityStableId, long SourceObjectId, string HierarchyPath, string ReferenceType, string ReferenceId, string Reason);
    private sealed record CellRecord(string CellId, int X, int Z, float CellSizeMeters, Vector3 Min, Vector3 Max, int EntityCount, int InteriorCount);

    private sealed class MeshInfo(string guid, string assetPath, string name)
    {
        public string Guid { get; } = guid; public string AssetPath { get; } = assetPath; public string Name { get; set; } = name;
        public int VertexCount { get; set; } public int IndexCount { get; set; }
        public Vector3 BoundsMin { get; private set; } = new(float.PositiveInfinity); public Vector3 BoundsMax { get; private set; } = new(float.NegativeInfinity);
        public bool HasBounds => float.IsFinite(BoundsMin.X) && float.IsFinite(BoundsMax.X);
        public void IncludeBounds(Vector3 min, Vector3 max) { BoundsMin = Vector3.Min(BoundsMin, min); BoundsMax = Vector3.Max(BoundsMax, max); }
    }

    private sealed class ColliderInfo(long id, int classId, long gameObjectId, bool enabled, bool trigger, bool convex, string meshGuid, long meshFileId, Vector3 center, Vector3 size, float radius, float height, int direction)
    {
        public long Id { get; } = id; public int ClassId { get; } = classId; public long GameObjectId { get; } = gameObjectId;
        public bool Enabled { get; } = enabled; public bool Trigger { get; } = trigger; public bool Convex { get; } = convex;
        public string MeshGuid { get; } = meshGuid; public long MeshFileId { get; } = meshFileId; public Vector3 Center { get; } = center;
        public Vector3 Size { get; } = size; public float Radius { get; } = radius; public float Height { get; } = height; public int Direction { get; } = direction;
        public string StableId { get; set; } = string.Empty; public string EntityStableId { get; set; } = string.Empty;
        public string HierarchyPath { get; set; } = string.Empty; public string ObjectName { get; set; } = string.Empty; public string ReplacementStatus { get; set; } = string.Empty;
    }

    private sealed record EntityRecord(string StableId, string SourceId, long SourceObjectId, long TransformId, long ParentObjectId,
        string ParentStableId, string HierarchyPath, string OriginalName, string NormalizedName, string Category,
        Vector3 LocalPosition, Quaternion LocalRotation, Vector3 LocalScale, Vector3 SourcePosition, Quaternion SourceRotation,
        Vector3 SourceScale, Vector3 ConvertedPosition, Vector3 SourceBoundsMin, Vector3 SourceBoundsMax, Vector3 ConvertedBoundsMin,
        Vector3 ConvertedBoundsMax, string MeshGuid, string MeshName, string MeshAssetPath, IReadOnlyList<string> MaterialGuids,
        IReadOnlyList<long> ColliderIds, long StaticFlags, bool Active, string CellId, string InteriorExterior, string LandmarkTag,
        string ReplacementStatus, string TransferStatus, string SourceSha256, bool HasGeometry, bool ReferenceWorldEligible,
        IReadOnlyList<int> ComponentClassIds);
}
