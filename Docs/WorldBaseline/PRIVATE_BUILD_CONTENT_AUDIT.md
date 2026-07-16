# Аудит private build и donor content boundary

Дата: 2026-07-17

Активный world profile: `donor-feature-parity-06b2`.

Классификация baseline: `TemporaryDirectImport`.

Режим: `PrivateLocalFeatureParityOnly`.

Итог: build/content safeguards существуют и public donor build отрицательно
проверен, но full-profile private Development standalone build на момент этого
аудита не выполнен. Он остаётся `PENDING`.

## 1. Статусы

| Статус | Значение |
|---|---|
| `PASS` | Проверка фактически выполнена или контракт непосредственно подтверждён текущим repository state |
| `PENDING` | Требуемая runtime/build проверка не выполнялась либо отдельный профиль ещё не создан |
| `N/A` | Система отсутствует в текущем milestone |
| `BLOCKED` | Обнаружено нарушение boundary или требований распространения |

## 2. Текущий content envelope

Активный manifest:

`Assets/Game/World/Content/Streaming/ProductionWorldStreamingManifest.asset`

Фактические параметры:

- profile kind: donor feature parity;
- `privateLocalRuntimeBaseline = true`;
- одна global scene;
- 49 cell scenes;
- generated payload:
  `Assets/Game/LegacyImport/RuntimeBaseline/`;
- 50 donor baseline scenes включены в текущий
  `ProjectSettings/EditorBuildSettings.asset`.

Наличие scenes в Build Settings не означает разрешение публичной сборки.
Безопасность обеспечивается hard build guard, а не утверждением, что donor
scenes физически отсутствуют из общего Editor scene list.

## 3. Результаты аудита

| Проверка | Статус | Evidence / ограничение |
|---|---|---|
| Raw donor extraction находится вне Git | `PASS` | Frozen source разрешается через external staging и ignored local configuration; raw/normalized boundaries перечислены в `.gitignore` |
| Generated RuntimeBaseline не отслеживается Git | `PASS` | В tracked set присутствует только `Assets/Game/LegacyImport/RuntimeBaseline/.gitkeep`; `git check-ignore` подтверждает generated path |
| ReferenceOnly не отслеживается Git | `PASS` | В tracked set присутствует только `Assets/Game/LegacyImport/ReferenceOnly/.gitkeep`; payload ignored |
| Baseline явно классифицирован | `PASS` | Source/presentation manifests и world docs используют `TemporaryDirectImport` |
| Donor scripts/FSM/runtime assemblies исключены | `PASS` | Sanitation audit и 06B2 donor runtime dependency scan прошли |
| Donor executable не является runtime component | `PASS` по архитектуре | Remake runtime использует project-owned Unity 6 assemblies; full private standalone launch без donor process отдельно остаётся `PENDING` |
| Donor installation не модифицируется | `PASS` по выполненным операциям и policy | Pipeline использует read-only source; этот аудит не выполнял новую запись или patch donor files |
| Private baseline build требует explicit opt-in | `PASS` | `DonorRuntimeBaselineBuildGuard` требует Development, env acknowledgement и private-enabled manifest |
| Public donor build блокируется | `PASS` | Negative probe ожидаемо заблокирован: `Logs/M06B2_PublicBuildGuard.log` |
| Full donor-profile private Development build | `PENDING` | Bounded 05B.1 build не является full 51-scene Player build: Bootstrap плюс 50 donor scenes |
| Отдельный distributable/public profile без donor baseline | `PENDING` | Текущий repository state защищён hard block; отдельный pruned public profile не зафиксирован |
| ReferenceOnly не попадает в public dependency graph | `PASS` для проверенного 06B2 scope | Повторный full public-profile audit потребуется при создании такого профиля |
| Public/distributable artifact не создан | `PASS` | Текущий donor baseline не заявляется и не выдаётся как distributable build |

## 4. Build guard

Реализация:

`Assets/Game/Editor/WorldBaseline/DonorRuntimeBaselineBuildGuard.cs`

Guard проверяет:

1. Прямое присутствие scene под
   `Assets/Game/LegacyImport/RuntimeBaseline/`.
2. Recursive dependency build scene от RuntimeBaseline.
3. Active donor feature-parity manifest и его private-local permission.
4. Наличие process environment variable:
   `MSC_PRIVATE_DONOR_BASELINE_BUILD=1`.
5. Наличие `BuildOptions.Development`.

При нарушении хотя бы одного обязательного условия выбрасывается
`BuildFailedException`.

Custom bounded build tools могут зарегистрировать точный scene scope через
`BeginExplicitSceneBuild`. Это предотвращает случайное включение всех enabled
donor scenes в узкий prototype build.

## 5. Матрица разрешений

| Build type | Development | Env acknowledgement | Donor dependency | Ожидаемое решение guard | Фактический smoke |
|---|---:|---:|---:|---|---|
| Обычный public/release | Нет | Нет | Да | Block | Negative probe `PASS` |
| Development без acknowledgement | Да | Нет | Да | Block | Контракт подтверждён кодом; отдельный build не требуется для разрешения |
| Non-development с acknowledgement | Нет | Да | Да | Block | Контракт подтверждён кодом |
| Explicit private local Development | Да | Да | Да | Allow, если manifest разрешает private baseline | `PENDING` full-profile smoke |
| Build без donor dependency | Любое допустимое значение | Не требуется guard-ом | Нет | Guard не вмешивается | Bounded 05B.1 build ранее `PASS`, но это не donor-profile evidence |

## 6. Git boundary

`.gitignore` исключает:

- `Assets/Game/LegacyImport/ReferenceOnly/**`;
- `Assets/Game/LegacyImport/RuntimeBaseline/**`;
- `Assets/Game/Imported/DonorGenerated/**`;
- raw/normalized/decompiled/extraction directories;
- builds, logs, test results и performance captures.

Разрешено коммитить:

- project-owned import/build tools;
- hashes и manifests без donor binary payload;
- sanitation/cellization mappings;
- tests;
- provenance и milestone reports;
- newly authored production assets.

Запрещено коммитить:

- frozen AssetRipper project/export;
- raw or normalized donor dumps;
- generated donor scenes, meshes, materials и textures;
- donor executable, runtime assemblies или decompiled source;
- machine-specific local paths/configuration.

## 7. Content classification

Generated baseline включает только sanitized donor-derived:

- static world geometry;
- roads, bridges, terrain/water/vegetation proxies;
- static buildings и props;
- temporary compatibility materials/textures;
- 32 explicitly allowlisted static colliders;
- transforms и project-owned metadata.

Он не включает donor:

- `MonoBehaviour` gameplay scripts;
- PlayMaker FSM;
- cameras/player/UI;
- lighting/weather/audio managers;
- save/platform/Steam/DRM logic;
- old `UnityEngine` assemblies.

Это private development baseline, а не `ProductionReady` art.

## 8. Что ещё нужно проверить

Для закрытия full-profile private build smoke требуется фактически:

1. Запустить Unity с acknowledgement только в текущем build process.
2. Выбрать Development build.
3. Собрать active Bootstrap и donor profile с точным scene scope.
4. Запустить standalone без запуска donor executable.
5. Подтвердить Bootstrap, global scene, focus cells и repeated load/unload.
6. Зафиксировать build log, размер, commit и результат запуска.
7. Удалить локальный build artifact после сохранения evidence, если он больше не
   нужен.

До выполнения этих шагов статус остаётся `PENDING`; наличие разрешающего code
path не равно успешно выполненному build.

Для будущего public/distributable profile необходимо:

- создать отдельный scene/content profile без RuntimeBaseline dependency;
- выполнить recursive dependency audit;
- убедиться, что ReferenceOnly и DonorGenerated payload отсутствуют;
- сохранить hard guard как дополнительную защиту;
- не снимать ограничения без явного подтверждения прав.

## 9. Риски

- Текущие 50 donor scenes enabled в общих Build Settings требуют сохранения
  build guard.
- Environment variable является explicit acknowledgement, но не заменяет
  Development flag и manifest permission.
- Удаление или обход guard создаст прямой distribution risk.
- Full standalone build может выявить dependency, memory или serialization
  проблемы, не видимые в Editor PlayMode.
- Наличие private baseline в локальном build не даёт права распространять этот
  build.

## 10. Решение

- Private-local policy и hard guard: `PASS`.
- Public donor build negative probe: `PASS`.
- Generated/reference Git boundary: `PASS`.
- Full donor-profile private Development standalone smoke: `PENDING`.
- Отдельный public profile, физически исключающий donor baseline: `PENDING`.
- Разрешение на public/distributable baseline: `NO-GO`.
