using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using MSC.Editor.WorldBaseline;
using MSC.World.Streaming;
using MSC.World.Vegetation.Diagnostics;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    /// <summary>Builds only the isolated private diagnostic; never changes the active Build Settings list.</summary>
    public static class MapVegetationPlayerProbeBuild
    {
        private const string Folder = MapVegetationActivationExperiment.ExperimentRoot;
        private const string Bootstrap = Folder + "/PlayerProbeBootstrap.unity";
        private const string ReportFolder = "Artifacts/VegetationRebuild/Performance";
        private const string OwnershipPath = ReportFolder + "/player-probe-bootstrap-owner.json";
        private const string FixturePath = ReportFolder + "/woody-alternative-experiment-fixture.json";
        private const string Version = "msc.vegetation-player-probe.v1";

        public static void BuildBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before building the diagnostic.");
            if (Environment.GetEnvironmentVariable(DonorRuntimeBaselineBuildGuard.PrivateBuildEnvironmentVariable) != "1")
                throw new BuildFailedException("This authorized private Development diagnostic still requires MSC_PRIVATE_DONOR_BASELINE_BUILD=1 in the build process environment.");
            ProductionWorldStreamingManifest manifest = AssetDatabase.LoadAssetAtPath<ProductionWorldStreamingManifest>(WorldBaseline06B2Paths.ActiveManifest);
            if (manifest == null || !manifest.PrivateLocalRuntimeBaseline)
                throw new BuildFailedException("The active manifest must explicitly allow the private local baseline; the diagnostic does not change that setting.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Dirty scenes will not be discarded.");
            if (UnityEngine.SceneManagement.SceneManager.GetSceneByPath(Bootstrap).isLoaded) throw new InvalidOperationException("Close the diagnostic bootstrap before building.");
            Fixture fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(FixturePath));
            if (fixture == null || !fixture.passed || fixture.version != "msc.woody-alternative-experiment.v1" || fixture.variants.Length != 3)
                throw new InvalidDataException("Prepare and validate the alternative scene fixtures first.");
            string[] expected = { MapVegetationActivationExperiment.SourceScene, Folder + "/Cell_1_-3_Unpacked.unity", Folder + "/Cell_1_-3_Slice64.unity", Folder + "/Cell_1_-3_Slice128.unity" };
            var variants = new List<Variant> { fixture.source }; variants.AddRange(fixture.variants);
            var descriptor = new Descriptor { version = Version, buildUtc = DateTime.UtcNow.ToString("O"), fixtureSha256 = HashFile(FixturePath),
                sourceSceneSha256 = fixture.sourceSha256, privateDevelopmentBuild = true,
                scriptingBackend = PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone).ToString(), scenes = new SceneDescriptor[4] };
            for (int i = 0; i < variants.Count; i++)
            {
                Variant variant = variants[i];
                if (variant.scenePath != expected[i] || HashFile(variant.scenePath) != variant.sha256 || variant.selected.Length != variant.woodyPrefabCount)
                    throw new InvalidDataException("Fixture source/hash/identity count mismatch: " + expected[i]);
                if (i == 1 && variant.woodyPrefabCount != variants[0].woodyPrefabCount || i == 2 && variant.woodyPrefabCount != 64 || i == 3 && variant.woodyPrefabCount != 128)
                    throw new InvalidDataException("Unexpected diagnostic population size.");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                var selectedIds = new string[variant.selected.Length];
                for (int j = 0; j < selectedIds.Length; j++) { selectedIds[j] = variant.selected[j].key; if (!ids.Add(selectedIds[j])) throw new InvalidDataException("Duplicate fixture identity."); }
                ValidateDependencies(variant.scenePath);
                descriptor.scenes[i] = new SceneDescriptor { name = variant.name, scenePath = variant.scenePath, sourceSha256 = variant.sha256,
                    buildIndex = i + 1, woodyPrefabCount = variant.woodyPrefabCount, gameObjects = variant.gameObjects, renderers = variant.renderers, colliders = variant.colliders,
                    dependencyHash = AssetDatabase.GetAssetDependencyHash(variant.scenePath).ToString(), selectedIds = selectedIds };
            }
            if (fixture.sourceScene != expected[0] || descriptor.sourceSceneSha256 != variants[0].sha256) throw new InvalidDataException("Source provenance mismatch.");
            string output = "Builds/VegetationSceneProbe/VegetationSceneProbe.exe";
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++) if (arguments[i] == "-msc-vegetation-build-output") output = arguments[++i];
            output = Path.GetFullPath(output);
            if (!string.Equals(Path.GetExtension(output), ".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Diagnostic output must be a Windows executable path.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            if (File.Exists(Bootstrap))
            {
                Owner previous = File.Exists(OwnershipPath) ? JsonUtility.FromJson<Owner>(File.ReadAllText(OwnershipPath)) : null;
                if (previous == null || previous.version != Version || previous.path != Bootstrap || previous.sha256 != HashFile(Bootstrap))
                    throw new InvalidDataException("Refusing to overwrite a changed/unjournaled diagnostic bootstrap.");
            }
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            string buildSettingsHash = HashFile("ProjectSettings/EditorBuildSettings.asset");
            var result = new BuildResultRecord { descriptor = descriptor, output = output, startedUtc = DateTime.UtcNow.ToString("O"), privateGuardEnabled = true };
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var owner = new GameObject("Private vegetation player diagnostic");
                VegetationScenePlayerProbe probe = owner.AddComponent<VegetationScenePlayerProbe>();
                var serialized = new SerializedObject(probe); serialized.FindProperty("descriptorJson").stringValue = JsonUtility.ToJson(descriptor); serialized.ApplyModifiedPropertiesWithoutUndo();
                if (!EditorSceneManager.SaveScene(scene, Bootstrap)) throw new IOException("Could not save the owned diagnostic bootstrap.");
                Directory.CreateDirectory(ReportFolder);
                File.WriteAllText(OwnershipPath, JsonUtility.ToJson(new Owner { version = Version, path = Bootstrap, sha256 = HashFile(Bootstrap) }, true));
                string[] scenes = { Bootstrap, expected[0], expected[1], expected[2], expected[3] };
                ValidateDependencies(Bootstrap);
                result.scenes = scenes;
                using (DonorRuntimeBaselineBuildGuard.BeginExplicitSceneBuild(scenes))
                {
                    // All existing build callbacks remain enabled. This scope
                    // supplies only the explicit scene list to the existing guard.
                    BuildReport build = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, target = BuildTarget.StandaloneWindows64,
                        locationPathName = output, options = BuildOptions.Development });
                    result.buildResult = build.summary.result.ToString(); result.bytes = build.summary.totalSize; result.durationSeconds = build.summary.totalTime.TotalSeconds;
                    if (build.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Diagnostic player build failed: " + build.summary.result);
                }
                ValidateSourcesUnchanged(descriptor);
                result.passed = true;
            }
            catch (Exception exception) { result.failure = exception.ToString(); throw; }
            finally
            {
                try
                {
                    bool restore = false;
                    foreach (SceneSetup entry in setup) if (entry.isLoaded && entry.isActive && !string.IsNullOrEmpty(entry.path)) restore = true;
                    if (restore) EditorSceneManager.RestoreSceneManagerSetup(setup); else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                catch (Exception exception)
                {
                    result.passed = false;
                    result.failure += "\nScene restoration: " + exception;
                    throw;
                }
                finally
                {
                    try
                    {
                        ValidateSourcesUnchanged(descriptor);
                        if (HashFile("ProjectSettings/EditorBuildSettings.asset") != buildSettingsHash) throw new InvalidDataException("Build Settings changed during the diagnostic build.");
                        result.sourceAndBuildSettingsUnchanged = true;
                    }
                    catch (Exception exception) { result.passed = false; result.failure += "\n" + exception; throw; }
                    finally { Directory.CreateDirectory(ReportFolder); File.WriteAllText(ReportFolder + "/player-probe-build.json", JsonUtility.ToJson(result, true)); }
                }
            }
            Debug.Log("MSC_VEGETATION_PLAYER_PROBE_BUILD_OK scenes=5 path=" + output);
        }

        private static void ValidateSourcesUnchanged(Descriptor descriptor)
        {
            if (HashFile(FixturePath) != descriptor.fixtureSha256) throw new InvalidDataException("Fixture journal changed during build.");
            foreach (SceneDescriptor scene in descriptor.scenes)
                if (HashFile(scene.scenePath) != scene.sourceSha256 || AssetDatabase.GetAssetDependencyHash(scene.scenePath).ToString() != scene.dependencyHash)
                    throw new InvalidDataException("Source scene or dependency changed during build: " + scene.scenePath);
        }
        private static void ValidateDependencies(string scene)
        {
            foreach (string path in AssetDatabase.GetDependencies(scene, true))
                if (path.StartsWith("Assets/Game/LegacyImport/ReferenceOnly/", StringComparison.Ordinal) || path.StartsWith("Assets/Game/Imported/DonorGenerated/", StringComparison.Ordinal))
                    throw new BuildFailedException("Forbidden reference-only/raw donor dependency in diagnostic build: " + path);
        }
        private static string HashFile(string path) { using var stream = File.OpenRead(path); using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        [Serializable] private sealed class Fixture { public string version, sourceScene, sourceSha256; public bool passed; public Variant source; public Variant[] variants; }
        [Serializable] private sealed class Variant { public string name, scenePath, sha256; public int woodyPrefabCount, gameObjects, renderers, colliders; public Selection[] selected; }
        [Serializable] private sealed class Selection { public string key; }
        [Serializable] private sealed class Descriptor
        {
            public string version, buildUtc, fixtureSha256, sourceSceneSha256, scriptingBackend; public bool privateDevelopmentBuild; public SceneDescriptor[] scenes;
        }
        [Serializable] private sealed class SceneDescriptor
        {
            public string name, scenePath, sourceSha256, dependencyHash; public int buildIndex, woodyPrefabCount, gameObjects, renderers, colliders; public string[] selectedIds;
        }
        [Serializable] private sealed class Owner { public string version, path, sha256; }
        [Serializable] private sealed class BuildResultRecord
        {
            public Descriptor descriptor; public string output, startedUtc, buildResult, failure; public string[] scenes;
            public bool passed, privateGuardEnabled, sourceAndBuildSettingsUnchanged; public ulong bytes; public double durationSeconds;
        }
    }
}
