import bpy
import json
import sys
from pathlib import Path
from mathutils import Matrix, Vector


TOLERANCE = 0.00002
TARGETS = (
    "ClosedDoorBlocker",
    "FrameCollision",
    "ExitCorridorLeftWall",
    "ExitCorridorRightWall",
    "ExitDoorBackingLeft",
    "ExitDoorBackingRight",
    "ExitDoorBackingBelow",
    "ExitDoorBackingAbove",
)
COPIED_AXES = {
    "ClosedDoorBlocker": (0,),
    "FrameCollision": (0, 1),
    "ExitCorridorLeftWall": (0,),
    "ExitCorridorRightWall": (0,),
    "ExitDoorBackingLeft": (1,),
    "ExitDoorBackingRight": (1,),
    "ExitDoorBackingBelow": (1,),
    "ExitDoorBackingAbove": (1,),
}
OPTIONAL_TARGETS = {
    "ExitDoorBackingLeft",
    "ExitDoorBackingRight",
    "ExitDoorBackingBelow",
    "ExitDoorBackingAbove",
}
CONVERT = Matrix(((1.0, 0.0, 0.0, 0.0),
                  (0.0, 0.0, -1.0, 0.0),
                  (0.0, 1.0, 0.0, 0.0),
                  (0.0, 0.0, 0.0, 1.0)))
CLOSED_BLOCKER_LOCAL = Matrix.Translation((0.0, 2.19, -0.03))


def target_name(name):
    parts = name.split("_", 2)
    target = parts[2] if len(parts) == 3 and parts[0] == "REF" and parts[1].isdigit() else name
    dot = target.rfind(".")
    return target[:dot] if dot > 0 and target[dot + 1:].isdigit() else target


def godot_matrix(values):
    return Matrix(((values[0], values[3], values[6], values[9]),
                   (values[1], values[4], values[7], values[10]),
                   (values[2], values[5], values[8], values[11]),
                   (0.0, 0.0, 0.0, 1.0)))


def blender_door_matrix(project, room_number):
    if room_number == 26:
        godot_door = Matrix.Translation((0.0, 8.38, -91.08))
    else:
        reference_path = project / "tools" / "blender" / "reference" / f"Room{room_number:02}.json"
        entries = json.loads(reference_path.read_text(encoding="utf-8"))
        blocker = next((entry for entry in entries if entry["name"] == "ClosedDoorBlocker"), None)
        if blocker is None:
            raise RuntimeError(f"Room {room_number:02} has no ClosedDoorBlocker reference")
        godot_door = godot_matrix(blocker["transform"]) @ CLOSED_BLOCKER_LOCAL.inverted()
    return CONVERT @ godot_door @ CONVERT.inverted()


def door_local_bounds(obj, door_matrix):
    to_door = door_matrix.inverted_safe()
    coordinates = [to_door @ obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    return (
        Vector(tuple(min(coordinate[axis] for coordinate in coordinates) for axis in range(3))),
        Vector(tuple(max(coordinate[axis] for coordinate in coordinates) for axis in range(3))),
    )


def collect_targets(door_matrix):
    result = {}
    for target in TARGETS:
        objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and target_name(obj.name) == target]
        objects.sort(key=lambda obj: door_local_bounds(obj, door_matrix)[0].x)
        expected = 2 if target == "FrameCollision" else 1
        if not objects and target in OPTIONAL_TARGETS:
            continue
        if len(objects) != expected:
            raise RuntimeError(f"{target}: expected {expected} objects, found {len(objects)}")
        for index, obj in enumerate(objects):
            result[f"{target}:{index}"] = obj
    return result


def remap_to_bounds(obj, door_matrix, desired_minimum, desired_maximum, axes):
    object_to_door = door_matrix.inverted_safe() @ obj.matrix_world
    door_to_object = object_to_door.inverted_safe()
    coordinates = [object_to_door @ vertex.co for vertex in obj.data.vertices]
    source_minimum = Vector(tuple(min(coordinate[axis] for coordinate in coordinates) for axis in range(3)))
    source_maximum = Vector(tuple(max(coordinate[axis] for coordinate in coordinates) for axis in range(3)))
    if obj.data.users > 1:
        obj.data = obj.data.copy()
    for vertex, coordinate in zip(obj.data.vertices, coordinates):
        remapped = coordinate.copy()
        for axis in axes:
            source_size = source_maximum[axis] - source_minimum[axis]
            desired_size = desired_maximum[axis] - desired_minimum[axis]
            if source_size <= 0.0 or desired_size <= 0.0:
                raise RuntimeError(f"{obj.name}: non-positive bound on axis {axis}")
            fraction = (coordinate[axis] - source_minimum[axis]) / source_size
            remapped[axis] = desired_minimum[axis] + (fraction * desired_size)
        vertex.co = door_to_object @ remapped
    obj.data.update()


def serialized_bounds(bounds):
    return {
        "minimum": [float(value) for value in bounds[0]],
        "maximum": [float(value) for value in bounds[1]],
    }


arguments = sys.argv[sys.argv.index("--") + 1:]
project = Path(arguments[0]).resolve()
apply_changes = "--apply" in arguments[1:]
report_argument = next((argument for argument in arguments[1:] if argument.startswith("--report=")), None)
directory = project / "assets" / "models" / "EditableWallsBlender"

bpy.ops.wm.open_mainfile(filepath=str(directory / "Room01Walls.blend"), load_ui=False)
source_door_matrix = blender_door_matrix(project, 1)
source_objects = collect_targets(source_door_matrix)
template = {key: door_local_bounds(obj, source_door_matrix) for key, obj in source_objects.items()}

report = []
for room in range(2, 31):
    path = directory / f"Room{room:02}Walls.blend"
    bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False)
    door_matrix = blender_door_matrix(project, room)
    objects = collect_targets(door_matrix)
    before = {key: door_local_bounds(obj, door_matrix) for key, obj in objects.items()}
    maximum_before = max(
        abs(value - expected)
        for key, bounds in before.items()
        for actual, target in zip(bounds, template[key])
        for axis, (value, expected) in enumerate(zip(actual, target))
        if axis in COPIED_AXES[key.split(":", 1)[0]])
    if apply_changes and maximum_before > TOLERANCE:
        for key, obj in objects.items():
            remap_to_bounds(
                obj,
                door_matrix,
                *template[key],
                COPIED_AXES[key.split(":", 1)[0]])
    after = {key: door_local_bounds(obj, door_matrix) for key, obj in objects.items()}
    maximum_after = max(
        abs(value - expected)
        for key, bounds in after.items()
        for actual, target in zip(bounds, template[key])
        for axis, (value, expected) in enumerate(zip(actual, target))
        if axis in COPIED_AXES[key.split(":", 1)[0]])
    if apply_changes and maximum_after > TOLERANCE:
        raise RuntimeError(f"Room {room:02}: template error remains {maximum_after:.9f} m")
    if apply_changes and maximum_before > TOLERANCE:
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(path), check_existing=False, compress=True)
    print(
        f"ROOM01_DOOR_TEMPLATE: Room {room:02} "
        f"before_max={maximum_before:.9f} after_max={maximum_after:.9f}")
    report.append({
        "room": room,
        "before": {key: serialized_bounds(bounds) for key, bounds in before.items()},
        "after": {key: serialized_bounds(bounds) for key, bounds in after.items()},
        "maximum_before": maximum_before,
        "maximum_after": maximum_after,
    })

if report_argument:
    report_path = Path(report_argument.split("=", 1)[1]).resolve()
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps({
        "source_room": 1,
        "template": {key: serialized_bounds(bounds) for key, bounds in template.items()},
        "rooms": report,
    }, indent=2), encoding="utf-8")
print(
    f"ROOM01_DOOR_TEMPLATE_PASS: mode={'apply' if apply_changes else 'dry-run'} "
    f"rooms={len(report)}")
