using System.IO;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using UnityEditor;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static class Phase1SatsumaStartReadinessAuthoring
    {
        public static int ApplyToInstance(VehicleAssemblyController assembly)
        {
            if (assembly == null) throw new InvalidDataException("Satsuma assembly is required.");
            var prerequisites = assembly.GetComponent<AssemblyVehiclePrerequisiteAdapter>();
            var electrical = assembly.GetComponent<SatsumaElectricalSystem>();
            if (prerequisites == null || electrical == null || prerequisites.AssemblyController != assembly)
                throw new InvalidDataException("Author the existing simulation/electrical adapter first.");
            if (prerequisites.UsesSatsumaAssemblyRequirements && prerequisites.SatsumaElectrical == electrical) return 0;
            prerequisites.ConfigureSatsumaRequirements(electrical);
            EditorUtility.SetDirty(prerequisites);
            return 1;
        }
    }
}
