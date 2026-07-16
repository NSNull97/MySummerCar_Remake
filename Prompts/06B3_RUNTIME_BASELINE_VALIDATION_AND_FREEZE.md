/plan

# MILESTONE 06B3 — RUNTIME BASELINE VALIDATION, DEBT CATALOGUE, AND FREEZE

Read `AGENTS.md` completely before doing anything.

Read:

- `Prompts/CURRENT_STATE.md`;
- `Prompts/PROJECT_DESIGN_GUARDRAILS.md`;
- `Docs/Milestones/MILESTONE_06B1_REPORT.md`;
- `Docs/Milestones/MILESTONE_06B2_REPORT.md`;
- all `Docs/WorldBaseline/*` outputs;
- current world, streaming, player, vehicle, collision, bootstrap and build
  validation reports;
- current Git status and diff.

## Entry gate

Stop if:

- 06B1 or 06B2 is not closed;
- the sanitized donor baseline is not the active world profile;
- the project does not compile;
- forbidden donor runtime logic remains;
- the two inaccurate custom visual roots are still active;
- a broad unrelated diff is present.

## Objective

Validate the full donor-map runtime baseline as the stable world foundation for
feature-parity development, catalogue temporary legacy defects without trying to
remaster them now, freeze a reproducible baseline revision, and hand the project
off to Enviro 3 weather work.

This is a technical world-baseline gate.

It is not final-art approval.

## Validation philosophy

The baseline is successful when:

- it is the actual recognizable original map;
- it loads and streams safely;
- player and vehicle can traverse required routes;
- gameplay can be built without depending on donor hierarchy;
- the import is reproducible;
- known old visual hacks are documented rather than mistaken for final art.

Do not reject the baseline merely because it contains original low-detail meshes,
textures, sprite forests, flat objects, or terrain voids. Those are expected
remaster debt.

Do reject or block it for crashes, missing critical world sections, wrong scale,
wrong coordinates, broken streaming, forbidden donor logic, or unplayable
collision.

## Full-map automated validation

Create or run validation for every registered cell and global scene:

- scene/manifest exists;
- source hash/revision recorded;
- object ownership valid;
- no duplicate active object key;
- no forbidden donor component;
- no missing required mesh/material/collider;
- no gameplay dependency on donor hierarchy name/path;
- no duplicate stable gameplay ID;
- no invalid scene reference;
- no active prototype-rejected visual root;
- no production override accidentally hiding the baseline without approval;
- cell can load and unload;
- global scene remains stable;
- baseline revision is reproducible.

Export a machine-readable validation result.

## Traversal validation

Validate representative routes covering at least:

- player home/garage area;
- Teimo/store area;
- Fleetari area;
- inspection/town area;
- major road loops;
- bridges;
- railway crossings where applicable;
- lake/shore approach where applicable;
- both regions previously represented by inaccurate custom cells;
- several consecutive streaming boundaries at vehicle speed.

For every route record:

- start/end;
- cells loaded;
- collision failures;
- visible duplicate/missing sections;
- load spikes;
- player/vehicle recovery use;
- known donor visual defects encountered.

Do not claim a full manual route was driven if it was not.

## Collision and out-of-bounds safety

Technical safety must exist even when visual donor defects remain.

Validate or implement the smallest project-owned safeguards for:

- falling below world bounds;
- imported collision gaps caused by conversion;
- vehicle recovery from invalid world state;
- critical item/NPC recovery hooks where current systems exist;
- safe spawn positions;
- no infinite fall or permanent softlock.

Do not visually fill terrain holes or replace tree walls here.

Record every safety-only workaround as temporary and identify the future
production replacement task.

## Legacy visual debt catalogue

Catalogue, but do not remaster:

- internal terrain voids;
- sprite/tree-wall forests;
- map-edge backdrops;
- under-map water/swamp hacks;
- flat strawberry beds, ash patches, fields, clutter, or other plane objects;
- low-detail buildings/props;
- old materials/textures;
- weak/oversized colliders;
- missing LODs;
- inefficient vegetation;
- visible seams inherited from the donor;
- objects that should later become SpeedTree, decals, shallow meshes, or rebuilt
  production geometry.

Create stable debt IDs and classify:

- `VisualOnly`;
- `TraversalRisk`;
- `GameplayBlocker`;
- `PerformanceRisk`;
- `ReplacementPlanned`;
- `Unknown`.

Only `GameplayBlocker` and direct technical `TraversalRisk` items block 06B3.

## Baseline revision freeze

Create a project-owned baseline revision record containing:

- canonical donor source revision/hash set;
- importer/sanitizer version;
- cellization version;
- world bounds/scale/origin;
- global/cell manifest hashes;
- active world profile ID;
- known exceptions;
- debt catalogue revision;
- build/commit identifier;
- validation result.

Suggested ID format:

```text
DonorWorldBaseline-v001
```

Future gameplay and weather milestones may depend on this revision.

Any later baseline regeneration must produce a diff report.

## Private build/content policy validation

Confirm:

- raw donor extraction is outside Git;
- runtime baseline assets are clearly `TemporaryDirectImport`;
- private local feature-parity builds can include the generated baseline;
- reference-only content is still excluded;
- distributable/public build profiles exclude the donor baseline unless explicit
  rights allow it;
- no donor executable/runtime assembly is required to launch the remake;
- no donor installation is modified.

Do not describe the baseline as production-ready art.

## Weather handoff preparation

Prepare, but do not implement Enviro:

- one active world bootstrap/profile;
- stable global environment service lifetime location;
- explicit neutral clear/dry baseline state;
- list of temporary HDRP sky/fog/light owners to audit in 07A;
- additive scene lifecycle hooks for environment presentation;
- list of legacy material families and expected wetness coverage limitations;
- representative WeatherLab-independent production route for 07C performance
  testing.

Do not add Enviro components or weather logic in this milestone.

## Tests and manual validation

Run:

- Unity compilation;
- all world/cell baseline validation tests;
- bootstrap/new session;
- repeated cell load/unload;
- representative player traversal;
- representative vehicle traversal;
- out-of-bounds recovery smoke tests;
- content/provenance/Git audit;
- private development build smoke test when practical;
- baseline revision reproducibility check.

Manual user review should answer only:

- Is this visibly the actual original MSC map baseline?
- Are the two formerly inaccurate custom regions now showing the donor map?
- Can the world be traversed without critical streaming/collision failures?

This is not a request to approve final remastered art.

## Output

Create:

- `Docs/WorldBaseline/FULL_MAP_VALIDATION_REPORT.md`;
- `Docs/WorldBaseline/TRAVERSAL_VALIDATION.csv`;
- `Docs/WorldBaseline/COLLISION_AND_OOB_SAFETY.md`;
- `Docs/WorldBaseline/LEGACY_VISUAL_DEBT.csv`;
- `Docs/WorldBaseline/BASELINE_REVISION.json`;
- `Docs/WorldBaseline/BASELINE_REGENERATION_POLICY.md`;
- `Docs/WorldBaseline/PRIVATE_BUILD_CONTENT_AUDIT.md`;
- `Docs/WorldBaseline/WEATHER_HANDOFF.md`;
- `Docs/Milestones/MILESTONE_06B3_REPORT.md`.

## Definition of done

1. Every registered baseline cell/global scene passes structural validation or
   has an explicit blocker.
2. The actual original donor map is the active runtime baseline.
3. The two inaccurate custom visual regions are inactive and donor visuals are
   shown instead.
4. Representative player and vehicle routes work across streaming boundaries.
5. No forbidden donor runtime code is present.
6. No gameplay system depends on donor hierarchy identity.
7. Critical collision/out-of-bounds failures are safe.
8. Legacy visual hacks are catalogued as future remaster debt, not silently
   treated as complete.
9. A reproducible baseline revision is frozen.
10. The project has a clear go/no-go for Enviro 3 Milestone 07A.

## Final response

Report:

1. Baseline revision.
2. Full-map structural results.
3. Traversal results.
4. Collision/OOB safety.
5. Legacy visual debt summary.
6. Private build/content policy result.
7. Tests and manual checks actually run.
8. Files changed.
9. Remaining blockers/risks.
10. Go/no-go for `07A_ENVIRO3_PREFLIGHT_AND_WEATHERLAB.md`.

Stop after 06B3. Do not start Enviro automatically.
