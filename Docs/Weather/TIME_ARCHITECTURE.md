# Архитектура игрового времени — Milestone 07B

Дата среза: 2026-07-17.

## Владение и поток данных

`GameTimeService` — единственный источник игрового времени. Он не читает системные часы и не принимает время обратно из Enviro.

```text
явный simulation delta
  -> GameTimeService
  -> GameTimeSnapshot
  -> WeatherDirector / WeatherEnvironmentOutputs
  -> WeatherEnvironmentFrameMapper
  -> Enviro3EnvironmentAdapter.SetDateTime (presentation-only)
```

Контракт находится в `Assets/Game/Core/Runtime/IGameTimeService.cs`, реализация и типы — в `Assets/Game/Core/Runtime/Time/`. Core assembly не ссылается на Enviro.

## Модель и единицы

- сутки содержат 86 400 игровых секунд;
- один игровой секундный тик равен 1 000 000 внутренних тиков;
- `DayLengthSimulationSeconds` задаёт длительность игровых суток при scale `1`;
- progression зависит только от переданного delta, time scale и pause;
- дата — project-owned Gregorian `GameDate`, day index отсчитывается от start date конфигурации;
- sunrise/sunset хранятся как reference inputs `[0,1)`, а не вычисляются Enviro;
- целые тики и дробный остаток сохраняются отдельно, чтобы малые шаги не терялись;
- hot path не форматирует дату/время и не создаёт строки.

Встроенная конфигурация `time.remake-default.v1` классифицирована как `RemakeDesignTarget`: donor capture `P0-TIME-PROGRESSION` отсутствует.

## События и restore

`GameTimeScheduler` использует абсолютный due tick и стабильный порядок `due tick -> insertion sequence`. Подписки и scheduled callbacks отменяются явно; callback не сериализуется. Во время clock notification нельзя реентрантно менять clock или scheduler queue. Исключение observer/scheduled callback оборачивается в `GameTimeNotificationException`, после чего clock state/revision и потреблённая due queue восстанавливаются до исходного checkpoint.

`GameTimeSaveDto` schema `1` хранит config ID, elapsed ticks, дробный остаток, time scale, pause и проверочные calendar-поля. Restore сначала валидирует весь DTO и строит кандидатный snapshot, затем атомарно заменяет состояние. Ошибка не должна оставлять частично изменённое время.

Файловое хранение не входит в 07B и остаётся Milestone 09.

## DEV управление и ошибки

Окно `Tools > MSC Remake > Time and Weather` работает только в Play Mode сцены WeatherLab. Оно позволяет задать дату/время, scale, pause и выполнить bounded advance. Один ручной advance ограничен 30 игровыми сутками; дата, scale, delta и итоговое состояние валидируются до изменения live-доменов. `AdvanceWhileRetainingPause` выполняет один clock commit и сохраняет pause без временных pause/unpause notifications.

Операция отклоняется без частичной мутации при invalid Gregorian date, NaN/Infinity, отрицательном delta, превышении 30 суток, overflow календаря или невозможности безопасно продвинуть weather/lightning checkpoint.

## Evidence

Финальный объединённый артефакт `TestResults/M07B_CoreWeatherPresentation_Final_05.xml` фиксирует `20/20 PASS` для GameTime в составе общего прогона `101/101 PASS`; `TestResults/M07B_WeatherLabTime_TransactionalCallbacks_03.xml` фиксирует `3/3 PASS` для bounded DEV advance, единиц transition и callback rollback. Подробности — в `WEATHERLAB_07B_VALIDATION.md`.
