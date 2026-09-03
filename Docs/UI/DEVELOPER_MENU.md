# Developer menu

Status: `ImplementationComplete / ManualVisualSmokePending`  
Date: `2026-08-05`

## Scope and access

The former text-command console is now a project-owned, button-driven
development menu. It is available only in the Unity Editor and Development
Builds. Open or close it with `F10` or backquote; `Escape` and the header close
button also close it. The accepted main-menu `Developer Tools` utility action
opens the same surface.

The compact layout targets gameplay viewports down to 720 px tall. Its modal
backdrop uses a 58% black veil on a deeper IMGUI layer: the world is subdued,
while the menu panel remains undimmed and retains the established dark/amber
developer styling.

The runtime type remains
`MSC.Bootstrap.Development.ProductionDeveloperConsole` for compatibility with
the established composition root and serialized references. It no longer
exposes a command text field.

## Sections

### Teleport

- Presents every previously supported project-owned destination as a button.
- Groups destinations into main locations, NPC/event locations, and job/service
  locations.
- Keeps the canonical coordinates and donor-evidence IDs already recorded by
  the project.
- Temporarily disables the player's `CharacterController`, moves the explicit
  composed player transform, synchronizes PhysX transforms, and restores the
  controller.
- Can optionally close the menu after a teleport.

### Needs

- Displays live 0–100 values for thirst, hunger, stress, urine, fatigue,
  dirtiness, intoxication, and hangover.
- Provides `-10`, `-1`, `+1`, and `+10` controls for every value.
- `Reset all` restores the fresh-game needs state, including 83 kg weight, and
  clears pending metabolism effects.
- `Disable needs` is a transient developer override. It suppresses passive
  progression and gameplay/item effects, cancels an active transient life
  action, and advances the internal processing cursor so enabling the system
  later cannot apply a catch-up spike. Explicit menu adjustments remain usable.
- The disabled state is deliberately absent from native save DTOs.

### Time

- Shows the authoritative project date/time, time scale, and paused state.
- Provides `-1 day`, `-6 h`, `-1 h`, `-10 min`, `+10 min`, `+1 h`, `+6 h`, and
  `+1 day` actions.
- Positive shifts use the production environment's coherent advance boundary,
  so weather, lightning, wetness, and time subscribers advance together.
- Negative shifts are an explicit clock correction and do not pretend to rewind
  already executed world events; this limitation is shown in the menu.
- Time-scale controls use bounded presets from `x0.25` through `x100`, with
  separate pause/resume and `x1` actions.

### Weather

- Offers all nine project-owned logical weather states as buttons.
- Applies a transient high-priority development override through
  `ProductionEnvironmentController`; Enviro 3 remains presentation-only.
- Provides automatic-weather restore, schedule freeze/unfreeze, and a test
  lightning action.
- Shows current precipitation and wind data from the authoritative weather
  state.

## Ownership and build boundary

`ProductionWorldStreamingInstaller` supplies the player, input router, needs
runtime, and production environment explicitly. The menu never searches donor
hierarchies, scene names, or asset filenames. Opening it preserves and disables
the current gameplay-input gate, unlocks the cursor, and restores both on close.

Mutable domain operations used by the menu compile under
`UNITY_EDITOR || DEVELOPMENT_BUILD`. Release capability filtering hides the
main-menu entry, the menu component disables itself, and development mutation
APIs do not compile into release player assemblies.

## Automated validation

- `dotnet build MSC.Bootstrap.Runtime.csproj --no-restore --nologo
  /p:UseSharedCompilation=false`: passed, 0 warnings, 0 errors.
- `dotnet build MSC.Needs.Tests.EditMode.csproj --no-restore --nologo
  /p:UseSharedCompilation=false`: passed, 0 warnings, 0 errors.
- Unity EditMode `MSC.Needs.Tests.EditMode`: passed `12/12`, including reset,
  disabled progression, suppressed gameplay effects, and no catch-up after
  re-enable. Result:
  `TestResults/DeveloperMenu_Needs_EditMode.xml`.

## Manual visual smoke

1. Enter Play Mode through the production Bootstrap and start gameplay.
2. Press `F10`; confirm the dark amber developer surface is above the HUD and
   gameplay look/movement is inactive.
3. Visit all four sections at 16:9 and one narrower viewport.
4. Exercise one teleport, a needs decrement/increment, a backward and forward
   clock shift, and one weather override.
5. Close with `Escape`; confirm the previous cursor and gameplay-input state are
   restored.
