/plan

# MILESTONE 08A — MAIN MENU, SETTINGS, HUD, AND UI FOUNDATION

Read `AGENTS.md` completely before doing anything.

Read:

- all reports through Milestone 08;
- player/interaction, vehicle, weather, audio, save, and input documentation;
- existing UI code and packages;
- accessibility and localization documentation when present;
- `References/UI/README.md`;
- UI concept images under `References/UI`;
- current Git status and diff.

## Objective

Implement a coherent modern UI foundation that remains recognizably connected
to My Summer Car while matching the remake's new visual direction.

Required prototype screens and systems:

- boot/loading flow;
- main menu;
- continue/new game/load entry points;
- pause menu;
- settings;
- controls/rebinding;
- accessibility;
- mods entry placeholder when supported by project scope;
- gameplay HUD;
- interaction prompts;
- survival/status panel hooks;
- vehicle HUD/telemetry presentation;
- inventory/tool quick bar prototype;
- notifications;
- save/load status;
- confirmation dialogs;
- controller/keyboard/mouse navigation.

The generated concept images are visual references, not final pixel-perfect
specifications and not authoritative for text content.

## Technology decision

Inspect existing project UI.

If a UI framework is already established and sound, preserve it.

If none exists, choose a first-party Unity solution suitable for Unity 6 and
the project's needs. Prefer a single coherent runtime UI approach unless a
documented reason justifies mixing systems.

Do not install a third-party UI framework silently.

Create an ADR documenting:

- chosen UI technology;
- why;
- input/navigation strategy;
- localization strategy;
- scaling strategy;
- testing strategy;
- limitations.

## Architecture

Create or align:

- `IUIScreenService`;
- `IUIRouteService`;
- `IUIDialogService`;
- `IUIInputModeService`;
- `IUIThemeService`;
- `IUISettingsBinding`;
- `UIScreenId`;
- `UIRoute`;
- `UIState`;
- `UITheme`;
- `UIAudioHooks`;
- `UIAccessibilitySettings`;
- `HUDViewModel`;
- `InteractionPromptViewModel`;
- `VehicleHudViewModel`;
- `PlayerStatusViewModel`;
- `NotificationQueue`;
- `LoadingScreenController`;
- `MenuFlowController`.

Keep UI view logic separate from gameplay services.

Do not make views search the scene for gameplay objects.

Do not let gameplay systems know concrete UI widgets.

## Visual language

Use the concept references as direction:

- dark translucent panels;
- restrained glass effect;
- warm amber/orange focus accent;
- white/gray primary typography;
- subtle industrial/automotive details;
- Finnish rural/mechanic identity;
- readable spacing;
- practical rather than sci-fi;
- modern but not sterile;
- limited animation;
- strong focus state.

Avoid:

- excessive blur;
- tiny text;
- unreadable low contrast;
- constant noisy motion;
- fake CRT effects;
- giant cinematic bars;
- excessive bloom;
- copying generated image text errors.

Create a reusable theme/token system for:

- colors;
- spacing;
- typography;
- corner radii;
- borders;
- panel opacity;
- focus/hover/pressed/disabled;
- warning/error/success;
- animation durations;
- icon sizes.

## Main menu

Prototype:

- Continue;
- New Game;
- Load;
- Settings;
- Mods placeholder only when supported;
- Credits;
- Quit;
- current profile/save summary;
- version/build label;
- background-scene integration;
- controller/keyboard/mouse navigation;
- confirmation dialogs.

Do not hard-code fake save data.

## Settings

Implement a settings architecture with apply/cancel/default behavior.

Categories:

### Graphics

- display mode;
- resolution;
- refresh rate;
- VSync;
- frame limit;
- HDR availability;
- render scale/upscaler hooks;
- quality preset;
- textures;
- shadows;
- reflections;
- volumetrics;
- vegetation;
- post-processing;
- motion blur;
- depth of field;
- weather quality;
- UI scale.

Do not expose unsupported features as working.

If DLSS/FSR/XeSS/frame generation/ray tracing are not integrated, represent
them only through capability-driven hooks or omit them.

### Audio

Bind to the audio configuration:

- master;
- vehicle;
- effects;
- ambience;
- music;
- UI;
- dynamic range;
- mute-on-focus-loss;
- captions/subtitles hooks.

### Controls

- action-map display;
- rebinding;
- conflict detection;
- reset;
- mouse sensitivity;
- invert axes;
- gamepad sensitivity;
- dead zones;
- steering/throttle/brake bindings;
- hold/toggle options.

Use Unity Input System APIs and existing input architecture.

### Gameplay

- units;
- interaction behavior;
- camera settings;
- HUD detail;
- tutorial/help options;
- autosave policy hooks;
- difficulty/design options only when implemented.

### Accessibility

- UI scale;
- text size;
- high contrast;
- color-independent status cues;
- subtitle/caption options;
- reduced motion;
- camera-shake reduction;
- hold/toggle alternatives;
- input remapping;
- warning-flash reduction;
- readable focus indicator.

## Gameplay HUD

Build a restrained, configurable HUD.

Support view models for:

- time/day;
- money;
- thirst;
- hunger;
- fatigue;
- stress;
- dirtiness;
- other project-approved needs;
- interaction prompt;
- task/progress prompt;
- held tool/item;
- quick bar;
- notifications;
- save status.

Do not show every panel permanently.

Allow context-based visibility and user-configurable detail.

## Vehicle HUD

Support:

- speed;
- RPM;
- gear;
- fuel;
- coolant/temperature;
- voltage/electrical state;
- warning indicators;
- surface/traction debug only in development;
- optional compact/full modes.

Respect the physical dashboard and avoid duplicating every real gauge unless
accessibility or gameplay settings request it.

## Loading and async flow

Provide:

- loading screen;
- progress source abstraction;
- minimum fake progress avoidance;
- error state;
- cancellation only when safe;
- scene/world-cell transition hooks;
- input lock;
- no blocking synchronous asset work on UI thread where avoidable.

## Localization readiness

Even if only one language is currently authored:

- do not hard-code user-facing strings across scripts;
- define string IDs/resources;
- support plural/format parameters;
- preserve Finnish names and labels correctly;
- support Russian/English later;
- document font/fallback needs.

Do not bundle unlicensed font files.

## Input and navigation

Validate:

- mouse;
- keyboard;
- gamepad;
- focus restoration;
- back/cancel;
- no focus traps;
- screen-reader hooks where feasible;
- safe pause behavior;
- input-mode switching;
- rebinding persistence.

## UI audio

Use audio-backend hooks for:

- navigate;
- confirm;
- cancel;
- error;
- notification;
- slider/toggle;
- pause/open/close.

Do not call Wwise directly from view widgets.

## Settings persistence

Create a versioned settings document separate from the world save where
appropriate.

Support:

- defaults;
- pending values;
- apply;
- cancel;
- capability validation;
- migration;
- command-line or safe-mode overrides where useful;
- reset category;
- reset all.

Do not overwrite gameplay saves to change graphics settings.

## Tests

Add tests for:

- route/navigation state;
- focus behavior;
- apply/cancel/default;
- settings serialization/migration;
- capability filtering;
- input rebinding conflict;
- UI scale;
- reduced-motion behavior;
- HUD view-model mapping;
- interaction prompt state;
- vehicle telemetry mapping;
- loading/error state;
- missing localization key reporting.

Add PlayMode smoke tests for:

- boot → main menu;
- main menu → settings → back;
- pause/resume;
- keyboard navigation;
- gamepad navigation;
- settings persistence;
- gameplay HUD;
- resolution/UI scale changes where testable.

## Performance

Measure:

- layout/rebuild cost;
- allocations;
- blur/background cost;
- HUD update frequency;
- world-space/UI overlap if used;
- menu background-scene cost;
- controller navigation latency.

Do not update every text field every frame when values did not change.

## Documentation

Create or update:

- `Docs/UI/UI_ARCHITECTURE.md`;
- `Docs/UI/UI_TECHNOLOGY_ADR.md`;
- `Docs/UI/VISUAL_LANGUAGE.md`;
- `Docs/UI/SCREEN_FLOW.md`;
- `Docs/UI/SETTINGS_SCHEMA.md`;
- `Docs/UI/HUD_SPEC.md`;
- `Docs/UI/ACCESSIBILITY_CHECKLIST.md`;
- `Docs/UI/LOCALIZATION_READINESS.md`;
- `Docs/UI/UI_TEST_MATRIX.md`;
- `Docs/Milestones/MILESTONE_08A_REPORT.md`.

## Non-goals

Do not implement:

- a complete mod browser;
- online profile services;
- store/monetization UI;
- final credits content;
- unsupported graphics features;
- unrelated gameplay systems.

## Definition of done

1. UI technology decision is documented.
2. Main menu and pause flow work.
3. Settings apply/cancel/default and persistence work.
4. Keyboard/mouse/gamepad navigation works.
5. HUD view models integrate with existing systems.
6. Accessibility foundations exist.
7. UI audio uses backend contracts.
8. Tests exist and run when possible.
9. UI remains readable and performant.
10. The implementation uses concepts as reference without baking screenshots
    into the UI.

## Final response

Report:

1. UI technology selected.
2. Architecture.
3. Screens implemented.
4. Settings implemented.
5. HUD implemented.
6. Input/accessibility/localization.
7. Tests.
8. Performance.
9. Files changed.
10. Manual art/font/icon work.
11. Known limitations.
12. Readiness for save hardening.

Stop after Milestone 08A.
