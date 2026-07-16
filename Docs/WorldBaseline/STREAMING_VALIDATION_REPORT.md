# Валидация donor world streaming profile 06B2

Дата: 2026-07-16

Unity: `6000.3.11f1`

Profile: `donor-feature-parity-06b2`

Автоматический gate: **PASS**

Предыдущая bounded geometry/traversal проверка: **PASS / HumanAccepted**

Textured presentation, включая исправленную воду: **PASS / HumanAccepted**

Bridge walking / cross-cell character relocation check: **PASS / HumanAccepted**

## 1. Structural validation

Последний full validator завершился:

```text
DONOR_WORLD_CELLIZATION_06B2_VALIDATION_OK
scenes=50
entities=3842
renderers=2605
colliders=32
anchors=15
ownershipFingerprint=1abf88e8047c2446e4ecdc1bdc894738171fac0f4ac9651be75470e985bbf28b
```

Evidence log:

`Logs/M06B2V51_CellizationValidator09_WaterFix.log`

Final deterministic generation:

- `Logs/M06B2V51_PresentationBuild12_WaterFix.log`;
- `Logs/M06B2V51_CellizationBuild09.log`.

Presentation generator: `1.1.0-06B2-v5.1.5`.

Presentation fingerprint:
`e337f9d1b5a0344473bd0f096cdb9e0dbf8da321ecb428ebc17da4f7dd8831fd`.

Material/texture manifest SHA-256:
`cfbce4faf14eac19658cbad7117b9a794de3009a664f438d3faa28a80ecc4192`.

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
| Focused 06B2 v5.1 EditMode | `7/7 PASS` | `65.344 s` |
| Focused 06B2 v5.1 PlayMode | `6/6 PASS` | `7.456 s` |
| Prototype regression EditMode | `3/3 PASS` | `0.129 s` |
| Prototype regression PlayMode | `8/8 PASS` | `1.977 s` |
| 06B2 v5.1 streaming performance PlayMode | `1/1 PASS` | `2.558 s` |

Test evidence:

```text
TestResults/M06B2V51_EditMode05_WaterFix.xml
TestResults/M06B2V51_PlayMode06_WaterFix.xml
TestResults/M06B2_PrototypeRegression_EditMode.xml
TestResults/M06B2_PrototypeRegression_PlayMode.xml
TestResults/M06B2V51_PerformancePlayMode06_WaterFix.xml
```

Performance capture SHA-256:
`60868e5862413b59df0adad2dee998617145b96de2306494a7bbd6e60e3ba271`.

Water-fix EditMode log:

`Logs/M06B2V51_EditMode05_WaterFix.log`

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
- `LegacyTextured` compatibility materials активны по умолчанию; temporary
  category materials доступны только в режиме `LegacyDiagnostic`.
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

Пользователь принял geometry/layout и non-water visual oddities как временный
remaster debt. Зафиксированы severe low-quality/stretch/banding terrain
texture, proxy/tree-wall surfaces и другие legacy material artifacts; final
production textures и материалы будут заменены.

При осмотре lakebed поверх него наблюдалась непрозрачная «пелена». Причина —
temporary-water compatibility material. Water fix `v5.1.5`:

- использует donor `_BaseColor.a`: `Water4Adv_Lake = 0.2901961`,
  `Water4Simple = 0.5058824`;
- больше не использует shore-foam texture как full-surface base map.

Presentation build, full validator и focused EditMode после исправления прошли.
Пользователь повторно проверил lake/shoreline и принял исправленную воду
2026-07-16: **PASS / HumanAccepted**.

2026-07-16 пользователь прошёл по мостам и переносил персонажа между ячейками;
проблем с traversal, collision, seam, duplicate, popping или load/unload не
обнаружено. Dedicated vehicle drive отдельно не выполнялся; это явно сохранено
как ограничение ручного метода, а автоматическая high-speed preload проверка
имеет статус PASS.

## 7. Gate conclusion

Техническая реализация v5.1: **PASS**.

Предыдущая bounded geometry/traversal проверка остаётся
**PASS / HumanAccepted**. Geometry/layout и non-water legacy texture artifacts
приняты как временный baseline debt. Исправленная вода также принята вручную.
Bridge/cell-boundary completion check имеет статус **PASS / HumanAccepted**.

Решение для `06B3_RUNTIME_BASELINE_VALIDATION_AND_FREEZE.md` — **GO**. Entry gate
закрыт; 06B3 в рамках этой фиксации не начинался.
