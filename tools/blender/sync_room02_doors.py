import bpy
import json
import re
import sys
from collections import defaultdict, deque
from pathlib import Path
from mathutils import Matrix


DOOR_TARGETS = {
    "FrameCollision",
    "ClosedDoorBlocker",
    "ExitCorridorFloor",
    "ExitCorridorCeiling",
    "ExitCorridorLeftWall",
    "ExitCorridorRightWall",
    "ExitCorridorEndWall",
}
REFERENCE_NAME = re.compile(r"^REF_(\d+)_(.+?)(?:\.\d+)?$")
CONVERT = Matrix(((1.0, 0.0, 0.0, 0.0),
                  (0.0, 0.0, -1.0, 0.0),
                  (0.0, 1.0, 0.0, 0.0),
                  (0.0, 0.0, 0.0, 1.0)))
CLOSED_BLOCKER_LOCAL = Matrix.Translation((0.0, 2.19, -0.03))


def reference_target(name):
    match = REFERENCE_NAME.match(name)
    return match.group(2) if match else None


def godot_matrix(values):
    return Matrix(((values[0], values[3], values[6], values[9]),
                   (values[1], values[4], values[7], values[10]),
                   (values[2], values[5], values[8], values[11]),
                   (0.0, 0.0, 0.0, 1.0)))


def door_matrix(project, room_number):
    if room_number == 26:
        return Matrix.Translation((0.0, 8.38, -91.08))

    reference_path = project / "tools" / "blender" / "reference" / f"Room{room_number:02}.json"
    entries = json.loads(reference_path.read_text(encoding="utf-8"))
    blocker = next((entry for entry in entries if entry["name"] == "ClosedDoorBlocker"), None)
    if blocker is None:
        raise RuntimeError(f"Room {room_number:02} has no ClosedDoorBlocker reference from which to locate its door.")
    return godot_matrix(blocker["transform"]) @ CLOSED_BLOCKER_LOCAL.inverted()


def blender_door_matrix(project, room_number):
    godot_door = door_matrix(project, room_number)
    return CONVERT @ godot_door @ CONVERT.inverted()


def ordered_names(objects):
    names = defaultdict(deque)
    for obj in sorted(objects, key=lambda item: (reference_target(item.name), item.matrix_world.translation.x)):
        names[reference_target(obj.name)].append(obj.name)
    return names


def next_reference_index(objects):
    indices = []
    for obj in objects:
        match = REFERENCE_NAME.match(obj.name)
        if match:
            indices.append(int(match.group(1)))
    return max(indices, default=-1) + 1


def sync_room(project, source_path, source_names, source_anchor, room_number):
    target_path = project / "assets" / "models" / "EditableWallsBlender" / f"Room{room_number:02}Walls.blend"
    bpy.ops.wm.open_mainfile(filepath=str(target_path), load_ui=False)
    target_collection = bpy.data.collections.get("EditablePlatforms")
    if target_collection is None:
        raise RuntimeError(f"Room {room_number:02} has no EditablePlatforms collection.")

    old_door_objects = [
        obj for obj in bpy.context.scene.objects
        if reference_target(obj.name) in DOOR_TARGETS
    ]
    available_names = ordered_names(old_door_objects)
    new_index = next_reference_index(bpy.context.scene.objects)
    for obj in old_door_objects:
        bpy.data.objects.remove(obj, do_unlink=True)

    with bpy.data.libraries.load(str(source_path), link=False) as (available, requested):
        missing = sorted(set(source_names) - set(available.objects))
        if missing:
            raise RuntimeError(f"Room 02 is missing canonical door objects: {missing}")
        requested.objects = list(source_names)
    canonical_objects = [obj for obj in requested.objects if obj is not None]
    target_anchor = blender_door_matrix(project, room_number)
    created = []
    duplicate_counts = defaultdict(int)
    for obj in sorted(canonical_objects, key=lambda item: (reference_target(item.name), item.matrix_world.translation.x)):
        target = reference_target(obj.name)
        if available_names[target]:
            destination_name = available_names[target].popleft()
        else:
            suffix = duplicate_counts[target]
            destination_name = f"REF_{new_index:03}_{target}" + (f".{suffix:03}" if suffix else "")
            if suffix == 0:
                new_index += 1
            duplicate_counts[target] += 1
        # Appended library objects are not linked to a collection yet, so
        # matrix_world is identity. These door objects have no parent and their
        # persistent authored transform is therefore matrix_basis.
        source_relative = source_anchor.inverted() @ obj.matrix_basis
        obj.name = destination_name
        obj.matrix_basis = target_anchor @ source_relative
        obj["godot_reference"] = True
        target_collection.objects.link(obj)
        created.append(obj)

    if len(created) != 8:
        raise RuntimeError(f"Room {room_number:02} received {len(created)} canonical door objects instead of 8.")
    counts = {target: sum(reference_target(obj.name) == target for obj in created) for target in DOOR_TARGETS}
    if counts["FrameCollision"] != 2 or any(counts[target] != 1 for target in DOOR_TARGETS - {"FrameCollision"}):
        raise RuntimeError(f"Room {room_number:02} has an invalid canonical door object distribution: {counts}")

    bpy.ops.wm.save_as_mainfile(filepath=str(target_path), check_existing=False, compress=True)
    print(f"BLENDER_DOOR_SYNC_ROOM_PASS: Room {room_number:02} objects=8")


def matrix_values(matrix):
    return tuple(round(matrix[row][column], 6) for row in range(4) for column in range(4))


def mesh_signature(obj):
    return (
        tuple(tuple(round(value, 6) for value in vertex.co) for vertex in obj.data.vertices),
        tuple(tuple(polygon.vertices) for polygon in obj.data.polygons),
    )


def door_records(anchor):
    records = defaultdict(list)
    for obj in bpy.context.scene.objects:
        target = reference_target(obj.name)
        if target in DOOR_TARGETS:
            relative = anchor.inverted() @ obj.matrix_world
            records[target].append((relative.translation.x, matrix_values(relative), mesh_signature(obj), obj))
    for target in records:
        records[target].sort(key=lambda record: record[0])
    return records


def verify_all(project, source_path):
    bpy.ops.wm.open_mainfile(filepath=str(source_path), load_ui=False)
    source_records = door_records(blender_door_matrix(project, 2))
    for room_number in range(1, 31):
        target_path = project / "assets" / "models" / "EditableWallsBlender" / f"Room{room_number:02}Walls.blend"
        bpy.ops.wm.open_mainfile(filepath=str(target_path), load_ui=False)
        records = door_records(blender_door_matrix(project, room_number))
        for target in DOOR_TARGETS:
            expected = source_records[target]
            actual = records[target]
            if len(actual) != len(expected):
                raise RuntimeError(f"Room {room_number:02} target {target} has {len(actual)} objects instead of {len(expected)}.")
            for expected_record, actual_record in zip(expected, actual):
                matrix_error = max(abs(left - right) for left, right in zip(expected_record[1], actual_record[1]))
                if matrix_error > 0.00002 or expected_record[2] != actual_record[2]:
                    raise RuntimeError(f"Room {room_number:02} target {target} differs from Room 02 (matrix error {matrix_error:.6f}).")
                if room_number != 2 and not any(collection.name == "EditablePlatforms" for collection in actual_record[3].users_collection):
                    raise RuntimeError(f"Room {room_number:02} target {target} is outside EditablePlatforms.")

        reference_path = project / "tools" / "blender" / "reference" / f"Room{room_number:02}.json"
        expected_backing_count = sum(
            entry["name"].startswith("ExitDoorBacking")
            for entry in json.loads(reference_path.read_text(encoding="utf-8"))
        )
        actual_backing_count = sum(
            reference_target(obj.name).startswith("ExitDoorBacking")
            for obj in bpy.context.scene.objects
            if reference_target(obj.name) is not None
        )
        if actual_backing_count != expected_backing_count:
            raise RuntimeError(
                f"Room {room_number:02} backing wall count changed: actual={actual_backing_count}, expected={expected_backing_count}.")
        print(f"BLENDER_DOOR_VERIFY_ROOM_PASS: Room {room_number:02} objects=8 backing={actual_backing_count}")
    print("BLENDER_DOOR_VERIFY_PASS: All 30 .blend files contain the Room 02 door at their local door transform and preserve their own backing walls.")


arguments = sys.argv[sys.argv.index("--") + 1:]
project = Path(arguments[0]).resolve()
source_path = project / "assets" / "models" / "EditableWallsBlender" / "Room02Walls.blend"
bpy.context.preferences.filepaths.save_version = 0
with bpy.data.libraries.load(str(source_path), link=False) as (available, requested):
    source_names = tuple(sorted(name for name in available.objects if reference_target(name) in DOOR_TARGETS))
if len(source_names) != 8:
    raise RuntimeError(f"Room 02 contains {len(source_names)} canonical door objects instead of 8: {source_names}")

if "--verify-only" not in arguments[1:]:
    bpy.ops.wm.open_mainfile(filepath=str(source_path), load_ui=False)
    source_anchor = blender_door_matrix(project, 2)
    for room_number in [1] + list(range(3, 31)):
        sync_room(project, source_path, source_names, source_anchor, room_number)
    print("BLENDER_DOOR_SYNC_PASS: Rooms 01 and 03-30 now contain the latest Room 02 door geometry; local backing walls were preserved.")

verify_all(project, source_path)
