# Milestone 08A default HUD specification

Status: `LockedCompositionImplemented / ProductionDataPartial / VisuallyApproved`

Reference authority:
`References/UI/Approved/08A/06_INGAME_HUD_APPROVED.png`  
Canonical viewport: `1672 x 941`, UI scale `100%`

The approved HUD reference overrides older generic guidance that hid exact
need percentages. The default 08A layout shows percentages, but only when an
authoritative provider exists. Missing production values are never invented.

## Persistent layout

| Block | Canonical bounds | Contents |
|---|---:|---|
| Time and money | `30,37,196,165` | day, time, date, money |
| Needs | `30,226,196,270` | six rows |
| Typical need row | `40,234,176,42` | icon, label, bar, percentage |

The two blocks are upper-left, share the same width and use a dark translucent
rounded glass surface with a semi-black tint. Row height is approximately
42-43 px. At 100% scale no other persistent block is allowed.

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
10. Dirtiness.

Each need row combines an original project icon, localized label, short fill
bar and numeric percentage. This redundant encoding supports
colour-independent reading.

## Data authority

| Field | Production authority | Current production behavior |
|---|---|---|
| time/day/date | `IGameTimeService.Snapshot` | live authoritative values; date/day derived consistently |
| money | future money/save provider | em dash plus currency suffix; no fake balance |
| six needs | future player-status provider | em dash and empty bar; no fake percentages |

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

- interaction affordance/crossdot while a candidate exists;
- save status;
- critical warning;
- bounded notification.

They are not part of the persistent locked HUD. Development diagnostic overlays
must be disabled by default and suppressed while menu UI is active.

## Forbidden persistent content

- GPS, minimap, route markers or waypoint distance;
- objective/quest tracker or task checklist;
- speedometer, tachometer, gear, fuel, coolant or battery overlays;
- permanent interaction prompt;
- inventory, hotbar or held-item strip;
- tutorial/headlight prompt;
- general notification clutter.

The vehicle's physical dashboard remains authoritative while driving.

## Scale and accessibility

- Canonical review uses 100% scale.
- User scale is applied to the shared safe-frame root.
- Critical meaning cannot rely on icon colour alone.
- Need labels and numeric values remain visible together.
- High-contrast token switching is not yet wired and remains pending.
- Screen-reader support is not part of the current Unity implementation.

## Update and performance contract

- HUD refresh runs only while `InGameHud` is active.
- Clock/date should change only when `IGameTimeService` revision changes.
- Future money/needs providers must expose revision/change signals; no polling
  allocation or per-frame view-model reconstruction is allowed.
- The two HUD blocks use stable dark translucent surfaces with no project-camera
  capture and no blur. The former recurring capture/downsample path was removed
  because it caused severe frame loss and visible stutter while rotating the
  camera.
- There is no minimap camera, telemetry graph or full-screen gameplay blur.

The current presentation still writes unavailable money/needs text during HUD
refresh. Final performance evidence must measure this path and may optimize it
without changing the visual contract.

## Acceptance checks

The HUD can be marked `ImplementationComplete` only when automated/static tests
confirm the exact categories, visible numeric percentage slots and absence of
forbidden permanent widgets, and a canonical implementation capture exists.
Only the user may mark it `VisuallyApproved` after reviewing the 50% blend; the
user completed that review and accepted the HUD on 2026-07-20.
