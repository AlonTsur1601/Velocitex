import bpy
import sys
from pathlib import Path


REQUIRED_BACKING = {
    "ExitDoorBackingLeft", "ExitDoorBackingRight",
    "ExitDoorBackingBelow", "ExitDoorBackingAbove",
}


def target_name(name):
    parts = name.split("_", 2)
    return parts[2] if len(parts) == 3 and parts[0] == "REF" and parts[1].isdigit() else name


def audit_room(path, apply_changes):
    bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False)
    end_wall = next((obj for obj in bpy.context.scene.objects
                     if target_name(obj.name) == "ExitCorridorEndWall"), None)
    corridor_floor = next((obj for obj in bpy.context.scene.objects
                           if target_name(obj.name) == "ExitCorridorFloor"), None)
    if end_wall is None or corridor_floor is None:
        raise RuntimeError(f"{path.stem}: exit corridor references are missing")
    direction = end_wall.matrix_basis.translation - corridor_floor.matrix_basis.translation
    if abs(direction.x) > abs(direction.y):
        wall_name = "RightWall" if direction.x > 0.0 else "LeftWall"
    else:
        wall_name = "ExitWall" if direction.y > 0.0 else "BackWall"

    backing = {target_name(obj.name) for obj in bpy.context.scene.objects} & REQUIRED_BACKING
    if backing != REQUIRED_BACKING and path.stem != "Room26Walls":
        raise RuntimeError(f"{path.stem}: carved door backing is incomplete ({sorted(backing)})")
    crossing = next((obj for obj in bpy.context.scene.objects if
                     obj.get("godot_reference") and target_name(obj.name) == wall_name), None)
    if crossing is None:
        print(f"EXIT_CROSSING_WALL_OK: {path.stem} has no full {wall_name} across its carved doorway")
        return 0

    print(f"EXIT_CROSSING_WALL_REMOVE: {path.stem}: {crossing.name}")
    if apply_changes:
        bpy.data.objects.remove(crossing, do_unlink=True)
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(path), check_existing=False, compress=True)
    return 1


arguments = sys.argv[sys.argv.index("--") + 1:]
project = Path(arguments[0]).resolve()
apply_changes = "--apply" in arguments[1:]
directory = project / "assets" / "models" / "EditableWallsBlender"
changed = 0
for room in range(1, 31):
    changed += audit_room(directory / f"Room{room:02}Walls.blend", apply_changes)
print(f"EXIT_CROSSING_WALL_AUDIT_PASS: mode={'apply' if apply_changes else 'dry-run'} changed_walls={changed}")
