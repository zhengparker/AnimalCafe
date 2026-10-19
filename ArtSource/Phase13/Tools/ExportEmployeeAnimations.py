"""Read-only employee export. 只打开母版；永不保存 .blend，排除参考杯。"""
import argparse
import json
from pathlib import Path
import sys
import bpy
from mathutils import Vector

def export_one(species, kind, suffix, last, fps, source_root, output_root):
    source = source_root / "Blender Model Item" / species / f"AnimalCafe_{species}_{kind}_v01.blend"
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
    scene = bpy.context.scene
    if abs(scene.render.fps / scene.render.fps_base - fps) > .001:
        raise ValueError(f"Unexpected fps: {source}")
    action = bpy.data.actions.get("AC_Core_" + kind)
    rigs = [o for o in scene.objects if o.type == "ARMATURE"]
    bodies = [o for o in scene.objects if o.type == "MESH" and o.name != "CarryProp_Cup"]
    if len(rigs) != 1 or len(bodies) != 1 or action is None or tuple(action.frame_range) != (1.,float(last)):
        raise ValueError(f"Unexpected source contract: {source}")
    rig, body = rigs[0], bodies[0]
    if "hand_socket.R" not in rig.data.bones:
        raise ValueError("Missing hand socket")
    if rig.animation_data is None: rig.animation_data_create()
    for track in list(rig.animation_data.nla_tracks): rig.animation_data.nla_tracks.remove(track)
    rig.animation_data.action = action
    scene.frame_start, scene.frame_end = 1, last
    scene.frame_set(1)
    folder = output_root / species
    folder.mkdir(parents=True, exist_ok=True)
    if kind == "Carry_Cup":
        cup = scene.objects["CarryProp_Cup"]
        bottom = cup.matrix_world @ Vector((0,0,-.05))
        hand = rig.matrix_world @ rig.pose.bones["hand.R"].matrix.translation
        socket = rig.matrix_world @ rig.pose.bones["hand_socket.R"].matrix.translation
        # Source-space references only; Builder verifies actual Unity conversion/fit.
        (folder / "carry-reference.json").write_text(json.dumps({"bottom":list(bottom),"hand":list(hand),"socket":list(socket)},indent=2))
    for obj in scene.objects: obj.select_set(False)
    rig.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active = rig
    target = folder / f"SM_{species}_{suffix}.fbx"
    bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,object_types={"ARMATURE","MESH"},
        axis_forward="-Z",axis_up="Y",global_scale=1.,apply_unit_scale=True,add_leaf_bones=False,
        use_armature_deform_only=False,bake_anim=True,bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,
        bake_anim_step=1.,bake_anim_simplify_factor=0.,path_mode="STRIP")
    print("AC_EMPLOYEE_EXPORT " + json.dumps({"species":species,"kind":kind,"first":1,"last":last,"fps":fps,"bytes":target.stat().st_size}))

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--source-root",required=True,type=Path)
    parser.add_argument("--output-root",required=True,type=Path)
    args=parser.parse_args(sys.argv[sys.argv.index("--")+1:])
    for species,fps in (("Shiba",30),("Westie",24)):
        for kind,suffix,last in (("Walk_Default","Employee_Walk",21),("Work_Generic_Busy","Work_Generic_Busy",37),("Carry_Cup","Carry_Cup",2)):
            export_one(species,kind,suffix,last,fps,args.source_root.resolve(),args.output_root.resolve())
if __name__ == "__main__": main()
