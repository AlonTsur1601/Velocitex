import hashlib
import json
import re
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


REFERENCE_NAME = re.compile(r"^REF_(\d+)_(.+?)(?:\.\d+)?$")
CONVERT = Matrix(((1.0, 0.0, 0.0, 0.0),
                  (0.0, 0.0, -1.0, 0.0),
                  (0.0, 1.0, 0.0, 0.0),
                  (0.0, 0.0, 0.0, 1.0)))
CLOSED_BLOCKER_LOCAL = Matrix.Translation((0.0, 2.19, -0.03))
BLENDER_BLOCKER_LOCAL = CONVERT @ CLOSED_BLOCKER_LOCAL @ CONVERT.inverted()
PROTECTED_TARGETS = {
    "ExitCorridorFloor", "ExitCorridorCeiling", "ExitCorridorLeftWall",
    "ExitCorridorRightWall", "ExitCorridorEndWall", "FrameCollision",
    "ClosedDoorBlocker", "ExitDoorBackingLeft", "ExitDoorBackingRight",
    "ExitDoorBackingBelow", "ExitDoorBackingAbove",
}
OUTER_SHELL_TARGETS = {"HazardFloor", "Ceiling", "LeftWall", "RightWall", "BackWall", "ExitWall"}
TOLERANCE = 0.001


def scalar(value):
    return round(float(value), 6)


def vector(value):
    return [scalar(component) for component in value]


def target_name(name):
    match = REFERENCE_NAME.match(name)
    if not match:
        return name
    target = match.group(2)
    dot = target.rfind(".")
    return target[:dot] if dot > 0 and target[dot + 1:].isdigit() else target


def file_identity(path):
    data = path.read_bytes()
    return {"size": len(data), "sha256": hashlib.sha256(data).hexdigest().upper()}


def rigid_door_anchor(blocker):
    rotation = blocker.matrix_world.to_quaternion().to_matrix().to_4x4()
    anchor = rotation.copy()
    anchor.translation = blocker.matrix_world.translation - rotation.to_3x3() @ BLENDER_BLOCKER_LOCAL.translation
    return anchor


def bounds(obj, anchor):
    transform = anchor.inverted_safe() @ obj.matrix_world
    coordinates = [transform @ vertex.co for vertex in obj.data.vertices]
    minimum = Vector(tuple(min(item[axis] for item in coordinates) for axis in range(3)))
    maximum = Vector(tuple(max(item[axis] for item in coordinates) for axis in range(3)))
    return minimum, maximum


def overlap(first_minimum, first_maximum, second_minimum, second_maximum, axis):
    return min(first_maximum[axis], second_maximum[axis]) - max(first_minimum[axis], second_minimum[axis])


def select_approach(anchor):
    candidates = []
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH" or target_name(obj.name) in PROTECTED_TARGETS:
            continue
        minimum, maximum = bounds(obj, anchor)
        overlap_x = min(maximum.x, 4.0) - max(minimum.x, -4.0)
        if overlap_x < 1.0:
            continue
        if minimum.y >= -0.22 or maximum.y < -20.22 or maximum.y > 2.0:
            continue
        if maximum.z < -0.8 or maximum.z > 0.8 or minimum.z >= maximum.z:
            continue
        score = abs(maximum.y + 0.22) + abs(maximum.z - 0.1863) * 2.0
        candidates.append((score, obj))
    candidates.sort(key=lambda item: (item[0], item[1].name))
    if not candidates:
        raise RuntimeError("Could not identify terminal approach")
    return candidates[0][1]


def object_record(obj, anchor, approach_minimum, approach_maximum):
    minimum, maximum = bounds(obj, anchor)
    size = maximum - minimum
    x_overlap = overlap(approach_minimum, approach_maximum, minimum, maximum, 0)
    y_overlap = overlap(approach_minimum, approach_maximum, minimum, maximum, 1)
    z_overlap = overlap(approach_minimum, approach_maximum, minimum, maximum, 2)
    top = approach_maximum.z
    touches_height = minimum.z <= top + 0.35 and maximum.z >= top - 0.10
    near_left = min(abs(minimum.x - approach_minimum.x), abs(maximum.x - approach_minimum.x))
    near_right = min(abs(minimum.x - approach_maximum.x), abs(maximum.x - approach_maximum.x))
    near_start = min(abs(minimum.y - approach_minimum.y), abs(maximum.y - approach_minimum.y))
    near_terminal = min(abs(minimum.y - approach_maximum.y), abs(maximum.y - approach_maximum.y))
    return {
        "name": obj.name,
        "target": target_name(obj.name),
        "bounds": {"minimum": vector(minimum), "maximum": vector(maximum), "size": vector(size)},
        "x_overlap": scalar(x_overlap),
        "y_overlap": scalar(y_overlap),
        "z_overlap": scalar(z_overlap),
        "touches_platform_height": touches_height,
        "distance_to_left": scalar(near_left),
        "distance_to_right": scalar(near_right),
        "distance_to_start": scalar(near_start),
        "distance_to_terminal": scalar(near_terminal),
    }


def room_record(number, path):
    bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False)
    blockers = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and target_name(obj.name) == "ClosedDoorBlocker"]
    if len(blockers) != 1:
        raise RuntimeError(f"Room {number:02}: expected one ClosedDoorBlocker, found {len(blockers)}")
    anchor = rigid_door_anchor(blockers[0])
    approach = select_approach(anchor)
    approach_minimum, approach_maximum = bounds(approach, anchor)
    nearby = []
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH" or obj == approach:
            continue
        record = object_record(obj, anchor, approach_minimum, approach_maximum)
        minimum = record["bounds"]["minimum"]
        maximum = record["bounds"]["maximum"]
        if maximum[1] < approach_minimum.y - 5.0 or minimum[1] > approach_maximum.y + 5.0:
            continue
        if maximum[0] < approach_minimum.x - 5.0 or minimum[0] > approach_maximum.x + 5.0:
            continue
        if maximum[2] < approach_minimum.z - 2.0 or minimum[2] > approach_maximum.z + 8.0:
            continue
        nearby.append(record)
    nearby.sort(key=lambda item: (item["bounds"]["minimum"][1], item["bounds"]["minimum"][0], item["name"]))
    relevant = [
        item for item in nearby
        if item["target"] not in PROTECTED_TARGETS | OUTER_SHELL_TARGETS
        and item["touches_platform_height"]
        and (
            item["distance_to_left"] <= 1.5
            or item["distance_to_right"] <= 1.5
            or item["distance_to_start"] <= 1.5
            or item["distance_to_terminal"] <= 1.5
        )
    ]
    return {
        "room": number,
        "file": file_identity(path),
        "approach": {
            "name": approach.name,
            "target": target_name(approach.name),
            "bounds": {"minimum": vector(approach_minimum), "maximum": vector(approach_maximum), "size": vector(approach_maximum - approach_minimum)},
        },
        "nearby": nearby,
        "relevant": relevant,
    }


arguments = sys.argv[sys.argv.index("--") + 1:]
project = Path(arguments[0]).resolve()
output_path = Path(arguments[1]).resolve()
rooms = []
for number in range(1, 31):
    path = project / "assets" / "models" / "EditableWallsBlender" / f"Room{number:02}Walls.blend"
    record = room_record(number, path)
    rooms.append(record)
    print(f"TERMINAL_PERIMETER_AUDIT: Room {number:02} approach={record['approach']['name']} nearby={len(record['nearby'])} relevant={len(record['relevant'])}")
output_path.parent.mkdir(parents=True, exist_ok=True)
output_path.write_text(json.dumps({"rooms": rooms}, indent=2, ensure_ascii=False), encoding="utf-8")
print(f"TERMINAL_PERIMETER_AUDIT_PASS: rooms={len(rooms)}")
