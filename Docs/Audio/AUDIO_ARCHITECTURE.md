# Milestone 08 audio architecture

Status: **ImplementationComplete / AutomatedValidated**  
Date: **2026-07-18**

## Ownership

Gameplay depends only on `MSC.Audio.Runtime`. The selected playback engine is an
implementation detail behind `IAudioBackend`; vehicle code uses the narrower
`IVehicleAudioBackend`. No vehicle, interaction, weather, world, player or UI
assembly calls an Audiokinetic API.

```text
Vehicle / Player / Interaction / Weather / World
        -> project-owned IDs and typed contexts
        -> AudioBackendRouter
             -> WwiseAudioBackend (preferred, official integration only)
             -> UnityAudioBackend (development fallback)
             -> explicit unavailable/silent diagnostic state
```

The runtime contract consists of `AudioEventId`, `AudioParameterId`,
`AudioSwitchId`, `AudioStateId`, `IAudioEventHandle`, `IAudioEmitter`,
`AudioListenerContext`, `AudioEnvironmentContext`, `AudioSurfaceContext` and
`AudioSettingsState`. IDs are stable project API values. Backend names live in
`AudioEventMap`, `AudioParameterMap` and `WwiseBackendNameMap`.

## Backend selection and failure behaviour

`AudioBackendRouter` chooses the ready preferred backend, otherwise the ready
fallback. It owns emitter registration, duplicate rejection, backend changes,
listener/settings forwarding, scene-unload cleanup and session teardown. A
missing integration, bank, map entry or emitter is reported; gameplay continues
without audio. The router does not silently redirect an unknown event to an
unrelated sound.

`WwiseAudioBackend` is isolated in `MSC.Audio.Wwise` and references only the
official integration. It resolves project IDs through maps, clamps/deduplicates
RTPC writes, registers stable emitters, tracks handles and reports loaded/missing
banks. Bank load ownership remains with the official integration/composition
root.

Authoring validation proves 65 events, 32 RTPCs, 4 switch groups, 3 state
groups, 5 user banks plus `Init`, 6 mixer buses/Volume curves, 7 routed
Actor-Mixer roots and explicit child routing `65/65`. The Windows banks total
`109,329,342 B` and remain ignored. The player-footstep Switch Container has
10 sounds, 5 two-variant random families and `9/9` switch assignments.

`UnityAudioBackend` is the operational development fallback. Generic events are
data-driven through `UnityAudioEventLibrary`; its older M06 vehicle diagnostic
can load seven hash-pinned clips from external donor staging in the Editor.
Missing staging makes that diagnostic silent and does not make the simulation
depend on donor content.

## Emitters, listener and streaming

- `AudioEmitterAuthoring` supplies a stable ID, transform, owning scene and
  explicit surface/environment metadata.
- `AudioListenerContextPresenter` forwards position, orientation, focus,
  shelter, time of day and exterior/sheltered/interior/vehicle-interior state;
  an explicit audio zone wins, otherwise project weather exposure supplies a
  bounded shelter/interior fallback.
- `AudioEnvironmentZone` selects the highest-priority active trigger volume.
- `AudioPortalAuthoring` stores a stable connection and normalized openness;
  door/window gameplay remains authoritative.
- `WorldAmbientAudioPresenter` recreates the donor `MAP/SoundAmbience` phase
  schedule at 06:00/12:00/18:00/24:00 with spatial morning, day, evening,
  night, swamp and meadow layers, an all-day distant dog and three local lake
  emitters. Each layer uses the frozen M04A1 coordinate translation and is
  attenuated by project-owned rain, wind and listener distance. The chainsaw is
  the deliberate user correction: one fair-weather source near the player home,
  attempted only every 15-35 minutes after an initial 5-15-minute delay.
- scene unload removes scene-owned emitters and voices; disable/session teardown
  stops handles and unregisters objects.

There is no per-emitter per-frame occlusion raycast system in this milestone.
Obstruction and reverb send are bounded context hooks awaiting a budgeted
acoustics implementation.

## Domain adapters

- `VehicleAudioPresenter` converts fixed-step simulation telemetry into a
  sanitized `VehicleAudioParameters` value. `VehicleAudioEmitterBackend` maps it
  to RTPCs, surface/engine state and the intake/exhaust/mechanical/tire layers.
- `WeatherAudioPresenter` consumes only `WeatherEnvironmentOutputs`,
  `LightningStrikeEvent` and `ThunderAudioRequest`. It owns rain-space loop
  switching, wind-loop hysteresis and delayed spatial thunder requests.
- `InteractionAudioBridge` listens to completed typed interaction actions and
  posts pickup, drop, throw, place and mount-handoff events. It does not route by
  object name.
- `PlayerFootstepAudioPresenter` derives cadence from grounded planar
  `CharacterController` travel, resets on teleport and selects a switch only
  from `IAudioSurfaceMetadataProvider`; the raycast runs only when a step is due.
- `PlayerLeanImpactAudioPresenter` listens to the project-owned
  `PlayerLeanImpact` notification, maps forward speed to the typed interaction
  impact-intensity parameter and posts `audio.event.interaction.impact` through
  the same registered player emitter used by interactions and footsteps. It
  does not reference BetterMSC or own an `AudioSource`.
- main donor world ambience is runtime-mapped. `wind_chime`, mosquito, two fly
  variants and wasp are imported and mapped as local gameplay-object hooks, not
  misclassified as unconditional global ambience. Garage/interior room tones,
  distant traffic and cell-level bank leasing remain `DeferredHook`.

## Enviro boundary

Enviro 3 remains a visual presentation backend. Production Enviro time,
schedule, lightning and audio ownership are disabled. Audio does not reference
Enviro types or subscribe to Enviro presets. Therefore rain, wind and thunder
have one project-owned audio path and Unity fallback does not require enabling
Enviro audio.

## Settings contract

`AudioSettingsState` exposes master, vehicle, effects, ambience, music and UI
levels; dynamic-range mode; mute-on-focus-loss; subtitles; captions;
accessibility-reduction and output-device ID hooks. Milestone 08 does not build
the final settings UI. Per the user decision, eventual menu/UI sounds are a
separate newly authored content set, not the donor gameplay set.

## Validation

`Tools -> MSC Remake -> Audio` exposes backend/emitter/listener snapshots, map
validation, missing-bank/fallback reporting, event audition where mapped and
validation-report export. Focused remediation suites pass: player EditMode
**3/3**, player PlayMode **1/1**, weather-audio EditMode **7/7**, vehicle/Wwise
playtest PlayMode **2/2**, audio runtime PlayMode **4/4** and Unity fallback
PlayMode **6/6**. Live official Bootstrap Wwise functional PlayMode passes
**7/7** with six loaded banks and no missing banks; the separate performance
case is skipped unless explicitly enabled. Private Windows x64 Development
build and native SoundEngine initialization are validated separately.
Bounded performance passes **1/1** with `0 B` allocated in measured RTPC/domain
update loops and lifecycle counts returning to baseline.

Headless batch suspends audible output. Final listening judgement and real-device
Wwise Profiler audio-thread CPU/virtualization/starvation remain manual; this is
not a final mix or audibility approval. Full EditMode is **350/356** with six
unrelated Garage/World baseline failures and is not claimed as a full-suite
pass. Full PlayMode is **68/70** with one unrelated VehiclePhysicsValidation
failure and one explicitly environment-gated performance skip; the performance
fixture separately passes **1/1** when enabled, and the exact failed vehicle-
route case passes **1/1 in 8.696 s** in isolation. Full PlayMode remains not
claimed as a pass.

## Conditional ambience and dialogue routing correction — 2026-08-02

`WorldAmbientAudioPresenter` consumes project-owned game time and weather
outputs, not Enviro presentation state. The listener-relative forest bed fades
in during the morning, fades out at night, is strongly suppressed by rain and is
partly suppressed by high wind. The chainsaw remains one spatial emitter at a
fixed reviewed world point near the player home; it is eligible only during
08:00–18:30 fair-weather windows and uses a deliberately rare 15–35 minute
successful interval (5–15 minutes for the first opportunity). This is a
project-authored scheduling approximation pending manual donor timing capture.

Dialogue is intentionally backend-neutral at gameplay call sites. Stable
`audio.npc.*` event IDs should ultimately resolve to Wwise dialogue events on a
dedicated Dialogue bus. That move is recommended because Wwise provides one
place for loudness normalization, voice priority/virtualization, music and
ambience ducking, speaker processing and future localization. Phase 1 temporary
donor voice clips remain removable `TemporaryDirectImport` presentation behind
the existing event IDs; no dialogue state machine or save schema depends on
Wwise media names.

Until corresponding Wwise events/banks are complete, the Unity fallback applies
an allocation-free soft gain of approximately +8.5 dB to the explicit Dialogue
category and to existing `audio.npc.*` IDs. It prevents hard clipping with a
bounded soft transfer. Dialogue currently follows the Effects user setting; a
separate dialogue slider is deferred until it can be added without redesigning
the accepted 08A settings presentation.
