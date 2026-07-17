# Fog и water compatibility: решение 07A

Дата среза: 2026-07-17.
Статус: single-owner стратегия реализована структурно; WeatherLab fog/transparent composition подтверждена пользователем как `MANUAL_PASS_BY_USER_CAPTURE_MISSING`, production-world и underwater проверки остаются `PENDING`.

## Текущая вода проекта

Проверенный production reference:

`E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\World\Production\Prefabs\WR_HomeShorelineWater.prefab`

`BoundedLakeSurface` — обычные `MeshFilter` + `MeshRenderer`. HDRP `WaterSurface` component в prefab не обнаружен. Материал:

`E:\GAYmDev_Studio\MySummerCar_Remake\Assets\Game\World\Production\Materials\WR_DitchWater.mat`

использует transparent HDRP/Lit shader с keywords:

- `_SURFACE_TYPE_TRANSPARENT`;
- `_ENABLE_FOG_ON_TRANSPARENT`;
- `_DISABLE_SSR_TRANSPARENT`.

Это временный water presentation proxy. В текущем scope нет доказанного underwater volume, подводного fog owner, swimming или buoyancy.

## Enviro fog evidence

Default Enviro Fog module содержит:

- custom height fog: `Settings.fog = true`;
- Enviro volumetrics: `Settings.volumetrics = true`;
- `controlHDRPFog = false`;
- `controlHDRPVolumetrics = false`.

`EnviroHDRPRenderer` композитит height fog и clouds в injection point `AfterOpaqueAndSky`. Vendor HDRP profile содержит HDRP `Fog`, но component `active: false`.

## Выбранная bounded стратегия

Целевое bounded решение для WeatherLab и перехода к 07B/07C:

`EnviroOwner: Enviro adapter/module; HDRP Fog component в project-owned profile — управляемый output substrate, а не отдельный authority`

Текущее implementation evidence:

- HDRP Global Settings содержит serialized registration `Enviro.EnviroHDRPRenderer, Enviro3.Runtime`;
- `WeatherLabBuilder.EnsureVolumeProfile()` добавляет HDRP `Fog` target в единственный project-owned profile;
- runtime adapter на своём Fog clone принудительно держит `controlHDRPFog=true` и `controlHDRPVolumetrics=true`;
- `EnviroFogModule.UpdateFogHDRP()` получает этот target через `manager.volumeHDRP.sharedProfile` и при включённых `controlHDRPFog` / `controlHDRPVolumetrics` записывает его параметры;
- authoritative presentation control остаётся у Enviro adapter/module; Volume/Fog не принимает самостоятельных weather decisions.

Таким образом, наличие компонента HDRP Fog само по себе не является duplicate owner. Gate остаётся ручным из-за transparency/water composition, а не из-за ownership conflict.

Условия принятия:

1. Effective registration `EnviroHDRPRenderer` подтверждена preflight без duplicate execution.
2. Project-owned lab VolumeProfile содержит только один HDRP Fog target, управляемый Enviro, без второго project/weather controller.
3. BootstrapGlobalVolume не загружается вместе с WeatherLab.
4. Fog preset применяется только явным adapter frame.
5. Transparent water/window и interior leakage проходят manual capture review.

Это design decision и подтверждённая граница authority, а не утверждение о пройденной визуальной совместимости.

## Bounded fallback

Если ручная проверка выявит неприемлемые дефекты на transparent water/windows или внутри помещения, допустимый fallback:

`HDRPNativeOwner: один HDRP Fog override, управляемый project adapter; Enviro Settings.fog=false`

Fallback требует отдельного решения, `Enviro Settings.fog=false` и повторных captures. Запрещены два независимо управляемых fog authority; HDRP `Fog`, которым Enviro управляет как output target в основной стратегии, допустим и не считается вторым owner. Vendor shaders не патчатся.

## Test matrix

| Test ID | Camera/условие | Проверяемое | Статус |
|---|---|---|---|
| `W07A-FOG-01` | exterior, fog near water proxy | shoreline silhouette, fog depth, отсутствие резкой границы | MANUAL_PASS_BY_USER / CAPTURE_MISSING |
| `W07A-FOG-02` | camera через transparent window | fogged exterior и читаемая поверхность стекла | MANUAL_PASS_BY_USER / CAPTURE_MISSING |
| `W07A-FOG-03` | interior room | отсутствие неприемлемого fog leakage внутрь | MANUAL_PASS_BY_USER / CAPTURE_MISSING |
| `W07A-FOG-04` | vehicle interior looking out | стекло/фон/осадки без двойной композиции | FOG_COMPOSITION_PASS / HIGH_RAIN_DROPLETS_PASS / CAPTURE_MISSING |
| `W07A-FOG-05` | headlights through fog | читаемые световые конусы без чрезмерной засветки | MANUAL_PENDING |
| `W07A-FOG-06` | camera над water proxy под острым углом | transparent fog keyword, sorting, shoreline | MANUAL_PASS_BY_USER / CAPTURE_MISSING |
| `W07A-FOG-07` | camera ниже water plane | только диагностическое наблюдение; underwater system отсутствует | MANUAL_PENDING / NON-AUTHORITATIVE |
| `W07A-FOG-08` | scene load/unload и повторный attach/detach | один fog owner, один renderer, bounded cleanup | AUTOMATED_RUNTIME_PASS; LONG_SOAK_PENDING |

## Что нельзя заявлять

- текущая вода не является final HDRP water system;
- underwater compatibility не подтверждена;
- fog не проверен на полном donor baseline;
- wetness/puddles отсутствуют в 07A;
- performance fog/cloud composition ещё не измерена.

Пользовательская проверка 2026-07-17 подтверждает отсутствие неприемлемого перекрытия fog и видимые капли после remediation в проверенных WeatherLab ракурсах, но не заменяет screenshot artifacts. Статусы capture evidence и Low/High comparison ведутся в `WEATHERLAB_CAPTURE_INDEX.csv`.
