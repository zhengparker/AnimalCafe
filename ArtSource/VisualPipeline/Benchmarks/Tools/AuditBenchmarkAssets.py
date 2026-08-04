"""Audit one benchmark Blender file and render front/side comparison images.

This utility is deliberately read-only for the opened .blend file.  Run it
with Blender background mode and an output directory outside Unity Assets:

    blender --background input.blend --python AuditBenchmarkAssets.py -- \
        --label WorkTable_Raw --output-dir .superpowers/sdd/.../task-5-art-audit
"""

import argparse
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


def parse_args():
    argv = sys.argv
    if "--" not in argv:
        raise SystemExit("Expected audit arguments after '--'.")

    parser = argparse.ArgumentParser()
    parser.add_argument("--label", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument(
        "--front",
        choices=("negative-y", "not-applicable"),
        default="negative-y",
        help="Declared Blender source front for the report; it is not inferred.",
    )
    parser.add_argument(
        "--target-dimensions",
        help="Optional in-memory X,Y,Z scale applied before an audit probe.",
    )
    parser.add_argument(
        "--voxel-size",
        type=float,
        help="Optional in-memory Voxel Remesh size for a cleanup probe; never saves the opened file.",
    )
    parser.add_argument(
        "--render-include",
        help="Optional comma-separated Mesh names to render while retaining a full audit.",
    )
    return parser.parse_args(argv[argv.index("--") + 1 :])


def rounded(values):
    return [round(value, 6) for value in values]


def triangle_count(mesh):
    return sum(max(0, len(polygon.vertices) - 2) for polygon in mesh.polygons)


def non_manifold_edge_count(mesh):
    topology = bmesh.new()
    try:
        topology.from_mesh(mesh)
        return sum(1 for edge in topology.edges if not edge.is_manifold)
    finally:
        topology.free()


def mesh_world_bounds(objects):
    points = []
    for obj in objects:
        for corner in obj.bound_box:
            points.append(obj.matrix_world @ Vector(corner))

    minimum = Vector((min(point.x for point in points), min(point.y for point in points), min(point.z for point in points)))
    maximum = Vector((max(point.x for point in points), max(point.y for point in points), max(point.z for point in points)))
    return minimum, maximum


def normalize_single_mesh_in_memory(obj, dimensions):
    minimum, maximum = mesh_world_bounds([obj])
    current = maximum - minimum
    if min(current.x, current.y, current.z) <= 0.0:
        raise RuntimeError("Cannot normalize a zero-sized mesh.")
    obj.scale = (dimensions[0] / current.x, dimensions[1] / current.y, dimensions[2] / current.z)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    minimum, maximum = mesh_world_bounds([obj])
    offset = Vector(((minimum.x + maximum.x) * 0.5, (minimum.y + maximum.y) * 0.5, minimum.z))
    for vertex in obj.data.vertices:
        vertex.co -= offset
    obj.data.update()


def apply_voxel_probe(obj, voxel_size):
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    modifier = obj.modifiers.new("AuditVoxelProbe", "REMESH")
    modifier.mode = "VOXEL"
    modifier.voxel_size = voxel_size
    modifier.use_smooth_shade = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def look_at(camera, target):
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


def render_views(label, output_dir, mesh_objects, minimum, maximum, render_include=None):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.studio_light = "paint.sl"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False

    visible_names = set(render_include or [obj.name for obj in mesh_objects])
    for obj in scene.objects:
        obj.hide_render = obj.type != "MESH" or obj.name not in visible_names

    center = (minimum + maximum) * 0.5
    dimensions = maximum - minimum
    largest_dimension = max(dimensions.x, dimensions.y, dimensions.z, 0.1)
    distance = largest_dimension * 3.0

    camera_data = bpy.data.cameras.new("Task5AuditCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = largest_dimension * 1.35
    camera = bpy.data.objects.new("Task5AuditCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    views = {
        "front": Vector((center.x, minimum.y - distance, center.z)),
        "side": Vector((maximum.x + distance, center.y, center.z)),
    }
    for suffix, position in views.items():
        camera.location = position
        look_at(camera, center)
        scene.render.filepath = str(output_dir / f"{label}_{suffix}.png")
        bpy.ops.render.render(write_still=True)


def audit(label, front, output_dir, target_dimensions=None, voxel_size=None, render_include=None):
    mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not mesh_objects:
        raise RuntimeError("No mesh objects found in opened Blender file.")

    if target_dimensions is not None:
        if len(mesh_objects) != 1:
            raise RuntimeError("The in-memory normalization probe expects exactly one mesh.")
        normalize_single_mesh_in_memory(mesh_objects[0], target_dimensions)
    if voxel_size is not None:
        if len(mesh_objects) != 1:
            raise RuntimeError("The in-memory remesh probe expects exactly one mesh.")
        apply_voxel_probe(mesh_objects[0], voxel_size)

    hidden_objects = [
        obj.name
        for obj in bpy.context.scene.objects
        if obj.type == "MESH" and (obj.hide_get() or obj.hide_render)
    ]
    minimum, maximum = mesh_world_bounds(mesh_objects)
    object_records = []
    total_triangles = 0
    total_non_manifold = 0
    material_names = set()
    for obj in mesh_objects:
        mesh = obj.data
        triangles = triangle_count(mesh)
        non_manifold = non_manifold_edge_count(mesh)
        total_triangles += triangles
        total_non_manifold += non_manifold
        slots = [slot.material.name if slot.material else None for slot in obj.material_slots]
        material_names.update(name for name in slots if name)
        object_records.append(
            {
                "name": obj.name,
                "hidden": obj.name in hidden_objects,
                "location": rounded(obj.location),
                "rotation_euler_degrees": rounded(math.degrees(angle) for angle in obj.rotation_euler),
                "scale": rounded(obj.scale),
                "origin_world": rounded(obj.matrix_world.translation),
                "triangles": triangles,
                "material_slots": slots,
                "non_manifold_edges": non_manifold,
                "normal_vectors_present": len(mesh.polygons),
            }
        )

    output_dir.mkdir(parents=True, exist_ok=True)
    result = {
        "label": label,
        "opened_file": bpy.data.filepath,
        "declared_front": front,
        "probe_target_dimensions_xyz": target_dimensions,
        "probe_voxel_size": voxel_size,
        "render_include": render_include,
        "mesh_object_count": len(mesh_objects),
        "mesh_objects": object_records,
        "hidden_mesh_objects": hidden_objects,
        "internal_geometry": "Not automatically separable; assessed by front/side renders and topology counts.",
        "world_dimensions_xyz": rounded(maximum - minimum),
        "world_min_xyz": rounded(minimum),
        "world_max_xyz": rounded(maximum),
        "total_triangles": total_triangles,
        "unique_materials": sorted(material_names),
        "total_non_manifold_edges": total_non_manifold,
        "normal_audit": "Per-face normal vectors present; manifold count and rendered faces retained as evidence.",
    }
    (output_dir / f"{label}_audit.json").write_text(
        json.dumps(result, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    render_views(label, output_dir, mesh_objects, minimum, maximum, render_include)
    print(json.dumps(result, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    arguments = parse_args()
    dimensions = None
    if arguments.target_dimensions:
        dimensions = tuple(float(value) for value in arguments.target_dimensions.split(","))
        if len(dimensions) != 3:
            raise SystemExit("--target-dimensions requires X,Y,Z.")
    included = arguments.render_include.split(",") if arguments.render_include else None
    audit(
        arguments.label,
        arguments.front,
        Path(arguments.output_dir).resolve(),
        dimensions,
        arguments.voxel_size,
        included,
    )
