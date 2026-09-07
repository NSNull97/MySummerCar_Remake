using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>Called after a purchased part's presentation is materialized or replaced.</summary>
    public interface IAssemblyItemPartPresentationBinding
    {
        void BindPartPresentation(PartInstance part, Transform visualRoot);
    }
}
