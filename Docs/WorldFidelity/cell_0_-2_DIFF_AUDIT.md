# Diff audit — `cell_0_-2`

Статус: **BlockedNotExecuted**

## Почему точный audit невозможен

Пользователь прямо отклонил сходство production shoreline/pier cell с
оригиналом. Однако в доступном runtime evidence нет доказанного вида:

- домашнего пирса;
- входа на пирс;
- обратного вида к дому;
- бокового силуэта пирса и опор;
- берегового профиля;
- трёх hedge segments в их shoreline-контексте.

`M06B-EVD-002` снят в дождь/туман с большой высоты. Его точная world location и
отношение к `MAP/PierHome` не доказаны. Использовать его как основание для
изменения берега или пирса означало бы угадывать.

## Что известно без визуального audit

- serialized anchors и stable IDs существуют;
- production scene проходит технические collision/streaming проверки;
- существующие `ReferenceOnly` captures показывают только metadata/proxy
  anchors, а не donor silhouette;
- пользовательский статус остаётся `Rejected / NeedsRework`.

## Решение

Ни один конкретный spatial/visual defect пирса не объявлен по памяти или
generic reference. Issues `FID-CELL-0-M2-001..002` остаются заблокированными до
десяти прямых donor views. Production geometry не изменялась.
