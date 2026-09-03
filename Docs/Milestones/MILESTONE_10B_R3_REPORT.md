# Milestone 10B-R3 — first story-traffic slice

## Inspected

- repository and accepted 10A–10B-R2 NPC/runtime/save foundations;
- locked `GAME.unity` KYLAJANI and AMIS2 vehicle, driver, wheel and passenger
  transforms;
- BetterMSC AssetRipper export for Suski presentation and available logic
  evidence;
- existing native-save migration pipeline and streaming presentation tests.

No BetterMSC managed assembly or source code was present. Its model, materials,
avatar and animation assets were available; behavior is therefore explicitly
reimplemented behind project-owned state rather than claimed as code-ported.

## Implemented

- stable Jani `P1.NPC.049` and Petteri `P1.NPC.050` character definitions;
- separate smooth looping, terrain-conforming review routes using the existing
  project-owned NPC simulation and streaming backend;
- two replaceable car/driver wrappers with four wheels rotating from traveled
  distance;
- BetterMSC Suski model/materials sampled into a Unity-6-safe baked car-sit
  presentation at the audited KYLAJANI passenger anchor;
- one Suski identity across contexts: passenger by default, standalone at the
  store only after `flag.suski.rescued-from-jani-crash`;
- save document version 13 and migration `12 -> 13`, preserving all compatible
  R2 snapshots and adding fresh Jani/Petteri state;
- roster/parity/provenance records, including rejection of `P1.NPC.103` as a
  duplicate persistent identity.

## Validation executed

- Unity 6.0.3.11f1 batch build:
  `Milestone10ANpcFoundationBuilder.BuildFromBatch` — passed;
- generated presentation report: 2 cars, 3 skinned crew renderers, 61 static
  vehicle renderers, 45 meshes (including three Editor-only passenger
  calibration references), 28 materials and 26 textures;
- `MSC.NPC.Tests.EditMode.NpcFoundationTests` — **16/16 passed**;
- `MSC.NPC.Tests.PlayMode` — **4/4 passed**;
- `MSC.Save.Integration.Tests.EditMode.NpcSaveMigrationTests` — **4/4 passed**;
- complete `MSC.Save.Integration.Tests.EditMode` — **27/27 passed**;
- complete `MSC.Save.Tests.EditMode` — **25/25 passed**.

The NPC test proves initial passenger visibility, absence of a duplicate
standalone Suski, post-rescue context switching, save/restore of the rescue flag,
streaming reconciliation and Jani/Petteri presentation validity.

## Manual Unity review still required

1. Open `Assets/Game/Bootstrap/Bootstrap.unity` and enter Play Mode.
2. Confirm both cars appear on the road, face their travel direction and their
   four wheels rotate without renderer detachment.
3. Inspect Jani at close range: BetterMSC Suski should be seated in the car and
   there must be no standalone Suski near the store before a future story owner
   sets the rescue flag.
4. Compare vehicle scale, driver seating, Suski seating and road clearance.

## Limitations and compatibility

- routes are bounded review routes, not donor-exact traffic schedules;
- crash detection, damage, rivalry, rescue trigger conditions, recovery,
  dialogue and audio are not implemented or claimed;
- the public rescue-state method is the intended later `P1.EVENT.007/010`
  integration boundary;
- established 00–08A APIs, stable IDs, UI, Enviro, streaming and save domains
  were preserved; only the additive document `12 -> 13` migration was required;
- all donor/BetterMSC presentation remains private `TemporaryDirectImport` and
  retains Phase 2 replacement keys.

## Exactly one next milestone

Continue 10B-R3 with the remaining ambient residents and transport occupants,
while leaving exact Jani/Petteri crash consequences to their owning story and
vehicle milestones.

## 2026-08-03 visual-correction pass

User comparison exposed missing seated context and vehicle-grounding errors.
The character wrapper manifest is now schema 8 / v4 and preserves the reviewed donor
lean plus context props for Grandmother and Jokke. Grandmother has her hat,
coffee cup and plate, chair, table and a product tray hidden until
`flag.grandmother.products-ordered`; Jokke has his chair, table and the locked
`terrace_shade` umbrella. Each of the five septic customers has a chair, held
beer and the donor `fat_handsright_drink` clip on its original `collar_right`
animation branch, while Livaloinen has his held vodka bottle. A project-owned
`LegacyLoopingAnimationLayer` now starts those five drinking clips at staggered
phases and restarts them after streaming disable/enable cycles; it remains
presentation-only and does not claim drink/job authority.

The job-location overlay is manifest v6 and contains 306 renderers, 102
colliders and nine cells. It restores `JOBS/Mummola/LOD/Shed/` and gives both
the reviewed Jokke house facade and adjacent `JOBS/HouseDrunk/Shed` walls,
windows and wooden door/trim renderers local double-sided HDRP material clones
without mutating shared materials.

The story-traffic importer is now schema 4 / manifest v7. It raises the Jani
and Petteri fixtures by measured wheel-renderer contact (0.36630034 m and
0.328477 m respectively). Suski remains the hash-locked BetterMSC mesh,
materials and `Suski_car_sit` pose. The four original passenger-only static
renderers (`RedHot`, `tail`, cigarette shaft and filter) are excluded before
slice construction, and the passenger anchor is cleared before the single
BetterMSC root is attached. Direct sampling of the old hand-IK channels crashes
Unity 6 native Mecanim; the importer therefore reads the locked Avatar's
serialized 43-node human T-pose, restores the Biped root plus all 42 mapped
body/finger transforms, builds a temporary compatibility Avatar, transfers the
ordinary humanoid muscle/root curves, deliberately ignores the incompatible IK
goals, and bakes an event-free transform pose. V7 preserves BetterMSC's actual
humanoid mapping: `HeadPivot` is the human head while `Bip01 Head` remains its
zero-offset fixed mesh child. This prevents the former double head transform and
stretched neck. Seating calibration now aligns the replacement pelvis and
projected foot direction to exact locked GAME transform IDs `69311`, `62678`
and `53657`, replacing the invalid ponytail-bounds height heuristic and adding
explicit seat-height/direction constraints. The importer also rolls back the
previous StoryTraffic output and catalog asset if generation fails, so an
interrupted rebuild cannot leave Jani/Petteri entries with null wrapper prefabs.
The temporary Animator/Avatar and reference renderers do not enter runtime.

Validation: the schema-4 / manifest-v7 story-traffic Unity batch builder passes
and regenerates both wrapper prefabs plus non-null catalog references. The
complete NPC EditMode suite passes 16/16 and the NPC PlayMode suite passes 4/4
against v7 output. Unity's generated Roslyn response files also compile both
`MSC.LegacyImport.Editor` and `MSC.NPC.Tests.EditMode` successfully, and
`git diff --check` passes. The exercised checks require the source-compatible
`HeadPivot`/zero-offset `Bip01 Head` chain, donor pelvis height and forward
travel along the car rather than across the cabin. The wider world fixture
previously passed 8/12; its
four failures are unrelated shared-world freeze/material/source-gate drift from
concurrent tree/world changes and were intentionally not rewritten here.

The user accepted Suski's v7 Play Mode presentation on 2026-08-04: seated
alignment, neck, arm pose, height and facing direction are accepted in Jani's
car. Manual comparison is still required for unrelated prop contact and vehicle
clearance. Grandmother's cup is currently a visible presentation prop, not yet
the persistent drinkable item instance; the product tray has the correct
project-owned visibility flag but still needs the grandmother-order story owner
to set it.
