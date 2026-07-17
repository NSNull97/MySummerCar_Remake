# Enviro 3 fingerprint migration audit — Milestone 07B

Дата: 2026-07-17. Статус: `MIGRATED / RUNTIME_STABLE / PREFLIGHT_PASS`.

## Причина документа

Принятый в 07A aggregate fingerprint перестал совпадать после того, как Unity 6 канонически пересериализовал один vendor prefab. Это не объявлялось молча «нулевым изменением»: старая и текущая формы, исходный архив и проверки зафиксированы отдельно. После source comparison и повторных runtime/preflight проверок validator constants явно мигрированы на canonical 07B baseline.

## Aggregate fingerprints

| Срез | Файлы | Байты | SHA-256 |
|---|---:|---:|---|
| Принятый 07A baseline | 538 | 305 967 970 | `9a4e8bab6bdf231c415fc8e60f3988f12f221cc20dfbfc0b7090feb14f431af3` |
| Принятый canonical 07B baseline | 538 | 305 967 931 | `8e376fa2748162157975fbdd8b1045e021b810fea40f01d99eafe22a4d30bd44` |

Количество файлов не изменилось; aggregate size уменьшился ровно на 39 байт.

## Единственный изменившийся файл

`Assets/Enviro 3 - Sky and Weather/Prefabs/Lightning/LightningStrike.prefab`:

- current canonical Unity 6 size: `10 642` bytes;
- current SHA-256: `F946C50A1942B8729DA96DF59FB71556712508407647314661A59935E58E33A5`;
- отличие от принятой 07A serialized form: `-39` bytes;
- semantic vendor code/asset edit не выявлен; изменение соответствует canonical Unity 6 YAML reserialization.

## Source chain

| Источник | Размер | SHA-256 | Назначение |
|---|---:|---|---|
| Локальный source archive `Enviro 3 - Sky and Weather.rar` | 247 060 928 | `98840A6B6A62BFCAF84FEBEC422A01C195F39B759AEC0D8A6719E36B51ADCC59` | Read-only package source, предоставленный пользователем |
| Raw package `LightningStrike.prefab` | 9 682 | `1412024F56CBA65CC115001C5827EC809629E21C2FCFEBD484301270581994ED` | Сырая package form до Unity import |
| Unity 6 canonical `LightningStrike.prefab` | 10 642 | `F946C50A1942B8729DA96DF59FB71556712508407647314661A59935E58E33A5` | Результат clean import/ForceReserializeAssets; byte-for-byte совпадает с принятым 07B baseline |

Read-only extraction выполнялась вне Git/production Assets. Raw extraction payload не добавляется в репозиторий.

## Объяснение

Ранняя adapter-итерация временно назначала runtime material непосредственно component source prefab, после чего восстанавливала значение. Значение было восстановлено, но Unity отметил prefab dirty и при выходе из Play Mode записал canonical Unity 6 YAML. Hardening устраняет этот путь: adapter создаёт inactive runtime prefab clone и runtime flash material, а source prefab больше не получает runtime assignment.

Таким образом, текущая 10 642-byte form воспроизводится из лицензированного source package штатной Unity 6 reserialization и не содержит project-authored функционального vendor patch. Это обосновало контролируемую миграцию fingerprint; финальный stability gate выполнен ниже.

## Выполненный stability gate

1. Current aggregate до обновлённых runtime tests зафиксирован как `538 / 305967931 / 8e376fa2…`.
2. `M07B_Enviro3Integration_Hardening_Final_03.xml`: `13/13 PASS`, включая несколько lightning requests, teardown/reload, runtime precipitation clones, exact transition units, callback rollback и WeatherLab smoke.
3. Aggregate после runtime tests повторно совпал; source `LightningStrike.prefab` остался `10642` bytes / `F946C50A…E33A5`.
4. `Enviro3PreflightValidator` и active 07B references явно переведены на canonical baseline.
5. `Logs/M07B_WeatherLabBuilder_Final_03.log`: builder `1.1.0`, fresh-process full preflight `PASS`, `errors=[]`, `warnings=[]`.

Итог: функциональных project-authored изменений vendor source нет; canonical serialized baseline стабилен и готов к использованию как 07C entry evidence.
