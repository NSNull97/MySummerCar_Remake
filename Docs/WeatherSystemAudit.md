# Weather system audit

Date: 2026-08-12  
Unity: 6000.3.11f1  
HDRP: 17.3.0  
Scope: production weather, sky, lighting, local weather exposure, audio, save and streaming boundaries.

## Executive result

The repository already has a valid 07B/07C foundation. It must be extended, not replaced:

- `ProductionEnvironmentController` is the project-owned authority for game time, deterministic logical weather, accumulated wetness, puddles, lightning and restore-before-world-reveal.
- `Enviro3EnvironmentAdapter` is the only runtime assembly that directly references Enviro types. Gameplay and core weather code do not query Enviro.
- `WeatherExposureResolver`, `WeatherZone`, `WeatherPortal` and `WeatherZoneRegistry` provide an existing non-binary local exposure model, but the current portal model is one-sided and has one combined transmission value.
- `NativeHdrpWeatherBridge` already owns a runtime-cloned HDRP Fog/Exposure/Indirect Lighting slice in the hybrid profile. It is not yet a complete sky/cloud/sun backend.
- `WeatherAudioPresenter` already routes project-owned events and normalized parameters through `IAudioBackend`; it must be fed the richer door/zone context rather than replaced with direct Wwise calls.
- `GlobalWetnessController` and `GlobalWetnessShaderBridge` already separate precipitation from accumulated wetness. They are the compatibility baseline for the richer surface controller.

The migration is now active through a reversible facade: `GameWeatherSystem`
selects exactly one `IWeatherBackend`. Production Bootstrap selects the Enviro
backend in its hybrid ownership mode; full Native HDRP remains an explicit,
reversible fallback until its celestial presentation reaches parity.

## Current ownership matrix

| Concern | Current authority | Current presentation writer | Decision |
|---|---|---|---|
| Game time/calendar | `GameTimeService` inside `ProductionEnvironmentController` | Enviro adapter receives write-only date/time frames | Keep |
| Weather schedule/RNG | `MSC.Weather.Domain.WeatherDirector` + climate-aware `WeatherSchedule` | `WeatherEnvironmentFrameMapper` | Active 16-state weighted scheduler; v2 history save, v1 restore compatibility |
| Rain/storm selection | Project weather domain | Enviro adapter runtime clones | Bounded rain/splashes active; Native emitters retained as fallback |
| Sky and clouds | Project state | Enviro sky/cloud modules | Active and exclusive in the hybrid route; Native sky/clouds remain fallback-only |
| Global fog | Project state | `NativeHdrpWeatherBridge` | Enviro fog controls disabled; one active HDRP owner |
| Exposure | Project state plus local exposure context | `NativeHdrpWeatherBridge` fixed HDRP exposure | Enviro exposure control disabled; retain camera-independent baseline |
| Indirect lighting | Project state | `NativeHdrpWeatherBridge` | Enviro indirect-light control disabled; one active owner |
| Sun/moon transforms | Project game time | Enviro time/sky/lighting modules through `Enviro3EnvironmentAdapter` | Moving sun, moon phases, stars and restrained moon-light curve remain active |
| Lightning gameplay | `LightningStrikeDirector` | Enviro is visual-only | Keep |
| Wetness/puddles | `GlobalWetnessController` | global shader bridge and optional material bridge | Keep; extend without `renderer.material` |
| Interior exposure | `WeatherExposureResolver` | precipitation/fog/audio/native bridge consumers | Adapt into the new local context; do not delete existing zones |
| Electrical lights | `ElectricalGridService`, `LightingRuntimeManager`, `LightingZone` | project light fixtures | Keep independent; weather only supplies ambient context |
| Save/load | native save coordinator plus `WeatherEnvironmentSaveParticipant` | restore is applied before reveal | Weather DTO v2 persists six-front history; v1 active front/RNG migrates on restore |
| Streaming | project world-cell lifecycle | zone registry and production topology revalidation | Keep; portal graph registration must remain event-driven |

## Direct Enviro references

Runtime source inspection found Enviro types only inside the dedicated assembly:

- `Assets/Game/Weather/Enviro3Integration/Runtime/Enviro3EnvironmentAdapter.cs`
- `Assets/Game/Weather/Enviro3Integration/Runtime/Enviro3EnvironmentBindings.cs`
- `Assets/Game/Weather/Enviro3Integration/Runtime/Enviro3ProductionVisualPolicy.cs`
- `Assets/Game/Weather/Enviro3Integration/Runtime/Enviro3WeatherZoneRemovalBridge.cs`

The assembly `MSC.Weather.Enviro3Integration` is constrained by `ENVIRO_3` and `ENVIRO_HDRP`. No gameplay/core assembly references Enviro. `EnviroLightingBridge` is a legacy name only; its implementation consumes project-owned production outputs and contains no Enviro type.

Decision: keep the dedicated integration assembly, add an `EnviroWeatherBackend` delegating to `Enviro3EnvironmentAdapter`, and never expose Enviro types through `IWeatherBackend`.

## Scene and prefab inventory

### Production bootstrap

`Assets/Game/Bootstrap/Bootstrap.unity` contains the active production graph:

- `ProductionEnvironmentController`;
- Enviro manager/sky volume graph;
- production and native HDRP global volumes (`ProductionEnviroSkyVolume`, `ProductionNativeHDRPVolume` assets);
- `NativeHdrpWeatherBridge` and `ProductionWeatherStateSource` in the hybrid implementation;
- three authored `WeatherZone` volumes and project shelter authoring;
- multiple `LocalVolumetricFog` objects named `HDRP_FogVoid` for interior correction;
- the weather audio presenter/listener context;
- inactive legacy `Directional Sun` / `Global Volume` objects retained by earlier migration tooling.

The scene has since been migrated and finalized through serialized references.
The weather root is owned by `GameCompositionRoot`, Native HDRP is selected,
project rain/drizzle/lightning presentation is assigned, and the final scene
validator reports no ownership conflicts.

### Isolated and prototype scenes

- `Assets/Game/Development/WeatherLab/Scenes/WeatherLab.unity` owns an isolated Enviro test graph and must remain self-contained.
- prototype/validation scenes contain their own directional lights and HDRP volumes. They are not loaded as production additive owners and must be excluded or rejected by the production ownership validator if loaded additively.
- garage lighting prefabs under `Assets/Game/Presentation/Lighting/GaragePrototype/` contain directional lights and volumes for isolated art comparison only.

### Existing interior coverage

The donor NoRain coverage audit records 24 donor targets, 23 with static coverage and 20 streamed definitions. Home/garage compound coverage exists. The outstanding material gap is dynamic opening/door portal coverage, plus vehicle cabin coverage called out as deferred. The current one-sided `WeatherPortal` is not sufficient for room-to-room graphs.

Relevant evidence:

- `Docs/Weather/INTERIOR_WEATHER_COVERAGE_AUDIT_2026-08-09.md`
- `Assets/Game/Weather/Enviro3Integration/Editor/Evidence/DonorNoRainCoverageAudit.json`
- `Assets/Game/Weather/Enviro3Integration/Editor/Evidence/HomeHouseNoRainCompoundEvidence.json`
- `Assets/Game/Weather/Enviro3Integration/Editor/Evidence/StreamedInteriorNoRainCorrectionEvidence.json`

## HDRP owners and conflicts

The existing hybrid policy intentionally splits ownership:

- Enviro: sky, clouds, precipitation and celestial presentation;
- `NativeHdrpWeatherBridge`: Fog, fixed Exposure, Indirect Lighting and sun/moon volumetric dimmers;
- interior `LocalVolumetricFog`: local removal/void presentation.

Known conflict risks:

1. Activating a native global sky/cloud backend without detaching Enviro produces two sky/cloud owners.
2. Restoring the Enviro volume profile while native Fog/Exposure remains active produces duplicate global overrides.
3. Loading prototype scenes additively can introduce a second directional sun or global volume.
4. Mutating a shared `VolumeProfile` would leak play-mode values back into assets. Existing hybrid code correctly uses a runtime clone; the new backend must do the same.

The new ownership validator therefore checks selected backend exclusivity, active directional lights, global weather volumes and overlapping active sky/cloud/fog/exposure overrides. Native activation fails closed unless configured legacy owner volumes are suspended reversibly.

## Doors and portals

The project-owned door API is `HingedDoorInteractionTarget`:

- `OpenNormalized` exposes `0..1` openness;
- `TargetOpen` exposes requested state;
- `SetOpen` and `RestoreState` are authoritative mutations;
- continuous interaction is supported;
- no public started/stopped/closed event is currently exposed.

Decision: do not change the door. `DoorWeatherPortalAdapter` reads `OpenNormalized` at a bounded cadence, derives moving/open/closed state changes, emits adapter events only when the value/state crosses a threshold, and owns the two-zone/opening/transmission authoring data.

The current `WeatherPortal` remains valid for old scenes but is wrapped/migrated to the two-sided `IEnvironmentPortal` graph. `null` on one side has the explicit meaning `Outdoor`; hierarchy names are never used as identity.

## Wwise audit

Existing project-owned logical mappings use normalized `0..1` ranges:

- RTPC: `MSC_Weather_Precipitation`, `MSC_Weather_Wind`, `MSC_Weather_ThunderRisk`, `MSC_Environment_Shelter`, `MSC_Environment_TimeOfDay`;
- switch group: `MSC_Weather_PrecipitationType`;
- state group: `MSC_Environment` with Exterior, Sheltered, Interior and Vehicle Interior;
- events: exterior/sheltered/interior rain loops, wind loop, thunder/distant/strike;
- SoundBank: `MSC_Weather`.

The repository contains project `AudioEnvironmentZone` and `AudioPortalAuthoring`, but no configured runtime `AkRoom`/`AkRoomPortal` graph was found. Factory Spatial Audio presets do not constitute authored production Rooms/Portals.

Decision:

- preserve the existing IDs and their actual `0..1` ranges;
- do not invent `0..100` conversion for mappings that are already authored as `0..1`;
- update the existing listener context at 5–10 Hz with a deadband and state hysteresis;
- keep a vendor-neutral extension seam for later Rooms/Portals;
- do not make Wwise Spatial Audio mandatory.

## Save, time, streaming and wetness

- `ProductionEnvironmentSaveDto` contains game time and the complete weather domain.
- `WeatherSaveDto` persists current/target profiles, front and transition durations, elapsed time, cursor, RNG state, overrides and revision.
- `WetnessSaveDto` persists ground, road, puddle and vegetation wetness.
- `LightningSaveDto` persists RNG, cooldowns, candidates and storm state.
- restore is validated transactionally and applied before world reveal.
- zone and portal registration uses `OnEnable`/`OnDisable`; scene lifecycle revalidation is bounded. One fallback listener discovery path exists in `WeatherExposureResolver`, but it is cadence-limited rather than per-frame.

Decision implemented: retain the persistence envelope, extend the authoritative
domain scheduler with the richer Finnish climate policy, persist its recent
history in weather DTO v2, and accept/migrate v1 saves. Global weather never
restarts on cell load.

## Components to keep, wrap and replace

Keep:

- `ProductionEnvironmentController` and its restore-before-reveal lifecycle;
- project weather/lightning/wetness domains;
- `IEnvironmentPresentationAdapter` frame boundary;
- existing zones, fog voids, shelter volumes and coverage evidence;
- `IAudioBackend`, current weather audio IDs and loops;
- electrical/fixture lighting runtime;
- streaming registries and stable IDs.

Wrap/extend:

- `Enviro3EnvironmentAdapter` -> `EnviroWeatherBackend`;
- current production adapter reference -> `GameWeatherSystem` router;
- `WeatherZone` -> `LegacyWeatherZoneAdapter` / local context;
- `HingedDoorInteractionTarget` -> `DoorWeatherPortalAdapter`;
- current one-sided portals -> two-sided strongest-path graph;
- current wetness output -> surface/water-neutral output contract.

Replace only after validation:

- Enviro sky/cloud/fog/exposure ownership, one module at a time, with the native backend;
- hard one-zone portal leakage with channel-specific graph transmission;
- sharp interior fog/precipitation boundaries with depth and roof-aware blending.

## Migration targets

Required scene/prefab work, in order:

1. `Assets/Game/Bootstrap/Bootstrap.unity`: add the router and both backend wrappers while retaining all existing serialized Enviro references; select `EnviroLegacy` initially.
2. Home/garage weather zones: add legacy zone adapters and bind door portals to Outdoor/interior nodes.
3. Teimo shop/bar and other streamed donor interiors: author stable zone IDs, roof coverage and every exterior opening.
4. Multi-room buildings: bind room-to-room portals and verify strongest path to Outdoor.
5. Vehicle cabin: use the same local context interface without replacing vehicle acoustics.
6. WeatherLab: add an isolated router/backend test fixture only; do not make it a production owner.

Migration is intentionally tool-driven and reversible. No Enviro component, profile or serialized reference is deleted by this implementation.
