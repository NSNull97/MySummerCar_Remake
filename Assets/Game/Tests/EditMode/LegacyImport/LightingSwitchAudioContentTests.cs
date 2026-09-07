using System.IO;
using System.Security.Cryptography;
using MSC.LegacyImport.Editor.GameplayPresentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class LightingSwitchAudioContentTests
    {
        [Test]
        public void GeneratedLightingLibraryUsesOneExistingStableSwitchEvent()
        {
            Phase1LightingSwitchAudioImporter.EnsureGeneratedForBuild();
            Object asset = AssetDatabase.LoadMainAssetAtPath(Phase1LightingSwitchAudioImporter.LibraryPath);
            Assert.That(asset, Is.Not.Null);
            var serialized = new SerializedObject(asset);
            SerializedProperty entries = serialized.FindProperty("events");
            Assert.That(entries.arraySize, Is.EqualTo(1));
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            Assert.That(entry.FindPropertyRelative("eventId").stringValue,
                Is.EqualTo("audio.event.lighting.switch"));
            Assert.That(entry.FindPropertyRelative("category").enumValueIndex, Is.EqualTo(1));
            Assert.That(entry.FindPropertyRelative("loop").boolValue, Is.False);
            Assert.That(entry.FindPropertyRelative("maximumDistanceMeters").floatValue, Is.EqualTo(10f));
            Assert.That(entry.FindPropertyRelative("clip").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void GeneratedClipMatchesFrozenPresentationHashWithoutDonorRuntimeAccess()
        {
            Assert.That(File.Exists(Phase1LightingSwitchAudioImporter.ClipPath), Is.True,
                "Run the scoped lighting audio importer before private Phase 1 validation.");
            using var stream = File.OpenRead(Phase1LightingSwitchAudioImporter.ClipPath);
            using var sha = SHA256.Create();
            string actual = System.BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            Assert.That(actual, Is.EqualTo("03AE4856D6515FFE539DA16C2E6BE100EF18204E9F8A59F38719376FEC8BCC37"));
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Phase1LightingSwitchAudioImporter.ClipPath);
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.samples, Is.GreaterThan(0));
            var importer = (AudioImporter)AssetImporter.GetAtPath(Phase1LightingSwitchAudioImporter.ClipPath);
            Assert.That(importer.userData, Is.EqualTo("TemporaryDirectImport"));
            Assert.That(importer.forceToMono, Is.True);
            Assert.That(importer.defaultSampleSettings.preloadAudioData, Is.True);
        }
    }
}
