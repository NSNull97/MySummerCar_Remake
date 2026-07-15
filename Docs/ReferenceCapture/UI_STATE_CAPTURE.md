# Checklist: UI, needs и game-state presentation

Reference capture only; не реализовывать финальный UI. Не читать/менять save для получения hidden values, если штатное поведение их не показывает.

## Needs/status variables

- [ ] Visible variable names/icons, apparent ranges and formatting.
- [ ] Update cadence from frame-accurate video; pause/menu behavior.
- [ ] Warning thresholds, hysteresis if observable, display priority and simultaneous warnings.
- [ ] State transition prerequisites and recovery behavior.

## Money/time/prompts

- [ ] Money sign, separators, rounding, negative/overflow presentation.
- [ ] Time/day formatting and update moment.
- [ ] Interaction prompt text, priority, range/aim behavior and blocked-state feedback.

## Vehicle/settings/save metadata

- [ ] Vehicle indicators and warning transitions tied to observable state.
- [ ] Settings category inventory and value presentation.
- [ ] Save-slot metadata visible through normal UI, failure/missing-save presentation.

## Acceptance

- [ ] 5 trials for short thresholds/prompts, 3 long observations for needs cadence.
- [ ] Pixel/color/font notes are observational unless measured with declared method.
- [ ] Hidden exact thresholds remain `Missing`; no decompiled label is treated as a value.
