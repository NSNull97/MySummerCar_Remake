# Phase 1 — политика donor quirks и defects

Категории фиксированы: `IntentionalRequiredParity`,
`RecognizableOptionalCompatibility`, `SafeDifference`,
`LegacyEngineDefectToFix`, `UnknownRequiresUserDecision`.

| ID | Наблюдение | Категория | Evidence | Решение / owner |
|---|---|---|---|---|
| QRK-001 | Неудобство и ручность сборки/крепежа/доставки предметов | IntentionalRequiredParity | Frozen scripts/FSM metadata; M05 captures | Сохранить правила и pacing; 09B/11A |
| QRK-002 | Физические предметы вместо бездонного RPG-инвентаря | IntentionalRequiredParity | Frozen ITEMS hierarchy; guardrails | Сохранить физический мир; 09B |
| QRK-003 | Отсутствие постоянного GPS/quest tracker и точных needs процентов | IntentionalRequiredParity | Donor captures; UI guardrails | Сохранить restrained UI; 09C/12B |
| QRK-004 | Временные world voids, sprite walls и under-map hacks | RecognizableOptionalCompatibility | 06B debt catalogue; user acceptance | Допустимы в private Legacy baseline, но не как production art; 14C/Phase 2 |
| QRK-005 | Donor exploits/undefined physics без progression necessity | SafeDifference | Требуется per-case evidence | Не переносить автоматически; решение через matrix HumanDecision |
| QRK-006 | Crash, data loss, permanent softlock или невосстановимый OOB | LegacyEngineDefectToFix | Project safety policy | Исправлять с сохранением gameplay rule; 09A/14B/15C |
| QRK-007 | Непроходимый штатный порог/ступень при корректном маршруте | LegacyEngineDefectToFix | User report: pub pass, Teimo fail | Collider-first audit и traversal regression; 09C |
| QRK-008 | Door collision до реализации project-owned opening mechanics | SafeDifference | User decision 2026-07-21 | Временно без solid door blockers; затем project-owned doors; 12A/14B |
| QRK-009 | Материалы donor baseline выглядят тускло/блестяще/нефизично | SafeDifference | 08A1 screenshots and user acceptance | Временно принято; production replacement Phase 2 |
| QRK-010 | Terrain/road textures низкого качества | SafeDifference | 06B user captures | Не блокирует Phase 1 при читаемой геометрии; Phase 2 |
| QRK-011 | Точная длительность/clearance переходов двойного приседа | UnknownRequiresUserDecision | Serialized heights известны, runtime transitions не измерены | Capture/spec до implementation; 09C |
| QRK-015 | Пешком наклон вперёд; в машине наклон влево/вправо только через открытое окно или дверь | IntentionalRequiredParity | Frozen FSM evidence + user runtime clarification 2026-07-21 | Зафиксировать как разные действия/preconditions; 09C/11A |
| QRK-012 | Точная donor save schema и несовместимые save quirks | UnknownRequiresUserDecision | Save contents not captured | Read-only audit и user decision по importer; 09A |
| QRK-013 | Случайные микрофризы старого runtime | SafeDifference | Narrow user observations only | Не воспроизводить; измерять новый runtime; 15B |
| QRK-014 | Permadeath и наказания, если включены правилами donor | IntentionalRequiredParity | Frozen gameplay metadata; roster evidence | Реализовать как режим/состояние по evidence; 13B |

Project-only дефекты не объявляются donor quirks. Проблема ступеней у Теймо
сейчас является gap нового проекта; до donor comparison она не доказывает
аналогичное поведение оригинала.
