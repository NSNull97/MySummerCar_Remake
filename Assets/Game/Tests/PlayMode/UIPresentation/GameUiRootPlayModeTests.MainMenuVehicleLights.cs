#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using MSC.UI.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.PlayMode.UIPresentation
{
    public sealed partial class GameUiRootPlayModeTests
    {
        private static void AssertMenuVehicleLampsAtTime(MainMenuVehiclePreview preview, bool lit)
        {
            Assert.That(preview.Model.LampBindings.Count, Is.EqualTo(4));
            Assert.That(preview.VehicleLights.Count, Is.EqualTo(4));
            Assert.That(preview.VehicleLampNightFactor, Is.EqualTo(lit ? 1f : 0f));
            int front = 0, rear = 0;
            for (int index = 0; index < preview.Model.LampBindings.Count; index++)
            {
                MainMenuVehicleLampBinding binding = preview.Model.LampBindings[index];
                Light light = preview.VehicleLights[index];
                Assert.That(light, Is.Not.Null, binding.LampId);
                Assert.That(light.enabled, Is.EqualTo(lit), binding.LampId);
                Assert.That(light.type, Is.EqualTo(binding.IsHeadlight ? LightType.Spot : LightType.Point));
                Assert.That(light.color, Is.EqualTo(binding.IsHeadlight ? Color.white : Color.red));
                Assert.That(light.useColorTemperature, Is.False);
                Assert.That(light.cullingMask, Is.EqualTo(preview.PreviewCamera.cullingMask));
                Assert.That(float.IsFinite(light.intensity), Is.True);
                if (lit) Assert.That(light.intensity, Is.GreaterThan(0f));
                else Assert.That(light.intensity, Is.Zero);
                Assert.That(Vector3.Distance(light.transform.position,
                    preview.Model.transform.TransformPoint(binding.LocalPosition)), Is.LessThan(0.003f));
                if (binding.IsHeadlight)
                {
                    front++;
                    Assert.That(light.spotAngle, Is.InRange(1f, 179f));
                    Assert.That(Vector3.Dot(light.transform.forward, preview.Model.transform.forward), Is.GreaterThan(0.8f));
                }
                else rear++;
                var block = new MaterialPropertyBlock();
                binding.LensRenderer.GetPropertyBlock(block, binding.MaterialSlot);
                Vector4 radiance = block.GetVector("_EmissiveColor");
                if (!lit)
                    Assert.That(new Vector3(radiance.x, radiance.y, radiance.z), Is.EqualTo(Vector3.zero));
                else if (binding.IsHeadlight)
                {
                    Assert.That(radiance.x, Is.GreaterThan(0f));
                    Assert.That(radiance.x, Is.EqualTo(radiance.y));
                    Assert.That(radiance.y, Is.EqualTo(radiance.z));
                }
                else
                {
                    Assert.That(radiance.x, Is.GreaterThan(0f));
                    Assert.That(radiance.y, Is.Zero);
                    Assert.That(radiance.z, Is.Zero);
                }
            }
            Assert.That(front, Is.EqualTo(2));
            Assert.That(rear, Is.EqualTo(2));
        }

        private static IEnumerator AssertMenuVehicleLampRendering(
            MainMenuVehiclePreview preview, Color paint, string directory, string evidencePath)
        {
            yield return AssertMenuLampGroupIlluminatesGround(preview, paint, true, directory, evidencePath);
            Camera camera = preview.PreviewCamera;
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            float fieldOfView = camera.fieldOfView;
            float nearPlane = camera.nearClipPlane;
            Vector3 rearCenter = Vector3.zero;
            int rearCount = 0;
            foreach (MainMenuVehicleLampBinding binding in preview.Model.LampBindings)
                if (!binding.IsHeadlight)
                {
                    rearCenter += preview.Model.transform.TransformPoint(binding.LocalPosition);
                    rearCount++;
                }
            rearCenter /= rearCount;
            try
            {
                // The production orbit cannot see the rear lenses. This close
                // diagnostic view is test-only, not an added user camera angle.
                // Stay immediately behind the bumper rather than beyond the house.
                camera.transform.position = rearCenter - preview.Model.transform.forward * 0.7f +
                    preview.Model.transform.up * 0.65f;
                Vector3 target = rearCenter - preview.Model.transform.up * 0.25f;
                camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, preview.Model.transform.up);
                camera.fieldOfView = 100f;
                camera.nearClipPlane = 0.03f;
                yield return RefreshMenuAtmosphereFrames(preview, paint);
                yield return AssertMenuLampGroupIlluminatesGround(preview, paint, false, directory, evidencePath);
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.fieldOfView = fieldOfView;
                camera.nearClipPlane = nearPlane;
                preview.SetPaint(paint);
            }
            yield return RefreshMenuAtmosphereFrames(preview, paint);
            Assert.That(camera.transform.position, Is.EqualTo(position));
            Assert.That(camera.transform.rotation, Is.EqualTo(rotation));
            Assert.That(camera.fieldOfView, Is.EqualTo(fieldOfView));
            AssertMenuVehicleInsideFullViewport(preview);
            AssertMenuOrbitCarSlot(preview);
        }

        private static IEnumerator AssertMenuLampGroupIlluminatesGround(
            MainMenuVehiclePreview preview, Color paint, bool headlights, string directory, string evidencePath)
        {
            string label = headlights ? "Headlights" : "RearLights-TestOnlyRearView";
            RectInt ground = MenuLampGroundPixelRegion(preview, headlights);
            var output = (RenderTexture)preview.OutputTexture;
            Color[] firstOn = ReadMenuPreviewLinearPixels(output);
            yield return RefreshMenuAtmosphereFrames(preview, paint);
            Color[] on = ReadMenuPreviewLinearPixels(output);
            var noise = MeasureLampGroundDifference(firstOn, on, output.width, ground);
            ReadMenuPreviewPixels(preview, Path.Combine(directory, "Vehicle-" + label + "-On.png"));
            bool[] enabled = new bool[preview.VehicleLights.Count];
            for (int index = 0; index < enabled.Length; index++) enabled[index] = preview.VehicleLights[index].enabled;
            Color[] off = null;
            try
            {
                for (int index = 0; index < enabled.Length; index++)
                    if (preview.Model.LampBindings[index].IsHeadlight == headlights)
                        preview.VehicleLights[index].enabled = false;
                // Keep lens emission and the other pair unchanged: this pair
                // proves light falling onto geometry, not a glowing material.
                yield return RefreshMenuAtmosphereFrames(preview, paint);
                off = ReadMenuPreviewLinearPixels(output);
                ReadMenuPreviewPixels(preview, Path.Combine(directory, "Vehicle-" + label + "-Off.png"));
            }
            finally
            {
                for (int index = 0; index < enabled.Length; index++)
                    if (preview.VehicleLights[index] != null) preview.VehicleLights[index].enabled = enabled[index];
                preview.SetPaint(paint);
            }
            yield return RefreshMenuAtmosphereFrames(preview, paint);
            var effect = MeasureLampGroundDifference(on, off, output.width, ground);
            var restored = MeasureLampGroundDifference(on, ReadMenuPreviewLinearPixels(output), output.width, ground);
            File.AppendAllText(evidencePath, label + ": actual Light.enabled on/off/on, emission unchanged; ground ROI=" + ground +
                "; changed pixels=" + effect.changed + "; mean RGB delta=" + effect.mean.ToString("G9") +
                "; unchanged noise=" + noise.mean.ToString("G9") + "; restored error=" + restored.mean.ToString("G9") +
                "; signed RGB=" + effect.signedRgb + "\n");
            Assert.That(effect.changed, Is.GreaterThan(Math.Max(16, ground.width * ground.height / 200)),
                label + ": actual lamps must illuminate a visible ground area, not isolated noise pixels.");
            Assert.That(effect.mean, Is.GreaterThan(Mathf.Max(0.0002f, noise.mean * 10f)));
            Assert.That(restored.mean, Is.LessThan(Mathf.Max(0.0002f, effect.mean * 0.1f)));
            Assert.That(effect.signedRgb.x + effect.signedRgb.y + effect.signedRgb.z, Is.GreaterThan(0f));
            if (!headlights)
            {
                Assert.That(effect.signedRgb.x, Is.GreaterThan(Mathf.Abs(effect.signedRgb.y) * 2f));
                Assert.That(effect.signedRgb.x, Is.GreaterThan(Mathf.Abs(effect.signedRgb.z) * 2f),
                    "Rear points must add red illumination to geometry.");
            }
        }

        private static RectInt MenuLampGroundPixelRegion(MainMenuVehiclePreview preview, bool headlights)
        {
            Vector2 minimum = Vector2.one * float.PositiveInfinity;
            Vector2 maximum = Vector2.one * float.NegativeInfinity;
            foreach (MainMenuVehicleLampBinding binding in preview.Model.LampBindings)
            {
                if (binding.IsHeadlight != headlights) continue;
                Bounds bounds = binding.LensRenderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 sign = new Vector3((corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                    Vector3 point = preview.PreviewCamera.WorldToViewportPoint(bounds.center + Vector3.Scale(bounds.extents, sign));
                    Assert.That(point.z, Is.GreaterThan(0f), "Lamp geometry must be in front of this diagnostic camera.");
                    minimum = Vector2.Min(minimum, point);
                    maximum = Vector2.Max(maximum, point);
                }
            }
            int width = preview.OutputTexture.width, height = preview.OutputTexture.height;
            int left = Mathf.Clamp(Mathf.FloorToInt((minimum.x - 0.1f) * width), 0, width);
            int right = Mathf.Clamp(Mathf.CeilToInt((maximum.x + 0.1f) * width), 0, width);
            int top = Mathf.Clamp(Mathf.FloorToInt(minimum.y * height) - 4, 0, height);
            Assert.That(right - left, Is.GreaterThan(20));
            Assert.That(top, Is.GreaterThan(20), "A visible lower ground region is required; hidden rear lamps cannot count as pixel evidence.");
            return new RectInt(left, 0, right - left, top);
        }

        private static (int changed, float mean, Vector3 signedRgb) MeasureLampGroundDifference(
            Color[] on, Color[] off, int width, RectInt region)
        {
            Assert.That(off, Is.Not.Null);
            int changed = 0;
            double absolute = 0d, red = 0d, green = 0d, blue = 0d;
            for (int y = region.yMin; y < region.yMax; y++)
                for (int x = region.xMin; x < region.xMax; x++)
                {
                    Color difference = on[y * width + x] - off[y * width + x];
                    float delta = Mathf.Abs(difference.r) + Mathf.Abs(difference.g) + Mathf.Abs(difference.b);
                    if (delta > 0.005f) changed++;
                    absolute += delta;
                    red += difference.r; green += difference.g; blue += difference.b;
                }
            int count = region.width * region.height;
            return (changed, (float)(absolute / count), new Vector3((float)(red / count), (float)(green / count), (float)(blue / count)));
        }
    }
}
#endif
