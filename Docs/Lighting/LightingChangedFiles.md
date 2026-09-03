# Local lighting changed-file inventory

This inventory records the project-owned files created or intentionally changed
for the 2026-08-11 local-lighting task. Unity `.meta` files beside new folders,
scripts and assets are part of the change but are not repeated line by line.
Unrelated concurrent-session changes are intentionally excluded.

## Runtime visibility regression fixes

- `Assets/Game/Weather/Enviro3Integration/Runtime/Enviro3EnvironmentAdapter.cs`
- `Assets/Game/Tests/EditMode/Enviro3Integration/Enviro3EnvironmentAdapterTests.cs`
- `Assets/Game/Lighting/Runtime/ElectricalGridService.cs`
- `Assets/Game/Lighting/Runtime/LightingRuntimeManager.cs`
- `Assets/Game/Lighting/Integration/Production/ProductionLightingInstaller.cs`
- `Assets/Game/Tests/EditMode/Lighting/ElectricalGridServiceTests.cs`
- `Assets/Game/Tests/EditMode/Lighting/LightingArchitectureTests.cs`

## Runtime and integration

- `Assets/Game/Lighting/Runtime/MSC.Lighting.Runtime.asmdef`
- `Assets/Game/Lighting/Runtime/LightingTypes.cs`
- `Assets/Game/Lighting/Runtime/LightFixtureProfile.cs`
- `Assets/Game/Lighting/Runtime/ElectricalGridService.cs`
- `Assets/Game/Lighting/Runtime/LightingQualityProfile.cs`
- `Assets/Game/Lighting/Runtime/LightingZone.cs`
- `Assets/Game/Lighting/Runtime/VolumetricBeamAdapter.cs`
- `Assets/Game/Lighting/Runtime/GameLightFixture.cs`
- `Assets/Game/Lighting/Runtime/LightingRuntimeManager.cs`
- `Assets/Game/Lighting/Runtime/LightingProfileCatalog.cs`
- `Assets/Game/Lighting/Runtime/LightingSessionAuthority.cs`
- `Assets/Game/Lighting/Runtime/LightSwitchInteractionTarget.cs`
- `Assets/Game/Lighting/Integration/Production/MSC.Lighting.ProductionIntegration.Runtime.asmdef`
- `Assets/Game/Lighting/Integration/Production/WorldLightingBindingCatalog.cs`
- `Assets/Game/Lighting/Integration/Production/WorldLightingFixtureAdapter.cs`
- `Assets/Game/Lighting/Integration/Production/HomeLightSwitchPresentationAdapter.cs`
- `Assets/Game/Lighting/Integration/Production/EnviroLightingBridge.cs`
- `Assets/Game/Lighting/Integration/Production/LightingProductionAdapters.cs`
- `Assets/Game/Lighting/Integration/Production/ProductionLightingInstaller.cs`
- `Assets/Game/Lighting/Integration/Production/VehicleLightingElectricalAdapter.cs`
- `Assets/Game/Lighting/Integration/Production/LightingValidationRunner.cs`

## Editor tooling

- `Assets/Game/Lighting/Editor/MSC.Lighting.Editor.asmdef`
- `Assets/Game/Lighting/Editor/LightingContentBuilder.cs`
- `Assets/Game/Lighting/Editor/LightingAuditModel.cs`
- `Assets/Game/Lighting/Editor/LightingMapBuilder.cs`
- `Assets/Game/Lighting/Editor/LightingMapBuilderWindow.cs`
- `Assets/Game/Lighting/Editor/WorldLightingBindingCatalogBuilder.cs`
- `Assets/Game/Lighting/Editor/LightingValidationWindow.cs`

## Generated project-owned content

- `Assets/Game/Lighting/Content/Bindings/Phase1WorldLightingBindings.asset`
- `Assets/Game/Lighting/Content/Profiles/Phase1LocalLightingCatalog.asset`
- `Assets/Game/Lighting/Content/Profiles/Phase1LightingCalibration.asset`
- `Assets/Game/Lighting/Content/Profiles/LightingQualityLow.asset`
- `Assets/Game/Lighting/Content/Profiles/LightingQualityMedium.asset`
- `Assets/Game/Lighting/Content/Profiles/LightingQualityHigh.asset`
- `Assets/Game/Lighting/Content/Profiles/LightingQualityUltra.asset`
- `Assets/Game/Lighting/Content/Profiles/DomesticIncandescent.asset`
- `Assets/Game/Lighting/Content/Profiles/EnclosedCeiling.asset`
- `Assets/Game/Lighting/Content/Profiles/Fluorescent.asset`
- `Assets/Game/Lighting/Content/Profiles/TeimoShop.asset`
- `Assets/Game/Lighting/Content/Profiles/TeimoPub.asset`
- `Assets/Game/Lighting/Content/Profiles/FleetariWorkshop.asset`
- `Assets/Game/Lighting/Content/Profiles/StreetLamp.asset`
- `Assets/Game/Lighting/Content/Profiles/ExteriorBuilding.asset`
- `Assets/Game/Lighting/Content/Profiles/VehicleLowBeam.asset`
- `Assets/Game/Lighting/Content/Profiles/VehicleHighBeam.asset`
- `Assets/Game/Lighting/Content/Profiles/VehicleTail.asset`
- `Assets/Game/Lighting/Content/Profiles/VehicleBrake.asset`
- `Assets/Game/Lighting/Content/Profiles/VehicleIndicator.asset`
- `Assets/Game/Lighting/Content/Profiles/VehicleReverse.asset`
- `Assets/Game/Lighting/Content/Profiles/VehicleLicensePlate.asset`
- `Assets/Game/Lighting/Content/Profiles/VehicleDashboard.asset`
- `Assets/Game/Lighting/Content/Profiles/VehicleInterior.asset`
- `Assets/Game/Lighting/Content/Profiles/PlayerFlashlight.asset`

## Existing world and save integration extended

- `Assets/Game/World/Runtime/Lighting/WorldLightingProbeCatalog.cs`
- `Assets/Game/World/Runtime/Lighting/WorldLightingProbeRuntime.cs`
- `Assets/Game/Editor/WorldLighting/Phase1WorldLightingProbeBuilder.cs`
- `Assets/Game/World/Content/Lighting/Phase1WorldLightingProbeCatalog.asset`
- `Assets/Game/Save/Integration/MSC.Save.Integration.asmdef`
- `Assets/Game/Save/Integration/LightingSaveParticipant.cs`
- `Assets/Game/Save/Integration/NativeSaveSessionController.cs`

## Tests

- `Assets/Game/Tests/EditMode/Lighting/MSC.Lighting.Tests.EditMode.asmdef`
- `Assets/Game/Tests/EditMode/Lighting/ElectricalGridServiceTests.cs`
- `Assets/Game/Tests/EditMode/Lighting/LightingArchitectureTests.cs`
- `Assets/Game/Tests/EditMode/SaveIntegration/MSC.Save.Integration.Tests.EditMode.asmdef`
- `Assets/Game/Tests/EditMode/SaveIntegration/LightingSaveParticipantTests.cs`
- `Assets/Game/Tests/PlayMode/Lighting/MSC.Lighting.Tests.PlayMode.asmdef`
- `Assets/Game/Tests/PlayMode/Lighting/ProductionLightingLifecycleTests.cs`

## Scene composition and safely configured fixtures

- `Assets/Game/Bootstrap/Bootstrap.unity` (production installer, zones and seven retired
  generated home switch targets; eight real streamed donor buttons are bound at
  runtime and no local fixture is attached to Enviro sun/moon)
- `Assets/Game/Development/WeatherLab/Scenes/WeatherLab.unity` (one interior
  practical and two stationary low-beam validation fixtures)

The audit opened 70 scenes and 168 prefabs. It did not alter donor-generated
content, and it does not attach local fixtures to directional/environment
lights. Other concurrently modified scenes/prefabs are therefore not claimed as
lighting-task changes in this inventory.

## VLB package-owned import

- `Assets/VolumetricLightBeam/` (Tech Salad VLB 2.2.3 package payload)
- `Assets/Resources/VLBConfigOverride.asset`

The vendor directory was imported from the supplied package and was not edited.
The VLB shader/dummy-material/config assets generated by the package itself are
kept with that dependency.

## Reports and provenance

- `Docs/Lighting/LightingAudit.before.csv`
- `Docs/Lighting/LightingAudit.before.json`
- `Docs/Lighting/LightingAudit.csv`
- `Docs/Lighting/LightingAudit.json`
- `Docs/Lighting/LightingManualReview.md`
- `Docs/Lighting/LightingImplementation.md`
- `Docs/Lighting/LightingCalibration.md`
- `Docs/Lighting/LightingPerformanceReport.md`
- `Docs/Lighting/LightingValidationResults.md`
- `Docs/Lighting/LightingChangedFiles.md`
- `Docs/Porting/DONOR_AUDIT.md`
- `Docs/Porting/PORTING_MATRIX.md`
- `Docs/Porting/SYSTEM_MAP.md`
- `Docs/Porting/PORTING_LEDGER.csv`

Local evidence files are under `Reports/Tests/lighting_*` and
`Logs/codex-lighting-audit-final.log`; they are listed here for traceability but
remain ordinary generated evidence rather than runtime inputs.
