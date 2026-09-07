# Night vehicle lights in the main menu — 2026-09-04

Status: implemented; Unity authoring succeeded and all 40 PlayMode tests passed.
User visual review is pending.

The user's follow-up adds two white front Spot Lights and two red rear Point
Lights to the existing isolated menu Satsuma. They use the computer-local
day/twilight/night presentation already implemented for the menu. Intensity
fades in from 18:00 to 20:00, stays on overnight, and fades out from 07:00 to
08:00; daytime lights are disabled and lens emission is zero. No gameplay
switch, battery, save, game clock or Enviro state is involved.

Front lights have a 70-degree outer / 45-degree inner cone, a six-degree
downward aim, 30 m range and 60,000 candela each at full night strength. Rear
points use 4 m range and 1,500 candela each, pure red. All four use only the
existing preview rendering/light layers and remain fixed to the displayed car
while the menu camera orbits. They are owned by the disposable preview stage;
the vehicle prefab and its runtime model hierarchy retain the mesh-only
component allowlist. The stronger white garage spotlight is unchanged.

Authoring resolves the four existing project part IDs, then explicit known
mesh and material GUIDs. It derives positions from installed lens geometry:
two centimetres outside the front/rear lens face, in model coordinates. The
imported renderer's forward axis is not the vehicle's forward axis, so the
front beam direction is explicitly model +Z with the downward pitch.

The rear source mesh combines the upper red lens with lower amber/white
sections. A single generated menu-only clone partitions its original
triangles into two material slots: source vertices 0–23 form the red section
and 24–43 the lower section. Only the red slot receives emission. Both slots
retain the original shared material; vertices, normals, tangents, colors,
all UV channels and bounds are validated unchanged. This is a presentation
binding derived from the existing sanitized baseline, not new production art.
The original donor installation and canonical gameplay prefab are untouched.

Changed implementation:

- `MainMenuVehicleModel.cs`: serialized lamp bindings and authoring validation.
- `MainMenuVehicleLights.cs`: stage-owned lights and per-slot lens emission.
- `MainMenuVehiclePreview.cs`: light lifetime and existing local-time updates.
- `MainMenuVehicleAuthoring.cs`: stable-part mapping, lens positions, red/lower
  rear-lens material partition and provenance report.
- Existing local-time PlayMode fixture and its lamp-rendering helper.

The generated `SatsumaMenuPreview.prefab`, its report and
`Meshes/RearLampSections.asset` remain under the ignored private Phase 1
`RuntimeBaseline/Vehicles/Satsuma/MenuPreview/` path, classified
`TemporaryDirectImport`. Project-owned authoring/runtime code is
`Reimplemented`. No scene rewire, package install or save migration is needed.
Rebuilding the existing preview refreshes its serialized lamp bindings.

Actual validation on Unity 6000.3.11f1:

- Runtime C# compilation passed using the installed Unity compiler.
- `MSC.UI.EditorTools.MainMenuVehicleAuthoring.Build` exited 0:
  `Artifacts/MainMenuRedesign/VehicleLights.Authoring.01.log`.
  Output retains 249 renderers, 166 unique meshes and seven paint surfaces.
  The rear mesh preserves 44 vertices and 66 triangles (36 red / 30 lower).
- `home-vehicle-lights-play-20260904-01` passed 40/40, with zero failures,
  skips or inconclusive cases, in 50.2954676 seconds; completed
  2026-09-04 17:45:50 UTC. Matching JSON/XML are under
  `Artifacts/MainMenuRedesign/`; the log is `VehicleLights.Graphical.01.log`.
- The real midnight light-only on/off/on comparison changed 61,112 pixels
  in the front ground region and 14,524 in the rear region. Mean RGB delta
  was 0.008295 front / 0.002618 rear, with zero measured unchanged-frame noise
  and restoration error. Red-channel dominance passed for the rear points.
  Lens emission stayed enabled during these checks, so the result measures
  actual light on geometry. Returning to next noon disabled all four lights
  and lens emission through the normal wall-clock poll.
- Native midnight and rear diagnostic PNGs were visually inspected: white
  headlights and red upper rear lenses are visible. The rear capture uses a
  labelled test-only camera angle; production orbit limits remain unchanged.
- Canonical prefab, source manifest, yard prefab and build-scene configuration
  hashes are unchanged from this task's preflight. Generated output is ignored
  by Git. `git diff --check` passed for the changed source/docs.

The existing pure clock calculation is unchanged; its previous 54 EditMode
results were not rerun and are historical. No new standalone/performance claim
is made. No manual authoring step remains: open the main menu to review the
lights at the current computer time.

Next milestone: user visual review of the nighttime headlamps and tail lamps.
