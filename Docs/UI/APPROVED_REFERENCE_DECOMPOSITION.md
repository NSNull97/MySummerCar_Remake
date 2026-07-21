# Milestone 08A — approved reference decomposition

Status: `ReferenceLocked / HashVerified / ImplementationComplete / VisuallyApproved`  
Canonical viewport: `1672 × 941`, UI scale `100%`  
Coordinate convention: `(x, y, width, height)` from the top-left of the
canonical reference frame. Measurements have an expected tolerance of 2–6 px
for panels and 10–15 px for live-scene silhouettes.

The six files under `References/UI/Approved/08A/` are authoritative review
inputs. Their SHA-256, byte size and dimensions were verified against
`REFERENCE_MANIFEST.json` on 2026-07-18. They are never runtime assets.

The tables below remain the measured source-reference decomposition. A bounded
direct user correction on 2026-07-20 supersedes selected live blocks without
rewriting these measurements: main/category buttons are reduced and aligned;
the greeting aligns with the main action right edge; Graphics performance,
Audio test, Controls input preview and Gameplay profile/immersion/summary are
removed; settings pages use one `Apply / Reset / Cancel` row. See
`APPROVED_REFERENCE_DEVIATIONS.md`.

## Shared visual language

- Reference frame: canonical 16:9 safe frame; wider displays extend only the
  active project-owned backdrop while authored UI remains inside the frame.
- Base spacing rhythm: 8 px; common panel padding: 20–26 px.
- Major panels: dark warm translucent fill, approximately 76–86% effective
  opacity, 11–14 px radius, 1 px neutral warm-grey border.
- Secondary rows/cards: approximately 55–75% effective opacity, 7–10 px radius.
- Selection/focus: amber border and restrained warm inner tint.
- Primary accent: approximately `#F5A019`; destructive: approximately
  `#F15440`; primary text: approximately `#F5F1E8`; muted text:
  approximately `#A8A39D`.
- Typography target: compact automotive grotesk/condensed sans. The reference
  proves hierarchy and approximate size, not a licensed font identity.
- Static references prove selected, primary, destructive, toggle-on and
  toggle-off states. Hover, pressed and disabled states are project-defined
  extensions and must remain consistent with the locked language.
- All icons, logo treatment, previews and background content are rebuilt from
  project-owned widgets, generated vector-like primitives and context-owned
  project backdrop data.

## 01 — Main menu

| Region | Pixel bounds | Normalized bounds |
|---|---:|---:|
| Logo | `88,51,354,260` | `0.0526,0.0542,0.2117,0.2763` |
| Greeting | `1413,40,210,68` | `0.8451,0.0425,0.1256,0.0723` |
| Primary action stack | `1288,182,274,439` | `0.7703,0.1934,0.1639,0.4665` |
| Car colour | `93,613,334,178` | `0.0556,0.6514,0.1998,0.1892` |
| Interior/trunk | `427,613,332,178` | `0.2554,0.6514,0.1986,0.1892` |
| Performance | `759,613,377,178` | `0.4539,0.6514,0.2255,0.1892` |
| Music import | `93,801,1043,82` | `0.0556,0.8512,0.6238,0.0871` |
| Utility strip | `1151,849,455,35` | `0.6884,0.9022,0.2721,0.0372` |

Composition: logo top-left; garage/vehicle hero plate across centre-left;
greeting top-right; right-middle actions in exact order `Continue`, `New Game`,
`Load Game`, `Credits`, `Quit`; lower-left information strip; lower-right
Settings/Mods/Developer Tools; version bottom-left. Continue is the reference
selection and Quit is destructive. The live correction keeps that order but
uses smaller, consistent action bounds and aligns the greeting to their right
edge.

## 02 — Graphics settings

| Region | Pixel bounds | Normalized bounds |
|---|---:|---:|
| Logo | `83,44,238,175` | `0.0496,0.0468,0.1423,0.1860` |
| Category navigation | `84,242,253,446` | `0.0502,0.2572,0.1513,0.4740` |
| Back | `84,771,253,67` | `0.0502,0.8193,0.1513,0.0712` |
| Main panel | `370,135,618,716` | `0.2213,0.1435,0.3696,0.7609` |
| Preview panel | `987,135,542,414` | `0.5903,0.1435,0.3242,0.4400` |
| Bottom actions | `1017,876,612,44` | `0.6083,0.9309,0.3660,0.0468` |

The main and preview panels share a vertical seam. Graphics is selected in the
left navigation. Rows use approximately 38 px cadence. Capability-sensitive
DLSS, frame generation, ray tracing and measured performance retain their
source-reference slots but cannot claim support without live evidence. The live
performance-preview panel is intentionally removed; the shared transaction row
is `Apply / Reset / Cancel`.

## 03 — Audio settings

| Region | Pixel bounds | Normalized bounds |
|---|---:|---:|
| Logo | `84,43,233,169` | `0.0502,0.0457,0.1394,0.1796` |
| Category navigation | `79,245,236,418` | `0.0472,0.2604,0.1411,0.4442` |
| Main panel | `337,181,678,689` | `0.2016,0.1923,0.4055,0.7322` |
| Right audio-test/profile card | `1200,489,391,373` | `0.7177,0.5197,0.2339,0.3964` |
| Bottom actions | `1109,867,482,37` | `0.6633,0.9214,0.2883,0.0393` |

Audio is selected. Six volume rows occupy the upper main panel; toggles and
drop-down rows occupy the lower panel. The right card is bottom-aligned rather
than shared with the graphics preview geometry. Values bind only through
`IAudioBackend`. The live correction removes the audio-test portion while
allowing compact truthful backend/profile status and uses the shared
`Apply / Reset / Cancel` row.

## 04 — Controls settings

| Region | Pixel bounds | Normalized bounds |
|---|---:|---:|
| Logo | `92,54,201,147` | `0.0550,0.0574,0.1202,0.1562` |
| Category navigation | `86,224,245,396` | `0.0514,0.2380,0.1465,0.4208` |
| Bindings panel | `357,119,643,741` | `0.2135,0.1265,0.3846,0.7875` |
| Mouse card | `1012,121,538,174` | `0.6053,0.1286,0.3218,0.1849` |
| Gamepad card | `1012,311,538,212` | `0.6053,0.3305,0.3218,0.2253` |
| Input preview card | `1012,545,538,314` | `0.6053,0.5792,0.3218,0.3337` |

Controls is selected. The table columns are Action/Primary/Secondary and rows
have approximately 38–39 px cadence. Rows must be sourced from stable Input
System action and binding IDs. Missing Inventory, Map, Journal and handbrake
actions remain visible but unavailable; they are not invented for the picture.
The live input-preview diagnostic is removed and the shared transaction row
applies to binding overrides as well as look/dead-zone settings.

## 05 — Gameplay settings

| Region | Pixel bounds | Normalized bounds |
|---|---:|---:|
| Logo | `45,35,194,145` | `0.0269,0.0372,0.1160,0.1541` |
| Category navigation | `46,217,223,395` | `0.0275,0.2306,0.1334,0.4198` |
| Main panel | `313,55,791,831` | `0.1872,0.0584,0.4731,0.8831` |
| Profile card | `1308,165,235,144` | `0.7823,0.1753,0.1406,0.1530` |
| Immersion card | `1308,318,235,204` | `0.7823,0.3379,0.1406,0.2168` |
| Summary card | `1308,532,235,266` | `0.7823,0.5654,0.1406,0.2827` |
| Bottom actions | `1134,836,460,50` | `0.6782,0.8884,0.2751,0.0531` |

The approved Gameplay image uses its own larger composition and omits the
greeting. The implementation follows the later user clarification in
`UI08A-DEV-013`: settings chrome remains invariant and the greeting is retained.
The background/vehicle corridor separates the main panel from three narrow
right cards in the source reference. The live correction removes the
profile/immersion/summary cards, retains truthful setting rows and uses the
shared `Apply / Reset / Cancel` transaction row.

## 06 — Default in-game HUD

| Region | Pixel bounds | Normalized bounds |
|---|---:|---:|
| Time and money | `30,37,196,165` | `0.0179,0.0393,0.1172,0.1753` |
| Needs panel | `30,226,196,270` | `0.0179,0.2402,0.1172,0.2869` |
| Typical need row | `40,234,176,42` | `0.0239,0.2487,0.1053,0.0446` |

The persistent HUD is restricted to the two upper-left blocks. Time/day/date,
money and six needs are the only permanent fields. Each need uses icon, label,
bar and numeric percentage, so meaning never depends on colour alone. No GPS,
minimap, objective tracker, vehicle telemetry, hotbar or permanent interaction
prompt is permitted.

## Responsive behaviour

- At the canonical target, measured pixel bounds are authoritative.
- Between 16:10 and 21:9, the canonical frame scales uniformly and remains
  centred; extra horizontal area belongs to the active project-owned backdrop.
- At narrower aspect ratios, safe-area clamping takes precedence over exact
  outer margins while panel proportions and order remain unchanged.
- User UI scale is applied inside the safe frame and is capped to prevent
  clipping; 100% is always used for review captures.
- The static menu plate is a separate full-canvas, aspect-preserved backdrop
  outside `UiScale`; lowering accessible UI scale cannot expose gameplay around
  the menu.
