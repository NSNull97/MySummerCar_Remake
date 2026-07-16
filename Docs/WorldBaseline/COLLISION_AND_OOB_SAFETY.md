# Collision и out-of-bounds safety donor world baseline

Дата: 2026-07-17

Область: technical safety временного private-local donor world baseline.

Статус: player и vehicle traversal, reset и below-world recovery имеют
подтверждённый bounded coverage. Полная collision parity и production vehicle
recovery policy остаются `PENDING`.

## 1. Принцип

Milestone 06B3 не ремастерит terrain voids, tree walls, buildings или proxy
geometry. Допустимы только минимальные project-owned safeguards, которые:

- предотвращают бесконечное падение или permanent softlock;
- не меняют donor map layout;
- не превращают узкий collider allowlist в неаудированный mass import;
- не используют donor scripts, FSM, hierarchy names или paths;
- явно остаются временными до production replacement.

## 2. Активный collision envelope

Allowlist:

`Assets/Game/LegacyImport/Manifests/WorldBaseline06B2SafeColliderAllowlist.csv`

Фактический состав:

| Collider type | Количество |
|---|---:|
| Static `MeshCollider` | 20 |
| Static `BoxCollider` | 12 |
| Triggers | 0 |
| Rigidbody на baseline colliders | 0 |
| Joints | 0 |
| Donor physics behavior | 0 |

Все 32 colliders принадлежат global legacy layer. Это предотвращает исчезновение
critical traversal support при смене focus cells.

Allowlist покрывает:

- home/bootstrap spawn structural safety;
- representative terrain support;
- asphalt, dirt, gravel, pavement и roadside traversal;
- highway и dirt bridge segments;
- railroad/rail tunnel segment;
- field/track surfaces;
- lakebed safety без блокирования water surface.

Это representative safety coverage, а не полный перенос 5 001 source collider
records.

## 3. Player safe spawn

Bootstrap authoring:

| Поле | Значение |
|---|---|
| Spawn position | `(153.495, 1.1, -1028.03)` |
| Spawn rotation | `(0, 180, 0)` Euler |
| OOB minimum Y | `-64` |

`ProductionWorldStreamingInstaller`:

1. Создаёт player неактивным.
2. Добавляет или находит `WorldOutOfBoundsRecovery`.
3. Настраивает recovery на project-owned spawn transform.
4. Привязывает player как streaming focus.
5. Загружает global и focus scenes.
6. Активирует player только после `RefreshNow`.

Таким образом, player не начинает session до готовности initial world support.

## 4. Player OOB recovery

Реализация:

`Assets/Game/World/Runtime/Streaming/WorldOutOfBoundsRecovery.cs`

Recovery срабатывает, если:

- position содержит non-finite component; или
- `position.y < -64`.

При recovery:

- временно отключается `CharacterController`, если он был включён;
- attached Rigidbody velocities обнуляются, если Rigidbody присутствует;
- owner переносится на safe transform;
- physics transforms синхронизируются;
- увеличивается `RecoveryCount`.

Важно: generic поддержка attached Rigidbody внутри компонента не означает
готовую production vehicle recovery policy. В обычном Bootstrap компонент
настраивается только на spawned player. Изолированный 06B3 development harness
добавляет его к M06 prototype vehicle только для safe-road traversal smoke.

## 5. Матрица safety

| ID | Проверка | Статус | Evidence / ограничение |
|---|---|---|---|
| `SAFE-PLAYER-001` | Player spawn после initial streaming readiness | `PASS` | 06B2 Bootstrap/PlayMode coverage и ручной запуск |
| `SAFE-PLAYER-002` | Падение player ниже world bound | `PASS` | Automated OOB recovery coverage; пользователь упал и вернулся домой |
| `SAFE-PLAYER-003` | Non-finite player transform | `IMPLEMENTED / PENDING SMOKE` | Code path существует, отдельный фактический smoke не зафиксирован |
| `SAFE-COLLIDER-001` | 32 allowlisted colliders корректно создаются | `PASS` | Full 06B2 validator |
| `SAFE-COLLIDER-002` | Representative imported collider raycasts | `PASS` | Focused 06B2 PlayMode |
| `SAFE-COLLIDER-003` | Global colliders переживают cell load/unload | `PASS` | Global lifetime/repeated streaming tests |
| `SAFE-BRIDGE-001` | Пеший проход по representative bridges | `PASS / HumanAccepted` | Пользователь прошёл по мостам без обнаруженных проблем |
| `SAFE-CELL-001` | Cross-cell character relocation | `PASS / HumanAccepted` | Не обнаружены seam, duplicate, popping или load/unload проблемы |
| `SAFE-FULLMAP-001` | Все дороги/объекты имеют полную collision parity | `PENDING / OUT OF SCOPE` | Allowlist намеренно узкий |
| `SAFE-VEHICLE-001` | Vehicle below-world recovery | `PASS / HumanAccepted in dev harness` | Evidence `recoveryCount = 1`; production last-safe/state policy отсутствует |
| `SAFE-VEHICLE-002` | Dedicated vehicle traversal across required routes | `PASS / HumanAccepted` | Пользователь проехал Fleetari, Teimo, inspection/town, major loop, railway crossing и bridge |
| `SAFE-ITEM-001` | Critical item recovery hook | `N/A CURRENT / PENDING FUTURE` | Critical-item world system не входит в map-only baseline |
| `SAFE-NPC-001` | Critical NPC recovery hook | `N/A CURRENT / PENDING FUTURE` | NPC runtime отсутствует в current baseline |
| `SAFE-SPAWN-VEHICLE-001` | Vehicle safe spawn | `PASS IN DEV HARNESS` | Runtime marker, road placement, reset и safe recovery подтверждены ручным запуском |
| `SAFE-SPAWN-ITEM-NPC-001` | Item/NPC safe spawns | `N/A CURRENT / PENDING FUTURE` | Соответствующие systems ещё не активны |

## 6. Временные safety workarounds

| Workaround ID | Реализация | Почему временно | Future replacement |
|---|---|---|---|
| `OOB-WA-001` | Возврат player на home spawn при `Y < -64` или non-finite position | Hard reset не использует last-safe route point и не сохраняет context | Общий project-owned recovery service с typed policies для player/vehicle/items |
| `COL-WA-001` | Узкий allowlist из 32 static colliders | Не даёт полной building/prop/route collision | Production collision cell-by-cell вместе с reauthored geometry |
| `COL-WA-002` | Critical traversal colliders остаются global | Повышает resident cost и не является финальной ownership granularity | Seam-safe production split после профилирования |
| `BOOT-WA-001` | Player активируется после initial global/focus load | Решает только initial session safety | Сохранить ordering contract в production bootstrap |
| `VEH-OOB-WA-001` | Development harness задаёт M06 vehicle safe-road spawn, reset pose и `WorldOutOfBoundsRecovery` | Не хранит production last-safe road point, assembly/save context или recovery cooldown | Typed production vehicle recovery policy после появления persistent vehicle runtime |

Эти workarounds не должны удаляться до появления проверенной замены.

## 7. Выполненные проверки

Автоматическое evidence 06B2:

- full validator: 50 scenes, 3 842 entities, 32 colliders, 15 anchors;
- representative `MeshCollider` raycasts;
- global scene/collider lifetime;
- repeated far-cell load/unload;
- vehicle-speed neighbor preload;
- player out-of-bounds recovery.

Автоматическое evidence 06B3:

- vehicle harness builder создаёт development-only scene и оставляет её вне
  Build Settings;
- serialized allowlist содержит ровно шесть road/asphalt/dirt/gravel/pavement
  collider IDs;
- focused EditMode isolation/configuration test: `1/1 PASS`;
- `F8` evidence export records observed cells, transition history, longest
  consecutive sequence, peak frame/streaming-frame time, peak speed and
  recovery count into
  `PerformanceCaptures/Milestone06B3/M06B3_VehicleTraversalEvidence.json`;
- runtime initialization, road probe и vehicle placement подтверждены
  пользовательским запуском; отдельный automated runtime test не добавлялся.

Ручное evidence:

- Bootstrap запускается;
- player дошёл до lake/shore;
- Teimo/store region загрузился и выгрузился при переносе player;
- player прошёл по bridges;
- cross-cell relocation не выявил seam/load/unload problem;
- падение ниже карты завершилось возвратом домой.
- dedicated vehicle drive охватил Fleetari, Teimo/store, inspection/town,
  major road loop, railway crossing и representative bridge;
- 24 unique cells, 35 transitions и шесть последовательных границ записаны
  `F8` evidence;
- `Backspace` вернул vehicle, а below-world smoke увеличил
  `recoveryCount` до `1`;
- критические collision, duplicate или missing-section failures не наблюдались;
- один краткий микрофриз отмечен как Editor-context observation без доказанной
  причины.

## 8. Известные collision gaps

### 8.1 Buildings и props

32-collider allowlist намеренно не переносит:

- doors;
- windows;
- dynamic props;
- NPC colliders;
- gameplay triggers;
- полный набор building shell/interior colliders.

В частности, пользователь подтвердил, что около Teimo ground collision есть, но
через сам building shell можно проходить. Это missing gameplay/building
collision, а не падение под карту. Для текущего map geometry baseline дефект
записан как future collision work; перед gameplay внутри/около магазина он
должен быть закрыт.

### 8.2 Terrain voids

Original-design terrain voids и области за tree-wall/backdrop остаются
визуальным debt. Текущий OOB recovery защищает только после ухода ниже threshold
или появления non-finite transform.

Он не гарантирует recovery при:

- застревании внутри geometry выше `Y = -64`;
- попадании в замкнутую полость;
- потере vehicle без падения ниже threshold;
- item/NPC softlock.

### 8.3 Large legacy triangles

Unity Physics сообщает о шести legacy mesh triangles с расстоянием между
вершинами более 500 m:

- `c1f2a3a5bcea99941a9c9f917697a612`;
- `66153ce7364c1e04cb739c4d81250c27`;
- `d047899dc789beb4a97f47c3921c1b09`;
- `62329ef689f366b4290c277a9647d692`;
- `e998e95284781ba45b1ceb2277292c22`;
- `216c10525466d2a4ca69f785d6dbcefe`.

Это donor geometry debt. Meshes намеренно не разрезаются без seam-safe
replacement tool. Предупреждения требуют route-based monitoring, но сами по себе
не доказывают runtime failure.

## 9. Vehicle recovery requirement

Development harness уже даёт узкую safety-only реализацию для текущего M06
prototype: safe-road spawn, manual reset и recovery при non-finite/below-world
transform. Это допустимый временный smoke fixture, но не production vehicle
recovery policy.

Минимальный production contract:

- finite transform/velocity validation;
- below-world и invalid-state detection;
- project-owned last-safe road or spawn anchor;
- reset linear/angular velocity;
- согласованное восстановление wheel/suspension state;
- сохранение vehicle stable ID и assembly/save state;
- cooldown и защита от recovery loop;
- explicit user/debug evidence;
- tolerance-based PlayMode tests.

После фактического harness smoke:

`SAFE-VEHICLE-001 = PASS / HumanAccepted in dev harness`.

## 10. Critical item/NPC hooks

Current donor map baseline не содержит project-owned critical item или NPC
runtime. Поэтому отсутствие concrete hooks не является ложным `PASS`.

Когда systems появятся, они должны:

- использовать project-owned stable IDs;
- хранить last-safe/authoritative state независимо от loaded cell;
- восстанавливаться без donor hierarchy lookup;
- не теряться при unload;
- иметь отдельные item/NPC recovery policies и save tests.

## 11. Минимальные дальнейшие проверки

Для будущей production safety остаются:

1. Полная collision authoring по мере замены baseline.
2. Typed vehicle last-safe recovery с assembly/save context.
3. Проверки collision gaps отдельно от original-design voids.
4. Recovery из softlock состояний выше OOB threshold.
5. Standalone private Development smoke отдельным build workflow.

## 12. Решение

Подтверждено:

- initial player spawn safety;
- bounded representative traversal collision;
- global collider lifetime;
- player below-world recovery;
- bridge/cell-boundary manual acceptance;
- dedicated vehicle traversal;
- development-harness vehicle reset и below-world recovery.

Не подтверждено:

- полное collision покрытие карты;
- production vehicle recovery policy;
- item/NPC recovery;
- recovery из всех softlock состояний выше OOB threshold.

Итог на момент документа:

`PLAYER BASELINE SAFETY = PASS WITH DOCUMENTED LIMITS`

`06B3 VEHICLE TRAVERSAL / DEV-HARNESS OOB SAFETY = PASS WITH DOCUMENTED LIMITS`

`FULL PRODUCTION COLLISION / RECOVERY POLICY = PENDING`
