# Отчёт collider transfer

Extractor нормализовал 5 001 collider components:

| Тип | Количество |
|---|---:|
| SphereCollider | 1 469 |
| BoxCollider | 1 280 |
| CapsuleCollider | 1 216 |
| MeshCollider | 1 036 |

3 262 collider имеют `IsTrigger=0`, 1 739 — `IsTrigger=1`. В reference-world слое 1 413 entities с 1 488 collider references.

Для каждого record сохранены stable ID, owner entity, component/source object ID, type, enabled/trigger/convex flags, mesh GUID/fileID, center, size/radius/height/direction и replacement status.

## Политика

Collider metadata импортирован как `CollisionReference`. Generated visual scenes не создают runtime collision: primitive proxy collider удаляется. Это предотвращает автоматическое использование donor render meshes как production MeshCollider.

## Не завершено вручную

- terrain holes и continuous driving surface;
- building floors/doorway blockage;
- duplicate/inverted/extreme colliders;
- road discontinuities;
- gameplay intent для triggers;
- production physics materials.

Итог: serialized collider coverage представлено полностью для распознанных collider types, gameplay fidelity не заявляется.
