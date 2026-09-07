using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace MSC.Audio.Wwise.Editor
{
    /// <summary>
    /// Explicit project-owned entry point for the official integration's
    /// SoundBank copy step. Source banks remain generated/ignored; this only
    /// refreshes the ignored StreamingAssets runtime mirror and verifies it.
    /// </summary>
    public static class WwiseBankSynchronizer
    {
        private static readonly string[] RequiredBanks =
        {
            "Init.bnk",
            "MSC_Vehicle.bnk",
            "MSC_Weather.bnk",
            "MSC_World.bnk",
            "MSC_Interaction.bnk",
            "MSC_UI.bnk",
        };

        [MenuItem("Tools/MSC Remake/Audio/Synchronize Windows SoundBanks")]
        public static void SynchronizeWindowsBanks()
        {
            string destination = string.Empty;
            if (!AkBuildPreprocessor.CopySoundbanks(false, "Windows", ref destination))
            {
                throw new InvalidOperationException(
                    "The official Wwise integration failed to copy Windows SoundBanks.");
            }

            string source = Path.Combine(
                Path.GetDirectoryName(AkWwiseEditorSettings.WwiseProjectAbsolutePath) ??
                string.Empty,
                "GeneratedSoundBanks",
                "Windows");
            for (int index = 0; index < RequiredBanks.Length; index++)
            {
                string bank = RequiredBanks[index];
                string sourcePath = Path.Combine(source, bank);
                string destinationPath = Path.Combine(destination, bank);
                if (!File.Exists(sourcePath) || !File.Exists(destinationPath))
                {
                    throw new FileNotFoundException(
                        $"Required Wwise bank was not synchronized: {bank}");
                }

                if (!string.Equals(
                        ComputeSha256(sourcePath),
                        ComputeSha256(destinationPath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"Synchronized Wwise bank hash mismatch: {bank}");
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                "M08_WWISE_WINDOWS_BANK_SYNC_OK banks=6 destination=" + destination);
        }

        /// <summary>
        /// Optional stronger gate for a caller that has resolved actual backend
        /// ownership. Never assumes that every mapped event belongs to Wwise.
        /// The original menu/copy entry point retains its existing behaviour.
        /// </summary>
        public static void ValidateAndSynchronizeWindowsBanks(
            IReadOnlyList<AudioEventMapEntry> expectedWwiseRoutes)
        {
            string source = Path.Combine(
                Path.GetDirectoryName(AkWwiseEditorSettings.WwiseProjectAbsolutePath) ??
                string.Empty, "GeneratedSoundBanks", "Windows");
            WwiseBankContentValidator.Inspect(source, expectedWwiseRoutes).ThrowIfFailed();
            SynchronizeWindowsBanks();
            string destination = Path.Combine(
                AkBasePathGetter.GetFullSoundBankPathEditor(), "Windows");
            WwiseBankContentReport report =
                WwiseBankContentValidator.Inspect(destination, expectedWwiseRoutes);
            report.ThrowIfFailed();
            Debug.Log("MSC_WWISE_WINDOWS_BANK_CONTENT_OK routes=" + report.InspectedRouteCount);
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }
    }
}
