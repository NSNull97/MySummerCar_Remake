using System.Linq;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.VehicleAssembly
{
    public sealed class VehicleAssemblyDependencyWindow : EditorWindow
    {
        private Vector2 scroll;

        [MenuItem("Tools/MSC Remake/Vehicle Assembly/Dependency Graph")]
        public static void Open()
        {
            GetWindow<VehicleAssemblyDependencyWindow>("Assembly Graph");
        }

        private void OnGUI()
        {
            VehicleAssemblyController controller = FindFirstObjectByType<VehicleAssemblyController>();
            if (controller == null)
            {
                EditorGUILayout.HelpBox(
                    "Откройте сцену VehicleAssemblyPrototype или выберите сборочный prefab.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Parts", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (PartInstance part in controller.Parts
                         .Where(part => part != null && part.Definition != null)
                         .OrderBy(part => part.Definition.DefinitionId))
            {
                EditorGUILayout.LabelField(
                    part.Definition.DefinitionId,
                    part.IsInstalled ? "Installed" : "Loose");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Dependencies", EditorStyles.boldLabel);
            foreach (AssemblyDependency dependency in controller.Dependencies
                         .Where(dependency => dependency != null)
                         .OrderBy(dependency => dependency.DependentPartDefinitionId))
            {
                EditorGUILayout.LabelField(
                    dependency.DependentPartDefinitionId,
                    $"{dependency.Kind} → {dependency.RelatedPartDefinitionId}");
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
