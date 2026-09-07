#if UNITY_EDITOR
using System.Collections;
using System.IO;
using MSC.UI.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Tests.PlayMode.UIPresentation
{
    public sealed partial class GameUiRootPlayModeTests
    {
        private static void AssertMenuAtmospherePlacement(MainMenuVehiclePreview preview)
        {
            MainMenuEnvironmentModel environment = preview.Environment;
            Light lamp = preview.GarageLampLight;
            Assert.That(lamp, Is.Not.Null);
            Assert.That(lamp.isActiveAndEnabled, Is.True);
            Assert.That(lamp.type, Is.EqualTo(LightType.Spot));
            Assert.That(lamp.color, Is.EqualTo(Color.white));
            Assert.That(lamp.useColorTemperature, Is.False);
            Vector3 vehicleCenter = preview.Model.transform.TransformPoint(preview.Model.LocalBounds.center);
            Assert.That(Vector3.Angle(lamp.transform.forward, vehicleCenter - lamp.transform.position), Is.LessThan(0.01f),
                "The white practical spotlight must aim at the car, independently of the orbit camera.");
            Bounds vehicleBounds = preview.Model.LocalBounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 sign = new Vector3((corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                Vector3 point = preview.Model.transform.TransformPoint(vehicleBounds.center + Vector3.Scale(vehicleBounds.extents, sign));
                Assert.That(Vector3.Angle(lamp.transform.forward, point - lamp.transform.position),
                    Is.LessThan(lamp.spotAngle * 0.5f), "The cone must cover every corner of the car's display bounds.");
            }
            Assert.That(lamp.intensity, Is.GreaterThan(0f));
            Assert.That(lamp.range, Is.GreaterThan(0f));
            Assert.That(Vector3.Distance(lamp.transform.position, environment.GarageLampAnchor.position), Is.LessThan(0.5f));
            Assert.That(lamp.cullingMask, Is.EqualTo(preview.PreviewCamera.cullingMask));
            Assert.That(lamp.GetComponent<HDAdditionalLightData>().interactsWithSky, Is.False);
            var emission = new MaterialPropertyBlock();
            environment.GarageLampRenderer.GetPropertyBlock(emission, environment.GarageLampMaterialIndex);
            Color glow = emission.GetColor("_EmissiveColor");
            Assert.That(glow.r, Is.EqualTo(glow.g));
            Assert.That(glow.g, Is.EqualTo(glow.b));
            Assert.That(glow.b, Is.GreaterThan(0f), "The existing lamp mesh must have neutral emission via its own MPB.");
            Assert.That(emission.GetFloat("_AlbedoAffectEmissive"), Is.EqualTo(1f));

            LocalVolumetricFog fog = preview.RearFog;
            Assert.That(fog, Is.Not.Null);
            Assert.That(fog.isActiveAndEnabled, Is.True);
            Assert.That(fog.parameters.meanFreePath, Is.GreaterThan(0f));
            Assert.That(fog.parameters.textureScrollingSpeed, Is.EqualTo(Vector3.zero));
            Assert.That(Quaternion.Angle(fog.transform.rotation, environment.transform.rotation), Is.LessThan(0.001f));
            var fogBounds = new Bounds(environment.transform.InverseTransformPoint(fog.transform.position), fog.parameters.size);
            Assert.That(fogBounds.max.z, Is.LessThan(environment.HomeExteriorBoundsLocal.min.z),
                "The finite fog volume must remain behind the measured house, not over the yard.");
            var carBounds = new Bounds(environment.transform.InverseTransformPoint(
                preview.Model.transform.TransformPoint(preview.Model.LocalBounds.center)), preview.Model.LocalBounds.size);
            Assert.That(fogBounds.Intersects(carBounds), Is.False);
            Assert.That(fogBounds.Contains(environment.transform.InverseTransformPoint(preview.PreviewCamera.transform.position)),
                Is.False, "The preview camera must remain outside the rear haze volume.");
        }

        private static IEnumerator AssertMenuAtmosphereRenderedPixels(
            MainMenuVehiclePreview preview, string quality, bool supportsVolumetrics, string evidencePath)
        {
            Color paint = new Color32(3, 38, 69, 255);
            yield return RefreshMenuAtmosphereFrames(preview, paint);
            var output = (RenderTexture)preview.OutputTexture;
            bool captureSupportedQuality = quality == "High Fidelity";
            string directory = Path.GetDirectoryName(evidencePath);
            if (captureSupportedQuality)
                ReadMenuPreviewPixels(preview, Path.Combine(directory, "MainMenu-Atmosphere-HighFidelity-On.png"));
            Color[] withLamp = ReadMenuPreviewLinearPixels(output);
            Renderer lampMesh = preview.Environment.GarageLampRenderer;
            int slot = preview.Environment.GarageLampMaterialIndex;
            var originalBlock = new MaterialPropertyBlock();
            lampMesh.GetPropertyBlock(originalBlock, slot);
            var unlitBlock = new MaterialPropertyBlock();
            lampMesh.GetPropertyBlock(unlitBlock, slot);
            unlitBlock.SetVector("_EmissiveColor", Vector4.zero);
            bool lampEnabled = preview.GarageLampLight.enabled;
            RectInt lampPixels = MenuLampPixelBounds(preview);
            int changedLampPixels;
            try
            {
                preview.GarageLampLight.enabled = false;
                lampMesh.SetPropertyBlock(unlitBlock, slot);
                yield return RefreshMenuAtmosphereFrames(preview, paint);
                changedLampPixels = CountMenuAtmospherePixelChanges(withLamp, ReadMenuPreviewLinearPixels(output), output.width, lampPixels);
                Assert.That(changedLampPixels, Is.GreaterThan(16), quality + ": the lamp's actual emission/light must affect rendered pixels.");
                if (captureSupportedQuality)
                    ReadMenuPreviewPixels(preview, Path.Combine(directory, "MainMenu-Atmosphere-HighFidelity-LampOff.png"));
            }
            finally
            {
                lampMesh.SetPropertyBlock(originalBlock, slot);
                preview.GarageLampLight.enabled = lampEnabled;
                preview.SetPaint(paint);
            }
            yield return RefreshMenuAtmosphereFrames(preview, paint);
            File.AppendAllText(evidencePath, quality + ": lamp/emission on-off changed pixels=" + changedLampPixels +
                "; pipeline.supportVolumetrics=" + supportsVolumetrics + "\n");
            if (!supportsVolumetrics)
            {
                File.AppendAllText(evidencePath, "Rear-fog pixels not claimed for this quality; its authored volumetric feature is disabled.\n");
                yield break;
            }

            Color[] firstWithFog = ReadMenuPreviewLinearPixels(output);
            yield return RefreshMenuAtmosphereFrames(preview, paint);
            Color[] withFog = ReadMenuPreviewLinearPixels(output);
            var noise = MeasureMenuAtmosphereDifference(firstWithFog, withFog);
            if (captureSupportedQuality)
                ReadMenuPreviewPixels(preview, Path.Combine(directory, "MainMenu-Atmosphere-HighFidelity-On.png"));
            bool fogEnabled = preview.RearFog.enabled;
            Color[] withoutFog = null;
            try
            {
                preview.RearFog.enabled = false;
                yield return RefreshMenuAtmosphereFrames(preview, paint);
                withoutFog = ReadMenuPreviewLinearPixels(output);
                if (captureSupportedQuality)
                    ReadMenuPreviewPixels(preview, Path.Combine(directory, "MainMenu-Atmosphere-HighFidelity-FogOff.png"));
            }
            finally
            {
                preview.RearFog.enabled = fogEnabled;
                preview.SetPaint(paint);
            }
            yield return RefreshMenuAtmosphereFrames(preview, paint);
            Color[] restoredFog = ReadMenuPreviewLinearPixels(output);
            var fogEffect = MeasureMenuAtmosphereDifference(withFog, withoutFog);
            var restoration = MeasureMenuAtmosphereDifference(withFog, restoredFog);
            File.AppendAllText(evidencePath, quality + ": rear-fog on/off/on; pixels with RGB delta > 0.02=" +
                fogEffect.changedPixels + "/" + withFog.Length + "; mean RGB delta=" + fogEffect.meanDelta.ToString("G9") +
                "; unchanged-on noise mean=" + noise.meanDelta.ToString("G9") +
                "; restored-on mean error=" + restoration.meanDelta.ToString("G9") + "\n");
            Assert.That(fogEffect.changedPixels, Is.GreaterThan(withFog.Length / 200),
                quality + ": rear haze must visibly change at least 0.5% of the image; isolated residual pixels are not evidence.");
            Assert.That(fogEffect.meanDelta, Is.GreaterThan(Mathf.Max(0.001f, noise.meanDelta * 10f)),
                quality + ": fog effect must exceed an unchanged-state noise baseline and a visible minimum.");
            Assert.That(restoration.meanDelta, Is.LessThan(Mathf.Max(0.0002f, fogEffect.meanDelta * 0.1f)),
                quality + ": restoring the same local fog must reproduce its original image.");
        }

        private static IEnumerator RefreshMenuAtmosphereFrames(MainMenuVehiclePreview preview, Color paint)
        {
            // HDRP submits LocalVolumetricFog draw calls before ScriptRunBehaviourUpdate.
            // A toggle followed by our same-frame LateUpdate StandardRequest can still
            // see the prior submission. Cross that player-loop boundary before both
            // independent requests; this does not change the runtime idle-render policy.
            for (int pass = 0; pass < 2; pass++)
            {
                yield return null;
                int frame = preview.RenderCount + 1;
                preview.SetPaint(paint);
                yield return AwaitMenuVehicleFrame(preview, frame);
            }
        }

        private static (int changedPixels, float meanDelta) MeasureMenuAtmosphereDifference(Color[] before, Color[] after)
        {
            Assert.That(after, Is.Not.Null);
            Assert.That(after.Length, Is.EqualTo(before.Length));
            int changed = 0;
            double total = 0d;
            for (int index = 0; index < before.Length; index++)
            {
                Color difference = before[index] - after[index];
                float delta = Mathf.Abs(difference.r) + Mathf.Abs(difference.g) + Mathf.Abs(difference.b);
                total += delta;
                if (delta > 0.02f) changed++;
            }
            return (changed, (float)(total / before.Length));
        }

        private static RectInt MenuLampPixelBounds(MainMenuVehiclePreview preview)
        {
            Bounds bounds = preview.Environment.GarageLampRenderer.bounds;
            Vector2 min = Vector2.one * float.PositiveInfinity;
            Vector2 max = Vector2.one * float.NegativeInfinity;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 sign = new Vector3((corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                Vector3 point = preview.PreviewCamera.WorldToViewportPoint(bounds.center + Vector3.Scale(bounds.extents, sign));
                Assert.That(point.z, Is.GreaterThan(0f));
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            int width = preview.OutputTexture.width;
            int height = preview.OutputTexture.height;
            int xMin = Mathf.Clamp(Mathf.FloorToInt(min.x * width) - 2, 0, width);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(min.y * height) - 2, 0, height);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(max.x * width) + 2, 0, width);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(max.y * height) + 2, 0, height);
            Assert.That(xMax - xMin, Is.GreaterThan(0), "Lamp must appear in the actual camera view.");
            Assert.That(yMax - yMin, Is.GreaterThan(0));
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static int CountMenuAtmospherePixelChanges(Color[] before, Color[] after, int width = 0, RectInt? region = null)
        {
            Assert.That(after.Length, Is.EqualTo(before.Length));
            int changed = 0;
            for (int index = 0; index < before.Length; index++)
            {
                if (region.HasValue && !region.Value.Contains(new Vector2Int(index % width, index / width))) continue;
                Color delta = before[index] - after[index];
                if (Mathf.Abs(delta.r) + Mathf.Abs(delta.g) + Mathf.Abs(delta.b) > 0.001f) changed++;
            }
            return changed;
        }
    }
}
#endif
