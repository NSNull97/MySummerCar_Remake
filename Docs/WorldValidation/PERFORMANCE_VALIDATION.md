# Performance validation — Milestone 05B.1

Дата: 2026-07-15
Итог: **bounded current-world standalone capture PASS; полный performance gate не закрыт**.

## Среда

- Windows x64 Development Player;
- Unity `6000.3.11f1`, HDRP `High Fidelity`;
- `1920x1080`, windowed, D3D12;
- AMD Ryzen 9 5950X, 32 logical cores;
- NVIDIA GeForce RTX 4070 SUPER, 11 999 MB device capacity;
- 32 686 MB system RAM;
- VSync `0`, target frame rate `-1`;
- 120 warmup + 300 measured frames на location.

Measured frames относятся к обычному видимому player backbuffer. После них один
HDRP `StandardRequest` рендерит verification PNG в отдельный ARGB32 sRGB
RenderTexture; этот sync request не входит в 300 measured frames.

## Результаты

| Location | Mean / p95 / worst, ms | CPU p95, ms | GPU p95, ms (samples) | Draw / Batches / SetPass mean |
|---|---:|---:|---:|---:|
| `pilot-home` | `3.014 / 3.620 / 3.894` | `3.430` | `3.329` (`239`) | `225.97 / 168.96 / 26.98` |
| `dense-vegetation` | `3.223 / 3.863 / 4.266` | `3.669` | `3.475` (`261`) | `223.03 / 193.02 / 27.99` |
| `interior-transition` | `3.296 / 4.003 / 4.345` | `3.737` | `3.444` (`284`) | `231.97 / 214.95 / 30.99` |
| `water-shoreline` | `3.131 / 3.803 / 4.001` | `3.570` | `3.442` (`259`) | `57.00 / 45.00 / 28.00` |

Все p95 ниже `16.667 ms`, но это только предварительный bounded steady-state
индикатор одной машины. Он не гарантирует стабильные 60 FPS полной будущей игры.

Четыре PNG:

- существуют и визуально содержат production geometry;
- имеют 4/4 уникальных SHA-256;
- имеют 4/4 уникальных sampled signatures;
- прошли thresholds: RGB buckets `>=6`, dominant bucket `<=0.985`, luminance range `>=0.05`.

Interior кадр очень тёмный, но не одноцветный; lighting readability остаётся
отдельной задачей. Production white-clipping defect исправлен согласованно:
`FoundationSceneBuilder` и `BootstrapGlobalVolume` используют fixed exposure EV100
`14` при directional sun `100000 lux`. User-owned `M3_NeutralVolume.asset` не менялся.

## Evidence и provenance

- raw ignored capture: `PerformanceCaptures/Milestone05B1/WorldPilot_1080p.json`;
- accepted machine summary: `Docs/WorldValidation/M05B1_WORLD_PERFORMANCE_EVIDENCE.json`;
- методика: `Docs/Performance/MILESTONE_05B1_PILOT_CAPTURE.md`;
- capture schema: `Docs/Performance/MILESTONE_05B1_PILOT_CAPTURE.schema.json`.

Player завершился `exit 0`, log содержит `M05B1_WORLD_PERFORMANCE_CAPTURE_OK`,
JSON — `completed: true`. В момент принятия provenance comparison был `53/53`,
`0 mismatches`, revision `53861b52ac6f8d9308f3e6f315c973a390210cd2`.
Durable reader закрепляет raw JSON/log/PNG hashes, hardware, resolution, method,
positive GPU/Draw/Batches/SetPass и четыре accepted location IDs.

## Что недоступно

- resident/allocated VRAM usage;
- isolated Present cost;
- `Physics.Processing` profiler counter;
- чистый streaming hitch и peak/recovered memory;
- `road-driving-speed`;
- полный `vertical-slice-route` и destination/service zone;
- повторные captures на целевом диапазоне hardware.

`loadTransitionMaximumFrameTimeMilliseconds` diagnostic-only: следующий delta
мог включить sync verification PNG предыдущей точки. Он не закрывает
`WORLD-STREAM-003`.

## Findings

- `WORLD-PERF-001`: **Closed** — текущий standalone bounded capture существует и fingerprinted;
- `WORLD-PERF-002`: **Open** — полный metric/streaming set отсутствует;
- `WORLD-PERF-003`: **Open** — road-at-speed и vertical-slice route content отсутствуют.

Performance не блокирует `PilotGate`, но оставшиеся два finding блокируют
`VerticalSliceGate` и `FullWorldGate`.
