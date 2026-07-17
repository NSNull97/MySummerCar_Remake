# Time/weather DTO schema — Milestone 07B

DTO принадлежат соответствующим domain assemblies. `MSC.Save.Runtime` позднее оркестрирует файлы и миграции; домены не ссылаются на Save и не образуют цикл. Финальное файловое хранение — Milestone 09.

## GameTimeSaveDto — schema 1

- config ID;
- elapsed game ticks и fractional remainder;
- time scale и pause;
- day index, date и time-of-day ticks как проверочные поля exact restore.

## WeatherDomainSaveDto — schema 1

Wrapper config `weather.domain.save.v1` содержит:

- `WeatherSaveDto`: schedule/config IDs, simulation seconds, freeze, current/target profile IDs, front/transition durations, elapsed progress, timeline cursor, RNG version/state/increment, next override sequence, revision и serializable overrides;
- `WetnessSaveDto`: config ID, ground/road/puddle/vegetation и revision;
- `LightningSaveDto`: config ID, simulation time, global cooldown, restore grace, gameplay sequence, non-lethal flag, RNG state и candidate records (`StableId`, cooldown, last gameplay sequence, strike count).

Override DTO хранит stable override ID, owner/reason, priority, lifetime, requested profile, serialization policy и stable sequence.

Во внешний DTO допускаются только overrides с policy `Save`. `Transient` используется DEV/test/reference-capture runtime scope, не попадает в capture и отклоняется при decode до изменения live state.

## Запрещённые данные

Не сериализуются:

- Enviro objects/assets/prefabs;
- vendor array indices и runtime clones;
- display-name lookup;
- Unity scene instance ID;
- текущий VFX/ripple frame;
- callbacks и presentation-only diagnostics.

## Restore contract

Каждая операция выполняет `parse -> validate complete candidate -> atomic commit`. Composite weather restore сохраняет предыдущие weather/wetness/lightning snapshots и откатывает их при ошибке. Invalid schema, config/profile ID, RNG version, duplicate candidate/override ID, NaN/Infinity или out-of-range значение не должны оставлять partial state.

Game time сохраняется отдельным DTO; production save coordinator Milestone 09 обязан применять согласованный restore ordering без visible clear-sky flash.
