"""Audit all driver expressions and targets without evaluating them."""

import json

import bpy


def collect_drivers(owner_name, animation_data):
    if animation_data is None:
        return []
    result = []
    for fcurve in animation_data.drivers:
        result.append(
            {
                "owner": owner_name,
                "data_path": fcurve.data_path,
                "array_index": fcurve.array_index,
                "expression": fcurve.driver.expression,
                "is_valid": fcurve.is_valid,
                "variables": [
                    {
                        "name": variable.name,
                        "type": variable.type,
                        "targets": [
                            {
                                "id": target.id.name if target.id else None,
                                "bone_target": target.bone_target,
                                "data_path": target.data_path,
                                "transform_type": target.transform_type,
                                "transform_space": target.transform_space,
                            }
                            for target in variable.targets
                        ],
                    }
                    for variable in fcurve.driver.variables
                ],
            }
        )
    return result


drivers = []
for obj in bpy.data.objects:
    drivers.extend(collect_drivers(f"Object:{obj.name}", obj.animation_data))
for shape_keys in bpy.data.shape_keys:
    drivers.extend(
        collect_drivers(f"ShapeKeys:{shape_keys.name}", shape_keys.animation_data)
    )

print("===BLENDER_DRIVER_AUDIT===")
print(
    json.dumps(
        {
            "count": len(drivers),
            "expressions": sorted({driver["expression"] for driver in drivers}),
            "invalid_count": sum(1 for driver in drivers if not driver["is_valid"]),
            "drivers": drivers,
        },
        ensure_ascii=False,
        indent=2,
    )
)
print("===END_BLENDER_DRIVER_AUDIT===")
