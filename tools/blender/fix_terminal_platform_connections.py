import bpy
import hashlib
import json
import re
import statistics
import sys
from pathlib import Path

from mathutils import Matrix, Vector


REFERENCE_NAME = re.compile(r"^REF_(\d+)_(.+?)(?:\.\d+)?$")
CONVERT = Matrix(((1.0, 0.0, 0.0, 0.0),
                  (0.0, 0.0, -1.0, 0.0),
                  (0.0, 1.0, 0.0, 0.0),
                  (0.0, 0.0, 0.0, 1.0)))
CLOSED_BLOCKER_LOCAL = Matrix.Translation((0.0, 2.19, -0.03))
BLENDER_BLOCKER_LOCAL = CONVERT @ CLOSED_BLOCKER_LOCAL @ CONVERT.inverted()

PROTECTED_TARGETS = {
    "FrameCollision",
    "ClosedDoorBlocker",
    "ExitCorridorFloor",
    "ExitCorridorCeiling",
    "ExitCorridorLeftWall",
    "ExitCorridorRightWall",
    "ExitCorridorEndWall",
    "ExitDoorBackingLeft",
    "ExitDoorBackingRight",
    "ExitDoorBackingBelow",
    "ExitDoorBackingAbove",
}
WALL_FRAGMENTS = ("rail", "sidewall", "guard", "kerb", "rim", "wall")
OUTER_SHELL_TARGETS = {"LeftWall", "RightWall"}
ROUND_DIGITS = 6
GEOMETRY_TOLERANCE = 0.00002
APPROACH_TERMINAL_Y = -0.220007
APPROACH_TOP_Z = 0.186300


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
    return {
        "path": str(path),
        "size": stat.st_size,
        "mtime_ns": stat.st_mtime_ns,
        "sha256": sha256(path),
    }


def rounded(values):
    return [round(float(value), ROUND_DIGITS) for value in values]


def scalar(value):
    return round(float(value), ROUND_DIGITS)


def json_value(value):
    if isinstance(value, (str, int, float, bool)) or value is None:
        return value
    if hasattr(value, "to_list"):
        return value.to_list()
    if isinstance(value, (list, tuple)):
        return [json_value(item) for item in value]
    return str(value)


def custom_properties(item):
    return {
        key: json_value(item[key])
        for key in sorted(item.keys())
        if key != "_RNA_UI"
    }


def rigid_door_anchor(blocker):
    rotation = blocker.matrix_world.to_quaternion().to_matrix().to_4x4()
    anchor = rotation.copy()
    anchor.translation = (
        blocker.matrix_world.translation
        - rotation.to_3x3() @ BLENDER_BLOCKER_LOCAL.translation
    )
    return anchor


def local_coordinates(obj, anchor):
    to_local = anchor.inverted_safe() @ obj.matrix_world
    return [to_local @ vertex.co for vertex in obj.data.vertices]


def bounds_from_coordinates(coordinates):
    minimum = Vector(tuple(min(item[axis] for item in coordinates) for axis in range(3)))
    maximum = Vector(tuple(max(item[axis] for item in coordinates) for axis in range(3)))
    return minimum, maximum


def object_bounds(obj, anchor):
    return bounds_from_coordinates(local_coordinates(obj, anchor))


def bounds_record(minimum, maximum):
    return {
        "minimum": rounded(minimum),
        "maximum": rounded(maximum),
        "size": rounded(maximum - minimum),
    }


def payload_digest(payload):
    encoded = json.dumps(payload, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest().upper()


def protected_object_record(obj, anchor):
    to_local = anchor.inverted_safe() @ obj.matrix_world
    coordinates = [to_local @ vertex.co for vertex in obj.data.vertices]
    minimum, maximum = bounds_from_coordinates(coordinates)
    payload = {
        "target": target_name(obj.name),
        "matrix_door_local": [
            scalar(to_local[row][column])
            for row in range(4)
            for column in range(4)
        ],
        "vertices_door_local": [rounded(coordinate) for coordinate in coordinates],
        "edges": [list(edge.vertices) for edge in obj.data.edges],
        "polygons": [
            [list(polygon.vertices), polygon.material_index, bool(polygon.use_smooth)]
            for polygon in obj.data.polygons
        ],
        "uv_layers": {
            layer.name: [rounded(loop.uv) for loop in layer.data]
            for layer in obj.data.uv_layers
        },
        "object_properties": custom_properties(obj),
        "mesh_properties": custom_properties(obj.data),
        "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
        "collections": sorted(collection.name for collection in obj.users_collection),
        "hide_viewport": bool(obj.hide_viewport),
        "hide_render": bool(obj.hide_render),
        "display_type": obj.display_type,
        "modifiers": [
            [modifier.name, modifier.type, bool(modifier.show_viewport), bool(modifier.show_render)]
            for modifier in obj.modifiers
        ],
    }
    return {
        "name": obj.name,
        "target": target_name(obj.name),
        "bounds_door_local": bounds_record(minimum, maximum),
        "digest": payload_digest(payload),
    }


def approach_score(obj, anchor):
    if obj.type != "MESH" or target_name(obj.name) in PROTECTED_TARGETS:
        return None
    minimum, maximum = object_bounds(obj, anchor)
    overlap_x = min(maximum.x, 4.0) - max(minimum.x, -4.0)
    if overlap_x < 1.0:
        return None
    if minimum.y >= APPROACH_TERMINAL_Y or maximum.y < APPROACH_TERMINAL_Y - 20.0 or maximum.y > 2.0:
        return None
    if maximum.z < -0.8 or maximum.z > 0.8 or minimum.z >= maximum.z:
        return None
    return abs(maximum.y - APPROACH_TERMINAL_Y) + abs(maximum.z - APPROACH_TOP_Z) * 2.0


def select_approach(anchor):
    candidates = []
    for obj in bpy.context.scene.objects:
        score = approach_score(obj, anchor)
        if score is not None:
            candidates.append((score, obj))
    candidates.sort(key=lambda item: (item[0], item[1].name))
    if not candidates:
        raise RuntimeError("Could not identify the terminal approach platform")
    best = candidates[0][0]
    selected = [obj for score, obj in candidates if score <= best + 0.0001]
    if len(selected) != 1:
        raise RuntimeError(f"Expected one terminal approach platform, found {[obj.name for obj in selected]}")
    return selected[0]


def contact_record(approach, anchor):
    coordinates = local_coordinates(approach, anchor)
    minimum, maximum = bounds_from_coordinates(coordinates)
    door_vertices = sorted(
        rounded(coordinate)
        for coordinate in coordinates
        if abs(coordinate.y - maximum.y) <= GEOMETRY_TOLERANCE
    )
    payload = {
        "name": approach.name,
        "target": target_name(approach.name),
        "door_terminal_y": scalar(maximum.y),
        "top_z": scalar(maximum.z),
        "door_vertices": door_vertices,
    }
    return {
        **payload,
        "bounds_door_local": bounds_record(minimum, maximum),
        "digest": payload_digest(payload),
    }


def overlap_length(first_minimum, first_maximum, second_minimum, second_maximum, axis):
    return min(first_maximum[axis], second_maximum[axis]) - max(first_minimum[axis], second_minimum[axis])


def is_surface_like(minimum, maximum, approach_top):
    size = maximum - minimum
    return (
        size.x >= 0.7
        and size.y >= 0.7
        and 0.0 < size.z <= 1.2
        and abs(maximum.z - approach_top) <= 0.08
    )


def is_wall_like(obj, minimum, maximum, approach_top):
    size = maximum - minimum
    name = target_name(obj.name).lower()
    return (
        (any(fragment in name for fragment in WALL_FRAGMENTS)
         or (size.y >= 3.0 and size.x <= 1.8))
        and maximum.z >= approach_top + 0.25
        and minimum.z <= approach_top + 0.30
        and min(size.x, size.y) <= 1.8
    )


def nearby_inventory(approach, anchor):
    approach_minimum, approach_maximum = object_bounds(approach, anchor)
    nearby = []
    wall_candidates = []
    platform_overlaps = []
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH" or obj == approach or target_name(obj.name) in PROTECTED_TARGETS:
            continue
        minimum, maximum = object_bounds(obj, anchor)
        x_overlap = overlap_length(approach_minimum, approach_maximum, minimum, maximum, 0)
        y_overlap = overlap_length(approach_minimum, approach_maximum, minimum, maximum, 1)
        if maximum.y < approach_minimum.y - 2.0 or minimum.y > approach_maximum.y + 2.0:
            continue
        if maximum.x < approach_minimum.x - 2.0 or minimum.x > approach_maximum.x + 2.0:
            continue
        if maximum.z < approach_minimum.z - 1.0 or minimum.z > approach_maximum.z + 6.0:
            continue
        wall_like = is_wall_like(obj, minimum, maximum, approach_maximum.z)
        surface_like = is_surface_like(minimum, maximum, approach_maximum.z)
        record = {
            "name": obj.name,
            "target": target_name(obj.name),
            "bounds_door_local": bounds_record(minimum, maximum),
            "x_overlap": scalar(x_overlap),
            "y_overlap": scalar(y_overlap),
            "wall_like": wall_like,
            "surface_like": surface_like,
        }
        nearby.append(record)

        left_error = min(
            abs(maximum.x - approach_minimum.x),
            abs(minimum.x - approach_minimum.x),
        )
        right_error = min(
            abs(minimum.x - approach_maximum.x),
            abs(maximum.x - approach_maximum.x),
        )
        side_error = min(left_error, right_error)
        if wall_like and y_overlap >= 0.5 and side_error <= 1.0:
            wall_candidates.append({
                **record,
                "side": "left" if left_error <= right_error else "right",
                "side_error": scalar(side_error),
                "start_error": scalar(minimum.y - approach_minimum.y),
                "terminal_error": scalar(maximum.y - approach_maximum.y),
            })

        if (
            surface_like
            and x_overlap >= 0.5
            and y_overlap > GEOMETRY_TOLERANCE
            and y_overlap <= 1.5
            and minimum.y < approach_minimum.y
            and maximum.y < approach_maximum.y - GEOMETRY_TOLERANCE
        ):
            platform_overlaps.append({
                **record,
                "overlap_depth": scalar(y_overlap),
                "expected_approach_start_y": scalar(maximum.y),
                "approach_start_error": scalar(approach_minimum.y - maximum.y),
            })

    nearby.sort(key=lambda item: (item["bounds_door_local"]["minimum"][1], item["name"]))
    wall_candidates.sort(key=lambda item: (item["side"], item["bounds_door_local"]["minimum"][1], item["name"]))
    platform_overlaps.sort(key=lambda item: (-item["overlap_depth"], item["name"]))
    return nearby, wall_candidates, platform_overlaps


def actionable_wall_candidates(record, canonical_terminal_offset):
    approach_bounds = record["approach_contact"]["bounds_door_local"]
    approach_length = approach_bounds["size"][1]
    desired_terminal_y = approach_bounds["maximum"][1] + canonical_terminal_offset
    result = []
    for wall in record["wall_candidates"]:
        if wall["target"] in OUTER_SHELL_TARGETS:
            continue
        if not any(fragment in wall["target"].lower() for fragment in WALL_FRAGMENTS):
            continue
        overlap_ratio = wall["y_overlap"] / approach_length if approach_length > 0.0 else 0.0
        if overlap_ratio < 0.5:
            continue
        terminal_error = wall["bounds_door_local"]["maximum"][1] - desired_terminal_y
        if abs(terminal_error) <= GEOMETRY_TOLERANCE:
            continue
        result.append({
            **wall,
            "overlap_ratio": scalar(overlap_ratio),
            "desired_terminal_y": scalar(desired_terminal_y),
            "canonical_terminal_error": scalar(terminal_error),
        })
    return result


def actionable_wall_start_candidates(record, canonical_start_overhang):
    approach_bounds = record["approach_contact"]["bounds_door_local"]
    approach_length = approach_bounds["size"][1]
    desired_start_y = approach_bounds["minimum"][1] - canonical_start_overhang
    result = []
    for wall in record["wall_candidates"]:
        if wall["target"] in OUTER_SHELL_TARGETS:
            continue
        overlap_ratio = wall["y_overlap"] / approach_length if approach_length > 0.0 else 0.0
        if overlap_ratio < 0.5:
            continue
        start_overhang = approach_bounds["minimum"][1] - wall["bounds_door_local"]["minimum"][1]
        # More than one metre means the wall intentionally spans an earlier platform.
        # Small differences are normal modelling tolerance. Only the isolated,
        # visibly protruding terminal-wall starts are corrected.
        if start_overhang <= 0.05 or start_overhang >= 1.0:
            continue
        result.append({
            **wall,
            "overlap_ratio": scalar(overlap_ratio),
            "start_overhang": scalar(start_overhang),
            "desired_start_y": scalar(desired_start_y),
            "canonical_start_error": scalar(wall["bounds_door_local"]["minimum"][1] - desired_start_y),
        })
    return result


def canonical_wall_start_overhang(records):
    samples = []
    for record in records:
        approach_bounds = record["approach_contact"]["bounds_door_local"]
        approach_length = approach_bounds["size"][1]
        for wall in record["wall_candidates"]:
            if wall["target"] in OUTER_SHELL_TARGETS:
                continue
            overlap_ratio = wall["y_overlap"] / approach_length if approach_length > 0.0 else 0.0
            if overlap_ratio < 0.5:
                continue
            start_overhang = approach_bounds["minimum"][1] - wall["bounds_door_local"]["minimum"][1]
            if 0.01 <= start_overhang <= 0.05:
                samples.append(start_overhang)
    if not samples:
        raise RuntimeError("Could not derive the normal terminal-wall start overhang")
    return statistics.median(samples)


def scene_record(room_number, path):
    blockers = [
        obj for obj in bpy.context.scene.objects
        if obj.type == "MESH" and target_name(obj.name) == "ClosedDoorBlocker"
    ]
    if len(blockers) != 1:
        raise RuntimeError(f"Room {room_number:02} has {len(blockers)} ClosedDoorBlockers")
    anchor = rigid_door_anchor(blockers[0])
    approach = select_approach(anchor)
    protected = [
        protected_object_record(obj, anchor)
        for obj in bpy.context.scene.objects
        if obj.type == "MESH" and target_name(obj.name) in PROTECTED_TARGETS
    ]
    protected.sort(key=lambda item: (item["target"], item["bounds_door_local"]["minimum"], item["name"]))
    nearby, wall_candidates, platform_overlaps = nearby_inventory(approach, anchor)
    protection_payload = {
        "protected_objects": [[item["target"], item["digest"]] for item in protected],
        "approach_contact": contact_record(approach, anchor)["digest"],
    }
    return {
        "room": room_number,
        "file": file_identity(path),
        "door_anchor_matrix_world": [
            scalar(anchor[row][column])
            for row in range(4)
            for column in range(4)
        ],
        "protected_objects": protected,
        "approach_contact": contact_record(approach, anchor),
        "protection_digest": payload_digest(protection_payload),
        "nearby_meshes": nearby,
        "wall_candidates": wall_candidates,
        "platform_overlaps": platform_overlaps,
    }


def room_record(room_number, path):
    bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False)
    return scene_record(room_number, path)


def change_boundary(obj, anchor, axis, edge, value):
    if obj.data.users > 1:
        obj.data = obj.data.copy()
    to_local = anchor.inverted_safe() @ obj.matrix_world
    from_local = to_local.inverted_safe()
    coordinates = [to_local @ vertex.co for vertex in obj.data.vertices]
    boundary = (
        min(coordinate[axis] for coordinate in coordinates)
        if edge == "minimum"
        else max(coordinate[axis] for coordinate in coordinates)
    )
    changed = 0
    for vertex, coordinate in zip(obj.data.vertices, coordinates):
        if abs(coordinate[axis] - boundary) > GEOMETRY_TOLERANCE:
            continue
        modified = coordinate.copy()
        modified[axis] = value
        vertex.co = from_local @ modified
        changed += 1
    if changed == 0:
        raise RuntimeError(f"Could not find {edge} axis {axis} boundary vertices on {obj.name}")
    obj.data.update()
    return changed


def object_by_name(name):
    obj = bpy.context.scene.objects.get(name)
    if obj is None or obj.type != "MESH":
        raise RuntimeError(f"Missing mesh object {name}")
    return obj


def apply_room(room_number, path, canonical_terminal_offset, canonical_start_overhang):
    bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False)
    before = scene_record(room_number, path)
    if room_number == 2:
        return {
            "room": room_number,
            "skipped": "immutable canonical Room 02",
            "file_before": before["file"],
            "file_after": before["file"],
            "protection_equal": True,
            "changes": [],
        }

    blocker = next(
        obj for obj in bpy.context.scene.objects
        if obj.type == "MESH" and target_name(obj.name) == "ClosedDoorBlocker"
    )
    anchor = rigid_door_anchor(blocker)
    approach = object_by_name(before["approach_contact"]["name"])
    changes = []

    overlaps = before["platform_overlaps"]
    if len(overlaps) > 1:
        raise RuntimeError(f"Room {room_number:02} has ambiguous platform overlaps: {[item['name'] for item in overlaps]}")
    if overlaps:
        overlap = overlaps[0]
        desired_start = overlap["expected_approach_start_y"]
        changed_vertices = change_boundary(approach, anchor, 1, "minimum", desired_start)
        changes.append({
            "kind": "platform_overlap",
            "object": approach.name,
            "adjoining_object": overlap["name"],
            "old_start_y": before["approach_contact"]["bounds_door_local"]["minimum"][1],
            "new_start_y": scalar(desired_start),
            "removed_overlap": overlap["overlap_depth"],
            "changed_vertices": changed_vertices,
        })

    working = scene_record(room_number, path)
    approach_minimum, approach_maximum = object_bounds(approach, anchor)
    desired_wall_terminal = approach_maximum.y + canonical_terminal_offset
    for wall_record in actionable_wall_candidates(working, canonical_terminal_offset):
        wall = object_by_name(wall_record["name"])
        old_terminal = wall_record["bounds_door_local"]["maximum"][1]
        changed_vertices = change_boundary(wall, anchor, 1, "maximum", desired_wall_terminal)
        changes.append({
            "kind": "adjacent_wall_terminal",
            "object": wall.name,
            "side": wall_record["side"],
            "old_terminal_y": old_terminal,
            "new_terminal_y": scalar(desired_wall_terminal),
            "changed_vertices": changed_vertices,
        })

    working = scene_record(room_number, path)
    desired_wall_start = approach_minimum.y - canonical_start_overhang
    for wall_record in actionable_wall_start_candidates(working, canonical_start_overhang):
        wall = object_by_name(wall_record["name"])
        old_start = wall_record["bounds_door_local"]["minimum"][1]
        changed_vertices = change_boundary(wall, anchor, 1, "minimum", desired_wall_start)
        changes.append({
            "kind": "adjacent_wall_start",
            "object": wall.name,
            "side": wall_record["side"],
            "old_start_y": old_start,
            "new_start_y": scalar(desired_wall_start),
            "old_start_overhang": wall_record["start_overhang"],
            "new_start_overhang": scalar(canonical_start_overhang),
            "changed_vertices": changed_vertices,
        })

    after_memory = scene_record(room_number, path)
    if before["protection_digest"] != after_memory["protection_digest"]:
        raise RuntimeError(f"Room {room_number:02} protected doorway changed before save")
    if before["approach_contact"]["digest"] != after_memory["approach_contact"]["digest"]:
        raise RuntimeError(f"Room {room_number:02} door-facing approach contact changed before save")

    remaining_walls = actionable_wall_candidates(after_memory, canonical_terminal_offset)
    if remaining_walls:
        raise RuntimeError(f"Room {room_number:02} still has wall mismatches: {[item['name'] for item in remaining_walls]}")
    if after_memory["platform_overlaps"]:
        raise RuntimeError(f"Room {room_number:02} still has platform overlaps")
    remaining_starts = actionable_wall_start_candidates(after_memory, canonical_start_overhang)
    if remaining_starts:
        raise RuntimeError(f"Room {room_number:02} still has wall-start mismatches: {[item['name'] for item in remaining_starts]}")

    if changes:
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(path), check_existing=False, compress=True)
        bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False)
    after = scene_record(room_number, path)
    if before["protection_digest"] != after["protection_digest"]:
        raise RuntimeError(f"Room {room_number:02} protected doorway changed after reopen")
    if before["approach_contact"]["digest"] != after["approach_contact"]["digest"]:
        raise RuntimeError(f"Room {room_number:02} door-facing approach contact changed after reopen")
    return {
        "room": room_number,
        "file_before": before["file"],
        "file_after": after["file"],
        "protection_digest_before": before["protection_digest"],
        "protection_digest_after": after["protection_digest"],
        "approach_contact_before": before["approach_contact"],
        "approach_contact_after": after["approach_contact"],
        "protection_equal": True,
        "changes": changes,
        "remaining_wall_mismatches": len(actionable_wall_candidates(after, canonical_terminal_offset)),
        "remaining_wall_start_mismatches": len(actionable_wall_start_candidates(after, canonical_start_overhang)),
        "remaining_platform_overlaps": len(after["platform_overlaps"]),
    }


arguments = sys.argv[sys.argv.index("--") + 1:]
project = Path(arguments[0]).resolve()
output_path = Path(arguments[1]).resolve()
apply_changes = "--apply" in arguments[2:]
room_paths = [
    project / "assets" / "models" / "EditableWallsBlender" / f"Room{number:02}Walls.blend"
    for number in range(1, 31)
]

room02_record = room_record(2, room_paths[1])
room02_length = room02_record["approach_contact"]["bounds_door_local"]["size"][1]
room02_walls = [
    wall for wall in room02_record["wall_candidates"]
    if wall["target"] not in OUTER_SHELL_TARGETS
    and wall["y_overlap"] / room02_length >= 0.5
]
if len(room02_walls) != 2:
    raise RuntimeError(f"Room 02 must provide exactly two canonical adjacent walls, found {[wall['name'] for wall in room02_walls]}")
room02_offsets = [
    wall["bounds_door_local"]["maximum"][1]
    - room02_record["approach_contact"]["bounds_door_local"]["maximum"][1]
    for wall in room02_walls
]
if max(room02_offsets) - min(room02_offsets) > GEOMETRY_TOLERANCE:
    raise RuntimeError(f"Room 02 adjacent-wall offsets disagree: {room02_offsets}")
canonical_terminal_offset = sum(room02_offsets) / len(room02_offsets)
baseline_records = [room_record(number, path) for number, path in enumerate(room_paths, start=1)]
canonical_start_overhang = canonical_wall_start_overhang(baseline_records)

records = []
changes = []
if apply_changes:
    room02_identity_before = file_identity(room_paths[1])
    for number, path in enumerate(room_paths, start=1):
        result = apply_room(number, path, canonical_terminal_offset, canonical_start_overhang)
        changes.append(result)
        print(
            f"TERMINAL_PLATFORM_FIX: Room {number:02} "
            f"changes={len(result['changes'])} protected={result['protection_equal']}"
        )
    room02_identity_after = file_identity(room_paths[1])
    if room02_identity_before != room02_identity_after:
        raise RuntimeError("Room 02 changed during terminal-platform correction")

for number, path in enumerate(room_paths, start=1):
    record = room_record(number, path)
    record["actionable_wall_candidates"] = (
        [] if number == 2 else actionable_wall_candidates(record, canonical_terminal_offset)
    )
    record["actionable_wall_start_candidates"] = (
        [] if number == 2 else actionable_wall_start_candidates(record, canonical_start_overhang)
    )
    records.append(record)
    print(
        f"TERMINAL_PLATFORM_AUDIT: Room {number:02} "
        f"approach={record['approach_contact']['name']} "
        f"walls={len(record['actionable_wall_candidates'])} overlaps={len(record['platform_overlaps'])}"
    )

output = {
    "mode": "apply" if apply_changes else "audit",
    "canonical_room02_wall_terminal_offset": scalar(canonical_terminal_offset),
    "canonical_wall_start_overhang": scalar(canonical_start_overhang),
    "rooms": records,
    "changes": changes,
    "totals": {
        "audited": len(records),
        "wall_candidates": sum(len(room["actionable_wall_candidates"]) for room in records),
        "wall_start_candidates": sum(len(room["actionable_wall_start_candidates"]) for room in records),
        "platform_overlaps": sum(len(room["platform_overlaps"]) for room in records),
        "changed_rooms": sum(bool(item.get("changes")) for item in changes),
        "changes": sum(len(item.get("changes", [])) for item in changes),
    },
}
output_path.parent.mkdir(parents=True, exist_ok=True)
output_path.write_text(json.dumps(output, indent=2, ensure_ascii=False), encoding="utf-8")
print(
    f"TERMINAL_PLATFORM_AUDIT_PASS: rooms={output['totals']['audited']} "
    f"walls={output['totals']['wall_candidates']} "
    f"overlaps={output['totals']['platform_overlaps']}"
)
