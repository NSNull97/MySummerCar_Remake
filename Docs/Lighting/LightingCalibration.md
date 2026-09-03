# Lighting calibration

Unity is pinned to 6000.3.11f1 with HDRP 17.3.0. Enviro 3 remains the only
sky/cloud/weather presentation owner. `Enviro3EnvironmentAdapter` keeps
`controlExposure = false`; project-owned `NativeHdrpWeatherBridge` is the sole
runtime HDRP Exposure/Fog writer.

The donor-reference global calibration now uses:

- day: 12.0 EV;
- readable night: 7.25 EV;
- dusk-on threshold: sun elevation -2.5 degrees;
- dawn-off threshold: sun elevation +0.5 degrees.

The active hybrid bridge remains the only HDRP Exposure/Fog/indirect owner.
Enviro owns the sky, clouds and celestial presentation. Daylight sky fill uses
a restrained steel-blue bias while direct sunlight stays closer to neutral.
The hybrid bridge owns the final weather-specific `ColorAdjustments` and
`WhiteBalance`: clear weather is moderately steel-blue, overcast is neutral and
desaturated, and rain is progressively cooler. The grade fades out at night and
is reduced inside so roads, timber and electric sources retain their authored
warmth. Cloud cover keeps a 0.22 direct-light floor and exterior diffuse
indirect spans 1.25-1.40, matching the donor behavior where a bright grey sky
does not reduce the terrain to black.

This lighting pass did not compensate with absurd light output or add a second
Exposure owner. Typical authored sources remain period-appropriate: domestic
incandescent 2700 K / 760 lm, enclosed ceiling 2900 K / 1050 lm, fluorescent
4000 K / 2800 lm, street 3100 K / 6100 lm, low beam 3200 K / 19000 cd and high
beam 3400 K / 43000 cd.

No Enviro, HDRP package, NWH, Wwise or VLB vendor source was edited. Final EV,
leak, glare and material-emission acceptance still requires the controlled
home/store/Fleetari/road/vehicle screenshot matrix on a graphical HDRP run.
