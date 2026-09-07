# Main menu revision — 2026-09-04

Status: implementation complete. The final local-system-time, garage-lamp,
rear-fog and Pause/Settings revision passed **54/54 EditMode and 40/40 PlayMode**,
with native day/evening/night and Settings/Pause captures reviewed. The earlier
canonical-spawn/orbit revision was accepted by the user ("все супер"); the later
amendments await the user's visual review. Earlier photo/player results below
are historical and do not establish a current standalone/performance result.
The later white car spotlight passed **40/40 PlayMode** in
`home-white-spot-play-20260904-01`; EditMode was not rerun for that light-only
change. Current parameters and evidence are in `MAIN_MENU_WHITE_SPOT_2026-09-04.md`.
The next nighttime vehicle-lamp follow-up also passed **40/40 PlayMode** in
`home-vehicle-lights-play-20260904-01`, including actual front/rear light
on/off comparisons and next-noon shutoff. See `MAIN_MENU_VEHICLE_LIGHTS_2026-09-04.md`.

## Scope and authority

The direct 2026-09-04 user request supersedes the historical 08A **MainMenu**
composition only. The latest follow-up explicitly requests a complete game
scene without a photographic background. The selected location is the existing
home yard, using accepted world geometry and the real Satsuma. Settings/HUD
layouts, gameplay scenes, cameras, lights and the existing logo remain their established dependencies. This is a
bounded menu revision, not approval to enter Phase 2.
The later explicit Pause/Settings styling request is tracked separately in
`MENU_STYLE_UNIFICATION_2026-09-04.md`; its existing layout and behavior are retained.

Review input: `References/UI/Requested/2026-09-04/MainMenu_Reference.png`.
SHA-256: `2422ad4677091eb248133b836265d19fbc062cd50057c7f3d890f6bd89cc7ce1`.
It is outside Assets and is not a runtime dependency. The historical approved
08A reference and its hash remain intact.

Inspected: `Bootstrap.unity`, `ProductionUiInstaller`, `GameUiDependencies`,
`GameUiRoot` partials, `UiFactory`, `UiVisualAssets`, the common blur owner,
route/input/audio boundaries, UI settings store, native save UI, new-game paint
callback, the 08A prompt and UI architecture/reference/test documents.

Plan: retain uGUI and existing routes; replace only the main-menu construction;
bind state to existing services; add bounded interaction and responsive layout;
validate settings migration, focus, save actions, viewports and build variants;
capture the implementation for user review.

## Implementation and reused behavior

- `GameUiRoot.MainAndHud` constructs one `MainMenuWorkspace`, containing the
  logo, greeting, `PrimaryActions`, `CarColourCard`, `UtilityStrip` and version.
  The centre stays clear. The removed interior/trunk, performance and music
  blocks are no longer constructed, and their builder/graph-update code is
  removed. No hidden duplicate old menu remains.
- Existing Continue/New Game/Load/Credits/Quit callbacks and nested screens
  remain authoritative. Continue uses the latest valid `ISaveService` slot;
  Load opens the existing browser, including its empty state. The helper shows
  the actual SlotId. There is no fabricated slot or new save subsystem.
  Completion/failure notifications queue one presentation refresh after the
  existing service releases its busy guard. This prevents a post-bind native
  enumeration or failed load from leaving Load disabled. The save coordinator
  and event order are unchanged; settled UI never polls the service.
- `UiFactory.MainMenu`, `MainMenuActionButton` and `MainMenuStyle` supply the
  common glass surface, icon/title/helper construction and interaction states.
  Pause/Settings now explicitly opt into this style under the later user
  request; other screens retain their existing factory methods and theme tokens.
- Existing project font/logo and project-owned procedural icons are reused.
  The smile and puzzle glyphs are authored procedurally in the same icon bank.
- Existing shared Gaussian menu texture feeds `UiGlassSurface`; there are no
  per-button cameras or RenderTextures. Fallback is dark translucent glass.
  Main-menu tint is neutral, independent of the selected paint. UVs follow the
  animated visual's transform without recapturing the background.
- UI confirmation/navigation/back use the existing `IAudioBackend` events.
  Widgets do not access Wwise or AudioSource directly.
- Developer Tools requires both the existing development capability and its
  composed callback and is compile-time excluded in non-development players.
  Mods continues to open the existing Mods route; no mod SDK is introduced.
- The existing AA debug overlay is suppressed only while MainMenu is active.
  A disposable scope on the existing `AntiAliasingController` restores the
  diagnostic panel on other screens and on menu destruction. It changes no
  rendering settings and respects independently acquired suppression scopes.
  The first scope disables the existing IMGUI component; the last restores its
  previous enabled state, including a previously disabled overlay. Its expanded
  state is retained. This avoids entering hidden OnGUI work each frame.

## Paint selection and compatibility

The existing twelve-colour palette is shown as five columns by two rows with
two real page controls. Only the visible page's swatches participate in focus.
Persistent selection and keyboard focus are separate states.

`UiSettingsDocument.MainMenuCarColourIndex` is stored through the existing
`UiSettingsJsonStore`. Schema 6 migrates to 7 with the original preference
default (index 0) and preserves existing categories. Selecting paint persists
only the applied preference and preserves unrelated pending settings edits.
The existing `ConfigureNewGameVehiclePaint` callback still applies the selected
colour to a new Satsuma; native vehicle saves and `VehiclePaintSaveDto` are not
changed and loading an existing save does not recolour that vehicle.

The subsequent direct request authorizes the previously proposed real-vehicle
preview. `MainMenuVehicleAuthoring` reads the existing sanitized Satsuma and
creates a mesh-only display copy with stock parts, installed mount poses and
an open hood. It copies explicit paint bindings; `MainMenuVehicleModel`
changes only material property blocks on that copy. No gameplay controller,
collider, rigidbody, stable entity or save participant is instantiated.

`MainMenuVehiclePreview` owns one offscreen HDRP camera, isolated lighting and
a cached opaque 3D render. `MainMenuEnvironmentAuthoring` reads the active 06B2
global world and two home cells into an explicit mesh-only environment prefab;
the measured parking surface determines the vehicle anchor. Borrowed meshes,
materials and source transforms remain unchanged. Whole cross-cell ground
meshes are referenced intact. Accepted packed woody vegetation supplies trees
and shrubs without importing its gameplay/collider controllers.

The latest placement correction uses the canonical Satsuma metadata's exact
spawn X/Z, as used by `ProductionSatsumaInstaller`, and measures support on the
existing concrete. It preserves the menu car's facing direction. The briefly
tested offset parking point is superseded. A starting camera reference inside
the audited front arc allows yaw -20…45° and pitch -3…7° relative to that view,
with no move through the nearby house or trees. No world spawn data is changed.

The menu camera uses a procedural sky and finite HDRP Box lights excluded from
global sky interaction. The 60 klux warm key uses the punctual shadow atlas;
a 22 klux soft cool fill affects only the car through a separate rendering
layer. The world's directional cascade atlas remains available to its sun.
HDRP's physical-sky renderer scans global directional lights even
across light layers, so it is unsuitable for this isolated presentation. The
game world's Enviro owner and settings remain unchanged. The cached render
matches the viewport aspect; shared dark-glass blur refreshes only when that
render changes. Production bindings clear the superseded photo and plate shader.
The brightness follow-up raises only the owned key/fill/sky illumination;
the fixed exposure, tone settings and material assets remain unchanged.
The final atmosphere amendment illuminates the existing garage fixture through
its explicit renderer/anchor binding. Instance-only emission uses the source
albedo to retain dark hardware. The user's later white-light request changes
the source to an 80-degree spotlight aimed at the car, up to 120,000 candela
with a 12 m range; this is ten times the previous native source intensity.
The shared atlas material is never edited. One owned HDRP LocalVolumetricFog box
begins two metres behind the measured rear house boundary; foreground global
fog is nearly transparent. Spatial Gaussian denoising supports cached renders
and avoids history trails during orbit. The selected HDRP quality remains
authoritative: Balanced/High support volumetrics, while Performant disables
volumetrics in its existing pipeline asset. The lamp remains active at all
three qualities. No shared quality asset or Enviro/world fog owner is changed.

The subsequent explicit system-time request adds an isolated presentation clock:
production reads `DateTime.Now` in the computer's local timezone. An optional
`Func<DateTime>` in `GameUiDependencies` supplies deterministic fixture times;
it defaults to the system clock and does not replace `IGameTimeService`.
`MainMenuLightingTime` evaluates smooth artistic day/twilight/night weights:
dawn 06–08, daytime 08–18, evening 18–22 and night 22–06. These are authored
menu phases, not geographic sunrise calculations or real weather synchronization.
Key/fill intensity, sky colors and brightness, key direction and lamp intensity
follow those weights. Peak day remains 60/22 klux with sky multiplier 6500;
night retains 2400 lux key and 9000 lux car fill with sky multiplier 1800.
Twilight uses 10/10 klux and sky multiplier 2600. Local fog albedo also follows
the phases: HDRP volumetrics do not filter directional illumination by mesh
light layers, so a local color grade keeps the night haze cool even when the
gameplay sun remains warm. The world sun itself is never changed.
The lamp stays on, from 35% during daytime to 100% at night.
The visible preview polls the wall clock once per real second; it renders and
updates shared blur only when the minute changes (or after existing paint,
orbit, viewport and route events). Hidden previews do no clock/render work and
resynchronize on return. System clock or timezone changes are picked up by the
same minute comparison. No save, game calendar, world sun or Enviro value changes.
See `MAIN_MENU_HOME_ENVIRONMENT.md` and `MAIN_MENU_VEHICLE_PREVIEW.md` for
provenance. Image-generation history in `MAIN_MENU_GARAGE_PROVENANCE_2026-09-04.md`
is retained as a rejected presentation experiment.

No public route IDs, established serialized fields, scene/prefab identities,
native save DTOs or gameplay APIs are removed or renamed. Existing unrelated
working-tree changes are outside this revision. The UI itself transfers no
donor content. To validate the private standalone build, the existing approved
PlayerVoice schema-2 manifest was regenerated from verified staging: 27 clips,
no new extraction, unchanged event IDs and audio boundary. Its generated
payload remains ignored; the existing provenance ledger row was updated.

## Visual state and layout

The existing 1672×941 logical canvas is retained, equivalent to the requested
1920×1080 composition under CanvasScaler. After the user's in-game review,
main buttons are 300×72 logical pixels with 10-pixel gaps; utility controls
are 54 pixels high. Header and save helper share a vertically centered text
column; single-line actions center the title alone. Header size remains 22.
The prior 336×92 controls were superseded because the user found them too large.
The colour panel remains 350×210. Main and utility groups use
Vertical/HorizontalLayoutGroup with explicit preferred sizes and anchored
blocks. `MainMenuWorkspace` fits the safe area and keeps a centred, bounded
working area on 16:10, 21:9 and 32:9. The area widens to at most 24:9 while
controls retain their sizes; on 3840×1080 its outer side margins are about
481 pixels, before each block's own inset. Greeting follows the right anchor.
This moves the logo/palette away from the vehicle's hood/front wheel without
editing the background. The existing CanvasScaler owns density at
720p through 4K. Geometry is recalculated only when screen/safe-area/parent
scale changes.

Following the user's glass correction, panels use the actual blurred backdrop
under a neutral dark tint. The synthetic upper highlight and its generated
gradient sprite were removed. Selected/pressed surfaces have no orange fill;
accent remains on the text, icon and thin local border/glow. Neutral hover
brightening is slight, and the exterior shadow is reduced.

Tune `MainMenuStyle` for surface alpha (0.74), text/border colours, orange
`#FF9D00`, destructive `#FF5545`, 18-pixel corner radius, font sizes, 150 ms
hover, 90 ms press, scales 1.012/0.988 and 320 ms appearance. Focus supplies
the local border/glow; unavailable controls suppress hover and dim the
surface, icon and text. The animated child leaves layout bounds unchanged.
Animations use unscaled time, stop writing properties after settling and
respect Reduced Motion. The logo has no panel.

## Input

The existing InputSystemUIInputModule remains the sole navigation driver.
Its Enter/controller Submit binding gains Space; default WASD/arrows/stick/
D-pad bindings remain. Primary Up/Down stays within enabled primary actions.
Left reaches the selected visible paint; Right reaches utilities. Palette
rows, pages and utilities have explicit links. New Game receives initial
focus. Existing route selection is retained; semantic group/index restoration
also survives locale-driven reconstruction. Inactive route focus is cleared;
hidden swatches cannot retain focus. A stationary cursor's synthetic enter
event cannot steal restored keyboard/controller focus. Escape/controller
Cancel uses the same back routes and closes credits/quit dialogs with focus
restoration. Cancel on the root menu is idle so closing the existing developer
console cannot also open a quit dialog.

The user's subsequent inspection request adds a left-mouse drag on the free
backdrop. A bounded yaw/pitch camera orbit keeps the car framed left of the
actions; one fitted radius avoids zoom changes during the drag. Double-clicking
the backdrop restores the initial angle. Panels and controls intercept pointer
input, and leaving MainMenu, opening a modal or losing application focus cancels
the drag. Camera changes request one coalesced render and shared blur refresh;
the preview returns to cached rendering when input stops. This does not rotate
the car, its parts or the world, and adds no save state.

The hover correction separates pointer-origin logical selection from the
visible keyboard/controller outline. Leaving a button restores its normal
border; actual movement/submit restores navigation focus. Persistent paint
and page selections remain independent and keep their selected marker.

## Reusable prefabs

`Tools > My Summer Car > UI > Main Menu > Rebuild Reusable Widgets` uses the
same runtime constructors to export primary button, utility button, glass
panel, colour swatch and greeting templates plus a project-owned sprite bank.
Templates contain presentation references and host-bound actions; they create
no save, navigation or vehicle singleton. The current screen continues to use
its established code-authored construction, sharing these exact constructors.

## Validation

Final actual Unity 6000.3.11f1 results:

- `tests-home-system-time-edit-20260904-02.xml`: **54/54**, 3.3428087 s,
  completed 2026-09-04 15:52:30 UTC.
- `tests-home-system-time-play-20260904-02.xml`: **40/40**, 46.7827529 s,
  completed 2026-09-04 15:56:17 UTC. Both runs have zero failed/skipped/inconclusive.
- The added nine pure EditMode cases cover local-time phase continuity and
  midnight/date handling. Actual PlayMode clock jumps 12:00→20:00→00:00 run
  with timeScale zero, Balanced volumetrics and the existing world sun active.
  RenderTexture identity, camera/car/environment transforms, paint, world-sun
  properties and the real GameTimeService save DTO remain unchanged. A clock
  poll within the same minute produces no extra render. The first nighttime
  attempt failed readability; after adjusting owned night light, the unchanged
  visibility test passes (car crop mean .020537, versus noon .171363).
- Final native `Captures/TimeOfDay/` images show noon, evening and midnight;
  `Captures/SettingsPause/` has four settings/pause views. Their reviewed layout
  and readable night vehicle are implementation evidence, not user approval.
- Strong real fog On/Off/On checks pass at both supported qualities: roughly
  153k/158k pixels visibly change, with mean differences .0213/.0286 far above
  the unchanged-state noise baseline. Restoring the fog restores the image.
  Lamp mesh/light pixels change on all three qualities. Performant's existing
  volumetric disable remains intact. No shared material/pipeline asset changes.
- `MainMenuEnvironmentAuthoring.Build` exported `.4` with process exit 0 after
  the separately authorized, twice-verified scoped vehicle refresh. All 15
  source hashes match; EditorBuildSettings remains at its preserved baseline.
  Current source/log/capture hashes live in `verified-editor-evidence.json`.

The full final graphical log is `HomeEnvironment.SystemTimeGraphical.02.log`.
Offline Roslyn compilation was used before integration, followed by the actual
Unity gates above. No additional package, donor extraction or asset migration
was needed. No current standalone build or full-world performance measurement
was performed.

The canonical-spawn/orbit revision passed **38/38 PlayMode** in 25.152 seconds,
`tests-home-spawn-play-20260904-01.xml`, with zero failed/skipped/inconclusive.
It includes real mouse drag/reset/control exclusion and eight car-bounds corner
checks across the permitted camera arc. The production capture is
`MainMenu-home-spawn-production-final-20260904.png` (1363×694). The user then
accepted this result. The attempted `home-spawn-edit-20260904-01` request was
blocked by Play Mode and is not an executed test result. The preceding actual
EditMode run remains 45/45 in `tests-home-orbit-edit-20260904-01.xml`.

The first lamp/fog and Pause/Settings pass was 45/45 EditMode (2.201 s) and
39/39 PlayMode (35.994 s), in `home-atmosphere-style-*-20260904-01` results.
Its four Settings/Pause captures verify the new surfaces and actual frozen
backdrop path. Its fog comparison changed only 57/71 pixels on supported
qualities; visual review rejected that as insufficient evidence. The stronger
test waits for HDRP's next fog submission and checks On/Off/On against a noise
baseline. Local system-time lighting is a later amendment and is not covered by
that first pass. The final combined results above supersede it.

The complete home-yard/compact-controls revision passed **45/45 EditMode**
in 4.885 seconds (`tests-home-ui-edit-20260904-01.xml`) and **36/36 PlayMode**
in 20.184 seconds (`tests-home-ui-play-20260904-02.xml`). All failed, skipped
and inconclusive counts were zero. The tests exercised actual paint pixels,
three graphics qualities with an existing world sun, six native viewports,
pointer/keyboard/controller input and presentation lifecycle. The production
Bootstrap capture was `MainMenu-home-production-final-20260904.png`, 1363×694.
This evidence is preserved under `History/HomePreOrbit45Edit36Play-20260904/`;
it predates the brighter-light/orbit follow-up and is not its verification.

The preceding real-car/photo revision passed 36/36 graphical UI
PlayMode tests (`tests-ui-vehicle-final-20260904-02.xml`). It verified pointer
focus, actual paint pixels, quality changes and rendering lifecycle. Those
results do not validate the new home environment or orbit behavior.

Executed results and their limits are recorded below. Standalone/UI-fixture
captures are clearly distinguished from full production gameplay validation.
Physical gamepad use, audible backend output and user visual approval cannot
be inferred from automated navigation tests.

Historical static-image Editor evidence:

- UI EditMode: 44/44 passed (`Artifacts/MainMenuRedesign/UI.EditMode.Verified.xml`).
- Final graphical UI PlayMode: 33/33 passed in 13.76 seconds
  (`Artifacts/MainMenuRedesign/tests-ui-glass-final-20260904-05.xml`). This includes
  virtual keyboard/gamepad processing, stationary-pointer focus restoration,
  palette persistence, native-save requests, real native completion/failure
  recovery, actual AA-overlay disabling/restoration and seven GameView captures
  of the corrected dark-glass design. Earlier 31/31 and 32/32 runs are history;
  their screenshots predate the user's glass correction.
- Captures cover 1280×720, 1920×1080, 1920×1200, 2560×1080, 3840×1080 and
  3840×2160, plus empty storage at 1920×1080. The multiple-save fixture uses
  isolated in-memory test metadata. The empty-storage image is suitable for
  showing the implementation without suggesting that test slots are user saves.
- Five reusable widget prefabs and the sprite bank were generated by Unity;
  final `Artifacts/MainMenuRedesign/Widgets.Export.Glass.log` records exit 0.
  The removed highlight is absent from the regenerated templates. The filenames,
  capture hashes and test result are recorded in
  `Artifacts/MainMenuRedesign/verified-editor-evidence.json`.
- Final captures at all six requested sizes were visually inspected.
  Controls remain in bounds; the final 32:9 layout moves the logo off the hood
  and the colour panel away from the front wheel. The AA debug chip, synthetic
  upper highlight and orange panel fill are absent.
- UI runtime/presentation release C# branches compiled using Unity's compiler
  and installed metadata with `UNITY_EDITOR` / `DEVELOPMENT_BUILD` excluded.
  This is not evidence of a packaged release run. The existing private-content
  build guard prevents a release player because donor RuntimeBaseline assets
  in Resources would be included even in the UI-only fixture. The guard was
  retained; no assets were moved to evade it.
- Graphics PlayMode startup was interrupted by concurrent handbrake source
  changes in the shared checkout. Failed startup runs contain no test result
  and are not counted as passing or failing UI tests.
- One necessary adjacent compile repair was made in
  `Phase1SatsumaBaselineBuilder.ApplyReviewedHandbrakeFastenerMeshes`:
  `IReadOnlyList<FastenerBuild>.Count` replaces `.Length`. All other existing
  and concurrent edits in that file belong to the separate vehicle work.
- The standalone attempt exposed an existing Character/StoryTraffic validator
  incompatibility: default Suski already uses the registered StoryTraffic
  wrapper, while the base guard accepted only base-manifest materials. The
  bounded correction accepts only `presentation.character.suski` at the exact
  `StoryTraffic/Generated/Prefabs/suski-better.prefab` path and catalog identity.
  It validates the report/source lock, expected mesh, ordered material slots,
  HDRP shader, base texture and surface type. All previous base structural,
  replacement and animation checks remain. Only this override adds a read-only
  build-validation dependency on its existing configured BetterSuski staging;
  no character content is regenerated. Targeted Editor regressions passed 8/8
  (`Artifacts/MainMenuRedesign/Standalone/SuskiValidation.EditMode.xml`).
- The other global build gate found a stale schema-1/16-clip PlayerVoice
  library. The existing `Phase1PlayerVoiceImporter.BuildFromBatch` completed
  with exit 0 and regenerated only its checked PlayerVoice output directory:
  schema 2, 27 WAVs and 27 events (16 swear, 11 finger). Report and current
  manifest hashes match. Existing source GUID, PCM-format, size/duration and
  metadata/resource hash checks ran; no original-game extraction or broad
  character rebuild was performed. This repairs local generated content and
  does not establish manual audio acceptance.
- Private Windows development builds now succeed. The fixture uses actual UI
  assets and an isolated empty native save store; it is not a full-world build.
  The latest build result is in
  `Artifacts/MainMenuRedesign/Standalone/development-build-result.json`.
  Build warnings remain in the log (primarily existing vendor shader warnings).
- The pre-glass player exposed the native save-completion ordering defect
  described above; the 33-test Editor run verifies the resulting correction.
  A later hidden-player run produced a black screenshot despite live Camera
  and Canvas components. Diagnostics found zero actual camera-render callbacks;
  its apparent 0.129 ms/frame and 0-byte allocation result were rejected, not
  counted as performance evidence. The final probe now requires visible content
  in the logo/actions regions and camera-render callbacks during the sample
  window. It warms up for at least one real second and 120 frames. A visible
  player run was not executed. These historical attempts predate the real
  vehicle/home environment and do not verify current rendering or performance.

## File inventory

Paths below are relative to the repository; each new Unity source/prefab also
has its `.meta` file.

Modified presentation files under `Assets/Game/UI/Presentation/Runtime/`:
`GameUiRoot.cs`, `GameUiRoot.MainAndHud.cs`, `GameUiRoot.Save.cs`,
`GameUiRoot.Backdrop.cs`, `GameUiRoot.Settings.cs`, `UiFactory.cs`, `UiGlassSurface.cs`,
`UiVisualAssets.cs`, `UiTextCatalog.cs`.

New presentation files in that directory:
`GameUiRoot.MainMenuBindings.cs`, `MainMenuWorkspace.cs`,
`MainMenuActionButton.cs`, `MainMenuStyle.cs`, `UiFactory.MainMenu.cs`,
`UiAuthoringAssemblyAccess.cs`, `MainMenuOrbitDrag.cs`, `UiFactory.MenuScreens.cs`,
`GameUiRoot.MainMenuPreview.cs`, `MainMenuVehiclePreview.cs`,
`MainMenuVehicleModel.cs`, `MainMenuEnvironmentModel.cs`, `MainMenuLightingTime.cs`.

Modified settings files under `Assets/Game/UI/Runtime/Settings/`:
`UiSettingsDocument.cs`, `UiSettingsJsonStore.cs`, `UiSettingsValidator.cs`.

Editor/prefab authoring: modified `Assets/Game/UI/Editor/MSC.UI.Editor.asmdef`;
added `Assets/Game/UI/Editor/MainMenuWidgetAuthoring.cs`. Output directory:
`Assets/Game/UI/Presentation/Content/MainMenu/Widgets/` (five prefabs and one
sprite-bank asset).

Validation tooling: new `Assets/Game/UI/Validation/Editor/` bridge,
`Assets/Game/UI/Validation/Runtime/MainMenuStandaloneProbe.cs`,
`Assets/Game/UI/Validation/StandaloneEditor/MainMenuStandaloneBuild.cs`, and
their dedicated assembly definitions. The probe only exists in its explicitly
authored validation scene; production Bootstrap never creates it.

Tests: modified
`Assets/Game/Tests/EditMode/UIEditor/UIPresentationSourceContractTests.cs` and
`Assets/Game/Tests/PlayMode/UIPresentation/GameUiRootPlayModeTests.cs`; added
`Assets/Game/Tests/EditMode/UIEditor/MainMenuLightingTimeTests.cs`,
`GameUiRootPlayModeTests.MainMenuLocalTime.cs`,
`GameUiRootPlayModeTests.MainMenuAtmosphere.cs`,
`GameUiRootPlayModeTests.SettingsPauseCapture.cs`,
`Assets/Game/Tests/EditMode/UIRuntime/MainMenuPreferencePersistenceTests.cs`
and the `GameUiRootPlayModeTests.MainMenuRedesign.cs`,
`GameUiRootPlayModeTests.MainMenuCapture.cs`,
`GameUiRootPlayModeTests.MainMenuInput.cs` partials under the same PlayMode
directory. Tests cover real input action processing with virtual keyboard and
gamepad devices, not only direct Button callbacks. The new
`GameUiRootPlayModeTests.MainMenuDiagnostics.cs` partial checks the temporary
AA-overlay scope's lifecycle, actual component disabling/restoration,
independent ownership and unchanged AA settings. The new
`GameUiRootPlayModeTests.SaveOperationCompletion.cs` uses a real native
SaveCoordinator and isolated file store to check completion/failure recovery
without repeated storage enumeration.

Diagnostic integration: modified
`Assets/Game/Presentation/AntiAliasing/Runtime/AntiAliasingController.cs` and
`AntiAliasingDebugOverlay.cs`; the additive API controls only the debug panel.

Bounded build compatibility: modified
`Assets/Game/LegacyImport/Editor/GameplayPresentation/Phase1CharacterPresentationImporter.cs`
and `Phase1StoryTrafficPresentationImporter.cs`; added
`Assets/Game/Tests/EditMode/LegacyImport/DefaultSuskiBuildValidationTests.cs`.
The adjacent `Phase1SatsumaBaselineBuilder.cs` change is only the `.Count`
compile correction identified above. The standalone Editor helper also
handles Unity's single clean untitled batch-startup scene while preserving
named scene setups and refusing dirty scenes.

Documentation/review: this report, `Docs/UI/UI_ARCHITECTURE.md`,
`Docs/UI/APPROVED_08A_REFERENCE_MANIFEST.md`,
`Docs/UI/APPROVED_REFERENCE_DEVIATIONS.md`, `Docs/UI/SETTINGS_SCHEMA.md`,
`Docs/UI/UI_TEST_MATRIX.md`, `Docs/UI/MAIN_MENU_VALIDATION_TOOLING.md` and the
review-only reference named above. Bounded build provenance is recorded in
`Docs/Porting/DONOR_AUDIT.md` and the existing PlayerVoice row of
`Docs/Porting/PORTING_LEDGER.csv`. Logs, XML, test captures and private builds stay under ignored
`Artifacts/MainMenuRedesign/`.

## Manual review and remaining limits

Live-preview additions: `MainMenuVehicleModel.cs`, `MainMenuVehiclePreview.cs`,
`MainMenuEnvironmentModel.cs`, `MainMenuEnvironmentAuthoring.cs`,
`GameUiRoot.MainMenuPreview.cs`, `MainMenuVehicleAuthoring.cs`, `MainMenuPreviewInstallation.cs` and
`GameUiRootPlayModeTests.MainMenuVehiclePreview.cs`. Optional explicit preview
references extend `GameUiDependencies` and `ProductionUiInstaller`; Bootstrap
stores generated vehicle and environment references, with legacy photo/shader
references cleared. No save schema migration is needed for this follow-up.
Generated presentation payload remains under ignored RuntimeBaseline.

The production authoring command is
`Tools > My Summer Car > UI > Main Menu > Build and Wire Vehicle Preview`.
Its batch entry is `MSC.UI.EditorTools.MainMenuPreviewInstallation.BuildAndWire`.
It changes only menu-owned generated assets and the explicit Bootstrap menu bindings,
and does not change EditorBuildSettings or build-profile scene lists.

Open `Assets/Game/Bootstrap/Bootstrap.unity` and enter Play mode to review the
production composition. No manual scene rewiring is needed. Confirm the new
menu against the supplied reference, then use the existing Settings, Load,
Credits, Quit and development-console routes. Test audible UI feedback through
the installed audio backend and a physical controller; the automated fixtures
exercise virtual input devices and do not prove these hardware/audio results.
The final automated review request stopped at the validation bridge's
dirty-scene guard: an unsaved untitled scene contains Main Camera, Directional
Light and WwiseGlobal. It was not discarded or saved. This is not a failed
runtime/UI test. The optional production screenshot was not taken; the final
native captures are the explicitly labeled test fixtures above.

The six-resolution captures prove the implementation rendered at those sizes;
they do not constitute user visual approval. Automated fixture performance
cannot establish full-world performance or an improvement relative to an
uncaptured old-menu baseline. Existing unrelated project compiler/vendor
warnings are retained in logs rather than described as a warning-free project.

Current follow-up render/paint validation passes separately from the
earlier static-plate results. The model remains temporary Phase 1 presentation with a static
display suspension pose and no simulation.

Recommended next milestone: review the integrated menu in production Bootstrap
with the existing new-game/load flow and approve its visual presentation.
