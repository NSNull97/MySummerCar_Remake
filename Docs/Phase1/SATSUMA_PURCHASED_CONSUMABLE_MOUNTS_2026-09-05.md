# Purchased Satsuma consumables — additive assembly packet

Status: generated and automatically checked in the integrated night packet.
The seven sockets and consumable rules are included in the executed1247/1247
EditMode pass (`Logs/codex-night-editmode-wiring-selection-final-20260906.xml`).
Actual purchased-plug installation/start/idle/rev/stop, physical bulb handoff and
two old-cube replacement tests passed again in the final108/108 PlayMode packet
(`Logs/codex-night-playmode-service-cabin-final-20260906.xml`). All six reviewed
held-spool wiring circuits also pass; earlier contact failures remain historical.
Manual acceptance remains pending. The earlier source-ready notes below are
implementation history; the main night report owns subsequent runner results.
No user save or donor file was edited by this packet.

## Scope and compatibility

- Three new reusable PartDefinitions: `vehicle.satsuma.part.spark-plug`, `vehicle.satsuma.part.alternator-belt`, `vehicle.satsuma.part.light-bulb`.
- Seven new mount IDs: `mount.satsuma.cylinder-head.spark-plug-1` through `-4`, `mount.satsuma.engine-block.alternator-belt`, `mount.satsuma.headlight-left.light-bulb`, `mount.satsuma.headlight-right.light-bulb`.
- The stock `vehicle.satsuma.part.oilfilter0` definition and its existing socket are reused. The root/save packet owns dynamic item registration, its compatible save extension, item materialization and stock-filter adjustment reuse.
- Base authored Parts remain unchanged. A purchased wrapper keeps its original item StableEntityId, WorldItemInstance, Rigidbody and physical collider. No duplicate starting purchases, hidden replacement identities or separate bolt meshes are created.
- Four plug-thread FastenerDefinitions, one per socket: `fastener.satsuma.cylinder-head-spark-plug-N.thread`. The plug itself is the fastened object; these are not additional nuts. Empty sockets have no active thread-interaction collider.
- Exact newly owned sockets are appended to the enclosing head, engine and headlight mount `RemovalRetainedChildMountIds`. The established opt-in compound-removal contract retains an installed plug/bulb/belt when its owning part is removed; no global child-removal exception or reverse structural edge is added.
- The authoring helper accepts a complete absent seven-socket packet or a complete reviewed current packet, rejecting partial socket migrations. Existing old mount IDs, fastener stages, tool definitions and saves are not renamed.

## Read-only evidence

Frozen `GAME.unity` SHA256: `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`. Existing assembly audit rows63–66 record the four plug assemblies; rows4–6 record belt and headlight bulb assemblies. Bounded object/FSM extraction was used, not a full in-memory GAME load.

Source prefabs under the frozen AssetRipper `Assets/GameObject` staging tree:

| File | SHA256 |
| --- | --- |
| `sparkplug0.prefab` | `EE6CFBE2E65A79AD515BBC1D20863503A75773D2B7F7016C656E8716CD5CCE4C` |
| `alternatorbelt0.prefab` | `ED14118FD00E542AA0E68655745BE5C428F5F9D717299A02F9833EB50E2531A0` |
| `lightbulb0.prefab` | `C30AF154F4A123FBF0B2BB366CD574698F26A3362685304E67A48C613B5F9B62` |

### Plugs

- Pivots68888/36778/55779/53182 under head41296: X `.122430325/.035472274/-.05150169/-.13845968`, Y `.07151509`, Z `.007053325`, quaternion `(-.38268396,0,0,.9238794)`. Assembly110723 sets the bought plug parent and resets local position/rotation. The three other plug rows use their respective pivots.
- `sparkplug0.prefab` Screw starts at Setup2 and contains stages0–8. Screw2/Unscrew2 add ±1 and reject beyond8/below0. Wait3/Wait4 add ±1 to Use.Tightness. Stages apply Self Z=`-.0025*stage`, EulerZ=`45*stage`; the wrapper remains on the socket in the remake while only presentation moves.
- Removal.Requirements2 reads Use.Tightness; equal0 and less0 route to REMOVE, greater0 routes LOOP. Existing compatibility group ON1/OFF0 mirrors this removal boundary; full safety remains8. This is an explicit project mapping, not a claim of a separate donor plug Bolted FSM.
- Frozen PickUp110228 at byte158676276, states Sparkplug/Socket plug, set global ToolWrenchSize to `.55` (`cdcc0c3f`). The semantic remake tool is existing `SparkPlugWrench/None`, not an arbitrary millimetre spanner.
- Source Rigidbody mass `.2 kg`; source loose collider `.025 × .025 × .08639 m`. The new thread hitbox is query-only and includes the original plug body's full20mm presentation travel; it does not add another physical collider to the purchased wrapper.

### Belt

- GAME Assembly110896 at byte170683955: Requirements reads crankwheel, water-pump pulley and alternator Installed. Check rot reads alternator BoltCheck.Rotation; only `<4` routes ASSEMBLE, equality/greater route LOOP.
- Original belt Removal.Requirements reads alternator Data.Bolted: false permits REMOVE immediately; true enters Check rot and permits removal only below4. Therefore the removal rule is `!alternatorBolted || alternatorSetting <4`, not an unconditional Bolted blocker.
- Belt pivot39226 under block66906 is `(.24000037,-.0199996,.010000048)`, identity. Trigger60106 is `(.24,-.0199996,.009999989)`. The bought wrapper is reparented/reset to that pivot; the donor then switches to a separate installed rig. Source mass is `.5 kg`.
- Installed SkinnedMeshRenderer101788 at byte41458563 uses mesh `7dab2df4b56946b4c9f8892f57e8e978` (`Mesh/motor_fanbelt_001.asset`) and material `1dc990c84250ce844b24b6439ec55445` (`Material/fanbelt.mat`), bones42547/70022, root42547. Its authored transforms and original bind poses/weights are retained by the presentation-only helper; the root task owns the reviewed sanitized mesh/material import.
- Bone70022 is ScaleBone. Jumping110290 Reset applies X/Z=`Scale` (initial1.1), Y=1. This assembly packet uses that parked reset pose. Running-RPM UV motion, random vibration, belt squeal and wear are not claimed as ported by this presenter.
- The visual callback creates the installed rig under the existing item PresentationRoot. Only loose renderers and installed-rig visibility switch; item root identity, mass, gravity and physical shape are untouched. Repeat binding must reuse the same rig.

### Bulbs

- Pivots47607/40713 under headlights45890/63443: `(-.0021003543,-.0084,.0023996313)`, identity. Trigger64030/50910 is `(0,-.0273,0)`, so interaction anchor and installed pose remain separate.
- `lightbulb0.prefab` has Use and Removal, no Screw FSM or fastener stage. Removal Mouse over directly accepts its button; no invented bulb nut is authored. Source mass `.2 kg`.

## Implementation ownership and hooks

- `Phase1SatsumaConsumableMountAuthoring.GetOrCreateDefinitions(assembly, generatedRoot)` persists three definitions, seven mounts, four thread definitions, the existing-type spark-plug wrench, a four-record VehicleItemPartCatalog and the reviewed installed-belt presentation prefab.
- `ApplyToInstance(assembly, definitions)` appends the seven mounts, binds the existing item bridge, binds the aggregate presentation callback, registers the tool and retains the seven exact children. Call after existing engine/compound authoring to preserve its established reviewed contracts.
- `RefreshConsumableMountsBatch` is scoped and checks repeat application returns0. Run root-owned `Phase1SatsumaInstalledBeltAssets.ImportReviewedAssets` first. No full baseline rebuild is required.
- `IAssemblyItemPartPresentationBinding.BindPartPresentation(part, visualRoot)` is the item bridge callback. Spark-plug visual stages are derived from the existing mount fastener state and require no extra save DTO.
- `SatsumaConsumableAssemblyRules.EvaluateInstall/Removal(part,mount,graph)` are narrowly keyed to the belt's exact part/mount IDs. Query and final controller hooks are integrated by the save/root owner; unrelated mounts return success unchanged.

## Validation and remaining measurements

Added fixture `MSC.Tests.EditMode.LegacyImport.SatsumaConsumableMountTests`:14 cases covering additive/idempotent authoring, partial rejection, four independently bound plugs and eight stages, wrong-tool rejection, nested head removal, strict belt boundary3.5/4/4.5, all-part prerequisites, the two removal branches, visual-switch identity/physics preservation, rig skin validation and real imported172-vertex skin bounds.

Executed by this agent: bounded donor reads/hashes and `git diff --check` (clean for this packet). No Unity compile, refresh or test run is claimed here.

Explicit measurement required in Unity: the skinned renderer's original AABB is in its bone frame. A generic installed-item interaction-box generator must not treat that AABB as an ordinary MeshRenderer-local box and create a multi-metre target. The root/save owner was notified to verify/use a one-time baked skin envelope. This is not a reason to enlarge or disable physical collision.

Classifications: measured definitions/rules are `ConfigurationTransferred` / `BehavioralReference` with project-owned `Reimplemented` runtime. The reviewed belt rig/mesh/material are private Phase1 `TemporaryDirectImport`, not production art. Running-engine belt presentation remains a separate explicitly unverified consumer, not hidden completion of this assembly packet.
