# Dashboard controls — isolated Phase 1 implementation, 2026-09-05

## Scope / source

Project-owned physical choke, hazards and exterior-light selection, independent
from old input/FSM runtime. This is a bounded integration packet, not a claim of
complete dashboard, lighting, carburetor or electrical fidelity.

Read-only frozen extracted `Assets/_Scenes/GAME.unity`, SHA256
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Classification: `BehavioralReference` / `ConfigurationTransferred` /
`Reimplemented`. Existing knob meshes remain replaceable private Phase 1
`TemporaryDirectImport`. No donor code, FSM assets, binary payload or runtime
names were added.

| Action | Reviewed source | Contract |
| --- | --- | --- |
| Choke | Use109202, consumer109201; GO16738 / renderer76824; trigger99697 | Hold LMB increases Choke by .0303/frame, RMB decreases; clamp0..1. Mesh localY0..-.03m, separate original knob step.001/frame. |
| Hazards | Use108638, TurnSignals111485; GO30726 / renderer80567; trigger99619 | LMB toggles. Knob localY0/-45deg. Lamp pulse .4s on/.4s off. DashboardInstalled, DashboardMetersBolted, ElectricsOK and Dash1 OR Dash2. |
| Lights | Use108649, BeamMode107136; GO26594 / renderer79478; trigger99623 | LMB cycles Off/Parking/Headlights. Knob localY0/-45/-90deg. Original states named Shorts/Longs are not themselves low/high beam selection. Selection1 enables Markers+RearLights; Selection2 enables RearLights and sends CHECK to the separate BeamMode. |
| Headlamps | BeamsShort106670 / BeamsLong108276 | Per-side bolted headlight, source bulb database wear>=7, side wiring, FrontLightsHarness, SwitchLights, ElectricsOK. Native binding additionally requires the actual occupied bulb socket and unbroken live Items condition. |
| Indicators | Left108388, Right104179 | Front: corresponding headlight wiring + FrontLightsHarness + installed fender. Rear: corresponding rear-light wiring + installed rear light. |
| Tail lamps | RearLights106771, Light98291/98261 | Corresponding installed rear light and wire. Red point lights, source range1m. |
| Parking lamps | Markers106176 | Bolted marker part, corresponding marker wire, FrontLightsHarness, SwitchLights. No fake headlight substitution when marker content is absent. |

Source mesh GUIDs: choke `d1e33ce86b3d5154fbf7274bfb73d57e`,
hazards `a33e2f4b5288f4d44875c1a0e7945fb4`, lights
`5af90b41049184c4e8382b7cf0ce4f1d`. Editor-only source binding resolves exact
shared meshes, then serializes explicit project references. Runtime never searches
donor names or paths. Interaction uses existing continuous mouse capabilities,
not F; fixed .02m trigger spheres do not move with the choke knob.

## Implementation / integration

- `SatsumaDashboardControlsController` owns physical setting and intent. Physical
  control movement requires the assembled dashboard and bolted meters but is not
  blocked by a missing battery. Choke intent is exposed as `Choke01`; no fake RPM
  response or carburetor simulation was added.
- `SatsumaDashboardControlInteractionTarget` exposes hold-LMB/RMB for choke,
  one-toggle-per-LMB-press for lights/hazards, explicit knob-only outline and
  parent-owner occlusion bypass. Releasing a choke hold leaves its setting;
  disabling/detaching the target cancels the hold.
- `SatsumaDashboardLightingPresenter` gates actual HDRP-compatible Unity Lights
  against the live graph, source wiring requirements, electrical voltage/terminal
  readiness and dynamic purchased bulb health. No per-frame hierarchy search;
  condition component discovery occurs only when the installed bulb changes.
- `IAssemblyItemCondition` is a read-only boundary. The existing Items bridge
  implements it using live `WorldItemInstance` condition/broken state. Items keeps
  ownership and persistence; no duplicate condition field was added to vehicle.
- Run `Phase1SatsumaDashboardControlsAuthoring.ApplyToInstance(assembly,
  generatedRoot: null)` **after** the seven consumable mounts are authored.
  Full generation passes its staging root explicitly; a canonical scoped refresh
  omits the optional argument. Repeating authoring on an unchanged car returns0.
- Save integration belongs to `VehicleSaveParticipant`, via explicit optional
  `hasDashboardControls` and `dashboardControls`. DTO schema1 contains choke01,
  headlightsMode0..2, hazardsOn. `TryRestore(null, out failure)` is quiet default;
  invalid schema/range/non-finite values reject before mutation. Transient held
  input and blink phase are not saved. `ActionRequested` is never emitted during
  configure/restore/simulation; only accepted physical input emits it.

## Deliberate limitations / calibration

1. Original frame-based choke increments are converted at an explicit60Hz
   reference to1.818units/s. A single authoritative normalized setting drives
   visual travel. The donor independent.001m/frame knob reaches its visual end
   slightly before its logical Choke reaches1; that frame-dependent mismatch is
   not reproduced. This is not a claimed byte-identical intermediate pose.
2. Headlights use the source default dipped-beam output. Separate stalk high-beam
   selection, turn-signal cancellation and other dashboard gauges are not in this
   packet. `Parking` must not be renamed to `LowBeam` in saves or UI.
3. Existing baseline may lack the separate marker-light part roster. Authoring
   skips only those explicit optional owners, preserving parking mode intent and
   tail lights. It does not substitute headlights. Adding those parts remains a
   content follow-up if the canonical roster still lacks them.
4. Lamp positions/color/range follow inspected source frames. Photometric output
   is an explicit HDRP calibration: dipped3000cd, point20cd. Legacy intensity3/2
   is not claimed equivalent to those photometric units. No shadows/volumetric
   light are added. In-game night/day brightness needs user review.
5. Outputs illuminate actual scene geometry, but isolated emissive lens/flare
   presentation is not implemented; entire fenders must never be made emissive
   as a shortcut. Dashboard button click/blinker audio is also still absent.
6. Bulb wear/battery charge consumption from lamp use is not newly simulated by
   this packet; existing live health/power are consumed as read-only gates. Do
   not claim electrical drain parity without mapping source Charge units.

## Verification

Added `SatsumaDashboardControlsTests`: physical controls without battery,
mouse-only continuous capabilities, single toggle per press, rotated choke axis,
release persistence, quiet/atomic DTO restore and invalid payloads, actual Light
output gating, dynamic bulb wear threshold7, unbolted headlamp rejection,
per-side wiring, parking vs headlamp distinction, power loss, .4/.4 hazards and
Dash2-only branch, generated bindings and idempotence.

Source-only `dotnet build Logs/dashboard-controls-compile.csproj --nologo -v:q`
passed with0errors/0warnings, runtime and Editor helper. A test-inclusive source
compile encountered only the pre-refresh compiled Assembly DLL missing the newly
added `TryRegisterDynamicPart` API; the source API already exists. Tests must be
executed after root's single coordinated Unity compile/scoped refresh. No Unity
process or save was modified by this subtask. Runtime play verification remains
pending; do not label parity Verified on the basis of compilation alone.
