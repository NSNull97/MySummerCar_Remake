/plan

# MILESTONE 08A — MAIN MENU, SETTINGS, HUD, AND UI FOUNDATION

Read `AGENTS.md` completely before doing anything.

Read `Prompts/CURRENT_STATE.md` and `Prompts/PROJECT_DESIGN_GUARDRAILS.md`.

Read:

- all reports through Milestone 08, including the complete 07A–07C weather sequence;
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
- donor-faithful survival/status panel;
- vehicle HUD/telemetry presentation;
- held item/tool context indicator only when justified;
- notifications;
- save/load status;
- confirmation dialogs;
- controller/keyboard/mouse navigation.

The generated concept images are visual references, not final pixel-perfect
specifications and not authoritative for text content.

## Donor-faithful UI guardrails

The default UI must not simplify the game into a modern guided experience.

Do not add as baseline features:

- permanent minimap/GPS;
- route lines or world-space navigation markers;
- quest tracker with objectives/checklists;
- exact hidden survival percentages;
- RPG inventory grid;
- permanent tool hotbar/quick bar;
- item rarity/color coding;
- profile levels, XP, achievements, or trophy progress on the main menu;
- modern phone/app metaphors.

Development/debug UI may expose telemetry, numeric values, and quick-access
controls, but it must be clearly separated and disabled in normal play.

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
- restrained translucent panels with little or no expensive live blur;
- warm amber/orange focus accent with donor-like colored status categories;
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
- current save summary without levels/XP/achievement gamification;
- version/build label;
- lightweight background-scene integration using recognizable world/garage views;
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
- weather quality through project-owned settings/binding APIs, never direct Enviro widget access;
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
- optional help/manual visibility without waypoint guidance;
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

Build a restrained, configurable HUD inspired primarily by the approved compact
vertical needs-panel direction.

Default presentation:

- compact vertical needs/status panel;
- dark translucent background or optional panel-less variant;
- clear icons and bars;
- distinct but restrained category colors;
- no numeric percentages by default;
- no permanent task/objective tracker;
- no GPS/minimap;
- time/day and money shown in a compact donor-recognizable form;
- interaction prompt only while a valid interaction is targeted;
- notifications used sparingly;
- save/load status visible only when active;
- no permanent held-item inventory strip.

Support view models for:

- time/day;
- money;
- thirst;
- hunger;
- fatigue;
- stress;
- urine;
- dirtiness;
- alcohol only when the approved design exposes it;
- other project-approved needs;
- interaction prompt;
- held tool/item context where useful;
- short contextual warning/feedback;
- notifications;
- save status.

Use smooth bars and qualitative warning states. Exact values belong in DEV tools
or an explicitly enabled accessibility/debug option, not the default experience.

Allow:

- compact/full/immersive HUD visibility modes;
- panel opacity;
- UI scale;
- color-independent critical cues;
- optional labels/icons;
- critical-state pulse with reduced-motion alternative.

Do not show every panel permanently.

## Vehicle HUD

Support project-owned vehicle view models for:

- speed/RPM/gear only when the physical dashboard is unavailable, unreadable, or
  the player enables an accessibility/compact overlay;
- fuel, coolant/temperature, voltage/electrical warning hooks;
- warning indicators;
- surface/traction telemetry only in development;
- optional compact/full/accessibility modes.

The physical dashboard is primary. The default driving HUD must not duplicate
every real gauge or add a modern navigation panel.

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
- default HUD contains no numeric survival percentages;
- default HUD contains no GPS/quest tracker/inventory bar;
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
- `Docs/UI/DONOR_FAITHFUL_UI_GUARDRAILS.md`;
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
- permanent GPS/minimap/quest tracker;
- RPG inventory or mandatory quick bar;
- menu XP/levels/achievement gamification;
- unrelated gameplay systems.

## Definition of done

1. UI technology decision is documented.
2. Main menu and pause flow work.
3. Settings apply/cancel/default and persistence work.
4. Keyboard/mouse/gamepad navigation works.
5. HUD view models integrate without adding GPS, RPG inventory, or default numeric survival telemetry.
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
