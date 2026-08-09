using Godot;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Tools;

public partial class BlenderRoomReferenceExporter : Node
{
    public override async void _Ready()
    {
        string outputDirectory = ProjectSettings.GlobalizePath("res://tools/blender/reference");
        DirAccess.MakeDirRecursiveAbsolute(outputDirectory);
        int firstRoom = 1;
        string[] args = OS.GetCmdlineUserArgs();
        if (args.Length > 0 && int.TryParse(args[0], out int requestedFirstRoom))
        {
            firstRoom = Mathf.Clamp(requestedFirstRoom, 1, 30);
        }
        for (int roomNumber = firstRoom; roomNumber <= 30; roomNumber++)
        {
            string scenePath = roomNumber == 1
                ? "res://scenes/MovementTestRoom.tscn"
                : $"res://scenes/Room{roomNumber:00}.tscn";
            Node room = GD.Load<PackedScene>(scenePath).Instantiate();
            AddChild(room);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            Godot.Collections.Array entries = new();
            CollectBoxes(room, entries);
            using Godot.FileAccess file = Godot.FileAccess.Open(
                $"res://tools/blender/reference/Room{roomNumber:00}.json",
                Godot.FileAccess.ModeFlags.Write);
            file.StoreString(Json.Stringify(entries, "  "));
            room.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print($"BLENDER_REFERENCE_ROOM_PASS: Room {roomNumber:00} boxes={entries.Count}");
        }

        GD.Print("BLENDER_REFERENCE_EXPORT_PASS");
        GetTree().Quit();
    }

    private static void CollectBoxes(Node node, Godot.Collections.Array entries)
    {
        if (node is StaticBody3D body &&
            !body.HasMeta(RoomGeometry.GeneratedPlatformWallMetadata))
        {
            foreach (Node child in body.GetChildren())
            {
                if (child is CollisionShape3D { Shape: BoxShape3D box } collision)
                {
                    Transform3D transform = body.GlobalTransform * collision.Transform;
                    Basis basis = transform.Basis;
                    entries.Add(new Godot.Collections.Dictionary
                    {
                        ["name"] = body.Name.ToString(),
                        ["size"] = new Godot.Collections.Array { box.Size.X, box.Size.Y, box.Size.Z },
                        ["transform"] = new Godot.Collections.Array
                        {
                            basis.X.X, basis.X.Y, basis.X.Z,
                            basis.Y.X, basis.Y.Y, basis.Y.Z,
                            basis.Z.X, basis.Z.Y, basis.Z.Z,
                            transform.Origin.X, transform.Origin.Y, transform.Origin.Z,
                        },
                    });
                    break;
                }
            }
        }

        foreach (Node child in node.GetChildren())
        {
            CollectBoxes(child, entries);
        }
    }
}
