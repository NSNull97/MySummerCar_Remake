# Milestone 08A report -- reference-locked main menu, settings and HUD

Status: **Five menu/settings screens remain ImplementationComplete /
VisuallyApproved; 2026-08-05 HUD revision TargetedPlayModePassed /
CaptureBlocked / VisualApprovalPending; 2026-09-02 context-action revision
ImplementationComplete / FocusedTestsPassed / VisualReviewPending**
Original closeout: **2026-07-20**; HUD direction update: **2026-08-05**;
context-action update: **2026-09-02**

The bounded Milestone 08A implementation and its final 2026-07-20 Gaussian
correction are technically complete. Focused authoring, EditMode, PlayMode,
private development build and native capture gates pass; corrected captures are
recorded. On 2026-07-20 the user reviewed the corrected presentation, including
the denser Gaussian blur, and explicitly accepted Milestone 08A as excellent.

### 2026-09-02 context-action direction and direct corrections

The new review-only reference replaces the old projected target-title/action
blocks with a centre dot/open-palm/check/cross reticle and a lower-left stack of at most
three compact translucent plaques. The user additionally requires carried-item
release, signed mouse-wheel actions and an Alt-held H/M/N alternative-action
layer, then explicitly removes the proposed `ALT — ДОП. ДЕЙСТВИЯ` plaque.
The subsequent in-Editor review adds rounded binding keycaps, effective-button
mouse glyphs, stronger icon halos and a dynamic bottom-centre target/subtitle
stack with distinct title/subtitle typography.

The implementation preserves the player-facing `CrossdotPresenter` and
interaction controller APIs while replacing their presentation internals with
a fixed-capacity typed snapshot. Directional read-only capability extensions
make bolt, steering-alignment and sauna-control hints endpoint-aware without
probing mutating operations. `PlayerInputRouter` owns the Alt state and cached
live binding labels and glyph categories; existing H/M/N dispatch remains
unchanged. `IPlayerSubtitleSource` keeps subtitle timing outside the HUD, while
`IRemovalInteractionTarget` exposes the cross without parsing localized text.
The source image is documentation-only; runtime icons come from pinned ISC
Lucide and Apache-2.0 Material Symbols sources.

Current interaction, player, vehicle assembly, home, Needs, EditMode-test and
PlayMode-test sources compile in Unity `6000.3.11f1` batch mode. The focused
related EditMode filter passes `88/88`; isolated Alt input passes `1/1`; the
broader player-flow class passes `8/9`, with only its pre-existing carry-spring
tolerance case failing. A canonical gameplay capture remains pending. This revision does not change
routes, stable IDs, save DTOs, gameplay simulations or the approved persistent
needs/time/money composition; it reopens only context-action visual approval.

### 2026-08-05 direct HUD direction correction

The user supplied a new bounded gameplay-HUD style direction and a direct
screenshot correction. It replaces only the live HUD composition: all six needs
form a vertical upper-left column, while the clock moves to upper-right and
money sits directly beneath it in `value MK` order. Typography increases by only
1–2 px and the need tracks widen from 124 to 144 px. The reference's
speedometer/gear cluster is explicitly excluded. Urine and Dirtiness are
retained even though the image omits them. Text, icons, need tracks, fills and
amber indicators receive a shared dark shadow so they remain readable over
bright sky, water and roads. The Russian Dirtiness label remains
`НЕОПРЯТНОСТЬ`. The optional FPS counter uses the same panel-free shadowed
treatment, with a pure-white numeric value followed by an amber `FPS` suffix and
no decorative line.

The source image is catalogued as review-only under
`References/UI/Direction/`; no reference pixels are runtime dependencies. This
bounded correction does not modify routes, settings/save schemas, stable IDs,
gameplay authority or the five accepted menu/settings screens. It reopens only
HUD validation, capture and user visual approval.

The revised runtime and PlayMode test assemblies compile. Three focused HUD and
backdrop/spacing tests pass. The full UI PlayMode class reports `16/17` because
the pre-existing Graphics assertion still expects the already removed
`UpscalerQualityState`; this failure is outside the HUD correction. A new
private-player capture was attempted but the player build is blocked by
unrelated `CS1061` use of `Light.lightmapBakeType` in the untracked
`Assets/Game/World/Runtime/Lighting/WorldLightingProbeRuntime.cs`. The HUD
capture and renewed visual approval therefore remain pending without expanding
this task into world-lighting work.

The latest vertical-layout rerun on 2026-08-05 passes all three affected
contracts: HUD positions/order/type sizes/track width/colour/shadow/no-line,
panel-free backdrop ownership and shared spacing
(`M08A_HudMinimal_Vertical_*_PlayMode.xml`).

A subsequent direct screenshot correction reduces the day/date field gap to
4 px and splits HUD FPS formatting from the complete main-menu performance
string: the HUD value is digits-only and one separate amber `FPS` suffix follows
it at the same 17 px size. Runtime and test assemblies compile; the updated
focused PlayMode rerun is pending because the project is currently open in the
user's Unity Editor, which correctly rejects a second batch instance.

### 2026-07-20 direct user correction

The user approved a bounded correction to the 08A implementation baseline:

- the static menu backdrop is full-canvas and outside `UiScale`;
- the production world may be prepared while gameplay stays dormant until
  `New Game` activates the explicit `IGameplaySessionGate`;
- Graphics performance preview, Audio test, Controls input preview and Gameplay
  profile/immersion/summary are removed;
- every settings page uses one equal-size `Apply / Reset / Cancel` row over the
  existing Pending/Applied transaction, including control binding overrides;
- main-menu/category buttons are smaller and internally consistent, and the
  greeting aligns with the main action stack's right edge;
- menu glass mixes only 8% of the selected car colour;
- rounded surfaces use a supersampled procedural 1.75 px ring, slider knobs are
  true `16 x 16` circles, and toast expiry hides the whole toast object.

These are direct user-approved deviations from the earlier reference blocks.
The final Gaussian correction replaces the
earlier `1/2 -> 1/4 -> 1/8` blur pyramid and recurring HUD capture. Its focused
rerun, private build and corrected recapture pass; the user subsequently closed
the visual and pause/readability gate on 2026-07-20.

## 1. References inspected and integrity

The six authoritative files under `References/UI/Approved/08A/` were inspected
at their native `1672 x 941` viewport. The Editor validator confirmed names,
roles, dimensions, byte sizes and SHA-256 hashes:

| Screen | SHA-256 | Integrity |
|---|---|---|
| Main menu | `cd04e5d9ea7b60989a99d92c640b27ddca856994bf26a25730c0e1b3e33054c0` | `PASS` |
| Graphics | `b18e04937bd0e7b9eb1819242a5e27363011a3b6a8423bc9c5a9c2500f14e641` | `PASS` |
| Audio | `e2189dcc8b382080ff6dffedad5488c361242d8ff542e6f7cba4659532e33de5` | `PASS` |
| Controls | `0db5b2cbfdb5c6169bf8ca69627ca53e8408826ed1bd4d1f34c02a3d37ee4052` | `PASS` |
| Gameplay | `7c1870cb4852d881ee8d0f60eb10ddf2337a1f9e74ea1724ae14e63139601eb7` | `PASS` |
| HUD | `088d2f9448b921c92cb0ad818af100975649c97eaa65a0126c26d6e427b9515f` | `PASS` |

Reference PNGs remain outside `Assets`. Runtime and presentation source do not
load them; only the Editor review assembly combines implementation captures
with reference pixels. The successful player build and native capture run did
not require the reference files as runtime visuals.

## 2. Technology selected

The runtime uses first-party uGUI plus the Unity Input System. No third-party
UI framework, external font or icon package was added. The decision and
rejected alternatives are recorded in `Docs/UI/UI_TECHNOLOGY_ADR.md`.

## 3. Architecture and visual ownership

Project-owned boundaries are:

- `MSC.UI.Runtime` for routes, capabilities, localization keys and versioned
  settings;
- `MSC.UI.Presentation.Runtime` for uGUI, route flow, theme, procedural icons,
  settings pages, HUD and development-only capture probe;
- `MSC.UI.Editor` for reference validation, overlays and review generation;
- explicit Bootstrap composition through `ProductionUiInstaller`.

`Bootstrap.unity` remains build index 0. Gameplay suspension uses
`IGameplayInputGate`, `IUiVisibilityGate`, project time pause and restored
cursor/time state. Runtime composition uses explicit references, not donor
hierarchy names or a service locator. Pause scans the full production
composition root, so player and sibling vehicle gates are suspended together;
the chase camera uses scaled delta time and therefore freezes with simulation.

Main-menu startup now uses a separate `IGameplaySessionGate`. World composition
and streaming can reach a prepared state while player camera/environment
simulation remain dormant. `New Game` activates this gate idempotently before
the HUD is entered. Non-menu/headless startup retains the established automatic
activation path.

The 2026-08-06 input-restoration correction preserves the pending enabled state
of an `IGameplayInputGate` whose hierarchy is inactive behind Main Menu. Before
this correction, the inactive `PlayerInputRouter` reported its action map as
disabled, that transient `false` was saved, and New Game restored it after
activating the player hierarchy; movement and camera sampling could therefore
remain disabled. Active gates still preserve their live state, and inactive
components authored as disabled remain disabled. The focused regression also
covers the following pause/resume cycle. Runtime and test assemblies compile;
the Unity PlayMode execution is pending because the project is open in the user
Editor and cannot be opened simultaneously by batch mode.

Main menu and settings use a sharp project-owned `1672 x 941` garage plate.
Only masked menu glass samples aligned slices from its one-time softened
full-resolution `1672 x 941`, ARGBHalf Linear Gaussian result, with an 8% tint
derived from the selected car colour. Four separable horizontal/vertical
iterations use radii `2 / 4 / 6 / 8`. The
full-canvas plate is outside `UiScale` and uses aspect-preserved crop coverage,
so a reduced UI scale cannot expose the dormant gameplay camera at the edges.
Gameplay stays sharp; the revised HUD uses shadowed scene-integrated graphics
with no persistent panel, live capture or blur. Pause captures `1024 x 576`
once, filters to a `512 x 288` ARGBHalf Gaussian result, displays it full-screen
and adds a `0.74`-alpha dark dim.

Filtering uses the project-owned
`Assets/Game/UI/Presentation/Content/Shaders/M08A_SeparableGaussianBlur.shader`.
`ProductionUiInstaller` supplies it as an explicit serialized dependency through
`GameUiDependencies`; runtime code does not call `Shader.Find`. The earlier
`1/2 -> 1/4 -> 1/8` downsample/upscale pyramid is removed.

Detailed architecture: `Docs/UI/UI_ARCHITECTURE.md`.  
Visual contract: `Docs/UI/VISUAL_LANGUAGE.md`.

## 4. Screen implementation state

| Screen | Technical state | Visual state |
|---|---|---|
| Main Menu | `ImplementationComplete` | `VisuallyApproved` |
| Graphics | `ImplementationComplete` | `VisuallyApproved` |
| Audio | `ImplementationComplete` | `VisuallyApproved` |
| Controls | `ImplementationComplete` | `VisuallyApproved` |
| Gameplay | `ImplementationComplete` | `VisuallyApproved` |
| Default HUD | `ImplementationComplete` | `VisuallyApproved` |
| Loading | `ImplementationComplete` | `ReferencePending` |
| Pause | `ImplementationComplete` | `ReferencePending` |
| Confirmation dialog | `ImplementationComplete` | `ReferencePending` |
| Save/load status | `ImplementationComplete; truthful unavailable state` | `ReferencePending` |
| Accessibility | `ImplementationComplete; bounded foundation` | `ReferencePending` |
| Mods | `ImplementationComplete; truthful unavailable placeholder` | `ReferencePending` |

The main-menu action order, shared settings category order and persistent HUD
categories follow the locked references. Continue and Load are visible but
disabled because no save storage provider exists. Quit opens a functional
confirmation dialog. The save/load status screen states that storage is
unavailable and never fabricates slot data. No minimap, GPS, quest tracker,
vehicle telemetry or hotbar was added.

The post-capture user refinement is also implemented: every settings category
uses one invariant logo/navigation/Back/greeting geometry; only its
central/right content and selected category state change. Main/category buttons
use corrected smaller bounds and the greeting shares the main action right edge.
Major surfaces use a visible 12 px radius with a continuous procedural 1.75 px
ring, navigation/action groups use explicit 10-12 px gaps, slider handles are
fixed circular `16 x 16` controls and the three main-menu cards no longer touch.
Transient toast expiry disables the full panel rather than leaving an empty
surface.

## 5. Settings

The accepted 08A baseline introduced schema v2 for Graphics, Audio, Controls,
Gameplay and Accessibility. The current post-08A schema is v5 and retains the
same validation, Defaults/Applied/Pending transactions, atomic JSON writes and
corrupt-file quarantine while adding compatible migrations through v4. Settings
remain separate from world saves.

Supported application covers resolution/display/refresh, VSync, Unity quality
level, the project `IAudioBackend`, locale, UI scale, mouse/gamepad look
sensitivity and inversion through `IPlayerLookSettingsSink`, Input System
dead-zone policy, binding override persistence, and horizontal FOV/gameplay
camera far clip through `IPlayerCameraSettingsSink`. Unsupported advanced
graphics, independent audio buses, save/profile features, vibration and
remaining gameplay/accessibility adapters stay disabled or adapter-pending.

The live pages no longer reproduce non-essential reference diagnostics:
Graphics performance preview, Audio test, Controls input preview and Gameplay
profile/immersion/summary are absent by direct user decision. All settings pages
share `Apply / Reset / Cancel`. Reset changes Pending to Defaults without
writing; Cancel restores Applied; Apply validates and persists. Accepted input
rebinds remain Pending until Apply instead of committing from the rebind widget.

See `Docs/UI/SETTINGS_SCHEMA.md`.

## 6. HUD

The HUD uses the revised corner-and-lower-row composition and exactly the
approved persistent categories. Project game time is authoritative; needs use
the explicitly composed `IPlayerNeedsService`, with a truthful unavailable
fallback. Money still has no production provider. Deterministic review values
exist only in Editor/development mode. HUD text and bars refresh from authority
revisions, not fabricated state. Clock, money and needs use shadowed
scene-integrated graphics without panels, camera capture or blur, preserving the
earlier correction for severe look-input stutter.

Context actions now use the separate 2026-09-02 contract: semantic centre
affordance state, a lower-left action stack and a bottom-centre dynamic
target/subtitle stack, with no target-title projection and no permanent Alt
instruction. `PlayerInteractionController` remains the gameplay authority;
presentation consumes its non-mutating action snapshot and explicit display
metadata.

See `Docs/UI/HUD_SPEC.md`.

## 7. Input, accessibility and localization

- UI navigation uses `InputSystemUIInputModule` with visible uGUI focus states.
- `System/Pause` has keyboard Escape and gamepad Start bindings.
- Player and vehicle binding overrides persist separately; rebinding supports
  cancel and effective-path conflict rollback.
- Look sensitivity/inversion and dead zone use project-owned adapters.
- Navigate/Confirm audio events use `IAudioBackend`, never Wwise directly.
- UI scale has a live presentation adapter. High contrast, reduced motion,
  toggle/hold and colour-independent-cue settings persist, but the remaining
  consuming adapters are still bounded future work.
- English/Russian presentation text and stable IDs exist. `UiLocaleFormatter`
  supplies culture-aware number/date formatting and English/Russian plural
  rules; the HUD date/day/time and input-device labels use the active locale.
  `UiFontResolver` validates the complete bounded Latin/Cyrillic glyph set
  before accepting a Windows font and has an explicit Unity fallback. A formal
  production string-table workflow and an approved bundled font remain later
  localization/art work.

See `Docs/UI/ACCESSIBILITY_CHECKLIST.md` and
`Docs/UI/LOCALIZATION_READINESS.md`.

## 8. Tests and actual results

The table below records executed evidence for the final 2026-07-20 Gaussian
correction.

| Gate | Actual result |
|---|---|
| Focused UI EditMode | `33/33 PASS`; `TestResults/M08A_UI_GaussianBlur_EditMode.xml` |
| UI PlayMode smoke | `11/11 PASS`; `TestResults/M08A_UI_GaussianBlur_PlayMode.xml` |
| Full project EditMode | pre-remediation context only: `386/392 PASS`; 6 known failures outside 08A; `TestResults/M08A_FullEditMode.xml`; `Logs/M08A_FinalEditModeFull.log` |
| Bootstrap authoring/wiring | `PASS`; explicit Gaussian shader reference validated; `Logs/M08A_UI_GaussianBlur_Wire.log` |
| Private Windows x64 development build | `PASS`; payload `851,423,934` bytes; `Logs/M08A_UI_GaussianBlur_Build.log` |
| Native D3D12 player boot and 12-screen capture | `PASS`; completion marker present; `Logs/M08A_UI_GaussianBlur_PlayerCapture.log` |
| Supplemental native DX11 capture | `PASS`; ignored output `Temp/M08A_UI_GaussianBlur_DX11`; `Logs/M08A_UI_GaussianBlur_DX11Capture.log` |
| Canonical review-set generation | `PASS`; corrected captures updated 2026-07-20 under `Docs/UI/Review/08A/` |

The focused suites cover route/focus flow, directional controller-style focus,
route selection memory and modal restoration, bounded New Game entry without
save/profile mutation, locked action order and HUD categories, pause input
gating, persisted UI scale/look/dead-zone application, rebind cancel/rollback,
binding conflicts, locale formatting/plural rules, settings
transactions/migration/recovery, capability truth, reference integrity,
missing-reference detection and blend/aspect handling. Remediation coverage also
locks the three backdrop modes, car-colour glass tint, composition-wide pause,
pixel-perfect/fractional-alpha rasterization, invariant settings chrome,
explicit inter-panel/action gaps, strengthened radius family, complete toast
dismissal, explicit Gaussian shader contract, stable HUD without live capture
and one-shot pause ownership.

The older full-project run was not repeated after the bounded remediation and
is retained only as regression context. Its six failures were not attributed
to Milestone 08A: two are the
pre-existing GaragePrototype lighting-profile expectation, two are donor world
cellization compatibility-material drift, one is the WorldRemaster PilotGate
expectation and one is donor world-transfer source-hash drift. They remain
visible rather than being hidden by the focused green suites.

The build log also contains non-fatal local Unity license/certificate and
ReportGenerator reflection warnings. They did not produce compiler errors or a
failed build. The capture completed and exited successfully, then the Unity
development player reported its generic shutdown leak-detector warning (8
persistent allocations) and debugger cleanup warning; this is retained in the
log and is not claimed as resolved by 08A. Physical controller traversal and
non-16:9 presentation still need manual device/viewport review and are not
claimed as passed.

Complete matrix: `Docs/UI/UI_TEST_MATRIX.md`.

Automated and capture evidence for the bounded correction is complete. Manual
pause blur/readability, physical controller traversal and non-16:9 presentation
remain pending.

## 9. Performance observations

Evidence comes from the native 1672x941 development capture player:

- initial construction of 12 routes: `93.193 ms` in the final D3D12 capture;
- individual review-route rebuilds observed: approximately `0.193-10.311 ms`;
- menu glass uses one full-resolution `1672 x 941` ARGBHalf Linear Gaussian
  result and has no recurring menu-camera render;
- HUD owns no camera capture, blur pass or recurring RenderTexture cadence;
- pause performs one `1024 x 576` capture and filters to `512 x 288` ARGBHalf:
  D3D12 CPU submission `969.897 ms`, `0` measured thread bytes;
- supplemental forced-DX11 one-shot pause submission: `596.251 ms`, `0`
  measured thread bytes; shader orientation and capture completion pass;
- render-texture vehicle preview is unavailable, therefore has zero recurring
  preview cost;
- HUD data refresh is revision-driven; frame sampling and input-device
  detection remain the only intentional per-frame UI observations;
- the development thread-allocation counter reported `0` bytes for the sampled
  build/rebuild sections, which is an instrumentation floor and not proof that
  the entire UI session has no GC allocations;
- route focus is assigned synchronously during route activation; physical
  controller-to-display latency was not profiled.

## 10. Comparison captures

All canonical files are under `Docs/UI/Review/08A/` at `1672 x 941`, 100% UI
scale and were updated on 2026-07-20 for the Gaussian correction:

| Evidence | State |
|---|---|
| six `*_Implementation.png` files | `PASS` |
| six `*_ReferenceOnly.png` files | `PASS` |
| six `*_Blended50.png` files | `PASS` |
| six `*_ReferencePending.png` files | `PASS`; separate from locked comparisons |
| supplemental DX11 implementation capture set | `PASS`; ignored output only |
| per-screen user verdict, including pause blur/readability | `PASS`; accepted by user on 2026-07-20 |

The Editor tool is available at
`Tools > My Summer Car > UI > Milestone 08A Reference Review`. It validates
manifest integrity, preserves aspect ratio and rejects crop/stretch.

## 11. Principal files changed/created

- `Assets/Game/UI/Runtime/`;
- `Assets/Game/UI/Presentation/Runtime/`;
- `Assets/Game/UI/Presentation/Runtime/UiGlassSurface.cs` and
  `GameUiRoot.Backdrop.cs`;
- `Assets/Game/UI/Presentation/Content/MainMenu/M08A_TemporaryGarageBackdrop.png`
  plus its provenance record;
- `Assets/Game/UI/Presentation/Content/Shaders/M08A_SeparableGaussianBlur.shader`;
- `Assets/Game/UI/Editor/`;
- `Assets/Game/Bootstrap/ProductionUiInstaller.cs`,
  `ProductionUiAuthoring.cs` and Bootstrap scene wiring;
- player/vehicle input, look-settings and UI-gate integration;
- `VehiclePrototypeChaseCamera.cs` scaled-time pause compatibility;
- focused tests under `Assets/Game/Tests/EditMode/UIRuntime/`,
  `Assets/Game/Tests/EditMode/UIEditor/` and
  `Assets/Game/Tests/PlayMode/UIPresentation/`;
- `Docs/UI/` specifications, matrices and canonical review evidence.

## 12. Manual art work remaining

- approved production font and fallback chain;
- final project-owned logo/icon art;
- authorised temporary Phase 1 legacy Satsuma presentation for the existing
  menu composition; newly authored production-remaster vehicle art remains
  Phase 2 work;
- live interior/trunk preview;
- final credits content;
- final authored UI sound set/mix and audible polish through the existing
  `IAudioBackend` route.

No approved-reference screenshot pixel is a runtime dependency.

## 13. Known deviations and limitations

- a temporary project-owned static garage plate with a generic compact vehicle
  stands in for the exact Phase 1 Satsuma presentation;
- profiles and money have no backing provider; needs bind through the explicitly
  composed `IPlayerNeedsService`;
- unsupported graphics/audio/input rows retain truthful disabled states;
- several accessibility/gameplay settings are persistence foundations only;
- the Windows/Unity font fallback and procedural icons remain bounded
  substitutes; 4x fractional-alpha rasterization fixes dominant edge aliasing
  but does not replace final typography art;
- menu blur is limited to masked glass; HUD uses shadowed scene-integrated
  graphics without panels/capture/blur; pause uses a one-shot Gaussian frozen frame with dark
  dim and still requires manual appearance/readability validation;
- the menu plate is deliberately outside accessible UI scale; gameplay remains
  prepared/dormant until the explicit session gate is activated;
- four diagnostic/reference-only settings blocks are intentionally omitted by
  direct user decision and settings actions use one shared transaction row;
- pause freezes project time, time scale, player/vehicle gates and the chase
  camera; backend-neutral audio duck/pause remains later integration work;
- widescreen variants and physical gamepad navigation need manual review;
- visual fidelity remains unapproved until the user reviews the canonical
  implementation and blended images.
- the 2026-09-02 context-action scale/readability still needs a canonical
  gameplay capture and user review; the focused automated contracts pass.

Detailed register: `Docs/UI/APPROVED_REFERENCE_DEVIATIONS.md`.

## 14. Compatibility impact and migrations

- route IDs, stable IDs, world-save boundaries and established gameplay
  interfaces are unchanged; the independent UI settings schema advances from
  v4 to v5 for horizontal FOV and camera far clip;
- one small project-owned `IGameplaySessionGate` boundary is added between the
  prepared production world and UI New Game flow; it extends rather than
  replaces streaming, player, weather or pause architecture;
- the production installer gained one explicit authored menu-texture reference,
  plus an explicit serialized project Gaussian-shader reference, wired
  deterministically through `ProductionUiInstaller` and `GameUiDependencies`;
  runtime performs no `Shader.Find`; test/headless composition retains a
  truthful tint-only fallback when no texture/camera is supplied;
- pause now covers all gates under the existing composition root and the
  existing vehicle chase camera honours scaled time; no player, interaction,
  vehicle simulation or world-streaming architecture was replaced;
- inactive input gates now preserve their authored pending enabled state across
  Main Menu -> New Game activation; the `IGameplayInputGate` API is unchanged;
- no world/data save migration is required. UI settings v1-v4 migrate
  automatically, with pre-v5 camera values initialized to the existing
  `120 degrees / 500 m` authored defaults. Existing control binding JSON remains
  compatible; only its commit timing is corrected to the shared Pending
  transaction.
- context-action capability extensions are optional/additive; established
  interaction interfaces and dispatch remain compatible. The canonical player
  Input System asset gains one `Player/AlternativeActions` action, defaulted to
  explicit left- and right-Alt bindings. The accepted Controls-screen geometry is unchanged;
  this bounded HUD task adds no settings row. No save migration is required.

## 15. Readiness and next gate

| Definition-of-done item | State |
|---|---|
| references catalogued/decomposed | `PASS` |
| Editor/development overlay and capture tooling | `PASS` |
| six locked screens implemented | `ImplementationComplete` |
| six unreferenced required screens implemented/captured | `ImplementationComplete / ReferencePending` |
| settings transaction/persistence | `PASS for bounded 08A adapters` |
| truthful capabilities | `PASS` |
| keyboard/mouse/gamepad architecture | `PASS automated foundation; manual physical-device review pending` |
| accessibility/localization foundations | `PASS for bounded foundation` |
| UI audio backend boundary | `PASS` |
| focused EditMode/PlayMode | `33/33 PASS`; `11/11 PASS` |
| 2026-07-20 authoring/private build/native D3D12 capture | `PASS` |
| supplemental native DX11 capture | `PASS` |
| corrected comparison files and ReferencePending captures | `PASS; updated 2026-07-20` |
| manual pause appearance/readability | `PASS`; user accepted final presentation on 2026-07-20 |
| user visual approval | `PASS`; `VisuallyApproved` on 2026-07-20 |
| 2026-09-02 context-action revision | `ImplementationComplete / FocusedTestsPassed / VisualReviewPending` |

The historical 2026-07-20 Milestone 08A composition remains
`ImplementationComplete / VisuallyApproved`. The later persistent-HUD,
Graphics-card and 2026-09-02 context-action corrections retain their explicitly
pending visual gates; they do not erase the historical evidence. Physical-device
and non-16:9 compatibility checks remain ordinary follow-up coverage. No later
milestone is started by this approval record.
