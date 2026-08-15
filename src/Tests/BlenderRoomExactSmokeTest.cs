using Godot;

namespace Velocitex.Tests;

public partial class BlenderRoomExactSmokeTest : Node
{
    private const float Tolerance = 0.0005f;

    public override async void _Ready()
    {
        int failures = 0;
        foreach (int roomNumber in new[] { 1, 2 })
        {
            PackedScene packed = GD.Load<PackedScene>(roomNumber == 1
                ? "res://scenes/MovementTestRoom.tscn"
                : $"res://scenes/Room{roomNumber:00}.tscn");
            Node3D room = (Node3D)packed.Instantiate();
            AddChild(room);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            failures += AuditRoom(room, roomNumber);
            room.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (failures == 0)
        {
            GD.Print("BLENDER_ROOM_EXACT_PASS: Rooms 01-02 runtime walls exactly match their exported Blender geometry.");
        }
        else
        {
            GD.PushError($"BLENDER_ROOM_EXACT_FAIL: {failures} mismatch(es) found.");
            throw new InvalidOperationException(
                $"Rooms 01-02 runtime geometry differs from the exported Blender geometry in {failures} place(s).");
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(failures == 0 ? 0 : 1);
    }

    private static int AuditRoom(Node3D room, int roomNumber)
    {
        string path = $"res://assets/models/EditableWallsBlender/Room{roomNumber:00}Walls.blend";
        PackedScene importedScene = GD.Load<PackedScene>(path);
        Node3D importedRoot = (Node3D)importedScene.Instantiate();
        Dictionary<string, MeshInstance3D> expected = importedRoot.GetChildren()
            .OfType<MeshInstance3D>()
            .Where(mesh => !mesh.Name.ToString().StartsWith("REF_", StringComparison.Ordinal))
            .ToDictionary(mesh => mesh.Name.ToString(), StringComparer.Ordinal);

        Node? wallsRoot = room.FindChild("EditableWalls", recursive: true, owned: false);
        Dictionary<string, MeshInstance3D> actual = wallsRoot?.GetChildren()
            .OfType<StaticBody3D>()
            .SelectMany(body => body.GetChildren().OfType<MeshInstance3D>())
            .ToDictionary(mesh => mesh.Name.ToString(), StringComparer.Ordinal)
            ?? new Dictionary<string, MeshInstance3D>(StringComparer.Ordinal);

        int failures = 0;
        float maximumError = 0.0f;
        if (actual.Count != expected.Count)
        {
            GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} wall count runtime={actual.Count}, Blender={expected.Count}.");
            failures++;
        }

        foreach ((string name, MeshInstance3D expectedMesh) in expected)
        {
            if (!actual.TryGetValue(name, out MeshInstance3D? actualMesh))
            {
                GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} missing wall {name}.");
                failures++;
                continue;
            }

            float error = GeometryError(actualMesh, expectedMesh);
            maximumError = Mathf.Max(maximumError, error);
            bool hasCollision = actualMesh.GetParent().GetChildren()
                .OfType<CollisionShape3D>()
                .Any(collision => collision.Shape is ConcavePolygonShape3D);
            if (error > Tolerance || !hasCollision)
            {
                GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} wall {name} error={error:F6}, collision={hasCollision}.");
                failures++;
            }
        }

        if (failures == 0)
        {
            GD.Print($"BLENDER_ROOM_EXACT_ROOM_PASS: Room {roomNumber:00} walls={expected.Count}, maximum error={maximumError:F6}.");
        }

        importedRoot.Free();
        return failures;
    }

    private static float GeometryError(MeshInstance3D actual, MeshInstance3D expected)
    {
        Aabb actualAabb = actual.Mesh?.GetAabb() ?? default;
        Aabb expectedAabb = expected.Mesh?.GetAabb() ?? default;
        return Mathf.Max(
            Mathf.Max(actual.Transform.Origin.DistanceTo(expected.Transform.Origin), actualAabb.Size.DistanceTo(expectedAabb.Size)),
            Mathf.Max(
                actual.Transform.Basis.X.DistanceTo(expected.Transform.Basis.X),
                Mathf.Max(
                    actual.Transform.Basis.Y.DistanceTo(expected.Transform.Basis.Y),
                    actual.Transform.Basis.Z.DistanceTo(expected.Transform.Basis.Z))));
    }
}
