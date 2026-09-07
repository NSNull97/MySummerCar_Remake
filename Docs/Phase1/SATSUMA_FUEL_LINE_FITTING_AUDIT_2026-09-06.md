# Fixed fuel line / tank-end 12 mm fitting

## Status and conclusion

The original read-only `BehavioralReference` / `MountPointSource` audit below
was followed by an explicitly approved bounded source implementation on6September.
The fixed fitting now has project-owned fastening/optional save code and a scoped
authorer. Root subsequently generated it and executed all31 dedicated cases
successfully; see the integrated result below. No native save or donor file was
changed. The original research author launched no Unity; root owns these runs.
**Fuel leakage and inspection remain unimplemented.** This is fastening/save
support, not complete hard-line or fluid parity and not gameplay verification.

### Integrated result, 6 September

Root's night refresh at04:42 passed changed11/repeat0: three fitting bindings
plus eight independent door visibility bindings. Graph126/124/302 is unchanged;
log `Logs/codex-night-refresh-fuel-door-20260906.log`, backup
`Logs/satsuma-startable-night-before-20260905-234237-2886720`.
`Logs/codex-night-editmode-fuel-door-final-20260906.xml` contains all12 typed
fitting cases,18 optional-save cases and one night-authorer case, all passed.
That broad run had one unrelated stale door-outline assertion, documented in
the main night report; it is not reported as a complete suite pass here.
Manual access/outline, visual comparison and actual leakage remain unverified.

The missing 12 mm nut is **one connection on the permanently installed hard
fuel line**, whose visible fitting happens to be parented to the removable
tank. It is not an eighth tank mounting bolt. Its separate fastening state
controls leakage and inspection, not the tank's removal gate and not a binary
engine-start permission. The accepted seven Wrench11 tank bolts remain correct.

Locked source: external staged
`raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity`,
SHA-256 `c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Line numbers below refer to that frozen YAML. Only bounded object/FSM blocks
and an exact-reference streaming scan were read; no donor state machine code
is proposed for copying.

## Approved bounded implementation — source checkpoint

- `SatsumaFuelLineConnection` on the vehicle aggregate owns the stable connection
  key `connection.satsuma.fuel-line-tank`, stage0..8 and sticky ON8/OFF0 latch.
  Explicit tank/assembly references control availability, not persistence.
  Tank detach/reinstall never resets the fitting or adds a removal blocker.
- `SatsumaFuelLineFastenerInteractionTarget` uses the existing held Wrench12 /
  directional-scroll / outline interfaces. The marker is unit scale, with the
  measured tank-local rest pose and a `(1.2,1.2,.8)` mesh child. Child movement is
  `.002m` and45degrees per stage: full16mm. Measured source sphere radius.012
  under maximum scale1.2 becomes a project unit-marker sphere radius.0144.
- `VehicleSaveRecordDto` gains only optional `hasFuelLineConnection` and
  schema1 `fuelLineConnection:{stage,isBolted}`. Old absence defaults0/false;
  endpoint contradictions or unsupported versions fail before mutation. A
  present payload without its aggregate binding is rejected. Capture allocates
  independent DTOs; existing JSON deferred storage retains this extension.
  Vehicle/assembly/native schema versions and the126/124/302 generic graph are
  unchanged. No eighth tank bolt, fake removable part or new generic subsystem.
- `Phase1SatsumaFuelLineConnectionAuthoring` reuses the measured source nut mesh
  and material as `TemporaryDirectImport`; replacement key
  `legacy.vehicle.satsuma.fuel-line-fitting`. Full source/pose/ownership preflight
  precedes creation, only the exact source duplicate is hidden, and repeat must
  return0. Existing seven tank bolts and their definitions are never written.
  NightBatch invokes it after Stock21; the existing FullBuilder NightBatch hook
  already covers full authoring without an additional pass.
- Isolated execute method:
  `MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaFuelLineConnectionAuthoring.RefreshFuelLineConnectionBatch`.
  It backs up the existing prefab underLogs, requires exact complete302 topology,
  applies/repeats, saves only that prefab and never writes a native save.

Source validation: fresh private csc compilation of Audio.Runtime,
Vehicle.Assembly, Vehicle.Runtime, LegacyImport.Editor and the fitting/save
fixtures succeeded. Only the five pre-existing rear-suspension unused-field
warnings appeared in Vehicle.Assembly; no new fitting warnings. This does not
claim an executed Unity/NUnit or visual result. Test fixtures cover18 save cases
plus typed tool/latch/visibility/tank-removal,9 world poses, source corruption,
prefab serialization and canonical302/repeat0 invariants. Root runs Unity next.

No change was made to fuel consumption, `SatsumaEngineAssemblyReadiness`, wiring,
the unsaved fluid bypass or startup permissions. A loose fitting currently has
**no simulated leak consequence**; UI names identify the connection without
claiming fuel is flowing or leak-free. Future leak calibration remains required.

## Ownership and one-stage-array contract

| Object | Evidence | Meaning |
| --- | --- | --- |
| Hard line GO22525 / Transform58580 | `fuel line(xxxxx)`, parent MiscParts70215; components Transform, MeshFilter86765, Renderer78361, ArrayList110472, BoltCheck110473 | Fixed chassis presentation; no Rigidbody, collider, Assembly or Removal FSM on this object |
| Database GO24327 / Data110964 | `ThisPart=22525`; initial `Installed=true`, `Bolted=false`, `Damaged=false` | Fixed line is already installed, not a missing pickup item |
| ArrayList110472 | one integer entry, initial0 | Exactly one independently saved fitting stage |
| Nut marker45288 / GO9234 / Screw106716 | `PartAssembled=22525` | Changes the hard-line BoltCheck, not FuelTank12246 |
| Tank bolt parent45132 | children include seven Wrench11 bolts plus marker45288 | Spatial parenting is not fastener-state ownership |

Screw106716 starts at byte91595235 / L3442562. `Get scroll 2` clamps Stage
to0..8 and writes the stage into GO22525's array. `Wait 3` adds+1 to
GO22525/BoltCheck.Tightness; `Wait 4` adds-1. The child selected during
`Save data 2` supplies array index0. Its stale embedded `ThisBolt` literal
must not be mistaken for the runtime-selected child.

BoltCheck110473 starts at byte163132880 / L5962983:

- `Bolts OFF`: SetFsmBool(Data24327.Bolted=false), compare Tightness to the
  **variable** BoltedYES=8; equal/greater transitions ON. Its embedded float
  literal24 is inactive because useVariable=1.
- `Bolts ON`: SetFsmBool(Bolted=true), compare to BoltedNO=0;
  equal/less transitions OFF.
- Thus normal live hysteresis is `0=false`, `8=true`, and stages1..7 preserve
  which side was last reached. Replacing the latch with `stage==8` would change
  the donor's response to partially loosening a previously sealed fitting.
- The hard line independently saves aggregate Tightness, its one-entry array
  and database Bolted. Startup/save-order fidelity for an intermediate stage
  was not live-tested; preserve explicit state rather than inventing it.

## Geometry, tool and visibility

Marker45288 (L703797) is directly below identity Bolts45132, itself below
tank Transform48576. Therefore these are also **tank-local** coordinates:

```text
position   = (0.027247787, 0.30196226, -0.07855269) metres
rotation   = (-0.0000019548993, 0.70710653, 0.70710707, 0.0000025231745)
scale      = (1.2, 1.2, 0.8)
visible47938 local position=(0,0,0), rotation=identity, scale=(1,1,1)
mesh       = e711c8a15b1135c4089caad19b8f56e8 (nut; MeshFilter84594)
```

Original tool selection compares marker scale against ToolWrenchSize
(player Check tool105041; Spanner pickup110228 reads the selected tool scale).
The X/Y1.2 marker denotes the12mm wrench, not a screwdriver. Do not infer size
from the Screw FSM's unused initial BoltSize=0. SphereCollider99370 has
radius.012, trigger=true, initially disabled; the original repair-mode events
enable/disable it. Accepted remake target sizing can stay on its existing
tool-ray convention rather than duplicating an old input FSM.

The collider does **not** follow the moving mesh: GO9234 at L149095 explicitly
owns Transform45288, Screw106716 and SphereCollider99370. Collider99370 at
L1615524 points to that same GO9234, center `(0,0,0)`. Visible child Transform47938
at L737922 belongs to GO11892 and is parented to45288. The stage actions target
the `ThisBolt` child selected by `Save data2/GetChild`, not the marker. Therefore
the project sphere stays on the unit marker with zero center at every stage;
the9-pose regression asserts this ownership and that the mesh child has no collider.

All nine Screw stage states have SetPosition+SetRotation enabled (`0101`),
local space1: `z=-0.0025*stage`, `zAngle=45*stage`. With a unit project marker
and scale moved to its visible child, travel multiplier is **0.8**, i.e.
16mm total at stage8, not24mm inferred from wrench size. The mounting frame
must remain fixed and the stage rotation/translation applied relative to it.

Tank Bolts GO13938 starts inactive. Tank Assembly107133 recursively activates
the installed Part; Removal107695 explicitly deactivates Bolts13938. The new
fitting renderer/target should therefore be available only while that exact
tank part is installed; the line's persistent stage is not owned by this
visibility condition. No evidence requires pickup or removal of the hard line.

## Removal, fuel, wiring and other consumers

Tank Removal107695 at byte112495540 has only two `Requirements` reads:
FuelTankPipe30714.Installed and FuelTank12246.Bolted. BoolNoneTrue permits
removal. **FuelLine24327 is not a requirement.** `Remove part` resets the
tank's own array and BoltCheck, not GO22525's; do not clear line stage merely
because the tank became loose. It also hides the tank's Bolts parent.

Fuel GO18546 / FuelLine109337, byte142457403:

- `Fuel line` L5233242 reads FuelLine24327.Bolted; true continues, false enters
  `Leak` L5233347.
- `Leak` activates GO7627 and subtracts0.025 from FuelTank12246.FuelLevel,
  once on that state entry (`everyFrame=false`, `perSecond=false`), then still
  proceeds to `Fuel strainer`. The full loop cadence is not calibrated here;
  **0.025 is not evidence for 0.025 litres/second**.
- No fuel-sender wire or other wiring endpoint is read by this branch. This
  mechanical connection must not become a required electrical wire or an
  unconditional `CanRun=false` gate. Actual loss of fuel may subsequently
  stop the engine through the established quantity gate.

Inspect104926 `Fuel line` reads Data24327.Bolted: true PASS to Fuel tank,
false FAIL to State11 (L2288465 onward). Inspection is future scope; record
the condition, do not implement a new inspection system in this fitting patch.

Exact-reference scan also found RandomBolt/Logic112377, `State 7`, selecting
GO22525, followed by `Loosen part`: array index0 is reduced one stage and
aggregate Tightness reduced1. That cross-system event is not implemented by
this audit; it is another reason not to conflate the fitting with tank bolts.
No wiring FSM reference to GO22525/24327 was found in the frozen scene scan.

## Historical project checkpoint and bounded proposal

At the initial audit checkpoint the successful night graph was126 fixed parts
/124 mounts /298 generic fasteners. The subsequent separately approved four
headlight mounting bolts make the latest canonical roster **126/124/302**;
they do not implement this fuel-line fitting. There is no `vehicle.satsuma.part.fuel-line`,
fuel-line mount or fitting state in that roster. The standalone original hard
line mesh3c72f0c0cd2dc874d99e8345f28447bd was not found among the generated
RuntimeBaseline assets by its source key; whether another merged presentation
already depicts that tube was not visually verified. Do not promise it exists.

Relevant existing seams:

- `Phase1SatsumaStockMountFastenerAuthoring.cs:41`: seven tank definitions;
  do not add this nut to their aggregate or `RequiredForRemoval` logic.
- `SatsumaEngineAssemblyReadiness.cs:64`: deliberately distinguishes leakage
  from startup veto; keep that distinction.
- `SatsumaElectricalTerminalFastenerInteractionTarget.cs:10`: precedent for
  a specialized held-tool/directional-scroll/outline target outside generic
  removable-part graph fastening. Reuse its interaction interfaces, **not**
  its electrical owner, hardcoded8mm constant or13-degree terminal travel.
- `VehiclePersistence.cs:59`, `:83`: existing optional vehicle-level state and
  explicit presence-bit pattern; `Save/Integration/VehicleSaveParticipant.cs`
  must be included in nested-copy and validation review.

The following proposal was subsequently approved and implemented as source above,
except the explicitly deferred leakage-consumption/presentation step:

1. One project-owned fixed fuel-line connection state on the existing vehicle
   aggregate, explicit reference to the existing installed tank, stage0..8 and
   its independent Bolted latch. No fake removable PartInstance/MountPoint, no
   eighth tank bolt, no renaming or expansion of the latest302 generic fastener
   IDs (298 was the historical pre-headlight checkpoint).
2. One specialized12mm held-wrench target/presenter at the tank-local pose
   above, retaining the original inactive source nut as a disabled duplicate.
   Availability follows tank installation; state does not. An explicit stable
   connection key should identify it; no final key is allocated by this audit.
3. Optional vehicle DTO with presence flag and independently versioned
   `{stage, bolted}`; validate endpoint constraints and intermediate latch
   history, deep-copy in every record-copy path, validate before any restore
   mutation. Missing old field defaults0/false (donor new-game state), not an
   invented fully tightened connection. This default and later leak enablement
   must be disclosed because old plays could previously ignore the connection.
4. Expose a sealed/leaking condition to the existing fuel simulation seam.
   Binding the interaction alone is not full parity: leak cadence/quantity and
   leak presentation still require a bounded donor-flow measurement before
   applying real consumption. Do not silently substitute a hard-start blocker.

Required tests for that proposal: 0→8→7→0 latch history; correct/wrong12mm tool;
all9 poses on a translated/rotated vehicle; exact tank visibility with stage
preserved through detach/reinstall; seven-bolt tank removal independent of
the fitting; new/old/malformed DTO validation and nested-copy independence;
generic126/124/302 roster preserved; no sender-wire fuel gate; explicit leak
cadence once measured. Manual reach/outline and source comparison remain
necessary. No feature is marked Verified by this read-only report.
