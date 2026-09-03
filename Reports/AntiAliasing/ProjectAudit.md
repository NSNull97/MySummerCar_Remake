# Anti-Aliasing Project Audit

Generated: `2026-08-17T12:26:08.9961610Z`
Unity / HDRP: `6000.3.11f1` / `17.3.0`
GPU: `Null Device` (`Null`)
Quality: `High Fidelity` (0)

## Pipeline assets

| Asset | Motion vectors | MSAA | Dynamic resolution | Mip bias | DLAA preset | Range | Filter | Upscalers |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `Assets/Settings/HDRP Balanced.asset` | True | None | True | False | 1 | 100–100% | CatmullRom | DLSS |
| `Assets/Settings/HDRP High Fidelity.asset` | True | None | True | False | 1 | 100–100% | CatmullRom | DLSS |
| `Assets/Settings/HDRP Performant.asset` | True | None | True | False | 1 | 100–100% | CatmullRom | DLSS |
| `Assets/Settings/HDRPDefaultResources/HDRenderPipelineAsset.asset` | True | None | True | False | 1 | 100–100% | TAAU | DLSS |

## HDRP Global Settings

| Asset | AA frame setting | Camera MV | Object MV | Transparent MV | Motion Blur | DoF |
| --- | --- | --- | --- | --- | --- | --- |
| `Assets/Settings/HDRPDefaultResources/HDRenderPipelineGlobalSettings.asset` | True | True | True | True | True | True |

## Cameras

| Source | Camera | HD data | Serialized AA | DynRes | DLSS | Policy |
| --- | --- | --- | --- | --- | --- | --- |
| `Assets/Game/Bootstrap/Bootstrap.unity` | `Game Composition Root/ProductionEnvironmentBackend/StartupCameraPlaceholder` | False | Runtime managed | False | False | Auto (implicit) |
| `Assets/Game/Development/WeatherLab/Scenes/WeatherLab.unity` | `WeatherLab_Root/Cameras/ExteriorCamera` | True | None | False | True | Auto (implicit) |
| `Assets/Game/Development/WeatherLab/Scenes/WeatherLab.unity` | `WeatherLab_Root/Cameras/InteriorLookingOutCamera` | True | None | False | True | Auto (implicit) |
| `Assets/Game/Development/WeatherLab/Scenes/WeatherLab.unity` | `WeatherLab_Root/Cameras/VehicleInteriorLookingOutCamera` | True | None | False | True | Auto (implicit) |
| `Assets/Game/LegacyImport/ReferenceOnly/Comparison/DonorProofComparison.unity` | `Comparison Camera` | False | Runtime managed | False | False | Auto (implicit) |
| `Assets/Game/LegacyImport/ReferenceOnly/Comparison/GarageM3Comparison.unity` | `M3_GarageScalePivotComparison_REFERENCE_ONLY/Comparison Camera` | True | None | False | True | Auto (implicit) |
| `Assets/Game/LegacyImport/ReferenceOnly/Comparison/M04A_WorldLayoutPilotComparison.unity` | `Comparison_Camera` | False | Runtime managed | False | False | Auto (implicit) |
| `Assets/Game/LegacyImport/ReferenceOnly/World/Generated/Scenes/WorldTransfer_Bootstrap.unity` | `REFERENCE_ONLY_WORLD_TRANSFER_BOOTSTRAP/WorldTransfer_FreeFlyCamera` | False | Runtime managed | False | False | Auto (implicit) |
| `Assets/Game/Vehicle/Content/Simulation/Scenes/VehicleSimulationPrototype.unity` | `M06_VehicleSimulationPrototype/M06_PrototypeCamera` | False | Runtime managed | False | False | Auto (implicit) |
| `Assets/Game/Vehicle/Content/Validation/Scenes/VehiclePhysicsValidation.unity` | `M06_VehicleSimulationPrototype/M06_PrototypeCamera` | False | Runtime managed | False | False | Auto (implicit) |
| `Assets/Game/World/Content/GaragePrototype/Scenes/GarageArtPrototype.unity` | `M3_GarageArtPrototype/Main Camera` | True | None | False | True | Auto (implicit) |
| `Assets/Game/World/Debug/Comparison/WR_HomeShorelineComparison.unity` | `WR_05A_Batch01_Comparison/Pier Comparison Camera` | True | None | False | True | Auto (implicit) |
| `Assets/Game/World/Debug/Comparison/WR_HomeShorelineComparison.unity` | `WR_05A_Batch01_Comparison/Hedge Comparison Camera` | True | None | False | True | Auto (implicit) |
| `Assets/Game/World/Debug/Comparison/WR_HomeShorelineComparison.unity` | `WR_05A_Batch01_Comparison/Seam Comparison Camera` | True | None | False | True | Auto (implicit) |
| `Assets/Game/World/Debug/Comparison/WR_HomeYardComparison.unity` | `WR_05A_Comparison/Comparison Camera` | True | None | False | True | Auto (implicit) |
| `Assets/Game/Player/Content/Prefabs/M4_FirstPersonPlayer.prefab` | `M4_FirstPersonPlayer/LeanPivot/CameraPivot/ImpactPivot/MotionPivot/LookPitchPivot/FirstPersonCamera` | False | Runtime managed | False | False | Auto (implicit) |

## Material and shader risk summary

- Materials: 739
- Alpha clipped: 31
- Transparent: 30
- Geometric specular AA enabled: 0
- Motion-vector pass enabled: 16
- Referenced textures with/without mipmaps: 514/1
- Alpha-clip materials referencing non-mip textures: 0

Custom shaders:
- `Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items/Source/ExpandedShop/Shader/StandardSpecular.shader` — MotionVectors=False, vertexTime=False, previousTime=False
- `Assets/Game/Presentation/Shaders/Vegetation/MSC_SpruceWindHDRP.shader` — MotionVectors=True, vertexTime=False, previousTime=False
- `Assets/Game/Presentation/Shaders/Vegetation/MSC_VegetationIndirectHDRP.shader` — MotionVectors=True, vertexTime=True, previousTime=True
- `Assets/Game/UI/Presentation/Content/Shaders/M08A_SeparableGaussianBlur.shader` — MotionVectors=False, vertexTime=False, previousTime=False

## Errors

None.

## Warnings

None.

## Notes

- Assets/Game/Bootstrap/Bootstrap.unity:Game Composition Root/ProductionEnvironmentBackend/StartupCameraPlaceholder has no serialized HD camera data; the runtime controller will add non-persistent data when instantiated.
- Assets/Game/Bootstrap/Bootstrap.unity:Game Composition Root/ProductionEnvironmentBackend/StartupCameraPlaceholder serializes allowMSAA=true; the runtime controller disables it for managed HDRP game cameras.
- Assets/Game/LegacyImport/ReferenceOnly/Comparison/DonorProofComparison.unity:Comparison Camera has no serialized HD camera data; the runtime controller will add non-persistent data when instantiated.
- Assets/Game/LegacyImport/ReferenceOnly/Comparison/DonorProofComparison.unity:Comparison Camera serializes allowMSAA=true; the runtime controller disables it for managed HDRP game cameras.
- Assets/Game/LegacyImport/ReferenceOnly/Comparison/M04A_WorldLayoutPilotComparison.unity:Comparison_Camera has no serialized HD camera data; the runtime controller will add non-persistent data when instantiated.
- Assets/Game/LegacyImport/ReferenceOnly/Comparison/M04A_WorldLayoutPilotComparison.unity:Comparison_Camera serializes allowMSAA=true; the runtime controller disables it for managed HDRP game cameras.
- Assets/Game/LegacyImport/ReferenceOnly/World/Generated/Scenes/WorldTransfer_Bootstrap.unity:REFERENCE_ONLY_WORLD_TRANSFER_BOOTSTRAP/WorldTransfer_FreeFlyCamera has no serialized HD camera data; the runtime controller will add non-persistent data when instantiated.
- Assets/Game/LegacyImport/ReferenceOnly/World/Generated/Scenes/WorldTransfer_Bootstrap.unity:REFERENCE_ONLY_WORLD_TRANSFER_BOOTSTRAP/WorldTransfer_FreeFlyCamera serializes allowMSAA=true; the runtime controller disables it for managed HDRP game cameras.
- Assets/Game/Vehicle/Content/Simulation/Scenes/VehicleSimulationPrototype.unity:M06_VehicleSimulationPrototype/M06_PrototypeCamera has no serialized HD camera data; the runtime controller will add non-persistent data when instantiated.
- Assets/Game/Vehicle/Content/Simulation/Scenes/VehicleSimulationPrototype.unity:M06_VehicleSimulationPrototype/M06_PrototypeCamera serializes allowMSAA=true; the runtime controller disables it for managed HDRP game cameras.
- Assets/Game/Vehicle/Content/Validation/Scenes/VehiclePhysicsValidation.unity:M06_VehicleSimulationPrototype/M06_PrototypeCamera has no serialized HD camera data; the runtime controller will add non-persistent data when instantiated.
- Assets/Game/Vehicle/Content/Validation/Scenes/VehiclePhysicsValidation.unity:M06_VehicleSimulationPrototype/M06_PrototypeCamera serializes allowMSAA=true; the runtime controller disables it for managed HDRP game cameras.
- Assets/Game/Player/Content/Prefabs/M4_FirstPersonPlayer.prefab:M4_FirstPersonPlayer/LeanPivot/CameraPivot/ImpactPivot/MotionPivot/LookPitchPivot/FirstPersonCamera has no serialized HD camera data; the runtime controller will add non-persistent data when instantiated.
- Assets/Game/Player/Content/Prefabs/M4_FirstPersonPlayer.prefab:M4_FirstPersonPlayer/LeanPivot/CameraPivot/ImpactPivot/MotionPivot/LookPitchPivot/FirstPersonCamera serializes allowMSAA=true; the runtime controller disables it for managed HDRP game cameras.
- Assets/Settings/HDRPDefaultResources/DefaultSettingsVolumeProfile.asset contains active Motion Blur (active=True, intensity=0,5, override=True); AA comparison captures must override it locally rather than using blur to hide artifacts.
