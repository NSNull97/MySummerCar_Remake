using UnityEngine;

namespace MSC.Audio.Composition
{
    /// <summary>
    /// Optional runtime hook implemented by a backend-specific composition
    /// owner. The base composition never references vendor assemblies.
    /// </summary>
    public interface IAudioRuntimeOwner
    {
        bool BindListener(Transform listenerTransform, out string failure);
    }
}
