# Unity 6000.6.0f1: main-menu render-test isolation audit

Classification: test-runner scene isolation; no runtime or presentation correction.
Status: mixed-run contamination demonstrated; the earlier dedicated menu run
passed all three tests. Final split-run evidence is owned by the migration
report and must be recorded from its completed XML, not inferred from this audit.

## Executed evidence

All times below are UTC on 2026-09-06. The three tests belong to
`MSC.Tests.PlayMode.UIPresentation.GameUiRootPlayModeTests`.

| Result XML | Executed interval | Result |
| --- | --- | --- |
| `Logs/unity660-menu-isolated-20260906.xml` | 08:22:59-08:23:12 | 3 passed, 0 failed, 0 skipped; 12.8989696 seconds |
| `Logs/unity660-playmode-final-r3-20260906.xml` | 08:30:47-08:34:46 | 173 total: 169 passed, 2 failed, 2 skipped; 238.21297 seconds |

The dedicated run preceded the test-only render-context diagnostic additions.
Its Editor log identifies Unity `6000.6.0f1` (`f7f8ed4d1e24`) with graphics
enabled through `-force-d3d11`; it used `-batchmode`, not `-nographics`.
The relevant filter was
`MSC.Tests.PlayMode.UIPresentation.GameUiRootPlayModeTests.MainMenuVehiclePreview_`.

| Test suffix | Dedicated run | Mixed diagnostic r3 |
| --- | --- | --- |
| `PaintChangesPropertyBlocksAndRenderedPixelsOnly` | Passed, 4.201127 s | Passed, 2.742868 s |
| `QualitySwitchKeepsOpaqueEnvironmentAndVehicleOutput` | Passed, 7.182950 s | Failed, 1.272613 s |
| `RouteHideResumeAndDisposeReleaseOwnedRendering` | Passed, 1.473828 s | Failed, 0.832405 s |

Both r3 failures report the same unexpected engine error:

```text
Cascade Shadow atlasing has failed, only one directional light can cast shadows at a time
```

This is not a successful combined 173-test gate. Runner separation must retain
all three menu tests in a dedicated process; omitting them is not validation.
These are rendered offscreen RenderTexture/pixel tests, not a human visual
approval or a replacement for the separate GameView capture workflow.

## Runtime proof of the extra world sun

The diagnostic records active/enabled directional lights immediately before
the first menu-frame await. All three snapshots use quality `2:Performant`,
`Assets/Settings/HDRP Performant.asset`, and a 640x480 screen. Both recorded
suns use soft shadows and culling mask `0xFFFFFFFF`.

| r3 test / frame | Directional count | Intensity and session-local EntityId |
| --- | --- | --- |
| Paint / 59002 | 1 | 95000: `64871186083641` |
| Quality / 59014 | 2 | 95000: `64871186083641`; 48000: `199011604705931` |
| Route / 59098 | 2 | 95000: `64871186083641`; 48000: `199011604705931` |

Every light in these snapshots is named `Directional Sun` and belongs to
`Assets/Game/Vehicle/Content/Assembly/Scenes/VehicleAssemblyPrototype.unity`.
EntityIds here are diagnostic identities within this one Editor session,
not project-owned persistent IDs or save keys.

The first sun is present before the menu's quality fixture adds its own world
sun. The unchanged pair of native identities in the Route snapshot proves
that the additional Quality-fixture sun remained alive after that test failed;
Route did not create a new unrelated second sun.

The snapshots also show `previewActive=True`, `cameraActive=False`,
`cameraEnabled=False`, `isReady=False`, and `renderCount=0`. They are pre-await
startup observations, not proof of a permanently disabled preview camera:
the Paint test subsequently passes in that same r3 run.

## Source and serialized-asset trace

1. `Assets/Game/Tests/PlayMode/VehicleAssembly/VehicleAssemblyPlayModeTests.cs`
   loads `VehicleAssemblyPrototype` with `LoadSceneMode.Single` in its
   `UnitySetUp` and has no scene-unloading teardown.
2. That scene instantiates
   `Assets/Game/Presentation/Lighting/GaragePrototype/M3_Lighting_Neutral.prefab`
   (GUID `3b5221c76797b5048907569066225f1d`). Its active, enabled directional sun
   has intensity 95000 and soft shadows. The scene retains this intensity and
   does not override the light to inactive, disabled or non-directional.
3. The intervening VehiclePhysics fixtures do not replace the active scene.
   `ProductionWorldCellLayerPlayModeTests` loads additive fixtures and unloads
   its owned scenes in `UnityTearDown`; it does not unload the pre-existing
   prototype scene. This explains how the 95000 sun reaches the UI tests.
4. In
   `Assets/Game/Tests/PlayMode/UIPresentation/GameUiRootPlayModeTests.MainMenuVehiclePreview.cs`,
   the Quality test explicitly instantiates `M3_Lighting_LateDay.prefab` with
   an active, enabled 48000-intensity soft-shadow directional sun. The test's
   stated purpose is coexistence with one unchanged world sun. When the
   prototype scene survives, this becomes two world suns before menu rendering.
5. `worldLighting` is local to that iterator and its destruction is only in
   the iterator's `finally`. The class-level `TearDown` in
   `GameUiRootPlayModeTests.cs` destroys `ownedFixtureObjects`, but this local
   lighting object is not registered in that list. The retained EntityIds
   establish that the failure path did not complete effective lighting cleanup.
   The existing `SatsumaInstalledPartPhysicsPlayModeTests` fixture also documents
   that an unexpected engine log can abort a UnityTest without iterator-finally
   cleanup. This audit does not assume all failure paths always skip `finally`.

## Why this is not a menu-lighting redesign

Read-only comparison against the pre-6000.6 backup found the same menu-light
types: the preview fill was already Box, vehicle headlights Spot, and rear
lights Point. The relevant migration edits move `shapeRadius` from
`HDAdditionalLightData` to Unity `Light`; they do not convert those lights into
directional lights or add a second menu sun.

Inspection of installed HDRP 17.3 and current HDRP 17.6 sources confirms that
Box/Pyramid/Spot lights follow the spot/punctual classification; only
directional lights use the cascaded shadow map classification. The reported
message is emitted when cascade-atlas layout fails, not by a literal
`directionalCount > 1` assertion. The unchanged atlas-layout algorithm and
the runtime pair of shadow-casting directional lights support the fixture
contamination diagnosis; the error string alone would not establish it.

The dedicated runner restores the intended test precondition: its Quality
fixture supplies exactly one world sun, and the existing assertions still
require the menu to coexist with that sun without changing its type, shadows,
intensity, color, culling mask, enabled state or rotation. The test also retains
quality-pipeline, finite/opaque pixel, vehicle/environment, atmosphere and
shared-HDRP-asset immutability checks.

Dedicated UI execution is already documented in
`Docs/UI/MAIN_MENU_VALIDATION_TOOLING.md`: PlayMode uses the UIPresentation
filter, while real GameView capture requires a graphical, non-batch workflow.
Separate test processes therefore preserve the established validation boundary;
they do not require disabling world shadows, changing light types/intensities,
mutating HDRP assets, suppressing the engine error, weakening assertions, or
redesigning the accepted 08A UI.

## Remaining limits and audit scope

The first mixed run, `Logs/unity660-playmode-final-20260906.xml`, also contained
a Paint first-frame timeout before the cascade failures. Its exact cause is
unresolved. Cold shader compilation is not established by this evidence. Paint
passing in diagnostic r3 with the retained prototype sun rules out treating
that earlier timeout as an invariably reproducible menu-initialization failure;
it does not prove a specific timing or shader cause.

Executed for this audit: XML result/diagnostic inspection, relevant test/source
and serialized-prefab inspection, read-only pre-upgrade source comparison,
local HDRP classification/layout inspection, and whitespace validation of this
document. No Unity process was started by the audit author. The migration owner
executes and reports the final non-UI and isolated UI runs separately.

This documentation-only change does not modify C#, runtime assets, scenes,
prefabs, lighting ownership, vendor code, HDRP settings, public APIs, stable IDs,
save DTOs, or accepted 00-08A presentation. No migration is required. Broader
cross-suite fixture-cleanup hardening remains separate from the bounded
Unity-version compatibility work.
