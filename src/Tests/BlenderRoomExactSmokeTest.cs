using Godot;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Tests;

public partial class BlenderRoomExactSmokeTest : Node
{
    private const float Tolerance = 0.0005f;

    public override async void _Ready()
    {
        int failures = 0;
        foreach (int roomNumber in Enumerable.Range(1, 30))
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
            GD.Print("BLENDER_ROOM_EXACT_PASS: All 30 rooms render every mesh directly from their imported Blender scene.");
        }
        else
        {
            GD.PushError($"BLENDER_ROOM_EXACT_FAIL: {failures} mismatch(es) found.");
            throw new InvalidOperationException(
                $"Runtime geometry differs from the imported Blender scenes in {failures} place(s).");
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
            .ToDictionary(mesh => mesh.Name.ToString(), StringComparer.Ordinal);

        Node? blenderGeometry = room.GetNodeOrNull<Node>("BlenderGeometry");
        Dictionary<string, MeshInstance3D> actual = (blenderGeometry?.GetChildren()
            .OfType<MeshInstance3D>() ?? Enumerable.Empty<MeshInstance3D>())
            .ToDictionary(mesh => mesh.Name.ToString(), StringComparer.Ordinal);

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
            bool isReference = name.StartsWith("REF_", StringComparison.Ordinal);
            Node? collisionRoot = room.GetNodeOrNull<Node>("BlenderCollisions");
            bool hasCollision = isReference || collisionRoot?.GetChildren()
                .OfType<StaticBody3D>()
                .Any(body => body.Name.ToString().Equals($"{name}_Collision", StringComparison.Ordinal) &&
                    body.GetChildren().OfType<CollisionShape3D>()
                        .Any(collision => collision.Shape is ConcavePolygonShape3D)) == true;
            if (error > Tolerance || !hasCollision)
            {
                GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} wall {name} error={error:F6}, collision={hasCollision}.");
                failures++;
            }

            if (IsVisible(actualMesh) && actualMesh.MaterialOverride is null)
            {
                GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} visible Blender mesh {name} did not inherit a game material.");
                failures++;
            }

            if (IsVisible(actualMesh) &&
                actualMesh.MaterialOverride is BaseMaterial3D texturedMaterial &&
                texturedMaterial.AlbedoTexture is not null &&
                (!texturedMaterial.Uv1Triplanar || !texturedMaterial.Uv1WorldTriplanar))
            {
                GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} textured Blender mesh {name} has no UV-independent mapping.");
                failures++;
            }

            if (IsVisible(actualMesh) &&
                actualMesh.MaterialOverride is BaseMaterial3D visibleMaterial &&
                visibleMaterial.CullMode != BaseMaterial3D.CullModeEnum.Disabled)
            {
                GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} Blender mesh {name} still culls camera-facing geometry with reversed normals.");
                failures++;
            }
        }

        failures += AuditLegacyVisuals(room, blenderGeometry, actual, roomNumber);

        if (failures == 0)
        {
            GD.Print($"BLENDER_ROOM_EXACT_ROOM_PASS: Room {roomNumber:00} meshes={expected.Count}, maximum error={maximumError:F6}.");
        }

        importedRoot.Free();
        return failures;
    }

    private static int AuditLegacyVisuals(
        Node3D room,
        Node? blenderGeometry,
        IReadOnlyDictionary<string, MeshInstance3D> importedMeshes,
        int roomNumber)
    {
        int failures = 0;
        MeshInstance3D[] legacyVisuals = EnumerateDescendants(room)
            .OfType<MeshInstance3D>()
            .Where(mesh => !IsDescendantOf(mesh, blenderGeometry))
            .ToArray();

        Node? oldWalls = room.GetNodeOrNull<Node>("EditableWalls");
        foreach (MeshInstance3D oldWallVisual in legacyVisuals.Where(mesh => IsDescendantOf(mesh, oldWalls)))
        {
            if (IsVisible(oldWallVisual))
            {
                GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} legacy wall visual {oldWallVisual.GetPath()} is still visible.");
                failures++;
            }
        }

        foreach (StaticBody3D generatedWall in EnumerateDescendants(room)
            .OfType<StaticBody3D>()
            .Where(body =>
                body.HasMeta(RoomGeometry.GeneratedPlatformWallMetadata) &&
                body.GetMeta(RoomGeometry.GeneratedPlatformWallMetadata).AsBool()))
        {
            foreach (MeshInstance3D visual in EnumerateDescendants(generatedWall)
                .Prepend(generatedWall)
                .OfType<MeshInstance3D>())
            {
                if (IsVisible(visual))
                {
                    GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} generated legacy wall {generatedWall.GetPath()} is still visible.");
                    failures++;
                }
            }
        }

        foreach ((string name, MeshInstance3D importedMesh) in importedMeshes)
        {
            if (!IsVisible(importedMesh))
            {
                continue;
            }

            Aabb importedBounds = GlobalBounds(importedMesh);
            foreach (MeshInstance3D legacyVisual in legacyVisuals.Where(IsVisible))
            {
                Aabb legacyBounds = GlobalBounds(legacyVisual);
                if (importedBounds.Position.DistanceTo(legacyBounds.Position) <= Tolerance &&
                    importedBounds.Size.DistanceTo(legacyBounds.Size) <= Tolerance)
                {
                    GD.PushError(
                        $"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} duplicate visible geometry: Blender {name} overlaps legacy {legacyVisual.GetPath()}.");
                    failures++;
                }
            }
        }

        foreach ((string name, MeshInstance3D importedMesh) in importedMeshes
            .Where(pair => pair.Key.StartsWith("REF_", StringComparison.Ordinal)))
        {
            string targetName = ReferenceTargetName(name);
            Node3D? target = EnumerateDescendants(room)
                .OfType<Node3D>()
                .Where(node => !IsDescendantOf(node, blenderGeometry))
                .FirstOrDefault(node => NamesMatch(node.Name.ToString(), targetName));
            MeshInstance3D[] targetVisuals = target is null
                ? Array.Empty<MeshInstance3D>()
                : EnumerateDescendants(target).Prepend(target).OfType<MeshInstance3D>().ToArray();
            bool hasReusableMaterial = targetVisuals.Any(visual =>
                visual.MaterialOverride is not null ||
                (visual.Mesh?.GetSurfaceCount() > 0 && visual.GetActiveMaterial(0) is not null));

            if (target is null || !hasReusableMaterial)
            {
                if (IsVisible(importedMesh))
                {
                    GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} unmatched reference {name} is visible.");
                    failures++;
                }

                continue;
            }

            foreach (MeshInstance3D targetVisual in targetVisuals)
            {
                if (IsVisible(targetVisual))
                {
                    GD.PushError(
                        $"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} reference {name} and its legacy target {targetVisual.GetPath()} are both visible.");
                    failures++;
                }
            }
        }

        return failures;
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

    private static bool IsDescendantOf(Node node, Node? ancestor)
    {
        for (Node? current = node; current is not null; current = current.GetParent())
        {
            if (current == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsVisible(MeshInstance3D mesh) => mesh.IsVisibleInTree();

    private static string ReferenceTargetName(string name)
    {
        int separator = name.IndexOf('_', 4);
        return separator >= 0 ? name[(separator + 1)..] : name;
    }

    private static bool NamesMatch(string actual, string imported) =>
        actual.Equals(imported, StringComparison.Ordinal) ||
        actual.Equals(imported.Replace("_001", ".001", StringComparison.Ordinal), StringComparison.Ordinal);

    private static Aabb GlobalBounds(MeshInstance3D mesh)
    {
        Aabb local = mesh.Mesh?.GetAabb() ?? default;
        Vector3[] corners =
        {
            local.Position,
            local.Position + new Vector3(local.Size.X, 0, 0),
            local.Position + new Vector3(0, local.Size.Y, 0),
            local.Position + new Vector3(0, 0, local.Size.Z),
            local.Position + new Vector3(local.Size.X, local.Size.Y, 0),
            local.Position + new Vector3(local.Size.X, 0, local.Size.Z),
            local.Position + new Vector3(0, local.Size.Y, local.Size.Z),
            local.End
        };
        Vector3 first = mesh.GlobalTransform * corners[0];
        Vector3 minimum = first;
        Vector3 maximum = first;
        foreach (Vector3 corner in corners.Skip(1))
        {
            Vector3 transformed = mesh.GlobalTransform * corner;
            minimum = minimum.Min(transformed);
            maximum = maximum.Max(transformed);
        }

        return new Aabb(minimum, maximum - minimum);
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
