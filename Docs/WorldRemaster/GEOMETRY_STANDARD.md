# World Geometry Standard

## Coordinate and pivot contract

- Y-up; `1 unit = 1 metre`; production scale positive and normally `(1,1,1)`.
- World anchors наследуют reviewed 04A1 coordinates; donor PathID не является project identity.
- Door/gate pivot располагается на реальной петле, а panel geometry — дочерний объект.
- Production prefabs не содержат donor mesh payload.

## Geometry/LOD/collision

- Modular shell and structural collision разделены от clutter.
- LOD0 хранит gameplay silhouette; последующие LOD сохраняют trunk/roof/door silhouette.
- MeshCollider допустим для static terrain/road; moving architecture использует primitive colliders.
- Неравномерный или отрицательный scale запрещён для moving/collision-critical roots.
- HLOD создаётся только после измеренного streaming benefit.

Pilot root bounds: `160 × 12 × 130 m`. Garage opening: `3.12 × 2.22 m`; representative vehicle envelope gate: `2.2 × 1.75 m` плюс documented clearance.
