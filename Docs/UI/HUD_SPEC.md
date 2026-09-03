# Milestone 08A default HUD specification

Status: `UserDirectionApplied / ContextActionImplementationComplete /
FocusedContextActionTestsPassed /
VisualApprovalPending`

Reference authority, in precedence order:

1. Direct user corrections dated `2026-09-02`;
2. `References/UI/Direction/2026-09-02_ContextActionUI_StyleReference.png`
   for the reticle and contextual action stack;
3. Direct user corrections dated `2026-08-09` and
   `References/UI/Direction/2026-08-09_GameplayHUD_ContextNeedsSubtitles_StyleReference.png`
   for needs and subtitles only;
4. `References/UI/Direction/2026-08-05_MinimalGameplayHUD_StyleReference.png`
   for visual weight and styling, with its illustrated speedometer explicitly
   excluded;
5. `References/UI/Approved/08A/06_INGAME_HUD_APPROVED.png` for the established
   functional category and truthful-data contract.

Canonical viewport: `1672 x 941`, UI scale `100%`

The later user direction replaces the original upper-left glass-card
composition with a lighter, scene-integrated HUD. It does not remove any
required survival state. Numeric need percentages are now explicitly absent;
missing production values are never invented.

## Persistent layout

| Block | Canonical bounds | Contents |
|---|---:|---|
| Clock | `1450,38,180,62` | right-aligned time, then one right-aligned abbreviated weekday/date line |
| Money | `1450,118,180,34` | whole-markka balance/unavailable value followed by amber `MK` |
| Needs | `42,38,152,310` | six vertical need groups |
| Typical need group | `0,0,152,44`, vertical stride `52` | 28 px white icon, label, 112 x 3 px track, gradient fill and 7 px endpoint |

Needs form a compact vertical column at upper-left. Clock sits at upper-right
with money directly beneath it. The localized weekday and date share one
right-aligned line (`FRI, 27 JUN`) so spacing cannot drift between fields. The three persistent groups
have no card, glass capture or blur. Their project-owned graphics use a
soft sub-pixel dark shadow (`0.38` alpha, `0.65 px` offset) so thin elements
remain legible over bright world content without a hard black duplicate. At
100% scale no other persistent block is allowed.

## Exact persistent categories

1. Time;
2. Day;
3. Date;
4. Money;
5. Thirst;
6. Hunger;
7. Stress;
8. Urine;
9. Fatigue;
10. Dirtiness (`НЕОПРЯТНОСТЬ` in Russian).

Each need row combines a licensed Lucide outline icon, localized label and short
fill bar. Numeric percentages are intentionally absent. A fixed full-width
white-to-amber gradient is revealed with `Image.fillAmount`, so severity moves
toward the accent at the full-scale end and a partial need reveals only the
corresponding portion rather than squeezing the whole gradient into its current
width. A white 7 px endpoint marks the exact value on the 3 px track. Large
white icons and the soft dark shadow retain colour-independent readability. The
icons use a 64 px source raster, 1.6 px Lucide contour, no mipmaps and no texture
compression so their 28 px presentation remains crisp.

## Data authority

| Field | Production authority | Current production behavior |
|---|---|---|
| time/day/date | `IGameTimeService.Snapshot` | live authoritative values; date/day derived consistently |
| money | future money/save provider | whole-markka balance/em dash followed by a separate amber `MK`; no fake balance |
| six needs | `IPlayerNeedsService.Snapshot` when composed | live values; otherwise empty fill and hidden indicator |

The game-time binding caches the snapshot revision before rebuilding date/time
text. In production, no concept value is copied into missing money or needs
state.

## Review fixture

Editor/development review mode may set deterministic values for the sole
purpose of the 1672x941 comparison capture. The fixture is enabled only through
the development review route, forces the comparison locale/scale and is not a
release data provider. A review screenshot must not be cited as proof that the
money or needs gameplay systems exist.

## Contextual-only UI

The following may appear only in response to a relevant event and must dismiss
promptly:

- centre interaction reticle: ordinary dot, pickup hand, valid-install check or
  valid-removal cross;
- lower-left stack of zero to three available-action plaques;
- the replacement H/M/N alternative-action stack while Alt is held, without an
  Alt instruction plaque;
- bottom-centred dynamic target-title/subtitle stack; either row may exist
  alone, and an active subtitle is placed below the current target title;
- save status;
- critical warning;
- bounded notification.

They are not part of the persistent locked HUD. Development diagnostic overlays
must be disabled by default and suppressed while menu UI is active.

The user-enabled FPS counter follows the same minimal scene-integrated styling
when shown: no panel, line, capture or blur; the pure-white numeric value comes
first, the amber `FPS` suffix follows it at the same 17 px type size and both
text elements use the shared dark shadow.

## Forbidden persistent content

- GPS, minimap, route markers or waypoint distance;
- objective/quest tracker or task checklist;
- speedometer, tachometer, gear, fuel, coolant or battery overlays;
- permanent interaction action copy;
- inventory, hotbar or held-item strip;
- tutorial/headlight prompt;
- general notification clutter.

The vehicle's physical dashboard remains authoritative while driving.

## Scale and accessibility

- Canonical review uses 100% scale.
- User scale is applied to the shared safe-frame root.
- Critical meaning cannot rely on icon colour alone.
- Need labels and large white icons remain visible together.
- Text, icons, tracks, fills and indicators use the same dark shadow contract;
  the HUD does not rely on backdrop darkness for contrast.
- High-contrast token switching is not yet wired and remains pending.
- Screen-reader support is not part of the current Unity implementation.

## Update and performance contract

- HUD refresh runs only while `InGameHud` is active.
- Clock/date should change only when `IGameTimeService` revision changes.
- Money and needs providers must expose revision/change signals; no polling
  allocation or per-frame view-model reconstruction is allowed.
- The clock, money and needs groups use no panel, project-camera capture or
  blur. The former recurring capture/downsample path remains removed because it
  caused severe frame loss and visible stutter while rotating the camera.
- There is no minimap camera, telemetry graph or full-screen gameplay blur.
- `InteractionActionSnapshot` uses fixed storage for three rows. Binding labels
  and upper-case action copy are cached until the capability snapshot or Input
  System binding revision changes; the renderer does not enumerate target
  components or rebuild collections every frame.

The current presentation still writes unavailable money text during HUD
refresh. Final performance evidence may optimize this path without changing the
visual contract.

## Acceptance checks

The focused historical PlayMode contracts still cover the exact six needs,
shadow coverage, vertical ordering, fixed gradient-fill geometry and forbidden
persistent widgets. The 2026-09-02 interaction tests cover fixed three-row
  storage, lower-left safe-frame geometry, pickup/install/removal reticles,
  carried-item release/throw/rotation, directional wheel availability, semantic
  binding glyphs, bottom-centred context text and Alt press/release state.
  Current runtime and test sources compile in Unity 6 batch mode; a fresh player
  capture and user visual review remain pending. Only the new context-action
  visual approval is reopened.

### Context action presentation

`PlayerInteractionController.CurrentActionSnapshot` is the single action-state
truth for the same capability routing used by input dispatch. It contains one
reticle state and at most three `InteractionActionHint` values. The separate
`CurrentDisplayName` path exposes only explicit `IInteractionDisplayTarget`
metadata for the bottom-centre title; no hierarchy path or renderer bound enters
the HUD. A loose assembly part exposes this metadata, but clears both its title
and localization key while installed (including after save restoration). An
installed part therefore contributes only capabilities whose live `Can*`
checks succeed; its fasteners likewise expose tool actions without name
metadata. A secured fixed lever produces no plaque or title, while an installed
hinged door can still produce its currently valid open/close actions.

At the canonical `1672 x 941` viewport the stack starts `36 px` from the left
safe edge and ends `44 px` above the bottom safe edge. A plaque is `40 px` high,
rows have a `7 px` gap, and width follows its unwrapped copy. Surfaces are
rounded, black at `0.40` alpha and unbordered. Every live binding label sits in
a compact `24 px`-high keycap with a lightly rounded one-pixel outline. Text and
licensed white icons receive a project-owned dark halo; centre affordance icons
use a stronger two-ring halo for bright-world readability. Wider aspect ratios
keep the stack against the centred canonical safe frame.

The centre state is a small white dot unless a more specific affordance exists.
The pickup state uses the hash-locked donor `gui_uset` open palm as a private
Phase 1 `TemporaryDirectImport`: white hand mass, black internal finger/palm
separators and the same strong project-owned halo as the other centre states.
Its replacement key is `ui.interaction.pickup-hand`; it remains presentation
only and must be reauthored for Phase 2.

States are selected as follows:

- hand plus `ЛКМ — ВЗЯТЬ` when pickup/tool selection is currently valid;
- check plus `ЛКМ — УСТАНОВИТЬ` when the carried target can be accepted by the
  current mount;
- cross plus `ПКМ — СНЯТЬ` (or the target's localized removal message) when an
  explicit `IRemovalInteractionTarget` is actionable;
- dot plus `ЛКМ — ОТПУСТИТЬ`, `ПКМ — БРОСИТЬ` and
  `КОЛЕСО — ВРАЩАТЬ` when the item is carried but cannot be installed;
- dot plus endpoint-filtered `КОЛЕСО ↑/↓` actions for staged fasteners, steering
  alignment and other explicit directional incremental capabilities;
- no plaques when no action is available.

Input labels are resolved from the live Input System bindings with compact
mouse names. A semantic mouse glyph follows the effective binding: LMB/RMB fill
the matching button, middle-click fills the wheel, and scroll uses adjacent
up/down arrows (one signed arrow when the capability is directional).
Directional capability interfaces expose availability without
performing the operation, so an end-stop does not advertise a useless wheel
direction. Holding the hidden `Player/AlternativeActions` Alt action preserves
the current reticle and replaces the entire ordinary stack with the existing
H/M/N gesture actions. Alt is a reveal layer only: H/M/N retain their original
dispatch, and no `ALT — ДОП. ДЕЙСТВИЯ` hint is rendered.

### Dynamic target and subtitle stack

`CrossdotPresenter` draws a bottom-centred stack ending `44 px` above the
canonical safe edge. Target title and subtitle use the same loaded font and the
same `18 px` size so the item name no longer reads as secondary microcopy. The
title is bold; the subtitle is regular and uses a slightly stronger dark
surface below it. Either row collapses completely when empty.

Gameplay supplies a stable localization key plus compatibility display text.
The presentation boundary resolves target names, compact action labels,
keycaps and common feedback in both `ru-RU` and `en-US`; donor/debug aliases
such as `battery0`, `oilpan`, `piston1` and `wheel_gt1` never appear as the
preferred player-facing copy. Unknown authored target names remain visible as
a compatibility fallback, while known mojibake/prototype labels are replaced
by a safe generic part name. Installed assembly parts and their fasteners are
the deliberate exception: their title row collapses completely, even when an
action row remains visible.

`FirstPersonLifeActionPresenter` remains timing and subtitle authority for life
actions plus the already routed NPC/traffic fallback copy. It implements the
small `IPlayerSubtitleSource` boundary and suppresses its legacy fallback frame
while `CrossdotPresenter` owns presentation, avoiding duplicate subtitles. Both
remain `IUiVisibilityGate`s, so pause/front-end routes suppress gameplay-only
text together.
