# Milestone 06B2 — Donor Map Streaming Cellization and Active World Profile

Дата: 2026-07-16

Unity: `6000.3.11f1`

Active profile: `donor-feature-parity-06b2`

Generator: `1.1.0-06B2-v5.1.5`

Классификация: `TemporaryDirectImport`

Milestone status: **COMPLETED / HumanAccepted**

Automated gate: **PASS**

Previous bounded geometry/traversal check: **PASS / HumanAccepted**

Textured presentation, включая исправленную воду: **PASS / HumanAccepted**

Bridge walking / cross-cell character relocation check: **PASS / HumanAccepted**

06B3 decision: **GO — entry gate cleared; 06B3 not started**

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

`1abf88e8047c2446e4ecdc1bdc894738171fac0f4ac9651be75470e985bbf28b`

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
| Focused EditMode | `M06B2V51_EditMode05_WaterFix.xml`: `7/7 PASS`, `65.344 s` |
| Focused PlayMode | `M06B2V51_PlayMode06_WaterFix.xml`: `6/6 PASS`, `7.456 s` |
| Prototype regression EditMode | `3/3 PASS` |
| Prototype regression PlayMode | `8/8 PASS` |
| Streaming performance PlayMode | `M06B2V51_PerformancePlayMode06_WaterFix.xml`: `1/1 PASS`, `2.558 s` |
| Bounded 05B.1 player build | PASS, 3 scenes |
| Public donor build guard probe | Expected block PASS |
| Generated payload Git boundary | PASS |
| Full-worktree `git diff --check` | PASS |

Final water-fix evidence:

- presentation build: `Logs/M06B2V51_PresentationBuild12_WaterFix.log`;
- full validator: `Logs/M06B2V51_CellizationValidator09_WaterFix.log`;
- focused EditMode:
  `TestResults/M06B2V51_EditMode05_WaterFix.xml`,
  `Logs/M06B2V51_EditMode05_WaterFix.log`.

Builder-generated blank `m_Name` lines in the two touched Unity scenes were
normalized after the final generation pass; no generated payload was added to
Git.

Runtime smoke bounded prototype build загрузил ожидаемые scenes. Capture
завершился неполным только потому, что hidden-window GPU FrameTiming не вернул
положительные samples; это не ошибка manifest или player build.

## 9. Performance

Editor PlayMode capture:

| Метрика | Результат |
|---|---:|
| Bootstrap ready | `1 068.26 ms` |
| Global loaded | `818.39 ms` |
| First focus cell loaded | `1 060.62 ms` |
| Donor initial used memory | `602.30 MiB` |
| Peak used memory | `732.64 MiB` |
| Peak reserved memory | `1 177.36 MiB` |
| Vehicle preload refresh | `95.46 ms` |
| Worst representative refresh | `404.40 ms` |
| Sampled max frame | `11.259 ms` |
| Main Thread max | `11.201 ms` |
| Peak loaded generated materials | `263` |
| Peak loaded generated textures | `217` |
| Initial resident texture memory | `396.25 MiB` |
| Preload/warmed texture-memory plateau | `502.88 MiB` |
| Runtime material instances | `0` |

Render Thread marker отсутствовал в текущем Editor PlayMode capture. Standalone
1080p capture остаётся задачей 06B3. Texture/material values отражают
резидентный набор посещённых ячеек; общие assets не клонировались.

Performance capture SHA-256:
`60868e5862413b59df0adad2dee998617145b96de2306494a7bbd6e60e3ba271`.

## 10. Основные созданные/обновлённые файлы

Runtime и composition:

- `Assets/Game/Bootstrap/Bootstrap.unity`;
- `Assets/Game/Bootstrap/ProductionWorldStreamingInstaller.cs`;
- `Assets/Game/World/Runtime/Streaming/ProductionWorldStreamingManifest.cs`;
- `Assets/Game/World/Runtime/Streaming/ProductionWorldStreamingService.cs`;
- `Assets/Game/World/Runtime/Streaming/WorldGameplayCellCatalog.cs`;
- `Assets/Game/World/Runtime/Streaming/WorldOutOfBoundsRecovery.cs`;
- `Assets/Game/LegacyImport/Runtime/DonorWorldBaselineColliderMetadata.cs`;
- `Assets/Game/LegacyImport/Runtime/DonorWorldLegacyMaterialBinding.cs`;
- `Assets/Game/LegacyImport/Runtime/DonorWorldLegacyPresentationController.cs`;
- `Assets/Game/LegacyImport/Runtime/DonorWorldLegacyPresentationMode.cs`;
- `Assets/Game/LegacyImport/Runtime/DonorWorldLegacyReplacementRegistry.cs`;
- `Assets/Game/LegacyImport/Runtime/DonorWorldStreamingSceneMetadata.cs`.

Editor/build tooling:

- `Assets/Game/Editor/WorldBaseline/DonorWorldCellizationBuilder.cs`;
- `Assets/Game/Editor/WorldBaseline/DonorWorldMaterialTexturePlan.cs`;
- `Assets/Game/Editor/WorldBaseline/DonorWorldMaterialTexturePipeline.cs`;
- `Assets/Game/Editor/WorldBaseline/DonorWorldCellizationPlan.cs`;
- `Assets/Game/Editor/WorldBaseline/DonorWorldCellizationValidator.cs`;
- `Assets/Game/Editor/WorldBaseline/DonorWorldCellizationWindow.cs`;
- `Assets/Game/Editor/WorldBaseline/DonorRuntimeBaselineBuildGuard.cs`;
- `Assets/Game/Editor/WorldBaseline/WorldBaseline06B2Paths.cs`;
- `Assets/Game/Editor/WorldBaseline/WorldGameplayAnchorManifest.cs`.

Authored manifests/profile assets:

- `Assets/Game/LegacyImport/Manifests/WorldBaseline06B2GameplayAnchors.csv`;
- `Assets/Game/LegacyImport/Manifests/WorldBaseline06B2SafeColliderAllowlist.csv`;
- `Assets/Game/LegacyImport/Manifests/DonorWorld06B2PresentationManifest.json`;
- `Assets/Game/World/Content/Streaming/ProductionWorldStreamingManifest.asset`;
- `Assets/Game/World/Content/Streaming/PrototypeWorldStreamingManifest.asset`;
- `Assets/Game/World/Content/Streaming/WorldGameplayCellCatalog.asset`;
- `Assets/Game/World/Debug/Streaming/PrototypeWorldStreamingFixture.unity`.

Tests:

- `Assets/Game/Tests/EditMode/WorldBaseline/DonorWorldCellizationEditModeTests.cs`;
- `Assets/Game/Tests/PlayMode/WorldBaseline/DonorWorldCellizationPlayModeTests.cs`;
- `Assets/Game/Tests/PlayMode/WorldBaseline/DonorWorldStreamingPerformancePlayModeTests.cs`.

Generated scenes/collision meshes находятся в ignored RuntimeBaseline boundary и
не перечисляются как committed project assets. То же относится к generated
HDRP compatibility materials и role-specific texture variants. Их committed
provenance/mapping находится в:

- `Docs/WorldBaseline/LEGACY_MATERIAL_TEXTURE_MANIFEST.csv`;
- `Docs/WorldBaseline/MATERIAL_SHADER_MAPPING.md`;
- `Docs/WorldBaseline/BASELINE_VISUAL_COMPLETENESS_REPORT.md`;
- `Docs/WorldBaseline/TEXTURE_MEMORY_BASELINE.md`.

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

- на карте остаются провалы, которые присутствуют в оригинальной игре и будут
  закрываться на поздних remaster/final stages;
- terrain texture имеет выраженно низкое исходное качество, растяжение и
  полосатость;
- proxy/tree-wall geometry и другие legacy material/texture artifacts остаются
  визуально грубыми.

Пользователь принял geometry/layout и перечисленные non-water texture artifacts
как временный visual debt. Final production textures и материалы будут
переавторены на позднем remaster этапе.

Ручной осмотр выявил непрозрачную «пелену» над lakebed. Это оказался дефект
temporary-water compatibility material, а не туман. В `v5.1.5` исправлено:

- итоговая alpha берётся из donor `_BaseColor.a`: `Water4Adv_Lake =
  0.2901961`, `Water4Simple = 0.5058824`;
- shore-foam texture больше не назначается как full-surface base map.

Пользователь повторно проверил воду после пересборки и принял результат
2026-07-16: **PASS / HumanAccepted**.

Финальный ручной completion check выполнен: пользователь прошёл по мостам и
переносил персонажа между ячейками без обнаруженных проблем. Dedicated vehicle
drive не выполнялся; автоматическая high-speed preload проверка имеет статус
PASS. Gate принят с этим явно записанным ограничением ручного метода.

## 12. Ограничения и риски

- Temporary donor presentation не является production art и не подлежит
  публичному распространению.
- Temporary compatibility/diagnostic materials, sprite forests, terrain voids
  и proxy art остаются
  documented remaster debt.
- Severe low-quality/stretch/banding terrain texture, tree-wall/proxy surfaces
  и другие legacy material artifacts приняты только как временная presentation
  baseline; это не production art.
- Подтверждённые original-design voids сохраняются намеренно; 06B2 не маскирует
  их как исправленные.
- 32-collider allowlist не является полным переносом donor collision/gameplay.
- Unsplit global aggregates повышают resident memory.
- Unity Physics предупреждает о шести legacy triangles крупнее 500 m.
- Реальный standalone GPU/render-thread baseline ещё не снят.
- Текущее v5.1 изменение оставлено незакоммиченным до отдельной явной команды
  на фиксацию.

## 13. Дополнение 06B2 v5.1 — donor material/texture presentation

Active donor profile теперь использует project-owned HDRP compatibility
presentation вместо category-only diagnostic colors:

- 2 605 renderers и 2 744 ordered source material slots;
- 292 resolved donor materials и один общий reviewed fallback для built-in
  material `10302`;
- 265 source images образуют 269 conversion records: 268 imported role
  variants и один intentionally excluded cubemap;
- declared sharing contract: `2 744 -> 293` material assets (`2 451`
  copies avoided) and `384 -> 268` source+role texture variants (`116`
  copies avoided);
- восемь detail-normal variants детерминированно упакованы в HDRP Detail Map
  как `R=.5, G=Y, B=.5, A=X`; donor detail UV и strength сохранены;
- Detail Map назначен 22 материалам: 20 detail-only и двум материалам вместе
  с primary normal;
- five renderer rows with one declared material and two sanitized submeshes are
  exported as explicit repeated effective slots; no extra material assets are
  created;
- 251 opaque, 14 alpha-clip, 12 transparent lit, 1 transparent unlit,
  7 unlit, 5 emissive и 2 temporary-water material mappings;
- temporary-water alpha следует donor `_BaseColor.a` (`0.2901961` для
  `Water4Adv_Lake`, `0.5058824` для `Water4Simple`), а shore foam не
  используется как full-surface base map;
- `LegacyTextured` active by default, `LegacyDiagnostic` доступен для сравнения,
  rejected prototype visuals остаются `PrototypeHidden`;
- все переключения используют `Renderer.sharedMaterials`; performance test не
  обнаружил runtime material instances;
- donor shaders, lighting, weather, audio, cameras, UI и runtime logic не
  импортированы.

Presentation fingerprint:

`e337f9d1b5a0344473bd0f096cdb9e0dbf8da321ecb428ebc17da4f7dd8831fd`

Manifest SHA-256:

| Артефакт | SHA-256 |
|---|---|
| Ownership matrix | `9f83de8770a966ef7cc28ae9be71f00e6668883b9c6e5e2498adf92b8fc93546` |
| Object-to-cell manifest | `794b68c9d86a083343e08452d38ef621bc2c989c433810e7de17429f354cfcb4` |
| Material/texture manifest | `cfbce4faf14eac19658cbad7117b9a794de3009a664f438d3faa28a80ecc4192` |

Автоматические результаты v5.1:

| Проверка | Результат |
|---|---|
| Full validator | PASS: 50 scenes, 3 842 entities, 2 605 renderers, 32 colliders, 15 anchors |
| Water-fix presentation build | `M06B2V51_PresentationBuild12_WaterFix.log`: PASS |
| Water-fix full validator | `M06B2V51_CellizationValidator09_WaterFix.log`: PASS |
| Focused EditMode | `M06B2V51_EditMode05_WaterFix.xml`: `7/7 PASS`, `65.344 s` |
| Focused PlayMode | `M06B2V51_PlayMode06_WaterFix.xml`: `6/6 PASS`, `7.456 s` |
| Streaming/material performance | `M06B2V51_PerformancePlayMode06_WaterFix.xml`: `1/1 PASS`, `2.558 s`, runtime material instances `0` |
| Repeated material/texture generation | Stable fingerprints |
| Donor shader/runtime dependency scan | PASS |
| Git generated-payload boundary | PASS |

Geometry/layout, non-water legacy texture oddities и исправленная вода приняты
пользователем для temporary baseline. 2026-07-16 пользователь также прошёл по
мостам и переносил персонажа между ячейками; проблем с traversal, collision,
seam, duplicate, popping или load/unload не обнаружено. Ручной метод зафиксирован
как walking/cross-cell relocation, а не dedicated vehicle drive; автоматическая
проверка скоростной предзагрузки уже имеет статус PASS.

## 14. Go/no-go

Automated v5.1 gate имеет статус **PASS**. Textured presentation, включая
исправленную воду, имеет статус **PASS / HumanAccepted**.
Предыдущая bounded geometry/traversal проверка остаётся
**PASS / HumanAccepted**. Финальный bridge/cell-boundary completion check также
имеет статус **PASS / HumanAccepted**.

Решение для `06B3_RUNTIME_BASELINE_VALIDATION_AND_FREEZE.md` — **GO**. Entry gate
06B2 v5.1 закрыт; работа над 06B3 в рамках этой фиксации не начиналась.

Единственный рекомендуемый следующий milestone:

`06B3_RUNTIME_BASELINE_VALIDATION_AND_FREEZE.md`.
