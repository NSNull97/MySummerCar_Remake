# Engine vibration and healthy audio — bounded read-only audit

Frozen `GAME.unity` SHA256:
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
Original and staging remained read-only. No Unity run or save edit by this
subtask. This note supersedes assumptions about shutdown and chassis vibration
in the initial 2026-09-05 engine-feedback packet.

## Vibration: actual donor consumers

1. **Physical engine oscillation is real.** `Symptoms` GO24871 /
   `MotorShake` FSM111116 alternates `AddTorque` on block GO30853 /
   Rigidbody91451, local X, `Space.Self`, `ForceMode.Impulse` (enum1), every
   fixed update. Original action also applies once on entering each state.
   Half-period is `(10000 - RPM) / 200000` seconds. At800RPM it is.046s,
   approximately10.87 complete cycles/s. It is not simply crankshaft rpm/60.
2. `Oil` FSM112415 `Starting engine` writes Min=-2, Max=2 before its3–7s
   startup wait. `Cylinders`104983 sets Flywheel=.4 for stock or1.2 for racing.
   `MotorShake.State2` computes Min/Max × Flywheel: healthy stock impulses are
   ±.8, racing ±2.4 in the original physics model. Serialized variable snapshots
   contain much larger stale values; they are not the healthy contract.
3. `Oil.Crank wear` takes the Shake branch when `WearCrankshaft <=10`.
   That branch sets Min/Max to±22000 and FrictionCrank=.2. This is a distinct
   damage symptom, **not** an idle amplitude to copy into the remake.
4. `BoltCheck`112951 `Set body` creates a HingeJoint on the block, connects it
   to Satsuma GO28145 / Rigidbody91271, enables limits[-.25,+.25]degrees and
   sets block.isKinematic=false. Anchor is zero; axis is left at the newly
   created hinge default. Therefore physical impulses on the mounted block can
   react through the joint into the car. Chassis amplitude is an emergent joint,
   mass, inertia and contact response, not an authored millimetre constant.
5. `Starter`106807 `Check clutch` enables Engine GO784; `Wait` disables it
   recursively. MotorShake resides under that engine subsystem. It is not an
   always-on background force. Startup/rundown timing follows that subsystem.
6. Generic `Drivetrain.cs:887` has a neutral/disengaged-clutch reaction-torque
   branch, but this Satsuma's authored112082 `engineOrientation` is **zero**.
   Thus the existence of that generic code does not establish an additional
   active chassis-shake path here. No corresponding nonzero setter was found.
7. Separate gear-lever presentation exists: FSM106376 and108963 play
   `satsuma_gear_lever_vibration` at RPM>=50, speed=RPM/200, subject to their
   linkage check. This is not the same engine-block or chassis effect.

### Current remake boundary

`SatsumaEngineVisualVibration.ApplyFrame` moves only explicitly bound,
collider-free engine renderer leaves, by less than1.2mm while Running. It does
not move the chassis, physical block, mount frames or camera. Its amplitude and
frequency are a restrained **presentation approximation**, not transferred
MotorShake behavior.

Engine docking authoring currently does not add an installed physical link to
the block. `PartInstance.InstallAt` therefore uses its kinematic installed-body
path. Original block-hinge reaction into the vehicle is a **real remaining gap**.

Minimum physical parity proposal: opt-in engine-mount hinge, reviewed±.25degree
limits, healthy alternating excitation, explicit stop/detach/restore guards.
First audit compound collider and mass ownership: turning the block dynamic must
not double its mass or break accepted docking, save restore or support behavior.
Required tests: unchanged docking stages/poses, conserved aggregate mass,
zero mean drift on level ground, bounded chassis/engine response, immediate
stop on detach/disable, repeatable safe restore. Parent deliberately deferred
this physics change; no physical source was edited in this audit.

## Audio coverage and confirmed remaining differences

The seven-clip packet contains: two key gestures, first starter engagement,
repeating starter crank, successful catch, throttle/idle and coast loops.
The two engine loops jointly provide idle/rev/coast rather than separate idle
and rev one-shots. This covers the basic healthy temporal sequence, but does
**not** establish a complete donor engine/exhaust soundscape.

- `Starter.Start engine` plays Starting/start3 at gain.45, not1. Approved
  correction changed the project manifest to.45 with a regression assertion.
- `Starter.Stall engine` has no separate audio play action; it removes torque
  and waits for RPM approximately100 before `Wait`. `SoundController.cs:246–250`
  continues to mix its engine loops from actual RPM during this rundown.
  Native `EngineSimulation` also keeps nonzero RPM for a while after its status
  becomes Off/Stalled. The previous feedback incorrectly muted immediately.
  Approved correction adds `EngineLoopsActive` separately from `Running`, keeps
  these loops while Off/Stalled RPM>0, and stops them at0. Detached block forces
  effective RPM0 regardless of stale telemetry. Restore never replays key,
  starter or catch one-shots. No fabricated stop/stall clip was added.
- The generic SoundController `startEngine` clip is null/volume0 in this
  Satsuma112089. The real starter is106807 + AudioSource95004. A generic field
  name is not evidence of another missing startup clip.
- Healthy starter pitch currently remains1; original uses clamped
  batteryCharge/120 in[.4,1]. Native voltage is not established equivalent to
  that donor Charge variable.
- **A normal exhaust layer was missing**, not merely optional damage
  sounds. FromPipe7590/106235, FromMuffler7750/106269,
  FromHeaders21968/110301 and FromEngine29225/112461 use one additional loop,
  `idle_sisa4.wav`, source GUID`2eba24e55e868434fa4a946e576b9bdd`.
  Source gains are respectively.8/.2/1/1; pitch=.55+RPM/10000, range1–500m.
  They also set base throttle/coast volume coefficients to.9/.7, .5/.5,
  1.5/1, 1.5/1 respectively. The former fixed.6/1 coefficients were only the
  serialized SoundController baseline, not all exhaust configurations.
  Gate/selection controller109210 was subsequently decoded and a bounded
  stock-chain extension approved and implemented below.
- Broken-starter grind, valve/belt sounds, backfire and transmission sounds
  remain separate conditional systems; this audit does not relabel them done.

## Executed checks for the approved small audio correction

`dotnet build Logs/engine-feedback-rundown-compile.csproj --nologo -v:q`:
runtime+SatsumaEngineFeedbackTests source compile passed,0errors/0warnings.
`git diff --check` for the changed feedback source/tests: clean. New tests cover
Off/Stalled rundown, quiet restore, detached-block silence despite nonzero RPM,
and catch manifest gain. Root owns the subsequent Unity import and test run;
source compilation is not an audible in-game verification.

## Follow-up: installed exhaust-chain layer

`Logic` FSM109210 on Exhaust GO18039 has seven states. Every guard reads
`Data.Installed`, not `Bolted`, torque or a bolt-stage threshold:

- Engine: no matching headers -> FromEngine; stock headers -> Stock headers;
  steel headers -> Racing headers.
- Stock headers: require Headers database GO17995; ExhaustPipe GO33059 advances
  to Stock exhaust. Missing headers returns to Engine.
- Stock exhaust: require headers and pipe; ExhaustMuffler GO26370 advances to
  Stock muffler. Missing either required upstream link returns to Engine.
- Stock muffler: all three required; missing any returns to Engine and
  recalculates the surviving contiguous path.
- The racing branch uses Steel Headers GO13357, Racing Exhaust GO2671 and
  Racing Muffler GO30815 with identical transitions. It does not accept a
  mismatched stock pipe/muffler to finish that chain.

Only the three stock parts currently have authored project IDs:
`vehicle.satsuma.part.headers`, `.exhaust-pipe`, `.exhaust-muffler`.
The extension implements these explicit references. It does **not** invent
racing PartInstances or claim their unavailable presentation is complete.

Measured audio positions below are meters in SATSUMA64200 local space.
Exhaust54098 and its parent systems71973 both have identity TRS:

| Outlet | Transform | Chassis-local XYZ | Outlet gain | Engine throttle/coast coefficients |
| --- | --- | --- | --- | --- |
| Engine | 65270 | .028999226, .12, 1.1820002 | 1 | 1.5 / 1 |
| Headers | 58024 | .106008425, -.014121518, 1.1816006 | 1 | 1.5 / 1 |
| Pipe | 43644 | -.386, -.231, -1.342 | .8 | .9 / .7 |
| Muffler | 43806 | -.39942816, -.24288762, -1.6977237 | .2 | .5 / .5 |

Starter106807 enables Exhaust in both `Start engine` and push-start `Start`,
not during bare starter cranking. `Wait` disables it. Stall engine compares
RPM to100 with tolerance10; equal or less enters Wait. Consequently the
independent outlet layer stops at<=110RPM during Off/Stalled rundown while
SoundController's separate engine beds can continue to0RPM. Native Running
maps to the successful-catch phase, rather than reproducing donor startup
coroutine delays as authoritative state.

Implementation is `SatsumaEngineExhaustBinding` plus the existing feedback
presenter/authorer and IAudioBackend. One audio-only emitter moves between
these points. Three cached PartInstance reads build an installation mask;
position updates only when that mask changes or after restore invalidation.
No runtime hierarchy lookup, per-frame allocation or physical transform move.
One loop handle remains alive through outlet changes, with scoped gain/pitch
parameters. It follows real RPM and is silent when the block is detached.
It requires no new save field because it derives from existing assembly and
simulation state. Old optional-binding-null callers keep their prior mix.

The eighth hash-locked media entry is `AudioClip/idle_sisa4.wav`, SHA256
`2c5de6300a501f0787525db9799d8a4c90efdcce62ae3f28c3977b4056993fe6`.
Event `audio.event.vehicle.satsuma.exhaust.loop`; gain/pitch IDs end in
`satsuma.exhaust.gain` / `satsuma.exhaust.pitch`. Classification remains
TemporaryDirectImport with the existing Phase2 replacement key. Importer and
existing full/scoped authoring hooks are extended, not replaced.

Added17 regression cases: all8 stock presence combinations,4 coefficient
modes,3 separate upstream removals/reinstalls without loop duplication,
quiet partial-new-game/restored-running/start/rundown/detach flow, and the
eighth clip/hash/RTPC/distance manifest contract. Source compile of runtime,
authorer, importer and all feedback tests passed with0errors; importer JSON
DTO reflection fields produce existing CS0649 under bare csc, suppressed by
the normal Unity warning configuration. Hash equality and diff-check passed.
Parent then executed `Logs/codex-night-editmode-20260906.xml`:
SatsumaEngineFeedbackTests passed31/31, failed0, skipped0, including these17
new cases. The overall suite has unrelated failures documented separately;
this is not a claim that the whole game passed. Audible comparison remains
pending.

Next bounded verification is the combined Unity fixture run and an in-game
comparison of fully muffled versus open-pipe engine sound, not physics work.
