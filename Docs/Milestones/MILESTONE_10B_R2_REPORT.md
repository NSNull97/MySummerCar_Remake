# Milestone 10B-R2 — relationship, service and job-contact NPC foundation

Status: **In progress / automated catalog, presentation, streaming and save
validation passed / manual in-world placement and pose review pending / exact
story, service, job, minigame, dialogue and voice gates pending**.

This bounded package adds `P1.NPC.003`–`.006`, `.009`–`.017` and `.101` to the
accepted R1/10A foundation. It does not claim that their dependent jobs or story
chains are complete.

## Implemented

- added 14 project-owned character definitions and stable instances;
- transferred 14 exact donor-scene anchors through the audited M04A1
  source-to-project translation and existing streaming cell ownership;
- built 13 removable `TemporaryDirectImport` physical wrappers from hash-locked
  donor meshes, textures, material identities, accessories and compatible clips;
- preserved one stable identity for Jokke across the buyer/hiker/suicidal
  evidence instead of creating duplicate NPCs;
- represented Jokke's wife as an explicit `StateOnly` character because the
  locked scene contains a state point, related Lamore object and wife audio, but
  no physical body. State-only characters are saved but cannot receive a
  materializing schedule or require a presentation prefab;
- kept Uncle Kesseli hidden until the owning story/event system can activate an
  evidenced variant. Jokke's wife is always non-materialized;
- gave the remaining eleven physical actors an all-day **review availability
  baseline** so placement and presentation can be inspected. These blocks are
  not claimed as donor-exact schedules and must be replaced by the owning
  job/service/story milestones;
- added identity-only dialogue hooks required by the generic interaction
  boundary. They are not donor quotations and their `.pending` audio IDs do not
  claim imported voice coverage;
- raised native save document version from 11 to 12. Migration 11→12 preserves
  every compatible accepted NPC snapshot and adds the missing R2 identities;
- rebuilt and rebound Bootstrap catalogs without replacing the accepted 00–08A
  architecture, stable IDs or R1 implementations.
- extended the existing development-console `player.teleport` list with R2
  review destinations; these use project anchor IDs and do not become gameplay
  or persistent identity.

## Generated presentation closure

`Phase1CharacterPresentationBuildReport.json` records:

- schema `5`;
- manifest `phase1-character-presentation-10b-r2-v1`;
- frozen scene SHA-256
  `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`;
- 20 wrapper fixtures including the accepted Teimo bicycle override;
- 33 generated materials, 32 textures and 22 accessory instances;
- donor scripts, PlayMaker/FSM controllers and donor shaders excluded.

## Validation executed

- Unity 6000.3.11f1 R2 presentation/catalog/Bootstrap batch rebuild: **PASS**;
- `MSC.NPC.Tests.EditMode.NpcFoundationTests`: **16/16 PASS**;
- `MSC.Save.Integration.Tests.EditMode.NpcSaveMigrationTests`: **3/3 PASS**;
- complete `MSC.Save.Integration.Tests.EditMode`: **26/26 PASS**;
- `MSC.NPC.Tests.PlayMode.NpcCharacterAnimationPlayModeTests`: **3/3 PASS**;
- runtime test initializes 20 saved character identities and materializes 16
  currently available physical actors, then unloads/reloads their streaming
  presentations without losing simulation state;
- generated wrapper validation checks linked bone animation, three textured body
  slots and the audited hats/glasses/chair accessories;
- save migration test proves the six accepted pre-R2 snapshots are retained and
  the resulting roster contains 20 identities.

## Not complete and not claimed

- donor-exact schedules and event gates for all R2 actors;
- Uncle Kesseli's Drinking/Home/Walking variant flow and vehicle-key logic;
- Grandmother's church variant and visit/delivery systems;
- Jokke's kilju trade, rides, move and suicide chain;
- Suski's relationship/passenger/crash chain;
- septic and firewood phone/job/payment flows;
- inspection and wastewater service authority;
- Ventti gameplay and house-ownership transition;
- donor voice/subtitle selection for R2;
- manual in-world pose, height, accessory and location comparison.

All 14 rows remain `PartiallyImplemented`, not `Verified`.

Manual correction, 2026-08-02: Suski must not use the review-availability
schedule. Her initial donor state is a passenger with Jani; the store-hiker
presentation becomes eligible only after the player rescues her from the crash.
The standalone Suski instance is therefore hidden until the story/traffic owner
can perform that transition.

## Exactly one next milestone

Continue 10B with the manual R2 placement/presentation acceptance pass, then
replace review availability blocks with the first evidence-backed story/service
gates before starting another roster package.
