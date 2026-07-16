using System;
using System.Collections.Generic;
using MSC.Editor.VehicleSimulation;
using MSC.Vehicle;
using MSC.Vehicle.Simulation;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.VehicleValidation
{
    internal static class VehiclePhysicsValidationCourseBuilder
    {
        private const float GroundThickness = 0.4f;
        private const float GroundTop = 0f;

        public static VehicleValidationRouteAuthoring Build(
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

            VehicleSimulationEditorUtility.EnsureFolder(VehiclePhysicsValidationPaths.Materials);
            var root = new GameObject("M06A_ValidationCourse");
            root.transform.SetParent(parent, false);
            Material paved = BuildMaterial("M06A_Paved", new Color(0.2f, 0.22f, 0.24f));
            Material gravel = BuildMaterial("M06A_Gravel", new Color(0.36f, 0.31f, 0.23f));
            Material dirt = BuildMaterial("M06A_Dirt", new Color(0.28f, 0.16f, 0.08f));
            Material grass = BuildMaterial("M06A_Grass", new Color(0.11f, 0.3f, 0.1f));
            Material marker = BuildMaterial("M06A_Marker", new Color(0.92f, 0.55f, 0.08f));
            Material structure = BuildMaterial("M06A_Structure", new Color(0.58f, 0.61f, 0.65f));

            float spawnHeight = CalculateEquilibriumAnchorHeight(config);
            BuildFlatAndTransitionLane(root.transform, paved);
            BuildSlalomLane(root.transform, paved, marker);
            BuildBumpLane(root.transform, paved, marker);
            BuildSurfaceLane(root.transform, paved, gravel, dirt, grass);
            BuildSlopeLane(root.transform, paved);
            BuildGarageLane(root.transform, paved, structure);

            var sections = new List<VehicleValidationRouteSection>(8)
            {
                Section(
                    root.transform,
                    "flat-acceleration-180m",
                    VehicleValidationRouteSectionKind.FlatAcceleration,
                    new Vector3(25f, GroundTop + spawnHeight, 4f),
                    VehicleSurfaceType.Paved,
                    180f,
                    12f),
                Section(
                    root.transform,
                    "braking-80m",
                    VehicleValidationRouteSectionKind.Braking,
                    new Vector3(25f, GroundTop + spawnHeight, 96f),
                    VehicleSurfaceType.Paved,
                    80f,
                    12f),
                Section(
                    root.transform,
                    "steering-slalom-80m",
                    VehicleValidationRouteSectionKind.SteeringSlalom,
                    new Vector3(45f, GroundTop + spawnHeight, 4f),
                    VehicleSurfaceType.Paved,
                    80f,
                    12f),
                Section(
                    root.transform,
                    "suspension-bump-60m",
                    VehicleValidationRouteSectionKind.SuspensionBump,
                    new Vector3(65f, GroundTop + spawnHeight, 4f),
                    VehicleSurfaceType.Paved,
                    60f,
                    8f,
                    bumpHeightMeters: 0.08f),
                Section(
                    root.transform,
                    "surface-comparison-80m",
                    VehicleValidationRouteSectionKind.SurfaceComparison,
                    new Vector3(85f, GroundTop + spawnHeight, 4f),
                    VehicleSurfaceType.Paved,
                    80f,
                    10f,
                    evidenceBoundary: "Paved/Gravel/Dirt/Grass explicit typed metadata; 20 m each."),
                Section(
                    root.transform,
                    "slope-6deg-30m",
                    VehicleValidationRouteSectionKind.Slope,
                    new Vector3(105f, GroundTop + spawnHeight, 2f),
                    VehicleSurfaceType.Paved,
                    30f,
                    8f,
                    slopeDegrees: 6f),
                Section(
                    root.transform,
                    "garage-clearance-50m",
                    VehicleValidationRouteSectionKind.GarageClearance,
                    new Vector3(125f, GroundTop + spawnHeight, 4f),
                    VehicleSurfaceType.Paved,
                    50f,
                    8f,
                    garageOpeningWidthMeters: 3.12f,
                    garageOpeningHeightMeters: 2.22f,
                    maximumCollisionStepMeters: 0.105f,
                    evidenceBoundary: "Project-authored 05A opening and driveway-to-floor threshold fixture."),
                Section(
                    root.transform,
                    "coplanar-collider-transition",
                    VehicleValidationRouteSectionKind.CollisionTransition,
                    new Vector3(25f, GroundTop + spawnHeight, 84f),
                    VehicleSurfaceType.Paved,
                    24f,
                    12f,
                    maximumCollisionStepMeters: 0.001f,
                    evidenceBoundary: "Two independent coplanar colliders meet at z=90 m with zero authored gap.")
            };

            VehicleValidationRouteAuthoring route =
                root.AddComponent<VehicleValidationRouteAuthoring>();
            route.Configure("m06a.physics-validation-course.v1", sections.ToArray());
            if (!route.Validate(out string failure))
            {
                throw new InvalidOperationException("M06A validation course is invalid: " + failure);
            }

            return route;
        }

        private static void BuildFlatAndTransitionLane(Transform parent, Material material)
        {
            CreateSurface(
                parent,
                "FlatPaved_000_090",
                new Vector3(25f, -GroundThickness * 0.5f, 45f),
                new Vector3(12f, GroundThickness, 90f),
                Quaternion.identity,
                VehicleSurfaceType.Paved,
                material);
            CreateSurface(
                parent,
                "FlatPaved_090_180",
                new Vector3(25f, -GroundThickness * 0.5f, 135f),
                new Vector3(12f, GroundThickness, 90f),
                Quaternion.identity,
                VehicleSurfaceType.Paved,
                material);
        }

        private static void BuildSlalomLane(Transform parent, Material ground, Material marker)
        {
            CreateSurface(
                parent,
                "SlalomGround",
                new Vector3(45f, -GroundThickness * 0.5f, 40f),
                new Vector3(12f, GroundThickness, 80f),
                Quaternion.identity,
                VehicleSurfaceType.Paved,
                ground);
            for (int index = 0; index < 7; index++)
            {
                GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cone.name = $"SlalomMarker_{index:00}";
                cone.transform.SetParent(parent, false);
                cone.transform.localPosition = new Vector3(
                    45f + (index % 2 == 0 ? -1.7f : 1.7f),
                    0.25f,
                    12f + index * 9f);
                cone.transform.localScale = new Vector3(0.22f, 0.25f, 0.22f);
                cone.GetComponent<Renderer>().sharedMaterial = marker;
                UnityEngine.Object.DestroyImmediate(cone.GetComponent<Collider>());
            }
        }

        private static void BuildBumpLane(Transform parent, Material ground, Material marker)
        {
            CreateSurface(
                parent,
                "BumpGround",
                new Vector3(65f, -GroundThickness * 0.5f, 30f),
                new Vector3(8f, GroundThickness, 60f),
                Quaternion.identity,
                VehicleSurfaceType.Paved,
                ground);
            CreateSurface(
                parent,
                "Bump_80mm",
                new Vector3(65f, 0.04f, 30f),
                new Vector3(8f, 0.08f, 0.5f),
                Quaternion.identity,
                VehicleSurfaceType.Paved,
                marker);
        }

        private static void BuildSurfaceLane(
            Transform parent,
            Material paved,
            Material gravel,
            Material dirt,
            Material grass)
        {
            VehicleSurfaceType[] types =
            {
                VehicleSurfaceType.Paved,
                VehicleSurfaceType.Gravel,
                VehicleSurfaceType.Dirt,
                VehicleSurfaceType.Grass
            };
            Material[] materials = { paved, gravel, dirt, grass };
            for (int index = 0; index < types.Length; index++)
            {
                CreateSurface(
                    parent,
                    $"Surface_{types[index]}_{index * 20:000}_{(index + 1) * 20:000}",
                    new Vector3(85f, -GroundThickness * 0.5f, index * 20f + 10f),
                    new Vector3(10f, GroundThickness, 20f),
                    Quaternion.identity,
                    types[index],
                    materials[index]);
            }
        }

        private static void BuildSlopeLane(Transform parent, Material material)
        {
            const float slopeDegrees = 6f;
            const float length = 30f;
            Quaternion rotation = Quaternion.Euler(-slopeDegrees, 0f, 0f);
            float centerHeight = Mathf.Sin(slopeDegrees * Mathf.Deg2Rad) * length * 0.5f;
            CreateSurface(
                parent,
                "Slope_6deg",
                new Vector3(105f, centerHeight - GroundThickness * 0.5f, 15f),
                new Vector3(8f, GroundThickness, length),
                rotation,
                VehicleSurfaceType.Paved,
                material);
        }

        private static void BuildGarageLane(Transform parent, Material ground, Material structure)
        {
            CreateSurface(
                parent,
                "GarageApproach",
                new Vector3(125f, -GroundThickness * 0.5f, 15f),
                new Vector3(8f, GroundThickness, 30f),
                Quaternion.identity,
                VehicleSurfaceType.Paved,
                ground);
            CreateSurface(
                parent,
                "GarageFloor_105mm",
                new Vector3(125f, 0.105f - GroundThickness * 0.5f, 40f),
                new Vector3(8f, GroundThickness, 20f),
                Quaternion.identity,
                VehicleSurfaceType.Paved,
                ground);

            const float openingWidth = 3.12f;
            const float openingHeight = 2.22f;
            const float pillarWidth = 0.5f;
            float pillarX = openingWidth * 0.5f + pillarWidth * 0.5f;
            CreateStructure(
                parent,
                "GarageFrameLeft",
                new Vector3(125f - pillarX, openingHeight * 0.5f, 30f),
                new Vector3(pillarWidth, openingHeight, 0.5f),
                structure);
            CreateStructure(
                parent,
                "GarageFrameRight",
                new Vector3(125f + pillarX, openingHeight * 0.5f, 30f),
                new Vector3(pillarWidth, openingHeight, 0.5f),
                structure);
            CreateStructure(
                parent,
                "GarageFrameHeader",
                new Vector3(125f, openingHeight + 0.25f, 30f),
                new Vector3(openingWidth + pillarWidth * 2f, 0.5f, 0.5f),
                structure);
        }

        private static VehicleValidationRouteSection Section(
            Transform parent,
            string id,
            VehicleValidationRouteSectionKind kind,
            Vector3 spawnPosition,
            VehicleSurfaceType surface,
            float lengthMeters,
            float widthMeters,
            float slopeDegrees = 0f,
            float bumpHeightMeters = 0f,
            float garageOpeningWidthMeters = 0f,
            float garageOpeningHeightMeters = 0f,
            float maximumCollisionStepMeters = 0f,
            string evidenceBoundary = "")
        {
            Transform pose = new GameObject("Spawn_" + id).transform;
            pose.SetParent(parent, false);
            pose.localPosition = spawnPosition;
            pose.localRotation = Quaternion.identity;
            var section = new VehicleValidationRouteSection();
            section.Configure(
                id,
                kind,
                pose,
                surface,
                lengthMeters,
                widthMeters,
                slopeDegrees,
                bumpHeightMeters,
                garageOpeningWidthMeters,
                garageOpeningHeightMeters,
                maximumCollisionStepMeters,
                evidenceBoundary);
            return section;
        }

        private static GameObject CreateSurface(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            VehicleSurfaceType surface,
            Material material)
        {
            GameObject result = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.name = name;
            result.transform.SetParent(parent, false);
            result.transform.localPosition = localPosition;
            result.transform.localRotation = localRotation;
            result.transform.localScale = localScale;
            result.GetComponent<Renderer>().sharedMaterial = material;
            result.AddComponent<VehicleSurfaceMetadataAuthoring>().Configure(surface);
            GameObjectUtility.SetStaticEditorFlags(
                result,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            return result;
        }

        private static void CreateStructure(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject result = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.name = name;
            result.transform.SetParent(parent, false);
            result.transform.localPosition = position;
            result.transform.localScale = scale;
            result.GetComponent<Renderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(
                result,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        private static float CalculateEquilibriumAnchorHeight(VehicleSimulationConfig config)
        {
            float staticLoadPerWheel = config.Dynamics.ProvisionalMassKilograms *
                                       Physics.gravity.magnitude /
                                       Mathf.Max(1, config.WheelCount);
            float staticCompression = Mathf.Clamp(
                staticLoadPerWheel / config.Dynamics.SpringRateNewtonPerMeter,
                0f,
                config.Dynamics.SuspensionTravelMeters);
            return config.Dynamics.WheelRadiusMeters +
                   config.Dynamics.SuspensionRestLengthMeters -
                   staticCompression;
        }

        private static Material BuildMaterial(string name, Color color)
        {
            string path = VehiclePhysicsValidationPaths.Materials + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "No compatible shader exists for M06A validation materials.");
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
