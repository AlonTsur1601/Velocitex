using Godot;
using Velocitex.Core.Physics;
using Velocitex.Gameplay.Physics;
using Velocitex.Gameplay.Visual;

namespace Velocitex.Gameplay.Rooms;

internal enum PlatformWallEdge
{
    Left,
    Right,
    Front,
    Back,
}

internal readonly record struct PlatformWallSelection(
    string SurfaceName,
    PlatformWallEdge Edge,
    string WallName,
    float EdgeStart = float.NaN,
    float EdgeEnd = float.NaN);

internal readonly record struct PlatformWallStyle(
    float Thickness,
    float Height,
    string TexturePath,
    Color Tint,
    float Metallic,
    float Roughness,
    float JoinOverlap = 0.04f);

    internal static class RoomGeometry
    {
    private readonly record struct WallEndOverlap(float Negative, float Positive)
    {
        public float Total => Negative + Positive;
        public float CenterOffset => (Positive - Negative) * 0.5f;
    }

    private const string StickyMaterialPath = "res://resources/materials/sticky_caramel.tres";
    private const string AcceleratorMaterialPath = "res://resources/materials/accelerator_belt.tres";
    private const string SuperElasticMaterialPath = "res://resources/materials/super_elastic_membrane.tres";
    private const string FrictionlessTexturePath = "res://assets/textures/frictionless_glass.svg";
    private const string AbsorbingTexturePath = "res://assets/textures/absorbing_foam.svg";
    private const string OneWayGripTexturePath = "res://assets/textures/one_way_teeth.svg";
    private const float StandardGeneratedWallHeight = 1.10f;
    private const float MaximumGeneratedWallEndOverlap = 0.90f;
    public const string StickyRollSfxMetadata = "sticky_roll_sfx";
    public const string BarrierBaseSeamSizeMetadata = "barrier_base_seam_size";
    public const string BarrierBaseSeamOffsetMetadata = "barrier_base_seam_offset";
    public const string GeneratedPlatformWallMetadata = "generated_platform_wall";
    public const string GeneratedPlatformWallSurfaceMetadata = "generated_platform_wall_surface";
    public const string GeneratedPlatformWallGuideSizeMetadata = "generated_platform_wall_guide_size";
    private const string GeneratedWallRoomMaterialMetadata = "generated_wall_room_material";
    public static Color SequenceButtonFrameTint => new("a96f50");

    public static StaticBody3D AddBox(
        Node parent,
        string name,
        Vector3 size,
        Vector3 position,
        Vector3 rotation,
        string texturePath,
        Color tint,
        float metallic,
        float roughness,
        float friction = 1.0f,
        float bounce = 0.0f,
        bool castShadow = true,
        SurfaceProfile? surfaceProfile = null,
        Material? materialOverride = null,
        float edgeBarrierEndOverlap = 0.55f,
        float edgeBarrierLongOffset = 0.0f)
    {
        string resolvedTexturePath = ResolveSurfaceTexture(name, size, texturePath);
        SurfaceProfile? effectiveProfile = surfaceProfile;
        StaticBody3D body = effectiveProfile is null
            ? new StaticBody3D()
            : new ProfiledSurfaceBody { Profile = effectiveProfile };
        body.Name = name;
        body.Position = position;
        body.Rotation = rotation;
        body.PhysicsMaterialOverride = new PhysicsMaterial
        {
            Friction = effectiveProfile?.Friction ?? friction,
            Bounce = bounce,
        };
        bool usesStickyRollSfx = effectiveProfile?.Kind == SurfaceKind.Sticky;
        body.SetMeta(StickyRollSfxMetadata, usesStickyRollSfx);
        Material surfaceMaterial = ResolveProfiledSurfaceMaterial(
            size,
            resolvedTexturePath,
            tint,
            metallic,
            roughness,
            effectiveProfile,
            materialOverride);
        bool edgeBarrier = IsEdgeBarrier(name, size);
        bool barrierRunsAlongX = size.X >= size.Z;
        float generatedWallHeight = edgeBarrier
            ? Mathf.Max(size.Y, StandardGeneratedWallHeight)
            : size.Y;
        Vector3 visualSize = edgeBarrier
            ? new Vector3(
                size.X + (barrierRunsAlongX ? edgeBarrierEndOverlap : 0.0f),
                generatedWallHeight + 0.12f,
                size.Z + (barrierRunsAlongX ? 0.0f : edgeBarrierEndOverlap))
            : size;
        float wallHeightOffset = edgeBarrier
            ? ((generatedWallHeight - size.Y) * 0.5f) - 0.06f
            : 0.0f;
        Vector3 visualOffset = edgeBarrier
            ? (barrierRunsAlongX
                ? new Vector3(edgeBarrierLongOffset, wallHeightOffset, 0.0f)
                : new Vector3(0.0f, wallHeightOffset, edgeBarrierLongOffset))
            : Vector3.Zero;
        if (edgeBarrier)
        {
            ClampBarrierOverlapToShell(parent, position, rotation, barrierRunsAlongX, ref visualSize, ref visualOffset);
        }
        body.AddChild(new MeshInstance3D
        {
            Position = visualOffset,
            Mesh = SurfaceMeshFactory.CreateTiledBox(visualSize),
            MaterialOverride = surfaceMaterial,
            CastShadow = castShadow
                ? GeometryInstance3D.ShadowCastingSetting.On
                : GeometryInstance3D.ShadowCastingSetting.Off,
        });
        if (edgeBarrier)
        {
            body.SetMeta(BarrierBaseSeamSizeMetadata, visualSize);
            body.SetMeta(BarrierBaseSeamOffsetMetadata, visualOffset);
        }
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = size },
        });
        SurfaceDetail.AddBoxWear(body, name, size, resolvedTexturePath);
        parent.AddChild(body);
        return body;
    }

    public static StaticBody3D AddWall(
        Node parent,
        string name,
        Vector3 size,
        Vector3 position,
        Vector3 rotation,
        string texturePath,
        Color tint,
        float metallic,
        float roughness,
        float friction = 1.0f,
        float bounce = 0.0f,
        bool castShadow = true,
        Material? materialOverride = null,
        float endJoinAllowance = 0.04f)
    {
        if (parent.GetNodeOrNull<StaticBody3D>($"EditableWalls/{name}") is StaticBody3D editableWall)
        {
            return editableWall;
        }

        if (!IsEdgeBarrier(name, size))
        {
            throw new ArgumentException(
                $"Generated wall '{name}' must be a long, narrow Rail, SideWall, Guard, Kerb, or Rim.",
                nameof(size));
        }

        Basis authoredBasis = Basis.FromEuler(rotation);
        Vector3 generatedWallSize = new(size.X, StandardGeneratedWallHeight, size.Z);
        Vector3 generatedWallPosition = position +
            (authoredBasis * (Vector3.Up * ((StandardGeneratedWallHeight - size.Y) * 0.5f)));
        Vector3 generatedVisualPosition = generatedWallPosition;
        string supportSurface = string.Empty;
        TrySnapWallGuideToPlatformEdge(
            parent,
            name,
            generatedWallSize,
            generatedWallPosition,
            rotation,
            out generatedVisualPosition,
            out supportSurface);
        WallEndOverlap generatedEndOverlap = CalculateWallEndOverlap(
            parent,
            generatedWallSize,
            generatedVisualPosition,
            rotation,
            endJoinAllowance);

        Material? roomWallMaterial = materialOverride;
        if (parent.HasMeta(GeneratedWallRoomMaterialMetadata))
        {
            roomWallMaterial = parent.GetMeta(GeneratedWallRoomMaterialMetadata).AsGodotObject() as Material;
        }

        StaticBody3D wall = AddBox(
            parent,
            name,
            generatedWallSize,
            generatedWallPosition,
            rotation,
            texturePath,
            tint,
            metallic,
            roughness,
            friction,
            bounce,
            castShadow,
            materialOverride: roomWallMaterial,
            edgeBarrierEndOverlap: generatedEndOverlap.Total,
            edgeBarrierLongOffset: generatedEndOverlap.CenterOffset);
        if (!generatedVisualPosition.IsEqualApprox(generatedWallPosition) &&
            wall.GetChildren().OfType<MeshInstance3D>().FirstOrDefault() is MeshInstance3D wallVisual)
        {
            Basis wallBasis = Basis.FromEuler(rotation);
            wallVisual.Position += wallBasis.Inverse() * (generatedVisualPosition - generatedWallPosition);
            wall.SetMeta(BarrierBaseSeamOffsetMetadata, wallVisual.Position);
        }
        MeshInstance3D generatedVisual = wall.GetChildren().OfType<MeshInstance3D>().First();
        if (!parent.HasMeta(GeneratedWallRoomMaterialMetadata) && generatedVisual.MaterialOverride is Material generatedMaterial)
        {
            parent.SetMeta(GeneratedWallRoomMaterialMetadata, generatedMaterial);
        }
        Vector3 generatedVisualSize = wall.GetMeta(BarrierBaseSeamSizeMetadata).AsVector3();
        if (Mathf.Abs(rotation.X) > 0.001f || Mathf.Abs(rotation.Z) > 0.001f)
        {
            // The lower edge follows the sloped platform, while the wall rises
            // in world-space Up.  Rotating a rectangular wall with the deck
            // shortens its apparent height and creates half-height joints when
            // it reaches a level wall.
            generatedVisual.Mesh = SurfaceMeshFactory.CreateTiledVerticalWall(
                generatedVisualSize,
                authoredBasis);
        }
        CollisionShape3D generatedHitbox = wall.GetChildren().OfType<CollisionShape3D>().First();
        generatedHitbox.Name = "GeneratedWallHitbox";
        generatedHitbox.Position = generatedVisual.Position;
        ((BoxShape3D)generatedHitbox.Shape).Size = generatedVisualSize;
        TrimGeneratedWallAwayFromPlatformInteriors(parent, wall, supportSurface);
        wall.SetMeta(GeneratedPlatformWallMetadata, true);
        wall.SetMeta(GeneratedPlatformWallGuideSizeMetadata, generatedWallSize);
        wall.SetMeta(
            GeneratedPlatformWallSurfaceMetadata,
            string.IsNullOrEmpty(supportSurface) ? "structural-join" : supportSurface);
        return wall;
    }

    private static void TrimGeneratedWallAwayFromPlatformInteriors(
        Node parent,
        StaticBody3D wall,
        string supportSurfaceName)
    {
        if (string.IsNullOrEmpty(supportSurfaceName))
        {
            return;
        }

        MeshInstance3D visual = wall.GetChildren().OfType<MeshInstance3D>().First();
        CollisionShape3D hitbox = wall.GetNode<CollisionShape3D>("GeneratedWallHitbox");
        BoxShape3D hitboxBox = (BoxShape3D)hitbox.Shape;
        Vector3 size = hitboxBox.Size;
        bool runsAlongX = size.X >= size.Z;
        Vector3 axis = runsAlongX ? Vector3.Right : Vector3.Back;
        float length = runsAlongX ? size.X : size.Z;
        float negativeTrim = MeasureIntrudingEndTrim(
            parent, wall, visual.Position, size, axis, -1.0f, supportSurfaceName);
        float positiveTrim = MeasureIntrudingEndTrim(
            parent, wall, visual.Position, size, axis, 1.0f, supportSurfaceName);
        float trimmedLength = length - negativeTrim - positiveTrim;
        if (trimmedLength < 0.20f ||
            (negativeTrim <= 0.001f && positiveTrim <= 0.001f))
        {
            return;
        }

        Vector3 trimmedSize = size;
        if (runsAlongX) { trimmedSize.X = trimmedLength; }
        else { trimmedSize.Z = trimmedLength; }
        Vector3 trimmedOffset = visual.Position +
            (axis * ((negativeTrim - positiveTrim) * 0.5f));
        visual.Position = trimmedOffset;
        visual.Mesh = SurfaceMeshFactory.CreateTiledBox(trimmedSize);
        hitbox.Position = trimmedOffset;
        hitboxBox.Size = trimmedSize;
        wall.SetMeta(BarrierBaseSeamSizeMetadata, trimmedSize);
        wall.SetMeta(BarrierBaseSeamOffsetMetadata, trimmedOffset);
    }

    private static float MeasureIntrudingEndTrim(
        Node parent,
        StaticBody3D wall,
        Vector3 visualOffset,
        Vector3 visualSize,
        Vector3 axis,
        float direction,
        string supportSurfaceName)
    {
        float length = Mathf.Max(visualSize.X, visualSize.Z);
        const float sampleStep = 0.04f;
        for (float trim = 0.0f; trim < length - 0.20f; trim += sampleStep)
        {
            Vector3 localBase = visualOffset - (Vector3.Up * (visualSize.Y * 0.5f)) +
                (axis * direction * ((length * 0.5f) - trim));
            Vector3 worldBase = wall.ToGlobal(localBase);
            if (!IsInsideOtherPlatformInterior(parent, worldBase, supportSurfaceName, visualSize.Y))
            {
                return trim;
            }
        }

        return 0.0f;
    }

    private static bool IsInsideOtherPlatformInterior(
        Node parent,
        Vector3 worldPoint,
        string supportSurfaceName,
        float wallHeight)
    {
        foreach (StaticBody3D surface in EnumerateDescendants(parent).OfType<StaticBody3D>())
        {
            if (surface.Name.ToString() == supportSurfaceName || !CanSupportGeneratedWall(surface))
            {
                continue;
            }

            foreach (CollisionShape3D collision in surface.GetChildren().OfType<CollisionShape3D>())
            {
                if (collision.Disabled || collision.Shape is not BoxShape3D box)
                {
                    continue;
                }

                Vector3 local = collision.ToLocal(worldPoint);
                Vector3 half = box.Size * 0.5f;
                const float interiorMargin = 0.04f;
                bool insideFootprint = Mathf.Abs(local.X) < half.X - interiorMargin &&
                    Mathf.Abs(local.Z) < half.Z - interiorMargin;
                bool nearTop = local.Y >= half.Y - 0.08f &&
                    local.Y <= half.Y + wallHeight + 0.08f;
                if (insideFootprint && nearTop)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void CloseNearbyGeneratedWallGaps(Node parent, StaticBody3D wall)
    {
        (Vector3 wallStart, Vector3 wallEnd, Vector3 wallAxis, float wallThickness) = GetGeneratedWallSpan(wall);
        foreach (StaticBody3D other in EnumerateDescendants(parent).OfType<StaticBody3D>().ToArray())
        {
            if (other == wall || !other.HasMeta(GeneratedPlatformWallMetadata) ||
                other.Name.ToString().StartsWith("GeneratedRailJoin", StringComparison.Ordinal))
            {
                continue;
            }

            (Vector3 otherStart, Vector3 otherEnd, Vector3 otherAxis, float otherThickness) = GetGeneratedWallSpan(other);
            (Vector3 A, Vector3 B)[] endpointPairs =
            {
                (wallStart, otherStart),
                (wallStart, otherEnd),
                (wallEnd, otherStart),
                (wallEnd, otherEnd),
            };
            (Vector3 A, Vector3 B) closest = endpointPairs
                .OrderBy(pair => pair.A.DistanceSquaredTo(pair.B))
                .First();
            Vector3 gapVector = closest.B - closest.A;
            float gap = gapVector.Length();
            if (gap <= 0.025f || gap > 0.90f)
            {
                continue;
            }

            // Extend the two existing wall bodies to a common endpoint.  A
            // separate connector box creates the visible teeth/protrusions at
            // corners and can inherit a different material or height.
            Vector3 meetingPoint = (closest.A + closest.B) * 0.5f;
            ExtendGeneratedWallEnd(wall, closest.A, meetingPoint, wallThickness);
            ExtendGeneratedWallEnd(other, closest.B, meetingPoint, otherThickness);
        }
    }

    private static void ExtendGeneratedWallEnd(
        StaticBody3D wall,
        Vector3 oldEndpoint,
        Vector3 target,
        float thickness)
    {
        CollisionShape3D hitbox = wall.GetNode<CollisionShape3D>("GeneratedWallHitbox");
        BoxShape3D box = (BoxShape3D)hitbox.Shape;
        bool runsAlongX = box.Size.X >= box.Size.Z;
        Vector3 localAxis = runsAlongX ? Vector3.Right : Vector3.Back;
        Vector3 worldAxis = (wall.GlobalTransform.Basis * localAxis).Normalized();
        Vector3 center = wall.ToGlobal(hitbox.Position);
        float signedEnd = Mathf.Sign((oldEndpoint - center).Dot(worldAxis));
        float extension = Mathf.Max(0.0f, (target - oldEndpoint).Dot(worldAxis) * signedEnd);
        if (extension <= 0.001f)
        {
            return;
        }

        float oldLength = runsAlongX ? box.Size.X : box.Size.Z;
        float newLength = oldLength + extension + (thickness * 0.5f);
        Vector3 newWorldCenter = center + (worldAxis * signedEnd * ((newLength - oldLength) * 0.5f));
        Vector3 newSize = box.Size;
        if (runsAlongX) { newSize.X = newLength; }
        else { newSize.Z = newLength; }
        hitbox.Position = wall.ToLocal(newWorldCenter);
        box.Size = newSize;

        MeshInstance3D visual = wall.GetChildren().OfType<MeshInstance3D>().First();
        visual.Position = hitbox.Position;
        Basis authoredBasis = wall.GlobalTransform.Basis;
        visual.Mesh = Mathf.Abs(wall.Rotation.X) > 0.001f || Mathf.Abs(wall.Rotation.Z) > 0.001f
            ? SurfaceMeshFactory.CreateTiledVerticalWall(newSize, authoredBasis)
            : SurfaceMeshFactory.CreateTiledBox(newSize);
        wall.SetMeta(BarrierBaseSeamSizeMetadata, newSize);
        wall.SetMeta(BarrierBaseSeamOffsetMetadata, hitbox.Position);
    }

    private static (Vector3 Start, Vector3 End, Vector3 Axis, float Thickness) GetGeneratedWallSpan(
        StaticBody3D wall)
    {
        CollisionShape3D collision = wall.GetNode<CollisionShape3D>("GeneratedWallHitbox");
        BoxShape3D box = (BoxShape3D)collision.Shape;
        bool runsAlongX = box.Size.X >= box.Size.Z;
        Vector3 localAxis = runsAlongX ? Vector3.Right : Vector3.Back;
        float length = runsAlongX ? box.Size.X : box.Size.Z;
        float thickness = runsAlongX ? box.Size.Z : box.Size.X;
        Vector3 center = wall.ToGlobal(collision.Position);
        Vector3 axis = (wall.GlobalTransform.Basis * localAxis).Normalized();
        Vector3 extent = axis * (length * 0.5f);
        return (center - extent, center + extent, axis, thickness);
    }

    private static void CloseNearbyStructuralWallGaps(Node parent, StaticBody3D wall)
    {
        (Vector3 wallStart, Vector3 wallEnd, _, float thickness) = GetGeneratedWallSpan(wall);
        foreach ((Vector3 endpoint, string endName) in new[] { (wallStart, "Start"), (wallEnd, "End") })
        {
            float nearestDistance = float.PositiveInfinity;
            Vector3 nearestPoint = Vector3.Zero;
            string nearestName = string.Empty;
            foreach (StaticBody3D body in EnumerateDescendants(parent).OfType<StaticBody3D>())
            {
                if (body == wall || body.HasMeta(GeneratedPlatformWallMetadata))
                {
                    continue;
                }

                string bodyName = body.Name.ToString();
                if (!bodyName.Contains("Wall", StringComparison.OrdinalIgnoreCase) &&
                    !bodyName.Contains("Frame", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (CollisionShape3D collision in body.GetChildren().OfType<CollisionShape3D>())
                {
                    if (collision.Disabled || collision.Shape is not BoxShape3D box)
                    {
                        continue;
                    }

                    Vector3 local = collision.ToLocal(endpoint);
                    Vector3 half = box.Size * 0.5f;
                    Vector3 closestLocal = new(
                        Mathf.Clamp(local.X, -half.X, half.X),
                        Mathf.Clamp(local.Y, -half.Y, half.Y),
                        Mathf.Clamp(local.Z, -half.Z, half.Z));
                    Vector3 closest = collision.ToGlobal(closestLocal);
                    Vector3 horizontalDelta = new(closest.X - endpoint.X, 0.0f, closest.Z - endpoint.Z);
                    float distance = horizontalDelta.Length();
                    if (distance <= 0.025f || distance > 0.90f || distance >= nearestDistance)
                    {
                        continue;
                    }

                    nearestDistance = distance;
                    nearestPoint = closest;
                    nearestName = bodyName;
                }
            }

            if (!float.IsFinite(nearestDistance))
            {
                continue;
            }

            ExtendGeneratedWallEnd(wall, endpoint, nearestPoint, thickness);
        }
    }

    private static WallEndOverlap CalculateWallEndOverlap(
        Node parent,
        Vector3 wallSize,
        Vector3 visualPosition,
        Vector3 rotation,
        float requestedOverlap)
    {
        float baseOverlap = Mathf.Clamp(requestedOverlap * 0.5f, 0.0f, MaximumGeneratedWallEndOverlap * 0.5f);
        if (parent is not Node3D parent3D)
        {
            return new WallEndOverlap(baseOverlap, baseOverlap);
        }

        Basis basis = Basis.FromEuler(rotation);
        bool runsAlongX = wallSize.X >= wallSize.Z;
        Vector3 axis = (basis * (runsAlongX ? Vector3.Right : Vector3.Back)).Normalized();
        float halfLength = (runsAlongX ? wallSize.X : wallSize.Z) * 0.5f;
        float negative = FindRequiredWallEndExtension(parent, parent3D, visualPosition, axis, -1.0f, halfLength, baseOverlap);
        float positive = FindRequiredWallEndExtension(parent, parent3D, visualPosition, axis, 1.0f, halfLength, baseOverlap);
        return new WallEndOverlap(negative, positive);
    }

    private static float FindRequiredWallEndExtension(
        Node parent,
        Node3D parent3D,
        Vector3 center,
        Vector3 axis,
        float endSign,
        float halfLength,
        float baseOverlap)
    {
        Vector3 endpointLocal = center + (axis * endSign * halfLength);
        Vector3 endpointWorld = parent3D.ToGlobal(endpointLocal);
        float best = float.PositiveInfinity;
        foreach (StaticBody3D body in EnumerateDescendants(parent).OfType<StaticBody3D>())
        {
            if (!body.HasMeta(GeneratedPlatformWallMetadata) &&
                !body.Name.ToString().Contains("Wall", StringComparison.OrdinalIgnoreCase) &&
                !body.Name.ToString().Contains("Frame", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (CollisionShape3D collision in body.GetChildren().OfType<CollisionShape3D>())
            {
                if (collision.Disabled || collision.Shape is not BoxShape3D box)
                {
                    continue;
                }
                Vector3 local = collision.ToLocal(endpointWorld);
                Vector3 half = box.Size * 0.5f;
                Vector3 closestLocal = new(
                    Mathf.Clamp(local.X, -half.X, half.X),
                    Mathf.Clamp(local.Y, -half.Y, half.Y),
                    Mathf.Clamp(local.Z, -half.Z, half.Z));
                Vector3 closestInParent = parent3D.ToLocal(collision.ToGlobal(closestLocal));
                Vector3 delta = closestInParent - endpointLocal;
                float forward = delta.Dot(axis) * endSign;
                float lateral = (delta - (axis * delta.Dot(axis))).Length();
                if (forward <= 0.001f || forward > MaximumGeneratedWallEndOverlap || lateral > 0.48f)
                {
                    continue;
                }
                best = Mathf.Min(best, forward + 0.025f);
            }
        }

        return float.IsFinite(best)
            ? Mathf.Clamp(Mathf.Max(baseOverlap, best), 0.0f, MaximumGeneratedWallEndOverlap)
            : baseOverlap;
    }

    private static bool TrySnapWallGuideToPlatformEdge(
        Node parent,
        string wallName,
        Vector3 wallSize,
        Vector3 requestedPosition,
        Vector3 requestedRotation,
        out Vector3 generatedPosition,
        out string supportSurfaceName)
    {
        generatedPosition = requestedPosition;
        supportSurfaceName = string.Empty;
        if (wallName.StartsWith("GeneratedRailJoin", StringComparison.Ordinal))
        {
            return false;
        }

        Basis wallBasis = Basis.FromEuler(requestedRotation);
        bool wallRunsAlongLocalX = wallSize.X >= wallSize.Z;
        Vector3 wallLongAxis = (wallBasis * (wallRunsAlongLocalX ? Vector3.Right : Vector3.Forward)).Normalized();
        float wallThickness = wallRunsAlongLocalX ? wallSize.Z : wallSize.X;
        float bestScore = float.PositiveInfinity;
        float bestEndpointOverrun = float.PositiveInfinity;
        Vector3 bestPosition = requestedPosition;
        string bestSurface = string.Empty;

        foreach (StaticBody3D surface in EnumerateDescendants(parent).OfType<StaticBody3D>())
        {
            if (!CanSupportGeneratedWall(surface))
            {
                continue;
            }

            foreach (CollisionShape3D collision in surface.GetChildren().OfType<CollisionShape3D>())
            {
                if (collision.Disabled || collision.Shape is not BoxShape3D platformBox ||
                    platformBox.Size.X < 0.7f || platformBox.Size.Z < 0.7f)
                {
                    continue;
                }

                Transform3D platformTransform = surface.Transform * collision.Transform;
                Basis inverseBasis = platformTransform.Basis.Inverse();
                Vector3 localGuide = platformTransform.AffineInverse() * requestedPosition;
                Vector3 localLongAxis = (inverseBasis * wallLongAxis).Normalized();
                bool followsPlatformX = Mathf.Abs(localLongAxis.X) >= Mathf.Abs(localLongAxis.Z);
                float alignment = followsPlatformX ? Mathf.Abs(localLongAxis.X) : Mathf.Abs(localLongAxis.Z);
                if (alignment < 0.94f)
                {
                    continue;
                }

                float lateralGuide = followsPlatformX ? localGuide.Z : localGuide.X;
                float platformHalfWidth = (followsPlatformX ? platformBox.Size.Z : platformBox.Size.X) * 0.5f;
                float side = lateralGuide < 0.0f ? -1.0f : 1.0f;
                float targetLateral = side * (platformHalfWidth + (wallThickness * 0.5f));
                bool embeddedRim = wallName.Contains("Rim", StringComparison.OrdinalIgnoreCase);
                float targetY = embeddedRim
                    ? localGuide.Y
                    : (platformBox.Size.Y + wallSize.Y) * 0.5f;
                float lateralError = Mathf.Abs(lateralGuide - targetLateral);
                float verticalError = Mathf.Abs(localGuide.Y - targetY);
                float longCoordinate = followsPlatformX ? localGuide.X : localGuide.Z;
                float platformHalfLength = (followsPlatformX ? platformBox.Size.X : platformBox.Size.Z) * 0.5f;
                float wallHalfLength = (wallRunsAlongLocalX ? wallSize.X : wallSize.Z) * 0.5f;
                float longitudinalOverrun = Mathf.Max(Mathf.Abs(longCoordinate) - platformHalfLength, 0.0f);
                float endpointOverrun = Mathf.Max(
                    Mathf.Abs(longCoordinate) + wallHalfLength - platformHalfLength,
                    0.0f);
                float normalizedCenterOffset = Mathf.Abs(longCoordinate) / Mathf.Max(platformHalfLength, 0.01f);
                float score = lateralError + verticalError +
                    (longitudinalOverrun * 1.5f) +
                    (endpointOverrun * 0.35f) +
                    (normalizedCenterOffset * 0.25f) +
                    ((1.0f - alignment) * 4.0f);
                if (score >= bestScore)
                {
                    continue;
                }

                Vector3 snappedLocal = localGuide;
                if (followsPlatformX)
                {
                    snappedLocal.Z = targetLateral;
                }
                else
                {
                    snappedLocal.X = targetLateral;
                }
                // Never lift an authored guide away from an adjoining slope
                // or platform.  A guide may intentionally overlap its support;
                // the generator only lowers guides that would leave a base gap.
                snappedLocal.Y = embeddedRim
                    ? targetY
                    : Mathf.Min(localGuide.Y, targetY);

                bestScore = score;
                bestEndpointOverrun = endpointOverrun;
                bestPosition = platformTransform * snappedLocal;
                bestSurface = surface.Name.ToString();
            }
        }

        // A route wall guide is intentionally allowed to bridge adjacent
        // platforms, but it must still be close to an actual platform edge.
        // Structural wall-to-wall junction fillers keep their authored guide.
        if (bestScore > 1.25f || string.IsNullOrEmpty(bestSurface))
        {
            return false;
        }

        // A guide that deliberately spans more than one platform cannot be
        // shifted as one rigid visual without lifting one end off its other
        // support. It is still generated and associated with the closest
        // platform, while its authored bridge transform remains unchanged.
        generatedPosition = bestEndpointOverrun <= 0.04f
            ? bestPosition
            : requestedPosition;
        supportSurfaceName = bestSurface;
        return true;
    }

    private static bool CanSupportGeneratedWall(StaticBody3D surface)
    {
        if (surface.HasMeta(GeneratedPlatformWallMetadata))
        {
            return false;
        }

        string name = surface.Name.ToString();
        string[] excludedFragments =
        {
            "Wall", "Rail", "Guard", "Kerb", "Rim", "Hazard", "Ceiling",
            "Frame", "Gate", "Pillar", "Post", "Beam", "Door", "Fan", "Blade",
        };
        return !excludedFragments.Any(fragment =>
            name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<StaticBody3D> AddPlatformWallLayout(
        Node parent,
        IEnumerable<PlatformWallSelection> selections,
        PlatformWallStyle style)
    {
        List<StaticBody3D> walls = new();
        foreach (PlatformWallSelection selection in selections)
        {
            StaticBody3D surface = parent.GetChildren()
                .OfType<StaticBody3D>()
                .FirstOrDefault(candidate => candidate.Name == selection.SurfaceName)
                ?? throw new InvalidOperationException(
                    $"Wall layout references missing surface '{selection.SurfaceName}'.");
            CollisionShape3D collision = surface.GetChildren()
                .OfType<CollisionShape3D>()
                .FirstOrDefault(candidate => candidate.Shape is BoxShape3D)
                ?? throw new InvalidOperationException(
                    $"Wall layout surface '{selection.SurfaceName}' has no box collision.");
            BoxShape3D box = (BoxShape3D)collision.Shape;

            bool sideEdge = selection.Edge is PlatformWallEdge.Left or PlatformWallEdge.Right;
            float halfSpan = (sideEdge ? box.Size.Z : box.Size.X) * 0.5f;
            float start = float.IsNaN(selection.EdgeStart)
                ? -halfSpan
                : Mathf.Clamp(selection.EdgeStart, -halfSpan, halfSpan);
            float end = float.IsNaN(selection.EdgeEnd)
                ? halfSpan
                : Mathf.Clamp(selection.EdgeEnd, -halfSpan, halfSpan);
            if (end <= start + 0.001f)
            {
                throw new InvalidOperationException(
                    $"Wall '{selection.WallName}' has an empty edge range on '{selection.SurfaceName}'.");
            }

            float spanCenter = (start + end) * 0.5f;
            float spanLength = end - start;
            float side = selection.Edge is PlatformWallEdge.Left or PlatformWallEdge.Front ? -1.0f : 1.0f;
            Vector3 localCenter = sideEdge
                ? new Vector3(
                    side * ((box.Size.X + style.Thickness) * 0.5f),
                    (box.Size.Y + style.Height) * 0.5f,
                    spanCenter)
                : new Vector3(
                    spanCenter,
                    (box.Size.Y + style.Height) * 0.5f,
                    side * ((box.Size.Z + style.Thickness) * 0.5f));
            Vector3 wallSize = sideEdge
                ? new Vector3(style.Thickness, style.Height, spanLength)
                : new Vector3(spanLength, style.Height, style.Thickness);
            Vector3 position = surface.Position + (surface.Basis * (collision.Position + localCenter));

            walls.Add(AddWall(
                parent,
                selection.WallName,
                wallSize,
                position,
                surface.Rotation,
                style.TexturePath,
                style.Tint,
                style.Metallic,
                style.Roughness,
                endJoinAllowance: style.JoinOverlap));
        }

        return walls;
    }

    private static bool IsEdgeBarrier(string name, Vector3 size)
    {
        string[] fragments = { "Rail", "SideWall", "Guard", "Kerb", "Rim" };
        float longHorizontal = Mathf.Max(size.X, size.Z);
        float shortHorizontal = Mathf.Min(size.X, size.Z);
        const float minimumLength = 0.2f;
        return fragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase)) &&
            longHorizontal >= minimumLength && shortHorizontal <= 0.8f && size.Y >= 0.35f;
    }

    private static void ClampBarrierOverlapToShell(
        Node parent,
        Vector3 position,
        Vector3 rotation,
        bool runsAlongX,
        ref Vector3 visualSize,
        ref Vector3 visualOffset)
    {
        bool axisAligned = Mathf.Abs(rotation.X) <= 0.001f && Mathf.Abs(rotation.Z) <= 0.001f &&
            Mathf.Abs(Mathf.Sin(rotation.Y)) <= 0.001f;
        if (!axisAligned || parent.GetNodeOrNull<Node3D>("RoomShell") is not Node3D shell)
        {
            return;
        }

        StaticBody3D? leftBody = shell.GetNodeOrNull<StaticBody3D>("LeftWall");
        StaticBody3D? rightBody = shell.GetNodeOrNull<StaticBody3D>("RightWall");
        StaticBody3D? backBody = shell.GetNodeOrNull<StaticBody3D>("BackWall");
        StaticBody3D? exitBody = shell.GetNodeOrNull<StaticBody3D>("ExitWall");
        CollisionShape3D? left = leftBody?.GetChildren().OfType<CollisionShape3D>().FirstOrDefault();
        CollisionShape3D? right = rightBody?.GetChildren().OfType<CollisionShape3D>().FirstOrDefault();
        CollisionShape3D? back = backBody?.GetChildren().OfType<CollisionShape3D>().FirstOrDefault();
        CollisionShape3D? exit = exitBody?.GetChildren().OfType<CollisionShape3D>().FirstOrDefault();
        if (left?.Shape is not BoxShape3D leftBox || right?.Shape is not BoxShape3D rightBox ||
            back?.Shape is not BoxShape3D backBox || exit?.Shape is not BoxShape3D exitBox)
        {
            return;
        }

        float shellMinimum = runsAlongX
            ? shell.Position.X + leftBody!.Position.X + left.Position.X + (leftBox.Size.X * 0.5f)
            : shell.Position.Z + exitBody!.Position.Z + exit.Position.Z + (exitBox.Size.Z * 0.5f);
        float shellMaximum = runsAlongX
            ? shell.Position.X + rightBody!.Position.X + right.Position.X - (rightBox.Size.X * 0.5f)
            : shell.Position.Z + backBody!.Position.Z + back.Position.Z - (backBox.Size.Z * 0.5f);
        float axisDirection = Mathf.Cos(rotation.Y);
        float center = runsAlongX
            ? position.X + (axisDirection * visualOffset.X)
            : position.Z + (axisDirection * visualOffset.Z);
        float length = runsAlongX ? visualSize.X : visualSize.Z;
        float clampedMinimum = Mathf.Max(center - (length * 0.5f), shellMinimum);
        float clampedMaximum = Mathf.Min(center + (length * 0.5f), shellMaximum);
        if (clampedMaximum <= clampedMinimum)
        {
            return;
        }

        float clampedCenterOffset = (((clampedMinimum + clampedMaximum) * 0.5f) -
            (runsAlongX ? position.X : position.Z)) / axisDirection;
        if (runsAlongX)
        {
            visualSize.X = clampedMaximum - clampedMinimum;
            visualOffset.X = clampedCenterOffset;
        }
        else
        {
            visualSize.Z = clampedMaximum - clampedMinimum;
            visualOffset.Z = clampedCenterOffset;
        }
    }

    private static Material ResolveProfiledSurfaceMaterial(
        Vector3 size,
        string texturePath,
        Color tint,
        float metallic,
        float roughness,
        SurfaceProfile? profile,
        Material? requestedMaterial)
    {
        Material material = requestedMaterial ?? CreateMaterial(texturePath, tint, metallic, roughness, size);
        if (profile is null)
        {
            return material;
        }

        material = profile.Kind switch
        {
            SurfaceKind.Frictionless => CreateMaterial(FrictionlessTexturePath, new Color("a9d4df"), 0.06f, 0.2f, size),
            SurfaceKind.Absorbing => requestedMaterial ?? CreateMaterial(AbsorbingTexturePath, new Color("9aa89c"), 0.02f, 0.98f, size),
            SurfaceKind.OneWayGrip => CreateMaterial(OneWayGripTexturePath, new Color("d1cb8b"), 0.34f, 0.68f, size),
            _ => material,
        };

        if (profile.Kind == SurfaceKind.Sticky)
        {
            return DuplicateCanonicalShaderMaterial(StickyMaterialPath, requestedMaterial);
        }

        if (profile.Kind == SurfaceKind.SuperElastic)
        {
            return DuplicateCanonicalShaderMaterial(SuperElasticMaterialPath, requestedMaterial);
        }

        if (profile.Kind == SurfaceKind.Accelerator)
        {
            ShaderMaterial accelerator = DuplicateCanonicalShaderMaterial(AcceleratorMaterialPath, requestedMaterial);
            accelerator.SetShaderParameter(
                "surface_u_span",
                Mathf.Max(0.125f, size.X / SurfaceMeshFactory.DefaultTileWorldSize));
            return accelerator;
        }

        if (profile.Kind == SurfaceKind.OneWayGrip && material is StandardMaterial3D oneWayMaterial)
        {
            StandardMaterial3D centeredOneWayMaterial = (StandardMaterial3D)oneWayMaterial.Duplicate();
            Vector3 uvScale = centeredOneWayMaterial.Uv1Scale;
            uvScale.X = 1.0f / Mathf.Max(0.125f, size.X / SurfaceMeshFactory.DefaultTileWorldSize);
            centeredOneWayMaterial.Uv1Scale = uvScale;
            return centeredOneWayMaterial;
        }

        return material;
    }

    private static ShaderMaterial DuplicateCanonicalShaderMaterial(string path, Material? requestedMaterial)
    {
        ShaderMaterial canonical = (ShaderMaterial)GD.Load<ShaderMaterial>(path).Duplicate();
        if (requestedMaterial is ShaderMaterial requestedShader)
        {
            Variant motionScale = requestedShader.GetShaderParameter("motion_scale");
            if (motionScale.VariantType != Variant.Type.Nil)
            {
                canonical.SetShaderParameter("motion_scale", motionScale);
            }
        }

        return canonical;
    }

    public static void AddSequencePips(MeshInstance3D insetPlate, int count)
    {
        StandardMaterial3D material = CreateMaterial(
            "res://assets/textures/brushed_metal.png",
            new Color("ead9ae"),
            0.22f,
            0.52f,
            emissionEnabled: true,
            emission: new Color("5d4a25"));
        const float spacing = 0.46f;
        float start = -((count - 1) * spacing * 0.5f);
        for (int pip = 0; pip < count; pip++)
        {
            AddCylinder(
                insetPlate,
                $"SequencePip{pip + 1}",
                new Vector3(start + (pip * spacing), 0.065f, 0.0f),
                Vector3.Zero,
                0.14f,
                0.055f,
                material);
        }
    }

    public static MeshInstance3D AddVisualBox(
        Node parent,
        string name,
        Vector3 size,
        Vector3 position,
        Vector3 rotation,
        string texturePath,
        Color tint,
        float metallic,
        float roughness,
        StandardMaterial3D? materialOverride = null)
    {
        string resolvedTexturePath = ResolveSurfaceTexture(name, size, texturePath);
        MeshInstance3D visual = new()
        {
            Name = name,
            Position = position,
            Rotation = rotation,
            Mesh = SurfaceMeshFactory.CreateTiledBox(size),
            MaterialOverride = materialOverride ?? CreateMaterial(resolvedTexturePath, tint, metallic, roughness, size),
        };
        SurfaceDetail.AddBoxWear(visual, name, size, resolvedTexturePath);
        parent.AddChild(visual);
        return visual;
    }

    public static string ResolveSurfaceTexture(string name, Vector3 size, string texturePath)
    {
        if (!texturePath.EndsWith("brushed_metal.png", StringComparison.OrdinalIgnoreCase))
        {
            return texturePath;
        }

        float smallest = Mathf.Min(size.X, Mathf.Min(size.Y, size.Z));
        float middle = size.X + size.Y + size.Z - smallest - Mathf.Max(size.X, Mathf.Max(size.Y, size.Z));
        bool isLargeArchitecture = middle >= 3.0f;
        bool isConcreteArchitecture = isLargeArchitecture &&
            (name.Contains("Wall", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("Bulkhead", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("Ceiling", StringComparison.OrdinalIgnoreCase));
        if (isConcreteArchitecture)
        {
            return "res://assets/textures/industrial_concrete.png";
        }

        bool isWideWalkingSurface = size.Y <= size.X &&
            size.Y <= size.Z &&
            size.Y <= 0.75f &&
            size.X >= 3.2f &&
            size.Z >= 3.2f;
        return isWideWalkingSurface
            ? "res://assets/textures/diamond_plate.png"
            : texturePath;
    }

    public static MeshInstance3D AddCylinder(
        Node parent,
        string name,
        Vector3 position,
        Vector3 rotation,
        float topRadius,
        float height,
        Material material,
        float? bottomRadius = null)
    {
        MeshInstance3D visual = new()
        {
            Name = name,
            Position = position,
            Rotation = rotation,
            Mesh = new CylinderMesh
            {
                TopRadius = topRadius,
                BottomRadius = bottomRadius ?? topRadius,
                Height = height,
                RadialSegments = 24,
            },
            MaterialOverride = material,
        };
        parent.AddChild(visual);
        return visual;
    }

    public static Node3D AddWindFan(
        Node parent,
        string name,
        Vector3 position,
        Vector3 rotation,
        float scale,
        Material hubMaterial,
        StandardMaterial3D bladeMaterial)
    {
        Node3D housing = new()
        {
            Name = name,
            Position = position,
            Rotation = rotation,
        };
        parent.AddChild(housing);
        AddCylinder(housing, "Hub", Vector3.Zero, new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f), 0.48f * scale, 0.8f * scale, hubMaterial);

        // A single smooth ring reads as a protective fan guard cleanly.
        // Building it out of many short straight box segments (the old
        // approach) creates a cluttered look with dozens of unnecessary
        // visible edges/lines where the flat segments meet at angles
        // instead of forming a true circle.
        const float guardRadius = 3.35f;
        const float guardTubeRadius = 0.11f;
        housing.AddChild(new MeshInstance3D
        {
            Name = "Guard",
            Position = new Vector3(0.0f, 0.0f, 0.18f * scale),
            // TorusMesh's default axis is Y, but the fan's own axis (hub,
            // blades) is Z - without this rotation the ring lies flat on
            // the wrong plane entirely instead of standing upright around
            // the blades.
            Rotation = new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f),
            Mesh = new TorusMesh
            {
                InnerRadius = (guardRadius - guardTubeRadius) * scale,
                OuterRadius = (guardRadius + guardTubeRadius) * scale,
                Rings = 48,
                RingSegments = 12,
            },
            MaterialOverride = bladeMaterial,
        });

        Node3D rotor = new() { Name = "Rotor" };
        housing.AddChild(rotor);

        // A wide, shallow spinner cap gives the blades a solid visual
        // anchor at the hub instead of appearing to float apart from it -
        // thin blade roots meeting a thin hub edge read as disconnected
        // even when their boxes technically overlap.
        rotor.AddChild(new MeshInstance3D
        {
            Name = "SpinnerCap",
            Rotation = new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f),
            Mesh = new CylinderMesh
            {
                TopRadius = 0.62f * scale,
                BottomRadius = 0.62f * scale,
                Height = 0.14f * scale,
                RadialSegments = 20,
            },
            MaterialOverride = bladeMaterial,
        });

        const int bladeCount = 5;
        const float bladeInnerRadius = 0.45f;
        const float bladeOuterRadius = 3.0f;
        const float bladeLength = bladeOuterRadius - bladeInnerRadius;
        const float bladeCenterRadius = bladeInnerRadius + (bladeLength * 0.5f);
        for (int index = 0; index < bladeCount; index++)
        {
            float angle = index * Mathf.Tau / bladeCount;
            AddVisualBox(
                rotor,
                $"Blade{index + 1}",
                new Vector3(0.42f, bladeLength, 0.16f) * scale,
                new Vector3(Mathf.Sin(angle) * bladeCenterRadius, Mathf.Cos(angle) * bladeCenterRadius, 0.0f) * scale,
                // A Z-rotation by +angle turns the box's own long (Y) axis
                // to point in the (-sin, cos) direction, not (sin, cos) -
                // the same direction used for the position above only at
                // angle 0. At every other angle the blade's long axis was
                // mismatched from its radial position, so it didn't point
                // back through the hub at all - reading as disconnected
                // sticks instead of spokes. Negating the angle here makes
                // the box's long axis match the radial direction exactly.
                new Vector3(0.0f, 0.0f, -angle),
                string.Empty,
                Colors.White,
                0.0f,
                1.0f,
                bladeMaterial);
        }
        return rotor;
    }

    public static Node3D AddGear(
        Node parent,
        string name,
        Vector3 position,
        float radius,
        int teeth,
        StandardMaterial3D material)
    {
        Node3D gear = new() { Name = name, Position = position };
        gear.AddChild(new MeshInstance3D
        {
            Rotation = new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f),
            Mesh = new CylinderMesh
            {
                TopRadius = radius * 0.72f,
                BottomRadius = radius * 0.72f,
                Height = 0.36f,
                RadialSegments = 24,
            },
            MaterialOverride = material,
        });

        for (int index = 0; index < teeth; index++)
        {
            float angle = Mathf.Tau * index / teeth;
            gear.AddChild(new MeshInstance3D
            {
                Position = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0.0f),
                Rotation = new Vector3(0.0f, 0.0f, angle),
                Mesh = SurfaceMeshFactory.CreateTiledBox(new Vector3(radius * 0.34f, radius * 0.23f, 0.46f), 0.55f),
                MaterialOverride = material,
            });
        }

        parent.AddChild(gear);
        return gear;
    }

    public static Node3D AddClosedRoomShell(
        Node parent,
        string name,
        Vector3 center,
        Vector2 footprint,
        float hazardFloorY,
        float ceilingY,
        string wallTexture,
        Color wallTint,
        Color hazardTint,
        Action<Node3D> onHazardEntered)
    {
        const float wallThickness = 0.45f;
        const float floorThickness = 0.5f;
        const float ceilingThickness = 0.4f;
        const float seamOverlap = 0.35f;
        const float cornerJoinThickness = 0.28f;
        const string hazardTexture = "res://assets/textures/hazard_grate.svg";

        Node3D shell = new()
        {
            Name = name,
            Position = center,
        };
        parent.AddChild(shell);

        float wallHeight = ceilingY - hazardFloorY + floorThickness + (seamOverlap * 2.0f);
        float wallCenterY = hazardFloorY + ((ceilingY - hazardFloorY) * 0.5f);
        Vector2 overlappingFootprint = footprint + new Vector2(seamOverlap * 2.0f, seamOverlap * 2.0f);

        StaticBody3D hazardFloor = AddBox(
            shell,
            "HazardFloor",
            new Vector3(overlappingFootprint.X, floorThickness, overlappingFootprint.Y),
            new Vector3(0.0f, hazardFloorY - (floorThickness * 0.5f), 0.0f),
            Vector3.Zero,
            hazardTexture,
            hazardTint,
            0.34f,
            0.7f,
            castShadow: false);
        MeshInstance3D hazardMesh = (MeshInstance3D)hazardFloor.GetChild(0);
        StandardMaterial3D hazardMaterial = (StandardMaterial3D)hazardMesh.MaterialOverride;
        hazardMaterial.EmissionEnabled = true;
        hazardMaterial.Emission = hazardTint.Darkened(0.74f);
        hazardMaterial.EmissionEnergyMultiplier = 0.32f;
        AddBox(
            shell,
            "Ceiling",
            new Vector3(overlappingFootprint.X, ceilingThickness, overlappingFootprint.Y),
            new Vector3(0.0f, ceilingY + (ceilingThickness * 0.5f), 0.0f),
            Vector3.Zero,
            wallTexture,
            wallTint.Lerp(Colors.White, 0.1f),
            0.36f,
            0.68f,
            castShadow: false);
        AddBox(shell, "LeftWall", new Vector3(wallThickness, wallHeight, overlappingFootprint.Y), new Vector3(-(footprint.X * 0.5f), wallCenterY, 0.0f), Vector3.Zero, wallTexture, wallTint, 0.42f, 0.65f, castShadow: false);
        AddBox(shell, "RightWall", new Vector3(wallThickness, wallHeight, overlappingFootprint.Y), new Vector3(footprint.X * 0.5f, wallCenterY, 0.0f), Vector3.Zero, wallTexture, wallTint, 0.42f, 0.65f, castShadow: false);
        AddBox(shell, "BackWall", new Vector3(overlappingFootprint.X, wallHeight, wallThickness), new Vector3(0.0f, wallCenterY, footprint.Y * 0.5f), Vector3.Zero, wallTexture, wallTint, 0.42f, 0.65f, castShadow: false);
        AddBox(shell, "ExitWall", new Vector3(overlappingFootprint.X, wallHeight, wallThickness), new Vector3(0.0f, wallCenterY, -(footprint.Y * 0.5f)), Vector3.Zero, wallTexture, wallTint, 0.42f, 0.65f, castShadow: false);

        float innerCornerX = (footprint.X * 0.5f) - (wallThickness * 0.5f);
        float innerCornerZ = (footprint.Y * 0.5f) - (wallThickness * 0.5f);
        foreach ((string cornerName, float x, float z) in new[]
        {
            ("BackLeft", -innerCornerX, innerCornerZ),
            ("BackRight", innerCornerX, innerCornerZ),
            ("ExitLeft", -innerCornerX, -innerCornerZ),
            ("ExitRight", innerCornerX, -innerCornerZ),
        })
        {
            MeshInstance3D cornerJoin = AddVisualBox(
                shell,
                $"{cornerName}CornerJoin",
                new Vector3(cornerJoinThickness, wallHeight, cornerJoinThickness),
                new Vector3(x, wallCenterY, z),
                Vector3.Zero,
                wallTexture,
                wallTint.Darkened(0.22f),
                0.52f,
                0.58f);
            cornerJoin.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }

        Area3D hazardTrigger = new()
        {
            Name = "HazardTrigger",
            Position = new Vector3(0.0f, hazardFloorY + 0.62f, 0.0f),
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true,
            Monitorable = false,
        };
        hazardTrigger.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D
            {
                Size = new Vector3(footprint.X - 0.8f, 1.2f, footprint.Y - 0.8f),
            },
        });
        hazardTrigger.BodyEntered += body => onHazardEntered(body);
        shell.AddChild(hazardTrigger);

        return shell;
    }

    public static ExitDoor3D AddExitDoor(
        Node parent,
        string name,
        Vector3 position,
        Vector3 rotation,
        Color frameTint,
        Color leafTint,
        Color lightColor)
    {
        ExitDoor3D door = new()
        {
            Name = name,
            Position = position,
            Rotation = rotation,
        };
        StandardMaterial3D frameMaterial = CreateMaterial(
            "res://assets/textures/brushed_metal.png",
            frameTint.Lerp(Colors.White, 0.22f),
            0.72f,
            0.42f);
        StandardMaterial3D leafMaterial = CreateMaterial(
            "res://assets/textures/copper_rivets.svg",
            leafTint.Lerp(Colors.White, 0.14f),
            0.38f,
            0.58f);
        StandardMaterial3D recessMaterial = CreateMaterial(
            "res://assets/textures/brushed_metal.png",
            new Color("111719"),
            0.05f,
            0.96f);
        StandardMaterial3D markingMaterial = CreateMaterial(
            "res://assets/textures/brushed_metal.png",
            new Color("ddd8c8"),
            0.05f,
            0.76f);

        AddExitCorridor(door);
        AddVisualBox(door, "LeftDoorLeaf", new Vector3(ExitDoor3D.DoorLeafClosedWidth, ExitDoor3D.DoorLeafClosedHeight, 0.3f), new Vector3(-ExitDoor3D.DoorLeafClosedWidth * 0.5f, ExitDoor3D.DoorLeafClosedCenterY, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, leafMaterial);
        AddVisualBox(door, "RightDoorLeaf", new Vector3(ExitDoor3D.DoorLeafClosedWidth, ExitDoor3D.DoorLeafClosedHeight, 0.3f), new Vector3(ExitDoor3D.DoorLeafClosedWidth * 0.5f, ExitDoor3D.DoorLeafClosedCenterY, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, leafMaterial);
        // The leaves slide into opaque side pockets. These masks sit just in
        // front of the moving meshes, so no part of a leaf can remain visible
        // after it travels beyond the outside edge of the frame.
        AddVisualBox(door, "LeftDoorPocketMask", new Vector3(1.5f, 4.02f, 0.38f), new Vector3(-3.1f, 2.13f, ExitDoor3D.FrameRoomSideCenterZ), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frameMaterial);
        AddVisualBox(door, "RightDoorPocketMask", new Vector3(1.5f, 4.02f, 0.38f), new Vector3(3.1f, 2.13f, ExitDoor3D.FrameRoomSideCenterZ), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frameMaterial);
        AddVisualBox(door, "LeftFrame", new Vector3(0.5f, 4.46f, 0.58f), new Vector3(-2.1f, 2.35f, ExitDoor3D.FrameRoomSideCenterZ), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frameMaterial);
        AddVisualBox(door, "RightFrame", new Vector3(0.5f, 4.46f, 0.58f), new Vector3(2.1f, 2.35f, ExitDoor3D.FrameRoomSideCenterZ), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frameMaterial);
        StaticBody3D frameCollision = new()
        {
            Name = "FrameCollision",
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.8f, Bounce = 0.0f },
        };
        foreach ((string hitboxName, Vector3 hitboxSize, Vector3 hitboxPosition) in new[]
        {
            ("LeftPocketHitbox", new Vector3(1.5f, 4.02f, 0.38f), new Vector3(-3.1f, 2.13f, ExitDoor3D.FrameRoomSideCenterZ)),
            ("RightPocketHitbox", new Vector3(1.5f, 4.02f, 0.38f), new Vector3(3.1f, 2.13f, ExitDoor3D.FrameRoomSideCenterZ)),
            ("LeftFrameHitbox", new Vector3(0.5f, 4.46f, 0.58f), new Vector3(-2.1f, 2.35f, ExitDoor3D.FrameRoomSideCenterZ)),
            ("RightFrameHitbox", new Vector3(0.5f, 4.46f, 0.58f), new Vector3(2.1f, 2.35f, ExitDoor3D.FrameRoomSideCenterZ)),
        })
        {
            frameCollision.AddChild(new CollisionShape3D
            {
                Name = hitboxName,
                Position = hitboxPosition,
                Shape = new BoxShape3D { Size = hitboxSize },
            });
        }
        door.AddChild(frameCollision);
        AddVisualBox(door, "CenterSeam", new Vector3(0.08f, ExitDoor3D.DoorLeafClosedHeight, 0.08f), new Vector3(0.0f, ExitDoor3D.DoorLeafClosedCenterY, 0.19f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, recessMaterial);
        AddVisualBox(door, "LeftHandle", new Vector3(0.1f, 0.9f, 0.08f), new Vector3(-0.22f, ExitDoor3D.DoorLeafClosedCenterY, 0.21f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, markingMaterial);
        AddVisualBox(door, "RightHandle", new Vector3(0.1f, 0.9f, 0.08f), new Vector3(0.22f, ExitDoor3D.DoorLeafClosedCenterY, 0.21f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, markingMaterial);
        float chevronY = ExitDoor3D.DoorLeafClosedCenterY + (ExitDoor3D.DoorLeafClosedHeight * 0.5f) + 0.70f;
        const float chevronDepth = 0.12f;
        float wallRoomSideZ = ExitDoor3D.FrameRoomSideCenterZ - (ExitDoor3D.FrameDepth * 0.5f);
        float chevronZ = wallRoomSideZ + (chevronDepth * 0.5f);
        AddVisualBox(door, "ChevronLeft", new Vector3(1.08f, 0.14f, chevronDepth), new Vector3(-0.43f, chevronY, chevronZ), new Vector3(0.0f, 0.0f, Mathf.DegToRad(-30.0f)), string.Empty, Colors.White, 0.0f, 1.0f, markingMaterial);
        AddVisualBox(door, "ChevronRight", new Vector3(1.08f, 0.14f, chevronDepth), new Vector3(0.43f, chevronY, chevronZ), new Vector3(0.0f, 0.0f, Mathf.DegToRad(30.0f)), string.Empty, Colors.White, 0.0f, 1.0f, markingMaterial);

        StaticBody3D closedDoorBlocker = new()
        {
            Name = "ClosedDoorBlocker",
            Position = new Vector3(0.0f, ExitDoor3D.DoorLeafClosedCenterY, -0.03f),
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.8f, Bounce = 0.0f },
        };
        closedDoorBlocker.AddChild(new CollisionShape3D
        {
            Name = "CollisionShape3D",
            Shape = new BoxShape3D { Size = new Vector3(ExitDoor3D.FrameOpeningHalfWidth * 2.0f, ExitDoor3D.DoorLeafClosedHeight, 0.34f) },
        });
        door.AddChild(closedDoorBlocker);

        door.AddChild(new OmniLight3D
        {
            Name = "DoorFillLight",
            Position = new Vector3(0.0f, 2.2f, 1.35f),
            LightColor = lightColor,
            LightEnergy = 0.35f,
            OmniRange = 5.2f,
            ShadowEnabled = false,
        });
        parent.AddChild(door);
        ConfigureCorridorDepthMaterials(door);
        return door;
    }

    public static ExitDoor3D AddGoalExitDoor(
        Node parent,
        Vector3 goalPosition,
        Vector3? outwardDirection = null)
    {
        Vector3 outward = (outwardDirection ?? Vector3.Forward).Normalized();
        Vector3 inward = -outward;
        float yaw = Mathf.Atan2(inward.X, inward.Z);
        float exitFloorY = FindExitFloorY(parent, goalPosition);
        Vector3 doorPosition = goalPosition + (outward * 1.08f);
        doorPosition.Y = exitFloorY - 0.12f;
        ExitDoor3D door = AddExitDoor(
            parent,
            "ExitDoor",
            doorPosition,
            new Vector3(0.0f, yaw, 0.0f),
            new Color("9eaaab"),
            new Color("8c654f"),
            new Color("d7bd83"));
        ConfigureCorridorEntranceTrigger(parent, door);
        CarveRoomShellDoorway(parent, door, outward);
        TrimExitPlatformsToThreshold(parent, door);
        ClearDoorwayBlockers(parent, door);
        AlignExitCorridorFloorToPlatform(parent, door);
        Callable.From(() => FinalizeFloorButtonsAndDoorIndicators(parent, door)).CallDeferred();
        return door;
    }

    public static GpuParticles3D AddLowGravityMotes(
        Node parent,
        string name,
        Vector3 position,
        Vector3 emissionExtents,
        int amount)
    {
        StandardMaterial3D material = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = Colors.White,
            EmissionEnabled = true,
            Emission = new Color("dfefff"),
            EmissionEnergyMultiplier = 0.85f,
        };
        ParticleProcessMaterial process = new()
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = emissionExtents,
            Direction = Vector3.Up,
            Spread = 18.0f,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 0.25f,
            InitialVelocityMax = 0.65f,
            ScaleMin = 0.55f,
            ScaleMax = 1.0f,
        };
        GpuParticles3D motes = new()
        {
            Name = name,
            Position = position,
            Amount = amount,
            Lifetime = 7.0,
            Randomness = 0.82f,
            ProcessMaterial = process,
            DrawPass1 = new SphereMesh { Radius = 0.05f, Height = 0.1f, RadialSegments = 8, Rings = 4, Material = material },
        };
        parent.AddChild(motes);
        return motes;
    }

    private static async void FinalizeFloorButtonsAndDoorIndicators(Node parent, ExitDoor3D door)
    {
        // Wait until the first physics frame has registered every room surface;
        // otherwise early deferred raycasts can miss freshly-created floors and
        // leave their floor buttons at the room-authored (often buried) height.
        await parent.ToSignal(parent.GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (!GodotObject.IsInstanceValid(parent) || !GodotObject.IsInstanceValid(door))
        {
            return;
        }

        RouteCheckpoint3D[] buttons = EnumerateDescendants(parent)
            .OfType<RouteCheckpoint3D>()
            .Where(checkpoint => checkpoint.IsPhysicalFloorButton || checkpoint.ShowFloorButtonIndicators)
            .ToArray();

        PhysicsDirectSpaceState3D space = ((Node3D)parent).GetWorld3D().DirectSpaceState;
        foreach (RouteCheckpoint3D button in buttons)
        {
            if (button.IsPhysicalFloorButton)
            {
                float markerY = button.GlobalPosition.Y - (button.TriggerSize.Y * 0.42f);
                PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
                    new Vector3(button.GlobalPosition.X, markerY + 2.0f, button.GlobalPosition.Z),
                    new Vector3(button.GlobalPosition.X, markerY - 6.0f, button.GlobalPosition.Z),
                    1);
                Godot.Collections.Dictionary hit = space.IntersectRay(query);
                if (hit.TryGetValue("position", out Variant hitPositionVariant))
                {
                    Vector3 floorPoint = hitPositionVariant.AsVector3();
                    Vector3 corrected = button.GlobalPosition;
                    corrected.Y = floorPoint.Y + (button.TriggerSize.Y * 0.42f) + 0.08f - button.FloorMarkerInset;
                    button.GlobalPosition = corrected;
                }
            }

            MeshInstance3D? inset = button.GetNodeOrNull<MeshInstance3D>("InsetPlate");
            if (inset is not null && !inset.GetChildren().Any(child => child.Name.ToString().StartsWith("SequencePip", StringComparison.Ordinal)))
            {
                AddSequencePips(inset, button.CheckpointIndex + 1);
            }
        }

        AddDoorButtonIndicators(parent, door);
    }

    private static void AddDoorButtonIndicators(Node parent, ExitDoor3D door)
    {
        RouteCheckpoint3D[] buttons = EnumerateDescendants(parent)
            .OfType<RouteCheckpoint3D>()
            .Where(checkpoint => checkpoint.IsPhysicalFloorButton || checkpoint.ShowFloorButtonIndicators)
            .OrderBy(checkpoint => checkpoint.CheckpointIndex)
            .ThenBy(checkpoint => checkpoint.Name.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (buttons.Length == 0)
        {
            return;
        }

        StandardMaterial3D housingMaterial = CreateMaterial(
            "res://assets/textures/brushed_metal.png",
            new Color("596260"),
            0.48f,
            0.66f);
        StandardMaterial3D inactiveMaterial = CreateMaterial(
            "res://assets/textures/brushed_metal.png",
            new Color("303836"),
            0.42f,
            0.74f);
        StandardMaterial3D activeMaterial = CreateMaterial(
            string.Empty,
            new Color("f4f7f5"),
            0.02f,
            0.38f,
            emissionEnabled: true,
            emission: new Color("dfe9e4"));

        const float indicatorWidth = 0.46f;
        float spacing = buttons.Length == 1
            ? 0.0f
            : Mathf.Min(0.56f, 3.7f / (buttons.Length - 1));
        float firstX = door.AuthoredDoorCenterX - ((buttons.Length - 1) * spacing * 0.5f);
        float housingWidth = Mathf.Max(0.88f, ((buttons.Length - 1) * spacing) + indicatorWidth + 0.34f);
        const float housingDepth = 0.16f;
        const float indicatorDepth = 0.08f;
        float indicatorY = door.AuthoredDoorTopY + (0.32f * 0.5f);
        float wallRoomSideZ = ExitDoor3D.FrameRoomSideCenterZ - (ExitDoor3D.FrameDepth * 0.5f);
        float housingZ = wallRoomSideZ + (housingDepth * 0.5f);
        float indicatorZ = wallRoomSideZ + housingDepth + (indicatorDepth * 0.5f);
        AddVisualBox(
            door,
            "ButtonIndicatorHousing",
            new Vector3(housingWidth, 0.32f, housingDepth),
            new Vector3(door.AuthoredDoorCenterX, indicatorY, housingZ),
            Vector3.Zero,
            string.Empty,
            Colors.White,
            0.0f,
            1.0f,
            housingMaterial);

        List<(RouteCheckpoint3D Button, MeshInstance3D Indicator)> indicators = new(buttons.Length);
        for (int index = 0; index < buttons.Length; index++)
        {
            MeshInstance3D indicator = AddVisualBox(
                door,
                $"ButtonRequirementIndicator{index + 1}",
                new Vector3(indicatorWidth, 0.22f, indicatorDepth),
                new Vector3(firstX + (index * spacing), indicatorY, indicatorZ),
                Vector3.Zero,
                string.Empty,
                Colors.White,
                0.0f,
                1.0f,
                inactiveMaterial);
            indicator.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            indicators.Add((buttons[index], indicator));
        }
        door.ConfigureButtonIndicators(indicators, inactiveMaterial, activeMaterial);
    }

    internal static HashSet<StaticBody3D> TrimExitPlatformsToThreshold(
        Node parent,
        ExitDoor3D door,
        bool preserveBodyTransform = false,
        float expectedFloorHeight = ExitDoor3D.FrameBottomY)
    {
        const float thresholdZ = 0.12f;
        HashSet<StaticBody3D> trimmedBodies = new();
        Node3D? shell = parent.GetNodeOrNull<Node3D>("RoomShell");
        foreach (StaticBody3D body in EnumerateDescendants(parent).OfType<StaticBody3D>().ToArray())
        {
            string bodyName = body.Name.ToString();
            CollisionShape3D? collision = body.GetChildren().OfType<CollisionShape3D>()
                .FirstOrDefault(candidate => !candidate.Disabled && candidate.Shape is BoxShape3D);
            if (collision?.Shape is not BoxShape3D box)
            {
                continue;
            }
            bool edgeBarrier = IsEdgeBarrier(bodyName, box.Size);
            if (door.IsAncestorOf(body) ||
                (shell is not null && shell.IsAncestorOf(body)) ||
                (bodyName.Contains("Wall", StringComparison.OrdinalIgnoreCase) && !edgeBarrier) ||
                bodyName.Contains("Hazard", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            (Vector3 minimum, Vector3 maximum) = GetBoxBoundsInDoorSpace(door, collision, box.Size);
            bool crossesThreshold = minimum.Z < thresholdZ - 0.01f && maximum.Z > thresholdZ + 0.25f;
            bool adjoiningExitFloor = crossesThreshold &&
                Mathf.Abs(maximum.Y - expectedFloorHeight) <= 0.06f &&
                maximum.X >= -ExitDoor3D.CorridorInteriorWidth * 0.5f &&
                minimum.X <= ExitDoor3D.CorridorInteriorWidth * 0.5f;
            if (!adjoiningExitFloor && !(edgeBarrier && crossesThreshold))
            {
                continue;
            }

            Vector3 xAxisInDoor = door.GlobalBasis.Inverse() * collision.GlobalBasis.X;
            Vector3 zAxisInDoor = door.GlobalBasis.Inverse() * collision.GlobalBasis.Z;
            bool trimX = Mathf.Abs(xAxisInDoor.Z) > Mathf.Abs(zAxisInDoor.Z);
            float axisDoorZ = trimX ? xAxisInDoor.Z : zAxisInDoor.Z;
            if (Mathf.Abs(axisDoorZ) < 0.999f)
            {
                GD.PushError($"Exit platform {body.Name} in {parent.Name} is not aligned with its door.");
                continue;
            }

            float protrusion = thresholdZ - minimum.Z;
            float localTrim = protrusion / Mathf.Abs(axisDoorZ);
            Vector3 trimmedSize = box.Size;
            if (trimX)
            {
                trimmedSize.X -= localTrim;
            }
            else
            {
                trimmedSize.Z -= localTrim;
            }
            if (trimmedSize.X <= 0.05f || trimmedSize.Z <= 0.05f)
            {
                GD.PushError($"Exit platform {body.Name} in {parent.Name} is too short to trim at the door threshold.");
                continue;
            }

            if (preserveBodyTransform)
            {
                collision.GlobalPosition += door.GlobalBasis * new Vector3(0.0f, 0.0f, protrusion * 0.5f);
            }
            else
            {
                body.GlobalPosition += door.GlobalBasis * new Vector3(0.0f, 0.0f, protrusion * 0.5f);
            }
            box.Size = trimmedSize;
            trimmedBodies.Add(body);
            MeshInstance3D? mesh = body.GetChildren().OfType<MeshInstance3D>().FirstOrDefault();
            if (mesh is not null && !preserveBodyTransform)
            {
                Vector3 trimmedVisualSize = trimmedSize;
                if (edgeBarrier && body.HasMeta(BarrierBaseSeamSizeMetadata))
                {
                    trimmedVisualSize = body.GetMeta(BarrierBaseSeamSizeMetadata).AsVector3();
                    if (trimX)
                    {
                        trimmedVisualSize.X -= localTrim;
                    }
                    else
                    {
                        trimmedVisualSize.Z -= localTrim;
                    }
                    body.SetMeta(BarrierBaseSeamSizeMetadata, trimmedVisualSize);
                }
                if (edgeBarrier && body.HasMeta(GeneratedPlatformWallGuideSizeMetadata))
                {
                    Vector3 trimmedGuideSize = body.GetMeta(GeneratedPlatformWallGuideSizeMetadata).AsVector3();
                    if (trimX)
                    {
                        trimmedGuideSize.X -= localTrim;
                    }
                    else
                    {
                        trimmedGuideSize.Z -= localTrim;
                    }
                    body.SetMeta(GeneratedPlatformWallGuideSizeMetadata, trimmedGuideSize);
                }
                mesh.Mesh = SurfaceMeshFactory.CreateTiledBox(trimmedVisualSize);
                CollisionShape3D? generatedHitbox = body.GetNodeOrNull<CollisionShape3D>("GeneratedWallHitbox");
                if (generatedHitbox?.Shape is BoxShape3D generatedHitboxBox)
                {
                    generatedHitboxBox.Size = trimmedVisualSize;
                    generatedHitbox.Position = body.HasMeta(BarrierBaseSeamOffsetMetadata)
                        ? body.GetMeta(BarrierBaseSeamOffsetMetadata).AsVector3()
                        : mesh.Position;
                }
            }
        }
        return trimmedBodies;
    }

    internal static void AlignExitCorridorFloorToPlatform(Node parent, ExitDoor3D door)
    {
        Node3D? shell = parent.GetNodeOrNull<Node3D>("RoomShell");
        CollisionShape3D? nearestPlatform = null;
        Vector3 nearestMinimum = Vector3.Zero;
        float bestDistance = float.PositiveInfinity;

        foreach (StaticBody3D body in EnumerateDescendants(parent).OfType<StaticBody3D>())
        {
            string bodyName = body.Name.ToString();
            if (door.IsAncestorOf(body) ||
                (shell is not null && shell.IsAncestorOf(body)) ||
                bodyName.Contains("Wall", StringComparison.OrdinalIgnoreCase) ||
                bodyName.Contains("Hazard", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (CollisionShape3D collision in body.GetChildren().OfType<CollisionShape3D>())
            {
                // ClearDoorwayBlockers can disable the original exit platform
                // before rebuilding its doorway-safe pieces. Its old bounds are
                // still the most reliable description of where the deck ends.
                if (collision.Shape is not BoxShape3D box ||
                    Mathf.Abs(collision.GlobalBasis.Y.Normalized().Dot(Vector3.Up)) < 0.95f)
                {
                    continue;
                }

                (Vector3 minimum, Vector3 maximum) = GetBoxBoundsInDoorSpace(door, collision, box.Size);
                if (Mathf.Abs(maximum.Y - 0.12f) > 0.06f ||
                    maximum.X < -0.45f || minimum.X > 0.45f)
                {
                    continue;
                }

                const float goalLocalZ = 1.08f;
                float distance = goalLocalZ < minimum.Z
                    ? minimum.Z - goalLocalZ
                    : goalLocalZ > maximum.Z
                        ? goalLocalZ - maximum.Z
                        : 0.0f;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearestPlatform = collision;
                    nearestMinimum = minimum;
                }
            }
        }

        if (nearestPlatform is null || bestDistance > 1.05f)
        {
            GD.PushError($"Exit door in {parent.Name} could not find its adjoining platform.");
            return;
        }

        StaticBody3D? corridorFloor = door.GetNodeOrNull<StaticBody3D>("ExitCorridorFloor");
        MeshInstance3D? floorMesh = corridorFloor?.GetChildren().OfType<MeshInstance3D>().FirstOrDefault();
        CollisionShape3D? floorCollision = corridorFloor?.GetChildren().OfType<CollisionShape3D>().FirstOrDefault();
        if (corridorFloor is null || floorMesh is null || floorCollision is null)
        {
            GD.PushError($"Exit door in {parent.Name} is missing its corridor floor.");
            return;
        }

        const float corridorBackZOffset = 0.12f;
        float corridorBackZ = -ExitDoor3D.CorridorLength - corridorBackZOffset;
        float platformEdgeZ = nearestMinimum.Z;
        float floorDepth = platformEdgeZ - corridorBackZ;
        if (floorDepth <= ExitDoor3D.CorridorTransitionDepth)
        {
            GD.PushError($"Exit door in {parent.Name} has an invalid adjoining platform edge.");
            return;
        }

        Vector3 floorSize = new(ExitDoor3D.CorridorInteriorWidth, 0.24f, floorDepth);
        corridorFloor.Position = new Vector3(0.0f, 0.0f, (corridorBackZ + platformEdgeZ) * 0.5f);
        floorMesh.Mesh = SurfaceMeshFactory.CreateTiledBox(floorSize);
        floorCollision.Shape = new BoxShape3D { Size = floorSize };
        if (floorMesh.MaterialOverride is ShaderMaterial floorDimMaterial)
        {
            // The depth-fade dimming shader computes its fade using the
            // corridor_length uniform it was created with. The floor gets
            // resized here to reach the adjoining platform edge, so without
            // updating this uniform to match, the dimming fade is computed
            // against the wrong length and stops covering the real surface.
            floorDimMaterial.SetShaderParameter("corridor_length", floorDepth);
        }
    }

    internal static bool CloseExitCorridorCollisionSeam(Node parent, ExitDoor3D door)
    {
        Node3D? shell = parent.GetNodeOrNull<Node3D>("RoomShell");
        Vector3? nearestMinimum = null;
        float bestDistance = float.PositiveInfinity;
        foreach (StaticBody3D body in EnumerateDescendants(parent).OfType<StaticBody3D>())
        {
            string bodyName = body.Name.ToString();
            if (door.IsAncestorOf(body) ||
                (shell is not null && shell.IsAncestorOf(body)) ||
                bodyName.Contains("Wall", StringComparison.OrdinalIgnoreCase) ||
                bodyName.Contains("Hazard", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (CollisionShape3D collision in body.GetChildren().OfType<CollisionShape3D>())
            {
                if (collision.Disabled || collision.Shape is not BoxShape3D box ||
                    Mathf.Abs(collision.GlobalBasis.Y.Normalized().Dot(Vector3.Up)) < 0.95f)
                {
                    continue;
                }

                (Vector3 minimum, Vector3 maximum) = GetBoxBoundsInDoorSpace(door, collision, box.Size);
                if (Mathf.Abs(maximum.Y - ExitDoor3D.AuthoredApproachFloorHeight) > 0.025f ||
                    maximum.X < -0.45f || minimum.X > 0.45f)
                {
                    continue;
                }

                const float goalLocalZ = 1.08f;
                float distance = goalLocalZ < minimum.Z
                    ? minimum.Z - goalLocalZ
                    : goalLocalZ > maximum.Z
                        ? goalLocalZ - maximum.Z
                        : 0.0f;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearestMinimum = minimum;
                }
            }
        }

        CollisionShape3D? floorCollision = door
            .GetNodeOrNull<StaticBody3D>("ExitCorridorFloor")?
            .GetChildren().OfType<CollisionShape3D>().FirstOrDefault();
        if (nearestMinimum is null || bestDistance > 1.05f ||
            floorCollision?.Shape is not BoxShape3D floorBox)
        {
            return false;
        }

        (Vector3 floorMinimum, Vector3 floorMaximum) =
            GetBoxBoundsInDoorSpace(door, floorCollision, floorBox.Size);
        float gap = nearestMinimum.Value.Z - floorMaximum.Z;
        if (gap <= 0.001f)
        {
            return false;
        }
        if (gap > ExitDoor3D.CorridorTransitionDepth ||
            Mathf.Abs(floorCollision.GlobalBasis.Z.Normalized().Dot(door.GlobalBasis.Z.Normalized())) < 0.995f)
        {
            GD.PushError($"Exit door in {parent.Name} has an unsupported {gap:F3} m corridor collision seam.");
            return false;
        }

        // Extend only the invisible hitbox toward the final imported platform.
        // The Blender-authored corridor mesh and its dimming material stay
        // byte-for-byte visually unchanged.
        floorCollision.Shape = new BoxShape3D
        {
            Size = new Vector3(floorBox.Size.X, floorBox.Size.Y, floorBox.Size.Z + gap),
        };
        floorCollision.GlobalPosition += door.GlobalBasis.Z.Normalized() * (gap * 0.5f);
        return true;
    }

    private static void ConfigureCorridorEntranceTrigger(Node parent, ExitDoor3D door)
    {
        Area3D? goal = parent.GetNodeOrNull<Area3D>("GoalCup");
        if (goal is null)
        {
            GD.PushError($"Exit door in {parent.Name} could not find its GoalCup trigger.");
            return;
        }

        goal.GlobalTransform = new Transform3D(door.GlobalBasis.Orthonormalized(), goal.GlobalPosition);
        foreach (CollisionShape3D collision in goal.GetChildren().OfType<CollisionShape3D>())
        {
            collision.Position = Vector3.Zero;
            collision.Rotation = Vector3.Zero;
            collision.Shape = new BoxShape3D { Size = new Vector3(6.8f, 4.5f, 14.0f) };
        }
    }

    private static float FindExitFloorY(Node parent, Vector3 goalPosition)
    {
        float bestFloorY = float.NegativeInfinity;
        foreach (StaticBody3D body in EnumerateDescendants(parent).OfType<StaticBody3D>())
        {
            if (body.Name == "HazardFloor" || body.Name.ToString().Contains("Wall", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (CollisionShape3D collision in body.GetChildren().OfType<CollisionShape3D>())
            {
                if (collision.Disabled || collision.Shape is not BoxShape3D box)
                {
                    continue;
                }

                Vector3 localGoal = collision.ToLocal(new Vector3(goalPosition.X, collision.GlobalPosition.Y, goalPosition.Z));
                if (Mathf.Abs(localGoal.X) > (box.Size.X * 0.5f) + 1.0f ||
                    Mathf.Abs(localGoal.Z) > (box.Size.Z * 0.5f) + 1.0f)
                {
                    continue;
                }

                Basis basis = collision.GlobalBasis;
                float verticalExtent =
                    (Mathf.Abs(basis.X.Y) * box.Size.X * 0.5f) +
                    (Mathf.Abs(basis.Y.Y) * box.Size.Y * 0.5f) +
                    (Mathf.Abs(basis.Z.Y) * box.Size.Z * 0.5f);
                float topY = collision.GlobalPosition.Y + verticalExtent;
                if (topY <= goalPosition.Y + 0.2f && topY >= goalPosition.Y - 4.0f)
                {
                    bestFloorY = Mathf.Max(bestFloorY, topY);
                }
            }
        }

        return float.IsNegativeInfinity(bestFloorY) ? goalPosition.Y - 1.8f : bestFloorY;
    }

    private static void ClearDoorwayBlockers(Node parent, ExitDoor3D door)
    {
        Node3D? shell = parent.GetNodeOrNull<Node3D>("RoomShell");
        foreach (StaticBody3D body in EnumerateDescendants(parent).OfType<StaticBody3D>().ToArray())
        {
            if (door.IsAncestorOf(body) || (shell is not null && shell.IsAncestorOf(body)))
            {
                continue;
            }

            bool blocksOpening = false;
            Vector3 blockerMinimum = Vector3.Zero;
            Vector3 blockerMaximum = Vector3.Zero;
            foreach (CollisionShape3D collision in body.GetChildren().OfType<CollisionShape3D>())
            {
                if (collision.Disabled || collision.Shape is not BoxShape3D box)
                {
                    continue;
                }

                (Vector3 minimum, Vector3 maximum) = GetBoxBoundsInDoorSpace(door, collision, box.Size);
                bool intersectsOpening =
                    maximum.X >= -1.82f && minimum.X <= 1.82f &&
                    minimum.X <= 0.0f && maximum.X >= 0.0f &&
                    maximum.Y >= 0.3f && minimum.Y <= 3.95f &&
                    maximum.Z >= -3.4f && minimum.Z <= 1.65f;
                if (intersectsOpening)
                {
                    blocksOpening = true;
                    blockerMinimum = minimum;
                    blockerMaximum = maximum;
                    break;
                }
            }

            if (!blocksOpening)
            {
                continue;
            }

            body.Visible = false;
            foreach (CollisionShape3D collision in body.GetChildren().OfType<CollisionShape3D>())
            {
                collision.Disabled = true;
            }

            Material? material = body.GetChildren().OfType<MeshInstance3D>().FirstOrDefault()?.MaterialOverride;
            if (material is not null)
            {
                AddClearedBlockerPiece(door, body.Name.ToString(), "Left", blockerMinimum, new Vector3(-1.82f, blockerMaximum.Y, blockerMaximum.Z), material);
                AddClearedBlockerPiece(door, body.Name.ToString(), "Right", new Vector3(1.82f, blockerMinimum.Y, blockerMinimum.Z), blockerMaximum, material);
                Vector3 belowMinimum = new(Mathf.Max(blockerMinimum.X, -1.82f), blockerMinimum.Y, blockerMinimum.Z);
                Vector3 belowMaximum = new(Mathf.Min(blockerMaximum.X, 1.82f), Mathf.Min(blockerMaximum.Y, 0.3f), blockerMaximum.Z);
                AddClearedBlockerPiece(door, body.Name.ToString(), "Below", belowMinimum, belowMaximum, material);
                AddClearedBlockerPiece(
                    door,
                    body.Name.ToString(),
                    "Above",
                    new Vector3(Mathf.Max(blockerMinimum.X, -1.82f), 3.95f, blockerMinimum.Z),
                    new Vector3(Mathf.Min(blockerMaximum.X, 1.82f), blockerMaximum.Y, blockerMaximum.Z),
                    material);
            }
        }
    }

    private static void AddClearedBlockerPiece(
        ExitDoor3D door,
        string sourceName,
        string side,
        Vector3 minimum,
        Vector3 maximum,
        Material material)
    {
        Vector3 size = maximum - minimum;
        if (size.X <= 0.02f || size.Y <= 0.02f || size.Z <= 0.02f)
        {
            return;
        }

        Vector3 center = (minimum + maximum) * 0.5f;
        // A cleared blocker piece rebuilt deep inside the dark exit
        // corridor (negative door-local Z) must not keep the source body's
        // bright room material - that leaves a lit patch breaking the
        // corridor's dimming, which is exactly the "bad door dimming" seen
        // in rooms where some room geometry happened to cross the doorway.
        Material resolvedMaterial = center.Z < -0.1f ? GetCorridorInfillMaterial() : material;

        AddCorridorPanel(
            door,
            $"ExitDoorwayTrim{sourceName}{side}",
            size,
            center,
            resolvedMaterial);
    }

    private static StandardMaterial3D? _corridorInfillMaterial;

    private static StandardMaterial3D GetCorridorInfillMaterial()
    {
        return _corridorInfillMaterial ??= new StandardMaterial3D
        {
            AlbedoColor = new Color("010203"),
            Roughness = 1.0f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
    }

    private static (Vector3 Minimum, Vector3 Maximum) GetBoxBoundsInDoorSpace(
        ExitDoor3D door,
        CollisionShape3D collision,
        Vector3 size)
    {
        Vector3 minimum = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        Vector3 half = size * 0.5f;
        foreach (float x in new[] { -half.X, half.X })
        foreach (float y in new[] { -half.Y, half.Y })
        foreach (float z in new[] { -half.Z, half.Z })
        {
            Vector3 point = door.ToLocal(collision.ToGlobal(new Vector3(x, y, z)));
            minimum = new Vector3(Mathf.Min(minimum.X, point.X), Mathf.Min(minimum.Y, point.Y), Mathf.Min(minimum.Z, point.Z));
            maximum = new Vector3(Mathf.Max(maximum.X, point.X), Mathf.Max(maximum.Y, point.Y), Mathf.Max(maximum.Z, point.Z));
        }
        return (minimum, maximum);
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

    private static void AddExitCorridor(ExitDoor3D door)
    {
        const float panelThickness = 0.24f;
        // Start the corridor side walls in front of the door plane.  Starting
        // them deeper inside the corridor left an open pocket behind each
        // inner side of the frame that the player could roll through and leave
        // the closed room shell.
        const float sideWallFrontZ = ExitDoor3D.CorridorSideWallFrontOffset;
        float corridorCenterZ = -(ExitDoor3D.CorridorLength * 0.5f) - 0.12f;
        float corridorBackDepth = ExitDoor3D.CorridorLength + 0.12f;
        float sideWallLength = corridorBackDepth + sideWallFrontZ;
        float sideWallCenterZ = (sideWallFrontZ - corridorBackDepth) * 0.5f;
        ShaderMaterial floorMaterial = CreateCorridorDepthMaterial(ExitDoor3D.CorridorLength + 0.5f);
        ShaderMaterial lengthMaterial = CreateCorridorDepthMaterial(ExitDoor3D.CorridorLength);
        ShaderMaterial sideMaterial = CreateCorridorDepthMaterial(sideWallLength);
        StandardMaterial3D terminalMaterial = new()
        {
            AlbedoColor = new Color("010203"),
            Roughness = 1.0f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };

        AddCorridorPanel(
            door,
            "ExitCorridorFloor",
            new Vector3(ExitDoor3D.CorridorInteriorWidth, panelThickness, ExitDoor3D.CorridorLength + 0.5f),
            new Vector3(0.0f, 0.0f, corridorCenterZ + 0.25f),
            floorMaterial);
        AddCorridorPanel(
            door,
            "ExitCorridorCeiling",
            new Vector3(ExitDoor3D.CorridorInteriorWidth, panelThickness, ExitDoor3D.CorridorLength),
            new Vector3(0.0f, ExitDoor3D.CorridorInteriorHeight + panelThickness, corridorCenterZ),
            lengthMaterial);
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            AddCorridorPanel(
                door,
                side < 0.0f ? "ExitCorridorLeftWall" : "ExitCorridorRightWall",
                new Vector3(panelThickness, ExitDoor3D.CorridorInteriorHeight + panelThickness, sideWallLength),
                new Vector3(side * ((ExitDoor3D.CorridorInteriorWidth + panelThickness) * 0.5f), (ExitDoor3D.CorridorInteriorHeight + panelThickness) * 0.5f, sideWallCenterZ),
                sideMaterial);
        }
        AddCorridorPanel(
            door,
            "ExitCorridorEndWall",
            new Vector3(ExitDoor3D.CorridorInteriorWidth + (panelThickness * 2.0f), ExitDoor3D.CorridorInteriorHeight + panelThickness, panelThickness),
            new Vector3(0.0f, (ExitDoor3D.CorridorInteriorHeight + panelThickness) * 0.5f, -ExitDoor3D.CorridorLength - 0.12f),
            terminalMaterial);

    }

    private static ShaderMaterial CreateCorridorDepthMaterial(float length)
    {
        Shader shader = new()
        {
            Code = @"shader_type spatial;
render_mode unshaded, cull_disabled;
uniform sampler2D corridor_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float corridor_length = 9.6;
uniform vec3 corridor_origin_world;
uniform vec3 corridor_direction_world = vec3(0.0, 0.0, -1.0);
varying float corridor_depth;
void vertex() {
    vec3 world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    corridor_depth = clamp(dot(world_position - corridor_origin_world, corridor_direction_world) / corridor_length, 0.0, 1.0);
}
void fragment() {
    vec3 detail = texture(corridor_texture, UV * vec2(8.0, 5.0)).rgb;
    float fade = smoothstep(0.0, 1.0, corridor_depth);
    ALBEDO = detail * mix(vec3(0.35, 0.39, 0.42), vec3(0.004, 0.006, 0.008), fade);
    ROUGHNESS = 0.96;
    METALLIC = 0.02;
}",
        };
        ShaderMaterial material = new() { Shader = shader };
        material.SetShaderParameter("corridor_texture", GD.Load<Texture2D>("res://assets/textures/industrial_concrete.png"));
        material.SetShaderParameter("corridor_length", length);
        return material;
    }

    private static void ConfigureCorridorDepthMaterials(ExitDoor3D door)
    {
        Vector3 corridorDirection = (door.GlobalBasis * Vector3.Forward).Normalized();
        foreach (ShaderMaterial material in EnumerateDescendants(door)
            .OfType<MeshInstance3D>()
            .Select(mesh => mesh.MaterialOverride)
            .OfType<ShaderMaterial>()
            .Distinct())
        {
            if (material.Shader?.Code.Contains("corridor_origin_world", StringComparison.Ordinal) != true)
            {
                continue;
            }

            material.SetShaderParameter("corridor_origin_world", door.GlobalPosition);
            material.SetShaderParameter("corridor_direction_world", corridorDirection);
        }
    }

    private static StaticBody3D AddCorridorPanel(
        Node parent,
        string name,
        Vector3 size,
        Vector3 position,
        Material material)
    {
        StaticBody3D body = new()
        {
            Name = name,
            Position = position,
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = 1.0f, Bounce = 0.0f },
        };
        body.AddChild(new MeshInstance3D
        {
            Mesh = SurfaceMeshFactory.CreateTiledBox(size),
            MaterialOverride = material,
        });
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = size },
        });
        parent.AddChild(body);
        return body;
    }

    private static void CarveRoomShellDoorway(Node parent, ExitDoor3D door, Vector3 outward)
    {
        Node3D? shell = parent.GetNodeOrNull<Node3D>("RoomShell");
        if (shell is null)
        {
            GD.PushError($"Exit door in {parent.Name} could not find its RoomShell.");
            return;
        }

        bool sideWall = Mathf.Abs(outward.X) > 0.5f;
        string wallName = sideWall
            ? (outward.X > 0.0f ? "RightWall" : "LeftWall")
            : (outward.Z > 0.0f ? "BackWall" : "ExitWall");
        StaticBody3D? wall = shell.GetNodeOrNull<StaticBody3D>(wallName);
        CollisionShape3D? wallCollision = wall?.GetChildren().OfType<CollisionShape3D>().FirstOrDefault();
        MeshInstance3D? wallMesh = wall?.GetChildren().OfType<MeshInstance3D>().FirstOrDefault();
        if (wall is null || wallCollision?.Shape is not BoxShape3D wallShape || wallMesh?.MaterialOverride is not Material wallMaterial)
        {
            GD.PushError($"Exit door in {parent.Name} could not carve {wallName}.");
            return;
        }

        Vector3 wallSize = wallShape.Size;
        Vector3 doorInShell = shell.ToLocal(door.GlobalPosition);
        float horizontalCenter = sideWall ? doorInShell.Z : doorInShell.X;
        float wallHorizontalCenter = sideWall ? wall.Position.Z : wall.Position.X;
        float wallHorizontalSize = sideWall ? wallSize.Z : wallSize.X;
        float wallHorizontalMin = wallHorizontalCenter - (wallHorizontalSize * 0.5f);
        float wallHorizontalMax = wallHorizontalCenter + (wallHorizontalSize * 0.5f);
        float openingHorizontalMin = Mathf.Max(wallHorizontalMin, horizontalCenter - ExitDoor3D.FrameOuterHalfWidth);
        float openingHorizontalMax = Mathf.Min(wallHorizontalMax, horizontalCenter + ExitDoor3D.FrameOuterHalfWidth);
        float wallVerticalMin = wall.Position.Y - (wallSize.Y * 0.5f);
        float wallVerticalMax = wall.Position.Y + (wallSize.Y * 0.5f);
        float openingVerticalMin = Mathf.Clamp(doorInShell.Y - 0.04f, wallVerticalMin, wallVerticalMax);
        // Let the frame-local backing overlap the rear edge of the header
        // slightly. Exact face-to-face joins can expose a bright slit above
        // the frame from low or oblique camera angles.
        const float headerWallOverlap = 0.50f;
        float openingVerticalMax = Mathf.Clamp(doorInShell.Y + ExitDoor3D.FrameOuterHeight, wallVerticalMin, wallVerticalMax);

        wall.Visible = false;
        wallCollision.Disabled = true;
        // Splitting a single wall into Left/Right/Below/Above pieces creates
        // a T-junction: Left and Right each run the wall's full height,
        // while Below and Above only span the doorway's width. Even with
        // pixel-perfect coplanar placement this configuration is prone to
        // hairline rendering cracks along the shared edge (no gap needed -
        // it's a mesh-tessellation seam, not a positional one), which reads
        // as a faint shadow line. Extending Below/Above horizontally into
        // Left/Right's territory turns the single continuous T-junction
        // line into two shorter, overlapping seams instead, hiding the
        // crack rather than relying on exact edge alignment.
        const float wallSeamOverlap = 0.03f;
        AddDoorwayWallPiece(shell, wallName, "Left", sideWall, wall.Position, wallSize, wallHorizontalMin, openingHorizontalMin, wallVerticalMin, wallVerticalMax, wallMaterial);
        AddDoorwayWallPiece(shell, wallName, "Right", sideWall, wall.Position, wallSize, openingHorizontalMax, wallHorizontalMax, wallVerticalMin, wallVerticalMax, wallMaterial);
        AddDoorwayWallPiece(shell, wallName, "Below", sideWall, wall.Position, wallSize, openingHorizontalMin - wallSeamOverlap, openingHorizontalMax + wallSeamOverlap, wallVerticalMin, openingVerticalMin, wallMaterial);
        // Dip this piece down into the frame opening by the same overlap
        // used for the door's own backing piece below, so the two
        // independently-transformed header pieces provably interpenetrate
        // instead of relying on an exact face-to-face join that can leave a
        // hairline gap (visible as a shadow line) above the frame.
        AddDoorwayWallPiece(shell, wallName, "Above", sideWall, wall.Position, wallSize, openingHorizontalMin - wallSeamOverlap, openingHorizontalMax + wallSeamOverlap, openingVerticalMax - headerWallOverlap, wallVerticalMax, wallMaterial);

        // Always finish the carved wall in the frame's own plane. Even when
        // the shell wall is nearby, its front face can still sit a few tenths
        // of a metre behind the frame and create a visible slit from the side.
        // For remote shell walls, this partition also connects the door to the
        // room while the original carve lets the dark corridor continue through.
        {
            Vector3 horizontalStartInShell = sideWall
                ? new Vector3(wall.Position.X, doorInShell.Y, wallHorizontalMin)
                : new Vector3(wallHorizontalMin, doorInShell.Y, wall.Position.Z);
            Vector3 horizontalEndInShell = sideWall
                ? new Vector3(wall.Position.X, doorInShell.Y, wallHorizontalMax)
                : new Vector3(wallHorizontalMax, doorInShell.Y, wall.Position.Z);
            float horizontalStart = door.ToLocal(shell.ToGlobal(horizontalStartInShell)).X;
            float horizontalEnd = door.ToLocal(shell.ToGlobal(horizontalEndInShell)).X;
            float partitionHorizontalMin = Mathf.Min(horizontalStart, horizontalEnd);
            float partitionHorizontalMax = Mathf.Max(horizontalStart, horizontalEnd);
            float partitionVerticalMin = wallVerticalMin - doorInShell.Y;
            float partitionVerticalMax = wallVerticalMax - doorInShell.Y;

            AddDoorBackingWallPiece(door, "Left", partitionHorizontalMin, -ExitDoor3D.FrameOuterHalfWidth, partitionVerticalMin, partitionVerticalMax, wallMaterial);
            AddDoorBackingWallPiece(door, "Right", ExitDoor3D.FrameOuterHalfWidth, partitionHorizontalMax, partitionVerticalMin, partitionVerticalMax, wallMaterial);
            AddDoorBackingWallPiece(door, "Below", -ExitDoor3D.FrameOuterHalfWidth, ExitDoor3D.FrameOuterHalfWidth, partitionVerticalMin, -0.04f, wallMaterial);
            AddDoorBackingWallPiece(
                door,
                "Above",
                -ExitDoor3D.FrameOuterHalfWidth,
                ExitDoor3D.FrameOuterHalfWidth,
                ExitDoor3D.FrameOuterHeight - headerWallOverlap,
                partitionVerticalMax,
                wallMaterial);
        }
    }

    private static void AddDoorBackingWallPiece(
        ExitDoor3D door,
        string pieceName,
        float horizontalMin,
        float horizontalMax,
        float verticalMin,
        float verticalMax,
        Material material)
    {
        float width = horizontalMax - horizontalMin;
        float height = verticalMax - verticalMin;
        if (width <= 0.02f || height <= 0.02f)
        {
            return;
        }

        // Keep the backing wall flush with the back of the frame instead of
        // centring it through the frame.  This seals the room and corridor
        // while leaving the complete header, arrow and side rails visible.
        const float thickness = 0.42f;
        float backingCenterZ =
            ExitDoor3D.FrameRoomSideCenterZ -
            (ExitDoor3D.FrameDepth * 0.5f) -
            (thickness * 0.5f);
        AddCorridorPanel(
            door,
            $"ExitDoorBacking{pieceName}",
            new Vector3(width, height, thickness),
            new Vector3((horizontalMin + horizontalMax) * 0.5f, (verticalMin + verticalMax) * 0.5f, backingCenterZ),
            material);
    }

    private static void AddDoorwayWallPiece(
        Node3D shell,
        string wallName,
        string pieceName,
        bool sideWall,
        Vector3 wallPosition,
        Vector3 wallSize,
        float horizontalMin,
        float horizontalMax,
        float verticalMin,
        float verticalMax,
        Material material)
    {
        float horizontalSize = horizontalMax - horizontalMin;
        float verticalSize = verticalMax - verticalMin;
        if (horizontalSize <= 0.02f || verticalSize <= 0.02f)
        {
            return;
        }

        Vector3 size = sideWall
            ? new Vector3(wallSize.X, verticalSize, horizontalSize)
            : new Vector3(horizontalSize, verticalSize, wallSize.Z);
        Vector3 position = sideWall
            ? new Vector3(wallPosition.X, (verticalMin + verticalMax) * 0.5f, (horizontalMin + horizontalMax) * 0.5f)
            : new Vector3((horizontalMin + horizontalMax) * 0.5f, (verticalMin + verticalMax) * 0.5f, wallPosition.Z);
        AddCorridorPanel(shell, $"{wallName}Doorway{pieceName}", size, position, material);
    }

    public static StandardMaterial3D CreateMaterial(
        string texturePath,
        Color tint,
        float metallic,
        float roughness,
        Vector3? size = null,
        bool emissionEnabled = false,
        Color? emission = null)
    {
        Color emissionColor = emission ?? Colors.Black;
        StandardMaterial3D material = new()
        {
            AlbedoTexture = string.IsNullOrWhiteSpace(texturePath) ? null : GD.Load<Texture2D>(texturePath),
            AlbedoColor = tint.Lerp(Colors.White, 0.12f),
            Metallic = Mathf.Min(metallic, 0.5f),
            Roughness = roughness,
            Uv1Scale = Vector3.One,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
            EmissionEnabled = emissionEnabled,
            Emission = emissionColor,
            EmissionEnergyMultiplier = emissionEnabled ? 1.35f : 1.0f,
        };
        return material;
    }
}
