# Satsuma engine feedback — bounded private Phase 1 packet

Status: implemented source and hash-checked local media manifest. Parent's
night Unity run passed SatsumaEngineFeedbackTests31/31 with0failures/0skips
(`Logs/codex-night-editmode-20260906.xml`). Audible in-game comparison remains
pending; this is not a claim of verified whole-engine sound parity.

## Evidence and classification

Locked `GAME.unity` SHA256:
`c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4`.
The manifest records eight original clip hashes after the 2026-09-06 exhaust
extension, stable replacement event IDs,
generated GUIDs and `TemporaryDirectImport` classification. Only media is
imported; no donor scripts, FSMs, controllers or assemblies enter the runtime.

| Frozen evidence | Reviewed behavior | New implementation |
| --- | --- | --- |
| Ignition UseNew 107294, Sound / Motor OFF | CarFoley/carkeys_in and carkeys_out; no separately established turn clip | Accepted key-gesture event, not an input or save polling heuristic |
| Starter 106807, Turn key | Starting/start1 at 0.45 gain; 0.316 s lead-in | One engagement event on actual entry to Cranking |
| StarterSound GO18528 / AudioSource95004 | motor_start_2, loop, gain 0.32, distance 1–8 m | One owned loop after the lead-in, stopped on leaving Cranking |
| Starter 106807, Start engine | Starting/start3 at0.45 gain | Catch only on Cranking → Running, not on a restored or push-started Running state |
| SoundController112089; Drivetrain112082 maxRPM8000 | idle_sisa5b throttle and idle_sisa4b coast, pitch factors1.25/0.7; volume coefficients selected by installed exhaust | Two replaceable loops driven by real runtime RPM/throttle and explicit exhaust binding |
| Exhaust109210 and four ExhaustSound FSMs | Contiguous Installed-only chain selects engine/headers/pipe/muffler outlet | Eighth clip idle_sisa4, one scoped emitter/loop, exact outlet coefficients and measured chassis-local positions |
| Starter Stall engine / Wait | RPM rundown, then disable starter; no dedicated normal stop/stall clip | Real RPM-driven tail while Off/Stalled, silence at0 or detach; no invented shutdown effect |

For `n = rpm / 8000`, `t = throttle`:

- throttle gain = clamp01((t + 0.2) × throttleVolume × n), pitch = 0.5 + 1.25 × n;
- coast gain = clamp01((1 − t) × coastVolume × n), pitch = 0.5 + 0.7 × n.

Throttle/coast coefficients are1.5/1 for engine/headers, .9/.7 for pipe,
.5/.5 for muffler. Optional unbound legacy callers retain.6/1. Independent
exhaust pitch=.55+RPM/10000 and gains1/1/.8/.2 at the four respective outlets.

The runtime additionally sanitizes non-finite inputs and clamps excessive input
RPM to twice the donor range for presentation safety. Existing audio project
headroom/category/user gains remain authoritative. Real engine load is exposed
as the existing scoped load parameter; it does not fabricate extra donor gain.

## Compatibility and bounds

`UnityAudioEventDefinition` gains optional volume/pitch parameter IDs. Empty
fields retain existing voices unchanged. Emitter-scoped values override global
ones. Pitch bindings are accepted only on loops: existing fixed one-shot expiry
is not silently invalidated. `IAudioBackend` itself is unchanged.

The presenter owns three explicitly authored emitters and its own handles. It
does not call global StopAll. Missing media is retried at most once per second
per loop. Restore/reset and rebind suppress fake key/start/catch transients.
No new save field is required: feedback is derived from persisted simulation.

Vibration is **Reimplemented presentation approximation**, not a transferred
donor chassis force algorithm. It applies bounded sub-1.2 mm offsets only to
explicit engine-owned, collider-free leaf renderers while the block and the
owning part are installed and the real engine is Running. No Rigidbody,
collider, joint, mount, part-root or camera is moved; no force/torque is added.
An authoritative tuning pose replacing the leaf pose is preserved. Offsets are
removed before the next presentation pass and on disable. Phase is transient.

## Integration hooks

1. `Phase1SatsumaEngineAudioImporter.Build()` verifies all source hashes before
   writing; no output-root deletion. It creates the ignored supplemental library.
2. After the existing simulation, ignition and engine graph authoring,
   `Phase1SatsumaEngineFeedbackAuthoring.ApplyToInstance(assembly)` authors the
   explicit bindings. Repeating it returns zero when nothing changed.
3. Production composition loads supplemental content at
   `SatsumaEngineFeedbackPresenter.FallbackResourcesPath`, then calls the prefab
   presenter's `ConfigureBackend(audioBackendRouter)` independently of assembly
   audio success. Never configure an explicit backend on these two emitters;
   the presenter is their registration owner.
4. Private build validation calls
   `Phase1SatsumaEngineAudioImporter.EnsureGeneratedForBuild()`. It validates
   generated media without requiring the original installation at play time.
5. Standalone scoped menu: Tools / MSC Remake / Phase 1 / Satsuma /
   Refresh Engine Feedback Only. It imports seven clips and refreshes only the
   Satsuma prefab, preserving a timestamped previous prefab in ignored Logs.

## Checks

- Read-only SHA256 check: all 7 media match manifest; all 7 generated GUIDs are
  32 characters and source files exist. No source media was changed.
- `git diff --check` for the modified fallback files: clean.
- Added EditMode fixture `SatsumaEngineFeedbackTests`: donor formulas, safe
  invalid inputs, crank delay/catch/loop lifecycle, restore, detach, bounded
  retry, physical-transform invariance and authoring idempotency.
- Added PlayMode fixture `UnityAudioScopedParameterTests`: actual fallback
  AudioSource gain/pitch, emitter precedence, legacy default preservation and
  rejection of unsupported one-shot changing-pitch authoring.
- Unity tests have not been executed by this subagent; root owns execution.

## Explicit remaining differences / listening checks

- Healthy starter pitch is 1. Original starter varies pitch using donor battery
  Charge; that quantity is not asserted equivalent to native voltage. Do not
  label a fabricated voltage conversion as transferred parity.
- Broken-starter grind, fan-belt squeal, damage/backfire and transmission whine
  are separate conditional systems. A clip filename alone is not a reason to
  emit them; they are not represented by the seven healthy-engine events.
- Vibration amplitude/order is a restrained project presentation choice, not
  a measured donor chassis response. This packet intentionally cannot retune
  accepted suspension or make the car creep.
- Exact healthy cold/warm start sound synchronization, loudness inside/outside,
  battery-degraded sound and component-damage conditional sounds still require
  executed comparison and corresponding real simulation consumers.

Next bounded verification: run the new fixtures, then listen to one failed
crank, one successful catch, idle/rev/coast and key-off, plus save restore with
the engine Off and Running. Do not start another physics redesign.

Follow-up audit: `SATSUMA_ENGINE_VIBRATION_AUDIO_AUDIT_2026-09-06.md` establishes
the donor block-to-chassis hinge vibration and documents the subsequently
implemented normal stock exhaust layer. Safe leaf vibration still is not
physical chassis parity. Catch-volume, RPM-rundown and exhaust corrections are
implemented and source-compiled; root owns Unity execution. Racing exhaust
presentation and conditional damage sounds remain outside this bounded packet.

## Executed canonical purchased-plug ignition integration — 2026-09-06

Independently inspected the root-run Unity PlayMode XML
`Logs/codex-night-real-plugs-wiring-20260906.xml`:
`CanonicalPurchasedPlugIgnitionPlayModeTests.RealPurchasedPlugsAndIgnitionHoldSustainIdleRevAndKeyOffThroughHostFrames`
**Passed**, 2.272501 seconds wall time, zero skips. The combined run was not
green: 1 passed / 2 failed; both failures belong to the separate wiring fixture
and occurred during setup because its NWH chassis was kinematic. They do not
invalidate this test result or count as verified wiring interaction.

The passing test uses the canonical prefab, original authored simulation config,
actual NWH backend, prerequisite source, ignition input adapter and ordinary
`VehicleSimulationHost.FixedUpdate` lifecycle. Four healthy `item.spark-plug`
instances use the real Items runtime, sanitized presentation provider, canonical
item/assembly bridge, physical carry handoff and eight spark-plug-wrench turns
each. Their actual condition bindings produce the four-cylinder firing mask;
no synthetic plug-condition component is substituted.

The real ignition target enforces its >=0.4-second held gesture. The test
observes Off → Cranking → Running, releases the key, then verifies three
simulated seconds of uninterrupted idle without starter or throttle. Holding
the actual carburetor target for two simulated seconds raises RPM by >500;
release returns to idle over five seconds. The next key action switches Off
and the real simulation runs down to zero RPM. Config/backend/input references
are asserted unchanged throughout. Engine state is not forced to Running,
`Root.Tick` is never called directly, and no fake wheel backend is installed.

Bounds: stock assembly and electrical readiness are prepared using in-memory
DTOs while the engine is Off at zero RPM; the four plugs alone exercise the
live installation path. Session-only F10 fluid readiness override is enabled,
and fuel/oil/coolant remain zero. Captured 0.02-second frame time makes the
simulated-duration assertions faster than wall time. The dynamic chassis has
`FreezeAll` in an isolated local physics scene, without explicit physics-scene
simulation. This verifies stationary engine startup/control integration, not
driving, road contacts, whole-engine assembly order, shop purchasing, physical
chassis vibration, full player raycast routing or audible sound quality. It
does not read or write the user's native save and does not load Bootstrap/world
scenes. Fixture time settings, transient items and provider ownership are
restored/removed on teardown. No runtime, generated asset or save migration
change was needed for this passing scenario.
