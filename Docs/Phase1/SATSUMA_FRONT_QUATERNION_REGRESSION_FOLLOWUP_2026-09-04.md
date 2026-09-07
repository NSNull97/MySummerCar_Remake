# Регрессии физических тестов Satsuma: исходное ТЗ и результат исправления

Дата: 2026-09-04. Текущий статус: **ResolvedAutomatedRegressionPassed**.
Финальный focused EditMode — **85/85**, совместный PlayMode — **33/33**,
без ошибок и пропусков. Это закрытие описанной ниже тестовой регрессии,
не новая полная ручная приёмка подвески или машины на склоне.

## История: исходное ТЗ до исправления

На момент исходного ТЗ исследование было частичным, исправления не вносились.
Следующие факты, последовательность и критерий сохранены как история; актуальный
результат и границы проверки находятся в разделе «Исправлено и проверено» ниже.

Контекст: [отчёт по ручнику](SATSUMA_HANDBRAKE_PARITY_2026-09-04.md).
Ручник прошёл 134/134 EditMode и 2/2 собственных PlayMode-проверки.
Общий PlayMode-прогон — 13/22: девять ошибок в существующей группе
`SatsumaInstalledPartPhysicsPlayModeTests`. Не считать всю физику проверенной.

## Подтверждённые факты

- `Logs/codex-handbrake-v64-slope-play.xml` и `.log` содержат финальный прогон.
  Старый полный прогон `Logs/codex-v1d44-satsuma-physics-full-results.xml`
  действительно проходил 20/20; нынешние ошибки нельзя списать на заведомо
  неработавшие тесты. Причинная связь с конкретным последующим изменением пока
  не установлена.
- Шесть проверок останавливаются на non-unit quaternion при записи
  `SatsumaFrontSteeringController.RefreshCorner` в `state.Anchor.rotation`.
  В исходном Rigidbody кузова измерено norm² `1.00010252`; сохранённый
  `BaseLocalRotation` наследует отклонение. Произведение даёт norm²
  `1.00020504` и отклоняется native setter. Transform кузова и колеса в том же
  измерении единичные. Начальные prefab-повороты также единичные.
- Подозрительные границы: захват
  `Quaternion.Inverse(chassis.rotation) * wheel.transform.rotation` и обратная
  композиция `chassis.rotation * state.BaseLocalRotation`. Это подтверждённая
  цепочка значений, но не доказательство первопричины отклонения Rigidbody
  после restore/interpolation.
- Ещё три ошибки: front assembled y `0.292074203` вместо `>0.302074641`;
  front no-strut y `0.410550267` вместо `0.23..0.32`; rear compression попадает
  в `collider_floor3_92417` вместо `Rear NWH compression patch`.
  Во фронтальных проверках также зарегистрирован чужой
  `Fresh Satsuma restore ground`. Утечка/изоляция тестовых объектов требует
  проверки; это пока не полное объяснение всех трёх ошибок.
- Временные диагностические вставки удалены. Контроллер руления и старый
  физический test fixture оставлены без изменений.

## Последовательность исправления

1. Согласовать свободное окно Unity с параллельной UI-задачей. Воспроизвести
   `FreshVehicleRestoreSettlesWithoutAssemblyMutation` и
   `RearInstalledArmUsesPhysicalHingeAtDonorMount` изолированно, затем вместе.
2. Проверить минимальную нормализацию **валидных** кватернионов на границах
   вычисления относительного кадра и передачи в физику. Сохранить ориентации,
   оси и принятую кинематику. Нулевые/нечисловые значения не маскировать
   молчаливой подстановкой identity. Добавить тест со слегка масштабированным
   единичным кватернионом и отдельные проверки невалидных входов.
3. Проверить владение и гарантированную очистку всех созданных fixture-объектов
   при assertion/Unity log failure. Сравнить контакты тестов отдельно и после
   предыдущего падения; явно проверять ожидаемую поверхность стенда.
4. Повторить все 20 старых физических тестов и оба теста ручника. Оставшиеся
   численные ошибки диагностировать уже на изолированных контактах, а не
   исправлять ослаблением допусков.

Не менять ради зелёного прогона жёсткость пружин, посадочные офсеты, принятую
геометрию, NWH vendor-код, stable IDs и формат сохранений. Не откатывать
параллельные изменения и не примешивать гидравлические тормоза.

## Критерий готовности

Старая группа 20/20 и ручник 2/2 проходят вместе без non-unit сообщений и чужих
контактов; focused EditMode остаётся зелёным. Загрузка собранной машины вручную
не требует установки/снятия детали для стабилизации. Если ошибка остаётся,
сохранить точный воспроизводимый случай и не объявлять регрессию закрытой.

## Исправлено и проверено — 2026-09-04

### Изолированное воспроизведение

До правки оба запрошенных теста действительно упали по отдельности, а не только
после соседних fixture:

| Прогон | Результат | Локальное свидетельство |
|---|---|---|
| `FreshVehicleRestoreSettlesWithoutAssemblyMutation` | 0/1; non-unit quaternion, также ошибка teardown | `Logs/codex-physics-regression-fresh-baseline.xml` и `.log` |
| `RearInstalledArmUsesPhysicalHingeAtDonorMount` | 0/1; non-unit quaternion | `Logs/codex-physics-regression-arm-baseline.xml` и `.log` |

Оба XML содержат `Rotation quaternions must be unit length`. Следовательно,
ошибка передачи поворота в Rigidbody была самостоятельным дефектом, а не только
последствием чужих контактных поверхностей. Причина первоначального небольшого
отклонения norm² у Rigidbody внутри Unity/PhysX отдельно не доказана; исправлена
наша ненормализованная граница захвата/композиции валидного кадра.

### Совместимая правка runtime и владения стендом

- `Assets/Game/Vehicle/NWH/Runtime/SatsumaFrontSteeringController.cs` нормализует
  валидные повороты при захвате кузова/колеса, вычислении относительного кадра,
  композиции anchor, назначении свободного yaw и чтении его Rigidbody.
  `TryNormalizePhysicsRotation` сохраняет ориентацию; double-расчёт norm²
  избегает переполнения при конечных масштабированных компонентах.
- NaN, infinity и norm² `<= 0.000001` не превращаются в identity. Выдаётся
  диагностическая ошибка с кадром и corner ID, yaw-helper выключается;
  невалидное значение не передаётся дальше в native setter.
- `Assets/Game/Tests/EditMode/VehicleNwhIntegration/SatsumaFrontSteeringRotationTests.cs`
  добавляет 13 случаев: пять валидных масштабов, три нулевых/почти нулевых,
  три non-finite варианта, композицию с сохранением donor yaw и реальную
  записанную цепочку noisy chassis → relative capture → anchor. В последней
  использован захваченный quaternion
  `(-6.05455952e-9, 1.91231209e-8, 2.0850166e-10, 1.00005126)`.
- `Assets/Game/Tests/PlayMode/VehiclePhysics/SatsumaInstalledPartPhysicsPlayModeTests.cs`
  явно владеет созданными машинами, всеми зарегистрированными частями, включая
  отсоединённые/переподчинённые, и землёй. `UnityTearDown` очищает их даже при
  прерывании coroutine неожиданным Unity log; две границы кадра завершают
  deferred destruction кузова и его отдельного yaw-helper, затем вызывается
  `Physics.SyncTransforms`. В fresh-restore источнику и отдельным частям
  выполняется очистка до появления восстановленной машины. Два фронтальных
  force-теста дополнительно проверяют принадлежность `HitCollider` своему стенду.

Геометрия, посадочные офсеты, жёсткости, демпфирование, массы, физические
настройки и численные допуски существующих assertions не изменялись.
NWH vendor-код, stable IDs, сериализованные поля и схемы сохранений сохранены;
миграция не нужна. Временные контактные диагностические вставки удалены.
Это ограниченная защита численной границы и жизненного цикла fixture,
не новая модель подвески и не аудит полной donor-сборки.

### Финальные исполненные результаты

Unity `6000.3.11f1`; XML проверены непосредственно после выполнения root-задачей.
Этот документационный шаг повторно Unity не запускал.

| Прогон | Состав | Результат | Свидетельство |
|---|---|---|---|
| Focused EditMode | rotation 13 + NWH 50 + handbrake 18 + generated 2 + audio 2 | **85/85**, failed 0, skipped 0 | `Logs/codex-physics-regression-rotation-edit.xml` и `.log` |
| Совместный PlayMode | installed-part physics 20 + handbrake 2 + front steering 3 + front spawn 8 | **33/33**, failed 0, skipped 0 | `Logs/codex-physics-regression-combined-play.xml` и `.log` |

EditMode завершился `2026-09-04 13:00:02Z`, PlayMode — `13:05:13Z`.
Все девять ранее красных случаев входят в прошедшую группу 20/20.
Старые 13/22 остаются историческим результатом, но больше не описывают
текущий проверенный код. Контактные/численные ошибки исчезли после нормализации
и гарантированной очистки без ослабления ожиданий; это не доказывает отдельную
новую физическую причину для каждого старого численного расхождения.

Команды записаны в соответствующих логах: `-batchmode -nographics -runTests`
с `-testPlatform EditMode` и фильтром
`MSC.Tests.EditMode.VehicleNwhIntegration.SatsumaFrontSteeringRotationTests;MSC.Tests.EditMode.VehicleNwhIntegration.NwhWheelPhysicsBackendTests;MSC.Tests.EditMode.VehicleAssembly.SatsumaHandbrake`;
затем `-testPlatform PlayMode` и фильтром
`MSC.Tests.PlayMode.VehiclePhysics.SatsumaInstalledPartPhysicsPlayModeTests;MSC.Tests.PlayMode.VehiclePhysics.SatsumaHandbrakePlayModeTests;MSC.Tests.PlayMode.VehiclePhysics.SatsumaFrontSteeringPlayModeTests;MSC.Tests.PlayMode.VehiclePhysics.SatsumaFrontSteeringSpawnPlayModeTests`.
`-testResults`/`-logFile` указывали на перечисленные выше локальные артефакты.

SHA256 проверенных XML; сами логи/XML остаются вне Git:

| Файл под `Logs/` | SHA256 |
|---|---|
| `codex-physics-regression-fresh-baseline.xml` | `A955F6480DF8F11101DA29C45B49C5D2A09C6E510204C1D7B14A09459F071968` |
| `codex-physics-regression-arm-baseline.xml` | `24BD965CD7289C0CF7045EFD3629087F995680CB24944E8DF633EE5B6389B24B` |
| `codex-physics-regression-rotation-edit.xml` | `F6255520D8DC344413AF7C3955F01ECE67034E9F17D64555D4B5C85DC29558D9` |
| `codex-physics-regression-combined-play.xml` | `ED9E4F5CA28FF05AA6176AA25F1320870CD1F9CBB095DE8F59161E2C3C407224` |

### Приёмка и оставшаяся граница

Ручник пользователь оценил как «вроде работает как смог проверил»:
**LimitedManualAccepted**, без утверждения о выполнении всего incline/save/audio
чек-листа. Автоматический склон по-прежнему использует стенд 200 kg только
с задней осью и ограниченным вращением; это не полноценная машина на склоне.
Новая полная ручная проверка загрузки/подвески в этом шаге не заявляется.
`P1.CAR.009` остаётся `PartiallyImplemented`; гидравлические тормоза и полный
donor brake parity не объявляются завершёнными.

Следующий один шаг: отдельная сверка текущей передней и задней сборки с
зафиксированным оригиналом. Она не входит в это закрытие численной регрессии.
