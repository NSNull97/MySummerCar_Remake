# Аудит координат и масштаба

## Вывод

Donor и remake используют Unity Y-up, одинаковое направление осей и метрический масштаб `1 unit = 1 m`. Handedness и rotation не меняются. Единственное преобразование — перенос origin к проверенному garage roof anchor.

`DonorWorldToRemakeWorldMatrix`:

```text
| 1 0 0  169.980 |
| 0 1 0    1.611 |
| 0 0 1 -1040.625|
| 0 0 0    1.000 |
```

То есть `remakePosition = donorPosition - (-169.98, -1.611, 1040.625)`.

## Конфигурация

`WorldCoordinateConversionConfig` хранит source/destination unit scale, axis mapping, handedness, translation, rotation, scale, pivot policy, negative-scale policy и floating-origin recommendation. Каноническая конфигурация записана в `Assets/Game/World/Content/WorldTransfer/M04A1_WorldGeometryDatabase.json`.

- Поворот: identity quaternion `(0, 0, 0, 1)`.
- Масштаб: `(1, 1, 1)`.
- Pivot: donor hierarchy pivots сохраняются в данных.
- Negative scale: сохраняется и требует визуальной проверки.
- Bounds: все восемь углов преобразуются матрицей; сомнительные combined-mesh bounds заменяются placement point и попадают в missing/review report.
- Floating origin: не нужен для reference proxy при диапазоне около 6,5 км; для production streaming решение следует пересмотреть по профилированию.

## Проверки

EditMode покрывает known point, directions, rotation, scale, parent-child composition и round trip. Garage fixture `fb0f962be1b325cc19296c66751818c0` преобразуется из `(-169.98, -1.6110001, 1040.625)` в `(0, ~0, 0)` с погрешностью float.

Границы reference-world после отклонения некорректных combined bounds:

- donor min `(-3163.1853, -414.4400, -2254.6733)`;
- donor max `(3383.1853, 911.7600, 3638.5908)`;
- remake min `(-2993.2053, -412.8290, -3295.2983)`;
- remake max `(3553.1653, 913.3710, 2597.9658)`.

Экстремумы по Y требуют ручного осмотра: часть может принадлежать скрытой/служебной геометрии, а не поверхности мира.
