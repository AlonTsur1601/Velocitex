import bpy
import hashlib
import json
import math
import re
import sys
from collections import defaultdict, deque
from pathlib import Path
from mathutils import Matrix, Vector


REFERENCE_NAME = re.compile(r"^REF_(\d+)_(.+?)(?:\.\d+)?$")
CONVERT = Matrix(((1.0, 0.0, 0.0, 0.0),
                  (0.0, 0.0, -1.0, 0.0),
                  (0.0, 1.0, 0.0, 0.0),
                  (0.0, 0.0, 0.0, 1.0)))
CLOSED_BLOCKER_LOCAL = Matrix.Translation((0.0, 2.19, -0.03))
BLENDER_BLOCKER_LOCAL = CONVERT @ CLOSED_BLOCKER_LOCAL @ CONVERT.inverted()

CENTRAL_TARGETS = {
    "FrameCollision",
    "ClosedDoorBlocker",
    "ExitCorridorFloor",
    "ExitCorridorCeiling",
    "ExitCorridorLeftWall",
    "ExitCorridorRightWall",
    "ExitCorridorEndWall",
}
BACKING_TARGETS = {
    "ExitDoorBackingLeft",
    "ExitDoorBackingRight",
    "ExitDoorBackingBelow",
    "ExitDoorBackingAbove",
}
COPY_TARGETS = CENTRAL_TARGETS | BACKING_TARGETS
TARGET_ROOMS = [1] + list(range(3, 31))
GEOMETRY_TOLERANCE = 0.00002
APPROACH_TERMINAL_Y = -0.220007
APPROACH_TOP_Z = 0.186300
APPROACH_OPENING_MIN_X = -4.0
APPROACH_OPENING_MAX_X = 4.0


def target_name(name):
    match = REFERENCE_NAME.match(name)
    if not match:
        return name
    target = match.group(2)
    dot = target.rfind(".")
    return target[:dot] if dot > 0 and target[dot + 1:].isdigit() else target


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def file_identity(path):
    stat = path.stat()
    return (stat.st_size, stat.st_mtime_ns, sha256(path))


def rigid_door_anchor(blocker):
    rotation = blocker.matrix_world.to_quaternion().to_matrix().to_4x4()
    anchor = rotation.copy()
    anchor.translation = (
        blocker.matrix_world.translation
        - rotation.to_3x3() @ BLENDER_BLOCKER_LOCAL.translation
    )
    return anchor


def objects_for_target(target):
    return [
        obj for obj in bpy.context.scene.objects
        if obj.type == "MESH" and target_name(obj.name) == target
    ]


def local_coordinates(obj, anchor):
    inverse = anchor.inverted_safe()
    return [inverse @ obj.matrix_world @ vertex.co for vertex in obj.data.vertices]


def bounds(obj, anchor):
    coordinates = local_coordinates(obj, anchor)
    minimum = Vector(tuple(min(item[axis] for item in coordinates) for axis in range(3)))
    maximum = Vector(tuple(max(item[axis] for item in coordinates) for axis in range(3)))
    return minimum, maximum


def source_objects(source_path):
    with bpy.data.libraries.load(str(source_path), link=False) as (available, requested):
        requested.objects = sorted(
            name for name in available.objects
            if target_name(name) in COPY_TARGETS
        )
    loaded = [obj for obj in requested.objects if obj is not None]
    counts = {target: sum(target_name(obj.name) == target for obj in loaded) for target in COPY_TARGETS}
    expected = {target: (2 if target == "FrameCollision" else 1) for target in COPY_TARGETS}
    if counts != expected:
        raise RuntimeError(f"Room 02 canonical object distribution is invalid: expected={expected}, actual={counts}")
    return loaded


def ordered_old_objects(objects):
    grouped = defaultdict(deque)
    for obj in sorted(objects, key=lambda item: (target_name(item.name), item.matrix_world.translation.x, item.name)):
        grouped[target_name(obj.name)].append(obj)
    return grouped


def next_reference_index(objects):
    indices = []
    for obj in objects:
        match = REFERENCE_NAME.match(obj.name)
        if match:
            indices.append(int(match.group(1)))
    return max(indices, default=-1) + 1


def collection_names(obj):
    names = [collection.name for collection in obj.users_collection]
    return names or ["EditablePlatforms"]


def old_backing_outer_extents(old_objects, anchor):
    result = {}
    for target in BACKING_TARGETS:
        candidates = [obj for obj in old_objects if target_name(obj.name) == target]
        if len(candidates) != 1:
            continue
        minimum, maximum = bounds(candidates[0], anchor)
        if target == "ExitDoorBackingLeft":
            result[target] = {"min_x": minimum.x, "min_z": minimum.z, "max_z": maximum.z}
        elif target == "ExitDoorBackingRight":
            result[target] = {"max_x": maximum.x, "min_z": minimum.z, "max_z": maximum.z}
        elif target == "ExitDoorBackingBelow":
            result[target] = {"min_z": minimum.z}
        else:
            result[target] = {"max_z": maximum.z}
    return result


def change_door_local_coordinates(obj, anchor, replacements):
    to_local = (anchor.inverted_safe() @ obj.matrix_world)
    from_local = to_local.inverted_safe()
    current = [to_local @ vertex.co for vertex in obj.data.vertices]
    original_minimum = Vector(tuple(min(item[axis] for item in current) for axis in range(3)))
    original_maximum = Vector(tuple(max(item[axis] for item in current) for axis in range(3)))
    for vertex, coordinate in zip(obj.data.vertices, current):
        modified = coordinate.copy()
        for axis, edge, value in replacements:
            boundary = original_minimum[axis] if edge == "minimum" else original_maximum[axis]
            if abs(coordinate[axis] - boundary) <= GEOMETRY_TOLERANCE:
                modified[axis] = value
        vertex.co = from_local @ modified
    obj.data.update()


def fit_backing_outer_extents(obj, target, anchor, extents):
    values = extents.get(target)
    if not values:
        return
    replacements = []
    if target == "ExitDoorBackingLeft":
        replacements = [(0, "minimum", values["min_x"]), (2, "minimum", values["min_z"]), (2, "maximum", values["max_z"])]
    elif target == "ExitDoorBackingRight":
        replacements = [(0, "maximum", values["max_x"]), (2, "minimum", values["min_z"]), (2, "maximum", values["max_z"])]
    elif target == "ExitDoorBackingBelow":
        replacements = [(2, "minimum", values["min_z"])]
    elif target == "ExitDoorBackingAbove":
        replacements = [(2, "maximum", values["max_z"])]
    change_door_local_coordinates(obj, anchor, replacements)


def approach_score(obj, anchor):
    if target_name(obj.name) in COPY_TARGETS:
        return None
    minimum, maximum = bounds(obj, anchor)
    overlap_x = min(maximum.x, APPROACH_OPENING_MAX_X) - max(minimum.x, APPROACH_OPENING_MIN_X)
    if overlap_x < 1.0:
        return None
    if minimum.y >= APPROACH_TERMINAL_Y or maximum.y < APPROACH_TERMINAL_Y - 20.0 or maximum.y > 2.0:
        return None
    if maximum.z < -0.8 or maximum.z > 0.8 or minimum.z >= maximum.z:
        return None
    distance = abs(maximum.y - APPROACH_TERMINAL_Y)
    height = abs(maximum.z - APPROACH_TOP_Z)
    excessive_width = max(0.0, (maximum.x - minimum.x) - 30.0) * 0.01
    return distance + height * 2.0 + excessive_width


def select_approach_objects(anchor):
    candidates = []
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        score = approach_score(obj, anchor)
        if score is not None:
            candidates.append((score, obj))
    candidates.sort(key=lambda item: (item[0], item[1].name))
    if not candidates:
        return []
    best = candidates[0][0]
    return [obj for score, obj in candidates if score <= best + 0.0001]


def open_passage_intrusions(anchor, approaches):
    approach_names = {obj.name for obj in approaches}
    region_minimum = Vector((-2.34998, 0.20002, 0.18632))
    region_maximum = Vector((2.34998, 9.59998, 4.19628))
    intrusions = []
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH" or obj.name in approach_names or target_name(obj.name) in COPY_TARGETS:
            continue
        minimum, maximum = bounds(obj, anchor)
        intersects = all(
            maximum[axis] > region_minimum[axis] and minimum[axis] < region_maximum[axis]
            for axis in range(3)
        )
        # Only shorten a mesh that crosses from the room into the corridor.
        # Do not delete room-specific geometry wholly inside either side.
        if intersects and minimum.y < 0.2 and maximum.y > 0.2:
            intrusions.append(obj)
    return intrusions


def fit_approach_terminal(obj, anchor):
    to_local = anchor.inverted_safe() @ obj.matrix_world
    from_local = to_local.inverted_safe()
    coordinates = [to_local @ vertex.co for vertex in obj.data.vertices]
    old_max_y = max(item.y for item in coordinates)
    old_max_z = max(item.z for item in coordinates)
    changed = 0
    for vertex, coordinate in zip(obj.data.vertices, coordinates):
        if abs(coordinate.y - old_max_y) > GEOMETRY_TOLERANCE:
            continue
        modified = coordinate.copy()
        modified.y = APPROACH_TERMINAL_Y
        if abs(coordinate.z - old_max_z) <= GEOMETRY_TOLERANCE:
            modified.z = APPROACH_TOP_Z
        vertex.co = from_local @ modified
        changed += 1
    if changed == 0:
        raise RuntimeError(f"Could not identify the door-facing terminal vertices of {obj.name}")
    obj.data.update()


def trim_corridor_intrusion(obj, anchor):
    change_door_local_coordinates(obj, anchor, [(1, "maximum", 0.199998)])


def copy_custom_identity(source, destination_name):
    source.name = destination_name
    source["godot_reference"] = True


def canonicalize_room(project, source_path, source_anchor, source_collections, room_number, dry_run):
    target_path = project / "assets" / "models" / "EditableWallsBlender" / f"Room{room_number:02}Walls.blend"
    bpy.ops.wm.open_mainfile(filepath=str(target_path), load_ui=False)
    blockers = objects_for_target("ClosedDoorBlocker")
    if len(blockers) != 1:
        raise RuntimeError(f"Room {room_number:02} has {len(blockers)} ClosedDoorBlocker objects instead of 1")
    target_anchor = rigid_door_anchor(blockers[0])
    old_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and target_name(obj.name) in COPY_TARGETS]
    grouped_old_objects = ordered_old_objects(old_objects)
    old_names = defaultdict(deque, {
        target: deque(obj.name for obj in items)
        for target, items in grouped_old_objects.items()
    })
    backing_extents = old_backing_outer_extents(old_objects, target_anchor)
    approaches = select_approach_objects(target_anchor)
    intrusions = open_passage_intrusions(target_anchor, approaches)
    approach_description = [
        {"name": obj.name, "bounds": [[round(v, 6) for v in edge] for edge in bounds(obj, target_anchor)]}
        for obj in approaches
    ]
    if dry_run:
        return {
            "room": room_number,
            "old_objects": len(old_objects),
            "backing_extents": backing_extents,
            "approaches": approach_description,
            "intrusions": [obj.name for obj in intrusions],
        }

    for obj in old_objects:
        bpy.data.objects.remove(obj, do_unlink=True)

    appended = source_objects(source_path)
    next_index = next_reference_index(bpy.context.scene.objects)
    duplicate_counts = defaultdict(int)
    created = []
    for obj in sorted(appended, key=lambda item: (target_name(item.name), item.matrix_basis.translation.x, item.name)):
        source_name = obj.name
        target = target_name(obj.name)
        if old_names[target]:
            destination_name = old_names[target].popleft()
        else:
            suffix = duplicate_counts[target]
            destination_name = f"REF_{next_index:03}_{target}" + (f".{suffix:03}" if suffix else "")
            if suffix == 0:
                next_index += 1
            duplicate_counts[target] += 1
        names = source_collections[source_name]
        linked = False
        for name in names:
            collection = bpy.context.scene.collection if name == bpy.context.scene.collection.name else bpy.data.collections.get(name)
            if collection is not None:
                collection.objects.link(obj)
                linked = True
        if not linked:
            collection = bpy.data.collections.get("EditablePlatforms") or bpy.context.scene.collection
            collection.objects.link(obj)
        source_world = obj.matrix_basis.copy()
        obj.matrix_world = target_anchor @ source_anchor.inverted_safe() @ source_world
        copy_custom_identity(obj, destination_name)
        if target in BACKING_TARGETS:
            fit_backing_outer_extents(obj, target, target_anchor, backing_extents)
        created.append(obj)

    for approach in approaches:
        fit_approach_terminal(approach, target_anchor)
    for intrusion in intrusions:
        trim_corridor_intrusion(intrusion, target_anchor)

    counts = {target: sum(target_name(obj.name) == target for obj in created) for target in COPY_TARGETS}
    expected = {target: (2 if target == "FrameCollision" else 1) for target in COPY_TARGETS}
    if counts != expected:
        raise RuntimeError(f"Room {room_number:02} received an invalid doorway distribution: expected={expected}, actual={counts}")

    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(target_path), check_existing=False, compress=True)
    return {
        "room": room_number,
        "created": len(created),
        "approaches": approach_description,
        "intrusions": [obj.name for obj in intrusions],
        "backing_extents": backing_extents,
    }


arguments = sys.argv[sys.argv.index("--") + 1:]
project = Path(arguments[0]).resolve()
dry_run = "--dry-run" in arguments[1:]
report_path = project / "artifacts" / "exit-door" / ("canonicalize-dry-run.json" if dry_run else "canonicalize-applied.json")
source_path = project / "assets" / "models" / "EditableWallsBlender" / "Room02Walls.blend"
source_backup_path = project / "assets" / "models" / "EditableWallsBlender" / "Room02Walls.blend1"
source_identity_before = file_identity(source_path)
source_backup_identity_before = file_identity(source_backup_path)

bpy.ops.wm.open_mainfile(filepath=str(source_path), load_ui=False)
source_blockers = objects_for_target("ClosedDoorBlocker")
if len(source_blockers) != 1:
    raise RuntimeError(f"Room 02 has {len(source_blockers)} ClosedDoorBlocker objects instead of 1")
source_anchor = rigid_door_anchor(source_blockers[0])
source_collections = {
    obj.name: collection_names(obj)
    for obj in bpy.context.scene.objects
    if obj.type == "MESH" and target_name(obj.name) in COPY_TARGETS
}

results = []
for room_number in TARGET_ROOMS:
    result = canonicalize_room(project, source_path, source_anchor, source_collections, room_number, dry_run)
    results.append(result)
    approaches = ",".join(item["name"] for item in result["approaches"]) or "NONE"
    print(
        f"ROOM02_CANONICAL_{'DRY_RUN' if dry_run else 'APPLY'}: "
        f"Room {room_number:02} approaches={approaches} objects={result.get('created', result.get('old_objects'))}"
    )
    if file_identity(source_path) != source_identity_before or file_identity(source_backup_path) != source_backup_identity_before:
        raise RuntimeError(f"Room 02 changed while processing Room {room_number:02}")

report_path.parent.mkdir(parents=True, exist_ok=True)
report_path.write_text(json.dumps({
    "dry_run": dry_run,
    "source_room02": {
        "blend": source_identity_before,
        "blend1": source_backup_identity_before,
    },
    "rooms": results,
}, indent=2), encoding="utf-8")
print(
    f"ROOM02_CANONICAL_{'DRY_RUN' if dry_run else 'APPLY'}_PASS: "
    f"rooms={len(results)} room02_unchanged=True report={report_path}"
)
