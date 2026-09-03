# UI settings schema

Status: `SchemaV6Implemented / AntiAliasingAdapterImplemented /
CameraAdapterImplemented / TargetedTestsPassed / VisualApprovalPending`

Settings are versioned independently from world saves. The production path is:

```text
Application.persistentDataPath/Settings/ui-settings.json
```

The current schema is `6` and is represented by `UiSettingsDocument`.

## Transaction contract

`UiSettingsTransactionService` owns detached Defaults, Applied and Pending
snapshots.

- Views edit Pending only.
- Apply validates Pending, replaces Applied, applies supported adapters and
  writes JSON.
- Reset replaces Pending with Defaults; it does not persist until Apply.
- Cancel/Back replaces Pending with Applied.
- Invalid edits fail before mutating the stored pending snapshot.
- Accepted Input System rebinds update Pending override JSON only. Rebind UI
  never bypasses the transaction by committing directly.

Every live settings page exposes the same equal-size action row in this order:
`Apply`, `Reset`, `Cancel`.

## Persistence and recovery

`UiSettingsJsonStore` writes UTF-8 JSON through `<path>.tmp`, flushes to disk and
uses `File.Replace` with `<path>.bak` when supported. A copy fallback exists for
platforms where replace is unavailable. Temporary files are cleaned in a
`finally` block.

Load outcomes are explicit:

| Status | Meaning |
|---|---|
| `Loaded` | valid current schema loaded |
| `MissingCreatedDefaults` | no file existed; validated defaults were created |
| `MigratedAndSaved` | an older supported schema was migrated to v6 and rewritten |
| `CorruptQuarantinedDefaultsCreated` | invalid file moved to `.corrupt.<UTC timestamp>` and defaults created |

Unknown schema versions are not guessed. Sequential compatibility covers
schemas v1-v5. Older binding, accessibility, FPS-counter and DLSS additions are
preserved. The v4-to-v5 step initializes camera FOV and far clip to the authored
`120 degrees / 500 m` defaults. The v5-to-v6 step maps an enabled legacy DLSS
selection to `TemporalUpscaler / Custom`, maps legacy DLAA to
`MaximumQuality / Custom`, and otherwise selects the authored High/TAA default.

## Graphics

| Field | Default | Validation | Runtime adapter/status |
|---|---:|---|---|
| `DisplayMode` | `FullscreenWindow` | defined enum | `Screen.SetResolution` |
| `ResolutionWidth` | `1920` | 640-16384 | `Screen.SetResolution` |
| `ResolutionHeight` | `1080` | 480-8640 | `Screen.SetResolution` |
| `RefreshRateNumerator` | `60` | 1-1,000,000 | `RefreshRate` |
| `RefreshRateDenominator` | `1` | 1-10,000 | `RefreshRate` |
| `VSync` | `true` | boolean | `QualitySettings.vSyncCount` |
| `QualityLevel` | `2` | 0-64, then available-name check | Unity quality level |
| `MotionBlur` | `false` | boolean | persisted; production Volume adapter pending |
| `DepthOfField` | `false` | boolean | persisted; production Volume adapter pending |
| `DlssEnabled` | `false` | boolean | compatibility mirror synchronized from the unified AA mode |
| `DlssQuality` | `Balanced` | defined enum | quality passed to the central controller for DLSS |
| `AntiAliasingMode` | `Taa` | defined enum | `AntiAliasingController`; Off/FXAA/SMAA/TAA/DLSS/Maximum |
| `AntiAliasingPreset` | `High` | defined enum | central Low/Medium/High/Ultra profile; Custom for direct edits |
| `AntiAliasingSharpening` | `0.28` | finite 0-0.75 | central controller; ignored where the active upscaler owns sharpening |
| `HorizontalFieldOfViewDegrees` | `120` | 60-140 degrees | `IPlayerCameraSettingsSink`; aspect-correct horizontal FOV |
| `CameraFarClipMeters` | `500` | 100-5000 m | `IPlayerCameraSettingsSink`; gameplay-camera far clip |

The Graphics route exposes AA mode, AA preset and sharpening as transactional
controls. Changing mode or sharpening selects `Custom`; choosing an authored
preset restores its mode and sharpening together. DLSS quality is interactive
only when the hardware/pipeline capability check succeeds, and unsupported
machines cannot cycle into DLSS or Maximum Quality from the menu. Apply updates
the runtime controller and the same versioned JSON document without writing a
second PlayerPrefs copy.

Frame generation, ray tracing and granular texture/shadow/reflection/
volumetric/vegetation controls remain disabled when no adapter exists. The
right-side Graphics performance preview remains removed. Its former space now
contains a compact functional camera card requested by the user; this does not
restore average FPS, 1% low or VRAM diagnostics.

`CameraFarClipMeters` deliberately controls only the gameplay camera's far clip.
It does not silently alter world-streaming radii, LOD groups, terrain detail or
vegetation culling, which remain owned by their respective systems.

## Audio

| Field | Default | Validation/application |
|---|---:|---|
| `Master01` | `1.0` | normalized; backend Master |
| `Engine01` | `1.0` | normalized; backend Vehicle |
| `Environment01` | `1.0` | normalized; backend Ambience |
| `Effects01` | `1.0` | normalized; backend Effects |
| `Music01` | `0.65` | normalized; backend Music |
| `Ui01` | `0.8` | normalized; backend UI |
| `DynamicRange` | `Standard` | defined enum; backend dynamic range |
| `MuteWhenUnfocused` | `false` | backend setting |
| `SubtitlesEnabled` | `false` | backend hook |
| `CaptionsEnabled` | `false` | backend hook |
| `ReducedLoudSounds` | `false` | backend hook |
| `OutputDeviceId` | `system.default` | non-empty identifier, max 256 chars |

The presentation calls only `IAudioBackend.ApplySettings`. Voice, radio,
weather, thunder, independent reverb, endpoint enumeration and a guaranteed
test event are unavailable because the backend does not expose independent 08A
capabilities for those locked rows. The live Audio test block was removed by
direct user correction; the compact backend/profile state remains truthful
informational text.

## Controls

| Field | Default | Validation/status |
|---|---:|---|
| `MouseSensitivity` | `1.0` | 0.05-10; `IPlayerLookSettingsSink` |
| `InvertMouseY` | `false` | `IPlayerLookSettingsSink` |
| `GamepadSensitivity` | `1.0` | 0.05-10; `IPlayerLookSettingsSink` |
| `InvertGamepadY` | `false` | `IPlayerLookSettingsSink` |
| `GamepadDeadzone` | `0.125` | 0-0.95; Input System default dead-zone minimum |
| `VibrationEnabled` | `true` | capability/adapter pending |
| `PlayerBindingOverridesJson` | empty | empty or bounded JSON object/array, max 1 MiB |
| `VehicleBindingOverridesJson` | empty | empty or bounded JSON object/array, max 1 MiB |

Binding overrides use Input System serialization and are loaded into the two
explicit InputActionAssets. Rebinding must keep stable action/binding IDs,
allow cancel and reject effective-path conflicts. Accepted overrides remain in
Pending until the shared Apply action; Reset and Cancel affect them exactly as
they affect every other setting. The actual assets provide
Player movement/look/crouch/interaction/carry/tool actions, System/Pause and
Vehicle throttle/brake/clutch/steering/ignition/starter/gears/reset. Inventory,
Map, Journal and Handbrake do not exist and must remain unavailable. The
right-side live input-preview diagnostic was removed by direct user correction.

## Gameplay

| Field | Default | Validation/application |
|---|---:|---|
| `HudMode` | `Full` | defined enum; HUD behavior adapter pending |
| `Units` | `Metric` | defined enum; consuming-system adapters pending |
| `LanguageId` | `ru-RU` | non-empty identifier, max 64 chars; presentation locale applied |
| `FatigueVisualIntensity01` | `1.0` | normalized; gameplay adapter pending |
| `AlcoholVisualIntensity01` | `1.0` | normalized; gameplay adapter pending |
| `ContextualHints` | `true` | consuming UI adapter pending |
| `InteractionOutlines` | `true` | interaction presentation adapter pending |
| `CameraShakeIntensity01` | `0.75` | normalized; camera adapter pending |
| `DevelopmentUiVisible` | `false` | development-only policy adapter pending |
| `ShowFpsCounter` | `true` | toggles the optional minimal HUD FPS readout |

Autosave slots, save confirmation, difficulty presets and profile management
have no backing service and are not persisted as functioning options.
Within 08A, `LanguageId` is the only interactive row in this gameplay group.
HUD mode, units, fatigue/alcohol presentation, hints, outlines, camera shake and
development-UI visibility remain visible for locked-layout fidelity but are
disabled and labelled `Adapter Pending` until consuming systems exist.
Gameplay profile, immersion-level and summary cards were removed from the live
route by direct user correction; no DTO or service capability is inferred from
their former reference slots.

## Accessibility

| Field | Default | Validation/application |
|---|---:|---|
| `UiScale` | `1.0` | persisted range 0.75-1.5; bounded presentation clamp 0.85-1.25 |
| `HighContrast` | `false` | persisted; token-variant adapter pending |
| `ReducedMotion` | `false` | persisted; bounded UI already mostly static |
| `ToggleHoldActions` | `false` | persisted; gameplay input adapter pending |
| `ColorIndependentCues` | `true` | persisted; HUD labels/numerics already redundant |

The validation/application scale difference is intentional in the bounded
screen implementation to avoid clipping, but it needs viewport accessibility
testing before the wider persisted range can be fully honored.
UI Scale is the only interactive accessibility-specific control in 08A.
High contrast, reduced motion, toggle/hold and colour-cue rows are disabled and
explicitly labelled `Adapter Pending`; existing schema fields are migration
foundations, not claims that those behaviours currently work. Subtitles and
captions remain interactive because they are applied through `IAudioBackend`.

## Capability policy

The settings UI must never infer support from a reference image. Each feature
uses a capability state:

- `Supported`;
- `DevelopmentOnly`;
- `AdapterPending`;
- `ReferencePending`;
- `Unavailable`.

Only Supported and DevelopmentOnly states are interactive. A disabled row keeps
its corrected live slot and communicates the reason in text. The four
diagnostic groups removed by direct user correction are not reintroduced as
disabled placeholders.
