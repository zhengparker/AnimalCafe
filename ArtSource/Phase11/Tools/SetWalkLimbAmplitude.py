"""Adjust the original Walk Action's limb swing, preserving its timing and model.

先 dry-run；--apply 会备份并保存两份原版 .blend。factor 是相对原版的绝对倍率，重复运行不会叠乘。
Run with Blender --background --factory-startup --disable-autoexec --python this_file -- ...
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import shutil
import sys

import bpy
import numpy as np

CHARACTERS = {"Shiba": 30, "Westie": 24}
LIMBS = ("upper_arm.L", "upper_arm.R", "thigh.L", "thigh.R",
         "shin.L", "shin.R", "foot.L", "foot.R")
FACTOR_KEY = "animalcafe_walk_limb_amplitude"


def curves_of(action):
    return [curve for layer in action.layers for strip in layer.strips
            for bag in strip.channelbags for curve in bag.fcurves]


def body_profile(mesh):
    """Measure evaluated skin over the cycle, including between authored frames."""
    heights, floors = [], []
    for index in range(81):
        frame = 1 + index * .25
        bpy.context.scene.frame_set(int(frame), subframe=frame % 1)
        evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
        skin = evaluated.to_mesh()
        try:
            vertices = np.empty(len(skin.vertices) * 3, dtype=np.float64)
            skin.vertices.foreach_get("co", vertices)
            vertices = vertices.reshape(-1, 3)
            matrix = np.array(evaluated.matrix_world)
            world = vertices @ matrix[:3, :3].T + matrix[:3, 3]
            low, high = float(world[:, 2].min()), float(world[:, 2].max())
            floors.append(low)
            heights.append(high - low)
        finally:
            evaluated.to_mesh_clear()
    return {"height_min": min(heights), "height_max": max(heights),
            "floor_min": min(floors), "floor_max": max(floors),
            "first_floor": floors[0], "first_height": heights[0]}


def process(source, fps, factor, apply):
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    scene = bpy.context.scene
    action = bpy.data.actions["AC_Core_Walk_Default"]
    rig = next(obj for obj in scene.objects if obj.type == "ARMATURE")
    mesh = next(obj for obj in scene.objects if obj.type == "MESH")
    if tuple(action.frame_range) != (1.0, 21.0) or scene.render.fps / scene.render.fps_base != fps:
        raise ValueError(f"Unexpected original Walk timing: {source}")
    if rig.animation_data.action != action:
        raise ValueError(f"Walk must be the active original Action: {source}")
    old_factor = float(action.get(FACTOR_KEY, 1.0))
    if not math.isfinite(old_factor) or old_factor <= 0:
        raise ValueError("Invalid stored amplitude factor")
    old_frame, old_subframe = scene.frame_current, scene.frame_subframe
    before = body_profile(mesh)
    selected = []
    for bone in LIMBS:
        matches = [c for c in curves_of(action)
                   if c.data_path == f'pose.bones["{bone}"].rotation_euler' and c.array_index == 0]
        if len(matches) != 1 or rig.pose.bones[bone].rotation_mode != "XYZ":
            raise ValueError(f"Expected one XYZ limb swing curve: {bone}")
        selected.append((bone, matches[0]))
    swings = []
    for bone, curve in selected:
        values = [curve.evaluate(1 + i * .25) for i in range(81)]
        low, high = min(values), max(values)
        centre = (low + high) * .5
        ratio = factor / old_factor
        # Scale deviation, including Bezier handles; preserve resting bend and timing.
        # 只放大相对摆幅；保留膝盖/脚踝平均弯曲角度以及 keyframe 时间。
        for key in curve.keyframe_points:
            for point in (key.co, key.handle_left, key.handle_right):
                point.y = centre + (point.y - centre) * ratio
        curve.update()
        values = [curve.evaluate(1 + i * .25) for i in range(81)]
        actual = max(values) - min(values)
        if abs(actual - (high - low) * ratio) > 1e-5:
            raise ValueError(f"Swing amplification failed: {bone}")
        if abs(curve.evaluate(1) - curve.evaluate(21)) > 1e-5:
            raise ValueError(f"Loop seam changed: {bone}")
        swings.append({"bone": bone, "range_degrees": math.degrees(actual)})
    action[FACTOR_KEY] = factor
    after = body_profile(mesh)
    scene.frame_set(old_frame, subframe=old_subframe)
    if apply and abs(factor - old_factor) > 1e-6:
        # Backup is made explicitly before opening either source; avoid an extra .blend1.
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(source), check_existing=False)
    return {"source": str(source), "old_factor": old_factor, "factor": factor,
            "saved": apply and abs(factor - old_factor) > 1e-6,
            "before": before, "after": after, "swings": swings}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", type=Path, required=True)
    parser.add_argument("--factor", type=float, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--backup-root", type=Path)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    if not math.isfinite(args.factor) or args.factor <= 0:
        raise ValueError("factor must be finite and positive")
    sources = [(args.source_root / name / f"AnimalCafe_{name}_Walk_Default_v01.blend", fps)
               for name, fps in CHARACTERS.items()]
    for source, _ in sources:
        if not source.is_file():
            raise FileNotFoundError(source)
    if args.apply:
        if args.backup_root is None:
            raise ValueError("--apply requires --backup-root")
        args.backup_root.mkdir(parents=True, exist_ok=True)
        for source, _ in sources:
            backup = args.backup_root / source.name
            if backup.exists():
                raise FileExistsError(f"Choose a fresh backup folder: {backup}")
        for source, _ in sources:
            backup = args.backup_root / source.name
            shutil.copy2(source, backup)
            if hashlib.sha256(source.read_bytes()).digest() != hashlib.sha256(backup.read_bytes()).digest():
                raise RuntimeError(f"Backup verification failed: {source}")
    reports = [process(source, fps, args.factor, args.apply) for source, fps in sources]
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(reports, indent=2), encoding="utf-8")
    print("AC_WALK_AMPLITUDE " + json.dumps(reports))


if __name__ == "__main__":
    main()
