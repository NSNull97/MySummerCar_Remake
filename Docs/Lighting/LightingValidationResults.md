# Lighting validation results

## Automated results

| Check | Result |
|---|---|
| Unity compile of Lighting Runtime/Production/Editor/tests | Passed; no lighting `CS` errors |
| Lighting Map Builder | Passed, exit 0 |
| Audit coverage | 70 scenes, 168 prefabs |
| Audit candidates | 297 |
| Existing lights safely configured | 3 |
| Manual review | 294 |
| Accepted streamed world bindings | 49 (46 donor-evidenced + 3 refrigerator fill) |
| Lighting EditMode | 33/33 passed in the latest focused run |
| Save Integration EditMode | 37/37 passed |
| Production PlayMode | D3D11 Bootstrap lighting lifecycle passed `1/1` in 42.90 s; manual visual QA remains |
| GPU/GC profiling | Not executed successfully |

The three direct placements are the WeatherLab interior practical and two
stationary vehicle low beams. Directional sun/moon and environment-owner lights
are explicitly excluded from local-light auto-placement; the final cleanup
restored their directional photometry and removed builder-owned fixture
components. The 42 streamed world fixtures are runtime-generated from the
already accepted catalog and are not duplicated into donor-generated scenes.

## Validation runner

Open `Tools > Lighting > Lighting Validation Runner` in Bootstrap Play Mode. It
can set the authoritative game time/weather override, quality tier, player
position/rotation, circuit/switch state, Teimo/Fleetari validation presence,
capture screenshots, and write JSON containing fixture/light/shadow/beam counts,
sun elevation, weather and active HDRP EV.

Required manual matrix remains: home night/day and adjacent rooms/doors; Teimo
shop and pub separately; Fleetari open/closed and owner present/absent; street
dusk/night/dawn in clear/rain/fog; full vehicle light channels and battery-off;
save/reload; leaking/popping; High/Ultra GPU capture. Manual review rows are in
`LightingManualReview.md` with exact asset path, hierarchy, positions and reason.

Known external console noise during audit/play attempts includes donor baseline
triangle-size warnings and service parity blockers. The final three PlayMode
attempts were specifically invalidated by forced domain reloads followed by
cleared runtime references and `RunError`; they are not reclassified as lighting
passes.

## 2026-08-11 runtime visibility regression

Live Bootstrap screenshots and `Editor.log` exposed two failures that the
earlier structural audit did not catch:

- Enviro's legacy directional `Light` deserialized as Candela under HDRP 17.
  Its vendor `SetIntensity` call then threw on the undefined Candela-to-Lux
  conversion every frame, leaving the 16:43 outdoor scene effectively unlit.
  The project-owned adapter now normalizes only directional Enviro lights to Lux
  before vendor startup and reasserts that invariant at runtime. Vendor files
  remain untouched.
- Single-scene composition replacement retired the old root but did not release
  `LightingSessionAuthority` until end-of-frame destruction. The incoming
  installer collided with the retired grid during `Awake`; it now participates
  in synchronous `IGameSessionLifetime` teardown.
- Newly registered switched fixtures now receive an explicit initial visual
  state. Electrical-grid restore also publishes state changes so saved switches
  immediately re-evaluate their lights.

EditMode regression coverage was added for directional Lux normalization,
initial fixture-off application and restore notifications. The generated
`.csproj` build was stale because concurrent weather sources had not yet been
added to it, so the affected Lighting Runtime, Production Integration, Lighting
EditMode, Enviro Integration and Enviro EditMode assemblies were instead
compiled successfully with Unity's current Bee response files into task-local
temporary outputs. After the active Editor closed, the three focused Unity
EditMode regressions passed `3/3` in
`Reports/Tests/lighting_visibility_regressions.xml`.

The focused Bootstrap PlayMode attempt remains inconclusive: its first run hit
the project's known forced-domain-reload `RunError`, and the warmed rerun loaded
the global donor world plus nearby cells without either the Candela/Lux or stale
lighting-grid exceptions, but terminated before producing test XML. It is not
recorded as a PlayMode pass. The manual 16:43 exterior and switched-home-light
visual matrix still must be rerun in the Editor.

## 2026-08-11 VLB 2.2.3 HDRP compatibility

The imported VLB override was incorrectly serialized for the Built-in render
pipeline and referenced `Hidden/VLB_*_BuiltIn_Default` shaders. It is now pinned
to HDRP with SRP Batcher rendering, the generated HD/SD shaders identify as
`Hidden/VLB_*_HDRP_SRPBatcher`, and the Standalone define set contains
`VLB_HDRP`. A project-owned Editor configurator validates and repairs this state
without modifying vendor source.

VLB 2.2.3 processes its HD/SD depth-camera occlusion through a recursive
`Camera.Render()` from `Update`. Under Unity 6.3 HDRP this can independently
corrupt local-fog draw registration and the cached-shadow atlas. The project
adapter now suppresses only depth-camera occlusion for SRP when the detected
vendor version predates 2.2.4. HDRP light shadows and VLB beam rendering remain
enabled. The vendor's
2.2.4 changelog identifies the corresponding scheduling change to `LateUpdate`;
the compatibility gate can therefore be removed or relaxed after a separately
approved package upgrade and regression run.

The focused compatibility tests passed `2/2`; the pre-existing lighting
EditMode suite passed `17/17` before the cached-shadow regression described
below.

## 2026-08-11 streamed cached-shadow stabilization

A fresh Editor run reproduced the cached-shadow failure without VLB's depth
camera being active. The first exception was an `ArgumentException` from
`HDCachedShadowAtlas.PerformPlacement`: the same shadow index was inserted
twice while streamed world lights were being registered. The later
`AddLightListToRecordList` null reference and local-volumetric-fog renderer
warning were downstream damage, not the initiating failure.

Two project owners were reconfiguring the same freshly created lights for
`ShadowUpdateMode.OnDemand` and requesting cached maps while additive cells
were still integrating. The immediate stabilization temporarily kept all
runtime-created shadows on the dynamic path. The later containment correction
below restores a single-owner cached/static split after making streamed
replacement synchronous and replay/idempotence explicit. No vendor or HDRP
package source was modified.

The validation runner now also reads the highest-priority active global volume
profile when batch mode has no blended `VolumeStack`. This makes the existing
Bootstrap lifecycle test diagnostic on D3D11 instead of throwing after the
world has already loaded.

Verification results:

- focused lighting and world-light EditMode tests: `17/17` passed;
- D3D11 Bootstrap lighting lifecycle: `1/1` passed in 33.4 seconds;
- the global donor world and eight nearby legacy cells loaded;
- `HDCachedShadowAtlas`, duplicate-key, renderer-after-culling,
  `LocalVolumetricFogManager`, `VolumetricShadowHD` and null-reference matches:
  all `0` in the final run log.

Manual Editor validation is still required for final image quality, especially
shadow hand-off while moving between more than four nearby fixtures.

## 2026-08-11 donor-interior readability and switch proxies

The donor-home fixtures existed and switched correctly, but their captured
photometry was overwritten by the generic fixture profile. For example, the
kitchen's captured 1300-lumen value was replaced by the 760-lumen domestic
default. The temporary donor world also has no baked interior bounce, while the
native HDRP fixed exposure did not react to the already smoothed indoor factor.
These three effects made switched rooms appear almost unlit.

Runtime-generated world fixtures now preserve captured photometry as a floor.
Interior bindings receive an additional `1.5x` Phase 1 baseline multiplier to
compensate for the missing baked bounce; exterior bindings are unchanged. The
kitchen fixture therefore renders at 1950 lumens. This multiplier belongs to
the temporary donor presentation baseline and must be recalibrated when Phase 2
materials and lightmaps replace it.

The native HDRP bridge now applies a smooth indoor readability lift of at most
2 EV from `WeatherExposureResolver.Current.IndoorFactor`. Exterior exposure is
unchanged, and doors/zone transitions use the existing smoothed factor instead
of an abrupt camera cut.

The visible cubes on the walls were generated interaction proxies, not donor
switch meshes. Existing proxies now disable their renderers during `Awake` but
retain their colliders and stable project-owned switch IDs. Future catalog
generation creates collider-only switch targets and no primitive mesh. Donor
presentation remains visible; its animation is not runtime authority.

Verification results:

- focused lighting and weather-production EditMode tests: `34/34` passed;
- D3D11 Bootstrap lighting lifecycle: `1/1` passed in 33.6 seconds;
- all seven existing home switch targets had no enabled proxy renderer;
- the switched kitchen fixture was enabled at no less than 1900 lumens;
- `HDCachedShadowAtlas`, renderer-after-culling, duplicate-key, compile-error
  and null-reference matches: all `0` in the final run log.

Manual Editor validation remains required for subjective day/night brightness,
light leaking through donor walls and the indoor/outdoor transition speed.

## 2026-08-11 Teimo shop/pub presence routing

The service runtime already tracked Teimo's shop and pub presence separately,
but the donor stores all six shared-building ceiling lamps under
`STORE/LOD/GFX_Store/Lamps`. The binding builder therefore classified every
lamp as `service.teimo.shop`, leaving the pub dark when Teimo started his night
shift and eventually switching the whole building off.

The shared donor lamp bank is now split by project-owned stable light ID. Five
fixtures in the retail room remain on `grid.teimo.shop`; only the eastern
fixture nearest Teimo's pub service point uses `grid.teimo.pub`. Runtime policy remains
`BusinessOpenAndOwnerPresent`, including the existing four-second shutdown
grace, so the occupied half follows Teimo without donor-name lookup or a new
global manager.

Automated coverage checks the deterministic 5/1 classification and the 22:00
transition: pub lighting availability must be active, then shop lighting
availability must be inactive after the bounded shutdown grace. Manual visual
QA remains required at both counters to confirm that the temporary donor walls
contain the light acceptably.

Verification results:

- focused Lighting and world-light EditMode tests: `44/44` passed;
- D3D11 Bootstrap lighting lifecycle: `1/1` passed in 36.8 seconds;
- 22:00 pub availability, 5/1 binding split and post-grace shop shutdown all
  passed;
- `HDCachedShadowAtlas`, renderer-after-culling, duplicate-key, compile-error
  and null-reference matches: all `0` in the final PlayMode log.

## 2026-08-11 preloaded-cell lights, refrigerator fill and local shadows

The Teimo cell may already be open additively in the Editor before Bootstrap
initializes. The streaming service intentionally does not claim or replay those
external scene-load events, while the world-light runtime previously listened
only for future owned-scene events. Consequently the shop fixtures and nearby
street lamps had no runtime `Light` components at all in this launch path.

World-light initialization now replays every currently loaded cell scene
through the same idempotent registration path used for later streaming events.
This restores the five shop fixtures, one pub fixture and nearby street lamps
without changing or serializing the donor scene. Runtime-created lights
also preserve their authored rotation so point-source catalog entries promoted
to HDRP rectangle or tube lights keep the intended orientation.

Three project-owned refrigerator display definitions were added behind the
donor shop doors. They use cool 6500 K rectangle lights, follow
`service.teimo.shop`, and intentionally do not request shadow slots. They are a
temporary Phase 1 presentation baseline and still need visual placement and
leakage review in the Editor.

Local real-time shadows were also incorrectly hard-gated by the current
`LightingZone`. Missing or mismatched streamed-zone metadata therefore forced
otherwise valid nearby lights to `LightShadows.None`. Distance is now the first
budget priority and zone relevance is the tie-breaker, so nearby interior lights
retain occluding shadows even when the camera stands just outside their zone.
The existing cap of four EveryFrame local shadow casters remains
in force to avoid returning to the HDRP cached-shadow failure. Profiles with
shadows disabled do not consume that budget.

Verification results:

- focused Lighting and world-light EditMode tests: `44/44` passed;
- D3D11 Bootstrap lifecycle with the Teimo cell preloaded: `1/1` passed in
  36.8 seconds;
- runtime assertions found 8 shop fixtures including 3 cool rectangle
  refrigerator lights, 1 pub fixture and at least 2 nearby street lamps;
- nearby shop lights, the occupied pub after shop shutdown, and the switched
  home kitchen each received a soft-shadow slot;
- `HDCachedShadowAtlas`, renderer-after-culling, duplicate-key, compile-error
  and null-reference matches: all `0` across the final EditMode and PlayMode
  logs.

Manual Editor validation remains required for refrigerator yaw/range,
wall leakage, subjective brightness, and visible shadow hand-off while moving
between groups containing more than four eligible local lights.

## 2026-08-11 inspection-office overexposure and shell leakage

The four inspection-office lights inherited the donor's generic 3200-lumen,
14-metre point-light values, then received the temporary `1.5x` interior bounce
compensation. Together with the 2 EV indoor exposure lift this produced 4800
lumens per fixture before exposure and illuminated the temporary building shell
from both sides.

The accepted home-only compensation is now restricted to `grid.home.*`.
Commercial fluorescent banks use the project HDRP profile directly: 1200
lumens, 6.5-metre range, 0.1 catalog indirect multiplier and reduced native
volumetric contribution. The deterministic world builder emits the same
inspection-specific limits, so rebuilding the catalog does not restore the
overbright donor values. Distance-first shadow selection gives all four
inspection lights occluding shadows while the player stands immediately
outside; the bounded four-light EveryFrame cap is unchanged.

Verification results:

- focused Lighting and world-light EditMode tests: `44/44` passed;
- D3D11 Bootstrap PlayMode: `1/1` passed in 36.8 seconds;
- all four inspection fixtures were enabled at exactly 1200 lumens and 6.5
  metres and received soft-shadow slots from the exterior test position;
- cached-shadow, renderer-after-culling, duplicate-key, compile and null-reference
  matches: all `0` in the final logs.

Manual visual acceptance is still required because the temporary donor shell
can contain missing, thin or one-sided occluders that no photometric calibration
can fully repair.

## 2026-08-11 home switch interaction and visual state

The seven Bootstrap switch targets were hidden generated proxies placed over
the donor presentation. They could update the electrical grid, but the visible
donor buttons had neither an interaction capability nor a collider. The proxy
also used its own hidden transform as the visual pivot and did not subscribe to
electrical state changes, so no visible button reacted to interaction, save
restore or developer state changes. At the hallway, donor collision could win
the raycast before the smaller hidden proxy and make the switch unavailable.

A bounded production adapter now resolves the seven home buttons by their
project-recorded donor stable IDs whenever the streamed home cell loads. It
attaches the project-owned interaction capability, preserves any existing host
capabilities, and adds a 12 x 12 x 7 cm minimum solid interaction collider to
the real button. The obsolete Bootstrap proxies are disabled so they cannot
compete for raycasts. Electrical state remains authoritative in
`ElectricalGridService`; donor metadata locates only the removable Phase 1
presentation.

The donor switch motion was transferred as configuration: `+12 degrees` local
X is off and `-12 degrees` is on. Every real button subscribes to grid changes,
so player interaction, save restore and external state changes all update the
same visible pivot immediately.

Verification results:

- focused Lighting EditMode tests: `32/32` passed;
- D3D11 Bootstrap PlayMode: `1/1` passed in 36.3 seconds;
- all seven real home buttons were bound with visible renderers, colliders and
  interaction hosts; all seven retired proxies were inactive;
- the hallway interaction toggled its grid state and moved the visible button
  from `+12 degrees` to `-12 degrees`; a direct grid update moved it back;
- cached-shadow, renderer-after-culling, duplicate-key, compile and
  null-reference matches: all `0` in the final test logs.

Manual Editor validation remains required for crosshair acquisition from the
normal standing position at each wall, especially the hallway's temporary
donor collision shell.

## 2026-08-11 home/Teimo fixture correction pass

The sanitized world import flattens donor presentation objects beneath a common
cell root. The prior switch animation assigned an absolute local `+/-12 degree`
X rotation and thereby erased each button's wall-mount basis. Depending on the
wall this made the rocker horizontal or nearly edge-on. Runtime binding now
derives the mount basis from the captured donor rocker angle and composes the
on/off angle on top of it. The real WC button is the eighth bound target.

Per the confirmed gameplay contract, the physical entrance and hallway buttons
now share `switch.home.hallway`. That state powers exactly three fixtures:
entrance, corridor and living hall. The WC fixture was removed from the generic
living-room binding and now uses `grid.home.toilet`, `switch.home.toilet` and
`zone.home.toilet`. Kitchen, both bedrooms, bathroom, WC, entrance, corridor
and hall use captured donor Light positions instead of fixture-bounds guesses.

Runtime world fixtures now receive a project-owned emissive lens controlled by
the same `GameLightFixture` visual factor as the real Light. HDRP Unlit,
Standard-style emission and base-color properties are all driven through one
shared material plus per-renderer `MaterialPropertyBlock`; off is black and on
tracks the profile emission. The presentation is a child of the streamed light
root and cannot survive its cell.

Long fluorescent fixtures in the garage, Teimo shop and pub use one-sided HDRP
Rectangle lights. The production adapter resolves the matching donor renderer
by stable ID/path and transfers its exact world rotation, so ceiling sources
face down and the garage bank retains its 90-degree layout. This removes the
omnidirectional Tube/Point contribution that crossed thin temporary walls
without lowering the accepted room brightness.

Finally, streamed lighting roots are disabled before deferred destruction.
`GameLightFixture.OnDisable` therefore unregisters old stable IDs synchronously
before a same-frame cell reload creates their replacements. The regression test
performs this exact unload/reload sequence on Teimo and requires eight unique
shop fixtures plus one pub fixture afterward.

Verification results:

- focused Lighting/world-light EditMode: `45/45` passed;
- D3D11 Bootstrap PlayMode: `1/1` passed in 36.94 seconds;
- two hallway rockers, three shared fixtures and the WC rocker/fixture passed;
- Teimo same-frame unload/reload retained `8 shop + 1 pub` unique fixtures;
- runtime emissive lenses and downward Teimo Rectangle orientation passed;
- `NullReferenceException`, `HDCachedShadowAtlas`, renderer-after-culling,
  duplicate fixture ID, duplicate-key and compile-error matches: all `0` in the
  final PlayMode log.

Manual visual acceptance is still required for subjective lamp placement,
emission geometry, garage/house containment and brightness. The current
`ElectricalGridService` remains a binary source/circuit/switch/save boundary;
whole-home kWh metering, physical fuses and electricity-bill payment/cutoff are
explicitly not claimed by this lighting correction pass.

## 2026-08-11 streamed fixture, lampshade and shadow-containment correction

The white points above domestic fixtures were generated HDR emissive spheres,
not donor lampshades. Point fixtures now bind emission to the resolved donor
renderer and never create replacement sphere geometry. The source material
keeps its authored base colour/alpha, and the temporary translucent shade does
not cast an opaque cap over the point source. Generated emissive geometry is
restricted to thin rectangle/tube lenses.

The production adapter now replays already-created runtime lights after startup
and one frame after every scene load. Registration is idempotent by stable light
ID, so callback order cannot leave an ordinary streamed cell with raw lights
but no `GameLightFixture`/emissive presentation. The catalog builder also owns
the three Teimo refrigerator definitions instead of relying on hand-edited YAML;
rebuild now produces 49 unique lights/bindings across 11 cells, including the
newly recognized jail, apartment and landfill fixtures.

Selected local lights always retain `LightShadows.Soft`. The nearest bounded
subset uses `EveryFrame` plus contact shadows for moving objects, while the
remaining selected lights use one requested `OnDemand` map for static room
containment. Lights outside the whole shadow budget remain uncached. This
removes the prior four-light cliff where a ceiling light changed to
`LightShadows.None` and immediately shone through a donor wall.

Domestic/enclosed sources are calibrated to 3000/3150 K, general fluorescents
to 4200 K and Teimo's shop/pub to 4100/2900 K. Emissive multipliers were reduced
to control bloom. Garage fluorescents receive a bounded additional 1.35x Phase
1 compensation and 7.5 m profile range; no global exposure was changed.

Verification results:

- focused Lighting EditMode: `33/33` passed;
- D3D11 Bootstrap lifecycle: `1/1` passed in `42.90 s`;
- the PlayMode route covers preloaded/same-frame-reloaded Teimo, normally
  streamed Fleetari and a return/reload of the home cell;
- `NullReferenceException`, `HDCachedShadowAtlas`, renderer-after-culling,
  duplicate fixture ID and missing binding matches: all `0` in the final log.

Manual Editor acceptance remains required for subjective brightness, shade
translucency, garage containment and shadow transitions on the target GPU.

## 2026-08-18 native-save runtime-derived electrical ID recovery

The valid document-version-16 `slot-01` save captured one flashlight source,
circuit and switch plus two active story-traffic circuits. Those IDs belong to
runtime presentation objects and are legitimately absent when the static grid
performs its early global-state restore. The strict grid therefore rejected the
otherwise valid save as containing an unknown ID.

The schema-1 participant now excludes those explicitly recognized
runtime-derived IDs from new payloads and removes them from older payloads during
preflight. Persistent world source/circuit/switch IDs remain strict and still
reject the complete DTO transactionally when unknown or duplicated. The save
file itself is not rewritten; item state remains authoritative for the
flashlight and traffic light output remains derived at runtime.

Verification results:

- focused Save Integration regressions: `3/3` passed;
- the existing local `slot-01` restored through the real D3D11 Bootstrap path:
  `1/1` passed in `45.63 s` without rewriting the save;
- the wider Save Integration suite produced `40/41`; the unrelated pre-existing
  `ReloadedSourceCell_DiscardsCanonicalCloneOfRehomedEntity` test also failed in
  isolation because its `PhysicsPickupTarget` had already been destroyed, so a
  full-suite pass is not claimed by this correction.
