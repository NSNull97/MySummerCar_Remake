using System;
using MSC.Vehicle;
using MSC.Vehicle.Simulation;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.VehicleSimulation
{
    internal readonly struct VehicleSimulationTrackBuildResult
    {
        public VehicleSimulationTrackBuildResult(Transform resetPose)
        {
            ResetPose = resetPose;
        }

        public Transform ResetPose { get; }
    }

    internal static class VehicleSimulationTrackBuilder
    {
        private const float SegmentLength = 25f;
        private const float TrackWidth = 12f;

        public static VehicleSimulationTrackBuildResult Build(
            Transform parent,
            VehicleSimulationConfig config)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            VehicleSimulationEditorUtility.EnsureFolder(VehicleSimulationPrototypePaths.Materials);
            GameObject trackRoot = new GameObject("M06_BoundedSurfaceTrack");
            trackRoot.transform.SetParent(parent, false);

            CreateSurfaceSegment(
                trackRoot.transform,
                "Paved_00_25m",
                VehicleSurfaceType.Paved,
                0,
                new Color(0.18f, 0.2f, 0.22f));
            CreateSurfaceSegment(
                trackRoot.transform,
                "Gravel_25_50m",
                VehicleSurfaceType.Gravel,
                1,
                new Color(0.34f, 0.29f, 0.2f));
            CreateSurfaceSegment(
                trackRoot.transform,
                "Dirt_50_75m",
                VehicleSurfaceType.Dirt,
                2,
                new Color(0.25f, 0.14f, 0.07f));
            CreateSurfaceSegment(
                trackRoot.transform,
                "Grass_75_100m",
                VehicleSurfaceType.Grass,
                3,
                new Color(0.1f, 0.28f, 0.09f));

            Material barrier = BuildMaterial("M06_Barrier", new Color(0.62f, 0.64f, 0.67f));
            CreateBarrier(trackRoot.transform, "LeftBoundary", new Vector3(-6.25f, 0.5f, 50f), new Vector3(0.5f, 1f, 101f), barrier);
            CreateBarrier(trackRoot.transform, "RightBoundary", new Vector3(6.25f, 0.5f, 50f), new Vector3(0.5f, 1f, 101f), barrier);
            CreateBarrier(trackRoot.transform, "StartBoundary", new Vector3(0f, 0.5f, -0.5f), new Vector3(12f, 1f, 0.5f), barrier);
            CreateBarrier(trackRoot.transform, "EndBoundary", new Vector3(0f, 0.5f, 100.5f), new Vector3(12f, 1f, 0.5f), barrier);

            Transform resetPose = new GameObject("M06_ResetPose").transform;
            resetPose.SetParent(trackRoot.transform, false);
            float staticLoadPerWheel = config.Dynamics.ProvisionalMassKilograms *
                                       Physics.gravity.magnitude /
                                       Mathf.Max(1, config.WheelCount);
            float staticCompression = Mathf.Clamp(
                staticLoadPerWheel / config.Dynamics.SpringRateNewtonPerMeter,
                0f,
                config.Dynamics.SuspensionTravelMeters);
            float equilibriumAnchorHeight = config.Dynamics.WheelRadiusMeters +
                                            config.Dynamics.SuspensionRestLengthMeters -
                                            staticCompression;
            resetPose.localPosition = new Vector3(0f, equilibriumAnchorHeight, 4f);
            resetPose.localRotation = Quaternion.identity;
            return new VehicleSimulationTrackBuildResult(resetPose);
        }

        private static void CreateSurfaceSegment(
            Transform parent,
            string name,
            VehicleSurfaceType surfaceType,
            int segmentIndex,
            Color color)
        {
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = name;
            surface.transform.SetParent(parent, false);
            surface.transform.localPosition = new Vector3(
                0f,
                -0.2f,
                SegmentLength * (segmentIndex + 0.5f));
            surface.transform.localScale = new Vector3(TrackWidth, 0.4f, SegmentLength);
            surface.GetComponent<Renderer>().sharedMaterial = BuildMaterial(
                "M06_Surface_" + surfaceType,
                color);
            surface.AddComponent<VehicleSurfaceMetadataAuthoring>().Configure(surfaceType);
        }

        private static void CreateBarrier(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.name = name;
            barrier.transform.SetParent(parent, false);
            barrier.transform.localPosition = position;
            barrier.transform.localScale = scale;
            barrier.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material BuildMaterial(string name, Color color)
        {
            string path = VehicleSimulationPrototypePaths.Materials + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
                if (shader == null)
                {
                    throw new InvalidOperationException("No compatible shader exists for M06 graybox materials.");
                }

                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
