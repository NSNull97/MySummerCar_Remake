# Main-menu home environment — 2026-09-04

The user rejected combining the real Satsuma mesh with a photographic garage and
requested an entirely 3D menu location from the existing game, preferably home or
Teimo. This implementation selects the existing home yard. The generated garage
image is superseded; it is not part of the new environment's source recipe.

Current amendment: the user accepted the main menu, then requested its garage
lamp switched on and volumetric fog behind the house. `MainMenuHomeYard.4`
export completed with Unity exit 0 in
`Artifacts/MainMenuRedesign/HomeEnvironment.AtmosphereAuthoring.01.log`, also
recorded in its `.result.json`. Structural authoring checks passed. The integrated
lamp/fog/local-time revision passed 54/54 EditMode and 40/40 PlayMode checks,
including rendering and menu lifecycle, in the final system-time run 02.
The subsequent white car spotlight passed 40/40 PlayMode checks in
`home-white-spot-play-20260904-01`; the 54 EditMode checks were not rerun for that
light-only change. See `MAIN_MENU_WHITE_SPOT_2026-09-04.md` for current evidence.
User visual approval of this revision is still pending. Performance remains unmeasured.
The subsequent nighttime vehicle-lamp follow-up is documented separately in
`MAIN_MENU_VEHICLE_LIGHTS_2026-09-04.md`; it adds front/rear menu lights while
preserving the yard, garage spotlight and camera orbit.
All 15 recorded source-file hashes match their current files after export.
The separately authorized front-latch canonical Satsuma refresh changed its
source SHA before .4; the exporter recorded and preserved that new source
snapshot. The .3 hashes below remain historical.

| Current executed generation result (`MainMenuHomeYard.4`) | Value |
|---|---|
| MeshRenderers / existing mesh assets / woody placements | 1549 / 363 / 693 |
| Required house/garage IDs / errors | 7 present / 0 |
| Exact spawn-ground centre | `(153.74903869628906,1.0371774435043335,-1029.2509765625)` m |
| Ground candidates / search radius / height variation | 1 / 0 m / `8.344650268554688e-7` m |
| Lamp source ID / material slot | `d76c0bb63327c058f2b82bc57a699d02` / 0 |
| Lamp emitter world centre | `(153.49456787109375,3.247178316116333,-1032.990478515625)` m |
| House exterior world bounds centre | `(159.77783203125,2.6530535221099854,-1034.323974609375)` m |
| House exterior bounds size | `(18.419845581054688,6.631758689880371,18.710302352905273)` m |
| Source hash verification | 15/15 match; BuildSettings baseline also matches |

The .4 model adds explicit `GarageLampRenderer`, `GarageLampMaterialIndex`,
`GarageLampAnchor` and `HomeExteriorBoundsLocal` bindings. Its separate
`ConfigureAtmosphereForAuthoring` method preserves the existing placement API.
The exporter requires lamp source ID `d76c0bb63327c058f2b82bc57a699d02`
(`YARD/Building/LOD/outdoor_lamp`), already selected from the accepted home cell.
The fixture has one renderer/submesh/material slot 0. It retains its canonical
mesh and atlas material; the generated wrapper gains only a typed anchor,
not a Light or fog component. The anchor uses source-local `(0,0,0.22)` at the
lower housing centre, with forward toward the yard. This is a menu-authored
emitter placement derived from the mesh, not a transferred donor light setting.
House exterior bounds combine the seven existing required home/garage renderers
and exclude the huge terrain bounds. Their offline rear roof edge is near
world Z=-1043.68; the runtime fog starts 2 m farther back, on negative Z.
Evidence: `Artifacts/MainMenuRedesign/HomeEnvironmentStage/LampAndHomeBounds.Offline.json`.

The preserved placement puts the display at the real Satsuma spawn, retaining
the car's menu rotation. Since `MainMenuHomeYard.3`, the exporter reads
`LegacySatsumaBaselineMetadata.DefaultWorldPosition` from the canonical prefab
at `Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/Resources/Phase1Vehicles/Satsuma_Phase1_V1a.prefab`.
`ProductionSatsumaInstaller` uses that same metadata when instantiating the
gameplay vehicle. Spawn X/Z are exactly `(153.74904,-1029.251)` m; the source Y
1.611 m is a gameplay pivot height, not the display's ground height. The menu
keeps identity car rotation instead of adopting the gameplay spawn's 180° yaw.
The exporter no longer searches for a nearby parking point: unsupported ground
at the exact requested X/Z fails generation. `HomeEnvironment.SpawnAuthoring.01.log`
completed with Unity exit 0; structural export checks passed. The .3 spawn/orbit
then passed 38/38 PlayMode tests in `tests-home-spawn-play-20260904-01.xml`, and
the user approved its main-menu presentation with “все супер”. This acceptance
predates the .4 lamp/fog change. Performance was not measured. The archived
scope/results are in
`Artifacts/MainMenuRedesign/History/MainMenuSpawnApproved38-20260904/evidence.json`.

| Historical executed generation result (`MainMenuHomeYard.3`) | Value |
|---|---|
| MeshRenderers / referenced meshes / woody placements | 1549 / 363 / 693, unchanged |
| Required house/garage source IDs / errors | 7 present / 0 |
| Canonical and actual X/Z | `(153.74903869628906, -1029.2509765625)` m; exact match |
| Actual ground Y | `1.0371774435043335` m |
| Anchor X/Z relative to unchanged origin | `(0.2540435791015625, -2.450927734375)` m |
| Candidates / search radius / search step | 1 / 0 / 0; no nearest-point search |
| Five-point ground variation | `0.0000008344650268554688` m |
| Ground source | concrete base `59be8f2e37d6d0e62fbdfec0aec537b9`, submesh 0, triangles 56/52/57 |
| Menu car rotation | identity; unchanged |

The initial camera direction is now `(-0.30726877,0.27872965,0.90988773)`,
which moves the starting view +20° yaw and +3° pitch from the old direction.
Relative orbit limits −20…45° yaw and −3…7° pitch retain the audited physical
arc 0…65° / 0…10° while allowing drag in both directions from the starting view.
The camera adjustment does not rotate the car or alter the gameplay spawn.

Offline ground evidence found all five samples on existing `house_base_concrete`,
source ID `59be8f2e37d6d0e62fbdfec0aec537b9`, with maximum Y approximately
1.037177839 m and height variation below 0.000001 m. The narrow physical camera
offsets yaw 0…65° and pitch 0…10° relative to `(-0.8,0.3,1)` had no blocked
centre/eight-corner sight rays in 729 sampled cameras, and camera-to-triangle
clearance at least 0.5 m. Stable radii were 9.766556 m at 16:9/32:9 and
10.833082 m at 4:3. The prior −50…65°, −4…10° arc was unsuitable here:
428 of 1269 camera samples had tree intersections. These are offline serialized
mesh checks, not a rendered acceptance result; vegetation alpha cutouts were
treated as opaque and supporting-ground endpoint contacts below 1 mm excluded.
Evidence is `Artifacts/MainMenuRedesign/HomeEnvironmentStage/SpawnGround.Offline.json`,
`OrbitClearance.Spawn.Offline.json` and `OrbitClearance.Spawn.SafeArc.Offline.json`.

Historical export, superseded in placement: `MainMenuHomeYard.2` environment generation passed, Unity exit 0,
in `Artifacts/MainMenuRedesign/HomeEnvironment.OrbitAuthoring.01.log`.
Source/placement and component allowlist checks passed. The existing Bootstrap
reference retains the same environment prefab identity. At this historical .2
export, graphical/lifecycle acceptance was still pending; it was superseded by
the approved .3 spawn presentation. None of these exports establishes gameplay
parity or a measured performance result.

Historical orbit amendment: the user requested brighter lighting and a bounded camera orbit
to inspect the real car. `MainMenuHomeYard.2` requested parking at
`(157.495,0,-1022.55)` before measuring its ground Y, 4 m east and 2 m north of
the previous anchor. The source origin, house, terrain and vegetation transforms
are unchanged. Regeneration accepted the requested point on its first candidate.
Historical pre-orbit run 03 measurements and hashes remain explicitly labelled
below, separate from the later spawn request.

The historical read-only geometry audit used the renderer's analytical fit, a
25-by-5 search for a stable radius and its 1.02 margin. It sampled 47 yaw values
from -50 to +65 degrees and nine pitch offsets from -4 to +10 degrees at 4:3,
16:9 and 32:9: 1269 positions for this parking candidate. All nine rays from
each camera to the car bounds centre/eight corners were clear, with minimum
camera-to-triangle clearance 0.420233 m. Contacts within 1 mm of a ray endpoint
on supporting ground were excluded; alpha-cutout vegetation triangles were
conservatively treated as opaque. This is an offline serialized-mesh audit, not
an executed Unity collision/render test. The prior point and a simple +3 m north
shift both crossed nearby spruce presentation.

Audit artifacts are under `Artifacts/MainMenuRedesign/HomeEnvironmentStage/`:
`OrbitClearance.Frustum.Offline.json`, `OrbitClearance.Analytic.Offline.json`
and `OrbitParking.Offline.json`, with their read-only Python query helpers.
That historical candidate had five supported ground samples across accepted Gravel,
Grass1 and Grass2. Unity confirmed maximum Y 1.020541191 m and height variation
0.00002658367157 m, consistent with the offline evidence.

| Historical executed generation result (`MainMenuHomeYard.2`) | Value |
|---|---|
| MeshRenderers | 1549: 339 selected legacy world renderers + 1210 woody renderers |
| Unique referenced mesh assets | 363; no newly generated/cut mesh geometry |
| Accepted woody placements | 693 |
| Required house/garage source IDs | 7 present |
| Errors | 0 |
| Parking candidates evaluated | 1; requested point supported |
| Actual vehicle ground centre | `(157.494995, 1.020541191, -1022.549988)` m |
| Offset from previous pre-orbit anchor | `(4, 0, 2)` m in XZ, subject to serialized float precision |
| Anchor XZ relative to unchanged environment origin | `(4, 4.250061)` m |
| Five-point ground height variation | `0.00002658367157` m |
| Ground sources | Gravel triangles 479/474; Grass1 triangle 19209; Grass2 triangle 27437; each submesh 0 |

The ground anchor uses the maximum of the five measured heights, 1.020541191 m.
Every sample's upward normal has Y=1 within floating-point precision. Large
whole source meshes make combined local bounds approximately
`9656.134 × 105.367 × 8667.408` m; these bounds are not the local object-selection
extent and must not be used to fit the menu camera. The camera frames the car.

Historical authoring run 03 selected `(153.494995,1.020515800,-1024.550049)`
after 251 candidates, +2.25 m north of the original request, with five-point
variation 0.000004768371582 m on Gravel triangles 479/482/477. The orbit request
superseded only that vehicle anchor. Renderer, mesh and vegetation counts remain
1549/363/693 respectively.

## Source recipe

The active profile is `donor-feature-parity-06b2`, schema 2, 512 m cells, in
`Assets/Game/World/Content/Streaming/ProductionWorldStreamingManifest.asset`.
The exporter reads these existing scenes through Editor preview scenes, closes
them without saving, and builds a separate mesh-only wrapper:

| Active ownership | Existing source scene |
|---|---|
| `global-legacy` | `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/World_Global_Legacy.unity` |
| `cell_0_-3` | `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_0_-3_Legacy.unity` |
| `cell_0_-2` | `Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_0_-2_Legacy.unity` |

Home spans the Z=-1024 cell boundary. Both cells are needed for its immediate
surroundings. The old `WR_HomeYardPilot` and `ProductionCells` prototype geometry
are not sources. No new donor extraction or canonical-world import is performed.

The menu-owned selection volume has project-world centre `(165,20,-1030)` and
size `(160,80,180)` metres. This limits selected local objects. An intersecting
global ground/road mesh remains **whole**, retaining its existing mesh asset and
material references even when its geometry extends outside that volume. The
exporter neither cuts nor duplicates terrain mesh data and does not construct a
second canonical map. Selected renderer transforms are copied under a separate
presentation root; their relative scale, rotation and spacing are preserved.

Generated scene coordinates already include the canonical source conversion.
The source-to-project translation is not applied a second time. The wrapper's
origin is `(153.495,0,-1026.8)` in project-world coordinates, with the vehicle
facing +Z. Version .3's initial camera-from-vehicle direction is
`(-0.30726877,0.27872965,0.90988773)` in environment-local axes. The bounded
orbit frames the car with the real house behind it; render acceptance is separate.

The garage door plane is near Z=-1033.25. Its `.95` root/anchor Y is **not** used
as a ground measurement. The authoring tool measures nearly horizontal,
two-sided triangles in the accepted Grass1, Grass2, DirtRoad, Gravel, Roadside
and concrete-base meshes at the canonical vehicle spawn centre and four
wheel-footprint samples. Missing samples or unsuitable slope/height variation
fail generation. Version .3 uses one exact candidate, with search radius and
step both zero; it never changes the requested X/Z. The original environment
origin and all source transforms remain fixed. Historical versions .1/.2 used
a deterministic 0.25 m grid within 4 m; that placement policy is superseded.
The report records the canonical spawn prefab/hash, source spawn position,
requested/actual parking centres, and every selected ground source ID,
triangle, position and normal. Runtime places the Satsuma's
local bounds minimum at the explicit vehicle anchor; this is a static installed
display pose, not a vehicle suspension simulation.

Seven existing source IDs are required to keep the home identifiable:

| Element | Existing baseline source ID |
|---|---|
| Concrete house base | `59be8f2e37d6d0e62fbdfec0aec537b9` |
| Brick wall | `458dc5a50e4ab4a9dca3f5445fc1dc80` |
| House roof | `1704d967080fa78707fe486f215b2811` |
| Garage roof | `b5e7b987d5aac9da197a6ce662beb64c` |
| Garage wall | `419f49d30da6bff0fcfa679c84d652ce` |
| Left garage door | `250d1cb74558e7c7e27e5e860981c938` |
| Right garage door | `e8660bda40e1d2946e004456e245c803` |

These IDs are Editor selection/provenance inputs. The menu runtime does not
search scene names, donor paths or persistent world IDs.

## Trees and ground presentation

The accepted vegetation is packed instance data rather than ordinary scene
MeshRenderers. The exporter reads `PackedOriginalTrees.asset`,
`PackedShrubsAndUndergrowth.asset` and `PackedBoundaryForest.asset` from
`Assets/Game/LegacyImport/RuntimeBaseline/VegetationRebuild/Data/cell_0_-3/` and
`cell_0_-2/`. It expands only placements inside the selection volume into static
Transform/MeshFilter/MeshRenderer objects, referencing the existing prototype
mesh and material assets. Every placement keeps its source transform and ID in
the report. Submesh material bindings are preserved without duplicate draws.

This bounded static display selects accepted LOD0 within 25 m and LOD1 farther
away. It does not copy `PackedWoodyCellRenderer`, collision pools or vegetation
services. The original billboard tree category is excluded. Indirect detail
grass services are not copied; the existing ground and bounded woody vegetation
remain visible. This visual limitation must be reviewed in the actual capture.

## Lighting and runtime boundary

`MainMenuEnvironmentModel` owns explicit renderer references, vehicle and lamp
anchors, lamp renderer/material slot, camera direction, source origin, measured
ground height, overall bounds and separate `HomeExteriorBoundsLocal`. Its
`MenuLightingProfile` binding references the existing project-owned
`Assets/Game/Presentation/Lighting/GaragePrototype/M3_LateDayVolume.asset` for
fixed exposure (12.7 EV) and tone settings. No prototype building geometry is
used. The profile asset itself is read-only; the isolated renderer clones only
its Exposure and Tonemapping components.

The latest user follow-up makes the menu lighting follow the computer's local
time. Runtime integration is complete in
`Assets/Game/UI/Presentation/Runtime/MainMenuLightingTime.cs` and
`MainMenuVehiclePreview.cs`; the final automated gate passed 54/54 EditMode and
40/40 PlayMode checks. The earlier 39/40 PlayMode run's sole night-image
readability failure is resolved by the revised night/twilight light and fog
values below. User visual approval remains pending.
The environment remains the exported `MainMenuHomeYard.4` payload:
no source meshes, material assets, placement data or provenance hashes change.

Production reads local `DateTime.Now`; `GameUiDependencies.MenuLocalTimeProvider`
allows a fixed clock in tests. The visible preview polls once per real second,
but applies changed lighting and requests cached render/blur refresh only when
the local date/minute changes. Re-entering the menu forces a fresh time sample.
An unchanged minute adds no clock-driven render work. Paint, orbit and viewport
changes retain their existing independent refresh triggers. The menu never
reads or writes `IGameTimeService`, gameplay saves, Enviro time or world weather
to implement this presentation clock.

`MainMenuLightingTime` blends authored day/twilight/night weights: dawn 06:00–08:00,
day 08:00–18:00, dusk 18:00–22:00, and night 22:00–06:00. Twilight peaks at
07:00 and 20:00; smooth transitions drive the light/sky colours and key direction.
This is a local-clock art schedule, not astronomy, seasonal sunrise calculation
or actual weather matching. The pure states use:

| State | Key, lux | Vehicle fill, lux | Sky multiplier | Lamp factor |
|---|---:|---:|---:|---:|
| Day | 60,000 | 22,000 | 6,500 | 35% |
| Twilight | 10,000 | 10,000 | 2,600 | 75% |
| Night | 2,400 | 9,000 | 1,800 | 100% |

The current accepted Enviro sky requires `EnviroManager.instance.Sky` and cannot
serve this standalone presentation without the game's weather runtime. The
menu renderer uses a camera-scoped procedural GradientSky with explicit dynamic
ambient lighting. Its box key (60,000 lux in the daytime state) covers a finite 50×40×90-metre volume
and uses an orthographic punctual shadow map. It creates no additional directional
light, leaving HDRP's directional cascade atlas to the existing world sun.
A cooler shadowless box fill (22,000 lux in daytime) follows the orbit camera and uses light
layer 64, received only by the vehicle; the environment receives layer 128 and
the vehicle receives both. The owned GradientSky multiplier reaches 6,500 in daytime.
Both owned lights have `interactsWithSky=false`. The renderer does not copy the
profile's PhysicallyBasedSky or create an Enviro manager. This avoids HDRP physical
sky's global directional-light discovery crossing the menu's light-layer boundary.
It is a menu presentation choice, not a claim of full weather parity. Source
material assets and tone settings are unchanged by these lighting adjustments;
the separately requested orbit rotates only the preview camera.

The runtime owns a white spotlight at the explicit lamp anchor plus 0.30 m
toward the yard, aimed at the real car's display-bounds centre. It uses an
80-degree cone with a 60-degree inner cone, 12 m range, up to 120,000 candela,
soft shadows at 1024 resolution and shape radius 0.12 m. Color-temperature
tint is disabled. It uses only the preview
light/render layers and has `interactsWithSky=false`. A MaterialPropertyBlock
on the cloned lamp renderer's slot 0 sets `_EmissiveColor` to linear radiance
`(6000,6000,6000)` using `SetVector`, scaled by the same time-of-day lamp factor,
and `_AlbedoAffectEmissive=1`, retaining the atlas's darker hardware. The
clock integration therefore ranges from 42,000 candela by day to 120,000 candela
at night. The earlier point light was labelled Lumen, but its native
`Light.intensity` value was 12,000 candela; the new source is explicitly
labelled Candela and ten times stronger at a matching time of day.
It does not pass HDR radiance through `SetColor` colour conversion. The shared
canonical material is never written. Because the housing and shade are one
submesh, this is albedo-modulated fixture emission, not separate bulb geometry.

The rear `LocalVolumetricFog` has size `160×22×100` m and mean free path 42 m,
no texture scrolling, and distance fade 250…400 m. Its albedo is blended by the
same time weights: day `(0.70,0.78,0.86)`, twilight `(0.35,0.48,0.70)`, night
`(0.025,0.10,0.28)`. HDRP volumetrics can receive global directional illumination
despite mesh light layers, so this adjustment grades only the owned fog's colour
to avoid a brown night horizon. The world sun is not modified. Brighter cool
night sky colours accompany the revised intensities to preserve car readability.
Its centre uses the measured house bounds: X at the house centre, Y at the
minimum plus 8 m, Z at the rear minimum minus 2 m minus half its depth. Its
front therefore remains behind the rear roof edge; the car and camera are
outside the local fog box. A camera-scoped Fog override uses mean free path
100,000 m for clear foreground air, depth extent 220 m and spatial Gaussian
denoising; volumetric reprojection is disabled for the cached menu render.
The .4 authoring export passed. The final revised night-readability and strong
fog-visibility checks also passed with HDRP Balanced and an existing world sun.

Volumetrics remain subject to the selected HDRP quality asset. Existing
`Assets/Settings/HDRP Performant.asset` has `supportVolumetrics: 0`, so this
quality does not support the local volumetric effect; Balanced and High
Fidelity have it enabled. The menu does not modify shared HDRP assets or force
a quality tier. The executed Balanced check does not establish visibility under
Performant, which lacks the required volumetric support.

The generated wrapper allows only its project-owned model component plus
Transform, MeshFilter and MeshRenderer. It contains no colliders, Rigidbody,
gameplay state, save participant, world streaming, input, audio, light or camera.
The UI renderer owns its isolated camera/light/volume lifecycle. Runtime uses
an explicit serialized environment reference from the installer; no world load,
resource search or runtime photo dependency is required by the new path.

## Executed validation of the local-time revision

Final evidence is under `Artifacts/MainMenuRedesign/`:

| Gate | XML / matching result JSON stem | Result | Duration | Completed UTC |
|---|---|---|---|---|
| EditMode | `tests-home-system-time-edit-20260904-02.xml` / `result-home-system-time-edit-20260904-02.json` | 54/54 passed | 3.3428087 s | 2026-09-04 15:52:30 |
| PlayMode | `tests-home-system-time-play-20260904-02.xml` / `result-home-system-time-play-20260904-02.json` | 40/40 passed | 46.7827529 s | 2026-09-04 15:56:17 |

Both runs have zero failed, skipped or inconclusive cases. The final capture
review with HDRP Balanced and an existing world sun measured mean image
luminance 0.020537 at night and 0.17136 in daytime; the strong fog comparison
passed again. Native capture inspection confirmed a visible car against the
blue night setting and a readable noon view. These are executed automated and
developer visual checks, not user approval or a standalone performance result.
The user's earlier “все супер” applies to the historical .3 spawn/orbit version.

## Provenance and replacement

Classification: `TemporaryDirectImport`, private Phase 1 presentation only.
Replacement key: `menu.home-yard-preview`. The output is not `ProductionReady`,
playable terrain or a simulation-complete world location. Production replacement
must preserve the wrapper contract and vehicle placement without changing saves
or gameplay state.

Output paths:

- `Assets/Game/LegacyImport/RuntimeBaseline/GameplayPresentation/MainMenu/HomeYardMenuEnvironment.prefab`
- `Assets/Game/LegacyImport/RuntimeBaseline/GameplayPresentation/MainMenu/HomeYardMenuEnvironmentReport.json`

Both are generated donor-derived payload under the existing ignored baseline.
Commit project-owned code, documentation and provenance only. The report records
source file hashes/dependency hashes, renderer source IDs, material/mesh asset
paths, tree placements, measured ground and errors. The exporter verifies source
file hashes again before reporting success. No source scene, source asset,
canonical material, world coordinate, build setting or simulation state is
intentionally changed.

Read-only audit hashes (before generation):

| Source | SHA-256 |
|---|---|
| `DonorWorldBaselineSourceManifest.json` | `e385298c0b6ef344ade8c3a5f1f7c5fd684b690a8114808d658aea5f7b15ec6a` |
| `ProductionWorldStreamingManifest.asset` | `722dbb599c7bfdaa8ddd9df252fe207777dcb9f49892f00ba6660f36706a037e` |
| `M3_LateDayVolume.asset` | `e303406b04c6be565cd6c4c825191aba5bd1963e09926b1b84495ae1b2e6b193` |

Current executed artifact hashes — `MainMenuHomeYard.4`:

| Artifact | SHA-256 |
|---|---|
| `HomeYardMenuEnvironmentReport.json` | `e3f9a363d6a09c810f79f39d9c1668f1b7e18e457ebdff5f7a4f22008e88730e` |
| `HomeYardMenuEnvironment.prefab` | `aa4d3471240ac83856c34d9efc93fd006aa2ffd2968fbb51c3a4587382d7d235` |
| `HomeEnvironment.AtmosphereAuthoring.01.log` | `8fcf688438f6ba9bee3a679534460239aa5e40c605c7ad28dac3da44ede9f729` |
| `HomeEnvironment.AtmosphereAuthoring.01.result.json` | `16f5dde0e219839227a60c75c0065d8d25f3a9f9adb8f5e0da4cbb8d384a3303` |

The .4 integrity check matched all 15 recorded sources: three world scenes, two
manifests, the exposure/tone profile, six packed vegetation assets, the current
canonical Satsuma prefab, and the lamp mesh/material. The complete check is
`Artifacts/MainMenuRedesign/HomeEnvironmentStage/AtmosphereAuthoring01.SourceIntegrity.json`.
Newly recorded/current source hashes are:

| Source | SHA-256 |
|---|---|
| Canonical `Satsuma_Phase1_V1a.prefab` | `7f4c06b701116f43428c54411c954085e4fa39dad4736e4bbe1848e7b2f58cf6` |
| Lamp mesh `d76c0bb63327c058f2b82bc57a699d02.asset` | `66e7225136067d2e15c973892d4a4de6c7bff835d5d2252692b867c2f3bbdcc5` |
| Lamp atlas material `M06B2_c34448a7491c8d047810e4e19aee2ab6.mat` | `4e8e6a2c996387facff4f0a8fffa620c9a7e3058205d1cae2c22e20945b21a86` |

The canonical change came from the separate bounded front-rules refresh:
`Artifacts/FrontInstallRules/FrontRules.Refresh.01.log` records 15 changed mount
definitions and six removed legacy dependencies, with `fullRebuild=false` and
`manifestUnchanged=true`. Its `.02` rerun records 0/0 changes. This is an
authorized source update before .4, not an environment-export mutation.
`ProjectSettings/EditorBuildSettings.asset` still matches the captured baseline
SHA-256 `1849b8be614e204fb0cf463cbccc784380598d1bb4302e82ac3840f46cf2e454`.

Historical executed artifact hashes — `MainMenuHomeYard.3`:

| Artifact | SHA-256 |
|---|---|
| `HomeYardMenuEnvironmentReport.json` | `3c61b68199c7597b15266f36f6609ccfd64470a9321b87b03a7fc0b8fc89b9ef` |
| `HomeYardMenuEnvironment.prefab` | `7af8997e04fadf6fbc519a69d96af0ebfccc14cb531db0c12da53a3ef784cbcb` |
| `HomeEnvironment.SpawnAuthoring.01.log` | `431c41ed48491a0d0ac04d35d732bbefedeefc21e2071a6b7cf8e0920ba0ae56` |

Historical open-yard artifact hashes — `MainMenuHomeYard.2`:

| Artifact | SHA-256 |
|---|---|
| `HomeYardMenuEnvironmentReport.json` | `d02782a88991af30e27aaa3a62e0c64e877d919eebe0ccfacf514897b1ad9758` |
| `HomeYardMenuEnvironment.prefab` | `d9490ebc315cf48853ea5297f6210e4bb8e36f05e4bb1327450687cbb3e870fa` |
| `HomeEnvironment.OrbitAuthoring.01.log` | `93624f8b2436cf0beb8ca275ac71f9ed1aa3f6417fec0536d5941c449506688f` |

Historical pre-orbit artifact hashes — authoring run 03, `MainMenuHomeYard.1`:

| Artifact | SHA-256 |
|---|---|
| `HomeYardMenuEnvironmentReport.json` | `d4b9ad2b0b6fb0261c59d10faeaf08f61d7ca94a7b9a2d3ed8ec7c7489799444` |
| `HomeYardMenuEnvironment.prefab` | `195d7a2a644e7d0a66e98ca6d955cb66dc4c13536989cb2951d663f72add450b` |
| `HomeEnvironment.Authoring.03.log` | `011b5b5fc9b45cf86826031f5fe160280365da55dc8e3f3b2965832d91227a9d` |

The .3 post-export SHA-256 checks matched all 13 recorded source files: three world
scenes, two manifests, one lighting profile, six packed vegetation assets and
the canonical Satsuma prefab supplying its spawn metadata. Evidence is
`Artifacts/MainMenuRedesign/HomeEnvironmentStage/SpawnAuthoring01.SourceIntegrity.json`.
The unchanged scene hashes are:

| Scene | SHA-256 |
|---|---|
| `World_Global_Legacy.unity` | `8c56276278ccf5949787ef1e8878807c22359b8d67f3affbfbc96a06f8a51c20` |
| `World_Cell_0_-3_Legacy.unity` | `ff5f8ee9d629a108678cde45d2a3bc60000c56252a8d4fa5b0d2c3fae4f833c1` |
| `World_Cell_0_-2_Legacy.unity` | `9f0d71e422c56b4307f95efd557f46458f626bb2ca557a8ac9bd4c6adbf28f6a` |

At the .3 check, the canonical Satsuma source prefab had its pre-menu-export SHA-256
`df1907cd72abf68db7bb6d871970959873f0752e58db0ef7dbe37ad9879a3e5c`.
`ProjectSettings/EditorBuildSettings.asset` also matched its pre-integration
SHA-256 `1849b8be614e204fb0cf463cbccc784380598d1bb4302e82ac3840f46cf2e454`.
Both were compared with `Artifacts/MainMenuRedesign/Staged/Before3DIntegration.Hashes.json`.
These are historical .3 observations; the current .4 canonical source and the
separate refresh evidence are recorded above.
The tool references persistent existing mesh/material assets and does not write
them. A separate before/after SHA-256 inventory of every referenced mesh binary
was not captured; neither the historical 13-file nor current 15-file source
verification must be presented as that
broader audit. Generated prefab/report ignore rules were verified with
`git check-ignore`.

The existing `PORTING_LEDGER.csv` and `DONOR_AUDIT.md` home-yard record now
separates the .4 structural export and passed local-time rendering/lifecycle
gates from the historical user-approved .3 spawn/orbit, under the same
`menu.home-yard-preview` replacement key.
The prior Satsuma record remains separate. Current user visual approval is
pending; no performance acceptance is claimed.
