# M05C1 — Контрольный список непрерывной земли у Теймо

Статус ручной проверки: **PASS — подтверждено пользователем 2026-07-15**

Этот checklist относится только к региону `M05C1-VOID-TEIMO-001` в
`cell_-4_0` и `cell_-3_0`.

## 1. Подготовка

1. Открой проект в закреплённой версии Unity `6000.3.11f1`.
2. Дождись завершения компиляции и импорта без ошибок Console.
3. Выполни:
   `Tools > MSC Remake > World Transfer > 05C1 > Rebuild Teimo Continuous Ground Baseline`.
4. Выполни:
   `Tools > MSC Remake > World Transfer > 05C1 > Validate Teimo Continuous Ground Baseline`.
5. Ожидай success token `WORLD_MAP_05C1_VALID` и отсутствие validation errors.
6. Открой совместный просмотр:
   `Tools > MSC Remake > World Transfer > 05C1 > Open Teimo Reference + Fill Review`.

Не сохраняй изменения в reference-сценах после ручного перемещения объектов.

## 2. Проверка границы scope

- [x] Загружены ровно две 05C1 fill-сцены:
  `VoidFill_cell_-4_0.unity` и `VoidFill_cell_-3_0.unity`.
- [x] В обеих сценах виден root `WR_05C1_VoidFill_<cell>`.
- [x] Обе части относятся к region `M05C1-VOID-TEIMO-001`.
- [x] Pieces имеют IDs `piece_cell_-4_0` и `piece_cell_-3_0`.
- [x] Stable IDs равны `9c32cc25615de19b4d8ce23a2ade1b5b` и
  `a708a95ca385ee7e5dbdf56bf2d26eca` соответственно.
- [x] На marker установлен `IntentionalDonorVoid`.
- [x] `SafetyTopologyBaseline=true`.
- [x] `OpensGameplayArea=false`.
- [x] Старые tree-card/boundary objects не удалены и не смещены.
- [x] За пределами bounded Teimo region новая земля не появилась.

## 3. Визуальная проверка поверхности

Осмотри район сверху и под косым углом. Для ориентира полный bounded AABB:

- `x=[-1742, -1447.7]`;
- `z=[100, 225]`;
- seam двух ячеек: `x=-1536`.

- [x] Пустота за лесной границей у Теймо закрыта непрерывной поверхностью.
- [x] Между исходной землёй и fill нет видимой щели.
- [x] На `x=-1536` нет разрыва, ступени или перекрывающихся мерцающих faces.
- [x] Fill не образует вертикальной стены, шипа или перевёрнутых triangles.
- [x] Поверхность не перекрывает дорогу и площадку магазина Теймо.
- [x] Поверхность не входит в здание магазина или другие строения.
- [x] Вода/shoreline не закрыты землёй.
- [x] Material выглядит как нейтральный first-pass terrain, а не donor texture.
- [x] Tree-card boundary остаётся исходной визуальной границей зоны.

## 4. Проверка коллизии

Включи отображение colliders в Scene View или Physics Debugger.

- [x] На каждой из двух частей ровно один `MeshCollider`.
- [x] Оба collider статические, non-convex и без `Rigidbody`.
- [x] Collider использует тот же mesh, что и `MeshFilter`.
- [x] Collision surface покрывает всю видимую fill-поверхность.
- [x] На seam `x=-1536` нет collision gap.
- [x] Нет невидимого blocking collider над дорогой, у магазина или над водой.
- [x] При inspection/probe нет возможности провалиться сквозь заполненный регион.

Этот этап не создаёт отдельную gameplay/playtest-зону. Не считать отсутствие
нового player route дефектом 05C1.

## 5. Reference evidence captures

При необходимости выполни:

`Tools > MSC Remake > World Transfer > 05C1 > Capture Teimo Void Evidence`.

Для отдельного overlay после rebuild выполни:

`Tools > MSC Remake > World Transfer > 05C1 > Capture Teimo Reference + Fill Review`.

Локально под `PerformanceCaptures/Milestone05C1/` должны появиться:

- `Teimo_AllReference.png`;
- `Teimo_SurfacesWithoutVegetation.png`;
- `Teimo_BoundaryVegetation.png`;
- `Teimo_ReferencePlusFill.png`;
- `Teimo_FillOnly.png`.

- [x] Кадры подтверждают, что рассматривается именно граница около Теймо.
- [x] Отдельный vegetation view показывает исходную визуальную стену.
- [x] Оранжевый review overlay показывает обе части fill и непрерывный seam.
- [x] Fill-only view не содержит третьей части или геометрии вне pilot AABB.
- [x] Captures не используются как runtime assets и не входят в Git.

## 6. Результат

Заполни после проверки:

- Дата: `2026-07-15`
- Проверил: `пользователь`
- Визуальная непрерывность: `PASS`
- Коллизия: `PASS`
- Scope boundary: `PASS`
- Итог: `PASS — «подтверждаю, пока ок»`
- Найденные дефекты: `не обнаружены при текущей bounded-проверке`

Если любой пункт не проходит, приложи screenshot, укажи cell, world position и
название объекта. Не расширяй исправление на другие void regions без нового
явного scope.
