# Unity 6000.6 weather removal-zone expectation audit

Date: 2026-09-06

Status: pre-existing test expectation drift diagnosed; bounded test-only
correction verified by a passing corrected PlayMode lifecycle test in run r3.
The combined r3 suite is not a full PASS; its two remaining failures concern UI
rendering isolation, not this weather assertion.

## Executed historical evidence

`ProductionEnvironmentLifecyclePlayModeTests.Bootstrap_RestoresBeforeRevealAndSurvivesAdditiveLifecycle`
failed at its removal-zone assertion with expected `13`, actual `20` in both:

- `Artifacts/Tests/DonorWorldLightingBootstrapPlayMode-retry.xml`,
  `2026-09-01 20:10:33Z` to `20:10:59Z`, before the Unity 6.6 migration;
- `Logs/unity660-playmode-final-20260906.xml`,
  `2026-09-06 08:18:32Z` to `08:18:39Z`.

Both report the same message and original source line 477. This is not a
newly observed Unity 6.6 zone duplication or a changed removal algorithm.

## What the 20 objects mean

`Enviro3WeatherZoneRemovalBridge.ActiveRemovalZoneCount` counts generated
removal ellipsoids, not logical WeatherZones and not the full streamed catalog's
20 definitions. The unchanged bridge tiles each enabled BoxCollider using
`ceil(long horizontal length / short horizontal length)`, after identifying its
world-up axis. The current saved Bootstrap and home-cell catalog yield:

| Authored volume | Horizontal dimensions used, metres | Ellipsoids |
| --- | --- | ---: |
| House's three compound boxes | `6.0411706 / 2.3143263`; `10.44192 / 8.573909`; `11.431875 / 10.848738` | `3 + 2 + 2 = 7` |
| Garage interior | `9.502197 / 4.589386` | 3 |
| Garage doorway transition | `4.289398 / 0.6290283` | 7 |
| Home-yard machine hall | `18.880299 / 7.7165165` | 3 |
| Total | | 20 |

The old expectation counted house 7 + garage 3 + yard 3 and omitted the seven
doorway ellipsoids. The transition is the second collider of the existing
`weather.shelter.home.garage.interior.v1` zone, with its existing ClosedInterior
profile; it is not seven added logical zones. The initial loaded set contains
three logical zones: house, garage, and
`weather.zone.cell_0_-3.yard.machine_hall.v1`. The separate legacy authored
house/garage `ActiveShelterVolumeCount` remains 2.

`Docs/Lighting/GARAGE_DAYLIGHT_ZONE_CORRECTION_2026-08-14.md` documents the narrow
`GarageEntryExposureTransition`: an interior overlap reaching the reviewed
door threshold with a `0.12 m` blend lip. The existing
`WorldLightingBindingCatalogBuilder.AlignHomeGarageLightingAndExposureZones`
registers both the interior and transition colliders on that same garage zone.
The additional seven ellipsoids therefore follow an already-authored bounded
garage exposure correction, not a count chosen to match the failed output.

## Snapshot comparison

SHA-256 comparisons against
`E:/GAYmDev_Studio/UpgradeBackups/MySummerCar_Remake_before_6000.6.0f1_20260906`
confirmed byte-identical Bootstrap, weather-zone catalog, three zone profiles,
removal bridge, zone registry, WeatherZone component, streaming manifest,
Bootstrap world installer, lighting binding builder, and the failing test
before this expectation correction. The relevant streaming-binder difference
is solely private `int` scene-cache keys/foreach types becoming `SceneHandle`;
zone creation and geometry are unchanged.

## Test-only correction and verification boundary

Only
`Assets/Game/Tests/PlayMode/WeatherProduction/ProductionEnvironmentLifecyclePlayModeTests.cs`
was changed: both the wait condition and assertion now use an exact local
`expectedRemovalZoneCount = 20`, with the `7 + 3 + 7 + 3` explanation and
pre-migration evidence. The test retains the strict two authored shelter-volume
assertion and additionally requires exactly three registry zones with the exact
house/garage/yard stable IDs. No greater-than/count relaxation or runtime-derived
expected-total substitute is used.

Runtime, authoring, colliders, profiles, stable IDs and lifecycle behavior were
not changed. Scoped `git diff --check` passed. The correction is source-frozen;
the coordinating agent owns Unity execution. No Unity process was launched by
this audit worker. The lifecycle PASS below does not establish visual
rain-coverage acceptance.

## Corrected PlayMode result

`Logs/unity660-playmode-final-r3-20260906.xml` records the corrected
`Bootstrap_RestoresBeforeRevealAndSurvivesAdditiveLifecycle` test as **Passed**,
from `2026-09-06 08:33:49Z` to `08:33:56Z`. Its executed assertions require
exactly 20 removal ellipsoids, exactly three registry zones with the specified
house/garage/yard stable IDs, and the unchanged two authored shelter volumes.
The remainder of this lifecycle test also completed; no count assertion was
relaxed or skipped.

The entire r3 run, `08:30:47Z` to `08:34:46Z`, reports **173 total: 169 passed,
2 failed, 2 skipped, 0 inconclusive**. Both failures are UI preview isolation
cases reporting the directional cascade-shadow atlasing conflict:
`MainMenuVehiclePreview_QualitySwitchKeepsOpaqueEnvironmentAndVehicleOutput`
and `MainMenuVehiclePreview_RouteHideResumeAndDisposeReleaseOwnedRendering`.
The two skips are the pre-existing private real-geometry wiring checks that
require `-engineSavePath`; neither is weather coverage. The coordinating
agent's subsequent non-UI and isolated-UI final runs are separate gates and
are not claimed complete in this audit update.
