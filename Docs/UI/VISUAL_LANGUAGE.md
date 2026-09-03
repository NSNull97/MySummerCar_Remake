# Milestone 08A visual language

Status: `ReferenceLocked / ImplementationComplete / VisuallyApproved`  
Canonical comparison viewport: `1672 x 941` at `100%` UI scale

The six approved PNGs define composition and visual hierarchy. They are review
inputs only. Runtime surfaces, icons, text and the backdrop are project-owned.

## Character

- rural, mechanical and practical rather than clean-room or sci-fi;
- dark warm translucent glass over a sharp project-owned garage plate;
- warm amber focus and selection;
- compact automotive typography and dense functional rows;
- restrained borders, short transitions and context-specific bounded blur;
- readable labels and values that do not rely on colour alone.

## Canonical frame and spacing

- Reference frame: `1672 x 941`, aspect-preserved and centered.
- Base spacing rhythm: `8 px`.
- Adjacent main cards and settings action groups use an explicit `10 px` gap; the
  main-card row to music card uses `14 px`.
- Common panel padding: `20-26 px`.
- Settings row cadence: approximately `38-39 px`.
- Locked panel bounds are recorded in
  `APPROVED_REFERENCE_DECOMPOSITION.md` and take precedence at 100% scale.
- Wider displays expose more project-owned background; they do not stretch the authored
  frame. Narrow layouts preserve order and clamp to safe area before changing
  proportions.

## Central tokens

The current token source is
`Assets/Game/UI/Presentation/Runtime/UiThemeTokens.cs`.

| Token | Runtime value | Use |
|---|---|---|
| `MenuBackdropFallback` | RGBA `(0.018, 0.015, 0.013, 1.0)` | safe fallback when the authored plate is absent |
| `PauseBackdropDim` | RGBA `(0.006, 0.005, 0.004, 0.74)` | dark layer over the frozen pause capture |
| `MenuGlassNeutral` | RGBA `(0.055, 0.050, 0.045, 0.60)` | neutral menu glass before restrained car-colour mixing |
| `HudGlassTint` | RGBA `(0.008, 0.009, 0.009, 0.70)` | semi-black HUD glass |
| `HudMinimalShadow` | RGBA `(0, 0, 0, 0.38)` | soft sub-pixel contrast shadow for minimal HUD graphics |
| `HudMinimalTrack` | RGBA `(0.94, 0.94, 0.92, 0.30)` | full need track and short corner rules |
| `HudMinimalFill` | RGBA `(0.96, 0.95, 0.92, 0.78)` | authoritative need fill before the amber indicator |
| `Panel` | RGBA `(0.055, 0.047, 0.040, 0.88)` | major surfaces |
| `Card` | RGBA `(0.080, 0.070, 0.060, 0.78)` | secondary surfaces |
| `Row` | RGBA `(0.150, 0.130, 0.115, 0.72)` | controls/settings rows |
| `RowAlternate` | RGBA `(0.110, 0.100, 0.090, 0.72)` | alternating rows |
| `Border` | RGBA `(0.720, 0.680, 0.620, 0.58)` | procedural 1.75 px rounded-ring outline |
| `Accent` | `#F5A019` | selected/focused/primary |
| `Destructive` | `#F15440` | Quit/destructive action |
| `TextPrimary` | `#F5F1E8` | primary copy |
| `TextMuted` | `#A8A39D` | helper/secondary copy |
| `Disabled` | warm grey at 58% alpha | unavailable state |
| `Positive` | `#6CD240` | positive measured result |

Current type sizes are 44/22 for the logo family, 28 for page headings, 19
for sections, 21 for primary actions, 15 for settings rows and 12 for captions.
Helvetica Neue Roman is the deterministic Latin/Cyrillic face across the main
menu, pause, settings, save/loading surfaces, HUD, interaction prompts,
bottom subtitle/status presentation and the development playtest console.

## Surfaces and radius family

- Major panels use a generated rounded nine-slice and a supersampled 1.75 px
  warm-grey rounded-ring border. The border is an inner visual layer rather
  than a uGUI `Outline`, so a rounded mask cannot cut its corners.
- Cards and rows reuse the same project-owned surface family at lower opacity.
- General rows, cards, buttons, masks and major panels use a clearly visible
  `12 px` nine-slice radius at the reference frame; compact tracks use `7 px`
  and circular controls remain fully round.
- Project-owned procedural masks and icons retain their logical 24-32 px output
  sizes but are rasterized at 4x and box-filtered to fractional alpha coverage.
  The screen-space overlay Canvas is pixel-perfect, so scaled edges no longer
  rely on binary low-resolution diagonals.
- Menu glass samples aligned slices from a one-time full-resolution
  `1672x941`, `ARGBHalf`, Linear Gaussian result of the static garage plate.
  Four separable horizontal/vertical iterations use radii `2 / 4 / 6 / 8`.
  Car selection mixes 8% of the selected colour into the neutral glass tint.
  The sharp plate itself is never blurred.
- Slider handles use centred fixed anchors and a simple `16 x 16` sprite; the
  white control is a true circle, not a vertically stretched capsule.

## Interaction states

| State | Treatment |
|---|---|
| Normal | warm dark card, neutral border, primary text |
| Hover/highlight | warmer tint, readable primary text |
| Keyboard/gamepad focus | amber highlight/selection tint |
| Pressed | darker amber/brown response, short 0.08 s fade |
| Selected category | amber filled-outline treatment |
| Disabled | muted foreground and disabled colour, explicit helper/reason |
| Destructive | red foreground/accent, never indicated by colour alone |

The static approved images do not prove every transient state; these extensions
must remain consistent and are subject to keyboard/gamepad manual review.

All six settings categories share invariant logo, category-stack, Back and
greeting bounds. Switching category changes only the selected state and the
central/right content; the chrome does not jump. Central panels keep at least an
8 px visual clearance from navigation and sibling panels. Main-menu actions and
settings categories use the same reduced scale within their respective stacks,
and the greeting aligns to the main action stack's right edge. Every settings
page ends with an equal-size `Apply / Reset / Cancel` row.

## Typography

The gameplay reference calls for a neutral, open neo-grotesque sans rather than
the previously used narrow face. The implementation bundles the user's locally
licensed Helvetica Neue Roman file for deterministic Latin/Cyrillic rendering
and to avoid platform drift from whichever font happens to be installed on a
player machine. Validated Windows families and Unity `LegacyRuntime.ttf` remain
bounded fallbacks if the project font asset is missing or fails glyph validation.
The selected source remains exposed for diagnostics.

Text rules:

- headings/actions are generally uppercase;
- helper text uses sentence case;
- unavailable features retain their slot and state why they are disabled;
- no concept telemetry, save age, profile score or player status is copied as
  a production value;
- semantic corrections such as `Map` replace visible AI artifacts such as
  `Mop`.

## Icons and logo

`UiVisualAssets` loads the six needs symbols (`droplet`, `utensils`, `brain`,
`toilet`, `bed-double`, `sparkles`) from Lucide under the ISC license and keeps
the procedural generator as a missing-asset fallback. Rounded surfaces and the
remaining bounded symbols stay project-generated. The logo is rebuilt from
project-owned text and geometric discs. Reference icons/logo pixels are not
cropped, traced or baked into the game. A final art pass may replace these assets without
changing locked bounds or route semantics.

## Background and blur ownership

Menus and settings use
`Assets/Game/UI/Presentation/Content/MainMenu/M08A_TemporaryGarageBackdrop.png`:
an original project-owned 1672x941 garage plate with a generic compact vehicle
and raised hood. The approved PNG is never a runtime dependency. The plate is
sharp; only masked glass surfaces sample its softened copy. Its aspect-preserved
full-canvas frame is outside `UiScale`, so reducing UI scale never reveals the
prepared gameplay world around the menu.

During gameplay the world remains sharp. The clock, money and needs graphics
have no panel, camera capture or blur; contrast comes from their shared dark
shadow. Pause performs a one-shot `1024x576` project-camera capture, filters it
to a `512x288`, `ARGBHalf` Gaussian result and places the darker pause dim above
it. Settings/dialogs entered from pause retain that frozen context. This
asset/effect split is explicit and does not claim that the temporary vehicle is
the final Phase 1 Satsuma presentation.

Both static-menu and pause filtering use the project-owned shader at
`Assets/Game/UI/Presentation/Content/Shaders/M08A_SeparableGaussianBlur.shader`,
provided through an explicit serialized dependency. There is no runtime
`Shader.Find`. The previous `1/2 -> 1/4 -> 1/8` downsample/upscale pyramid is not
part of the final visual pipeline.

## HUD language

The 2026-08-05 user direction makes the HUD the lightest and most
scene-integrated application of the token family. At 100% scale it places all
six needs in one vertical upper-left column, with the clock at upper-right and
money immediately beneath it. Day and date share one baseline with a compact
4 px field gap. No enclosing card is used. Pure-white 28 px icons, slightly
larger labels, 150 px gradient tracks and white endpoint dots provide the need
readout without numeric percentages, while a shared dark one-pixel shadow keeps every thin graphic
readable over bright sky, water and roads. Urine and Dirtiness
(`НЕОПРЯТНОСТЬ` in Russian) remain present even though the style image omits
them. Its illustrated speedometer and gear display are explicitly not part of
the runtime HUD. When enabled, the FPS counter uses the same panel-free shadowed
language: pure-white numeric value followed by amber `FPS`, with no decorative
line; both elements use the same 17 px type size and baseline. The numeric field
contains digits only, so the amber suffix cannot be duplicated.

The 2026-09-02 direction supersedes the earlier target-anchored context prompt.
The centre reticle is a tiny white dot in its ordinary state, a compact filled
open palm while pickup is valid, a white check while a carried item can be
installed and a white cross while an installed item can be removed. The action
icons use a stronger two-ring black halo. Zero to three compact rounded black
plaques sit in the lower-left canonical safe frame. Each binding label has a
lightly rounded outlined keycap; optional mouse glyphs fill the active button or
show adjacent wheel arrows. No title or action follows world bounds. Holding
Alt replaces those rows with H/M/N gesture actions, but Alt itself is
deliberately not advertised on the HUD.

Explicit target names and subtitles share a dynamic bottom-centre stack in the
same rounded translucent family. A `15 px` bold target row sits above an `18 px`
regular wrapping subtitle row; either row disappears without reserving space.
The pickup palm comes from Material Symbols under Apache-2.0, while check,
removal cross and mouse bases come from the pinned Lucide source.

## Motion and accessibility

- No mandatory animation is used. There is no recurring HUD blur source; pause
  capture is one-shot on entry and menu blur is prepared once from the static
  plate.
- Transition colour fade is 0.08 s.
- Reduced-motion remains a disabled `Adapter Pending` row; no additional motion
  adapter currently exists because the bounded implementation is already
  largely static.
- UI scale is applied under the canonical frame; review captures always use
  100%.
- High contrast remains disabled and explicitly `Adapter Pending` until a
  token-variant adapter exists.
- Colour-independent labels, icons and endpoint position are required even when
  gradient colour is present.
- Transient toast presentation owns its complete rounded panel. When its timer
  expires, the panel and text are both hidden; an empty persistent toast is not
  part of any route.

## Direct user correction to locked blocks

The user explicitly superseded four diagnostic/reference-only blocks after the
initial captures. The live settings routes omit Graphics performance preview,
Audio test, Controls input preview and Gameplay profile/immersion/summary.
Their removal does not authorize a broader redesign: remaining rows, category
order, capability truth and project-owned styling stay within the 08A baseline.

## Approval state

`ImplementationComplete` means the live project-owned screen is built and has
passed technical checks. `VisuallyApproved` can be assigned only by the user
after reviewing implementation-only and 50% blended captures. The user
completed that review on 2026-07-20; all six locked screens are
`VisuallyApproved`.
