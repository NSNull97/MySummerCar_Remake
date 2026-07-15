# Streaming validation — Milestone 05B.1

Дата: 2026-07-15
Итог: **production wiring PASS; runtime lifecycle PASS**.

## Production contract

`ProductionWorldStreamingManifest` содержит ровно две принятые production cells:

| Cell | Coordinates | Build index | Scene |
|---|---:|---:|---|
| `cell_0_-3` | `(0,-3)` | `6` | `Production_cell_0_-3.unity` |
| `cell_0_-2` | `(0,-2)` | `8` | `Production_cell_0_-2.unity` |

Параметры: cell size `512 m`, load radius `0`, unload radius `1`.
Bootstrap содержит явный `ProductionWorldStreamingInstaller`, M4 player prefab и
focus transform. Runtime не выполняет поиск player/object по имени.

Service:

- выбирает cell по focus position;
- загружает только по build index;
- до принятия load проверяет точный scene path;
- применяет hysteresis между load/unload radii;
- ведёт собственный set загруженных сцен;
- выгружает только owned scenes и не забирает ownership у заранее загруженной сцены.

Strict `WorldPilotGateRemediationValidator`: **PASS**, `0 errors / 0 warnings`.
Проверка учитывает `EditorOnly` не только на composition object, но и по всей
parent transform chain.

## Fingerprinted lifecycle evidence

Файл: `Docs/WorldValidation/M05B1_PRODUCTION_STREAMING_LIFECYCLE.json`.

PlayMode fixture начинается с чистого `Bootstrap` и выполняет два цикла:

1. `pilot` — owned count `1`;
2. `pilot+next` — `2`;
3. `next` — `1`;
4. `none` — `0`;
5. та же последовательность повторно.

На обоих циклах подтверждены:

- exact stable-ID snapshots: `7` для pilot и `8` для next zone;
- отсутствие duplicate stable IDs;
- уничтожение pilot/next roots после unload;
- отсутствие orphan ownership;
- manifest, Bootstrap и implementation SHA-256 fingerprints;
- Unity `6000.3.11f1`.

Stale JSON удаляется перед тестом и не создаётся при failure. Canonical runner
закрывает `WORLD-STREAM-002` только при `executed=true && passed=true` у отдельного
validator run `production-streaming-lifecycle`.

## Закрытые findings

- `WORLD-STREAM-001` — production streamer подключён и strict wiring validator проходит;
- `WORLD-STREAM-002` — двухцикловый runtime lifecycle доказан fingerprinted evidence.

## Открытые streaming contracts

- `WORLD-STREAM-003`: peak/recovered memory, persistence и чистый load/unload hitch limit;
- `WORLD-STREAM-004`: непрерывный multi-cell road, water, large-object, interior и persistent-landmark contracts;
- `WORLD-STREAM-005`: две принятые 05C1 safety cells намеренно не включены в runtime manifest.

Performance probe записал transition wall-clock и diagnostic frame delta, но
verification PNG предыдущей точки мог попасть в следующий delta. Поэтому этот
показатель не закрывает `WORLD-STREAM-003`.

Streaming lifecycle и M4 traversal проверяются отдельными дополняющими fixtures;
единый streamed end-to-end walkthrough пока не заявляется.
