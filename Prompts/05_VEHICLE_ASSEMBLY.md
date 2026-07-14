/plan

Read `AGENTS.md`, prior milestone reports, `Docs/VEHICLE_SYSTEM.md`, and donor porting records.

Complete **Milestone 5 only: vehicle assembly**.

Implement the minimal robust architecture for:

- `PartDefinition`;
- `PartInstance`;
- `MountPoint`;
- compatibility and placement tolerances;
- `FastenerDefinition`/`FastenerState`;
- tool compatibility;
- `AssemblyGraph`;
- `VehicleAssemblyController`;
- stable IDs and save DTOs;
- 12–20 representative parts in the prototype scene.

Preserve validated donor pivots and mount coordinates. Use rebuilt production meshes.

Tests:

- valid/invalid mounting;
- orientation/position tolerance;
- blocked dependency;
- fastener tightening/tool mismatch;
- install/uninstall graph consistency;
- save/load round trip for part/fastener state.

Do not implement detailed engine physics yet.

Create `Docs/Milestones/MILESTONE_05_REPORT.md` and stop.
