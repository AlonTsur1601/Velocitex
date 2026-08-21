using Godot;
using System.Reflection;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Tests;

public partial class DoorDimmingSmokeTest : Node
{
    public override async void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://artifacts/door-dimming"));
        if (!await CaptureRoom(1, "res://scenes/MovementTestRoom.tscn") ||
            !await CaptureRoom(2, "res://scenes/Room02.tscn"))
        {
            return;
        }

        GD.Print("DOOR_DIMMING_PASS: Rooms 1 and 2 preserve their authored corridor geometry and use the identical shared corridor dimming path.");
        GetTree().Quit(0);
    }

    private async Task<bool> CaptureRoom(int roomNumber, string scenePath)
    {
        RoomRuntime? room = GD.Load<PackedScene>(scenePath)?.Instantiate<RoomRuntime>();
        if (room is null)
        {
            Fail($"Room {roomNumber} could not be instantiated.");
            return false;
        }

        AddChild(room);
        for (int frame = 0; frame < 8; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        ExitDoor3D? door = room.GetNodeOrNull<ExitDoor3D>("ExitDoor");
        if (door is null)
        {
            Fail($"Room {roomNumber} has no ExitDoor.");
            return false;
        }

        Light3D[] doorLights = EnumerateDescendants(door).OfType<Light3D>().ToArray();
        if (doorLights.Length != 1 || doorLights[0].Name != "DoorFillLight")
        {
            Fail($"Room {roomNumber} does not contain exactly the canonical DoorFillLight.");
            return false;
        }

        MeshInstance3D[] corridorMeshes = EnumerateDescendants(door)
            .OfType<MeshInstance3D>()
            .Where(mesh => mesh.GetParent().Name.ToString().StartsWith("ExitCorridor", StringComparison.Ordinal))
            .ToArray();
        MeshInstance3D[] depthMeshes = corridorMeshes
            .Where(mesh => mesh.MaterialOverride is ShaderMaterial material &&
                material.Shader?.Code.Contains("MODEL_MATRIX", StringComparison.Ordinal) == true &&
                material.Shader.Code.Contains("corridor_origin_world", StringComparison.Ordinal))
            .ToArray();
        if (corridorMeshes.Length != 5 || depthMeshes.Length != 4)
        {
            Fail($"Room {roomNumber} corridor material coverage is incomplete: meshes={corridorMeshes.Length}, depth_meshes={depthMeshes.Length}.");
            return false;
        }

        MeshInstance3D[] visibleImportedCorridorReferences = EnumerateDescendants(room)
            .OfType<MeshInstance3D>()
            .Where(mesh => mesh.Visible && mesh.HasMeta(BlenderRoomEdits.ReferenceTargetPathMetadata))
            .Where(mesh => mesh.GetMeta(BlenderRoomEdits.ReferenceTargetPathMetadata)
                .AsNodePath().ToString().Contains("ExitCorridor", StringComparison.Ordinal))
            .ToArray();
        if (visibleImportedCorridorReferences.Length != 5)
        {
            Fail($"Room {roomNumber} did not preserve all five authored corridor surfaces: visible={visibleImportedCorridorReferences.Length}.");
            return false;
        }
        if (visibleImportedCorridorReferences.Any(importedReference =>
        {
            string targetName = importedReference.GetMeta(BlenderRoomEdits.ReferenceTargetPathMetadata)
                .AsNodePath().ToString();
            if (targetName.EndsWith("ExitCorridorEndWall", StringComparison.Ordinal))
            {
                return false;
            }

            return importedReference.MaterialOverride is not ShaderMaterial material ||
                material.Shader?.Code.Contains("render_mode unshaded, cull_disabled", StringComparison.Ordinal) != true ||
                !material.Shader.Code.Contains("mix(vec3(0.35, 0.39, 0.42), vec3(0.004, 0.006, 0.008), fade)", StringComparison.Ordinal) ||
                !material.Shader.Code.Contains("corridor_depth", StringComparison.Ordinal);
        }))
        {
            Fail($"Room {roomNumber} corridor surfaces did not receive the identical canonical corridor fade.");
            return false;
        }

        PlayerBall player = room.GetNode<PlayerBall>("Player");
        PlayerCameraRig cameraRig = room.GetNode<PlayerCameraRig>("CameraRig");
        cameraRig.SetInputEnabled(false);
        player.Freeze = true;
        player.ResetTo(new Transform3D(Basis.Identity, door.ToGlobal(new Vector3(0.0f, 0.72f, 5.8f))));
        typeof(RoomRuntime)
            .GetMethod("CompleteRoom", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(room, null);
        for (int frame = 0; frame < 24; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (door.OpenAmount < 0.95f)
        {
            Fail($"Room {roomNumber} exit door did not open for the corridor capture: open={door.OpenAmount:F3}.");
            return false;
        }

        string capturePath = $"res://artifacts/door-dimming/room{roomNumber:00}.png";
        Error saveError = GetViewport().GetTexture().GetImage().SavePng(capturePath);
        if (saveError != Error.Ok)
        {
            Fail($"Room {roomNumber} capture failed: {saveError}.");
            return false;
        }

        GD.Print($"DOOR_DIMMING_CAPTURE: room={roomNumber}, path={ProjectSettings.GlobalizePath(capturePath)}, corridor_meshes={corridorMeshes.Length}, depth_meshes={depthMeshes.Length}, lights={doorLights.Length}.");
        room.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        return true;
    }

    private static IEnumerable<Node> EnumerateDescendants(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            yield return child;
            foreach (Node descendant in EnumerateDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private void Fail(string message)
    {
        GD.PushError($"DOOR_DIMMING_FAIL: {message}");
        GetTree().Quit(1);
    }
}
