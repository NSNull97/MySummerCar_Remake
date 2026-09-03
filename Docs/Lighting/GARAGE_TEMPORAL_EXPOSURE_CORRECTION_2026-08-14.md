# Garage temporal exposure correction — 2026-08-14

> **Evidence correction (2026-08-15):** the 13:39, 19:48 and 22:59 images
> referenced below were captures from the donor game, not from the remake.
> They are valid visual targets for global time-of-day lighting, but they do
> not prove that the remake garage circuit or exposure changed by 32x. The
> causal interpretation in the original Evidence section is rejected. The
> current global calibration is documented in
> `DONOR_GLOBAL_LIGHTING_REFERENCE_2026-08-15.md`.

## Evidence

The donor game was compared in the same garage at 13:39, 19:48 and 22:59.
The electric circuit and three fixture intensities did not change, but the
generic clock-driven fixed exposure did:

- daylight and early dusk stayed close to the exterior daylight exposure;
- the generic indoor lift left the garage near `10.6 EV` at 19:48;
- by 22:59 the same room reached approximately `5.25 EV`;
- because one EV is one stop, the rendered response changed by more than 32x.

This made the garage nearly black at dusk and abruptly bright later at night.
Adding more lumens would only have hidden the exposure fault and overexposed
the night image.

## Rejected intermediate correction

A garage-only fixed target of `7.75 EV` removed the time-of-day jump, but the
rendered result was rejected: the room stayed visibly bright even with its
electrical circuit switched off. That treated exposure as a substitute for
electric illumination and broke the required switch feedback. The runtime
override and its bridge path were removed.

## Current correction

- garage and exterior use one authoritative camera exposure curve;
- dusk adaptation starts at 17:00 rather than 19:30 and reaches the accepted
  7.25 EV night floor at 22:30 rather than 23:00;
- ambient darkness subtracts at most 0.35 EV for readability instead of adding
  0.35 EV and making dark weather darker;
- garage fluorescents resolve to approximately 16.2 klm each, so switching them
  changes actual illumination;
- public street, exterior-building and home exterior fixtures use respectively
  9000, 3500 and 4000 lm;
- dusk-controlled exterior loads engage at an approximate solar elevation of
  12 degrees, before the rendered sky becomes black;
- the reviewed doorway transition, circuits, fuses, billing, saves, shadows and
  stable IDs remain unchanged.

## Validation

- the rejected fixed-EV fields and runtime branch are absent from project code
  and the serialized garage profile;
- active profile assets contain 9000/3500/4000 lm exterior photometry and the
  12/2 degree dusk/dawn thresholds; the deterministic builder declares the same
  values for future regeneration;
- direct Roslyn compilation passed for seven affected assemblies:
  Weather Production Runtime and EditMode tests, Lighting Runtime, Production
  Integration, Editor, EditMode tests and PlayMode tests;
- Unity generator and Test Runner execution are currently blocked before entry
  by unrelated concurrent compiler errors in `GarbageBarrelFirePresenter.cs`
  and `FirstPersonLifeActionPresenter.cs`; no current Test Runner PASS is
  claimed.

Manual rendered acceptance remains required at 13:39, 19:48 and 22:59 with the
garage switch both off and on, plus one exterior comparison around 19:30-20:00.

Classification: `BehavioralReference; Reimplemented`.
