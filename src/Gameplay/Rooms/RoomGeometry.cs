using Godot;
using Velocitex.Core.Physics;
using Velocitex.Gameplay.Physics;
using Velocitex.Gameplay.Visual;

namespace Velocitex.Gameplay.Rooms;

    internal static class RoomGeometry
    {
    private const string StickyMaterialPath = "res://resources/materials/sticky_caramel.tres";
    private const string AcceleratorMaterialPath = "res://resources/materials/accelerator_belt.tres";
    private const string SuperElasticMaterialPath = "res://resources/materials/super_elastic_membrane.tres";
    private const string FrictionlessTexturePath = "res://assets/textures/frictionless_glass.svg";
    private const string AbsorbingTexturePath = "res://assets/textures/absorbing_foam.svg";
    private const string OneWayGripTexturePath = "res://assets/textures/one_way_teeth.svg";
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
        Material? materialOverride = null)
    {
        string resolvedTexturePath = ResolveSurfaceTexture(name, size, texturePath);
        StaticBody3D body = surfaceProfile is null
            ? new StaticBody3D()
            : new ProfiledSurfaceBody { Profile = surfaceProfile };
        body.Name = name;
        body.Position = position;
        body.Rotation = rotation;
        body.PhysicsMaterialOverride = new PhysicsMaterial
        {
            Friction = surfaceProfile?.Friction ?? friction,
            Bounce = bounce,
        };
        Material surfaceMaterial = ResolveProfiledSurfaceMaterial(
            size,
            resolvedTexturePath,
            tint,
            metallic,
            roughness,
            surfaceProfile,
            materialOverride);
        body.AddChild(new MeshInstance3D
        {
            Mesh = SurfaceMeshFactory.CreateTiledBox(size),
            MaterialOverride = surfaceMaterial,
            CastShadow = castShadow
                ? GeometryInstance3D.ShadowCastingSetting.On
                : GeometryInstance3D.ShadowCastingSetting.Off,
        });
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = size },
        });
        SurfaceDetail.AddBoxWear(body, name, size, resolvedTexturePath);
        parent.AddChild(body);
        return body;
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
        AddVisualBox(door, "LeftDoorLeaf", new Vector3(1.72f, 3.72f, 0.3f), new Vector3(-0.88f, 2.08f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, leafMaterial);
        AddVisualBox(door, "RightDoorLeaf", new Vector3(1.72f, 3.72f, 0.3f), new Vector3(0.88f, 2.08f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, leafMaterial);
        // The leaves slide into opaque side pockets. These masks sit just in
        // front of the moving meshes, so no part of a leaf can remain visible
        // after it travels beyond the outside edge of the frame.
        AddVisualBox(door, "LeftDoorPocketMask", new Vector3(1.5f, 4.1f, 0.38f), new Vector3(-3.1f, 2.08f, ExitDoor3D.FrameRoomSideCenterZ), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frameMaterial);
        AddVisualBox(door, "RightDoorPocketMask", new Vector3(1.5f, 4.1f, 0.38f), new Vector3(3.1f, 2.08f, ExitDoor3D.FrameRoomSideCenterZ), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frameMaterial);
        AddVisualBox(door, "LeftFrame", new Vector3(0.5f, 4.55f, 0.58f), new Vector3(-2.1f, 2.3f, ExitDoor3D.FrameRoomSideCenterZ), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frameMaterial);
        AddVisualBox(door, "RightFrame", new Vector3(0.5f, 4.55f, 0.58f), new Vector3(2.1f, 2.3f, ExitDoor3D.FrameRoomSideCenterZ), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frameMaterial);
        AddVisualBox(door, "Header", new Vector3(4.7f, 0.58f, 0.58f), new Vector3(0.0f, 4.55f, ExitDoor3D.FrameRoomSideCenterZ), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frameMaterial);
        StaticBody3D frameCollision = new()
        {
            Name = "FrameCollision",
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.8f, Bounce = 0.0f },
        };
        foreach ((string hitboxName, Vector3 hitboxSize, Vector3 hitboxPosition) in new[]
        {
            ("LeftPocketHitbox", new Vector3(1.5f, 4.1f, 0.38f), new Vector3(-3.1f, 2.08f, ExitDoor3D.FrameRoomSideCenterZ)),
            ("RightPocketHitbox", new Vector3(1.5f, 4.1f, 0.38f), new Vector3(3.1f, 2.08f, ExitDoor3D.FrameRoomSideCenterZ)),
            ("LeftFrameHitbox", new Vector3(0.5f, 4.55f, 0.58f), new Vector3(-2.1f, 2.3f, ExitDoor3D.FrameRoomSideCenterZ)),
            ("RightFrameHitbox", new Vector3(0.5f, 4.55f, 0.58f), new Vector3(2.1f, 2.3f, ExitDoor3D.FrameRoomSideCenterZ)),
            ("HeaderHitbox", new Vector3(4.7f, 0.58f, 0.58f), new Vector3(0.0f, 4.55f, ExitDoor3D.FrameRoomSideCenterZ)),
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
        AddVisualBox(door, "CenterSeam", new Vector3(0.1f, 3.45f, 0.08f), new Vector3(0.0f, 2.08f, 0.19f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, recessMaterial);
        AddVisualBox(door, "LeftHandle", new Vector3(0.1f, 0.9f, 0.08f), new Vector3(-0.22f, 2.08f, 0.21f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, markingMaterial);
        AddVisualBox(door, "RightHandle", new Vector3(0.1f, 0.9f, 0.08f), new Vector3(0.22f, 2.08f, 0.21f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, markingMaterial);
        AddVisualBox(door, "ChevronLeft", new Vector3(1.08f, 0.14f, 0.12f), new Vector3(-0.43f, 4.48f, 0.86f), new Vector3(0.0f, 0.0f, Mathf.DegToRad(-30.0f)), string.Empty, Colors.White, 0.0f, 1.0f, markingMaterial);
        AddVisualBox(door, "ChevronRight", new Vector3(1.08f, 0.14f, 0.12f), new Vector3(0.43f, 4.48f, 0.86f), new Vector3(0.0f, 0.0f, Mathf.DegToRad(30.0f)), string.Empty, Colors.White, 0.0f, 1.0f, markingMaterial);

        StaticBody3D closedDoorBlocker = new()
        {
            Name = "ClosedDoorBlocker",
            Position = new Vector3(0.0f, 2.08f, -0.03f),
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.8f, Bounce = 0.0f },
        };
        closedDoorBlocker.AddChild(new CollisionShape3D
        {
            Name = "CollisionShape3D",
            Shape = new BoxShape3D { Size = new Vector3(3.55f, 3.72f, 0.34f) },
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
        ClearDoorwayBlockers(parent, door);
        AddExitPlatformWallBridge(parent, door);
        Callable.From(() => FinalizeFloorButtonsAndDoorIndicators(parent, door)).CallDeferred();
        return door;
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
            "res://assets/textures/sugar_glaze.svg",
            new Color("98d6bd"),
            0.08f,
            0.46f,
            emissionEnabled: true,
            emission: new Color("28674f"));

        const float indicatorWidth = 0.46f;
        float spacing = buttons.Length == 1
            ? 0.0f
            : Mathf.Min(0.56f, 3.7f / (buttons.Length - 1));
        float firstX = -((buttons.Length - 1) * spacing * 0.5f);
        float housingWidth = Mathf.Max(0.88f, ((buttons.Length - 1) * spacing) + indicatorWidth + 0.34f);
        AddVisualBox(
            door,
            "ButtonIndicatorHousing",
            new Vector3(housingWidth, 0.32f, 0.16f),
            new Vector3(0.0f, ExitDoor3D.FrameOuterHeight + 0.16f, 0.80f),
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
                new Vector3(indicatorWidth, 0.22f, 0.08f),
                new Vector3(firstX + (index * spacing), ExitDoor3D.FrameOuterHeight + 0.16f, 0.925f),
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

    private static void AddExitPlatformWallBridge(Node parent, ExitDoor3D door)
    {
        Node3D? shell = parent.GetNodeOrNull<Node3D>("RoomShell");
        CollisionShape3D? nearestPlatform = null;
        Material? nearestMaterial = null;
        Vector3 nearestMinimum = Vector3.Zero;
        Vector3 nearestMaximum = Vector3.Zero;
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

            Material? sourceMaterial = body.GetChildren().OfType<MeshInstance3D>().FirstOrDefault()?.MaterialOverride;
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
                    nearestMaterial = sourceMaterial;
                    nearestMinimum = minimum;
                    nearestMaximum = maximum;
                }
            }
        }

        if (nearestPlatform is null || bestDistance > 1.05f)
        {
            GD.PushError($"Exit door in {parent.Name} could not find its adjoining platform.");
            return;
        }

        float frameBackZ = ExitDoor3D.FrameRoomSideCenterZ - (ExitDoor3D.FrameDepth * 0.5f);
        float platformEdgeZ = nearestMinimum.Z;
        if (platformEdgeZ <= frameBackZ + 0.02f)
        {
            return;
        }

        float minimumX = Mathf.Max(
            Mathf.Min(nearestMinimum.X, -ExitDoor3D.CorridorInteriorWidth * 0.5f),
            -ExitDoor3D.FrameOuterHalfWidth);
        float maximumX = Mathf.Min(
            Mathf.Max(nearestMaximum.X, ExitDoor3D.CorridorInteriorWidth * 0.5f),
            ExitDoor3D.FrameOuterHalfWidth);
        const float overlap = 0.04f;
        float minimumZ = frameBackZ - overlap;
        float maximumZ = platformEdgeZ + overlap;
        Material bridgeMaterial = nearestMaterial ?? CreateMaterial(
            "res://assets/textures/diamond_plate.png",
            new Color("a5ada8"),
            0.32f,
            0.66f);
        AddCorridorPanel(
            door,
            "ExitPlatformWallBridge",
            new Vector3(maximumX - minimumX, 0.24f, maximumZ - minimumZ),
            new Vector3((minimumX + maximumX) * 0.5f, 0.0f, (minimumZ + maximumZ) * 0.5f),
            bridgeMaterial);
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
                AddClearedBlockerPiece(
                    door,
                    body.Name.ToString(),
                    "Below",
                    new Vector3(Mathf.Max(blockerMinimum.X, -1.82f), blockerMinimum.Y, blockerMinimum.Z),
                    new Vector3(Mathf.Min(blockerMaximum.X, 1.82f), 0.3f, blockerMaximum.Z),
                    material);
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

        AddCorridorPanel(
            door,
            $"ExitDoorwayTrim{sourceName}{side}",
            size,
            (minimum + maximum) * 0.5f,
            material);
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
render_mode diffuse_burley, specular_schlick_ggx;
uniform sampler2D corridor_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float corridor_length = 9.6;
varying float corridor_depth;
void vertex() {
    corridor_depth = clamp((-VERTEX.z + corridor_length * 0.5) / corridor_length, 0.0, 1.0);
}
void fragment() {
    vec3 detail = texture(corridor_texture, UV * vec2(8.0, 5.0)).rgb;
    float fade = smoothstep(0.0, 1.0, corridor_depth);
    ALBEDO = detail * mix(vec3(0.095, 0.115, 0.125), vec3(0.002, 0.003, 0.004), fade);
    ROUGHNESS = 0.96;
    METALLIC = 0.02;
}",
        };
        ShaderMaterial material = new() { Shader = shader };
        material.SetShaderParameter("corridor_texture", GD.Load<Texture2D>("res://assets/textures/industrial_concrete.png"));
        material.SetShaderParameter("corridor_length", length);
        return material;
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
        AddDoorwayWallPiece(shell, wallName, "Left", sideWall, wall.Position, wallSize, wallHorizontalMin, openingHorizontalMin, wallVerticalMin, wallVerticalMax, wallMaterial);
        AddDoorwayWallPiece(shell, wallName, "Right", sideWall, wall.Position, wallSize, openingHorizontalMax, wallHorizontalMax, wallVerticalMin, wallVerticalMax, wallMaterial);
        AddDoorwayWallPiece(shell, wallName, "Below", sideWall, wall.Position, wallSize, openingHorizontalMin, openingHorizontalMax, wallVerticalMin, openingVerticalMin, wallMaterial);
        AddDoorwayWallPiece(shell, wallName, "Above", sideWall, wall.Position, wallSize, openingHorizontalMin, openingHorizontalMax, openingVerticalMax, wallVerticalMax, wallMaterial);

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
