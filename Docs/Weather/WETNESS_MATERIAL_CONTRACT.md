# Контракт wetness для материалов

Project globals:

- `_MSC_GroundWetness`;
- `_MSC_RoadWetness`;
- `_MSC_PuddleAmount`;
- `_MSC_VegetationWetness`.

`GlobalWetnessShaderBridge` обновляет их dirty-only по project-owned revision. Обычный HDRP/Lit не читает произвольные globals автоматически, поэтому это стабильный контракт для будущих shaders/material systems, а не утверждение о полном покрытии мира.

## WeatherLab proof

`WeatherLabWetnessMaterialBridge` использует один переиспользуемый `MaterialPropertyBlock` для явно назначенных proxy renderers:

- asphalt;
- gravel/dirt;
- building exterior;
- vehicle paint;
- vegetation;
- puddle.

Bridge меняет base color/smoothness/alpha через MPB и не вызывает `renderer.material`. Identity `sharedMaterial` должна оставаться неизменной; уникальные runtime material instances на каждый объект не создаются.

## Bounded puddle/ripple proxy

Puddle proxy получает `_MSC_PuddleAmount` и `_MSC_PuddleRipplePhase`. При puddle amount выше порога phase обновляется с фиксированным максимумом 12 Гц, один MPB переиспользуется, а alpha/smoothness получают небольшую sinusoidal модуляцию. Это project-owned диагностический proxy, не физическая симуляция воды, не HDRP Water и не финальный ripple shader.

При disable property blocks и globals очищаются, revision/ripple counters сбрасываются.

## Ограничения

- Enviro Terrain Shader не требуется;
- donor baseline materials в 07B не переавториваются;
- production coverage и shader-family compatibility остаются `PENDING` до 07C;
- screenshot evidence puddle/ripple остаётся `PENDING`; automated Editor performance cadence записан в `M07B_WeatherLab_Performance.json`, production/render/GPU sign-off остаётся 07C validation debt.
