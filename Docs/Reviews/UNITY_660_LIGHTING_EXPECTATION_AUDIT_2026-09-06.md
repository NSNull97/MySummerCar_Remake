# Unity 6000.6.0f1: existing world-lighting expectation failures

Classification: test-baseline maintenance; no runtime or presentation change.
Status: source correction complete; Unity rerun pending the migration owner.

## Evidence

The first migration EditMode result,
`Logs/unity660-editmode1-20260906.xml`, reports three failures in
`MSC.Tests.EditMode.WorldRemaster.WorldLightingProbeCatalogTests`:

| Test | Stale expectation | Existing baseline |
| --- | --- | --- |
| `GeneratedCatalog_IsValidAndUsesStableUniqueIds` | 45 lights | 49 lights |
| `StreetLights_AreNightOnly` | 12 lux/night-only lights | 14 lights |
| `Runtime_PreservesPoseForPointPromotedToAreaLight` | Euler `(0, -117.5, 0)` | Euler `(90, 147.397, 90.001)` |

`Artifacts/VegetationRebuild/revision-editmode-final-r7.xml` records the same
three failures on **2026-08-31 at 20:57:22 UTC**, including the identical
`176.391525` degree pose difference. This is also documented in
`Docs/WorldRemaster/MAP_VEGETATION_PRESENTATION_REVISION.md`, in its expanded
EditMode r7 results. These failures predate the Unity 6.6 migration.

The current `Assets/Game/World/Content/Lighting/Phase1WorldLightingProbeCatalog.asset`
and the matching file in the ignored pre-upgrade backup
`MySummerCar_Remake_before_6000.6.0f1_20260906` have the same SHA-256:

```text
D48D725DCF8F87D3D3B04F38ACC964E4B816E5DC459A5C1CB093D456518588BA
```

Read-only serialized inspection counts **49 lights, 14 lux streetlights and
10 reflection probes**. The old total-probe assertion of six was masked by the
earlier light-count failure. `Docs/WorldRemaster/PHASE1_LIGHTING_AND_REFLECTION_BASELINE.md`
already specifies 49 lights, 10 probes and the shared 14-streetlight profile.
`Docs/Lighting/LightingValidationResults.md` documents the expanded fixture
coverage and three project-owned refrigerator fills.

`Phase1WorldLightingProbeBuilder.AddTeimoRefrigeratorLights` and the independent
`TeimoRefrigerators_PreserveCapturedAreaLightOverrides` test both specify the
first refrigerator's Euler pose as `(90, 147.397, 90.001)`.
`WorldLightingProbeRuntime.CreateLightCore` still assigns the definition's
rotation before the presentation-adapter handoff. Its migration diff contains
only `SceneHandle` compatibility and the HDRP `shapeRadius` API move; its pose
assignment is unchanged. No runtime pose regression is evidenced by this failure.

## Bounded correction and verification

Only the test expectations are corrected: 49 lights, 10 probes, 14 streetlights
and the already authored refrigerator pose. The rotation tolerance remains
strictly below `0.001` degrees. Unique-ID, catalog validation, streetlight
photometry and the independent fixed-position/rotation/area-size assertions
remain intact. No catalog regeneration, scene save, runtime behavior, stable
ID, public API, save schema, vendor file or donor payload is changed.

Executed during this audit: historical/current XML inspection, SHA-256 backup
comparison, read-only serialized counts, source/backup diff and scoped
`git diff --check`. No Unity process or test runner was started by this audit;
the migration owner must rerun the existing `WorldLightingProbeCatalogTests`
class before recording these corrections as passing test evidence.
