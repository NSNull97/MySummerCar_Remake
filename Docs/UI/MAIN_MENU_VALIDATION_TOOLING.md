# Main menu validation tooling

Current night-lamp evidence: `home-vehicle-lights-play-20260904-01` passed
40/40 PlayMode cases, zero failed/skipped/inconclusive, in 50.2954676 seconds
(2026-09-04 17:45:50 UTC). Authoring also exited 0. The current generic evidence
manifest and `VehicleLightsStage/verified-editor-evidence.json` include this
run and capture hashes; prior EditMode and white-spot results remain historical.
Front/rear light-only on/off/on comparisons and return-to-noon shutoff passed.
See `MAIN_MENU_VEHICLE_LIGHTS_2026-09-04.md` for scope and measured results.

The September 2026 main-menu refresh keeps the existing `GameUiRoot`, uGUI,
UI settings store, save-service boundary and project-owned presentation assets.
Validation fixtures create independent temporary UI settings and save stores;
they never load or overwrite a player's save.

## Safe existing-Editor bridge

`Assets/Game/UI/Validation/Editor/MainMenuValidationBridge.cs` watches
`Artifacts/MainMenuRedesign/editor-request.json` after normal script import.
Ordinary requests use `id` (letters, digits, `_`, `-`, maximum 80 characters)
and `action`:

```json
{"id":"menu-edit-20260904","action":"edit-tests"}
```

Allowed actions:

- `status`: read-only status.
- `edit-tests`: existing `MSC.Tests.EditMode.UIRuntime` and `UIEditor` suites.
- `play-tests`: `MSC.Tests.PlayMode.UIPresentation` suite, including input and
  isolated visual capture fixtures.
- `capture-main-menu`: screenshots an already visible, single main-menu
  `GameUiRoot` in Play mode. It never navigates, changes resolution or saves.
- `open-owned-menu-review`: requires the exact `ownedProcessId` and
  `ownedProcessStartedUtc`, a clean idle Editor, no Play mode, no import/compiler
  failure and no Test Runner job. It opens only the canonical
  `Assets/Game/Bootstrap/Bootstrap.unity` and requests Play mode without saving a
  scene. It also shows/focuses that owned Editor's GameView so native capture can
  receive an end-of-frame backbuffer. This is production-menu review, separate from isolated test fixtures;
  the normal runtime's preferences and slot metadata may be shown. A completed
  request records the Play-mode request, not a successful boot or visual pass.
- `cleanup-owned-empty-scene`: an explicit recovery action for the validation
  process only. It additionally requires `ownedProcessId` and
  `ownedProcessStartedUtc` to match that Editor's exact current process identity.
  The Editor must be idle, outside Play mode, with no active Test Runner job and
  exactly one untitled scene containing zero root objects. Only that empty scene
  is replaced with a clean empty scene. Named or populated scenes are refused;
  no scene asset is saved. This is not an ordinary test prerequisite or a way to
  resolve an arbitrary user's dirty scene.

Tests refuse active Play mode, unsaved scenes, import/compilation, compilation
errors and another active Test Runner job. The bridge never stops a user's
session; normal test requests never discard dirty scenes. Request IDs are
idempotent. Each request
produces `result-<id>.json`; test requests also produce `tests-<id>.xml`.
Current state is in `editor-status.json`.

## Coordinated command-line runs

Use the Unity patch pinned in `ProjectSettings/ProjectVersion.txt`. Only launch
against this checkout when its active Editor is closed and other tasks have
finished writing scripts. Do not add `-quit` to `-runTests`: the installed Test
Runner exits after saving its result.

Arguments for the focused EditMode suite:

```text
-batchmode -nographics -projectPath <project-root>
-runTests -testPlatform EditMode
-testFilter ^MSC\.Tests\.EditMode\.(UIRuntime|UIEditor)\.
-testResults <artifact-directory>/UI.EditMode.xml
-logFile <artifact-directory>/UI.EditMode.log
```

For PlayMode, use `-testPlatform PlayMode` and filter
`^MSC\.Tests\.PlayMode\.UIPresentation\.`. Omit `-batchmode` and `-nographics`
for the capture fixture: it uses a real GameView backbuffer and
`WaitForEndOfFrame`. The CLI runner still exits automatically. On Windows,
launch background validation helpers with a hidden window.

The successful graphical run in this checkout used a plain Editor launch,
`-projectPath <project-root> -logFile <artifact-directory>/UI.GraphicalFinal.log`,
waited for clean idle status, then submitted the bridge's `play-tests` request.
Direct graphical `-runTests` startup had previously exited with `No callbacks
received`; those attempts are not passing test evidence. Prefer the proven
existing-Editor bridge for captures. A batch behavioral-only gate can exclude
`MainMenuCapture_` with the filter
`^MSC\.Tests\.PlayMode\.UIPresentation\.GameUiRootPlayModeTests\.(?!MainMenuCapture_)`.

## Behavioral and visual evidence

The focused suite retains settings, native-save request, game-session gate,
HUD, pause and existing route coverage. New main-menu regressions cover:

- primary navigation skipping unavailable saves;
- palette pagination without invisible focus;
- selected paint surviving UI destruction/recreation and reaching the existing
  new-game paint callback;
- hover/press animation using unscaled time and settling;
- real Input System keyboard and gamepad events through `InputSystemUIInputModule`,
  including WASD/arrows, Enter/Space/Escape, D-pad/stick and A/B;
- bounded main-menu layout, removed legacy cards and neutral glass tint.

The subsequent fully 3D correction uses the existing canonical Satsuma and
home-yard meshes through explicitly exported presentation-only prefabs. Its regressions
exercise red/blue palette changes through both material property blocks and real
offscreen pixels, preserve the borrowed canonical material assets, check the
component allowlist and measured ground placement, and verify hide/resume/disposal.
They also hide only the fixture's environment renderers and require the offscreen
pixels outside the car to change. The quality-switch fixture
renders through all three authored HDRP profiles, requires finite opaque output,
and restores the previously selected quality in `finally`.

`MainMenuVehiclePreview` submits a bounded HDRP `StandardRequest` into its owned
opaque RGB texture, capped at 1920×1080 with the actual viewport aspect. It renders
on reveal, aspect, paint, drag-orbit and local wall-clock minute changes, then
retains the result without issuing requests for an unchanged minute. Reveal
submits two warmup frames and a final frame after a short
sky-ambient settling delay. The camera, layer-31 presentation stage, fixed exposure
and light-layer-128 box key are owned by the preview. Real environment geometry receives
the car's shadows; there is no background image, photo quad or contact ellipse.
The same rendered output feeds the UI's existing blur.

The lighting profile supplies cloned Exposure and Tonemapping components only.
An explicit camera-only GradientSky supplies menu ambient light; the owned key
has `interactsWithSky=false`. This bounded menu presentation does not instantiate
Enviro, modify world weather or depend on the HDRP physically-based sky's global
celestial-light list. No gameplay vehicle, physics, audio, save participant or
world camera is instantiated.

The key is a finite HDRP box light with an orthographic punctual shadow map,
60,000 lux during daytime, a warm tint and a cooler sky fill. It shares no directional
cascade allocation with the existing world sun. The first production attempt
with an additional directional light reported a cascade-atlas conflict; that
attempt is not evidence of working menu shadows. The existing world light must
remain enabled and unchanged during the quality-profile regression.
A separate shadowless box fill, 22,000 lux during daytime, uses light layer 64, received only by
the vehicle; the environment retains layer 128. It follows the presentation
camera during orbit. The daytime owned GradientSky multiplier is 6,500. These changes
improve dark-paint readability without changing material assets or post-processing.

The user's later local-time request uses `DateTime.Now` by default through an
optional `Func<DateTime>` boundary, separate from the authoritative game clock.
One unscaled poll per second checks the local calendar minute. A changed minute
applies the day/twilight/night lighting state and performs bounded sky warmup;
an unchanged minute does not render. The pure time mapping uses an artistic
daily arc, not astronomical location or weather calculations. Night keeps an
owned 2,400-lux key, 9,000-lux vehicle fill and sky multiplier 1,800; twilight
uses 10,000/10,000/2,600 respectively. These values are implementation facts,
not visual approval or performance measurements. The rear fog's albedo also
follows local time: HDRP's volumetric directional-light evaluation does not
apply surface light-layer filtering, so the global world sun remains unchanged
while the preview controls its own medium's tint.

All existing visual fixtures inject fixed local time 2026-09-04 16:00.
`GameUiRootPlayModeTests.MainMenuLocalTime.cs` adds one integration regression
at 12:00, 20:00 and the following midnight, using Balanced HDRP with supported
local fog and the existing M3 world-sun prefab still active. It captures three
native 1920×1080 images under `Captures/TimeOfDay/`, measures broad car-crop
brightness/variation, and requires night to be visibly darker while readable.
It advances an injected clock through the actual unscaled poll, preserves the
same output texture, geometry, paint, camera and real `GameTimeService` DTO,
and checks that within-minute clock changes produce no extra frames. The
machine clock is not changed. Quality and owned fixture objects are restored
in `finally`; physical user input is excluded from this direct integration test.

The background-only `MainMenuOrbitDrag` adapter consumes normal uGUI pointer
events. LMB movement on free background orbits the camera; native double-click
returns to the authored view. Controls, palette panels, modals and other routes
retain their pointer ownership. Crossing a control, releasing LMB, losing focus
or disabling the adapter cancels the drag. There is no gameplay-input polling.
`MainMenuVehiclePreview.Orbit` uses screen-height-normalized deltas, bounded yaw
and pitch, and a stable camera radius fitted for the full permitted arc. The
model and environment transforms remain stationary while orbiting. At a clamp
or after release there are no repeated camera or blur requests.

The later atmosphere extension binds the existing garage lamp explicitly. Its
instance material property block supplies neutral emission. The subsequent
white-light correction uses an owned spotlight aimed at the vehicle center:
80-degree cone, 60-degree inner cone, 12-metre range and 120,000 native candela
at the full night lamp factor. Tests require white color, disabled temperature
grading, correct aim and all eight car-bounds corners inside the cone. The
10× change describes source intensity, not whole-image luminance.
A finite `LocalVolumetricFog` sits
behind the measured house bounds, outside the car and camera. The camera uses
spatial volumetric denoising without temporal reprojection. The authored
Performant HDRP profile has volumetric support disabled; the test records that
limitation and makes no fog-pixel claim for that profile. Shared HDRP assets and
world lights are never modified to enable it.

The existing three-quality regression checks the lamp's MPB and light, fog
placement and immutable shared materials/pipelines. Its lamp on/off pixel
comparison is restricted to the projected lamp-mesh bounds plus two pixels;
fog on/off/on comparisons run only with actual pipeline support. State changes
cross HDRP's early local-fog draw-submission boundary before two independent
render requests. A repeated unchanged-on image measures noise; the fog effect
must exceed that noise by ten times, change at least 0.5% of image pixels by
an RGB-sum delta above 0.02, and exceed a mean RGB-sum delta of 0.001. Restoring
fog must reproduce the original image within 10% of the effect (minimum
tolerance 0.0002). Three separately labeled High Fidelity diagnostic PNGs show
on, lamp-off and fog-off renders.
`ATMOSPHERE_EVIDENCE.txt` records the per-quality comparisons; completed XML is
still required before treating them as passing evidence.

`GameUiRootPlayModeTests.MainMenuOrbit.cs` adds two focused regressions: one uses
actual virtual-mouse events through `InputSystemUIInputModule` for drag, release,
UI/modal exclusion and double-click reset; only the focus-loss callback is
simulated. The other renders four yaw/pitch limit combinations at 16:10 and
32:9, checks all eight vehicle-bounds corners inside the reserved car area,
stable radius, unchanged geometry, reset, hidden-input rejection and idle
render count. It writes five native default/corner PNGs and context under
`Captures/Orbit/`. These are test definitions until a completed result below
records their execution.

The pause/settings style extension retains the existing route geometry,
settings callbacks, Apply/Reset/Cancel transactions, bindings and gameplay gates.
Surface checks address the actual nested `GlassBody`; the new transparent
control root is not the rounded surface. One additional PlayMode fixture writes
four native images under `Captures/SettingsPause/`: Graphics, General, Pause and
Graphics opened from Pause. It uses the real menu output for the first two,
then destroys that fixture before creating a separate mesh-only camera source
for the production `PauseFrozen` StandardRequest and one-shot Gaussian blur.
It verifies that later source paint renders leave the frozen pixels unchanged,
mouse hover animates at `timeScale=0`, Back retains pause/focus context and
keyboard Resume restores gameplay input and time. This is explicitly an
isolated rendering fixture, not a captured player gameplay session. All virtual
device filters are installed before the fixture's first yielded frame.

`GameUiRootPlayModeTests.MainMenuCapture.cs` uses the actual mesh-only environment,
vehicle, project logo, blur and native overlay canvas. It captures 1280×720, 1920×1080,
1920×1200, 2560×1080, 3840×1080 and 3840×2160 plus an empty-save-browser state.
It restores the previous GameView resolution and removes only temporary size
entries that it created. PNGs and `CAPTURE_CONTEXT.txt` go to
`Artifacts/MainMenuRedesign/Captures/`.

The fixture requires a completed `FrameReady` render before capturing the native
GameView, and a fresh render after an aspect change. It checks all eight projected
vehicle-bounds corners against the viewport. Separate red/blue
diagnostic PNGs read the linear HDR target and explicitly convert it to sRGB;
they are labeled separately from untouched native UI screenshots. Their pixel
comparison establishes real paint response, not visual approval.

The Editor-only GameView reflection adapter uses Unity's total indices for
custom-size removal, as defined by the
[Unity reference implementation](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/GameView/GameViewSizeGroup.cs).
It also recovers only entries carrying this harness's `MSC menu capture` name
prefix after an interrupted capture. Failed behavioral tests explicitly destroy
their registered fixture objects, preventing a second overlay from covering
subsequent screenshots.

The multiple-save screenshots contain explicit in-memory test metadata, never
player data. They are implementation evidence, not user visual approval. This
fixture does not enable review data or force a different UI locale.

## Reusable widget export

Invoke `MSC.UI.EditorTools.MainMenuWidgetAuthoring.BuildAll` with
`-batchmode -executeMethod <method> -quit`, or use
`Tools > My Summer Car > UI > Main Menu > Rebuild Reusable Widgets`.
It exports five prefab widgets and their project-owned sprite bank under
`Assets/Game/UI/Presentation/Content/MainMenu/Widgets/`, using the same
constructors as the runtime menu. It creates a temporary preview scene and
preserves the open scene setup.

## Private standalone fixture

Invoke
`MSC.UI.StandaloneValidation.EditorTools.MainMenuStandaloneBuild.BuildPrivateDevelopment`
from a coordinated batch Editor. This builds an isolated UI scene using real
menu assets, leaves project build settings untouched and deletes only its own
GUID-named temporary scene. It honors the project's donor-content build guard.
Both `BuildPlayerOptions.scenes` and the existing explicit build-guard scope
receive the fixture scene list; the helper must never assign to
`EditorBuildSettings.scenes`, including as a temporary override. This keeps
production streaming indices intact even if the build process is interrupted.

Run the resulting `Artifacts/MainMenuRedesign/Standalone/Development/MainMenuUiFixture.exe`
with:

```text
-msc-main-menu-probe <artifact-directory>/Standalone/Evidence
-logFile <artifact-directory>/Standalone/player.log
```

The player writes to a new run-GUID directory, captures 1920×1080, verifies the
empty native-save store and developer-button gate, observes 300 idle frames,
writes `result.json`, and exits with 0/2. Whole-player GC/frame observations are
not proof of UI-only allocation or full-game performance.
Before capture, it waits for both 120 frames and at least one real second,
so a high frame rate cannot capture the menu during its fade. Its post-binding
native store enumeration intentionally exercises save-operation completion.
It validates bright content in the expected logo and action regions of the
native PNG, records camera/canvas/window/pipeline diagnostics, and requires the
owned HDRP camera to render throughout the 300-frame sample window. Update
ticks, a file's existence, or `Time.renderedFrameCount` alone are insufficient.

A non-development player from this checkout is currently blocked because
global donor `Resources` would be included even with an isolated UI scene.
Do not disable the guard or move those assets to manufacture a release result.
A separately reported release C# compilation validates conditional branches
only; it is not a release build or performance result.

## Evidence interpretation

Only completed XML/player reports establish pass/fail. Startup compiler errors,
`No callbacks received`, missing XML and missing screenshots are incomplete
runs. Preserve their logs; fix the concrete issue and rerun the smallest gate.
Physical controller checks and user visual approval remain distinct from
automated virtual-device tests and screenshot generation.

## Executed evidence — 2026-09-04

The latest white-spot correction passed **40/40 PlayMode**
(`home-white-spot-play-20260904-01`, UTC 17:12:45, 56.528 seconds), with no
failures, skips or inconclusive cases. All 24 PNGs and six context reports were
regenerated. Noon, evening, midnight and 32:9 captures were inspected: the
white roof highlight and ground light are visible, with no apparent new
clipping or layout problem. Midnight car-crop mean luminance is 0.0233690 and
the readable fraction is 41.92%, versus 0.0205373 and 38.79% previously.
The prior **54/54 EditMode** gate was **not rerun for this spotlight change**.

The current complete captures, result/XML and manifest are archived under
`History/WhiteSpotFirst40-20260904/`. The previous manifest and XML are under
`History/SystemTimeFinal54Edit40Play-20260904/`; its PNGs had already been
overwritten before archival. The archive explicitly records that missing
binary copy and retains the original hashes, without substituting newer
images. The generic `verified-editor-evidence.json` describes the current
spotlight revision; the following SystemTime02 metrics are historical.

The preceding system-time, atmosphere and settings/pause revision passed **54/54
EditMode** (`home-system-time-edit-20260904-02`, UTC 15:52:30, 3.343 seconds)
and **40/40 graphical PlayMode** (`home-system-time-play-20260904-02`, UTC
15:56:17, 46.783 seconds), with zero failures, skips or inconclusive cases.
The run regenerated **24 PNGs**: seven main-menu resolution/save fixtures,
five orbit views, four settings/pause views, three day/evening/night views,
two paint diagnostics and three atmosphere diagnostics. Native UI captures and
HDR linear-to-sRGB render-texture diagnostics are identified separately in
`Artifacts/MainMenuRedesign/verified-editor-evidence.json`, alongside current
source/asset hashes and six capture-context reports.

Balanced night car-crop mean luminance is now **0.0205373**, with 38.79% of
the crop above the unchanged readable-pixel floor, versus noon 0.1713633.
The native night image was inspected: the car remains visible, the background
is dark and the rear haze is cool rather than brown. The clock gate also
passed its unchanged-minute no-render check and preserved the real gameplay
clock DTO, paint, geometry, camera and output texture. Final fog on/off/on
comparisons changed 153,231 pixels in High Fidelity and 158,458 in Balanced;
mean RGB differences were 0.0213269 and 0.0285825, with both unchanged-on noise
and restored-on error below 0.000001. Performant retains its authored lack of
volumetric support. These are Editor fixture results, not standalone FPS/GC
measurements or user visual approval of the new scope.

The optional final production Bootstrap capture was **not taken**. The owned
Editor had an unsaved untitled scene containing `Main Camera`, `Directional
Light` and `WwiseGlobal`; `cleanup-owned-empty-scene` correctly refused it at
UTC 15:58:08. `open-owned-menu-review` then refused the dirty scene at UTC
16:00:44. Both blocked reports are included in the evidence manifest. No guard
was weakened, and the scene was neither saved nor discarded by this workflow.
This is an Editor scene-ownership limitation, not a failed runtime test. For
an optional production review, the scene owner first resolves that unsaved
scene, then opens `Assets/Game/Bootstrap/Bootstrap.unity` and enters Play mode;
the menu uses the computer's actual local time. The 24 images above remain
clearly labeled fixture evidence.

The first local-time run passed **54/54 EditMode** (`home-system-time-edit-20260904-01`)
and **39/40 PlayMode** (`home-system-time-play-20260904-01`, UTC 15:46:42,
44.787 seconds). The sole failure was meaningful: the midnight car-crop mean
luminance was 0.0010357, below the unchanged 0.002 visibility floor, and the
native image was too dark. Its captures/results are preserved under
`History/SystemTimeFirst40-20260904/`. The stricter fog gate did pass: High
Fidelity changed 153,232 pixels with mean RGB delta 0.021327; Balanced changed
158,458 with mean delta 0.0285825. Unchanged-on noise and restored-on error
remained below 0.000001 in both profiles, and the on/off images show visible
rear haze. A subsequent bounded night-light correction preserves the pixel
thresholds; the final passing rerun is recorded above.

The first atmosphere/settings-style run completed **45/45 EditMode**
(`home-atmosphere-style-edit-20260904-01`, UTC 15:10:11, 2.201 seconds) and
**39/39 graphical PlayMode** (`home-atmosphere-style-play-20260904-01`, UTC
15:11:23, 35.994 seconds). The four new settings/pause screenshots were
inspected for layout and legibility; the pause capture uses a clearly labeled
isolated frozen-render fixture. Its rear-fog visual evidence was rejected on
review: only 57/71 pixels crossed the old permissive difference threshold and
the High Fidelity on/off images looked identical. This is not evidence of
visible fog. Results and captures are preserved under
`Artifacts/MainMenuRedesign/History/AtmosphereFirst39-20260904/`, with that
limitation recorded. The stricter frame-boundary, noise-baseline and on/off/on
comparison described above subsequently passed in the local-time run recorded above.
The user's new system-local-time lighting request also remains outside that
historical 39-test result.

The full home-yard implementation with compact controls, the warm box key and
vehicle-only fill passed **45/45 EditMode** (`tests-home-ui-edit-20260904-01.xml`,
UTC 13:32:44, 4.885 seconds) and **36/36 graphical PlayMode**
(`tests-home-ui-play-20260904-02.xml`, UTC 13:40:28, 20.184 seconds), with no
failures, skips or inconclusive cases. The latter includes the live world sun
coexistence check across all three HDRP quality profiles. Seven native fixture
screenshots and two paint diagnostics were regenerated at UTC 13:40:14–21.
The separate production review captured the actual 1363×694 menu at UTC
13:42:13. The user subsequently requested brighter lighting and drag orbit;
these records do **not** verify that later scope. Their XML, result reports and
images are preserved under `History/HomePreOrbit45Edit36Play-20260904/`, with
SHA-256 hashes in `verified-editor-evidence.home-pre-orbit.json`.

The preceding 35/36 attempt is retained as `tests-home-ui-play-20260904-01.xml`.
Its only failure compared Unity's internal, uninitialized Light JSON cache;
the corrected test compares the observable world-light state instead. The
final pass above is the executed evidence for that correction. Reusable compact
widgets exported with exit 0 in `HomeEnvironment.Widgets.01.log`.

The 33-test dark-glass and earlier records below precede the user's subsequent
live-Satsuma and fully 3D home-yard corrections. The intermediate photo-backed
vehicle suite passed 36/36 at UTC 12:14:51 (`tests-ui-vehicle-final-20260904-02.xml`),
but those results do not verify the later full-environment implementation.
Their image hashes describe those earlier revisions; the generated capture
filenames are reused by later graphical suites.

- Focused EditMode: **44/44 passed**, `UI.EditMode.Verified.xml`, UTC 07:58:39.
- Final dark-glass graphical PlayMode: **33/33 passed**,
  `tests-ui-glass-final-20260904-05.xml`, UTC 10:17:36, duration 13.756 seconds.
  It includes real native-save operation completion/failure recovery and the
  AA overlay component's enabled-state restoration across nested scopes.
  Seven native PNGs were regenerated at UTC 10:17:27–31. These supersede earlier
  visual captures; no screenshot was edited.
- Final widget export: `Widgets.Export.Glass.log`, successful exit 0.
  The five prefabs and sprite bank now match the dark-glass runtime factory.
  Final Presentation release-conditional C# compilation also completed with
  exit 0, after the glass, save UI and AA changes.
- Graphical PlayMode before the user's dark-glass correction: **32/32 passed**,
  `tests-ui-final-20260904-04.xml`, UTC 09:06:44, duration 13.611 seconds.
  This includes real virtual-device keyboard/gamepad events, stationary cursor
  focus restoration, palette persistence, main-menu-only AA diagnostic suppression
  and restoration, and the native screenshot fixture.
- All seven PNGs were regenerated by that passing run at UTC 09:06:34–39.
  These captures precede the requested dark-glass correction. The capture uses the runtime's transient AA diagnostic
  suppression; no image was edited to conceal the chip. Settings restore the
  existing diagnostic policy. The 32:9 workspace expands to a bounded 24:9.
- Five prefab widgets plus the serialized sprite bank exported through the
  pinned Unity Editor. `Widgets.Export.log` ends with successful exit code 0.
- Both UI runtime assemblies compiled again using player conditional symbols
  with `DEVELOPMENT_BUILD` and `DEBUG` removed. This is a C# conditional-branch
  check, using current project dependency metadata, not a complete release build.

Earlier failed logs/XML are retained. In particular, the first graphical run's
PNG files were invalid because a failed test left another overlay on screen;
`INVALID_CAPTURE_RUN_ui-gui-20260904-02.txt` records this. Those PNG paths were
overwritten only by the subsequent passing native capture run.
The intermediate 31/31 pass is retained as `tests-ui-gui-20260904-03.xml`;
the 32/32 report covers the final diagnostic and ultrawide corrections.

Private development standalone build/probe evidence is tracked separately;
no standalone timing or allocation result is implied by the Editor tests.

An earlier private development attempt reached player preprocessing and failed
after 27.326 seconds with three reported errors and twenty warnings. See
`Standalone.Build.Retry.log`, `Standalone/development-build-result.global-guards.json`
and `Standalone/blocker-analysis.json`. No executable was produced by that attempt.

Two project-wide asset guards initially blocked this UI-only scene. The base character
validator rejected the existing StoryTraffic-owned Suski material paths, although
the referenced wrapper contains no Animator and the base manifest hash is
current. Its generic error mentioned a missing fixture or Animator. Rebuilding
all character output would also delete nested StoryTraffic output and does not
resolve the incompatible validation contracts. Separately, generated player
voices were v1/16 clips while the current manifest was v2/27 clips. The dedicated
voice generator could use already hash-verified staging input.

The compatible correction preserves base checks and delegates only the exact
registered BetterSuski override to its strict StoryTraffic material/source
validator. Its focused Editor regression passed **8/8**
(`Standalone/SuskiValidation.EditMode.xml`). No Character or StoryTraffic output
was regenerated. The existing PlayerVoice generator then completed with exit 0,
producing schema 2/v2/27 clips using existing hash-verified staging. Its validated
deletion scope was only `RuntimeBaseline/Audio/PlayerVoice`; see
`Standalone/voice-output-preflight.json` and `Standalone/Voice.Regeneration.log`.

The next private development build **succeeded** at UTC 09:53:43, with zero
errors and 87 warnings, in 331.861 seconds (663,766,217 bytes). Its report is
archived as `Standalone/development-build-result.pre-glass.json`; its log is
`Standalone.Build.Final.log`. Both project guards passed. This executable
precedes the user's requested dark-glass correction and is not final visual
evidence. Build warnings include existing Enviro shader warnings.

The pre-glass standalone probe actually ran and captured a native 1920×1080 PNG
under `Standalone/PreGlassEvidence/run-20260904T095546Z-94a1570274db46498a314a8936482f28/`.
Its result is **failed**, because `LoadGame` stayed disabled after the probe's
post-binding enumeration of the real empty native store. `SaveCoordinator`
emits completion before clearing its busy flag; the synchronous UI completion
refresh therefore retained the busy state. This is an integration regression,
not a passing player gate. The probe's native operation remains as regression
evidence; the planned fix belongs in the UI adapter, without changing the save
service's API or event ordering.

The failed probe still recorded 300 idle frames: mean 1.581 ms, p95 2.083 ms,
maximum 6.167 ms and 400 allocated bytes/frame. These are whole development-player
observations on this machine, not UI-only GC or full-game FPS claims. Its managed
log callback counted zero errors/warnings; the native player log also contains
Unity TLS certificate messages. Keep that limitation visible alongside the
report. Final dark-glass Editor captures are complete. The subsequent dark-glass
development build succeeded with zero errors and 44 warnings in 58.760 seconds
(`Standalone.Build.GlassFinal.log`). Native save-button recovery passed, but
the player's screenshot was completely black. That run's initial `passed=true`
is overridden by `INVALID_RENDER_EVIDENCE.txt` in its evidence directory;
its 0.129 ms/zero-allocation measurements are rejected.

The next diagnostic player exited with 2 and correctly rejected the black PNG
before collecting performance data. It reported an enabled 1920×1080 HDRP
fixture camera and overlay canvas, an unfocused window, and **zero camera
render callbacks** during warmup despite thousands of Update/render-count
ticks. See `Standalone/RenderDiagnostics/run-20260904T102905Z-3b3548df490c4710bcc40c6da72cd94e/result.json`.
This indicates the hidden-player render loop is unsuitable for this measurement;
it does not establish a widget rendering failure. A normal visible isolated
player run has been requested from the user and is pending authorization.
No valid final standalone rendering/performance result is claimed yet.

An earlier attempt stopped at Unity's prohibition on additive creation beside
a clean untitled batch startup scene. The build helper now handles only that
single clean untitled case with an owned temporary scene; the executed retry
recorded an empty initial root inventory. Dirty-scene guards and preservation of
existing named scene setups remain intact, and temporary build scene files were
removed after failure.
