# Руководство по ручному reference capture

## 1. Безопасность и допустимые действия

Donor installation и donor saves всегда read-only. Запрещены injection, patching, memory editing, console forcing, DRM/access-control bypass и изменение файлов установки. Этот workflow использует обычный запуск лицензированной игры, внешнюю запись экрана/звука, визуальные маркеры и read-only inspection.

Перед сессией закройте игру и сделайте резервную копию save directory средствами ОС в отдельную внешнюю папку, если это законно и технически уместно. Не заменяйте и не удаляйте исходный save. Для эксперимента используйте отдельную заранее подготовленную копию профиля только через штатно поддерживаемый игровой workflow. Если нельзя гарантировать отсутствие записи, проводите лишь наблюдения, не требующие смены состояния, и пометьте остальные строки `Blocked`.

После сессии сравните timestamps/hashes исходных donor files и защищённого baseline save. Любое неожиданное изменение прекращает сессию; данные не переводятся в `Validated` до разбора.

## 2. Паспорт сессии

До запуска заполните новую строку `CAPTURE_SESSION_LOG.csv`:

- уникальный `session_id` вида `MSC-20171487-YYYYMMDD-HHMM-CATEGORY`;
- donor build ID, Unity version и hash проверяемого source container;
- категория, оператор, часовой пояс;
- display resolution, graphics preset, FOV, VSync/frame cap;
- input device, sensitivity/dead zones и bindings;
- audio master/submix settings и recording chain, если применимо;
- логическое имя save-state и его prerequisites без копирования save в Git;
- evidence root вне Git.

Текущий baseline локально модифицирован и не объявляется clean stock. Это ограничение должно переноситься в каждую запись.

## 3. Настройки игры и захвата

1. Зафиксируйте resolution, aspect ratio, graphics quality, FOV и frame cap скриншотом меню.
2. Не меняйте настройки между trials одной серии.
3. Для timing используйте запись не ниже 60 fps, constant frame rate, с видимым/слышимым input marker.
4. Для distance/angle используйте неподвижную камеру, известную плоскость, минимум два масштабирующих ориентира и ортогональный ракурс, когда возможно.
5. Для audio выключите processing/auto gain, запишите sample rate/bit depth и не сравнивайте абсолютный SPL без калиброванного тракта.
6. Любая пауза, frame drop, смена окна или потеря input marker делает trial недействительным и явно логируется.

Не устанавливайте сторонний capture tool в рамках milestone без отдельного разрешения. Можно использовать уже установленный штатный screen/audio recorder; имя и версию запишите в session log.

## 4. Save-state prerequisites

Для каждого checklist укажите:

- игровой день/время и погоду;
- местоположение игрока и автомобиля;
- установленные детали, fasteners, fluids, electrical/fuel state;
- температуру двигателя, tire/wheel variant, cargo/load;
- needs/damage/wear, если они влияют на опыт;
- что должно оставаться неизменным между trials.

Не редактируйте save вручную ради получения состояния. Если повторяемый baseline нельзя получить штатно без необратимой записи, пометьте capture `Blocked`, опишите причину и сохраните только безопасные наблюдения.

## 5. Камера и scale

Для геометрии:

1. Назовите объект и иерархический locator; имя само по себе не доказывает identity.
2. Запишите coordinate space и оси.
3. Разместите два независимых известных ориентира в той же плоскости, что измеряемая грань.
4. Делайте фронтальный, боковой и верхний кадр без изменения FOV.
5. Храните raw pixel endpoints отдельно; normalized meters вычисляйте отдельной `DerivedCalculation` записью со ссылками на inputs.
6. Учитывайте perspective/parallax. Если ортогональность не доказана, confidence не выше `Medium`.

Для travel distance/time сначала задайте start/end gates. Distance может быть scene-transform measurement, размеченный маршрут или производная от проверенной скорости/времени; метод нельзя смешивать в одной серии.

## 6. Timing и повторные trials

Start/end events определяются до записи: например, первый кадр движения input и первый кадр пересечения speed threshold. Время вычисляется как `frame_delta / actual_capture_fps`; dropped/duplicated frames проверяются по metadata или таймкоду.

Минимум:

- 5 trials для коротких movement/start/interaction/audio событий;
- 7 trials для acceleration/braking;
- 3 длительных наблюдения для time/weather;
- больше trials при широком разбросе или редких состояниях.

Храните каждый raw trial. Публикуйте median, min/max и число валидных/отбракованных trials. Не удаляйте неудобные результаты без записанной причины.

## 7. Uncertainty и confidence

Для каждого результата запишите:

- instrument/display resolution;
- marker placement error;
- timing frame granularity и reaction error;
- state uncertainty;
- абсолютную tolerance в той же единице и, при необходимости, relative percent;
- правило округления.

`Exact` нельзя использовать для manual/video/screenshot approximation. Если object identity, state или conversion неоднозначны — `NeedsReview`. Если значение надёжно не измеряется — `Missing` с описанием следующего безопасного шага, а не приблизительное число.

## 8. Evidence naming и хранение

Шаблон имени:

```text
<session-id>__<category>__<fixture>__<trial-###>__<view-or-event>.<ext>
```

Пример: `MSC-20171487-20260714-1900-VEHICLE__vehicle__braking-40-0__trial-003__side.mp4`.

Raw evidence находится вне Git под выбранным session root. Не используйте имя donor person/profile. Для каждого файла сохраните SHA-256, размер, UTC timestamp и logical relative path. Производные thumbnails/CSV также связываются с raw evidence, но donor screenshot/audio/video не добавляются в production `Assets`.

## 9. Ввод результата

1. Откройте `Tools → MSC Remake → Reference Capture`.
2. Проверьте Missing P0/P1 и нужный category checklist.
3. Для одиночного числа используйте `Add Manual Observation`; укажите unit, coordinate space, raw observation, tolerance, evidence path и honest confidence.
4. Для серии подготовьте import envelope по `ReferenceCaptureImportTemplate.json`.
5. Не заменяйте donor measurement tuning-значением. Настройка ремейка добавляется отдельно в `ReferenceTuningOverrides.json` с rationale.
6. Выполните `Validate Database`, обновите index/missing/session log и проверьте Git diff на donor payload и absolute paths.

## 10. Ненадёжные и недоступные данные

Если измерение нельзя получить безопасно или воспроизводимо:

- status: `Missing` или `Blocked`;
- confidence: `Unknown`;
- value: observation `MISSING`, без guessed numeric value;
- notes: конкретный blocker, проверенные варианты и следующий безопасный метод;
- requirement остаётся в `MISSING_REFERENCE_DATA.csv`;
- fixture не переводится в `Ready`.

Это полноценный результат capture milestone: честная граница лучше ложной точности.
