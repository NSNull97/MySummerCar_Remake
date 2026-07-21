# 08A.1 — исправление материалов и освещения baseline

Статус: **локальный candidate v002 / material-lighting remediation принят пользователем**  
Дата: **2026-07-21**

## Причина

В 1 683 производных static-batch мешах сохранялись вершины и UV, но не
сохранялись normals и tangents. Из-за этого HDRP рассчитывал свет
камерозависимо: грунтовка у дома резко меняла тон от ракурса, а детали фонарей
у магазина Теймо освещались частично.

Дополнительно временные donor-материалы наследовали неоднозначный metallic,
слишком высокий smoothness/SSR и статические emissive-варианты. Это усиливало
блеск и создавало впечатление самосвечения.

## Что изменено

- Добавлен bounded Editor-remediator
  `DonorWorldMaterialLightingRemediator` с policy
  `08A1-local-material-lighting-remediation-v1`.
- Исходные reference/donor assets читаются через read-only MeshData API и не
  изменяются.
- В generated `RuntimeBaseline` восстановлены точные transformed normals;
  tangents восстановлены из источника либо пересчитаны только при наличии
  подходящих UV и triangle topology.
- Геометрия, vertex order, индексы, submeshes, transforms, stable IDs,
  стриминг и коллизии не менялись.
- Material compatibility policy поднята до
  `08A1-temporary-hdrp-compatibility-v3`:
  - временный world baseline принудительно dielectric (`Metallic = 0`);
  - smoothness ограничен консервативными значениями;
  - frozen emissive-варианты отключены до появления project-owned gameplay
    presenter;
  - для проверенных road/ground материалов отключён SSR;
  - для таких же ground renderer-ов отключены reflection probes.
- Двери и их намеренно отсутствующие коллизии не затрагивались.

## Выполненная регенерация

Основной успешный прогон:

```text
materials=292
meshes=2129
normalsRepaired=1683
tangentsRepaired=1693
rendererPolicies=2605
```

Целевые generated meshes подтверждены с normal/tangent channels `3/4`:

- грунтовка у дома: `b3326cd1a123a496fe44b26d64dda2e6`;
- фонарь Теймо: `5321706220112780baeed0bbe56912b3`;
- фонарь Теймо: `8946ab55e770546f2c76e72945c54f24`.

Логи:

- `Logs/08A1_MaterialLightingRemediation_v3_r2.log` — основное восстановление;
- `Logs/08A1_MaterialLightingRemediation_v3_r3.log` — применение финальной
  matte-ground renderer policy;
- `Logs/08A1_MaterialLightingRemediation_v3_r4_materials.log` — финальная
  синхронизация material reports.

## Проверки

По явному решению пользователя автоматические тестовые наборы и отдельные
формальные валидаторы для этой графической итерации не запускались. Unity во
время регенерации успешно скомпилировал скрипты, а batch-команды завершились с
кодом `0`. Автоматический статус не повышался: эта графическая итерация принята
пользователем по результату ручной проверки 2026-07-21.

436 мешей не получили tangents, поскольку у них нет полного UV0 либо triangle
topology. Normals присутствуют; normal/detail-normal mapping для таких мешей не
может быть корректно вычислен без отдельной reauthoring-процедуры.

## Ручная проверка

Открыть `Assets/Game/Bootstrap/Bootstrap.unity`, запустить Play → New Game и
проверить:

1. Одну и ту же грунтовку у дома с двух противоположных ракурсов при дневном
   свете — не должно быть чёрного/синего скачка тона.
2. Дом, крышу, грунт и стены — они не должны выглядеть самосветящимися или
   металлическими.
3. Оба фонаря у Теймо с локальным источником света — все видимые части должны
   освещаться непрерывно, без пропавших сегментов.
4. Крышу дома изнутри и снаружи — внешние тени/небо не должны просвечивать
   через материал.
5. Двери по-прежнему должны оставаться проходимыми — это намеренное временное
   ограничение.

## Ручное принятие

Пользователь явно подтвердил принятие материалов 2026-07-21. Это закрывает
только material/lighting remediation gate. Полный candidate v002 остаётся
отдельным объектом приёмки и не повышается автоматически.

Milestone 08B на момент этой записи не начат; material/lighting gate для
перехода к нему открыт.
