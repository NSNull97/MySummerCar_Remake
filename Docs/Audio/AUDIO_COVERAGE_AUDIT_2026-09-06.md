# Existing gameplay audio coverage audit — 2026-09-06

Status: **ReadOnlySourceAndLocalContentAudit / PreRemediationSnapshot**.
This is not an audibility approval and does not mean the complete donor soundscape
is implemented. The parent audio repair task owns runtime fixes and executed tests.
No Unity process was launched, no native save or donor file was changed here.

## Inventory and reproducibility

Inspected project-owned runtime consumers, `AudioProjectIds`,
`SatsumaEngineAudioIds`, Bootstrap composition, the actual serialized event/RTPC
maps and every local `UnityAudioEventLibrary` under the audio baseline.

| Actual serialized inventory | Count | Mapping/content observation |
|---|---:|---|
| `Audio/Content/AudioEventMap.asset` | 69 unique events | A name is not proof the loaded bank contains that event. |
| `Audio/Content/AudioParameterMap.asset` | 32 RTPCs | The six Satsuma-specific gain/pitch IDs are absent. |
| Main `Audio/Content/UnityAudioEventLibrary.asset` | 60 events | All 60 are mapped in Wwise; many generic fallback roles deliberately share diagnostic media. |
| SatsumaAssembly override | 10 events | Install/remove/tighten/loosen, four impact variants and two handbrake gestures. |
| SatsumaEngine supplement | 8 events | All eight absent from Wwise map. |
| PlayerVoice supplement | 27 events | All absent from Wwise map; caller already attempts a Unity fallback. |
| NpcR1 supplement | 17 events | All absent from Wwise map; dialogue callback already attempts a Unity fallback. |
| StoryTrafficR2B supplement | 8 events | All absent from Wwise map; consumers explicitly use Unity fallback. |
| Union of six Unity libraries | 126 unique IDs | 130 rows, with four intended main/assembly overrides. |
| Unique referenced clip GUIDs | 73 | All resolve to local `.meta` files; no serialized `clip: {fileID: 0}`. This does not prove decoding/playback. |

The main library lacks precisely the four body-impact IDs and the five UI IDs
that are present in `AudioEventMap`. The four body impacts do exist in the
SatsumaAssembly override loaded by production Bootstrap. The five UI events have
no Unity clip in any inspected library; existing policy reserves newly authored
UI sounds, so an unrelated donor sound must not be substituted.

Static commands used: `rg --files`, `rg -n` of project-owned `.cs`/`.asset`
sources, PowerShell `Get-Content` and regex extraction of `audioEventId`,
`eventId`, `audioParameterId`, clip GUIDs; scoped `.meta` GUID resolution.
Ignored local presentation libraries were included using `rg --files -uuu`
only under `Assets/Game/LegacyImport/RuntimeBaseline/Audio`.

## Existing consumers and route status before the repair

| Domain | Existing producer / stable IDs | Content and route evidence |
|---|---|---|
| Satsuma ignition/engine | `SatsumaEngineFeedbackPresenter`: `audio.event.vehicle.satsuma.key.{inserted,removed}`, `starter.{engaged,loop}`, `engine.{caught,throttle.loop,coast.loop}`, `exhaust.loop` | Eight real clips in private supplement; Bootstrap loads into Unity then passes general router. Ready Wwise rejects all eight before bank posting. |
| Satsuma engine mix | `audio.parameter.vehicle.satsuma.{throttle,coast,exhaust}.{gain,pitch}` | Bound by the Unity engine library, not the 32-entry Wwise RTPC map. Event fallback alone would leave these parameters misrouted. |
| Satsuma assembly | `VehicleAssemblyAudioPresenter`: `interaction.part.{install,remove}`, `interaction.fastener.{tighten,loosen}` | Explicit four overrides replace generic main-library media. Wwise-preferred path ignores that local override choice. |
| Satsuma impacts | `vehicle.body.impact.{low,high}.{01,02}` | Four Wwise names exist in map; four real clips exist only in loaded assembly override. User runtime reports `Event ID not found`; bank membership is being audited separately. |
| Handbrake | `vehicle.handbrake.{raise,lower}` on `HoldStarted` | Two clips in assembly override; neither ID is in Wwise map. No per-frame repeated ratchet event is intended. |
| Generic vehicle telemetry | `VehicleAudioPresenter` → `VehicleAudioEmitterBackend`; six state transitions, intake/exhaust/mechanical and tire-roll layers | M06/M08 diagnostic path remains distinct from the current Satsuma-specific presenter. Do not run both engine packages on the same Satsuma. |
| Player movement | `PlayerFootstepAudioPresenter`, `PlayerLeanImpactAudioPresenter` | `player.footstep`, `interaction.impact` mapped in both paths; shared registered player emitter. Footsteps use typed surface switches. |
| Carrying/items | `InteractionAudioBridge` | Pickup/drop/throw/place mapped in both paths. Mount handoff is deliberately suppressed; successful assembly notification owns the install sound. No universal item-action audio adapter was found beyond these implemented carry events. |
| Player voice | `PlayerVoiceReactionController` | Sixteen `player.swear.01..16`, eleven `player.finger.01..11`; primary-then-local fallback currently avoids the unmapped-event silence. |
| NPC dialogue | Bootstrap dialogue callback, `NpcDialogueCatalog` | Seventeen installed voice IDs: Teimo greeting seven/pub four, Fleetari one, farmer one, berry collector four. Additional catalog `.pending`/`.foundation` IDs without clips are explicit missing presentation, not recoverable bank content. |
| Story traffic | `StoryTrafficVehicleAudioPresenter` | Jani/Petteri engine loops, skid loops and crash one-shots; Jani music. Explicit Unity backend. Bus driver's stuck-curse event is the eighth StoryTraffic library entry, posted by `TrafficTransportPresentationBinding`. |
| Weather | `WeatherAudioPresenter` | Exterior/sheltered/interior rain, wind, scheduled thunder via router. Project-owned weather is authority; Enviro audio is explicitly silenced. The separate weather-system integration is not proof both paths are active in Bootstrap. |
| World ambience | `WorldAmbientAudioPresenter` | Reviewed time/weather-dependent bird phases, swamp, meadow, dog, lake positions and rare chainsaw. Mapped main library + Wwise; retained handles and scene/context lifetime. |
| Fridge | `FridgeDoorInteractionTarget` in `ProductionFoodApplianceInstaller` | Posts `interaction.door.open/close`, both mapped and present in main Unity library. Other doors do not inherit this adapter. |
| Building light switch | `WwiseLightingAudioAdapter` | Posts `audio.event.lighting.switch` at switch world position; missing from Wwise map AND all six Unity libraries. This is a concrete implemented-producer/content gap. |
| UI | `GameUiRoot` navigation/confirm and `MainMenuBindings` cancel | Three live producers address a five-ID reserved Wwise UI map. No Unity UI clips. Save/load-feedback IDs have no discovered runtime producer. Existing policy requires newly authored UI content. |
| Radio/media | No project radio player/audio producer found | Only the seven story-car events include Jani's music. A radio item/vehicle mounting point does not constitute a radio playback implementation. |

Paths in the table are relative to `Assets/Game` unless a full relative path is
given. Representative pre-repair source locations: Bootstrap installer lines
699–726 (assembly/engine), 736–770 (voice/traffic), 803–815 (dialogue fallback);
`VehicleAssemblyAudioPresenter` lines 136–223; `SatsumaEngineFeedbackPresenter`
lines 135–227; lighting adapter lines 91–114; UI root lines 1093–1127.

## Declared hooks / missing producers: do not call them repaired audio

- No Satsuma door/boot/hood movement or latch audio producer was found in the
  accepted hinged-part runtime. Fridge audio does not cover car doors.
- No dashboard light/hazard/choke click or wiper-motion audio producer was found.
- Generic transmission, body-rattle, suspension-impact and generic tire-skid
  event hooks remain without a corresponding production event producer. Traffic
  skid is a separate implemented stable-ID family.
- Garage/interior room-tone, distant-traffic ambience, local wind-chime,
  mosquito/fly/wasp hooks are not blanket auto-start world beds.
- Generic tool-use/fastener-insert, gate/window events remain hooks; successful
  Satsuma tightening/loosening is implemented under its own typed notifications.
- Radio, full drinking/eating/smoking/fluid/fire sound feedback and unimported
  NPC dialogue are not established simply because gameplay/visuals exist.

`Docs/Audio/AUDIO_EVENT_MATRIX.csv` still labels several now-implemented assembly,
fridge and UI consumers as `No current producer`; it is a historical M08 matrix,
not sufficient current coverage evidence on its own.

## Cross-route and lifecycle defects to cover in parent remediation

1. `AudioBackendRouter.PostEvent` selects a ready engine, not per-event content.
   Ready Wwise plus missing mappings/old bank causes silence despite usable
   same-ID Unity media. Explicit overrides must have one authoritative route,
   not double playback.
2. `SetParameter`, `SetListenerContext`, `ApplySettings`, `StopAll` and
   `EndGameSession` forward only to the active backend in the inspected source.
   Existing direct Unity dialogue/traffic/voice routes therefore miss shared
   settings/listener forwarding and unified stop ownership while Wwise is ready.
3. New Satsuma engine loops have no pause guard. Passing `Time.deltaTime == 0`
   does not pause an existing AudioSource. Traffic also has no pause guard and
   clamps a zero delta to 0.001. World ambience/weather already stop on pause.
4. `WwiseRuntimeBankOwner.BindListener` creates the official default listener on
   the spawned player camera; the Unity listener must independently remain
   valid for simultaneous fallback playback. A transition-time “no listeners”
   message alone does not prove the active gameplay session has no listener.
5. Real tests need both backends selected and real same-ID media, settings/focus,
   stop/return-to-menu/reload, pause/resume, failed-bank/mapping reporting and
   emitter cleanup. A mocked `IAudioBackend` or non-null clip assertion cannot
   demonstrate actual playback.

## Lighting donor evidence (read-only)

Frozen M04A1 `GAME.unity`, not the mutable installed donor, references clip GUID
`3cb43f687046eb84495b520acf746461` from AudioSource **94656**, GameObject **11333**
named `light_switch` (scene lines 183096–183113 and 1449952–1449972).
The matching staged clip is `AudioClip/house_light_switch.ogg`, SHA-256
`03AE4856D6515FFE539DA16C2E6BE100EF18204E9F8A59F38719376FEC8BCC37`.
Metadata: non-looping, play-on-awake false, volume/pitch 1, min/max distance
1/10 m. A compatible repair may import only this hash-pinned presentation into
the ignored RuntimeBaseline and bind the existing stable switch event. It must
not copy the original AudioSource/MonoBehaviour/gameplay owner.

## Validation boundary

Only source/content inspection was executed in this subtask. No tests,
production playback, physical-device recording or human listening were run.
All baseline payloads remain `TemporaryDirectImport`; no change to stable IDs,
save schema, physics or accepted 08A UI presentation is implied by this audit.

## Post-remediation evidence link

The tables above intentionally preserve the pre-repair inventory. The scoped
lighting supplement was subsequently generated, hash-checked and integrated;
the seven Unity libraries now contain 127 unique event IDs / 74 clip GUIDs.
The root repair passed 87/87 EditMode and 49/49 combined PlayMode tests, including
two production Wwise sessions with all 19 engine/assembly/lighting definitions
playing through real Unity sources, plus native Wwise thunder. Native reload
uses a generated temporary slot; the user's slot remains unchanged. Human
listening and the separately listed missing-content features remain open.
Current changes, exact execution evidence and limitations are authoritative in
`Docs/Audio/AUDIO_ROUTING_REPAIR_2026-09-06.md`; the bank audit still reports all
14 remaining empty Wwise content entries, not an artificial 100% completion.
