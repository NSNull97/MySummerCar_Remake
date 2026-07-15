# 04B — runtime-проверка повторений заднего тормозного барабана

Дата ревью: 2026-07-14
Session ID: `04B-RUNTIME-DRUM-REPETITIONS-20260714`
Классификация: `BehavioralReference`, только read-only наблюдение

## Источник

- внешний пользовательский файл: `Videos/Captures/My Summer Car 2026-07-14 18-52-48.mp4`;
- SHA-256: `86ad948bda1450fb8d2cf32583b51d0c2bc55ccef5aad9f428da3bc38e8e3c84`;
- размер: `98 630 211` bytes;
- длительность: `136.000 s`;
- кадр: `1920 x 1080`;
- заявленная частота в metadata Windows: `33.04 fps`;
- donor build `20171487` взят из текущего appmanifest-контекста и не считается embedded metadata видео.

Видео осталось вне Git. Абсолютный machine-specific path в database не сохраняется. Donor installation и donor save не изменялись этим ревью.

## Метод

Видео просмотрено последовательным декодированием через Windows Media Foundation/WPF `MediaPlayer`. Извлечён внешний временный ряд по одному кадру в секунду от `00:00` до `02:16`, затем проверены полноразмерные кадры вокруг выбора инструмента и концов каждого цикла. Временные JPEG и contact sheets находились только в `%LOCALAPPDATA%/Temp` и не добавлялись в repository.

Из-за шага `1 s` границы ниже округлены примерно до секунды. Кадровое декодирование используется для классификации видимых состояний, а не для вывода физического момента, угла поворота ключа или точного числа input events.

## Наблюдаемая последовательность

| Интервал | Наблюдение | Классификация |
|---|---|---|
| `00:00–00:42` | Установлен и затянут rear-left trailing arm; в `~00:17` HUD явно показывает ключ `12`. | Подготовка prerequisites, не trial барабана. |
| `~00:43–01:20` | Барабан взят, установлен, в `~00:52` HUD явно показывает `ГАЕЧНЫЙ КЛЮЧ (14)`, выполнены две противоположные фазы работы ключом; затем барабан снова свободен и снят. | Clean trial 1. |
| `~01:21–01:51` | Повторная установка того же барабана, фаза затяжки, обратная фаза и возвращение к свободному/снятому состоянию. | Clean trial 2. |
| `~01:52–02:15` | Третья установка, две противоположные фазы работы ключом и снятие барабана. | Clean trial 3. |

Во всех трёх trials rear-left wheel отсутствует, trailing arm остаётся установленным и затянутым, барабан начинает и заканчивает цикл в свободном состоянии. Видимый outcome повторяется без расхождения. Ключ `14` явно выбран перед первым trial и визуально остаётся используемым для последующих циклов.

## Сопоставление со static trace

Runtime-наблюдение согласуется с `04B-STATIC-DRUM-FSM-20260714`:

- ключ `14` принимается целевым `BoltPM` marker;
- последовательность допускает переход из установленного незатянутого состояния в полностью затянутое, затем обратно в полностью незатянутое;
- после обратного прохода removal снова становится возможным;
- три установки и снятия имеют одинаковый результат.

Static trace остаётся источником точных дискретных правил: один marker, stages `0..8`, endpoint gates `0/8`, positive scroll — `TIGHTEN`, negative scroll — `UNTIGHTEN`. Видео подтверждает runtime-проходимость полного прямого и обратного stage progression, но не позволяет увидеть внутреннее числовое значение stage на каждом input event. Физический torque/continuous turn не заявляется: donor contract дискретный.

## Решение по coverage

### `P0-ASSEMBLY-FASTENER-SEMANTICS`: Covered

Совокупность static trace и этой runtime-сессии покрывает marker count/position, tool size, direction, discrete stage count, completion gates и три одинаковых полных прохода. Запись не вводит guessed torque или физический угол.

### `P0-ASSEMBLY-MOUNT-RULE`: Covered after operator attestation

Три успешных install/remove outcome и runtime snap подтверждены самим видео. Колесо отсутствует во всей записи, поэтому видео отдельно не показывает blocked removal. После frame review пользователь подтвердил runtime-наблюдение: установленное rear-left wheel блокирует снятие барабана; оно оформлено отдельно в `04B_VEHICLE_ASSEMBLY_BLOCKED_REMOVAL_ATTESTATION_20260714.md` с `Medium` confidence.

Static trace содержит collider-overlap candidate radius `0.01 m`, но не содержит отдельного angular compare. Поэтому точное donor angular tolerance не выдумывается: runtime snap и overlap gate покрыты, а remake orientation tolerance остаётся project-authored tuning.

Совокупность видео, user attestation и static trace переводит mount rule в `Covered`. Оба assembly prerequisite закрыты; representative fixture получает статус `Ready`. Остальные требования Vehicle Geometry/Assembly этим evidence не переоцениваются.

## Ограничения

- В кадре нет overlay входных событий, поэтому знак прокрутки берётся из exact serialized trace, а runtime-видео подтверждает только направленность двух противоположных фаз.
- Внутренние числовые stages не выведены на HUD; endpoints подтверждаются возможностью снятия после обратного прохода.
- Wheel-installed blocked case подтверждён пользователем текстом без отдельного frame-addressable видео; это evidence имеет `Medium` confidence.
- Аудио, физический torque, stripping/failure и универсальность поведения для других fasteners не измерялись.
- Это behavioral reference для одного rear-left drum, не перенос donor runtime code и не production implementation.
