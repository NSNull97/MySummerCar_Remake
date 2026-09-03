# Milestone 08B report — Phase 1 scope lock and donor feature-parity audit

Статус: **UserApproved / 09A Authorized**

Дата: **2026-07-21**

Donor authority: **`msc-world-baseline-04a1.1-c3f2f337`**

## 1. Результат

Создан authoritative draft полного Phase 1 scope для приватной Legacy Feature
Complete сборки. Gameplay-код, сцены, prefabs и assets не изменялись. Milestone
09A не начат.

Матрица содержит **533** project-owned FeatureId:

- **517** required rows;
- **16** evidence-кандидатов с `Required=No`;
- **0** duplicate/empty IDs;
- **0** required rows без owner milestone;
- **0** required `Unknown` или `Blocked` rows на момент аудита;
- все owner значения находятся в диапазоне 09A–14B.

Текущие статусы отражают не будущую цель, а реальное состояние repository:

| Status | Count |
|---|---:|
| `Verified` | 4 |
| `KnownDifferenceApproved` | 1 |
| `PartiallyImplemented` | 35 |
| `Specified` | 6 |
| `EvidenceCaptured` | 471 |
| `Unknown` | 13 |
| `Blocked` | 1 |
| `RejectedNotInLockedDonorVersion` | 2 |

`Unknown` и `Blocked` относятся только к `Required=No` кандидатам: optional
donor-save importer evidence, неподтверждённые identities/recovery endpoint,
условные needs, dormant memorial states, debug call и candidate item types.

## 2. Donor version lock

Выбран замороженный 04A1 source, на котором построен принятый world baseline.
Lock содержит:

- Steam AppID/depot/build/manifest evidence;
- Unity и executable version strings;
- SHA-256 executable, managed assemblies и relevant hash register;
- SHA-256 frozen `mainData`, `level0–3`, resources/sharedassets payload;
- hashes `GAME.unity`, `path_id_map.json` и exported project version;
- extraction/reverification dates и canonical relative source;
- explicit exclusion текущей переустановленной копии.

Текущая donor installation не смешивается с frozen evidence: несмотря на тот
же build ID, `sharedassets3.assets` и `.resource` имеют другие размер/хеш.

Точная donor save schema остаётся `Unknown / not captured`. Это не блокирует
native save foundation, но optional importer нельзя проектировать как
совместимый до read-only анализа 09A.

## 3. Методы аудита

Прочитаны и сопоставлены:

- `AGENTS.md`, 08B prompt, current-state и design guardrails;
- milestone reports и accepted baseline через 08A/08A1;
- donor audit, ledger, system map, reference-capture and world-fidelity docs;
- canonical world/source/hash manifests;
- frozen AssetRipper `GAME.unity`, normalized world object evidence, GameObject,
  AnimationClip, AudioClip и script metadata;
- frozen `FPSInputController`/`CharacterMotor` и PLAYER FSM evidence;
- текущие runtime modules, registries, scenes, save boundaries и tests.

Hierarchy/object names доказывают существование или presentation evidence, но
не используются как доказательство reachability, поведения, расписания,
порядка, цены, persistence или реализации.

## 4. Supporting inventories

| Inventory | Rows |
|---|---:|
| NPC roster | 75 |
| Vehicle roster | 40 |
| Job/activity roster | 22 |
| Story/event roster | 25 |
| Service/location/SKU roster | 44 |
| Item/consumable categories | 20 |
| Concrete item-definition subroster | 82 |
| Phone caller/event roster | 18 |
| Media/minigame content roster | 40 |
| Authority offense roster | 14 |
| Legacy presentation inventory | 10 |

Grouped parents remain useful for planning, но persistent identities и
конкретные content definitions имеют отдельные opaque FeatureId там, где frozen
evidence позволяет их различить.

## 5. Current project gap

### Working/accepted foundations

- stable identity authoring/validation;
- bounded first-person movement and interaction from M04;
- bounded Satsuma assembly and FWD simulation prototypes;
- canonical donor world and 49-cell streaming baseline;
- project-owned time/weather/wetness/lightning with Enviro presentation;
- `IAudioBackend` and official Wwise foundation;
- accepted 08A UI and gameplay session gate.

### Architecture only or partial

- save has interfaces and isolated DTOs but no native save document, storage,
  slots, aggregate snapshot, migrations or concrete entity provider;
- current player has walk, look and one binary crouch; no run, deep crouch,
  forward lean or complete seated lean conditions;
- Satsuma prototype is not a complete car; tuning, wear, damage, wiring, fluids,
  full presentation, inspection and aggregate persistence are missing;
- collision is partial and unverified independently from accepted materials;
- audio covers the backend and bounded fixtures, not every feature producer;
- UI shells for money/needs/save truthfully lack real providers.

### Entire domains absent

No production runtime modules currently exist for complete Characters, NPC,
Items, Needs, Traffic, Economy, Services, Jobs, Progression or Media domains.
These are recorded as `Nothing`, not placeholders or implementations.

## 6. Player locomotion and Teimo stairs

Frozen 04A1 proves:

- `PlayerHorizontal`/`PlayerVertical` analog movement and CharacterMotor values;
- a `Run` FSM coupled to thirst/fatigue/dirtiness/weight variables;
- two crouch levels with serialized camera heights `1.4 / 0.85 / 0.3` and
  crouch speed `1`;
- a forward on-foot lean/reach action;
- bilateral vehicle camera lean through an open window or door, per direct user
  runtime clarification on 2026-07-21;
- legacy CharacterController values `height 0.5`, `radius 0.12`,
  `slopeLimit 90`, `stepOffset 0.4`, `skinWidth 0.03`;
- authored school/soccer/skijump stairs and climb structures.

Serialized values are not treated as verified runtime tuning. Run speed,
transition timing, posture collider clearance and exact lean offsets still need
executed capture.

Current project values (`height 1.8`, `radius 0.32`, `stepOffset 0.3`,
`skinWidth 0.08`, `slopeLimit 50`) plus the absence of step/ground-snap probes
may contribute to the issue, but the location-specific result — pub passes,
Teimo blocks — first points to threshold/frame/stair collision geometry.

Decision: keep the current CharacterController through 08B. The first 09C batch
must audit exact colliders, build stable landmark traversal fixtures, then test
compatible tuning/step assist. Backend replacement is allowed only if correct
colliders and a bounded solver still fail repeatably at target/low frame rates
and after cell reload. No per-location invisible ramps or name-based hacks.

## 7. Evidence corrections made during audit

- Latanen is recorded as bus driver, not a third passenger;
- frozen roles are kept neutral as `KYLAJANI_DRIVER`/`AMIS2_DRIVER` until public
  character names are evidenced;
- the band role is synthist, not an assumed guitarist;
- Jokke suicidal-hiker presentation is one NPC identity/story state, not a
  duplicate persistent NPC;
- hospital/recovery endpoint remains an optional `Unknown` candidate because
  direct frozen evidence was not found;
- `SlotPivot` is not promoted to a separate slot-machine game; proven Video
  Poker and Lottery are separate rows;
- combine delivery/return is evidenced; harvesting is not claimed yet;
- dormant graves, candidate cosmetics/trophies and cheat/debug call are not
  promoted to required content from object names alone.

## 8. Execution plan

Every required matrix row has an owner in 09A–14B. Large milestones are split
into bounded ordered batches:

- 09A native save/storage/aggregate/evidence;
- 09B item definitions, consumables, containers and persistence;
- 09C locomotion first, then needs/home/sauna/life loop;
- 10A NPC foundation; 10B full roster batches;
- 11A Satsuma/full vehicle roster; 11B traffic/transit/routes;
- 12A economy/services/phone/mail; 12B jobs/activities;
- 13A story/progression; 13B inspection/authority/rally/death;
- 13C media/minigames/remaining mechanics;
- 14A read-only gap audit; 14B only approved residual waves.

14C–15C remain later completion/integration/RC gates and do not replace the
original feature owner.

## 9. Unknowns and risks

- frozen source represents the accepted observed state and may contain mods; it
  is not claimed as a clean stock installation;
- exact donor save schema is absent;
- hierarchy evidence can include dormant/unreachable objects;
- exact dialogue, schedule, price, reward, timing and branching values need
  bounded behavioral capture before implementation/verification;
- one Texture2D was unreadable, 37 class IDs were unsupported and 2007 bounds
  records remain review debt in the frozen export;
- the 08A full-project suite was not rerun after its final bounded remediation;
  focused suites are green but six historical full-suite failures remain known;
- `Prompts/CURRENT_STATE.md` is historically stale and was not rewritten during
  this audit-only milestone;
- three pre-existing untracked Unity reflection-probe artifacts remain outside
  this milestone and were not staged, deleted or attributed to 08B.

## 10. Files created/updated

Authoritative required deliverables:

- `Docs/Phase1/DONOR_VERSION_LOCK.md`;
- `Docs/Phase1/PHASE1_SCOPE_LOCK.md`;
- `Docs/Phase1/LEGACY_FEATURE_PARITY_MATRIX.csv`;
- required NPC/vehicle/job/story/service/item/presentation inventories;
- `Docs/Phase1/DONOR_QUIRK_AND_BUG_POLICY.md`;
- `Docs/Phase1/PHASE1_EXECUTION_PLAN.md`;
- `Docs/Phase1/PHASE2_BACKLOG.md`;
- updated `Docs/Phase1/PHASE1_DEFINITION_OF_DONE.md`;
- this report.

Additional bounded subrosters were added for item definitions, phone callers,
media content and authority offenses to prevent umbrella rows from hiding
required content.

## 11. Checks executed

Executed documentation checks:

- PowerShell `Import-Csv` parse for every CSV;
- exact 19-column parity-matrix schema;
- allowed status/owner/Required values;
- empty and duplicate FeatureId checks;
- required-row owner coverage;
- roster-to-matrix reference resolution;
- transfer-classification vocabulary check;
- status/domain/owner/count summaries;
- Git diff/status and whitespace checks.

Unity/EditMode/PlayMode/build tests were not run: 08B changed documentation only
and deliberately did not touch runtime content. Existing test evidence is cited,
not re-labelled as fresh execution.

## 12. Compatibility impact

No runtime API, serialized field, stable entity ID, scene, prefab, registry,
save DTO, package or accepted 00–08A behavior changed. The new FeatureId values
are planning identities and do not silently rename existing runtime IDs.

## 13. Stop condition and next milestone

Milestone 08B завершён. Пользователь принял scope и разрешил переход к 09A.

Exactly one recommended next milestone: **09A — Full Game Native Save
Foundation**.
