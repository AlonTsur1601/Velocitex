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
    "FrameCollision", "ClosedDoorBlocker", "ExitCorridorFloor", "ExitCorridorCeiling",
    "ExitCorridorLeftWall", "ExitCorridorRightWall", "ExitCorridorEndWall",
    "ExitDoorBackingLeft", "ExitDoorBackingRight", "ExitDoorBackingBelow", "ExitDoorBackingAbove",
}
OUTER_SHELL_TARGETS = {"HazardFloor", "Ceiling", "LeftWall", "RightWall", "BackWall", "ExitWall"}


def target_name(name):
    match = REFERENCE_NAME.match(name)
    if not match:
        return name
    target = match.group(2)
    dot = target.rfind(".")
    return target[:dot] if dot > 0 and target[dot + 1:].isdigit() else target


def rigid_door_anchor(blocker):
    rotation = blocker.matrix_world.to_quaternion().to_matrix().to_4x4()
    anchor = rotation.copy()
    anchor.translation = blocker.matrix_world.translation - rotation.to_3x3() @ BLENDER_BLOCKER_LOCAL.translation
    return anchor


def object_bounds(obj, anchor):
    transform = anchor.inverted_safe() @ obj.matrix_world
    coordinates = [transform @ vertex.co for vertex in obj.data.vertices]
    minimum = Vector(tuple(min(item[axis] for item in coordinates) for axis in range(3)))
    maximum = Vector(tuple(max(item[axis] for item in coordinates) for axis in range(3)))
    return minimum, maximum


def select_approach(anchor):
    candidates = []
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH" or target_name(obj.name) in PROTECTED_TARGETS:
            continue
        minimum, maximum = object_bounds(obj, anchor)
        overlap_x = min(maximum.x, 4.0) - max(minimum.x, -4.0)
        if overlap_x < 1.0 or minimum.y >= -0.220007 or maximum.y < -20.220007 or maximum.y > 2.0:
            continue
        if maximum.z < -0.8 or maximum.z > 0.8 or minimum.z >= maximum.z:
            continue
        score = abs(maximum.y + 0.220007) + abs(maximum.z - 0.1863) * 2.0
        candidates.append((score, obj, minimum, maximum))
    candidates.sort(key=lambda item: (item[0], item[1].name))
    if not candidates:
        raise RuntimeError("Could not identify terminal approach")
    return candidates[0][1:]


arguments = sys.argv[sys.argv.index("--") + 1:]
source = Path(arguments[0]).resolve()
destination = Path(arguments[1]).resolve()
bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False)
blocker = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH" and target_name(obj.name) == "ClosedDoorBlocker")
anchor = rigid_door_anchor(blocker)
approach, minimum, maximum = select_approach(anchor)

for obj in bpy.context.scene.objects:
    if target_name(obj.name) in OUTER_SHELL_TARGETS:
        obj.hide_render = True

camera_data = bpy.data.cameras.new("TerminalPlatformAuditCamera")
camera = bpy.data.objects.new("TerminalPlatformAuditCamera", camera_data)
bpy.context.scene.collection.objects.link(camera)
width = maximum.x - minimum.x
length = maximum.y - minimum.y
camera_local = Vector((max(10.0, width * 0.85), minimum.y - max(5.0, length * 0.30), maximum.z + max(5.0, width * 0.42)))
target_local = Vector((0.0, (minimum.y + maximum.y) * 0.50, maximum.z + 0.25))
camera.location = anchor @ camera_local
target_world = anchor @ target_local
camera.rotation_euler = (target_world - camera.location).to_track_quat("-Z", "Y").to_euler()
camera_data.lens = 48.0
bpy.context.scene.camera = camera

scene = bpy.context.scene
try:
    scene.render.engine = "BLENDER_WORKBENCH_NEXT"
except TypeError:
    scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = "WORLD"
scene.render.resolution_x = 1280
scene.render.resolution_y = 720
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
if scene.world is None:
    scene.world = bpy.data.worlds.new("TerminalPlatformAuditWorld")
scene.world.color = (0.035, 0.035, 0.035)
destination.parent.mkdir(parents=True, exist_ok=True)
scene.render.filepath = str(destination)
bpy.ops.render.render(write_still=True)
print(f"TERMINAL_PLATFORM_RENDER_PASS: {source.name} {approach.name} -> {destination}")
