# 04B — подтверждение blocked removal заднего тормозного барабана

Дата: 2026-07-14
Session ID: `04B-RUNTIME-DRUM-BLOCKED-REMOVAL-20260714`
Классификация: `BehavioralReference`, user-reported runtime observation

## Наблюдение

После ревью видео `My Summer Car 2026-07-14 18-52-48.mp4` пользователь отдельно подтвердил наблюдавшийся в donor runtime сценарий:

> При установленном заднем левом колесе снятие заднего левого тормозного барабана блокируется.

Это подтверждение не является frame-addressable video evidence и поэтому учитывается с `Medium` confidence. Donor installation и save в рамках документирования не изменялись.

## Сопоставление с остальными evidence

- exact static trace требует active `TriggerWheelRL_New` и `Drumbrake_RL.Data.Bolted=false` для removal;
- user observation подтверждает смысл wheel-state gate: установленное колесо блокирует снятие барабана;
- runtime-видео `18-52-48` показывает три успешных снятия при отсутствующем колесе после полного обратного fastener progression;
- static trace candidate использует sphere trigger radius `0.01 m`, tag/child identity и не содержит отдельного angular compare;
- то же runtime-видео трижды показывает успешный snap при попадании loose drum в candidate overlap.

Таким образом, donor mount rule определён collider-overlap/candidate gate, prerequisites и wheel-state removal blocker. Отдельное числовое angular tolerance не объявляется, потому что оно отсутствует в traced donor rule; remake orientation tolerance будет явным project-authored tuning, а не выдуманным donor measurement.

## Coverage decision

`P0-ASSEMBLY-MOUNT-RULE`: **Covered**.

Совокупность evidence покрывает candidate identity, positional overlap gate, prerequisites, snap/completion, три одинаковых install/remove outcomes и wheel-installed blocked removal. `P0-ASSEMBLY-FASTENER-SEMANTICS` уже покрыт static trace и тройным runtime progression.

Ограничение: подтверждение blocked case дано оператором текстом без отдельного видео; если позднее появится frame-addressable capture, его следует добавить как stronger evidence, не меняя текущий behavioral contract без выявленного противоречия.
