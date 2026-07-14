# Производительность pipeline и editor scalability

Измерения сделаны 2026-07-14 на текущей локальной машине. Это не game FPS benchmark.

| Операция | Результат |
|---|---:|
| AssetRipper full external export | ~29,6 с |
| Extractor final normalization | 3,295 с |
| Unity clean generate after semantic filtering | 19,547 с |
| Unity repeat generate before filtering | 16,706 с |
| World validation batch with three source hashes and scene-content parity | 21,127 с process wall time |
| EditMode suite | 67/67, final test duration 12,156 с |
| PlayMode suite | 9/9, test duration 0,121 с |

## Размеры

- raw AssetRipper export: 17 247 файлов / 1 754 855 811 байт;
- external normalized manifests: 12 файлов / около 26,1 МБ;
- project entity CSV: около 9,6 МБ;
- project collider CSV: около 1,1 МБ;
- database JSON: около 43 КБ;
- generated ReferenceOnly tree: 154 файла / 13 015 783 байта;
- generated scenes: 52;
- category materials: 24;
- generated proxy entities: 3 842.

## Scalability policy

- 49 additive 512 м cells вместо одной сцены;
- 31 large/global entities отдельно;
- no Rigidbody и no proxy colliders;
- HDRP/Unlit category materials, instancing enabled, shadows off;
- grass/vegetation остаются records/proxies, не persistent production GameObjects;
- selected-cell generation доступна;
- full rebuild отделён от обычного Editor workflow;
- local generated tree ignored и удаляем.

Две плотные cells (`cell_-3_0` 1 456 и `cell_0_-3` 671) требуют особого внимания при future replacement/streaming. Reference bootstrap loader создан, но disabled: ignored scenes не входят в release build settings. Editor overview открывает garage-adjacent subset, а остальные cells можно открывать выборочно.

## Repeatability observation

Состав, stable IDs, database version и counts воспроизводимы. 166 generated files до semantic filtering имели одинаковые пути/count на повторном запуске, но 164 byte hashes изменились из-за Unity internal YAML fileID/meta GUID. Generated files не являются durable source of truth; критерий reproducibility проверяется по database plan/stable IDs/stamps, а не по их byte hash.
