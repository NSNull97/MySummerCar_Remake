# BetterMSC map remediation — отчёт переноса

Дата: 2026-07-24  
Статус: `Generated / ManualAcceptancePending`  
Классификация: `TemporaryDirectImport`, только приватная Phase 1.

## Что было проверено

Read-only изучены:

- `BetterMSC.dll`;
- `BetterMSC.unity3d`;
- AssetRipper-export выбранных объектов;
- текущая архитектура `World_Global_Legacy` и streaming profile.

Связанный `NavMesh` bundle инвентаризирован и захеширован, но не импортирован:
для закрытия земли и замены визуальных стен чужая навигационная сетка не нужна.

IL `BetterMSC.dll` подтвердил точное назначение объектов:

- `MissingTerrain` закрывает отсутствующие участки земли;
- `Forests` добавляет детализированные группы деревьев, кустов и их
  коллайдеров;
- `TREEWALL_LOW` и `TREEWALL_HI` заменяют исходные меши лесных стен;
- `worldsmostsimplefix` удаляется как устаревшая физическая заглушка.

DLL, bundle и raw AssetRipper-export не добавлены в Git и не требуются во время
игры.

## Что перенесено

Добавлена повторяемая Editor-команда:

`Tools/MSC Remake/World Baseline/Apply BetterMSC Map Remediation`

Команда:

1. читает только утверждённый AssetRipper-export из внешнего donor staging;
2. разрешает зависимости четырёх выбранных prefab-групп;
3. копирует их в игнорируемый локальный runtime baseline;
4. создаёт HDRP Lit alpha-clip/double-sided материал леса без donor shader;
5. добавляет `Forests` и `MissingTerrain` в `World_Global_Legacy`;
6. назначает лесным и грунтовым коллайдерам слой `WorldSolid` и проектный
   `WorldSolidZeroBounce`;
7. заменяет меши `TREEWALL_LOW` и `TREEWALL_HI`;
8. заменяет активный collider `TREEWALL_LOW`;
9. выключает `worldsmostsimplefix`;
10. сохраняет стабильные project-owned metadata IDs на новых группах.

Итоговый локальный generated payload:

- 47 выбранных source dependencies;
- 40 mesh assets;
- 4 prefabs;
- 1 forest atlas;
- 1 новый HDRP compatibility material;
- около 261 МБ вместе с `.meta` и импортированными зависимостями.

### Исправление системы координат

Первый generated-вариант ошибочно сохранял корневой поворот `+90° X` из
AssetRipper-prefab и размещал новые группы в мировом нуле. При этом вершины
экспортированных мешей уже были приведены к Y-up. В результате лес стоял
вертикально, а `MissingTerrain` не совпадал с отверстиями исходной карты.

Исправленный генератор воспроизводит подтверждённую IL-схему BetterMSC:
объекты создаются с локальным поворотом `+90° X` относительно donor
`/MAP/MESH`. В проектной flattened-сцене эквивалентом этой матрицы служит уже
перенесённый объект `MAP/MESH/TERRAIN_OBJ/Grass1`. Его world pose теперь
используется как канонический Y-up anchor для `Forests` и `MissingTerrain`.

Добавлена fail-fast проверка world bounds:

- обе группы должны занимать карту по `X/Z` не менее чем на 1000 м;
- высота `Forests` не может превышать 250 м;
- высота `MissingTerrain` не может превышать 100 м;
- position, rotation и scale обязаны совпадать с каноническим anchor.

Это исключает повторное применение осевого поворота и случайное смещение
слоёв относительно карты.

Generated payload находится под:

`Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/BetterMscMapRemediation`

Он игнорируется Git по общей политике runtime baseline.

## Совместимость

- Активный streaming manifest не изменён.
- Состав и радиусы загрузки 49 cell scenes не изменены.
- Remediation находится в уже существующей global scene, поэтому непрерывные
  меши не разрезаются по ячейкам.
- Player, Interaction, Save, UI, Weather, Audio и Vehicle не изменены.
- Runtime не выполняет поиск по donor-именам и не загружает BetterMSC bundle.
  Donor hierarchy paths используются только Editor-командой при сборке сцены.

## Выполненные проверки

Выполнен Unity 6.3 batch compile/import и сама Editor-команда.

Результат:

`BETTERMSC_MAP_REMEDIATION_APPLY_OK dependencies=47 terrainVoidFill=1 forests=1 treeWalls=2`

Автоматические EditMode/PlayMode тесты по указанию пользователя не запускались.

Unity сообщил об уже известном риске PhysX: несколько глобальных legacy
MeshCollider содержат треугольники с рёбрами более 500 метров. Импорт не
тесселирует эти reference-меши, чтобы не менять их геометрию без ручного
сравнения. Нужно проверить стабильность столкновений в проблемных местах.

## Ручная проверка

Открыть `Assets/Game/Bootstrap/Bootstrap.unity`, запустить Play Mode и выбрать
новую игру или рабочее сохранение.

Проверить:

1. Дом игрока → дорога → магазин Теймо: вместо большинства плоских стен леса
   видны группы деревьев и кустов.
2. Известные пустоты за прежними sprite-wall зонами закрыты грунтом; игрок и
   предметы не проваливаются.
3. Около Теймо и других ранее ограждённых зон нет чёрных просветов за краем
   рельефа.
4. В новые деревья и кустовые collider-группы можно упереться, но дорога и
   обязательные проходы не перекрыты.
5. TREEWALL не создаёт двойной геометрии, мерцания или невидимой старой стены.
6. Перенос игрока дом ↔ Теймо ↔ дальняя ячейка не выгружает global remediation
   и не ломает cell streaming.
7. Проезд автомобиля по дорогам рядом с исправленными зонами не даёт
   неожиданных ударов о лесные коллайдеры.

## Ограничения

- Это временная donor-derived Phase 1 геометрия, не production art.
- Forest atlas и объединённые меши требуют замены в Phase 2.
- Точный performance budget новых глобальных групп должен быть подтверждён
  ручным playtest/profiler-проходом.
- `NavMesh` bundle пока оставлен только reference evidence: импорт чужой
  навигации не нужен для закрытия земли и замены визуальных стен.

## Следующий рекомендуемый этап

После ручной приёмки этой карты вернуться к следующему незавершённому пункту
Phase 1 execution plan, не расширяя текущий world-remediation scope.
