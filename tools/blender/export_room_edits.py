import bpy
import json
import re
import sys
from pathlib import Path
from mathutils import Matrix


CONVERT = Matrix(((1.0, 0.0, 0.0, 0.0),
                  (0.0, 0.0, -1.0, 0.0),
                  (0.0, 1.0, 0.0, 0.0),
                  (0.0, 0.0, 0.0, 1.0)))


def local_dimensions(obj):
    coordinates = [vertex.co for vertex in obj.data.vertices]
    return tuple(
        max(coordinate[axis] for coordinate in coordinates) -
        min(coordinate[axis] for coordinate in coordinates)
        for axis in range(3)
    )


def export_object(obj):
    location, rotation, scale = obj.matrix_basis.decompose()
    blender_rotation = rotation.to_matrix().to_4x4()
    blender_rotation.translation = location
    godot_world = CONVERT.inverted() @ blender_rotation @ CONVERT
    basis = godot_world.to_3x3()
    dimensions = local_dimensions(obj)
    blender_size = (
        dimensions[0] * abs(scale.x),
        dimensions[1] * abs(scale.y),
        dimensions[2] * abs(scale.z),
    )
    return {
        "name": obj.name,
        "size": [blender_size[0], blender_size[2], blender_size[1]],
        "transform": [
            basis.col[0].x, basis.col[0].y, basis.col[0].z,
            basis.col[1].x, basis.col[1].y, basis.col[1].z,
            basis.col[2].x, basis.col[2].y, basis.col[2].z,
            godot_world.translation.x,
            godot_world.translation.y,
            godot_world.translation.z,
        ],
    }


def write_room(project, room_number, walls, platforms):
    walls = sorted(walls, key=lambda obj: obj.name)
    platforms = sorted(platforms, key=lambda obj: int(obj.name.split("_", 2)[1]))
    output = {
        "room": room_number,
        "walls": [export_object(obj) for obj in walls],
        "platforms": [export_object(obj) for obj in platforms],
    }
    destination = project / "resources" / "blender_room_edits" / f"Room{room_number:02}.json"
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(output, indent=2), encoding="utf-8")
    print(
        f"BLENDER_ROOM_EDIT_EXPORT_PASS: Room {room_number:02} "
        f"walls={len(walls)} platforms={len(platforms)}"
    )


def export_current(project):
    match = re.fullmatch(r"Room(\d{2})Walls\.blend", Path(bpy.data.filepath).name)
    if not match:
        raise RuntimeError("Open a RoomXXWalls.blend file before exporting it.")

    room_number = int(match.group(1))
    write_room(
        project,
        room_number,
        (obj for obj in bpy.context.scene.objects if obj.get("godot_wall")),
        (obj for obj in bpy.context.scene.objects if obj.get("godot_reference")),
    )


def export_all(project):
    source_directory = project / "assets" / "models" / "EditableWallsBlender"
    for room_number in range(1, 31):
        source = source_directory / f"Room{room_number:02}Walls.blend"
        loaded_objects = []
        with bpy.data.libraries.load(str(source), link=False) as (available, requested):
            requested.objects = list(available.objects)
        loaded_objects.extend(obj for obj in requested.objects if obj is not None)
        try:
            write_room(
                project,
                room_number,
                (obj for obj in loaded_objects if obj.get("godot_wall")),
                (obj for obj in loaded_objects if obj.get("godot_reference")),
            )
        finally:
            for obj in loaded_objects:
                bpy.data.objects.remove(obj, do_unlink=True)


arguments = sys.argv[sys.argv.index("--") + 1:]
project = Path(arguments[0]).resolve()
if len(arguments) > 1 and arguments[1] == "--all":
    export_all(project)
else:
    export_current(project)
