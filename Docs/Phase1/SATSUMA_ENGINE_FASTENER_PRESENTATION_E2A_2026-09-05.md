# Engine E2a — oilpan and gearbox bolt presentation

Status: **ImplementedUnverified / AutomatedPassedManualPending**.
Night cutoff: 2026-09-05 03:00 UTC. Manual acceptance pending.

## Evidence and bounded decision

Root fully read `SATSUMA_ENGINE_FASTENER_OWNERSHIP_AUDIT_2026-09-05.md` and
the current two mount definitions. Oilpan nine and gearbox seven logical
fasteners have donor-correct counts, ownership and tool-size splits; their
current presentation incorrectly uses the default nut mesh. Correct only these
sixteen existing MeshFilter references. Do not change transforms, colliders,
materials, fastener state, tool sizes, thresholds, grouping, IDs or save schema.

Frozen GAME SHA256:
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Oilpan Screw/Setup2 ownership: root17353, BoltCheck109026 @136637438.
Gearbox Screw/Setup2 ownership: root1533, BoltCheck104546 @51520580.

| Existing stable fastener IDs | Donor markers in the same order | Expected mesh |
| --- | --- | --- |
| fastener.satsuma.engine-block-oilpan.boltpm-1..9 | 40758,44950,53276,54874,56771,56810,58044,58670,61811 | all short |
| fastener.satsuma.engine-block-gearbox.boltpm-1..7 | 36907,38331,42660,44837,51824,67087,68100 | long,long,long,long,short,long,long |

Short source GUID `aec6c756751308a4d830708366ad5cdb`, existing runtime mesh
Unity GUID `8ef089521fb8cd4da9ed6869fb4bdda6`. Long source GUID
`bd64aade39680ac43a380f1c62373e0b`, Unity GUID
`3d7a237428d477495202167ad4407d68`. Current nut source GUID
`e711c8a15b1135c4089caad19b8f56e8`, Unity GUID
`94f895bfb846fa9aa8a476a78a3322d4`.

Oilpan #3 remains the existing13mm drain plug, gearbox #5 remains10mm; all
other entries remain7mm. The donor drain side effect is not implemented by
this visual packet. Current oilpan/gearbox ON1 thresholds are known separate
debt (donor15/36), not silently changed here. Rocker cover duplicate aliases
and rocker shaft valve-adjuster separation are explicitly excluded.

## Plan recorded before implementation

1. Add an Editor-only exact stable-ID mapping and fail-closed scoped helper.
   Validate all sixteen targets and serialized presentation ownership before
   mutating any mesh. Only the expected old nut or each target's correct new
   mesh is accepted; no wrong bolt kind or foreign target is silently repaired.
2. Reuse already imported sanitized short/long meshes. Preserve each existing
   material, marker/presentation TRS, base pose, travel scale and collider.
   The helper must not call Configure/initialize the runtime assembly graph.
3. Call the same mapping from full-builder composition for future repeatability;
   its existing source validator also checks each marker's sole MeshFilter,
   material and child frame before changing only FastenerBuild.MeshSourceGuid.
   Use one shared exact mapping, not two drift-prone tables. Do not run full
   generation now. Scoped refresh backs up the canonical prefab,
   changes16 meshes once and0 on repeat; reject missing/duplicate binding drift.
   A mixture of the old nut and each target's own reviewed new mesh is accepted
   for safe recovery; a foreign or wrong kind of bolt is rejected.
4. Add donor/generated mapping tests, idempotence, fail-before-write and exact
   non-mesh payload/fastener-definition preservation assertions. Extend the
   existing unreviewed-nut regression with only these16 newly reviewed IDs.
5. Capture protected hashes before/after. Require all117 mount definitions,
   all fastener definitions, manifest/build settings and all unrelated prefab
   YAML records byte-identical. Run relevant Edit/Play regressions and record
   failures honestly; no new save migration is needed for mesh references.

## Results

Pre-edit protection captured404 files:117 mount definitions,280 fastener
definitions, canonical prefab, full manifest, build settings, three existing
fastener meshes and their shared material. Prefab starts at post-C2 SHA256
`EFC2DEBCDB4346EF3356D082F5990A4C2FE780855ACEB6D5F10ACFDA656C39AD`.
All six user-save/backup hashes are also retained for read-only comparison.
Root independently traversed the current prefab's serialized target ->
presentation -> GameObject -> MeshFilter references: exactly16 unique expected
filters, all using the nut GUID. The actual frozen GAME hash was rechecked and
still matches the lock. Bounded reads of the two raw BoltCheck components also
confirm the separate ON15/ON36 debt; E2a intentionally preserves current ON1.

### Pre-run review correction: donor child frame

Independent source review found a real exception before any Unity refresh:
all nine oilpan renderer children have local position `(0,0,-0.02)`, whereas
all seven gearbox renderer children have zero local position. Both sets use
identity rotation and unit child scale. The generic front/rear source validator
required zero position and would therefore reject the new oilpan full-builder
hook. The first draft of the donor test also incorrectly equated the renderer
transform with the marker itself; it was corrected before execution.

Approved bounded design adjustment: include the expected donor child position
in the same reviewed table and pass it to an optional source-validator argument
whose default remains zero. Other reviewed fastener scopes are not relaxed.
The current project marker/presentation/base poses remain unchanged: E2a only
changes mesh identity. Raw serialized donor child position is not automatically
equivalent to an initialized runtime stage-zero pose. Donor initialization and
staged rest-position parity need a separate audit; do not claim full pose or
tightening-animation parity from this mesh-only correction.

### Executed scoped refresh

Unity `6000.3.11f1`, one hidden `-batchmode -nographics` process at a time:
`Phase1SatsumaEngineFastenerPresentation.RefreshEngineFastenerPresentationBatch`.
First run PID9248 reports changedMeshFilters16 and return code0; repeat PID32764
reports changedMeshFilters0 and exit0. Logs:
`Logs/codex-engine-e2a-refresh-01.log` and `...refresh-02.log`.
No full builder or world/car regeneration was run.

Backup: `Logs/codex-engine-fasteners-before-20260905-023020-5589002.prefab`.
Post-refresh prefab SHA256:
`6E89F47CAAD045082F76EFCC863953ACFA25FF0F2D1F61FD0A1D298F24839A0D`.
The repeat preserves that exact hash. Independent serialized-record comparison
finds7382 records before and after,0 added/removed, and exactly16 changed
MeshFilter records. Each difference is only the old nut GUID replaced by the
correct short/long GUID; all other bytes of those records and all other records
are identical. Only the prefab changed among404 protected files;117 mount
definitions,280 fastener definitions, manifest/build settings, meshes and shared
material remain byte-identical.

Implementation files: new Editor-only
`Phase1SatsumaEngineFastenerPresentation.cs` and its meta; bounded source and
post-composition hooks in `Phase1SatsumaBaselineBuilder.cs`. Test files: new
`SatsumaEngineFastenerPresentationTests.cs` and meta, plus only the16 reviewed
exceptions/count199->183 in `SatsumaFrontFastenerMeshTests.cs`. All paths are
inside the existing LegacyImport Editor or LegacyImport EditMode modules; no
new dependency, public runtime API, stable ID, save DTO or migration is added.
Both agent offline Editor compile passes report exit0; actual scoped Unity
compile/refresh is also successful. C2 and accepted physics remain frozen.

### Executed regression checks

- EditMode PID29760: **542/542**, failed0/skipped0, duration113.4785957s,
  exit0. Includes the12 new E2a cases,6 front-mesh cases,6 rear-mesh cases,
  and the prior518-case cockpit/assembly/interaction/save regression filter.
  `Logs/codex-engine-e2a-edit.xml`, SHA256
  `7D1CCC6B52213FEEADB2A865C84640B2420B0B12841A933984D555F7B44DD8F7`.
- Fixture PlayMode PID17368: **63/63**, failed0/skipped0,
  duration80.2328026s, exit0. Existing installation/collapse, wheel cadence,
  installed-part physics, handbrake, steering/spawn, rear droop/compression/
  drive-lifecycle, assembly and ignition fixtures were run without changes.
  `Logs/codex-engine-e2a-play.xml`, SHA256
  `6EEAE74EE4F453F5C00523078E922F20F99EB0E065B43623E6DF4DCEED8927FC`.
- Final404 protected hashes match the first refresh exactly. All six existing
  user save/backup hashes remain unchanged, with no added or removed records.
- `git diff --check` on the touched tracked source/test/report files passed.
  No Unity process remains. No player build or new native Bootstrap Play test
  was needed or run for this mesh-only packet; do not reuse C2's prior native
  Play result as if it was rerun here.

Both executed E2a Unity suites passed on their first run. The pre-run donor
frame mistakes and correction above are retained, not disguised as already
validated source assumptions. A preliminary shell hash request exceeded the
Windows command-line length limit and did not launch; the actual successful
404-file comparison enumerated the two exact definition directories plus the
seven explicit remaining protected files.

### Manual acceptance and remaining boundaries

No further import/menu action is needed in this local project: the scoped
prefab change is applied. In a fresh Editor test or a newly built player:

1. Inspect the oilpan's eight7mm bolts and13mm drain plug: all must have short
   bolt geometry instead of nuts. Do not infer an implemented oil-drain service.
2. Inspect the gearbox: six7mm long bolts and one10mm short bolt at existing
   boltpm-5. Check interaction, outline and visibility through installation,
   partial tightening, full tightening and removal.
3. Watch seating and stage travel specifically; E2a preserves their preceding
   implementation and does not claim donor-correct rest frames or animation.

The delivered player024640 predates E2a and cockpit C1a/C1b/C2; it still uses
native16, whereas the current project uses native17. Do not test these changes
with that old build or overwrite upgraded slots from it.

The follow-up read-only stage/frame audit has now completed for two
representative screws: raw oilpan-.02 is a pre-init serialized pose, not a
different stage-zero base. A separate effective-travel scale difference
(approximately20mm project versus14mm donor at stage8 for the.7 examples) is
documented, including the marker/child rotation-owner review still needed.
No correction was mixed into E2a. Next bounded step/specification:
`SATSUMA_ENGINE_FASTENER_STAGE_FRAME_AUDIT_2026-09-05.md`.
Oilpan/gearbox latch15/36, drain
service, rocker-cover duplicate retirement and rocker-shaft valve adjusters
remain pending. No full-engine completion or manual acceptance is claimed.
