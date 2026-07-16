using System.Collections;
using System.Linq;
using MSC.LegacyImport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace MSC.Tests.PlayMode.WorldBaseline
{
    [Category("LocalDonorBaseline")]
    public sealed class DonorWorldBaselinePlayModeTests
    {
        private const string CanonicalScene =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Scenes/" +
            "World_DonorBaseline_Canonical.unity";

        [UnityTest]
        public IEnumerator CanonicalSanitizedBaseline_BootsWithoutDonorRuntime()
        {
#if UNITY_EDITOR
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    CanonicalScene) == null)
            {
                Assert.Ignore(
                    "The generated local donor RuntimeBaseline scene is " +
                    "intentionally absent from a clean clone.");
                yield break;
            }

            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                CanonicalScene,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(
                load,
                Is.Not.Null,
                "Canonical baseline cannot be loaded by asset path.");
            yield return load;
            yield return null;

            DonorWorldBaselineSceneMetadata stamp =
                Object.FindObjectsByType<DonorWorldBaselineSceneMetadata>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Single();
            Assert.That(
                stamp.Classification,
                Is.EqualTo(
                    DonorWorldBaselineClassification.TemporaryDirectImport));
            Assert.That(
                stamp.ActivationState,
                Is.EqualTo(
                    DonorWorldBaselineActivationState
                        .PreparedNotActiveUntil06B2));
            Assert.That(stamp.SourceEntityCount, Is.EqualTo(3842));
            Assert.That(stamp.RendererEntityCount, Is.EqualTo(2605));
            Assert.That(stamp.MetadataOnlyEntityCount, Is.EqualTo(1237));

            Assert.That(
                Object.FindObjectsByType<DonorWorldBaselineEntityMetadata>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length,
                Is.EqualTo(3842));
            Assert.That(
                Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Is.Empty);
            Assert.That(
                Object.FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Is.Empty);
            Assert.That(
                Object.FindObjectsByType<AudioSource>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Is.Empty);
            Assert.That(
                Object.FindObjectsByType<Collider>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Is.Empty);
            Assert.That(
                Object.FindObjectsByType<Light>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Length,
                Is.EqualTo(1));
#else
            Assert.Ignore(
                "The local donor baseline is intentionally excluded from " +
                "distributable Build Settings.");
            yield break;
#endif
        }
    }
}
