namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// Lets a part replace its loose renderer during the short handoff to an
    /// installed mount. Simulation and assembly state remain owned by
    /// VehicleAssemblyController; this contract only prevents specialized
    /// rigged parts from flying toward an obsolete loose-mesh orientation.
    /// </summary>
    public interface IAssemblyInstallTransitionPresentation
    {
        void BeginInstallTransition(MountPointAuthoring mount);

        void ApplyInstallTransition(float normalizedProgress);

        void CompleteInstallTransition(bool installed);
    }
}
