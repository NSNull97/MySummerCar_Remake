/plan

Read `AGENTS.md`, prior reports, and `Docs/AUDIO_GUIDE.md`.

Complete **Milestone 8 only: Wwise integration and audio prototype**.

Before changing the project:

1. Inspect whether the official Wwise Unity integration is already installed.
2. Report its exact version.
3. If absent, do not install it silently. Provide the exact official setup action required and complete all backend-independent work first.

Implement/finish:

- `IAudioBackend` contract;
- Unity fallback backend;
- Wwise adapter only when official types exist;
- parameter/event mapping configuration;
- missing-event validation;
- engine RPM/load/throttle/gear/clutch/speed/wheel-slip parameters;
- interior/exterior blend;
- rain and ambience prototype;
- documentation of Wwise project/bank locations.

Do not fake Wwise classes. Do not commit caches/generated banks unless an ADR explicitly approves it.

Create `MILESTONE_08_REPORT.md` and stop.
