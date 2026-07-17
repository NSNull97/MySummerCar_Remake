# Enviro 3 presentation bindings — Milestone 07B

Дата среза: 2026-07-17.

## Локальная версия и fingerprint

Точный semantic patch пакета неизвестен; честное обозначение — hash-identified Enviro 3.x. В 07A был принят baseline `538 / 305967970 / 9a4e8bab6bdf231c415fc8e60f3988f12f221cc20dfbfc0b7090feb14f431af3`.

Принятый 07B baseline после canonical Unity 6 reserialization: `538 / 305967931 / 8e376fa2748162157975fbdd8b1045e021b810fea40f01d99eafe22a4d30bd44`. Он повторно совпал после обновлённых runtime tests; fresh-process builder/preflight завершился `PASS`, а validator constants мигрированы явно. Цепочка доказательств описана в `ENVIRO3_FINGERPRINT_MIGRATION_AUDIT_07B.md`.

## Typed binding и runtime isolation

`Enviro3EnvironmentBindings` хранит typed asset references; domain хранит только stable binding IDs. Для четырёх precipitation bindings adapter создаёт отдельные owned runtime-клоны:

- `weather.drizzle` -> clone `Rain.asset`;
- `weather.rain` -> отдельный clone `Rain.asset`;
- `weather.heavy_rain` -> отдельный clone `Rain.asset`;
- `weather.storm_visual` -> clone `Storm.asset`.

На каждом кадре с новой revision adapter находит ровно один exact effect override с именем `Rain` и dirty-only обновляет его `emission` из `EnvironmentPresentationFrame.PrecipitationIntensity01`. Изменение intensity при том же binding не перезапускает weather transition. Source `Rain.asset`/`Storm.asset` не мутируются; отсутствие или неоднозначность exact `Rain` override является hard failure.

Clear/partly/overcast/fog используют прямые typed assets. Полная матрица — `ENVIRO3_BINDING_MATRIX.csv`.

## Time, transition и refresh

- Enviro autonomous time и zone schedule выключены;
- project time передаётся только вперёд через public API;
- automatic front duration до adapter переводится из game seconds в simulation seconds по активной скорости clock; coarse DEV jump использует bounded minimum `0.25 s` smooth path;
- duration `D > 0` отображается в Enviro exponential blend с `k = -ln(0.01) / D`, то есть приблизительно 99% convergence, а не точный finite-duration progress;
- `D = 0` использует instant path;
- time-only revision и intensity-only revision не должны перезапускать weather binding;
- environment refresh выполняется только по новой sequence и bounded cadence, не каждый кадр.

## Lightning и audio

Autonomous Enviro lightning и weather audio отключены. Adapter клонирует paid `LightningStrike.prefab` под inactive runtime host, создаёт один owned runtime flash material и передаёт Enviro только этот клон. Ambient visual request использует project-selected position/sequence. Source prefab и source material остаются read-only; vendor thunder временно отсоединяется на время visual call.

Intensity bolt — bounded approximation через цвет runtime flash material; vendor line/plane/light curves сохраняют собственные authored curves. Gameplay strike point, fairness, thunder delay и effect hooks принадлежат project domain.

## Quality и diagnostics

Low/High сохраняют сериализованные значения enum `0/1`. Medium не добавляется в 07B.

DEV окно: `Tools > MSC Remake > Time and Weather` (WeatherLab Play Mode). Domain diagnostics экспортируются в `Logs/M07B_TimeWeatherDiagnostics.json`; screenshot — в `References/Weather/Milestone07B/M07B_WeatherLab_<timestamp>.png`. Enviro preflight остаётся под `Tools > MSC Remake > Enviro 3 Preflight`.

Диагностика обязана явно сообщать: detached/startup barrier, missing manager/module/camera/binding, duplicate owner, stale/invalid revision, invalid frame, runtime clone/isolation failure, отсутствующий exact `Rain` override, missing runtime lightning prefab/material, unsupported capability и vendor fingerprint mismatch. Silent fallback на display-name lookup запрещён.
