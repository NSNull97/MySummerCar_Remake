# Обнаружение donor-источников мира — Milestone 04A1

Дата аудита: 2026-07-14. Режим: `ReferenceGeometryFirst`.

## Результат

Основной геометрический мир находится в serialized-сцене `GAME`, соответствующей donor-файлу `mysummercar_Data/level2`. Геометрические payload и resource stream сцены связаны с `sharedassets3.assets` и `sharedassets3.resource`. AssetRipper 1.3.14 прочитал их без записи в donor-каталог и восстановил внешний Unity-проект.

Установка donor-модифицирована MSCLoader/doorstop и не считается чистым stock baseline. Все результаты привязаны к точным SHA-256 текущих файлов. Донор не изменялся.

## Источники

| Источник | Назначение | Извлечение | Уверенность |
|---|---|---|---|
| `mysummercar_Data/level2` | Serialized scene `GAME`, объекты, компоненты, transforms и hierarchy | Успешно | Высокая для serialized-состояния этой установки |
| `mysummercar_Data/sharedassets3.assets` | Mesh/material/texture references сцены | Успешно для геометрии; одна Texture2D не прочитана | Высокая для mesh metadata, средняя для визуального payload |
| `mysummercar_Data/sharedassets3.resource` | Resource stream для shared assets | Успешно | Высокая |
| `ExportedProject/Assets/_Scenes/GAME.unity` во внешнем staging | YAML-представление для нормализации | Успешно | Высокая, привязано к hash |

AssetRipper выгрузил пять сцен и 17 247 файлов общим объёмом 1 754 855 811 байт. В 04A1 нормализована только геометрически значимая сцена `GAME`; menu/intro/ending сцены не являются источниками карты.

## Runtime-generated и неподдерживаемые данные

Полный перенос runtime-instantiated поведения, PlayMaker FSM, NPC/traffic, procedural effects и scripts не выполнялся: это вне геометрического Milestone 04A. Все 37 встреченных class ID без специального геометрического процессора сохранены в `WorldUnsupportedObjects.csv` как `MetadataOnly`, а component IDs остаются в placement-таблице.

`WorldObjectPlacements.csv` сохраняет все 36 045 GameObject/Transform записей. Из них 13 509 имеют mesh, renderer или collider; 3 842 после контекстной фильтрации принадлежат reference-world слою. Ragdoll, мини-игры, rally cars/spectators и динамические store boxes не удалены из инвентаря, но помечены `ClassifiedNonWorld`.

## Известные пробелы

- AssetRipper сообщил об одной нечитаемой `Texture2D`; она не блокирует геометрию и не используется как production texture.
- 2 007 записей имеют безопасно отклонённые bounds объединённых static/combined meshes; вместо ложного гигантского bounds используется placement point.
- Смысл 37 unsupported serialized class IDs не переносился; данные перечислены для дальнейшего аудита.
- Доказательство визуального совпадения с запущенным donor отсутствует до выполнения ручного checklist.
- Road centerlines, terrain heightfield и portal graph как отдельные высокоуровневые структуры не восстановлены: в 04A1 сохранены mesh/placement/collider references.

Полный машинный inventory находится во внешнем staging и в [DONOR_WORLD_SOURCES.csv](DONOR_WORLD_SOURCES.csv).
