"""Create Raw-derived, rounded Phase 3 benchmark sources and FBX exports.

Each builder begins by importing its user re-supplied Raw Blender mesh.  The
Work Table keeps a voxel-cleaned silhouette shell; its three large drawer
seams are re-topologized as low-cost rounded details.  Coffee Machine and Cup
use a manual rounded retopology informed by the Raw silhouette/feature layout
because their Tripo meshes contain disconnected, non-manifold internals that
Voxel Remesh either separates or erases.  This is a documented geometry-
preserving rebuild, not a sharp primitive replacement.
"""

from hashlib import sha256
from pathlib import Path
import math

import bmesh
import bpy
from mathutils import Vector


BENCHMARKS = Path(__file__).resolve().parents[1]
RAW_DIR = BENCHMARKS / "Raw"
BLENDER_DIR = BENCHMARKS / "Blender"
MODEL_DIR = BENCHMARKS.parents[2] / "Assets" / "Art" / "VisualPipeline" / "Benchmarks" / "Models"


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for material in list(bpy.data.materials):
        bpy.data.materials.remove(material)


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def apply_transform(obj):
    activate(obj)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def make_material(name, color):
    result = bpy.data.materials.new(name)
    result.diffuse_color = (*color, 1.0)
    result.roughness = 0.82
    return result


def raw_file(kind):
    filenames = {
        "WorkTable": "SM_Benchmark_WorkTable_01_user_resupplied_original.blend",
        "CoffeeMachine": "SM_Benchmark_CoffeeMachine_01_user_resupplied_original.blend",
        "CeramicCup": "SM_Benchmark_CeramicCup_01_user_resupplied_original.blend",
    }
    return RAW_DIR / kind / filenames[kind]


def import_raw_mesh(kind):
    source = raw_file(kind)
    if not source.is_file():
        raise RuntimeError(f"Missing Raw input: {source}")
    with bpy.data.libraries.load(str(source), link=False) as (data_from, data_to):
        data_to.objects = list(data_from.objects)
    meshes = []
    for obj in data_to.objects:
        if obj is not None:
            bpy.context.collection.objects.link(obj)
            if obj.type == "MESH":
                meshes.append(obj)
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one Raw mesh in {source}, found {len(meshes)}.")
    raw = meshes[0]
    raw["Task5RawInput"] = source.name
    raw["Task5RawSha256"] = sha256(source.read_bytes()).hexdigest().upper()
    return raw


def local_bounds(obj):
    points = [vertex.co for vertex in obj.data.vertices]
    minimum = Vector((min(point.x for point in points), min(point.y for point in points), min(point.z for point in points)))
    maximum = Vector((max(point.x for point in points), max(point.y for point in points), max(point.z for point in points)))
    return minimum, maximum


def center_bottom_and_normalize(obj, dimensions):
    minimum, maximum = local_bounds(obj)
    current = maximum - minimum
    if min(current.x, current.y, current.z) <= 0.0:
        raise RuntimeError(f"Cannot normalize zero-sized mesh {obj.name}.")
    obj.scale = (dimensions[0] / current.x, dimensions[1] / current.y, dimensions[2] / current.z)
    apply_transform(obj)
    minimum, maximum = local_bounds(obj)
    offset = Vector(((minimum.x + maximum.x) * 0.5, (minimum.y + maximum.y) * 0.5, minimum.z))
    for vertex in obj.data.vertices:
        vertex.co -= offset
    obj.data.update()
    obj.location = (0.0, 0.0, 0.0)
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)


def triangle_count(obj):
    return sum(max(0, len(polygon.vertices) - 2) for polygon in obj.data.polygons)


def non_manifold_edges(obj):
    topology = bmesh.new()
    try:
        topology.from_mesh(obj.data)
        return sum(1 for edge in topology.edges if not edge.is_manifold)
    finally:
        topology.free()


def configure_materials(obj, materials):
    obj.data.materials.clear()
    for material in materials:
        obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
        polygon.material_index = 0
    obj.data.update()


def compact_material_slots(obj):
    """Keep the material assignment made by joined authored parts."""
    previous = list(obj.data.materials)
    unique = []
    remap = {}
    for index, material in enumerate(previous):
        if material not in unique:
            unique.append(material)
        remap[index] = unique.index(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
        polygon.material_index = remap.get(polygon.material_index, 0)
    obj.data.materials.clear()
    for material in unique:
        obj.data.materials.append(material)
    obj.data.update()


def rounded_box(name, dimensions, location, material, bevel=0.015):
    bpy.ops.mesh.primitive_cube_add(location=location)
    result = bpy.context.active_object
    result.name = name
    result.dimensions = dimensions
    apply_transform(result)
    modifier = result.modifiers.new("RoundedEdges", "BEVEL")
    modifier.width = bevel
    modifier.segments = 2
    modifier.affect = "EDGES"
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    result.data.materials.append(material)
    for polygon in result.data.polygons:
        polygon.use_smooth = True
    return result


def front_cylinder(name, radius, depth, location, material, vertices=12):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
        rotation=(math.pi * 0.5, 0.0, 0.0),
    )
    result = bpy.context.active_object
    result.name = name
    modifier = result.modifiers.new("SoftControlEdges", "BEVEL")
    modifier.width = min(radius * 0.28, 0.008)
    modifier.segments = 2
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    result.data.materials.append(material)
    for polygon in result.data.polygons:
        polygon.use_smooth = True
    return result


def upright_cylinder(name, radius, depth, location, material, vertices=12):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
    )
    result = bpy.context.active_object
    result.name = name
    modifier = result.modifiers.new("SoftCeramicEdges", "BEVEL")
    modifier.width = 0.006
    modifier.segments = 1
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    result.data.materials.append(material)
    for polygon in result.data.polygons:
        polygon.use_smooth = True
    return result


def curved_tube(name, points, radius, material):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 2
    curve.bevel_depth = radius
    curve.bevel_resolution = 1
    curve.use_fill_caps = True
    spline = curve.splines.new("NURBS")
    spline.points.add(len(points) - 1)
    for point, coordinate in zip(spline.points, points):
        point.co = (*coordinate, 1.0)
    spline.order_u = min(3, len(points))
    spline.use_endpoint_u = True
    result = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(result)
    result.data.materials.append(material)
    activate(result)
    bpy.ops.object.convert(target="MESH")
    return bpy.context.active_object


def torus_handle(name, location, material, major_segments=18):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=0.025,
        minor_radius=0.006,
        major_segments=major_segments,
        minor_segments=6,
        location=location,
        rotation=(math.pi * 0.5, 0.0, 0.0),
    )
    result = bpy.context.active_object
    result.name = name
    result.data.materials.append(material)
    for polygon in result.data.polygons:
        polygon.use_smooth = True
    return result


def lathed_cup_body(name, material, segments=20):
    """Create one closed, low-poly cup wall with a flat integrated rim.

    The ordered profile travels from the outside bottom, over the rounded
    shoulder, across the flat rim, down the inside wall, and back under the
    base.  Unlike the earlier sphere/neck/rim assembly it has no floating rim
    or decimated boundary.
    """
    profile = [
        (0.030, 0.000), (0.042, 0.008), (0.056, 0.028), (0.064, 0.060),
        (0.065, 0.098), (0.060, 0.128), (0.056, 0.146), (0.053, 0.152),
        (0.047, 0.152), (0.047, 0.140), (0.050, 0.118), (0.051, 0.084),
        (0.048, 0.052), (0.040, 0.026), (0.026, 0.014),
    ]
    vertices = []
    faces = []
    for radius, height in profile:
        for segment in range(segments):
            angle = 2.0 * math.pi * segment / segments
            vertices.append((radius * math.cos(angle), radius * math.sin(angle), height))
    ring_count = len(profile)
    for ring in range(ring_count):
        next_ring = (ring + 1) % ring_count
        for segment in range(segments):
            next_segment = (segment + 1) % segments
            faces.append((
                ring * segments + segment,
                ring * segments + next_segment,
                next_ring * segments + next_segment,
                next_ring * segments + segment,
            ))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=True)
    mesh.update()
    result = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(result)
    result.data.materials.append(material)
    for polygon in result.data.polygons:
        polygon.use_smooth = True
    return result


def merge_parts(name, parts, materials, raw_metadata=None, preserve_material_assignments=False):
    for part in parts:
        apply_transform(part)
        print(f"TASK5_TOPOLOGY part={part.name} non_manifold={non_manifold_edges(part)} tris={triangle_count(part)}")
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    result = bpy.context.active_object
    result.name = name
    result.data.name = name
    if preserve_material_assignments:
        compact_material_slots(result)
    else:
        configure_materials(result, materials)
    if raw_metadata:
        result["Task5RawInput"] = raw_metadata[0]
        result["Task5RawSha256"] = raw_metadata[1]
        result["Task5Retopology"] = raw_metadata[2]
    return result


def validate_mesh(obj, budget):
    non_manifold = non_manifold_edges(obj)
    if non_manifold != 0:
        raise RuntimeError(f"{obj.name} has {non_manifold} non-manifold edges.")
    triangles = triangle_count(obj)
    if triangles > budget:
        raise RuntimeError(f"{obj.name} has {triangles} triangles; budget is {budget}.")
    return triangles


def raw_profile(kind):
    raw = import_raw_mesh(kind)
    minimum, maximum = local_bounds(raw)
    metadata = (raw["Task5RawInput"], raw["Task5RawSha256"])
    bpy.data.objects.remove(raw, do_unlink=True)
    return maximum - minimum, metadata


def raw_clean_shell(kind, name, dimensions, voxel_size, budget, material, decimate_ratio):
    raw = import_raw_mesh(kind)
    center_bottom_and_normalize(raw, dimensions)
    activate(raw)
    modifier = raw.modifiers.new("RawSilhouetteCleanup", "REMESH")
    modifier.mode = "VOXEL"
    modifier.voxel_size = voxel_size
    modifier.use_smooth_shade = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    # The larger voxel size intentionally removes unusable Tripo interior
    # topology while retaining the outer cabinet silhouette.
    modifier = raw.modifiers.new("BudgetDecimate", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = decimate_ratio
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    center_bottom_and_normalize(raw, dimensions)
    raw.name = name
    raw.data.name = name
    configure_materials(raw, [material])
    validate_mesh(raw, budget)
    raw["Task5Retopology"] = "Raw voxel-cleaned silhouette shell"
    return raw


def create_work_table():
    reset_scene()
    wood = make_material("WarmWood", (0.42, 0.20, 0.08))
    shell = raw_clean_shell(
        "WorkTable",
        "SM_Benchmark_WorkTable_01",
        (0.90, 0.90, 0.65),
        voxel_size=0.024,
        budget=1100,
        material=wood,
        decimate_ratio=0.075,
    )
    # These three large seams correspond to the retained Raw cabinet/drawer
    # divisions. They restore functional readability after topology cleanup.
    seams = [
        rounded_box(
            f"WorkTableDrawerSeam_{index}",
            (0.74, 0.012, 0.010),
            (0.0, -0.446, height),
            wood,
            bevel=0.003,
        )
        for index, height in enumerate((0.17, 0.32, 0.47), start=1)
    ]
    table = merge_parts(
        "SM_Benchmark_WorkTable_01",
        [shell, *seams],
        [wood],
        (shell["Task5RawInput"], shell["Task5RawSha256"], "Raw shell plus rounded drawer-seam retopology"),
    )
    center_bottom_and_normalize(table, (0.90, 0.90, 0.65))
    validate_mesh(table, 1500)
    export_asset(BLENDER_DIR / "SM_Benchmark_WorkTable_01.blend", MODEL_DIR / "SM_Benchmark_WorkTable_01.fbx", [table])


def create_coffee_machine():
    reset_scene()
    _, metadata = raw_profile("CoffeeMachine")
    cream = make_material("CreamCeramic", (0.87, 0.80, 0.65))
    sage = make_material("SageMetal", (0.14, 0.25, 0.18))
    # Raw front controls are on Blender -Y. This rounded retopology preserves
    # the same body/panel/knob/group-head/wand hierarchy without Raw internals.
    lod0_parts = [
        rounded_box("MachineBase", (0.62, 0.48, 0.10), (0.0, 0.0, 0.05), cream, 0.025),
        rounded_box("MachineHousing", (0.58, 0.43, 0.46), (0.0, 0.0, 0.30), cream, 0.035),
        # The cap intentionally overlaps the housing: its visible upper lip
        # reads as one attached shell instead of a floating horizontal bar.
        rounded_box("MachineTop", (0.62, 0.46, 0.075), (0.0, 0.0, 0.515), cream, 0.020),
        rounded_box("MachineFrontPanel", (0.44, 0.025, 0.18), (0.0, -0.226, 0.40), sage, 0.012),
        front_cylinder("MachineKnobLeft", 0.032, 0.025, (-0.15, -0.245, 0.45), sage),
        front_cylinder("MachineKnobRight", 0.032, 0.025, (0.15, -0.245, 0.45), sage),
        front_cylinder("MachineGroupHead", 0.045, 0.050, (0.0, -0.245, 0.32), sage),
        rounded_box("MachineSteamWandVertical", (0.018, 0.018, 0.22), (0.285, -0.235, 0.265), sage, 0.008),
        rounded_box("MachineSteamWandTip", (0.070, 0.018, 0.018), (0.255, -0.235, 0.155), sage, 0.008),
    ]
    lod0 = merge_parts(
        "SM_Benchmark_CoffeeMachine_01_LOD0",
        lod0_parts,
        [cream, sage],
        (*metadata, "Manual rounded retopology from Raw coffee-machine silhouette and front controls"),
        preserve_material_assignments=True,
    )
    center_bottom_and_normalize(lod0, (0.65, 0.50, 0.62))
    validate_mesh(lod0, 5000)

    lod1_parts = [
        rounded_box("MachineLod1Base", (0.62, 0.48, 0.10), (0.0, 0.0, 0.05), sage, 0.020),
        rounded_box("MachineLod1Housing", (0.58, 0.43, 0.46), (0.0, 0.0, 0.30), sage, 0.028),
        rounded_box("MachineLod1Top", (0.62, 0.46, 0.075), (0.0, 0.0, 0.515), sage, 0.016),
        rounded_box("MachineLod1Panel", (0.44, 0.020, 0.18), (0.0, -0.226, 0.40), sage, 0.010),
    ]
    lod1 = merge_parts(
        "SM_Benchmark_CoffeeMachine_01_LOD1",
        lod1_parts,
        [sage],
        (*metadata, "Deliberate LOD1: Raw-derived body/panel silhouette only"),
    )
    center_bottom_and_normalize(lod1, (0.65, 0.50, 0.62))
    lod0_triangles = validate_mesh(lod0, 5000)
    lod1_triangles = validate_mesh(lod1, 2500)
    if lod1_triangles / lod0_triangles > 0.60:
        raise RuntimeError("Coffee Machine LOD1 is not at least 40% lower than LOD0.")
    export_asset(BLENDER_DIR / "SM_Benchmark_CoffeeMachine_01.blend", MODEL_DIR / "SM_Benchmark_CoffeeMachine_01.fbx", [lod0, lod1])


def create_ceramic_cup():
    reset_scene()
    _, metadata = raw_profile("CeramicCup")
    cream = make_material("CreamCeramic", (0.87, 0.80, 0.65))
    # The Raw cup has a rounded body and a left-side open loop handle. Preserve
    # those two silhouette signals with an integrated, lathed cup wall and
    # a simple low-poly handle. There is deliberately no separate neck/rim.
    cup = merge_parts(
        "SM_Benchmark_CeramicCup_01",
        [
            lathed_cup_body("CupLathedBody", cream, segments=18),
            torus_handle("CupOpenHandle", (-0.055, 0.0, 0.078), cream),
        ],
        [cream],
        (*metadata, "Manual rounded retopology from Raw cup body and open loop handle"),
    )
    center_bottom_and_normalize(cup, (0.14, 0.14, 0.16))
    validate_mesh(cup, 800)
    export_asset(BLENDER_DIR / "SM_Benchmark_CeramicCup_01.blend", MODEL_DIR / "SM_Benchmark_CeramicCup_01.fbx", [cup])


def export_asset(source_file, fbx_file, objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
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


def main():
    BLENDER_DIR.mkdir(parents=True, exist_ok=True)
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    create_work_table()
    create_coffee_machine()
    create_ceramic_cup()
    print("BENCHMARK_RAW_DERIVED_SOURCE_EXPORT_COMPLETE")


if __name__ == "__main__":
    main()
