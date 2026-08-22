using Godot;
using System.Text.Json;
using Velocitex.Gameplay.Visual;

namespace Velocitex.Gameplay.Rooms;

internal static class BlenderRoomEdits
{
    private static readonly string[] DoorPocketVisualNames =
    {
        "LeftDoorPocketMask",
        "RightDoorPocketMask",
    };

    private static readonly string[] CanonicalDoorReferenceTargets =
    {
        "FrameCollision",
        "ClosedDoorBlocker",
    };

    private static readonly Transform3D Room02CanonicalDoorSpace = new(
        Basis.Identity,
        new Vector3(0.0f, 5.63f, -37.75f));

    internal const string ReferenceTargetPathMetadata = "blender_reference_target_path";
    internal const string ReferenceCollisionPathMetadata = "blender_reference_collision_path";
    internal const string DeletedByBlenderMetadata = "blender_reference_deleted";
    internal const string JoinShadowSuppressedMetadata = "blender_join_shadow_suppressed";
    internal const string CanonicalDoorSourceRoomMetadata = "blender_canonical_door_source_room";
    internal const string SeamSafeCollisionMetadata = "blender_seam_safe_collision";

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
        importedMeshes = ApplyCanonicalDoorGeometry(room, roomNumber, importedRoot, importedMeshes);

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

            ApplyAuthoredDoorVisualBounds(target, importedMesh);

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
        if (room.GetNodeOrNull<ExitDoor3D>("ExitDoor") is ExitDoor3D exitDoor &&
            RoomGeometry.CloseExitCorridorCollisionSeam(room, exitDoor))
        {
            foreach (MeshInstance3D floorReference in importedMeshes.Where(mesh =>
                IsReferenceMesh(mesh) &&
                RemoveNumericDuplicateSuffix(ReferenceTargetName(mesh)) == "ExitCorridorFloor"))
            {
                floorReference.SetMeta(SeamSafeCollisionMetadata, true);
            }
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

    private static MeshInstance3D[] ApplyCanonicalDoorGeometry(
        Node3D room,
        int roomNumber,
        Node3D importedRoot,
        IReadOnlyCollection<MeshInstance3D> roomMeshes)
    {
        ExitDoor3D? door = room.GetNodeOrNull<ExitDoor3D>("ExitDoor");
        if (door is null)
        {
            return roomMeshes.ToArray();
        }

        MeshInstance3D[] existingDoorReferences = roomMeshes
            .Where(mesh => IsReferenceMesh(mesh) &&
                IsCanonicalDoorReference(mesh))
            .ToArray();
        if (roomNumber == 2)
        {
            foreach (MeshInstance3D reference in existingDoorReferences)
            {
                reference.SetMeta(CanonicalDoorSourceRoomMetadata, 2);
            }
            return roomMeshes.ToArray();
        }

        PackedScene? canonicalScene = GD.Load<PackedScene>(
            "res://assets/models/EditableWallsBlender/Room02Walls.blend");
        if (canonicalScene?.Instantiate() is not Node3D canonicalRoot)
        {
            GD.PushError("BLENDER_ROOM_EDIT_FAIL: unable to load the Room 02 canonical door geometry");
            return roomMeshes.ToArray();
        }

        MeshInstance3D[] canonicalReferences = canonicalRoot.GetChildren()
            .OfType<MeshInstance3D>()
            .Where(mesh => IsReferenceMesh(mesh) &&
                IsCanonicalDoorReference(mesh))
            .ToArray();
        Dictionary<string, Queue<string>> namesByTarget = existingDoorReferences
            .GroupBy(CanonicalDoorTargetName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new Queue<string>(group
                    .OrderBy(mesh => mesh.Transform.Origin.X)
                    .Select(mesh => mesh.Name.ToString())),
                StringComparer.Ordinal);
        foreach (MeshInstance3D oldReference in existingDoorReferences)
        {
            importedRoot.RemoveChild(oldReference);
            oldReference.Free();
        }

        foreach (MeshInstance3D canonicalReference in canonicalReferences
            .OrderBy(CanonicalDoorTargetName, StringComparer.Ordinal)
            .ThenBy(mesh => mesh.Transform.Origin.X))
        {
            string targetName = CanonicalDoorTargetName(canonicalReference);
            string cloneName = namesByTarget.TryGetValue(targetName, out Queue<string>? availableNames) &&
                availableNames.Count > 0
                ? availableNames.Dequeue()
                : canonicalReference.Name.ToString();
            MeshInstance3D clone = new()
            {
                Name = cloneName,
                Mesh = canonicalReference.Mesh,
                CastShadow = canonicalReference.CastShadow,
                Layers = canonicalReference.Layers,
                Visible = canonicalReference.Visible,
            };
            importedRoot.AddChild(clone);
            Transform3D canonicalRelative = Room02CanonicalDoorSpace.AffineInverse() *
                canonicalReference.Transform;
            clone.GlobalTransform = door.GlobalTransform * canonicalRelative;
            clone.SetMeta(CanonicalDoorSourceRoomMetadata, 2);
        }

        canonicalRoot.Free();
        return importedRoot.GetChildren().OfType<MeshInstance3D>().ToArray();
    }

    private static bool IsCanonicalDoorReference(MeshInstance3D mesh) =>
        CanonicalDoorReferenceTargets.Contains(CanonicalDoorTargetName(mesh), StringComparer.Ordinal);

    private static string CanonicalDoorTargetName(MeshInstance3D mesh) =>
        RemoveNumericDuplicateSuffix(ReferenceTargetName(mesh));

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

        Aabb importedBounds = GlobalBounds(importedMesh);
        return parent.GetChildren()
            .OfType<MeshInstance3D>()
            .Where(visual => DoorPocketVisualNames.Contains(visual.Name.ToString(), StringComparer.Ordinal) && !claimed.Contains(visual))
            .OrderBy(visual => BoundsError(importedBounds, GlobalBounds(visual)))
            .FirstOrDefault();
    }

    private static bool IsDoorFrameTarget(Node3D target) =>
        target.Name.ToString().Equals("FrameCollision", StringComparison.Ordinal);

    private static void ApplyAuthoredDoorVisualBounds(Node3D target, MeshInstance3D importedMesh)
    {
        if (target.GetParentOrNull<ExitDoor3D>() is not ExitDoor3D door)
        {
            return;
        }

        if (target.Name.ToString().Equals("ClosedDoorBlocker", StringComparison.Ordinal))
        {
            door.ApplyAuthoredClosedDoorBounds(BoundsRelativeTo(door, importedMesh));
        }
        else if (target.Name.ToString().Equals("FrameCollision", StringComparison.Ordinal))
        {
            door.ApplyAuthoredDoorPocketBounds(BoundsRelativeTo(door, importedMesh));
            door.GetNodeOrNull<MeshInstance3D>("LeftFrame")?.Hide();
            door.GetNodeOrNull<MeshInstance3D>("RightFrame")?.Hide();
        }
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
        => BoundsWithTransform(mesh, mesh.GlobalTransform);

    private static Aabb BoundsRelativeTo(Node3D space, MeshInstance3D mesh)
        => BoundsWithTransform(mesh, space.GlobalTransform.AffineInverse() * mesh.GlobalTransform);

    private static Aabb BoundsWithTransform(MeshInstance3D mesh, Transform3D transform)
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
        Vector3 first = transform * corners[0];
        Vector3 minimum = first;
        Vector3 maximum = first;
        foreach (Vector3 corner in corners.Skip(1))
        {
            Vector3 transformed = transform * corner;
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
            Transform3D relativeTransform = body.GlobalTransform.AffineInverse() * mesh.GlobalTransform;
            Shape3D? importedShape;
            Transform3D collisionTransform;
            if (TryCreateExactBoxCollision(mesh, relativeTransform, out BoxShape3D? boxShape, out Transform3D boxTransform))
            {
                importedShape = boxShape;
                collisionTransform = boxTransform;
            }
            else
            {
                importedShape = CreateImportedCollisionShape(mesh.Mesh);
                collisionTransform = relativeTransform;
            }
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

            if (importedShape is BoxShape3D &&
                index < originalBoxCollisions.Length &&
                IsConnectedRollingSlope(originalBoxCollisions[index]))
            {
                // Non-uniformly scaled Blender cuboids can leave a raised edge
                // at a ramp end. Retain the original single-piece gameplay box
                // for rolling slopes; flat boxes and non-box edits still use
                // their imported geometry.
                collision = originalBoxCollisions[index];
                mesh.SetMeta(SeamSafeCollisionMetadata, true);
            }
            else
            {
                collision.Transform = collisionTransform;
                collision.Shape = importedShape;
            }

            // Cuboid platforms keep one continuous box collision, avoiding
            // internal triangle edges and join impulses. Edited non-box meshes
            // retain their exact double-sided Blender triangle collision.
            mesh.SetMeta(ReferenceCollisionPathMetadata, body.GetPathTo(collision));
        }

        // Additional boxes on gameplay mechanisms are intentionally kept.
        // The Blender reference exporter includes every wall/platform body,
        // but only the primary envelope of complex props such as cannons.
    }

    private static bool IsConnectedRollingSlope(CollisionShape3D collision)
    {
        if (collision.Shape is not BoxShape3D box)
        {
            return false;
        }

        float upAlignment = Mathf.Abs(collision.GlobalBasis.Y.Normalized().Dot(Vector3.Up));
        bool thinRollingSurface = box.Size.Y <= Mathf.Min(box.Size.X, box.Size.Z) * 0.25f;
        return thinRollingSurface && upAlignment is > 0.45f and < 0.995f;
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

    internal static bool TryCreateExactBoxCollision(
        MeshInstance3D mesh,
        Transform3D relativeTransform,
        out BoxShape3D? shape,
        out Transform3D collisionTransform)
    {
        shape = null;
        collisionTransform = relativeTransform;
        if (mesh.Mesh is not Mesh source)
        {
            return false;
        }

        Aabb bounds = source.GetAabb();
        Vector3[] vertices = source.GetFaces();
        if (vertices.Length == 0 || bounds.Size.X <= 0.0001f ||
            bounds.Size.Y <= 0.0001f || bounds.Size.Z <= 0.0001f)
        {
            return false;
        }

        HashSet<int> corners = new();
        const float tolerance = 0.0005f;
        foreach (Vector3 vertex in vertices)
        {
            int mask = 0;
            foreach ((float coordinate, float minimum, float maximum, int bit) in new[]
            {
                (vertex.X, bounds.Position.X, bounds.End.X, 1),
                (vertex.Y, bounds.Position.Y, bounds.End.Y, 2),
                (vertex.Z, bounds.Position.Z, bounds.End.Z, 4),
            })
            {
                if (Mathf.Abs(coordinate - minimum) <= tolerance)
                {
                    continue;
                }
                if (Mathf.Abs(coordinate - maximum) <= tolerance)
                {
                    mask |= bit;
                    continue;
                }
                return false;
            }
            corners.Add(mask);
        }
        if (corners.Count != 8)
        {
            return false;
        }

        Vector3 scale = relativeTransform.Basis.Scale.Abs();
        if (scale.X <= 0.0001f || scale.Y <= 0.0001f || scale.Z <= 0.0001f)
        {
            return false;
        }
        Basis orientation = new(
            relativeTransform.Basis.X / scale.X,
            relativeTransform.Basis.Y / scale.Y,
            relativeTransform.Basis.Z / scale.Z);
        if (Mathf.Abs(orientation.X.Dot(orientation.Y)) > tolerance ||
            Mathf.Abs(orientation.X.Dot(orientation.Z)) > tolerance ||
            Mathf.Abs(orientation.Y.Dot(orientation.Z)) > tolerance)
        {
            return false;
        }

        shape = new BoxShape3D { Size = bounds.Size * scale };
        collisionTransform = new Transform3D(orientation, relativeTransform * bounds.GetCenter());
        return true;
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
