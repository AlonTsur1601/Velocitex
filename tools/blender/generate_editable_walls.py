import bpy
import json
import re
import sys
from pathlib import Path
from mathutils import Matrix


def numbers(value):
    return [float(item) for item in re.findall(r"[-+]?(?:\d*\.\d+|\d+)(?:[eE][-+]?\d+)?", value)]


def godot_transform(raw):
    n = numbers(raw)
    return Matrix(((n[0], n[3], n[6], n[9]),
                   (n[1], n[4], n[7], n[10]),
                   (n[2], n[5], n[8], n[11]),
                   (0.0, 0.0, 0.0, 1.0)))


CONVERT = Matrix(((1.0, 0.0, 0.0, 0.0),
                  (0.0, 0.0, -1.0, 0.0),
                  (0.0, 1.0, 0.0, 0.0),
                  (0.0, 0.0, 0.0, 1.0)))

ROOM_SHELL_NAMES = {"HazardFloor", "Ceiling", "LeftWall", "RightWall", "BackWall", "ExitWall"}


def correct_wall_slope_direction(world, size):
    basis = world.to_3x3()
    length_axis = 0 if size[0] > size[2] else 1
    side_axis = 1 - length_axis
    length = basis.col[length_axis]
    side = basis.col[side_axis]
    height = basis.col[2]
    normalized_length_z = length.z / length.length
    if abs(normalized_length_z) <= 0.025 or abs(normalized_length_z) >= 0.95:
        return world

    corrected_length = length.copy()
    corrected_length.z = -corrected_length.z
    if length_axis == 0:
        corrected_height = corrected_length.cross(side).normalized() * height.length
    else:
        corrected_height = side.cross(corrected_length).normalized() * height.length
    corrected = world.copy()
    corrected_basis = corrected.to_3x3()
    corrected_basis.col[length_axis] = corrected_length
    corrected_basis.col[side_axis] = side
    corrected_basis.col[2] = corrected_height
    for row in range(3):
        for column in range(3):
            corrected[row][column] = corrected_basis[row][column]
    return corrected


def create_box(collection, name, size, world, material, editable):
    sx, sy, sz = size[0] * 0.5, size[2] * 0.5, size[1] * 0.5
    vertices = [
        (-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz),
        (-sx, -sy, sz), (sx, -sy, sz), (sx, sy, sz), (-sx, sy, sz),
    ]
    faces = [
        (0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1),
        (1, 5, 6, 2), (2, 6, 7, 3), (4, 0, 3, 7),
    ]
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    # These objects are created in a scene that is not active yet. Assigning
    # matrix_world in that state is discarded when Blender activates/saves the
    # scene, leaving the object at the origin. With no parent, matrix_basis is
    # the exact persistent object transform we need.
    obj.matrix_basis = world
    obj.data.materials.append(material)
    obj.hide_select = not editable
    return obj, mesh


def build_room(source, destination, reference_path):
    text = source.read_text(encoding="utf-8-sig")
    scene = bpy.data.scenes.new(source.stem)
    collection = bpy.data.collections.new("EditableWalls")
    scene.collection.children.link(collection)
    reference_collection = bpy.data.collections.new("EditablePlatforms")
    scene.collection.children.link(reference_collection)
    shell_collection = bpy.data.collections.new("ROOM SHELL — HIDE HERE")
    scene.collection.children.link(shell_collection)
    material = bpy.data.materials.new("RoomWallMaterial")
    material.diffuse_color = (0.30, 0.39, 0.42, 1.0)
    reference_material = bpy.data.materials.new("RoomReferenceMaterial")
    reference_material.diffuse_color = (0.16, 0.24, 0.28, 1.0)
    created_objects = []
    created_meshes = []

    node_pattern = re.compile(
        r'^\[node name="([^"]+)" type="StaticBody3D" parent="\."[^\]]*\]\s*'
        r'(.*?)(?=^\[node |\Z)', re.MULTILINE | re.DOTALL)
    for name, block in node_pattern.findall(text):
        transform_match = re.search(r'^transform = Transform3D\(([^\n]+)\)', block, re.MULTILINE)
        size_match = re.search(r'^metadata/barrier_base_seam_size = Vector3\(([^\n]+)\)', block, re.MULTILINE)
        offset_match = re.search(r'^metadata/barrier_base_seam_offset = Vector3\(([^\n]+)\)', block, re.MULTILINE)
        if not size_match:
            continue
        size = numbers(size_match.group(1))
        offset = numbers(offset_match.group(1)) if offset_match else [0.0, 0.0, 0.0]
        body = godot_transform(transform_match.group(1)) if transform_match else Matrix.Identity(4)
        local_offset = Matrix.Translation((offset[0], offset[1], offset[2]))
        world = CONVERT @ body @ local_offset @ CONVERT.inverted()
        world = correct_wall_slope_direction(world, size)

        obj, mesh = create_box(collection, name, size, world, material, True)
        obj["godot_wall"] = True
        obj["godot_source_scene"] = source.name
        created_objects.append(obj)
        created_meshes.append(mesh)

    for index, entry in enumerate(json.loads(reference_path.read_text(encoding="utf-8"))):
        body = Matrix(((entry["transform"][0], entry["transform"][3], entry["transform"][6], entry["transform"][9]),
                       (entry["transform"][1], entry["transform"][4], entry["transform"][7], entry["transform"][10]),
                       (entry["transform"][2], entry["transform"][5], entry["transform"][8], entry["transform"][11]),
                       (0.0, 0.0, 0.0, 1.0)))
        world = CONVERT @ body @ CONVERT.inverted()
        obj, mesh = create_box(reference_collection, f"REF_{index:03}_{entry['name']}", entry["size"], world, reference_material, True)
        obj["godot_reference"] = True
        if entry["name"] in ROOM_SHELL_NAMES:
            reference_collection.objects.unlink(obj)
            shell_collection.objects.link(obj)
        created_objects.append(obj)
        created_meshes.append(mesh)

    scene["velocitex_room"] = source.stem.replace("Walls", "")
    scene["instructions"] = "Edit objects in EditableWalls/EditablePlatforms. Toggle ROOM SHELL — HIDE HERE to hide the outer room shell; the door stays visible. Keep object names unchanged."
    destination.parent.mkdir(parents=True, exist_ok=True)
    original_scene = bpy.context.window.scene
    try:
        bpy.context.window.scene = scene
        bpy.ops.wm.save_as_mainfile(
            filepath=str(destination),
            check_existing=False,
            copy=True,
            compress=True,
        )
    finally:
        bpy.context.window.scene = original_scene

    bpy.data.scenes.remove(scene)
    for obj in created_objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in created_meshes:
        bpy.data.meshes.remove(mesh)
    bpy.data.collections.remove(collection)
    bpy.data.collections.remove(reference_collection)
    bpy.data.collections.remove(shell_collection)
    bpy.data.materials.remove(material)
    bpy.data.materials.remove(reference_material)


args = sys.argv[sys.argv.index("--") + 1:]
project = Path(args[0]).resolve()
source_dir = project / "scenes" / "EditableWalls"
destination_dir = project / "assets" / "models" / "EditableWallsBlender"
reference_dir = project / "tools" / "blender" / "reference"
room_filter = args[1] if len(args) > 1 else "Room??Walls"
for source in sorted(source_dir.glob(f"{room_filter}.tscn")):
    room_stem = source.stem.replace("Walls", "")
    build_room(source, destination_dir / f"{source.stem}.blend", reference_dir / f"{room_stem}.json")
print("BLENDER_EDITABLE_WALLS_PASS")
