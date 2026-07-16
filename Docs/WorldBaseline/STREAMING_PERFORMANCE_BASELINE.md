# Performance baseline donor world streaming 06B2

Дата capture: 2026-07-16

Unity: `6000.3.11f1`

Execution mode: `EditorPlayMode`

Graphics device: `NVIDIA GeForce RTX 4070 SUPER`

Profile: `donor-feature-parity-06b2`

Performance test: **1/1 PASS**

Evidence:

`PerformanceCaptures/Milestone06B2/M06B2_STREAMING_PERFORMANCE.json`

`TestResults/M06B2V51_PerformancePlayMode06_WaterFix.xml`

`Logs/M06B2V51_PerformancePlayMode06_WaterFix.log`

SHA-256:

`60868e5862413b59df0adad2dee998617145b96de2306494a7bbd6e60e3ba271`

## 1. Startup

| Метрика | Результат |
|---|---:|
| Bootstrap ready | `1 068.26 ms` |
| Global scene loaded | `818.39 ms` |
| First focus cell loaded | `1 060.62 ms` |
| Initial owned scenes | `8` |
| Initial renderers | `389` |
| Initial colliders | `33` |

Runtime collider count `33` включает 32 baseline colliders и player
controller/collider.

## 2. Memory and prototype comparison

| Snapshot | Used memory | Reserved memory | Owned scenes | Renderers | Materials | Textures | Texture memory | Instances |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Previous prototype fixture | `282.88 MiB` | `734.34 MiB` | `1` | `332` | `0` | `0` | `0 MiB` | `0` |
| Donor initial | `602.30 MiB` | `1 040.35 MiB` | `8` | `389` | `149` | `145` | `396.25 MiB` | `0` |
| Vehicle preload | `732.64 MiB` | `1 173.36 MiB` | `21` | `572` | `263` | `217` | `502.88 MiB` | `0` |
| Recovered after route | `732.58 MiB` | `1 177.36 MiB` | `10` | `452` | `263` | `217` | `502.88 MiB` | `0` |
| Recovered after repeated warmed route | `732.48 MiB` | `1 177.36 MiB` | `10` | `422` | `263` | `217` | `502.88 MiB` | `0` |

Capture JSON explicit peak fields report:

- used memory: `732.64 MiB`;
- reserved memory: `1 177.36 MiB`.

Материалы и текстуры считаются по уникальным generated asset instance IDs.
`runtimeMaterialInstanceCount=0` во всех snapshots подтверждает отсутствие
непреднамеренных `(Instance)` clones после preload и возврата. Повторный
прогретый маршрут сохранил plateau `263` materials / `217` textures /
`502.88 MiB` texture estimate без роста.

Разница с bounded prototype ожидаема: active donor profile содержит global
legacy aggregates и существенно больше streamed cells. Снижение collider count
объясняется explicit узким safe allowlist, а не потерей учтённых source
colliders.

## 3. Vehicle preload

Threshold: `12 m/s`.

Normal radius: `1`.

Vehicle preload radius: `2`.

Preload refresh: `95.46 ms`.

Owned scene count вырос с 8 до 21, что подтверждает расширенный соседний preload
для быстрого перемещения.

## 4. Representative transitions

| Focus position | Refresh | Max sampled frame | Owned scenes | Renderers | Materials | Textures | Used memory |
|---|---:|---:|---:|---:|---:|---:|---:|
| `(153.495, 10, -800)` | `27.01 ms` | `3.00 ms` | 10 | 428 | 172 | 150 | `609.54 MiB` |
| `(-1280, 10, 256)` | `404.40 ms` | `11.20 ms` | 10 | 1 058 | 246 | 201 | `702.74 MiB` |
| `(1792, 10, -1792)` | `44.79 ms` | `2.98 ms` | 8 | 166 | 249 | 204 | `702.79 MiB` |
| `(153.495, 10, -1280)` | `52.46 ms` | `3.86 ms` | 10 | 422 | 249 | 204 | `704.58 MiB` |

Наиболее дорогой measured refresh — `404.40 ms` в cell region с 1 058 active
renderers. Это candidate для будущего profiling/optimization, но broad art
optimization не входит в 06B2.

## 5. Frame and thread samples

| Метрика | Результат |
|---|---:|
| Frame samples | `6 265` |
| Mean sampled frame | `0.170 ms` |
| P95 | `0.190 ms` |
| Maximum | `11.259 ms` |
| Main Thread marker | available |
| Main Thread maximum | `11.201 ms` |
| Render Thread marker | unavailable |

Эти значения получены в batch Editor PlayMode и не являются доказательством
standalone 1080p GPU frame rate.

## 6. Ограничения и следующий performance gate

- Standalone Development Player donor-profile capture не выполнялся; он относится
  к 06B3.
- Render Thread marker недоступен в текущем batch/headless backend.
- Large unsplit global static-batch aggregates доминируют resident memory.
- Capture измеряет streaming architecture и scene lifecycle, а не final HDRP
  graphics performance.
- Manual traversal 06B2 принят пользователем. Следующий performance gate —
  standalone 1080p capture на реальном graphics device в рамках 06B3.
