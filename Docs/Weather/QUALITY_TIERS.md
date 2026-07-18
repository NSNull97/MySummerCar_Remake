# Production weather quality tiers

Дата среза: 2026-07-18.

Статус: `LOW/MEDIUM/HIGH BINDING PASS / DEFAULT MEDIUM / PRODUCTION PERFORMANCE PENDING`.

## Project-owned contract

UI и другие consumers должны работать только со stable IDs:

| Tier | Serialized enum | Stable ID | Direct Enviro asset |
|---|---:|---|---|
| Low | `0` | `quality.low` | `Assets/Enviro 3 - Sky and Weather/Profiles/Quality/Low.asset` |
| Medium | `2` | `quality.medium` | `Assets/Enviro 3 - Sky and Weather/Profiles/Quality/Medium.asset` |
| High | `1` | `quality.high` | `Assets/Enviro 3 - Sky and Weather/Profiles/Quality/High.asset` |

`High=1` сохранён для обратной совместимости уже сериализованных 07A/07B scenes
и assets. `Medium` добавлен в конец enum как `2`; вставлять его между Low/High
запрещено. Production Bootstrap стартует на `Medium`.

Legacy authoring overloads, созданные до прямого Medium binding, fail-safe
сопоставляют Medium с уже существующим High. Пересобранные WeatherLab и
production binding assets имеют отдельную прямую ссылку на vendor Medium.

## Что реально переключается

Adapter назначает один direct `EnviroQuality` в cloned Quality module и вызывает
его update. Текущие vendor presets содержат:

| Параметр vendor preset | Low | Medium | High |
|---|---:|---:|---:|
| Volumetric clouds enabled | да | да | да |
| Cloud downsampling | 4 | 3 | 3 |
| Cloud steps layer 1 | 128 | 128 | 48 |
| Cloud steps layer 2 | 64 | 64 | 24 |
| Reprojection blend time | 3 | 2 | 4 |
| Cloud LOD distance | 0.7 | 0.7 | 0.5 |
| Fog enabled | да | да | да |
| Fog volumetrics | нет | да | да |
| Fog steps | 32 | 32 | 12 |
| Aurora enabled | да | да | нет |

Это точная инвентаризация локальных vendor assets, а не доказательство того,
что названия Low/Medium/High дают ожидаемый порядок GPU cost. Например, число
steps в локальном High меньше Low; смысл этих vendor полей нельзя переименовывать
или «исправлять» без измерений. Vendor files остаются read-only.

Production adapter после каждого quality application принудительно выключает
aurora и обновляет sky presentation. Поэтому source-значения `да/да/нет` выше
остаются точной инвентаризацией vendor assets, но effective production aurora —
`off` для всех трёх tiers. Скорректированная ночная яркость получила targeted
`USER PASS` 2026-07-18 без capture artifact, но per-tier Low/Medium/High и
matched no-aurora night comparison остаются `PENDING`.

## Что пока не связано с weather tier

Project `QualitySettings` уже содержит `High Fidelity`, `Balanced` и
`Performant`, но 07C не создаёт скрытую автоматическую связь между индексом
Unity quality и weather tier. Также пока нет отдельных проверенных per-tier
настроек для:

- HDRP shadow distance;
- precipitation distance/density beyond vendor weather profile;
- puddle/reflection resolution;
- vegetation LOD/detail;
- weather VFX distance;
- optional module disablement;
- project-wide render-pipeline quality.

Они должны добавляться только после production profiler evidence. UI 08A может
потреблять `quality.low|medium|high`, но не должно обращаться к Enviro component
или vendor asset напрямую.

## Общие bounded cadences

Независимо от tier:

- authoritative simulation не меняет точность или deterministic seed;
- presentation frame публикуется максимум 4 раза/с (`0.25 s` interval);
- ambient/reflection request идёт по binding/`600` game seconds/`25 m`, а
  adapter coalesces его минимум до `1 s`;
- global wetness обновляется dirty-only;
- legacy wetness bridge применяет quantized state (`64` steps) и не создаёт
  unique material instances;
- wind пишет один Enviro-owned WindZone, а не каждое дерево.

## Evidence

- `Logs/M07C_VisualRemediation2_EnviroIntegration.xml`: `17/17 PASS`.
- `Logs/M07C_VisualRemediation2_WeatherPresentation.xml`: `53/53 PASS`.
- `Logs/M07C_VisualRemediation2_WeatherProduction.xml`: combined production
  contracts `32/32 PASS`.
- Production binding/content tests подтверждают три отдельные direct assets.
- `Logs/M07C_VisualRemediation2_Build.log`: Builder `1.0.2` и production
  validator PASS, default Medium.

Проверено автоматически: stable IDs, enum compatibility, exact direct
resolution, unknown tier fail-closed, Medium frame validation, single wind owner
и actual WeatherLab lifecycle.

Не проверено:

- production-world визуальное сравнение Low/Medium/High;
- 1080p development-build CPU/GPU budget каждого tier;
- отсутствие quality switch spikes на полном donor baseline;
- соответствие tier ожидаемому project-wide QualitySettings.

Пока эти измерения не выполнены, tiers считаются `COHERENT_BINDING`, но не
`PERFORMANCE_SIGNED_OFF`.
