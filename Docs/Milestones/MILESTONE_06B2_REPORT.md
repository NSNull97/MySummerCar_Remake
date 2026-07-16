# Milestone 06B2 — Donor Map Streaming Cellization and Active World Profile

Дата: 2026-07-16

Unity: `6000.3.11f1`

Active profile: `donor-feature-parity-06b2`

Generator: `1.0.0-06B2`

Классификация: `TemporaryDirectImport`

Automated gate: **PASS**

Manual acceptance gate: **PASS / HumanAccepted**

06B3 decision: **GO после фиксации 06B2**

## 1. Итог

Sanitized donor map 06B1 активирован как temporary private-local runtime
baseline через существующую project-owned streaming architecture.

Карта не реконструировалась и не интерпретировалась заново. Eligible static
objects детерминированно распределены в одну global и 49 additive cell scenes.
Непрерывные/cross-cell objects оставлены global. Две visual custom cells,
отклонённые по fidelity, исключены из active donor profile и сохранены только
как отдельный regression fixture.

06B2 не переходил к weather, remaster art или 06B3 freeze.

## 2. Переиспользованная архитектура

Сохранены и расширены:

- 512 m `WorldCellIndex`;
- IDs `cell_X_Z`;
- explicit manifest и additive scene lifecycle;
- normal load radius `1` и unload radius `2`;
- player-driven focus;
- vehicle preload threshold `12 m/s`, radius `2`;
- global scene lifetime;
- owned-scene unload;
- stable gameplay identity;
- production replacement/override contract.

Streaming service получил bounded исправления scene-handle ownership и внешнего
unload/reload reconciliation; отдельный streaming framework не создавался.

## 3. Cellization result

| Метрика | Результат |
|---|---:|
| Generated streaming scenes | 50 |
| Global scenes | 1 |
| Cell scenes | 49 |
| Eligible entities | 3 842 |
| Global entities | 88 |
| Cell-owned entities | 3 754 |
| Renderers | 2 605 |
| Effective-active entities | 2 777 |
| Safe colliders | 32 |
| Gameplay anchors | 15 |

Ownership fingerprint:

`0a9de0beb45d83d6983153a48d0eb1eb93bb51fdfcc63baa629425f2777b13f3`

Normal static content сохраняет frozen source cell. Large/continuous,
cross-cell traversal, static-batch aggregate и bootstrap safety geometry
остаётся global. Geometry не разрезалась.

## 4. Active profile и prototype migration

Bootstrap теперь использует
`Assets/Game/World/Content/Streaming/ProductionWorldStreamingManifest.asset`.

Active manifest загружает donor scenes для `cell_0_-3` и `cell_0_-2`.
Отклонённые project-authored visuals сохранены в:

- `PrototypeWorldStreamingManifest.asset`;
- `PrototypeWorldStreamingFixture.unity`.

Они отсутствуют в active donor profile, не удалены и продолжают проходить
regression tests.

## 5. Gameplay/visual separation

Project-owned gameplay anchors вынесены в
`WorldGameplayCellCatalog.asset`. 15 anchors используют stable IDs и не зависят
от donor GameObject names, hierarchy paths или scene object references.

Все legacy records используют project-owned replacement keys:

`legacy-world:<stable-id>`

Production override может отключить matching legacy renderer/collider без
изменения gameplay anchor или save identity.

## 6. Collision and traversal

Перенесён explicit allowlist:

- 20 `MeshCollider`;
- 12 `BoxCollider`.

Все colliders статические, без triggers, Rigidbody, joints и donor behavior.
Они покрывают bootstrap/home safety и representative terrain, road, bridge,
rail, field, roadside и lakebed traversal.

Добавлен project-owned out-of-bounds recovery. Bootstrap активирует player после
готовности global/focus world scenes.

## 7. Editor/build safety

Добавлены bounded generation, dry-run, validation и cell inspection tools.
Generated payload остаётся под ignored
`Assets/Game/LegacyImport/RuntimeBaseline/`.

Donor baseline build разрешается только как явно подтверждённый private local
Development build. Public/distributable build блокируется pre-build guard.
Custom bounded builds регистрируют точный scene scope и не подхватывают 50 donor
scenes случайно.

## 8. Выполненные проверки

| Проверка | Результат |
|---|---|
| Full 06B2 validator | PASS: 50 scenes, 3 842 entities, 32 colliders, 15 anchors |
| Focused EditMode | `4/4 PASS` |
| Focused PlayMode | `5/5 PASS` |
| Prototype regression EditMode | `3/3 PASS` |
| Prototype regression PlayMode | `8/8 PASS` |
| Streaming performance PlayMode | `1/1 PASS` |
| Bounded 05B.1 player build | PASS, 3 scenes |
| Public donor build guard probe | Expected block PASS |
| Generated payload Git boundary | PASS |
| Scoped `git diff --check` | PASS |

Full-worktree `git diff --check` не используется как чистый 06B2 gate:
широкий ранее существовавший user diff содержит многочисленные trailing spaces
в unrelated Unity YAML assets. Эти файлы не исправлялись в рамках 06B2.

Runtime smoke bounded prototype build загрузил ожидаемые scenes. Capture
завершился неполным только потому, что hidden-window GPU FrameTiming не вернул
положительные samples; это не ошибка manifest или player build.

## 9. Performance

Editor PlayMode capture:

| Метрика | Результат |
|---|---:|
| Bootstrap ready | `900.15 ms` |
| Global loaded | `770.59 ms` |
| First focus cell loaded | `894.46 ms` |
| Donor initial used memory | `356.94 MiB` |
| Capture peak-field used memory | `417.89 MiB` |
| Observed max snapshot used memory | `418.70 MiB` |
| Capture peak-field reserved memory | `966.35 MiB` |
| Observed max snapshot reserved memory | `971.35 MiB` |
| Vehicle preload refresh | `84.33 ms` |
| Worst representative refresh | `311.76 ms` |
| Sampled max frame | `9.202 ms` |
| Main Thread max | `9.148 ms` |

Render Thread marker отсутствовал в batch/headless backend. Standalone 1080p
capture на реальном GPU остаётся задачей 06B3. Explicit peak fields текущего
JSON не включают более поздний `recovered` snapshot; в таблице поэтому отдельно
указаны conservative observed maxima.

## 10. Основные созданные/обновлённые файлы

Runtime и composition:

- `Assets/Game/Bootstrap/Bootstrap.unity`;
- `Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs`;
- `Assets/Game/World/Runtime/Streaming/ProductionWorldStreamingManifest.cs`;
- `Assets/Game/World/Runtime/Streaming/ProductionWorldStreamingService.cs`;
- `Assets/Game/World/Runtime/Streaming/WorldGameplayCellCatalog.cs`;
- `Assets/Game/World/Runtime/Streaming/WorldOutOfBoundsRecovery.cs`;
- `Assets/Game/LegacyImport/Runtime/DonorWorldBaselineColliderMetadata.cs`;
- `Assets/Game/LegacyImport/Runtime/DonorWorldLegacyReplacementRegistry.cs`;
- `Assets/Game/LegacyImport/Runtime/DonorWorldStreamingSceneMetadata.cs`.

Editor/build tooling:

- `Assets/Game/Editor/WorldBaseline/DonorWorldCellizationBuilder.cs`;
- `Assets/Game/Editor/WorldBaseline/DonorWorldCellizationPlan.cs`;
- `Assets/Game/Editor/WorldBaseline/DonorWorldCellizationValidator.cs`;
- `Assets/Game/Editor/WorldBaseline/DonorWorldCellizationWindow.cs`;
- `Assets/Game/Editor/WorldBaseline/DonorRuntimeBaselineBuildGuard.cs`;
- `Assets/Game/Editor/WorldBaseline/WorldBaseline06B2Paths.cs`;
- `Assets/Game/Editor/WorldBaseline/WorldGameplayAnchorManifest.cs`.

Authored manifests/profile assets:

- `Assets/Game/LegacyImport/Manifests/WorldBaseline06B2GameplayAnchors.csv`;
- `Assets/Game/LegacyImport/Manifests/WorldBaseline06B2SafeColliderAllowlist.csv`;
- `Assets/Game/World/Content/Streaming/ProductionWorldStreamingManifest.asset`;
- `Assets/Game/World/Content/Streaming/PrototypeWorldStreamingManifest.asset`;
- `Assets/Game/World/Content/Streaming/WorldGameplayCellCatalog.asset`;
- `Assets/Game/World/Debug/Streaming/PrototypeWorldStreamingFixture.unity`.

Tests:

- `Assets/Game/Tests/EditMode/WorldBaseline/DonorWorldCellizationEditModeTests.cs`;
- `Assets/Game/Tests/PlayMode/WorldBaseline/DonorWorldCellizationPlayModeTests.cs`;
- `Assets/Game/Tests/PlayMode/WorldBaseline/DonorWorldStreamingPerformancePlayModeTests.cs`.

Generated scenes/collision meshes находятся в ignored RuntimeBaseline boundary и
не перечисляются как committed project assets.

## 11. Ручная проверка — выполнена

Пользователь выполнил ручную проверку 2026-07-16 и подтвердил:

- запуск из `Bootstrap.unity` работает;
- активный baseline соответствует оригинальной карте, а прежние rejected
  custom visuals не отображаются как feature-parity world;
- пеший маршрут к озеру работает;
- район магазина Теймо отображается корректно;
- перенос персонажа к Теймо и обратно вызывает ожидаемую выгрузку/загрузку
  объектов без замеченного duplicate world;
- падение ниже карты вызывает project-owned recovery и возвращает персонажа
  домой.

Отдельно замечено и принято как legacy/remaster debt:

- озеро визуально плоское, без углублений;
- на карте остаются провалы, которые присутствуют в оригинальной игре и будут
  закрываться на поздних remaster/final stages.

Специальный автомобильный маршрут и отдельный осмотр каждого моста в этой
проверке не заявлены. Это не блокирует принятую bounded 06B2 проверку, но
остаётся полезным расширенным traversal coverage для 06B3.

## 12. Ограничения и риски

- Temporary donor presentation не является production art и не подлежит
  публичному распространению.
- Diagnostic materials, sprite forests, terrain voids и proxy art остаются
  documented remaster debt.
- Плоская геометрия озера и подтверждённые original-design voids сохраняются
  намеренно; 06B2 не маскирует их как исправленные.
- 32-collider allowlist не является полным переносом donor collision/gameplay.
- Unsplit global aggregates повышают resident memory.
- Unity Physics предупреждает о шести legacy triangles крупнее 500 m.
- Реальный standalone GPU/render-thread baseline ещё не снят.
- Рабочее дерево содержит широкий ранее существовавший unrelated diff; 06B2
  changes необходимо отделять при будущей фиксации.

## 13. Go/no-go

Automated gate и пользовательская visual/traversal проверка имеют статус
**PASS / HumanAccepted**.

Решение для `06B3_RUNTIME_BASELINE_VALIDATION_AND_FREEZE.md` — **GO после
фиксации текущего 06B2 состояния отдельным коммитом**.

Единственный рекомендуемый следующий milestone:

`06B3_RUNTIME_BASELINE_VALIDATION_AND_FREEZE.md`.
