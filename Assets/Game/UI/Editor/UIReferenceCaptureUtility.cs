using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MSC.UI.EditorTools
{
    public static class UIReferenceCaptureUtility
    {
        public static UIReferenceCaptureResult GenerateScreenReviewSet(
            string projectRoot,
            UIReferenceScreen screen,
            string implementationPath)
        {
            var result = new UIReferenceCaptureResult();
            var validation = UIReferenceManifestValidator.ValidateProject(projectRoot);
            if (!validation.IsValid)
            {
                AddValidationErrors(validation, result);
                return result;
            }

            if (!File.Exists(implementationPath))
            {
                result.AddError($"Implementation capture is missing: {implementationPath}");
                return result;
            }

            try
            {
                var captures = PrepareScreenCaptures(validation, screen, implementationPath);
                WriteScreenCaptures(projectRoot, screen, captures, result);
            }
            catch (Exception exception)
            {
                result.AddError($"{UIReferenceCatalog.Get(screen).ReviewStem}: {exception.Message}");
            }

            return result;
        }

        public static UIReferenceCaptureResult GenerateAllReviewSets(string projectRoot)
        {
            var result = new UIReferenceCaptureResult();
            var validation = UIReferenceManifestValidator.ValidateProject(projectRoot);
            if (!validation.IsValid)
            {
                AddValidationErrors(validation, result);
                return result;
            }

            var prepared = new Dictionary<UIReferenceScreen, PreparedCaptures>();
            try
            {
                foreach (var descriptor in UIReferenceCatalog.All)
                {
                    var implementationPath = UIReferencePaths.GetImplementationPath(
                        projectRoot,
                        descriptor.Screen);
                    if (!File.Exists(implementationPath))
                    {
                        result.AddError($"Implementation capture is missing: {implementationPath}");
                        continue;
                    }

                    prepared.Add(
                        descriptor.Screen,
                        PrepareScreenCaptures(validation, descriptor.Screen, implementationPath));
                }

                if (result.Errors.Count > 0)
                {
                    return result;
                }

                foreach (var descriptor in UIReferenceCatalog.All)
                {
                    WriteScreenCaptures(
                        projectRoot,
                        descriptor.Screen,
                        prepared[descriptor.Screen],
                        result);
                }
            }
            catch (Exception exception)
            {
                result.AddError(exception.Message);
            }

            return result;
        }

        private static PreparedCaptures PrepareScreenCaptures(
            UIReferenceValidationResult validation,
            UIReferenceScreen screen,
            string implementationPath)
        {
            if (!validation.TryGetRecord(screen, out var record))
            {
                throw new InvalidOperationException($"Validated manifest has no record for {screen}.");
            }

            Texture2D reference = null;
            Texture2D implementation = null;
            Texture2D canonicalReference = null;
            Texture2D canonicalImplementation = null;
            Texture2D blended = null;

            try
            {
                reference = UIReferenceImageUtility.LoadPng(record.FilePath);
                implementation = UIReferenceImageUtility.LoadPng(implementationPath);
                canonicalReference = UIReferenceImageUtility.NormalizeToCanonical(reference);
                canonicalImplementation = UIReferenceImageUtility.NormalizeToCanonical(implementation);
                blended = UIReferenceImageUtility.Blend(
                    canonicalImplementation,
                    canonicalReference,
                    0.5f);

                return new PreparedCaptures(
                    UIReferenceImageUtility.EncodePng(canonicalReference),
                    UIReferenceImageUtility.EncodePng(canonicalImplementation),
                    UIReferenceImageUtility.EncodePng(blended));
            }
            finally
            {
                DestroyTexture(blended);
                DestroyTexture(canonicalImplementation);
                DestroyTexture(canonicalReference);
                DestroyTexture(implementation);
                DestroyTexture(reference);
            }
        }

        private static void WriteScreenCaptures(
            string projectRoot,
            UIReferenceScreen screen,
            PreparedCaptures captures,
            UIReferenceCaptureResult result)
        {
            var referencePath = UIReferencePaths.GetReferenceCapturePath(projectRoot, screen);
            var implementationPath = UIReferencePaths.GetImplementationPath(projectRoot, screen);
            var blendedPath = UIReferencePaths.GetBlendedCapturePath(projectRoot, screen);

            UIReferenceImageUtility.WriteBytesAtomically(referencePath, captures.ReferencePng);
            UIReferenceImageUtility.WriteBytesAtomically(
                implementationPath,
                captures.ImplementationPng);
            UIReferenceImageUtility.WriteBytesAtomically(blendedPath, captures.BlendedPng);

            result.AddOutput(referencePath);
            result.AddOutput(implementationPath);
            result.AddOutput(blendedPath);
        }

        private static void AddValidationErrors(
            UIReferenceValidationResult validation,
            UIReferenceCaptureResult result)
        {
            for (var index = 0; index < validation.Issues.Count; index++)
            {
                result.AddError(validation.Issues[index]);
            }
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture != null)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private sealed class PreparedCaptures
        {
            public PreparedCaptures(byte[] referencePng, byte[] implementationPng, byte[] blendedPng)
            {
                ReferencePng = referencePng;
                ImplementationPng = implementationPng;
                BlendedPng = blendedPng;
            }

            public byte[] ReferencePng { get; }

            public byte[] ImplementationPng { get; }

            public byte[] BlendedPng { get; }
        }
    }
}
