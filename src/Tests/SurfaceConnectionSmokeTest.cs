using Godot;
using System.Text.Json;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Tests;

public partial class SurfaceConnectionSmokeTest : Node
{
    private const float DetectionGap = 0.7f;
    private const float DetectionHeight = 2.0f;
    private const float MaximumSeamGap = 0.01f;
    private const float MaximumSeamStep = 0.01f;
    private const float MinimumGeneratedWallVisualHeight = 1.21f;
    private const float MaximumGeneratedWallVisualHeight = 1.60f;
    private const float MaximumGeneratedWallTotalOverhang = 0.95f;

    private readonly record struct Surface(
        StaticBody3D Body,
        CollisionShape3D Collision,
        BoxShape3D Shape,
        bool IsSloped);
    private readonly record struct Barrier(
        StaticBody3D Body,
        Vector3 SupportSize,
        Vector3 VisualSize,
        Vector3 VisualOffset);
    private readonly record struct StructuralWall(StaticBody3D Body, BoxShape3D Shape);
    private readonly record struct Edge(Vector3 Start, Vector3 End, Vector2 Outward);
    private readonly record struct Seam(
        Surface A,
        Surface B,
        float SignedGap,
        float MaximumHeightDifference,
        float OverlapLength);

    public override async void _Ready()
    {
        int[] rooms = ResolveRequestedRooms();
        string? geometryExportPath = OS.GetCmdlineUserArgs()
            .FirstOrDefault(argument => argument.StartsWith("--surface-geometry-export=", StringComparison.Ordinal))?
            ["--surface-geometry-export=".Length..];
        if (!string.IsNullOrWhiteSpace(geometryExportPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(geometryExportPath)!);
            File.Delete(geometryExportPath);
        }
        int issueCount = 0;
        foreach (int room in rooms)
        {
            string scenePath = room == 1
                ? "res://scenes/MovementTestRoom.tscn"
                : $"res://scenes/Room{room:00}.tscn";
            PackedScene? packed = GD.Load<PackedScene>(scenePath);
            if (packed is null)
            {
                GD.PushError($"SURFACE_CONNECTION_FAIL: Room {room:00} scene is missing.");
                issueCount++;
                continue;
            }

            Node roomRoot = packed.Instantiate();
            AddChild(roomRoot);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            roomRoot.ProcessMode = ProcessModeEnum.Disabled;
            if (!string.IsNullOrWhiteSpace(geometryExportPath))
            {
                ExportRoomGeometry(room, roomRoot, geometryExportPath);
            }
            AuditResult result = AuditRoom(room, roomRoot);
            issueCount += result.IssueCount;
            if (result.IssueCount == 0)
            {
                GD.Print(
                    $"SURFACE_CONNECTION_ROOM_PASS: Room {room:00} audited {result.SurfaceCount} rolling surfaces and " +
                    $"{result.SeamCount} adjoining seams plus {result.BarrierCount} supported edge barriers; " +
                    $"worst gap={result.WorstGap:F3} m, worst step={result.WorstStep:F3} m, " +
                    $"start-wall gap={result.StartWallGap:F3} m.");
            }

            roomRoot.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (issueCount > 0)
        {
            GD.PushError($"SURFACE_CONNECTION_FAIL: found {issueCount} raised, dropped or separated rolling-surface seams.");
            await FinishAsync(1);
            return;
        }

        GD.Print($"SURFACE_CONNECTION_PASS: all adjoining platform and slope edges in {rooms.Length} requested room(s) are flush and connected.");
        await FinishAsync(0);
    }

    private static void ExportRoomGeometry(int room, Node root, string path)
    {
        using StreamWriter writer = new(path, append: true);
        foreach (StaticBody3D body in EnumerateDescendants(root).OfType<StaticBody3D>())
        {
        if (body.HasMeta(RoomGeometry.GeneratedPlatformWallMetadata) &&
            body.GetChildren().OfType<MeshInstance3D>().FirstOrDefault() is MeshInstance3D visual &&
            visual.Mesh is Mesh wallMesh)
        {
            Vector3[] vertices = wallMesh.GetFaces()
                .Select(vertex => visual.ToGlobal(vertex))
                .ToArray();
            writer.WriteLine(JsonSerializer.Serialize(new
            {
                room,
                name = body.Name.ToString(),
                kind = "wall_visual",
                vertices = vertices
                    .SelectMany(vertex => new[] { vertex.X, vertex.Y, vertex.Z })
                    .ToArray(),
            }));
        }
        foreach (CollisionShape3D collision in body.GetChildren().OfType<CollisionShape3D>())
        {
            if (collision.Disabled || collision.Shape is not BoxShape3D box)
            {
                continue;
            }

            Transform3D transform = collision.GlobalTransform;
            bool generatedWall = body.HasMeta(RoomGeometry.GeneratedPlatformWallMetadata);
            bool barrier = IsBarrierBox(body.Name.ToString(), box);
            bool rollingSurface = !barrier && IsRollingSurface(body, box);

            writer.WriteLine(JsonSerializer.Serialize(new
            {
                room,
                name = body.Name.ToString(),
                kind = generatedWall ? "wall" : rollingSurface ? "platform" : "structural",
                support = body.HasMeta(RoomGeometry.GeneratedPlatformWallSurfaceMetadata)
                    ? body.GetMeta(RoomGeometry.GeneratedPlatformWallSurfaceMetadata).AsString()
                    : string.Empty,
                origin = new[] { transform.Origin.X, transform.Origin.Y, transform.Origin.Z },
                basis = new[]
                {
                    transform.Basis.X.X, transform.Basis.X.Y, transform.Basis.X.Z,
                    transform.Basis.Y.X, transform.Basis.Y.Y, transform.Basis.Y.Z,
                    transform.Basis.Z.X, transform.Basis.Z.Y, transform.Basis.Z.Z,
                },
                size = new[] { box.Size.X, box.Size.Y, box.Size.Z },
            }));
        }
        }
    }

    private static int[] ResolveRequestedRooms()
    {
        string? requested = OS.GetCmdlineUserArgs()
            .FirstOrDefault(argument => argument.StartsWith("--surface-room=", StringComparison.Ordinal));
        if (requested is not null &&
            int.TryParse(requested["--surface-room=".Length..], out int room) &&
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

    private readonly record struct AuditResult(
        int IssueCount,
        int SurfaceCount,
        int SeamCount,
        int BarrierCount,
        float WorstGap,
        float WorstStep,
        float StartWallGap);

    private static AuditResult AuditRoom(int room, Node root)
    {
        List<Surface> surfaces = new();
        CollectSurfaces(root, surfaces);
        List<Barrier> barriers = new();
        CollectBarriers(root, barriers);
        List<StructuralWall> structuralWalls = CollectStructuralWalls(root);
        List<Seam> seams = new();

        for (int first = 0; first < surfaces.Count; first++)
        {
            for (int second = first + 1; second < surfaces.Count; second++)
            {
                if (TryFindAdjoiningSeam(surfaces[first], surfaces[second], out Seam seam))
                {
                    seams.Add(seam);
                }
            }
        }

        int issues = 0;
        float worstGap = 0.0f;
        float worstStep = 0.0f;
        foreach (Seam seam in seams)
        {
            float gap = Mathf.Abs(seam.SignedGap);
            GD.Print(
                $"SURFACE_SEAM_AUDIT: Room {room:00} {seam.A.Body.Name} <-> {seam.B.Body.Name}: " +
                $"gap={seam.SignedGap:F3} m, step={seam.MaximumHeightDifference:F3} m, overlap={seam.OverlapLength:F3} m.");
            if (IsAcceptableSeam(seam))
            {
                worstGap = Mathf.Max(worstGap, gap);
                worstStep = Mathf.Max(worstStep, seam.MaximumHeightDifference);
                continue;
            }

            if (IsBridgedBySurface(seam, seams))
            {
                GD.Print(
                    $"SURFACE_SEAM_BRIDGED: Room {room:00} {seam.A.Body.Name} <-> {seam.B.Body.Name} " +
                    "is fully covered by a flush transition surface.");
                continue;
            }

            issues++;
            GD.PushError(
                $"SURFACE_SEAM: Room {room:00} {seam.A.Body.Name} <-> {seam.B.Body.Name}: " +
                $"gap={seam.SignedGap:F3} m, step={seam.MaximumHeightDifference:F3} m, overlap={seam.OverlapLength:F3} m.");
        }

        float startWallGap = MeasureStartWallGap(root);
        if (float.IsPositiveInfinity(startWallGap))
        {
            issues++;
            GD.PushError($"SURFACE_START_WALL: Room {room:00} could not identify the player's starting surface and back wall.");
        }
        else if (startWallGap > 0.05f)
        {
            issues++;
            GD.PushError($"SURFACE_START_WALL: Room {room:00} leaves a {startWallGap:F3} m nuisance gap behind the starting surface.");
        }

        foreach (Barrier barrier in barriers)
        {
            if (barrier.Body.HasMeta(RoomGeometry.GeneratedPlatformWallMetadata))
            {
                bool runsAlongX = barrier.SupportSize.X >= barrier.SupportSize.Z;
                float supportLength = runsAlongX ? barrier.SupportSize.X : barrier.SupportSize.Z;
                float visualLength = runsAlongX ? barrier.VisualSize.X : barrier.VisualSize.Z;
                float totalOverhang = visualLength - supportLength;
                if (barrier.VisualSize.Y < MinimumGeneratedWallVisualHeight)
                {
                    issues++;
                    GD.PushError(
                        $"SURFACE_BARRIER_HEIGHT: Room {room:00} {barrier.Body.Name} is only " +
                        $"{barrier.VisualSize.Y:F3} m high instead of the shared tall-wall height.");
                }
                if (barrier.VisualSize.Y > MaximumGeneratedWallVisualHeight)
                {
                    issues++;
                    GD.PushError(
                        $"SURFACE_BARRIER_HEIGHT: Room {room:00} {barrier.Body.Name} is " +
                        $"{barrier.VisualSize.Y:F3} m high, above the approved normalized-wall range.");
                }
                CollisionShape3D? generatedHitbox = barrier.Body.GetNodeOrNull<CollisionShape3D>("GeneratedWallHitbox");
                BoxShape3D? generatedHitboxBox = generatedHitbox?.Shape as BoxShape3D;
                if (generatedHitbox is null || generatedHitboxBox is null ||
                    generatedHitboxBox.Size.DistanceTo(barrier.VisualSize) > 0.001f ||
                    generatedHitbox.Position.DistanceTo(barrier.VisualOffset) > 0.001f)
                {
                    issues++;
                    GD.PushError(
                        $"SURFACE_BARRIER_HITBOX: Room {room:00} {barrier.Body.Name} does not have a hitbox matching its generated visual wall; " +
                        $"found={generatedHitbox is not null}, size={generatedHitboxBox?.Size}, expected-size={barrier.VisualSize}, " +
                        $"offset={generatedHitbox?.Position}, expected-offset={barrier.VisualOffset}.");
                }
                if (totalOverhang > MaximumGeneratedWallTotalOverhang)
                {
                    issues++;
                    GD.PushError(
                        $"SURFACE_BARRIER_OVERHANG: Room {room:00} {barrier.Body.Name} extends " +
                        $"{totalOverhang:F3} m beyond its authored wall guide.");
                }
                if (!IsBarrierClearOfPlatformInteriors(barrier, surfaces, out string intrusionDetails))
                {
                    issues++;
                    GD.PushError(
                        $"SURFACE_BARRIER_INTRUSION: Room {room:00} {barrier.Body.Name} leaves the platform edge; " +
                        intrusionDetails);
                }
            }

            if (IsBarrierSupported(barrier, surfaces, structuralWalls, out string supportDetails))
            {
                continue;
            }

            issues++;
            GD.PushError(
                $"SURFACE_BARRIER_SUPPORT: Room {room:00} {barrier.Body.Name} loses contact with its supporting floor; {supportDetails}.");
        }

        ulong[] generatedWallMaterialIds = barriers
            .Where(barrier => barrier.Body.HasMeta(RoomGeometry.GeneratedPlatformWallMetadata))
            .Select(barrier => barrier.Body.GetChildren().OfType<MeshInstance3D>().FirstOrDefault()?.MaterialOverride)
            .Where(material => material is not null)
            .Select(material => material!.GetInstanceId())
            .Distinct()
            .ToArray();
        if (generatedWallMaterialIds.Length > 1)
        {
            issues++;
            string materialAssignments = string.Join(", ", barriers
                .Where(barrier => barrier.Body.HasMeta(RoomGeometry.GeneratedPlatformWallMetadata))
                .Select(barrier =>
                {
                    Material? material = barrier.Body.GetChildren().OfType<MeshInstance3D>()
                        .FirstOrDefault()?.MaterialOverride;
                    return $"{barrier.Body.Name}={(material is null ? "none" : material.GetInstanceId())}";
                }));
            GD.PushError($"SURFACE_BARRIER_COLOR: Room {room:00} uses {generatedWallMaterialIds.Length} different generated-wall materials instead of one room-wide material.");
            GD.PushError($"SURFACE_BARRIER_COLOR_DETAILS: Room {room:00} {materialAssignments}.");
        }

        foreach (Barrier barrier in barriers)
        {
            if (TryFindNearbyDisconnectedBarrierEnd(barrier, barriers, structuralWalls, out string connectionDetails))
            {
                issues++;
                GD.PushError($"SURFACE_BARRIER_CONNECTION: Room {room:00} {barrier.Body.Name} has a nearby disconnected end; {connectionDetails}.");
            }
        }

        issues += AuditSharedWindFans(room, root);

        return new AuditResult(issues, surfaces.Count, seams.Count, barriers.Count, worstGap, worstStep, startWallGap);
    }

    private static bool IsBarrierClearOfPlatformInteriors(
        Barrier barrier,
        List<Surface> surfaces,
        out string details)
    {
        details = string.Empty;
        bool runsAlongX = barrier.VisualSize.X >= barrier.VisualSize.Z;
        float halfLength = (runsAlongX ? barrier.VisualSize.X : barrier.VisualSize.Z) * 0.5f;
        Vector3 localAxis = runsAlongX ? Vector3.Right : Vector3.Back;
        Vector3 localBase = barrier.VisualOffset - (Vector3.Up * (barrier.VisualSize.Y * 0.5f));
        string supportName = barrier.Body.HasMeta(RoomGeometry.GeneratedPlatformWallSurfaceMetadata)
            ? barrier.Body.GetMeta(RoomGeometry.GeneratedPlatformWallSurfaceMetadata).AsString()
            : string.Empty;

        for (int sample = 0; sample <= 12; sample++)
        {
            float offset = Mathf.Lerp(-halfLength, halfLength, sample / 12.0f);
            Vector3 worldPoint = barrier.Body.ToGlobal(localBase + (localAxis * offset));
            foreach (Surface surface in surfaces)
            {
                if (surface.Body.Name.ToString() == supportName)
                {
                    continue;
                }

                Vector3 local = surface.Collision.ToLocal(worldPoint);
                Vector3 half = surface.Shape.Size * 0.5f;
                const float interiorMargin = 0.04f;
                bool insideFootprint = Mathf.Abs(local.X) < half.X - interiorMargin &&
                    Mathf.Abs(local.Z) < half.Z - interiorMargin;
                bool nearTop = local.Y >= half.Y - 0.08f &&
                    local.Y <= half.Y + barrier.VisualSize.Y + 0.08f;
                if (!insideFootprint || !nearTop)
                {
                    continue;
                }

                details = $"base sample {sample}/12 is inside platform {surface.Body.Name} " +
                    $"at local ({local.X:F3}, {local.Y:F3}, {local.Z:F3}).";
                return false;
            }
        }

        return true;
    }

    private static int AuditSharedWindFans(int room, Node root)
    {
        int expectedCount = room switch
        {
            13 => 3,
            15 => 3,
            26 => 1,
            _ => 0,
        };
        if (expectedCount == 0)
        {
            return 0;
        }

        List<Node3D> housings = new();
        CollectWindFanHousings(root, housings);
        bool sharedModel = housings.Count == expectedCount && housings.All(housing =>
            housing.GetNodeOrNull<MeshInstance3D>("Hub") is not null &&
            housing.GetNodeOrNull<MeshInstance3D>("Guard") is MeshInstance3D { Mesh: TorusMesh } &&
            housing.GetNodeOrNull<Node3D>("Rotor") is Node3D rotor &&
            rotor.GetNodeOrNull<MeshInstance3D>("SpinnerCap") is not null &&
            Enumerable.Range(1, 5).All(index =>
                rotor.GetNodeOrNull<MeshInstance3D>($"Blade{index}") is not null));
        if (sharedModel)
        {
            GD.Print($"SHARED_WIND_FAN_PASS: Room {room:00} uses the canonical hub, smooth torus guard, spinner cap and five-blade rotor for all {housings.Count} fans.");
            return 0;
        }

        GD.PushError($"SHARED_WIND_FAN_FAIL: Room {room:00} found {housings.Count}/{expectedCount} canonical fan housings.");
        return 1;
    }

    private static void CollectWindFanHousings(Node node, List<Node3D> housings)
    {
        string name = node.Name.ToString();
        if (node is Node3D housing &&
            (name.StartsWith("WindFanHousing", StringComparison.Ordinal) || name == "VacuumFan"))
        {
            housings.Add(housing);
        }
        foreach (Node child in node.GetChildren())
        {
            CollectWindFanHousings(child, housings);
        }
    }

    private static bool TryFindNearbyDisconnectedBarrierEnd(
        Barrier barrier,
        List<Barrier> barriers,
        List<StructuralWall> structuralWalls,
        out string details)
    {
        bool runsAlongX = barrier.SupportSize.X >= barrier.SupportSize.Z;
        float supportLongHalf = (runsAlongX ? barrier.SupportSize.X : barrier.SupportSize.Z) * 0.5f;
        float visualLongHalf = (runsAlongX ? barrier.VisualSize.X : barrier.VisualSize.Z) * 0.5f;
        foreach (float endSign in new[] { -1.0f, 1.0f })
        {
            Vector3 localSupportEnd = runsAlongX
                ? new Vector3(endSign * supportLongHalf, 0.0f, 0.0f)
                : new Vector3(0.0f, 0.0f, endSign * supportLongHalf);
            // Keep lateral/base snapping, but exclude the longitudinal centre
            // shift introduced by one-sided visual overlap.
            Vector3 logicalVisualOffset = barrier.VisualOffset;
            if (runsAlongX)
            {
                logicalVisualOffset.X = 0.0f;
            }
            else
            {
                logicalVisualOffset.Z = 0.0f;
            }
            Vector3 worldSupportEnd = barrier.Body.ToGlobal(localSupportEnd + logicalVisualOffset);
            Vector3 localAhead = localSupportEnd + (runsAlongX
                ? new Vector3(endSign * 0.25f, 0.0f, 0.0f)
                : new Vector3(0.0f, 0.0f, endSign * 0.25f));
            Vector3 worldAhead = barrier.Body.ToGlobal(localAhead + logicalVisualOffset);
            float nearestGap = float.PositiveInfinity;
            string nearestName = string.Empty;
            foreach (Barrier other in barriers)
            {
                if (other.Body == barrier.Body)
                {
                    continue;
                }
                float gap = DistanceToBox(worldSupportEnd, other.Body, other.SupportSize, other.VisualOffset);
                float gapAhead = DistanceToBox(worldAhead, other.Body, other.SupportSize, other.VisualOffset);
                if (gapAhead >= gap - 0.01f)
                {
                    continue;
                }
                if (gap < nearestGap)
                {
                    nearestGap = gap;
                    nearestName = other.Body.Name.ToString();
                }
            }
            foreach (StructuralWall wall in structuralWalls)
            {
                float gap = DistanceToBox(worldSupportEnd, wall.Body, wall.Shape.Size);
                float gapAhead = DistanceToBox(worldAhead, wall.Body, wall.Shape.Size);
                if (gapAhead >= gap - 0.01f)
                {
                    continue;
                }
                if (gap < nearestGap)
                {
                    nearestGap = gap;
                    nearestName = wall.Body.Name.ToString();
                }
            }
            if (nearestGap <= 1.0f && !VisualBarrierEndTouchesAnother(
                    barrier,
                    runsAlongX,
                    endSign,
                    visualLongHalf,
                    barriers,
                    structuralWalls))
            {
                details = $"{(endSign < 0.0f ? "start" : "end")} is {nearestGap:F3} m from {nearestName}";
                return true;
            }
        }
        details = string.Empty;
        return false;
    }

    private static bool VisualBarrierEndTouchesAnother(
        Barrier barrier,
        bool runsAlongX,
        float endSign,
        float visualLongHalf,
        List<Barrier> barriers,
        List<StructuralWall> structuralWalls)
    {
        // Sloped and flat wall boxes can meet farther inside the sloped box
        // even when their authored collision endpoints are adjacent.
        float sampleLength = Mathf.Min(3.0f, visualLongHalf * 2.0f);
        float visualThinHalf = (runsAlongX ? barrier.VisualSize.Z : barrier.VisualSize.X) * 0.5f;
        const float sampleStep = 0.025f;
        for (float inward = 0.0f; inward <= sampleLength + 0.001f; inward += sampleStep)
        {
            float longOffset = endSign * (visualLongHalf - inward);
            foreach (float thinOffset in new[] { -visualThinHalf, 0.0f, visualThinHalf })
            {
                Vector3 localSample = runsAlongX
                    ? new Vector3(longOffset, 0.0f, thinOffset)
                    : new Vector3(thinOffset, 0.0f, longOffset);
                Vector3 worldSample = barrier.Body.ToGlobal(localSample + barrier.VisualOffset);
                if (barriers.Any(other => other.Body != barrier.Body &&
                        DistanceToBox(worldSample, other.Body, other.VisualSize, other.VisualOffset) <= 0.02f) ||
                    structuralWalls.Any(wall => DistanceToBox(worldSample, wall.Body, wall.Shape.Size) <= 0.02f))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static float DistanceToBox(Vector3 worldPoint, Node3D body, Vector3 size)
    {
        return DistanceToBox(worldPoint, body, size, Vector3.Zero);
    }

    private static float DistanceToBox(
        Vector3 worldPoint,
        Node3D body,
        Vector3 size,
        Vector3 localOffset)
    {
        Vector3 local = body.ToLocal(worldPoint) - localOffset;
        Vector3 half = size * 0.5f;
        Vector3 outside = new(
            Mathf.Max(Mathf.Abs(local.X) - half.X, 0.0f),
            Mathf.Max(Mathf.Abs(local.Y) - half.Y, 0.0f),
            Mathf.Max(Mathf.Abs(local.Z) - half.Z, 0.0f));
        return outside.Length();
    }

    private static bool IsBarrierSupported(
        Barrier barrier,
        List<Surface> surfaces,
        List<StructuralWall> structuralWalls,
        out string details)
    {
        if (barrier.Body.Name.ToString().Contains("Junction", StringComparison.OrdinalIgnoreCase) ||
            barrier.Body.Name.ToString().StartsWith("GeneratedRailJoin", StringComparison.Ordinal))
        {
            details = "wall-to-wall junction filler";
            return true;
        }
        details = "no nearby rolling surface";
        bool runsAlongX = barrier.SupportSize.X >= barrier.SupportSize.Z;
        float longHalf = (runsAlongX ? barrier.SupportSize.X : barrier.SupportSize.Z) * 0.5f;
        float thinHalf = (runsAlongX ? barrier.SupportSize.Z : barrier.SupportSize.X) * 0.5f;
        Vector3 supportVisualOffset = barrier.VisualOffset;
        if (runsAlongX)
        {
            supportVisualOffset.X = 0.0f;
        }
        else
        {
            supportVisualOffset.Z = 0.0f;
        }
        // Generated wall visuals deliberately overlap neighbouring wall ends
        // by up to 0.275 m so a camera cannot expose a hairline seam. Audit
        // the supported wall body just inside that cosmetic end overlap.
        float endInset = Mathf.Min(0.30f, longHalf * 0.1f);
        foreach (float longOffset in new[]
        {
            -longHalf + endInset,
            -longHalf * 0.5f,
            0.0f,
            longHalf * 0.5f,
            longHalf - endInset,
        })
        {
            bool supportedAtSample = false;
            float bestScore = float.PositiveInfinity;
            string nearestDetails = "no nearby rolling surface";
            foreach (float thinOffset in new[] { -thinHalf, 0.0f, thinHalf })
            {
                Vector3 localBottom = runsAlongX
                    ? new Vector3(longOffset, -barrier.SupportSize.Y * 0.5f, thinOffset)
                    : new Vector3(thinOffset, -barrier.SupportSize.Y * 0.5f, longOffset);
                Vector3 bottomPoint = barrier.Body.ToGlobal(localBottom + supportVisualOffset);
                if (IsBackedByStructuralWall(bottomPoint, structuralWalls))
                {
                    supportedAtSample = true;
                    break;
                }
                foreach (Surface surface in surfaces)
                {
                    if (surface.Body == barrier.Body)
                    {
                        continue;
                    }

                    Vector3 local = surface.Collision.ToLocal(bottomPoint);
                    float horizontalTolerance = 0.015f;
                    float xOverrun = Mathf.Max(0.0f, Mathf.Abs(local.X) - surface.Shape.Size.X * 0.5f);
                    float zOverrun = Mathf.Max(0.0f, Mathf.Abs(local.Z) - surface.Shape.Size.Z * 0.5f);
                    float supportGap = local.Y - (surface.Shape.Size.Y * 0.5f);
                    float score = xOverrun + zOverrun + Mathf.Abs(supportGap);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        nearestDetails = $"nearest={surface.Body.Name}, x-overrun={xOverrun:F3} m, z-overrun={zOverrun:F3} m, vertical-gap={supportGap:F3} m";
                    }
                    if (Mathf.Abs(local.X) > surface.Shape.Size.X * 0.5f + horizontalTolerance ||
                        Mathf.Abs(local.Z) > surface.Shape.Size.Z * 0.5f + horizontalTolerance)
                    {
                        continue;
                    }

                    if (supportGap >= -1.10f && supportGap <= 0.02f)
                    {
                        supportedAtSample = true;
                        break;
                    }
                }

                if (supportedAtSample) { break; }
            }

            if (!supportedAtSample)
            {
                details = $"unsupported {(longOffset < 0.0f ? "start" : longOffset > 0.0f ? "end" : "midpoint")} sample at local offset {longOffset:F3} m; {nearestDetails}";
                return false;
            }
        }

        return true;
    }

    private static bool IsBackedByStructuralWall(Vector3 point, List<StructuralWall> walls)
    {
        const float tolerance = 0.08f;
        foreach (StructuralWall wall in walls)
        {
            Vector3 local = wall.Body.ToLocal(point);
            Vector3 half = wall.Shape.Size * 0.5f;
            if (Mathf.Abs(local.X) <= half.X + tolerance &&
                Mathf.Abs(local.Y) <= half.Y + tolerance &&
                Mathf.Abs(local.Z) <= half.Z + tolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static List<StructuralWall> CollectStructuralWalls(Node root)
    {
        string[] wallNames = { "LeftWall", "RightWall", "BackWall", "ExitWall" };
        List<StructuralWall> walls = new();
        foreach (StaticBody3D body in EnumerateDescendants(root).OfType<StaticBody3D>())
        {
            if (!wallNames.Contains(body.Name.ToString(), StringComparer.Ordinal))
            {
                continue;
            }

            foreach (CollisionShape3D collision in body.GetChildren().OfType<CollisionShape3D>())
            {
                if (!collision.Disabled && collision.Shape is BoxShape3D box)
                {
                    walls.Add(new StructuralWall(body, box));
                }
            }
        }

        return walls;
    }

    private static float MeasureStartWallGap(Node root)
    {
        Node3D? shell = root.GetNodeOrNull<Node3D>("RoomShell");
        StaticBody3D? backWall = shell?.GetNodeOrNull<StaticBody3D>("BackWall");
        CollisionShape3D? backCollision = backWall?.GetChildren().OfType<CollisionShape3D>().FirstOrDefault();
        PlayerBall? player = EnumerateDescendants(root).OfType<PlayerBall>().FirstOrDefault();
        if (shell is null || backWall is null || backCollision?.Shape is not BoxShape3D backBox || player is null)
        {
            return float.PositiveInfinity;
        }

        CollisionShape3D? startCollision = null;
        BoxShape3D? startBox = null;
        float highestTop = float.NegativeInfinity;
        foreach (StaticBody3D body in EnumerateDescendants(root).OfType<StaticBody3D>())
        {
            if (body.Name == "HazardFloor" ||
                body.Name.ToString().Contains("Wall", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (CollisionShape3D collision in body.GetChildren().OfType<CollisionShape3D>())
            {
                if (collision.Disabled || collision.Shape is not BoxShape3D box)
                {
                    continue;
                }

                Vector3 localPlayer = collision.ToLocal(player.GlobalPosition);
                if (Mathf.Abs(localPlayer.X) > (box.Size.X * 0.5f) + 0.2f ||
                    Mathf.Abs(localPlayer.Z) > (box.Size.Z * 0.5f) + 0.2f)
                {
                    continue;
                }

                float topY = collision.ToGlobal(new Vector3(localPlayer.X, box.Size.Y * 0.5f, localPlayer.Z)).Y;
                if (topY <= player.GlobalPosition.Y && topY > highestTop)
                {
                    highestTop = topY;
                    startCollision = collision;
                    startBox = box;
                }
            }
        }

        if (startCollision is null || startBox is null)
        {
            return float.PositiveInfinity;
        }

        float startMaximumZ = float.NegativeInfinity;
        Vector3 half = startBox.Size * 0.5f;
        foreach (float x in new[] { -half.X, half.X })
        foreach (float y in new[] { -half.Y, half.Y })
        foreach (float z in new[] { -half.Z, half.Z })
        {
            startMaximumZ = Mathf.Max(startMaximumZ, shell.ToLocal(startCollision.ToGlobal(new Vector3(x, y, z))).Z);
        }

        float wallInnerZ = shell.ToLocal(backCollision.GlobalPosition).Z - (backBox.Size.Z * 0.5f);
        return wallInnerZ - startMaximumZ;
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

    private static bool IsAcceptableSeam(Seam seam)
    {
        float gap = Mathf.Abs(seam.SignedGap);
        bool benignCoplanarOverlap =
            !seam.A.IsSloped && !seam.B.IsSloped &&
            seam.SignedGap <= 0.0f && gap <= 0.55f &&
            seam.MaximumHeightDifference <= 0.005f;
        return benignCoplanarOverlap ||
            (gap <= MaximumSeamGap && seam.MaximumHeightDifference <= MaximumSeamStep);
    }

    private static bool IsBridgedBySurface(Seam rejected, List<Seam> seams)
    {
        foreach (Seam firstLeg in seams)
        {
            if (!IsAcceptableSeam(firstLeg))
            {
                continue;
            }

            Surface? bridge = null;
            if (firstLeg.A.Body == rejected.A.Body && firstLeg.B.Body != rejected.B.Body)
            {
                bridge = firstLeg.B;
            }
            else if (firstLeg.B.Body == rejected.A.Body && firstLeg.A.Body != rejected.B.Body)
            {
                bridge = firstLeg.A;
            }

            if (bridge is null)
            {
                continue;
            }

            bool reachesOtherSide = seams.Any(secondLeg =>
                IsAcceptableSeam(secondLeg) &&
                ((secondLeg.A.Body == bridge.Value.Body && secondLeg.B.Body == rejected.B.Body) ||
                 (secondLeg.B.Body == bridge.Value.Body && secondLeg.A.Body == rejected.B.Body)));
            if (reachesOtherSide)
            {
                return true;
            }
        }

        return false;
    }

    private static void CollectSurfaces(Node node, List<Surface> surfaces)
    {
        if (node is StaticBody3D body && !IsExcluded(body.Name.ToString()))
        {
            foreach (CollisionShape3D collision in body.GetChildren().OfType<CollisionShape3D>())
            {
                if (collision.Disabled || collision.Shape is not BoxShape3D box ||
                    IsBarrierBox(body.Name.ToString(), box) || !IsRollingSurface(body, box))
                {
                    continue;
                }

                float up = Mathf.Abs((collision.GlobalTransform.Basis * Vector3.Up).Normalized().Dot(Vector3.Up));
                surfaces.Add(new Surface(body, collision, box, up < 0.9995f));
                break;
            }
        }

        foreach (Node child in node.GetChildren())
        {
            CollectSurfaces(child, surfaces);
        }
    }

    private static void CollectBarriers(Node node, List<Barrier> barriers)
    {
        if (node is StaticBody3D body && body.CollisionLayer != 0 && IsEdgeBarrier(body.Name.ToString()))
        {
            foreach (CollisionShape3D collision in body.GetChildren().OfType<CollisionShape3D>())
            {
                if (collision.Disabled || collision.Shape is not BoxShape3D box)
                {
                    continue;
                }

                if (IsBarrierBox(body.Name.ToString(), box))
                {
                    Vector3 supportedBaseSize = body.HasMeta(RoomGeometry.BarrierBaseSeamSizeMetadata)
                        ? body.GetMeta(RoomGeometry.BarrierBaseSeamSizeMetadata).AsVector3()
                        : box.Size;
                    Vector3 guideSize = body.HasMeta(RoomGeometry.GeneratedPlatformWallGuideSizeMetadata)
                        ? body.GetMeta(RoomGeometry.GeneratedPlatformWallGuideSizeMetadata).AsVector3()
                        : box.Size;
                    Vector3 supportSize = new(guideSize.X, supportedBaseSize.Y, guideSize.Z);
                    Vector3 visualOffset = body.HasMeta(RoomGeometry.BarrierBaseSeamOffsetMetadata)
                        ? body.GetMeta(RoomGeometry.BarrierBaseSeamOffsetMetadata).AsVector3()
                        : Vector3.Zero;
                    barriers.Add(new Barrier(body, supportSize, supportedBaseSize, visualOffset));
                    break;
                }
            }
        }

        foreach (Node child in node.GetChildren())
        {
            CollectBarriers(child, barriers);
        }
    }

    private static bool IsEdgeBarrier(string name)
    {
        string[] fragments = { "Rail", "SideWall", "Guard", "Kerb", "Rim" };
        return fragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsBarrierBox(string name, BoxShape3D box)
    {
        float longHorizontal = Mathf.Max(box.Size.X, box.Size.Z);
        float shortHorizontal = Mathf.Min(box.Size.X, box.Size.Z);
        float minimumLength = name.StartsWith("GeneratedRailJoin", StringComparison.Ordinal) ||
            name.Contains("StepRail", StringComparison.OrdinalIgnoreCase)
            ? 0.2f
            : 1.0f;
        return IsEdgeBarrier(name) &&
            longHorizontal >= minimumLength &&
            shortHorizontal <= 0.8f &&
            box.Size.Y >= 0.35f;
    }

    private static bool IsRollingSurface(StaticBody3D body, BoxShape3D shape)
    {
        if (shape.Size.X < 0.7f || shape.Size.Z < 0.7f ||
            shape.Size.Y > Mathf.Min(shape.Size.X, shape.Size.Z))
        {
            return false;
        }

        Vector3 topNormal = (body.GlobalTransform.Basis * Vector3.Up).Normalized();
        return topNormal.Dot(Vector3.Up) >= 0.62f;
    }

    private static bool TryFindAdjoiningSeam(Surface first, Surface second, out Seam seam)
    {
        seam = default;
        bool found = false;
        float bestScore = float.PositiveInfinity;
        foreach (Edge firstEdge in GetTopEdges(first))
        {
            foreach (Edge secondEdge in GetTopEdges(second))
            {
                if (firstEdge.Outward.Dot(secondEdge.Outward) > -0.92f ||
                    !TryMeasureEdges(
                        firstEdge,
                        secondEdge,
                        first.IsSloped || second.IsSloped,
                        out float signedGap,
                        out float heightDifference,
                        out float overlapLength))
                {
                    continue;
                }

                float score = Mathf.Abs(signedGap) + heightDifference;
                if (!found || score < bestScore)
                {
                    found = true;
                    bestScore = score;
                    seam = new Seam(first, second, signedGap, heightDifference, overlapLength);
                }
            }
        }

        if (TryMeasureEmbeddedSlopeEndpoint(first, second, out Seam firstEmbedded))
        {
            float score = firstEmbedded.MaximumHeightDifference;
            if (!found || score < bestScore)
            {
                found = true;
                bestScore = score;
                seam = firstEmbedded;
            }
        }

        if (TryMeasureEmbeddedSlopeEndpoint(second, first, out Seam secondEmbedded))
        {
            float score = secondEmbedded.MaximumHeightDifference;
            if (!found || score < bestScore)
            {
                found = true;
                seam = secondEmbedded;
            }
        }

        return found;
    }

    private static bool TryMeasureEmbeddedSlopeEndpoint(Surface slope, Surface flat, out Seam seam)
    {
        seam = default;
        if (!slope.IsSloped)
        {
            return false;
        }

        bool found = false;
        float bestStep = float.PositiveInfinity;
        float flatTop = flat.Shape.Size.Y * 0.5f;
        foreach (Edge edge in GetTopEdges(slope))
        {
            Vector3 localStart = flat.Collision.ToLocal(edge.Start);
            Vector3 localEnd = flat.Collision.ToLocal(edge.End);
            Vector3 localMidpoint = (localStart + localEnd) * 0.5f;
            if (Mathf.Abs(localMidpoint.X) > flat.Shape.Size.X * 0.5f + 0.01f ||
                Mathf.Abs(localMidpoint.Z) > flat.Shape.Size.Z * 0.5f + 0.01f)
            {
                continue;
            }

            bool spansX = Mathf.Abs(localEnd.X - localStart.X) >= Mathf.Abs(localEnd.Z - localStart.Z);
            float segmentMinimum = spansX
                ? Mathf.Min(localStart.X, localEnd.X)
                : Mathf.Min(localStart.Z, localEnd.Z);
            float segmentMaximum = spansX
                ? Mathf.Max(localStart.X, localEnd.X)
                : Mathf.Max(localStart.Z, localEnd.Z);
            float flatHalfExtent = spansX ? flat.Shape.Size.X * 0.5f : flat.Shape.Size.Z * 0.5f;
            float overlap = Mathf.Min(segmentMaximum, flatHalfExtent) - Mathf.Max(segmentMinimum, -flatHalfExtent);
            float segmentLength = segmentMaximum - segmentMinimum;
            if (segmentLength <= 0.001f || overlap < segmentLength * 0.6f)
            {
                continue;
            }

            float step = Mathf.Max(
                Mathf.Abs(localStart.Y - flatTop),
                Mathf.Max(Mathf.Abs(localMidpoint.Y - flatTop), Mathf.Abs(localEnd.Y - flatTop)));
            if (!found || step < bestStep)
            {
                found = true;
                bestStep = step;
                seam = new Seam(slope, flat, 0.0f, step, overlap);
            }
        }

        return found;
    }

    private static Edge[] GetTopEdges(Surface surface)
    {
        Transform3D transform = surface.Collision.GlobalTransform;
        Vector3 center = transform * (Vector3.Up * surface.Shape.Size.Y * 0.5f);
        Vector3 halfX = transform.Basis * (Vector3.Right * surface.Shape.Size.X * 0.5f);
        Vector3 halfZ = transform.Basis * (Vector3.Back * surface.Shape.Size.Z * 0.5f);
        Vector3 corner00 = center - halfX - halfZ;
        Vector3 corner10 = center + halfX - halfZ;
        Vector3 corner11 = center + halfX + halfZ;
        Vector3 corner01 = center - halfX + halfZ;
        Vector2 xOutward = Horizontal(transform.Basis * Vector3.Right).Normalized();
        Vector2 zOutward = Horizontal(transform.Basis * Vector3.Back).Normalized();
        return new[]
        {
            new Edge(corner00, corner10, -zOutward),
            new Edge(corner10, corner11, xOutward),
            new Edge(corner11, corner01, zOutward),
            new Edge(corner01, corner00, -xOutward),
        };
    }

    private static bool TryMeasureEdges(
        Edge first,
        Edge second,
        bool includesSlope,
        out float signedGap,
        out float maximumHeightDifference,
        out float overlapLength)
    {
        signedGap = float.PositiveInfinity;
        maximumHeightDifference = float.PositiveInfinity;
        overlapLength = 0.0f;
        Vector2 firstStart = Horizontal(first.Start);
        Vector2 firstEnd = Horizontal(first.End);
        Vector2 secondStart = Horizontal(second.Start);
        Vector2 secondEnd = Horizontal(second.End);
        Vector2 tangent = (firstEnd - firstStart).Normalized();
        Vector2 secondTangent = (secondEnd - secondStart).Normalized();
        if (Mathf.Abs(tangent.Dot(secondTangent)) < 0.97f)
        {
            return false;
        }

        float firstMinimum = Mathf.Min(firstStart.Dot(tangent), firstEnd.Dot(tangent));
        float firstMaximum = Mathf.Max(firstStart.Dot(tangent), firstEnd.Dot(tangent));
        float secondMinimum = Mathf.Min(secondStart.Dot(tangent), secondEnd.Dot(tangent));
        float secondMaximum = Mathf.Max(secondStart.Dot(tangent), secondEnd.Dot(tangent));
        float overlapMinimum = Mathf.Max(firstMinimum, secondMinimum);
        float overlapMaximum = Mathf.Min(firstMaximum, secondMaximum);
        overlapLength = overlapMaximum - overlapMinimum;
        float shorterLength = Mathf.Min(firstMaximum - firstMinimum, secondMaximum - secondMinimum);
        float requiredOverlap = shorterLength * 0.6f;
        if (overlapLength < requiredOverlap)
        {
            return false;
        }

        Vector2 firstMidpoint = (firstStart + firstEnd) * 0.5f;
        Vector2 secondMidpoint = (secondStart + secondEnd) * 0.5f;
        signedGap = (secondMidpoint - firstMidpoint).Dot(first.Outward);
        float maximumOverlap = includesSlope ? 6.0f : DetectionGap;
        if (signedGap > DetectionGap || signedGap < -maximumOverlap)
        {
            return false;
        }

        maximumHeightDifference = 0.0f;
        foreach (float projection in new[] { overlapMinimum, (overlapMinimum + overlapMaximum) * 0.5f, overlapMaximum })
        {
            float firstHeight = HeightAtProjection(first, tangent, projection);
            float secondHeight = HeightAtProjection(second, tangent, projection);
            maximumHeightDifference = Mathf.Max(maximumHeightDifference, Mathf.Abs(firstHeight - secondHeight));
        }

        return maximumHeightDifference <= DetectionHeight;
    }

    private static float HeightAtProjection(Edge edge, Vector2 tangent, float projection)
    {
        float startProjection = Horizontal(edge.Start).Dot(tangent);
        float endProjection = Horizontal(edge.End).Dot(tangent);
        float denominator = endProjection - startProjection;
        if (Mathf.Abs(denominator) <= 0.0001f)
        {
            return (edge.Start.Y + edge.End.Y) * 0.5f;
        }

        float weight = Mathf.Clamp((projection - startProjection) / denominator, 0.0f, 1.0f);
        return Mathf.Lerp(edge.Start.Y, edge.End.Y, weight);
    }

    private static Vector2 Horizontal(Vector3 value) => new(value.X, value.Z);

    private static bool IsExcluded(string name)
    {
        string[] excluded =
        {
            "Wall", "Ceiling", "Beam", "Frame", "Rim", "Stop", "Barrier", "Divider", "Kerb",
            "Pillar", "Post", "Hazard", "Gate", "Slat", "Arm", "Mount", "Brace", "Tooth", "Pocket",
            "DoorLeaf", "Handle", "Marker", "Latch", "Rib", "Blade", "Cable", "Mass", "Counterweight",
        };
        return excluded.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
