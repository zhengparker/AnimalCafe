"""Build the approved Phase 4 production Model sources with Blender 5.2.0.

Usage from Blender:
    blender --background --factory-startup --python BuildPhase4ProductionAssets.py -- \
        <project-root> <approved-work-table.blend> <approved-cash.glb> \
        [all|counter-only|cash-only]

The protected inputs are opened read-only and verified by SHA-256 before and
after production. The Counter uses the Studio Owner-approved controlled
per-axis derivative from the authoritative Work Table source.
"""

from hashlib import sha256
from math import radians
from pathlib import Path
import json
import sys

import bpy
from mathutils import Matrix, Vector


WORK_TABLE_SHA256 = "CDA670B6DEAF309225E1636AA3B07EEBECC6D7D8497939027BC05659C156F60A"
CASH_REGISTER_SHA256 = "28859431416BD3D40D0C52D9F56DE9CD577566964094CD945B69C9120253321D"

COUNTER_TARGET_XYZ = Vector((1.00, 1.00, 0.72))
CASH_TARGET_XYZ = Vector((0.43, 0.26, 0.45))
CASH_MAX_TRIANGLES = 6000
TARGET_TEXTURE_SIZE = 512
DIMENSION_TOLERANCE = 0.03


def file_sha256(path):
    digest = sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def require_hash(path, expected):
    if not path.is_file():
        raise RuntimeError(f"Missing protected source: {path}")
    actual = file_sha256(path)
    if actual != expected:
        raise RuntimeError(
            f"Protected source hash mismatch: {path}; expected={expected}; actual={actual}"
        )
    return actual


def dimensions_tuple(obj):
    return tuple(float(value) for value in obj.dimensions)


def triangle_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def bounds_points(obj):
    return [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]


def bottom_center_geometry(obj):
    points = bounds_points(obj)
    center_x = (min(point.x for point in points) + max(point.x for point in points)) * 0.5
    center_y = (min(point.y for point in points) + max(point.y for point in points)) * 0.5
    minimum_z = min(point.z for point in points)
    obj.data.transform(Matrix.Translation(Vector((-center_x, -center_y, -minimum_z))))
    obj.data.update()
    obj.location = Vector((0.0, 0.0, 0.0))


def assert_close_dimensions(actual, expected, label):
    differences = [abs(actual[index] - expected[index]) for index in range(3)]
    if any(difference > DIMENSION_TOLERANCE for difference in differences):
        raise RuntimeError(
            f"{label} dimensions outside tolerance: actual={actual}; "
            f"expected={tuple(expected)}; tolerance={DIMENSION_TOLERANCE}"
        )


def load_counter_source(work_table_path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    with bpy.data.libraries.load(str(work_table_path), link=False) as (data_from, data_to):
        data_to.objects = data_from.objects
    objects = [obj for obj in data_to.objects if obj is not None]
    meshes = [obj for obj in objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(
            f"Counter source must contain exactly one Mesh; found {len(meshes)}."
        )
    for obj in objects:
        bpy.context.scene.collection.objects.link(obj)
    target = meshes[0]
    remove_everything_except(target)
    return target


def counter_material_evidence(obj):
    materials = [material for material in obj.data.materials if material is not None]
    if len(materials) != 1:
        raise RuntimeError(f"Counter requires exactly one source Material; found {len(materials)}.")
    material = materials[0]
    image_nodes = [
        node for node in material.node_tree.nodes
        if node.type == "TEX_IMAGE" and node.image is not None
    ] if material.node_tree is not None else []
    if len(image_nodes) != 1:
        raise RuntimeError(
            f"Counter requires exactly one source image node; found {len(image_nodes)}."
        )
    image = image_nodes[0].image
    if image.packed_file is None:
        raise RuntimeError("Counter source image must remain packed.")
    return material, image


def find_cash_mesh():
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("Cash GLB contains no Mesh.")
    mesh_with_counts = [(obj, triangle_count(obj)) for obj in meshes]
    target, triangles = max(mesh_with_counts, key=lambda item: item[1])
    if triangles <= 12:
        raise RuntimeError("Could not distinguish the high-detail terminal Mesh from raw Cube.")
    return target, triangles


def remove_everything_except(target):
    for obj in list(bpy.data.objects):
        if obj != target:
            bpy.data.objects.remove(obj, do_unlink=True)


def production_image_for_material(material, texture_path):
    if material is None or not material.use_nodes:
        raise RuntimeError("Cash Register requires one node-based source Material.")
    image_nodes = [
        node for node in material.node_tree.nodes
        if node.type == "TEX_IMAGE" and node.image is not None
    ]
    if len(image_nodes) != 1:
        raise RuntimeError(
            f"Cash Register requires exactly one Base Color image node; found {len(image_nodes)}."
        )
    source_image = image_nodes[0].image
    source_size = tuple(source_image.size)
    if source_size != (2048, 2048):
        raise RuntimeError(f"Unexpected Cash source image size: {source_size}")

    source_image.scale(TARGET_TEXTURE_SIZE, TARGET_TEXTURE_SIZE)
    source_image.file_format = "PNG"
    source_image.filepath_raw = str(texture_path)
    source_image.save()

    production_image = bpy.data.images.load(str(texture_path), check_existing=False)
    production_image.name = "T_Equipment_CashRegister_BaseColor_01"
    production_image.colorspace_settings.name = "sRGB"
    production_image.pack()
    image_nodes[0].image = production_image
    if source_image.users == 0:
        bpy.data.images.remove(source_image)
    return source_size, production_image


def select_only(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def save_cash_source(path, obj, source_hash):
    bpy.context.preferences.filepaths.save_version = 0
    obj["AnimalCafeSourceSha256"] = source_hash
    obj["AnimalCafeProductionTargetXYZ"] = "0.43 width, 0.26 depth, 0.45 height"
    obj["AnimalCafeEmployeeForwardBlender"] = "+Y"
    obj["AnimalCafeUnityForward"] = "+Z via FBX Forward -Z / Up Y"
    bpy.context.scene["animalcafe_phase"] = "Phase4 Task7"
    bpy.context.scene["raw_source_unchanged"] = True
    bpy.ops.wm.save_as_mainfile(filepath=str(path), check_existing=False)


def verify_reopened_cash(path):
    bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Reopened Cash source has {len(meshes)} Meshes; expected 1.")
    if any(obj.type in {"CAMERA", "LIGHT"} for obj in bpy.context.scene.objects):
        raise RuntimeError("Reopened Cash source contains Camera or Light.")
    obj = meshes[0]
    actual_dimensions = dimensions_tuple(obj)
    assert_close_dimensions(actual_dimensions, CASH_TARGET_XYZ, "Cash Register")
    if any(abs(value) > 0.00001 for value in obj.location):
        raise RuntimeError(f"Reopened Cash object location is not zero: {tuple(obj.location)}")
    if any(abs(value) > 0.00001 for value in obj.rotation_euler):
        raise RuntimeError(f"Reopened Cash object rotation is not identity: {tuple(obj.rotation_euler)}")
    if any(abs(value - 1.0) > 0.00001 for value in obj.scale):
        raise RuntimeError(f"Reopened Cash object scale is not one: {tuple(obj.scale)}")

    points = bounds_points(obj)
    minimum_z = min(point.z for point in points)
    center_x = (min(point.x for point in points) + max(point.x for point in points)) * 0.5
    center_y = (min(point.y for point in points) + max(point.y for point in points)) * 0.5
    if max(abs(minimum_z), abs(center_x), abs(center_y)) > 0.0001:
        raise RuntimeError(
            f"Reopened Cash pivot is not bottom-center: minZ={minimum_z}, "
            f"centerX={center_x}, centerY={center_y}"
        )

    triangles = triangle_count(obj)
    if triangles > CASH_MAX_TRIANGLES:
        raise RuntimeError(f"Reopened Cash has {triangles} triangles; cap is {CASH_MAX_TRIANGLES}.")
    materials = [material for material in obj.data.materials if material is not None]
    if len(materials) != 1:
        raise RuntimeError(f"Reopened Cash has {len(materials)} Materials; expected 1.")
    image_nodes = [
        node for node in materials[0].node_tree.nodes
        if node.type == "TEX_IMAGE" and node.image is not None
    ]
    if len(image_nodes) != 1 or tuple(image_nodes[0].image.size) != (512, 512):
        raise RuntimeError("Reopened Cash does not contain exactly one packed 512 x 512 image.")
    if image_nodes[0].image.packed_file is None:
        raise RuntimeError("Reopened Cash production image is not packed.")
    return obj, actual_dimensions, triangles, materials, image_nodes[0].image


def export_fbx(path, obj):
    select_only(obj)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        apply_scale_options="FBX_SCALE_ALL",
        use_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        path_mode="STRIP",
        embed_textures=False,
        bake_space_transform=True,
    )


def create_preview(path, obj, camera_location):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.filepath = str(path)
    if scene.world is None:
        scene.world = bpy.data.worlds.new("PreviewOnly_World")
    scene.world.color = (0.035, 0.035, 0.035)

    bpy.ops.mesh.primitive_plane_add(size=4.0, location=(0.0, 0.0, -0.002))
    ground = bpy.context.active_object
    ground.name = "PreviewOnly_Ground"
    ground_material = bpy.data.materials.new("PreviewOnly_GroundMaterial")
    ground_material.diffuse_color = (0.19, 0.17, 0.14, 1.0)
    ground.data.materials.append(ground_material)

    bpy.ops.object.camera_add(location=camera_location)
    camera = bpy.context.active_object
    camera.name = "PreviewOnly_Camera"
    target = Vector((0.0, 0.0, float(obj.dimensions.z) * 0.45))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.lens = 58
    scene.camera = camera

    for name, location, energy, size in (
        ("PreviewOnly_Key", (1.8, 1.5, 2.4), 900, 2.0),
        ("PreviewOnly_Fill", (-1.3, 0.4, 1.2), 450, 1.5),
    ):
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.active_object
        light.name = name
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        light.rotation_euler = (target - light.location).to_track_quat("-Z", "Y").to_euler()

    bpy.ops.render.render(write_still=True)


def save_counter_source(path, obj, source_hash, scale_factors):
    bpy.context.preferences.filepaths.save_version = 0
    obj["AnimalCafeSourceSha256"] = source_hash
    obj["AnimalCafeStudioOwnerApproval"] = (
        "2026-08-04 controlled non-uniform Counter derivative"
    )
    obj["AnimalCafePerAxisScaleXYZ"] = json.dumps(scale_factors)
    obj["AnimalCafeProductionTargetXYZ"] = "1.00 width, 1.00 depth, 0.72 height"
    obj["AnimalCafeForwardBlender"] = "+Y"
    obj["AnimalCafeUnityForward"] = "+Z via FBX Forward -Z / Up Y"
    bpy.context.scene["animalcafe_phase"] = "Phase4 Task7"
    bpy.context.scene["raw_source_unchanged"] = True
    bpy.ops.wm.save_as_mainfile(filepath=str(path), check_existing=False)


def verify_reopened_counter(path):
    bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Reopened Counter source has {len(meshes)} Meshes; expected 1.")
    if any(obj.type in {"CAMERA", "LIGHT"} for obj in bpy.context.scene.objects):
        raise RuntimeError("Reopened Counter source contains Camera or Light.")
    obj = meshes[0]
    actual_dimensions = dimensions_tuple(obj)
    assert_close_dimensions(actual_dimensions, COUNTER_TARGET_XYZ, "Counter")
    if any(abs(value) > 0.00001 for value in obj.location):
        raise RuntimeError(f"Reopened Counter location is not zero: {tuple(obj.location)}")
    if any(abs(value) > 0.00001 for value in obj.rotation_euler):
        raise RuntimeError(f"Reopened Counter rotation is not identity: {tuple(obj.rotation_euler)}")
    if any(abs(value - 1.0) > 0.00001 for value in obj.scale):
        raise RuntimeError(f"Reopened Counter scale is not one: {tuple(obj.scale)}")

    points = bounds_points(obj)
    minimum_z = min(point.z for point in points)
    center_x = (min(point.x for point in points) + max(point.x for point in points)) * 0.5
    center_y = (min(point.y for point in points) + max(point.y for point in points)) * 0.5
    if max(abs(minimum_z), abs(center_x), abs(center_y)) > 0.0001:
        raise RuntimeError(
            f"Reopened Counter pivot is not bottom-center: minZ={minimum_z}, "
            f"centerX={center_x}, centerY={center_y}"
        )

    triangles = triangle_count(obj)
    material, image = counter_material_evidence(obj)
    return obj, actual_dimensions, triangles, material, image


def build_counter(project_root, work_table_path, source_hash):
    blend_path = project_root / "ArtSource/Phase4/Blender/SM_Furniture_CounterModule_01.blend"
    fbx_path = project_root / "Assets/Art/Phase4/Models/SM_Furniture_CounterModule_01.fbx"
    preview_dir = project_root / "ArtSource/Phase4/Previews"
    for directory in (blend_path.parent, fbx_path.parent, preview_dir):
        directory.mkdir(parents=True, exist_ok=True)

    obj = load_counter_source(work_table_path)
    input_dimensions = dimensions_tuple(obj)
    input_triangles = triangle_count(obj)
    source_material, source_image = counter_material_evidence(obj)
    source_material_name = source_material.name
    source_image_name = source_image.name
    source_image_size = tuple(source_image.size)

    scale_factors = tuple(
        float(COUNTER_TARGET_XYZ[index]) / input_dimensions[index]
        for index in range(3)
    )
    obj.scale = Vector(scale_factors)
    select_only(obj)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bottom_center_geometry(obj)
    obj.name = "SM_Furniture_CounterModule_01"
    obj.data.name = "SM_Furniture_CounterModule_01"
    assert_close_dimensions(dimensions_tuple(obj), COUNTER_TARGET_XYZ, "Counter")

    save_counter_source(blend_path, obj, source_hash, scale_factors)
    reopened, output_dimensions, output_triangles, material, image = (
        verify_reopened_counter(blend_path)
    )
    if material.name != source_material_name or image.name != source_image_name:
        raise RuntimeError("Counter source Material or image identity changed during production.")
    if tuple(image.size) != source_image_size:
        raise RuntimeError("Counter packed source image dimensions changed during production.")
    export_fbx(fbx_path, reopened)
    create_preview(
        preview_dir / "SM_Furniture_CounterModule_01_employee_front_preview.png",
        reopened,
        Vector((1.65, 1.85, 1.35)),
    )

    metrics = {
        "asset": "SM_Furniture_CounterModule_01",
        "input_dimensions_blender_xyz_m": input_dimensions,
        "output_dimensions_blender_xyz_m": output_dimensions,
        "output_dimensions_unity_xyz_m": (
            output_dimensions[0], output_dimensions[2], output_dimensions[1]
        ),
        "input_triangles": input_triangles,
        "output_triangles": output_triangles,
        "materials": [material.name],
        "packed_image": image.name,
        "packed_image_size": tuple(image.size),
        "controlled_scale_xyz": scale_factors,
        "forward_blender": "+Y",
        "unity_forward": "+Z",
        "reopen_verified": True,
    }
    print("TASK7_COUNTER_METRICS " + json.dumps(metrics, sort_keys=True))
    return metrics


def build_cash(project_root, raw_path, source_hash):
    blend_path = project_root / "ArtSource/Phase4/Blender/SM_Equipment_CashRegister_01.blend"
    fbx_path = project_root / "Assets/Art/Phase4/Models/SM_Equipment_CashRegister_01.fbx"
    texture_path = project_root / "Assets/Art/Phase4/Textures/T_Equipment_CashRegister_BaseColor_01.png"
    preview_dir = project_root / "ArtSource/Phase4/Previews"
    for directory in (blend_path.parent, fbx_path.parent, texture_path.parent, preview_dir):
        directory.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(raw_path))
    obj, input_triangles = find_cash_mesh()
    input_dimensions = dimensions_tuple(obj)
    remove_everything_except(obj)

    # The raw Tripo screen faces -Y (its supplied Camera is on the -Y side).
    # Rotate 180 degrees so the Employee screen side is Blender +Y.
    obj.rotation_euler.z += radians(180.0)
    uniform_scale = 0.43 / float(obj.dimensions.x)
    obj.scale *= uniform_scale
    select_only(obj)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bottom_center_geometry(obj)
    obj.name = "SM_Equipment_CashRegister_01"
    obj.data.name = "SM_Equipment_CashRegister_01"

    triangles = triangle_count(obj)
    if triangles > CASH_MAX_TRIANGLES:
        raise RuntimeError(
            f"Cash cleanup has {triangles} triangles; optimization decision is required."
        )
    if len(obj.data.materials) != 1:
        raise RuntimeError(f"Cash high-detail Mesh has {len(obj.data.materials)} Material slots.")
    material = obj.data.materials[0]
    material.name = "M_Equipment_CashRegister_Source_01"
    source_texture_size, production_image = production_image_for_material(
        material, texture_path
    )
    assert_close_dimensions(dimensions_tuple(obj), CASH_TARGET_XYZ, "Cash Register")

    save_cash_source(blend_path, obj, source_hash)
    reopened, output_dimensions, output_triangles, materials, image = verify_reopened_cash(
        blend_path
    )
    export_fbx(fbx_path, reopened)
    create_preview(
        preview_dir / "SM_Equipment_CashRegister_01_employee_front_preview.png",
        reopened,
        Vector((0.78, 1.00, 0.72)),
    )

    metrics = {
        "asset": "SM_Equipment_CashRegister_01",
        "input_dimensions_blender_xyz_m": input_dimensions,
        "output_dimensions_blender_xyz_m": output_dimensions,
        "output_dimensions_unity_xyz_m": (
            output_dimensions[0], output_dimensions[2], output_dimensions[1]
        ),
        "input_triangles": input_triangles,
        "output_triangles": output_triangles,
        "source_texture_size": source_texture_size,
        "production_texture_size": tuple(image.size),
        "materials": [item.name for item in materials],
        "uniform_scale": uniform_scale,
        "employee_forward_blender": "+Y",
        "unity_forward": "+Z",
        "reopen_verified": True,
    }
    print("TASK7_CASH_METRICS " + json.dumps(metrics, sort_keys=True))
    return metrics


def main():
    try:
        separator = sys.argv.index("--")
    except ValueError as error:
        raise RuntimeError("Expected Blender arguments after '--'.") from error
    args = sys.argv[separator + 1:]
    if len(args) not in (3, 4):
        raise RuntimeError(
            "Expected <project-root> <work-table.blend> <cash.glb> "
            "[all|counter-only|cash-only]."
        )

    project_root = Path(args[0]).resolve()
    work_table_path = Path(args[1]).resolve()
    cash_path = Path(args[2]).resolve()
    mode = args[3] if len(args) == 4 else "all"
    if mode not in {"all", "counter-only", "cash-only"}:
        raise RuntimeError(f"Unknown production mode: {mode}")

    work_hash_before = require_hash(work_table_path, WORK_TABLE_SHA256)
    cash_hash_before = require_hash(cash_path, CASH_REGISTER_SHA256)
    if mode in {"all", "counter-only"}:
        build_counter(project_root, work_table_path, work_hash_before)
    if mode in {"all", "cash-only"}:
        build_cash(project_root, cash_path, cash_hash_before)

    work_hash_after = require_hash(work_table_path, WORK_TABLE_SHA256)
    cash_hash_after = require_hash(cash_path, CASH_REGISTER_SHA256)
    print(
        "TASK7_PROTECTED_HASHES "
        f"work_before={work_hash_before} work_after={work_hash_after} "
        f"cash_before={cash_hash_before} cash_after={cash_hash_after} unchanged=True"
    )


if __name__ == "__main__":
    main()
