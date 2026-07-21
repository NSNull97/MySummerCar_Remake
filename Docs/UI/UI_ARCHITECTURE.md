# Milestone 08A UI architecture

Status: `BoundedImplementationComplete / VisualReviewPending`  
Visual status: `VisuallyApproved` (user acceptance recorded 2026-07-20)
Date: `2026-07-20`

This document describes the project-owned UI implementation used for the
reference-locked Milestone 08A screens. It records the current code, not a
future framework proposal. Only the user may change a locked screen from
`ImplementationComplete` to `VisuallyApproved`.

## Technology and ownership

The runtime presentation uses first-party Unity uGUI and the Unity Input
System. The decision and rejected alternatives are recorded in
`Docs/UI/UI_TECHNOLOGY_ADR.md`.

| Assembly | Owns | Must not own/reference |
|---|---|---|
| `MSC.UI.Runtime` | route IDs, capability states, localization key contract, settings DTOs, validation, transactions and JSON persistence | uGUI, Wwise, Enviro, Bootstrap, approved PNGs |
| `MSC.UI.Presentation.Runtime` | `GameUiRoot`, uGUI construction, theme tokens, project-owned procedural icons, route presentation, HUD binding and development capture probe | Editor APIs in release code, donor hierarchy paths, reference PNG loading, direct Wwise calls |
| `MSC.UI.Editor` | reference manifest validation, overlay window, canonical normalization, comparison output and Bootstrap authoring validation | shipping/runtime dependencies |
| `MSC.Bootstrap.Runtime` | explicit dependency composition through `ProductionUiInstaller` | concrete widget behavior or reference-image handling |

The approved references remain under `References/UI/Approved/08A/`, outside
`Assets`. `runtimeUsageAllowed` is `false`; no runtime assembly reads those
files. The development capture probe produces implementation-only images and
also does not read reference pixels. Only `MSC.UI.Editor` combines captures for
review.

## Production composition

`Assets/Game/Bootstrap/Bootstrap.unity` remains the first enabled build scene.
`ProductionUiInstaller` is authored on the same composition-root object as
`ProductionWorldStreamingInstaller` and receives serialized references to:

- the production world installer;
- `M4_Player.inputactions`;
- `M06_Vehicle.inputactions`;
- the project-owned temporary 1672x941 menu garage plate;
- the project-owned separable Gaussian blur shader;
- the start-in-main-menu policy.

At runtime the installer creates one `GameUiRoot` and passes a
`GameUiDependencies` object containing world readiness, the explicit
`IGameplaySessionGate`, project game time, `IAudioBackend`, both Input System
assets, the full composition root, explicit menu/background camera references
and the settings path. The shader is an explicit serialized dependency passed
from `ProductionUiInstaller` through `GameUiDependencies`; runtime code does not
use `Shader.Find` and therefore does not depend on string lookup or shader-strip
heuristics. Runtime composition does not search for objects by name and does not
use a service locator. Test fixtures may omit the bitmap/camera and receive
readable tint-only fallbacks.

Initialization is ordered as follows:

1. Production world composition creates the player and initializes time/audio,
   but the main-menu startup policy leaves gameplay camera/environment
   activation dormant.
2. `ProductionUiInstaller.Awake` validates explicit references and uniqueness
   and binds the production `IGameplaySessionGate`.
3. `GameUiRoot.Initialize` loads settings and binding overrides.
4. The root builds one screen-space uGUI canvas, a full-canvas static backdrop,
   a canonical 1672x941 reference frame, the accessible scale root and the
   route objects. The backdrop is deliberately outside `UiScale`.
5. The loading route is shown until the world readiness delegate reports that
   the prepared world is ready.
6. The configured startup policy opens the main menu with gameplay still
   dormant, or explicitly activates the session before opening the HUD.
7. `New Game` calls the idempotent session gate and enters the HUD only after
   activation succeeds.

Failure is explicit: missing input assets, a duplicate UI root, a non-primary
composition root or incomplete production time/audio wiring throws during
startup instead of silently creating a partial UI.

## Route model

`UiRouteCatalog` is the stable route registry. The currently declared routes
are:

```text
Boot
Loading
MainMenu
SettingsGraphics
SettingsAudio
SettingsControls
SettingsGameplay
SettingsAccessibility
SettingsMods
Pause
ConfirmationDialog
SaveStatus
InGameHud
```

Main menu, the four locked settings pages and in-game HUD are marked as having
locked visual references. Accessibility, Mods, loading, pause, confirmation
and save status are `ReferencePending` by design. All six now have bounded
concrete screens: Quit uses `ConfirmationDialog`, while `SaveStatus` truthfully
reports that no slot/storage provider exists.

`GameUiRoot` owns current route state and enables exactly one route object at a
time. Each route remembers its last valid interactable `Selectable`; returning
from confirmation, save status or a rebuilt localized route restores that
selection. If the control disappeared or became disabled, focus falls back to
the first interactable item. Directional controller-style focus and the modal
restore paths are covered by focused PlayMode tests; physical-device traversal
remains a manual gate.

## Gameplay suspension and visibility

The UI crosses the gameplay boundary only through project-owned interfaces:

- `IGameplaySessionGate` separates a prepared world from an active gameplay
  session and is the sole Main Menu -> New Game activation boundary;
- `IGameplayInputGate` disables player and vehicle gameplay input;
- `IUiVisibilityGate` suppresses contextual/debug overlays while menus are up;
- `IGameTimeService.SetPaused` pauses the authoritative clock;
- `Time.timeScale` pauses PhysX/gameplay presentation;
- cursor visibility and lock mode follow the active UI mode.

The session gate is distinct from pause. Main-menu startup may finish world
streaming and composition while player camera/environment simulation remain
dormant; it does not briefly begin and then pause gameplay. The default
non-menu/headless world startup policy retains automatic activation for
compatibility.

The pause scan covers the complete composition root rather than only the Player
subtree, so sibling vehicle gates are also disabled. The previous state of every
input gate, the authoritative clock and `Time.timeScale` is restored when
gameplay resumes. The vehicle chase camera uses scaled delta time and therefore
freezes with simulation. The `System/Pause` Input System action is the route
entry point for pause/resume. Backend-neutral audio pause/ducking remains a later
mixing concern; UI sounds themselves are not globally suspended.

## Presentation construction

The bounded 08A screens are generated by project-owned code rather than donor
or screenshot-derived prefabs:

- `UiThemeTokens` is the styling source;
- `UiVisualAssets` creates rounded surfaces and simple original icons through
  4x coverage rasterization/downsampling rather than binary 24-32 px edges;
- `UiFactory` creates panels, text, buttons, sliders, toggles and focusable
  controls;
- `UiTextCatalog` resolves the current English/Russian presentation strings;
- `UiLocaleFormatter` owns bounded culture-aware number/date/plural formatting,
  while `UiFontResolver` accepts a Windows font only after validating the
  required Latin/Cyrillic glyph set;
- `GameUiRoot` and partial screen builders bind routes and live values.

The canonical UI frame uses `AspectRatioFitter.FitInParent`. The static menu
plate instead lives in a sibling full-canvas backdrop frame using
`AspectRatioFitter.EnvelopeParent`: it covers every viewport edge and crops
without stretching. It is outside the accessible `UiScale` root, so values such
as 85% cannot expose the dormant gameplay camera around the menu. `CanvasScaler`
uses 1672x941 with a 0.5 width/height match and the overlay Canvas is
pixel-perfect. User scale is applied only below the canonical UI frame so the
locked 100% layout remains measurable.

Backdrop ownership is explicit and route-aware:

- `MenuStatic` shows the sharp project-owned garage plate. Glass masks sample a
  one-time full-resolution `1672x941`, `ARGBHalf`, Linear Gaussian result. Four
  separable horizontal/vertical iterations use radii `2 / 4 / 6 / 8`. Their UV
  rectangles come from actual world corners relative to the unscaled backdrop
  frame, so slices stay aligned under aspect fitting while accessible UI scale
  changes.
- `HudLiveGlass` is retained as the route-mode name for compatibility, but it
  performs no live camera capture or blur. `ClockMoney` and `Needs` use stable
  dark translucent surfaces, avoiding the severe look-input stutter observed
  with recurring capture.
- `PauseFrozen` requests exactly one project-camera capture at `1024x576`, then
  writes a Gaussian `512x288`, `ARGBHalf` result behind the route and adds a
  0.74-alpha dark dim. It does not reuse or depend on a HUD capture.

The previous `1/2 -> 1/4 -> 1/8` downsample/upscale pyramid has been removed.
Gaussian filtering uses the project-owned shader at
`Assets/Game/UI/Presentation/Content/Shaders/M08A_SeparableGaussianBlur.shader`.
Pause render textures are retained only for the frozen pause context, released
on menu or shutdown, and have no recurring HUD cadence. Approved reference PNGs
remain entirely outside this path.

Rounded visual borders are project-owned supersampled ring sprites rather than
uGUI `Outline` effects clipped by rounded masks. The normal screen-space stroke
is 1.75 px, preserving continuous corners. Slider handles explicitly use centred
anchors and a `16 x 16` simple sprite, which keeps the white knob circular.
Transient notices own a complete toast root; expiry disables that root instead
of clearing only its label.

## Settings flow

Settings are independent of world saves:

`CreateSettingsShell` owns one invariant chrome geometry for all six settings
routes: `ProjectOwnedLogo`, category navigation, destructive Back and greeting.
Each route supplies only selected-category state plus central/right content.
This intentionally avoids a second persistent-chrome subsystem: route activation
is synchronous, while PlayMode geometry tests guarantee identical chrome bounds.
Shared spacing tokens enforce visible clearance between navigation, content
panels and transaction buttons. Main-menu actions and settings categories use
reduced, internally consistent bounds; the greeting shares the main action
stack's right edge.

```text
JSON load/migrate/validate
        |
        v
Applied snapshot <---- Apply ----- Pending snapshot
        ^                               |
        |                               +-- edit
        +---------- Cancel --------------+
Defaults snapshot -------- Reset ------->+
```

`UiSettingsTransactionService` returns detached snapshots so a view cannot
mutate applied state by retaining a DTO reference. Apply validates, updates
supported adapters, saves Input System override JSON and writes the document
atomically. Reset replaces Pending with Defaults without persistence. Cancel
and Back restore Pending from Applied. Control rebinding follows the same
transaction: accepted bindings remain pending and are not committed by the
rebind widget itself.

All settings pages use one equal-size `Apply / Reset / Cancel` action row. The
direct user correction removes non-essential diagnostic/reference blocks from
the live settings shell: Graphics performance preview, Audio test, Controls
input preview and Gameplay profile/immersion/summary. This does not remove the
underlying settings DTOs, capability truth or main-menu performance card.

Capability-sensitive rows use `UiCapabilitySet`. A required visual slot may
remain present while disabled, with a reason such as `Unavailable`,
`AdapterPending`, `DevelopmentOnly` or `ReferencePending`. This is the sole
approved behavior for unsupported save, graphics, audio, input and profile
features.

## Audio boundary

Audio settings are applied through `IAudioBackend.ApplySettings`; widgets do
not reference Wwise. `GameUiRoot` posts the project-owned UI Navigate and UI
Confirm event IDs through `IAudioBackend.PostEvent`. `UiFactory` invokes the
confirm hook only for interactive controls with an action, while selection
changes are observed centrally for navigation feedback. Availability and
audibility still require final backend/PlayMode validation; no concrete Wwise
type enters the UI assemblies.

## HUD data boundary

The production HUD currently consumes only `IGameTimeService.Snapshot`.
No money or player-needs provider exists, so those slots display unavailable
values rather than fabricated state. Editor/development review mode supplies a
deterministic fixture solely for comparison captures. It is not a production
provider and cannot enter a release build flow.

See `Docs/UI/HUD_SPEC.md` for the complete persistent-category contract.

## Review tooling boundary

Open the Editor tool from:

`Tools > My Summer Car > UI > Milestone 08A Reference Review`

It validates all six manifest records, supports reference-only,
implementation-only and blended modes, adjustable opacity, safe-area and 0.1
normalized guides, and writes canonical review files to
`Docs/UI/Review/08A/`. A non-16:9 implementation capture is rejected rather
than cropped or stretched.

The development-only command-line probe accepts:

```text
-msc-ui08a-capture <output-directory>
```

It requests a 1672x941 window and captures the six locked implementation
routes plus the six separate `ReferencePending` routes. The Editor tool then
creates reference-only and 50% blended evidence for the locked set.

## Known bounded gaps

- The 2026-07-20 Gaussian correction passes focused authoring, EditMode,
  PlayMode, private build and native capture gates. Manual visual validation of
  pause blur/readability remains pending.
- Save slots/latest-save metadata, money and needs have no provider.
- High contrast, reduced motion, toggle/hold and most gameplay settings are
  persisted foundations without gameplay/presentation adapters.
- Mouse/gamepad sensitivity and invert-Y apply through
  `IPlayerLookSettingsSink`; gamepad dead-zone applies through Input System
  settings. Vibration still needs a capability adapter.
- UI navigation/confirm events are routed through `IAudioBackend`, but audible
  behavior still needs final runtime validation.
- Focused EditMode/PlayMode, Bootstrap validation, native capture and the
  private Windows development build pass. Physical gamepad traversal and
  non-16:9 viewport review remain manual gaps.
- The user reviewed the generated comparison set on 2026-07-20; all six locked
  screens are now `VisuallyApproved`.
- The project-owned menu plate approximates the garage/open-hood composition but
  deliberately uses an original generic car. It remains temporary until the
  authorized Phase 1 legacy Satsuma presentation is integrated.
- The native one-shot pause capture/blur recorded zero thread allocation. Its
  CPU submission is intentionally paid only when entering pause; the user
  accepted the resulting pause blur/readability on 2026-07-20. HUD owns no
  camera-capture cost.
