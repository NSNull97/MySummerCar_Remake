using System;
using System.Collections.Generic;
using System.IO;
using MSC.World.Vegetation;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    internal sealed class VegetationMaskEditState : ScriptableObject
    {
        [SerializeField] private Texture2D densityMask;
        [SerializeField] private byte[] encodedPng = Array.Empty<byte>();
        [SerializeField] private int revision;

        public Texture2D DensityMask => densityMask;
        public IReadOnlyList<byte> EncodedPng =>
            encodedPng ?? Array.Empty<byte>();
        public int Revision => revision;

        public void ConfigureForAuthoring(Texture2D mask, byte[] pngBytes)
        {
            densityMask = mask;
            encodedPng = pngBytes != null
                ? (byte[])pngBytes.Clone()
                : Array.Empty<byte>();
            revision = 0;
        }

        public void CaptureForUndo(byte[] pngBytes)
        {
            encodedPng = pngBytes != null
                ? (byte[])pngBytes.Clone()
                : Array.Empty<byte>();
            revision++;
        }
    }

    [InitializeOnLoad]
    internal static class VegetationMaskStorage
    {
        private static bool restoreScheduled;

        static VegetationMaskStorage()
        {
            Undo.undoRedoPerformed += ScheduleRestoreAllStates;
        }

        public static VegetationMaskEditState GetState(Texture2D mask)
        {
            string maskPath = AssetDatabase.GetAssetPath(mask);
            if (string.IsNullOrWhiteSpace(maskPath))
            {
                return null;
            }

            string statePath = GetStatePath(maskPath);
            return AssetDatabase.LoadAssetAtPath<VegetationMaskEditState>(
                statePath);
        }

        public static VegetationMaskEditState CreateOrUpdateState(
            Texture2D mask,
            byte[] pngBytes)
        {
            string maskPath = AssetDatabase.GetAssetPath(mask);
            string statePath = GetStatePath(maskPath);
            VegetationMaskEditState state =
                AssetDatabase.LoadAssetAtPath<VegetationMaskEditState>(
                    statePath);
            if (state == null)
            {
                state = ScriptableObject.CreateInstance<VegetationMaskEditState>();
                state.name =
                    Path.GetFileNameWithoutExtension(maskPath) + "_EditState";
                state.ConfigureForAuthoring(mask, pngBytes);
                AssetDatabase.CreateAsset(state, statePath);
            }
            else if (state.DensityMask != mask || state.EncodedPng.Count == 0)
            {
                state.ConfigureForAuthoring(mask, pngBytes);
                EditorUtility.SetDirty(state);
            }

            return state;
        }

        public static void Commit(
            Texture2D mask,
            VegetationMaskEditState state)
        {
            if (mask == null || state == null)
            {
                throw new ArgumentNullException(
                    mask == null ? nameof(mask) : nameof(state));
            }

            byte[] bytes = mask.EncodeToPNG();
            state.CaptureForUndo(bytes);
            EditorUtility.SetDirty(state);
            string path = AssetDatabase.GetAssetPath(mask);
            File.WriteAllBytes(ToAbsolutePath(path), bytes);
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceUpdate);
        }

        public static void ScheduleRestoreAllStates()
        {
            if (restoreScheduled)
            {
                return;
            }

            restoreScheduled = true;
            EditorApplication.delayCall += RestoreAllStates;
        }

        private static void RestoreAllStates()
        {
            restoreScheduled = false;
            string[] guids = AssetDatabase.FindAssets(
                "t:VegetationMaskEditState",
                new[] { VegetationAssetBuilder.MaskPath });
            for (int index = 0; index < guids.Length; index++)
            {
                VegetationMaskEditState state =
                    AssetDatabase.LoadAssetAtPath<VegetationMaskEditState>(
                        AssetDatabase.GUIDToAssetPath(guids[index]));
                if (state == null ||
                    state.DensityMask == null ||
                    state.EncodedPng.Count == 0)
                {
                    continue;
                }

                string maskPath = AssetDatabase.GetAssetPath(state.DensityMask);
                byte[] desired = new byte[state.EncodedPng.Count];
                for (int byteIndex = 0;
                     byteIndex < state.EncodedPng.Count;
                     byteIndex++)
                {
                    desired[byteIndex] = state.EncodedPng[byteIndex];
                }

                string absolutePath = ToAbsolutePath(maskPath);
                byte[] existing = File.Exists(absolutePath)
                    ? File.ReadAllBytes(absolutePath)
                    : Array.Empty<byte>();
                if (ByteArraysEqual(existing, desired))
                {
                    continue;
                }

                File.WriteAllBytes(absolutePath, desired);
                AssetDatabase.ImportAsset(
                    maskPath,
                    ImportAssetOptions.ForceUpdate);
            }

            SceneView.RepaintAll();
        }

        private static string GetStatePath(string maskPath)
        {
            return Path.ChangeExtension(maskPath, null) + "_EditState.asset";
        }

        private static string ToAbsolutePath(string projectRelativePath)
        {
            string projectRoot =
                Directory.GetParent(Application.dataPath)?.FullName ??
                throw new InvalidOperationException(
                    "Unable to resolve Unity project root.");
            return Path.GetFullPath(
                Path.Combine(projectRoot, projectRelativePath));
        }

        private static bool ByteArraysEqual(
            IReadOnlyList<byte> left,
            IReadOnlyList<byte> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (int index = 0; index < left.Count; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
