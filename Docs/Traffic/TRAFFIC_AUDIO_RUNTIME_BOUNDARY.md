# Phase 1 traffic audio runtime boundary

Status: **implemented; focused automated and manual acceptance pending**

Audit date: 2026-08-07

Authority: `Docs/Traffic/PHASE1_LOCKED_TRAFFIC_BEHAVIOR_AUDIT.md`

## Scope

This pass fixes the runtime boundary shared by story cars and the temporary
Phase 1 traffic audio profiles. It does not change traffic routes, schedules,
collision authority, vehicle physics, UI, or streaming ownership.

The recreated runtime remains independent of donor and Wwise runtime code:

- gameplay posts project-owned stable `AudioEventId` values through
  `IAudioBackend`;
- each logical vehicle owns one stable `AudioEmitterAuthoring` outside its
  streamed visual wrapper;
- `VehicleAudioEmitterBackend` scopes RPM, load, throttle, gear, speed, wheel
  slip, surface and impact parameters to that emitter;
- the Unity fallback resolves temporary private Phase 1 media from a
  replaceable supplemental `UnityAudioEventLibrary`;
- a later Wwise implementation must map the same event/parameter IDs instead
  of introducing Wwise calls or donor filenames into NPC/traffic code.

## Corrected defects

### Streaming start chirp

Stopping a traffic driving session used to post the generic
`audio.event.vehicle.reset` one-shot. The current fallback library maps that
diagnostic event to the same temporary clip family used for vehicle starting,
so a materialization or residency transition could sound like a new ignition.

Traffic teardown now stops and disposes its owned loop handles without posting
starter, engine-start, or reset feedback. Binding a replacement streamed
wrapper only changes the transform source; it does not restart the persistent
engine/music handles.

### Missing ordinary impact feedback

Audio previously watched the terminal `IsCrashed` state. Ordinary physical
contacts therefore had no sound, while collision sound was incorrectly tied to
story-crash authority. `StoryTrafficVehiclePresentationBinding` already emits
one accepted `CollisionIncident` per contact window, independently of whether
the incident is terminal.

The traffic audio owner now subscribes to that event and posts one emitter-
scoped spatial impact event above `1 m/s`. The temporary Phase 1 response uses
the reviewed TCE reference curve:

```text
volume01 = clamp(relative impact speed / 5, 0.25, 1.0)
impact RTPC = clamp(relative impact speed / 20, 0, 1)
```

Collision cooldown and terminal story decisions remain owned by the physical
presentation and NPC story runtime; audio does not alter either state.

### Spatial ownership and attenuation

Generated traffic definitions are now rejected if loop identity, full 3D
spatial blend, minimum distance, or maximum distance differs from the manifest.
The Unity fallback uses linear rolloff, a small Doppler factor, and moves the
pooled `AudioSource` with the registered emitter every frame. Current temporary
ranges are data-driven in
`Phase1StoryTrafficR2BAudioManifest.json` (engine `3..65 m`, Jani music up to
`55 m`, skid up to `45 m`, collision up to `80 m`). No source is intentionally
map-wide.

## Lifecycle contract

1. The logical traffic actor creates and owns the audio presenter/emitter.
2. A streamed physical wrapper may bind or unbind without changing the audio
   session.
3. While no wrapper is resident, the logical pose keeps the 3D emitter at the
   vehicle pose.
4. A real driving-state stop disposes engine, skid, and music handles; it emits
   no synthetic ignition/reset one-shot.
5. Terminal disable cannot be undone by a later logical-pose refresh.
6. Disabling/destroying the logical owner unregisters the emitter and releases
   all handles through the existing audio backend lifecycle.

## Validation

Focused PlayMode coverage verifies:

- persistent engine/music ownership across wrapper replacement;
- no engine-start/reset event during materialization, pause/resume, or terminal
  teardown;
- emitter-scoped RPM, engine-load, speed, and suspension-impact parameters;
- one spatial ordinary-impact request per physical contact cooldown;
- fallback 3D blend, exact min/max range, linear rolloff, Doppler configuration,
  source-follow behavior, and RPM-driven pitch.

Manual acceptance still required in the production world:

1. Listen at the vehicle, `30 m`, the configured maximum distance, and across
   the map.
2. Cross a streaming boundary repeatedly while a car is already driving; no
   starter chirp or music restart is allowed.
3. Produce one light contact and one severe contact; both must be spatial, but
   only the physical/story thresholds may decide terminal behavior.
4. Confirm simultaneous vehicles retain independent RPM/load/speed response.

## Known Phase 1 limitations

- Ambient cars, bus, train and boats may temporarily share an existing vehicle
  media profile. They retain independent stable emitters, but dedicated
  per-archetype event media and Wwise authoring remain pending.
- Temporary donor-derived clips remain `TemporaryDirectImport` and must be
  replaced during Phase 2 without changing event IDs or simulation state.
- Listener/interior occlusion and final production mixing are not claimed by
  this pass.
