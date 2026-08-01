"""Export Studio Owner-approved original benchmark meshes without LOD0 rebuilds.

Studio Owner override, 2026-08-01:
the three user-re-supplied Raw Blender inputs are the authoritative LOD0
sources. This tool first copies each Raw file byte-for-byte to its tracked
``Blender/`` counterpart and verifies the SHA-256 equality. It never saves,
applies modifiers to, decimates, normalizes, or retopologizes an LOD0 object.

Coffee Machine alone receives an in-memory LOD1 duplicate for FBX export.
That derivative is not written back to the authoritative Coffee `.blend`.
"""

from hashlib import sha256
from pathlib import Path
import shutil

import bpy


BENCHMARKS = Path(__file__).resolve().parents[1]
RAW_DIR = BENCHMARKS / "Raw"
BLENDER_DIR = BENCHMARKS / "Blender"
MODEL_DIR = BENCHMARKS.parents[2] / "Assets" / "Art" / "VisualPipeline" / "Benchmarks" / "Models"
MAX_LOD0_TRIANGLES = 6000
MAX_COFFEE_LOD1_TRIANGLES = 2500
MAX_COFFEE_LOD1_RATIO = 0.60

SOURCES = {
    "WorkTable": {
        "raw": "SM_Benchmark_WorkTable_01_user_resupplied_original.blend",
        "authoritative": "SM_Benchmark_WorkTable_01.blend",
        "fbx": "SM_Benchmark_WorkTable_01.fbx",
        "mesh": "SM_Benchmark_WorkTable_01",
    },
    "CoffeeMachine": {
        "raw": "SM_Benchmark_CoffeeMachine_01_user_resupplied_original.blend",
        "authoritative": "SM_Benchmark_CoffeeMachine_01.blend",
        "fbx": "SM_Benchmark_CoffeeMachine_01.fbx",
        "mesh": "SM_Benchmark_CoffeeMachine_01",
    },
    "CeramicCup": {
        "raw": "SM_Benchmark_CeramicCup_01_user_resupplied_original.blend",
        "authoritative": "SM_Benchmark_CeramicCup_01.blend",
        "fbx": "SM_Benchmark_CeramicCup_01.fbx",
        "mesh": "SM_Benchmark_CeramicCup_01",
    },
}


def sha256_file(path):
    return sha256(path.read_bytes()).hexdigest().upper()


def raw_path(kind):
    return RAW_DIR / kind / SOURCES[kind]["raw"]


def authoritative_path(kind):
    return BLENDER_DIR / SOURCES[kind]["authoritative"]


def copy_authoritative_raw(kind):
    """Copy Raw bytes before any export and prove the files are identical."""
    source = raw_path(kind)
    destination = authoritative_path(kind)
    if not source.is_file():
        raise RuntimeError(f"Missing user-re-supplied Raw input: {source}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, destination)
    raw_hash = sha256_file(source)
    authoritative_hash = sha256_file(destination)
    if raw_hash != authoritative_hash:
        raise RuntimeError(f"Authoritative copy mismatch for {kind}.")
    print(
        "TASK5_AUTHORITATIVE_COPY "
        f"kind={kind} raw={source.name} authoritative={destination.name} "
        f"sha256={raw_hash} identical=True"
    )
    return destination, raw_hash


def open_authoritative(kind):
    path, raw_hash = copy_authoritative_raw(kind)
    bpy.ops.wm.open_mainfile(filepath=str(path))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one Raw mesh in authoritative {path}, found {len(meshes)}.")
    return meshes[0], raw_hash


def triangle_count(obj):
    return sum(max(0, len(polygon.vertices) - 2) for polygon in obj.data.polygons)


def select_only(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def export_fbx(kind, objects):
    select_only(objects)
    output = MODEL_DIR / SOURCES[kind]["fbx"]
    bpy.ops.export_scene.fbx(
        filepath=str(output),
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=False,
        apply_scale_options="FBX_SCALE_ALL",
        use_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        path_mode="AUTO",
        embed_textures=False,
        bake_space_transform=False,
    )
    print(f"TASK5_RAW_EXPORT kind={kind} fbx={output.name}")


def rename_for_export(obj, name):
    """Names are assigned in memory only; the authoritative `.blend` stays raw."""
    obj.name = name
    obj.data.name = name


def export_simple_raw(kind):
    lod0, raw_hash = open_authoritative(kind)
    triangles = triangle_count(lod0)
    if triangles > MAX_LOD0_TRIANGLES:
        raise RuntimeError(f"{kind} Raw LOD0 has {triangles} triangles; cap is {MAX_LOD0_TRIANGLES}.")
    rename_for_export(lod0, SOURCES[kind]["mesh"])
    lod0["Task5RawInput"] = SOURCES[kind]["raw"]
    lod0["Task5RawSha256"] = raw_hash
    lod0["Task5OwnerOverride"] = "2026-08-01 preserve original LOD0 geometry"
    export_fbx(kind, [lod0])


def export_coffee_raw_with_lod1():
    lod0, raw_hash = open_authoritative("CoffeeMachine")
    lod0_triangles = triangle_count(lod0)
    if lod0_triangles > MAX_LOD0_TRIANGLES:
        raise RuntimeError(f"Coffee Raw LOD0 has {lod0_triangles} triangles; cap is {MAX_LOD0_TRIANGLES}.")

    # Rename only in memory. LOD0's mesh data and transforms are otherwise untouched.
    rename_for_export(lod0, "SM_Benchmark_CoffeeMachine_01_LOD0")
    lod0["Task5RawInput"] = SOURCES["CoffeeMachine"]["raw"]
    lod0["Task5RawSha256"] = raw_hash
    lod0["Task5OwnerOverride"] = "2026-08-01 original LOD0; no cleanup or decimation"

    # LOD1 is an independent in-memory copy. Its decimation cannot affect LOD0
    # or the byte-identical authoritative Blender file on disk.
    lod1 = lod0.copy()
    lod1.data = lod0.data.copy()
    bpy.context.collection.objects.link(lod1)
    rename_for_export(lod1, "SM_Benchmark_CoffeeMachine_01_LOD1")
    select_only([lod1])
    modifier = lod1.modifiers.new("OwnerApprovedLod1Simplification", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = 0.45
    bpy.ops.object.modifier_apply(modifier=modifier.name)

    lod1_triangles = triangle_count(lod1)
    if lod1_triangles > MAX_COFFEE_LOD1_TRIANGLES:
        raise RuntimeError(f"Coffee LOD1 has {lod1_triangles} triangles; cap is {MAX_COFFEE_LOD1_TRIANGLES}.")
    if lod1_triangles / lod0_triangles > MAX_COFFEE_LOD1_RATIO:
        raise RuntimeError("Coffee LOD1 is not at least 40% lower than original LOD0.")
    lod1["Task5OwnerOverride"] = "2026-08-01 independent simplified LOD1 derivative"
    print(
        "TASK5_COFFEE_LODS "
        f"lod0_triangles={lod0_triangles} lod1_triangles={lod1_triangles} "
        f"ratio={lod1_triangles / lod0_triangles:.4f}"
    )
    export_fbx("CoffeeMachine", [lod0, lod1])


def main():
    MODEL_DIR.mkdir(parents=True, exist_ok=True)
    export_simple_raw("WorkTable")
    export_coffee_raw_with_lod1()
    export_simple_raw("CeramicCup")
    print("TASK5_OWNER_APPROVED_RAW_EXPORT_COMPLETE")


if __name__ == "__main__":
    main()
