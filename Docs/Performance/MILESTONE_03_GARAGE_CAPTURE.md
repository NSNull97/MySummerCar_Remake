# Performance Capture — Milestone 3 Garage Art Prototype

Дата: 2026-07-14

Сцена: `Assets/Game/World/Content/GaragePrototype/Scenes/GarageArtPrototype.unity`

Результат: **предварительный CPU-side бюджет пройден; GPU frame time не получен и не заявляется**.

## Методика

- Отдельный Windows x64 Development Player содержал только production-сцену Milestone 3.
- Unity `6000.3.11f1`, HDRP `17.3.0`, профиль качества `High Fidelity`.
- Direct3D 12, `1920x1080`, VSync выключен, `Application.targetFrameRate = -1`.
- Скрытое окно не использовалось как backbuffer-эталон: каждый кадр принудительно отрисовывался через поддерживаемый HDRP `RenderPipeline.StandardRequest` в `1920x1080` ARGB32 sRGB RenderTexture.
- Выполнено 180 прогревочных и 600 измеряемых кадров.
- Итоговый frame time — максимум между временем синхронной отправки render request, `Time.unscaledDeltaTime` и доступным CPU FrameTiming.
- Контрольный PNG последнего RenderTexture проверен визуально: геометрия гаража, terrain, дорога и растительность присутствуют.

Методика не измеряет стоимость Present и не дала GPU FrameTiming на текущем D3D12-драйвере. Поэтому число FPS ниже — индикатор сложности текущей сцены на CPU/render-submit path, а не обещание полной производительности будущей игры.

## Аппаратная конфигурация

| Параметр | Значение |
|---|---|
| ОС | Windows 10 64-bit, build 19045 |
| CPU | AMD Ryzen 9 5950X, 16 ядер / 32 логических процессора |
| GPU | NVIDIA GeForce RTX 4070 SUPER |
| VRAM | 11 999 MB |
| API | Direct3D 12 |
| Разрешение | 1920x1080 |

## Player capture

| Метрика | Результат |
|---|---:|
| Среднее время кадра | 3,249 мс |
| Медиана | 2,902 мс |
| p95 | 7,458 мс |
| p99 | 9,474 мс |
| Худший кадр | 14,537 мс |
| Среднее CPU FrameTiming | 2,962 мс |
| p95 CPU FrameTiming | 3,276 мс |
| GPU FrameTiming | недоступен (`0`) |
| Расчётный FPS по среднему CPU-side frame time | 307,8 FPS |
| Размер Development Player | итоговый каталог 217 388 044 байта |

Предварительный бюджет 60 FPS равен 16,667 мс. Даже худший измеренный CPU-side кадр находится ниже него, поэтому Milestone 3 проходит предварительный CPU-бюджет. Полный GPU/present замер остаётся обязательным для будущего вертикального среза.

## Статическая сложность сцены

| Метрика | Результат | Бюджет |
|---|---:|---:|
| Renderer | 142 | 300 |
| Уникальные Mesh | 11 | — |
| Вершины | 4 295 | — |
| Треугольники | 7 904 | 100 000 |
| Уникальные материалы в сцене | 11 | — |
| Уникальные текстуры в сцене | 33 | — |
| Оценка несжатого mip-resident объёма текстур | 2 883 573 байта | 33 554 432 байта |
| Collider | 70 | минимум 12 |
| LODGroup | 36 | минимум 3 |
| Light | 1 | 8 |
| Reflection Probe | 1 | — |

## Локальные артефакты

Артефакты намеренно исключены из Git:

- `PerformanceCaptures/Milestone03/GaragePrototype_Static.json`;
- `PerformanceCaptures/Milestone03/GaragePrototype_Player_1080p.json`;
- `PerformanceCaptures/Milestone03/GaragePrototype_Player_1080p.png`;
- `Builds/Milestone03/`;
- `Logs/Milestone03_Performance*.log`.

## Воспроизведение

1. Выполнить `MSC.Editor.GaragePrototype.GaragePrototypeAssetBuilder.RunBatch`.
2. Выполнить `MSC.Editor.GaragePrototype.GaragePrototypeValidator.RunBatch`.
3. Выполнить `MSC.Editor.GaragePrototype.GaragePrototypePerformanceBuild.RunBatch`.
4. Запустить player с аргументами `-screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -msc-m3-capture <absolute-json-path>`.
