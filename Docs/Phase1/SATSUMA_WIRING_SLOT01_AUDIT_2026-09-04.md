# Satsuma wiring: slot-01 audit — 2026-09-04

## Scope and evidence

This is a read-only inspection of the native `slot-01` save requested by the
player. No save file, backup, connection, fastener, or installed part was changed.
The report describes the saved snapshot, not an observed live Editor session.

- Storage: `Application.persistentDataPath/Saves/Native/slot-01/current.save.json`.
- Domain inspected: `vehicle.satsuma`; electrical payload schema: `2`.
- Last-write time: `2026-09-04T06:20:05.2036887Z`, or `11:20:05` in
  Asia/Yekaterinburg.
- File size: `218445` bytes.
- SHA-256: `E43F87539CD2A587DF0EFF29E5DB6AC8F0BDCAD1A2663C7F73D67E746210AB10`.
- Timestamp, size, and hash were unchanged on the follow-up read before this
  report was written. No raw save payload is included in the repository.

Reference checks used the project-owned electrical connection definitions,
assembly lifecycle and mount records, and
`Phase1SatsumaElectricalStateAudit.csv` donor availability evidence. Donor
reference classification is `BehavioralReference`; this report does not claim
an executed full-game electrical comparison.

## Installed connections: 4 of 26

| Stable connection ID | Fixed pair |
|---|---|
| `BatteryHarness` | Battery positive terminal ↔ main harness |
| `FrontLightsHarness` | Main harness ↔ front lights connector |
| `Ignition` | Fusebox ↔ ignition switch |
| `RegulatorHarness` | Main harness ↔ voltage regulator |

All four IDs identify valid donor pairs. There are no duplicate or unknown
connection IDs, orphan electrical fastener stages, or contradictions with the
required installed battery and steering column in this snapshot.

**Valid topology does not prove intended player actions.** The earlier
one-press implementation could install multiple eligible connections around a
connector. This save contains final connection flags, not an action history;
there is no evidence to determine which of the four wires the player intended
to install. No automatic rollback or deletion is justified by this audit.

## Terminals and power

| State | Saved value |
|---|---|
| Positive battery terminal | `8/8` |
| Negative battery terminal | `0/8` |
| Starter positive cable fastener | `0/8` |
| Battery charge | `1.0` normalized |
| Battery voltage | approximately `12.6 V` |
| Wiper mode | Off, no active sweep |

The negative and starter cable stages are consistent with those wires being
absent. The positive stage is consistent with the installed `BatteryHarness`.
The electrical circuit is not complete: battery ground is absent. The
`SwitchLights` connection needed by the current wiper power contract is also
absent. A charged battery alone does not make this snapshot electrically ready.

## Missing connections: 22

The following **14 stock circuits** are absent. This grouping includes the
stock radio circuit; it does not imply that radio installation is required to
start or drive the car.

| Stable connection ID | Fixed pair |
|---|---|
| `Alternator` | Alternator ↔ regulator |
| `CoilHarness` | Ignition coil ↔ main harness |
| `Dash1` | Instrument panel 1 ↔ fusebox |
| `Dash2` | Instrument panel 2 ↔ fusebox |
| `GroundBattery` | Negative battery terminal ↔ starter ground connector |
| `FuelTank` | Fuel tank ↔ rear harness |
| `HeadlightLeft` | Left headlight ↔ front lights connector |
| `HeadlightRight` | Right headlight ↔ front lights connector |
| `RadiatorFan` | Radiator fan ↔ main harness |
| `Radio` | Radio ↔ radio harness |
| `RearlightLeft` | Left rear light ↔ rear harness |
| `RearlightRight` | Right rear light ↔ rear harness |
| `Starter` | Starter ↔ positive battery terminal |
| `SwitchLights` | Light switch ↔ dash harness |

The following **8 accessory circuits** are also absent:

- `AmplifierPower` and `AmplifierAudio`;
- `MarkerLeft` and `MarkerRight`;
- `GaugeAfr` and `GaugeExtra`;
- `SubwooferLeft` and `SubwooferRight`.

Those accessories are not installed in this snapshot. Their absent wires are
not a defect and they are not mandatory prerequisites for basic wiring.

## Why endpoints are unavailable in this snapshot

The following part records are `Loose` with empty installed mount IDs:

- engine block, starter, alternator, and `electrics`/ignition coil assembly;
- radiator;
- fuel tank;
- both headlights and both rear lights;
- dashboard itself.

Thus the matching component-side wiring endpoints are not expected to be
available just because a harness-side junction exists.

The dashboard is especially important:

| Part | Saved assembly state |
|---|---|
| `dashboard` | Loose; `mount.satsuma.dashboard` is empty |
| `dashboard-meters` | Installed on `mount.satsuma.dashboard.meters` |
| `radio` | Installed on `mount.satsuma.dashboard-meters.radio` |

The meters and radio are a partial subassembly attached to a dashboard that is
not installed in the car. Donor `Wiring/Status/Dash` checks both dashboard and
dashboard meters; the `Dash1`, `Dash2`, and `SwitchLights` component endpoints
are therefore correctly unavailable in this saved state.

One implementation limitation needs separate verification: the inspected
`IsPartDefinitionInstalled` checks a part's own installed lifecycle, not its
entire ancestry to the chassis. The then-current Radio endpoint prerequisite
could therefore pass for the radio on this loose dashboard while the endpoint
remained at a fixed car position. Ordinary Radio availability is not explicitly
gated by the donor `Wiring/Status` rows examined here, unlike the documented
dashboard/CD-player gate for amplifier audio. This audit alone does **not**
establish exact donor parity for that Radio edge case.

## Compact player checklist

- Уже сохранены четыре допустимых провода: плюс АКБ — основной жгут;
  основной жгут — передний свет; блок предохранителей — замок зажигания;
  основной жгут — регулятор.
- Сама торпеда пока снята, хотя приборка и магнитола закреплены на ней.
  После установки торпеды станут доступны предусмотренные точки приборки и
  переключателя света.
- Для моторных концов ещё нужны установленные блок, стартер, генератор и
  катушка; для остальных концов — соответствующие радиатор, бак и фонари.
- Масса АКБ ещё не подключена. Полностью затянутый плюс и заряженный аккумулятор
  не означают, что цепь уже собрана.
- Ненужные дополнительные приборы, габариты, усилитель и сабвуферы можно не
  устанавливать и не подключать.
- Сохранение не менялось. Какие провода достроил старый баг, а какие были
  сознательно выбраны, по этому файлу восстановить нельзя.

## Verification boundaries

Executed: bounded JSON parsing, electrical ID/stage checks, assembly lifecycle
and mount correlation, donor availability-row inspection, and repeat file
metadata/hash checks. This audit did not load the save in Unity, exercise
interactive targeting, simulate electrical consumers, or alter existing save
state. The interaction correction and its tests are tracked separately from
this snapshot report.
