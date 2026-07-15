# Checklist: world и landmarks

Reuse 04A/04A1 data; не дублировать world records под новыми ID. Donor scene coordinates: Unity Y-up, 1 unit = 1 m; remake positions use documented garage-anchor offset.

## Baseline

- [ ] World origin/axes/unit-scale evidence verified.
- [ ] Known garage anchor and representative landmark transforms resolve to database records.
- [ ] Terrain extents/elevation range identified from a reviewed source; route waypoints are not terrain truth.

## Garage/home P0

- [ ] Exterior/interior wall spans and floor elevations.
- [ ] Narrowest door/gate opening width/height and open/closed pivot/angle.
- [ ] Usable ceiling height and Satsuma clearance with named vehicle state.
- [ ] Workbench and key interaction anchors.
- [ ] Roof envelope/pivot cross-check existing records; do not infer full garage from roof mesh.

## Roads and landmarks P1

- [ ] Major junction, bridge, shoreline/water and landmark positions.
- [ ] Road width at named cross-sections; shoulder/ditch distinguished.
- [ ] Certified road surface elevation and bridge deck height; traffic waypoints labelled separately.
- [ ] Player/vehicle clearances at gates/bridges.
- [ ] Representative travel distance and normal-play travel time with route and vehicle state.

## Acceptance

- [ ] Coordinate space, conversion, pivot and measurement plane are explicit.
- [ ] Visual comparisons close current `NeedsReview` landmark rows.
- [ ] Raw screenshot/video evidence remains outside Git.
