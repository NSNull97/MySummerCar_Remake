using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MSCMapMigration
{
    internal static class BlenderMapExporter
    {
        internal const string ExporterVersion = "1.2";

        private static readonly HashSet<string> CopyableTextureExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".bmp", ".dds", ".exr"
            };

        public static MapExportManifest Export(
            IMapMigrationSettings settings,
            MapScanResult scan)
        {
            if (TryLoadReusableExport(settings, scan, out MapExportManifest reusable))
            {
                Debug.Log(
                    $"MAP_MIGRATION_EXPORT_REUSED root={reusable.exportRoot} " +
                    $"instances={reusable.instanceCount} version={reusable.exporterVersion}");
                return reusable;
            }

            string exportRoot = ResolveExportRoot(settings, scan.Inventory.sourceSetFingerprint);
            RecreateExportRoot(settings.BlenderExportFolder, exportRoot);
            CreateStructure(exportRoot);

            IReadOnlyList<ScannedMeshInstance> all = scan.Instances;
            ScannedMeshInstance[] ground = all
                .Where(instance => instance.Record.category == MapMeshCategory.GroundCandidate)
                .ToArray();
            ScannedMeshInstance[] roads = all
                .Where(instance => MapMeshScanner.IsRoad(instance.Record.category))
                .ToArray();
            Dictionary<Material, string> materialNames = BuildMaterialNames(all);
            var warnings = new List<string>();
            Dictionary<Texture, string> textureFiles = CopyTextures(
                exportRoot,
                all,
                warnings);
            MapMaterialCatalog catalog = BuildMaterialCatalog(
                all,
                textureFiles,
                warnings);
            WriteJson(
                Path.Combine(exportRoot, "Metadata", "materials.json"),
                JsonUtility.ToJson(catalog, true));

            var manifest = new MapExportManifest
            {
                exporterVersion = ExporterVersion,
                generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                sourceFingerprint = scan.Inventory.sourceSetFingerprint,
                exportRoot = MapMigrationPaths.NormalizeRelative(
                    Path.GetRelativePath(MapMigrationPaths.ProjectRoot, exportRoot)),
                fbxExporterAvailable = FindFbxExporterType() != null,
                instanceCount = all.Count,
                roadInstanceCount = roads.Length,
                groundInstanceCount = ground.Length,
                sceneBounds = SerializableBounds.From(CalculateBounds(all)),
                warnings = warnings
            };

            ExportCombined(exportRoot, "BaseMap_All", all, materialNames, textureFiles, manifest);
            ExportCombined(exportRoot, "Ground_All", ground, materialNames, textureFiles, manifest);
            ExportCombined(exportRoot, "Roads_All", roads, materialNames, textureFiles, manifest);
            ExportIndividualObjects(exportRoot, all, materialNames, textureFiles, manifest);
            manifest.uniqueObjectFileCount = manifest.entries
                .Select(entry => entry.geometryFile)
                .Distinct(StringComparer.Ordinal)
                .Count();

            WriteJson(
                Path.Combine(exportRoot, "Metadata", "manifest.json"),
                JsonUtility.ToJson(manifest, true));
            File.Copy(
                MapMigrationPaths.ToAbsoluteProjectPath(MapMigrationPaths.InventoryCsv),
                Path.Combine(exportRoot, "Metadata", "inventory.csv"),
                overwrite: true);
            WriteBlenderScript(Path.Combine(exportRoot, "Blender", "import_base_map.py"));
            WriteReadme(Path.Combine(exportRoot, "README.md"), manifest);
            return manifest;
        }

        private static bool TryLoadReusableExport(
            IMapMigrationSettings settings,
            MapScanResult scan,
            out MapExportManifest manifest)
        {
            manifest = null;
            string configuredRoot = MapMigrationPaths.ToAbsoluteProjectPath(
                settings.BlenderExportFolder);
            if (!Directory.Exists(configuredRoot))
            {
                return false;
            }

            foreach (string directory in Directory
                         .EnumerateDirectories(configuredRoot, "*", SearchOption.TopDirectoryOnly)
                         .OrderByDescending(Directory.GetLastWriteTimeUtc))
            {
                string manifestPath = Path.Combine(directory, "Metadata", "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                MapExportManifest candidate;
                try
                {
                    candidate = JsonUtility.FromJson<MapExportManifest>(
                        File.ReadAllText(manifestPath, Encoding.UTF8));
                }
                catch (Exception)
                {
                    continue;
                }

                if (candidate == null ||
                    !string.Equals(candidate.exporterVersion, ExporterVersion, StringComparison.Ordinal) ||
                    !string.Equals(
                        candidate.sourceFingerprint,
                        scan.Inventory.sourceSetFingerprint,
                        StringComparison.Ordinal) ||
                    candidate.instanceCount != scan.Instances.Count ||
                    !File.Exists(Path.Combine(directory, "Scene", "BaseMap_All.obj")) ||
                    !File.Exists(Path.Combine(directory, "Scene", "Ground_All.obj")) ||
                    !File.Exists(Path.Combine(directory, "Scene", "Roads_All.obj")) ||
                    !File.Exists(Path.Combine(directory, "Blender", "import_base_map.py")) ||
                    !File.Exists(Path.Combine(directory, "README.md")))
                {
                    continue;
                }

                manifest = candidate;
                return true;
            }

            return false;
        }

        private static void ExportCombined(
            string root,
            string name,
            IReadOnlyList<ScannedMeshInstance> instances,
            IReadOnlyDictionary<Material, string> materialNames,
            IReadOnlyDictionary<Texture, string> textureFiles,
            MapExportManifest manifest)
        {
            string obj = Path.Combine(root, "Scene", name + ".obj");
            string mtl = Path.Combine(root, "Materials", name + ".mtl");
            ObjExporter.Write(obj, "../Materials/" + name + ".mtl", instances, true, materialNames);
            ObjExporter.WriteMtl(
                mtl,
                CollectMaterials(instances),
                materialNames,
                textureFiles,
                "../Textures/");
            manifest.createdFiles.Add(Relative(root, obj));
            manifest.createdFiles.Add(Relative(root, mtl));

            if (manifest.fbxExporterAvailable)
            {
                string fbx = Path.Combine(root, "Scene", name + ".fbx");
                if (TryExportFbx(fbx, instances, worldSpace: true))
                {
                    manifest.createdFiles.Add(Relative(root, fbx));
                    manifest.preferredFormat = "FBX";
                }
                else
                {
                    manifest.warnings.Add(
                        "Official FBX Exporter was detected but failed for " + name + ". OBJ remains authoritative.");
                }
            }
        }

        private static void ExportIndividualObjects(
            string root,
            IReadOnlyList<ScannedMeshInstance> instances,
            IReadOnlyDictionary<Material, string> materialNames,
            IReadOnlyDictionary<Texture, string> textureFiles,
            MapExportManifest manifest)
        {
            var filesByGeometry = new Dictionary<string, string>(StringComparer.Ordinal);
            int index = 0;
            foreach (ScannedMeshInstance instance in instances)
            {
                index++;
                MapMigrationProgress.Check(
                    "Export Source Meshes for Blender",
                    $"Object geometry {index}/{instances.Count}",
                    index / (float)Math.Max(1, instances.Count));
                string geometryKey = !string.IsNullOrEmpty(instance.Record.assetGuid)
                    ? instance.Record.assetGuid + ":" + instance.Record.localFileId + ":" +
                      instance.Record.category
                    : instance.Record.recordId;
                bool shared = filesByGeometry.TryGetValue(geometryKey, out string relativeObj);
                if (!shared)
                {
                    string categoryFolder = CategoryFolder(instance.Record.category);
                    string baseName = ObjExporter.SafeName(instance.Record.meshAssetName) + "_" +
                                      MapMigrationPaths.StableHash(geometryKey);
                    relativeObj = $"Objects/{categoryFolder}/{baseName}.obj";
                    string absoluteObj = Path.Combine(root, relativeObj.Replace('/', Path.DirectorySeparatorChar));
                    string mtlName = baseName + ".mtl";
                    string absoluteMtl = Path.Combine(root, "Materials", mtlName);
                    ObjExporter.Write(
                        absoluteObj,
                        "../../Materials/" + mtlName,
                        new[] { instance },
                        worldSpace: false,
                        materialNames);
                    ObjExporter.WriteMtl(
                        absoluteMtl,
                        instance.Renderer.sharedMaterials,
                        materialNames,
                        textureFiles,
                        "../Textures/");
                    filesByGeometry.Add(geometryKey, relativeObj);
                    manifest.createdFiles.Add(relativeObj);
                    manifest.createdFiles.Add("Materials/" + mtlName);

                    if (manifest.fbxExporterAvailable)
                    {
                        string fbx = Path.ChangeExtension(absoluteObj, ".fbx");
                        if (TryExportFbx(fbx, new[] { instance }, worldSpace: false))
                        {
                            manifest.createdFiles.Add(Relative(root, fbx));
                        }
                    }
                }

                manifest.entries.Add(new MapExportEntry
                {
                    recordId = instance.Record.recordId,
                    scenePath = instance.Record.scenePath,
                    hierarchyPath = instance.Record.hierarchyPath,
                    category = instance.Record.category,
                    geometryFile = relativeObj,
                    geometryShared = shared,
                    worldMatrix = instance.Record.worldMatrix
                });
            }
        }

        private static Dictionary<Material, string> BuildMaterialNames(
            IReadOnlyList<ScannedMeshInstance> instances)
        {
            var result = new Dictionary<Material, string>();
            foreach (Material material in CollectMaterials(instances))
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    material,
                    out string guid,
                    out long localId);
                result[material] = ObjExporter.SafeName(material.name) + "_" +
                                   MapMigrationPaths.StableHash((guid ?? string.Empty) + ":" + localId);
            }

            return result;
        }

        private static Dictionary<Texture, string> CopyTextures(
            string root,
            IReadOnlyList<ScannedMeshInstance> instances,
            List<string> warnings)
        {
            var result = new Dictionary<Texture, string>();
            foreach (Material material in CollectMaterials(instances))
            {
                foreach (string property in material.GetTexturePropertyNames())
                {
                    Texture texture = material.GetTexture(property);
                    if (texture == null || result.ContainsKey(texture))
                    {
                        continue;
                    }

                    string assetPath = AssetDatabase.GetAssetPath(texture);
                    string extension = Path.GetExtension(assetPath);
                    if (string.IsNullOrEmpty(assetPath) ||
                        !CopyableTextureExtensions.Contains(extension))
                    {
                        warnings.Add(
                            $"Texture '{texture.name}' is not backed by a directly copyable project image file ({assetPath}).");
                        continue;
                    }

                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        texture,
                        out string guid,
                        out long localId);
                    string fileName = ObjExporter.SafeName(texture.name) + "_" +
                                      MapMigrationPaths.StableHash((guid ?? string.Empty) + ":" + localId) +
                                      extension.ToLowerInvariant();
                    File.Copy(
                        MapMigrationPaths.ToAbsoluteProjectPath(assetPath),
                        Path.Combine(root, "Textures", fileName),
                        overwrite: true);
                    result.Add(texture, fileName);
                }
            }

            return result;
        }

        private static MapMaterialCatalog BuildMaterialCatalog(
            IReadOnlyList<ScannedMeshInstance> instances,
            IReadOnlyDictionary<Texture, string> textureFiles,
            List<string> warnings)
        {
            var catalog = new MapMaterialCatalog();
            foreach (Material material in CollectMaterials(instances))
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    material,
                    out string guid,
                    out long ignored);
                Texture baseTexture = GetTexture(material, "_BaseColorMap", "_BaseMap", "_MainTex");
                Texture normal = GetTexture(material, "_NormalMap", "_BumpMap");
                Texture mask = GetTexture(material, "_MaskMap");
                Vector2 tiling = material.HasProperty("_BaseColorMap")
                    ? material.GetTextureScale("_BaseColorMap")
                    : material.HasProperty("_MainTex")
                        ? material.GetTextureScale("_MainTex")
                        : Vector2.one;
                Vector2 offset = material.HasProperty("_BaseColorMap")
                    ? material.GetTextureOffset("_BaseColorMap")
                    : material.HasProperty("_MainTex")
                        ? material.GetTextureOffset("_MainTex")
                        : Vector2.zero;
                catalog.materials.Add(new MapMaterialRecord
                {
                    name = material.name,
                    assetGuid = guid ?? string.Empty,
                    shader = material.shader != null ? material.shader.name : "<missing>",
                    baseColor = GetColor(material),
                    baseColorTexture = TextureFile(baseTexture, textureFiles),
                    normalTexture = TextureFile(normal, textureFiles),
                    maskTexture = TextureFile(mask, textureFiles),
                    tiling = tiling,
                    offset = offset,
                    usedByRecordIds = instances
                        .Where(instance => instance.Renderer.sharedMaterials.Contains(material))
                        .Select(instance => instance.Record.recordId)
                        .ToList()
                });
            }

            return catalog;
        }

        private static string ResolveExportRoot(
            IMapMigrationSettings settings,
            string fingerprint)
        {
            if (string.Equals(
                    settings.LastExportFingerprint,
                    fingerprint,
                    StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(settings.LastExportRelativePath))
            {
                string existing = MapMigrationPaths.ToAbsoluteProjectPath(
                    settings.LastExportRelativePath);
                string allowed = MapMigrationPaths.ToAbsoluteProjectPath(
                    settings.BlenderExportFolder);
                if (existing.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    return existing;
                }
            }

            string configuredRoot = MapMigrationPaths.ToAbsoluteProjectPath(
                settings.BlenderExportFolder);
            if (Directory.Exists(configuredRoot))
            {
                string incomplete = Directory
                    .EnumerateDirectories(configuredRoot, "*", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(path => Directory.GetLastWriteTimeUtc(path))
                    .FirstOrDefault(path => !File.Exists(
                        Path.Combine(path, "Metadata", "manifest.json")));
                if (!string.IsNullOrEmpty(incomplete))
                {
                    return incomplete;
                }

                string staleMatchingExport = Directory
                    .EnumerateDirectories(configuredRoot, "*", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(path => Directory.GetLastWriteTimeUtc(path))
                    .FirstOrDefault(path => ManifestMatchesSourceButNeedsUpgrade(path, fingerprint));
                if (!string.IsNullOrEmpty(staleMatchingExport))
                {
                    return staleMatchingExport;
                }
            }

            string timestamp = DateTime.UtcNow.ToString(
                "yyyyMMdd_HHmmss_fff'Z'",
                CultureInfo.InvariantCulture);
            return Path.Combine(
                MapMigrationPaths.ToAbsoluteProjectPath(settings.BlenderExportFolder),
                timestamp);
        }

        private static bool ManifestMatchesSourceButNeedsUpgrade(
            string directory,
            string fingerprint)
        {
            string path = Path.Combine(directory, "Metadata", "manifest.json");
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                MapExportManifest manifest = JsonUtility.FromJson<MapExportManifest>(
                    File.ReadAllText(path, Encoding.UTF8));
                return manifest != null &&
                       string.Equals(
                           manifest.sourceFingerprint,
                           fingerprint,
                           StringComparison.Ordinal) &&
                       !string.Equals(
                           manifest.exporterVersion,
                           ExporterVersion,
                           StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void RecreateExportRoot(string configuredRoot, string exportRoot)
        {
            string allowed = Path.GetFullPath(
                MapMigrationPaths.ToAbsoluteProjectPath(configuredRoot));
            string target = Path.GetFullPath(exportRoot);
            if (!target.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Resolved export target escaped its configured root.");
            }

            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }

            Directory.CreateDirectory(target);
        }

        private static void CreateStructure(string root)
        {
            foreach (string path in new[]
                     {
                         "Scene", "Objects/Ground", "Objects/Roads",
                         "Objects/Buildings", "Objects/Vegetation", "Objects/Utility",
                         "Objects/Props", "Objects/Water", "Objects/Residual",
                         "Objects/Ambiguous", "Objects/Technical", "Textures",
                         "Materials", "Metadata", "Blender"
                     })
            {
                Directory.CreateDirectory(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
            }
        }

        private static IEnumerable<Material> CollectMaterials(
            IEnumerable<ScannedMeshInstance> instances) => instances
            .SelectMany(instance => instance.Renderer.sharedMaterials)
            .Where(material => material != null)
            .Distinct();

        private static string CategoryFolder(MapMeshCategory category)
        {
            return category switch
            {
                MapMeshCategory.GroundCandidate => "Ground",
                MapMeshCategory.RoadAsphalt => "Roads",
                MapMeshCategory.RoadDirtOrGravel => "Roads",
                MapMeshCategory.RoadStructure => "Roads",
                MapMeshCategory.Building => "Buildings",
                MapMeshCategory.Vegetation => "Vegetation",
                MapMeshCategory.Utility => "Utility",
                MapMeshCategory.Prop => "Props",
                MapMeshCategory.Water => "Water",
                MapMeshCategory.ResidualUnsupported => "Residual",
                MapMeshCategory.Ambiguous => "Ambiguous",
                _ => "Technical"
            };
        }

        private static Bounds CalculateBounds(IReadOnlyList<ScannedMeshInstance> instances)
        {
            if (instances.Count == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            Bounds bounds = instances[0].Renderer.bounds;
            for (int index = 1; index < instances.Count; index++)
            {
                bounds.Encapsulate(instances[index].Renderer.bounds);
            }

            return bounds;
        }

        private static Type FindFbxExporterType()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(
                    "UnityEditor.Formats.Fbx.Exporter.ModelExporter",
                    throwOnError: false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static bool TryExportFbx(
            string absolutePath,
            IReadOnlyList<ScannedMeshInstance> instances,
            bool worldSpace)
        {
            Type type = FindFbxExporterType();
            MethodInfo method = type?.GetMethod(
                "ExportObject",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), typeof(UnityEngine.Object) },
                modifiers: null);
            if (method == null)
            {
                return false;
            }

            var root = new GameObject("MSCMapMigration_FbxExport")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            try
            {
                foreach (ScannedMeshInstance instance in instances)
                {
                    var child = new GameObject(
                        ObjExporter.SafeName(instance.Record.gameObjectName + "_" + instance.Record.recordId));
                    child.transform.SetParent(root.transform, false);
                    if (worldSpace)
                    {
                        ApplyMatrix(child.transform, instance.LocalToWorld);
                    }

                    child.AddComponent<MeshFilter>().sharedMesh = instance.Mesh;
                    child.AddComponent<MeshRenderer>().sharedMaterials =
                        instance.Renderer.sharedMaterials;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
                object result = method.Invoke(null, new object[] { absolutePath, root });
                return result != null && File.Exists(absolutePath) &&
                       new FileInfo(absolutePath).Length > 0;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("FBX export failed: " + exception.GetBaseException().Message);
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ApplyMatrix(Transform transform, Matrix4x4 matrix)
        {
            transform.localPosition = matrix.GetColumn(3);
            transform.localRotation = matrix.rotation;
            Vector3 scale = matrix.lossyScale;
            if (matrix.determinant < 0f)
            {
                scale.x = -scale.x;
            }

            transform.localScale = scale;
        }

        private static void WriteBlenderScript(string path)
        {
            const string script = @"import bpy
from pathlib import Path

root = Path(__file__).resolve().parents[1]
fbx = root / 'Scene' / 'BaseMap_All.fbx'
obj = root / 'Scene' / 'BaseMap_All.obj'

bpy.context.scene.unit_settings.system = 'METRIC'
bpy.context.scene.unit_settings.scale_length = 1.0

before = set(bpy.data.objects)
if fbx.exists():
    bpy.ops.import_scene.fbx(filepath=str(fbx), automatic_bone_orientation=False)
elif obj.exists():
    if hasattr(bpy.ops.wm, 'obj_import'):
        bpy.ops.wm.obj_import(filepath=str(obj), forward_axis='NEGATIVE_Z', up_axis='Y')
    else:
        bpy.ops.import_scene.obj(filepath=str(obj), axis_forward='-Z', axis_up='Y')
else:
    raise FileNotFoundError('Neither BaseMap_All.fbx nor BaseMap_All.obj exists')

imported = [obj for obj in bpy.data.objects if obj not in before]
categories = ['Ground', 'Roads', 'Buildings', 'Vegetation', 'Utility',
              'Props', 'Water', 'Residual', 'Ambiguous', 'Technical']
collections = {}
for name in categories:
    collection = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if collection.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(collection)
    collections[name] = collection

for item in imported:
    prefix = item.name.split('__', 1)[0]
    mapping = {
        'GroundCandidate': 'Ground', 'RoadAsphalt': 'Roads',
        'RoadDirtOrGravel': 'Roads', 'RoadStructure': 'Roads',
        'Building': 'Buildings', 'Vegetation': 'Vegetation',
        'Utility': 'Utility', 'Prop': 'Props', 'Water': 'Water',
        'ResidualUnsupported': 'Residual', 'Ambiguous': 'Ambiguous',
        'Technical': 'Technical'
    }
    target = collections.get(mapping.get(prefix, 'Technical'))
    for collection in list(item.users_collection):
        collection.objects.unlink(item)
    target.objects.link(item)

print(f'MSC base map import complete: {len(imported)} objects, 1 Unity unit = 1 metre')
";
            File.WriteAllText(path, script.Replace("\r\n", "\n"), new UTF8Encoding(false));
        }

        private static void WriteReadme(string path, MapExportManifest manifest)
        {
            string preferred = manifest.preferredFormat == "FBX"
                ? "Scene/BaseMap_All.fbx (official Unity FBX Exporter)"
                : "Scene/BaseMap_All.obj (FBX Exporter was unavailable)";
            string text = $@"# Base map source export

- Classification: TemporaryDirectImport / private reference workflow.
- 1 Unity unit = 1 metre.
- Preferred import: `{preferred}`.
- OBJ files are written in Unity Y-up coordinates. The provided Blender script
  imports with forward `-Z`, up `Y`, producing Blender Z-up without changing scale.
- Combined roads: `Scene/Roads_All.obj`.
- Combined source ground: `Scene/Ground_All.obj`.
- Individual objects: `Objects/<Category>/` with instance transforms in
  `Metadata/manifest.json`.
- Material metadata: `Metadata/materials.json`; directly copyable image assets
  are in `Textures/`.

## Blender

Open Blender's Scripting workspace and run `Blender/import_base_map.py`, or run:

```text
blender --python Blender/import_base_map.py
```

The script prefers FBX when present and otherwise imports OBJ. World transforms
in combined exports are already baked; the script does not apply them twice.
";
            File.WriteAllText(path, text.Replace("\r\n", "\n"), new UTF8Encoding(false));
        }

        private static Texture GetTexture(Material material, params string[] properties)
        {
            foreach (string property in properties)
            {
                if (material.HasProperty(property))
                {
                    Texture texture = material.GetTexture(property);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
            }

            return null;
        }

        private static Color GetColor(Material material)
        {
            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            if (material.HasProperty("_Color"))
            {
                return material.GetColor("_Color");
            }

            return Color.white;
        }

        private static string TextureFile(
            Texture texture,
            IReadOnlyDictionary<Texture, string> textureFiles) =>
            texture != null && textureFiles.TryGetValue(texture, out string value)
                ? value
                : string.Empty;

        private static string Relative(string root, string absolute) =>
            MapMigrationPaths.NormalizeRelative(Path.GetRelativePath(root, absolute));

        private static void WriteJson(string path, string json) =>
            File.WriteAllText(path, json, new UTF8Encoding(false));
    }
}
