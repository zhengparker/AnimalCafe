"""Create the Phase 3 benchmark Blender sources and production FBX exports.

Run from Blender in background mode.  This is intentionally deterministic: the
approved dimensions, source-axis convention, materials, LOD mesh separation,
and export settings are all recorded here rather than relying on a manual UI.
"""

from pathlib import Path

import bpy


BENCHMARKS = Path(__file__).resolve().parents[1]
BLENDER_DIR = BENCHMARKS / "Blender"
MODEL_DIR = BENCHMARKS.parents[2] / "Assets" / "Art" / "VisualPipeline" / "Benchmarks" / "Models"


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for material in list(bpy.data.materials):
        bpy.data.materials.remove(material)


def material(name, color):
    result = bpy.data.materials.new(name)
    result.diffuse_color = (*color, 1.0)
    return result


def cube(name, size, location, material_slot):
    bpy.ops.mesh.primitive_cube_add(location=location)
    result = bpy.context.active_object
    result.name = name
    result.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    result.data.materials.append(material_slot)
    return result


def cylinder(name, radius, depth, location, material_slot, vertices=16):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location)
    result = bpy.context.active_object
    result.name = name
    result.data.materials.append(material_slot)
    return result


def join_objects(name, objects):
    bpy.ops.object.select_all(action="DESELECT")
    for item in objects:
        item.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    result = bpy.context.active_object
    result.name = name
    # The exported mesh is consumed directly by the Unity Prefab builder.
    # Applying location keeps the bottom-center pivot at source origin instead
    # of depending on an FBX object transform that the builder would discard.
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return result


def export_asset(source_file, fbx_file, objects):
    bpy.ops.object.select_all(action="DESELECT")
    for item in objects:
        item.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.wm.save_as_mainfile(filepath=str(source_file))
    bpy.ops.export_scene.fbx(
        filepath=str(fbx_file),
        use_selection=True,
        object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        use_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        path_mode="AUTO",
        embed_textures=False,
        bake_space_transform=False,
    )


def create_work_table():
    reset_scene()
    wood = material("WarmWood", (0.42, 0.20, 0.08))
    parts = [cube("SM_Benchmark_WorkTable_01_Top", (0.90, 0.90, 0.06), (0, 0, 0.62), wood)]
    for x in (-0.37, 0.37):
        for y in (-0.37, 0.37):
            parts.append(cube("SM_Benchmark_WorkTable_01_Leg", (0.08, 0.08, 0.62), (x, y, 0.31), wood))
    parts = [join_objects("SM_Benchmark_WorkTable_01", parts)]
    export_asset(
        BLENDER_DIR / "SM_Benchmark_WorkTable_01.blend",
        MODEL_DIR / "SM_Benchmark_WorkTable_01.fbx",
        parts,
    )


def create_coffee_machine():
    reset_scene()
    cream = material("CreamCeramic", (0.80, 0.70, 0.52))
    sage = material("SageMetal", (0.14, 0.25, 0.18))
    honey = material("HoneyAccent", (0.92, 0.55, 0.08))

    # Blender front is -Y.  The front panel therefore sits at the negative-Y edge.
    lod0 = [
        cube("SM_Benchmark_CoffeeMachine_01_LOD0_Body", (0.65, 0.50, 0.46), (0, 0, 0.23), cream),
        cube("SM_Benchmark_CoffeeMachine_01_LOD0_Top", (0.61, 0.46, 0.04), (0, 0, 0.60), cream),
        cube("SM_Benchmark_CoffeeMachine_01_LOD0_Panel", (0.48, 0.025, 0.22), (0, -0.2525, 0.40), sage),
        cylinder("SM_Benchmark_CoffeeMachine_01_LOD0_Knob", 0.025, 0.018, (0.15, -0.27, 0.46), sage, 12),
    ]
    # The small knob faces Blender -Y rather than its default vertical orientation.
    lod0[-1].rotation_euler = (1.570796, 0, 0)
    bpy.context.view_layer.objects.active = lod0[-1]
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    lod0 = [join_objects("SM_Benchmark_CoffeeMachine_01_LOD0", lod0)]

    lod1 = [
        cube("SM_Benchmark_CoffeeMachine_01_LOD1_Body", (0.65, 0.50, 0.58), (0, 0, 0.29), sage),
        cube("SM_Benchmark_CoffeeMachine_01_LOD1_Top", (0.61, 0.46, 0.04), (0, 0, 0.60), sage),
    ]
    lod1 = [join_objects("SM_Benchmark_CoffeeMachine_01_LOD1", lod1)]
    export_asset(
        BLENDER_DIR / "SM_Benchmark_CoffeeMachine_01.blend",
        MODEL_DIR / "SM_Benchmark_CoffeeMachine_01.fbx",
        lod0 + lod1,
    )


def create_ceramic_cup():
    reset_scene()
    cream = material("CreamCeramic", (0.87, 0.80, 0.65))
    parts = [
        cylinder("SM_Benchmark_CeramicCup_01_Body", 0.065, 0.145, (0, 0, 0.0725), cream, 16),
        cylinder("SM_Benchmark_CeramicCup_01_Rim", 0.070, 0.015, (0, 0, 0.1525), cream, 16),
        cube("SM_Benchmark_CeramicCup_01_Handle", (0.03, 0.035, 0.07), (0.055, 0, 0.085), cream),
    ]
    parts = [join_objects("SM_Benchmark_CeramicCup_01", parts)]
    export_asset(
        BLENDER_DIR / "SM_Benchmark_CeramicCup_01.blend",
        MODEL_DIR / "SM_Benchmark_CeramicCup_01.fbx",
        parts,
    )


def main():
    BLENDER_DIR.mkdir(parents=True, exist_ok=True)
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    create_work_table()
    create_coffee_machine()
    create_ceramic_cup()
    print("BENCHMARK_SOURCE_EXPORT_COMPLETE")


if __name__ == "__main__":
    main()
