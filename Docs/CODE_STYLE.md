# C# and Unity Coding Style

- File-scoped namespaces where supported.
- One primary public type per file.
- PascalCase for types/methods/properties; camelCase for locals/parameters; `_camelCase` for private fields.
- Serialized fields are private.
- Prefer explicit units in names: `speedMetersPerSecond`, `temperatureCelsius`.
- Avoid abbreviations except established domain terms such as RPM.
- Prefer plain C# classes for simulation logic.
- MonoBehaviours own lifecycle and scene references, not every calculation.
- Do not use `Update` when a scheduled or event-driven path is sufficient.
- Do not use global singletons as the default dependency mechanism.
- Guard public boundaries and validate authoring data.
- Throw clear development exceptions for impossible states; use structured result types for expected failures.
- Keep temporary code marked with owner/replacement milestone.
- Tests should explain behavior, not internal implementation details.
