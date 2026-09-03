# Checklist: time, weather и environment

Наблюдать только естественные состояния. Не использовать console injection или save mutation ради погоды.

## Time P0/P1

- [x] Donor clock cadence extracted from the read-only `SUN/Clock` PlayMaker
  FSM in the locked GAME scene: `MinutesAdd = 0.2` game minutes per real
  second and `TimeScale = 300` real seconds per game hour. This yields
  7,200 real seconds (120 minutes) per complete game day at scale 1.
- [ ] 3 наблюдения не менее 30 real minutes: visible game clock versus monotonic timer.
- [ ] Game/real-time ratio, display granularity, pause/menu and reload behavior.
- [ ] Sunrise/sunset, day length and scheduled discontinuities with date/session context.

## Weather P0/P1

- [ ] Fixed outdoor camera with sky, distance landmark and ground reference.
- [ ] Natural clear/overcast/rain/fog onset and decay; 3 transitions where feasible.
- [ ] Transition durations, intensity bands, visibility, fog and wind observations.
- [ ] Wetness onset/persistence/drying lag relative to precipitation.
- [ ] Indoor/outdoor exposure and ambient color under named lighting conditions.

## Acceptance

- [ ] Timing uncertainty includes clock granularity and capture fps.
- [ ] Subjective color/visibility notes remain observations unless instrumented.
- [ ] Rare state not observed is `Missing`, never synthesized or guessed.
