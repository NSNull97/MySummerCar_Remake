# Preliminary diff audit — `cell_0_-3`

Статус: **ограниченный предварительный аудит; canonical matched audit
заблокирован**

## Использованные данные

- donor:
  `M06B-EVD-001`, ясный aerial overview домашнего участка;
- remake:
  исторический нематченный
  `PerformanceCaptures/Milestone05A/ProductionOnly.png`;
- serialized layout:
  frozen 04A1 world database и подтверждённый home/garage anchor;
- explicit user judgment:
  ячейка не похожа на соответствующее место оригинала.

Donor и remake изображения имеют разные camera transform/FOV и не могут
использоваться для численной фотограмметрии. Выводы ниже ограничены крупными
композиционными различиями.

## Предварительно видимые различия

### Critical identity / road approach

В donor overview основная дорога заметно изгибается перед/слева от участка, а
driveway отдельно входит к гаражной площадке. В историческом production capture
подъезд читается как длинный прямой коридор через ворота, направленный к
центральному building mass. Это меняет узнаваемую схему прибытия к дому.

### Spatial / building massing

Donor overview показывает длинный одноэтажный прямоугольный дом с гаражной
частью/площадкой позади и справа относительно главного фасада. Production
baseline читается как более компактный составной объём с другой связью здания,
дороги и двора. Точные footprint, rotation и высоту нельзя корректировать без
matched views и измерений.

### Visual composition / property boundary

В оригинале передняя часть участка очерчена характерной подстриженной
изгородью, а дорога, utility pole, открытая площадка и плотная нерегулярная
растительность образуют узнаваемую композицию. Production baseline использует
другую схему ворот, открытого пространства и условных деревьев.

## Что не установлено

- точный donor road centerline, width и elevation;
- footprint/height/rotation tolerance дома и гаража;
- положение дверей/окон и детальный фасадный ритм;
- точная vegetation boundary;
- camera transforms, FOV и eye height для десяти видов.

## Решение

Записи `FID-CELL-0-M3-001..004` остаются открытыми. Никакая геометрия не
исправлялась. После canonical donor capture требуется снять matched
rejected-before, уточнить issue matrix и только затем менять layout в порядке:
road/terrain → footprint/silhouette → facade landmarks → vegetation.
