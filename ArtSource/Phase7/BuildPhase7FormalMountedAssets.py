import bpy
import os
import sys
import re
import hashlib

args = sys.argv[sys.argv.index("--") + 1:]
source, destination, expected_sha256 = args
with open(source, "rb") as source_file:
    actual_sha256 = hashlib.sha256(source_file.read()).hexdigest().upper()
if actual_sha256 != expected_sha256.upper():
    raise RuntimeError(f"Raw source SHA-256 mismatch: {os.path.basename(source)} {actual_sha256}")
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=source)
asset_stem = os.path.splitext(os.path.basename(destination))[0]
for image in bpy.data.images:
    if image.size[0] <= 0 or image.size[1] <= 0 or image.packed_file is None:
        continue
    safe_name = re.sub(r"[^A-Za-z0-9_]+", "_", image.name).strip("_")
    if image.size[0] > 1024 or image.size[1] > 1024:
        ratio = min(1024 / image.size[0], 1024 / image.size[1])
        image.scale(round(image.size[0] * ratio), round(image.size[1] * ratio))
    image.filepath_raw = os.path.join(os.path.dirname(destination), f"{asset_stem}_{safe_name}.png")
    image.file_format = "PNG"
    image.save()
for obj in bpy.context.scene.objects:
    obj.select_set(obj.type == "MESH")
bpy.ops.export_scene.fbx(
    filepath=destination,
    use_selection=True,
    object_types={"MESH"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Z",
    axis_up="Y",
    bake_anim=False,
    add_leaf_bones=False,
    path_mode="COPY",
    embed_textures=True,
)
