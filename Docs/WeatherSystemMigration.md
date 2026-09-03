# Weather System Migration

## Safety model

Migration is additive, Undo-backed, and Enviro-preserving. It does not delete Enviro,
rewrite the door API, infer room topology from object names, modify shared HDRP
profiles at runtime, or mass-replace accepted interior zones.

The migration tool is intentionally not an automatic asset postprocessor.
Concurrent work or an accidentally opened scene cannot silently rewrite the
bootstrap hierarchy.

## Generated content

Use:

`Tools > MSC > Weather System > Create or Update Finnish Summer Content`

The deterministic builder updates these project-owned assets in place:

- `Assets/Game/Weather/System/Content/FinnishSummer/FinnishSummerClimateProfile.asset`
- 16 assets below `.../Presets/`;
- `FinnishSummerGeography.asset` (62.4° N, 25.7° E, UTC+3 project baseline);
- `NativeHDRPWeatherVolume.asset`.
- `VFX/WeatherPrecipitationStreak.asset` and
  `VFX/WeatherPrecipitationStreak.mat` when Native HDRP is finalized.

## Stage a loaded scene

1. Commit or shelve unrelated scene edits first.
2. Open exactly the target scene and its required additive authoring scenes.
3. Ensure there is exactly one `ProductionEnvironmentController`, one
   `Enviro3EnvironmentAdapter`, and one unambiguous directional sun.
4. Run `Tools > MSC > Weather System > Migrate Loaded Scene (Enviro Preserved)`.
5. Inspect the created `GameWeatherSystem (Staged Migration)` root.
6. Run `Tools > MSC > Weather System > Scene Validator`.
7. Fix every error before entering Play Mode.

The command creates/configures:

- `GameWeatherSystem`, with `EnviroLegacy` selected;
- `EnviroWeatherBackend` around the existing Enviro adapter;
- inactive `NativeHDRPWeatherBackend` and its dedicated Volume;
- portal graph, interior resolver, roof resolver, local composer, and debug
  controller;
- non-destructive `LegacyWeatherZoneAdapter` components;
- `WeatherAudioController` only when the existing presenter exposes a valid
  `IAudioBackend`; the old presenter is suspended explicitly and restored on
  disable;
- the existing production presentation reference routed through the facade.

The tool does not save the scene automatically. Review the diff before saving.

## Full Native HDRP parity preview in a loaded scene

After staging and validation, run:

`Tools > MSC > Weather System > Preview Loaded Scene (Full Native HDRP - No Moon/Stars)`

This command creates/configures project-owned rain, drizzle and lightning
presentation, parents the weather root below `GameCompositionRoot`, selects
`NativeHDRP`, and assigns the 16-state Finnish summer climate to the accepted
production controller. It is a reversible parity-preview route, not the current
production default: Native HDRP still has no accepted moon/star implementation.
Enviro assets and serialized references remain intact. While Native is attached,
Enviro/hybrid presentation owners and their global weather Volumes are suspended
and are restored by detach/rollback.

## Door and zone authoring

For each room:

1. Keep an accepted `WeatherZone` and add/use its generated
   `LegacyWeatherZoneAdapter`, or author an `InteriorZone` with an explicit
   stable ID, `EnvironmentZoneProfile`, priority, and collider array.
2. Add `DoorWeatherPortalAdapter` beside the existing
   `HingedDoorInteractionTarget`.
3. Assign a project-owned portal stable ID.
4. Assign Zone A and Zone B. A `null` endpoint means Outdoor.
5. Mark `Connects To Outdoor` for an exterior door and verify exactly one side
   is null.
6. Assign the opening transform, width, height, and calibrated fully-open angle.
7. Keep the default per-channel curves unless a measured opening needs a local
   calibration.

Do not make both endpoints null. Do not keep a reference to a zone owned by a
different streaming cell unless both objects have an explicit shared lifetime.
An unloaded endpoint is reported by the validator; the graph fails closed until
the neighbor registers again.

## New building checklist

1. Add `WeatherBuildingEnvelope` to the authoring root and assign a stable ID.
2. Add at least one interior or shelter zone with volume colliders.
3. Add a `WeatherPrecipitationBlocker` for roof geometry, assign its owning
   zone, and list the actual colliders.
4. Ensure roof colliders are included in the `RoofExposureResolver` mask
   (`WorldSolid`/the approved collision policy in the current project).
5. Add a two-sided portal for every gameplay door and open passage.
6. For a canopy, use a shelter/roof result rather than a closed-interior profile.
7. Run the scene validator and test the threshold from both sides.

## Switching presentation backend

The serialized setting is `GameWeatherSystem.selectedBackend`:

- `WeatherBackendType.EnviroLegacy`
- `WeatherBackendType.NativeHDRP`

At runtime use `GameWeatherSystem.SetBackend(type)`. A failed Native attach
detaches the failed owner and restores Enviro plus the last accepted frame.

Before selecting Native HDRP:

- assign project-owned rain and drizzle particle systems or accept the explicit
  degraded warning;
- verify the native dedicated Volume references the generated base profile;
- verify its legacy Volume/owner suspension arrays contain the current Enviro
  and hybrid weather owners;
- validate that there is one sun and no unsuspended global fog/cloud/sky Volume;
- capture side-by-side visual and performance evidence.

Enviro components, profiles, and serialized references remain in the scene until
Native HDRP reaches approved parity. In the current repository no Enviro module
has been deleted or permanently disabled.

## Revert

Use `Tools > MSC > Weather System > Revert Loaded Scene to Direct Enviro`.

The command reconnects `ProductionEnvironmentController` directly to the
preserved `Enviro3EnvironmentAdapter`, removes only the staged facade root and
generated legacy-zone adapters, and supports Undo. It does not delete content
assets or touch doors.

## Current activation note (2026-08-13)

`Assets/Game/Bootstrap/Bootstrap.unity` was deliberately migrated after the
parallel scene state remained stable and a safety copy was written to
`Logs/Bootstrap.pre-weather-migration.2026-08-12.unity`.

The production scene now contains one active
`GameWeatherSystem` below `GameCompositionRoot`, both backend
wrappers, one dedicated Native HDRP Volume, project-owned rain/drizzle emitters,
a lightning flash, local resolvers, the audio bridge, and the streaming bridge
for accepted legacy zones and explicitly marked exterior gameplay doors.

`GameWeatherSystem.selectedBackend` is `EnviroLegacy`. In this route Enviro owns
the sky, moving sun/moon, stars, clouds and the bounded runtime rain/splash
presentation. `NativeHdrpWeatherBridge` remains the single active HDRP
fog/exposure/indirect-lighting writer. The full Native backend, its dedicated
Volume, authored sun and precipitation emitters remain serialized but detached
as a reversible fallback. This restores the intended module-by-module hybrid
migration and avoids claiming celestial parity that Native does not have.

The production controller uses the generated 16-state Finnish summer catalog.
The authoritative scheduler
now applies authored time/month eligibility, temperature, humidity, rarity and
six-front repeat history; its history is persisted in weather save schema v2.
Legacy v1 saves retain the active front/RNG and are upgraded on the next save.

Current automated evidence:

- scene validator: `0 errors / 0 warnings`;
- weather-system EditMode: `21/21 PASS`;
- weather-domain/save EditMode: `30/30 PASS`;
- production environment EditMode: `46/46 PASS`;
- production ownership/integration EditMode: `12/12 PASS`;
- production PlayMode lifecycle: `9 PASS / 0 FAIL / 1 intentional live-Wwise
  performance-capture SKIP`;
- focused Bootstrap fallback PlayMode: the test boots through the hybrid route,
  then explicitly switches to Native to exercise its runtime Volume,
  camera-anchored heavy rain and project-owned lightning flash.

The production lifecycle wait now uses a real-time budget suitable for the
large donor global scene, and backend ownership assertions follow the selected
Native/Enviro route. Session replacement hands portal/streaming ownership to
the incoming root without duplicate-owner errors. Visual calibration and
Windows-player CPU/GPU capture remain manual acceptance work; batch mode cannot
establish that the art direction is approved.
