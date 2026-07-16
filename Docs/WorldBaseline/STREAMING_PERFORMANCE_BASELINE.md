# Performance baseline donor world streaming 06B2

Дата capture: 2026-07-16

Unity: `6000.3.11f1`

Execution mode: `EditorPlayMode`

Graphics backend: `Null Device`

Profile: `donor-feature-parity-06b2`

Performance test: **1/1 PASS**

Evidence:

`PerformanceCaptures/Milestone06B2/M06B2_STREAMING_PERFORMANCE.json`

SHA-256:

`43e23efbfde282d52f274f6264729d5dd91492357d68dba05989419582f56863`

## 1. Startup

| Метрика | Результат |
|---|---:|
| Bootstrap ready | `900.15 ms` |
| Global scene loaded | `770.59 ms` |
| First focus cell loaded | `894.46 ms` |
| Initial owned scenes | `8` |
| Initial renderers | `389` |
| Initial colliders | `33` |

Runtime collider count `33` включает 32 baseline colliders и player
controller/collider.

## 2. Memory and prototype comparison

| Snapshot | Used memory | Reserved memory | Owned scenes | Renderers | Colliders |
|---|---:|---:|---:|---:|---:|
| Previous prototype fixture | `263.27 MiB` | `808.34 MiB` | `1` | `332` | `135` |
| Donor initial | `356.94 MiB` | `900.34 MiB` | `8` | `389` | `33` |
| Vehicle preload | `417.89 MiB` | `966.35 MiB` | `21` | `572` | `33` |
| Recovered after route | `418.70 MiB` | `971.35 MiB` | `10` | `452` | `33` |

Capture JSON explicit peak fields report:

- used memory: `417.89 MiB`;
- reserved memory: `966.35 MiB`.

However, the later `recovered` snapshot is slightly higher:

- used memory: `418.70 MiB`;
- reserved memory: `971.35 MiB`.

Therefore the conservative observed snapshot maxima are `418.70 MiB` and
`971.35 MiB`. The capture schema currently computes its explicit peak fields
before the recovered snapshot; 06B3 should clarify or correct that contract.

Разница с bounded prototype ожидаема: active donor profile содержит global
legacy aggregates и существенно больше streamed cells. Снижение collider count
объясняется explicit узким safe allowlist, а не потерей учтённых source
colliders.

## 3. Vehicle preload

Threshold: `12 m/s`.

Normal radius: `1`.

Vehicle preload radius: `2`.

Preload refresh: `84.33 ms`.

Owned scene count вырос с 8 до 21, что подтверждает расширенный соседний preload
для быстрого перемещения.

## 4. Representative transitions

| Focus position | Refresh | Max sampled frame | Owned scenes | Renderers | Used memory |
|---|---:|---:|---:|---:|---:|
| `(153.495, 10, -800)` | `17.22 ms` | `1.56 ms` | 10 | 428 | `359.24 MiB` |
| `(-1280, 10, 256)` | `311.76 ms` | `9.03 ms` | 10 | 1 058 | `407.75 MiB` |
| `(1792, 10, -1792)` | `35.10 ms` | `2.90 ms` | 8 | 166 | `406.29 MiB` |
| `(153.495, 10, -1280)` | `54.17 ms` | `5.20 ms` | 10 | 422 | `407.87 MiB` |

Наиболее дорогой measured refresh — `311.76 ms` в cell region с 1 058 active
renderers. Это candidate для будущего profiling/optimization, но broad art
optimization не входит в 06B2.

## 5. Frame and thread samples

| Метрика | Результат |
|---|---:|
| Frame samples | `4 851` |
| Mean sampled frame | `0.142 ms` |
| P95 | `0.169 ms` |
| Maximum | `9.202 ms` |
| Main Thread marker | available |
| Main Thread maximum | `9.148 ms` |
| Render Thread marker | unavailable |

Эти значения получены в batch/headless Editor PlayMode с `Null Device` и не
являются доказательством реального GPU frame rate.

## 6. Ограничения и следующий performance gate

- Standalone Development Player donor-profile capture не выполнялся; он относится
  к 06B3.
- Render Thread marker недоступен в текущем batch/headless backend.
- Large unsplit global static-batch aggregates доминируют resident memory.
- Explicit peak fields do not include the later recovered snapshot; the larger
  observed snapshot values are reported above.
- Capture измеряет streaming architecture и scene lifecycle, а не final HDRP
  graphics performance.
- Для финального решения нужны manual traversal и standalone 1080p capture на
  реальном graphics device.
