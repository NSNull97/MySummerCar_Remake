#if UNITY_EDITOR
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle.Diagnostics
{
    /// <summary>Explicit synthetic condition for four temporary smoke plugs, never a fallback for purchased/native items.</summary>
    public sealed class SatsumaNativeSmokePlugCondition : MonoBehaviour, IAssemblyItemCondition
    {
        public float ConditionPercent => 100f;
        public bool IsBroken => false;
    }
}
#endif
