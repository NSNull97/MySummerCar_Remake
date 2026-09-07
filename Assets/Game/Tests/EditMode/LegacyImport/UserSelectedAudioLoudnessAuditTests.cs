using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Audio;
using MSC.Audio.UnityFallback;
using MSC.LegacyImport.Editor.GameplayPresentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.LegacyImport
{
    /// <summary>
    /// Private diagnostic, not a perceived-loudness or listening-test assertion.
    /// Decode actual Unity imports on temporary copies; never reimport/change
    /// the selected or stock media. Readback WAVs stay in ignored local Logs.
    /// </summary>
    public sealed class UserSelectedAudioLoudnessAuditTests
    {
        [Test]
        public void MeasureActualImportedStockAndReplacementPcmWithoutChangingEitherLibrary()
        {
            const string root = Phase1UserSelectedAudioImporter.OutputRoot;
            var replacement = AssetDatabase.LoadAssetAtPath<UnityAudioEventLibrary>(Phase1UserSelectedAudioImporter.LibraryPath);
            if (replacement == null) Assert.Ignore("This private diagnostic requires the explicitly selected user audio pack.");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Phase1UserSelectedAudioImporter.ManifestPath));
            Assert.That(manifest.mappings, Has.Length.EqualTo(26));
            string folderName = "Audit-" + Guid.NewGuid().ToString("N");
            string temporaryRoot = root + "/" + folderName;
            string folderGuid = AssetDatabase.CreateFolder(root, folderName);
            Assert.That(folderGuid, Is.Not.Empty);
            string output = "Logs/user-audio-loudness-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(output);
            var unchanged = new Dictionary<string, string>(StringComparer.Ordinal);
            var measured = new Dictionary<string, ClipReport>(StringComparer.Ordinal);
            var entries = new List<Entry>();
            void Protect(string path)
            {
                if (!unchanged.ContainsKey(path)) unchanged.Add(path, Hash(path));
                if (File.Exists(path + ".meta") && !unchanged.ContainsKey(path + ".meta"))
                    unchanged.Add(path + ".meta", Hash(path + ".meta"));
            }
            try
            {
                Protect(Phase1UserSelectedAudioImporter.LibraryPath);
                Protect(Phase1UserSelectedAudioImporter.ManifestPath);
                ClipReport Measure(AudioClip original)
                {
                    string originalPath = AssetDatabase.GetAssetPath(original);
                    if (measured.TryGetValue(originalPath, out ClipReport cached)) return cached;
                    Protect(originalPath);
                    string copy = temporaryRoot + "/clip-" + measured.Count.ToString("000") + Path.GetExtension(originalPath);
                    Assert.That(AssetDatabase.CopyAsset(originalPath, copy), Is.True, originalPath);
                    var importer = AssetImporter.GetAtPath(copy) as AudioImporter;
                    Assert.That(importer, Is.Not.Null);
                    AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                    // Only storage/load policy changes. Preserve mono normalization,
                    // codec, quality and sample rate from the actual source importer.
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    importer.defaultSampleSettings = settings;
                    if (importer.ContainsSampleSettingsOverride("Standalone"))
                    {
                        settings = importer.GetOverrideSampleSettings("Standalone");
                        settings.loadType = AudioClipLoadType.DecompressOnLoad;
                        Assert.That(importer.SetOverrideSampleSettings("Standalone", settings), Is.True);
                    }
                    importer.loadInBackground = false;
                    importer.SaveAndReimport();
                    var decoded = AssetDatabase.LoadAssetAtPath<AudioClip>(copy);
                    Assert.That(decoded, Is.Not.Null);
                    Assert.That(decoded.LoadAudioData(), Is.True);
                    Assert.That(decoded.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
                    var interleaved = new float[checked(decoded.samples * decoded.channels)];
                    Assert.That(decoded.GetData(interleaved.AsSpan(), 0), Is.True, originalPath);
                    var mono = new float[decoded.samples];
                    int windowLength = Mathf.Max(1, decoded.frequency / 10);
                    var windowEnergies = new List<double>();
                    double totalEnergy = 0d, windowEnergy = 0d, peak = 0d;
                    int windowSamples = 0;
                    for (int frame = 0; frame < mono.Length; frame++)
                    {
                        double value = 0d;
                        for (int channel = 0; channel < decoded.channels; channel++)
                            value += interleaved[frame * decoded.channels + channel] / decoded.channels;
                        Assert.That(double.IsFinite(value), Is.True);
                        mono[frame] = (float)value;
                        peak = Math.Max(peak, Math.Abs(value));
                        double energy = value * value; totalEnergy += energy; windowEnergy += energy;
                        if (++windowSamples == windowLength || frame == mono.Length - 1)
                        { windowEnergies.Add(windowEnergy / windowSamples); windowEnergy = 0d; windowSamples = 0; }
                    }
                    double maximumWindowEnergy = windowEnergies.Max();
                    double[] active = windowEnergies.Where(value => value >= maximumWindowEnergy * .01d).ToArray();
                    string readback = output + "/decoded-" + measured.Count.ToString("000") + ".wav";
                    WriteFloatWave(readback, mono, decoded.frequency);
                    var result = new ClipReport
                    {
                        assetPath = originalPath, sourceHash = unchanged[originalPath],
                        importedChannels = decoded.channels, sampleRate = decoded.frequency,
                        seconds = decoded.length, monoRmsDbfs = Db(totalEnergy / mono.Length),
                        monoSamplePeakDbfs = Db(peak * peak), maximum100msRmsDbfs = Db(maximumWindowEnergy),
                        active100msRmsDbfs = Db(active.Average()), activeWindowFraction = (float)active.Length / windowEnergies.Count,
                        forceToMono = importer.forceToMono, readbackWav = readback,
                    };
                    measured.Add(originalPath, result);
                    return result;
                }
                foreach (Mapping mapping in manifest.mappings)
                {
                    Protect(mapping.templateLibrary);
                    var library = AssetDatabase.LoadAssetAtPath<UnityAudioEventLibrary>(mapping.templateLibrary);
                    Assert.That(library, Is.Not.Null);
                    var eventId = new AudioEventId(mapping.eventId);
                    Assert.That(library.TryResolve(eventId, out UnityAudioEventDefinition stock), Is.True);
                    Assert.That(replacement.TryResolve(eventId, out UnityAudioEventDefinition selected), Is.True);
                    ClipReport oldPcm = Measure(stock.Clip), newPcm = Measure(selected.Clip);
                    entries.Add(new Entry
                    {
                        eventId = mapping.eventId, stockVolume = stock.Volume, selectedVolume = selected.Volume,
                        stock = oldPcm, selected = newPcm,
                        deltaRmsDb = newPcm.monoRmsDbfs - oldPcm.monoRmsDbfs,
                        deltaActiveDb = newPcm.active100msRmsDbfs - oldPcm.active100msRmsDbfs,
                    });
                }
                var report = new Report { unityVersion = Application.unityVersion, entries = entries.ToArray() };
                File.WriteAllText(output + "/imported-pcm-report.json", JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                Debug.Log("USER_AUDIO_PCM_AUDIT_OK events=" + entries.Count + " uniqueClips=" + measured.Count + " report=" + output +
                    "/imported-pcm-report.json preservedOriginals=" + unchanged.Count);
            }
            finally
            {
                // Delete only the exact audit folder created by this invocation.
                Assert.That(AssetDatabase.AssetPathToGUID(temporaryRoot), Is.EqualTo(folderGuid));
                Assert.That(AssetDatabase.DeleteAsset(temporaryRoot), Is.True);
                foreach (var protectedFile in unchanged)
                    Assert.That(Hash(protectedFile.Key), Is.EqualTo(protectedFile.Value), "Audit changed original: " + protectedFile.Key);
            }
        }

        private static double Db(double energy) => 10d * Math.Log10(Math.Max(1e-12d, energy));
        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path); using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }
        private static void WriteFloatWave(string path, float[] mono, int rate)
        {
            using var stream = File.Create(path); using var writer = new BinaryWriter(stream);
            int bytes = checked(mono.Length * sizeof(float));
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(bytes + 36); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)3); writer.Write((short)1); writer.Write(rate); writer.Write(rate * sizeof(float));
            writer.Write((short)sizeof(float)); writer.Write((short)32); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
            foreach (float value in mono) writer.Write(value);
        }
        [Serializable] private sealed class Manifest { public Mapping[] mappings = Array.Empty<Mapping>(); }
        [Serializable] private sealed class Mapping { public string templateLibrary = "", eventId = ""; }
        [Serializable] private sealed class Report { public string unityVersion; public Entry[] entries; }
        [Serializable] private sealed class Entry
        {
            public string eventId; public float stockVolume, selectedVolume;
            public ClipReport stock, selected; public double deltaRmsDb, deltaActiveDb;
        }
        [Serializable] private sealed class ClipReport
        {
            public string assetPath, sourceHash, readbackWav; public int importedChannels, sampleRate;
            public float seconds, activeWindowFraction; public bool forceToMono;
            public double monoRmsDbfs, monoSamplePeakDbfs, maximum100msRmsDbfs, active100msRmsDbfs;
        }
    }
}
