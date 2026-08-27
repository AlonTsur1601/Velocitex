import bpy
import hashlib
import json
import math
import re
import sys
from pathlib import Path
from mathutils import Matrix, Vector


REFERENCE_NAME = re.compile(r"^REF_(\d+)_(.+?)(?:\.\d+)?$")
COPY_PREFIXES = (
    "ExitCorridor",
    "ExitDoorBacking",
)
COPY_TARGETS = {
    "ClosedDoorBlocker",
    "FrameCollision",
    "ExitRun",
}
COARSE_REGION_MINIMUM = Vector((-8.0, -5.0, -12.0))
COARSE_REGION_MAXIMUM = Vector((8.0, 15.0, 15.0))
CONTACT_TOLERANCE = 0.005
CONVERT = Matrix(((1.0, 0.0, 0.0, 0.0),
                  (0.0, 0.0, -1.0, 0.0),
                  (0.0, 1.0, 0.0, 0.0),
                  (0.0, 0.0, 0.0, 1.0)))
CLOSED_BLOCKER_LOCAL = Matrix.Translation((0.0, 2.19, -0.03))
BLENDER_BLOCKER_LOCAL = CONVERT @ CLOSED_BLOCKER_LOCAL @ CONVERT.inverted()


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


def target_name(name):
    match = REFERENCE_NAME.match(name)
    if not match:
        return name
    target = match.group(2)
    dot = target.rfind(".")
    return target[:dot] if dot > 0 and target[dot + 1:].isdigit() else target


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


def matrix_values(matrix):
    return [float(matrix[row][column]) for row in range(4) for column in range(4)]


def mesh_payload(obj):
    mesh = obj.data
    payload = {
        "vertices": [[float(value) for value in vertex.co] for vertex in mesh.vertices],
        "edges": [list(edge.vertices) for edge in mesh.edges],
        "polygons": [
            {
                "vertices": list(polygon.vertices),
                "material_index": polygon.material_index,
                "use_smooth": polygon.use_smooth,
            }
            for polygon in mesh.polygons
        ],
        "uv_layers": {
            layer.name: [[float(value) for value in loop.uv] for loop in layer.data]
            for layer in mesh.uv_layers
        },
        "custom_properties": custom_properties(mesh),
    }
    encoded = json.dumps(payload, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return payload, hashlib.sha256(encoded).hexdigest()


def material_record(material):
    if material is None:
        return None
    nodes = []
    if material.node_tree is not None:
        for node in sorted(material.node_tree.nodes, key=lambda item: item.name):
            inputs = {}
            for socket in node.inputs:
                if hasattr(socket, "default_value"):
                    try:
                        inputs[socket.name] = json_value(socket.default_value)
                    except (TypeError, ValueError):
                        pass
            nodes.append({
                "name": node.name,
                "type": node.bl_idname,
                "inputs": inputs,
            })
    return {
        "name": material.name,
        "diffuse_color": [float(value) for value in material.diffuse_color],
        "metallic": float(material.metallic),
        "roughness": float(material.roughness),
        "use_nodes": material.node_tree is not None,
        "nodes": nodes,
        "custom_properties": custom_properties(material),
    }


def door_local_coordinates(obj, door_matrix):
    to_door = door_matrix.inverted_safe()
    return [to_door @ obj.matrix_world @ vertex.co for vertex in obj.data.vertices]


def bounds_of(coordinates):
    return (
        Vector(tuple(min(coordinate[axis] for coordinate in coordinates) for axis in range(3))),
        Vector(tuple(max(coordinate[axis] for coordinate in coordinates) for axis in range(3))),
    )


def bounds_record(bounds):
    minimum, maximum = bounds
    return {
        "minimum": [float(value) for value in minimum],
        "maximum": [float(value) for value in maximum],
        "size": [float(maximum[axis] - minimum[axis]) for axis in range(3)],
        "center": [float((minimum[axis] + maximum[axis]) * 0.5) for axis in range(3)],
    }


def overlaps_region(bounds):
    minimum, maximum = bounds
    return all(
        maximum[axis] >= COARSE_REGION_MINIMUM[axis] and
        minimum[axis] <= COARSE_REGION_MAXIMUM[axis]
        for axis in range(3)
    )


def explicit_role(target):
    return target in COPY_TARGETS or target.startswith(COPY_PREFIXES)


def relation_record(left, right):
    left_min, left_max = left["_bounds"]
    right_min, right_max = right["_bounds"]
    signed_separations = []
    for axis in range(3):
        if left_max[axis] < right_min[axis]:
            signed_separations.append(float(right_min[axis] - left_max[axis]))
        elif right_max[axis] < left_min[axis]:
            signed_separations.append(float(left_min[axis] - right_max[axis]))
        else:
            overlap = min(left_max[axis], right_max[axis]) - max(left_min[axis], right_min[axis])
            signed_separations.append(float(-overlap))
    positive = [max(0.0, value) for value in signed_separations]
    distance = math.sqrt(sum(value * value for value in positive))
    return {
        "a": left["name"],
        "b": right["name"],
        "axis_separation": signed_separations,
        "aabb_distance": distance,
        "touching_or_overlapping": distance <= CONTACT_TOLERANCE,
    }


arguments = sys.argv[sys.argv.index("--") + 1:]
project = Path(arguments[0]).resolve()
source_report_path = Path(arguments[1]).resolve()
assembly_report_path = Path(arguments[2]).resolve()
source_path = project / "assets" / "models" / "EditableWallsBlender" / "Room02Walls.blend"
backup_path = project / "assets" / "models" / "EditableWallsBlender" / "Room02Walls.blend1"

identity_before = {
    "blend": file_identity(source_path),
    "blend1": file_identity(backup_path),
}
bpy.ops.wm.open_mainfile(filepath=str(source_path), load_ui=False)

blockers = [
    obj for obj in bpy.context.scene.objects
    if obj.type == "MESH" and target_name(obj.name) == "ClosedDoorBlocker"
]
if len(blockers) != 1:
    raise RuntimeError(f"Room 02: expected one ClosedDoorBlocker, found {len(blockers)}")
blocker = blockers[0]
door_rotation = blocker.matrix_world.to_quaternion().to_matrix().to_4x4()
door_matrix = door_rotation.copy()
door_matrix.translation = (
    blocker.matrix_world.translation -
    door_rotation.to_3x3() @ BLENDER_BLOCKER_LOCAL.translation
)

records = []
for obj in sorted(bpy.context.scene.objects, key=lambda item: item.name):
    if obj.type != "MESH" or len(obj.data.vertices) == 0:
        continue
    target = target_name(obj.name)
    coordinates = door_local_coordinates(obj, door_matrix)
    bounds = bounds_of(coordinates)
    payload, fingerprint = mesh_payload(obj)
    relative = door_matrix.inverted_safe() @ obj.matrix_world
    record = {
        "name": obj.name,
        "target": target,
        "parent": obj.parent.name if obj.parent else None,
        "collections": sorted(collection.name for collection in obj.users_collection),
        "explicit_door_role": explicit_role(target),
        "inside_coarse_door_region": overlaps_region(bounds),
        "matrix_world": matrix_values(obj.matrix_world),
        "matrix_door_local": matrix_values(relative),
        "pivot_door_local": [float(value) for value in relative.translation],
        "bounds_door_local": bounds_record(bounds),
        "mesh_fingerprint": fingerprint,
        "mesh": payload,
        "materials": [material_record(slot.material) for slot in obj.material_slots],
        "custom_properties": custom_properties(obj),
        "hide_viewport": bool(obj.hide_viewport),
        "hide_render": bool(obj.hide_render),
        "hide_get": bool(obj.hide_get()),
        "display_type": obj.display_type,
        "modifiers": [
            {"name": modifier.name, "type": modifier.type, "show_viewport": modifier.show_viewport,
             "show_render": modifier.show_render}
            for modifier in obj.modifiers
        ],
        "_bounds": bounds,
    }
    records.append(record)

region_records = [record for record in records if record["explicit_door_role"] or record["inside_coarse_door_region"]]
relations = []
for left_index, left in enumerate(region_records):
    for right in region_records[left_index + 1:]:
        relation = relation_record(left, right)
        if relation["touching_or_overlapping"] or left["explicit_door_role"] or right["explicit_door_role"]:
            relations.append(relation)

for record in records:
    del record["_bounds"]

assembly = {
    "room": 2,
    "source": identity_before,
    "door_anchor_matrix_world": matrix_values(door_matrix),
    "coarse_region": {
        "minimum": [float(value) for value in COARSE_REGION_MINIMUM],
        "maximum": [float(value) for value in COARSE_REGION_MAXIMUM],
    },
    "objects": records,
    "region_object_names": [record["name"] for record in region_records],
    "explicit_object_names": [record["name"] for record in records if record["explicit_door_role"]],
    "relations": relations,
}

identity_after = {
    "blend": file_identity(source_path),
    "blend1": file_identity(backup_path),
}
if identity_after != identity_before:
    raise RuntimeError("Room 02 source changed during read-only audit")

source_report_path.parent.mkdir(parents=True, exist_ok=True)
assembly_report_path.parent.mkdir(parents=True, exist_ok=True)
source_report_path.write_text(json.dumps(identity_before, indent=2), encoding="utf-8")
assembly_report_path.write_text(json.dumps(assembly, indent=2), encoding="utf-8")
print(
    f"ROOM02_CANONICAL_AUDIT_PASS: meshes={len(records)} "
    f"region={len(region_records)} explicit={len(assembly['explicit_object_names'])} "
    f"relations={len(relations)} source_unchanged=True")
