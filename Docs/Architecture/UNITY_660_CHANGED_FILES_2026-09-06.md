# Unity 6000.6 migration: changed-file inventory

Date: 2026-09-06

Status: source-frozen inventory, updated with the three final PlayMode test-only
corrections/diagnostics after the broader EditMode run. This is a migration delta inventory, not a claim that
all graphics, PlayMode, standalone or performance gates have passed.

## Baseline and method

The comparison baseline is the pre-6000.6 local snapshot:

`E:/GAYmDev_Studio/UpgradeBackups/MySummerCar_Remake_before_6000.6.0f1_20260906`

The current checkout is:

`E:/GAYmDev_Studio/MySummerCar_Remake`

Existing `Assets/**/*.cs` files were compared to the same relative snapshot paths
using SHA-256 of their bytes, including ignored local vendor files. Added and
removed files were checked separately. This independently reproduces the main
migration owner's initial count of 72; three subsequent PlayMode test-only files
bring the final inventory to **75 C# files: 73 modified, 2 added, 0 removed**.

The snapshot already contains the accepted pre-upgrade worktree, including
uncommitted gameplay/save/audio work. This report does **not** use the entire
dirty Git diff as the migration delta, and does not attribute pre-snapshot DTO,
ownership, fastener, audio or content changes to the editor upgrade.

`M` means changed relative to the snapshot; `A` means absent in the snapshot.
Every C# path below is exact and repository-relative. These are changed files,
not necessarily entire rewritten files or new gameplay features.

| C# owner | Modified | Added | Total |
| --- | ---: | ---: | ---: |
| Project-owned code and tests (`Assets/Editor`, `Assets/Game`) | 60 | 2 | 62 |
| Local NWH source, including generated input wrappers | 4 | 0 | 4 |
| Wwise managed source | 9 | 0 | 9 |
| Total | 73 | 2 | 75 |

No new temporary C# test fixtures appeared in this scan. Transient test assets
and initialization scenes observed during the simultaneous Unity run are
excluded explicitly below.

## Exact C# inventory

### Project-owned code and tests: 62

This group includes typed transient `EntityId` / `SceneHandle` migration,
compatible light/Editor API changes, inactive Rigidbody save/rollback pose
selection, and regression coverage. The lighting/weather expectation corrections
document pre-existing validation drift; they are not donor-world remodelling.
Stable gameplay identities and native-save schema are not replaced by Unity's
session-local identities.

```text
M Assets/Editor/MSCMapMigration/MapMeshInventoryWriter.cs
M Assets/Editor/MSCMapMigration/MapMeshScanner.cs
M Assets/Editor/MSCMapMigration/MapMigrationModels.cs
M Assets/Editor/MSCMapMigration/Tests/MapMigrationEditModeTests.cs
M Assets/Game/Audio/Runtime/AudioBackendRouter.cs
M Assets/Game/Audio/Runtime/AudioEmitterAuthoring.cs
M Assets/Game/Audio/Runtime/AudioPlaybackContracts.cs
M Assets/Game/Bootstrap/Development/ProductionDeveloperConsole.cs
M Assets/Game/Bootstrap/GameCompositionRoot.cs
M Assets/Game/Bootstrap/ProductionFoodApplianceInstaller.cs
M Assets/Game/Bootstrap/ProductionWorldDoorInstaller.cs
M Assets/Game/Editor/Vegetation/MapVegetationGrassTextureMask.cs
M Assets/Game/Editor/Vegetation/MapVegetationPackedWoodyBuilder.cs
M Assets/Game/Editor/WorldBaseline/Phase1UnifiedTerrainPilotBuilder.cs
M Assets/Game/Items/Runtime/ItemWorldRuntime.Ownership.cs
M Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaEngineAdditionalFastenerPresentation.cs
M Assets/Game/LegacyImport/Runtime/DonorWorldLegacyReplacementRegistry.cs
M Assets/Game/Lighting/Editor/LightingMapBuilder.cs
M Assets/Game/Lighting/Integration/Production/PortableFlashlightLightingAdapter.cs
M Assets/Game/Lighting/Integration/Production/TrafficVehicleLightingAdapter.cs
M Assets/Game/Lighting/Integration/Production/WorldLightingFixtureAdapter.cs
M Assets/Game/Lighting/Runtime/GameLightFixture.cs
M Assets/Game/Presentation/AntiAliasing/Runtime/AntiAliasingController.cs
M Assets/Game/Presentation/AntiAliasing/Runtime/AntiAliasingDebugOverlay.cs
M Assets/Game/Save/Integration/ItemSaveParticipant.cs
M Assets/Game/Save/Integration/VehicleSaveParticipant.cs
M Assets/Game/Save/Integration/WorldEntitySaveParticipant.cs
M Assets/Game/Tests/EditMode/AudioInteractionIntegration/InteractionAudioBridgeTests.cs
M Assets/Game/Tests/EditMode/AudioRuntime/AudioRuntimeEditModeTests.cs
M Assets/Game/Tests/EditMode/Enviro3Integration/WeatherLabRuntimeSmokeTests.cs
M Assets/Game/Tests/EditMode/Items/ItemPartInteractionBoundsTests.cs
A Assets/Game/Tests/EditMode/Items/ItemRuntimeAndSaveTests.InactivePhysicsPose.cs
M Assets/Game/Tests/EditMode/Items/ItemRuntimeAndSaveTests.Ownership.cs
M Assets/Game/Tests/EditMode/SaveIntegration/CanonicalConsumableNativeSaveTests.cs
A Assets/Game/Tests/EditMode/SaveIntegration/CurrentDomainSaveIntegrationTests.InactivePhysicsPose.cs
M Assets/Game/Tests/EditMode/WeatherProductionIntegration/ProductionEnvironmentContentTests.cs
M Assets/Game/Tests/EditMode/WorldRemaster/WorldLightingProbeCatalogTests.cs
M Assets/Game/Tests/PlayMode/AudioUnityFallback/UnityAudioBackendPlayModeTests.cs
M Assets/Game/Tests/PlayMode/NpcFoundation/NpcCharacterAnimationPlayModeTests.cs
M Assets/Game/Tests/PlayMode/Traffic/TrafficRoadAiPlayModeTests.cs
M Assets/Game/Tests/PlayMode/UIPresentation/GameUiRootPlayModeTests.MainMenuVehiclePreview.cs
M Assets/Game/Tests/PlayMode/VehicleAssembly/CanonicalPurchasedBulbHandoffPlayModeTests.cs
M Assets/Game/Tests/PlayMode/VehicleAssembly/CanonicalPurchasedPlugIgnitionPlayModeTests.cs
M Assets/Game/Tests/PlayMode/VehicleAssembly/VehicleAssemblyPlayModeTests.cs
M Assets/Game/Tests/PlayMode/WeatherProduction/ProductionEnvironmentLifecyclePlayModeTests.cs
M Assets/Game/Tests/PlayMode/WorldBaseline/DonorWorldCellizationPlayModeTests.cs
M Assets/Game/Tests/PlayMode/WorldBaseline/DonorWorldStreamingPerformancePlayModeTests.cs
M Assets/Game/Tests/Shared/AudioWwise/RecordingWwiseSoundEngineApi.cs
M Assets/Game/UI/Presentation/Runtime/MainMenuVehicleLights.cs
M Assets/Game/UI/Presentation/Runtime/MainMenuVehiclePreview.cs
M Assets/Game/Vehicle/NWH/Runtime/NwhAssemblyWheelSupportController.cs
M Assets/Game/Vehicle/Runtime/VehiclePersistence.cs
M Assets/Game/Weather/Enviro3Integration/Editor/Enviro3PreflightValidator.cs
M Assets/Game/Weather/Enviro3Integration/Editor/Enviro3PreflightWindow.cs
M Assets/Game/Weather/Enviro3Integration/Editor/ProductionEnvironmentValidationWindow.cs
M Assets/Game/Weather/Enviro3Integration/Editor/ProductionEnvironmentValidator.cs
M Assets/Game/Weather/Enviro3Integration/Runtime/Enviro3ShelterRemovalBridge.cs
M Assets/Game/Weather/Production/LegacyBaseline/Runtime/DonorWorldLegacyWetnessBridge.cs
M Assets/Game/Weather/Production/Runtime/ProductionEnvironmentController.cs
M Assets/Game/Weather/Production/Runtime/Zones/WeatherZoneStreamingBinder.cs
M Assets/Game/World/Runtime/Lighting/WorldLightingProbeRuntime.cs
M Assets/Game/World/Runtime/Streaming/ProductionWorldStreamingService.cs
```

The two new partial test files supply 11 `PhysicsPose_` cases. The separate
existing ownership test file adds two inactive ItemTopology rollback cases.
Existing canonical purchased-part tests retain strict Transform and serialized
pose checks and strengthen active native body-to-mount checks.

### NWH local source: 4

Two files are the bounded Unity 6.6 identity/contact API compatibility patch.
The two `*InputActions.cs` files were regenerated by the existing Input System
importer: generator version `1.19.0 -> 1.20.0` and default action
`priority: 0` fields. Their inspected diffs do not change action IDs or bindings.
These generated wrappers are separate from `NwhUnity660.patch`, which contains
only the two manual compatibility source deltas.

```text
M Assets/NWH/Common/Scripts/Input/InputSystem/SceneInputActions.cs
M Assets/NWH/Common/Scripts/NUI/NUIDrawer.cs
M Assets/NWH/Vehicle Physics 2/Scripts/VehicleController/Input/InputProviders/InputSystem/VehicleInputActions.cs
M Assets/NWH/WheelController/Scripts/WheelController.cs
```

### Wwise managed source: 9

The version-guarded changes preserve full native identity width in registration,
callback resolution and Editor caches. They are a local compatibility patch,
not an official Wwise version upgrade. Native plugins, event/bank IDs and bank
payloads are not part of this source inventory.

```text
M Assets/Wwise/API/Runtime/Handwritten/Common/AkCallbackManager.cs
M Assets/Wwise/API/Runtime/Handwritten/Common/AkUnitySoundEngine.cs
M Assets/Wwise/MonoBehaviour/Editor/AkPortalManager.cs
M Assets/Wwise/MonoBehaviour/Editor/WwiseSetupWizard/AkWwiseSetupWizard.cs
M Assets/Wwise/MonoBehaviour/Runtime/AkRoomPortal.cs
M Assets/Wwise/MonoBehaviour/Runtime/AkSurfaceReflector.cs
M Assets/Wwise/Timeline/Editor/AkEventPlayableInspector.cs
M Assets/Wwise/Timeline/Runtime/AkTimelineEventPlayable.cs
M Assets/Wwise/Utilities/Runtime/AkDelegates.cs
```

## Other migration changes, grouped

### Packages: 2 modified files

- `Packages/manifest.json`
- `Packages/packages-lock.json`

The snapshot comparison confirms these direct dependency changes:

| Direct dependency | Before | After |
| --- | --- | --- |
| `com.unity.collab-proxy` | 2.11.4 | 2.13.6 |
| `com.unity.inputsystem` | 1.19.0 | 1.20.0 |
| `com.unity.multiplayer.center` | 1.0.1 | 2.0.1 |
| `com.unity.render-pipelines.high-definition` | 17.3.0 | 17.6.0 |
| `com.unity.render-pipelines.universal` | 17.3.0 | 17.6.0 |
| `com.unity.timeline` | 1.8.11 | 6.6.0 |
| `com.unity.ugui` | 2.0.0 | 2.6.0 |
| `com.unity.visualscripting` | 1.9.10 | 1.9.12 |

The manifest also adds the editor-bundled `physicscore2d`, `tetgen` and
`timelinefoundation` module declarations and drops obsolete `modules.vr`.
Existing `multiplayer.center` / XR package declarations are not implementation
of multiplayer or VR gameplay. HDRP remains the active renderer; URP remains a
compatibility dependency.

The lock file additionally resolves Core/HDRP config/ShaderGraph/VFX to
`17.6.0`, URP config from `17.0.3` to `17.6.0`, Burst `1.8.28 -> 2.0.0`,
Collections `2.6.2 -> 6.6.0`, Editor Coroutines `1.0.1 -> 6.6.0`,
Test Framework `1.6.0 -> 1.8.0`, NUnit `2.0.5 -> 2.1.0`, Performance Testing
`3.2.0 -> 6.6.0`, Profile Analyzer `1.3.2 -> 1.4.0`, and Searcher
`4.9.4 -> 4.9.5`. New resolved dependencies include
`com.unity.graph-authoring@1.0.0` and `com.unity.profiling.core@1.0.3`.
The lock is the exact dependency record; this paragraph is a compact grouping,
not a replacement lock file.

### Project settings: 3 modified, 2 added

| Kind | Exact path | Scope |
| --- | --- | --- |
| M | `ProjectSettings/ProjectVersion.txt` | Actual editor pin becomes `6000.6.0f1 (f7f8ed4d1e24)`. |
| M | `ProjectSettings/EditorBuildSettings.asset` | Removes obsolete `m_UseUCBPForAssetBundles: 0`; snapshot diff preserves the 233 scene entries and order. Temporary test registrations are not migration content. |
| M | `ProjectSettings/VFXManager.asset` | Adds the package's `m_EditorResources` registration; existing runtime resources remain. |
| A | `ProjectSettings/PhysicsCoreProjectSettings2D.asset` | Editor-generated 2D PhysicsCore settings container with a null settings reference, not a 3D vehicle physics rewrite. |
| A | `ProjectSettings/ProjectAuditorSettings.asset` | Editor-generated auditor settings/default diagnostic parameters. |

No other `ProjectSettings` file differed in the scoped snapshot hash comparison.

### Local configuration and repository instructions

- `Config/DonorPaths.local.json` — only `UnityEditorExecutable` changes from
  the installed 6000.3.11f1 editor to 6000.6.0f1. This machine-local file remains
  ignored by the existing `.gitignore` rule; donor/staging/reference paths stay
  unchanged. Do not commit it or move its absolute path into runtime C#.
- `AGENTS.md` — adds the explicitly user-approved 2026-09-06 exception for
  Unity 6000.6.0f1 / HDRP 17.6.0, correctly identifies 6.6 as the update stream
  rather than LTS, and links the migration/recovery record.
- `.gitignore` — byte-identical to the snapshot; it is not an upgrade change.

### New test metadata: 2 added files

- `Assets/Game/Tests/EditMode/Items/ItemRuntimeAndSaveTests.InactivePhysicsPose.cs.meta`
  — GUID `1c6918d67e8240318728beaa3eab9fc2`.
- `Assets/Game/Tests/EditMode/SaveIntegration/CurrentDomainSaveIntegrationTests.InactivePhysicsPose.cs.meta`
  — GUID `4711a3aaf1a948f4b3b182273b00f509`.

These accompany the two new C# partials above. Existing test script GUIDs were
not regenerated.

### Texture importer metadata: 70 modified files

The all-Assets `.meta` SHA-256 comparison confirms 70 persistent modified
metadata files, all texture importers, grouped as follows:

| Root below `Assets/` | Count |
| --- | ---: |
| `Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/Textures/` | 34 |
| `NWH/Common/` | 4 |
| `NWH/Vehicle Physics 2/` | 30 |
| `NWH/WheelController/` | 2 |
| Total | 70 |

The NWH changes are Editor/publication/demo/UI textures. This is importer
serialization/default metadata, not donor texture remastering. The migration
owner's field-level audit records preserved GUIDs, 2,526 prior root settings
and 253 prior platform blocks, with the iPhone platform label migrated to iOS.
See the main migration record for that semantic audit; this inventory confirms
the changed-file set rather than repeating its entire field-level analysis.

### SRP global settings: 2 modified assets

- `Assets/Settings/HDRPDefaultResources/HDRenderPipelineGlobalSettings.asset`
- `Assets/UniversalRenderPipelineGlobalSettings.asset`

Both assets differ from the snapshot; both accompanying `.meta` files are
byte-identical. The migration record documents the SRP settings-container
migration and preservation of the 410 / 157 prior asset references. These are
pipeline serialization changes, not replacement of the lighting/weather owners.

### Vendor shaders: 3 modified files

- `Assets/Chernobyl/Other/CR_Leaves.shader`
- `Assets/NatureManufacture Assets/Foliage Shaders/NM_Foliage.shader`
- `Assets/NatureManufacture Assets/Foliage Shaders/NM_Cross.shader`

Each snapshot/current hash differs and each `.meta` remains byte-identical.
The bounded patch replaces the obsolete single shadow-quality pragma with the
HDRP 17.6 punctual/directional/area keyword sets. These ignored vendor sources
remain outside Git; reproducible project-owned deltas are listed below.

### Migration documentation and compatibility artifacts

The external backup did not include the complete `Docs/` or `Tools/` trees.
Consequently this is the explicitly migration-owned artifact set, not a claim
derived from diffing unrelated pre-existing documents against an absent backup:

- `Docs/Architecture/UNITY_660_MIGRATION_2026-09-06.md`
- `Docs/Architecture/UNITY_660_VENDOR_COMPATIBILITY_2026-09-06.md`
- `Docs/Architecture/UNITY_660_CHANGED_FILES_2026-09-06.md` (this inventory)
- `Docs/Reviews/UNITY_660_LIGHTING_EXPECTATION_AUDIT_2026-09-06.md`
- `Docs/Reviews/UNITY_660_INACTIVE_RIGIDBODY_POSE_AUDIT_2026-09-06.md`
- `Docs/Reviews/UNITY_660_INACTIVE_PHYSICS_POSE_COMPATIBILITY_2026-09-06.md`
- `Tools/Compatibility/NwhUnity660.patch`
- `Tools/Compatibility/NwhUnity660.hashes.json`
- `Tools/Compatibility/FoliageHdrp176.patch`
- `Tools/Compatibility/FoliageHdrp176.hashes.json`

Patch manifests record original/current byte and normalized SHA-256 values.
They do not authorize force-importing ignored vendor payloads or applying the
patch to a different vendor version.

## Exclusions and interpretation limits

During the running test suite the metadata scan also observed seven temporary
added `.meta` files: the `GeneratedCellLayerAssetFixture` directory metadata,
its five fixture asset/scene metadata files, and a runner initialization scene.
They are not counted among the two persistent added test metadata files:

```text
Assets/Game/World/Debug/Streaming/GeneratedCellLayerAssetFixture.meta
Assets/Game/World/Debug/Streaming/GeneratedCellLayerAssetFixture/Catalog.asset.meta
Assets/Game/World/Debug/Streaming/GeneratedCellLayerAssetFixture/Cell.asset.meta
Assets/Game/World/Debug/Streaming/GeneratedCellLayerAssetFixture/Mask.asset.meta
Assets/Game/World/Debug/Streaming/GeneratedCellLayerAssetFixture/OwnedScene.unity.meta
Assets/Game/World/Debug/Streaming/GeneratedCellLayerAssetFixture/SharedScene.unity.meta
Assets/InitTestScene2bbefe03-d529-4abf-9331-0ccad7a68278.unity.meta
```

Their corresponding temporary assets/scenes and any other
`Assets/InitTestScene*.unity[.meta]` produced by the runner are excluded too.
This inventory did not delete them or interfere with test cleanup.

Rebuilt `Library`, logs/XML, shader/import caches, test reports, generated
diagnostic captures and the externally moved recoverable Burst cache are
execution artifacts, not newly authored migration gameplay content. No donor
installation or user save is included in this inventory.

Pre-existing Enviro fingerprint drift, native Wwise plugin metadata changes
visible only relative to Git HEAD, prior Satsuma/save work and other pre-snapshot
dirty files must not be relabelled as upgrade changes. The snapshot is the
boundary. The separate migration reports retain those evidence/provenance
limitations and validation results.

This inventory was produced using read-only file/hash/diff inspection and an
edit to this document only. No Assets/C# file was changed and no Unity process,
Editor API or test run was started by the inventory worker.
