# Milestone 08A screen flow

Status: `BoundedFlowImplemented / VisualReviewPending`
Date: `2026-07-20`

## Route graph

```text
Bootstrap
  -> Loading
      -> prepared/dormant Main Menu (default production policy)
          -> New Game -> activate IGameplaySessionGate -> In-Game HUD
          -> Settings -> Graphics <-> Audio <-> Controls <-> Gameplay
                              <-> Accessibility <-> Mods
          -> Credits overlay -> Main Menu
          -> Quit -> Confirmation -> Quit process / Main Menu

In-Game HUD
  -> System/Pause -> Pause
      -> Resume -> In-Game HUD
      -> Settings -> settings shell -> Pause
      -> Save / Load Status -> truthful unavailable status -> Pause
      -> Return to Main Menu -> reload Bootstrap build index 0
      -> Quit -> Confirmation -> Quit process / Pause
```

`Continue` and `Load Game` remain visible in their locked slots but are disabled
because save storage/latest-save metadata do not exist. They do not enter a
fake route. The main-menu Mods utility and settings category both open the same
minimal truthful unavailable placeholder.

## Route catalogue

| Route | Reference state | Entry | Exit/current behavior |
|---|---|---|---|
| `Boot` | `ReferencePending` | initial enum state | composition immediately opens Loading; no concrete Boot view |
| `Loading` | `ReferencePending` | initialization | world-ready delegate selects Main Menu or HUD |
| `MainMenu` | locked | production startup, fresh reload | prepared world remains dormant; New Game activates the session gate; settings, credits, quit |
| `SettingsGraphics` | locked | Main Menu or Pause | category navigation; Back reverts pending changes |
| `SettingsAudio` | locked | category navigation | same shared transaction shell |
| `SettingsControls` | locked | category navigation | same shared transaction shell |
| `SettingsGameplay` | locked | category navigation | same shared transaction shell |
| `SettingsAccessibility` | `ReferencePending` | category navigation | minimal settings shell; Back to caller |
| `SettingsMods` | `ReferencePending` | category navigation | truthful unavailable page |
| `Pause` | `ReferencePending` | `System/Pause` while in gameplay | resume/settings/reload menu/quit |
| `ConfirmationDialog` | `ReferencePending` | Quit from Main Menu or Pause | Cancel returns to caller; Confirm requests process exit |
| `SaveStatus` | `ReferencePending` | Pause menu and development review | explicit unavailable state; Back restores the calling route/focus |
| `InGameHud` | locked | New Game or start-in-game policy | `System/Pause` opens Pause |

## Main-menu ordering contract

The primary action order is fixed:

1. Continue;
2. New Game;
3. Load Game;
4. Credits;
5. Quit.

Continue and Load must become interactive only after a real save capability is
provided. New Game activates the explicit idempotent `IGameplaySessionGate`,
starts a fresh bounded local Bootstrap session and enters the HUD without
creating or mutating a save/profile. Before that activation, the world may be
prepared but gameplay camera/environment simulation remain dormant. The full
new-game setup flow is outside 08A and must not be inferred.

The utility order is Settings, Mods, Developer Tools. Developer Tools is
interactive only in an Editor/development context and is unavailable in a
release build.

## Settings navigation and transaction behavior

Category order is fixed:

1. Graphics;
2. Audio;
3. Controls;
4. Gameplay;
5. Accessibility;
6. Mods.

Settings pages edit only the pending snapshot. Their shared equal-size action
row is exactly `Apply / Reset / Cancel`: Apply validates and persists; Reset
replaces Pending with Defaults but still requires Apply; Cancel restores
Applied. Back also cancels pending edits and returns to the caller, either Main
Menu or Pause. Accepted control rebinds follow this transaction and do not
persist until Apply.

Changing locale rebuilds localized routes after Apply. Controls binding
overrides are stored with the applied settings document. Unsupported rows stay
visible where the corrected composition retains them but cannot receive focus
as working controls. Direct user correction removes Graphics performance
preview, Audio test, Controls input preview and Gameplay
profile/immersion/summary from live settings routes.

## Focus and input mode

The UI uses `InputSystemUIInputModule` for mouse, keyboard and gamepad. Each
route remembers its last valid interactable selection; returning from a modal
restores it, while a missing/disabled target falls back to the first
interactable Unity `Selectable`. Automated PlayMode coverage includes
directional controller-style focus, confirmation/save-status restoration and
focus restore after route rebuild. The removed Controls input-preview card is
not a runtime device diagnostic. The following remain pending final
verification:

- directional traversal through every locked page;
- no focus traps at disabled rows;
- 16:10, ultrawide and 4:3 focus/layout smoke runs.

## Pause ownership

The `System/Pause` action is part of the project player InputActionAsset and is
bound to keyboard Escape and gamepad Start. Opening UI suspension:

- disables project gameplay input gates;
- suppresses contextual/debug overlay gates;
- pauses authoritative game time and `Time.timeScale`;
- unlocks and shows the cursor.

Resume restores the captured states. Settings opened from Pause returns to
Pause rather than entering gameplay directly.

## Review-only flow

In Editor/development builds `ShowReviewScreen` opens only a locked route, and
`ShowReferencePendingReviewScreen` opens only the six approved unreferenced
08A routes. Both force English and 100% scale; the locked path also supplies
deterministic HUD review data. Release builds reject the review APIs. The
command-line probe captures six locked implementation screens and six separate
`ReferencePending` screens, then exits.

## Approval boundary

Navigation or PlayMode success does not imply visual approval. Each locked
screen requires explicit user review of its canonical implementation-only and
blended captures. That review was completed on
2026-07-20, and the six locked screens are now `VisuallyApproved`.
