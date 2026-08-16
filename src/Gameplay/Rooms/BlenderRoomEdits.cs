using Godot;
using Velocitex.Gameplay.Visual;

namespace Velocitex.Gameplay.Rooms;

internal static class BlenderRoomEdits
{
    public static void Apply(Node3D room, int roomNumber)
    {
        Node? wallsRoot = room.GetNodeOrNull<Node>("EditableWalls");
        if (wallsRoot is null)
        {
            return;
        }

        string blendPath = $"res://assets/models/EditableWallsBlender/Room{roomNumber:00}Walls.blend";
        PackedScene? importedScene = GD.Load<PackedScene>(blendPath);
        if (importedScene?.Instantiate() is not Node3D importedRoot)
        {
            GD.PushError($"BLENDER_ROOM_EDIT_FAIL: unable to load {blendPath}");
            return;
        }

        MeshInstance3D[] importedMeshes = importedRoot.GetChildren().OfType<MeshInstance3D>().ToArray();
        if (importedMeshes.Length == 0)
        {
            importedRoot.Free();
            GD.PushError($"BLENDER_ROOM_EDIT_FAIL: {blendPath} contains no visible geometry");
            return;
        }

        importedRoot.Name = "BlenderGeometry";
        room.AddChild(importedRoot);

        Dictionary<ulong, Material> blenderMaterials = new();
        Material? wallMaterial = PrepareBlenderMaterial(FindFirstMaterial(wallsRoot), blenderMaterials);
        foreach (MeshInstance3D importedWall in importedMeshes.Where(mesh => !IsReferenceMesh(mesh)))
        {
            importedWall.MaterialOverride = wallMaterial;
        }

        foreach (MeshInstance3D importedMesh in importedMeshes.Where(IsReferenceMesh))
        {
            Node3D? target = FindReferenceTarget(room, importedMesh);
            if (target is null)
            {
                importedMesh.Visible = false;
                continue;
            }

            Material? targetMaterial = PrepareBlenderMaterial(FindFirstMaterial(target), blenderMaterials);
            if (targetMaterial is null)
            {
                importedMesh.Visible = false;
                continue;
            }

            importedMesh.MaterialOverride = targetMaterial;
            if (target is StaticBody3D targetBody)
            {
                AlignCollisionToImportedMesh(room, targetBody, importedMesh);
            }

            HideVisuals(target);
        }

        foreach (StaticBody3D oldWall in EnumerateDescendants(room)
            .OfType<StaticBody3D>()
            .Where(body =>
                IsDescendantOf(body, wallsRoot) ||
                (body.HasMeta(RoomGeometry.GeneratedPlatformWallMetadata) &&
                    body.GetMeta(RoomGeometry.GeneratedPlatformWallMetadata).AsBool())))
        {
            oldWall.CollisionLayer = 0;
            oldWall.CollisionMask = 0;
            HideVisuals(oldWall);
        }

        Node3D blenderCollisions = new() { Name = "BlenderCollisions" };
        room.AddChild(blenderCollisions);
        foreach (MeshInstance3D importedWall in importedMeshes.Where(mesh => !IsReferenceMesh(mesh)))
        {
            StaticBody3D body = new()
            {
                Name = $"{importedWall.Name}_Collision",
                CollisionLayer = 1,
                CollisionMask = 1
            };
            blenderCollisions.AddChild(body);

            CollisionShape3D collision = new()
            {
                Name = "BlenderWallCollision",
                Transform = importedWall.Transform,
                Shape = importedWall.Mesh?.CreateTrimeshShape()
            };
            body.AddChild(collision);
        }
    }

    private static bool IsReferenceMesh(MeshInstance3D mesh) =>
        mesh.Name.ToString().StartsWith("REF_", StringComparison.Ordinal);

    private static Material? FindFirstMaterial(Node root)
    {
        foreach (MeshInstance3D visual in EnumerateDescendants(root)
            .Prepend(root)
            .OfType<MeshInstance3D>())
        {
            if (visual.MaterialOverride is Material materialOverride)
            {
                return materialOverride;
            }

            if (visual.Mesh?.GetSurfaceCount() > 0 && visual.GetActiveMaterial(0) is Material activeMaterial)
            {
                return activeMaterial;
            }
        }

        return null;
    }

    private static Material? PrepareBlenderMaterial(
        Material? source,
        IDictionary<ulong, Material> cache)
    {
        if (source is null)
        {
            return null;
        }

        ulong sourceId = source.GetInstanceId();
        if (cache.TryGetValue(sourceId, out Material? cached))
        {
            return cached;
        }

        Material prepared = (Material)source.Duplicate();
        if (prepared is BaseMaterial3D spatialMaterial)
        {
            spatialMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            spatialMaterial.Uv1Triplanar = true;
            spatialMaterial.Uv1WorldTriplanar = true;
            spatialMaterial.Uv1Scale = Vector3.One / SurfaceMeshFactory.DefaultTileWorldSize;
        }

        cache[sourceId] = prepared;
        return prepared;
    }

    private static string ReferenceTargetName(MeshInstance3D mesh)
    {
        string name = mesh.Name.ToString();
        int separator = name.IndexOf('_', 4);
        return separator >= 0 ? name[(separator + 1)..] : name;
    }

    private static Node3D? FindReferenceTarget(Node root, MeshInstance3D importedMesh)
    {
        string importedName = ReferenceTargetName(importedMesh);
        string dottedName = importedName.Replace("_001", ".001", StringComparison.Ordinal);
        Node3D[] candidates = EnumerateDescendants(root)
            .OfType<Node3D>()
            .Where(node =>
                node.Name.ToString().Equals(importedName, StringComparison.Ordinal) ||
                node.Name.ToString().Equals(dottedName, StringComparison.Ordinal))
            .ToArray();
        if (candidates.Length <= 1)
        {
            return candidates.FirstOrDefault();
        }

        Aabb importedBounds = GlobalBounds(importedMesh);
        return candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = EnumerateDescendants(candidate)
                    .Prepend(candidate)
                    .OfType<MeshInstance3D>()
                    .Select(visual => BoundsError(importedBounds, GlobalBounds(visual)))
                    .DefaultIfEmpty(float.PositiveInfinity)
                    .Min()
            })
            .OrderBy(match => match.Score)
            .Select(match => match.Candidate)
            .FirstOrDefault();
    }

    private static float BoundsError(Aabb left, Aabb right) =>
        left.Position.DistanceTo(right.Position) + left.Size.DistanceTo(right.Size);

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

    private static bool IsDescendantOf(Node node, Node ancestor)
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

    private static void HideVisuals(Node target)
    {
        foreach (MeshInstance3D visual in EnumerateDescendants(target)
            .Prepend(target)
            .OfType<MeshInstance3D>())
        {
            visual.Visible = false;
        }
    }

    private static void AlignCollisionToImportedMesh(Node3D room, StaticBody3D body, MeshInstance3D mesh)
    {
        CollisionShape3D? collision = body.GetChildren()
            .OfType<CollisionShape3D>()
            .FirstOrDefault(child => child.Shape is BoxShape3D);
        if (collision?.Shape is not BoxShape3D box || mesh.Mesh is null)
        {
            return;
        }

        Aabb aabb = mesh.Mesh.GetAabb();
        Vector3 scale = mesh.Transform.Basis.Scale.Abs();
        Basis rotation = mesh.Transform.Basis.Orthonormalized();
        Transform3D geometryTransform = new(rotation, mesh.Transform * aabb.GetCenter());
        body.GlobalTransform = room.GlobalTransform * geometryTransform * collision.Transform.AffineInverse();
        box.Size = aabb.Size * scale;
    }
}
