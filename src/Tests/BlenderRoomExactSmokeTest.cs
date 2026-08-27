using Godot;
using System.Reflection;
using Velocitex.Core.Physics;
using Velocitex.Gameplay.Physics;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Tests;

public partial class BlenderRoomExactSmokeTest : Node
{
    private const int CanonicalRoom02DoorReferenceCount = 6;
    private const float Tolerance = 0.0005f;
    private static readonly Transform3D ClosedDoorBlockerLocal = new(
        Basis.Identity,
        new Vector3(0.0f, 2.19f, -0.03f));

    public override async void _Ready()
    {
        if (OS.GetCmdlineUserArgs().Contains("--negative-collision-control", StringComparer.Ordinal))
        {
            await RunNegativeCollisionControl();
            return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--negative-canonical-door-control", StringComparer.Ordinal))
        {
            await RunNegativeCanonicalDoorControl();
            return;
        }
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
            failures += await AuditReferenceMotion(room, roomNumber);
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

    private async Task RunNegativeCollisionControl()
    {
        PackedScene packed = GD.Load<PackedScene>("res://scenes/Room02.tscn");
        Node3D room = (Node3D)packed.Instantiate();
        AddChild(room);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        MeshInstance3D? importedMesh = room.GetNode<Node>("BlenderGeometry").GetChildren()
            .OfType<MeshInstance3D>()
            .FirstOrDefault(mesh =>
                mesh.HasMeta(BlenderRoomEdits.ReferenceTargetPathMetadata) &&
                mesh.HasMeta(BlenderRoomEdits.ReferenceCollisionPathMetadata) &&
                !mesh.HasMeta(BlenderRoomEdits.SeamSafeCollisionMetadata));
        StaticBody3D? target = importedMesh is null
            ? null
            : room.GetNodeOrNull<StaticBody3D>(
                importedMesh.GetMeta(BlenderRoomEdits.ReferenceTargetPathMetadata).AsNodePath());
        CollisionShape3D? collision = importedMesh is null || target is null
            ? null
            : target.GetNodeOrNull<CollisionShape3D>(
                importedMesh.GetMeta(BlenderRoomEdits.ReferenceCollisionPathMetadata).AsNodePath());
        if (collision is null)
        {
            GD.PushError("BLENDER_ROOM_EXACT_NEGATIVE_CONTROL_FAIL: could not find an imported Room 02 collision.");
            GetTree().Quit(1);
            return;
        }

        collision.Position += Vector3.Right * 0.25f;
        int detectedFailures = AuditRoom(room, 2);
        if (detectedFailures == 0)
        {
            GD.PushError("BLENDER_ROOM_EXACT_NEGATIVE_CONTROL_FAIL: a 0.25 m hitbox displacement was not detected.");
            GetTree().Quit(1);
            return;
        }

        GD.Print(
            $"BLENDER_ROOM_EXACT_NEGATIVE_CONTROL_PASS: detected {detectedFailures} failure(s) " +
            "after displacing one imported Room 02 hitbox by 0.25 m.");
        room.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(0);
    }

    private async Task RunNegativeCanonicalDoorControl()
    {
        PackedScene packed = GD.Load<PackedScene>("res://scenes/Room03.tscn");
        Node3D room = (Node3D)packed.Instantiate();
        AddChild(room);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        MeshInstance3D? canonicalPart = room.GetNode<Node>("BlenderGeometry").GetChildren()
            .OfType<MeshInstance3D>()
            .FirstOrDefault(mesh => mesh.HasMeta(BlenderRoomEdits.CanonicalDoorSourceRoomMetadata));
        if (canonicalPart is null)
        {
            GD.PushError("BLENDER_ROOM_EXACT_NEGATIVE_CONTROL_FAIL: Room 03 has no canonical Room 02 entrance part.");
            GetTree().Quit(1);
            return;
        }

        canonicalPart.GlobalPosition += Vector3.Right * 0.25f;
        int detectedFailures = AuditRoom(room, 3);
        if (detectedFailures == 0)
        {
            GD.PushError("BLENDER_ROOM_EXACT_NEGATIVE_CONTROL_FAIL: a 0.25 m canonical door displacement was not detected.");
            GetTree().Quit(1);
            return;
        }

        GD.Print(
            $"BLENDER_ROOM_CANONICAL_DOOR_NEGATIVE_CONTROL_PASS: detected {detectedFailures} failure(s) " +
            "after displacing one Room 03 canonical entrance part by 0.25 m.");
        room.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(0);
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
        int expectedRoomDoorReferenceCount = expected.Values.Count(mesh =>
            IsCanonicalDoorReference(mesh.Name.ToString()));
        int expectedRuntimeCount = roomNumber == 2
            ? expected.Count
            : expected.Count - expectedRoomDoorReferenceCount + CanonicalRoom02DoorReferenceCount;
        if (actual.Count != expectedRuntimeCount)
        {
            GD.PushError(
                $"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} wall count runtime={actual.Count}, " +
                $"expected={expectedRuntimeCount} (Blender={expected.Count}, replaced door refs={expectedRoomDoorReferenceCount}).");
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

            bool usesRoom02CanonicalDoor = actualMesh.HasMeta(BlenderRoomEdits.CanonicalDoorSourceRoomMetadata) &&
                actualMesh.GetMeta(BlenderRoomEdits.CanonicalDoorSourceRoomMetadata).AsInt32() == 2;
            float error = usesRoom02CanonicalDoor ? 0.0f : GeometryError(actualMesh, expectedMesh);
            maximumError = Mathf.Max(maximumError, error);
            bool isReference = name.StartsWith("REF_", StringComparison.Ordinal);
            MeshInstance3D collisionAuditMesh = actualMesh;
            if (actualMesh.HasMeta(BlenderRoomEdits.CanonicalDoorVisualOnlyMetadata))
            {
                collisionAuditMesh = blenderGeometry?
                    .GetNodeOrNull<Node>("LocalDoorCollisionSources")?
                    .GetChildren()
                    .OfType<MeshInstance3D>()
                    .FirstOrDefault(mesh => mesh.Name.ToString().Equals(name, StringComparison.Ordinal))!;
                if (collisionAuditMesh is null)
                {
                    GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} canonical visual {name} has no same-room collision source.");
                    failures++;
                    continue;
                }
            }
            int collisionFailures = isReference
                ? AuditReferenceCollision(room, collisionAuditMesh, roomNumber)
                : AuditWallCollision(room, actualMesh, roomNumber);
            failures += collisionFailures;
            bool hasCollision = collisionFailures == 0;

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
                !BlenderRoomEdits.HasUvLayer(actualMesh.Mesh) &&
                (!texturedMaterial.Uv1Triplanar || !texturedMaterial.Uv1WorldTriplanar))
            {
                GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} textured Blender mesh {name} has no UV-independent mapping.");
                failures++;
            }

            if (IsVisible(actualMesh) &&
                actualMesh.MaterialOverride is ShaderMaterial &&
                !BlenderRoomEdits.HasUvLayer(actualMesh.Mesh))
            {
                GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} special Blender surface {name} has no generated UV layer.");
                failures++;
            }

            if (IsVisible(actualMesh) &&
                actualMesh.MaterialOverride is ShaderMaterial specialMaterial &&
                (specialMaterial.Shader is null ||
                    !specialMaterial.Shader.Code.Contains("cull_disabled", StringComparison.Ordinal)))
            {
                GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} special Blender surface {name} still culls reversed faces.");
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
        failures += AuditDoorEntrance(room, actual, roomNumber);
        failures += AuditLighting(room, roomNumber);
        failures += AuditBrokenGlassBinding(room, actual, roomNumber);

        if (failures == 0)
        {
            GD.Print($"BLENDER_ROOM_EXACT_ROOM_PASS: Room {roomNumber:00} meshes={expected.Count}, maximum error={maximumError:F6}.");
        }

        importedRoot.Free();
        return failures;
    }

    private static int AuditBrokenGlassBinding(
        Node3D room,
        IReadOnlyDictionary<string, MeshInstance3D> importedMeshes,
        int roomNumber)
    {
        if (roomNumber != 6)
        {
            return 0;
        }

        ProfiledSurfaceBody? glass = EnumerateDescendants(room)
            .OfType<ProfiledSurfaceBody>()
            .FirstOrDefault(surface => surface.Profile.Kind == SurfaceKind.Frictionless);
        MeshInstance3D? importedGlass = glass is null
            ? null
            : importedMeshes.Values.FirstOrDefault(mesh =>
                mesh.HasMeta(BlenderRoomEdits.ReferenceTargetPathMetadata) &&
                room.GetNodeOrNull<Node3D>(mesh.GetMeta(BlenderRoomEdits.ReferenceTargetPathMetadata).AsNodePath()) == glass);
        Node? binding = room.GetNodeOrNull<Node>("BlenderReferenceBindings");
        MethodInfo? breakGlass = typeof(ProfiledSurfaceBody).GetMethod(
            "BreakGlass",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (glass is null || importedGlass is null || binding is null || breakGlass is null)
        {
            GD.PushError("BLENDER_ROOM_EXACT_FAIL: Room 06 could not locate a Blender-bound timed-glass surface for the break-visibility audit.");
            return 1;
        }

        breakGlass.Invoke(glass, null);
        binding._Process(0.0);
        bool hiddenWhenBroken = glass.IsBroken && !importedGlass.Visible;
        glass.RestoreGlass();
        binding._Process(0.0);
        bool restoredAfterReset = !glass.IsBroken && importedGlass.Visible;
        if (!hiddenWhenBroken || !restoredAfterReset)
        {
            GD.PushError(
                $"BLENDER_ROOM_EXACT_FAIL: Room 06 imported glass visibility did not follow break/reset state: " +
                $"hidden={hiddenWhenBroken}, restored={restoredAfterReset}.");
            return 1;
        }

        return 0;
    }

    private static int AuditDoorEntrance(
        Node3D room,
        IReadOnlyDictionary<string, MeshInstance3D> importedMeshes,
        int roomNumber)
    {
        MeshInstance3D[] authoredEntranceMeshes = importedMeshes
            .Where(pair =>
            {
                string target = ReferenceTargetName(pair.Key);
                return target.StartsWith("ExitCorridor", StringComparison.Ordinal) ||
                    target.StartsWith("ExitDoorBacking", StringComparison.Ordinal) ||
                    target.StartsWith("FrameCollision", StringComparison.Ordinal);
            })
            .Select(pair => pair.Value)
            .ToArray();
        if (authoredEntranceMeshes.Length == 0)
        {
            return 0;
        }

        int failures = 0;
        failures += AuditCanonicalRoom02DoorCopy(room, importedMeshes, roomNumber);
        foreach (MeshInstance3D authoredMesh in authoredEntranceMeshes)
        {
            if (!authoredMesh.HasMeta(BlenderRoomEdits.ReferenceTargetPathMetadata) ||
                !IsVisible(authoredMesh))
            {
                GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} authored entrance part {authoredMesh.Name} is not visibly bound in the game.");
                failures++;
            }
        }

        MeshInstance3D[] authoredFrameMeshes = authoredEntranceMeshes
            .Where(mesh => ReferenceTargetName(mesh.Name.ToString()).StartsWith("FrameCollision", StringComparison.Ordinal))
            .ToArray();
        if (authoredFrameMeshes.Length == 0)
        {
            return failures;
        }

        MeshInstance3D[] localDoorCollisionMeshes = room
            .GetNodeOrNull<Node>("BlenderGeometry/LocalDoorCollisionSources")?
            .GetChildren()
            .OfType<MeshInstance3D>()
            .Where(mesh => mesh.HasMeta(BlenderRoomEdits.LocalDoorCollisionSourceMetadata))
            .ToArray() ?? Array.Empty<MeshInstance3D>();
        MeshInstance3D[] expectedFrameCollisionMeshes = roomNumber == 2
            ? authoredFrameMeshes
            : localDoorCollisionMeshes
                .Where(mesh => CanonicalDoorTargetName(mesh.Name.ToString()) == "FrameCollision")
                .ToArray();
        if (expectedFrameCollisionMeshes.Length == 0)
        {
            expectedFrameCollisionMeshes = authoredFrameMeshes;
        }
        if (roomNumber != 2 && localDoorCollisionMeshes.Any(IsVisible))
        {
            GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} does not retain hidden same-room door collision sources.");
            failures++;
        }

        MeshInstance3D[] canonicalDoorMeshes = importedMeshes.Values
            .Where(mesh => IsCanonicalDoorReference(mesh.Name.ToString()))
            .ToArray();
        if (canonicalDoorMeshes.Length != CanonicalRoom02DoorReferenceCount ||
            canonicalDoorMeshes.Any(mesh =>
                !mesh.HasMeta(BlenderRoomEdits.CanonicalDoorSourceRoomMetadata) ||
                mesh.GetMeta(BlenderRoomEdits.CanonicalDoorSourceRoomMetadata).AsInt32() != 2))
        {
            GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} uses {canonicalDoorMeshes.Length}/{CanonicalRoom02DoorReferenceCount} exact Room 02 canonical blocker and corridor references.");
            failures++;
        }

        StaticBody3D? frameCollision = room.GetNodeOrNull<StaticBody3D>("ExitDoor/FrameCollision");
        if (frameCollision is null)
        {
            GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} has authored frame meshes but no runtime FrameCollision target.");
            return failures + 1;
        }

        string[] replacedPocketNames =
        {
            "LeftDoorPocketMask",
            "RightDoorPocketMask",
            "LeftFrame",
            "RightFrame",
        };
        foreach (MeshInstance3D legacyFrame in frameCollision.GetParent().GetChildren()
            .OfType<MeshInstance3D>()
            .Where(mesh => replacedPocketNames.Contains(mesh.Name.ToString(), StringComparer.Ordinal)))
        {
            if (IsVisible(legacyFrame))
            {
                GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} legacy pocket part {legacyFrame.Name} remains visible over its Blender-authored replacement.");
                failures++;
            }
        }

        if (frameCollision.GetParent().GetNodeOrNull<MeshInstance3D>("Header") is not null ||
            frameCollision.GetNodeOrNull<CollisionShape3D>("HeaderHitbox") is not null)
        {
            GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} still contains the removed upper door-frame mesh or hitbox.");
            failures++;
        }

        CollisionShape3D[] activeFrameCollisions = frameCollision.GetChildren()
            .OfType<CollisionShape3D>()
            .Where(collision => !collision.Disabled)
            .ToArray();
        CollisionShape3D[] authoredPocketCollisions = activeFrameCollisions
            .Where(collision => collision.Name.ToString() is "LeftPocketHitbox" or "RightPocketHitbox")
            .ToArray();
        if (authoredPocketCollisions.Length != expectedFrameCollisionMeshes.Length ||
            activeFrameCollisions.Length != expectedFrameCollisionMeshes.Length)
        {
            GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} door collision is not an exact replacement of that room's Blender FrameCollision meshes: active={activeFrameCollisions.Length}, local={authoredPocketCollisions.Length}/{expectedFrameCollisionMeshes.Length}.");
            failures++;
        }

        return failures;
    }

    private static int AuditCanonicalRoom02DoorCopy(
        Node3D room,
        IReadOnlyDictionary<string, MeshInstance3D> importedMeshes,
        int roomNumber)
    {
        Node3D? door = room.GetNodeOrNull<Node3D>("ExitDoor");
        PackedScene? sourceScene = GD.Load<PackedScene>(
            "res://assets/models/EditableWallsBlender/Room02Walls.blend");
        if (door is null || sourceScene?.Instantiate() is not Node3D sourceRoot)
        {
            GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} cannot load the canonical Room 02 entrance source.");
            return 1;
        }

        MeshInstance3D? sourceBlocker = sourceRoot.GetChildren()
            .OfType<MeshInstance3D>()
            .FirstOrDefault(mesh => CanonicalDoorTargetName(mesh.Name.ToString()) == "ClosedDoorBlocker");
        if (sourceBlocker is null)
        {
            sourceRoot.Free();
            GD.PushError("BLENDER_ROOM_EXACT_FAIL: Room 02 canonical source has no ClosedDoorBlocker.");
            return 1;
        }
        Transform3D sourceDoorSpace = sourceBlocker.Transform.Orthonormalized() *
            ClosedDoorBlockerLocal.AffineInverse();
        Dictionary<string, MeshInstance3D[]> expectedByTarget = sourceRoot.GetChildren()
            .OfType<MeshInstance3D>()
            .Where(mesh => IsCanonicalDoorReference(mesh.Name.ToString()))
            .GroupBy(mesh => CanonicalDoorTargetName(mesh.Name.ToString()), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(mesh => mesh.Transform.Origin.X).ToArray(),
                StringComparer.Ordinal);
        Dictionary<string, MeshInstance3D[]> actualByTarget = importedMeshes.Values
            .Where(mesh => IsCanonicalDoorReference(mesh.Name.ToString()))
            .GroupBy(mesh => CanonicalDoorTargetName(mesh.Name.ToString()), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(mesh => door.ToLocal(mesh.GlobalPosition).X).ToArray(),
                StringComparer.Ordinal);

        int failures = 0;
        foreach ((string target, MeshInstance3D[] expectedParts) in expectedByTarget)
        {
            if (!actualByTarget.TryGetValue(target, out MeshInstance3D[]? actualParts) ||
                actualParts.Length != expectedParts.Length)
            {
                GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} canonical target {target} count differs from Room 02.");
                failures++;
                continue;
            }

            for (int index = 0; index < expectedParts.Length; index++)
            {
                Transform3D expectedRelative = sourceDoorSpace.AffineInverse() * expectedParts[index].Transform;
                Transform3D actualRelative = door.GlobalTransform.AffineInverse() * actualParts[index].GlobalTransform;
                Aabb expectedMeshBounds = expectedParts[index].Mesh?.GetAabb() ?? default;
                Aabb actualMeshBounds = actualParts[index].Mesh?.GetAabb() ?? default;
                float error = TransformedMeshError(
                    actualParts[index],
                    actualRelative,
                    expectedParts[index],
                    expectedRelative);
                if (error > Tolerance)
                {
                    GD.PushError(
                        $"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} canonical target {target}[{index}] " +
                        $"differs numerically from Room 02 by {error:F6}; " +
                        $"expectedTransform={expectedRelative}, actualTransform={actualRelative}, " +
                        $"expectedBounds={expectedMeshBounds}, actualBounds={actualMeshBounds}.");
                    failures++;
                }
            }
        }

        sourceRoot.Free();
        return failures;
    }

    private static int AuditWallCollision(Node3D room, MeshInstance3D importedMesh, int roomNumber)
    {
        string name = importedMesh.Name.ToString();
        StaticBody3D? body = room.GetNodeOrNull<Node>("BlenderCollisions")?.GetChildren()
            .OfType<StaticBody3D>()
            .FirstOrDefault(candidate => candidate.Name.ToString().Equals($"{name}_Collision", StringComparison.Ordinal));
        CollisionShape3D? collision = body?.GetChildren().OfType<CollisionShape3D>().FirstOrDefault();
        if (collision?.Shape is not ConcavePolygonShape3D actualShape || importedMesh.Mesh is null)
        {
            GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} wall {name} does not use an imported trimesh hitbox.");
            return 1;
        }

        ConcavePolygonShape3D? expectedShape = importedMesh.Mesh.CreateTrimeshShape();
        Transform3D expectedTransform = body!.GlobalTransform.AffineInverse() * importedMesh.GlobalTransform;
        float transformError = TransformError(collision.Transform, expectedTransform);
        float meshError = ConcaveShapeError(actualShape, expectedShape);
        bool doubleSided = actualShape.BackfaceCollision;
        if (!doubleSided || transformError > Tolerance || meshError > Tolerance)
        {
            GD.PushError(
                $"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} wall {name} hitbox differs from its rendered mesh; transform={transformError:F6}, triangles={meshError:F6}, double_sided={doubleSided}.");
            return 1;
        }

        return 0;
    }

    private static int AuditReferenceCollision(Node3D room, MeshInstance3D importedMesh, int roomNumber)
    {
        string name = importedMesh.Name.ToString();
        if (!importedMesh.HasMeta(BlenderRoomEdits.ReferenceTargetPathMetadata))
        {
            // Unmatched references are intentionally hidden and do not replace
            // a runtime object or its collision.
            return IsVisible(importedMesh) ? 1 : 0;
        }

        NodePath targetPath = importedMesh.GetMeta(BlenderRoomEdits.ReferenceTargetPathMetadata).AsNodePath();
        if (room.GetNodeOrNull<StaticBody3D>(targetPath) is not StaticBody3D targetBody)
        {
            GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} reference {name} target {targetPath} is not a StaticBody3D.");
            return 1;
        }

        if (!importedMesh.HasMeta(BlenderRoomEdits.ReferenceCollisionPathMetadata))
        {
            GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} reference {name} has no exact collision binding.");
            return 1;
        }

        NodePath collisionPath = importedMesh.GetMeta(BlenderRoomEdits.ReferenceCollisionPathMetadata).AsNodePath();
        CollisionShape3D? collision = targetBody.GetNodeOrNull<CollisionShape3D>(collisionPath);
        if (collision?.Shape is null || importedMesh.Mesh is null)
        {
            GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} reference {name} has no imported hitbox.");
            return 1;
        }

        if (importedMesh.HasMeta(BlenderRoomEdits.SeamSafeCollisionMetadata) &&
            importedMesh.GetMeta(BlenderRoomEdits.SeamSafeCollisionMetadata).AsBool())
        {
            if (collision.Shape is BoxShape3D && !collision.Disabled)
            {
                return 0;
            }

            GD.PushError(
                $"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} seam-safe surface {name} " +
                "does not retain one active box hitbox.");
            return 1;
        }

        Transform3D importedRelative = targetBody.GlobalTransform.AffineInverse() * importedMesh.GlobalTransform;
        if (collision.Shape is BoxShape3D actualBox)
        {
            bool expectedIsBox = BlenderRoomEdits.TryCreateExactBoxCollision(
                importedMesh,
                importedRelative,
                out BoxShape3D? expectedBox,
                out Transform3D expectedBoxTransform);
            float transformError = TransformError(collision.Transform, expectedBoxTransform);
            float sizeError = expectedBox is null
                ? float.PositiveInfinity
                : actualBox.Size.DistanceTo(expectedBox.Size);
            if (!expectedIsBox || transformError > Tolerance || sizeError > Tolerance)
            {
                GD.PushError(
                    $"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} cuboid reference {name} hitbox differs from its Blender box; transform={transformError:F6}, size={sizeError:F6}.");
                return 1;
            }
            return 0;
        }

        if (collision.Shape is ConcavePolygonShape3D actualShape)
        {
            ConcavePolygonShape3D? expectedShape = importedMesh.Mesh.CreateTrimeshShape();
            float transformError = TransformError(collision.Transform, importedRelative);
            float meshError = ConcaveShapeError(actualShape, expectedShape);
            bool doubleSided = actualShape.BackfaceCollision;
            if (doubleSided && transformError <= Tolerance && meshError <= Tolerance)
            {
                return 0;
            }
            GD.PushError(
                $"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} non-box reference {name} hitbox differs from its Blender triangles; transform={transformError:F6}, triangles={meshError:F6}, double_sided={doubleSided}.");
            return 1;
        }

        GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} reference {name} uses unsupported collision shape {collision.Shape.GetType().Name}.");
        return 1;
    }

    private async Task<int> AuditReferenceMotion(Node3D room, int roomNumber)
    {
        MeshInstance3D? importedMesh = room.GetNodeOrNull<Node>("BlenderGeometry")?.GetChildren()
            .OfType<MeshInstance3D>()
            .FirstOrDefault(mesh =>
                mesh.Name.ToString().StartsWith("REF_", StringComparison.Ordinal) &&
                mesh.HasMeta(BlenderRoomEdits.ReferenceTargetPathMetadata));
        if (importedMesh is null)
        {
            return 0;
        }

        NodePath targetPath = importedMesh.GetMeta(BlenderRoomEdits.ReferenceTargetPathMetadata).AsNodePath();
        Node3D? target = room.GetNodeOrNull<Node3D>(targetPath);
        if (target is null)
        {
            GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} reference binding target {targetPath} is missing.");
            return 1;
        }

        Vector3 offset = new(0.017f, 0.013f, -0.011f);
        Vector3 targetStart = target.GlobalPosition;
        Vector3 meshStart = importedMesh.GlobalPosition;
        target.GlobalPosition += offset;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Vector3 observedOffset = importedMesh.GlobalPosition - meshStart;
        target.GlobalPosition = targetStart;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (observedOffset.DistanceTo(offset) > Tolerance)
        {
            GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} Blender reference did not follow its runtime target; expected={offset}, actual={observedOffset}.");
            return 1;
        }

        return 0;
    }

    private static int AuditLighting(Node3D room, int roomNumber)
    {
        WorldEnvironment? world = room.GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
        if (world?.Environment is null ||
            world.Environment.AmbientLightEnergy < 2.25f - Tolerance ||
            world.Environment.TonemapExposure < 1.5f - Tolerance)
        {
            GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} is below the Room 01 lighting baseline.");
            return 1;
        }

        return 0;
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

        foreach (MeshInstance3D cornerJoin in legacyVisuals.Where(mesh =>
            mesh.Name.ToString().EndsWith("CornerJoin", StringComparison.Ordinal)))
        {
            if (IsVisible(cornerJoin))
            {
                GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} legacy corner strip {cornerJoin.GetPath()} is still visible.");
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
            Node3D? target = importedMesh.HasMeta(BlenderRoomEdits.ReferenceTargetPathMetadata)
                ? room.GetNodeOrNull<Node3D>(importedMesh.GetMeta(BlenderRoomEdits.ReferenceTargetPathMetadata).AsNodePath())
                : EnumerateDescendants(room)
                    .OfType<Node3D>()
                    .Where(node => !IsDescendantOf(node, blenderGeometry))
                    .FirstOrDefault(node => NamesMatch(node.Name.ToString(), targetName));
            MeshInstance3D[] targetVisuals = target is null
                ? Array.Empty<MeshInstance3D>()
                : EnumerateDescendants(target).Prepend(target).OfType<MeshInstance3D>().ToArray();
            bool hasReusableMaterial = targetVisuals.Any(visual =>
                visual.MaterialOverride is not null ||
                (visual.Mesh?.GetSurfaceCount() > 0 && visual.GetActiveMaterial(0) is not null)) ||
                (target?.Name.ToString().Equals("FrameCollision", StringComparison.Ordinal) == true &&
                    importedMesh.MaterialOverride is not null);

            if (target is null || !hasReusableMaterial)
            {
                if (IsVisible(importedMesh))
                {
                    GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} unmatched reference {name} is visible.");
                    failures++;
                }

                continue;
            }

            if (!importedMesh.HasMeta(BlenderRoomEdits.ReferenceTargetPathMetadata))
            {
                GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} matched reference {name} is not bound to its runtime target.");
                failures++;
            }

            if (!BlenderRoomEdits.IsVisibleThrough(target, room) && IsVisible(importedMesh))
            {
                GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} hidden doorway wall reference {name} covers its runtime opening.");
                failures++;
            }

            MeshInstance3D? originalVisual = targetVisuals.FirstOrDefault();
            if (originalVisual is not null &&
                importedMesh.CastShadow != originalVisual.CastShadow &&
                !importedMesh.HasMeta(BlenderRoomEdits.JoinShadowSuppressedMetadata))
            {
                GD.PushError($"BLENDER_ROOM_EXACT_FAIL: Room {roomNumber:00} reference {name} changed the target's shadow policy.");
                failures++;
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

    private static bool IsCanonicalDoorReference(string name)
    {
        string target = CanonicalDoorTargetName(name);
        return target is "ClosedDoorBlocker" or
            "ExitCorridorFloor" or "ExitCorridorCeiling" or
            "ExitCorridorLeftWall" or "ExitCorridorRightWall" or
            "ExitCorridorEndWall";
    }

    private static string CanonicalDoorTargetName(string name)
    {
        string target = ReferenceTargetName(name);
        int separator = Mathf.Max(target.LastIndexOf('.'), target.LastIndexOf('_'));
        if (separator > 0 && separator < target.Length - 1 &&
            target[(separator + 1)..].All(char.IsDigit))
        {
            target = target[..separator];
        }

        return target;
    }

    private static bool NamesMatch(string actual, string imported) =>
        actual.Equals(imported, StringComparison.Ordinal) ||
        actual.Equals(imported.Replace("_001", ".001", StringComparison.Ordinal), StringComparison.Ordinal);

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

    private static float TransformedMeshError(
        MeshInstance3D actual,
        Transform3D actualTransform,
        MeshInstance3D expected,
        Transform3D expectedTransform)
    {
        Vector3[] actualFaces = actual.Mesh?.GetFaces() ?? Array.Empty<Vector3>();
        Vector3[] expectedFaces = expected.Mesh?.GetFaces() ?? Array.Empty<Vector3>();
        if (actualFaces.Length != expectedFaces.Length || actualFaces.Length == 0)
        {
            return float.PositiveInfinity;
        }

        float maximum = 0.0f;
        for (int index = 0; index < actualFaces.Length; index++)
        {
            maximum = Mathf.Max(
                maximum,
                (actualTransform * actualFaces[index]).DistanceTo(
                    expectedTransform * expectedFaces[index]));
        }
        return maximum;
    }

    private static float TransformError(Transform3D actual, Transform3D expected) =>
        Mathf.Max(
            actual.Origin.DistanceTo(expected.Origin),
            Mathf.Max(
                actual.Basis.X.DistanceTo(expected.Basis.X),
                Mathf.Max(
                    actual.Basis.Y.DistanceTo(expected.Basis.Y),
                    actual.Basis.Z.DistanceTo(expected.Basis.Z))));

    private static float ConcaveShapeError(
        ConcavePolygonShape3D actual,
        ConcavePolygonShape3D? expected)
    {
        if (expected is null)
        {
            return float.PositiveInfinity;
        }

        Vector3[] actualFaces = actual.Data;
        Vector3[] expectedFaces = expected.Data;
        if (actualFaces.Length != expectedFaces.Length)
        {
            return float.PositiveInfinity;
        }

        float maximumError = 0.0f;
        for (int index = 0; index < actualFaces.Length; index++)
        {
            maximumError = Mathf.Max(maximumError, actualFaces[index].DistanceTo(expectedFaces[index]));
        }

        return maximumError;
    }
}
