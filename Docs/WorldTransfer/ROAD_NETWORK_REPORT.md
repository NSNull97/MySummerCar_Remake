# Отчёт road network transfer

## Статус

В reference database находятся 29 контекстно подтверждённых `Road`, 2 `Bridge`, 163 `RoadSign` и 14 `UtilityPole` records. Они сохраняют source/converted transforms, mesh GUID, bounds/review status, cell и provenance.

После удаления name-only false positives нет подтверждённых категорий `RoadShoulder`, `Ditch`, `Culvert` или `Driveway`. Это не доказательство их отсутствия: отдельная centerline/spline topology из donor не восстановлена.

## Что доступно

- визуальный bounds/point proxy для каждого record;
- source hierarchy и mesh reference;
- bridge landmarks;
- road/sign/pole spatial relationship для ручного обзора;
- collider metadata, когда компонент прикреплён к source entity.

## Что не заявлено

- непрерывный road graph;
- точные width/elevation samples;
- intersections и lane topology;
- driveable production surface;
- shoulder/ditch profile;
- road/terrain blending.

Поэтому coverage — `RepresentedNeedsManualTopologyReview`, а не production-ready. Проверка непрерывного проезда и сравнение junction/elevation остаются ручными пунктами.
