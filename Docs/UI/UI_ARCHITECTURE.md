# Milestone 08A UI architecture

The direct 2026-09-04 MainMenu revision is documented in
`MAIN_MENU_REDESIGN_2026-09-04.md`: bounded glass widgets, two-page paint palette,
settings schema 7 preference persistence, explicit input navigation and a
centred safe workspace. This supersedes the historical main-menu geometry and
performance-card descriptions below. The user accepted the real-car canonical
spawn/orbit revision, then requested the final lamp/rear-fog amendment and the
same glass style for Pause/Settings. The latter retains existing geometry and
behavior; see `MENU_STYLE_UNIFICATION_2026-09-04.md`. HUD and service boundaries
remain established dependencies. These latest amendments await visual review.
The final combined revision passes 54 EditMode and 40 PlayMode checks. Its
`MainMenuLightingTime` calculation reads local system time through an optional
presentation-only provider; it never changes `IGameTimeService`. The isolated
preview checks the clock once a second and refreshes only on a new minute,
visibility return or existing camera/paint changes. Settings/Pause reuse the
main menu's opt-in glass widgets and existing cached/frozen blur owner.

Status: `BoundedImplementationComplete / ContextActionRevisionImplemented /
VisualReviewPending`
Visual status: the historical four settings layouts retain their approval.
Their new glass styling and Pause styling are `VisualApprovalPending`, as is
the latest lamp/rear-fog amendment of the accepted MainMenu. The `2026-08-05`
HUD and `2026-09-02` context-action revisions remain `VisualApprovalPending`.
Date: `2026-09-04`

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
- explicit mesh-only Satsuma and home-yard menu prefabs;
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

1. Production composition creates the inactive player and initializes the
   service boundaries needed by the front end. Under the main-menu startup
   policy it does not bind a streaming focus or load additive gameplay scenes.
2. `ProductionUiInstaller.Awake` validates explicit references and uniqueness
   and binds the production `IGameplaySessionGate`.
3. `GameUiRoot.Initialize` loads settings and binding overrides.
4. The root builds one screen-space uGUI canvas, a full-canvas static backdrop,
   a canonical 1672x941 reference frame, the accessible scale root and the
   route objects. The backdrop is deliberately outside `UiScale`.
5. The loading route covers front-end initialization. Main-menu startup does
   not wait for gameplay-world readiness; start-in-game/save restoration still
   does.
6. The configured startup policy opens the lightweight main menu with gameplay
   scenes unloaded, or prepares and activates a session before opening the HUD.
7. `New Game` requests asynchronous gameplay preparation, which binds the
   streaming focus and loads the required world scenes exactly once, then calls
   the idempotent activation gate and enters the HUD.

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

- `IGameplaySessionPreparationGate` separates lightweight front-end boot from
  asynchronous gameplay-world preparation;
- `IGameplaySessionGate` separates a prepared world from an active gameplay
  session and remains the sole activation boundary;
- `IGameplayInputGate` disables player and vehicle gameplay input;
- `IUiVisibilityGate` suppresses contextual/debug overlays while menus are up;
- `IGameTimeService.SetPaused` pauses the authoritative clock;
- `Time.timeScale` pauses PhysX/gameplay presentation;
- cursor visibility and lock mode follow the active UI mode.

The session gates are distinct from pause. Main-menu startup leaves additive
gameplay scenes unloaded and the streaming focus unbound; it does not briefly
start, unload and restart gameplay. The default non-menu/headless world startup
policy retains automatic preparation and activation for compatibility.

The pause scan covers the complete composition root rather than only the Player
subtree, so sibling vehicle gates are also disabled. The previous state of every
input gate, the authoritative clock and `Time.timeScale` is restored when
gameplay resumes. The vehicle chase camera uses scaled delta time and therefore
freezes with simulation. The `System/Pause` Input System action is the route
entry point for pause/resume. Backend-neutral audio pause/ducking remains a later
mixing concern; UI sounds themselves are not globally suspended.

For a gate under an inactive hierarchy behind the lightweight main menu,
`IsGameplayInputEnabled` cannot describe its pending activation state because
its action map is correctly inactive. Suspension therefore records the authored
`MonoBehaviour.enabled` value for that inactive gate. After New Game activates
the player hierarchy, restoration enables a router that was meant to start
enabled while preserving an intentionally disabled component. Active player and
vehicle gates continue to use their live `IsGameplayInputEnabled` state.

## Presentation construction

The bounded 08A screens are generated by project-owned code rather than donor
or screenshot-derived prefabs:

- `UiThemeTokens` is the styling source;
- `UiVisualAssets` creates rounded surfaces, loads the licensed Lucide needs
  icons and retains 4x procedural icon generation as a bounded fallback;
- `UiFactory` creates panels, text, buttons, sliders, toggles and focusable
  controls;
- `UiTextCatalog` resolves the current English/Russian presentation strings;
- `UiLocaleFormatter` owns bounded culture-aware number/date/plural formatting,
  while `UiFontResolver` prefers the bundled user-supplied Helvetica Neue Roman font and
  validates required Latin/Cyrillic glyphs before using any Windows fallback;
- `GameUiRoot` and partial screen builders bind routes and live values.

The overlay `CanvasScaler` uses the `1672 x 941` reference resolution with
`ScreenMatchMode.Expand`. A fixed, centred `1672 x 941` logical safe frame owns
every menu, settings, pause and HUD route, so narrower viewports add vertical
space and wider viewports add horizontal space without changing route geometry
or pushing edge-aligned content off-screen. The menu backdrop lives in a
sibling full-canvas backdrop frame using
`AspectRatioFitter.EnvelopeParent`: it covers every viewport edge and crops
without stretching. It is outside the accessible `UiScale` root, so values such
as 85% cannot expose the dormant gameplay camera around the menu. The overlay
Canvas remains pixel-perfect. User scale is applied only below the canonical UI
frame so the locked 100% layout remains measurable.

Backdrop ownership is explicit and route-aware:

- `MenuStatic` is the retained route-mode name. Production composes an explicit
  mesh-only home yard and Satsuma through
  `MainMenuVehiclePreview`. One isolated HDRP camera renders initial/paint/orbit
  changes to a cached opaque texture; it never renders continuously at idle.
  `MainMenuOrbitDrag` receives free-background uGUI pointer events. Bounded
  yaw/pitch moves only the preview camera and car fill light; double-click resets
  the view. Panels, routes, modal windows and focus loss cancel pointer ownership.
  Settings retain the cached background with the preview hierarchy hidden.
  Fixtures may omit the models and use their supplied static plate. Production
  clears the historical photographic plate/shader references. The rendered
  environment uses actual viewport aspect with capped internal resolution;
  glass masks share a matching `ARGBHalf`, Linear Gaussian result, refreshed only when
  the menu preview signals a new frame. Four
  separable horizontal/vertical iterations use radii `2 / 4 / 6 / 8`. Their UV
  rectangles come from actual world corners relative to the unscaled backdrop
  frame, so slices stay aligned under aspect fitting while accessible UI scale
  changes.
- `HudLiveGlass` is retained as the route-mode name for compatibility, but it
  performs no live camera capture or blur. The name is now historical:
  `Clock`, `Money` and the vertical `Needs` root use no glass surface at all.
  Project-owned shadowed graphics provide contrast without reintroducing the
  severe look-input stutter observed with recurring capture.
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

Gameplay-camera settings cross the project-owned
`IPlayerCameraSettingsSink` boundary. The UI supplies validated horizontal FOV
and far-clip metres without referencing `MSC.Player`; the first-person camera
converts horizontal FOV to the current aspect ratio and remains the sole owner
of zoom transitions. Far clip does not claim ownership of streaming or LOD
policy.

All settings pages use one equal-size `Apply / Reset / Cancel` action row. The
direct user correction removes non-essential diagnostic/reference blocks from
the live settings shell: Graphics performance preview, Audio test, Controls
input preview and Gameplay profile/immersion/summary. This does not remove the
underlying settings DTOs, capability truth or main-menu performance card.
The later user-requested Graphics camera card occupies a small part of the
vacated right-side area without moving the locked primary Graphics rows.

Anti-aliasing settings cross a similar bounded adapter boundary. The UI runtime
persists project-owned `UiAntiAliasingMode`/`UiAntiAliasingPreset` values and
does not reference HDRP or NVIDIA types. `HdrpDlssRuntimeAdapter` maps the
validated schema-v6 snapshot into the central `AntiAliasingController` with
controller-side PlayerPrefs persistence disabled. The existing Graphics panel
hosts the live mode, preset, sharpening and supported-only DLSS-quality rows;
the shared transaction and action bar are unchanged.

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

The production HUD consumes `IGameTimeService.Snapshot` and, when explicitly
composed, `IPlayerNeedsService.Snapshot`. Money still has no provider and shows
an unavailable value rather than fabricated state; needs do the same when the
service is absent. Editor/development review mode supplies a deterministic
fixture solely for comparison captures. It is not a production provider and
cannot enter a release build flow.

Context actions remain on the existing `MSC.Player` presentation boundary.
`InteractionTargetHost` exposes capability interfaces, and
`PlayerInteractionController` composes a fixed-capacity
`InteractionActionSnapshot` from the same checks used by input dispatch. The
snapshot owns only a reticle enum plus up to three binding/action pairs. A
separate read-only `CurrentDisplayName` and
`CurrentDisplayLocalizationKey` expose explicit
`IInteractionDisplayTarget`/`IInteractionLocalizationTarget` metadata;
hierarchy names and renderer bounds do not cross into presentation. Assembly
part metadata is state-aware: loose parts expose their localized title, while
installed parts clear it and leave only capabilities whose live availability
checks pass. Fastener targets intentionally expose directional tool capability
without display-name metadata. This makes a fixed installed control silent and
still lets an installed hinged door advertise open/close.

`CrossdotPresenter` preserves its established player-prefab type and
`IUiVisibilityGate`, but the old projected target-title blocks are gone. Its
bounded IMGUI renderer draws a centre dot/open-palm/check/removal-cross, a
lower-left canonical safe-frame action stack and a dynamic bottom-centre
target/subtitle stack. Binding keycaps have rounded outlines; semantic mouse
variants show the effective left/right/middle/scroll control. The renderer
caches formatted binding text and loads only explicitly serialized icon
textures. `PlayerInputRouter` caches preferred keyboard/mouse binding labels and
glyph categories, then increments a revision after binding changes.
`InteractionUiTextCatalog` is the bounded bilingual presentation adapter for
target names, action copy, keycaps and common feedback. `GameUiRoot` applies the
persisted gameplay locale through `IGameplayLocaleSettingsSink`, so interaction
copy and player-voice subtitles switch with the same settings transaction as the
rest of the UI. Target and subtitle rows share the loaded font and `18 px` size;
bold versus regular weight supplies the hierarchy.
Holding `Player/AlternativeActions` asks the snapshot to replace its ordinary
rows with H/M/N actions while preserving the reticle; no Alt discoverability
widget is created and gesture dispatch remains unchanged.

Optional directional capability extensions expose read-only positive/negative
availability and labels for selected-tool and ordinary incremental targets.
This keeps wheel hints endpoint-aware without invoking a mutating operation as
a UI probe. Existing non-directional capabilities retain their compatibility
fallback.

NPC/traffic fallback subtitles continue through the explicitly composed
`FirstPersonLifeActionPresenter`. It owns subtitle timing and exposes the active
copy through `IPlayerSubtitleSource`; `CrossdotPresenter` owns the combined
target/subtitle layout. The presenter retains its old frame only as a fallback
when no context HUD claims the source. Both implement `IUiVisibilityGate`, so
the central UI root can suppress them without referencing NPC or traffic types.

The pickup reticle is the hash-locked donor `gui_uset` texture generated under
the ignored private runtime baseline by `Phase1InteractionUiImporter`. Its
tracked manifest, deterministic GUID, source hash and Phase 2 replacement key
keep the temporary presentation auditable without making donor pixels a Git or
gameplay dependency.

See `Docs/UI/HUD_SPEC.md` for the complete persistent-category contract.

## Developer-menu boundary

The 2026-08-05 developer menu is an independent Editor/Development Build
overlay, not an additional `UiRouteId` and not a redesign of any locked 08A
screen. `ProductionWorldStreamingInstaller` owns and initializes the retained
`ProductionDeveloperConsole` compatibility type, while `ProductionUiInstaller`
passes its `Open` action into the optional `GameUiDependencies` callback. The
main-menu Developer Tools slot invokes that callback without learning about
needs, weather, time, teleport coordinates, or the concrete overlay.

The overlay borrows centralized 08A colors and spacing but owns its transient
IMGUI textures and destroys them with the component. Domain mutations remain
behind explicit project-owned services and development-only APIs. See
`Docs/UI/DEVELOPER_MENU.md` for behavior and validation.

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
- Money has no provider; needs use the explicitly composed
  `IPlayerNeedsService` and retain a truthful unavailable fallback.
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
- The 2026-09-02 context-action renderer and new icon scale still require a
  canonical gameplay capture and user visual approval. Related EditMode tests
  pass `88/88`; isolated Alt input passes `1/1`; the broader player-flow class
  is `8/9` because of an existing carry-spring tolerance failure outside UI.
- The installed-part title/action correction passes vehicle assembly `27/27`,
  player interaction `49/49` and donor BoltCheck parity `5/5`. A manual Game
  View check of representative fixed and hinged installed parts remains open;
  the installed Satsuma hinge PlayMode contract passes `1/1`.
