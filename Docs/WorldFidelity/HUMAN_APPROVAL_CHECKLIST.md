# Human approval checklist — Milestone 06B

Текущий статус: **не готово к approval**.

Пользовательские PNG приняты как supplementary evidence, но пакет из двадцати
matched comparisons отсутствует. Ни одна ячейка не может получить
`ProductionCandidate`, `AwaitingHumanApproval`, `Approved` или `Complete`.

## Обязательный пакет на каждую cell

- [ ] 10 donor views с известным camera state, FOV/условиями и SHA-256.
- [ ] 10 matched rejected-before remake views без выгодного reframing.
- [ ] Issue matrix с доказательством для каждого Critical/Spatial finding.
- [ ] Bounded repair только доказанных различий.
- [ ] 10 matched repaired-after views.
- [ ] Overlay/flicker, где это технически возможно.
- [ ] Сохранён root stable ID и cell identity.
- [ ] Проверены road/terrain seam, traversal, vehicle clearance и entrances.
- [ ] Проверены streaming, LOD/bounds и production-only dependencies.
- [ ] В кадрах нет fog/rain/cinematic effects, скрывающих геометрию.
- [ ] Оставшиеся manual-art ограничения перечислены явно.

## Что пользователь должен решить после готового пакета

Для каждой ячейки отдельно выбрать только один статус:

```text
Rejected
NeedsRework
Approved
```

Codex не присваивает `Approved` самостоятельно. `Complete` допустим только
позднее, после explicit approval и всех technical gates.

## Текущие статусы

| Cell | Статус | Причина |
|---|---|---|
| `cell_0_-3` | `Rejected / NeedsRework` | предварительно видны крупные road/building/property differences; canonical capture set отсутствует |
| `cell_0_-2` | `Rejected / NeedsRework` | direct donor pier/shore views отсутствуют; точный diff нельзя устанавливать по памяти |
