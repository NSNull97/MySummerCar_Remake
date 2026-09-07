using NUnit.Framework;
using UnityEditor;

namespace MSC.Lighting.Tests.EditMode
{
    public sealed class LightingSerializedReferenceTests
    {
        private const string CalibrationScriptPath =
            "Assets/Game/Lighting/Runtime/LightingCalibrationProfile.cs";
        private const string CalibrationAssetPath =
            "Assets/Game/Lighting/Content/Profiles/" +
            "Phase1LightingCalibration.asset";
        private const string CatalogAssetPath =
            "Assets/Game/Lighting/Content/Profiles/" +
            "Phase1LocalLightingCatalog.asset";

        [Test]
        public void Calibration_HasMatchingScriptAndCatalogBinding()
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(
                CalibrationScriptPath);
            LightingCalibrationProfile calibration =
                AssetDatabase.LoadAssetAtPath<LightingCalibrationProfile>(
                    CalibrationAssetPath);
            LightingProfileCatalog catalog =
                AssetDatabase.LoadAssetAtPath<LightingProfileCatalog>(
                    CatalogAssetPath);

            Assert.That(script, Is.Not.Null);
            Assert.That(
                script.GetClass(),
                Is.EqualTo(typeof(LightingCalibrationProfile)));
            Assert.That(
                AssetDatabase.AssetPathToGUID(CalibrationScriptPath),
                Is.Not.Empty);
            Assert.That(calibration, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Calibration, Is.SameAs(calibration));
            Assert.That(calibration.DuskOnSunElevationDegrees, Is.EqualTo(12f));
            Assert.That(calibration.DawnOffSunElevationDegrees, Is.EqualTo(2f));
        }
    }
}
