import bpy
import sys
from pathlib import Path
from mathutils import Vector


SHELL_TARGETS = {"LeftWall", "RightWall", "BackWall", "ExitWall"}
TOLERANCE = 0.02


def target_name(name):
    parts = name.split("_", 2)
    return parts[2] if len(parts) == 3 and parts[0] == "REF" and parts[1].isdigit() else name


def find_target(name):
    return next((obj for obj in bpy.context.scene.objects if target_name(obj.name) == name), None)


def world_corners(obj):
    # Hidden shell collections are intentionally excluded in Blender's viewport;
    # matrix_world can therefore remain unevaluated in a headless pass.  These
    # generated references are unparented, so matrix_basis is their authored
    # and persistent world transform.
    return [obj.matrix_basis @ Vector(corner) for corner in obj.bound_box]


def projection_bounds(obj, direction):
    values = [corner.dot(direction) for corner in world_corners(obj)]
    return min(values), max(values)


def aligned_local_axis(obj, direction):
    basis = obj.matrix_basis.to_3x3()
    axes = [basis @ Vector((1.0, 0.0, 0.0)),
            basis @ Vector((0.0, 1.0, 0.0)),
            basis @ Vector((0.0, 0.0, 1.0))]
    alignments = [abs(axis.normalized().dot(direction)) for axis in axes]
    axis_index = max(range(3), key=alignments.__getitem__)
    return axis_index, alignments[axis_index], axes[axis_index].length


def extend_far_face(obj, direction, extension):
    if obj.data.users > 1:
        obj.data = obj.data.copy()
    local_direction = obj.matrix_basis.inverted().to_3x3() @ direction
    axis, alignment, world_scale = aligned_local_axis(obj, direction)
    if alignment < 0.98 or world_scale <= 0.0001:
        raise RuntimeError(f"{obj.name}: could not resolve a local corridor axis")
    sign = 1.0 if local_direction[axis] >= 0.0 else -1.0
    coordinates = [vertex.co[axis] for vertex in obj.data.vertices]
    extreme = max(coordinates) if sign > 0.0 else min(coordinates)
    local_extension = extension / world_scale
    for vertex in obj.data.vertices:
        if abs(vertex.co[axis] - extreme) <= 0.0001:
            vertex.co[axis] += sign * local_extension
    obj.data.update()


def recenter_mesh_origin(obj):
    minimum = Vector(tuple(min(vertex.co[axis] for vertex in obj.data.vertices) for axis in range(3)))
    maximum = Vector(tuple(max(vertex.co[axis] for vertex in obj.data.vertices) for axis in range(3)))
    center = (minimum + maximum) * 0.5
    if center.length <= 0.000001:
        return False
    basis = obj.matrix_basis.copy()
    basis.translation += basis.to_3x3() @ center
    obj.matrix_basis = basis
    for vertex in obj.data.vertices:
        vertex.co -= center
    obj.data.update()
    return True


def audit_room(path, apply_changes):
    bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False)
    bpy.context.view_layer.update()
    end_wall = find_target("ExitCorridorEndWall")
    corridor_floor = find_target("ExitCorridorFloor")
    if end_wall is None or corridor_floor is None:
        print(f"OUTER_WALL_SKIP: {path.stem} has no Blender exit corridor")
        return 0

    direction = end_wall.matrix_basis.translation - corridor_floor.matrix_basis.translation
    direction.z = 0.0
    if direction.length <= 0.001:
        raise RuntimeError(f"{path.stem}: exit corridor direction is undefined")
    direction.normalize()
    _, target_far = projection_bounds(end_wall, direction)
    changes = []
    parallel_walls = []

    for obj in bpy.context.scene.objects:
        if target_name(obj.name) not in SHELL_TARGETS or not obj.get("godot_reference"):
            continue
        _, alignment, _ = aligned_local_axis(obj, direction)
        if alignment < 0.98:
            continue
        current_near, current_far = projection_bounds(obj, direction)
        if current_far - current_near < 5.0:
            continue
        parallel_walls.append(obj)
        extension = target_far - current_far
        if extension <= TOLERANCE:
            continue
        changes.append((obj, extension))

    if len(changes) not in {0, 2}:
        names = ", ".join(obj.name for obj, _ in changes)
        raise RuntimeError(f"{path.stem}: expected zero or two parallel shell walls, found {len(changes)} ({names})")

    normalized = []
    if apply_changes:
        for obj, extension in changes:
            extend_far_face(obj, direction, extension)
        for obj in parallel_walls:
            if recenter_mesh_origin(obj):
                normalized.append(obj)

    if not changes and not normalized:
        print(f"OUTER_WALL_OK: {path.stem} already encloses the full corridor")
        return 0

    if changes:
        details = ", ".join(f"{target_name(obj.name)} +{extension:.3f}m" for obj, extension in changes)
        print(f"OUTER_WALL_EXTEND: {path.stem}: {details}")
    if normalized:
        print(f"OUTER_WALL_RECENTER: {path.stem}: " + ", ".join(target_name(obj.name) for obj in normalized))
    if apply_changes:
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(path), check_existing=False, compress=True)
    return len(changes)


arguments = sys.argv[sys.argv.index("--") + 1:]
project = Path(arguments[0]).resolve()
apply_changes = "--apply" in arguments[1:]
directory = project / "assets" / "models" / "EditableWallsBlender"
changed = 0
for room in range(1, 31):
    changed += audit_room(directory / f"Room{room:02}Walls.blend", apply_changes)
print(f"OUTER_WALL_AUDIT_PASS: mode={'apply' if apply_changes else 'dry-run'} changed_walls={changed}")
