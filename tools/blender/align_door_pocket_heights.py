import bpy
import json
import sys
from pathlib import Path
from mathutils import Matrix, Vector


TOLERANCE = 0.00002
GENERATED_BACKING_ABOVE_BOTTOM = 4.34
POCKET_ROOM_SIDE_OFFSET = 0.08001630008220673
POCKET_SIDE_CLEARANCE = 0.0004999637603759766
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
    to_local = door_matrix.inverted_safe()
    coordinates = [to_local @ obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    return (
        Vector(tuple(min(coordinate[axis] for coordinate in coordinates) for axis in range(3))),
        Vector(tuple(max(coordinate[axis] for coordinate in coordinates) for axis in range(3))),
    )


def require_relative_axis_aligned(obj, door_matrix):
    relative = door_matrix.inverted_safe() @ obj.matrix_world
    linear = relative.to_3x3()
    axes = (
        linear @ Vector((1.0, 0.0, 0.0)),
        linear @ Vector((0.0, 1.0, 0.0)),
        linear @ Vector((0.0, 0.0, 1.0)),
    )
    expected = (Vector((1.0, 0.0, 0.0)), Vector((0.0, 1.0, 0.0)), Vector((0.0, 0.0, 1.0)))
    if any(axis.normalized().dot(direction) < 0.9999 for axis, direction in zip(axes, expected)):
        raise RuntimeError(f"{obj.name}: expected positive axis-aligned axes relative to its door")
    return relative, axes


def set_vertical_bounds(obj, door_matrix, desired_minimum, desired_maximum):
    relative, axes = require_relative_axis_aligned(obj, door_matrix)
    local_minimum = min(vertex.co.z for vertex in obj.data.vertices)
    local_maximum = max(vertex.co.z for vertex in obj.data.vertices)
    local_height = local_maximum - local_minimum
    desired_local_height = (desired_maximum - desired_minimum) / axes[2].length
    if desired_local_height <= 0.0:
        raise RuntimeError(f"{obj.name}: requested vertical height is not positive")
    if obj.data.users > 1:
        obj.data = obj.data.copy()
    for vertex in obj.data.vertices:
        normalized_height = (vertex.co.z - local_minimum) / local_height
        vertex.co.z = (normalized_height - 0.5) * desired_local_height
    obj.data.update()
    relative.translation.z = (desired_minimum + desired_maximum) * 0.5
    obj.matrix_world = door_matrix @ relative


def move_in_door_space(obj, door_matrix, delta_x, delta_y):
    relative = door_matrix.inverted_safe() @ obj.matrix_world
    relative.translation.x += delta_x
    relative.translation.y += delta_y
    obj.matrix_world = door_matrix @ relative


def alignment_errors(blocker_bounds, cover_top, pockets, door_matrix):
    door_minimum, door_maximum = blocker_bounds
    left_bounds = door_local_bounds(pockets[0], door_matrix)
    right_bounds = door_local_bounds(pockets[1], door_matrix)
    return {
        "bottom": max(
            abs(left_bounds[0].z - door_minimum.z),
            abs(right_bounds[0].z - door_minimum.z)),
        "top": max(
            abs(left_bounds[1].z - cover_top),
            abs(right_bounds[1].z - cover_top)),
        "rear": max(
            abs(left_bounds[1].y - (door_minimum.y - POCKET_ROOM_SIDE_OFFSET)),
            abs(right_bounds[1].y - (door_minimum.y - POCKET_ROOM_SIDE_OFFSET))),
        "side": max(
            abs(left_bounds[1].x - (door_minimum.x - POCKET_SIDE_CLEARANCE)),
            abs(right_bounds[0].x - (door_maximum.x + POCKET_SIDE_CLEARANCE))),
    }


def audit_room(project, room_number, path, apply_changes):
    bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False)
    door_matrix = blender_door_matrix(project, room_number)
    blocker = next((obj for obj in bpy.context.scene.objects
                    if target_name(obj.name) == "ClosedDoorBlocker"), None)
    pockets = [obj for obj in bpy.context.scene.objects if target_name(obj.name) == "FrameCollision"]
    pockets.sort(key=lambda obj: door_local_bounds(obj, door_matrix)[0].x)
    backing_above = next((obj for obj in bpy.context.scene.objects
                          if target_name(obj.name) == "ExitDoorBackingAbove"), None)
    if blocker is None or len(pockets) != 2:
        raise RuntimeError(
            f"{path.stem}: expected one door and two side pockets; "
            f"found door={blocker is not None}, pockets={len(pockets)}")

    blocker_bounds = door_local_bounds(blocker, door_matrix)
    # Every room except Room 26 has a Blender-authored backing wall above the
    # doorway. Room 26 keeps the standard runtime backing wall, whose lower
    # edge is FrameOuterHeight (4.84 m) minus the 0.50 m overlap used when the
    # room shell is carved.
    cover_top = (door_local_bounds(backing_above, door_matrix)[0].z
                 if backing_above is not None
                 else GENERATED_BACKING_ABOVE_BOTTOM)
    errors = alignment_errors(blocker_bounds, cover_top, pockets, door_matrix)
    maximum_error = max(errors.values())
    if maximum_error <= TOLERANCE:
        print(
            f"DOOR_POCKET_FIT_OK: {path.stem} "
            + " ".join(f"{name}_error={error:.6f}" for name, error in errors.items()))
        return 0

    print(
        f"DOOR_POCKET_FIT_ALIGN: {path.stem} "
        + " ".join(f"{name}_error={error:.6f}" for name, error in errors.items()))
    if apply_changes:
        door_minimum, door_maximum = blocker_bounds
        original_bottoms = [door_local_bounds(pocket, door_matrix)[0].z for pocket in pockets]
        for pocket, original_bottom in zip(pockets, original_bottoms):
            set_vertical_bounds(pocket, door_matrix, original_bottom, cover_top)

        left_bounds = door_local_bounds(pockets[0], door_matrix)
        right_bounds = door_local_bounds(pockets[1], door_matrix)
        move_in_door_space(
            pockets[0],
            door_matrix,
            (door_minimum.x - POCKET_SIDE_CLEARANCE) - left_bounds[1].x,
            (door_minimum.y - POCKET_ROOM_SIDE_OFFSET) - left_bounds[1].y)
        move_in_door_space(
            pockets[1],
            door_matrix,
            (door_maximum.x + POCKET_SIDE_CLEARANCE) - right_bounds[0].x,
            (door_minimum.y - POCKET_ROOM_SIDE_OFFSET) - right_bounds[1].y)

        errors = alignment_errors(blocker_bounds, cover_top, pockets, door_matrix)
        if max(errors.values()) > TOLERANCE:
            raise RuntimeError(
                f"{path.stem}: side pockets remain misaligned: "
                + ", ".join(f"{name}={error:.6f}" for name, error in errors.items()))
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(path), check_existing=False, compress=True)
    return len(pockets)


arguments = sys.argv[sys.argv.index("--") + 1:]
project = Path(arguments[0]).resolve()
apply_changes = "--apply" in arguments[1:]
directory = project / "assets" / "models" / "EditableWallsBlender"
changed = 0
for room in range(1, 31):
    changed += audit_room(
        project,
        room,
        directory / f"Room{room:02}Walls.blend",
        apply_changes)
print(
    f"DOOR_POCKET_FIT_AUDIT_PASS: mode={'apply' if apply_changes else 'dry-run'} "
    f"changed_pockets={changed}")
