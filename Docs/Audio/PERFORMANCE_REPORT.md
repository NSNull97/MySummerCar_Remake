# Milestone 08 audio performance report

Status: **AutomatedValidated / UserAcceptedBaseline / WwiseProfilerPolishDeferred**  
Report date: **2026-07-18**

## Capture

The automated capture is
`PerformanceCaptures/Milestone08/M08_Audio_Performance.json`. It ran in Unity
Editor PlayMode batch with `-wwiseEnableWithNoGraphics`, Wwise SDK
`2025.1.9.9197`, backend `wwise.official.2025.1.9.4241`, all six banks loaded,
and passed **1/1** after remediation in
`Logs/M08_Remediation_AudioPerformance.xml`.

This mode validates routing, counts, cleanup, timing and allocations. It does
not validate audible output or Wwise audio-thread CPU because Wwise suspends
the output thread in headless batch mode.

## Measured results

| Metric | Result |
|---|---|
| Registered emitters | baseline `2`, peak `3`, cleanup `2` |
| Active voices | baseline `0`, peak `5`, cleanup `0` |
| Global RTPC write | `0.782 us/iteration`, 512 iterations, `0 B` allocated |
| Vehicle parameter batch | `17.906 us/iteration`, 64 iterations, `0 B` allocated |
| Weather parameter batch | `2.930 us/iteration`, 128 iterations, `0 B` allocated |
| Occlusion raycast budget | `0` per frame; no per-emitter query system exists |
| Loaded banks | `6` (`Init` plus five user banks) |
| Generated Windows bank bytes | `26,115,951 B` |
| Unity allocated memory | `340,423,612 B` before Bootstrap to `866,855,152 B` after capture |
| Batch process working set | unavailable (`0` reported by Unity batch environment) |
| External built-player working set | not remeasured; prior pre-remediation smoke observed `454,602,752 B` |
| Wwise audio-thread CPU | unavailable in headless batch; manual Wwise Profiler required |

The Unity allocation delta includes Bootstrap, Enviro and the streamed donor
world. It is not attributed wholly to audio and is not an audio leak claim.
Emitter/voice cleanup returned exactly to the measured baseline.

## Bank footprint

| Bank | Bytes | SHA-256 |
|---|---:|---|
| `Init.bnk` | 1,877 | `86CA0EFC46DC6878DAB0619F8611B76B3BA939980BBF5B566352D1ED90EEFBF9` |
| `MSC_Interaction.bnk` | 1,179,302 | `009D7A6E9EFCA2E3750EA8B07412F10203798256A26860DF3A659CCCED64B5D8` |
| `MSC_UI.bnk` | 145 | `F282D401C047128722E5A902FB3F35CFE3E3A23B65529492FC1A1FD9E074E1E1` |
| `MSC_Vehicle.bnk` | 1,992,969 | `39A777154F4FE0D6C7A2DD5A978A35C32D45EFBF14F3ACF3F1313321350CF2D8` |
| `MSC_Weather.bnk` | 12,554,129 | `1B598E9165D4521718CE8A03D1D2ACD92C4C1A723F6CED11B9DD50ED3D8D44B3` |
| `MSC_World.bnk` | 10,387,529 | `273F47B925EBFF41D86B3CB6594F860469C97136D9485BD0CFB1F2536D530B5E` |
| **Total** | **26,115,951** | per-file hashes above |

Banks embed 30 prototype media objects and contain no loose `.wem` payload.
They are generated/ignored and are not committed.

## Build and native smoke

The post-remediation private Windows x64 Development build passed in
`Logs/M08_Remediation_Windows64_DevelopmentBuild2.log`:

- Unity build report size: `844,040,197 B`;
- complete build folder size: `844,257,747 B`;
- copied bank files match the source generated-bank hashes;
- native player boot log `Logs/M08_Remediation_PlayerSmoke.log` records Wwise
  SDK `2025.1.9.9197` and `Sound engine initialized successfully`; the process
  is stopped after the bounded initialization marker.

This proves native initialization and bank packaging, not audible mix quality.

## Interpretation

- Stable RTPC and domain-update paths allocated `0 B` in the bounded harness.
- Peak voices stayed at five and emitter/voice lifecycle returned to baseline.
- The current occlusion budget is deliberately zero; obstruction/reverb remain
  data hooks until a measured queued system is approved.
- The largest prototype banks are Weather and World because they contain local
  temporary donor media. Their size is not a production content budget.
- Full EditMode is **350/356**, with exactly six unrelated Garage/World
  baseline failures. It is not reported as a full-suite pass.
- Full PlayMode is **68/70**: one unrelated
  `VehiclePhysicsValidation.ProductionWorld_GarageExit...` failure and the
  performance case skipped by explicit environment design. The same performance
  case passes **1/1** in its explicit enabled run. The exact failed production
  vehicle-route case passes **1/1 in 8.696 s** when rerun in isolation, making
  the full-suite result order-dependent/flaky and outside audio scope
  (`Logs/M08_Final_UnrelatedVehicleRoute_Rerun.xml`).

## Manual limitation

The user accepted the bounded Milestone 08 audio baseline on 2026-07-18.
Real-device Wwise audio-thread CPU, starvation, virtualization, speaker/output-
device behaviour, detailed zone tuning and final mix remain later Wwise
Profiler/listening work. The acceptance closes this milestone but does not
claim final production audio or mix approval.
