using Godot;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Tools;

public partial class EditableWallBaker : Node
{
    public override async void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://scenes/EditableWalls"));
        for (int roomNumber = 1; roomNumber <= 30; roomNumber++)
        {
            string roomPath = roomNumber == 1
                ? "res://scenes/MovementTestRoom.tscn"
                : $"res://scenes/Room{roomNumber:00}.tscn";
            PackedScene? source = GD.Load<PackedScene>(roomPath);
            if (source is null)
            {
                GD.PushError($"EDITABLE_WALL_BAKE_FAIL: missing {roomPath}");
                GetTree().Quit(1);
                return;
            }

            Node runtimeRoom = source.Instantiate();
            AddChild(runtimeRoom);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            Node3D wallsRoot = new() { Name = "EditableWalls" };
            foreach (StaticBody3D wall in FindGeneratedWalls(runtimeRoom).ToArray())
            {
                Transform3D roomTransform = runtimeRoom is Node3D room3D
                    ? room3D.GlobalTransform.AffineInverse() * wall.GlobalTransform
                    : wall.GlobalTransform;
                wall.Reparent(wallsRoot, keepGlobalTransform: false);
                wall.Transform = roomTransform;
                SetOwnerRecursive(wall, wallsRoot);
            }

            PackedScene wallScene = new();
            Error packError = wallScene.Pack(wallsRoot);
            string wallPath = $"res://scenes/EditableWalls/Room{roomNumber:00}Walls.tscn";
            Error saveError = packError == Error.Ok
                ? ResourceSaver.Save(wallScene, wallPath)
                : packError;
            wallsRoot.Free();
            runtimeRoom.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (saveError != Error.Ok)
            {
                GD.PushError($"EDITABLE_WALL_BAKE_FAIL: {wallPath}: {saveError}");
                GetTree().Quit(1);
                return;
            }

            PackedScene? savedWalls = GD.Load<PackedScene>(wallPath);
            Node cleanRoom = source.Instantiate();
            cleanRoom.GetNodeOrNull<Node>("EditableWalls")?.Free();
            Node wallInstance = savedWalls!.Instantiate();
            cleanRoom.AddChild(wallInstance);
            wallInstance.Owner = cleanRoom;
            cleanRoom.SetEditableInstance(wallInstance, true);
            PackedScene updatedRoom = new();
            Error roomPackError = updatedRoom.Pack(cleanRoom);
            Error roomSaveError = roomPackError == Error.Ok
                ? ResourceSaver.Save(updatedRoom, roomPath)
                : roomPackError;
            cleanRoom.Free();
            if (roomSaveError != Error.Ok)
            {
                GD.PushError($"EDITABLE_WALL_BAKE_FAIL: {roomPath}: {roomSaveError}");
                GetTree().Quit(1);
                return;
            }

            GD.Print($"EDITABLE_WALL_BAKE_ROOM_PASS: Room {roomNumber:00}");
        }

        GD.Print("EDITABLE_WALL_BAKE_PASS");
        GetTree().Quit();
    }

    private static IEnumerable<StaticBody3D> FindGeneratedWalls(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is StaticBody3D body && body.HasMeta(RoomGeometry.GeneratedPlatformWallMetadata))
            {
                yield return body;
            }

            foreach (StaticBody3D nested in FindGeneratedWalls(child))
            {
                yield return nested;
            }
        }
    }

    private static void SetOwnerRecursive(Node node, Node owner)
    {
        node.Owner = owner;
        foreach (Node child in node.GetChildren())
        {
            SetOwnerRecursive(child, owner);
        }
    }
}
