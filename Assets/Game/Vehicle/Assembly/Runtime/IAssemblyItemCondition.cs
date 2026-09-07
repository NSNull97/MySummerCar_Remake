namespace MSC.Vehicle.Assembly
{
    /// <summary>Read-only condition of an installed item; Items remains its save authority.</summary>
    public interface IAssemblyItemCondition
    {
        float ConditionPercent { get; }
        bool IsBroken { get; }
    }

    /// <summary>Optional wear command; the implementing part/item keeps save authority.</summary>
    public interface IAssemblyWearSink
    {
        bool TryApplyWear(float conditionLoss);
    }
}
