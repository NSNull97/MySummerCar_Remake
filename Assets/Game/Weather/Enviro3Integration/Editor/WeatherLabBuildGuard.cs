using System;
using System.Linq;
using MSC.Development.WeatherLab;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace MSC.Weather.Enviro3Integration.Editor
{
    public sealed class WeatherLabBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            bool listed = EditorBuildSettings.scenes.Any(scene =>
                string.Equals(scene.path, WeatherLabSceneMarker.SceneAssetPath, StringComparison.Ordinal));
            if (listed)
            {
                throw new BuildFailedException(
                    "Non-shipping WeatherLab is listed in Build Settings. Remove it before building.");
            }
        }
    }
}
