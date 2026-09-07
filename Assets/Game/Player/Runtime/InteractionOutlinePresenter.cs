using System.Collections.Generic;
using MSC.Core.Lifecycle;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using UnityEngine;
using UnityEngine.Serialization;

namespace MSC.Player
{
    /// <summary>
    /// Resolves the actionable interaction candidate into an explicit set of
    /// renderers. The actual outline backend lives in a presentation adapter.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class InteractionOutlinePresenter : MonoBehaviour,
        IUiVisibilityGate
    {
        [SerializeField]
        private PlayerInteractionController interactionController;

        [SerializeField]
        private Color outlineColor = Color.white;

        [SerializeField]
        private Color invalidOutlineColor = new Color(1f, 0.08f, 0.04f, 1f);

        [FormerlySerializedAs("completeOutlineColor")]
        [SerializeField]
        private Color looseOutlineColor = new Color(0.08f, 1f, 0.18f, 1f);

        [SerializeField]
        private Color partialOutlineColor = new Color(1f, 0.78f, 0.05f, 1f);

        [SerializeField, Range(0.5f, 8f)]
        private float outlineWidthPixels = 3f;

        [SerializeField, Min(1)]
        private int maximumOutlinedRenderers = 32;

        private readonly List<Renderer> sourceRenderers =
            new List<Renderer>();
        private InteractionTargetHost presentedHost;
        private Collider presentedCollider;
        private bool uiSuppressed;
        private uint revision;
        private Color activeOutlineColor = Color.white;

        public bool IsUiSuppressed => uiSuppressed;
        public int ActiveOutlineRendererCount => sourceRenderers.Count;
        public IReadOnlyList<Renderer> ActiveRenderers => sourceRenderers;
        public Color OutlineColor => activeOutlineColor;
        public float OutlineWidthPixels => outlineWidthPixels;
        public uint Revision => revision;

        public void Configure(
            PlayerInteractionController controller,
            Color color,
            float widthPixels)
        {
            interactionController = controller;
            outlineColor = color;
            activeOutlineColor = color;
            outlineWidthPixels = Mathf.Clamp(widthPixels, 0.5f, 8f);
            RefreshHighlight();
        }

        public void SetUiSuppressed(bool suppressed)
        {
            uiSuppressed = suppressed;
            if (suppressed)
            {
                ClearHighlight();
            }
        }

        public void RefreshHighlight()
        {
            if (interactionController == null)
            {
                interactionController =
                    GetComponent<PlayerInteractionController>();
            }

            bool actionable =
                !uiSuppressed &&
                interactionController != null &&
                interactionController.IsInteractionEnabled &&
                !string.IsNullOrWhiteSpace(
                    interactionController.CurrentPrompt);
            InteractionCandidate candidate = actionable
                ? interactionController.CurrentCandidate
                : default;
            Present(candidate, actionable);
        }

        /// <summary>
        /// Explicit presentation input used by Update and focused tests.
        /// </summary>
        public void Present(
            in InteractionCandidate candidate,
            bool actionable)
        {
            if (!actionable || !candidate.IsValid ||
                candidate.TryGetCapability(out IInteractionOutlineVisibility visibility) &&
                !visibility.ShouldShowOutline)
            {
                ClearHighlight();
                return;
            }

            Color resolvedColor = ResolveOutlineColor(candidate);
            bool colorChanged = activeOutlineColor != resolvedColor;
            activeOutlineColor = resolvedColor;

            if (candidate.Host == presentedHost &&
                candidate.SourceCollider == presentedCollider &&
                sourceRenderers.Count > 0)
            {
                if (colorChanged)
                {
                    revision++;
                }

                return;
            }

            ClearHighlight();
            InteractionTargetHost host = candidate.Host;
            if (host == null)
            {
                return;
            }

            if (candidate.TryGetCapability(
                    out IInteractionOutlineRendererSource rendererSource) &&
                rendererSource.ResolveOutlineRenderer() is Renderer scopedRenderer)
            {
                CollectAuthoredRenderers(new[] { scopedRenderer });
            }
            else if (host.HasOutlineRendererOverride)
            {
                CollectAuthoredRenderers(host.OutlineRenderers);
            }
            else
            {
                Transform visualRoot = ResolveVisualRoot(candidate);
                if (visualRoot != null)
                {
                    CollectSourceRenderers(visualRoot);
                }
            }

            if (sourceRenderers.Count == 0)
            {
                return;
            }

            presentedHost = host;
            presentedCollider = candidate.SourceCollider;
            revision++;
        }

        public void ClearHighlight()
        {
            if (sourceRenderers.Count == 0 &&
                presentedHost == null &&
                presentedCollider == null)
            {
                return;
            }

            sourceRenderers.Clear();
            presentedHost = null;
            presentedCollider = null;
            revision++;
        }

        private Color ResolveOutlineColor(
            in InteractionCandidate candidate)
        {
            if (!candidate.TryGetCapability(
                    out IInteractionOutlineFeedbackSource feedbackSource))
            {
                return outlineColor;
            }

            IHeldToolIdentity heldTool = null;
            interactionController?.TryGetHeldCapability(out heldTool);
            return feedbackSource.GetOutlineFeedback(heldTool) switch
            {
                InteractionOutlineFeedback.Invalid => invalidOutlineColor,
                InteractionOutlineFeedback.Loose => looseOutlineColor,
                InteractionOutlineFeedback.Partial => partialOutlineColor,
                InteractionOutlineFeedback.Complete => outlineColor,
                _ => outlineColor,
            };
        }

        private void Update()
        {
            RefreshHighlight();
        }

        private void OnDisable()
        {
            ClearHighlight();
        }

        private Transform ResolveVisualRoot(
            in InteractionCandidate candidate)
        {
            InteractionTargetHost host = candidate.Host;
            if (host == null)
            {
                return null;
            }

            if (HasSupportedRenderer(host.transform))
            {
                return host.transform;
            }

            // Handle and switch acquisition volumes are often a renderer-less
            // child beside the real mesh. Climb only one level, and only when
            // that scope belongs to this single interaction host.
            Transform parent = host.transform.parent;
            if (parent == null ||
                parent.GetComponentsInChildren<InteractionTargetHost>(true)
                    .Length != 1 ||
                !HasSupportedRenderer(parent))
            {
                return null;
            }

            return parent;
        }

        private bool HasSupportedRenderer(Transform root)
        {
            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (IsSupportedSource(renderers[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private void CollectAuthoredRenderers(
            IReadOnlyList<Renderer> renderers)
        {
            sourceRenderers.Clear();
            int count = Mathf.Min(
                renderers.Count,
                Mathf.Max(1, maximumOutlinedRenderers));
            for (int index = 0; index < count; index++)
            {
                Renderer renderer = renderers[index];
                if (IsSupportedSource(renderer) &&
                    !sourceRenderers.Contains(renderer))
                {
                    sourceRenderers.Add(renderer);
                }
            }
        }

        private void CollectSourceRenderers(Transform root)
        {
            sourceRenderers.Clear();
            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(true);
            int maximum = Mathf.Max(1, maximumOutlinedRenderers);
            for (int index = 0;
                 index < renderers.Length && sourceRenderers.Count < maximum;
                 index++)
            {
                Renderer renderer = renderers[index];
                if (IsSupportedSource(renderer))
                {
                    sourceRenderers.Add(renderer);
                }
            }
        }

        private static bool IsSupportedSource(Renderer renderer)
        {
            if (renderer == null ||
                !renderer.enabled ||
                renderer.forceRenderingOff)
            {
                return false;
            }

            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh != null;
            }

            return renderer is MeshRenderer &&
                renderer.TryGetComponent(out MeshFilter filter) &&
                filter.sharedMesh != null;
        }
    }
}
