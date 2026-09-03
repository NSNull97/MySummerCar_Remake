# Audio naming and SoundBank policy

Status: **ActivePolicy / AuthoringAutomatedValidated**

## Stable project IDs

Runtime/gameplay uses project-owned lowercase dot-separated IDs:

| Kind | Format | Example |
|---|---|---|
| Event | `audio.event.<domain>.<object>.<action>` | `audio.event.vehicle.engine.started` |
| Parameter | `audio.parameter.<domain>.<name>` | `audio.parameter.vehicle.rpm` |
| Switch group/value | `audio.switch.<domain>.<group>[.<value>]` | `audio.switch.vehicle.surface.gravel` |
| State group/value | `audio.state.<domain>[.<group>][.<value>]` | `audio.state.vehicle.engine.running` |

IDs never contain scene names, cell coordinates, donor hierarchy paths, Unity
instance IDs or Wwise short IDs. Renaming a stable ID requires an explicit alias
or migration. Empty and duplicate IDs are validation failures.

## Wwise backend names

Backend names are authoring data in maps, not gameplay string constants:

| Kind | Pattern | Example |
|---|---|---|
| Play event | `Play_MSC_<Domain>_<Object>` | `Play_MSC_Vehicle_Engine_Intake` |
| Stop event | `Stop_MSC_<Domain>_<Object>` | `Stop_MSC_Vehicle_Engine` |
| RTPC | `MSC_<Domain>_<Parameter>` | `MSC_Vehicle_RPM` |
| Switch group | `MSC_<Domain>_<Group>` | `MSC_Vehicle_Surface` |
| Switch/state value | concise PascalCase | `Gravel`, `Interior`, `Running` |
| State group | `MSC_<Domain>` or `MSC_<Domain>_<Group>` | `MSC_Environment` |
| Bank | `MSC_<Domain>` | `MSC_Weather` |

`AUDIO_EVENT_MATRIX.csv` and `AUDIO_PARAMETER_MATRIX.csv` list only IDs declared
by `AudioProjectIds`. A row marked `DeferredHook` is not a claim that a runtime
producer or final Wwise object/content already exists.

Verified authoring contains 65 distinct events, 32 RTPCs, 4 switch groups and
3 state groups. Runtime aliases do not create duplicate Wwise events.

## Work Units

Use domain folders/Work Units for Vehicle, Weather, World, Interaction and UI,
with shared parameters/switches/states kept reviewable. Do not create one Work
Unit per sound, import donor Work Units, or couple a Work Unit path to runtime
identity. Wwise XML diffs require normal code review.

## Banks

| Bank | Lifetime/purpose | Status boundary |
|---|---|---|
| `Init.bnk` | mandatory Wwise initialization metadata; session | generated/verified, `1,877 B` |
| `MSC_Vehicle` | vehicle layers and transitions; vehicle/session | generated/verified, `1,992,969 B` |
| `MSC_Weather` | rain, wind and thunder; session | generated/verified, `12,554,129 B` |
| `MSC_World` | streamed ambience/room tones | generated/verified, `93,600,920 B`; full donor phase ambience and local gameplay hooks imported; final bank streaming/compression pending |
| `MSC_Interaction` | interaction one-shots, player footsteps and later surfaces/tools | generated/verified, `1,179,302 B` |
| `MSC_UI` | later UI/menu events | generated empty reservation, `145 B`; no donor gameplay content |

Do not split banks further without a measured streaming or memory reason. World
bank leasing and cell-specific content remain future composition work; scene
unload already removes scene-owned emitters/voices.

## Generated content

The Wwise project is
`MySummerCar_Remake_WwiseProject/MySummerCar_Remake_WwiseProject.wproj`.
Generated banks/caches are ignored under both the Wwise output root and Unity's
runtime-copy root. Do not commit `.bnk`, `.wem`, generated metadata, profiler
captures, `.cache*`, local `.wsettings`, validation cache or temporary donor
originals. Record sizes/hashes as reports, not binaries.

Project-authored `.wproj`/`.wwu` data and newly authored sources may be reviewed
for version control separately from generated banks and the official vendor
integration. The verified Windows bank total is `109,329,342 B`; current
per-file hashes are recorded in `WWISE_SETUP.md`. `PERFORMANCE_REPORT.md`
preserves the smaller accepted M08 performance baseline until the expanded
world bank receives a new real-device profiling pass.

Authoring also contains six mixer buses, exactly six normalized Volume RTPC
curves, seven routed Actor-Mixer roots and explicit child routing `65/65`.
The footstep Switch Container validates 10 sounds, 5 random pairs and `9/9`
assignments. Generated output and caches remain ignored even though they are
validated.

## Missing content

A missing bank, event mapping or RTPC mapping produces a structured diagnostic.
It must not substitute an unrelated sound. The router may select the ready Unity
fallback; otherwise audio is silent and gameplay proceeds. Enviro audio remains
disabled in every path.

## Donor/local prototype content

Donor gameplay clips used privately are `TemporaryDirectImport`, hash-ledgered,
local-only and ignored. They do not become production-ready by entering Wwise.
Final gameplay audio is newly authored. Menu/UI audio will be a different,
newly authored set in the UI milestone.
