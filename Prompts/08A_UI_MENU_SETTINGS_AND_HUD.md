/plan

# MILESTONE 08A — REFERENCE-LOCKED MAIN MENU, SETTINGS, HUD, AND UI FOUNDATION

Read `AGENTS.md` completely before doing anything.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- all milestone reports through Milestone 08, including 07A–07C;
- player, interaction, vehicle, weather, audio, save, input, accessibility,
  localization, and settings documentation;
- all existing UI code, prefabs/documents, packages, tests, and current Git diff;
- `References/UI/Approved/08A/REFERENCE_README.md`;
- `References/UI/Approved/08A/REFERENCE_MANIFEST.json`;
- every PNG under `References/UI/Approved/08A/`.

If any approved reference is missing, unreadable, or does not match the manifest
hash, stop and report the exact problem. Do not substitute another concept image.

---

## 1. Objective

Implement the remake's UI foundation and the following screens so that their
visual composition matches the approved reference images as closely as practical:

1. Main menu;
2. Graphics settings;
3. Audio settings;
4. Controls settings;
5. Gameplay settings;
6. Default in-game HUD.

This is a **fidelity implementation milestone**, not a free UI redesign.

The approved screenshots are no longer loose mood references. They are the
user-approved target for:

- screen composition;
- panel placement and proportions;
- navigation placement;
- spacing rhythm;
- hierarchy;
- color logic;
- component density;
- typography character;
- icon treatment;
- background treatment;
- selected/hover/focus states;
- default HUD contents.

When a choice exists between a visually cleaner invention and closer reference
fidelity, choose the reference.

Do not reinterpret the layout merely because another arrangement is more common
in modern games.

---

## 2. Source-of-truth hierarchy

Use this order when resolving conflicts:

1. The six approved PNG references in `References/UI/Approved/08A/` for visual
   layout and composition;
2. Explicit user-approved UI rules in this milestone;
3. Existing project architecture, actual supported capabilities, and correct
   gameplay semantics;
4. Original My Summer Car functionality where the references preserve it;
5. Existing generic UI guidance.

The approved references supersede older generated UI/HUD concepts and the old
statement that concepts are only loose inspiration.

The references do **not** authorize copying their pixels into the game. Rebuild
all UI with project-owned widgets, icons, materials, text, layout, and live scene
content.

Do not reproduce obvious AI image artifacts, misspellings, impossible values, or
incorrect control names. Preserve the intended function and exact visual slot,
but use correct project terminology. For example, use `Map`, not an accidental
`Mop` label.

---

## 3. Approved reference mapping

The following files are authoritative:

- `01_MAIN_MENU_APPROVED.png` — main menu;
- `02_GRAPHICS_SETTINGS_APPROVED.png` — graphics page;
- `03_AUDIO_SETTINGS_APPROVED.png` — audio page;
- `04_CONTROLS_SETTINGS_APPROVED.png` — controls page;
- `05_GAMEPLAY_SETTINGS_APPROVED.png` — gameplay page;
- `06_INGAME_HUD_APPROVED.png` — default gameplay HUD.

The canonical comparison viewport is the native reference size listed in the
manifest: **1672 × 941**, 16:9. The implementation must also scale correctly to
other resolutions and aspect ratios, but 16:9 at 100% UI scale is the visual
fidelity target.

The approved HUD reference in this directory is authoritative even if earlier
concepts or conversation notes placed its blocks differently.

---

## 4. Mandatory pre-implementation decomposition

Before implementing or changing UI, create:

- `Docs/UI/APPROVED_REFERENCE_DECOMPOSITION.md`;
- `Docs/UI/APPROVED_REFERENCE_COMPONENT_INVENTORY.md`;
- `Docs/UI/APPROVED_REFERENCE_DEVIATIONS.md`.

For each reference record:

- native resolution and aspect ratio;
- major panel bounds as normalized screen coordinates;
- anchor and alignment strategy;
- approximate spacing units;
- panel opacity and border treatment;
- corner radius family;
- typography hierarchy;
- selected, hover, pressed, disabled, and focus states;
- icons required;
- background-scene composition;
- data that is live, placeholder, unsupported, or capability-driven;
- any detected AI text artifact that must not be copied;
- any unavoidable deviation and why.

Do not start the final screen implementation until this decomposition is written.

---

## 5. Reference overlay and capture tooling

Create Editor/development-only tooling that makes fidelity practical instead of
subjective.

Required capabilities:

- choose one of the six approved reference images;
- display it as a full-screen overlay at preserved aspect ratio;
- adjustable opacity from 0–100%;
- reference-only / implementation-only / blended modes;
- optional safe-area and normalized guide display;
- one-click capture at 1672 × 941;
- no inclusion in shipping builds;
- no runtime dependency from gameplay UI to reference PNGs.

Suggested project-owned concept names:

- `UIReferenceOverlayWindow`;
- `UIReferenceOverlayController`;
- `UIReferenceCaptureUtility`.

Do not add an external image-diff package silently. A transparent overlay and
side-by-side capture are sufficient for human approval.

---

## 6. Technology and architecture

Inspect the established UI framework first. Preserve it when sound.

If no coherent runtime framework exists, choose one first-party Unity approach
appropriate for Unity 6. Do not silently install a third-party UI framework.
Document the decision in `Docs/UI/UI_TECHNOLOGY_ADR.md`.

Create or align project-owned boundaries such as:

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
- `PlayerStatusViewModel`;
- `InteractionPromptViewModel` only for contextual use;
- `NotificationQueue`;
- `LoadingScreenController`;
- `MenuFlowController`.

Keep views separate from gameplay state. Views must not search scenes by object
name. Gameplay systems must not know concrete widgets.

---

## 7. Centralized visual tokens

Create a single theme/token source for:

- panel colors and opacity;
- amber/orange focus accent;
- white and muted text;
- warning/error/success colors;
- survival-status colors;
- line/border colors;
- spacing scale;
- corner radii;
- typography styles;
- icon sizes;
- row heights;
- focus/hover/pressed/disabled states;
- transition durations;
- backdrop dimming.

Do not scatter literal styling values across views.

Match the references' visual language:

- dark, translucent, rounded panels;
- restrained borders;
- warm amber/orange selection;
- condensed, practical automotive typography;
- high readability;
- garage/mechanic identity;
- minimal animation;
- no sci-fi styling;
- no expensive mandatory live blur.

Use an already licensed/project-owned font closest to the reference character.
Do not download or bundle an unapproved font. If no suitable font exists, use a
clearly documented temporary fallback and list the font as manual art work.

Use existing project-owned icons. If required icons are missing, author simple
original vector icons in the project. Do not crop icons from the references or
silently add a third-party icon pack.

---

## 8. Main menu — locked composition

Reference:
`References/UI/Approved/08A/01_MAIN_MENU_APPROVED.png`

Reproduce the screen composition closely:

- large My Summer Car Remake logo in the upper-left;
- live garage background;
- Satsuma hero vehicle centered/center-left with open hood;
- right-side vertical primary menu stack;
- greeting/smiley card in the upper-right;
- wide lower information strip;
- small utility navigation at lower-right;
- version/build label at lower-left.

Required primary actions and order:

1. Continue;
2. New Game;
3. Load Game;
4. Credits;
5. Quit.

Required lower information blocks:

- car color selection;
- interior/trunk preview;
- performance graph/preview;
- import music files.

Required lower-right utility entries:

- Settings;
- Mods;
- Developer Tools in development builds only.

Functional rules:

- `Continue` binds to actual latest-save metadata and is disabled when no valid
  save exists;
- `New Game` uses the project new-game flow;
- `Load Game` uses actual save slots;
- color selection preserves original functionality and must not mutate a save
  until the new-game flow confirms it;
- music import shows real state or a truthful unavailable state;
- performance data must be measured/capability-driven or clearly marked as a
  development preview; do not present fabricated production telemetry;
- trunk/interior preview can use a project-owned render texture or documented
  temporary image until the live preview is available;
- Mods may remain a scope-approved placeholder;
- Developer Tools is hidden outside development builds unless explicitly enabled.

Do not replace this composition with a generic centered menu, card carousel, or
flat launcher.

Do not use the approved screenshot as the background. Recreate the garage camera,
vehicle composition, lighting, and widgets with live/project-owned content.

---

## 9. Shared settings shell — locked composition

References:

- `02_GRAPHICS_SETTINGS_APPROVED.png`;
- `03_AUDIO_SETTINGS_APPROVED.png`;
- `04_CONTROLS_SETTINGS_APPROVED.png`;
- `05_GAMEPLAY_SETTINGS_APPROVED.png`.

All settings pages must share one consistent shell:

- logo upper-left;
- left vertical category navigation;
- large main settings panel;
- optional right-side preview/summary panel where the reference has one;
- garage/Satsuma background visible behind translucent UI;
- greeting/smiley card upper-right;
- Back action lower-left;
- page actions along the lower edge;
- version/build text lower-left.

Left navigation order:

1. Graphics;
2. Audio;
3. Controls;
4. Gameplay;
5. Accessibility;
6. Mods.

The selected category uses the same amber/orange filled-outline treatment as the
references.

Accessibility and Mods do not yet have approved page references. Keep them in the
navigation. Implement only the smallest functional page consistent with the
shared shell, with no speculative visual redesign. Mark them `ReferencePending`
in documentation.

---

## 10. Graphics settings — locked page

Reference:
`02_GRAPHICS_SETTINGS_APPROVED.png`

Match the page structure and row density closely.

Required rows, subject to capability validation:

- Display Mode;
- Resolution;
- Refresh Rate;
- V-Sync;
- Upscaling / Sharpening;
- Upscaler quality;
- Sharpening;
- Frame Generation;
- Texture Quality;
- Shadow Quality;
- Reflection Quality;
- Volumetric Quality;
- Vegetation Density;
- Post Processing;
- Motion Blur;
- Depth of Field;
- Ray Tracing.

Required right panel:

- performance preview image/render;
- Average FPS;
- 1% Low;
- VRAM Usage;
- horizontal VRAM meter;
- small explanatory copy.

Required actions:

- Apply;
- Reset to Defaults;
- Revert Changes.

Capability rule:

Rows visible in the reference may remain visible for layout fidelity, but an
unsupported feature must be disabled and labeled truthfully as unavailable. Do
not fake DLSS, frame generation, ray tracing, HDR, or benchmark values.

Do not convert the page into tabs, accordion sections, or a generic vertical
settings list.

---

## 11. Audio settings — locked page

Reference:
`03_AUDIO_SETTINGS_APPROVED.png`

Required rows:

- Master Volume;
- Engine Volume;
- Environment Volume;
- Voice Volume;
- Music Volume;
- Radio Volume;
- UI Sounds;
- Weather Sounds;
- Thunder Sounds;
- Reverb / Ambience;
- Dynamic Range;
- Subtitles;
- Audio Output.

Required right panel:

- Audio Test;
- test action and truthful state;
- Sound Profile summary;
- explanatory helper text/tip.

Required actions:

- Apply;
- Reset;
- Defaults.

Bind through the project audio backend. Widgets must not call Wwise directly.

---

## 12. Controls settings — locked page

Reference:
`04_CONTROLS_SETTINGS_APPROVED.png`

Required main table columns:

- Action;
- Primary;
- Secondary.

Representative rows must include actual project action IDs for:

- Steering Left;
- Steering Right;
- Throttle;
- Brake;
- Clutch;
- Gear Up;
- Gear Down;
- Ignition;
- Handbrake;
- Interact;
- Inventory when the project actually has it;
- Map;
- Journal;
- Pause.

Required right-side cards:

- Mouse;
- Gamepad;
- Input Preview.

Required settings include:

- look sensitivity;
- aiming/interaction sensitivity when supported;
- invert Y;
- vibration;
- trigger dead zone;
- controller layout;
- controller support/input mode.

Required action:

- Reset to Defaults.

Use the Unity Input System and existing action maps. Support conflict detection,
cancel, and persistence. Do not invent bindings for systems that do not exist.

---

## 13. Gameplay settings — locked page

Reference:
`05_GAMEPLAY_SETTINGS_APPROVED.png`

Required left/main content, where implemented or capability-driven:

- Autosave Slots;
- Save Confirmation;
- Pause Behavior;
- HUD Mode;
- Mileage Units;
- Language;
- Difficulty Preset;
- Fatigue Effects;
- Alcohol Effects Intensity;
- Hints;
- Tutorial Prompts;
- Interaction Outlines;
- Camera Shake;
- Developer Mode Visibility.

Required right-side cards:

- Profile;
- Manage Profiles;
- Immersion Level gauge;
- Summary.

Required actions:

- Reset to Defaults;
- Apply Changes.

Do not let an `Immersion Level` gauge become a gameplay score. It is a concise
summary of selected options only.

Do not add route guidance, minimap, quest-tracker, RPG inventory, XP, or automatic
mechanic assistance as a default gameplay setting.

---

## 14. Default in-game HUD — locked composition

Reference:
`06_INGAME_HUD_APPROVED.png`

This image is the authoritative default HUD target.

The default HUD contains only:

- compact time/day/date block;
- compact money block;
- vertical needs/status panel containing:
  - Thirst;
  - Hunger;
  - Stress;
  - Urine;
  - Fatigue;
  - Dirtiness.

Match closely:

- upper-left placement;
- stacked time/money block;
- needs panel directly below with matching width;
- icon/label/value alignment;
- compact row height;
- dark translucent panel treatment;
- colored icon and short bar treatment;
- numeric percentages visible as shown in this approved reference;
- restrained spacing and no extra permanent widgets.

The displayed percentage convention is an explicit user-approved exception to
older generic guidance that hid exact values.

Do **not** show by default:

- GPS;
- minimap;
- quest/objective tracker;
- route markers;
- waypoint distance;
- speedometer overlay;
- tachometer overlay;
- gear overlay;
- fuel/coolant/battery overlay;
- task checklist;
- permanent interaction prompt;
- permanent inventory/hotbar;
- held-item strip;
- tutorial hint;
- headlight prompt;
- modern notification clutter.

The physical vehicle dashboard remains authoritative while driving.

Contextual interaction prompts, save indicators, and critical warnings may appear
only when relevant and must disappear promptly. They are not part of the
persistent reference layout.

Support UI scale and color-independent critical cues without changing the default
composition at 100% scale.

---

## 15. Unreferenced required screens

The existing milestone also requires functional:

- boot/loading flow;
- pause menu;
- confirmation dialogs;
- save/load status;
- accessibility page;
- minimal Mods placeholder when allowed.

No approved exact reference exists for these screens. Extend the approved visual
tokens and settings-shell language conservatively. Do not invent a new style or
large new navigation structure. Mark each as `ReferencePending` and include it in
the review captures separately from the six locked targets.

---

## 16. Settings, localization, input, and persistence

Retain the sound technical requirements from the previous milestone specification:

- pending/apply/cancel/default settings model;
- capability validation;
- versioned settings persistence separate from world save;
- migrations;
- safe-mode overrides where useful;
- keyboard, mouse, and gamepad navigation;
- visible focus state;
- focus restoration;
- no focus traps;
- localization-ready string IDs;
- plural/format support;
- font fallbacks;
- UI scale;
- high contrast;
- reduced motion;
- subtitle/caption hooks;
- hold/toggle alternatives;
- color-independent status cues.

Do not hard-code user-facing strings across scripts.

---

## 17. Performance requirements

Measure and document:

- layout/rebuild cost;
- allocations;
- panel/background cost;
- live garage scene cost;
- render-texture preview cost;
- HUD update frequency;
- controller-navigation latency.

Do not update labels, bars, graphs, or previews every frame when their source has
not changed.

Do not require a real-time blur effect to match the translucent references.

---

## 18. Tests

Add or update tests for:

- route/navigation state;
- focus behavior;
- apply/cancel/default;
- settings serialization/migration;
- capability filtering and truthful unavailable states;
- input rebinding and conflicts;
- keyboard/mouse/gamepad navigation;
- UI scale;
- reduced-motion behavior;
- localization key validation;
- HUD view-model mapping;
- default HUD includes exactly the approved persistent categories;
- default HUD includes numeric percentages as approved;
- default HUD contains no GPS/minimap/quest tracker/vehicle telemetry/hotbar;
- main menu action order;
- development-only Developer Tools visibility;
- loading and error states;
- missing approved-reference detection in Editor tooling.

Add PlayMode smoke tests for:

- boot → main menu;
- main menu → settings → back;
- all four locked settings pages;
- pause/resume;
- settings persistence;
- gameplay HUD;
- controller focus traversal;
- UI scale at representative 16:9, 16:10, ultrawide, and 4:3 viewports where
  practical.

---

## 19. Required review captures

Capture at the native reference comparison viewport, 1672 × 941, 100% UI scale:

- `MainMenu_Implementation.png`;
- `Graphics_Implementation.png`;
- `Audio_Implementation.png`;
- `Controls_Implementation.png`;
- `Gameplay_Implementation.png`;
- `HUD_Implementation.png`.

For each locked screen produce:

- reference-only capture;
- implementation-only capture;
- 50% blended overlay capture;
- short deviation note.

Store under:

`Docs/UI/Review/08A/`

Do not claim visual fidelity from code inspection alone.

Final user approval is required for the six locked screens. Codex may mark them
`ImplementationComplete`, but not `VisuallyApproved`.

---

## 20. Documentation

Create or update:

- `Docs/UI/UI_ARCHITECTURE.md`;
- `Docs/UI/UI_TECHNOLOGY_ADR.md`;
- `Docs/UI/VISUAL_LANGUAGE.md`;
- `Docs/UI/APPROVED_REFERENCE_DECOMPOSITION.md`;
- `Docs/UI/APPROVED_REFERENCE_COMPONENT_INVENTORY.md`;
- `Docs/UI/APPROVED_REFERENCE_DEVIATIONS.md`;
- `Docs/UI/SCREEN_FLOW.md`;
- `Docs/UI/SETTINGS_SCHEMA.md`;
- `Docs/UI/HUD_SPEC.md`;
- `Docs/UI/ACCESSIBILITY_CHECKLIST.md`;
- `Docs/UI/LOCALIZATION_READINESS.md`;
- `Docs/UI/UI_TEST_MATRIX.md`;
- `Docs/Milestones/MILESTONE_08A_REPORT.md`.

---

## 21. Explicit non-goals

Do not implement or introduce:

- a free redesign of approved screens;
- a different information architecture for locked pages;
- permanent minimap/GPS;
- objective/quest tracker;
- RPG inventory or mandatory hotbar;
- route lines or world-space guidance;
- levels, XP, battle-pass, store, news, or social panels;
- a complete mod browser;
- online profile services;
- unsupported graphics/audio features presented as working;
- full final credits content;
- screenshot pixels baked into runtime UI;
- external UI/font/icon packages without approval;
- unrelated gameplay systems.

---

## 22. Definition of done

Milestone 08A is technically complete only when:

1. The six approved references are catalogued and decomposed.
2. Reference overlay/capture tooling works in Editor/development only.
3. Main menu composition closely matches its approved reference.
4. Graphics, Audio, Controls, and Gameplay pages closely match their approved
   references and share one settings shell.
5. Default HUD closely matches its approved reference and contains no extra
   permanent widgets.
6. Settings apply/cancel/default and persistence work.
7. Unsupported capabilities are represented truthfully.
8. Keyboard, mouse, and gamepad navigation work.
9. Accessibility and localization foundations exist.
10. UI audio uses backend contracts.
11. Relevant tests pass or are honestly reported.
12. The six implementation and blended comparison captures are produced.
13. No reference PNG is included as a runtime visual dependency.
14. No screen is marked `VisuallyApproved` without user review.

A screen that is functional and attractive but visibly departs from the approved
layout is **not accepted**.

---

## 23. Final response

Report:

1. References inspected and hashes verified;
2. UI technology selected/preserved;
3. Architecture and theme tokens;
4. Screens implemented;
5. Settings implemented;
6. HUD implemented;
7. Input/accessibility/localization;
8. Tests and actual results;
9. Performance observations;
10. Comparison-capture paths;
11. Files changed;
12. Manual icon/font/art work remaining;
13. Known visual deviations;
14. Readiness for user visual review.

Stop after Milestone 08A and the required captures. Do not continue into save
hardening or speculative UI expansion until the references are reviewed.
