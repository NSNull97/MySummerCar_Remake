# Audio, liquid, garbage-barrel and puddle gap pass — 2026-08-02

## Scope and inspected evidence

This bounded Phase 1 pass extends the accepted through-08A architecture. It
inspected the existing `IAudioBackend`, item instance/save runtime, domestic
targets, project weather outputs and the frozen donor scene evidence for daytime
ambience, water containers and the garbage barrel. No established stable ID,
save DTO, scene, public audio boundary or accepted UI presentation was replaced.

## Implemented

- Active daytime birds plus a deterministic periodic distant chainsaw routed by
  project audio event IDs. This initial pass validated 53 events and exact
  `53/53` child routing; the later full-ambience parity correction below
  supersedes those roster counts.
- A +4 dB project master ceiling in both Wwise authoring and Unity fallback to
  address the globally quiet mix without changing individual feature balance.
- Neutral liquid source/receiver capabilities: kitchen-tap fill, item-to-item
  transfer, electric-sauna pour and ground spill for the 10 L sauna bucket and
  0.5 L dipper. Their open/close action is removed.
- A one-way garbage-barrel ignition action. Eligible items that remain inside a
  lit barrel for 2.5 seconds become consumed and disappear while retaining their
  stable save record. Critical-recovery items are protected.
- Pooled HDRP/Lit ground puddles driven by the existing authoritative rain
  `PuddleAmount01`, plus local water-spill puddles whose size/drying time depends
  on spilled litres.

## Executed validation

- Phase 1 item catalog builder: PASS.
- Focused Items, Home, Fluid and Audio Runtime EditMode suites: 44/44 PASS.
- Wwise 2025.1.9 authoring: 53 events, 32 RTPCs, 4 switches, 3 states, 6 mixer
  buses, 5 user banks plus Init, 31 embedded media, 0 loose WEM: PASS.
- Official Unity Windows bank synchronization and source/runtime SHA comparison
  for all six banks: PASS; total bank payload 28,448,590 bytes.
- `git diff --check`: PASS (PowerShell CRLF warning only).

The targeted Bootstrap PlayMode audio/puddle smoke did not reach these systems:
startup currently lacks `Resources/Phase1Characters/CharacterPresentationCatalog`
from concurrent NPC work and therefore fails the production composition gate.
This unrelated shared-worktree blocker was not overwritten or worked around.

## Manual acceptance still required

Run the production Bootstrap scene in the Unity Editor and check audible master
level, birds/chainsaw distance and cadence, tap/bucket/dipper/sauna flows, spill
puddle placement/drying, rain puddle population, and barrel ignition/burning.
Final flow/fire/puddle art, fill/pour/spill/fire-specific audio and locked-donor
timing calibration remain pending. Local spill surfaces are presentation-only;
authoritative item liquid and global weather puddle amount retain their existing
save ownership.

## Compatibility impact

The pass adds adapters and presenters around completed 00–08A systems. It does
not migrate or rename accepted APIs, stable IDs, scenes, UI, weather authority or
save schemas. No migration is required.

## Manual acceptance corrections — 2026-08-02

The first in-world acceptance pass exposed four presentation/runtime gaps that
were not represented by the original focused tests:

- The shower and tap streams were visual-only. They now own bounded physical
  trigger volumes and continuously add water to compatible bucket/dipper
  instances while those containers remain in the stream.
- The reviewed donor sauna-bucket wrapper contains a flat `Water` insert that
  depended on donor FSM presentation. The project wrapper now suppresses that
  geometry by its flat mesh signature and creates a project-owned HDRP/Lit water
  surface whose visibility and height follow authoritative saved litres.
- The garbage-barrel particle renderer used Unity's unsupported default particle
  material in HDRP. Fire presentation now uses an explicit validated HDRP/Unlit
  transparent material, a project-owned soft flame texture and light flicker;
  burn/consumption authority remains in the item runtime.
- Rain puddles are world-anchored, use a softer irregular mask and smaller
  radii. The initial 30 m relocation threshold and slightly blue low-alpha base
  were later superseded by the 2026-08-03 correction below.

Focused Items, Home, Fluid and Audio Runtime EditMode validation now passes
`47/47`. A clean Bootstrap PlayMode smoke loaded the production scene and
compiled the new runtime without exceptions from these presenters, but its
existing readiness helper called `TryBeginGameplayPreparation` before the
installer's first `Start()` and failed on the Bootstrap lifecycle precondition.
That unrelated test-order race remains outside this gap pass. Manual visual
acceptance after restarting Play Mode is still required.

## Tilt, fire and conditional-audio corrections — 2026-08-02

The next manual acceptance pass showed that a flat surface alone did not read as
water, rotating the bucket did not drain its authoritative contents, and the
garbage-barrel flame remained hidden behind the opaque barrel wall. The bounded
correction now:

- calibrates the container mouth from the reviewed flat donor insert but renders
  a closed project-owned water volume inside the bucket/dipper;
- continuously drains authoritative litres when the open axis is tilted, sends
  the existing typed spill event to puddle presentation and shows a downward
  procedural pour stream;
- scans the full hot cavity of a lit barrel rather than relying on one narrow
  trigger callback, so eligible thrown items burn after the existing 2.5-second
  delay and retain their consumed save state;
- raises the flame origin above the barrel rim and initially used an explicit
  emissive HDRP/Unlit material, project texture and stronger flickering light;
  that temporary procedural presentation is superseded by the selectively
  imported Fire001 preset described below;
- makes daytime ambience depend on project game time and weather. Birds are
  suppressed by night and strongly reduced by rain/wind. The chainsaw is a
  single spatial source roughly 70 m from the player home, is eligible only from
  08:00 to 18:30 in fair weather, and now attempts roughly every 15–35 real
  minutes after an initial 5–15-minute delay;
- applies an allocation-free soft dialogue gain of about +8.5 dB in the Unity
  fallback for the explicit Dialogue category and existing `audio.npc.*` IDs.

Focused Items, Home, Fluid and Audio Runtime EditMode validation passes `51/51`.
The test log contains no compiler errors, null-reference exceptions, unhandled
exceptions or assertion failures. Manual restarted-Play listening and visual
acceptance remain required because headless Unity cannot judge perceived mix,
water transparency, flame readability or spatial localization.

## Full original-world ambience parity correction — 2026-08-02

The initial single daytime-birds bed was not the full donor ambience. A bounded
read-only audit of the frozen `GAME.unity` `MAP/SoundAmbience` subtree found the
authoritative 06:00/12:00/18:00/24:00 switch schedule and the missing complete
clips: morning/day/evening/night birds, morning/evening swamp birds, daytime
meadow, distant dog and three separate lake-water positions. The familiar
"hu-hu", whistle and other bird phrases are embedded in those original
`birds_*` recordings rather than represented by separate scene clips.

The correction adds stable project IDs, Wwise events and eleven spatial runtime
layers for that reviewed roster. Layer positions use the frozen M04A1
donor-to-project translation; project time chooses the phase and project rain,
wind and listener distance control gain. The previously accepted chainsaw rule
remains deliberately different from donor loop metadata: it is one source near
the player home, fair-weather-only from 08:00 to 18:30, with a 5-15-minute
initial delay and 15-35-minute recurring interval.

`wind_chime.ogg`, `mosquito.ogg`, `fly.ogg`, `fly2.ogg` and `wasp_fly2.ogg`
were imported and mapped too, but donor evidence places them on local or
inactive gameplay objects. They are explicit gameplay hooks and are not
auto-started as a global forest bed.

Executed evidence:

- Audio Runtime EditMode: **13/13 PASS**, including exact phase boundaries,
  the complete eleven-layer roster, M04A1 coordinate translation,
  time/weather/distance gain and rare chainsaw policy.
- Wwise 2025.1.9 authoring: **65 events**, **65/65** exact child routing,
  32 RTPCs, 4 switches, 3 states, 6 buses, 43 embedded media and 0 loose WEM.
- Six-bank generation and Unity synchronization: **PASS**, source/runtime
  SHA-256 match; current ignored Windows payload **109,329,342 bytes**.

A later attempt to rerun Audio Runtime and Wwise EditMode together was rejected
before test execution because another Unity session had this shared project
open. That session was left untouched. The completed post-compile 13/13 focused
result and independent Wwise authoring/bank validations above remain the
executed evidence for this correction.

The temporary donor clips remain local/private `TemporaryDirectImport`. The
93.6 MB `MSC_World` bank is intentionally not called production-ready: audible
Phase 1 acceptance comes first, then streaming/codec/compression tuning must
reduce its memory cost without changing stable event IDs.

## Pause lifecycle and Fire001 barrel presentation — 2026-08-02

The world ambience previously sampled only the project clock during its
three-second environment refresh. Opening the pause menu could therefore leave
already posted bird, lake and chainsaw events playing. Weather rain and wind
loops had the same missing lifecycle boundary. Playback now evaluates both the
authoritative `IGameTimeService` pause flag and Unity's immediate
`Time.timeScale` signal every frame. Entering pause stops world ambience,
chainsaw, rain and wind handles; resuming reconstructs the valid time/weather
layers. The rare chainsaw deadline is shifted by the paused real-time duration,
so a pause does not silently consume its 15–35-minute schedule.

The user-supplied `Fire001.unitypackage` was audited with package SHA-256
`1BF7B7B503BF2DD5B648C306E21230FBDC41D13610961F0A75D65EA30D8A1063`.
The deterministic importer selects only its six-particle prefab, fire/smoke/
spark textures and third-party notice. Nova shader code, renderer features,
editor scripts, demo content and incompatible source materials are excluded.
At runtime `GarbageBarrelFirePresenter` instantiates the selected preset and
binds project-owned HDRP/Unlit transparent emissive materials, preserving the
existing project-owned ignition and item-consumption state machine.

Executed validation:

- focused Audio Runtime, Audio Weather Integration and Fluid EditMode suites:
  **27/27 PASS**;
- imported prefab: exactly six ParticleSystems and six HDRP/Unlit renderers;
- selected payload: zero Nova or source-material GUID references;
- Unity batch compilation and test run: exit code `0`.

Manual restarted-Play acceptance is still required for perceived pause fades,
flame scale/placement, smoke density and exposure-dependent brightness.

## Continuous barrel-fire correction — 2026-08-02

Manual acceptance exposed two source-preset assumptions that were unsuitable
for the barrel. Five Fire001 layers used finite 230-250-cycle bursts at a
0.02-second interval inside a seven-second loop. They therefore emitted for
roughly 4.6-5 seconds, went visibly empty and restarted as a new ignition. The
general-purpose preset was also too large and originated too close to the rim.

The project presenter now stops the source systems before adaptation, removes
all finite bursts, assigns bounded constant emission to all six layers, forces
zero-delay looping and primes the systems when a lit barrel becomes visible.
After visual feedback, the continuous rates were restored to the source
preset's dense aggregate output of 410 particles/second while keeping the empty
loop phase removed. The effect origin is lowered to 25% of the positive barrel
half-height above its centre. Follow-up acceptance confirmed that the original
scale was correct, so the 0.72 barrel-diameter multiplier and 0.35-1.15 bounds
are restored. Point-light intensity/range remain reduced to 720/4.2 m.
Authoritative ignition and consumption behavior did not change.

The open Unity Editor completed its automatic Tundra compilation successfully.
Independent Unity-Roslyn compilation of both the fire runtime and updated Fluid
EditMode test assembly also exited `0`; the new regression asserts zero bursts,
positive continuous emission, source-equivalent aggregate density, looping,
internal origin and bounded scale. After the shared Editor closed, the focused
Fluid EditMode suite passed **6/6** with Unity exit code `0`.

## Rain-puddle pop-in and grazing-angle color correction — 2026-08-03

The prior world-anchor correction stopped visible puddles from running away,
but the 5.6-15.8 m source-offset ring and 30 m recycle threshold still allowed
new slots to appear directly in front of a running player. The slightly cool
`(0.13, 0.145, 0.155)` absorption base also became visibly blue at a low seated
camera angle.

Rain slots are now prepared 30-55 m from the player, remain anchored until more
than 85 m away, and fade from zero to their weather-driven alpha over four
seconds. Hiding a slot invalidates its old anchor so a later rain-intensity
increase cannot reactivate a nearby surface instantly. The Lit material and its
property blocks now use equal neutral `(0.018, 0.018, 0.018)` RGB channels;
visible color may therefore come only from scene lighting and reflection, not a
blue material tint. Local water-spill puddles retain their real spill position
and drying behavior.

The open Unity Editor completed Tundra compilation without C# errors, and
independent Unity-Roslyn compilation of the Fluid runtime and updated test
assembly exited `0`. The regression now covers minimum preparation distance,
zero-alpha first frame, fixed world anchoring and neutral RGB. After the shared
Editor closed, the focused Fluid EditMode suite passed **6/6** with Unity exit
code `0`.
