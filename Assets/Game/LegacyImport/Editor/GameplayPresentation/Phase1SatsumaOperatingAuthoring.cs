using System;
using System.IO;
using System.Linq;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    public static class Phase1SatsumaOperatingAuthoring
    {
        private static readonly string[] HealthParts =
        { "piston1", "piston2", "piston3", "piston4", "crankshaft", "head-gasket", "water-pump", "alternator", "rocker-shaft" };

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Bind Live Engine Operating Model Only")]
        public static void RefreshBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before authoring.");
            Phase1SatsumaCockpitRules.ValidateManifestIdentity();
            string path = Phase1SatsumaBaselineBuilder.RuntimePrefabPath;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Directory.CreateDirectory("Logs");
                File.Copy(path, "Logs/codex-operating-before-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".prefab", false);
                int changes = ApplyToInstance(contents.GetComponent<VehicleAssemblyController>());
                if (changes > 0 && PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                    throw new InvalidDataException("Could not save live engine bindings.");
                Debug.Log("SATSUMA_OPERATING_SOURCE_OK changed=" + changes + " partHealth=9 model=explicit fullRebuild=false");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        public static int ApplyToInstance(VehicleAssemblyController assembly)
        {
            if (assembly == null || Application.isPlaying) throw new ArgumentException("Stopped editor and explicit assembly required.");
            VehicleSimulationHost host = assembly.GetComponent<VehicleSimulationHost>();
            SatsumaElectricalSystem electrical = assembly.GetComponent<SatsumaElectricalSystem>();
            SatsumaDashboardControlsController dashboard = assembly.GetComponent<SatsumaDashboardControlsController>();
            PartInstance[] parts = HealthParts.Select(id => Part(assembly, id)).ToArray();
            AssemblyEngineAdjustmentState alternator = Part(assembly, "alternator").GetComponent<AssemblyEngineAdjustmentState>();
            AssemblyEngineAdjustmentState distributor = Part(assembly, "distributor").GetComponent<AssemblyEngineAdjustmentState>();
            AssemblyEngineAdjustmentState mixture = Part(assembly, "carburetor").GetComponent<AssemblyEngineAdjustmentState>();
            AssemblyCamshaftTimingState cam = Part(assembly, "camshaft-gear").GetComponent<AssemblyCamshaftTimingState>();
            AssemblyValveAdjustmentState valves = Part(assembly, "rocker-shaft").GetComponent<AssemblyValveAdjustmentState>();
            AssemblyServiceCapState oil = Part(assembly, "rocker-cover").GetComponent<AssemblyServiceCapState>();
            AssemblyServiceCapState radiator = Part(assembly, "radiator").GetComponent<AssemblyServiceCapState>();
            if (host == null || electrical == null || dashboard == null || alternator == null || distributor == null ||
                mixture == null || cam == null || valves == null || oil == null || radiator == null)
                throw new InvalidDataException("Author reviewed controls/caps before the live operating source.");
            foreach (PartInstance part in parts)
            {
                IAssemblyItemCondition condition = part.GetComponent<IAssemblyItemCondition>();
                if (condition != null && (condition is not AssemblyMechanicalConditionState health || health.Part != part))
                    throw new InvalidDataException("Never duplicate an existing item-owned condition: " + part.Definition.DefinitionId);
            }
            SatsumaEngineOperatingSource existing = assembly.GetComponent<SatsumaEngineOperatingSource>();
            if (existing != null && (!existing.ValidateBindings(out _) || host.SatsumaOperatingSourceComponent != existing))
                throw new InvalidDataException("Existing live operating source bindings drifted.");
            if (existing == null && host.SatsumaOperatingSourceComponent != null)
                throw new InvalidDataException("Another operating source already owns this host.");
            // All references/ownership checked before mutating the prefab contents.
            int changed = 0;
            foreach (PartInstance part in parts)
                if (part.GetComponent<AssemblyMechanicalConditionState>() == null)
                { part.gameObject.AddComponent<AssemblyMechanicalConditionState>().Configure(part); changed++; }
            if (existing == null)
            {
                existing = assembly.gameObject.AddComponent<SatsumaEngineOperatingSource>();
                existing.Configure(assembly, electrical, dashboard, alternator, distributor, mixture, cam, valves, oil, radiator);
                host.ConfigureSatsumaOperatingSource(existing); changed++;
            }
            return changed;
        }
        private static PartInstance Part(VehicleAssemblyController assembly, string suffix) =>
            assembly.Parts.Single(value => value?.Definition?.DefinitionId == "vehicle.satsuma.part." + suffix);
    }
}
