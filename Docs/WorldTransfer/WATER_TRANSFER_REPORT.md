# Отчёт water and shoreline transfer

После контекстной фильтрации подтверждены 12 `Water` records под `MAP/`. Пять spatial representatives имеют тег `MajorWater`; крупнейший `LAKEBED` переведён в global scene и имеет сохранённые bounds.

Отдельная категория `Shoreline` не подтверждена правилами. Shoreline shape может быть частью lakebed/terrain mesh и не должна считаться отсутствующей без mesh inspection.

Сохранено:

- water transforms/elevations;
- source mesh GUID и bounds/review status;
- cell/global membership;
- relationships, доступные через hierarchy и spatial database.

Не выполнено:

- donor water materials не импортированы как production;
- HDRP Water System не настраивался;
- shoreline spline, underwater collision и transition volumes не доказаны;
- elevation against docks/bridges/terrain не проверена вручную.

Generated proxy использует нейтральный HDRP/Unlit category material без realtime shadows. Статус: `WorldLayoutReference`, manual fidelity pending.
