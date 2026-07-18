# World-fidelity regression under production weather

Дата среза: 2026-07-18.

Статус: `FROZEN_REFERENCE_RECORDED / TRACKED_SCOPE_CLEAN /
THIRD_VISUAL_ROUTE_PARTIAL_PASS / CURRENT_GENERATED_MATERIAL_CONTRACT_FAIL /
MATCHED_RETEST_AND_GEOMETRY_DIFF_PENDING`.

## Frozen authority

Единственный authoritative baseline —
`Docs/WorldBaseline/BASELINE_REVISION.json`:

| Field | Frozen value |
|---|---|
| Revision | `DonorWorldBaseline-v001` |
| Source revision | `msc-world-baseline-04a1.1-c3f2f337` |
| Source scene SHA-256 | `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4` |
| Source files fingerprint | `72567486f72bd9106eadc0ba87aadb290accefdcee9e6517ffe30c2218d5ceec` |
| Ownership fingerprint | `1abf88e8047c2446e4ecdc1bdc894738171fac0f4ac9651be75470e985bbf28b` |
| Presentation fingerprint | `e337f9d1b5a0344473bd0f096cdb9e0dbf8da321ecb428ebc17da4f7dd8831fd` |
| Active profile | `donor-feature-parity-06b2` |
| Composition | 1 global + 49 cells; 3 842 entities; 2 605 renderers; 32 colliders; 15 anchors |

Frozen source-to-project transform:

```text
translation = (169.97999572753906, 1.6109999418258667, -1040.625)
rotation    = (0, 0, 0, 1)
scale       = (1, 1, 1)
origin      = historical CABIN/Shed garage-roof anchor; do not recenter
```

World bounds:

```text
min = (-2993.205322265625, -412.8290100097656, -3295.29833984375)
max = ( 3553.165283203125,  913.3709716796875,  2597.9658203125)
```

## 07C tracked scope result

Production weather builder редактирует Bootstrap и project-owned weather
content, но не generated donor scenes, world manifests, frozen revision,
terrain/roads/landmarks или prototype profile disposition. Текущий tracked diff
не содержит 07C изменений под `Docs/WorldBaseline/` или generated
`RuntimeBaseline` world content.

Это полезный scope audit, но не заменяет требуемый geometry/transform diff:
generated RuntimeBaseline payload находится вне Git, а визуальная композиция
может измениться без geometry mutation. Поэтому definitive fidelity status не
повышается до PASS.

## Production visual evidence and remediation scope

Первый ручной production-проход дал `FAIL` по presentation-качеству:

- fixed HDRP `EV 10` пересвечивал day/interior и давил night readability;
- double/misbound fog давал opaque/red mist, который скрывал world debt;
- oversized shelter ellipsoid создавал видимый овал за пределами
  frozen-derived home/garage AABB;
- rain спавнился, но был нечитаем в Game View из-за runtime
  `maxParticleSize = 0.001`.

Исправления ограничены project-owned presentation слоем: isolated
runtime `VolumeProfile` с rebinding, deterministic fixed exposure curve с
context offsets, один visibility-to-HDRP-MFP fog pass, tiled shelter removal
zones и runtime-only rain particle-size floor `0.01`. Enviro vendor content,
frozen scenes, transforms, terrain, roads, landmarks и world manifests не менялись.

Эти исправления ещё не имеют matched post-remediation captures. Поэтому
они не повышают fidelity disposition до PASS без повторного ручного маршрута.

Повторный ручной маршрут после первой remediation также дал `FAIL`:
temporary surfaces выглядели металлическими, дождь проходил через гостиную,
интерьер оставался пересвеченным, ночь была почти чёрной с нежелательной
aurora, а `18:00–20:00` не соответствовали ожидаемому финскому летнему свету.

Вторая remediation осталась в project-owned presentation слое: runtime location
`64.166 N / 24.3 E / UTC+2`, same-pass sun/moon update, forced aurora off,
daylight/context exposure tuning, reflection intensity `0.6`, wet target
smoothness `0.45/0.25` и shelter vertical stretch floor `1`. Frozen world и
vendor payload не менялись. Новый matched manual route остаётся `PENDING`.

Третий ручной маршрут подтвердил rain presentation и текущий
вечерний закат. Остаточные отражения приняты пользователем как временный долг
donor-материалов/шейдеров до их замены. Ночь оказалась немного слишком яркой,
а рассвет начинался после `03:00`; follow-up меняет только project-owned runtime
presentation policy на `60 N / 27.3 E / UTC+3` (около `04:59 / 21:35` на
`1995-08-01`) и smooth night minimum `7.5 EV` на `solarTime 0.43 -> 0.50`.
Daylight branch, frozen geometry и vendor payload не меняются. Скорректированные
рассвет около `05:00` и ночная яркость получили `USER PASS` 2026-07-18 без
capture artifact; matched manual route остаётся `PENDING`.

## Second-remediation automated evidence

- Builder/validator `1.0.2`: `PASS`,
  `Logs/M07C_VisualRemediation2_Build.log`.
- EnviroIntegration `17/17`, combined WeatherProduction/ProductionIntegration/
  legacy-wetness `32/32`, WeatherPresentation `53/53`, Production PlayMode
  `6/6`, world-only `1/1`: `PASS` в `Logs/M07C_VisualRemediation2_*.xml`.
- Full EditMode: `330/334`, с теми же четырьмя unrelated historical
  Garage/World failures, `Logs/M07C_VisualRemediation2_FullEditMode.xml`.
- Strict frozen-world revalidation: `PASS`, 50 scenes / 49 cells / 3 842 entities,
  result SHA-256
  `10544fc3ed5bd6c8cd44b451155e766c0552710f4c9d8cc91dda263de001ba5f`,
  `Logs/M07C_VisualRemediation2_WorldFreeze.log`.
- Enviro vendor payload и frozen world changes: `0`.
- Matched human visual retest: `PENDING`.

## Solar/night follow-up evidence

- EnviroIntegration `17/17`, combined WeatherProduction/ProductionIntegration/
  legacy-wetness `32/32` и Production PlayMode `6/6`: `PASS` в
  `Logs/M07C_VisualRemediation3_*.xml`.
- Full EditMode: `328/334`, четыре прежние historical failures плюс две текущие
  WorldBaseline material-contract failures,
  `Logs/M07C_VisualRemediation3_FullEditMode.xml`.
- Focused WorldBaseline: `8/10` с теми же двумя failures,
  `Logs/M07C_VisualRemediation3_WorldBaseline.xml`; это подтверждает persistent
  generated-payload drift, а не test-order pollution.
- Затронуты ignored generated materials с source GUID
  `06cd824234ef21f40b2c797b15acbd26`,
  `2b8378937c6afb64390d5474f4bcd14a`,
  `5cc44389f1f10bf4cabee6d33feb1551` и
  `69ad9b54c687ad847a9df25112c2d430`. Их timestamp `09:19:37` предшествует
  follow-up; frozen payload не менялся.
- Поэтому previous strict freeze PASS и unchanged hash сохраняются только как
  prior evidence. Текущий материал-contract gate — `FAIL`, свежий frozen PASS
  не заявляется.
- Manual: rain, sunset и corrected dawn/night brightness —
  `USER PASS 2026-07-18`; reflections accepted temporary debt. Dawn/night
  capture artifact отсутствует, matched fidelity route остаётся `PENDING`.

## Required matched capture contract

До и после weather integration необходимо сравнить neutral clear/dry состояние:

- одна Camera transform;
- один FOV/aspect/resolution;
- один project date/time;
- `weather.clear` без перехода;
- ground/road wetness и puddles = 0;
- gameplay lightning disabled;
- один exposure/reference mode;
- одинаковые loaded cells и active profile;
- никакой fog/vegetation masking unresolved geometry mismatch.

AI concept art и generic Finnish references не являются authority.

## Canonical capture set

| Capture ID | Location / purpose | Before | After | Side-by-side | Status |
|---|---|---|---|---|---|
| `W07C-FID-01` | Home/garage spawn and yard | missing | missing | missing | PENDING |
| `W07C-FID-02` | Main-road departure sightline | missing | missing | missing | PENDING |
| `W07C-FID-03` | Representative bridge | missing | missing | missing | PENDING |
| `W07C-FID-04` | Teimo/store and town context | missing | missing | missing | PENDING |
| `W07C-FID-05` | Lake/shore and horizon | missing | missing | missing | PENDING |
| `W07C-FID-06` | Each active production override cell | missing | missing | missing | PENDING |

06B1/06B2/06B3 screenshots и accepted traversal подтверждают baseline до 07C,
но без exact camera/FOV/time/exposure metadata они не считаются strict matched
before captures.

## Geometry/transform diff requirements

Отдельный export должен сравнить с frozen revision:

- source-to-project transform;
- global/cell scene membership и ownership;
- entity stable IDs/replacement keys;
- renderer/mesh/material slot ownership;
- gameplay anchor IDs/transforms;
- terrain/road/landmark transforms;
- active/inactive state rejected prototype visuals;
- 1 global + 49 cell scene addresses;
- manifest/fingerprint values.

Любое отличие требует documented migration, а не silent baseline rewrite.

## Known visual debt that must remain visible in review

- terrain voids/tree walls/map-edge hacks;
- stretched/low-quality legacy terrain textures;
- temporary water and flat proxies;
- incomplete building collision;
- legacy vegetation/LOD debt;
- inactive rejected custom cells.

Погода, туман и экспозиция не могут использоваться для маскировки этих проблем.

## Disposition

- Frozen reference available: `PASS`.
- 07C tracked world-content mutation: `NONE OBSERVED`.
- Enviro vendor/frozen world mutation during visual remediation: `NONE`.
- Prior post-remediation frozen-world automated audit: `PASS`, unchanged result
  hash; current focused material-contract validation: `8/10 FAIL`.
- First and second production visual routes: `FAIL` (historical evidence).
- Third visual route: `PARTIAL PASS` (rain/sunset accepted; reflection debt
  explicitly accepted as temporary).
- Targeted corrected dawn/night brightness: `USER PASS 2026-07-18`, no capture
  artifact; matched route remains `PENDING`.
- Dedicated geometry/transform diff: `PENDING`.
- Matched before/after side-by-side: `PENDING`.
- Human fidelity acceptance under production weather: `PENDING`.

Следовательно, world-fidelity gate 07C ещё не закрыт.
