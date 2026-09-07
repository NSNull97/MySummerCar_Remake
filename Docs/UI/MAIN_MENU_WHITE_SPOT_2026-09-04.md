# Main-menu white spotlight — 2026-09-04

Status: implemented and compiled; Unity PlayMode validation passed 40/40.
User visual approval is pending.

The user requested a much stronger white spotlight aimed at the car. The
existing garage fixture supplies the source: its menu-owned Point Light becomes
a Spot Light aimed at the real Satsuma display bounds centre. No new image,
geometry, world light, gameplay clock or material asset is introduced.

The cone is 80 degrees with a 60-degree inner cone and 12 m range. The emitter
sits 0.30 m toward the yard from the existing explicit lamp anchor, clear of
its housing. Shadows remain soft, with a 1024 shadow map. The neutral white
light disables color-temperature tint and uses an explicit native intensity
of 120000 candela. This is ten times the preceding point light's actual
12000-candela intensity. That earlier code displayed Lumen as its unit, but in
this installed Unity/HDRP version the Light.intensity property itself stores
candela for punctual lights; this revision labels and assigns it explicitly.

The existing local-time lamp factor remains 0.35 in daylight through 1 at
night. The spotlight therefore stays white and ten times stronger at the same
time of day; only its intensity follows that factor. The shade uses neutral
linear RGB emission (6000,6000,6000), still modulated by its existing albedo
through a renderer-only MaterialPropertyBlock. The shared atlas is unchanged.

Files: `Assets/Game/UI/Presentation/Runtime/MainMenuVehiclePreview.cs` and the
existing `GameUiRootPlayModeTests.MainMenuAtmosphere.cs` helper. Its three-quality
checks now require a white Spot Light aimed at the car and all eight display
bounds corners inside the cone. Existing actual-pixel, fog, time-of-day,
pause/settings, paint and orbit checks remain available in the same PlayMode
suite. The pure clock calculation and its EditMode tests are unchanged.

No baseline rebuild, scene rewiring, save migration or package installation is
required. The previous 54 EditMode / 40 PlayMode result predates this spotlight
and is retained as historical evidence; EditMode was not rerun for this change.

Current validation: Unity 6000.3.11f1, request
`home-white-spot-play-20260904-01`, completed 2026-09-04 17:12:45 UTC.
All 40 PlayMode tests passed in 56.5280321 seconds, with zero failures, skips
or inconclusive cases. The result JSON and matching test XML are under
`Artifacts/MainMenuRedesign/`; the Editor log is
`HomeEnvironment.WhiteSpotGraphical.01.log`.

The executed suite covers spotlight direction and full-car cone coverage,
lamp/fog rendering at the supported quality levels, orbit, paint, menu
lifecycle, Pause/Settings and local-time renders. Native midnight inspection
shows a white roof/rear-body highlight and a white pool on the yard, without
visible highlight clipping. Car-region mean luminance is 0.023369 at midnight,
0.050261 at 20:00 and 0.171927 at noon. Ten times the source intensity does not
mean ten times the whole image brightness: other lights, incidence angle and
occlusion also contribute. No performance or standalone-build claim is made.

Compatibility: completed menu/world/vehicle integration remains intact. No
manual asset authoring or migration is required; inspect the result in the
main menu during normal Play Mode.

Next milestone: user review of the brighter car spotlight in the main menu.
