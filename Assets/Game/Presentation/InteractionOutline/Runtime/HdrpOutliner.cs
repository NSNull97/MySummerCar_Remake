using EPOOutline;

namespace MSC.Presentation.InteractionOutline.EPO
{
    /// <summary>
    /// HDRP-safe EPO Outliner. EPO 3.4.2's Editor-only LateUpdate maintains
    /// built-in pipeline command buffers even when an SRP is active, which
    /// emits warnings every frame. HDRP rendering is owned by
    /// OutlineCustomPass, so the built-in maintenance message is intentionally
    /// shadowed while preserving the vendor component's runtime API and data.
    /// </summary>
    public sealed class HdrpOutliner : Outliner
    {
#if UNITY_EDITOR
        private void LateUpdate()
        {
        }
#endif
    }
}
