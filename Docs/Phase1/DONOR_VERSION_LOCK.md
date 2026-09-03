# Phase 1 — фиксация версии донора

Статус: **Locked**

Дата решения пользователя: **2026-07-21**

Авторитетная ревизия: **`msc-world-baseline-04a1.1-c3f2f337`**

Версия базы импорта: **`04A1.1`**

## Решение

Для Phase 1 используется только замороженное состояние, из которого была
получена и принята карта Milestone 04A1. Текущая переустановленная копия игры и
последующие runtime-capture могут быть вспомогательным материалом, но не могут
добавлять, удалять или переопределять строки parity matrix без отдельной новой
ревизии, diff-аудита и решения пользователя.

Нельзя смешивать файлы или поведение разных установок под одним build ID.

## Идентификация продукта

| Поле | Зафиксированное значение |
|---|---|
| Steam AppID | `516750` |
| Depot | `516751` |
| Steam build | `20171487` |
| Manifest | `7688087324231130953` |
| Depot size | `922581588` bytes |
| App manifest LastUpdated | `2025-10-14T09:12:00Z` |
| Поддерживающий changelog | `v.250908-04`, 2025-09-08; не является самостоятельным authority |
| Donor Unity | `5.0.0f4 (5b98b70ebeb9)` |
| Executable file version | `5.0.0.6002871` |
| Frozen extraction | 2026-07-14 |
| Canonical source re-verification | 2026-07-16 |

## Хеши исполняемых и managed-файлов

SHA-256:

| Файл | SHA-256 |
|---|---|
| `mysummercar.exe` | `ffc59ccbf20af4dff5c1406a434f616893ad2242be879b215e17debe0da1c0b0` |
| `Assembly-CSharp.dll` | `10b25ae12fd1fd563c15f42a47e92f9dfa405d3d5355c7ec97da160b1c7c864f` |
| `Assembly-CSharp-firstpass.dll` | `6046b79f03889cf3fb1faf040bd79ad7c69dedf9e3a920dc0cfc2ed6c0b4c149` |
| `Assembly-UnityScript.dll` | `f9c331e58a694e07458d0434b6612ef0376d957e2d593ad8ec62a06871c3d3fc` |
| `Assembly-UnityScript-firstpass.dll` | `638e8996b7851d9479ae784ee46b76946ef66926f3d414810d7cdde175aa5bc6` |
| `PlayMaker.dll` | `6cf097d27fafcd0717a65c6d9f8cbfd62ae88bb94e788c62e198da1a3015e95d` |
| `ES2.dll` | `863733f06a0d988f9e71db3a5d7cf5db5de8108b40f106f5673d303018b02d24` |

Внешний реестр `DONOR_RELEVANT_HASHES_2026-07-13.csv` имеет SHA-256
`d44208b655bc32f98228b7437f053ef400af917746e12daf6668344d805cd7c7`.

## Хеши frozen scene/asset payload

| Файл | SHA-256 |
|---|---|
| `mainData` | `bdeb2298a71b45bcce81d1d91e6f3fde5c8954b5b76f88535de8fe17bdd66931` |
| `level0` | `0da8800d7ef65dd9a368ab878443e8a84e77e99ac167d7945ed29c22c324d130` |
| `level1` | `aa37a88817372a44137fe8813a82ae43ebf8ada526a6eb2ad1d143147de596b0` |
| `level2` | `39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31` |
| `level3` | `334667c90459c78869b18ba32e4d945f532e1c3f644ac3f33b067a3602b73642` |
| `resources.assets` | `292106e4f021b1214ea7d4213763ad25d1073233945ccbd38c5528c24f1d29e1` |
| `resources.resource` | `9801e4a9d2ac8f130d988cd0fcfd16ad0ff5a422ba9a4e256753ebc2db64b5c0` |
| `sharedassets0.assets` | `cb4c806ba1be6f6b9d46579e9907c77d64c60e515da71e5df749eaa159edad97` |
| `sharedassets1.assets` | `8f0a0984f4e55229ecaebb57ef931b052f56998b9569a32e013780aa9dc78e02` |
| `sharedassets1.resource` | `76b6ae4802bd98e0dc12d10f385d3e520e08078e005ed4cb9a41c68365fc34cc` |
| `sharedassets2.assets` | `a90f2ddca72bf25b691f54565a808ceabae34af2392bbbc72b9d8eb5d447608f` |
| `sharedassets2.resource` | `6337b97796e05342fc04756f2cd274b4f671227ab0dbb47c5e8b9d4a7b379231` |
| `sharedassets3.assets` | `1e956c8acd9f3c075b2e4eece3d836228eeb3ad0a18ae41ad1ca6aedb3c9d684` |
| `sharedassets3.resource` | `19797fa0386c74d091b530b874a03325191e002c921767240c71ba7791a5a20b` |
| `sharedassets4.assets` | `17132def711445c14ffcc5987aececeee15645c73ef4c2b87f9d58e86f262a49` |
| `sharedassets4.resource` | `1a94c197d062cdecd6c292bf2a9718fbf240f460936cbbe5cc321ac3406206b1` |

## Канонические производные

| Артефакт | SHA-256 |
|---|---|
| Exported `GAME.unity` | `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4` |
| `path_id_map.json` | `dfdbd71aac8a942c78f27dfd54d73ff3f1fdce3670271482a6fd2c6aa8c73165` |
| Exported `ProjectVersion.txt` | `3ca7f33892922c75f49fad3146fa8a5cc4930fa4e8ac946af5ac984b1d5eb29a` |

Канонический относительный источник: `raw/world/milestone-04a1`. Наблюдавшийся
локальный путь является только способом разрешения источника и не входит в
идентичность ревизии.

## Авторитет evidence

- `Docs/WorldBaseline/CANONICAL_DONOR_MAP_SOURCE.md`;
- `Docs/WorldTransfer/DONOR_WORLD_SOURCES.csv`;
- `Docs/Milestones/MILESTONE_04A1_REPORT.md`;
- `Docs/Porting/DONOR_AUDIT.md`;
- `Docs/ReferenceCapture/REFERENCE_SOURCE_MAP.md`;
- `Docs/ReferenceCapture/CAPTURE_SESSION_LOG.csv`;
- `Docs/WorldFidelity/DONOR_EVIDENCE_INDEX.csv`;
- frozen AssetRipper manifests и hashes во внешнем donor staging.

Capture-набор 04B используется только как узкое поведенческое подтверждение:
видео не содержит встроенного build ID и не может самостоятельно изменить scope.

## Явно исключённое состояние

Переустановленная копия по ожидаемому пути donor installation исключена из
Phase 1 authority. При одинаковом Steam build обнаружены несовпадающие payload:

- текущий `sharedassets3.assets`: `533259176` bytes / SHA-256 начинается с
  `9511802c`; frozen: `533373096` bytes /
  `1e956c8acd9f3c075b2e4eece3d836228eeb3ad0a18ae41ad1ca6aedb3c9d684`;
- текущий `sharedassets3.resource`: `625111663` bytes / SHA-256 начинается с
  `53aa0a21`; frozen: `625805147` bytes /
  `19797fa0386c74d091b530b874a03325191e002c921767240c71ba7791a5a20b`.

Одинаковый build ID не считается доказательством одинакового содержимого.
Любое будущее извлечение получает новую revision ID и проходит quarantined diff.

## Save-version evidence

Точная donor save schema **Unknown / not captured**. Доступны только косвенные
индикаторы ES2/`UniqueSaveManager` и метаданные файлов; содержимое save не
копировалось, не разбиралось и не хешировалось. Это не блокирует file/version
lock, но блокирует утверждения о точном donor-save импорте до отдельного
read-only evidence capture в 09A.

## Ограничения evidence

- frozen состояние могло содержать модификации; lock означает точное принятое
  наблюдаемое состояние, а не заявление о чистой stock-установке;
- `Mods`, MSCLoader, 0Harmony, doorstop, Resource Importer и производные
  вспомогательные сборки не являются evidence базовых функций;
- один несвязанный `Texture2D` не прочитан, 37 serialized class IDs не
  поддержаны экспортёром, 2007 bounds-записей требуют review;
- serialized geometry не доказывает runtime-instantiated поведение.
