# Milestone 08A — approved component inventory

Status: the unchanged menu/settings screens remain `ImplementationComplete /
VisuallyApproved`; the `2026-08-05` HUD revision and `2026-08-15` Graphics
camera card are `TargetedPlayModePassed / VisualApprovalPending`

This inventory maps the locked visual slots to project-owned components and
their data authority. It is deliberately capability-aware: a visual row may be
required while its interaction remains disabled.

## Shared shell

| Component | Responsibility | Data/source |
|---|---|---|
| `UiReferenceFrame` | Canonical 1672×941 safe frame and aspect handling below accessible scale | presentation-only |
| `UiTheme` | Central colours, spacing, typography sizes, states and generated sprites | project-owned tokens |
| `UiLogo` | Rebuilt text/shape logo, never reference pixels | project-owned presentation |
| `UiGreetingCard` | Invariant upper-right settings/menu goodwill card aligned to the main action right edge | localization ID |
| `UiCategoryNav` | Reduced, invariant Graphics/Audio/Controls/Gameplay/Accessibility/Mods stack | route state + capabilities |
| `UiPanel`, `UiCard`, `UiRow` | Shared translucent surfaces | theme tokens |
| `UiFocusPresenter` | Mouse/keyboard/gamepad focus | EventSystem selection |
| `UiBottomActions` | Equal-size Apply/Reset/Cancel actions | settings Pending/Applied transaction |
| `UiBackdropModes`, `UiGlassSurface` | full-canvas `MenuStatic` outside `UiScale`, stable dark HUD surfaces without capture, one-shot `PauseFrozen` and aligned masked sampling | project-owned static menu plate plus explicit project camera |
| `M08A_SeparableGaussianBlur` | four separable H/V iterations at radii 2/4/6/8 for static menu glass and one-shot pause | explicit serialized project shader; no `Shader.Find` |
| `UiRoundedBorder` | supersampled 1.75 px ring with continuous rounded corners | project-owned procedural sprite |
| `UiToast` | transient notice whose complete root dismisses after timeout | route-independent presentation state |
| `IGameplaySessionGate` | prepared/dormant world -> active gameplay transition | explicit Bootstrap composition |

## Main menu

| Slot | Component | Authority |
|---|---|---|
| Primary actions | `MainMenuActionStack` | route controller and capabilities |
| Continue metadata | `SaveAvailabilityViewModel` | `ISaveService`; currently unavailable |
| Vehicle colour | `VehicleColourSelector` | settings/profile data; initial bounded local setting |
| Interior/trunk preview | `VehiclePreviewCard` | live/project-owned content; bounded placeholder if unavailable |
| Performance graph | `PerformancePreviewCard` | measured samples only; never concept numbers |
| Music import | `MusicImportCard` | capability state; unavailable until importer exists |
| Settings/Mods/Dev | `MainMenuUtilityStrip` | route/capability/development context |
| Version | `BuildVersionLabel` | `Application.version` |

## Settings controls

| Component | Use |
|---|---|
| `UiValueStepper` | Enumerated display/quality/policy values |
| `UiDropdown` | Resolution, refresh, language and supported selections |
| `UiSlider` | Supported normalized values with visible numeric value and centred circular `16 x 16` handle |
| `UiToggle` | Boolean values; label remains independent of colour |
| `UiDisabledRow` | Locked slot with explicit unavailable reason |
| `UiCapabilityBadge` | Optional `Unavailable`/`Development only` marker |
| `SettingsTransactionBar` | Equal-size Apply/Reset/Cancel over pending/applied snapshots |

## Graphics page

Supported now: display mode, resolution, refresh rate, VSync, the existing
Unity quality level, horizontal FOV and gameplay-camera far clip. The compact
Camera card uses the vacated right-side area and keeps the authored `120
degrees / 500 m` defaults. Far clip does not alter streaming, LOD or vegetation
policy. Motion blur and depth-of-field persist as user settings but are
`AdapterPending` until the production Volume owns their application.

Capability-disabled: DLSS/upscaler-specific quality, frame generation, ray
tracing and discrete texture/shadow/reflection/volumetric/vegetation/post-process
adapters. The Graphics performance-preview panel is intentionally absent after
the direct user correction; the main-menu performance card is unaffected.

## Audio page

Direct `IAudioBackend` mappings:

- Master → `Master01`;
- Engine → `Vehicle01`;
- Environment → `Ambience01`;
- effects/mechanical support → `Effects01`;
- Music → `Music01`;
- UI → `Ui01`;
- dynamic range, focus mute, subtitles, captions, reduced loud sounds and
  output ID → matching `AudioSettingsState` fields.

Voice, radio, weather, thunder, reverb profile and real endpoint enumeration
have no independent backend capability in M08 and remain disabled/unavailable
rather than silently sharing unrelated buses. The Audio test block is absent by
direct user correction; a compact backend/profile status may remain.

## Controls page

| Component | Authority |
|---|---|
| `InputBindingTable` | Input System action GUID + binding GUID |
| `InteractiveRebindRow` | Input System interactive rebind with cancel |
| `BindingConflictPresenter` | effective-path conflicts in active control group |
| `MouseSettingsCard` | look sensitivity/invert settings |
| `GamepadSettingsCard` | sensitivity/deadzone/vibration capability |
| Shared transaction row | pending binding overrides plus all other control settings |

The existing player and vehicle InputActionAssets are reused. A project-owned
System/Pause action is added; absent Inventory/Map/Journal/handbrake actions are
not fabricated. The former live input-preview diagnostic is intentionally
absent. Accepted rebinds remain pending until Apply.

## Gameplay, accessibility and mods

Implemented bounded settings: HUD mode, metric units, language selection over
available locales, fatigue/alcohol presentation intensity, contextual hints,
interaction outlines, camera shake and development UI visibility.

Reference slots with no service remain disabled where retained: autosave slots,
save confirmation and difficulty presets. Profile/immersion/summary cards are
absent by direct user correction. Accessibility is a
minimal `ReferencePending` page with UI scale, high contrast, reduced motion,
subtitles/captions and hold/toggle policy. Mods is a truthful
`ReferencePending / Not available in this build` page.

## HUD

| Component | Authority |
|---|---|
| `ClockHudView` | `IGameTimeService.Snapshot` |
| `MoneyHudView` | future money/save provider; unavailable now |
| `NeedsHudView` | `IPlayerNeedsService.Snapshot` when composed; truthful unavailable state otherwise |
| `NeedRowView` | icon, label, thin track/fill, amber indicator and percentage/unavailable state |
| `HudShadowPass` | shared one-pixel dark shadow on text, icons, tracks, fills and indicators |

The post-08A user correction uses no persistent HUD glass. The upper-left
vertical six-needs column and the upper-right clock/money stack are
scene-integrated graphics with a restrained shadow for bright-background
readability. They own no live camera capture,
RenderTexture refresh cadence or blur pass. The optional user-enabled FPS
counter remains separate from the default persistent composition, but uses the
same minimal shadowed treatment: no panel or line, pure-white numeric value
followed by an amber `FPS` suffix.

An Editor/development-only `UiReviewDataFixture` provides deterministic values
for visual comparison captures. It is visibly tagged in diagnostics and never
becomes the production data provider.

## Non-locked supporting UI

Boot/loading, pause, confirmation dialog, save-status notification,
Accessibility and Mods use the same theme with minimal layouts. They are marked
`ReferencePending`; their existence does not change any locked composition.
