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
These are implementation baselines, not evidence of a licensed reference font.

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

The reference calls for a compact condensed automotive sans, but no approved
font asset is present. On Windows the bounded implementation resolves the first
available family from Bahnschrift SemiCondensed, Bahnschrift, Arial Narrow and
Segoe UI, then falls back explicitly to Unity `LegacyRuntime.ttf`. The selected
source is exposed for diagnostics. This is a licensed/system-font foundation,
not final font matching; no external font was downloaded or bundled.

Text rules:

- headings/actions are generally uppercase;
- helper text uses sentence case;
- unavailable features retain their slot and state why they are disabled;
- no concept telemetry, save age, profile score or player status is copied as
  a production value;
- semantic corrections such as `Map` replace visible AI artifacts such as
  `Mop`.

## Icons and logo

`UiVisualAssets` generates simple original bitmap sprites at runtime for the
bounded icon set and rounded surfaces. The logo is rebuilt from project-owned
text and geometric discs. Reference icons/logo pixels are not cropped, traced
or baked into the game. A final art pass may replace these assets without
changing locked bounds or route semantics.

## Background and blur ownership

Menus and settings use
`Assets/Game/UI/Presentation/Content/MainMenu/M08A_TemporaryGarageBackdrop.png`:
an original project-owned 1672x941 garage plate with a generic compact vehicle
and raised hood. The approved PNG is never a runtime dependency. The plate is
sharp; only masked glass surfaces sample its softened copy. Its aspect-preserved
full-canvas frame is outside `UiScale`, so reducing UI scale never reveals the
prepared gameplay world around the menu.

During gameplay the world remains sharp. Only the two HUD blocks sample a
stable semi-black translucent surface; they do not request camera capture or
blur. Pause performs a one-shot `1024x576` project-camera capture, filters it to
a `512x288`, `ARGBHalf` Gaussian result and places the darker pause dim above
it. Settings/dialogs entered from pause retain that frozen context. This
asset/effect split is explicit and does not claim that the temporary vehicle is
the final Phase 1 Satsuma presentation.

Both static-menu and pause filtering use the project-owned shader at
`Assets/Game/UI/Presentation/Content/Shaders/M08A_SeparableGaussianBlur.shader`,
provided through an explicit serialized dependency. There is no runtime
`Shader.Find`. The previous `1/2 -> 1/4 -> 1/8` downsample/upscale pyramid is not
part of the final visual pipeline.

## HUD language

The HUD is the darkest and most compact application of the token family. At
100% scale it contains exactly two upper-left blocks: time/day/date plus money,
then six needs rows. Icons, labels, a short bar and numeric percentages provide
redundant meaning. The numeric percentage convention is the explicit 08A
exception to the older generic guardrail.

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
- Colour-independent labels/numerics are required even when status colours are
  present.
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
