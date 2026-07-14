# Major landmarks

29 spatial representatives образуют 10 semantic groups. Таблица ниже показывает по одному navigation representative; полный список находится в `M04A1_WorldLandmarks.csv`.

| Group | Representative stable ID | Source name | Remake position | Cell | Records | Geometry/collider/interior status | Validation |
|---|---|---|---|---|---:|---|---|
| PrimaryHomeGarage | `fb0f962be1b325cc19296c66751818c0` | `garage_shed_roof` | `(0, ~0, 0)` | `cell_0_0` | 1 | Geometry; source collider association available; exterior | Automated transform pass; screenshot pending |
| Bridge | `16445375121da21b9f46882b9d2df474` | `NoRain` | `(1615.712, 8.281, 34.907)` | `cell_3_0` | 3 | Point/bounds proxy; context review required | Data pass; screenshot pending |
| Church | `5922254ed391973d33d7babd6be0b89f` | `FlagPole` | `(-1220.884, 6.771, 121.177)` | `cell_-3_0` | 1 | Navigation representative, not canonical church root | Needs manual verification |
| IslandCottage | `cc1ebee642bc6f1c46285b30093c5a35` | `kitchen_bench 1` | `(-676.192, -1.016, -535.819)` | `cell_-2_-2` | 1 | Interior representative | Needs manual verification |
| Landfill | `161f1947ab0236ca3cc8f38f5eafd13f` | `SHITHOUSE` | `(-609.630, 14.490, -1688.729)` | `cell_-2_-4` | 2 | Sparse geometry; colliders present in root | Needs manual verification |
| MajorWater | `40bfaf26ded22db2438894014bbbd415` | `LAKEBED` | `(2007.215, -2.719, -1834.176)` | `global` | 5 | Large bounds and mesh reference | Bounds/data pass; elevation pending |
| RepairWorkshop | `1d50e7313ee1bdc52fbbf6ea10a3efa3` | `Spawn` | `(-1279.620, 23.770, -1409.485)` | `cell_-3_-3` | 13 | Root-derived navigation representatives | Needs manual verification |
| TownService | `52287202f0ac2d9c79937d5f26a4a0e4` | `VideoPoker` | `(-1374.197, 5.762, 141.972)` | `cell_-3_0` | 1 | Spatial anchor only; name is not canonical landmark | Needs manual verification |
| VehicleInspection | `66481b9e0a97c74cd440dc9268ca28fe` | `office_lamp 11` | `(-1358.920, 7.421, 221.491)` | `cell_-3_0` | 1 | Spatial anchor only | Needs manual verification |
| UniqueStructure | `62baf5836244e43fde82119cde1bb400` | `Landfill` | `(2007.215, -2.719, -1834.176)` | `cell_3_-4` | 1 | Name-based unique structure candidate | Needs semantic review |

Representative selection выбирает shallowest geometry record в каждом 128 м spatial bucket. Это удобная navigation identity, но не гарантирует «главный mesh» локации; слабые names выше намеренно оставлены видимыми как review queue.

Bounds, source scene (`GAME`), mesh GUID, replacement status и transfer status доступны в landmark CSV. Screenshot/reference status для всех, кроме garage transform, остаётся pending.
