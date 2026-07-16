/plan

# MILESTONE 04A — FULL WORLD GEOMETRY TRANSFER

> This exhaustive prompt conflicts with the current `AGENTS.md` non-goal that
> excludes the entire original map until the single-player vertical slice is
> stable. It is retained as future planning material only. The executable next
> prompt is `Prompts/04A_WORLD_LAYOUT_PILOT.md`. If this file is invoked before
> project policy is explicitly changed, stop without modifying files.

Read `AGENTS.md` completely before doing anything.

Read `Prompts/CURRENT_STATE.md` and `Prompts/PROJECT_DESIGN_GUARDRAILS.md` when present.

## CURRENT SEQUENCE NOTE

Milestones 00 through 04 are expected to be complete before this prompt is run.

This milestone is intentionally inserted after player interaction and before
vehicle assembly. Preserve the completed player/interaction architecture and
do not rerun or rewrite prior milestones unless a measured world-integration
defect requires a tightly scoped fix.


Also read, if they exist:

- `Docs/PROJECT.md`
- `Docs/ARCHITECTURE.md`
- `Docs/PORTING_GUIDE.md`
- `Docs/REVERSE_ENGINEERING.md`
- `Docs/ART_GUIDE.md`
- `Docs/WORLD_WEATHER.md`
- `Docs/ROADMAP.md`
- `Docs/Porting/DONOR_AUDIT.md`
- `Docs/Porting/PORTING_MATRIX.md`
- `Docs/Porting/PORTING_LEDGER.csv`
- `Docs/Milestones/MILESTONE_00_REPORT.md`
- `Docs/Milestones/MILESTONE_01_REPORT.md`
- `Docs/Milestones/MILESTONE_02_REPORT.md`

This task must be executed only after the donor-reference pipeline exists or
after its missing prerequisites have been explicitly identified and created.

---

## PROJECT CONTEXT

This is a private donor-assisted recreation of My Summer Car in Unity 6 HDRP.

The locally installed licensed original game is used as a read-only donor for:

- complete world layout;
- terrain and ground geometry;
- roads and roadside geometry;
- building shells and interiors;
- static props;
- collider geometry;
- pivots;
- transforms;
- hierarchy;
- landmark positions;
- shoreline and water placement;
- vegetation placement data;
- gameplay-relevant spatial relationships.

The long-term production visuals will be reauthored.

The immediate purpose of this milestone is to reconstruct the complete original
map geometry and layout inside the Unity 6 project with measurable spatial
fidelity, while keeping donor content isolated and replaceable.

The durable output is not merely a pile of imported meshes.

The durable outputs are:

1. A complete world-layout database.
2. A complete provenance manifest.
3. Deterministic world-generation tools.
4. A spatially partitioned reference/prototype world.
5. Validation reports proving that the transferred map is complete enough to
   serve as the geometric foundation of the remake.
6. Stable placement identities that future reauthored assets can reuse without
   moving gameplay-critical coordinates.

---

## PATHS

Use the current repository root as `PROJECT_ROOT`.

Read machine-specific paths from:

`Config/DonorPaths.local.json`

If that file does not exist, create it from the example file without committing
the local copy.

Expected donor installation:

`D:\SteamLibrary\steamapps\common\My Summer Car`

Expected project locations may be either:

`E:\GAYmDev_Studio\MySummerCar_Remake`

or:

`E:\GAYmDev_Studio\MySummerCar_Remake_Game`

Do not move or rename the current project. Treat the currently opened repository
as authoritative.

Preferred external staging directories must come from the local config. If no
staging path is configured, use a sibling directory outside the Unity project:

`<PROJECT_ROOT>_DonorStaging`

Preferred legacy decompilation/reference directory:

`<PROJECT_ROOT>_LegacyReference`

The original installation is always read-only.

---

## EXECUTION MODE

Set:

`WORLD_TRANSFER_MODE = ReferenceGeometryFirst`

This means:

- transfer the complete geometric world as a reference/prototype layer;
- preserve exact layout and gameplay-critical dimensions;
- do not treat old graphics as final production art;
- keep original meshes and original textures out of final production folders;
- create data and tooling that allow every donor mesh to be replaced later
  without changing the map layout.

Use three logical layers:

### Layer A — Donor Source Layer

External, read-only or generated staging data.

Examples:

- raw serialized asset exports;
- original scene exports;
- original terrain data;
- original meshes;
- original collider meshes;
- original hierarchy records;
- decompiled references;
- source hashes.

This layer must remain outside Git and outside normal production content.

### Layer B — Legacy Reference World

A complete transferred map used for:

- dimension checking;
- visual comparison;
- navigation;
- gameplay prototyping;
- replacement alignment;
- road and terrain reconstruction;
- landmark validation.

This layer may use donor geometry in private local development builds, but it
must be clearly labeled as transitional reference content.

### Layer C — Durable World Layout

Project-owned data that survives replacement of all donor meshes.

It must contain:

- stable world entity IDs;
- global transforms;
- bounds;
- hierarchy relationships;
- semantic categories;
- source provenance;
- cell membership;
- landmark tags;
- road data;
- terrain data;
- interior links;
- collider intent;
- replacement status.

Future production assets must bind to this layer.

---

## HARD CONSTRAINTS

1. Never modify the original game installation.
2. Never patch the original executable.
3. Never bypass DRM, Steam ownership checks, encryption, anti-tamper, or access
   controls.
4. Do not import the entire donor dump directly into `Assets/`.
5. Do not create one monolithic scene containing the complete world.
6. Do not put thousands of independent vegetation GameObjects into scenes.
7. Do not use original textures as final production textures.
8. Do not use original lightmaps as the new lighting solution.
9. Do not use old materials as final HDRP materials.
10. Do not convert every mesh into a production prefab without classification.
11. Do not use render meshes as collision meshes by default.
12. Do not silently change scale, origin, handedness, pivots, or road elevation.
13. Do not silently discard unknown or unsupported objects.
14. Do not declare completion when major landmarks, roads, terrain sections, or
    interiors are missing.
15. Do not manually edit generated world scenes as the primary workflow.
16. Do not hard-code absolute local paths in committed C# source.
17. Do not start final art production during this milestone.
18. Do not implement NPCs, quests, traffic, survival systems, or vehicle
    simulation during this milestone.
19. Do not add networking.
20. Do not assume one Unity unit equals one meter until scale is verified.
21. Do not assume the extracted hierarchy is complete until cross-checked against
    the original serialized data and runtime observations.
22. Do not silently install external tools or packages.
23. If a required extractor is missing, create exact manual instructions and
    continue implementing the deterministic import/build pipeline.
24. All generated donor-derived content must be removable and regenerable.
25. All important omissions must be recorded in a machine-readable missing-data
    report.

---

## DONOR-FIDELITY AUTHORITY

Real donor captures/data and verified reference geometry are authoritative for
layout. AI concepts and generic Finnish references are not valid sources for
world placement, terrain, roads, buildings or landmark identity.

Every later production cell must retain canonical camera fixtures and human
approval state.

## PRIMARY OBJECTIVE

Transfer the complete geometric map of the donor game into the Unity 6 project
as a spatially accurate, partitioned, validated reference/prototype world.

“Complete geometric map” includes, where present in the donor data:

- global world extents;
- terrain;
- ground meshes;
- roads;
- junctions;
- shoulders;
- ditches;
- bridges;
- culverts;
- shoreline;
- water surfaces;
- islands;
- building exteriors;
- building interiors;
- garages;
- stores;
- workshops;
- houses;
- barns;
- sheds;
- utility buildings;
- road signs;
- fences;
- gates;
- poles;
- wires represented by geometry or placement data;
- rocks;
- major static props;
- trees and vegetation placement;
- fields;
- yards;
- driveways;
- static collision geometry;
- door and window pivots;
- stairs;
- floors;
- ramps;
- spawn or marker transforms when discoverable;
- gameplay-relevant anchor points;
- scene hierarchy and object relationships.

Do not confuse completeness with production quality.

The geometry may initially be visually old, neutral, or proxy-based.
Spatial fidelity and reproducibility are the priority.

---

## REQUIRED PRE-FLIGHT CHECK

Before changing files:

1. Confirm the current repository root.
2. Confirm the Unity version from `ProjectSettings/ProjectVersion.txt`.
3. Confirm HDRP is installed or record that it is not yet installed.
4. Confirm the donor-game path exists.
5. Confirm the donor installation can be read without modification.
6. Confirm the staging directory exists or create it outside the repository.
7. Detect available local tools, including when present:

   - AssetRipper;
   - AssetStudio;
   - ilspycmd or ILSpy;
   - Blender;
   - Unity batch-mode executable;
   - any project-provided donor extraction tools.

8. Inspect previous milestone reports.
9. Inspect current importer and manifest code.
10. Identify what is already implemented and reuse it where sound.
11. Produce a concrete execution plan.
12. List blockers and assumptions.
13. Do not duplicate systems that already exist.

If the Unity project currently has compiler errors, fix or document them before
building the world pipeline.

---

# PHASE 1 — DONOR WORLD DISCOVERY

Inspect the donor game and identify every source that contributes to world
geometry.

Search for:

- primary Unity scenes;
- additive scenes;
- serialized scene data;
- shared assets;
- asset bundles;
- resource files;
- terrain data;
- mesh assets;
- nested GameObject hierarchies;
- prefab-like serialized hierarchies;
- collider assets;
- procedural generation data;
- object-placement tables;
- road geometry;
- vegetation placement;
- water geometry;
- animation objects that contain important pivots;
- runtime-instantiated world objects;
- decompiled classes that create, reposition, enable, or disable map objects;
- scripts or state machines that load world regions;
- PlayMaker FSMs that hold transform values or instantiate geometry;
- world-related plugins;
- hidden or disabled scene objects;
- LOD groups;
- occlusion areas;
- navmesh-related data;
- light probes and reflection probes as reference metadata;
- lightmaps only as historical reference;
- spawn and teleport points;
- doors and gates;
- interior/exterior transition anchors.

Create:

`Docs/WorldTransfer/DONOR_WORLD_DISCOVERY.md`

It must document:

- all discovered source files;
- scene names;
- asset-bundle names;
- serialized file names;
- relevant assemblies and classes;
- relevant state machines;
- extraction status;
- confidence level;
- known missing data;
- likely runtime-generated sections;
- third-party dependencies;
- whether each source is required for geometry completeness.

Create a source inventory:

`Docs/WorldTransfer/DONOR_WORLD_SOURCES.csv`

Required columns:

- `SourceId`
- `RelativePath`
- `FileType`
- `SizeBytes`
- `Sha256`
- `ContainsScenes`
- `ContainsMeshes`
- `ContainsTerrain`
- `ContainsColliders`
- `ContainsPlacementData`
- `ContainsRuntimeGeneration`
- `ExtractionTool`
- `ExtractionStatus`
- `Notes`

Do not repeatedly hash very large irrelevant files.
Hash all files that directly contribute to the world transfer.

---

# PHASE 2 — COORDINATE SYSTEM AND SCALE AUDIT

Before importing map content, determine the donor coordinate conventions.

Investigate:

- world origin;
- unit scale;
- axis orientation;
- handedness;
- transform serialization;
- local versus global transforms;
- parent scale usage;
- negative scale;
- rotated coordinate roots;
- mesh import scale;
- terrain dimensions;
- known landmark distances;
- player eye height if available;
- vehicle dimensions if useful as a scale fixture;
- road width;
- garage dimensions;
- building door height;
- known object dimensions.

Do not assume scale.

Create:

`Docs/WorldTransfer/COORDINATE_AND_SCALE_AUDIT.md`

Define a single explicit conversion:

`DonorWorldToRemakeWorldMatrix`

If no conversion is required, document that it is the identity matrix and show
the evidence.

Create a serializable project-owned configuration:

`WorldCoordinateConversionConfig`

It must contain at least:

- source unit scale;
- destination unit scale;
- axis mapping;
- handedness conversion;
- global translation;
- global rotation;
- global scale;
- pivot policy;
- negative-scale handling policy;
- floating-origin recommendation;
- precision notes.

Create EditMode tests that verify:

- known point conversion;
- known direction conversion;
- rotation conversion;
- scale conversion;
- parent-child transform preservation;
- round-trip conversion within numeric tolerance.

Select at least five landmark or dimensional fixtures from different parts of
the world.

Record them in:

`Docs/WorldTransfer/LANDMARK_FIXTURES.csv`

Required columns:

- `FixtureId`
- `Name`
- `SourcePositionX`
- `SourcePositionY`
- `SourcePositionZ`
- `ExpectedPositionX`
- `ExpectedPositionY`
- `ExpectedPositionZ`
- `ToleranceMeters`
- `MeasurementSource`
- `VerificationStatus`
- `Notes`

---

# PHASE 3 — INTERMEDIATE WORLD DATA MODEL

Do not make Unity scenes the only source of truth.

Create a project-owned, versioned intermediate world representation.

Recommended runtime/editor data types:

- `WorldGeometryDatabase`
- `WorldGeometryDatabaseVersion`
- `WorldSourceRecord`
- `WorldEntityRecord`
- `WorldTransformRecord`
- `WorldHierarchyRecord`
- `WorldMeshReferenceRecord`
- `WorldColliderRecord`
- `WorldTerrainRecord`
- `WorldRoadRecord`
- `WorldWaterRecord`
- `WorldVegetationRecord`
- `WorldInteriorRecord`
- `WorldPortalRecord`
- `WorldLandmarkRecord`
- `WorldCellRecord`
- `WorldMissingReferenceRecord`
- `WorldReplacementRecord`
- `WorldTransferReport`

Every world entity must support:

- deterministic stable ID;
- donor source ID;
- donor object/local file ID when available;
- original hierarchy path;
- original name;
- normalized name;
- semantic category;
- source transform;
- converted transform;
- source bounds;
- converted bounds;
- mesh reference;
- material reference metadata;
- collider metadata;
- static flags;
- active state;
- cell membership;
- interior/exterior classification;
- landmark tag;
- replacement status;
- transfer status;
- notes;
- source hash or source-manifest reference.

Use versioned JSON, binary data, ScriptableObjects, or a hybrid format.
Choose a format that:

- can be regenerated;
- can be diffed where practical;
- does not require loading all meshes to inspect placement data;
- supports very large object counts;
- supports editor validation;
- supports future migration;
- supports stable IDs.

Do not store millions of lines in one hand-edited JSON file if a more scalable
format is required.

Document the selected format in:

`Docs/WorldTransfer/WORLD_DATA_FORMAT.md`

---

# PHASE 4 — STABLE ID AND PROVENANCE RULES

Create deterministic stable IDs.

A stable ID should be based on immutable donor provenance, such as:

- source file hash or source ID;
- serialized object/local ID;
- scene or bundle name;
- original hierarchy path;
- mesh/object role;
- normalized duplicate index only when unavoidable.

Do not base persistent identity only on:

- current Unity instance ID;
- generated scene order;
- current object name;
- random GUID created on every import;
- transform position alone.

Create:

- `WorldStableId`
- `WorldStableIdUtility`
- duplicate-ID validator;
- missing-ID validator;
- deterministic-ID tests.

Every generated object must have:

- a stable world ID;
- a legacy/donor reference;
- a transfer category;
- a replacement status.

Recommended replacement statuses:

- `DonorReference`
- `PrototypeRuntime`
- `ReplacementPlanned`
- `ReplacementInProgress`
- `ProductionReplacement`
- `Verified`
- `Rejected`
- `Missing`
- `Unsupported`

Update:

`Docs/Porting/PORTING_LEDGER.csv`

Do not overwrite prior ledger data.
Add world-specific records or create:

`Docs/WorldTransfer/WORLD_PORTING_LEDGER.csv`

---

# PHASE 5 — COMPLETE GEOMETRY EXTRACTION

Use the best available local non-destructive extraction workflow.

Preferred workflow:

Original installation
→ external raw extraction
→ normalized intermediate data
→ Unity controlled importer
→ world database
→ generated world cells/scenes

If AssetRipper is available and compatible:

- export to the external raw staging directory;
- preserve export logs;
- preserve source file relationships;
- do not export directly into `Assets/`;
- do not treat reconstructed materials as production materials;
- do not assume reconstructed scenes are complete without comparison.

If AssetRipper is unavailable or fails:

- inspect whether AssetStudio or another local tool can export the required
  meshes and serialized data;
- do not silently download executables;
- create exact manual extraction instructions;
- create parsers/importers around the available export format;
- continue with pipeline implementation;
- clearly distinguish “pipeline implemented” from “full geometry extracted”.

Create:

`Docs/WorldTransfer/EXTRACTION_LOG.md`

Create machine-readable manifests in the external staging directory:

- `WorldGeometryManifest.json`
- `WorldObjectPlacements.csv`
- `WorldMeshManifest.csv`
- `WorldColliderManifest.csv`
- `WorldTerrainManifest.json`
- `WorldRoadManifest.json`
- `WorldWaterManifest.json`
- `WorldVegetationManifest.csv`
- `WorldInteriorManifest.csv`
- `WorldLandmarkManifest.csv`
- `WorldMissingReferences.csv`
- `WorldUnsupportedObjects.csv`

At minimum, each placement record must contain:

- stable source ID;
- source object ID;
- source hierarchy path;
- object name;
- object type;
- parent ID;
- local position;
- local rotation;
- local scale;
- global position;
- global rotation;
- global scale;
- active state;
- mesh ID;
- collider IDs;
- source bounds;
- semantic category;
- source scene or bundle;
- extraction status.

Record unsupported serialized types instead of dropping them.

---

# PHASE 6 — WORLD SEMANTIC CLASSIFICATION

Classify every discovered world object.

Suggested categories:

- `Terrain`
- `GroundMesh`
- `Road`
- `RoadShoulder`
- `Ditch`
- `Bridge`
- `Culvert`
- `Water`
- `Shoreline`
- `BuildingExterior`
- `BuildingInterior`
- `Floor`
- `Roof`
- `Door`
- `Gate`
- `Window`
- `Fence`
- `UtilityPole`
- `Wire`
- `RoadSign`
- `Landmark`
- `StaticProp`
- `InteractivePropCandidate`
- `VegetationTree`
- `VegetationBush`
- `VegetationGrass`
- `Rock`
- `Field`
- `Yard`
- `Driveway`
- `ColliderOnly`
- `SpawnMarker`
- `GameplayMarker`
- `AudioZoneReference`
- `InteriorPortal`
- `Unknown`

Classification must be:

- data-driven;
- overridable;
- recorded in the manifest;
- deterministic;
- testable;
- not based only on English object names.

Create classification rules in:

`Config/WorldTransferRules.example.json`

Create an ignored local override file:

`Config/WorldTransferRules.local.json`

Create a report:

`Docs/WorldTransfer/WORLD_CLASSIFICATION_REPORT.md`

Include counts per category and unresolved objects.

Unknown objects are not deleted.
They remain visible in validation reports and debug views.

---

# PHASE 7 — WORLD PARTITIONING AND STREAMING LAYOUT

Do not generate a single giant map scene.

Create a configurable world-partition system suitable for Unity 6.

Recommended scene structure:

- `World_Persistent`
- `World_GlobalReference`
- `World_Terrain`
- `World_Water`
- `World_Roads`
- `World_Interiors`
- `World_Cell_<X>_<Z>`
- optional landmark scenes for unusually complex areas

Use a `WorldPartitionConfig` containing:

- cell size;
- world origin;
- world bounds;
- loading radius;
- unloading radius;
- vertical bounds;
- landmark override rules;
- interior handling;
- always-loaded categories;
- large-object handling;
- cross-cell parent policy;
- cross-cell collider policy;
- editor preview rules.

Do not arbitrarily choose cell size without inspecting:

- world dimensions;
- object density;
- terrain resolution;
- road continuity;
- building clusters;
- future streaming needs;
- editor usability.

A starting cell size may be proposed, but it must remain configurable.

Large continuous objects such as:

- terrain;
- long road segments;
- lakes;
- bridges;
- utility lines;

must have explicit handling rather than being duplicated across cells.

Create:

- `WorldPartitionBuilder`
- `WorldCellIndex`
- `WorldCellBounds`
- `WorldCellMembershipUtility`
- `WorldSceneGenerationSettings`
- editor commands for generating selected cells and all cells.

Generated scenes must be deterministic and regenerable.

Manual edits to generated scenes must either:

- be forbidden and validated;
- or be stored as explicit overrides outside the generated scene.

Document the policy.

---

# PHASE 8 — TERRAIN TRANSFER

Determine whether the donor world uses:

- Unity Terrain;
- mesh terrain;
- multiple terrain tiles;
- terrain plus overlay meshes;
- procedural deformation;
- hidden collision terrain;
- runtime detail placement.

Transfer all available terrain geometry and metadata.

Preserve:

- terrain dimensions;
- height range;
- height samples;
- tile alignment;
- terrain holes if present;
- ground-mesh overlays;
- shoreline shape;
- major embankments;
- ditches;
- road cuts;
- building pads;
- collider intent.

If the donor terrain is a mesh:

- preserve the donor mesh as reference;
- generate a heightfield only if the conversion is valid;
- record areas where overhangs, bridges, tunnels, steep cuts, or non-heightfield
  geometry cannot be represented by Unity Terrain;
- retain supplemental meshes where necessary.

If the donor uses Unity Terrain:

- export height data;
- export size;
- export tree placements;
- export detail placements;
- export layer identity as metadata only;
- do not use donor textures as final terrain layers.

Create:

- `TerrainTransferProcessor`
- `TerrainTileManifest`
- `TerrainValidationReport`
- neutral HDRP debug terrain material or materials.

Validate:

- world bounds;
- minimum and maximum elevations;
- landmark ground heights;
- tile seams;
- terrain/road alignment;
- terrain/building alignment;
- shoreline elevation;
- collider continuity.

Create:

`Docs/WorldTransfer/TERRAIN_TRANSFER_REPORT.md`

---

# PHASE 9 — ROAD NETWORK TRANSFER

Transfer the complete road network.

Preserve:

- centerline shape;
- elevation;
- width;
- banking or camber where detectable;
- junction positions;
- road intersections;
- driveways;
- shoulders;
- ditches;
- bridge alignment;
- roadside-sign placements;
- transitions between road types;
- collision surfaces.

Keep the original road meshes as a reference layer.

Also produce a durable road representation suitable for future reauthoring.

Preferred durable representation:

- road graph;
- ordered spline segments;
- lane or centerline metadata;
- width samples;
- elevation samples;
- road-surface category;
- shoulder width;
- junction IDs;
- bridge/culvert flags;
- landmark associations.

Do not simplify the road network until geometric deviation is measured.

Create:

- `WorldRoadDatabase`
- `WorldRoadSegment`
- `WorldRoadJunction`
- `WorldRoadSplineSample`
- `RoadGeometryReference`
- `RoadDeviationValidator`
- editor visualization for centerlines and widths.

If automatic centerline extraction is unreliable:

- preserve exact donor road geometry;
- create manual-correction data;
- generate a review report;
- do not pretend the spline representation is exact.

Produce:

`Docs/WorldTransfer/ROAD_NETWORK_REPORT.md`

Include:

- total discovered road-mesh length;
- total reconstructed centerline length;
- segment count;
- junction count;
- missing segments;
- maximum observed deviation;
- average observed deviation;
- areas requiring manual review.

---

# PHASE 10 — BUILDINGS AND INTERIORS

Transfer all discovered building shells and interior geometry.

Preserve:

- global transform;
- foundation height;
- building footprint;
- room dimensions;
- floor heights;
- stairs;
- ramps;
- door pivots;
- gate pivots;
- window locations;
- roof shape;
- interior/exterior alignment;
- garage-bay dimensions;
- interactive anchor points when discoverable;
- collider boundaries.

Do not merge every building into one mesh.

Create a hierarchy that separates:

- building root;
- exterior shell;
- interior shell;
- floors;
- doors;
- gates;
- windows;
- static props;
- collider groups;
- interaction-anchor references;
- replacement slots.

Create interior metadata:

- interior ID;
- owning building ID;
- entrance portal IDs;
- bounding volume;
- connected exterior cell;
- always-loaded status;
- streaming policy;
- audio-zone placeholder;
- lighting-volume placeholder.

The transferred donor geometry may remain as reference/prototype geometry.
Future reauthored building prefabs must be able to replace it through stable IDs
without changing the building transform.

Create:

`Docs/WorldTransfer/BUILDINGS_AND_INTERIORS_REPORT.md`

Include a table of all major buildings and whether:

- exterior geometry was found;
- interior geometry was found;
- colliders were found;
- doors/gates were found;
- pivots were verified;
- placement was verified;
- replacement is planned.

---

# PHASE 11 — VEGETATION AND STATIC PROP PLACEMENT

Transfer vegetation placement and static prop placement without creating a
GameObject explosion.

For vegetation, preserve:

- species/source identity;
- position;
- rotation;
- scale;
- terrain association;
- cell association;
- density grouping;
- active state;
- source provenance.

Use data-oriented placement records.

Do not create one persistent GameObject per grass blade or distant tree.

Create a reference rendering strategy using:

- GPU instancing;
- terrain tree instances;
- batched prototype renderers;
- editor-only proxy points;
- or another scalable project-owned solution.

The reference strategy must be replaceable by the final vegetation system.

For static props, classify:

- major landmark prop;
- gameplay-relevant prop candidate;
- decorative static prop;
- collider-only prop;
- vegetation-like repeated prop;
- unknown.

Create:

- `VegetationPlacementDatabase`
- `StaticPropPlacementDatabase`
- `VegetationPrototypeRecord`
- `PropPrototypeRecord`
- `PlacementBatch`
- editor visualization and filtering.

Create:

`Docs/WorldTransfer/VEGETATION_AND_PROPS_REPORT.md`

Include total counts and rendering strategy.

---

# PHASE 12 — WATER AND SHORELINE

Transfer all water-related geometry and placement.

Preserve:

- water-body bounds;
- water elevation;
- shoreline shape;
- islands;
- docks or shoreline structures;
- bridge-water relationships;
- underwater collision or ground where present;
- water transition volumes if discoverable.

Do not use donor water materials as final HDRP water.

Create:

- `WorldWaterDatabase`
- `WaterBodyRecord`
- `ShorelineRecord`
- neutral HDRP water proxy or debug material;
- future replacement hooks for HDRP Water System or a project-owned solution.

Validate water elevation against:

- terrain;
- shoreline;
- docks;
- bridges;
- buildings near water.

Create:

`Docs/WorldTransfer/WATER_TRANSFER_REPORT.md`

---

# PHASE 13 — COLLIDER TRANSFER

Transfer and classify collision geometry.

Collider categories:

- terrain collision;
- road collision;
- building shell collision;
- floor collision;
- wall collision;
- roof collision;
- door/gate collision;
- static prop collision;
- vehicle-accessible surface;
- player-only blocker;
- trigger/volume candidate;
- unknown.

Preserve original collider dimensions and transforms as reference.

Do not automatically use high-poly visual meshes as MeshColliders.

For each collider, record:

- stable ID;
- source object;
- collider type;
- convex state;
- dimensions;
- center;
- transform;
- material metadata;
- trigger state;
- associated render entity;
- gameplay intent if known;
- replacement status.

Create:

- `WorldColliderDatabase`
- `ColliderTransferProcessor`
- `ColliderDebugView`
- `ColliderValidationTool`

Validate:

- no major terrain holes;
- no missing building floors;
- no blocked doorways caused by conversion;
- no inverted collider transforms;
- no extreme scales;
- no duplicate colliders;
- no obvious road discontinuities;
- no accidental trigger conversion.

Create:

`Docs/WorldTransfer/COLLIDER_TRANSFER_REPORT.md`

---

# PHASE 14 — MATERIAL AND VISUAL DEBUG POLICY

This milestone is not final art production.

Create neutral category-based HDRP debug materials, for example:

- terrain;
- roads;
- buildings;
- interiors;
- props;
- vegetation;
- water;
- colliders;
- missing references;
- unknown objects.

Do not copy donor textures into production material folders.

Donor textures may be extracted into external staging for identification and
reference, but they must not become the final materials.

Create a world-debug display mode that can visualize:

- semantic categories;
- streaming cells;
- stable IDs;
- source scenes;
- missing references;
- colliders;
- donor versus replacement geometry;
- landmark fixtures;
- road centerlines;
- interior volumes;
- object bounds.

Do not spend time on final lighting or color grading here.

---

# PHASE 15 — EDITOR TOOLING

Create a Unity Editor toolset under a menu such as:

`Tools → MSC Remake → World Transfer`

Required commands:

- `Open World Transfer Window`
- `Validate Paths`
- `Scan Donor World`
- `Dry Run Extraction Plan`
- `Build Intermediate World Database`
- `Import Selected Zone`
- `Import All World Geometry`
- `Generate Selected World Cells`
- `Generate All World Cells`
- `Validate World Database`
- `Validate Generated Scenes`
- `Compare Landmark Fixtures`
- `Show Missing References`
- `Show Unsupported Objects`
- `Open World Transfer Reports`
- `Clear Generated World Content`
- `Rebuild Generated World Content`

Create a `WorldTransferWindow` with:

- configured paths;
- tool availability;
- extraction status;
- database version;
- object counts;
- category counts;
- missing-reference count;
- unsupported-object count;
- generated-cell count;
- validation status;
- last-build timestamp;
- dry-run button;
- selected-zone controls;
- build-all control with explicit confirmation;
- progress reporting;
- cancellation support where practical;
- log output;
- links to reports.

Long operations must:

- show progress;
- remain cancelable where practical;
- write logs;
- avoid leaving partially valid data marked as complete;
- use staging and atomic replacement when practical.

Generated content must include a generator-version stamp.

---

# PHASE 16 — REFERENCE/PROTOTYPE WORLD SCENES

Create a navigable reference world.

Required scenes:

- `WorldTransfer_Bootstrap`
- `World_Persistent`
- generated cell scenes or equivalent partitioned content
- optional `WorldTransfer_Validation`
- optional landmark comparison scenes

Add a development-only free-fly camera.

The reference world must allow:

- editor navigation;
- play-mode navigation;
- loading nearby cells;
- viewing the complete transferred layout;
- toggling debug categories;
- jumping to landmarks by stable ID or landmark name;
- viewing object provenance;
- viewing missing-reference markers.

The fly camera and debug UI must be development-only and excluded from normal
release builds unless explicitly enabled.

Do not add gameplay systems during this milestone.

---

# PHASE 17 — COMPLETENESS AND FIDELITY VALIDATION

Create automated and manual validation.

## Required automated checks

- donor source files exist;
- source hashes match the manifest;
- all placement records have stable IDs;
- stable IDs are unique;
- all parent references resolve;
- all cell references resolve;
- all referenced meshes resolve or appear in missing-reference reports;
- all collider references resolve or appear in missing-reference reports;
- no invalid NaN or infinity transforms;
- no zero-scale objects unless explicitly allowed;
- no extreme unexpected scales;
- world bounds are plausible;
- terrain bounds match expected dimensions;
- road segments remain within world bounds;
- buildings are not far outside expected cells;
- landmark fixtures are within configured tolerances;
- generated scenes match the database version;
- generated object counts match database counts by category;
- no runtime assembly references editor-only assemblies;
- generated reference content is clearly marked;
- production replacement folders do not depend on donor texture assets.

## Required parity reports

Create:

`Docs/WorldTransfer/WORLD_COMPLETENESS_REPORT.md`

It must contain:

- discovered source-object count;
- extracted object count;
- imported object count;
- generated world-entity count;
- missing object count;
- unsupported object count;
- count by semantic category;
- count by source scene/bundle;
- count by generated cell;
- major-landmark checklist;
- terrain coverage;
- road coverage;
- building coverage;
- interior coverage;
- collider coverage;
- vegetation coverage;
- water coverage;
- known limitations;
- manual review queue.

Create:

`Docs/WorldTransfer/WORLD_FIDELITY_REPORT.md`

It must contain:

- coordinate conversion summary;
- world extents comparison;
- landmark deviation table;
- terrain elevation deviation;
- road deviation;
- building placement deviation;
- interior alignment issues;
- collider alignment issues;
- confidence rating per zone;
- screenshots required for manual comparison;
- unresolved discrepancies.

## Required manual validation checklist

Create:

`Docs/WorldTransfer/WORLD_MANUAL_VALIDATION_CHECKLIST.md`

Include at minimum:

- compare major landmarks;
- compare garage and home area;
- compare major road junctions;
- compare road elevation changes;
- compare bridge positions;
- compare shoreline;
- compare major buildings;
- compare interior/exterior alignment;
- compare driveways and yards;
- compare sign and pole placement;
- test continuous driving surfaces;
- inspect missing-reference markers;
- inspect cell boundaries;
- inspect terrain seams.

Where direct runtime capture from the original is not automated, describe exact
manual capture steps.

Do not claim visual parity without evidence.

---

# PHASE 18 — PERFORMANCE AND EDITOR-SCALABILITY RULES

The complete reference world must remain usable.

Apply these rules:

- avoid a single giant hierarchy;
- avoid a GameObject for every grass detail;
- use scene partitioning;
- use batched placement data;
- use LOD or proxy rendering for reference geometry where available;
- disable expensive donor shaders;
- use neutral debug materials;
- do not enable realtime shadows on every reference object;
- do not bake final lighting;
- avoid unnecessary Rigidbody components;
- mark static reference geometry appropriately after validation;
- avoid non-convex dynamic MeshColliders;
- do not load all interiors if a streaming policy can keep them separate;
- allow selected-zone generation for iteration;
- provide “full-world validation” separately from normal editor work;
- record import time, scene-generation time, database size, generated-object
  count, and memory observations.

Create:

`Docs/WorldTransfer/WORLD_TRANSFER_PERFORMANCE_REPORT.md`

This is not the final game-performance pass.
It is a usability and pipeline scalability report.

---

# PHASE 19 — TESTS

Add EditMode tests for at least:

- coordinate conversion;
- stable-ID determinism;
- stable-ID uniqueness;
- manifest serialization;
- database version migration;
- hierarchy reconstruction;
- local/global transform preservation;
- cell assignment;
- large-object cell handling;
- path normalization;
- source-hash comparison;
- duplicate-source detection;
- missing-reference reporting;
- classification rules;
- terrain bounds;
- road-record serialization;
- collider-record serialization;
- deterministic generation planning;
- dry-run/import-plan parity.

Add PlayMode tests where practical for:

- loading `WorldTransfer_Bootstrap`;
- loading and unloading a nearby cell;
- resolving a landmark by stable ID;
- spawning the development fly camera;
- validating that generated geometry appears at expected landmark positions.

If Unity batch mode is available, run tests.

If it is not available:

- do not claim they passed;
- report static inspection separately;
- provide exact commands to run;
- list any manual Editor steps.

---

# PHASE 20 — GENERATED FILE AND GIT POLICY

Raw donor content must remain outside Git.

Generated donor-derived geometry must be:

- ignored by Git where appropriate;
- reproducible from manifests and local donor data;
- clearly marked;
- removable without deleting project-owned source code or world-layout schemas.

Commit:

- importer code;
- generator code;
- configuration examples;
- schema definitions;
- tests;
- documentation;
- small non-copyrighted fixtures created for tests;
- manifests that contain metadata only when appropriate;
- reports that do not embed donor binary content.

Do not commit:

- original game files;
- extracted donor meshes;
- extracted donor textures;
- raw scenes;
- decompiled donor source;
- generated donor asset bundles;
- generated local world scenes when they contain donor geometry, unless the
  project’s existing private repository policy explicitly allows it.

Update `.gitignore` carefully.
Do not accidentally ignore project-owned source or documentation.

---

# REQUIRED PROJECT STRUCTURE

Create or align with the existing project structure.

Suggested paths:

`Assets/Game/LegacyImport/Runtime/World`
`Assets/Game/LegacyImport/Editor/World`
`Assets/Game/LegacyImport/ReferenceOnly/World`
`Assets/Game/World/Runtime/Data`
`Assets/Game/World/Runtime/Partition`
`Assets/Game/World/Runtime/Streaming`
`Assets/Game/World/Editor`
`Assets/Game/World/Generated`
`Assets/Game/World/Debug`
`Assets/Game/Tests/EditMode/WorldTransfer`
`Assets/Game/Tests/PlayMode/WorldTransfer`

Documentation:

`Docs/WorldTransfer`

Configuration:

`Config/WorldTransferConfig.example.json`
`Config/WorldTransferConfig.local.json`
`Config/WorldTransferRules.example.json`
`Config/WorldTransferRules.local.json`

External staging:

`raw/world`
`normalized/world`
`manifests/world`
`logs/world`
`captures/world`

Respect existing assembly definitions.
Create new assembly definitions only where needed.

Runtime code must not depend on editor assemblies.

---

# REQUIRED CONFIGURATION

Create:

`Config/WorldTransferConfig.example.json`

It should support at least:

- donor game path;
- donor staging path;
- legacy reference path;
- raw extraction path;
- normalized data path;
- generated Unity content path;
- reference-only Unity content path;
- coordinate conversion config path;
- partition cell size;
- selected-zone filter;
- import batch size;
- vegetation proxy mode;
- collider import mode;
- material debug mode;
- generate runtime prototype;
- generate editor-only reference;
- strict validation;
- allow unsupported objects;
- dry-run mode;
- log path;
- report path.

Create a local ignored copy for machine-specific values.

Do not hard-code machine paths in C#.

---

# MAJOR LANDMARK COVERAGE

Do not rely solely on a hard-coded landmark list.

Derive the complete landmark list from the donor world and existing project
documentation.

At minimum, explicitly identify and validate:

- the player’s primary home/garage area;
- major town/service areas;
- major workshop/repair areas;
- major road junctions;
- major bridges;
- major water-adjacent locations;
- major remote buildings;
- major yards and driveways;
- major interiors;
- any large unique world structures;
- any location used as a spawn, teleport, delivery, race, service, or quest
  anchor when discoverable.

Create:

`Docs/WorldTransfer/MAJOR_LANDMARKS.md`

For every major landmark, record:

- stable ID;
- source name;
- normalized name;
- position;
- bounds;
- source scene/bundle;
- geometry status;
- collider status;
- interior status;
- validation status;
- screenshot/reference status;
- known issues.

---

# ZONE-BY-ZONE EXECUTION

A full-map transfer is too risky as one unobserved batch.

Process the map in deterministic zones.

Recommended workflow:

1. Build the source inventory.
2. Build the complete world database.
3. Generate a low-cost proxy overview.
4. Verify world origin and bounds.
5. Select a representative pilot zone.
6. Transfer terrain, roads, buildings, props, vegetation, water, and colliders
   for the pilot zone.
7. Validate the pilot zone.
8. Fix conversion and generation defects.
9. Transfer all remaining zones in batches.
10. Run complete-world validation.
11. Produce the final reports.

The pilot zone should contain several geometry types, such as:

- terrain;
- road;
- building exterior;
- interior;
- props;
- vegetation;
- colliders;
- a known landmark.

The player’s home/garage area is a reasonable pilot candidate if the donor
audit supports it.

Do not stop permanently after the pilot zone.

The final objective remains the complete map.
However, do not continue mass generation while known conversion defects would
corrupt every remaining zone.

For every zone, write a structured status record:

- `NotScanned`
- `Scanned`
- `Extracted`
- `Normalized`
- `Generated`
- `Validated`
- `NeedsReview`
- `Blocked`
- `Complete`

Create:

`Docs/WorldTransfer/ZONE_STATUS.csv`

---

# DEFINITION OF DONE

This milestone is complete only when all of the following are true, or when
specific blockers are transparently documented:

1. The donor world sources have been inventoried.
2. The coordinate system and scale have been verified.
3. The world database exists and is versioned.
4. Deterministic stable IDs exist.
5. The complete discovered map geometry has a database record.
6. Terrain coverage is represented.
7. The road network is represented.
8. Major buildings are represented.
9. Major interiors are represented where donor data exists.
10. Water bodies and shoreline placement are represented.
11. Vegetation and static-prop placements are represented.
12. Collider data is represented.
13. The world is partitioned into manageable cells or scenes.
14. A navigable reference/prototype world can be generated.
15. Major landmarks can be located by stable ID.
16. Missing and unsupported objects are reported rather than silently dropped.
17. Object-count and category parity reports exist.
18. Landmark-fidelity reports exist.
19. The import/generation pipeline is repeatable.
20. Generated donor content can be cleared and rebuilt.
21. The original installation remains unchanged.
22. No donor textures are treated as final production textures.
23. No final art work is falsely declared complete.
24. Tests were run or exact unexecuted test commands are reported.
25. `Docs/Milestones/MILESTONE_04A_REPORT.md` exists.
26. The report includes a precise recommendation for the next milestone.
27. The project has no new unexplained compiler errors.
28. All blocked zones and missing source data are listed explicitly.

If automatic extraction limitations prevent complete transfer, do not claim
completion.

Instead provide:

- exact completed percentage by category;
- exact blocked zones;
- exact missing source types;
- exact manual steps required;
- the implemented pipeline status;
- the safest next action.

---

# NON-GOALS

Do not implement during this milestone:

- final production models;
- final textures;
- final HDRP materials;
- final lighting;
- final vegetation visuals;
- final water visuals;
- weather;
- NPCs;
- traffic;
- quests;
- player survival systems;
- vehicle assembly;
- vehicle simulation;
- Wwise content;
- final UI;
- multiplayer;
- final navmesh;
- full gameplay logic;
- final optimization.

A basic fly camera and debug UI are allowed only for validation.

---

# FINAL RESPONSE FORMAT

At the end of the task, report:

1. Executive summary.
2. Repository and path validation.
3. Donor world sources discovered.
4. Coordinate and scale findings.
5. World-data format created.
6. Stable-ID strategy.
7. Extraction tools used.
8. Extraction results.
9. Total source-object counts.
10. Counts by geometry category.
11. Terrain transfer status.
12. Road-network transfer status.
13. Building and interior transfer status.
14. Vegetation and prop transfer status.
15. Water transfer status.
16. Collider transfer status.
17. Partitioning strategy and cell count.
18. Generated scenes and tools.
19. Validation results.
20. Landmark fidelity results.
21. Missing references.
22. Unsupported objects.
23. Tests executed and results.
24. Unity batch-mode commands executed.
25. Manual Editor actions still required.
26. Files created and modified.
27. Generated local content paths.
28. Known risks.
29. Exact blockers.
30. Exact recommended next prompt.

Create:

`Docs/Milestones/MILESTONE_04A_REPORT.md`

Do not proceed to final art, garage remodeling, player systems, or vehicle
systems in this task.

Begin now.
