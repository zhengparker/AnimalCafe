"""Export only the existing character body and default walk.

用 Blender 后台运行；只读打开母版，不保存 .blend。
blender --background --factory-startup --disable-autoexec --python this_file -- \
  --source-root PROJECT_ROOT --output-root CHECKOUT/Assets/Art/Phase11/Characters
"""

import argparse
import json
from pathlib import Path
import sys

import bpy


CHARACTERS = {
    "Shiba": ("Shiba/AnimalCafe_Shiba_Walk_Default_v01.blend", 30),
    "Westie": ("Westie/AnimalCafe_Westie_Walk_Default_v01.blend", 24),
}


def source_path(root: Path, relative: str) -> Path:
    direct = root / relative
    nested = root / "Blender Model Item" / relative
    found = direct if direct.is_file() else nested
    if not found.is_file():
        raise FileNotFoundError(f"Missing walk source: {direct} or {nested}")
    return found


def export_one(name: str, relative: str, expected_fps: int, source_root: Path, output_root: Path):
    source = source_path(source_root, relative)
    # load_ui=False and --disable-autoexec prevent source UI/scripts from running.
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    scene = bpy.context.scene
    fps = scene.render.fps / scene.render.fps_base
    if abs(fps - expected_fps) > 0.001:
        raise ValueError(f"{name}: expected {expected_fps} fps, found {fps}")

    action = bpy.data.actions.get("AC_Core_Walk_Default")
    if action is None or tuple(action.frame_range) != (1.0, 21.0):
        raise ValueError(f"{name}: default walk Action must span frames 1–21")
    rigs = [obj for obj in scene.objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in scene.objects if obj.type == "MESH"]
    if len(rigs) != 1 or len(meshes) != 1 or rigs[0].name != "AnimalCafe_MasterRig":
        raise ValueError(f"{name}: expected one master rig and one body Mesh")
    rig, mesh = rigs[0], meshes[0]
    if not any(mod.type == "ARMATURE" and mod.object == rig for mod in mesh.modifiers):
        raise ValueError(f"{name}: body Mesh is not bound to master rig")

    # Keep only this Action in memory. The source .blend is never saved.
    if rig.animation_data is None:
        rig.animation_data_create()
    for track in list(rig.animation_data.nla_tracks):
        rig.animation_data.nla_tracks.remove(track)
    rig.animation_data.action = action
    scene.frame_start = 1
    scene.frame_end = 21  # Shiba's scene ends at 20; the Action ends at 21.

    images = []
    for material in mesh.data.materials:
        if material and material.use_nodes:
            for node in material.node_tree.nodes:
                if node.type == "TEX_IMAGE" and node.image and node.image.packed_file:
                    if node.image not in images:
                        images.append(node.image)
    if len(images) != 1:
        raise ValueError(f"{name}: expected one referenced packed BaseColor image, got {len(images)}")

    folder = output_root / name
    folder.mkdir(parents=True, exist_ok=True)
    texture = folder / f"T_{name}_BaseColor.png"
    image = images[0]
    image.filepath_raw = str(texture)
    image.file_format = "PNG"
    image.save()

    for obj in scene.objects:
        obj.select_set(False)
    rig.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = rig
    fbx = folder / f"SM_{name}_Walk_Default.fbx"
    bpy.ops.export_scene.fbx(
        filepath=str(fbx), use_selection=True, object_types={"ARMATURE", "MESH"},
        axis_forward="-Z", axis_up="Y", global_scale=1.0, apply_unit_scale=True,
        add_leaf_bones=False, use_armature_deform_only=True,
        bake_anim=True, bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0, path_mode="STRIP",
    )
    if not fbx.is_file() or not texture.is_file():
        raise RuntimeError(f"{name}: FBX or BaseColor extraction failed")
    print("AC_EXPORT " + json.dumps({"character": name, "fps": fps,
        "action_first": 1, "action_last": 21, "fbx_bytes": fbx.stat().st_size,
        "texture_bytes": texture.stat().st_size}))


def main():
    arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", required=True, type=Path)
    parser.add_argument("--output-root", required=True, type=Path)
    args = parser.parse_args(arguments)
    for name, (relative, fps) in CHARACTERS.items():
        export_one(name, relative, fps, args.source_root.resolve(), args.output_root.resolve())


if __name__ == "__main__":
    main()
