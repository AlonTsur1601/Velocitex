import bpy
import hashlib
import json
import re
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
ASSEMBLY_TARGETS = CENTRAL_TARGETS | BACKING_TARGETS | {"ExitRun"}
ROUND_DIGITS = 6
TOLERANCE = 0.00002
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
    inverse = anchor.inverted_safe()
    return [inverse @ obj.matrix_world @ vertex.co for vertex in obj.data.vertices]


def bounds(obj, anchor):
    coordinates = local_coordinates(obj, anchor)
    minimum = Vector(tuple(min(item[axis] for item in coordinates) for axis in range(3)))
    maximum = Vector(tuple(max(item[axis] for item in coordinates) for axis in range(3)))
    return minimum, maximum


def rounded(values):
    return [round(float(value), ROUND_DIGITS) for value in values]


def bounds_record(value):
    minimum, maximum = value
    return {
        "minimum": rounded(minimum),
        "maximum": rounded(maximum),
        "size": rounded(maximum - minimum),
    }


def mesh_fingerprint(obj):
    payload = {
        "vertices": [rounded(vertex.co) for vertex in obj.data.vertices],
        "edges": [list(edge.vertices) for edge in obj.data.edges],
        "polygons": [
            [list(polygon.vertices), polygon.material_index, bool(polygon.use_smooth)]
            for polygon in obj.data.polygons
        ],
        "uv_layers": {
            layer.name: [rounded(loop.uv) for loop in layer.data]
            for layer in obj.data.uv_layers
        },
        "custom_properties": custom_properties(obj.data),
    }
    encoded = json.dumps(payload, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest().upper()


def material_record(material):
    if material is None:
        return None
    nodes = []
    has_nodes = material.node_tree is not None
    if has_nodes:
        for node in sorted(material.node_tree.nodes, key=lambda item: item.name):
            inputs = {}
            for socket in node.inputs:
                if not hasattr(socket, "default_value"):
                    continue
                try:
                    inputs[socket.name] = json_value(socket.default_value)
                except (TypeError, ValueError):
                    pass
            nodes.append({"name": node.name, "type": node.bl_idname, "inputs": inputs})
    return {
        "diffuse_color": rounded(material.diffuse_color),
        "metallic": round(float(material.metallic), ROUND_DIGITS),
        "roughness": round(float(material.roughness), ROUND_DIGITS),
        "use_nodes": has_nodes,
        "nodes": nodes,
        "custom_properties": custom_properties(material),
    }


def object_state_fingerprint(obj):
    payload = {
        "materials": [material_record(slot.material) for slot in obj.material_slots],
        "custom_properties": custom_properties(obj),
        "hide_viewport": bool(obj.hide_viewport),
        "hide_render": bool(obj.hide_render),
        "hide_get": bool(obj.hide_get()),
        "display_type": obj.display_type,
        "modifiers": [
            {
                "name": modifier.name,
                "type": modifier.type,
                "show_viewport": bool(modifier.show_viewport),
                "show_render": bool(modifier.show_render),
            }
            for modifier in obj.modifiers
        ],
        "collections": sorted(collection.name for collection in obj.users_collection),
    }
    encoded = json.dumps(payload, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest().upper()


def matrix_record(matrix):
    return [round(float(matrix[row][column]), ROUND_DIGITS) for row in range(4) for column in range(4)]


def object_records(anchor):
    records = {}
    grouped = {}
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        target = target_name(obj.name)
        grouped.setdefault(target, []).append(obj)
    for target, objects in grouped.items():
        items = []
        for obj in objects:
            local_matrix = anchor.inverted_safe() @ obj.matrix_world
            items.append({
                "name": obj.name,
                "target": target,
                "matrix_door_local": matrix_record(local_matrix),
                "bounds_door_local": bounds_record(bounds(obj, anchor)),
                "mesh_fingerprint": mesh_fingerprint(obj),
                "state_fingerprint": object_state_fingerprint(obj),
                "vertex_count": len(obj.data.vertices),
                "polygon_count": len(obj.data.polygons),
                "collections": sorted(collection.name for collection in obj.users_collection),
            })
        items.sort(key=lambda item: (
            item["bounds_door_local"]["minimum"][0],
            item["bounds_door_local"]["minimum"][1],
            item["name"],
        ))
        records[target] = items
    return records


def maximum_error(left, right):
    return max(abs(float(a) - float(b)) for a, b in zip(left, right))


def canonical_mismatch(source_records, target_records, target):
    expected = source_records.get(target, [])
    actual = target_records.get(target, [])
    result = {
        "expected_count": len(expected),
        "actual_count": len(actual),
        "items": [],
    }
    if len(expected) != len(actual):
        result["mismatch"] = True
        return result
    mismatch = False
    for source, current in zip(expected, actual):
        matrix_error = maximum_error(source["matrix_door_local"], current["matrix_door_local"])
        bounds_error = max(
            maximum_error(source["bounds_door_local"][edge], current["bounds_door_local"][edge])
            for edge in ("minimum", "maximum")
        )
        fingerprint_equal = source["mesh_fingerprint"] == current["mesh_fingerprint"]
        state_equal = source["state_fingerprint"] == current["state_fingerprint"]
        item = {
            "source_name": source["name"],
            "target_name": current["name"],
            "matrix_error": round(matrix_error, ROUND_DIGITS),
            "bounds_error": round(bounds_error, ROUND_DIGITS),
            "mesh_equal": fingerprint_equal,
            "state_equal": state_equal,
        }
        item["mismatch"] = (
            matrix_error > TOLERANCE
            or bounds_error > TOLERANCE
            or not fingerprint_equal
            or not state_equal
        )
        mismatch = mismatch or item["mismatch"]
        result["items"].append(item)
    result["mismatch"] = mismatch
    return result


def backing_interface(source_records, target_records, target):
    expected = source_records.get(target, [])
    actual = target_records.get(target, [])
    result = {
        "expected_count": len(expected),
        "actual_count": len(actual),
        "items": [],
        "mismatch": len(expected) != len(actual),
    }
    if len(expected) != len(actual):
        return result
    for source, current in zip(expected, actual):
        source_bounds = source["bounds_door_local"]
        target_bounds = current["bounds_door_local"]
        if target == "ExitDoorBackingLeft":
            edges = (("maximum", 0), ("minimum", 1), ("maximum", 1))
        elif target == "ExitDoorBackingRight":
            edges = (("minimum", 0), ("minimum", 1), ("maximum", 1))
        elif target == "ExitDoorBackingBelow":
            edges = (("minimum", 0), ("maximum", 0), ("maximum", 2), ("minimum", 1), ("maximum", 1))
        else:
            edges = (("minimum", 0), ("maximum", 0), ("minimum", 2), ("minimum", 1), ("maximum", 1))
        edge_errors = {
            f"{edge}_{axis}": round(abs(source_bounds[edge][axis] - target_bounds[edge][axis]), ROUND_DIGITS)
            for edge, axis in edges
        }
        item_mismatch = max(edge_errors.values(), default=0.0) > TOLERANCE
        result["items"].append({
            "source_name": source["name"],
            "target_name": current["name"],
            "interface_errors": edge_errors,
            "mismatch": item_mismatch,
        })
        result["mismatch"] = result["mismatch"] or item_mismatch
    return result


def intersects(minimum, maximum, region_minimum, region_maximum):
    return all(maximum[axis] > region_minimum[axis] and minimum[axis] < region_maximum[axis] for axis in range(3))


def select_approach_records(records):
    candidates = []
    for target, items in records.items():
        if target in CENTRAL_TARGETS or target in BACKING_TARGETS:
            continue
        for item in items:
            minimum = item["bounds_door_local"]["minimum"]
            maximum = item["bounds_door_local"]["maximum"]
            overlap_x = min(maximum[0], 4.0) - max(minimum[0], -4.0)
            if overlap_x < 1.0:
                continue
            if minimum[1] >= APPROACH_TERMINAL_Y or maximum[1] < APPROACH_TERMINAL_Y - 20.0 or maximum[1] > 2.0:
                continue
            if maximum[2] < -0.8 or maximum[2] > 0.8 or minimum[2] >= maximum[2]:
                continue
            score = abs(maximum[1] - APPROACH_TERMINAL_Y) + abs(maximum[2] - APPROACH_TOP_Z) * 2.0
            candidates.append((score, item))
    candidates.sort(key=lambda candidate: (candidate[0], candidate[1]["name"]))
    if not candidates:
        return []
    best = candidates[0][0]
    return [item for score, item in candidates if score <= best + 0.0001]


def room_audit(room_number, path, source_records, source_identity):
    bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False)
    blockers = [obj for obj in bpy.context.scene.objects if target_name(obj.name) == "ClosedDoorBlocker"]
    if len(blockers) != 1:
        return {
            "room": room_number,
            "file": file_identity(path),
            "fatal": f"ClosedDoorBlocker count is {len(blockers)}, expected 1",
        }
    anchor = rigid_door_anchor(blockers[0])
    records = object_records(anchor)
    comparisons = {
        target: canonical_mismatch(source_records, records, target)
        for target in sorted(CENTRAL_TARGETS)
    }
    backing = {
        target: backing_interface(source_records, records, target)
        for target in sorted(BACKING_TARGETS)
    }
    current_run = select_approach_records(records)
    approach = {
        "expected_count": 1,
        "actual_count": len(current_run),
        "items": [],
        "mismatch": len(current_run) != 1,
    }
    for current in current_run:
        actual_bounds = current["bounds_door_local"]
        errors = {
            "door_terminal_y": round(abs(APPROACH_TERMINAL_Y - actual_bounds["maximum"][1]), ROUND_DIGITS),
            "top_z": round(abs(APPROACH_TOP_Z - actual_bounds["maximum"][2]), ROUND_DIGITS),
        }
        mismatch = max(errors.values()) > TOLERANCE
        approach["items"].append({"target_name": current["name"], "interface_errors": errors, "mismatch": mismatch})
        approach["mismatch"] = approach["mismatch"] or mismatch

    # The conflict volume is the genuinely open passage, not the surrounding
    # frame/backing boundary.  Objects that merely touch x=+-2.35, remain
    # below the corridor floor, or stop on the room side of y=0.20 are valid.
    region_minimum = Vector((-2.34998, 0.20002, 0.18632))
    region_maximum = Vector((2.34998, 9.59998, 4.19628))
    conflicts = []
    approach_names = {item["name"] for item in current_run}
    for target, items in records.items():
        if target in ASSEMBLY_TARGETS:
            continue
        for item in items:
            if item["name"] in approach_names:
                continue
            minimum = Vector(item["bounds_door_local"]["minimum"])
            maximum = Vector(item["bounds_door_local"]["maximum"])
            if intersects(minimum, maximum, region_minimum, region_maximum):
                conflicts.append({
                    "name": item["name"],
                    "target": target,
                    "bounds_door_local": item["bounds_door_local"],
                })
    mismatch_targets = [target for target, result in comparisons.items() if result["mismatch"]]
    backing_mismatches = [target for target, result in backing.items() if result["mismatch"]]
    return {
        "room": room_number,
        "file": file_identity(path),
        "source_room02_identity": source_identity,
        "door_anchor_matrix_world": matrix_record(anchor),
        "central": comparisons,
        "backing_interfaces": backing,
        "approach_interface": approach,
        "conflicts": conflicts,
        "summary": {
            "central_mismatch_targets": mismatch_targets,
            "backing_mismatch_targets": backing_mismatches,
            "approach_mismatch": approach["mismatch"],
            "conflict_count": len(conflicts),
            "needs_correction": bool(mismatch_targets or backing_mismatches or approach["mismatch"] or conflicts),
        },
    }


arguments = sys.argv[sys.argv.index("--") + 1:]
project = Path(arguments[0]).resolve()
output_path = Path(arguments[1]).resolve() if len(arguments) > 1 else project / "artifacts" / "exit-door" / "all-room-canonical-before.json"
source_path = project / "assets" / "models" / "EditableWallsBlender" / "Room02Walls.blend"
source_identity_before = file_identity(source_path)

bpy.ops.wm.open_mainfile(filepath=str(source_path), load_ui=False)
source_blocker = next(obj for obj in bpy.context.scene.objects if target_name(obj.name) == "ClosedDoorBlocker")
source_records = object_records(rigid_door_anchor(source_blocker))

rooms = []
for number in [1] + list(range(3, 31)):
    room_path = project / "assets" / "models" / "EditableWallsBlender" / f"Room{number:02}Walls.blend"
    record = room_audit(number, room_path, source_records, source_identity_before)
    rooms.append(record)
    summary = record.get("summary")
    if summary is None:
        print(f"ROOM_DOOR_AUDIT_FAIL: Room {number:02} {record['fatal']}")
    else:
        print(
            f"ROOM_DOOR_AUDIT: Room {number:02} "
            f"central={len(summary['central_mismatch_targets'])} "
            f"backing={len(summary['backing_mismatch_targets'])} "
            f"approach={summary['approach_mismatch']} conflicts={summary['conflict_count']}"
        )

source_identity_after = file_identity(source_path)
if source_identity_after != source_identity_before:
    raise RuntimeError("Room 02 changed during the read-only audit.")

output_path.parent.mkdir(parents=True, exist_ok=True)
output = {
    "source_room": 2,
    "source_identity_before": source_identity_before,
    "source_identity_after": source_identity_after,
    "source_unchanged": source_identity_before == source_identity_after,
    "rooms": rooms,
    "totals": {
        "audited": len(rooms),
        "needs_correction": sum(bool(room.get("summary", {}).get("needs_correction")) for room in rooms),
        "fatal": sum("fatal" in room for room in rooms),
    },
}
output_path.write_text(json.dumps(output, indent=2, ensure_ascii=False), encoding="utf-8")
print(
    f"ALL_ROOM_DOOR_AUDIT_PASS: rooms={output['totals']['audited']} "
    f"needs_correction={output['totals']['needs_correction']} "
    f"fatal={output['totals']['fatal']} source_unchanged={output['source_unchanged']}"
)
