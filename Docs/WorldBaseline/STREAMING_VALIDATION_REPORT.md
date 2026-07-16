# Валидация donor world streaming profile 06B2

Дата: 2026-07-16

Unity: `6000.3.11f1`

Profile: `donor-feature-parity-06b2`

Автоматический gate: **PASS**

Ручной gate: **PASS / HumanAccepted**

## 1. Structural validation

Последний full validator завершился:

```text
DONOR_WORLD_CELLIZATION_06B2_VALIDATION_OK
scenes=50
entities=3842
renderers=2605
colliders=32
anchors=15
ownershipFingerprint=0a9de0beb45d83d6983153a48d0eb1eb93bb51fdfcc63baa629425f2777b13f3
```

Evidence log:

`Logs/M06B2_validate_06.log`

Проверено:

- одна global scene и 49 canonical cell scenes;
- exact scene paths и Build Settings mapping;
- deterministic object ownership и exported CSV manifests;
- generated scene stamps, IDs, transforms, mesh/material references;
- collision type, transform и mesh topology;
- forbidden donor components;
- active donor versus inactive prototype profile;
- Bootstrap wiring, spawn readiness и out-of-bounds contract;
- gameplay catalog и replacement-key uniqueness;
- ignored generated-payload boundary.

## 2. Test results

| Suite | Result | Duration |
|---|---:|---:|
| Focused 06B2 EditMode | `4/4 PASS` | `13.755 s` |
| Focused 06B2 PlayMode | `5/5 PASS` | `5.078 s` |
| Prototype regression EditMode | `3/3 PASS` | `0.129 s` |
| Prototype regression PlayMode | `8/8 PASS` | `1.977 s` |
| 06B2 streaming performance PlayMode | `1/1 PASS` | `1.995 s` |

Test evidence:

```text
TestResults/M06B2_EditMode_03.xml
TestResults/M06B2_PlayMode_05.xml
TestResults/M06B2_PrototypeRegression_EditMode.xml
TestResults/M06B2_PrototypeRegression_PlayMode.xml
TestResults/M06B2_Performance_03.xml
```

PlayMode coverage включает:

- bootstrap/global/home-cell startup;
- prototype visual inactivity;
- unique IDs и replacement keys;
- representative `MeshCollider` raycasts;
- global scene lifetime;
- vehicle-speed neighboring-cell preload;
- repeated far-cell load/unload;
- production override persistence;
- externally initiated unload/reload ownership reconciliation;
- gameplay catalog availability;
- out-of-bounds recovery.

## 3. Build and boundary checks

Bounded 05B.1 prototype regression player build:

```text
M05B1_PERFORMANCE_BUILD_OK
scenes=3
pilotIndex=1
nextIndex=2
bytes=219018471
```

Evidence:

`Logs/M06B2_PrototypePerformanceBuild_02.log`

Public donor-baseline build probe был ожидаемо заблокирован pre-build guard.
Guard требует одновременно:

- Development build;
- explicit process acknowledgement
  `MSC_PRIVATE_DONOR_BASELINE_BUILD=1`.

Evidence:

`Logs/M06B2_PublicBuildGuard.log`

`git check-ignore` подтверждает, что generated
`Assets/Game/LegacyImport/RuntimeBaseline/` payload не входит в Git.

## 4. Collision and traversal

Validated allowlist:

- 20 static `MeshCollider`;
- 12 static `BoxCollider`;
- 0 triggers;
- 0 Rigidbody;
- 0 joints.

Автоматические raycasts подтверждают representative imported traversal mesh.
Bootstrap activation ждёт готовности global/focus scenes. Global colliders не
исчезают при смене cells. Out-of-bounds recovery возвращает player на
project-owned safe position.

Шесть Unity Physics warnings о legacy triangles крупнее 500 m зафиксированы как
временный donor geometry debt. Они не появились из-за cell split: соответствующая
geometry намеренно не разрезалась.

## 5. Предупреждения full validator

- Источник 06B1 не имел runtime collision; безопасный узкий allowlist добавлен
  только в 06B2.
- Temporary category materials остаются diagnostic presentation.
- Unsplit global static-batch aggregates остаются дорогими.
- 32-collider allowlist не переносит двери, окна, dynamic props, NPC и triggers.

## 6. Ручные проверки

Пользователь выполнил ручную проверку 2026-07-16:

1. Bootstrap запускается и работает.
2. Active baseline визуально соответствует оригинальной карте; rejected
   prototype interpretation не подменяет donor world.
3. Пеший маршрут до озера проходит.
4. Район магазина Теймо отображается корректно.
5. Перенос персонажа к Теймо и обратно подтверждает unload/reload объектов без
   замеченного duplicate world.
6. При падении ниже карты срабатывает recovery и персонаж возвращается домой.
7. Наблюдаемые terrain voids распознаны как присутствующие в оригинале, а не
   новые seams от cellization.

Озеро остаётся плоским, без углублений. Пользователь принял это вместе с
original-design voids как временный remaster debt для поздних этапов.

Dedicated vehicle/bridge traversal отдельно не заявлен и переносится как
расширенное coverage в 06B3, не отменяя bounded human acceptance 06B2.

## 7. Gate conclusion

Техническая реализация и пользовательская ручная проверка: **PASS /
HumanAccepted**.

Решение для `06B3_RUNTIME_BASELINE_VALIDATION_AND_FREEZE.md` — **GO после
фиксации 06B2 отдельным коммитом**.
