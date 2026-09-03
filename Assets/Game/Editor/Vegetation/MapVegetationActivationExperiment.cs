using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MSC.World.Vegetation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.Vegetation
{
    /// <summary>Private diagnostic copies only; no production activation policy or hierarchy change.</summary>
    public static class MapVegetationActivationExperiment
    {
        public const string SourceScene = MapVegetationRebuildOptions.GeneratedRoot + "/Scenes/World_Cell_1_-3_Vegetation.unity";
        public const string ExperimentRoot = MapVegetationRebuildOptions.GeneratedRoot + "/Experiments/SceneActivation";
        public const string CategoryScene = ExperimentRoot + "/Cell_1_-3_CategoryInactive.unity";
        public const string IndividualScene = ExperimentRoot + "/Cell_1_-3_IndividualInactive.unity";
        public const string ReportPath = "Artifacts/VegetationRebuild/Performance/woody-activation-experiment-fixture.json";
        private const string Version = "msc.woody-activation-experiment.v1";

        public static void PrepareBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before preparing diagnostic copies.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or close dirty scenes before this explicit experiment; no user changes will be discarded.");
            foreach (string path in new[] { SourceScene, CategoryScene, IndividualScene })
                if (SceneManager.GetSceneByPath(path).isLoaded)
                    throw new InvalidOperationException("Close the experiment/source scene before preparation: " + path);
            if (!File.Exists(SourceScene)) throw new FileNotFoundException("Generate the source vegetation scene first.", SourceScene);

            string originalHash = HashFile(SourceScene);
            var report = new FixtureReport { version = Version, utc = DateTime.UtcNow.ToString("O"), sourceScene = SourceScene,
                sourceSha256 = originalHash, categoryScene = CategoryScene, individualScene = IndividualScene };
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
                Snapshot original = Inspect(source, "active");
                report.woodyPrefabCount = original.woodyPrefabCount;
                report.woodyCategoryCount = original.woodyCategoryCount;
                report.transformPrefabFingerprint = original.transformPrefabFingerprint;
                report.grassFingerprint = original.grassFingerprint;
                report.gameObjectCount = original.gameObjectCount;

                // Single-mode opening is deliberate: an unsaved empty staging scene
                // cannot reliably accept an additive scene in Unity's test environment.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EnsureFolder(ExperimentRoot);
                foreach (string destination in new[] { CategoryScene, IndividualScene })
                {
                    if (File.Exists(destination))
                    {
                        if (!File.Exists(ReportPath)) throw new InvalidOperationException("Existing unjournaled diagnostic scene will not be overwritten: " + destination);
                        FixtureReport previous = JsonUtility.FromJson<FixtureReport>(File.ReadAllText(ReportPath));
                        if (previous == null || previous.version != Version || previous.categoryScene != CategoryScene || previous.individualScene != IndividualScene)
                            throw new InvalidDataException("The diagnostic ownership journal is invalid.");
                        string expected = destination == CategoryScene ? previous.categorySha256 : previous.individualSha256;
                        if (HashFile(destination) != expected) throw new InvalidDataException("The diagnostic scene changed after preparation: " + destination);
                        File.Copy(SourceScene, destination, true); // Preserve only this owned copy's GUID.
                        AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
                    }
                    else if (!AssetDatabase.CopyAsset(SourceScene, destination))
                        throw new IOException("Could not create diagnostic scene copy: " + destination);

                    Scene copy = EditorSceneManager.OpenScene(destination, OpenSceneMode.Single);
                    bool individual = destination == IndividualScene;
                    foreach (GameObject root in copy.GetRootGameObjects())
                    {
                        GeneratedVegetationGroup group = root.GetComponent<GeneratedVegetationGroup>();
                        if (!IsWoody(group)) continue;
                        if (individual)
                            for (int i = 0; i < root.transform.childCount; i++)
                            {
                                GameObject child = root.transform.GetChild(i).gameObject;
                                child.SetActive(false);
                                PrefabUtility.RecordPrefabInstancePropertyModifications(child);
                            }
                        else root.SetActive(false);
                    }
                    Snapshot changed = Inspect(copy, individual ? "individual-inactive" : "category-inactive");
                    RequireSamePopulation(original, changed);
                    if (!EditorSceneManager.SaveScene(copy, destination)) throw new IOException("Could not save diagnostic copy: " + destination);
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    // Validate the serialized copy, not just the in-memory edit.
                    Scene reopened = EditorSceneManager.OpenScene(destination, OpenSceneMode.Single);
                    RequireSamePopulation(original, Inspect(reopened, individual ? "individual-inactive" : "category-inactive"));
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    if (individual) report.individualSha256 = HashFile(destination);
                    else report.categorySha256 = HashFile(destination);
                }
            }
            finally
            {
                try
                {
                    bool hasRestorableActiveScene = false;
                    foreach (SceneSetup entry in setup)
                        if (entry.isLoaded && entry.isActive && !string.IsNullOrEmpty(entry.path)) hasRestorableActiveScene = true;
                    // A batch executeMethod may start without any loaded scene;
                    // Unity rejects restoring that snapshot rather than opening an empty one.
                    if (hasRestorableActiveScene) EditorSceneManager.RestoreSceneManagerSetup(setup);
                    else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                finally
                {
                    // Always verify the read-only source, even if restoration fails.
                    if (HashFile(SourceScene) != originalHash)
                        throw new InvalidDataException("The source scene changed during diagnostic preparation.");
                }
            }
            report.sourceUnchanged = true;
            report.passed = true;
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            // The explicit test owns append-only build entries through its
            // IPrebuildSetup/IPostBuildCleanup; preparation never changes them.
            Debug.Log("MAP_VEGETATION_ACTIVATION_EXPERIMENT_READY woody=" + report.woodyPrefabCount + " sourceUnchanged=true " + ReportPath);
        }

        public static void RemoveBuildEntriesBatch()
        {
            // Never restore an old global Build Settings snapshot over unrelated work.
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(scene => scene.path == CategoryScene || scene.path == IndividualScene);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public const string AlternativeReportPath = "Artifacts/VegetationRebuild/Performance/woody-alternative-experiment-fixture.json";
        private const string AlternativeVersion = "msc.woody-alternative-experiment.v1";

        public static void PrepareAlternativeBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before preparation.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Dirty scenes will not be discarded.");
            string[] destinations = { ExperimentRoot + "/Cell_1_-3_Unpacked.unity", ExperimentRoot + "/Cell_1_-3_Slice64.unity", ExperimentRoot + "/Cell_1_-3_Slice128.unity" };
            foreach (string path in destinations)
                if (SceneManager.GetSceneByPath(path).isLoaded) throw new InvalidOperationException("Close the diagnostic scene: " + path);
            if (SceneManager.GetSceneByPath(SourceScene).isLoaded) throw new InvalidOperationException("Close the read-only source scene.");
            string sourceHash = HashFile(SourceScene);
            var report = new AlternativeFixture { version = AlternativeVersion, utc = DateTime.UtcNow.ToString("O"), sourceScene = SourceScene, sourceSha256 = sourceHash,
                selection = "Deterministic round-robin across category/prefab-GUID buckets, with ordinal stable IDs inside buckets; slice64 is a subset of slice128. Full transforms and render/collider references are checked per selected ID. This is a population subset, not the full cell." };
            AlternativeFixture previous = File.Exists(AlternativeReportPath) ? JsonUtility.FromJson<AlternativeFixture>(File.ReadAllText(AlternativeReportPath)) : null;
            // Check all ownership journals before changing any existing copy.
            foreach (string path in destinations)
            {
                if (!File.Exists(path)) continue;
                AlternativeVariant owned = previous != null && previous.version == AlternativeVersion ? previous.variants.Find(v => v.scenePath == path) : null;
                if (owned == null || HashFile(path) != owned.sha256) throw new InvalidDataException("Refusing changed/unjournaled diagnostic copy: " + path);
            }
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                AlternativePopulation source = ReadAlternativePopulation(EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single));
                if (source.records.Count < 128 || source.connectedPrefabs != source.records.Count) throw new InvalidDataException("Expected at least 128 source prefab instances.");
                report.source = DescribeAlternative("active", SourceScene, sourceHash, source);
                List<string> selection = SelectRepresentative(source, 128);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EnsureFolder(ExperimentRoot);
                for (int variantIndex = 0; variantIndex < destinations.Length; variantIndex++)
                {
                    string destination = destinations[variantIndex];
                    bool unpack = variantIndex == 0;
                    int selectedCount = unpack ? source.records.Count : variantIndex == 1 ? 64 : 128;
                    var selected = unpack ? new HashSet<string>(source.records.Keys, StringComparer.Ordinal) : new HashSet<string>(selection.GetRange(0, selectedCount), StringComparer.Ordinal);
                    if (File.Exists(destination)) { File.Copy(SourceScene, destination, true); AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate); }
                    else if (!AssetDatabase.CopyAsset(SourceScene, destination)) throw new IOException("Could not copy " + destination);
                    Scene copy = EditorSceneManager.OpenScene(destination, OpenSceneMode.Single);
                    foreach (GameObject root in copy.GetRootGameObjects())
                    {
                        GeneratedVegetationGroup group = root.GetComponent<GeneratedVegetationGroup>();
                        if (!IsWoody(group)) continue;
                        for (int i = root.transform.childCount - 1; i >= 0; i--)
                        {
                            GameObject child = root.transform.GetChild(i).gameObject;
                            string key = group.Category + "/" + child.name;
                            if (!selected.Contains(key)) UnityEngine.Object.DestroyImmediate(child);
                            else if (unpack) PrefabUtility.UnpackPrefabInstance(child, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                        }
                    }
                    ValidateAlternative(source, ReadAlternativePopulation(copy), selected, unpack);
                    if (!EditorSceneManager.SaveScene(copy, destination)) throw new IOException("Could not save " + destination);
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    AlternativePopulation reopened = ReadAlternativePopulation(EditorSceneManager.OpenScene(destination, OpenSceneMode.Single));
                    ValidateAlternative(source, reopened, selected, unpack);
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    report.variants.Add(DescribeAlternative(unpack ? "full-unpacked" : "slice-" + selectedCount, destination, HashFile(destination), reopened));
                }
            }
            finally
            {
                try
                {
                    bool restore = false;
                    foreach (SceneSetup entry in setup) if (entry.isLoaded && entry.isActive && !string.IsNullOrEmpty(entry.path)) restore = true;
                    if (restore) EditorSceneManager.RestoreSceneManagerSetup(setup);
                    else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                finally { if (HashFile(SourceScene) != sourceHash) throw new InvalidDataException("Read-only source scene changed."); }
            }
            report.passed = true;
            Directory.CreateDirectory(Path.GetDirectoryName(AlternativeReportPath));
            File.WriteAllText(AlternativeReportPath, JsonUtility.ToJson(report, true));
            Debug.Log("MAP_VEGETATION_ALTERNATIVE_EXPERIMENT_READY source=" + report.source.woodyPrefabCount + " copies=unpacked/64/128");
        }

        private static AlternativePopulation ReadAlternativePopulation(Scene scene)
        {
            var result = new AlternativePopulation();
            var categories = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GeneratedVegetationGroup group = root.GetComponent<GeneratedVegetationGroup>();
                if (group == null || group.GeneratorId != MapVegetationRebuildOptions.GeneratorId || group.CellId != "cell_1_-3" || !categories.Add(group.Category) || !root.activeSelf)
                    throw new InvalidDataException("Unexpected source/copy ownership or inactive category.");
                result.gameObjects += root.GetComponentsInChildren<Transform>(true).Length;
                result.renderers += root.GetComponentsInChildren<Renderer>(true).Length;
                result.colliders += root.GetComponentsInChildren<Collider>(true).Length;
                string groupState = root.name + "|" + group.Fingerprint + "|" + JsonUtility.ToJson(new TransformProof(root.transform));
                result.groupStates.Add(group.Category, groupState);
                if (group.Category == "GrassCoverage")
                {
                    result.grassFingerprint = PresentationFingerprint(root.transform);
                    continue;
                }
                if (!IsWoody(group)) throw new InvalidDataException("Unexpected category.");
                for (int i = 0; i < root.transform.childCount; i++)
                {
                    GameObject child = root.transform.GetChild(i).gameObject;
                    if (!child.activeSelf) throw new InvalidDataException("Alternative probe must keep every selected prefab active.");
                    GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(child);
                    if (prefab != null) result.connectedPrefabs++;
                    string key = group.Category + "/" + child.name;
                    result.records.Add(key, new AlternativeRecord { key = key, prefabGuid = prefab == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)),
                        bucket = group.Category + "/" + (prefab == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab))), fingerprint = PresentationFingerprint(child.transform) });
                }
            }
            if (categories.Count != 4 || string.IsNullOrEmpty(result.grassFingerprint)) throw new InvalidDataException("Expected four category roots including unchanged grass.");
            return result;
        }

        private static string PresentationFingerprint(Transform root)
        {
            using var bytes = new MemoryStream();
            using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
            int ignored = 0;
            WriteHierarchy(writer, root, true, ref ignored);
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            {
                writer.Write(node.gameObject.layer); writer.Write((int)GameObjectUtility.GetStaticEditorFlags(node.gameObject));
                MeshFilter filter = node.GetComponent<MeshFilter>();
                writer.Write(filter != null); if (filter != null) WriteAssetIdentity(writer, filter.sharedMesh);
                foreach (Renderer renderer in node.GetComponents<Renderer>())
                {
                    writer.Write(renderer.enabled); writer.Write((int)renderer.shadowCastingMode); writer.Write(renderer.receiveShadows);
                    writer.Write(renderer.sharedMaterials.Length);
                    foreach (Material material in renderer.sharedMaterials) WriteAssetIdentity(writer, material);
                    if (renderer is SkinnedMeshRenderer skinned) WriteAssetIdentity(writer, skinned.sharedMesh);
                }
                foreach (Collider collider in node.GetComponents<Collider>())
                {
                    writer.Write(collider.enabled); writer.Write(collider.isTrigger); writer.Write(collider.contactOffset); WriteAssetIdentity(writer, collider.sharedMaterial);
                    if (collider is BoxCollider box) { WriteVector(writer, box.center); WriteVector(writer, box.size); }
                    else if (collider is CapsuleCollider capsule) { WriteVector(writer, capsule.center); writer.Write(capsule.radius); writer.Write(capsule.height); writer.Write(capsule.direction); }
                    else if (collider is SphereCollider sphere) { WriteVector(writer, sphere.center); writer.Write(sphere.radius); }
                    else if (collider is MeshCollider mesh) { WriteAssetIdentity(writer, mesh.sharedMesh); writer.Write(mesh.convex); writer.Write((int)mesh.cookingOptions); }
                    else throw new InvalidDataException("Unsupported collider proof: " + collider.GetType().Name);
                }
                LODGroup lodGroup = node.GetComponent<LODGroup>();
                if (lodGroup != null)
                {
                    writer.Write(lodGroup.enabled); writer.Write(lodGroup.size); WriteVector(writer, lodGroup.localReferencePoint);
                    writer.Write((int)lodGroup.fadeMode); writer.Write(lodGroup.animateCrossFading);
                    LOD[] lods = lodGroup.GetLODs(); writer.Write(lods.Length);
                    foreach (LOD lod in lods)
                    {
                        writer.Write(lod.screenRelativeTransitionHeight); writer.Write(lod.fadeTransitionWidth); writer.Write(lod.renderers.Length);
                        foreach (Renderer renderer in lod.renderers)
                            writer.Write(renderer == null ? "null" : AnimationUtility.CalculateTransformPath(renderer.transform, root));
                    }
                }
                VegetationWorldRenderer grass = node.GetComponent<VegetationWorldRenderer>();
                if (grass != null)
                {
                    WriteAssetIdentity(writer, grass.Catalog);
                    var serialized = new SerializedObject(grass);
                    writer.Write(serialized.FindProperty("renderInGameView").boolValue); writer.Write(serialized.FindProperty("renderInSceneView").boolValue);
                    writer.Write(serialized.FindProperty("renderingLayerMask").intValue);
                }
            }
            writer.Flush(); return Hash(bytes.ToArray());
        }

        private static void WriteAssetIdentity(BinaryWriter writer, UnityEngine.Object asset)
        {
            if (asset == null) { writer.Write(""); writer.Write(0L); return; }
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id)) throw new InvalidDataException("Expected persistent render/collider asset: " + asset.name);
            writer.Write(guid); writer.Write(id);
        }

        private static void WriteVector(BinaryWriter writer, Vector3 value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }

        private static List<string> SelectRepresentative(AlternativePopulation source, int count)
        {
            var buckets = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (AlternativeRecord record in source.records.Values)
            {
                if (!buckets.TryGetValue(record.bucket, out List<string> entries)) buckets.Add(record.bucket, entries = new List<string>());
                entries.Add(record.key);
            }
            foreach (List<string> entries in buckets.Values) entries.Sort(StringComparer.Ordinal);
            var selected = new List<string>();
            for (int index = 0; selected.Count < count; index++)
                foreach (List<string> entries in buckets.Values)
                {
                    if (index < entries.Count) selected.Add(entries[index]);
                    if (selected.Count == count) break;
                }
            return selected;
        }

        private static void ValidateAlternative(AlternativePopulation source, AlternativePopulation copy, HashSet<string> selected, bool unpack)
        {
            if (copy.records.Count != selected.Count || copy.grassFingerprint != source.grassFingerprint || copy.connectedPrefabs != (unpack ? 0 : selected.Count))
                throw new InvalidDataException("Alternative copy changed its selected population, prefab status or grass.");
            foreach (var group in source.groupStates)
                if (!copy.groupStates.TryGetValue(group.Key, out string state) || state != group.Value) throw new InvalidDataException("Category transform/metadata changed.");
            foreach (string key in selected)
            {
                if (!copy.records.TryGetValue(key, out AlternativeRecord record) || record.fingerprint != source.records[key].fingerprint ||
                    (!unpack && record.prefabGuid != source.records[key].prefabGuid))
                    throw new InvalidDataException("Transform/render/collider/LOD references changed: " + key);
            }
        }

        private static AlternativeVariant DescribeAlternative(string name, string path, string sha, AlternativePopulation population)
        {
            var records = new List<AlternativeRecord>(population.records.Values); records.Sort((a, b) => StringComparer.Ordinal.Compare(a.key, b.key));
            return new AlternativeVariant { name = name, scenePath = path, sha256 = sha, woodyPrefabCount = records.Count,
                connectedPrefabCount = population.connectedPrefabs, gameObjects = population.gameObjects, renderers = population.renderers, colliders = population.colliders,
                grassFingerprint = population.grassFingerprint, selected = records };
        }

        [Serializable] private sealed class TransformProof
        {
            public Vector3 position, scale; public Quaternion rotation;
            public TransformProof(Transform value) { position = value.localPosition; rotation = value.localRotation; scale = value.localScale; }
        }
        private sealed class AlternativePopulation
        {
            public int connectedPrefabs, gameObjects, renderers, colliders; public string grassFingerprint;
            public Dictionary<string, AlternativeRecord> records = new Dictionary<string, AlternativeRecord>(StringComparer.Ordinal);
            public Dictionary<string, string> groupStates = new Dictionary<string, string>(StringComparer.Ordinal);
        }
        [Serializable] private sealed class AlternativeRecord { public string key, prefabGuid, bucket, fingerprint; }
        [Serializable] private sealed class AlternativeVariant
        {
            public string name, scenePath, sha256, grassFingerprint;
            public int woodyPrefabCount, connectedPrefabCount, gameObjects, renderers, colliders;
            public List<AlternativeRecord> selected;
        }
        [Serializable] private sealed class AlternativeFixture
        {
            public string version, utc, sourceScene, sourceSha256, selection; public bool passed;
            public AlternativeVariant source;
            public List<AlternativeVariant> variants = new List<AlternativeVariant>();
        }


        private static Snapshot Inspect(Scene scene, string mode)
        {
            var result = new Snapshot();
            var categories = new HashSet<string>(StringComparer.Ordinal);
            using var bytes = new MemoryStream();
            using var writer = new BinaryWriter(bytes, Encoding.UTF8, true);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GeneratedVegetationGroup group = root.GetComponent<GeneratedVegetationGroup>();
                if (group == null || group.GeneratorId != MapVegetationRebuildOptions.GeneratorId || group.CellId != "cell_1_-3" || !categories.Add(group.Category))
                    throw new InvalidDataException("Unexpected source/copy ownership or duplicate category.");
                writer.Write(group.Category); writer.Write(group.Fingerprint ?? "");
                WriteHierarchy(writer, root.transform, false, ref result.gameObjectCount);
                if (IsWoody(group))
                {
                    result.woodyCategoryCount++;
                    if (root.activeSelf != (mode != "category-inactive")) throw new InvalidDataException("Unexpected category activation state.");
                    for (int i = 0; i < root.transform.childCount; i++)
                    {
                        GameObject child = root.transform.GetChild(i).gameObject;
                        if (child.activeSelf != (mode != "individual-inactive")) throw new InvalidDataException("Unexpected direct prefab activation state.");
                        GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(child);
                        if (prefab == null) throw new InvalidDataException("A woody direct child has no source prefab: " + child.name);
                        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab, out string guid, out long localId))
                            throw new InvalidDataException("A woody prefab identity could not be read.");
                        writer.Write(child.name); writer.Write(guid); writer.Write(localId);
                        result.woodyPrefabCount++;
                    }
                }
                else if (group.Category == "GrassCoverage")
                {
                    if (!root.activeSelf) throw new InvalidDataException("Grass must remain active.");
                    using var grassBytes = new MemoryStream();
                    using var grassWriter = new BinaryWriter(grassBytes, Encoding.UTF8, true);
                    int ignored = 0;
                    WriteHierarchy(grassWriter, root.transform, true, ref ignored);
                    foreach (VegetationWorldRenderer renderer in root.GetComponentsInChildren<VegetationWorldRenderer>(true))
                    {
                        // Serialized object references must stay identical between scene copies.
                        var serialized = new SerializedObject(renderer);
                        UnityEngine.Object catalog = serialized.FindProperty("catalog").objectReferenceValue;
                        if (catalog == null) throw new InvalidDataException("The copied grass renderer lost its catalog.");
                        grassWriter.Write(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(catalog)));
                    }
                    grassWriter.Flush(); result.grassFingerprint = Hash(grassBytes.ToArray());
                }
                else throw new InvalidDataException("Unexpected generated category: " + group.Category);
            }
            if (categories.Count != 4 || result.woodyCategoryCount != 3 || result.woodyPrefabCount == 0 || string.IsNullOrEmpty(result.grassFingerprint))
                throw new InvalidDataException("The experiment requires the full saved four-category population.");
            writer.Flush(); result.transformPrefabFingerprint = Hash(bytes.ToArray());
            return result;
        }

        private static void WriteHierarchy(BinaryWriter writer, Transform transform, bool active, ref int count)
        {
            count++;
            writer.Write(transform.name); writer.Write(transform.childCount);
            Vector3 p = transform.localPosition, s = transform.localScale;
            Quaternion r = transform.localRotation;
            writer.Write(p.x); writer.Write(p.y); writer.Write(p.z);
            writer.Write(r.x); writer.Write(r.y); writer.Write(r.z); writer.Write(r.w);
            writer.Write(s.x); writer.Write(s.y); writer.Write(s.z);
            if (active) writer.Write(transform.gameObject.activeSelf);
            foreach (Component component in transform.GetComponents<Component>())
                writer.Write(component == null ? "MissingScript" : component.GetType().FullName);
            for (int i = 0; i < transform.childCount; i++) WriteHierarchy(writer, transform.GetChild(i), active, ref count);
        }

        private static bool IsWoody(GeneratedVegetationGroup group) => group != null && group.GeneratorId == MapVegetationRebuildOptions.GeneratorId &&
            (group.Category == "OriginalTrees" || group.Category == "BoundaryForest" || group.Category == "ShrubsAndUndergrowth");

        private static void RequireSamePopulation(Snapshot source, Snapshot copy)
        {
            if (source.woodyPrefabCount != copy.woodyPrefabCount || source.gameObjectCount != copy.gameObjectCount ||
                source.transformPrefabFingerprint != copy.transformPrefabFingerprint || source.grassFingerprint != copy.grassFingerprint)
                throw new InvalidDataException("The diagnostic copy changed transforms, hierarchy, prefab references or grass.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string HashFile(string path) { using var stream = File.OpenRead(path); using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        private static string Hash(byte[] bytes) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }

        private sealed class Snapshot
        {
            public int woodyPrefabCount, woodyCategoryCount, gameObjectCount;
            public string transformPrefabFingerprint, grassFingerprint;
        }

        [Serializable]
        private sealed class FixtureReport
        {
            public string version, utc, sourceScene, sourceSha256, categoryScene, categorySha256, individualScene, individualSha256;
            public string transformPrefabFingerprint, grassFingerprint;
            public int woodyPrefabCount, woodyCategoryCount, gameObjectCount;
            public bool sourceUnchanged, passed;
        }
    }
}
