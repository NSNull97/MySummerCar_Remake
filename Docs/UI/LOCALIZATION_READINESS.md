# Milestone 08A localization readiness

Status: `FoundationPartial / ProductionLocalizationPipelinePending`
Date: `2026-07-19`

## Current implementation

The UI has four related contracts:

- `UiLocalizationKeys` exposes a stable, prefixed subset used by runtime
  contracts and tests;
- `UiTextCatalog` resolves presentation IDs to English or Russian text;
- `UiLocaleFormatter` supplies culture-aware composite formatting and bounded
  English/Russian plural rules with a truthful `Other` fallback;
- `UiFontResolver` selects an installed Windows condensed/readable family and
  falls back explicitly to Unity `LegacyRuntime.ttf`.

`GameplaySettingsDto.LanguageId` persists the selected locale. The default is
`ru-RU`; validation accepts the bounded `ru-RU` and `en-US` locale IDs and
repairs unsupported/corrupt values instead of silently inventing a locale.
Missing presentation IDs return the ID itself so a missing key is visible
instead of becoming blank.

Development comparison mode intentionally forces `en-US` to match the approved
reference language and rebuilds localized routes. This affects review captures
only and does not overwrite persisted player settings.

## Key conventions

- IDs begin with `ui.`.
- Category and purpose are dot-separated, for example
  `ui.gameplay.camera_shake`.
- User-facing capability reasons use localization IDs rather than raw exception
  text.
- New screens must add the ID before binding the widget.
- Object names, action GUIDs and internal debug diagnostics are not
  localization keys.

## Current locale coverage

| Area | English | Russian | Status |
|---|---|---|---|
| Main menu | present | present | foundation complete, visual review pending |
| Locked settings shell/pages | present for current rows | present for current rows | presentation literal-key source audit passes |
| Loading/pause/confirmation/save status | present | present | bounded `ReferencePending` layouts captured |
| HUD labels | present | present | production data providers partial |
| Capability reasons | present for bounded rows | present for bounded rows | source contract covered |
| Credits/free-form helper copy | present for bounded screen | present for bounded screen | final credits content pending |
| Input action/display names | localized bounded mapping plus safe unknown-device fallback | localized bounded mapping plus safe unknown-device fallback | unknown future device strings remain fallback debt |
| Composite/number/plural formatting | `UiLocaleFormatter` | `UiLocaleFormatter` | foundation covered by six EditMode tests |
| Day/month/date formatting | locale formatter wired to HUD | locale formatter wired to HUD | bounded 08A integration complete |

## Known hard-coded presentation debt

The current bounded presentation still contains a small set of project-owned
non-player-facing or art literals such as logo words, version prefix and
development review-state labels. Unknown future Input System device names use
a truthful raw-name fallback. These are not donor/reference pixels, but the
remaining player-facing cases must move behind localization IDs before a
project-wide production localization pass is complete.

## Missing production capabilities

- no Unity Localization string-table package/workflow;
- plural and composite formatting exist for English/Russian, but select/gender
  and project-wide date/currency integration remain pending;
- no pseudo-localization or string-expansion test;
- source-level validation covers literal `UiTextCatalog.Get` IDs, but no
  production string-table completeness/export validation exists;
- the Windows-first font resolver validates the complete bounded
  Latin/Cyrillic glyph set before selection and falls back to Unity
  `LegacyRuntime.ttf`; there is still no approved bundled production font
  asset, and non-Windows presentation remains a later platform concern;
- no translator context, screenshots or extraction/export format.

These gaps are honest localization readiness debt, not blockers to the bounded
English/Russian 08A review.

## Required validation before production localization

1. Extract every presentation ID and reject unknown/duplicate keys.
2. Move remaining user-facing literals into the catalog/string-table source.
3. Extend the existing locale-aware date/time/decimal formatter with the future
   save/economy currency contract when that data exists.
4. Extend the existing English/Russian plural formatter with select/gender only
   when real copy requires it; do not concatenate translated fragments.
5. Bundle and validate the approved production font/fallback chain on every
   supported platform; the bounded Windows runtime glyph check remains the
   interim guard.
6. Run pseudo-localization at +30-40% text expansion on every route and supported
   aspect ratio.
7. Record truncated/overflowing fields and fix them without changing locked
   canonical hierarchy.
8. Verify review mode does not mutate persisted locale.

## Reference lock and semantic corrections

The approved English screenshots define visual slots, not text pixels. Obvious
AI artifacts or impossible data are corrected semantically (`Map`, not `Mop`;
real capability labels, not fabricated device/profile data). Translation must
preserve function and hierarchy while allowing the normal expansion required by
Russian and future locales.
