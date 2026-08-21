using Godot;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Tests;

public partial class BlenderDeletionSmokeTest : Node
{
    public override async void _Ready()
    {
        RoomRuntime? room = GD.Load<PackedScene>("res://scenes/MovementTestRoom.tscn")?.Instantiate<RoomRuntime>();
        if (room is null)
        {
            Fail("Room 01 could not be instantiated.");
            return;
        }

        AddChild(room);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        Node3D? deletedRunout = room.GetNodeOrNull<Node3D>("ExitRunout");
        bool deletedCollisionRemains = deletedRunout is not null && EnumerateDescendants(deletedRunout)
            .Prepend(deletedRunout)
            .OfType<CollisionObject3D>()
            .Any(collision => collision.CollisionLayer != 0 || collision.CollisionMask != 0);
        if (deletedRunout is null ||
            !deletedRunout.HasMeta(BlenderRoomEdits.DeletedByBlenderMetadata) ||
            deletedRunout.Visible ||
            deletedRunout.ProcessMode != ProcessModeEnum.Disabled ||
            deletedCollisionRemains)
        {
            Fail("Room 01's deleted ExitRunout still renders, processes or collides at runtime.");
            return;
        }

        MeshInstance3D[] importedFramePieces = room.GetNode<Node>("BlenderGeometry").GetChildren()
            .OfType<MeshInstance3D>()
            .Where(mesh => mesh.Name.ToString().StartsWith("REF_024_FrameCollision", StringComparison.Ordinal))
            .ToArray();
        ExitDoor3D door = room.GetNode<ExitDoor3D>("ExitDoor");
        bool originalPocketVisible = new[] { "LeftDoorPocketMask", "RightDoorPocketMask" }
            .Select(name => door.GetNode<MeshInstance3D>(name))
            .Any(mesh => mesh.Visible);
        if (importedFramePieces.Length != 2 ||
            importedFramePieces.Any(mesh => !mesh.Visible || mesh.MaterialOverride is null) ||
            originalPocketVisible)
        {
            Fail($"Room 01 did not replace both visible door-frame pocket pieces from Blender (imported={importedFramePieces.Length}, original_visible={originalPocketVisible}).");
            return;
        }

        GD.Print("BLENDER_DELETION_PASS: Room 01's deleted ExitRunout is absent from rendering, processing and collision, and both edited door-frame pieces render from Blender.");
        room.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(0);
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
        GD.PushError($"BLENDER_DELETION_FAIL: {message}");
        GetTree().Quit(1);
    }
}
