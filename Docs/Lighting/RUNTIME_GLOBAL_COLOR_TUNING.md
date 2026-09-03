# Accepted global color calibration

The runtime visual calibration accepted from the in-game comparison on
2026-09-02 is now the code-owned default in `NativeHdrpWeatherBridge`:

- grade strength: `1.36`;
- temperature offset: `27.14`;
- tint offset: `2.76`;
- contrast offset: `0.75`;
- saturation offset: `1.21`;
- exposure offset: `+0.41 EV` (higher is darker).

The temporary runtime sliders and their `Reset` / `Copy values` buttons were
removed after acceptance. The remaining environment diagnostic overlay is
read-only and does not modify lighting. Old serialized runtime-tuning fields in
existing scenes are intentionally ignored by the code-owned constants.

The existing programmatic tuning API remains for source compatibility with
development tooling, but no in-game UI invokes it. Its reset operation restores
the accepted production values rather than the former neutral values.

This calibration affects only the global HDRP color grade and fixed exposure.
It does not change local lights, the electrical simulation, weather scheduling,
sky ownership or precipitation.
