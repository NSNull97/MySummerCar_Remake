# Milestone 10A — NPC Character Runtime Foundation

Status: **Completed / automated validated / user accepted bounded foundation
(2026-08-01).** Milestone 10B has not started.

## Inspected baseline and donor evidence

- preserved the accepted 00–09C architecture, Bootstrap, stable IDs, world
  streaming, GameTime, Interaction, audio boundary, native save and 08A UI;
- reviewed `Prompts/10A_NPC_CHARACTER_RUNTIME_FOUNDATION.md`, Phase 1 scope/
  execution/definition-of-done documents, NPC roster, presentation inventory,
  save coverage and provenance ledger;
- inspected the locked external AssetRipper `GAME.unity` scene read-only and
  selected exactly three bounded role fixtures: Teimo stationary-service, Alpo
  scheduled-roaming and Latanen vehicle-linked;
- recorded the locked scene SHA-256, donor transform/renderer IDs, source GUIDs
  and SHA-256 values for two meshes and three clips in the committed manifest.

## Implemented runtime

- `MSC.Characters.Runtime`: immutable definitions, stable mutable instances,
  flags, relationships, activity state, snapshots, project presentation IDs and
  production replacement keys;
- `MSC.NPC.Runtime`: anchors, routes, schedule blocks, deterministic off-screen
  route progression, pose resolution, navigation backend boundary, dialogue
  conditions, localization/audio IDs and explicit external-domain event hooks;
- `NpcWorldRuntime`: GameTime-driven logical simulation in persistent Bootstrap,
  project-cell streaming reconciliation and transient wrapper creation/removal;
- routes support explicit `Once`, `Loop` and `PingPong` traversal policies. The
  roaming fixture uses a visible 120-game-second ping-pong traversal instead of
  completing once after midnight, and presentation orientation follows the
  active traversal direction;
- streaming unload removes only presentation. Logical state and route progress
  remain present and reload materializes from the same project-owned state;
- an Inspector diagnostic lists active instances, schedule/anchor/route state,
  materialized wrapper count and a bounded transition/restore trace.

The three definitions are `frameworkFixtureOnly`. Their authored schedules and
short route exercise the architecture and are explicitly not donor calibration.
The corresponding 10B NPC rows remain `EvidenceCaptured`, not `Verified`.

## Private temporary presentation

`Phase1CharacterPresentationImporter` is manifest-driven and deterministic. It
copies only the selected mesh/clip payload into ignored
`RuntimeBaseline/Characters`, assigns deterministic generated GUIDs, rebuilds
the minimal bone slices, uses a neutral project HDRP material, converts clips to
legacy presentation clips and removes all animation events.

Generated prefabs contain project-owned
`LegacyCharacterPresentationBinding`, `Animation`, transforms and skinned
renderers. They contain no donor `MonoBehaviour`, PlayMaker FSM, Animator,
AnimatorController, material, texture, audio, runtime assembly, input, schedule,
dialogue or save logic. The build guard checks the local catalog and current
manifest hash and fails closed if the ignored payload is absent or stale.

## Save compatibility

- native save document version increased from 7 to 8;
- required domain `npc.state`, schema 1, restores after `core.time` in the global
  state phase;
- DTO covers stable identity, active schedule/anchor/route, route progress,
  activity, flags and relationships independent of loaded cells/presentation;
- migration `7 -> 8` inserts a validated fresh catalog snapshot without
  modifying the original file;
- semantic preflight requires the configured project roster to match before
  mutation; existing transaction rollback remains authoritative.

No existing stable ID, public API, scene, prefab, UI contract or earlier save
domain was renamed or removed. Old initialization overloads remain available.

## Files created or modified

- runtime: `Assets/Game/Characters/**`, `Assets/Game/NPC/**`;
- integration: `ProductionWorldStreamingInstaller.cs`, Bootstrap scene/asmdef,
  `NpcSaveParticipant.cs`, `NativeSaveSessionController.cs`, save document v8;
- tooling/content: `Milestone10ANpcFoundationBuilder.cs`, three committed
  catalogs, character presentation importer/manifest/build guard;
- tests: focused NPC foundation/presentation tests and save migration test;
- documentation: parity matrix, NPC roster, presentation/save inventories,
  execution plan, native save architecture, system map, donor audit and ledger.

The generated donor payload and build report remain ignored and are not part of
the committed file list.

## Executed validation

- Unity 6000.3.11f1 batch compilation: PASS, no C# errors
  (`Logs/Milestone10A-Compile-2.log`);
- manifest-driven presentation import and catalog/Bootstrap builder: PASS
  (`Logs/Milestone10A-Build-Final.log`), including the manifest-linked report
  and deterministic generated catalog validation;
- focused NPC runtime/presentation EditMode after the manual animation fix:
  PASS `8/8`, including default-noon movement, no per-tick animation restart,
  `AlwaysAnimate` validation and actual bound-bone motion sampling for all three
  generated clips, deterministic off-screen progression, save round-trip,
  dialogue hook and real wrapper unload/reload
  (`Logs/Milestone10A-NpcAnimationFix-Tests.xml`);
- generated character animation PlayMode smoke: PASS `1/1`; Teimo, Alpo and
  Latanen wrappers all change bound skeleton rotations over rendered frames
  (`Logs/Milestone10A-NpcAnimationFix-PlayMode.xml`);
- focused NPC save migration: PASS `1/1`
  (`Logs/Milestone10A-SaveEditMode.xml`);
- complete existing `MSC.Save*` EditMode regression after the animation fix:
  PASS `52/52` (`Logs/Milestone10A-NpcAnimationFix-SaveTests.xml`);
- authoritative CSV parsing passed for all four updated Phase 1 tables, with no
  malformed rows or duplicate feature IDs. The project-wide save-coverage test
  still reports two pre-existing 09C composite domain strings and an incomplete
  `home.state` contract; the 10A `npc.state` rows now satisfy the validator;
- the full EditMode run completed `536/552`, with 16 failures in the already
  dirty Garage, world baseline/cellization/material/remaster/transfer, menu-logo
  and Enviro weather-lab state. The two mixed validators were rerun after the
  10A integration fix: neither reports character-baseline nor NPC save-policy
  errors. This report does not claim the repository-wide suite is green;
- scoped `git diff --check`: PASS.

## Manual feedback correction

The first user visual pass was rejected because all three NPCs appeared frozen.
The reproduced causes were both in 10A: the all-day roaming route used one-shot
progress measured from midnight, so it was already complete at the default noon
start, and presentation reconciliation replayed the same legacy clip on every
GameTime notification, resetting it to frame zero. The route now ping-pongs and
unchanged activity states no longer restart their clip. The next manual pass
confirmed Alpo movement and streaming but exposed Teimo/Latanen as static
mannequins: renderer-based Animation culling could not see the sibling skinned
renderer branch. All sanitized wrappers now use validated `AlwaysAnimate`, and
the focused test samples real bone rotation changes in every clip. The final
manual retest confirmed Teimo operating the register, Alpo walking with the
expected gait, Latanen performing the receipt/register action and streaming
transitions remaining stable. The user explicitly accepted 10A on 2026-08-01.

## Manual acceptance result

The visual/motion and streaming portions of
`Docs/NPC/M10A_NPC_FOUNDATION_MANUAL_TEST_RU.md` passed. The user did not report
a separate manual mid-route save/load observation; automated NPC and save
coverage remains green. Acceptance applies only to the bounded 10A foundation
and three fixtures, not to full donor NPC roster parity.

## Limitations and risks

- only three bounded fixtures exist; full roster, multiplicity, exact donor
  schedules/routes/dialogue/relationships/events and audio remain 10B;
- temporary meshes/clips use a neutral material and are not production art;
- vehicle-linked foundation stores an explicit project vehicle binding ID but
  the production bus/route owner does not yet exist;
- the direct navigation backend applies the authoritative simulated pose; the
  8 m / 120-game-second ping-pong route is only a visibly testable fixture.
  Final obstacle-aware/local navigation belongs to roster implementation and
  tuning;
- full donor comparison and complete roster acceptance remain 10B work.

## Next milestone

The one next milestone is **10B — Full NPC Roster, Dialogue and Schedule
Parity**.
