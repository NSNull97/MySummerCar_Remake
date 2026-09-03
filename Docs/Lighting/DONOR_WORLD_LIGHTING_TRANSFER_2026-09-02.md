# Donor world-lighting transfer — 2026-09-02

## Scope

This pass changes only light received by world geometry:

- the Enviro-owned sun directional light color;
- clear-sky direct-light and shadow baselines;
- the flat global ambient fill;
- the HDRP indirect-diffuse multiplier applied on top of that fill.

Sky, clouds, fog, precipitation, exposure, color grading, local/interior lights,
interior-zone resolution, wetness, reflections, moon logic and the game clock are
outside this pass and remain unchanged.

## Donor evidence

Read-only source:

`raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity`

SHA-256:

`C3F2F3373CCAD4FCBE104840FCB83E364F55438070E808D11EBE4996BC0476C4`

Transferred values:

- `SUN` directional light: legacy intensity `1.75`, soft-shadow strength `0.8`;
- daytime sun color `White`: `(0.9852941, 0.95731413, 0.8838668)`;
- sunrise: `(0.42647058, 0.27595153, 0.2790658)`;
- transition orange: `(0.875, 0.59847873, 0.34742647)`;
- sunset: `(0.41911763, 0.3197791, 0.24345803)`;
- daytime flat ambient `Shadow`: `(0.28222317, 0.28222317, 0.42647058)`;
- ambient intensity `1` and two-hour PlayMaker color transitions.

The donor used Gamma color space. The runtime therefore interpolates these keys
in donor Gamma space and converts the result once for the Linear HDRP project.

## HDRP mapping and compatibility

The legacy scalar intensity `1.75` has no exact physical-lux conversion. The
existing Enviro HDRP sun curve (roughly 0–100,000 lux) remains authoritative;
only its clear-sky multiplier is normalized from `0.98` to `1.0`. Existing cloud
attenuation remains intact so overcast weather still suppresses direct light and
hard shadows. Existing phase-aware moon lighting also remains intact.

The former exterior indirect boost (`1.25–1.40`) is neutralized to `1.0`, matching
the donor ambient intensity instead of brightening it a second time. Interior
occlusion still applies through the existing production bridge.

The adapter writes no vendor asset and restores the prior `RenderSettings`
ambient values when its runtime isolation is torn down. The hot path allocates
nothing, creates no extra lights, requests no dynamic-GI refresh and adds no
render pass.

## Automated verification

- `DonorWorldLightingPolicyTests`: `14/14` passed.
- `HybridEnvironmentMathTests`: `29/29` passed.
- `Enviro3EnvironmentAdapterTests`: `8/8` passed.

A targeted production `Bootstrap` PlayMode run was attempted after these checks.
The first attempt was stopped by a concurrent unrelated vegetation compile error
(`MapVegetationGrassBindings.cs(80,23): CS0246 MixtureAudit`). After that error
was resolved by its owning work, the retry compiled and entered PlayMode, then
failed later on an unrelated shelter-topology assertion in
`ProductionEnvironmentLifecyclePlayModeTests.cs:477`: expected `13` active home
removal/shelter volumes, found `20`. No passing PlayMode result is claimed; the
focused lighting EditMode suites above remain green.

Results are stored under `Artifacts/Tests/` and remain local build evidence.

## Manual visual gate

In `Bootstrap`, use the existing environment debug menu and compare the same
outdoor matte surface at `05:00`, `07:00`, `12:00`, `19:00`, `21:00` and `23:00`.
The expected result is warm-white daytime sunlight, donor-like orange transitions,
blue-violet flat daytime fill, `0.8` clear-sky shadows and a fade to black ambient
at night. No sky, cloud, fog, rain, exposure or indoor-light behavior should change.
