# Weather System Testing

## Automated result

Unity 6000.3.11f1 final focused runs:

```text
Assembly: MSC.Weather.System.Tests.EditMode
Result: Passed
Total: 21
Passed: 21
Failed: 0
Skipped: 0

Assembly: MSC.Weather.Domain.Tests.EditMode
Result: Passed (30/30)

Assembly: MSC.Weather.Production.Tests.EditMode
Result: Passed (46/46)

Assembly: MSC.Weather.Production.Integration.Tests.EditMode
Result: Passed (12/12)

Assembly: MSC.Weather.Production.Tests.PlayMode
Result: Passed 9, Failed 0, Skipped 1
Skip: explicit live-Wwise performance capture (requires opt-in flag)

Assembly: MSC.Weather.System.Tests.PlayMode
Result: Passed (1/1)

Bootstrap scene validator
Result: 0 errors / 0 warnings
```

Celestial/cloud/fog follow-up (`2026-08-13`): the focused EditMode suite passes
`59/59`, including runtime weather isolation, monotonic clear-to-storm cloud
coverage, Finnish night/star/moon calibration, `80 m` Lake Mist, schedule, frame
mapping, and hybrid exposure math. The real production Bootstrap PlayMode route
passes `1/1` in `49.32 s`, including the Enviro/HDRP ownership contract, minimum
moon scale, visible `23:20` star intensity, and continuous `23:20 -> 23:22`
celestial movement. Results are
`Reports/weather-night-cloud-fog-editmode.xml` and
`Reports/weather-night-cloud-fog-playmode.xml`. These headless checks do not
replace the manual rendered and performance acceptance route below.

Final XML results are `Logs/WeatherSystemFinalEditMode5.xml`,
`Logs/WeatherDomainFinalEditMode3.xml`,
`Logs/WeatherProductionFinalEditMode3.xml`,
`Logs/WeatherProductionIntegrationFinal6.xml`, and
`Logs/WeatherProductionPlayModeFinal3.xml`, and
`Logs/WeatherSystemFinalPlayMode6.xml`. The final scene validation log is
`Logs/WeatherFinalValidation2.log`.

Covered contracts:

- exactly 16 unique Finnish-summer states;
- generated profile references 16 valid preset assets;
- forbidden direct clear/heavy-rain and mist/clear front transitions;
- thunderstorm predecessor restriction;
- same seed/context produces the same sequence and continuous state;
- scheduler snapshot restore continues the same RNG sequence;
- the production projection exposes all 16 stable states and applies
  time/month, temperature, humidity, rarity and recent-history selection;
- weather save v2 persists scheduler history and restores the exact weighted
  sequence, while v1 DTOs upgrade without discarding the active front/RNG;
- both the standalone and authoritative production advances allocate zero
  managed bytes over 2,000 warmed steady-state steps;
- every `WeatherState` channel blends continuously;
- wetness persists after rain and later dries;
- JSON save/restore preserves a mid-front state and future evolution, rejects a
  mismatched continuous channel, and leaves the destination checkpoint intact;
- the portal graph selects strongest paths independently per channel;
- portal unload unregisters cleanly without resetting global weather;
- backend switching keeps one attached owner, reverses correctly, and rolls
  back when the requested backend rejects the replayed presentation frame;
- Native HDRP attach clones its Volume profile, accepts a presentation frame,
  consumes local precipitation exposure, suspends a legacy owner, and restores
  the profile, particle emission, and authored owner state on detach;
- vehicle body-rain and cabin-listener rain remain separate, and a future
  aperture continuously opens the cabin context;
- Finnish-summer noon solar elevation exceeds midnight;
- Native fallback directional-light rotation follows solar elevation/azimuth,
  and the exact `1995-08-01 23:20 -> 23:22` solar calculation remains
  continuous instead of crossing a binary light cutoff;
- the real Bootstrap selects the Enviro/HDRP hybrid, keeps the project clock
  authoritative, and retains configured Enviro sun/moon, stars, moon lighting,
  bounded rain/splashes and a single HDRP fog/exposure owner;
- the focused fallback PlayMode test explicitly switches from the production
  hybrid to Native before exercising its rain impacts and lightning.

The allocation tests warm both scheduler paths and assert zero managed bytes
over 2,000 steady-state advances.

## Commands

Compile/import:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe' `
  -batchmode -nographics -quit `
  -projectPath 'E:\GAYmDev_Studio\MySummerCar_Remake' `
  -logFile 'E:\GAYmDev_Studio\MySummerCar_Remake\Logs\WeatherSystemCompile.log'
```

Focused tests:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'E:\GAYmDev_Studio\MySummerCar_Remake' `
  -runTests -testPlatform EditMode `
  -assemblyNames MSC.Weather.System.Tests.EditMode `
  -testResults 'E:\GAYmDev_Studio\MySummerCar_Remake\Logs\WeatherSystemTests.xml' `
  -logFile 'E:\GAYmDev_Studio\MySummerCar_Remake\Logs\WeatherSystemTests.log'
```

## Required scene acceptance pass

After applying the staged migration to a clean scene revision, run these in a
development build with the runtime overlay and Unity Profiler connected.

### Weather sequence

1. Force ClearCool → PartlyCloudy → MostlyCloudy → BrightOvercast →
   HeavyOvercast.
2. Continue through LightDrizzle → LightRain → SteadyRain.
3. End rain and verify PostRainWet remains visibly wet.
4. Verify gradual drying under warmer, windy, brighter conditions.
5. Exercise MorningMist and LakeMist entry/exit without visibility jumps.
6. Save during a transition, restart, load before world reveal, and compare all
   debug values and the next seeded fronts.
7. Stream the current world cell out/in and confirm global revision, RNG,
   wetness, and transition progress do not reset.

### Celestial and night route

Use the in-game cheat/debug menu with date `1995-08-01` and test `12:00`,
`20:30`, `22:00`, `23:20`, `23:22`, `00:30`, and `05:00`.

1. Confirm the sun moves continuously at every clock step. In particular,
   `23:20 -> 23:22` must remain twilight/night without a bright-day-to-black
   snap.
2. At night, confirm stars are visible in clear weather and are attenuated by
   cloud cover rather than removed by a second black sky.
3. Confirm the Enviro moon follows its calculated position and phase. When a
   full or near-full moon is above the horizon in clear weather, it must provide
   restrained directional illumination; a moon below the horizon must not
   provide that illumination.
4. Repeat clear, overcast, rain, and thunderstorm at noon and night. Confirm
   exposure remains readable outdoors and powered interior fixtures remain
   visible.
5. Capture the same camera/FOV at each time so sky, exposure, sun direction,
   moon, and shadows can be compared without guesswork.

Weather buttons do not change the realistic lunar orbit. Use the Time section's
`+1 DAY` button to scan dates while keeping a clear night and fixed camera; a
moon below the horizon is not a missing renderer.

### Cloud, daylight, and dense-fog route

1. At `12:00`, compare Clear, Partly Cloudy, Overcast, Drizzle, and Heavy Rain
   from one camera. Clear must have the least cover and restrained cirrus;
   Overcast must be substantially more closed than Clear and must not emit rain.
2. Confirm daylight is slightly cooler and dimmer without crushing foliage,
   building façades, or powered interior fixtures.
3. Force Morning Mist, then Dense Fog. Dense Fog must have the shorter visible
   range, with no second fog owner, hard exposure jump, or indoor rain leak.
4. Repeat at the selected production quality tier and record CPU, render-thread,
   GPU, and frame-time deltas.

### Player house doorway

1. Stand outside a closed door, open it, and stop on the threshold.
2. Enter, close it, and walk deeper into the room.
3. Open it again from inside.
4. Verify precipitation exposure remains zero under the roof.
5. Verify fog, wind, exposure, and exterior audio change smoothly and at
   different rates.
6. Verify closed-door weather remains audible but muffled.
7. Verify debug overlay shows the expected zone, portal, direction, openness,
   strongest Outdoor path, roof exposure, and interior depth.

### Building matrix

Repeat at minimum for the player house, Teimo shop, pub, garage/workshop, a
known leaky legacy building, a one-door shed, a multi-door room, connected
rooms, and a wall-less canopy. Run the scene validator for every loaded cell.

### Vehicle

Enter a vehicle while it rains. Verify the listener remains
`VehicleInterior`, outside rain and body-rain parameters remain distinct, and
building portal state does not overwrite vehicle door/window acoustics.

## Profiling protocol

Capture the same camera path, weather seed, quality settings, resolution, and
build before and after scene migration. Record:

- main-thread time for every `Weather.*` marker;
- render-thread and GPU frame time;
- GC allocation per frame after warm-up;
- raycast count and `Weather.RoofResolve` cadence;
- portal graph rebuild frequency during streaming/door motion;
- Wwise API/RTPC calls per second;
- HDRP Volume/cloud/fog GPU cost for both backends.

`Bootstrap.unity` now selects the Enviro/HDRP hybrid while full Native remains
a reversible fallback. A
pre-migration safety copy is kept at
`Logs/Bootstrap.pre-weather-migration.2026-08-12.unity`, but no controlled
before/after CPU/GPU capture has been recorded yet. The implementation supplies
markers and allocation tests; profiling numbers remain pending rather than
being inferred from batch-mode validation.

## Known test debt

- The production integration audit understands selected-backend ownership:
  Enviro owns the celestial sky/cloud/precipitation presentation, the hybrid
  bridge owns fog/exposure/indirect lighting, and full Native validates its
  dedicated runtime-cloned Volume only when explicitly selected.
- Native HDRP now has project-owned rain/drizzle emitters, but their final
  density, drop shape, impacts and GPU budget still require visual/profile
  acceptance in a Windows Development Player.
- Existing exterior gameplay doors are bridged to the nearest explicit weather
  zone at runtime. Full player-house/Teimo/pub/garage threshold and multi-room
  route acceptance remains manual; unresolved portals fail closed and are
  reported by debug counters/validator.
- Wwise Spatial Audio Room/Portal behavior is not testable until those official
  objects and buses exist in the Wwise project.
- Native/Enviro visual calibration needs captured reference frames across
  clear, overcast, rain, post-rain, mist, sunset, blue hour, and night.
- The full production PlayMode assembly now passes all nine normal tests. The
  tenth test is the intentional live-Wwise performance capture skip; enable it
  only with `MSC_CAPTURE_M08_AUDIO_PERF=1` and
  `-wwiseEnableWithNoGraphics`.
