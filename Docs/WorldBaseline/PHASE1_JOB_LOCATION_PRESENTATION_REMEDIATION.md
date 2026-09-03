# Phase 1 missing job-location presentation remediation

Status: **generated and automated-valid; manual map comparison pending**.

## Finding

The active donor streaming baseline contained only objects marked
`ReferenceWorldEligible` in the frozen 04A1 entity table. Seven complete donor
hierarchy roots were classified out of that original allowlist, so their
absence was deterministic and was not caused by cell transitions:

- `JOBS/StrawberryField/`;
- `JOBS/HouseShit1/` through `JOBS/HouseShit5/`;
- `JOBS/Farm/`;
- `JOBS/Mummola/LOD/Shed/`.

This explains the missing strawberry field, Berryman tent, septic-job houses,
waste wells and the complete Farmer farm context. The missing tent also made
Berryman's correct seated donor pose look like a broken standing pose.

## Implemented boundary

`Phase1JobLocationPresentationBuilder` selects only reviewed active static
`MeshRenderer` + `MeshFilter` presentation and enabled non-trigger static
collision below those roots. It explicitly excludes `/skeleton/`, `/ShitNPC/`,
`/Functions/`, `/WaspStrawberry/`, `/Farmer/` and `/LOD/combine/`, so donor
NPCs, PlayMaker/job authority, triggers, the dynamic combine hierarchy and
other dynamic actors are not imported.

The current hash-locked manifest produces:

- 306 temporary renderers, including `LOD/field`, both `LOD/Tent` renderers,
  49 reviewed Farm renderers and the five-renderer Mummola shed closure;
- 102 effective static colliders;
- two explicitly audited donor `MeshCollider` records with no mesh, omitted
  because they have no collision effect;
- overlays in nine existing cells;
- project-owned stable replacement keys and supplemental metadata understood by
  the existing Phase-2 replacement registry.

The reviewed Jokke-house facade renderer with stable ID
`26ba07f32895986ad6b5e1af60e8f42f`, adjacent HouseDrunk shed wall
`a4fcacc09bcdf19b2870e93c5ae37c1f`, windows
`8009a38a36145d5f2e723307bdab9f5d` and wooden doors/trim
`877425d5f49f92c6ad267866a9225a45` each receive a generated per-overlay HDRP
material clone with double-sided rasterization. Globally shared materials are
not modified, so missing exterior/interior faces are repaired without changing
unrelated world renderers.

Generated donor payload remains ignored under
`Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Phase1JobLocations`.
The committed manifest, builder, metadata and tests are project-owned. The
frozen scene and staging remained read only.

## Validation

- Unity batch generator: PASS,
  `PHASE1_JOB_LOCATIONS_BUILD_OK renderers=306 colliders=102 cells=9`;
- focused selection/closure EditMode: PASS `1/1`;
- Bootstrap PlayMode Farm streaming, unload/reload, Farmhouse presence and
  replacement-state round trip: PASS `1/1`;
- C# compilation: PASS with no errors;
- no generated scene dependency on `ReferenceOnly`;
- full active-world validator was executed and still reports independent
  independent generated-profile debt owned by concurrent world work: material
  contract drift, changed player spawn, a multi-root global scene and lower
  base entity/collider counts. The
  supplemental layer passes its own closure validation and does not change
  those base counts.

## Limitations

This is a private Phase-1 `TemporaryDirectImport`, not remastered art. The field
remains the donor proxy rather than authored strawberry bushes. Houses, tent,
wells, farm buildings/props, materials and collision require Phase-2 production
replacement. The Farmer and combine remain project-owned/dynamic presentation
domains rather than static world imports. This remediation covers the locations
reported so far; it is not evidence that a complete map-completeness audit has
been performed. Manual in-game comparison of the restored Farm is pending.
