using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using MSC.Player;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.VehicleAssembly
{
    public static class VehicleAssemblyPrototypeBuilder
    {
        private sealed class PartSpec
        {
            public PartSpec(
                string id,
                string name,
                PartCategory category,
                float mass,
                string socket,
                Vector3 mountPosition,
                Vector3 loosePosition,
                bool root = false,
                bool initiallyInstalled = false)
            {
                Id = id;
                Name = name;
                Category = category;
                Mass = mass;
                Socket = socket;
                MountPosition = mountPosition;
                LoosePosition = loosePosition;
                Root = root;
                InitiallyInstalled = initiallyInstalled;
            }

            public string Id { get; }
            public string Name { get; }
            public PartCategory Category { get; }
            public float Mass { get; }
            public string Socket { get; }
            public Vector3 MountPosition { get; }
            public Vector3 LoosePosition { get; }
            public bool Root { get; }
            public bool InitiallyInstalled { get; }
        }

        private static readonly PartSpec[] PartSpecs =
        {
            new PartSpec("vehicle.chassis", "Кузов-основание", PartCategory.Structural, 389f, "", new Vector3(0f, 0.55f, 4f), new Vector3(0f, 0.55f, 4f), root: true),
            new PartSpec("vehicle.trailing_arm_rl", "Задний левый рычаг", PartCategory.Suspension, 9f, "suspension.rear_left", new Vector3(-0.78f, 0.48f, 4.75f), new Vector3(-2.6f, 0.45f, 1.6f), initiallyInstalled: true),
            new PartSpec("vehicle.brake_drum_rl", "Задний левый тормозной барабан", PartCategory.Brake, 6f, "hub.rear_left", new Vector3(-1.02f, 0.52f, 4.75f), new Vector3(-1.45f, 0.35f, 0.6f)),
            new PartSpec("vehicle.wheel_rl", "Заднее левое колесо", PartCategory.Wheel, 15f, "wheel.rear_left", new Vector3(-1.22f, 0.52f, 4.75f), new Vector3(1.45f, 0.42f, 0.8f)),
            new PartSpec("vehicle.battery", "Аккумулятор", PartCategory.Electrical, 12f, "electrical.battery", new Vector3(0.52f, 0.72f, 3.25f), new Vector3(-2.8f, 0.35f, 3.2f)),
            new PartSpec("vehicle.driver_seat", "Сиденье водителя", PartCategory.Interior, 14f, "interior.driver_seat", new Vector3(-0.42f, 0.82f, 4.45f), new Vector3(2.8f, 0.45f, 3.3f)),
            new PartSpec("vehicle.hood", "Капот", PartCategory.Body, 11f, "body.hood", new Vector3(0f, 1.18f, 3.2f), new Vector3(-3f, 0.3f, 5.1f)),
            new PartSpec("vehicle.door_left", "Левая дверь", PartCategory.Body, 13f, "body.door_left", new Vector3(-0.92f, 0.9f, 4.15f), new Vector3(3f, 0.5f, 5.2f)),
            new PartSpec("vehicle.radiator", "Радиатор", PartCategory.Cooling, 7f, "cooling.radiator", new Vector3(0f, 0.76f, 2.95f), new Vector3(-2.8f, 0.45f, 7f)),
            new PartSpec("vehicle.engine_block", "Блок двигателя", PartCategory.Engine, 36f, "engine.block", new Vector3(0f, 0.78f, 3.55f), new Vector3(2.7f, 0.5f, 7f)),
            new PartSpec("vehicle.cylinder_head", "Головка блока", PartCategory.Engine, 13f, "engine.head", new Vector3(0f, 1.08f, 3.55f), new Vector3(-3f, 0.4f, 8.7f)),
            new PartSpec("vehicle.starter", "Стартер", PartCategory.Electrical, 4f, "engine.starter", new Vector3(0.48f, 0.68f, 3.58f), new Vector3(3f, 0.3f, 8.7f)),
            new PartSpec("vehicle.alternator", "Генератор", PartCategory.Electrical, 5f, "engine.alternator", new Vector3(-0.5f, 0.82f, 3.45f), new Vector3(-2.8f, 0.3f, 10.3f)),
            new PartSpec("vehicle.exhaust_manifold", "Выпускной коллектор", PartCategory.Exhaust, 5f, "exhaust.manifold", new Vector3(0.42f, 1.03f, 3.55f), new Vector3(2.8f, 0.3f, 10.3f)),
            new PartSpec("vehicle.intake_manifold", "Впускной коллектор", PartCategory.Engine, 4f, "engine.intake", new Vector3(-0.42f, 1.03f, 3.55f), new Vector3(0f, 0.3f, 11.3f))
        };

        private static readonly Dictionary<string, string> OwnerByPartId = new Dictionary<string, string>
        {
            ["vehicle.trailing_arm_rl"] = "vehicle.chassis",
            ["vehicle.brake_drum_rl"] = "vehicle.trailing_arm_rl",
            ["vehicle.wheel_rl"] = "vehicle.brake_drum_rl",
            ["vehicle.battery"] = "vehicle.chassis",
            ["vehicle.driver_seat"] = "vehicle.chassis",
            ["vehicle.hood"] = "vehicle.chassis",
            ["vehicle.door_left"] = "vehicle.chassis",
            ["vehicle.radiator"] = "vehicle.chassis",
            ["vehicle.engine_block"] = "vehicle.chassis",
            ["vehicle.cylinder_head"] = "vehicle.engine_block",
            ["vehicle.starter"] = "vehicle.engine_block",
            ["vehicle.alternator"] = "vehicle.engine_block",
            ["vehicle.exhaust_manifold"] = "vehicle.cylinder_head",
            ["vehicle.intake_manifold"] = "vehicle.cylinder_head"
        };

        [MenuItem("Tools/MSC Remake/Vehicle Assembly/Build Representative Test Vehicle")]
        public static void Build()
        {
            EnsureFolders();
            Dictionary<FastenerSize, ToolDefinition> tools = BuildTools();
            Dictionary<string, FastenerDefinition> fasteners = BuildFasteners();
            Dictionary<string, GameObject> visualPrefabs = BuildVisualPrefabs();
            Dictionary<string, PartDefinition> parts = BuildPartDefinitions(visualPrefabs);
            Dictionary<string, MountPointDefinition> mounts = BuildMountDefinitions(fasteners);
            BuildScene(parts, mounts, tools);
            EnsureSceneInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"M05_VEHICLE_ASSEMBLY_BUILD_OK version={VehicleAssemblyPrototypePaths.BuilderVersion} parts={PartSpecs.Length} mounts={mounts.Count}");
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("M05 batch builder requires batch mode.");
            }

            Build();
        }

        private static Dictionary<FastenerSize, ToolDefinition> BuildTools()
        {
            var result = new Dictionary<FastenerSize, ToolDefinition>();
            FastenerSize[] sizes =
            {
                FastenerSize.Millimeter8,
                FastenerSize.Millimeter10,
                FastenerSize.Millimeter11,
                FastenerSize.Millimeter13,
                FastenerSize.Millimeter14,
                FastenerSize.Millimeter17
            };
            for (int i = 0; i < sizes.Length; i++)
            {
                FastenerSize size = sizes[i];
                string millimeters = ((int)size).ToString();
                string path = $"{VehicleAssemblyPrototypePaths.ToolDefinitions}/Wrench_{millimeters}.asset";
                ToolDefinition definition = LoadOrCreate<ToolDefinition>(path);
                definition.Configure($"tool.wrench.{millimeters}", $"Ключ {millimeters}", "Wrench", size);
                EditorUtility.SetDirty(definition);
                result.Add(size, definition);
            }

            return result;
        }

        private static Dictionary<string, FastenerDefinition> BuildFasteners()
        {
            var result = new Dictionary<string, FastenerDefinition>(StringComparer.Ordinal);
            foreach (PartSpec spec in PartSpecs)
            {
                if (spec.Root)
                {
                    continue;
                }

                FastenerSize size = GetFastenerSize(spec);
                int stages = spec.Id == "vehicle.brake_drum_rl" ? 8 : GetFastenerStages(spec);
                string id = spec.Id == "vehicle.brake_drum_rl"
                    ? "fastener.drum_rl.boltpm"
                    : "fastener." + spec.Id.Substring("vehicle.".Length);
                string fileName = id.Replace('.', '_') + ".asset";
                FastenerDefinition definition = LoadOrCreate<FastenerDefinition>(
                    VehicleAssemblyPrototypePaths.FastenerDefinitions + "/" + fileName);
                definition.Configure(
                    id,
                    spec.Id == "vehicle.brake_drum_rl" ? "BoltPM заднего барабана" : "Крепёж " + spec.Name,
                    size,
                    stages,
                    FastenerDirection.ClockwiseToTighten,
                    insertOnInstall: true,
                    blocksRemoval: true,
                    ToolCompatibilityRule.Create("Wrench", size));
                EditorUtility.SetDirty(definition);
                result.Add(spec.Id, definition);
            }

            return result;
        }

        private static Dictionary<string, GameObject> BuildVisualPrefabs()
        {
            Material structural = BuildMaterial("M05_Structural", new Color(0.18f, 0.24f, 0.28f));
            Material mechanical = BuildMaterial("M05_Mechanical", new Color(0.3f, 0.32f, 0.34f));
            Material body = BuildMaterial("M05_Body", new Color(0.58f, 0.12f, 0.08f));
            Material electrical = BuildMaterial("M05_Electrical", new Color(0.08f, 0.09f, 0.1f));
            Material interior = BuildMaterial("M05_Interior", new Color(0.2f, 0.12f, 0.07f));

            var result = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (PartSpec spec in PartSpecs)
            {
                string path = VehicleAssemblyPrototypePaths.Prefabs + "/" +
                    spec.Id.Replace('.', '_') + ".prefab";
                GameObject root = new GameObject(spec.Name + " [Project-authored prototype]");
                try
                {
                    GameObject visual;
                    if (spec.Id == "vehicle.brake_drum_rl")
                    {
                        GameObject proof = AssetDatabase.LoadAssetAtPath<GameObject>(
                            VehicleAssemblyPrototypePaths.ReauthoredDrumPrefab);
                        visual = proof != null
                            ? (GameObject)PrefabUtility.InstantiatePrefab(proof)
                            : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    }
                    else
                    {
                        visual = GameObject.CreatePrimitive(GetPrimitive(spec));
                    }

                    visual.name = "Visual";
                    visual.transform.SetParent(root.transform, false);
                    visual.transform.localScale = GetVisualScale(spec);
                    if (spec.Category == PartCategory.Wheel || spec.Category == PartCategory.Brake)
                    {
                        visual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    }

                    Material material = GetMaterial(spec, structural, mechanical, body, electrical, interior);
                    Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        renderers[rendererIndex].sharedMaterial = material;
                    }

                    GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                    result.Add(spec.Id, prefab);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            return result;
        }

        private static Dictionary<string, PartDefinition> BuildPartDefinitions(
            Dictionary<string, GameObject> visualPrefabs)
        {
            var result = new Dictionary<string, PartDefinition>(StringComparer.Ordinal);
            foreach (PartSpec spec in PartSpecs)
            {
                string path = VehicleAssemblyPrototypePaths.PartDefinitions + "/" +
                    spec.Id.Replace('.', '_') + ".asset";
                PartDefinition definition = LoadOrCreate<PartDefinition>(path);
                PartCompatibilityRule[] rules = spec.Root
                    ? Array.Empty<PartCompatibilityRule>()
                    : new[] { PartCompatibilityRule.Create(spec.Socket, OwnerByPartId[spec.Id]) };
                definition.Configure(spec.Id, spec.Name, spec.Category, spec.Mass, visualPrefabs[spec.Id], rules);
                EditorUtility.SetDirty(definition);
                result.Add(spec.Id, definition);
            }

            return result;
        }

        private static Dictionary<string, MountPointDefinition> BuildMountDefinitions(
            Dictionary<string, FastenerDefinition> fasteners)
        {
            var result = new Dictionary<string, MountPointDefinition>(StringComparer.Ordinal);
            foreach (PartSpec spec in PartSpecs)
            {
                if (spec.Root)
                {
                    continue;
                }

                string mountId = GetMountId(spec.Id);
                string path = VehicleAssemblyPrototypePaths.MountDefinitions + "/" +
                    mountId.Replace('.', '_') + ".asset";
                MountPointDefinition definition = LoadOrCreate<MountPointDefinition>(path);
                float positionTolerance = spec.Id == "vehicle.brake_drum_rl" ? 0.42f : 0.55f;
                float angleTolerance = spec.Id == "vehicle.brake_drum_rl" ? 35f : 55f;
                definition.Configure(
                    mountId,
                    "Точка: " + spec.Name,
                    spec.Socket,
                    OwnerByPartId[spec.Id],
                    new[] { spec.Id },
                    new MountConstraint(positionTolerance, angleTolerance, 1.4f, 0.09f),
                    spec.Id == "vehicle.brake_drum_rl" ? 0.01f : 0f,
                    new[] { fasteners[spec.Id] });
                EditorUtility.SetDirty(definition);
                result.Add(spec.Id, definition);
            }

            return result;
        }

        private static void BuildScene(
            Dictionary<string, PartDefinition> definitions,
            Dictionary<string, MountPointDefinition> mountDefinitions,
            Dictionary<FastenerSize, ToolDefinition> tools)
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VehicleAssemblyPrototypePaths.PlayerPrefab);
            if (playerPrefab == null)
            {
                throw new InvalidOperationException("M4 player prefab is required before building M05.");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("M05_VehicleAssemblyPrototype");
            root.AddComponent<VehicleAssemblyPrototypeMarker>();
            CreateEnvironment(root.transform);

            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.name = "M05_Player_M4Architecture";
            player.transform.SetParent(root.transform);
            player.transform.position = new Vector3(0f, 0f, -4.5f);
            player.transform.rotation = Quaternion.identity;
            if (player.GetComponent<CrossdotPresenter>() == null)
            {
                throw new InvalidOperationException(
                    "M05 requires the shared M4 player prefab with CrossdotPresenter.");
            }

            GameObject assemblyRoot = new GameObject("RepresentativeVehicle_15Parts");
            assemblyRoot.transform.SetParent(root.transform);
            Transform loosePartsRoot = new GameObject("LooseParts").transform;
            loosePartsRoot.SetParent(root.transform);
            Transform vehicleFrame = new GameObject("VehicleFrame").transform;
            vehicleFrame.SetParent(assemblyRoot.transform);

            VehicleAssemblyController controller = assemblyRoot.AddComponent<VehicleAssemblyController>();
            var partInstances = new List<PartInstance>();
            var mountAuthorings = new List<MountPointAuthoring>();
            var previewBindings = new List<AssemblyMountPreviewBinding>();

            foreach (PartSpec spec in PartSpecs)
            {
                PartInstance instance = CreatePartInstance(
                    spec,
                    definitions[spec.Id],
                    spec.Root || spec.InitiallyInstalled ? vehicleFrame : loosePartsRoot,
                    controller);
                partInstances.Add(instance);
            }

            Material mountMaterial = BuildMaterial("M05_Mount", new Color(0.08f, 0.32f, 0.58f));
            Material fastenerMaterial = BuildMaterial("M05_Fastener", new Color(0.75f, 0.58f, 0.12f));
            Material validPreview = BuildMaterial("M05_PreviewValid", new Color(0.05f, 0.8f, 0.18f));
            Material invalidPreview = BuildMaterial("M05_PreviewInvalid", new Color(0.9f, 0.08f, 0.05f));

            foreach (PartSpec spec in PartSpecs)
            {
                if (spec.Root)
                {
                    continue;
                }

                MountPointDefinition mountDefinition = mountDefinitions[spec.Id];
                MountPointAuthoring authoring = CreateMount(
                    spec,
                    mountDefinition,
                    vehicleFrame,
                    controller,
                    tools[GetFastenerSize(spec)],
                    mountMaterial,
                    fastenerMaterial,
                    out Renderer previewRenderer);
                mountAuthorings.Add(authoring);
                previewBindings.Add(AssemblyMountPreviewBinding.Create(authoring, previewRenderer));
            }

            AssemblyDependency[] dependencies = BuildDependencies();
            controller.Configure(
                partInstances.ToArray(),
                mountAuthorings.ToArray(),
                dependencies,
                tools.Values.OrderBy(tool => (int)tool.Size).ToArray(),
                loosePartsRoot);

            for (int i = 0; i < partInstances.Count; i++)
            {
                AssemblyInstalledPartInteractionTarget removal =
                    partInstances[i].GetComponent<AssemblyInstalledPartInteractionTarget>();
                removal.Configure(controller, partInstances[i]);
                InteractionTargetHost host = partInstances[i].GetComponent<InteractionTargetHost>();
                host.Configure(partInstances[i].PickupTarget, removal);
            }

            for (int i = 0; i < mountAuthorings.Count; i++)
            {
                MountPointAuthoring mount = mountAuthorings[i];
                mount.GetComponent<AssemblyMountHandoffTarget>().Configure(controller, mount);
                AssemblyFastenerInteractionTarget fastenerTarget =
                    mount.GetComponentInChildren<AssemblyFastenerInteractionTarget>(true);
                FastenerDefinition fastener = mount.Definition.Fasteners[0];
                fastenerTarget.Configure(
                    controller,
                    mount.MountId,
                    fastener.DefinitionId,
                    tools[fastener.Size],
                    reverseAtLimits: true);
            }

            AssemblyMountPreviewPresenter preview = assemblyRoot.AddComponent<AssemblyMountPreviewPresenter>();
            preview.Configure(
                controller,
                player.GetComponent<PhysicalCarryController>(),
                validPreview,
                invalidPreview,
                previewBindings.ToArray());

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, VehicleAssemblyPrototypePaths.PrototypeScene))
            {
                throw new InvalidOperationException("Failed to save M05 prototype scene.");
            }
        }

        private static PartInstance CreatePartInstance(
            PartSpec spec,
            PartDefinition definition,
            Transform parent,
            VehicleAssemblyController controller)
        {
            GameObject part = new GameObject(spec.Name);
            part.transform.SetParent(parent);
            part.transform.position = spec.Root || spec.InitiallyInstalled
                ? spec.MountPosition
                : spec.LoosePosition;
            part.transform.rotation = Quaternion.identity;

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(definition.VisualPrefab);
            visual.transform.SetParent(part.transform, false);
            visual.name = "Visual";

            Rigidbody body = part.AddComponent<Rigidbody>();
            body.mass = spec.Mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            if (spec.Root)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            StableEntityIdAuthoring identity = part.AddComponent<StableEntityIdAuthoring>();
            SetStableId(identity, Hash128.Compute("m05.part.instance." + spec.Id).ToString());
            PhysicsPickupTarget pickup = part.AddComponent<PhysicsPickupTarget>();
            pickup.Configure(body, identity, "Поднять: " + spec.Name, 45f);
            PartInstance instance = part.AddComponent<PartInstance>();
            instance.Configure(
                definition,
                identity,
                body,
                pickup,
                spec.Root,
                spec.InitiallyInstalled ? GetMountId(spec.Id) : string.Empty);
            part.AddComponent<AssemblyInstalledPartInteractionTarget>();
            part.AddComponent<InteractionTargetHost>();
            return instance;
        }

        private static MountPointAuthoring CreateMount(
            PartSpec spec,
            MountPointDefinition definition,
            Transform parent,
            VehicleAssemblyController controller,
            ToolDefinition tool,
            Material mountMaterial,
            Material fastenerMaterial,
            out Renderer previewRenderer)
        {
            GameObject mount = new GameObject();
            mount.name = "Mount_" + spec.Name;
            mount.transform.SetParent(parent);
            mount.transform.position = spec.MountPosition;
            mount.transform.rotation = Quaternion.identity;
            mount.transform.localScale = Vector3.one;

            GameObject debugMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debugMarker.name = "MountDebugMarker";
            debugMarker.transform.SetParent(mount.transform, false);
            debugMarker.transform.localScale = Vector3.one * 0.13f;
            debugMarker.GetComponent<Renderer>().sharedMaterial = mountMaterial;

            Transform pose = new GameObject("MountPose").transform;
            pose.SetParent(mount.transform, false);
            pose.localPosition = Vector3.zero;
            pose.localRotation = Quaternion.identity;
            pose.localScale = Vector3.one;

            MountPointAuthoring authoring = mount.AddComponent<MountPointAuthoring>();
            authoring.Configure(definition, GetMountId(spec.Id), pose, 0);
            AssemblyMountHandoffTarget handoff = mount.AddComponent<AssemblyMountHandoffTarget>();
            handoff.Configure(controller, authoring);
            mount.AddComponent<InteractionTargetHost>().Configure(handoff);

            GameObject preview = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            preview.name = "MountPreviewGhost";
            preview.transform.SetParent(pose, false);
            preview.transform.localScale = Vector3.one * 0.29f;
            UnityEngine.Object.DestroyImmediate(preview.GetComponent<Collider>());
            previewRenderer = preview.GetComponent<Renderer>();
            previewRenderer.enabled = false;

            GameObject fastener = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fastener.name = "FastenerTarget_" + definition.Fasteners[0].DefinitionId;
            fastener.transform.SetParent(mount.transform);
            fastener.transform.localPosition = new Vector3(0.15f, 0.15f, 0f);
            fastener.transform.localScale = Vector3.one * 0.08f;
            fastener.GetComponent<Renderer>().sharedMaterial = fastenerMaterial;
            AssemblyFastenerInteractionTarget fastenerTarget =
                fastener.AddComponent<AssemblyFastenerInteractionTarget>();
            fastenerTarget.Configure(
                controller,
                authoring.MountId,
                definition.Fasteners[0].DefinitionId,
                tool,
                reverseAtLimits: true);
            fastener.AddComponent<InteractionTargetHost>().Configure(fastenerTarget);
            return authoring;
        }

        private static AssemblyDependency[] BuildDependencies()
        {
            var result = new List<AssemblyDependency>();
            foreach (KeyValuePair<string, string> pair in OwnerByPartId)
            {
                result.Add(AssemblyDependency.Create(
                    pair.Key,
                    pair.Value,
                    AssemblyDependencyKind.InstallRequiresInstalled));
            }

            result.Add(AssemblyDependency.Create(
                "vehicle.brake_drum_rl",
                "vehicle.wheel_rl",
                AssemblyDependencyKind.RemovalBlockedWhileInstalled));
            result.Add(AssemblyDependency.Create(
                "vehicle.trailing_arm_rl",
                "vehicle.brake_drum_rl",
                AssemblyDependencyKind.RemovalBlockedWhileInstalled));
            result.Add(AssemblyDependency.Create(
                "vehicle.engine_block",
                "vehicle.cylinder_head",
                AssemblyDependencyKind.RemovalBlockedWhileInstalled));
            result.Add(AssemblyDependency.Create(
                "vehicle.engine_block",
                "vehicle.starter",
                AssemblyDependencyKind.RemovalBlockedWhileInstalled));
            result.Add(AssemblyDependency.Create(
                "vehicle.engine_block",
                "vehicle.alternator",
                AssemblyDependencyKind.RemovalBlockedWhileInstalled));
            result.Add(AssemblyDependency.Create(
                "vehicle.cylinder_head",
                "vehicle.exhaust_manifold",
                AssemblyDependencyKind.RemovalBlockedWhileInstalled));
            result.Add(AssemblyDependency.Create(
                "vehicle.cylinder_head",
                "vehicle.intake_manifold",
                AssemblyDependencyKind.RemovalBlockedWhileInstalled));
            return result.ToArray();
        }

        private static void CreateEnvironment(Transform parent)
        {
            Material floorMaterial = BuildMaterial("M05_Floor", new Color(0.16f, 0.18f, 0.16f));
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "AssemblyWorkshopFloor";
            floor.transform.SetParent(parent);
            floor.transform.position = new Vector3(0f, -0.1f, 4f);
            floor.transform.localScale = new Vector3(16f, 0.2f, 20f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

            GameObject lightingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                VehicleAssemblyPrototypePaths.NeutralLightingPrefab);
            if (lightingPrefab != null)
            {
                GameObject lighting = (GameObject)PrefabUtility.InstantiatePrefab(lightingPrefab);
                lighting.name = "M05_NeutralLighting";
                lighting.transform.SetParent(parent);
            }
            else
            {
                GameObject light = new GameObject("FallbackDirectionalLight");
                light.transform.SetParent(parent);
                Light component = light.AddComponent<Light>();
                component.type = LightType.Directional;
                component.intensity = 3f;
                light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            }
        }

        private static Material BuildMaterial(string name, Color color)
        {
            string path = VehicleAssemblyPrototypePaths.Materials + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static T LoadOrCreate<T>(string path)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static PrimitiveType GetPrimitive(PartSpec spec)
        {
            if (spec.Category == PartCategory.Wheel || spec.Category == PartCategory.Brake ||
                spec.Id == "vehicle.starter" || spec.Id == "vehicle.alternator")
            {
                return PrimitiveType.Cylinder;
            }

            return PrimitiveType.Cube;
        }

        private static Vector3 GetVisualScale(PartSpec spec)
        {
            switch (spec.Id)
            {
                case "vehicle.chassis": return new Vector3(1.8f, 0.35f, 3.2f);
                case "vehicle.trailing_arm_rl": return new Vector3(0.18f, 0.16f, 0.75f);
                case "vehicle.brake_drum_rl": return Vector3.one;
                case "vehicle.wheel_rl": return new Vector3(0.62f, 0.18f, 0.62f);
                case "vehicle.battery": return new Vector3(0.42f, 0.32f, 0.28f);
                case "vehicle.driver_seat": return new Vector3(0.48f, 0.72f, 0.58f);
                case "vehicle.hood": return new Vector3(1.55f, 0.08f, 1.25f);
                case "vehicle.door_left": return new Vector3(0.08f, 0.72f, 1.05f);
                case "vehicle.radiator": return new Vector3(0.85f, 0.58f, 0.12f);
                case "vehicle.engine_block": return new Vector3(0.78f, 0.62f, 0.72f);
                case "vehicle.cylinder_head": return new Vector3(0.72f, 0.22f, 0.58f);
                case "vehicle.starter": return new Vector3(0.18f, 0.42f, 0.18f);
                case "vehicle.alternator": return new Vector3(0.24f, 0.25f, 0.24f);
                default: return new Vector3(0.55f, 0.16f, 0.28f);
            }
        }

        private static Material GetMaterial(
            PartSpec spec,
            Material structural,
            Material mechanical,
            Material body,
            Material electrical,
            Material interior)
        {
            switch (spec.Category)
            {
                case PartCategory.Structural: return structural;
                case PartCategory.Body: return body;
                case PartCategory.Electrical: return electrical;
                case PartCategory.Interior: return interior;
                default: return mechanical;
            }
        }

        private static FastenerSize GetFastenerSize(PartSpec spec)
        {
            switch (spec.Id)
            {
                case "vehicle.brake_drum_rl": return FastenerSize.Millimeter14;
                case "vehicle.wheel_rl": return FastenerSize.Millimeter13;
                case "vehicle.battery": return FastenerSize.Millimeter8;
                case "vehicle.driver_seat": return FastenerSize.Millimeter11;
                case "vehicle.engine_block": return FastenerSize.Millimeter17;
                default: return FastenerSize.Millimeter10;
            }
        }

        private static int GetFastenerStages(PartSpec spec)
        {
            switch (spec.Category)
            {
                case PartCategory.Body: return 4;
                case PartCategory.Interior: return 4;
                case PartCategory.Electrical: return 3;
                default: return 5;
            }
        }

        private static string GetMountId(string partId)
        {
            return "mount." + partId.Substring("vehicle.".Length);
        }

        private static void SetStableId(StableEntityIdAuthoring authoring, string stableId)
        {
            if (!StableEntityId.TryParse(stableId, out _))
            {
                throw new InvalidOperationException("Generated stable ID is not canonical: " + stableId);
            }

            var serializedObject = new SerializedObject(authoring);
            serializedObject.FindProperty("stableId").stringValue = stableId;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolders()
        {
            string[] paths =
            {
                VehicleAssemblyPrototypePaths.PartDefinitions,
                VehicleAssemblyPrototypePaths.MountDefinitions,
                VehicleAssemblyPrototypePaths.FastenerDefinitions,
                VehicleAssemblyPrototypePaths.ToolDefinitions,
                VehicleAssemblyPrototypePaths.Materials,
                VehicleAssemblyPrototypePaths.Prefabs,
                VehicleAssemblyPrototypePaths.Scenes
            };
            for (int i = 0; i < paths.Length; i++)
            {
                EnsureFolder(paths[i]);
            }
        }

        private static void EnsureFolder(string path)
        {
            string current = "Assets";
            string[] parts = path.Split('/');
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void EnsureSceneInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => !string.Equals(
                    scene.path,
                    VehicleAssemblyPrototypePaths.PrototypeScene,
                    StringComparison.Ordinal))
                .ToList();
            int m4Index = scenes.FindIndex(scene => string.Equals(
                scene.path,
                "Assets/Game/Player/Content/Scenes/PlayerInteractionPrototype.unity",
                StringComparison.Ordinal));
            int insertIndex = m4Index >= 0 ? m4Index + 1 : Math.Min(1, scenes.Count);
            scenes.Insert(insertIndex, new EditorBuildSettingsScene(
                VehicleAssemblyPrototypePaths.PrototypeScene,
                true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
