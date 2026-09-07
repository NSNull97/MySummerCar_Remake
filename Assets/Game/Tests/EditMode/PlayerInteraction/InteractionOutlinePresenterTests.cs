using System.Linq;
using EPOOutline;
using MSC.Editor.PlayerInteraction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Prototype;
using MSC.Interaction.Query;
using MSC.Player;
using MSC.Presentation.InteractionOutline.EPO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Tests.EditMode.PlayerInteraction
{
    public sealed class InteractionOutlinePresenterTests
    {
        [Test]
        public void ExplicitVisibilityGateUpdatesWithoutChangingCandidateOrItsCapabilities()
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var owner = new GameObject("Outline visibility fixture");
            try
            {
                var capability = target.AddComponent<ContextToggleTarget>();
                var gate = target.AddComponent<OutlineVisibilityProbe>();
                var host = target.AddComponent<InteractionTargetHost>();
                host.Configure(capability, gate);
                var presenter = owner.AddComponent<InteractionOutlinePresenter>();
                presenter.Configure(null, Color.white, 3f);
                var candidate = new InteractionCandidate(host, Vector3.zero, Vector3.up, 1f,
                    target.GetComponent<Collider>());
                presenter.Present(candidate, true);
                Assert.That(presenter.ActiveOutlineRendererCount, Is.EqualTo(1));
                gate.ShouldShowOutline = false;
                presenter.Present(candidate, true);
                Assert.That(presenter.ActiveOutlineRendererCount, Is.Zero);
                Assert.That(host.TryGetCapability(out IContextInteractionTarget action), Is.True);
                Assert.That(action, Is.SameAs(capability));
                gate.ShouldShowOutline = true;
                presenter.Present(candidate, true);
                Assert.That(presenter.ActiveOutlineRendererCount, Is.EqualTo(1));
                Assert.That(target.GetComponent<Collider>().enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void CandidatePreservesExactSourceCollider()
        {
            GameObject target = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            try
            {
                ContextToggleTarget capability =
                    target.AddComponent<ContextToggleTarget>();
                InteractionTargetHost host =
                    target.AddComponent<InteractionTargetHost>();
                host.Configure(capability);
                Collider collider = target.GetComponent<Collider>();

                var candidate = new InteractionCandidate(
                    host,
                    Vector3.one,
                    Vector3.up,
                    1.25f,
                    collider);

                Assert.That(candidate.SourceCollider, Is.SameAs(collider));
                Assert.That(candidate.Host, Is.SameAs(host));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void PresenterSelectsSourceWithoutMutatingRendererOrCollider()
        {
            GameObject target = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            GameObject owner = new GameObject("Outline Presenter");
            try
            {
                ContextToggleTarget capability =
                    target.AddComponent<ContextToggleTarget>();
                InteractionTargetHost host =
                    target.AddComponent<InteractionTargetHost>();
                host.Configure(capability);
                Collider collider = target.GetComponent<Collider>();
                Renderer sourceRenderer = target.GetComponent<Renderer>();
                Material[] sourceMaterials = sourceRenderer.sharedMaterials;
                InteractionOutlinePresenter presenter =
                    owner.AddComponent<InteractionOutlinePresenter>();
                presenter.Configure(null, Color.yellow, 2f);

                presenter.Present(
                    new InteractionCandidate(
                        host,
                        Vector3.zero,
                        Vector3.forward,
                        1f,
                        collider),
                    actionable: true);

                Assert.That(
                    presenter.ActiveOutlineRendererCount,
                    Is.EqualTo(1));
                Assert.That(
                    presenter.ActiveRenderers[0],
                    Is.SameAs(sourceRenderer));
                Assert.That(
                    sourceRenderer.sharedMaterials,
                    Is.EqualTo(sourceMaterials));
                Assert.That(collider.enabled, Is.True);
                Assert.That(
                    target.transform.Find("Interaction Outline"),
                    Is.Null);

                presenter.ClearHighlight();

                Assert.That(
                    presenter.ActiveOutlineRendererCount,
                    Is.Zero);
                Assert.That(sourceRenderer.enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void PresenterKeepsFastenerOutlinedAndMapsLoosePartialCompleteAndInvalidColors()
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var owner = new GameObject("Outline Presenter");
            try
            {
                ContextToggleTarget capability =
                    target.AddComponent<ContextToggleTarget>();
                OutlineFeedbackProbe feedback =
                    target.AddComponent<OutlineFeedbackProbe>();
                InteractionTargetHost host =
                    target.AddComponent<InteractionTargetHost>();
                host.Configure(capability, feedback);
                Collider collider = target.GetComponent<Collider>();
                InteractionOutlinePresenter presenter =
                    owner.AddComponent<InteractionOutlinePresenter>();
                presenter.Configure(null, Color.white, 3f);
                var candidate = new InteractionCandidate(
                    host,
                    Vector3.zero,
                    Vector3.forward,
                    1f,
                    collider);

                presenter.Present(candidate, actionable: true);
                Assert.That(presenter.OutlineColor, Is.EqualTo(Color.white));
                Assert.That(presenter.ActiveOutlineRendererCount, Is.EqualTo(1));
                uint whiteRevision = presenter.Revision;

                feedback.Feedback = InteractionOutlineFeedback.Loose;
                presenter.Present(candidate, actionable: true);
                Assert.That(
                    presenter.OutlineColor,
                    Is.EqualTo(new Color(0.08f, 1f, 0.18f, 1f)));
                Assert.That(presenter.Revision, Is.GreaterThan(whiteRevision));
                Assert.That(presenter.ActiveOutlineRendererCount, Is.EqualTo(1));
                uint looseRevision = presenter.Revision;

                feedback.Feedback = InteractionOutlineFeedback.Partial;
                presenter.Present(candidate, actionable: true);
                Assert.That(
                    presenter.OutlineColor,
                    Is.EqualTo(new Color(1f, 0.78f, 0.05f, 1f)));
                Assert.That(presenter.Revision, Is.GreaterThan(looseRevision));
                uint partialRevision = presenter.Revision;

                feedback.Feedback = InteractionOutlineFeedback.Complete;
                presenter.Present(candidate, actionable: true);
                Assert.That(
                    presenter.OutlineColor,
                    Is.EqualTo(Color.white));
                Assert.That(presenter.Revision, Is.GreaterThan(partialRevision));
                Assert.That(
                    presenter.ActiveOutlineRendererCount,
                    Is.EqualTo(1),
                    "A fully tightened fastener must stay outlined in white.");
                uint completeRevision = presenter.Revision;

                feedback.Feedback = InteractionOutlineFeedback.Invalid;
                presenter.Present(candidate, actionable: true);
                Assert.That(
                    presenter.OutlineColor,
                    Is.EqualTo(new Color(1f, 0.08f, 0.04f, 1f)));
                Assert.That(presenter.Revision, Is.GreaterThan(completeRevision));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void AuthoredOutlineScopeCanSelectOnlyHandleRenderer()
        {
            var door = new GameObject("Door");
            GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "Leaf";
            leaf.transform.SetParent(door.transform, false);
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Handle";
            handle.transform.SetParent(door.transform, false);
            var zone = new GameObject("Handle Zone");
            zone.transform.SetParent(door.transform, false);
            BoxCollider collider = zone.AddComponent<BoxCollider>();
            ContextToggleTarget capability =
                zone.AddComponent<ContextToggleTarget>();
            InteractionTargetHost host =
                zone.AddComponent<InteractionTargetHost>();
            host.Configure(capability);
            Renderer handleRenderer = handle.GetComponent<Renderer>();
            host.ConfigureOutlineRenderers(handleRenderer);
            var owner = new GameObject("Outline Presenter");

            try
            {
                InteractionOutlinePresenter presenter =
                    owner.AddComponent<InteractionOutlinePresenter>();
                presenter.Configure(null, Color.white, 3f);
                presenter.Present(
                    new InteractionCandidate(
                        host,
                        Vector3.zero,
                        Vector3.forward,
                        1f,
                        collider),
                    actionable: true);

                Assert.That(presenter.ActiveRenderers, Has.Count.EqualTo(1));
                Assert.That(
                    presenter.ActiveRenderers[0],
                    Is.SameAs(handleRenderer));
                Assert.That(
                    presenter.ActiveRenderers.Contains(
                        leaf.GetComponent<Renderer>()),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(door);
            }
        }

        [Test]
        public void EpoAdapterBindsSelectionAndGrowsDilateInReferenceTime()
        {
            GameObject target = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            var owner = new GameObject("Outline Presenter");
            try
            {
                ContextToggleTarget capability =
                    target.AddComponent<ContextToggleTarget>();
                InteractionTargetHost host =
                    target.AddComponent<InteractionTargetHost>();
                host.Configure(capability);
                Collider collider = target.GetComponent<Collider>();
                InteractionOutlinePresenter presenter =
                    owner.AddComponent<InteractionOutlinePresenter>();
                presenter.Configure(null, Color.white, 3f);
                Outlinable outlinable = owner.AddComponent<Outlinable>();
                EpoInteractionOutlineAdapter adapter =
                    owner.AddComponent<EpoInteractionOutlineAdapter>();
                adapter.Configure(presenter, outlinable);

                presenter.Present(
                    new InteractionCandidate(
                        host,
                        Vector3.zero,
                        Vector3.forward,
                        1f,
                        collider),
                    actionable: true);
                adapter.Synchronize();

                Assert.That(adapter.BoundRendererCount, Is.EqualTo(1));
                Assert.That(outlinable.enabled, Is.True);
                Assert.That(
                    outlinable.OutlineParameters.Color,
                    Is.EqualTo(Color.white));
                Assert.That(
                    outlinable.OutlineParameters.DilateShift,
                    Is.Zero.Within(0.001f));
                Assert.That(
                    outlinable.OutlineParameters.BlurShift,
                    Is.Zero.Within(0.001f));

                adapter.Advance(0.1f);
                Assert.That(
                    outlinable.OutlineParameters.DilateShift,
                    Is.EqualTo(0.5f).Within(0.001f));
                adapter.Advance(0.1f);
                Assert.That(
                    outlinable.OutlineParameters.DilateShift,
                    Is.EqualTo(1f).Within(0.001f));

                presenter.ClearHighlight();
                adapter.Synchronize();
                Assert.That(outlinable.enabled, Is.False);
                Assert.That(adapter.BoundRendererCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void AuthoredPlayerPrefabUsesEpoHdrpOutline()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PlayerInteractionPrototypePaths.PlayerPrefab);
            Assert.That(prefab, Is.Not.Null);

            InteractionOutlinePresenter presenter =
                prefab.GetComponent<InteractionOutlinePresenter>();
            EpoInteractionOutlineAdapter adapter =
                prefab.GetComponent<EpoInteractionOutlineAdapter>();
            Outlinable outlinable = prefab.GetComponent<Outlinable>();
            Camera camera = prefab.GetComponentInChildren<Camera>(true);
            Outliner outliner = camera.GetComponent<Outliner>();
            HdrpOutliner hdrpOutliner =
                camera.GetComponent<HdrpOutliner>();
            CustomPassVolume passVolume =
                camera.GetComponentInChildren<CustomPassVolume>(true);

            Assert.That(presenter, Is.Not.Null);
            Assert.That(adapter, Is.Not.Null);
            Assert.That(adapter.Presenter, Is.SameAs(presenter));
            Assert.That(adapter.Outlinable, Is.SameAs(outlinable));
            Assert.That(
                adapter.GrowDurationSeconds,
                Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(outliner, Is.Not.Null);
            Assert.That(hdrpOutliner, Is.SameAs(outliner));
            Assert.That(
                outliner.PrimaryBufferSizeMode,
                Is.EqualTo(BufferSizeMode.Native));
            Assert.That(outliner.DilateIterations, Is.EqualTo(1));
            Assert.That(outliner.BlurIterations, Is.Zero);
            Assert.That(passVolume, Is.Not.Null);
            Assert.That(
                passVolume.customPasses.Any(
                    pass => pass is OutlineCustomPass),
                Is.True);
            Assert.That(
                presenter.OutlineWidthPixels,
                Is.EqualTo(3f).Within(0.001f));
            Assert.That(presenter.OutlineColor, Is.EqualTo(Color.white));
        }
    }

    public sealed class OutlineVisibilityProbe : MonoBehaviour, IInteractionOutlineVisibility
    {
        public bool ShouldShowOutline { get; set; } = true;
    }

    public sealed class OutlineFeedbackProbe : MonoBehaviour,
        IInteractionOutlineFeedbackSource
    {
        public InteractionOutlineFeedback Feedback { get; set; }

        public InteractionOutlineFeedback GetOutlineFeedback(
            IHeldToolIdentity heldTool) => Feedback;
    }
}
