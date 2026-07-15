# Формат базы reference capture

## Назначение и границы

`ReferenceCaptureDatabase` — проектный, версионируемый источник измерений и наблюдаемого поведения. Он читается без donor assets и не является игровой конфигурацией. Значения донора находятся в `ReferenceCaptureDatabase.json`, а осознанные настройки ремейка — только в отдельном `ReferenceTuningOverrides.json`.

Текущая версия схемы — `1`, версия набора — `04B.4`. Формат основан на UTF-8 JSON, сериализуется Unity `JsonUtility` и хранит массивы в стабильном порядке. Инструмент перед сохранением сортирует sources, evidence, measurements, behavior fixtures и requirements по стабильным ключам. Изменение смысла поля требует новой версии схемы и мигратора в `ReferenceCaptureDatabase.MigrateToCurrent`.

## Файлы

| Файл | Содержимое | Входит в runtime build |
|---|---|---|
| `Assets/Game/Core/Configuration/ReferenceCapture/ReferenceCaptureDatabase.json` | Источники, evidence, измерения, behavioral fixtures и очередь требований | Да, как проектные данные без donor payload |
| `Assets/Game/Core/Configuration/ReferenceCapture/ReferenceTuningOverrides.json` | Отдельные настройки ремейка и причины расхождений | Да |
| `Assets/Game/Core/Configuration/ReferenceCapture/Fixtures/*.json` | Небольшие calibration fixtures | Да |
| `Assets/Game/Core/Configuration/ReferenceCapture/ReferenceCaptureImportTemplate.json` | Контракт структурированного импорта | Editor-only workflow |
| `Docs/ReferenceCapture/REFERENCE_DATABASE_INDEX.csv` | Читаемый индекс записей | Нет |
| `Docs/ReferenceCapture/MISSING_REFERENCE_DATA.csv` | Очередь незакрытых данных | Нет |

Donor-файлы, AssetRipper-представления, видео, скриншоты и записи звука не копируются в эти файлы. В базе находятся только логические пути, hashes и описания evidence.

## Корневая схема

```text
ReferenceCaptureDatabase
  version: ReferenceDatasetVersion
  sources[]: ReferenceSourceRecord
  evidence[]: ReferenceEvidenceRecord
  measurements[]: MeasurementRecord
  behaviorFixtures[]: BehaviorFixture
  requirements[]: ReferenceRequirementRecord
```

`ReferenceRecord` — общая часть измерения или behavioral fixture:

- `stableId`: 32 lowercase hex; проектный ID, не Unity InstanceID и не donor PathID;
- `category`, `subcategory`, `name`, `priority`;
- `normalizedValue`: `Numeric`, `Vector3`, `Curve`, `Enum`, `String` или `Observation`;
- `unit`, `coordinateSpace`;
- `sourceId`, `sourceLocator`, `captureMethod`, `captureDateUtc`;
- `confidence`, `tolerance`, `rawObservation`, `notes`;
- `evidenceIds[]`;
- `dependencyRecordIds[]` для производных значений;
- `validationStatus`: `Validated`, `NeedsReview`, `Missing` или `Rejected`.

Стабильный ID создаётся из канонического логического ключа методом `ReferenceStableIdUtility.Create`. Переименование текста для UI не должно менять ID. Новый смысл записи получает новый ID; старый ID сохраняется для миграции или помечается `Rejected`.

## Значения, единицы и координаты

Поддерживаемые единицы перечислены в `ReferenceUnit`: длина, скорость, ускорение, масса, время, угол, RPM, ratio, dB, Hz, литры и температура. `Unknown` запрещён для подтверждённой записи. `Unitless` применяется только к действительно безразмерным значениям и описательным observations.

Координатное пространство обязательно для геометрии:

- `DonorWorld`, `RemakeWorld`;
- `DonorMeshLocal`, `RemakeMeshLocal`;
- `VehicleLocal`, `PlayerLocal`;
- `ScreenPixels`, `Normalized`;
- `NotApplicable` для скаляров без пространственного смысла.

`Unknown` допускается только у `Missing`/непроверенных записей. В текущем world transfer donor и remake используют Unity Y-up, 1 unit = 1 m. Старое преобразование controlled-proof OBJ `(X,Y,Z) → (X,Z,Y)` относится только к конкретным нормализованным mesh-файлам и всегда указывается в notes.

## Provenance и confidence

`ReferenceSourceRecord` идентифицирует исходный контейнер или проектную процедуру: логический путь, SHA-256, donor build и Unity version. `ReferenceEvidenceRecord` связывает источник с конкретным внешним или проектным артефактом. `externalToGit=true` означает, что payload остаётся во внешнем staging/reference storage.

Разрешённые capture methods:

`SerializedDonorData`, `DecompiledConstant`, `DecompiledFormula`, `SceneTransform`, `AssetMetadata`, `ManualMeasurement`, `VideoTiming`, `ScreenshotMeasurement`, `RuntimeObservation`, `DerivedCalculation`, `Approximation`, `Unknown`.

Правила confidence:

- `Exact` — только прямое однозначное значение с проверяемым источником; не для approximation;
- `High` — прямое сериализованное значение или надёжный расчёт с известными входами;
- `Medium`/`Low` — неоднозначность объекта, метода или состояния описана в notes;
- `Unknown` — только неполученное/невалидированное значение;
- `Approximation` никогда не получает `Exact`;
- `DerivedCalculation` обязан перечислять input record IDs и формулу в `sourceLocator` или `rawObservation`.

## Требования и fixtures

`ReferenceRequirementRecord` хранит P0–P3 coverage: `Covered`, `Partial`, `Missing`, `Blocked`. `coveredByRecordIds` не превращает частичный источник в полное измерение: например, root Rigidbody mass остаётся `Partial` для curb mass.

`BehaviorFixture` в основной базе хранит процедуру и coverage, а отдельный `ReferenceCalibrationFixture` — компактный контракт будущего теста. Пустые `recordIds` и статус `Missing` разрешены; выдуманные expected numbers запрещены.

## Настройки ремейка

`ReferenceTuningOverrideSet` ссылается на donor measurement через `measuredRecordId` и отдельно хранит `tunedValue`, unit, rationale, authoring source и дату. В measurement schema поля `tunedValue` нет. Сравнение выполняется Editor dashboard, но исходное измерение не переписывается.

## Валидация и миграция

`ReferenceCaptureValidator` проверяет:

- версию схемы и dataset;
- формат и уникальность ID;
- source/evidence references;
- единицы, coordinate space, finite values и tolerances;
- confidence/capture-method rules;
- derived dependencies и циклы;
- ссылки requirements/fixtures;
- отсутствие дублированных tuning overrides и совпадение единиц;
- очередь Missing/Partial P0/P1 отдельно от ошибок структуры.

Версия `0` мигрируется в `1` через повторную нормализацию JSON. Неизвестная будущая версия отвергается. До добавления несовместимого поля необходимо добавить пошаговую миграцию и EditMode fixture старой схемы.

## Импорт и запросы

Структурированный import envelope содержит `schemaVersion`, `datasetVersion`, новые `evidence[]` и `measurements[]`. Импорт выполняет merge по stable ID, затем полную валидацию; конфликт ID заменяет запись только осознанно и остаётся видимым в diff.

Runtime/editor consumers используют `FindMeasurement`, `FindSource`, `FindEvidence` и `Query(category, priority, status)`. Ни один запрос не открывает donor installation. Все машинные absolute paths разрешаются только Editor-кодом через ignored `Config/DonorPaths.local.json`.
