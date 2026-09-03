# Donor global lighting reference — 2026-08-15

## Scope and evidence

The four home-road images supplied on 2026-08-15 are captures from the donor
game. They are visual references for global exterior lighting, atmosphere and
color response. They are not captures of the remake and must not be used to
diagnose a remake garage circuit, fixture or streaming failure.

This pass changes only the global Enviro/HDRP presentation contract. Local
fixtures, switches, electrical consumption, fuses, billing, stable IDs and
streamed-light generators are outside this correction.

## Accepted visual target

- daylight and rainy skies are restrained blue-grey rather than cyan or gold;
- broad overcast can have a bright sky while roads and vegetation remain
  substantially darker, but tree trunks and ground detail stay readable;
- road, soil, timber and artificial-light warmth is preserved;
- rain adds cool aerial depth without a full-frame blue color filter;
- weather changes direct/indirect balance rather than pumping camera exposure
  from whatever object happens to fill the frame.

Read-only image sampling of the supplied donor references produced approximate
sky luminance values of 91-152 and road luminance values of 41-111 on an 8-bit
display image. These are comparison landmarks, not physical photometry.

## Runtime calibration

- sole HDRP fixed-exposure owner: `NativeHdrpWeatherBridge`;
- summer daylight baseline: `12.0 EV`;
- readable night floor: `7.25 EV`;
- clear/clouded direct-sun multipliers: `0.98 / 0.22`;
- exterior diffuse-indirect range: `1.25-1.40`;
- Enviro direct sunlight: cool-neutral daylight;
- Enviro ambient/cloud fill: a stronger steel-blue daylight bias;
- the authoritative hybrid HDRP volume applies weather-specific daytime
  `ColorAdjustments` and `WhiteBalance` after Enviro has rendered the sky;
- clear weather receives a restrained steel-blue filter, overcast remains
  nearly neutral and desaturated, and rain becomes progressively cooler;
- grading fades to zero at night and to 35% inside closed interiors so local
  electric-light colors remain readable.

The earlier gradient-only correction did not materially change the final HDRP
frame because it affected Enviro source gradients without owning the last
color-processing stage. The hybrid bridge now creates and reasserts the final
grading components in its runtime-owned volume clone.

The profile asset, Bootstrap serialization and deterministic migration tool all
resolve the same daylight EV through `NativeHdrpExposureMath`.

## Acceptance still required

Capture the remake from the same home-road camera at matching clock and weather
states: clear/broken cloud, bright overcast, dense overcast and rain. Compare sky
brightness, road readability, foliage hue and preservation of warm materials.
This code/test pass can verify ownership and deterministic values, but only an
HDRP graphical run can approve the final visual match.
