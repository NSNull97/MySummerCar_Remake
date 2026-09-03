using System.IO;
using System.Text.RegularExpressions;
using MSC.World.Vegetation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class PackedWoodyMotionVectorContractTests
    {
        private const string RendererSourceRelativePath =
            "Game/World/Runtime/Vegetation/PackedWoodyCellRenderer.cs";

        [Test]
        public void PackedMatrixRenderParams_UseCameraOnlyMotionVectors()
        {
            Assert.That(
                PackedWoodyCellRenderer.PackedMatrixMotionVectorMode,
                Is.EqualTo(MotionVectorGenerationMode.Camera),
                "Packed trees submit current Matrix4x4 data only. Object " +
                "motion vectors require previous instance transforms; forced " +
                "zero vectors also misrepresent a moving camera.");

            string sourcePath = Path.Combine(
                Application.dataPath,
                RendererSourceRelativePath);
            Assert.That(File.Exists(sourcePath), Is.True,
                "The packed renderer source is required for this draw-path " +
                "contract check: " + sourcePath);

            string source = File.ReadAllText(sourcePath);
            int renderParamsInitializers = Regex.Matches(
                source,
                @"new\s+RenderParams\s*\(").Count;
            int cameraOnlyBindings = Regex.Matches(
                source,
                @"motionVectorMode\s*=\s*PackedMatrixMotionVectorMode\b")
                .Count;

            Assert.That(renderParamsInitializers, Is.GreaterThan(0),
                "The test could not locate the packed RenderParams draw path.");
            Assert.That(cameraOnlyBindings, Is.EqualTo(renderParamsInitializers),
                "Every packed RenderParams initializer must bind the " +
                "camera-only Matrix4x4 motion-vector contract.");
            Assert.That(
                Regex.IsMatch(
                    source,
                    @"MotionVectorGenerationMode\s*\.\s*" +
                    @"(?:Object|ForceNoMotion)\b"),
                Is.False,
                "The Matrix4x4-only packed renderer must never request " +
                "per-object or forced-zero motion vectors. Both produce " +
                "invalid temporal presentation during camera motion.");
        }
    }
}
