# Satsuma engine fastener ownership audit — 2026-09-05

Status: `BehavioralReference / OwnershipCorrectionsPending`  
Scope: fastener ownership only for the rocker cover, rocker shaft, oilpan and
gearbox. Installation order, `B`/`Drop` cadence, breakage, damage, tuning,
service actions and engine simulation are deliberately outside this report.

2026-09-05 E2a addendum: the tables below retain the pre-correction audit
snapshot. The sixteen oilpan/gearbox nut mesh references have since been
replaced with their exact reviewed bolt kinds, without changing any definition,
ownership, count, tool size, pose, stage or latch. Scoped16then0; Edit542/542,
Play63/63, zero failures/skips; manual pending. Rocker-cover and rocker-shaft
corrections remain unimplemented. The oilpan raw child-.02Z / gearboxzero
frame distinction is now explicitly source-validated, not copied as a runtime
base pose. Full result and limitations:
`SATSUMA_ENGINE_FASTENER_PRESENTATION_E2A_2026-09-05.md`.

## Executive verdict

| Mount | Frozen donor ownership | Current generated contract | Verdict |
|---|---|---|---|
| Rocker cover, stock/GT | Each alternative owns the same six physical 7 mm short bolts at exactly matching mount-local poses. | One shared mount owns 12 required 7 mm fasteners and therefore contains both alternative-specific copies. All 12 use the default nut presentation. | **Wrong ownership and wrong presentation.** The logical mount needs six positions, with an explicit migration/alias for each duplicate pair. |
| Rocker shaft | Five 8 mm long mounting bolts. Eight other descendant `BoltPM` objects are valve-adjustment screws and are not owned mounting fasteners. | 13 required fasteners: the five real 8 mm mounts plus eight leaked 6 mm valve adjusters. All 13 use the default nut presentation. | **Wrong ownership and wrong presentation.** Keep the five real stable IDs; retire the eight leaked mounting IDs without turning them into ordinary shaft bolts. |
| Oilpan | Nine owned short bolts: eight 7 mm flange bolts plus one 13 mm drain plug. The drain plug participates in the same donor `BoltCheck` array and also owns an oil side effect. | Nine required fasteners with the correct eight-plus-one size split, but all nine use the default nut presentation. | **Ownership and sizes match; presentation is wrong.** Do not discard the 13 mm entry merely because it is also a service point. |
| Gearbox | Seven owned mounting bolts: six 7 mm long bolts and one 10 mm short bolt. | Seven required fasteners with the correct size split, but all seven use the default nut presentation. | **Ownership and sizes match; presentation is wrong.** The mixed size is donor-correct. |

There are 33 reviewed donor source markers across both rocker-cover variants,
the rocker shaft, oilpan and gearbox. Every one of the 33 is rendered by one of
the donor bolt meshes; none uses the donor nut mesh. After alternative dedupe,
the four project mounts should expose 27 logical fasteners (`6 + 5 + 9 + 7`),
not the current 41 (`12 + 13 + 9 + 7`).

## Evidence boundary and method

- Frozen donor scene:
  `raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity`.
- Frozen scene SHA-256:
  `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
- Donor files were read only. No donor file, Unity asset, scene, prefab or
  generated definition was changed, and Unity was not launched for this audit.
- Byte offsets below are zero-based offsets reported by `rg -n -b` against that
  exact frozen file. Component, GameObject and Transform IDs are donor YAML
  `fileID`s.
- A marker is accepted as donor-owned only when its own `Screw` FSM is the
  mounting `Setup 2` contract and its `PartAssembled` reference points to the
  relevant loose-part root. A descendant name of `BoltPM` alone is not treated
  as ownership evidence.
- Current generated state was read from the four `MountPointDefinition` assets
  and `Satsuma_Phase1_V1a.prefab`. A read-only YAML traversal resolved every
  relevant `AssemblyFastenerInteractionTarget.fastenerPresentation` Transform
  to its actual `MeshFilter`.

## Why the current extractor over-owns descendants

`Phase1SatsumaBaselineBuilder.AddLoosePartOwnedMounts` discovers fasteners by
recursively enumerating every descendant of every accepted part alternative and
accepting every GameObject named `BoltPM`
(`Phase1SatsumaBaselineBuilder.cs:5996-6009`). It then concatenates the results,
deduplicates only by donor marker Transform ID and sorts by that ID
(`:6033-6039`). It does not check the marker's own `Screw` state, its
`PartAssembled` owner, or whether two alternative parts provide the same logical
mount-local position.

That creates two distinct defects:

1. rocker-shaft valve adjusters are descendants named `BoltPM`, so they are
   mistaken for shaft-retention fasteners despite using `Screw/Initiate` and
   having no `PartAssembled` reference;
2. stock and GT rocker covers have different donor Transform IDs, so their two
   identical six-position sets survive ID-based grouping and become 12 logical
   fasteners on one shared mount.

`BuildFastenerAssets` constructs these inferred `FastenerBuild` values without a
mesh override (`Phase1SatsumaBaselineBuilder.cs:6619-6636`). The constructor's
default is `DonorFastenerMeshSourceGuid` (`:13033-13050`), and the builder
explicitly documents that source as the legacy nut (`:147-150`). The existing
reviewed bolt-mesh dictionaries cover front/rear suspension markers, not these
engine markers.

The four current mount assets begin their generated groups at:

- `mount.satsuma.cylinder-head.rocker-cover.asset:41` — 12 IDs;
- `mount.satsuma.cylinder-head.rocker-shaft.asset:41` — 13 IDs;
- `mount.satsuma.engine-block.oilpan.asset:37` — 9 IDs;
- `mount.satsuma.engine-block.gearbox.asset:35` — 7 IDs.

## Presentation identity: bolt versus nut

| Meaning used by the reviewed builder | Frozen donor source GUID | Imported Unity GUID | Imported mesh name |
|---|---|---|---|
| Default nut | `e711c8a15b1135c4089caad19b8f56e8` | `94f895bfb846fa9aa8a476a78a3322d4` | `bolt2` |
| Short bolt | `aec6c756751308a4d830708366ad5cdb` | `8ef089521fb8cd4da9ed6869fb4bdda6` | `bolt` |
| Long bolt | `bd64aade39680ac43a380f1c62373e0b` | `3d7a237428d477495202167ad4407d68` | `bolt3` |

The read-only generated-prefab traversal found exactly 41 relevant
presentations and resolved all of them to the default-nut Unity GUID:

| Current generated group | Presentation count | Actual mesh GUID |
|---|---:|---|
| `fastener.satsuma.cylinder-head-rocker-cover` | 12 | `94f895bfb846fa9aa8a476a78a3322d4` |
| `fastener.satsuma.cylinder-head-rocker-shaft` | 13 | `94f895bfb846fa9aa8a476a78a3322d4` |
| `fastener.satsuma.engine-block-oilpan` | 9 | `94f895bfb846fa9aa8a476a78a3322d4` |
| `fastener.satsuma.engine-block-gearbox` | 7 | `94f895bfb846fa9aa8a476a78a3322d4` |

This is not merely a default inferred from source code: it is the mesh actually
serialized into every relevant presentation in the current generated prefab.

## 1. Rocker cover: two physical alternatives, one logical six-bolt mount

### Donor ownership

- Stock root: GameObject `30430`, Transform `66479`.
- Stock `BoltCheck`: component `112846`, line `7499326`, byte `206590512`;
  `BoltedNO = 0`, `BoltedYES = 2`, `Tightness = 0`,
  `db_ThisPart = Valvecover 15596`.
- GT root: GameObject `4932`, Transform `40984`.
- GT `BoltCheck`: component `105477`, line `2648854`, byte `69177589`;
  `BoltedNO = 0`, `BoltedYES = 2`, `Tightness = 0`,
  `db_ThisPart = ValvecoverGT 17824`.
- Every marker below uses `Screw/Setup 2`, points `PartAssembled` at its own
  cover root, resolves to 7 mm and uses the short-bolt mesh
  `aec6c756751308a4d830708366ad5cdb`.

| Variant | Marker Transform | `Screw` component | Donor line | Donor byte | Current generated ID |
|---|---:|---:|---:|---:|---|
| GT | `39308` | `105044` | `2361523` | `60978448` | `boltpm-1` |
| Stock | `41931` | `105751` | `2817099` | `73925729` | `boltpm-2` |
| GT | `43047` | `106083` | `3032366` | `79987173` | `boltpm-3` |
| Stock | `43107` | `106095` | `3036164` | `80093238` | `boltpm-4` |
| GT | `51693` | `108552` | `4738872` | `128491046` | `boltpm-5` |
| GT | `53156` | `108967` | `4993232` | `135663539` | `boltpm-6` |
| GT | `63635` | `111903` | `6885033` | `189172942` | `boltpm-7` |
| Stock | `65810` | `112610` | `7374793` | `203105650` | `boltpm-8` |
| Stock | `67202` | `113041` | `7622769` | `210047729` | `boltpm-9` |
| Stock | `70160` | `113881` | `8152686` | `225027426` | `boltpm-10` |
| GT | `70985` | `114087` | `8300017` | `229185057` | `boltpm-11` |
| Stock | `71935` | `114348` | `8446412` | `233311135` | `boltpm-12` |

The six stock/GT position pairs are exact in the frozen YAML, not a loose
distance-based guess:

| Current ID pair | Donor Transform pair | Exact shared mount-local position |
|---|---|---|
| `boltpm-1` / `boltpm-12` | GT `39308` / stock `71935` | `(0.122877955, -0.0513382, -0.034416866)` |
| `boltpm-3` / `boltpm-8` | GT `43047` / stock `65810` | `(0.12149751, 0.062173855, -0.03440928)` |
| `boltpm-2` / `boltpm-5` | stock `41931` / GT `51693` | `(-0.13673234, -0.0513382, -0.034412872)` |
| `boltpm-6` / `boltpm-10` | GT `53156` / stock `70160` | `(-0.13894153, 0.0621729, -0.034412798)` |
| `boltpm-7` / `boltpm-9` | GT `63635` / stock `67202` | `(-0.052220702, 0.062173855, -0.034412738)` |
| `boltpm-4` / `boltpm-11` | stock `43107` / GT `70985` | `(-0.048354626, -0.0513382, -0.034416568)` |

### Current mismatch

The shared mount accepts both
`vehicle.satsuma.part.gt-rocker-cover-gt` and
`vehicle.satsuma.part.rocker-cover`, but serializes all 12 IDs into one
`fastenerGroup`. Therefore two interaction targets occupy every physical bolt
position. The correct project-owned abstraction is six logical fasteners whose
presentation works with either accepted cover.

## 2. Rocker shaft: five mounting bolts plus eight unrelated adjusters

### Donor ownership

- Root: GameObject `21223`, Transform `57286`.
- `BoltCheck`: component `110088`, line `5712523`, byte `156049930`;
  `BoltedNO = 0`, `BoltedYES = 16`, `Tightness = 0`, all three
  `dbBoltThis` references are zero, and `db_ThisPart = RockerShaft 19072`.

The five real mounting markers all use `Screw/Setup 2`, point
`PartAssembled = rocker shaft(Clone) 21223`, resolve to 8 mm and use the
long-bolt mesh `bd64aade39680ac43a380f1c62373e0b`:

| Marker Transform | `Screw` component | Donor line | Donor byte | Current generated ID |
|---:|---:|---:|---:|---|
| `40906` | `105458` | `2635771` | `68802205` | `boltpm-2` |
| `44469` | `106458` | `3277770` | `86957732` | `boltpm-5` |
| `45373` | `106740` | `3459277` | `92068294` | `boltpm-6` |
| `56020` | `109708` | `5452844` | `148718951` | `boltpm-8` |
| `56364` | `109809` | `5511141` | `150379255` | `boltpm-9` |

The eight false positives below are valve-adjustment markers. Each uses the
different `Screw/Initiate` FSM, has `db_PartRequired = RockerShaft 19072`, has
no `PartAssembled` owner, and exposes adjustment variables such as `Alignment`,
`Min = 1` and `Max = 12`. They are legitimate future valve-tuning interaction
points, but they are not shaft-retention fasteners:

| Marker Transform | `Screw` component | Donor line | Donor byte | Leaked current ID/size |
|---:|---:|---:|---:|---|
| `37384` | `104479` | `1985658` | `50323022` | `boltpm-1`, 6 mm |
| `42106` | `105791` | `2851701` | `74926454` | `boltpm-3`, 6 mm |
| `43489` | `106185` | `3089661` | `81622819` | `boltpm-4`, 6 mm |
| `47683` | `107429` | `3979701` | `106944099` | `boltpm-7`, 6 mm |
| `62067` | `111428` | `6564990` | `180155902` | `boltpm-10`, 6 mm |
| `62501` | `111531` | `6648846` | `182556038` | `boltpm-11`, 6 mm |
| `66255` | `112762` | `7456165` | `205384007` | `boltpm-12`, 6 mm |
| `68329` | `113362` | `7821699` | `215642646` | `boltpm-13`, 6 mm |

### Current mismatch

The current generated sort order is:

`1=37384(adjuster), 2=40906(mount), 3=42106(adjuster),
4=43489(adjuster), 5=44469(mount), 6=45373(mount),
7=47683(adjuster), 8=56020(mount), 9=56364(mount),
10=62067(adjuster), 11=62501(adjuster), 12=66255(adjuster),
13=68329(adjuster)`.

The safest future correction preserves the five real mounting IDs
`boltpm-2,5,6,8,9`. The eight leaked IDs must not be remapped onto those five
bolts; their eventual state belongs to a separate valve-adjustment mechanic.

## 3. Oilpan: the 13 mm entry is an owned drain plug, not hierarchy noise

### Donor ownership

- Root: GameObject `17353`, Transform `53416`.
- `BoltCheck`: component `109026`, line `5026905`, byte `136637438`;
  `BoltedNO = 0`, `BoltedYES = 15`, `Tightness = 0`,
  `BlockedPart1 = 32707`, all three `dbBoltThis` references are zero, and
  `db_ThisPart = Oilpan 23052`.
- All nine markers use `Screw/Setup 2`, point
  `PartAssembled = oilpan(Clone) 17353`, and use the short-bolt mesh
  `aec6c756751308a4d830708366ad5cdb`.

| Marker Transform | `Screw` component | Donor line | Donor byte | Size | Current generated ID |
|---:|---:|---:|---:|---:|---|
| `40758` | `105412` | `2600893` | `67812366` | 7 mm | `boltpm-1` |
| `44950` | `106615` | `3380455` | `89831314` | 7 mm | `boltpm-2` |
| `53276` | `108997` | `5005977` | `136029102` | 13 mm | `boltpm-3` |
| `54874` | `109405` | `5262449` | `143334704` | 7 mm | `boltpm-4` |
| `56771` | `109925` | `5608260` | `153099175` | 7 mm | `boltpm-5` |
| `56810` | `109934` | `5624150` | `153547317` | 7 mm | `boltpm-6` |
| `58044` | `110308` | `5876663` | `160691702` | 7 mm | `boltpm-7` |
| `58670` | `110497` | `5971589` | `163371879` | 7 mm | `boltpm-8` |
| `61811` | `111370` | `6525689` | `179049943` | 7 mm | `boltpm-9` |

Transform `53276` is the drain plug. Its marker-local position is
`(0.10059955, 0.0053000264, -0.15680002)`, its scale is
`(1.3, 1.2999998, 0.8)`, and its `Screw 108997` contains an explicit
`Oil = GameObject 12380` reference. The nearby inactive oil presentation is
GameObject `12380`, Transform `48426`, at
`(0.10040069, 0.0049000196, -0.14200008)`.

This service side effect does not make `53276` foreign to the oilpan mount:
the same FSM also points `PartAssembled` to root `17353` and writes into the
oilpan `BoltCheck` array. The current project has not yet reproduced the drain
side effect, but the ownership evidence says to retain the 13 mm definition
until that service behavior is implemented deliberately.

## 4. Gearbox: the 10 mm bolt is intentional

### Donor ownership

- Root: GameObject `1533`, Transform `37593`.
- `BoltCheck`: component `104546`, line `2027758`, byte `51520580`;
  `BoltedNO = 0`, `BoltedYES = 36`, `Tightness = 0`,
  `BlockedPart1 = 34310`, and `db_ThisPart = Gearbox 31625`.
- Every marker is under `gearbox(Clone)/Bolts`, uses `Screw/Setup 2`, and points
  `PartAssembled = gearbox(Clone) 1533`.

| Marker Transform | `Screw` component | Donor line | Donor byte | Size | Donor mesh | Current generated ID |
|---:|---:|---:|---:|---:|---|---|
| `36907` | `104337` | `1902185` | `47992057` | 7 mm | long bolt | `boltpm-1` |
| `38331` | `104763` | `2180793` | `55849095` | 7 mm | long bolt | `boltpm-2` |
| `42660` | `105955` | `2946750` | `77607864` | 7 mm | long bolt | `boltpm-3` |
| `44837` | `106563` | `3349497` | `88958055` | 7 mm | long bolt | `boltpm-4` |
| `51824` | `108590` | `4764298` | `129218526` | 10 mm | short bolt | `boltpm-5` |
| `67087` | `113005` | `7594072` | `209254692` | 7 mm | long bolt | `boltpm-6` |
| `68100` | `113315` | `7796069` | `214917124` | 7 mm | long bolt | `boltpm-7` |

The current `boltpm-5 = 10 mm` definition is therefore correct. It should gain
the reviewed short-bolt presentation while the other six gain the long-bolt
presentation; it should not be normalized to the surrounding 7 mm size.

## Smallest safe future correction

This audit supports a bounded engine-fastener ownership refresh after the
current build/cockpit work returns the Unity window. It does not justify a broad
assembly rewrite.

1. Add an engine-specific reviewed ownership predicate/table. A descendant
   marker is eligible only when its own evidence is `Screw/Setup 2` and
   `PartAssembled` equals the accepted alternative's donor root. Missing or
   ambiguous evidence must fail closed. This reduces the rocker shaft from 13
   to the five real IDs `2,5,6,8,9`.
2. For a mount with alternative accepted parts, group reviewed markers by
   mount-local pose plus wrench size and presentation identity, and require all
   alternatives to provide the same set. The six exact rocker-cover pairs above
   then become six logical definitions rather than 12.
3. Supply the reviewed donor mesh GUID on each retained `FastenerBuild` instead
   of falling through to the default nut:
   - rocker cover: six short bolts;
   - rocker shaft: five long bolts;
   - oilpan: nine short bolts;
   - gearbox: six long 7 mm bolts and one short 10 mm bolt.
4. Preserve gearbox and oilpan counts, sizes and current stable IDs. Do not split
   the oil drain plug from the aggregate until the service controller has an
   explicit compatible contract.
5. Treat stable-ID and save compatibility as part of the refresh. Preserve the
   user's accepted save/load baseline; no permission to discard existing
   fastener state or postpone migration follows from this audit:
   - rocker cover needs six explicit pair aliases and a defined stage-merge
     rule; silently deleting either member can discard existing fastener state;
   - rocker shaft should retain real IDs `2,5,6,8,9`; leaked adjuster IDs are
     retired/reserved, not renumbered onto physical mounting bolts.
6. Use a scoped, fail-closed refresh with exact old/new-shape preflight. A repeat
   invocation must report zero changes. Hash-check all unrelated mount
   definitions, prefab content, manifest and build settings.

## Required validation for that future correction

Automated coverage should prove:

- rocker cover generates six 7 mm short-bolt definitions and each exact
  stock/GT pose pair maps to one logical state;
- rocker shaft generates only five 8 mm long-bolt definitions and none of the
  eight `Screw/Initiate` markers becomes a mount fastener;
- oilpan remains `8 x 7 mm + 1 x 13 mm`, with the 13 mm marker retained;
- gearbox remains `6 x 7 mm + 1 x 10 mm`, with the correct long/short mesh split;
- none of the 27 logical presentations resolves to the default nut mesh;
- alias/retirement behavior is deterministic and serialized state remains
  recoverable;
- scoped refresh is idempotent and unrelated generated content is byte-identical.

Manual verification should install stock and GT rocker covers separately and
observe six non-overlapping bolt targets each time, then inspect five shaft
bolts, nine oilpan bolts including the 13 mm drain plug, and seven gearbox bolts.
The drain action itself remains a separate service-mechanics acceptance item.

## Audit result and limitations

- Read-only donor and generated YAML inspection: **completed**.
- Unity import/build/EditMode/PlayMode execution for this audit: **not run**.
- Runtime or generated asset correction: **not implemented**.
- Save migration/alias behavior: **specified as a requirement, not implemented**.
- Donor `BlockedPart`, installation/removal sequence, bolt cadence and engine
  simulation were observed only where needed to disambiguate ownership; their
  parity belongs to the separate assembly-rule audit.
