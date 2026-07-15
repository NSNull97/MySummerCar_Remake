# Матрица тестирования сборки

| Область | EditMode | PlayMode | Результат M05 |
|---|---|---|---|
| Definitions, categories, unique IDs | количество 15/14, definition/stable uniqueness | scene boot | PASS |
| Compatibility | правильный/wrong mount | handoff через M4 | PASS |
| Deterministic candidate | повторяемый winner ID | preview query | PASS |
| Position/orientation tolerances | distance и angle rejection | M4 handoff у pose | PASS |
| Prerequisites | missing engine block для head | trailing arm initial state | PASS |
| Obstruction/access | explicit obstruction marker | runtime collider path | PASS |
| Install transition | snap, parent, kinematic, loose fastener | carry → handoff, renderer remains active, world scale preserved | PASS |
| Wrong mount/occupied mount | оба rejection | duplicate state protected | PASS |
| Tool compatibility | wrench 11 rejected | wrong tool no mutation | PASS |
| Fastener lifecycle | `Absent → Inserted → Loose → PartiallyTightened → Tightened` | target tighten/reverse | PASS |
| Fastener direction | authored tighten direction maps to runtime turn direction | wrong direction rejected | PASS |
| Removal gate | tightened fastener blocks | full runtime path | PASS |
| Wheel blocker | installed wheel blocks drum | user flow | PASS |
| Detach transition | mount release, dynamic body | pickup-ready after remove | PASS |
| Save DTO | JSON round trip, duplicate и occupancy mismatch rejection | inserted/seated/stage restore | PASS |
| Graph validation | install cycle detection | — | PASS |
| Query API | missing/unsecured/completeness/connection | preview feedback | PASS |
| Donor boundary | clean prefab path/reference marker | scene boot without donor | PASS |
| Performance | 10 000 queries, allocation/mutation audit | preview hot path | PASS |

Автоматические результаты от 2026-07-14:

- filtered M05 EditMode: `22/22`;
- filtered M05 PlayMode: `8/8`;
- M05 static validator: PASS;
- performance audit: `10 000` queries, `0 B` managed allocations, `0` graph mutations.

Ручной smoke test остаётся обязательным для субъективной читаемости preview, удобства допуска, физического ощущения carry и проверки HDRP Game View.
