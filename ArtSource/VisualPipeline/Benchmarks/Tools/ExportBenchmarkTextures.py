"""Export audited packed base-color images without saving protected sources.

Run from the project root:

    E:\\Blender\\blender.exe --background --factory-startup --python \
        ArtSource/VisualPipeline/Benchmarks/Tools/ExportBenchmarkTextures.py

The script requires the exact material graph audited for the three protected
LOD0 sources. It writes only project-relative 512px PNG textures under Assets.
"""

from hashlib import sha256
from pathlib import Path

import bpy


BENCHMARKS = Path(__file__).resolve().parents[1]
RAW_DIR = BENCHMARKS / "Raw"
BLENDER_DIR = BENCHMARKS / "Blender"
TEXTURE_DIR = (
    BENCHMARKS.parents[2]
    / "Assets"
    / "Art"
    / "VisualPipeline"
    / "Benchmarks"
    / "Textures"
)
MAX_TEXTURE_SIZE = 512

SOURCES = {
    "WorkTable": {
        "raw": "SM_Benchmark_WorkTable_01_user_resupplied_original.blend",
        "blend": "SM_Benchmark_WorkTable_01.blend",
        "image": "wooden+dresser+3d+model_basecolor.jpg",
        "output": "T_Benchmark_WorkTable_BaseColor_01.png",
    },
    "CoffeeMachine": {
        "raw": "SM_Benchmark_CoffeeMachine_01_user_resupplied_original.blend",
        "blend": "SM_Benchmark_CoffeeMachine_01.blend",
        "image": "espresso+machine+3d+model_basecolor.jpg",
        "output": "T_Benchmark_CoffeeMachine_BaseColor_01.png",
    },
    "CeramicCup": {
        "raw": "SM_Benchmark_CeramicCup_01_user_resupplied_original.blend",
        "blend": "SM_Benchmark_CeramicCup_01.blend",
        "image": "green+mug+3d+model_basecolor.jpg",
        "output": "T_Benchmark_CeramicCup_BaseColor_01.png",
    },
}


def sha256_file(path):
    return sha256(path.read_bytes()).hexdigest().upper()


def require_audited_base_color_image(kind, expected_name):
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1 or len(meshes[0].material_slots) != 1:
        raise RuntimeError(f"{kind} must contain exactly one Mesh and one Material slot.")
    material = meshes[0].material_slots[0].material
    if material is None or not material.use_nodes or material.node_tree is None:
        raise RuntimeError(f"{kind} is missing its audited node Material.")

    principled = [node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"]
    if len(principled) != 1:
        raise RuntimeError(f"{kind} must contain exactly one Principled BSDF.")
    base_color = principled[0].inputs.get("Base Color")
    links = list(base_color.links) if base_color is not None else []
    if len(links) != 1 or links[0].from_node.type != "TEX_IMAGE":
        raise RuntimeError(f"{kind} Base Color must be driven by one Image Texture.")

    image = links[0].from_node.image
    if image is None or image.name != expected_name:
        raise RuntimeError(f"{kind} image does not match the audited source.")
    if image.colorspace_settings.name != "sRGB":
        raise RuntimeError(f"{kind} base-color image must use sRGB.")
    packed_files = list(image.packed_files)
    if len(packed_files) != 1:
        raise RuntimeError(f"{kind} must contain exactly one packed image payload.")
    packed_data = bytes(packed_files[0].packed_file.data)
    if not packed_data:
        raise RuntimeError(f"{kind} packed image payload is not readable.")
    expected_pixels = image.size[0] * image.size[1] * image.channels
    if image.size[0] <= 0 or image.size[1] <= 0 or len(image.pixels[:]) != expected_pixels:
        raise RuntimeError(f"{kind} packed image pixels are not readable.")
    return image, sha256(packed_data).hexdigest().upper()


def export_texture(kind, record):
    raw_path = RAW_DIR / kind / record["raw"]
    blend_path = BLENDER_DIR / record["blend"]
    raw_hash = sha256_file(raw_path)
    blend_hash = sha256_file(blend_path)
    if raw_hash != blend_hash:
        raise RuntimeError(f"Protected Raw/authoritative hash mismatch for {kind}.")

    bpy.ops.wm.open_mainfile(filepath=str(blend_path), load_ui=False)
    image, packed_hash = require_audited_base_color_image(kind, record["image"])
    width, height = image.size
    scale = min(1.0, MAX_TEXTURE_SIZE / max(width, height))
    target_width = max(1, round(width * scale))
    target_height = max(1, round(height * scale))
    image.scale(target_width, target_height)

    output_path = TEXTURE_DIR / record["output"]
    image.filepath_raw = str(output_path)
    image.file_format = "PNG"
    image.save()

    if sha256_file(raw_path) != raw_hash or sha256_file(blend_path) != blend_hash:
        raise RuntimeError(f"Protected source bytes changed while exporting {kind}.")
    print(
        "READABILITY_TEXTURE_EXPORT "
        f"kind={kind} packedSha256={packed_hash} "
        f"source={width}x{height} output={target_width}x{target_height} "
        f"outputSha256={sha256_file(output_path)} file={record['output']}"
    )


def main():
    TEXTURE_DIR.mkdir(parents=True, exist_ok=True)
    for kind, record in SOURCES.items():
        export_texture(kind, record)
    print("READABILITY_TEXTURE_EXPORT_COMPLETE")


if __name__ == "__main__":
    main()
