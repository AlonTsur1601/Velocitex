using Godot;
using Velocitex.Gameplay.Physics;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Tests;

public partial class RoomShellContainmentSmokeTest : Node
{
    private const float BoundaryTolerance = 0.02f;
    private const float StructuralJoinTolerance = 0.20f;

    private readonly record struct Bounds(Vector3 Minimum, Vector3 Maximum)
    {
        public Vector3 Size => Maximum - Minimum;
    }

    private readonly record struct AuditResult(int IssueCount, int MeshCount, int CollisionCount, int MovingEndpointCount);

    public override async void _Ready()
    {
        int[] rooms = ResolveRequestedRooms();
        int totalIssues = 0;
        foreach (int room in rooms)
        {
            string scenePath = room == 1
                ? "res://scenes/MovementTestRoom.tscn"
                : $"res://scenes/Room{room:00}.tscn";
            PackedScene? packed = GD.Load<PackedScene>(scenePath);
            if (packed is null)
            {
                GD.PushError($"ROOM_SHELL_CONTAINMENT_FAIL: Room {room:00} scene is missing.");
                totalIssues++;
                continue;
            }

            Node roomRoot = packed.Instantiate();
            AddChild(roomRoot);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            roomRoot.ProcessMode = ProcessModeEnum.Disabled;

            AuditResult result = AuditRoom(room, roomRoot);
            totalIssues += result.IssueCount;
            if (result.IssueCount == 0)
            {
                GD.Print(
                    $"ROOM_SHELL_CONTAINMENT_ROOM_PASS: Room {room:00} kept {result.MeshCount} visible meshes, " +
                    $"{result.CollisionCount} relevant collision shapes and {result.MovingEndpointCount} moving-platform endpoints inside the shell.");
            }

            roomRoot.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (totalIssues > 0)
        {
            GD.PushError($"ROOM_SHELL_CONTAINMENT_FAIL: found {totalIssues} object(s) crossing a room wall, floor or ceiling.");
            await FinishAsync(1);
            return;
        }

        GD.Print($"ROOM_SHELL_CONTAINMENT_PASS: all relevant geometry in {rooms.Length} requested room(s) stays inside the shell.");
        await FinishAsync(0);
    }

    private static AuditResult AuditRoom(int room, Node roomRoot)
    {
        Node3D? shell = roomRoot.GetNodeOrNull<Node3D>("RoomShell");
        if (shell is null || !TryResolveShellBounds(shell, out Bounds interior, out Bounds structuralEnvelope))
        {
            GD.PushError($"ROOM_SHELL_CONTAINMENT_FAIL: Room {room:00} has no measurable RoomShell.");
            return new AuditResult(1, 0, 0, 0);
        }

        Area3D? goalTrigger = roomRoot.GetNodeOrNull<Area3D>("GoalCup");
        int issues = 0;
        int meshCount = 0;
        int collisionCount = 0;

        foreach (Node node in EnumerateDescendants(roomRoot))
        {
            if (IsLegalShellExternal(node, roomRoot, shell, goalTrigger))
            {
                continue;
            }

            if (node is MeshInstance3D mesh && mesh.Mesh is not null && mesh.IsVisibleInTree())
            {
                meshCount++;
                Bounds bounds = TransformBounds(mesh.Mesh.GetAabb(), mesh.GlobalTransform, shell);
                issues += ReportIfOutside(room, roomRoot, "mesh", mesh, bounds, interior, structuralEnvelope, shell, null);
            }
            else if (node is CollisionShape3D collision && collision.Shape is not null && collision.IsVisibleInTree())
            {
                if (!TryGetShapeBounds(collision.Shape, out Aabb localBounds))
                {
                    issues++;
                    GD.PushError(
                        $"ROOM_SHELL_CONTAINMENT_UNSUPPORTED: Room {room:00} collision {roomRoot.GetPathTo(collision)} " +
                        $"uses unsupported shape {collision.Shape.GetType().Name}.");
                    continue;
                }

                collisionCount++;
                Bounds bounds = TransformBounds(localBounds, collision.GlobalTransform, shell);
                issues += ReportIfOutside(room, roomRoot, "collision", collision, bounds, interior, structuralEnvelope, shell, null);
            }
        }

        MovingPlatform3D[] movingPlatforms = EnumerateDescendants(roomRoot).OfType<MovingPlatform3D>().ToArray();
        foreach (MovingPlatform3D platform in movingPlatforms)
        {
            Vector3 originalPosition = platform.Position;
            platform.Position = originalPosition + platform.EndOffset;
            foreach (Node node in EnumerateSelfAndDescendants(platform))
            {
                if (node is MeshInstance3D mesh && mesh.Mesh is not null && mesh.IsVisibleInTree())
                {
                    Bounds bounds = TransformBounds(mesh.Mesh.GetAabb(), mesh.GlobalTransform, shell);
                    issues += ReportIfOutside(room, roomRoot, "mesh", mesh, bounds, interior, structuralEnvelope, shell, "moving endpoint");
                }
                else if (node is CollisionShape3D collision && collision.Shape is not null && collision.IsVisibleInTree())
                {
                    if (!TryGetShapeBounds(collision.Shape, out Aabb localBounds))
                    {
                        issues++;
                        GD.PushError(
                            $"ROOM_SHELL_CONTAINMENT_UNSUPPORTED: Room {room:00} endpoint collision {roomRoot.GetPathTo(collision)} " +
                            $"uses unsupported shape {collision.Shape.GetType().Name}.");
                        continue;
                    }

                    Bounds bounds = TransformBounds(localBounds, collision.GlobalTransform, shell);
                    issues += ReportIfOutside(room, roomRoot, "collision", collision, bounds, interior, structuralEnvelope, shell, "moving endpoint");
                }
            }
            platform.Position = originalPosition;
        }

        return new AuditResult(issues, meshCount, collisionCount, movingPlatforms.Length);
    }

    private static int ReportIfOutside(
        int room,
        Node roomRoot,
        string kind,
        Node node,
        Bounds bounds,
        Bounds interior,
        Bounds structuralEnvelope,
        Node3D shell,
        string? state)
    {
        Vector3 excess = new(
            Mathf.Max(Mathf.Max(interior.Minimum.X - bounds.Minimum.X, bounds.Maximum.X - interior.Maximum.X), 0.0f),
            Mathf.Max(Mathf.Max(interior.Minimum.Y - bounds.Minimum.Y, bounds.Maximum.Y - interior.Maximum.Y), 0.0f),
            Mathf.Max(Mathf.Max(interior.Minimum.Z - bounds.Minimum.Z, bounds.Maximum.Z - interior.Maximum.Z), 0.0f));
        if (excess.X <= BoundaryTolerance && excess.Y <= BoundaryTolerance && excess.Z <= BoundaryTolerance)
        {
            return 0;
        }

        if (IsLegalStructuralJoin(node, bounds, interior, structuralEnvelope, shell))
        {
            return 0;
        }

        if (IsBlenderStructuralGeometry(node) && Contains(structuralEnvelope, bounds, StructuralJoinTolerance))
        {
            return 0;
        }

        string stateSuffix = state is null ? string.Empty : $" at its {state}";
        GD.PushError(
            $"ROOM_SHELL_CONTAINMENT_OBJECT: Room {room:00} {kind} {roomRoot.GetPathTo(node)}{stateSuffix} " +
            $"crosses the shell by ({excess.X:F3}, {excess.Y:F3}, {excess.Z:F3}) m; " +
            $"object=[{bounds.Minimum}..{bounds.Maximum}], interior=[{interior.Minimum}..{interior.Maximum}].");
        return 1;
    }

    private static bool TryResolveShellBounds(Node3D shell, out Bounds interior, out Bounds structuralEnvelope)
    {
        bool left = TryGetShellPieceBounds(shell, "LeftWall", out Bounds leftWall);
        bool right = TryGetShellPieceBounds(shell, "RightWall", out Bounds rightWall);
        bool back = TryGetShellPieceBounds(shell, "BackWall", out Bounds backWall);
        bool exit = TryGetShellPieceBounds(shell, "ExitWall", out Bounds exitWall);
        bool floor = TryGetShellPieceBounds(shell, "HazardFloor", out Bounds hazardFloor);
        bool ceiling = TryGetShellPieceBounds(shell, "Ceiling", out Bounds ceilingPiece);
        if (!left || !right || !back || !exit || !floor || !ceiling)
        {
            interior = default;
            structuralEnvelope = default;
            return false;
        }

        interior = new Bounds(
            new Vector3(leftWall.Maximum.X, hazardFloor.Maximum.Y, exitWall.Maximum.Z),
            new Vector3(rightWall.Minimum.X, ceilingPiece.Minimum.Y, backWall.Minimum.Z));
        structuralEnvelope = new Bounds(
            new Vector3(
                (leftWall.Minimum.X + leftWall.Maximum.X) * 0.5f,
                (hazardFloor.Minimum.Y + hazardFloor.Maximum.Y) * 0.5f,
                (exitWall.Minimum.Z + exitWall.Maximum.Z) * 0.5f),
            new Vector3(
                (rightWall.Minimum.X + rightWall.Maximum.X) * 0.5f,
                (ceilingPiece.Minimum.Y + ceilingPiece.Maximum.Y) * 0.5f,
                (backWall.Minimum.Z + backWall.Maximum.Z) * 0.5f));
        return interior.Size.X > 0.0f && interior.Size.Y > 0.0f && interior.Size.Z > 0.0f &&
            structuralEnvelope.Size.X > 0.0f && structuralEnvelope.Size.Y > 0.0f && structuralEnvelope.Size.Z > 0.0f;
    }

    private static bool IsLegalStructuralJoin(
        Node node,
        Bounds objectBounds,
        Bounds interior,
        Bounds structuralEnvelope,
        Node3D shell)
    {
        StaticBody3D? body = FindStaticBodyAncestor(node) ?? FindImportedReferenceTarget(node, shell);
        if (body is null || !TryGetStaticBodyBounds(body, shell, out Bounds bodyBounds))
        {
            return false;
        }

        if (!Contains(structuralEnvelope, bodyBounds))
        {
            return false;
        }

        return CrossingIsContainedByBody(objectBounds.Minimum.X, objectBounds.Maximum.X, bodyBounds.Minimum.X, bodyBounds.Maximum.X, interior.Minimum.X, interior.Maximum.X) &&
            CrossingIsContainedByBody(objectBounds.Minimum.Y, objectBounds.Maximum.Y, bodyBounds.Minimum.Y, bodyBounds.Maximum.Y, interior.Minimum.Y, interior.Maximum.Y) &&
            CrossingIsContainedByBody(objectBounds.Minimum.Z, objectBounds.Maximum.Z, bodyBounds.Minimum.Z, bodyBounds.Maximum.Z, interior.Minimum.Z, interior.Maximum.Z);
    }

    private static StaticBody3D? FindStaticBodyAncestor(Node node)
    {
        for (Node? ancestor = node; ancestor is not null; ancestor = ancestor.GetParent())
        {
            if (ancestor is StaticBody3D body)
            {
                return body;
            }
        }

        return null;
    }

    private static StaticBody3D? FindImportedReferenceTarget(Node node, Node3D shell)
    {
        if (!node.HasMeta(BlenderRoomEdits.ReferenceTargetPathMetadata))
        {
            return null;
        }

        Node? roomRoot = shell.GetParent();
        return roomRoot?.GetNodeOrNull<StaticBody3D>(
            node.GetMeta(BlenderRoomEdits.ReferenceTargetPathMetadata).AsNodePath());
    }

    private static bool IsBlenderStructuralGeometry(Node node)
    {
        if (node.Name.ToString().StartsWith("REF_", StringComparison.Ordinal))
        {
            return false;
        }

        for (Node? ancestor = node; ancestor is not null; ancestor = ancestor.GetParent())
        {
            string ancestorName = ancestor.Name.ToString();
            if (ancestorName == "BlenderGeometry" || ancestorName == "BlenderCollisions" || ancestorName == "EditableWalls")
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetStaticBodyBounds(StaticBody3D body, Node3D shell, out Bounds bounds)
    {
        Vector3 minimum = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        bool found = false;
        foreach (CollisionShape3D collision in body.GetChildren().OfType<CollisionShape3D>())
        {
            if (!TryGetShapeBounds(collision.Shape, out Aabb localBounds))
            {
                continue;
            }

            Bounds collisionBounds = TransformBounds(localBounds, collision.GlobalTransform, shell);
            minimum = new Vector3(
                Mathf.Min(minimum.X, collisionBounds.Minimum.X),
                Mathf.Min(minimum.Y, collisionBounds.Minimum.Y),
                Mathf.Min(minimum.Z, collisionBounds.Minimum.Z));
            maximum = new Vector3(
                Mathf.Max(maximum.X, collisionBounds.Maximum.X),
                Mathf.Max(maximum.Y, collisionBounds.Maximum.Y),
                Mathf.Max(maximum.Z, collisionBounds.Maximum.Z));
            found = true;
        }

        bounds = new Bounds(minimum, maximum);
        return found;
    }

    private static bool Contains(Bounds outer, Bounds inner, float tolerance = BoundaryTolerance)
    {
        return inner.Minimum.X >= outer.Minimum.X - tolerance && inner.Maximum.X <= outer.Maximum.X + tolerance &&
            inner.Minimum.Y >= outer.Minimum.Y - tolerance && inner.Maximum.Y <= outer.Maximum.Y + tolerance &&
            inner.Minimum.Z >= outer.Minimum.Z - tolerance && inner.Maximum.Z <= outer.Maximum.Z + tolerance;
    }

    private static bool CrossingIsContainedByBody(
        float objectMinimum,
        float objectMaximum,
        float bodyMinimum,
        float bodyMaximum,
        float interiorMinimum,
        float interiorMaximum)
    {
        if (objectMinimum < interiorMinimum - BoundaryTolerance &&
            (bodyMinimum >= interiorMinimum - BoundaryTolerance || objectMinimum < bodyMinimum - BoundaryTolerance))
        {
            return false;
        }

        if (objectMaximum > interiorMaximum + BoundaryTolerance &&
            (bodyMaximum <= interiorMaximum + BoundaryTolerance || objectMaximum > bodyMaximum + BoundaryTolerance))
        {
            return false;
        }

        return true;
    }

    private static bool TryGetShellPieceBounds(Node3D shell, string name, out Bounds bounds)
    {
        StaticBody3D? body = shell.GetNodeOrNull<StaticBody3D>(name);
        CollisionShape3D? collision = body?.GetChildren().OfType<CollisionShape3D>().FirstOrDefault();
        if (collision?.Shape is null || !TryGetShapeBounds(collision.Shape, out Aabb localBounds))
        {
            bounds = default;
            return false;
        }

        bounds = TransformBounds(localBounds, collision.GlobalTransform, shell);
        return true;
    }

    private static bool IsLegalShellExternal(Node node, Node roomRoot, Node3D shell, Area3D? goalTrigger)
    {
        if (node == shell || shell.IsAncestorOf(node))
        {
            return true;
        }

        if (goalTrigger is not null && (node == goalTrigger || goalTrigger.IsAncestorOf(node)))
        {
            return true;
        }

        for (Node? ancestor = node; ancestor is not null; ancestor = ancestor.GetParent())
        {
            if (ancestor is ExitDoor3D || ancestor is InterferenceCannon3D)
            {
                return true;
            }
        }

        if (node is MeshInstance3D mesh && IsLegalExternalReference(mesh))
        {
            return true;
        }

        if (node is CollisionShape3D collision)
        {
            NodePath collisionPath = roomRoot.GetPathTo(collision);
            bool belongsToDoorInfrastructure = EnumerateDescendants(roomRoot)
                .OfType<MeshInstance3D>()
                .Where(IsLegalExternalReference)
                .Any(reference =>
                    reference.HasMeta(BlenderRoomEdits.ReferenceCollisionPathMetadata) &&
                    reference.GetMeta(BlenderRoomEdits.ReferenceCollisionPathMetadata).AsNodePath() == collisionPath);
            if (belongsToDoorInfrastructure)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLegalExternalReference(MeshInstance3D mesh)
    {
        string name = mesh.Name.ToString();
        int separator = name.Length > 4 ? name.IndexOf('_', 4) : -1;
        string target = separator >= 0 ? name[(separator + 1)..] : name;
        int duplicateSeparator = Mathf.Max(target.LastIndexOf('.'), target.LastIndexOf('_'));
        if (duplicateSeparator > 0 && duplicateSeparator < target.Length - 1 &&
            target[(duplicateSeparator + 1)..].All(char.IsDigit))
        {
            target = target[..duplicateSeparator];
        }

        return target.Equals("HazardFloor", StringComparison.Ordinal) ||
            target.Equals("Ceiling", StringComparison.Ordinal) ||
            target.Equals("LeftWall", StringComparison.Ordinal) ||
            target.Equals("RightWall", StringComparison.Ordinal) ||
            target.Equals("BackWall", StringComparison.Ordinal) ||
            target.Equals("ExitWall", StringComparison.Ordinal) ||
            target.Contains("WallDoorway", StringComparison.Ordinal) ||
            target.Equals("ClosedDoorBlocker", StringComparison.Ordinal) ||
            target.StartsWith("FrameCollision", StringComparison.Ordinal) ||
            target.StartsWith("ExitCorridor", StringComparison.Ordinal) ||
            target.StartsWith("ExitDoorBacking", StringComparison.Ordinal);
    }

    private static bool TryGetShapeBounds(Shape3D shape, out Aabb bounds)
    {
        switch (shape)
        {
            case BoxShape3D box:
                bounds = new Aabb(-box.Size * 0.5f, box.Size);
                return true;
            case SphereShape3D sphere:
                Vector3 sphereSize = Vector3.One * sphere.Radius * 2.0f;
                bounds = new Aabb(-sphereSize * 0.5f, sphereSize);
                return true;
            case CylinderShape3D cylinder:
                Vector3 cylinderSize = new(cylinder.Radius * 2.0f, cylinder.Height, cylinder.Radius * 2.0f);
                bounds = new Aabb(-cylinderSize * 0.5f, cylinderSize);
                return true;
            case ConcavePolygonShape3D concave:
                return TryGetPointBounds(concave.GetFaces(), out bounds);
            case ConvexPolygonShape3D convex:
                return TryGetPointBounds(convex.Points, out bounds);
            default:
                bounds = default;
                return false;
        }
    }

    private static bool TryGetPointBounds(Vector3[] points, out Aabb bounds)
    {
        if (points.Length == 0)
        {
            bounds = default;
            return false;
        }

        Vector3 minimum = points[0];
        Vector3 maximum = points[0];
        foreach (Vector3 point in points.Skip(1))
        {
            minimum = minimum.Min(point);
            maximum = maximum.Max(point);
        }

        bounds = new Aabb(minimum, maximum - minimum);
        return true;
    }

    private static Bounds TransformBounds(Aabb localBounds, Transform3D globalTransform, Node3D shell)
    {
        Vector3 minimum = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        Vector3 localEnd = localBounds.Position + localBounds.Size;
        foreach (float x in new[] { localBounds.Position.X, localEnd.X })
        foreach (float y in new[] { localBounds.Position.Y, localEnd.Y })
        foreach (float z in new[] { localBounds.Position.Z, localEnd.Z })
        {
            Vector3 point = shell.ToLocal(globalTransform * new Vector3(x, y, z));
            minimum = new Vector3(Mathf.Min(minimum.X, point.X), Mathf.Min(minimum.Y, point.Y), Mathf.Min(minimum.Z, point.Z));
            maximum = new Vector3(Mathf.Max(maximum.X, point.X), Mathf.Max(maximum.Y, point.Y), Mathf.Max(maximum.Z, point.Z));
        }
        return new Bounds(minimum, maximum);
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

    private static IEnumerable<Node> EnumerateSelfAndDescendants(Node root)
    {
        yield return root;
        foreach (Node descendant in EnumerateDescendants(root))
        {
            yield return descendant;
        }
    }

    private static int[] ResolveRequestedRooms()
    {
        string? requested = OS.GetCmdlineUserArgs()
            .FirstOrDefault(argument => argument.StartsWith("--containment-room=", StringComparison.Ordinal));
        if (requested is not null &&
            int.TryParse(requested["--containment-room=".Length..], out int room) &&
            room is >= 1 and <= 30)
        {
            return new[] { room };
        }

        return Enumerable.Range(1, 30).ToArray();
    }

    private async Task FinishAsync(int exitCode)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(exitCode);
    }
}
