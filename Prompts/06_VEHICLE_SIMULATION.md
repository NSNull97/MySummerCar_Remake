/plan

Read `AGENTS.md`, prior reports, `Docs/VEHICLE_SYSTEM.md`, donor measurements, and reference captures.

Complete **Milestone 6 only: vehicle simulation prototype**.

Implement separated testable components:

- engine;
- clutch;
- gearbox;
- differential;
- wheel/brake prototype;
- starter and stall behavior;
- simple thermal state;
- prerequisites from installed parts where available;
- presentation adapter;
- `IWheelPhysicsBackend` with a simple prototype implementation.

Use an explicit powertrain graph. Do not create one giant car controller.

Calibrate only against documented donor values/captures. Record assumptions and tolerances.

Tests:

- torque/RPM behavior;
- clutch transfer/slip;
- gear ratios;
- differential distribution;
- start/stall prerequisites;
- braking/drive smoke test;
- tolerance comparison fixtures.

Create a short drivable prototype and performance capture. Stop after `MILESTONE_06_REPORT.md`.
