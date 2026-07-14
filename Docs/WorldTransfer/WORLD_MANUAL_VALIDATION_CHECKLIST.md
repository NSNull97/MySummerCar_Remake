# Ручная проверка reference world

## Как открыть

1. Открыть проект Unity 6000.3.11f1.
2. Выполнить `Tools > MSC Remake > World Transfer > Open Reference Overview`.
3. Для полной пересборки использовать `Rebuild Generated World Content`, затем `Validate Generated Scenes`.
4. Запустить Play Mode для development free-fly camera; loader намеренно disabled, потому что ignored ReferenceOnly scenes не входят в release Build Settings.

## Checklist

- [ ] Сравнить primary home/garage с donor capture.
- [ ] Сравнить все группы из `MAJOR_LANDMARKS.md`.
- [ ] Сравнить major town/service и repair workshop.
- [ ] Сравнить inspection, church, cottage и landfill.
- [ ] Проверить крупные road junctions и изменение высоты дорог.
- [ ] Проверить bridge positions и подходы к ним.
- [ ] Сравнить lake elevation, shoreline, docks и острова.
- [ ] Сравнить building exteriors и major remote buildings.
- [ ] Проверить interior/exterior alignment, floors, stairs, doors и gates.
- [ ] Сравнить driveways/yards, signs, poles и wires.
- [ ] Протестировать непрерывные driving surfaces на collider replacement prototype, когда он появится.
- [ ] Включить review 2 007 bounds markers и проверить крупные combined meshes.
- [ ] Проверить 37 unsupported class IDs на геометрическое влияние.
- [ ] Осмотреть cell boundaries и возможные seams/parent splits.
- [ ] Проверить terrain coverage и extreme Y records.
- [ ] Проверить, что production prefabs не зависят от `ReferenceOnly/World/Generated`.

## Donor capture

Запуск donor допустим только обычным способом через лицензированную установку. Не патчить executable и не подключать automation/DRM bypass. Для каждой точки записать world location/context, camera orientation, FOV, resolution и screenshot time; хранить captures во внешнем `captures/world/milestone-04a1`. В remake перейти к landmark через registry/scene proxy и повторить ракурс.

Результаты checklist следует вписать в fidelity report отдельным будущим review change. В этой задаче ручные пункты не отмечены как выполненные.
