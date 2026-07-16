/plan

# HISTORICAL / FUTURE REMASTER BATCH — DO NOT RUN AS THE NEXT MILESTONE

Read `AGENTS.md` completely before doing anything.

Read `Prompts/CURRENT_STATE.md`, `Prompts/PROJECT_DESIGN_GUARDRAILS.md`, and
`Prompts/WORLD_CELL_FIDELITY_CAPTURE_GUIDE_RU.md`.

## AUTHORITATIVE DONOR-FIDELITY DIRECTIVE

This project is reconstructing the original MSC world on a new foundation. It is
not creating an attractive location merely inspired by it.

For layout and identity, real donor captures/data outrank concept art. New
meshes, materials and lighting must preserve the original location's road
approach, terrain profile, building footprint, silhouette, proportions,
landmarks, clutter character, vegetation boundaries, open space and gameplay
clearances.

Do not use weather, fog, depth of field, dramatic lighting or dense vegetation
to conceal a structural mismatch. Do not make a location cleaner, wealthier,
more symmetrical or more generically Scandinavian than the donor.

Every production cell requires matched neutral side-by-side donor captures and
explicit human approval before `Approved` or `Complete`. Automated parity is
necessary but not sufficient.

The inaccurate custom cell visuals are inactive prototypes. The active donor
runtime baseline provides exact-map visuals during feature parity. Future
production overrides must pass donor comparison and human approval; do not
reactivate or silently mark the old custom visuals complete.

Read all relevant project documentation and previous milestone reports,
including these files when present:

- `README.md`
- `Docs/PROJECT.md`
- `Docs/ARCHITECTURE.md`
- `Docs/ART_GUIDE.md`
- `Docs/PORTING_GUIDE.md`
- `Docs/REVERSE_ENGINEERING.md`
- `Docs/WORLD_WEATHER.md`
- `Docs/PERFORMANCE_BUDGET.md`
- `Docs/TESTING_AND_VALIDATION.md`
- `Docs/ROADMAP.md`
- `Docs/WorldTransfer/DONOR_WORLD_DISCOVERY.md`
- `Docs/WorldTransfer/COORDINATE_AND_SCALE_AUDIT.md`
- `Docs/WorldTransfer/WORLD_DATA_FORMAT.md`
- `Docs/WorldTransfer/WORLD_COMPLETENESS_REPORT.md`
- `Docs/WorldTransfer/WORLD_FIDELITY_REPORT.md`
- `Docs/WorldTransfer/MAJOR_LANDMARKS.md`
- `Docs/WorldTransfer/ZONE_STATUS.csv`
- `Docs/WorldTransfer/WORLD_PORTING_LEDGER.csv`
- `Docs/Milestones/MILESTONE_04A_REPORT.md`
- `Docs/Milestones/MILESTONE_04B_REPORT.md`
- `Docs/Milestones/MILESTONE_05_REPORT.md`

If milestone numbering differs in the current repository, locate the actual
world-transfer and vehicle-assembly reports by content rather than assuming
their names.

This task is a production-art and world-reconstruction milestone.

It must not replace or invalidate the donor-reference world database.

---


## STAGED EXECUTION REQUIREMENT

This document defines the complete world-remaster track, but one Codex run must
remain bounded.

For the first execution:

1. Build or validate the replacement architecture and dashboards.
2. Complete one representative pilot production zone.
3. Validate the pilot against donor/reference fixtures.
4. Create the complete replacement registry, zone status, and manual-art backlog.
5. Report exact coverage honestly.
6. Stop.

Continue remaining zones with:

`Prompts/05A_CONTINUE_NEXT_WORLD_ZONE.md`

Do not claim the entire map is production-ready after completing only the pilot.
Do not attempt months of art work as one unreviewed autonomous operation.


# PROJECT CONTEXT

This is a private donor-assisted recreation of My Summer Car in Unity 6 HDRP.

The original licensed game installation is used as a read-only spatial,
mechanical, and visual reference.

The donor game is not the final visual target.

The project must preserve the recognizable identity of the original world:

- map layout;
- roads;
- landmark locations;
- building footprints;
- interior dimensions;
- garage and workshop proportions;
- shoreline;
- terrain silhouette;
- major vegetation masses;
- gameplay travel distances;
- pivots;
- mounting positions;
- door and gate clearances;
- vehicle-accessible routes;
- interaction anchors;
- world-scale relationships.

At the same time, the final production world must look and behave like a
high-quality modern 2026 game.

The final world should feel like:

- rural Finland in summer;
- practical rather than glossy;
- lived-in rather than theme-park clean;
- mechanically believable;
- naturally overgrown;
- visually rich without becoming visually noisy;
- atmospheric without destroying gameplay readability;
- modern while remaining unmistakably connected to My Summer Car.

---

# PATHS

Use the currently opened repository as `PROJECT_ROOT`.

Read machine-specific paths from:

`Config/DonorPaths.local.json`

Expected original game location:

`D:\SteamLibrary\steamapps\common\My Summer Car`

Expected project location may currently be:

`E:\GAYmDev_Studio\MySummerCar_Remake`

or:

`E:\GAYmDev_Studio\MySummerCar_Remake_Game`

Do not move or rename the current repository.

Do not hard-code either project path in committed C# source.

All donor extraction and decompiled reference data must remain external to the
Unity project or inside clearly ignored generated/reference directories.

---

# MILESTONE POSITION

This milestone may be executed after:

- the Unity/HDRP foundation exists;
- the donor-reference pipeline exists;
- the complete or substantially complete world geometry transfer exists;
- stable world IDs exist;
- the world is partitioned into cells or equivalent streaming units;
- player movement exists or a development fly camera exists;
- vehicle assembly code, if already implemented, has stable mounting and
  clearance fixtures.

Recommended sequence:

1. Bootstrap and donor audit.
2. Unity foundation.
3. Donor-reference pipeline.
4. Complete world geometry transfer.
5. Player interaction.
6. Vehicle assembly foundation.
7. `05A_COMPLETE_WORLD_REMASTER`.
8. Vehicle simulation, weather, audio, and gameplay expansion.

This milestone may also run as a parallel art track after world transfer, but
it must not modify gameplay-system APIs without coordination.

---

# EXECUTION MODE

Set:

`WORLD_REMASTER_MODE = ProductionReplacementWithReferenceParity`

The donor-reference world remains available as:

- comparison geometry;
- dimensional reference;
- transform source;
- pivot source;
- collision reference;
- landmark reference;
- placement reference;
- regression fixture.

The production world becomes a separate layer.

Use these world layers:

## Layer A — Donor Reference

- original or extracted geometry;
- neutral debug materials;
- editor-only or private-development-only;
- never considered production-ready;
- removable without breaking project-owned production content.

## Layer B — Production Replacement

- newly authored geometry;
- newly authored PBR materials;
- new UVs;
- new LODs;
- new collision geometry;
- new vegetation assets;
- new roads;
- new terrain presentation;
- new water presentation;
- production lighting compatibility;
- stable binding to the durable world-layout database.

## Layer C — Comparison and Validation

- side-by-side mode;
- overlay mode;
- bounds comparison;
- pivot comparison;
- transform comparison;
- road-deviation view;
- landmark comparison;
- collision comparison;
- production-status visualization.

Production prefabs must not depend on donor meshes or donor textures at runtime.

Deleting the donor-reference asset directory must not break production prefabs,
world placement, stable IDs, or saved world state.

---

# PRIMARY OBJECTIVE

Rebuild the complete transferred world as modern production-quality Unity 6
HDRP content while preserving gameplay-critical spatial fidelity.

This includes:

- terrain appearance and terrain geometry corrections;
- roads;
- shoulders;
- gravel;
- ditches;
- driveways;
- bridges;
- culverts;
- shorelines;
- water bodies;
- building exteriors;
- building interiors;
- garages;
- stores;
- repair workshops;
- houses;
- barns;
- sheds;
- fences;
- gates;
- road signs;
- utility poles;
- wires;
- fields;
- yards;
- static props;
- rocks;
- forests;
- bushes;
- grass;
- weeds;
- debris;
- decals;
- dirt;
- rust;
- wetness support;
- snow-free summer seasonal presentation;
- new collision proxies;
- LODs;
- HLODs or cell proxies when beneficial;
- streaming compatibility;
- lighting and weather compatibility;
- modern material response;
- performance budgets;
- validation against the donor layout.

The final result must preserve identity rather than old topology.

When there is a conflict:

- preserve gameplay-critical dimensions;
- preserve spatial layout;
- preserve pivots and interaction anchors;
- preserve recognizable silhouette;
- rebuild topology;
- rebuild materials;
- rebuild textures;
- rebuild collision;
- rebuild LODs.

---

# SUCCESS PHILOSOPHY

The goal is not:

“Import old meshes and place an HDRP material on them.”

The goal is:

“Use the old world as an exact survey, then construct a modern world on top of
the same spatial truth.”

The donor world is treated like:

- a CAD survey;
- an architectural scan;
- a historical reference;
- a gameplay-coordinate database;
- a parity test fixture.

The production world is treated like a new game.

---

# HARD CONSTRAINTS

1. Never modify the original game installation.
2. Never bypass DRM, ownership checks, Steam integration, encryption,
   anti-tamper, or access controls.
3. Do not use original textures as final production textures.
4. Do not treat AI-upscaled donor textures as final production textures.
5. Do not ship donor-reference meshes as the default production world.
6. Do not use automatic subdivision as the primary remastering method.
7. Do not preserve broken donor topology merely for historical fidelity.
8. Do not change road routes, landmark locations, or building footprints
   without an explicit design decision record.
9. Do not change gameplay-critical dimensions silently.
10. Do not move doors, gates, stairs, floors, sockets, or interaction anchors
    without validation.
11. Do not rebuild the world as one monolithic scene.
12. Do not manually place thousands of production assets without a reproducible
    placement system.
13. Do not create one GameObject per grass blade.
14. Do not create one material instance per repeated object.
15. Do not use visual high-poly meshes as default collision meshes.
16. Do not leave production scenes dependent on editor-only assemblies.
17. Do not install third-party Unity packages, Blender add-ons, or external
    binaries without reporting the reason and obtaining approval when needed.
18. Do not silently download asset packs.
19. Do not use unlicensed third-party art.
20. Do not claim a production asset is finished when it is only a proxy.
21. Do not claim the world is fully remastered when entire zones still use donor
    geometry.
22. Do not break stable world IDs.
23. Do not overwrite the world-layout database with scene-object state.
24. Do not duplicate world-position truth across unrelated systems.
25. Do not change save identity because a visual prefab was replaced.
26. Do not add multiplayer in this milestone.
27. Do not implement NPC behavior, traffic logic, quests, or survival mechanics.
28. Do not implement final Wwise content in this milestone.
29. Do not bake final lighting before major geometry stabilizes.
30. Do not enable expensive features globally without measuring them.
31. Do not use ray tracing as a baseline requirement.
32. Do not hide missing art behind excessive darkness, fog, bloom, or depth of
    field.
33. Do not over-polish one screenshot while leaving the navigable world broken.
34. Do not sacrifice player or vehicle readability for cinematic post-processing.
35. Do not continue mass replacement when a systematic scale, pivot, or
    alignment defect has been found.
36. Every manual production-art task that cannot be completed programmatically
    must be recorded in a structured art backlog.

---

# PRE-FLIGHT AUDIT

Before modifying content:

1. Confirm the active Unity version.
2. Confirm HDRP package and render-pipeline assets.
3. Confirm the project compiles.
4. Confirm the world-reference database exists.
5. Confirm stable IDs exist.
6. Confirm world cells or scene partitions exist.
7. Confirm donor/reference geometry can be toggled.
8. Confirm major landmarks have validation fixtures.
9. Confirm roads, terrain, buildings, water, and colliders have transfer records.
10. Confirm the current project folder structure.
11. Inspect existing art assets and do not duplicate good work.
12. Inspect current materials and shaders.
13. Inspect current terrain implementation.
14. Inspect current road implementation.
15. Inspect current vegetation implementation.
16. Inspect current streaming implementation.
17. Inspect current performance reports.
18. Inspect vehicle dimensions and garage-clearance fixtures when available.
19. Identify currently broken references.
20. Produce a concrete execution plan.
21. List assumptions and blockers.
22. Select a pilot production zone.
23. Do not begin full-world replacement before validating the pilot zone.

Create:

`Docs/WorldRemaster/PRE_FLIGHT_AUDIT.md`

---

# REQUIRED OUTPUT ARCHITECTURE

Create or align with this structure:

`Assets/Game/World/Production`
`Assets/Game/World/Production/Terrain`
`Assets/Game/World/Production/Roads`
`Assets/Game/World/Production/Buildings`
`Assets/Game/World/Production/Interiors`
`Assets/Game/World/Production/Props`
`Assets/Game/World/Production/Vegetation`
`Assets/Game/World/Production/Water`
`Assets/Game/World/Production/Infrastructure`
`Assets/Game/World/Production/Decals`
`Assets/Game/World/Production/Materials`
`Assets/Game/World/Production/Textures`
`Assets/Game/World/Production/Collision`
`Assets/Game/World/Production/LODs`
`Assets/Game/World/Production/HLOD`
`Assets/Game/World/Production/Prefabs`

`Assets/Game/World/Authoring`
`Assets/Game/World/Authoring/Definitions`
`Assets/Game/World/Authoring/ReplacementProfiles`
`Assets/Game/World/Authoring/MaterialProfiles`
`Assets/Game/World/Authoring/VegetationProfiles`
`Assets/Game/World/Authoring/RoadProfiles`
`Assets/Game/World/Authoring/BuildingProfiles`
`Assets/Game/World/Authoring/ZoneProfiles`

`Assets/Game/World/Debug`
`Assets/Game/World/Editor`
`Assets/Game/World/Generated`
`Assets/Game/World/Generated/ProductionCells`
`Assets/Game/World/Generated/ProductionProxies`

`Assets/Game/Tests/EditMode/WorldRemaster`
`Assets/Game/Tests/PlayMode/WorldRemaster`

Reference-only donor content must remain in its existing clearly marked,
ignored, or development-only location.

Do not move donor content into production folders.

---

# PRODUCTION ASSET REGISTRY

Create a durable replacement registry.

Recommended types:

- `WorldProductionAssetRegistry`
- `WorldProductionAssetRecord`
- `WorldReplacementBinding`
- `WorldReplacementProfile`
- `WorldReplacementStatus`
- `WorldProductionPrefabReference`
- `WorldProductionMaterialReference`
- `WorldProductionCollisionReference`
- `WorldProductionLodReference`
- `WorldProductionValidationRecord`
- `WorldProductionZoneRecord`
- `WorldArtTaskRecord`

Every replacement record must include:

- stable world ID or prototype ID;
- donor source reference;
- semantic category;
- production prefab;
- production materials;
- collision reference;
- LOD group;
- pivot mode;
- scale mode;
- expected bounds;
- measured bounds;
- allowed deviation;
- transform offset;
- replacement status;
- authoring status;
- validation status;
- production zone;
- performance tier;
- wetness compatibility;
- weather compatibility;
- notes;
- source-control status;
- manual-art dependency;
- last validation timestamp;
- asset version.

Recommended replacement statuses:

- `Unassigned`
- `ReferenceOnly`
- `Blockout`
- `FirstPass`
- `ProductionCandidate`
- `NeedsReview`
- `Approved`
- `Verified`
- `Blocked`
- `Rejected`
- `Deprecated`

Create an editor-facing replacement dashboard.

Create machine-readable files:

`Docs/WorldRemaster/WORLD_REPLACEMENT_LEDGER.csv`
`Docs/WorldRemaster/WORLD_ART_BACKLOG.csv`
`Docs/WorldRemaster/ZONE_REMASTER_STATUS.csv`

Do not rely on a handwritten checklist as the only state database.

---

# ART DIRECTION

Create:

`Docs/WorldRemaster/WORLD_ART_DIRECTION.md`

The visual direction must be explicit.

## Environment identity

- Finnish rural summer;
- warm low-angle evening light;
- cool overcast and rain compatibility;
- old practical buildings;
- mixed forest;
- lakes, shorelines, fields, gravel roads, ditches;
- visible age, maintenance, repairs, rust, moss, dust, mud, oil, and weathering;
- modest rural infrastructure;
- no generic American countryside;
- no tropical vegetation;
- no alpine fantasy mountains;
- no luxury showroom cleanliness;
- no cyberpunk neon;
- no exaggerated cinematic orange-teal grading.

## Realism level

Use grounded stylized realism:

- believable materials;
- believable scale;
- high-quality silhouettes;
- selective microdetail;
- readable interaction surfaces;
- practical asset density;
- restrained wear;
- controlled clutter;
- recognizable original landmarks;
- no photogrammetry chaos for its own sake.

## Color language

- warm aged timber;
- faded painted metal;
- oxidized steel;
- muted rural greens;
- gravel gray and brown;
- pale Nordic sky;
- lake blue-gray;
- restrained amber UI and practical-light accents;
- Finnish blue-and-white details only where contextually appropriate.

## Readability

Interactive and vehicle-relevant areas must remain readable under:

- direct sun;
- overcast;
- rain;
- fog;
- dusk;
- interior lighting;
- headlights.

---

# SCALE, PIVOT, AND FIT PARITY

Production replacements must bind to donor-derived spatial fixtures.

For every production replacement, validate:

- world transform;
- local transform;
- pivot;
- rotation axis;
- parent relationship;
- bounds;
- footprint;
- height;
- doorway clearance;
- stair height;
- floor level;
- roof clearance;
- vehicle clearance;
- interaction-anchor position;
- collider bounds;
- road connection;
- terrain contact;
- adjacent-object overlap.

Create:

- `WorldReplacementFitValidator`
- `WorldPivotParityValidator`
- `WorldBoundsParityValidator`
- `WorldClearanceValidator`
- `WorldAnchorParityValidator`

Support:

- overlay comparison;
- ghosted donor geometry;
- side-by-side view;
- numeric deviation report;
- bounding-box comparison;
- pivot gizmos;
- door-swing visualization;
- vehicle-clearance envelope visualization.

Default tolerances must be category-specific.

Examples:

- major building placement: very low positional deviation;
- door pivot: near-exact;
- road centerline: near-exact;
- decorative foliage: broad tolerance;
- terrain microdetail: broad tolerance;
- garage floor height: near-exact;
- bridge deck height: near-exact;
- sign placement: moderate tolerance;
- shoreline silhouette: measured tolerance;
- hidden decorative backfaces: low priority.

Document tolerance values and rationale.

---

# PILOT PRODUCTION ZONE

Use one representative pilot zone before full-world remastering.

Preferred pilot:

- player home;
- garage;
- driveway;
- nearby road;
- nearby terrain;
- nearby vegetation;
- nearby shoreline or ditch where available;
- representative interior;
- doors and gates;
- static props;
- vehicle-accessible space.

The pilot must exercise:

- terrain;
- road;
- building exterior;
- building interior;
- modular assets;
- props;
- vegetation;
- collision;
- LOD;
- streaming;
- lighting compatibility;
- wetness;
- landmark validation;
- player navigation;
- vehicle clearance.

Pilot workflow:

1. Freeze donor/reference fixtures.
2. Create a production replacement profile.
3. Produce blockout replacements.
4. Validate scale and pivots.
5. Create first-pass geometry.
6. Create first-pass materials.
7. Create collision.
8. Create LODs.
9. Integrate with streaming.
10. Test player and vehicle clearance.
11. Test clear, overcast, rain, dusk, and night preview conditions.
12. Profile.
13. Fix systemic defects.
14. Approve the pipeline.
15. Only then apply it to the full world.

Create:

`Docs/WorldRemaster/PILOT_ZONE_REPORT.md`

Do not mass-remaster all zones before the pilot pipeline is accepted.

---

# TERRAIN REMASTER

Preserve:

- world scale;
- terrain extents;
- major height profile;
- road elevations;
- ditches;
- shoreline;
- yards;
- building pads;
- bridges;
- landmarks;
- gameplay traversal.

Rebuild terrain presentation using modern production methods.

Required capabilities:

- new terrain layers;
- physically plausible albedo;
- normals;
- mask/smoothness data;
- macro variation;
- detail variation;
- slope-aware blending;
- height-aware blending;
- road-edge blending;
- shoreline blending;
- mud and wetness support;
- puddle masks or accumulation inputs;
- vegetation masks;
- distance consistency;
- no obvious tiled repetition;
- runtime performance appropriate for the baseline target.

Do not use donor terrain textures as final layers.

If terrain geometry needs correction:

- preserve landmark fixtures;
- preserve road and building contact;
- record the correction;
- update deviation reports;
- do not deform gameplay-critical areas casually.

Create:

- `TerrainProductionProfile`
- `TerrainLayerSet`
- `TerrainBlendRule`
- `TerrainWetnessProfile`
- `TerrainRemasterProcessor`
- `TerrainProductionValidator`

Create:

`Docs/WorldRemaster/TERRAIN_REMASTER_REPORT.md`

---

# ROAD REMASTER

Rebuild roads from the durable road representation and donor reference.

Road categories may include:

- gravel road;
- dirt road;
- paved road;
- driveway;
- yard track;
- bridge deck;
- service area;
- parking area.

Preserve:

- route;
- width;
- elevation;
- junction position;
- bridge alignment;
- driveway connections;
- roadside clearances;
- travel distance;
- gameplay navigation.

Create production road profiles supporting:

- surface material;
- edge profile;
- shoulder width;
- ditch profile;
- crown/camber;
- gravel scatter;
- mud;
- tire tracks;
- puddle masks;
- deformation or decal hooks;
- wetness;
- dust hooks;
- snow-free summer baseline;
- collision;
- audio-surface tags;
- wheel-friction tags;
- LOD strategy.

Avoid floating road ribbons and hard terrain seams.

Create:

- `ProductionRoadProfile`
- `ProductionRoadBuilder`
- `RoadShoulderBuilder`
- `RoadDitchBuilder`
- `RoadJunctionBuilder`
- `RoadBridgeBinding`
- `RoadSurfaceMetadata`
- `RoadRemasterValidator`

Validate:

- centerline deviation;
- width deviation;
- elevation deviation;
- junction continuity;
- vehicle clearance;
- collider continuity;
- material continuity;
- terrain blending;
- cell-boundary continuity.

Create:

`Docs/WorldRemaster/ROAD_REMASTER_REPORT.md`

---

# BUILDING EXTERIOR REMASTER

Rebuild all production building exteriors.

Use modular construction where sensible, but do not force every unique landmark
into generic modular pieces.

Production building workflow:

1. Extract dimensions and silhouette from donor reference.
2. Identify gameplay-critical openings and clearances.
3. Define modular and unique sections.
4. Create high-detail source geometry where needed.
5. Create game-ready topology.
6. Create UVs.
7. Bake maps where needed.
8. Create material assignment.
9. Create LODs.
10. Create collision.
11. Preserve door, gate, and window pivots.
12. Bind to stable world IDs.
13. Validate against donor reference.
14. Validate player and vehicle access.
15. Validate weather and wetness.

Required material families may include:

- aged timber;
- painted timber;
- corrugated metal;
- rusted metal;
- galvanized metal;
- concrete;
- brick;
- plaster;
- glass;
- roofing felt;
- roof sheet metal;
- dirt;
- moss;
- oil contamination;
- painted signage.

Create:

- `BuildingProductionDefinition`
- `BuildingModuleDefinition`
- `BuildingReplacementBinding`
- `BuildingOpeningRecord`
- `BuildingMaterialProfile`
- `BuildingProductionValidator`

Create:

`Docs/WorldRemaster/BUILDING_EXTERIOR_REPORT.md`

---

# INTERIOR REMASTER

Rebuild interiors while preserving:

- room dimensions;
- floor elevations;
- door positions;
- stair positions;
- window positions;
- garage bays;
- work areas;
- storage areas;
- gameplay routes;
- interaction anchors;
- vehicle access;
- tool and part placement capacity.

Interior production requirements:

- coherent modular wall/floor/ceiling system;
- believable structural thickness;
- trim and transition pieces;
- practical lighting fixture positions;
- material variation;
- dirt and wear where people work or walk;
- oil and mechanical grime in workshops;
- moisture and age where appropriate;
- readable interactive surfaces;
- interior acoustic-zone hooks;
- lighting-volume hooks;
- reflection-probe hooks;
- wetness exclusion or interior wetness rules;
- no light leaks caused by broken shells.

Do not add clutter that blocks original gameplay routes.

Do not permanently bake prop placement into meshes when props need independent
state or interaction.

Create:

- `InteriorProductionDefinition`
- `InteriorRoomRecord`
- `InteriorPortalBinding`
- `InteriorLightingAnchor`
- `InteriorAudioZoneAnchor`
- `InteriorClutterProfile`
- `InteriorProductionValidator`

Create:

`Docs/WorldRemaster/INTERIOR_REMASTER_REPORT.md`

---

# DOORS, GATES, WINDOWS, AND MOVING ARCHITECTURE

All moving architecture must preserve mechanical relationships.

For every door, gate, hatch, or window:

- stable ID;
- frame binding;
- pivot;
- axis;
- opening range;
- closed transform;
- open transform;
- collider states;
- handle or interaction anchors;
- sound-event hooks;
- obstruction behavior;
- save-state binding;
- replacement prefab;
- donor parity report.

Do not merge moving elements into static building meshes.

Create editor visualization for:

- pivot;
- swing arc;
- clearance;
- collider states;
- interaction point.

Create:

`Docs/WorldRemaster/MOVING_ARCHITECTURE_REPORT.md`

---

# PROPS AND CLUTTER

Rebuild static and semi-static world props.

Classify props as:

- landmark prop;
- reusable production prop;
- gameplay-relevant prop;
- interactive prop;
- decorative prop;
- repeated infrastructure;
- debris cluster;
- decal-only replacement;
- vegetation-like scatter;
- reject;
- manual-art task.

Do not model every tiny object as a unique high-poly hero asset.

Use:

- reusable prop libraries;
- trim sheets;
- atlases where appropriate;
- decals;
- material variants;
- controlled procedural scatter;
- cluster prefabs;
- per-instance variation;
- stable IDs for gameplay-relevant props only.

Preserve exact placement for:

- landmark props;
- gameplay-relevant props;
- props affecting vehicle traversal;
- props used by interaction or quests;
- collision-critical props.

Allow art-directed variation for purely decorative repeated props.

Create:

- `ProductionPropDefinition`
- `PropReplacementProfile`
- `PropScatterProfile`
- `PropClusterDefinition`
- `PropProductionValidator`

Create:

`Docs/WorldRemaster/PROPS_AND_CLUTTER_REPORT.md`

---

# VEGETATION REMASTER

Replace donor vegetation visuals with a scalable production vegetation system.

Preserve:

- forest masses;
- major tree positions where silhouette or gameplay requires them;
- clearings;
- roadside visibility;
- field boundaries;
- landmark trees when identifiable;
- traversal corridors;
- collision-critical trees;
- shoreline vegetation;
- yard vegetation;
- gameplay sightlines.

Production vegetation categories:

- conifers;
- deciduous trees;
- young trees;
- dead trees;
- bushes;
- shrubs;
- grass;
- weeds;
- reeds;
- roadside vegetation;
- yard vegetation;
- field crops or field cover;
- moss;
- ground litter;
- fallen branches;
- stumps.

Required features:

- GPU instancing;
- wind compatibility;
- LODs;
- billboards or impostors when appropriate;
- density controls;
- biome profiles;
- terrain masks;
- road exclusion;
- building exclusion;
- shoreline rules;
- collider rules;
- shadow tiers;
- distance fade;
- seasonal extensibility without implementing winter now;
- wetness compatibility;
- color variation;
- scale variation;
- no obvious repeated rotation pattern.

Do not create a persistent GameObject for every grass instance.

Create:

- `VegetationBiomeProfile`
- `VegetationSpeciesDefinition`
- `VegetationPlacementConverter`
- `VegetationExclusionVolume`
- `VegetationLandmarkBinding`
- `VegetationProductionValidator`

Create:

`Docs/WorldRemaster/VEGETATION_REMASTER_REPORT.md`

---

# ROCKS, GROUND DEBRIS, AND NATURAL DETAIL

Create production systems for:

- rocks;
- pebbles;
- gravel;
- fallen branches;
- forest litter;
- mud clumps;
- roadside debris;
- shoreline stones;
- grass-to-road transitions;
- ditch detail.

Use a mix of:

- terrain detail;
- instancing;
- decals;
- cluster meshes;
- material layering;
- sparse hero assets.

Prevent:

- floating rocks;
- repeated identical clusters;
- road obstruction;
- collider spam;
- excessive overdraw;
- visual noise around interaction zones.

---

# WATER AND SHORELINE REMASTER

Replace donor water visuals with a modern production solution.

Preserve:

- water-body location;
- water level;
- shoreline shape;
- islands;
- docks;
- bridge relationships;
- gameplay access;
- swimming/boat routes when relevant.

Production requirements:

- HDRP-compatible water;
- reflection strategy;
- refraction strategy;
- shallow/deep color;
- shoreline foam where appropriate;
- wind response;
- rain response;
- underwater extensibility;
- interaction hooks;
- audio-zone hooks;
- performance tiers;
- no ray-tracing requirement;
- no obvious seam across world cells.

Rebuild shoreline transitions with:

- wet ground;
- reeds;
- stones;
- mud;
- vegetation masks;
- shallow-water blending.

Create:

- `WaterProductionProfile`
- `WaterBodyProductionBinding`
- `ShorelineProductionProfile`
- `WaterProductionValidator`

Create:

`Docs/WorldRemaster/WATER_REMASTER_REPORT.md`

---

# INFRASTRUCTURE

Rebuild production infrastructure:

- road signs;
- speed signs;
- directional signs;
- utility poles;
- wires;
- fences;
- gates;
- mailboxes;
- roadside markers;
- culverts;
- drains;
- bridges;
- small rural structures.

Preserve placement and readable sign identity where it contributes to world
recognition.

Important text and signage must be recreated as:

- new vector artwork;
- new high-resolution decals;
- new typography;
- new material response.

Do not use a low-resolution donor sign texture as the final sign.

Create infrastructure profiles and reusable production prefabs.

Utility wires must use a scalable spline or cable system rather than manually
modeled unique long meshes where practical.

Create:

`Docs/WorldRemaster/INFRASTRUCTURE_REMASTER_REPORT.md`

---

# MATERIAL SYSTEM

Create:

`Docs/WorldRemaster/MATERIAL_STANDARD.md`

Use HDRP Lit or justified project-owned shaders.

Every production material must define the relevant data:

- base color without baked lighting;
- metallic;
- smoothness or perceptual roughness conversion;
- normal;
- ambient occlusion;
- optional height;
- detail normal;
- detail mask;
- emissive when physically justified;
- wetness response;
- dirt or damage layering where needed;
- decal response;
- double-sided policy;
- alpha clipping policy;
- transparent-surface policy.

Use consistent mask-map packing.

Document channel packing explicitly.

Define material families rather than one-off material chaos.

Recommended master families:

- terrain;
- road;
- soil;
- gravel;
- timber;
- painted timber;
- metal;
- painted metal;
- rusted metal;
- concrete;
- brick;
- plaster;
- glass;
- roofing;
- rubber;
- plastic;
- vegetation;
- water;
- decals;
- signage;
- dirt;
- oil;
- wet surfaces.

Avoid creating hundreds of near-identical material assets.

Use material property overrides and variants where appropriate.

Create a material-audit tool that reports:

- missing maps;
- invalid texture import settings;
- duplicate materials;
- extreme smoothness;
- invalid metallic values;
- donor texture dependencies;
- non-HDRP shaders;
- excessive material slots;
- transparent materials in inappropriate categories.

---

# TEXTURE STANDARD

Create:

`Docs/WorldRemaster/TEXTURE_STANDARD.md`

Define:

- naming;
- color-space import rules;
- compression rules;
- normal-map import rules;
- maximum sizes by category;
- minimum practical sizes;
- texel-density targets;
- trim-sheet policy;
- atlas policy;
- decal policy;
- mipmap policy;
- streaming-mipmap policy;
- alpha policy;
- source-file policy;
- generated-map policy.

Recommended texture suffixes:

- `_BC`
- `_N`
- `_MASK`
- `_H`
- `_E`
- `_D`
- `_ID`
- `_OPACITY`

Do not duplicate the same source texture across unrelated folders.

Do not commit temporary AI-upscaled donor textures as production assets.

If AI-assisted reference enhancement is used:

- mark it as reference-only;
- do not treat fabricated detail as source truth;
- verify labels and markings manually;
- recreate production art independently.

---

# GEOMETRY STANDARD

Create:

`Docs/WorldRemaster/GEOMETRY_STANDARD.md`

Define:

- scale;
- axis;
- pivot;
- naming;
- topology;
- smoothing;
- normals;
- tangents;
- UV channels;
- lightmap UV policy;
- LOD naming;
- collision naming;
- modular-grid rules;
- transform-freeze rules;
- negative-scale policy;
- FBX or interchange settings;
- source-authoring file policy;
- prefab nesting;
- mesh-readability policy;
- mesh-compression policy;
- static batching policy.

Production geometry must:

- use clean topology;
- avoid unnecessary hidden geometry;
- preserve important silhouette;
- include bevels where lighting requires them;
- avoid subpixel detail that belongs in normal maps;
- avoid giant combined meshes crossing streaming cells;
- avoid excessive separate material slots;
- preserve gameplay-critical dimensions.

---

# LOD, HLOD, AND DISTANCE STRATEGY

Every production asset category must have an explicit distance strategy.

Create:

- `WorldLodProfile`
- `WorldHlodProfile`
- `LodValidationTool`
- `HlodGenerationPlan`
- cell proxy generation where justified.

Define:

- LOD0 use;
- LOD1;
- LOD2;
- billboard/impostor;
- culling distance;
- shadow distance;
- material simplification;
- collider persistence;
- interior visibility;
- landmark exceptions.

Do not apply the same LOD percentages blindly to every asset.

Validate:

- silhouette stability;
- popping;
- bounds;
- material continuity;
- shadow popping;
- cell-boundary behavior;
- driving-speed visibility.

Create:

`Docs/WorldRemaster/LOD_HLOD_REPORT.md`

---

# COLLISION REMASTER

Replace donor collision with production collision where appropriate.

Collision categories:

- terrain;
- road;
- building shell;
- floor;
- wall;
- roof;
- door/gate;
- static prop;
- vegetation;
- player blocker;
- vehicle blocker;
- trigger candidate;
- shoreline;
- bridge;
- stairs;
- ramps.

Requirements:

- simple primitives where possible;
- simplified meshes where needed;
- correct layer;
- correct physics material;
- no invisible obstruction;
- no floor gaps;
- no doorway blockage;
- no accidental thin-wall tunneling at expected speeds;
- no high-poly visual-mesh colliders by default;
- no dynamic non-convex MeshCollider misuse;
- stable binding to world IDs.

Create:

- `ProductionCollisionProfile`
- `ProductionCollisionBuilder`
- `ProductionCollisionValidator`
- player-clearance tests;
- vehicle-clearance tests;
- continuous-road collision tests.

Create:

`Docs/WorldRemaster/COLLISION_REMASTER_REPORT.md`

---

# DECALS, DIRT, DAMAGE, AND WEATHERING

Create a layered environment-detail strategy.

Use decals and masks for:

- oil stains;
- tire marks;
- rust streaks;
- dirt;
- mud;
- water streaks;
- faded paint;
- moss;
- wall damage;
- road patches;
- signage wear;
- workshop grime.

Do not bake all wear into unique textures.

Do not apply random dirt uniformly.

Wear must follow plausible causes:

- water flow;
- wheel paths;
- hand contact;
- vehicle movement;
- leaks;
- foot traffic;
- sun exposure;
- ground proximity;
- roof runoff;
- workshop use.

Create:

- `EnvironmentDecalProfile`
- `WeatheringProfile`
- `WetnessMaskBinding`
- `DirtAccumulationAnchor`
- `DecalProductionValidator`

The system must be compatible with future dynamic wetness and weather.

---

# LIGHTING AND WEATHER COMPATIBILITY

This milestone does not finalize weather or final lighting, but production
geometry and materials must be compatible with them.

Create preview conditions:

- clear midday;
- golden evening;
- overcast;
- rain;
- fog;
- dusk;
- night;
- interior practical lighting;
- vehicle headlights.

Validate:

- material response;
- readability;
- light leaks;
- reflection behavior;
- wetness;
- glass;
- signage;
- road visibility;
- interior/exterior exposure transitions.

Do not hide defects with color grading.

Use restrained temporary post-processing.

Create:

`Docs/WorldRemaster/LIGHTING_COMPATIBILITY_REPORT.md`

---

# STREAMING AND WORLD PARTITION COMPATIBILITY

Production replacements must use the existing world-partition system.

Requirements:

- no cross-cell hierarchy that breaks unloading;
- explicit handling of large objects;
- explicit handling of roads crossing cells;
- explicit handling of water crossing cells;
- explicit handling of utility wires crossing cells;
- interior loading policy;
- landmark always-loaded policy where justified;
- production proxy support;
- deterministic generated scenes;
- no manual scene drift from the world database.

Create:

- `ProductionWorldCellBuilder`
- `ProductionReplacementResolver`
- `ProductionCellValidationTool`
- production-cell generation commands;
- production-only world mode;
- reference-only world mode;
- comparison world mode.

Generated production cells must be rebuildable.

Manual art overrides must be stored as explicit project-owned override data,
not hidden scene edits.

Create:

`Docs/WorldRemaster/STREAMING_INTEGRATION_REPORT.md`

---

# EDITOR TOOLING

Create a tool menu:

`Tools → MSC Remake → World Remaster`

Required tools:

- `Open World Remaster Dashboard`
- `Select Pilot Zone`
- `Show Donor Reference`
- `Show Production Replacement`
- `Show Overlay Comparison`
- `Validate Selected Replacement`
- `Validate Selected Zone`
- `Build Selected Production Cell`
- `Build All Production Cells`
- `Show Missing Replacements`
- `Show Donor Dependencies`
- `Show Bounds Deviations`
- `Show Pivot Deviations`
- `Show Road Deviations`
- `Show Collision Deviations`
- `Generate Art Backlog`
- `Open Replacement Ledger`
- `Open Performance Report`
- `Clear Generated Production Cells`
- `Rebuild Generated Production Cells`

Create `WorldRemasterDashboard`.

It must show:

- zone status;
- replacement counts;
- approved counts;
- donor-reference counts;
- missing replacements;
- blocked assets;
- manual-art tasks;
- material validation;
- LOD validation;
- collision validation;
- performance status;
- estimated scene memory;
- production-cell build status;
- last validation;
- links to reports.

Long operations must:

- show progress;
- support cancellation where practical;
- write logs;
- not mark partial output as valid;
- use atomic replacement where practical.

---

# AUTHORING TOOL INTEGRATION

Inspect available local authoring tools.

When Blender exists locally and project policy permits:

- create documented CLI-compatible conversion/export steps;
- create non-destructive source files where practical;
- use deterministic export presets;
- store Blender source files in an appropriate project-art location if they are
  newly authored;
- do not place donor binary assets into committed production source files unless
  project policy explicitly permits it;
- do not install Blender add-ons silently.

When no capable external authoring tool is available:

- implement Unity-side procedural and modular tooling where practical;
- generate accurate blockouts;
- create a manual-art backlog for hero and complex assets;
- do not claim blockouts are final production assets.

Codex must distinguish between:

- code/tooling completed;
- procedural asset completed;
- production candidate completed;
- manual modeling required;
- manual texturing required;
- blocked.

---

# ZONE-BY-ZONE REMASTER WORKFLOW

Do not remaster the entire world as one opaque operation.

Each zone must pass:

1. `ReferenceVerified`
2. `BlockoutReady`
3. `GeometryFirstPass`
4. `MaterialsFirstPass`
5. `CollisionReady`
6. `LodReady`
7. `StreamingReady`
8. `LightingCompatible`
9. `PerformanceChecked`
10. `ParityValidated`
11. `ArtReviewRequired`
12. `Approved`
13. `Complete`

Create:

`Docs/WorldRemaster/ZONE_REMASTER_STATUS.csv`

For every zone, record:

- zone ID;
- zone name;
- source cells;
- major landmarks;
- terrain status;
- road status;
- building status;
- interior status;
- vegetation status;
- water status;
- infrastructure status;
- props status;
- collision status;
- LOD status;
- material status;
- performance status;
- parity status;
- blockers;
- manual tasks;
- review notes.

Process zones in batches.

Recommended order:

1. home and garage pilot zone;
2. nearby road and shoreline;
3. main workshop/service landmark;
4. primary town/service cluster;
5. major road junctions;
6. bridge and water zones;
7. remote buildings;
8. forest and field cells;
9. remaining roads and minor props;
10. full-world consistency pass.

Derive actual zones from the world database rather than relying only on this
example.

---

# MANUAL ART BACKLOG

Codex must not pretend to hand-author AAA assets that it cannot actually create.

Create:

`Docs/WorldRemaster/WORLD_ART_BACKLOG.csv`

Required columns:

- `TaskId`
- `ZoneId`
- `StableWorldId`
- `AssetCategory`
- `AssetName`
- `TaskType`
- `Priority`
- `ReferenceAvailable`
- `DimensionsVerified`
- `PivotVerified`
- `BlockoutAvailable`
- `RequiredSourceTool`
- `RequiredMaps`
- `RequiredLods`
- `RequiredCollision`
- `EstimatedComplexity`
- `Dependencies`
- `AcceptanceCriteria`
- `Status`
- `Notes`

Task types may include:

- `HighPolyModel`
- `GameReadyModel`
- `Retopology`
- `UV`
- `Bake`
- `Texture`
- `Material`
- `Decal`
- `Signage`
- `LOD`
- `Collision`
- `VegetationAsset`
- `InteriorSet`
- `HeroAsset`
- `ManualPlacementReview`
- `ParityReview`

Every blocked production asset must appear in this backlog.

---

# PERFORMANCE TARGET

Use the project’s existing performance budget when available.

Otherwise use this baseline for the production world:

- Windows x64;
- 1920×1080 baseline;
- 60 FPS target on a reasonable mid-range gaming PC;
- no ray tracing required;
- scalable settings;
- HDRP;
- streaming enabled;
- development profiling with representative player and vehicle movement.

Do not guess final hardware-specific FPS.

Record actual test hardware and configuration.

Measure:

- CPU frame time;
- GPU frame time;
- draw calls;
- batches;
- triangles;
- visible renderer count;
- shadow casters;
- material count;
- texture memory;
- mesh memory;
- cell load time;
- cell unload time;
- peak memory;
- vegetation cost;
- water cost;
- road cost;
- interior cost;
- HLOD benefit.

Create:

`Docs/WorldRemaster/WORLD_REMASTER_PERFORMANCE_REPORT.md`

Use tiered quality settings where appropriate.

---

# TESTS

Add EditMode tests for:

- replacement-registry serialization;
- stable binding between donor record and production replacement;
- production prefab independence from donor meshes;
- production material independence from donor textures;
- bounds-parity calculations;
- pivot-parity calculations;
- road-deviation calculations;
- cell assignment;
- production-cell deterministic generation;
- material validation;
- LOD validation;
- collision validation;
- missing replacement reporting;
- art-backlog generation;
- zone-status transitions;
- invalid production dependency detection.

Add PlayMode tests where practical for:

- loading the production world;
- loading a production cell;
- unloading a production cell;
- switching reference/production/comparison modes;
- locating major landmarks;
- player traversal through pilot-zone doors and stairs;
- vehicle clearance through garage and driveway;
- continuous road collision;
- no missing production prefab in approved pilot zone;
- no donor-reference renderer active in production-only mode.

If Unity batch mode is available, run tests.

If it is unavailable:

- do not claim tests passed;
- report static validation separately;
- provide exact test commands;
- list manual Unity steps.

---

# VISUAL REGRESSION CAPTURES

Create a repeatable screenshot/capture plan.

For each major landmark and pilot zone, capture:

- donor/reference view;
- production view;
- overlay/comparison view;
- clear midday;
- golden evening;
- overcast;
- rain preview;
- night or dusk;
- interior view;
- road approach view;
- vehicle-clearance view.

Create:

`Docs/WorldRemaster/VISUAL_REGRESSION_PLAN.md`

Store generated captures outside source control or in a controlled reports
location according to repository policy.

Do not use screenshots as the only geometric validation.

---

# DOCUMENTATION OUTPUTS

Create or update:

`Docs/WorldRemaster/PRE_FLIGHT_AUDIT.md`
`Docs/WorldRemaster/WORLD_ART_DIRECTION.md`
`Docs/WorldRemaster/MATERIAL_STANDARD.md`
`Docs/WorldRemaster/TEXTURE_STANDARD.md`
`Docs/WorldRemaster/GEOMETRY_STANDARD.md`
`Docs/WorldRemaster/PILOT_ZONE_REPORT.md`
`Docs/WorldRemaster/TERRAIN_REMASTER_REPORT.md`
`Docs/WorldRemaster/ROAD_REMASTER_REPORT.md`
`Docs/WorldRemaster/BUILDING_EXTERIOR_REPORT.md`
`Docs/WorldRemaster/INTERIOR_REMASTER_REPORT.md`
`Docs/WorldRemaster/MOVING_ARCHITECTURE_REPORT.md`
`Docs/WorldRemaster/PROPS_AND_CLUTTER_REPORT.md`
`Docs/WorldRemaster/VEGETATION_REMASTER_REPORT.md`
`Docs/WorldRemaster/WATER_REMASTER_REPORT.md`
`Docs/WorldRemaster/INFRASTRUCTURE_REMASTER_REPORT.md`
`Docs/WorldRemaster/LOD_HLOD_REPORT.md`
`Docs/WorldRemaster/COLLISION_REMASTER_REPORT.md`
`Docs/WorldRemaster/LIGHTING_COMPATIBILITY_REPORT.md`
`Docs/WorldRemaster/STREAMING_INTEGRATION_REPORT.md`
`Docs/WorldRemaster/WORLD_REMASTER_PERFORMANCE_REPORT.md`
`Docs/WorldRemaster/VISUAL_REGRESSION_PLAN.md`
`Docs/WorldRemaster/WORLD_REPLACEMENT_LEDGER.csv`
`Docs/WorldRemaster/WORLD_ART_BACKLOG.csv`
`Docs/WorldRemaster/ZONE_REMASTER_STATUS.csv`

Create:

`Docs/Milestones/MILESTONE_05A_REPORT.md`

Update:

- `Docs/ROADMAP.md`
- the project porting ledger;
- architecture documentation where new systems are introduced;
- performance documentation;
- testing documentation;
- `.gitignore` where generated donor-derived or production-cell content must not
  be committed.

Do not overwrite prior milestone reports.

---

# DEFINITION OF DONE

This milestone is complete only when all applicable conditions are true or
specific blockers are documented.

## Foundation

1. The donor-reference world remains intact.
2. The production world is a separate layer.
3. Stable world IDs remain unchanged.
4. The replacement registry exists.
5. The production world does not use donor textures as final assets.
6. Production prefabs do not depend on donor meshes.
7. The pilot zone is completed and validated.
8. The world-remaster dashboard exists.
9. Generated production cells are deterministic.
10. Comparison modes exist.

## World coverage

11. Every discovered remaster zone has a status record.
12. Every donor/reference asset has a replacement status.
13. Terrain has a production strategy and implementation.
14. Roads have a production strategy and implementation.
15. Major building exteriors have production replacements or explicit backlog
    tasks.
16. Major interiors have production replacements or explicit backlog tasks.
17. Doors, gates, and windows preserve pivots and clearances.
18. Vegetation has a scalable production system.
19. Water has a production replacement strategy.
20. Infrastructure has production replacements or explicit backlog tasks.
21. Props have production replacements or explicit backlog tasks.
22. Collision has production replacements.
23. LOD strategy is implemented.
24. Streaming integration is validated.
25. Major landmarks remain within configured parity tolerances.

## Quality

26. Material standards exist.
27. Texture standards exist.
28. Geometry standards exist.
29. Production material audit passes for approved zones.
30. Production texture audit passes for approved zones.
31. LOD validation passes for approved zones.
32. Collision validation passes for approved zones.
33. Player traversal passes in approved zones.
34. Vehicle clearance passes in vehicle-accessible approved zones.
35. Lighting compatibility has been checked.
36. Wetness compatibility has been checked.
37. Performance has been measured.
38. Visual-regression captures exist for the pilot zone.

## Honesty

39. No blockout is falsely labeled final.
40. No missing hero art is silently ignored.
41. Every manual-art task is recorded.
42. Every blocked zone is listed.
43. Completion percentages are reported by category and by zone.
44. Unexecuted tests are clearly marked unexecuted.
45. No public-release claim is made.
46. No donor/reference binary content is accidentally committed contrary to
    repository policy.
47. The original installation remains unchanged.
48. The project has no new unexplained compiler errors.
49. `Docs/Milestones/MILESTONE_05A_REPORT.md` exists.
50. The report gives an exact recommended next milestone.

A world with only the pilot zone remastered is not “complete.”

If complete world production art cannot realistically be generated during one
execution, do not lie.

Instead:

- complete the pipeline;
- complete and validate the pilot zone;
- generate deterministic blockouts where useful;
- create the full replacement registry;
- create the full art backlog;
- report exact completion by category and zone;
- identify which production assets require manual Blender/Substance-equivalent
  work;
- provide an ordered execution plan for subsequent zone batches.

---

# NON-GOALS

Do not implement during this milestone:

- vehicle powertrain simulation;
- final Wwise integration;
- NPC behavior;
- traffic;
- quests;
- dialogue;
- survival balancing;
- multiplayer;
- final menu UI;
- full weather simulation;
- final save migration from the donor game;
- public release packaging;
- final cinematic trailer scenes;
- unrelated architecture rewrites.

Do not refactor player or vehicle systems unless required to preserve world
interfaces and the change is tightly scoped.

---

# REQUIRED FINAL RESPONSE

At the end, provide:

1. Executive summary.
2. Prerequisites found and missing.
3. Current world-reference status.
4. Production-world architecture created.
5. Replacement-registry status.
6. Pilot zone selected.
7. Pilot zone work completed.
8. Terrain status.
9. Road status.
10. Building-exterior status.
11. Interior status.
12. Moving-architecture status.
13. Prop and clutter status.
14. Vegetation status.
15. Water status.
16. Infrastructure status.
17. Material-system status.
18. Texture-standard status.
19. Geometry-standard status.
20. Collision status.
21. LOD/HLOD status.
22. Streaming status.
23. Landmark parity status.
24. Player and vehicle clearance status.
25. Lighting and weather compatibility status.
26. Performance measurements.
27. Automated tests executed and results.
28. Manual validation performed.
29. Files created and modified.
30. Generated local-content paths.
31. Donor dependencies remaining.
32. Manual-art backlog summary.
33. Completion percentage by category.
34. Completion percentage by zone.
35. Exact blockers.
36. Risks.
37. Manual Unity or Blender actions still required.
38. Recommended next milestone.
39. Exact next Codex prompt.

Create:

`Docs/Milestones/MILESTONE_05A_REPORT.md`

Do not proceed to unrelated milestones during this task.

Begin now.
