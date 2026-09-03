# Gameplay HUD visual direction — 2026-08-05

`2026-08-05_MinimalGameplayHUD_StyleReference.png` is a user-supplied visual
direction reference for the post-08A gameplay HUD refinement.

- Native size: `1672 x 941`.
- SHA-256: `38a29a13d399c77f5e3a8eae2eb883bf2db8016e9fb15f0aea10682c7a9aa7b6`.
- Runtime usage: forbidden; the PNG remains review/documentation input only.
- Authority: styling, visual weight, corner placement, thin need tracks,
  typography mood and amber accent treatment.
- Explicit exclusion: the illustrated speedometer/gear cluster is not part of
  the remake HUD. The physical vehicle dashboard remains authoritative.
- Project completion: the shipped HUD retains all six required needs — Thirst,
  Hunger, Stress, Urine, Fatigue and Dirtiness — even though the style image
  depicts only four. The Russian Dirtiness label is `НЕОПРЯТНОСТЬ`.
- Readability correction: project-owned text, icons, thin tracks, fills and
  amber indicators receive a restrained dark shadow so they remain readable
  over bright sky, water and roads.
- Direct layout correction: six needs form a vertical upper-left column; clock
  is upper-right with `value MK` money directly beneath it; type increases by
  only 1–2 px, need tracks widen from 124 to 144 px, and localized day/date
  fields share a baseline with a 4 px gap.
- Optional FPS treatment: no panel or line; pure-white numeric value followed by
  amber `FPS`, both at 17 px on one baseline and with the same restrained text
  shadow. The numeric field contains no suffix of its own.

The exact project-owned layout and data contract are recorded in
`Docs/UI/HUD_SPEC.md`. Do not crop, trace or bake pixels from this image into
runtime UI.

## Context, needs and subtitle direction — 2026-08-09

`2026-08-09_GameplayHUD_ContextNeedsSubtitles_StyleReference.png` is a second
user-supplied review-only direction image.

- Native size: `1672 x 941`.
- SHA-256: `5b71d658bbf2dc79ae7ad37241ddef5ecea9505bab5e452a7aeeb9f046111bda`.
- Runtime usage: forbidden; no pixels from the image are loaded by the game.
- Interaction authority: a target title above an optional action row; the two
  rows use separate compact, barely-black surfaces with a 1 px gap. The action
  binding is amber and the remaining copy is white. Placement follows the
  projected renderer/collider bounds of the current target and flips around
  viewport edges.
- Need authority: large pure-white icons, no numeric percentage text, a full
  amber-to-white gradient revealed by fill amount without horizontal squeezing,
  and a white endpoint dot slightly thicker than the track.
- Subtitle authority: bottom-centred white text in a compact translucent-black
  frame with wrapping for long dialogue.
- Explicit user corrections override literal pixels in the concept: title and
  action must not duplicate the item name; an absent action row is hidden; HUD
  Dirtiness remains the Russian `НЕОПРЯТНОСТЬ`; speedometer/gear remain excluded.

## Context action direction — 2026-09-02

`2026-09-02_ContextActionUI_StyleReference.png` is the current user-supplied
review-only authority for contextual actions. It supersedes only the
near-target prompt treatment from the 2026-08-09 image; that earlier image
remains authoritative for needs and subtitles.

- Native size: `1650 x 953`.
- SHA-256: `fc49b3f10833bbfd0926492512013240af559279d0b5af6e7fc0d08a8fef4971`.
- Runtime usage: forbidden; no reference pixels are loaded, cropped or traced.
- Reticle authority: a small white dot in the ordinary state, a compact filled
  open palm while pickup is valid, a white check while a carried item can be
  installed, and a white cross while an installed item can be removed. Action
  icons own a deliberately strong dark halo.
- Action authority: available actions appear only as separate compact
  translucent-black plaques in the lower-left safe frame. White copy and icons
  own a dark readability halo; the stack contains no more than three rows and
  disappears when empty.
- Superseded treatment: projected world-bound anchoring and the old two-row
  near-object prompt are removed. A target title may still appear in the new
  dynamic bottom-centre text stack; it never follows renderer bounds.
- Direct user additions override the literal image: an unplaceable carried item
  exposes `ЛКМ — ОТПУСТИТЬ`; signed mouse-wheel actions are shown for bolts and
  other scroll capabilities; holding Alt replaces the ordinary stack with the
  existing `H — ПОМАХАТЬ`, `M — ПОКАЗАТЬ ФАК` and `N — ВЫРУГАТЬСЯ` actions.
- Direct visual corrections also require a lightly rounded outline around every
  binding label; mouse glyphs fill the active left/right/middle control and add
  adjacent direction arrows for wheel scrolling. The target name and an active
  subtitle share a dynamic bottom-centre stack, with the smaller bold title
  above the larger regular subtitle.
- Intentional non-discoverability: there is no `ALT — ДОП. ДЕЙСТВИЯ` plaque or
  any other permanent Alt reminder.

The implementation uses independently licensed Lucide and Material Symbols
source icons plus project-owned rendering; the attachment remains documentation
evidence only.
