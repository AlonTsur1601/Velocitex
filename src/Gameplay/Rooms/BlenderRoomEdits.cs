using Godot;
using System.Text.Json;
using Velocitex.Gameplay.Visual;

namespace Velocitex.Gameplay.Rooms;

internal static class BlenderRoomEdits
{
    internal const string ReferenceTargetPathMetadata = "blender_reference_target_path";
    internal const string ReferenceCollisionPathMetadata = "blender_reference_collision_path";
    internal const string DeletedByBlenderMetadata = "blender_reference_deleted";
    internal const string JoinShadowSuppressedMetadata = "blender_join_shadow_suppressed";

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

        BlenderReferenceBinding referenceBinding = new(room)
        {
            Name = "BlenderReferenceBindings",
            ProcessPriority = 100,
        };
        room.AddChild(referenceBinding);

        Dictionary<(ulong SourceId, bool UsesUv), Material> blenderMaterials = new();
        foreach (MeshInstance3D importedMesh in importedMeshes)
        {
            EnsureUvLayer(importedMesh);
        }

        Material? wallMaterial = PrepareBlenderMaterial(
            FindFirstMaterial(wallsRoot),
            blenderMaterials,
            HasUvLayer(importedMeshes.FirstOrDefault(mesh => !IsReferenceMesh(mesh))?.Mesh));
        foreach (MeshInstance3D importedWall in importedMeshes.Where(mesh => !IsReferenceMesh(mesh)))
        {
            importedWall.MaterialOverride = wallMaterial;
            // The authored wall pieces intentionally overlap slightly at joins.
            // Let the room's actual platforms and props cast shadows, but keep
            // these shell pieces from projecting dark seams onto one another.
            importedWall.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }

        Dictionary<StaticBody3D, List<MeshInstance3D>> collisionReferences = new();
        HashSet<MeshInstance3D> replacedDoorFrameVisuals = new();
        foreach (MeshInstance3D importedMesh in importedMeshes.Where(IsReferenceMesh))
        {
            if (IsDenseCannonRoom(roomNumber) &&
                ReferenceTargetName(importedMesh).Equals("CannonHitbox", StringComparison.Ordinal))
            {
                // Dense cannon rooms already keep one independent runtime
                // hitbox per cannon. Importing a second triangle mesh for
                // every hitbox adds hundreds of meshes and concave shapes.
                importedMesh.Visible = false;
                continue;
            }

            Node3D? target = FindReferenceTarget(room, importedMesh);
            if (target is null)
            {
                importedMesh.Visible = false;
                continue;
            }

            MeshInstance3D? targetVisual = FindFirstVisual(target) ??
                FindDoorFrameVisual(target, importedMesh, replacedDoorFrameVisuals);
            Material? sourceMaterial = FindFirstMaterial(target) ??
                (targetVisual is null ? null : FindFirstMaterial(targetVisual));
            Material? targetMaterial = PrepareBlenderMaterial(
                sourceMaterial,
                blenderMaterials,
                HasUvLayer(importedMesh.Mesh));
            if (targetMaterial is null)
            {
                importedMesh.Visible = false;
                if (target is StaticBody3D hiddenTargetBody)
                {
                    AddCollisionReference(collisionReferences, hiddenTargetBody, importedMesh);
                    importedMesh.SetMeta(ReferenceTargetPathMetadata, room.GetPathTo(target));
                }
                continue;
            }

            importedMesh.MaterialOverride = targetMaterial;
            bool suppressJoinShadow = IsVerticalJoinPanel(importedMesh);
            importedMesh.CastShadow = suppressJoinShadow
                ? GeometryInstance3D.ShadowCastingSetting.Off
                : targetVisual?.CastShadow ?? GeometryInstance3D.ShadowCastingSetting.Off;
            if (suppressJoinShadow)
            {
                importedMesh.SetMeta(JoinShadowSuppressedMetadata, true);
            }
            if (target is StaticBody3D targetBody)
            {
                AddCollisionReference(collisionReferences, targetBody, importedMesh);
            }

            HideVisuals(target);
            if (targetVisual is not null)
            {
                targetVisual.Visible = false;
                replacedDoorFrameVisuals.Add(targetVisual);
            }
            importedMesh.Visible = IsVisibleThrough(target, room);
            importedMesh.SetMeta(ReferenceTargetPathMetadata, room.GetPathTo(target));
            referenceBinding.Bind(importedMesh, target);
        }

        foreach ((StaticBody3D body, List<MeshInstance3D> references) in collisionReferences)
        {
            ApplyImportedMeshCollisions(body, references.OrderBy(mesh => mesh.Name.ToString(), StringComparer.Ordinal).ToArray());
        }

        ApplyDeletedReferences(room, roomNumber, importedMeshes);

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

        Node? roomShell = room.GetNodeOrNull<Node>("RoomShell");
        foreach (MeshInstance3D legacyCornerJoin in EnumerateDescendants(roomShell ?? room)
            .OfType<MeshInstance3D>()
            .Where(mesh => mesh.Name.ToString().EndsWith("CornerJoin", StringComparison.Ordinal)))
        {
            legacyCornerJoin.Visible = false;
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
                // BlenderGeometry and the imported mesh can both carry a
                // transform.  The collision body is a sibling of that import
                // root, so copying only the mesh's local transform puts the
                // hitbox somewhere different from the rendered triangles.
                Transform = body.GlobalTransform.AffineInverse() * importedWall.GlobalTransform,
                Shape = CreateImportedCollisionShape(importedWall.Mesh)
            };
            body.AddChild(collision);
        }
    }

    private static bool IsReferenceMesh(MeshInstance3D mesh) =>
        mesh.Name.ToString().StartsWith("REF_", StringComparison.Ordinal);

    private static bool IsDenseCannonRoom(int roomNumber) => roomNumber is 17 or 20 or 30;

    private static bool IsVerticalJoinPanel(MeshInstance3D mesh)
    {
        Aabb bounds = GlobalBounds(mesh);
        float horizontalThickness = Mathf.Min(bounds.Size.X, bounds.Size.Z);
        return bounds.Size.Y >= 1.0f && horizontalThickness <= 1.0f;
    }

    private static MeshInstance3D? FindDoorFrameVisual(
        Node3D target,
        MeshInstance3D importedMesh,
        ISet<MeshInstance3D> claimed)
    {
        if (!target.Name.ToString().Equals("FrameCollision", StringComparison.Ordinal) ||
            target.GetParent() is not Node parent)
        {
            return null;
        }

        string[] frameVisualNames =
        {
            "LeftDoorPocketMask",
            "RightDoorPocketMask",
            "LeftFrame",
            "RightFrame",
            "Header",
        };
        Aabb importedBounds = GlobalBounds(importedMesh);
        return parent.GetChildren()
            .OfType<MeshInstance3D>()
            .Where(visual => frameVisualNames.Contains(visual.Name.ToString(), StringComparer.Ordinal) && !claimed.Contains(visual))
            .OrderBy(visual => BoundsError(importedBounds, GlobalBounds(visual)))
            .FirstOrDefault();
    }

    private static void ApplyDeletedReferences(
        Node3D room,
        int roomNumber,
        IReadOnlyCollection<MeshInstance3D> importedMeshes)
    {
        string referencePath = $"res://tools/blender/reference/Room{roomNumber:00}.json";
        string json = Godot.FileAccess.GetFileAsString(referencePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            GD.PushError($"BLENDER_ROOM_EDIT_FAIL: unable to read deletion manifest {referencePath}");
            return;
        }

        HashSet<string> importedNames = importedMeshes
            .Where(IsReferenceMesh)
            .Select(mesh => mesh.Name.ToString())
            .ToHashSet(StringComparer.Ordinal);
        using JsonDocument document = JsonDocument.Parse(json);
        int index = 0;
        foreach (JsonElement entry in document.RootElement.EnumerateArray())
        {
            string targetName = entry.GetProperty("name").GetString() ?? string.Empty;
            string referenceName = $"REF_{index:000}_{targetName}";
            index++;
            if (IsOuterShellReferenceTarget(targetName) ||
                (IsDenseCannonRoom(roomNumber) && targetName.Equals("CannonHitbox", StringComparison.Ordinal)) ||
                importedNames.Any(name => name.Equals(referenceName, StringComparison.Ordinal) ||
                    name.StartsWith(referenceName + ".", StringComparison.Ordinal)))
            {
                continue;
            }

            Node3D? target = FindReferenceTargetByName(room, targetName);
            if (target is null)
            {
                continue;
            }

            DisableDeletedTarget(target);
            target.SetMeta(DeletedByBlenderMetadata, true);
        }
    }

    private static bool IsOuterShellReferenceTarget(string targetName) =>
        targetName is "HazardFloor" or "Ceiling" or "LeftWall" or "RightWall" or "BackWall" or "ExitWall";

    private static Node3D? FindReferenceTargetByName(Node root, string importedName)
    {
        string dottedName = importedName.Replace("_001", ".001", StringComparison.Ordinal);
        string unsuffixedName = RemoveNumericDuplicateSuffix(importedName);
        return EnumerateDescendants(root)
            .OfType<Node3D>()
            .FirstOrDefault(node =>
                node.Name.ToString().Equals(importedName, StringComparison.Ordinal) ||
                node.Name.ToString().Equals(dottedName, StringComparison.Ordinal) ||
                node.Name.ToString().Equals(unsuffixedName, StringComparison.Ordinal));
    }

    private static void DisableDeletedTarget(Node3D target)
    {
        target.Visible = false;
        target.ProcessMode = Node.ProcessModeEnum.Disabled;
        foreach (Node node in EnumerateDescendants(target).Prepend(target))
        {
            if (node is CollisionObject3D collisionObject)
            {
                collisionObject.CollisionLayer = 0;
                collisionObject.CollisionMask = 0;
            }
            if (node is CollisionShape3D collisionShape)
            {
                collisionShape.Disabled = true;
            }
            if (node is Area3D area)
            {
                area.Monitoring = false;
                area.Monitorable = false;
            }
        }
    }

    private static void AddCollisionReference(
        IDictionary<StaticBody3D, List<MeshInstance3D>> referencesByBody,
        StaticBody3D body,
        MeshInstance3D mesh)
    {
        if (!referencesByBody.TryGetValue(body, out List<MeshInstance3D>? references))
        {
            references = new List<MeshInstance3D>();
            referencesByBody[body] = references;
        }
        references.Add(mesh);
    }

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

    private static MeshInstance3D? FindFirstVisual(Node root) =>
        EnumerateDescendants(root)
            .Prepend(root)
            .OfType<MeshInstance3D>()
            .FirstOrDefault();

    private static Material? PrepareBlenderMaterial(
        Material? source,
        IDictionary<(ulong SourceId, bool UsesUv), Material> cache,
        bool usesUv)
    {
        if (source is null)
        {
            return null;
        }

        (ulong SourceId, bool UsesUv) cacheKey = (source.GetInstanceId(), usesUv);
        if (cache.TryGetValue(cacheKey, out Material? cached))
        {
            return cached;
        }

        Material prepared = (Material)source.Duplicate();
        if (prepared is BaseMaterial3D spatialMaterial)
        {
            spatialMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            spatialMaterial.Uv1Triplanar = !usesUv;
            spatialMaterial.Uv1WorldTriplanar = !usesUv;
            spatialMaterial.Uv1Scale = usesUv
                ? Vector3.One
                : Vector3.One / SurfaceMeshFactory.DefaultTileWorldSize;
        }
        else if (prepared is ShaderMaterial shaderMaterial && shaderMaterial.Shader is Shader sourceShader)
        {
            Shader doubleSidedShader = (Shader)sourceShader.Duplicate();
            doubleSidedShader.Code = EnsureCullDisabled(doubleSidedShader.Code);
            shaderMaterial.Shader = doubleSidedShader;
        }

        cache[cacheKey] = prepared;
        return prepared;
    }

    private static string EnsureCullDisabled(string code)
    {
        if (code.Contains("cull_disabled", StringComparison.Ordinal))
        {
            return code;
        }

        int renderModeIndex = code.IndexOf("render_mode", StringComparison.Ordinal);
        if (renderModeIndex >= 0)
        {
            int semicolonIndex = code.IndexOf(';', renderModeIndex);
            if (semicolonIndex >= 0)
            {
                return code.Insert(semicolonIndex, ", cull_disabled");
            }
        }

        const string shaderType = "shader_type spatial;";
        int shaderTypeIndex = code.IndexOf(shaderType, StringComparison.Ordinal);
        if (shaderTypeIndex < 0)
        {
            return code;
        }

        int insertionIndex = shaderTypeIndex + shaderType.Length;
        return code.Insert(insertionIndex, "\nrender_mode cull_disabled;");
    }

    private static void EnsureUvLayer(MeshInstance3D mesh)
    {
        if (mesh.Mesh is not ArrayMesh source || HasUvLayer(source))
        {
            return;
        }

        ArrayMesh rebuilt = new();
        Vector3 scale = mesh.Transform.Basis.Scale.Abs();
        for (int surface = 0; surface < source.GetSurfaceCount(); surface++)
        {
            Godot.Collections.Array arrays = source.SurfaceGetArrays(surface);
            Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            Vector3[] normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            if (vertices.Length == 0)
            {
                continue;
            }

            Vector3 minimum = vertices[0] * scale;
            foreach (Vector3 vertex in vertices.Skip(1))
            {
                minimum = minimum.Min(vertex * scale);
            }

            Vector2[] uvs = new Vector2[vertices.Length];
            for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
            {
                Vector3 position = (vertices[vertexIndex] * scale) - minimum;
                Vector3 normal = vertexIndex < normals.Length ? normals[vertexIndex].Abs() : Vector3.Up;
                if (normal.Y >= normal.X && normal.Y >= normal.Z)
                {
                    uvs[vertexIndex] = new Vector2(position.X, position.Z) / SurfaceMeshFactory.DefaultTileWorldSize;
                }
                else if (normal.X >= normal.Z)
                {
                    uvs[vertexIndex] = new Vector2(position.Z, position.Y) / SurfaceMeshFactory.DefaultTileWorldSize;
                }
                else
                {
                    uvs[vertexIndex] = new Vector2(position.X, position.Y) / SurfaceMeshFactory.DefaultTileWorldSize;
                }
            }

            arrays[(int)Mesh.ArrayType.TexUV] = uvs;
            rebuilt.AddSurfaceFromArrays(
                source.SurfaceGetPrimitiveType(surface),
                arrays,
                source.SurfaceGetBlendShapeArrays(surface),
                new Godot.Collections.Dictionary(),
                0);
            rebuilt.SurfaceSetName(surface, source.SurfaceGetName(surface));
            rebuilt.SurfaceSetMaterial(surface, source.SurfaceGetMaterial(surface));
        }

        if (rebuilt.GetSurfaceCount() > 0)
        {
            mesh.Mesh = rebuilt;
        }
    }

    internal static bool HasUvLayer(Mesh? mesh)
    {
        if (mesh is null || mesh.GetSurfaceCount() == 0)
        {
            return false;
        }

        for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++)
        {
            Godot.Collections.Array arrays = mesh.SurfaceGetArrays(surface);
            if (arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array().Length == 0)
            {
                return false;
            }
        }

        return true;
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
        string unsuffixedName = RemoveNumericDuplicateSuffix(importedName);
        Node3D[] candidates = EnumerateDescendants(root)
            .OfType<Node3D>()
            .Where(node =>
                node.Name.ToString().Equals(importedName, StringComparison.Ordinal) ||
                node.Name.ToString().Equals(dottedName, StringComparison.Ordinal) ||
                node.Name.ToString().Equals(unsuffixedName, StringComparison.Ordinal))
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

    private static string RemoveNumericDuplicateSuffix(string name)
    {
        int separator = Mathf.Max(name.LastIndexOf('.'), name.LastIndexOf('_'));
        if (separator <= 0 || separator == name.Length - 1)
        {
            return name;
        }

        return name[(separator + 1)..].All(char.IsDigit)
            ? name[..separator]
            : name;
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

    internal static bool IsVisibleThrough(Node3D target, Node3D room)
    {
        for (Node? current = target; current is not null; current = current.GetParent())
        {
            if (current is Node3D spatial && !spatial.Visible)
            {
                return false;
            }

            if (current == room)
            {
                break;
            }
        }

        return true;
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

    private static void ApplyImportedMeshCollisions(StaticBody3D body, IReadOnlyList<MeshInstance3D> meshes)
    {
        CollisionShape3D[] originalBoxCollisions = body.GetChildren()
            .OfType<CollisionShape3D>()
            .Where(collision => collision.Shape is BoxShape3D)
            .ToArray();

        for (int index = 0; index < meshes.Count; index++)
        {
            MeshInstance3D mesh = meshes[index];
            ConcavePolygonShape3D? importedShape = CreateImportedCollisionShape(mesh.Mesh);
            if (importedShape is null)
            {
                continue;
            }

            CollisionShape3D collision;
            if (index < originalBoxCollisions.Length)
            {
                collision = originalBoxCollisions[index];
            }
            else
            {
                collision = new CollisionShape3D { Name = $"BlenderReferenceCollision{index + 1}" };
                body.AddChild(collision);
            }

            // Keep the original CollisionShape3D instances where possible,
            // because doors and mechanisms retain references to them. Replace
            // every authored box, not only the first one, with the exact
            // Blender triangles that are visible for that same body.
            collision.Transform = body.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
            collision.Shape = importedShape;
            mesh.SetMeta(ReferenceCollisionPathMetadata, body.GetPathTo(collision));
        }

        // Additional boxes on gameplay mechanisms are intentionally kept.
        // The Blender reference exporter includes every wall/platform body,
        // but only the primary envelope of complex props such as cannons.
    }

    private static ConcavePolygonShape3D? CreateImportedCollisionShape(Mesh? mesh)
    {
        ConcavePolygonShape3D? shape = mesh?.CreateTrimeshShape();
        if (shape is not null)
        {
            // Imported materials are rendered double-sided, so the matching
            // collision surface must also be solid from either side.
            shape.BackfaceCollision = true;
        }

        return shape;
    }
}

internal partial class BlenderReferenceBinding : Node
{
    private readonly record struct Entry(
        MeshInstance3D Mesh,
        Node3D Target,
        Transform3D RelativeTransform);

    private readonly List<Entry> _entries = new();
    private readonly Node3D _room;

    public BlenderReferenceBinding(Node3D room)
    {
        _room = room;
    }

    public void Bind(MeshInstance3D mesh, Node3D target)
    {
        _entries.Add(new Entry(
            mesh,
            target,
            target.GlobalTransform.AffineInverse() * mesh.GlobalTransform));
    }

    public override void _Process(double delta)
    {
        foreach (Entry entry in _entries)
        {
            if (!GodotObject.IsInstanceValid(entry.Mesh) || !GodotObject.IsInstanceValid(entry.Target))
            {
                continue;
            }

            entry.Mesh.GlobalTransform = entry.Target.GlobalTransform * entry.RelativeTransform;
            entry.Mesh.Visible = BlenderRoomEdits.IsVisibleThrough(entry.Target, _room);
        }
    }
}
