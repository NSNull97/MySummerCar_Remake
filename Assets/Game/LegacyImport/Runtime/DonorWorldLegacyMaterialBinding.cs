using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.LegacyImport
{
    /// <summary>
    /// Project-owned renderer binding for switching between the shared
    /// compatibility and diagnostic material sets of one legacy renderer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DonorWorldLegacyMaterialBinding : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private string[] sourceMaterialGuids =
            Array.Empty<string>();
        [SerializeField] private Material[] texturedMaterials =
            Array.Empty<Material>();
        [SerializeField] private Material[] diagnosticMaterials =
            Array.Empty<Material>();

        public Renderer TargetRenderer => targetRenderer;
        public IReadOnlyList<string> SourceMaterialGuids =>
            sourceMaterialGuids;
        public IReadOnlyList<Material> TexturedMaterials =>
            texturedMaterials;
        public IReadOnlyList<Material> DiagnosticMaterials =>
            diagnosticMaterials;
        public int MaterialSlotCount => sourceMaterialGuids?.Length ?? 0;

        public bool IsConfigured =>
            TryValidateConfiguration(out _);

        public void Configure(
            Renderer renderer,
            IReadOnlyList<string> sourceGuids,
            IReadOnlyList<Material> sharedTexturedMaterials,
            IReadOnlyList<Material> sharedDiagnosticMaterials)
        {
            if (renderer == null)
            {
                throw new ArgumentNullException(nameof(renderer));
            }

            ValidateConfiguration(
                sourceGuids,
                sharedTexturedMaterials,
                sharedDiagnosticMaterials);

            targetRenderer = renderer;
            sourceMaterialGuids = CopyStrings(sourceGuids);
            texturedMaterials = CopyMaterials(
                sharedTexturedMaterials);
            diagnosticMaterials = CopyMaterials(
                sharedDiagnosticMaterials);
        }

        public bool Apply(DonorWorldLegacyPresentationMode mode)
        {
            if (!TryValidateConfiguration(out string failureReason))
            {
                Debug.LogError(
                    "Cannot apply donor world material binding: " +
                    failureReason,
                    this);
                return false;
            }

            Material[] selectedMaterials;
            switch (mode)
            {
                case DonorWorldLegacyPresentationMode.LegacyTextured:
                    selectedMaterials = texturedMaterials;
                    break;
                case DonorWorldLegacyPresentationMode.LegacyDiagnostic:
                    selectedMaterials = diagnosticMaterials;
                    break;
                default:
                    Debug.LogError(
                        "Unsupported donor world presentation mode: " +
                        mode,
                        this);
                    return false;
            }

            targetRenderer.sharedMaterials = selectedMaterials;
            return true;
        }

        public bool TryValidateConfiguration(
            out string failureReason)
        {
            if (targetRenderer == null)
            {
                failureReason = "Target renderer is missing.";
                return false;
            }

            if (sourceMaterialGuids == null ||
                texturedMaterials == null ||
                diagnosticMaterials == null)
            {
                failureReason =
                    "One or more serialized material arrays are null.";
                return false;
            }

            int slotCount = sourceMaterialGuids.Length;
            if (slotCount == 0)
            {
                failureReason =
                    "At least one source material slot is required.";
                return false;
            }

            if (texturedMaterials.Length != slotCount ||
                diagnosticMaterials.Length != slotCount)
            {
                failureReason =
                    "Source, textured, and diagnostic slot counts differ.";
                return false;
            }

            for (int index = 0; index < slotCount; index++)
            {
                if (string.IsNullOrWhiteSpace(
                        sourceMaterialGuids[index]))
                {
                    failureReason =
                        "Source material GUID is empty at slot " +
                        index + ".";
                    return false;
                }

                if (texturedMaterials[index] == null)
                {
                    failureReason =
                        "Textured material is missing at slot " +
                        index + ".";
                    return false;
                }

                if (diagnosticMaterials[index] == null)
                {
                    failureReason =
                        "Diagnostic material is missing at slot " +
                        index + ".";
                    return false;
                }
            }

            failureReason = string.Empty;
            return true;
        }

        private void Reset()
        {
            targetRenderer = GetComponent<Renderer>();
        }

        private static void ValidateConfiguration(
            IReadOnlyList<string> sourceGuids,
            IReadOnlyList<Material> sharedTexturedMaterials,
            IReadOnlyList<Material> sharedDiagnosticMaterials)
        {
            if (sourceGuids == null)
            {
                throw new ArgumentNullException(nameof(sourceGuids));
            }

            if (sharedTexturedMaterials == null)
            {
                throw new ArgumentNullException(
                    nameof(sharedTexturedMaterials));
            }

            if (sharedDiagnosticMaterials == null)
            {
                throw new ArgumentNullException(
                    nameof(sharedDiagnosticMaterials));
            }

            int slotCount = sourceGuids.Count;
            if (slotCount == 0)
            {
                throw new ArgumentException(
                    "At least one source material slot is required.",
                    nameof(sourceGuids));
            }

            if (sharedTexturedMaterials.Count != slotCount ||
                sharedDiagnosticMaterials.Count != slotCount)
            {
                throw new ArgumentException(
                    "Source, textured, and diagnostic slot counts " +
                    "must match.");
            }

            for (int index = 0; index < slotCount; index++)
            {
                if (string.IsNullOrWhiteSpace(sourceGuids[index]))
                {
                    throw new ArgumentException(
                        "Source material GUID is empty at slot " +
                        index + ".",
                        nameof(sourceGuids));
                }

                if (sharedTexturedMaterials[index] == null)
                {
                    throw new ArgumentException(
                        "Textured material is missing at slot " +
                        index + ".",
                        nameof(sharedTexturedMaterials));
                }

                if (sharedDiagnosticMaterials[index] == null)
                {
                    throw new ArgumentException(
                        "Diagnostic material is missing at slot " +
                        index + ".",
                        nameof(sharedDiagnosticMaterials));
                }
            }
        }

        private static string[] CopyStrings(
            IReadOnlyList<string> values)
        {
            var copy = new string[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                copy[index] = values[index];
            }

            return copy;
        }

        private static Material[] CopyMaterials(
            IReadOnlyList<Material> values)
        {
            var copy = new Material[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                copy[index] = values[index];
            }

            return copy;
        }
    }
}
