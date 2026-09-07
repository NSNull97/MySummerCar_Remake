using System;
using System.Collections.Generic;
using MSC.Interaction.Architecture;
using MSC.Interaction.Query;
using MSC.LegacyImport;
using MSC.Weather.System.Local;
using MSC.World.Streaming;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Adds project-owned interaction, motion and collision to the confirmed
    /// hand-operated doors in the temporary donor world. Stable transfer IDs
    /// are the only presentation binding contract; donor names are evidence,
    /// never runtime lookup keys.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionWorldDoorInstaller : MonoBehaviour
    {
        public const float DoorSwingAngleDegrees = 85f;
        public const float DoorAngularSpeedDegrees = 200f;
        public const float GarageDoorSwingAngleDegrees = 140f;
        public const float GarageDoorAngularSpeedDegrees = 145f;
        public const float GarageDoorAccelerationDegrees = 1100f;
        public const float GarageDoorReleaseDecelerationDegrees = 1300f;
        public const float HandleZoneRadiusMeters = 0.24f;

        // ConfigurationTransferred from the frozen 04A1 GAME hierarchy and
        // the v002 door-collider disposition. Static facade panels, appliance
        // doors and schedule/visibility-controlled garage doors are excluded.
        private static readonly DoorDefinition[] DoorDefinitions =
        {
            SingleLeaf(
                "door.home.front", "cell_0_-3",
                "9c5d1f176cabbc3945c75ebc533e5b36",
                "b0cb3c552a1e6eb113e47cd44ac07534",
                new Vector3(157.08923f, 2.1022449f, -1030.2072f),
                new Quaternion(0.00000009916323f, 0.70710707f, 0.7071066f, 0.000000047458474f)),
            SingleLeaf(
                "door.home.middle", "cell_0_-3",
                "c2b775474751925da4392c39bdf96e64",
                "2dce712cd1a109387a2edbe14a6d1ed2",
                new Vector3(158.486f, 2.1032448f, -1035.6061f),
                new Quaternion(0.50000095f, 0.50000006f, 0.4999991f, -0.5f)),
            SingleLeaf(
                "door.home.bedroom1", "cell_0_-3",
                "8e8328b424c86a899501d521b5cbb4c9",
                "8cff53e3e532cf07a6219bc806b579c6",
                new Vector3(160.40845f, 2.1022458f, -1029.7656f),
                new Quaternion(0.50000054f, 0.50000024f, 0.49999964f, -0.49999964f)),
            SingleLeaf(
                "door.home.rear", "cell_0_-3",
                "2a6586c9aa8d82db1de3688dabaeb65c",
                "ca17bc7c04823b516fc70d00674c8957",
                new Vector3(158.9466f, 2.0732446f, -1036.8734f),
                new Quaternion(-0.70710707f, 0.0000000561999f, -0.000000107904775f, 0.7071065f)),
            SingleLeaf(
                "door.home.bedroom2", "cell_0_-3",
                "1d2e264981873aa720f6877d923d4f28",
                "8e4358dd09c873210d0655794c424113",
                new Vector3(164.80537f, 2.1022468f, -1029.8127f),
                new Quaternion(-0.50000006f, 0.5000007f, 0.49999982f, 0.49999946f)),
            SingleLeaf(
                "door.home.pantry", "cell_0_-3",
                "b0393c5a1c9189b95deb3fbf7f1b79b4",
                "356dcfed984cc00d9e3bd5293d617620",
                new Vector3(157.17102f, 2.1002445f, -1034.4698f),
                new Quaternion(-0.7071075f, 0.00000059821065f, 0.00000044590206f, 0.7071062f)),
            SingleLeaf(
                "door.home.bathroom", "cell_0_-3",
                "f04cffe7d7987442b9841c06a8cdf34b",
                "1e4857b3e18dac804cd9880753a51adb",
                new Vector3(158.48618f, 2.1032443f, -1038.1184f),
                new Quaternion(0.5000011f, 0.49999997f, 0.49999908f, -0.49999997f)),
            MultiLeaf(
                "door.home.sauna", "cell_0_-3",
                new Vector3(158.49492f, 2.141244f, -1040.8698f),
                new Quaternion(0.50000095f, 0.50000006f, 0.4999991f, -0.5f),
                "9f4cbb4cb4fb884082c6821b30796edd",
                new[]
                {
                    "e866550b15656a7b3c9169827524fe5a",
                    "93bcfceab2dc7d5ab638efa350cb7b3c",
                    "546149d262d083082394dd52dc97b9f4",
                },
                new[] { "e866550b15656a7b3c9169827524fe5a" }),
            SingleLeaf(
                "door.home.wc", "cell_0_-3",
                "42e4d1e2b1d26b9c9c97c969990c458a",
                "12558217d3c7360272096b02d1973cbd",
                new Vector3(161.45358f, 2.102246f, -1029.8125f),
                new Quaternion(-0.50000006f, 0.5000007f, 0.49999982f, 0.49999946f)),
            MultiLeaf(
                "door.home.garage.left", "cell_0_-3",
                new Vector3(154.72856f, 2.0871778f, -1033.2264f),
                new Quaternion(0.0000020448992f, 0.7071068f, 0.7071067f, -0.0000022636366f),
                string.Empty,
                new[]
                {
                    "250d1cb74558e7c7e27e5e860981c938",
                    "55ad5aedde1b470048fe7629bc1785c7",
                },
                new[] { "55ad5aedde1b470048fe7629bc1785c7" },
                swingAngleDegrees: GarageDoorSwingAngleDegrees,
                angularSpeedDegrees: GarageDoorAngularSpeedDegrees,
                requiresDirectionalHold: true),
            MultiLeaf(
                "door.home.garage.right", "cell_0_-3",
                new Vector3(152.26256f, 2.087177f, -1033.2264f),
                new Quaternion(0.0000020448992f, 0.7071068f, 0.7071067f, -0.0000022636366f),
                string.Empty,
                new[]
                {
                    "e8660bda40e1d2946e004456e245c803",
                    "123af7b86ede4a604f3c9f630022cf2f",
                },
                new[] { "123af7b86ede4a604f3c9f630022cf2f" },
                openingSign: -1f,
                swingAngleDegrees: GarageDoorSwingAngleDegrees,
                angularSpeedDegrees: GarageDoorAngularSpeedDegrees,
                requiresDirectionalHold: true),

            SingleLeaf(
                "door.cabin.front", "cell_0_-1",
                "42bcd71dd7850d9622768df6c86a6f29",
                "797913dc13e4ce2febb1a1a68b21d615",
                new Vector3(5.61327f, -1.443f, -21.69025f),
                new Quaternion(-0.5080084f, 0.4918612f, 0.49186134f, 0.5080083f)),
            SingleLeaf(
                "door.cabin.shed.left", "cell_0_-1",
                "45972bddb744670f58602c878841a829", string.Empty,
                new Vector3(2.02227f, -1.000001f, -1.67786f),
                new Quaternion(0.10643678f, 0.69905037f, 0.6990502f, -0.10643662f)),
            SingleLeaf(
                "door.cabin.shed.right", "cell_0_-1",
                "a03bebce4b4c56b1bcc3f706bcdb1ec3", string.Empty,
                new Vector3(2.6175f, -1.000001f, 0.23145f),
                new Quaternion(0.10643678f, 0.69905037f, 0.6990502f, -0.10643662f),
                -1f),

            MultiLeaf(
                "door.cottage.front1", "cell_-2_-2",
                new Vector3(-677.9662f, -0.112f, -532.06674f),
                new Quaternion(0.4221428f, 0.56727016f, 0.56727016f, -0.4221429f),
                "5bcbb77b2dde1412772d97f752c5eb91",
                new[]
                {
                    "fd6af920e38e4103f71d45a3003226f1",
                    "314739b2eb56885d1cdd176b40ffab2b",
                    "4dca067382c394b76b9c1c013686e70a",
                },
                new[]
                {
                    "fd6af920e38e4103f71d45a3003226f1",
                    "314739b2eb56885d1cdd176b40ffab2b",
                    "4dca067382c394b76b9c1c013686e70a",
                }),
            MultiLeaf(
                "door.cottage.front2", "cell_-2_-2",
                new Vector3(-679.7565f, -0.112f, -532.6035f),
                new Quaternion(0.4221428f, 0.56727016f, 0.5672701f, -0.4221429f),
                "8a54e48af105c750bc8af94e40678d85",
                new[]
                {
                    "9e314af78b18d5581e9f9fcd2a960dec",
                    "75f4d389ba84ffb67c3e77f67e1b2836",
                    "28f594a718dc6e4fb59d150236c4ddf4",
                },
                new[]
                {
                    "9e314af78b18d5581e9f9fcd2a960dec",
                    "75f4d389ba84ffb67c3e77f67e1b2836",
                    "28f594a718dc6e4fb59d150236c4ddf4",
                }),
            SingleLeaf(
                "door.cottage.sauna", "cell_-2_-2",
                "edb3ce7fd532a2950857e27956cce779",
                "246dce1a100824ee19e65ba6fe8739bd",
                new Vector3(-680.7039f, -0.12f, -535.5163f),
                new Quaternion(-0.4221426f, 0.5672704f, -0.5672701f, -0.42214283f)),

            SingleLeaf(
                "door.dancehall.left", "cell_1_0",
                "b23152cad6b00196d43b591154d8d419", string.Empty,
                new Vector3(634.0774f, 12.354013f, 276.5055f),
                new Quaternion(0.000008539848f, 0.7071069f, 0.70710665f, -0.000008350498f)),
            SingleLeaf(
                "door.dancehall.right", "cell_1_0",
                "2e8300d0e1093fff7e14aaa4a8650319", string.Empty,
                new Vector3(634.07745f, 12.354013f, 278.56946f),
                new Quaternion(0.000008539848f, 0.7071069f, 0.70710665f, -0.000008350498f),
                -1f),

            SingleLeaf(
                "door.abandoned.front.left", "cell_2_-1",
                "3a41116b4c9c167353cc794f2a67d5bc", string.Empty,
                new Vector3(1531.8358f, 11.493f, -242.35065f),
                new Quaternion(0.23353268f, 0.6753638f, 0.65706027f, -0.24003778f)),
            SingleLeaf(
                "door.abandoned.front.right", "cell_2_-1",
                "3dd648cd2592e825fbe98d862f8e8778", string.Empty,
                new Vector3(1532.8101f, 11.493f, -243.1434f),
                new Quaternion(0.21950893f, 0.6908998f, 0.64724016f, -0.23569795f),
                -1f),

            SingleLeaf(
                "door.terrace.apartment1.front", "cell_-3_0",
                "aca526e9646006b30c150177974d95e3",
                "886df828025087d9b6b86e2214c84e27",
                new Vector3(-1117.4485f, 2.810002f, 43.5249f),
                new Quaternion(-0.61758876f, -0.34436095f, -0.3443602f, 0.61758876f)),
            SingleLeaf(
                "door.terrace.apartment1.rear", "cell_-3_0",
                "b435f6403a0457a3b014dc49ff2dd930",
                "25fd98b2cd48d5370b38997935ce6b6a",
                new Vector3(-1122.0465f, 2.81f, 37.10547f),
                new Quaternion(-0.3443607f, 0.6175889f, 0.6175888f, 0.34436002f)),
            SingleLeaf(
                "door.terrace.white.north", "cell_-3_0",
                "584c75be6d1caf39d2ccf27c3090667a",
                "4ad3abc9add02a87a50d42164d3abba5",
                new Vector3(-1119.352f, 2.814f, 43.09424f),
                new Quaternion(-0.6802011f, 0.19320108f, 0.19320163f, 0.6802007f)),
            SingleLeaf(
                "door.terrace.white.south", "cell_-3_0",
                "8df688c7352bc6907b68c5c8f95c27f2",
                "34798f0d7e2d82c9f4e55b95dede7433",
                new Vector3(-1120.4412f, 2.845001f, 39.8822f),
                new Quaternion(-0.6802011f, 0.19320108f, 0.19320163f, 0.6802007f)),

            SingleLeaf(
                "door.teimo.pub", "cell_-3_0",
                "e91f2cc3e2cd67b51ab177768d30b616",
                "842f192b7eab90ed5b59e74c41f0be53",
                new Vector3(-1372.2518f, 6.291004f, 143.74182f),
                new Quaternion(-0.6786814f, -0.19847302f, -0.19847277f, 0.67868143f)),
            SingleLeaf(
                "door.teimo.store", "cell_-3_0",
                "ea9d2af3c36fa9ef81aff11a25e6ec54",
                "2f785b45bfabd0495966219d344faacb",
                new Vector3(-1379.6492f, 6.2899985f, 137.93042f),
                new Quaternion(-0.62024206f, 0.33955848f, 0.33955857f, 0.6202417f)),
            SingleLeaf(
                "door.uncle.front", "cell_0_-3",
                "e93af79fd95d65722c719fd246de8c8c",
                "ce7301e1ebac4de10d6ddccd01850bc6",
                new Vector3(198.79207f, 2.596f, -1080.1809f),
                new Quaternion(-0.007614849f, 0.70706576f, 0.70706594f, 0.007614847f)),
            SingleLeaf(
                "door.fleetari.side", "cell_3_-1",
                "16ea65c17671fb74e5fb3c4654ded0a8",
                "94295ec44ac780dde0fcc9acb575a210",
                new Vector3(1722.4943f, 7.472998f, -304.40094f),
                new Quaternion(-0.15141009f, 0.69070596f, 0.69070625f, 0.15141052f)),
            SingleLeaf(
                "door.inspection.side", "cell_-3_0",
                "f873bbb3fdeb66fecaa405172d16efe5",
                "aacf86382d5420e2e754eae8c165d418",
                new Vector3(-1359.3467f, 5.818085f, 217.91736f),
                new Quaternion(-0.15817292f, 0.68918866f, 0.6891893f, 0.15817252f)),
            SingleLeaf(
                "door.inspection.office", "cell_-3_0",
                "37bffd5405797bb4a64b95a60ef14ad2", string.Empty,
                new Vector3(-1361.887f, 5.825079f, 223.1304f),
                new Quaternion(-0.158173f, 0.6891886f, 0.6891893f, 0.1581724f)),
            SingleLeaf(
                "door.waterfacility.front", "cell_-3_0",
                "53498ae52480517fc591b98fbf94bb55",
                "8e9fc179f78bfdd5c1291d9c89f7c23f",
                new Vector3(-1349.034f, 8.059999f, 301.4524f),
                new Quaternion(-0.5032381f, 0.4967408f, 0.4967552f, 0.5032238f)),
        };

        private readonly Dictionary<string, DoorRuntimeState> retainedStates =
            new Dictionary<string, DoorRuntimeState>(StringComparer.Ordinal);
        private readonly Dictionary<string, DoorBinding> activeBindings =
            new Dictionary<string, DoorBinding>(StringComparer.Ordinal);

        private ProductionWorldStreamingService worldStreaming;
        private bool initialized;

        public int ExpectedDoorCount => DoorDefinitions.Length;

        public int BoundDoorCount => activeBindings.Count;

        public string LastFailure { get; private set; } = string.Empty;

        public void Initialize(ProductionWorldStreamingService configuredStreaming)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Production world door installer is already initialized.");
            }

            worldStreaming = configuredStreaming ??
                throw new ArgumentNullException(nameof(configuredStreaming));
            worldStreaming.OwnedSceneLoaded += HandleOwnedSceneLoaded;
            worldStreaming.OwnedSceneWillUnload += HandleOwnedSceneWillUnload;
            initialized = true;
            LastFailure = string.Empty;

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isLoaded &&
                    worldStreaming.TryGetCellIdForScene(
                        scene,
                        out string cellId))
                {
                    BindScene(scene, cellId);
                }
            }
        }

        private void HandleOwnedSceneLoaded(Scene scene)
        {
            if (worldStreaming != null &&
                worldStreaming.TryGetCellIdForScene(scene, out string cellId))
            {
                BindScene(scene, cellId);
            }
        }

        private void HandleOwnedSceneWillUnload(Scene scene)
        {
            if (!scene.IsValid())
            {
                return;
            }

            var removeIds = new List<string>();
            foreach (KeyValuePair<string, DoorBinding> pair in activeBindings)
            {
                DoorBinding binding = pair.Value;
                if (binding.SceneHandle != scene.handle)
                {
                    continue;
                }

                retainedStates[pair.Key] =
                    DoorRuntimeState.Capture(binding.Target);
                removeIds.Add(pair.Key);
            }

            for (int index = 0; index < removeIds.Count; index++)
            {
                activeBindings.Remove(removeIds[index]);
            }
        }

        private void BindScene(Scene scene, string cellId)
        {
            if (!scene.IsValid() || !scene.isLoaded ||
                string.IsNullOrWhiteSpace(cellId))
            {
                return;
            }

            var metadataByStableId = new Dictionary<
                string,
                DonorWorldBaselineEntityMetadata>(StringComparer.Ordinal);
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                DonorWorldBaselineEntityMetadata[] metadata =
                    roots[rootIndex].GetComponentsInChildren<
                        DonorWorldBaselineEntityMetadata>(
                        includeInactive: true);
                for (int index = 0; index < metadata.Length; index++)
                {
                    DonorWorldBaselineEntityMetadata entity = metadata[index];
                    if (entity != null &&
                        !string.IsNullOrWhiteSpace(entity.StableId))
                    {
                        metadataByStableId[entity.StableId] = entity;
                    }
                }
            }

            for (int index = 0; index < DoorDefinitions.Length; index++)
            {
                DoorDefinition definition = DoorDefinitions[index];
                if (!string.Equals(
                        definition.CellId,
                        cellId,
                        StringComparison.Ordinal) ||
                    activeBindings.ContainsKey(definition.DoorId))
                {
                    continue;
                }

                if (!TryBindDoor(
                        scene,
                        definition,
                        metadataByStableId,
                        out string failure))
                {
                    AppendFailure(failure);
                }
            }
        }

        private bool TryBindDoor(
            Scene scene,
            in DoorDefinition definition,
            IReadOnlyDictionary<string, DonorWorldBaselineEntityMetadata> metadataByStableId,
            out string failure)
        {
            var movingLeaves = new List<Transform>(
                definition.MovingStableIds.Length);
            var uniqueLeaves = new HashSet<Transform>();
            for (int index = 0; index < definition.MovingStableIds.Length; index++)
            {
                string stableId = definition.MovingStableIds[index];
                if (!metadataByStableId.TryGetValue(
                        stableId,
                        out DonorWorldBaselineEntityMetadata metadata) ||
                    metadata == null)
                {
                    failure =
                        $"Door '{definition.DoorId}' is missing stable leaf " +
                        $"'{stableId}' in {definition.CellId}.";
                    return false;
                }

                if (uniqueLeaves.Add(metadata.transform))
                {
                    movingLeaves.Add(metadata.transform);
                }
            }

            var physicalColliders = new List<Collider>(
                definition.CollisionStableIds.Length);
            for (int index = 0; index < definition.CollisionStableIds.Length; index++)
            {
                string stableId = definition.CollisionStableIds[index];
                if (!metadataByStableId.TryGetValue(
                        stableId,
                        out DonorWorldBaselineEntityMetadata metadata) ||
                    metadata == null)
                {
                    failure =
                        $"Door '{definition.DoorId}' is missing collision leaf " +
                        $"'{stableId}' in {definition.CellId}.";
                    return false;
                }

                physicalColliders.Add(
                    EnsurePhysicalCollider(metadata.gameObject));
            }

            var pivotObject = new GameObject(
                "Door Gameplay - " + definition.DoorId);
            SceneManager.MoveGameObjectToScene(pivotObject, scene);
            Transform pivot = pivotObject.transform;
            pivot.SetPositionAndRotation(
                definition.PivotWorldPosition,
                definition.PivotWorldRotation);
            Quaternion closedLocalRotation = pivot.localRotation;

            for (int index = 0; index < movingLeaves.Count; index++)
            {
                movingLeaves[index].SetParent(pivot, worldPositionStays: true);
            }

            Physics.SyncTransforms();
            Bounds physicalBounds = CalculateBounds(
                physicalColliders,
                definition.DoorId);
            HingedDoorInteractionTarget target =
                pivotObject.AddComponent<HingedDoorInteractionTarget>();
            target.Configure(
                pivot,
                closedLocalRotation,
                pivot.InverseTransformDirection(Vector3.up),
                definition.SwingAngleDegrees,
                definition.AngularSpeedDegrees,
                definition.RequiresDirectionalHold
                    ? "ЛКМ: открыть / ПКМ: закрыть"
                    : "Открыть дверь",
                definition.RequiresDirectionalHold
                    ? "ЛКМ: открыть / ПКМ: закрыть"
                    : "Закрыть дверь",
                definition.OpeningSign,
                definition.RequiresDirectionalHold,
                GarageDoorAccelerationDegrees,
                GarageDoorReleaseDecelerationDegrees);

            Vector3? authoredHandlePosition = TryGetHandlePosition(
                definition.HandleStableId,
                metadataByStableId);
            Renderer[] authoredHandleRenderers =
                TryGetHandleRenderers(
                    definition.HandleStableId,
                    metadataByStableId);
            CreateHandleZone(
                pivot,
                target,
                physicalBounds,
                definition.PivotWorldPosition,
                authoredHandlePosition,
                authoredHandleRenderers);
            if (IsExteriorWeatherDoor(definition.DoorId))
            {
                CreateWeatherPortal(
                    definition.DoorId,
                    pivot,
                    target,
                    physicalBounds,
                    definition.SwingAngleDegrees);
            }

            if (retainedStates.TryGetValue(
                    definition.DoorId,
                    out DoorRuntimeState retainedState))
            {
                retainedState.Restore(target);
            }

            activeBindings.Add(
                definition.DoorId,
                new DoorBinding(scene.handle, target));
            failure = string.Empty;
            return true;
        }

        private static void CreateWeatherPortal(
            string doorId,
            Transform pivot,
            HingedDoorInteractionTarget target,
            in Bounds physicalBounds,
            float fullyOpenAngleDegrees)
        {
            var openingObject = new GameObject("Weather Portal Opening");
            Transform opening = openingObject.transform;
            opening.SetParent(pivot, worldPositionStays: false);
            opening.SetPositionAndRotation(
                physicalBounds.center,
                pivot.rotation);

            Vector3 size = physicalBounds.size;
            var openingSize = new Vector2(
                Mathf.Clamp(Mathf.Max(size.x, size.z), 0.55f, 7f),
                Mathf.Clamp(size.y, 1.2f, 4.5f));
            DoorWeatherPortalAdapter portal =
                pivot.gameObject.AddComponent<DoorWeatherPortalAdapter>();
            portal.Configure(
                "weather.portal." + doorId,
                target,
                null,
                null,
                opening,
                openingSize,
                fullyOpenAngleDegrees);
            LegacyWeatherStreamingBridge.NotifyTopologyChanged();
        }

        private static bool IsExteriorWeatherDoor(string doorId)
        {
            switch (doorId)
            {
                case "door.home.middle":
                case "door.home.bedroom1":
                case "door.home.bedroom2":
                case "door.home.pantry":
                case "door.home.bathroom":
                case "door.home.sauna":
                case "door.home.wc":
                case "door.inspection.office":
                    return false;
                default:
                    return true;
            }
        }

        private static Collider EnsurePhysicalCollider(GameObject collisionLeaf)
        {
            Collider[] existing = collisionLeaf.GetComponents<Collider>();
            for (int index = 0; index < existing.Length; index++)
            {
                if (existing[index] != null && !existing[index].isTrigger)
                {
                    existing[index].enabled = true;
                    return existing[index];
                }
            }

            BoxCollider collider = collisionLeaf.AddComponent<BoxCollider>();
            MeshFilter meshFilter = collisionLeaf.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Bounds meshBounds = meshFilter.sharedMesh.bounds;
                collider.center = meshBounds.center;
                collider.size = EnsureMinimumSize(meshBounds.size, 0.035f);
            }
            else
            {
                // Collider-only transfer rows preserve the original transform
                // scale. A unit box therefore reconstructs the source volume.
                collider.center = Vector3.zero;
                collider.size = Vector3.one;
            }

            collider.isTrigger = false;
            return collider;
        }

        private static Vector3? TryGetHandlePosition(
            string handleStableId,
            IReadOnlyDictionary<string, DonorWorldBaselineEntityMetadata> metadataByStableId)
        {
            if (!string.IsNullOrWhiteSpace(handleStableId) &&
                metadataByStableId.TryGetValue(
                    handleStableId,
                    out DonorWorldBaselineEntityMetadata metadata) &&
                metadata != null)
            {
                return metadata.transform.position;
            }

            return null;
        }

        private static Renderer[] TryGetHandleRenderers(
            string handleStableId,
            IReadOnlyDictionary<string, DonorWorldBaselineEntityMetadata> metadataByStableId)
        {
            if (string.IsNullOrWhiteSpace(handleStableId) ||
                !metadataByStableId.TryGetValue(
                    handleStableId,
                    out DonorWorldBaselineEntityMetadata metadata) ||
                metadata == null)
            {
                return Array.Empty<Renderer>();
            }

            return metadata.GetComponentsInChildren<Renderer>(
                includeInactive: true);
        }

        private static void CreateHandleZone(
            Transform pivot,
            HingedDoorInteractionTarget target,
            in Bounds physicalBounds,
            Vector3 pivotWorldPosition,
            Vector3? authoredHandlePosition,
            Renderer[] authoredHandleRenderers)
        {
            Vector3 handleWorldPosition = authoredHandlePosition ??
                EstimateHandlePosition(
                    pivot,
                    physicalBounds,
                    pivotWorldPosition);

            var handleObject = new GameObject(
                "Door Handle Interaction Zone");
            handleObject.transform.SetParent(pivot, worldPositionStays: false);
            handleObject.transform.position = handleWorldPosition;
            SphereCollider handleCollider =
                handleObject.AddComponent<SphereCollider>();
            handleCollider.radius = HandleZoneRadiusMeters;
            handleCollider.isTrigger = true;
            InteractionTargetHost host =
                handleObject.AddComponent<InteractionTargetHost>();
            host.Configure(target);
            host.ConfigureOutlineRenderers(authoredHandleRenderers);
        }

        private static Vector3 EstimateHandlePosition(
            Transform pivot,
            in Bounds physicalBounds,
            Vector3 pivotWorldPosition)
        {
            Vector3 latchDirection = Vector3.ProjectOnPlane(
                physicalBounds.center - pivotWorldPosition,
                Vector3.up);
            if (latchDirection.sqrMagnitude <= 0.0001f)
            {
                latchDirection = Vector3.ProjectOnPlane(
                    pivot.right,
                    Vector3.up);
            }

            latchDirection.Normalize();
            Vector3 extents = physicalBounds.extents;
            float halfWidthAlongLatch =
                Mathf.Abs(latchDirection.x) * extents.x +
                Mathf.Abs(latchDirection.z) * extents.z;
            Vector3 handleWorldPosition = physicalBounds.center +
                latchDirection * Mathf.Max(
                    0.12f,
                    halfWidthAlongLatch * 0.72f);
            handleWorldPosition.y += Mathf.Min(
                0.1f,
                extents.y * 0.08f);
            return handleWorldPosition;
        }

        private static Bounds CalculateBounds(
            IReadOnlyList<Collider> colliders,
            string doorId)
        {
            if (colliders.Count == 0 || colliders[0] == null)
            {
                throw new InvalidOperationException(
                    $"Door '{doorId}' has no physical collision bounds.");
            }

            Bounds bounds = colliders[0].bounds;
            for (int index = 1; index < colliders.Count; index++)
            {
                if (colliders[index] != null)
                {
                    bounds.Encapsulate(colliders[index].bounds);
                }
            }

            return bounds;
        }

        private static Vector3 EnsureMinimumSize(Vector3 size, float minimum)
        {
            size.x = Mathf.Max(minimum, size.x);
            size.y = Mathf.Max(minimum, size.y);
            size.z = Mathf.Max(minimum, size.z);
            return size;
        }

        private static DoorDefinition SingleLeaf(
            string doorId,
            string cellId,
            string stableId,
            string handleStableId,
            Vector3 pivotWorldPosition,
            Quaternion pivotWorldRotation,
            float openingSign = 1f,
            float swingAngleDegrees = DoorSwingAngleDegrees,
            float angularSpeedDegrees = DoorAngularSpeedDegrees,
            bool requiresDirectionalHold = false) =>
            MultiLeaf(
                doorId,
                cellId,
                pivotWorldPosition,
                pivotWorldRotation,
                handleStableId,
                new[] { stableId },
                new[] { stableId },
                openingSign,
                swingAngleDegrees,
                angularSpeedDegrees,
                requiresDirectionalHold);

        private static DoorDefinition MultiLeaf(
            string doorId,
            string cellId,
            Vector3 pivotWorldPosition,
            Quaternion pivotWorldRotation,
            string handleStableId,
            string[] movingStableIds,
            string[] collisionStableIds,
            float openingSign = 1f,
            float swingAngleDegrees = DoorSwingAngleDegrees,
            float angularSpeedDegrees = DoorAngularSpeedDegrees,
            bool requiresDirectionalHold = false) =>
            new DoorDefinition(
                doorId,
                cellId,
                pivotWorldPosition,
                pivotWorldRotation,
                handleStableId,
                movingStableIds,
                collisionStableIds,
                openingSign,
                swingAngleDegrees,
                angularSpeedDegrees,
                requiresDirectionalHold);

        private void AppendFailure(string failure)
        {
            if (string.IsNullOrWhiteSpace(failure))
            {
                return;
            }

            if (LastFailure.Length > 0)
            {
                LastFailure += Environment.NewLine;
            }

            LastFailure += failure;
            Debug.LogError(failure, this);
        }

        private void OnDestroy()
        {
            if (worldStreaming != null)
            {
                worldStreaming.OwnedSceneLoaded -= HandleOwnedSceneLoaded;
                worldStreaming.OwnedSceneWillUnload -= HandleOwnedSceneWillUnload;
            }

            activeBindings.Clear();
        }

        private readonly struct DoorDefinition
        {
            public DoorDefinition(
                string doorId,
                string cellId,
                Vector3 pivotWorldPosition,
                Quaternion pivotWorldRotation,
                string handleStableId,
                string[] movingStableIds,
                string[] collisionStableIds,
                float openingSign,
                float swingAngleDegrees,
                float angularSpeedDegrees,
                bool requiresDirectionalHold)
            {
                DoorId = doorId;
                CellId = cellId;
                PivotWorldPosition = pivotWorldPosition;
                PivotWorldRotation = pivotWorldRotation.normalized;
                HandleStableId = handleStableId;
                MovingStableIds = movingStableIds;
                CollisionStableIds = collisionStableIds;
                OpeningSign = openingSign < 0f ? -1f : 1f;
                SwingAngleDegrees = Mathf.Clamp(
                    swingAngleDegrees,
                    1f,
                    170f);
                AngularSpeedDegrees = Mathf.Max(
                    1f,
                    angularSpeedDegrees);
                RequiresDirectionalHold = requiresDirectionalHold;
            }

            public string DoorId { get; }
            public string CellId { get; }
            public Vector3 PivotWorldPosition { get; }
            public Quaternion PivotWorldRotation { get; }
            public string HandleStableId { get; }
            public string[] MovingStableIds { get; }
            public string[] CollisionStableIds { get; }
            public float OpeningSign { get; }
            public float SwingAngleDegrees { get; }
            public float AngularSpeedDegrees { get; }
            public bool RequiresDirectionalHold { get; }
        }

        private readonly struct DoorBinding
        {
            public DoorBinding(
                SceneHandle sceneHandle,
                HingedDoorInteractionTarget target)
            {
                SceneHandle = sceneHandle;
                Target = target;
            }

            public SceneHandle SceneHandle { get; }
            public HingedDoorInteractionTarget Target { get; }
        }

        private readonly struct DoorRuntimeState
        {
            private DoorRuntimeState(
                bool targetOpen,
                float openNormalized)
            {
                TargetOpen = targetOpen;
                OpenNormalized = openNormalized;
            }

            private bool TargetOpen { get; }
            private float OpenNormalized { get; }

            public static DoorRuntimeState Capture(
                HingedDoorInteractionTarget target) =>
                target != null
                    ? new DoorRuntimeState(
                        target.TargetOpen,
                        target.OpenNormalized)
                    : default;

            public void Restore(HingedDoorInteractionTarget target)
            {
                target.RestoreState(
                    TargetOpen,
                    OpenNormalized);
            }
        }
    }
}
