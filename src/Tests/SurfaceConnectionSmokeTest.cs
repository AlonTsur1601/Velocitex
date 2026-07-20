using Godot;
using Velocitex.Gameplay.Player;

namespace Velocitex.Tests;

public partial class SurfaceConnectionSmokeTest : Node
{
    private const float DetectionGap = 0.7f;
    private const float DetectionHeight = 2.0f;
    private const float MaximumSeamGap = 0.01f;
    private const float MaximumSeamStep = 0.01f;

    private readonly record struct Surface(StaticBody3D Body, BoxShape3D Shape, bool IsSloped);
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
            AuditResult result = AuditRoom(room, roomRoot);
            issueCount += result.IssueCount;
            if (result.IssueCount == 0)
            {
                GD.Print(
                    $"SURFACE_CONNECTION_ROOM_PASS: Room {room:00} audited {result.SurfaceCount} rolling surfaces and " +
                    $"{result.SeamCount} adjoining seams; worst gap={result.WorstGap:F3} m, worst step={result.WorstStep:F3} m, " +
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

    private static int[] ResolveRequestedRooms()
    {
        string? requested = OS.GetCmdlineUserArgs()
            .FirstOrDefault(argument => argument.StartsWith("--surface-room=", StringComparison.Ordinal));
        if (requested is not null &&
            int.TryParse(requested["--surface-room=".Length..], out int room) &&
            room is >= 1 and <= 28)
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
        float WorstGap,
        float WorstStep,
        float StartWallGap);

    private static AuditResult AuditRoom(int room, Node root)
    {
        List<Surface> surfaces = new();
        CollectSurfaces(root, surfaces);
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

        return new AuditResult(issues, surfaces.Count, seams.Count, worstGap, worstStep, startWallGap);
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
                if (collision.Disabled || collision.Shape is not BoxShape3D box || !IsRollingSurface(body, box))
                {
                    continue;
                }

                float up = Mathf.Abs((body.GlobalTransform.Basis * Vector3.Up).Normalized().Dot(Vector3.Up));
                surfaces.Add(new Surface(body, box, up < 0.9995f));
                break;
            }
        }

        foreach (Node child in node.GetChildren())
        {
            CollectSurfaces(child, surfaces);
        }
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
            Vector3 localStart = flat.Body.ToLocal(edge.Start);
            Vector3 localEnd = flat.Body.ToLocal(edge.End);
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
        Transform3D transform = surface.Body.GlobalTransform;
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
            "Rail", "Wall", "Ceiling", "Beam", "Frame", "Rim", "Stop", "Barrier", "Divider", "Kerb",
            "Pillar", "Post", "Hazard", "Gate", "Slat", "Arm", "Mount", "Fin", "Brace", "Tooth", "Pocket",
            "DoorLeaf", "Handle", "Marker", "Latch", "Rib", "Blade", "Cable", "Mass", "Counterweight",
        };
        return excluded.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
