using System.Collections.Generic;
using EPOOutline;
using MSC.Player;
using UnityEngine;

namespace MSC.Presentation.InteractionOutline.EPO
{
    /// <summary>
    /// Licensed Easy Performant Outline backend for the project-owned hover
    /// selection. It mirrors the reference grow-in without making EPO a
    /// gameplay dependency.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    [DisallowMultipleComponent]
    public sealed class EpoInteractionOutlineAdapter : MonoBehaviour
    {
        public const float ReferenceGrowSeconds = 0.2f;

        [SerializeField]
        private InteractionOutlinePresenter presenter;

        [SerializeField]
        private Outlinable outlinable;

        [SerializeField, Min(0.01f)]
        private float growDurationSeconds = ReferenceGrowSeconds;

        private readonly List<Renderer> boundRenderers =
            new List<Renderer>();
        private uint appliedRevision = uint.MaxValue;
        private float growElapsedSeconds;

        public InteractionOutlinePresenter Presenter => presenter;
        public Outlinable Outlinable => outlinable;
        public float GrowDurationSeconds => growDurationSeconds;
        public int BoundRendererCount => boundRenderers.Count;

        public void Configure(
            InteractionOutlinePresenter configuredPresenter,
            Outlinable configuredOutlinable,
            float configuredGrowDurationSeconds = ReferenceGrowSeconds)
        {
            presenter = configuredPresenter;
            outlinable = configuredOutlinable;
            growDurationSeconds = Mathf.Max(
                0.01f,
                configuredGrowDurationSeconds);
            appliedRevision = uint.MaxValue;
            Synchronize(force: true);
        }

        public void Synchronize(bool force = false)
        {
            if (presenter == null || outlinable == null)
            {
                DisableOutline();
                return;
            }

            if (!force && appliedRevision == presenter.Revision)
            {
                return;
            }

            appliedRevision = presenter.Revision;
            BindRenderers(presenter.ActiveRenderers);
        }

        public void Advance(float unscaledDeltaTime)
        {
            if (outlinable == null || !outlinable.enabled ||
                !float.IsFinite(unscaledDeltaTime) ||
                unscaledDeltaTime <= 0f)
            {
                return;
            }

            growElapsedSeconds = Mathf.Min(
                growDurationSeconds,
                growElapsedSeconds + unscaledDeltaTime);
            float widthScale = presenter != null
                ? Mathf.Clamp(presenter.OutlineWidthPixels / 3f, 0.25f, 2f)
                : 1f;
            ApplyDilateShift(
                Mathf.Clamp01(growElapsedSeconds / growDurationSeconds) *
                widthScale);
        }

        private void Update()
        {
            Synchronize();
            Advance(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            DisableOutline();
        }

        private void BindRenderers(IReadOnlyList<Renderer> renderers)
        {
            outlinable.enabled = false;
            RemoveBoundTargets();

            for (int index = 0; index < renderers.Count; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || boundRenderers.Contains(renderer))
                {
                    continue;
                }

                boundRenderers.Add(renderer);
                int submeshCount = GetSubmeshCount(renderer);
                for (int submesh = 0;
                     submesh < submeshCount;
                     submesh++)
                {
                    outlinable.TryAddTarget(
                        new OutlineTarget(renderer, submesh));
                }
            }

            if (boundRenderers.Count == 0)
            {
                growElapsedSeconds = 0f;
                return;
            }

            outlinable.RenderStyle = RenderStyle.Single;
            outlinable.DrawingMode = OutlinableDrawingMode.Normal;
            outlinable.OutlineParameters.Enabled = true;
            outlinable.OutlineParameters.Color = presenter.OutlineColor;
            outlinable.OutlineParameters.BlurShift = 0f;
            growElapsedSeconds = 0f;
            ApplyDilateShift(0f);
            outlinable.enabled = true;
        }

        private void DisableOutline()
        {
            if (outlinable != null)
            {
                outlinable.enabled = false;
                ApplyDilateShift(0f);
                RemoveBoundTargets();
            }

            growElapsedSeconds = 0f;
        }

        private void RemoveBoundTargets()
        {
            if (outlinable == null)
            {
                boundRenderers.Clear();
                return;
            }

            while (outlinable.OutlineTargets.Count > 0)
            {
                outlinable.RemoveTarget(outlinable[0]);
            }

            boundRenderers.Clear();
        }

        private void ApplyDilateShift(float value)
        {
            if (outlinable == null)
            {
                return;
            }

            outlinable.OutlineParameters.DilateShift =
                Mathf.Clamp(value, 0f, 2f);
        }

        private static int GetSubmeshCount(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned &&
                skinned.sharedMesh != null)
            {
                return Mathf.Max(1, skinned.sharedMesh.subMeshCount);
            }

            if (renderer is MeshRenderer &&
                renderer.TryGetComponent(out MeshFilter filter) &&
                filter.sharedMesh != null)
            {
                return Mathf.Max(1, filter.sharedMesh.subMeshCount);
            }

            return 1;
        }
    }
}
