# WeatherLab 07B — performance evidence

Дата: 2026-07-17. Статус: `AUTOMATED_EDITOR_BASELINE_CAPTURED / PRODUCTION_SIGN_OFF_PENDING`.

Целевой профиль: Windows x64, 1920x1080, стабильные 60 FPS на mid-range PC. WeatherLab measurement не подтверждает production-world budget.

Актуальный artifact: `PerformanceCaptures/Milestone07B/M07B_WeatherLab_Performance.json`. Среда: Unity `6000.3.11f1`, Editor PlayMode batch, D3D11, RTX 4070 SUPER, `640x480`, по 15 warmup и 40 sampled frames. Значения нельзя переносить на 1080p development build.

| Режим | Frame mean / p95, ms | Main max, ms | Render / GPU | GC max, B/frame | Memory, MiB | Статус |
|---|---:|---:|---|---:|---:|---|
| Domain без Enviro presentation | — | — | — | — | — | UNAVAILABLE: публичный WeatherLab path всегда синхронизирует presentation |
| Clear High | 0.337 / 0.574 | 0.868 | unavailable / unavailable | 410 | 500.3 | AUTOMATED_EDITOR_BASELINE |
| Overcast High | 0.370 / 0.543 | 0.880 | unavailable / unavailable | 410 | 500.3 | AUTOMATED_EDITOR_BASELINE |
| Rain High | 0.361 / 0.539 | 0.837 | unavailable / unavailable | 410 | 500.3 | AUTOMATED_EDITOR_BASELINE |
| Heavy rain High | 0.344 / 0.552 | 0.826 | unavailable / unavailable | 410 | 500.3 | AUTOMATED_EDITOR_BASELINE |
| Storm/lightning High | 0.437 / 0.773 | 1.388 | unavailable / unavailable | 1064 | 500.4 | AUTOMATED_EDITOR_BASELINE |
| Fog/mist High | 0.384 / 0.555 | 0.728 | unavailable / unavailable | 656 | 500.4 | AUTOMATED_EDITOR_BASELINE |
| Night High | 0.401 / 0.580 | 0.685 | unavailable / unavailable | 470 | 500.4 | AUTOMATED_EDITOR_BASELINE |
| Wetness/puddles High | 0.359 / 0.652 | 0.724 | unavailable / unavailable | 410 | 500.4 | AUTOMATED_EDITOR_BASELINE |
| Clear Low | 0.338 / 0.487 | 0.925 | unavailable / unavailable | 430 | 500.4 | AUTOMATED_EDITOR_BASELINE |
| Clear High recovery | 0.395 / 0.616 | 0.730 | unavailable / unavailable | 410 | 500.4 | AUTOMATED_EDITOR_BASELINE |

## Требуемые артефакты

Рекомендуемый корень:

```text
PerformanceCaptures/Milestone07B/
  M07B_WeatherLab_Performance.json
  M07B_WeatherLab_Profiler.data              # PENDING
  Captures/                                  # optional automated screenshots
```

Отдельно сохранить diagnostics `Logs/M07B_TimeWeatherDiagnostics.json` и 07B screenshots под `References/Weather/Milestone07B/`.

Отдельная assembly `MSC.WeatherLab.Tests.PlayMode` использует `ProfilerRecorder`, frame sampling и явные availability flags. Render Thread/GPU markers в этом прогоне не дали samples и потому не заменены нулями. Subsystem decomposition clouds/fog/precipitation/reflections/adapter/domain/wetness/lightning и `.data` capture остаются для development-build sign-off.

## Bounded cadence, который нужно проверить

- presentation обновляется с ограниченной частотой и по revision;
- unchanged values не пишутся каждый кадр;
- reflection/environment refresh выполняется только по sequence/cadence;
- puddle ripple MPB обновляется максимум 12 Гц и не создаёт material instances;
- precipitation intensity update не перезапускает текущий binding;
- runtime lightning переиспользует один owned flash material.

Числа не экстраполируются из Editor или 07A. Automated harness — воспроизводимый regression baseline, но не доказательство 60 FPS, GPU budget или production-world стоимости. Эти ограничения остаются явным validation debt для 07C.
