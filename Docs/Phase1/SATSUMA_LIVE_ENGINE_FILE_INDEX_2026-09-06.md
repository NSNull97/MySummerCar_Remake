# Live-engine pass — scoped file index

This inventory compares project-owned C#, assembly definitions and manifests with
the source backup taken immediately before this authorized pass. It is **not**
`git diff HEAD`: the worktree already contained unrelated user changes, which
remain outside this inventory. New files are labelled only inside the directories
actually included in that backup. Unity-generated `.meta` sidecars accompany new
Assets files and are not individually repeated below.

## Source and test files

- Modified: `Assets/Game/Audio/Composition/ProductionAudioComposition.cs`
- Modified: `Assets/Game/Audio/Runtime/AudioBackendRouter.cs`
- Modified: `Assets/Game/Audio/Runtime/IAudioBackend.cs`
- Modified: `Assets/Game/Audio/Runtime/SatsumaEngineAudioIds.cs`
- Modified: `Assets/Game/Audio/UnityFallback/UnityAudioBackend.cs`
- New: `Assets/Game/Audio/UnityFallback/UnityAudioCalibrationFilter.cs`
- Modified: `Assets/Game/Audio/UnityFallback/UnityAudioEventLibrary.cs`
- Modified: `Assets/Game/Bootstrap/Development/ProductionDeveloperConsole.cs`
- Modified: `Assets/Game/Bootstrap/ProductionItemEffectsBridge.cs`
- Modified: `Assets/Game/Bootstrap/ProductionPuddleBridge.cs`
- Modified: `Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs`
- New: `Assets/Game/Bootstrap/SatsumaEngineEnvironmentBridge.cs`
- New: `Assets/Game/Bootstrap/SatsumaServiceLevelComposition.cs`
- New: `Assets/Game/Interaction/Runtime/Capabilities/ILiquidLevelPresentationSource.cs`
- Modified: `Assets/Game/Items/Runtime/ItemWorldRuntime.cs`
- New: `Assets/Game/Items/Runtime/ServiceFluidPourController.cs`
- Modified: `Assets/Game/Items/Runtime/WorldItemInstance.cs`
- Modified: `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaBaselineBuilder.cs`
- Modified: `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaDashboardControlsAuthoring.cs`
- Modified: `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaEngineAudioImporter.cs`
- Modified: `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaEngineFastenerPresentation.cs`
- Modified: `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaEngineFeedbackAuthoring.cs`
- New: `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaEngineMotionAudit.cs`
- New: `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaEngineMotionAuthoring.cs`
- Modified: `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaInstalledBeltAssets.cs`
- New: `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaOperatingAuthoring.cs`
- New: `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaServiceCapsAuthoring.cs`
- Modified: `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaStartableCarNightBatch.cs`
- New: `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1SatsumaValveAdjustmentsAuthoring.cs`
- New: `Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1UserSelectedAudioImporter.cs`
- New: `Assets/Game/LegacyImport/Editor/GameplayPresentation/SatsumaLiveEnginePassAuthoring.cs`
- Modified: `Assets/Game/LegacyImport/Manifests/Phase1SatsumaEngineAudioManifest.json`
- New: `Assets/Game/LegacyImport/Manifests/Phase1UserSelectedAudioManifest.json`
- Modified: `Assets/Game/Presentation/Fluid/Runtime/ProceduralFluidStreamPresenter.cs`
- Modified: `Assets/Game/Presentation/Fluid/Runtime/ProceduralPuddlePresenter.cs`
- New: `Assets/Game/Presentation/Fluid/Runtime/ServiceFluidPourPresenter.cs`
- New: `Assets/Game/Presentation/Fluid/Runtime/ServiceReservoirLevelPresenter.cs`
- Modified: `Assets/Game/Save/Integration/VehicleItemSaveRestorePlanFactory.cs`
- New: `Assets/Game/Tests/EditMode/AudioUnityFallback/UnityAudioCalibrationTests.cs`
- Modified: `Assets/Game/Tests/EditMode/Items/ItemRuntimeAndSaveTests.GeneratedConsumables.cs`
- Modified: `Assets/Game/Tests/EditMode/Items/ItemRuntimeAndSaveTests.Ownership.cs`
- New: `Assets/Game/Tests/EditMode/Items/ServiceFluidPourTests.cs`
- Modified: `Assets/Game/Tests/EditMode/LegacyImport/Phase1SatsumaGeneratedContentTests.cs`
- Modified: `Assets/Game/Tests/EditMode/LegacyImport/SatsumaCanonicalNightTestShape.cs`
- Modified: `Assets/Game/Tests/EditMode/LegacyImport/SatsumaFrontAlignmentEditModeTests.cs`
- Modified: `Assets/Game/Tests/EditMode/LegacyImport/SatsumaFrontFastenerEditModeTests.cs`
- Modified: `Assets/Game/Tests/EditMode/LegacyImport/SatsumaFrontFastenerMeshTests.cs`
- Modified: `Assets/Game/Tests/EditMode/LegacyImport/SatsumaFuelLineConnectionTests.cs`
- Modified: `Assets/Game/Tests/EditMode/LegacyImport/SatsumaReviewedGraphShapeTests.cs`
- Modified: `Assets/Game/Tests/EditMode/LegacyImport/SatsumaStartableCarNightBatchTests.cs`
- New: `Assets/Game/Tests/EditMode/LegacyImport/UserSelectedAudioLoudnessAuditTests.cs`
- Modified: `Assets/Game/Tests/EditMode/MSC.Tests.EditMode.asmdef`
- Modified: `Assets/Game/Tests/EditMode/SaveIntegration/CanonicalConsumableNativeSaveTests.cs`
- Modified: `Assets/Game/Tests/EditMode/SaveIntegration/CanonicalHeadlightFastenerSaveTests.cs`
- Modified: `Assets/Game/Tests/EditMode/SaveIntegration/CurrentDomainSaveIntegrationTests.cs`
- Modified: `Assets/Game/Tests/EditMode/SaveIntegration/MSC.Save.Integration.Tests.EditMode.asmdef`
- New: `Assets/Game/Tests/EditMode/SaveIntegration/SatsumaLiveEngineNativeSaveTests.cs`
- New: `Assets/Game/Tests/EditMode/SaveIntegration/SatsumaNativeLightingGraphicsTests.cs`
- Modified: `Assets/Game/Tests/EditMode/VehicleAssembly/SatsumaDashboardControlsTests.cs`
- Modified: `Assets/Game/Tests/EditMode/VehicleAssembly/SatsumaEngineAdjustmentTests.cs`
- Modified: `Assets/Game/Tests/EditMode/VehicleAssembly/SatsumaEngineFeedbackTests.cs`
- New: `Assets/Game/Tests/EditMode/VehicleAssembly/SatsumaEngineMechanicalGraphicsTests.cs`
- New: `Assets/Game/Tests/EditMode/VehicleAssembly/SatsumaEngineMechanicalMotionTests.cs`
- New: `Assets/Game/Tests/EditMode/VehicleAssembly/SatsumaEngineSymptomAudioTests.cs`
- New: `Assets/Game/Tests/EditMode/VehicleAssembly/SatsumaMechanicalConditionTests.cs`
- New: `Assets/Game/Tests/EditMode/VehicleAssembly/SatsumaOperatingSourceTests.cs`
- New: `Assets/Game/Tests/EditMode/VehicleAssembly/SatsumaServiceCapTests.cs`
- New: `Assets/Game/Tests/EditMode/VehicleAssembly/SatsumaServiceLevelGraphicsTests.cs`
- New: `Assets/Game/Tests/EditMode/VehicleAssembly/SatsumaValveAdjustmentTests.cs`
- New: `Assets/Game/Tests/EditMode/VehicleSimulation/SatsumaOperatingModelTests.cs`
- Modified: `Assets/Game/Tests/EditMode/VehicleSimulation/VehicleSimulationEditModeTests.cs`
- Modified: `Assets/Game/Tests/PlayMode/AudioUnityFallback/AudioHybridRoutingPlayModeTests.cs`
- Modified: `Assets/Game/Tests/PlayMode/MSC.Tests.PlayMode.asmdef`
- Modified: `Assets/Game/Tests/PlayMode/PlayerInteraction/SatsumaCabinControlFramePlayModeTests.cs`
- Modified: `Assets/Game/Tests/PlayMode/VehicleAssembly/CanonicalPurchasedBulbHandoffPlayModeTests.cs`
- Modified: `Assets/Game/Tests/PlayMode/VehicleAssembly/CanonicalPurchasedPlugIgnitionPlayModeTests.cs`
- Modified: `Assets/Game/Tests/PlayMode/VehicleAssembly/ProductionSatsumaBootstrapPlayModeTests.cs`
- Modified: `Assets/Game/Tests/PlayMode/VehicleAssembly/SatsumaEngineFeedbackPausePlayModeTests.cs`
- Modified: `Assets/Game/Tests/PlayMode/VehicleAssembly/SatsumaEngineFeedbackPlayModeTests.cs`
- New: `Assets/Game/Tests/PlayMode/VehicleAssembly/SatsumaEngineMechanicalMotionPlayModeTests.cs`
- New: `Assets/Game/Tests/PlayMode/VehicleAssembly/SatsumaEngineSymptomPlayModeTests.cs`
- Modified: `Assets/Game/Vehicle/Assembly/Runtime/AssemblyAlternatorBeltPresentation.cs`
- Modified: `Assets/Game/Vehicle/Assembly/Runtime/AssemblyEngineAdjustmentTarget.cs`
- New: `Assets/Game/Vehicle/Assembly/Runtime/AssemblyMechanicalConditionState.cs`
- New: `Assets/Game/Vehicle/Assembly/Runtime/AssemblyServiceCapState.cs`
- New: `Assets/Game/Vehicle/Assembly/Runtime/AssemblyServiceCapTarget.cs`
- New: `Assets/Game/Vehicle/Assembly/Runtime/AssemblyValveAdjustmentState.cs`
- New: `Assets/Game/Vehicle/Assembly/Runtime/AssemblyValveAdjustmentTarget.cs`
- Modified: `Assets/Game/Vehicle/Assembly/Runtime/IAssemblyItemCondition.cs`
- New: `Assets/Game/Vehicle/Assembly/Runtime/SatsumaRockerShaftFastenerMigration.cs`
- Modified: `Assets/Game/Vehicle/Assembly/Runtime/VehicleAssemblyController.cs`
- Modified: `Assets/Game/Vehicle/Assembly/Runtime/VehicleAssemblyController.DynamicParts.cs`
- Modified: `Assets/Game/Vehicle/Assembly/Runtime/VehicleAssemblySaveData.cs`
- Modified: `Assets/Game/Vehicle/ItemsIntegration/Runtime/ItemPartPresentationBinding.cs`
- Modified: `Assets/Game/Vehicle/Runtime/AssemblyVehiclePrerequisiteAdapter.cs`
- Modified: `Assets/Game/Vehicle/Runtime/SatsumaDashboardControlInteractionTarget.cs`
- Modified: `Assets/Game/Vehicle/Runtime/SatsumaDashboardLightingPresenter.cs`
- Modified: `Assets/Game/Vehicle/Runtime/SatsumaEngineAssemblyReadiness.cs`
- Modified: `Assets/Game/Vehicle/Runtime/SatsumaEngineExhaustBinding.cs`
- Modified: `Assets/Game/Vehicle/Runtime/SatsumaEngineFeedbackPresenter.cs`
- Modified: `Assets/Game/Vehicle/Runtime/SatsumaEngineFeedbackRules.cs`
- New: `Assets/Game/Vehicle/Runtime/SatsumaEngineMechanicalMotion.cs`
- New: `Assets/Game/Vehicle/Runtime/SatsumaEngineOperatingSource.cs`
- New: `Assets/Game/Vehicle/Runtime/SatsumaEngineSymptomAudio.cs`
- New: `Assets/Game/Vehicle/Runtime/SatsumaReciprocatingMesh.cs`
- New: `Assets/Game/Vehicle/Runtime/SatsumaServiceFluidReceiver.cs`
- Modified: `Assets/Game/Vehicle/Runtime/VehiclePersistence.cs`
- Modified: `Assets/Game/Vehicle/Runtime/VehicleSimulationHost.cs`
- New: `Assets/Game/Vehicle/Simulation/SatsumaOperatingContracts.cs`
- New: `Assets/Game/Vehicle/Simulation/SatsumaOperatingModel.cs`
- New: `Assets/Game/Vehicle/Simulation/SatsumaOperatingState.cs`
- Modified: `Assets/Game/Vehicle/Simulation/VehicleSimulationContracts.cs`
- Modified: `Assets/Game/Vehicle/Simulation/VehicleSimulationNodes.cs`
- Modified: `Assets/Game/Vehicle/Simulation/VehicleSimulationRoot.cs`

## Configuration and documentation

- Added read-only `Tools/DonorPipeline/Read-UnityYamlObject.ps1` during the
  initial reference audit; raw donor data remains outside the repository.
- Added `Config/UserAudioPack.example.json`; local `Config/UserAudioPack.local.json`
  is ignored and contains the user-selected source directory.
- Updated `.gitignore` to exclude that local machine configuration.
- Added or updated this report, `SATSUMA_LIVE_ENGINE_PASS_PLAN_2026-09-06.md`,
  `SATSUMA_LIVE_ENGINE_REFERENCE_AUDIT_2026-09-06.md`,
  `SATSUMA_OPERATING_MODEL_PASS_2026-09-06.md`,
  `SATSUMA_SERVICE_FLUID_PASS_2026-09-06.md`,
  `SATSUMA_ENGINE_MECHANICAL_MOTION_2026-09-06.md`,
  `SATSUMA_LIVE_ENGINE_INTEGRATION_2026-09-06.md`,
  `SATSUMA_HEADLIGHT_VISIBILITY_FOLLOWUP_2026-09-06.md` and
  `SATSUMA_ENGINE_PLAYER_GUIDE_RU_2026-09-06.md` under `Docs/Phase1/`.
- Added or updated `Docs/Audio/SATSUMA_USER_AUDIO_PASS_2026-09-06.md`,
  `Docs/Audio/SATSUMA_ENGINE_SYMPTOMS_2026-09-06.md` and
  `Docs/Audio/USER_PACK_LOUDNESS_CALIBRATION_2026-09-06.md`.
- Recorded transfer classes, source hashes, dependencies and evidence in
  `Docs/Porting/PORTING_LEDGER.csv`; updated `DONOR_AUDIT.md`,
  `PORTING_MATRIX.md` and `SYSTEM_MAP.md` in the same directory.
- The audit also produced `SATSUMA_PURCHASED_BATTERY_BOUNDARY_2026-09-06.md`
  and `SATSUMA_ENGINE_PHYSICAL_MOUNT_COMPATIBILITY_PROPOSAL_2026-09-06.md`.
  Those remain proposals, not authorization or claims of implemented ownership
  or chassis/engine-mount changes.

## Generated private content

The existing canonical Satsuma prefab was extended in place by scoped Editor
authorers; there was no full car/map rebuild. The exact rocker-shaft mount's
eight mistaken fastening definitions were retired from its active roster, while
the five real fastening identities and historical definitions were preserved.
Existing valve/service-cap/operating/audio/motion/lens bindings were authored
against reviewed geometry. The known missing belt texture binding was restored.

New private audio clips/libraries and the missing radiator-cap mesh are under
ignored `Assets/Game/LegacyImport/RuntimeBaseline/`. Source media, donor
installation and the user's native save remain unchanged. These generated
assets are TemporaryDirectImport, not new production art. The detailed audio,
service-fluid and motion reports identify exact generated paths and hashes.

Test-created GUID-scoped asset copies and test save slots were cleaned up by
their owning fixtures. Ignored Logs retain XML, captures, PCM audit output and
recoverable pre-authoring copies. No unrelated files were deleted.
