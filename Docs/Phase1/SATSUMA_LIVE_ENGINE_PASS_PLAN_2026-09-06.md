# Satsuma — “try to start it” integration pass

Status: **Scoped implementation and final automated regression completed;
manual auditory/gameplay acceptance pending.** Exact executed results and
remaining limits are in `SATSUMA_LIVE_ENGINE_INTEGRATION_2026-09-06.md`.
This plan is not a complete 11A-V1, donor-parity or Phase 1 gate approval.

## User-approved scope

The user requested this continuation of **11A-V1 (full Satsuma state)** on
2026-09-06, after accepting that the Unity 6.6 session produces sound:

1. Make the existing engine adjustments affect actual operation, add missing
   donor-evidenced adjustments, and deliver a clear Russian adjustment guide.
2. Fill all Satsuma service-fluid domains **except petrol refuelling**, whose
   reported bug is outside this pass. Preserve real fuel availability and the
   explicitly labelled development-only testing boundary; never fill a save
   silently or bypass assembly/wiring.
3. Implement cold/hot starting, choke/mixture/ignition dependencies, running,
   stopping and the associated failure feedback as project-owned simulation.
4. Add visible pouring with distinguishable liquid properties, mouse-wheel
   screw caps on service points, and visible physical levels where the actual
   geometry permits them. Litres, not particles, remain authoritative.
5. Drive mechanical noises, belt squeal, valve noise, knock/backfire and other
   supported symptoms from actual operating conditions, not arbitrary timers.
6. Audit donor and real-engine moving components; implement the supported
   motion without moving collision/assembly roots merely to animate a mesh.
   Correct the alternator-belt material.
7. Distinguish battery-assisted cranking from alternator-supported running.
   An explicit session-only debug autocharge is permitted; the actual charger
   appliance and purchased-battery replacement are not implicitly complete.
8. Diagnose and correct the reported engine sound apparently displaced toward
   the exhaust. Preserve separate physical engine/starter and effective exhaust
   outlet emitters; verify distance attenuation, layer balance and listener
   placement, including moving/detached parts and restored assemblies.

9. Replace audio with the user-selected `zvuki100aformsc` pack wherever a real
   corresponding event already exists. Inventory every supplied file; unmatched
   content is not permission to substitute unrelated sounds or invent gameplay.

### Queued follow-up after this integration plan

The user closed Unity again on 2026-09-06 and authorized resuming implementation.
After the five integration batches below, address the manual-test findings:

10. The replacement engine and other new sounds are much quieter than stock.
    Measure replacement versus effective stock levels and routed playback gain;
    restore usable relative loudness with headroom, without blindly raising all
    voices or changing unrelated events.
11. Front headlights emit no visible light and the cabin light switch is absent
    or inaccessible. Audit and reuse the existing dashboard switch, electrical
    connections, purchased bulbs, headlight wrappers and HDRP-light bindings;
    repair the missing behavior/presentation and add only genuinely missing
    controls. Preserve established wiring, stable IDs, save state and the 08A UI.

The manual-test hold was lifted by the user's “поехали исправлять” instruction
on 2026-09-06. Unity was confirmed closed and a scoped source/generated-content
backup was made before implementation. User saves remain read-only.

## Bounded audio compatibility extension

Keep the current primary/supplemental/override libraries and their duplicate-ID
validation intact. Add one optional, explicit user-replacement library resolved
before them, with stable event-ID routing through `IAudioBackend`. It does not
rewrite Wwise banks, donor imports or default libraries. Absence preserves all
existing behavior. Imported payload stays ignored and private; source hashes,
mapping, provenance and the Editor-only local path are recorded separately.
Per-event distance rolloff defaults to the existing linear behavior; reviewed
engine/exhaust events opt into logarithmic rolloff. Pooled voices must reset
this setting on every playback. No existing serialized field or ID is renamed.

## Existing foundations to preserve

### Bounded operating-state extension

The generic M06 simulation remains the default. Only a host with an explicitly
serialized Satsuma condition source enables the new pure operating model.
It consumes the existing settings, graph, choke and electrical identities;
it does not replace wheel physics, input, assembly, audio routing or weather.
Oil/coolant/fuel and battery voltage keep their existing simulation authority.
An optional, presence-bit, independently versioned simulation DTO stores the
new hydraulic reservoirs, oil condition and cold-start progress. Old records
retain their existing litres/temperature/voltage; absent new reservoirs are
empty, not automatically serviced. Unsupported hosts reject this extension
at restore preflight instead of silently dropping it.

Mechanical condition belongs to each existing part stable ID, not to its
renderer or the car aggregate. A separate optional part extension will preserve
baseline-part condition through detachment/replacement; purchased consumables
keep their existing item-owned condition. No battery-ownership migration or
physical engine-mount rewrite is authorized by this extension. New thermal,
pressure and combustion smoothing/calibration must be labelled Reimplemented,
with frozen donor constants distinguished from modern solver adaptation.

Caps add an optional independently versioned part-owned extension, with an
explicit ordered cap-kind identity (the brake master owns two). Old records
default to closed without changing stored litres. Five stock cap renderers are
reused by measured source mesh/pose bindings; only render leaves rotate/hide.
New passive pour receivers do not implement the existing instant F transfer
capability. Contents remain in the simulation and item authorities. Unsupported,
duplicate or wrong-owner cap state fails restore preflight before mutation.

- Unity `6000.6.0f1`, HDRP `17.6.0`, accepted 00–08A architecture and UI.
- Canonical Satsuma wrappers, stable part/mount IDs, NWH wheel/contact boundary,
  chassis/loose-compound ownership, native save and streaming lifecycle.
- Existing alternator/distributor/mixture/filter/camshaft settings and their
  versioned part-owned state; existing physical choke and ignition input.
- Item-owned service-fluid contents, liquid capability interfaces and the
  project-owned procedural-fluid presentation. No duplicate item inventory.
- `IAudioBackend` and the tested explicit-content routing. Supplemental private
  donor media stays generated, ignored and `TemporaryDirectImport`.
- Existing petrol station/pour implementation and unrelated dirty changes.

## Confirmed initial gaps

- Existing alternator angle, distributor angle and mixture values persist but
  do not affect combustion/belt/oil simulation. Choke is a physical control with
  no mixture consumer yet.
- Eight rocker-shaft valve adjusters were mistakenly imported as ordinary
  mounting bolts. The reviewed five real shaft bolts must retain their IDs.
  Any exact retirement requires old-save recognition, a bounded migration and
  rejection tests; never infer a valve setting from a former fastening stage.
- Service products already contain `liquid.motor-oil`, `liquid.coolant` and
  `liquid.brake-fluid`. Vehicle service points, per-reservoir hydraulic state,
  caps and a physically placed pour-to-vehicle boundary are missing.
- Current prototype oil/coolant checks prevent combustion immediately. Donor
  lubrication/cooling symptoms and physical consequences require a Satsuma-only
  extension rather than changing every prototype/traffic vehicle.
- Failed cranking is incorrectly classified as `Stalled` whenever the starter
  RPM limiter removes torque. Feedback then enables motor/exhaust rundown and
  restarts the lead-in on each return to `Cranking`. These exact source files
  are identical to the pre-6.6 backup; this is not introduced by the migration.
- Feedback also needs to distinguish a failed-start rundown from a genuine
  previously running engine. Fixing RPM-limit chatter alone is insufficient.
- Canonical engine audio follows the block, not the exhaust transform. However,
  donor exhaust AudioSources 94487/95540 use logarithmic attenuation, while
  the explicit Unity audio path hard-codes linear attenuation. Copying the
  donor's 1–500 m range under that different curve makes the exhaust remain
  nearly full strength around the whole car. This is a confirmed configuration
  mismatch and a likely contributor, not yet an executed auditory diagnosis.
  Any new per-event curve setting must preserve existing audio-library defaults.
- Battery voltage is currently car-global; purchased-battery ownership and
  installed physical engine-mount shake have separate compatibility proposals.
  Do not silently treat broad “alive engine” wording as permission to rewrite
  those accepted ownership/physics foundations.

## Execution batches and acceptance evidence

1. **Reference and compatibility lock.** Decode enabled actions from frozen
   `GAME.unity` (revision `msc-world-baseline-04a1.1-c3f2f337`), record relevant
   paths/hashes/IDs, map real-engine mechanical reference separately, and agree
   explicit state ownership and bounded migrations before authoring changes.
2. **Operating model and adjustments.** Add opt-in, testable Satsuma consumers
   for settings, choke, fuel readiness/priming, temperature, combustion,
   lubrication and electrical supply. Preserve the old generic path. Cover
   continuous no-start cranking, cold/hot starts, bad adjustments, stop/restart,
   battery/alternator loss, pause and restore.
3. **Service-fluid interaction.** Add evidence-backed capacities and independent
   reservoirs; bind actual caps, fill openings, drains and level presentation.
   Transfer contents from real containers at bounded rates, reject wrong/closed
   receivers, account for overflow/missed pours, preserve saves and streaming.
   Petrol refuelling remains excluded.
4. **Causal feedback and motion.** Add only reviewed source clips or clearly
   classified project-owned presentation; bind symptoms to model outputs.
   Correct the Satsuma distance-attenuation mismatch without globally changing
   other audio events; verify separate front/rear spatial origins and layers.
   Animate reviewed render-only mechanical leaves at actual shaft speeds and
   stop broken/disconnected drives; correct belt material. Do not use animation
   frames as gameplay authority.
5. **Integration and regression.** Execute focused and broad EditMode/PlayMode
   checks, native old/current save round trips including unloaded cells,
   generated-content idempotence, real input paths and graphics/audio checks.
   Preserve the user's save files. Manual auditory/visual acceptance is separate
   from automated passes and remains the user's decision.

## Completion reporting

Deliver exact implemented controls, filling steps, cold/hot start procedure,
symptom-to-cause guidance, test commands/results, migrations and limitations.
Do not mark an entire parity row `Verified` from class/prefab existence or a
synthetic start fixture. This pass neither closes all 11A content nor opens
Phase 2.

Reference findings and their remaining evidence limits are recorded in
`SATSUMA_LIVE_ENGINE_REFERENCE_AUDIT_2026-09-06.md`.
