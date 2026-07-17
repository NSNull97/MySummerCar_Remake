# Модель weather front, timeline и seed

Дата среза: 2026-07-17. Версия project-owned RNG: `1` (`PCG32`). `UnityEngine.Random` не используется.

## Непрерывность фронта

Планировщик выбирает не независимые случайные пресеты, а допустимую цепочку профилей:

```text
clear -> partly cloudy -> overcast -> drizzle/rain
      -> heavy rain/thunderstorm -> overcast -> breakup/clear
      -> morning mist (только разрешённый сосед и контекст)
```

Для каждого сегмента RNG определяет successor, hold duration и transition duration в диапазонах profile. `WeatherTransition` интерполирует числовые outputs. Дискретные поля переключаются по зафиксированной границе. Большой bounded шаг может пройти несколько сегментов и обязан дать тот же domain state, что эквивалентная последовательность малых шагов в рамках целочисленной timeline-модели.

Когда automatic transition меняет presentation binding, WeatherLab переводит оставшуюся project-owned duration из игровых секунд в simulation seconds через активные `DayLengthSimulationSeconds` и `TimeScale`. Enviro использует приблизительный exponential blend; он не становится владельцем timeline. Если coarse DEV advance перескочил остаток целиком, adapter всё равно получает минимум `0.25` simulation seconds, чтобы не переходить на visible instant path.

## Состояние детерминизма

Для воспроизведения сохраняются:

- schedule/config ID;
- current/target profile и front state;
- simulation seconds, timeline cursor и transition progress;
- RNG version/state/increment;
- next override sequence;
- persistent overrides.

Одинаковые versioned DTO, каталог профилей и seed должны воспроизводить project-owned progression. Рендер, PhysX и внутренний покадровый blend Enviro детерминированными между машинами не объявляются.

## Overrides и freeze

Freeze останавливает schedule progression, но не уничтожает cursor/RNG. Override изменяет видимое логическое состояние по приоритету, пока underlying timeline продолжает хранить собственное состояние. После удаления override adapter получает переход к открытому schedule state.

## Failure modes и ограничения

- нулевой/невалидный config ID, неизвестный profile, invalid RNG version или NaN/Infinity отклоняются до commit;
- несовместимая transition topology не исправляется случайным fallback;
- алгоритм RNG и schema version должны мигрироваться явно;
- текущая модель не реализует сезоны, снег и географически независимые фронты;
- численные диапазоны пока `RemakeDesignTarget`, а не donor measurement.
