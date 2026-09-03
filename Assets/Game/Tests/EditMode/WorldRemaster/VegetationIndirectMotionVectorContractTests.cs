using System.IO;
using System.Text.RegularExpressions;
using MSC.World.Vegetation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class VegetationIndirectMotionVectorContractTests
    {
        private const string RendererSourceRelativePath =
            "Game/World/Runtime/Vegetation/VegetationWorldRenderer.cs";

        [Test]
        public void PackedIndirectGrass_UsesCameraOnlyMotionVectors()
        {
            Assert.That(
                VegetationWorldRenderer.PackedIndirectMotionVectorMode,
                Is.EqualTo(MotionVectorGenerationMode.Camera),
                "The current custom grass motion pass does not implement the " +
                "complete HDRP normal/MRT/stencil prepass contract required " +
                "for safe per-object motion vectors.");

            string sourcePath = Path.Combine(Application.dataPath, RendererSourceRelativePath);
            Assert.That(File.Exists(sourcePath), Is.True, sourcePath);

            string source = File.ReadAllText(sourcePath);
            int renderParamsInitializers = Regex.Matches(source, @"new\s+RenderParams\s*\(").Count;
            int cameraOnlyBindings = Regex.Matches(
                source,
                @"motionVectorMode\s*=\s*PackedIndirectMotionVectorMode\b").Count;

            Assert.That(renderParamsInitializers, Is.GreaterThan(0));
            Assert.That(cameraOnlyBindings, Is.EqualTo(renderParamsInitializers),
                "Every compact indirect grass draw must use the camera-only motion-vector contract.");
            Assert.That(
                Regex.IsMatch(
                    source,
                    @"motionVectorMode\s*=\s*MotionVectorGenerationMode\s*\.\s*" +
                    @"(?:Object|ForceNoMotion)\b"),
                Is.False,
                "Grass must not request incomplete object vectors or forced-zero camera vectors.");
        }
    }
}
