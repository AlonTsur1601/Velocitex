using Godot;

namespace Velocitex.Gameplay.Visual;

public static class SurfaceDetail
{
    private readonly record struct ContainedOverlay(Vector2 Center, Vector2 Size);

    private static readonly string[] GrimeVariants =
    {
        "res://assets/textures/overlays/grime.svg",
        "res://assets/textures/overlays/grime_02.svg",
        "res://assets/textures/overlays/grime_03.svg",
        "res://assets/textures/overlays/grime_04.svg",
        "res://assets/textures/overlays/grime_05.svg",
    };

    public static void AddBoxWear(Node3D parent, string surfaceName, Vector3 size, string baseTexturePath)
    {
        float smallest = Mathf.Min(size.X, Mathf.Min(size.Y, size.Z));
        float middle = size.X + size.Y + size.Z - smallest - Mathf.Max(size.X, Mathf.Max(size.Y, size.Z));
        if (string.IsNullOrWhiteSpace(baseTexturePath) || middle < 2.8f)
        {
            return;
        }

        string[] textures;
        Color tint;
        float opacity;
        string microTexture;
        float microOpacity;
        if (baseTexturePath.EndsWith("frictionless_glass.svg", StringComparison.OrdinalIgnoreCase))
        {
            textures = new[] { "res://assets/textures/overlays/scratches.svg", "res://assets/textures/overlays/edge_scuffs.svg" };
            tint = new Color("e9fbff");
            opacity = 0.24f;
            microTexture = "res://assets/textures/overlays/micro_grain.svg";
            microOpacity = 0.3f;
        }
        else if (baseTexturePath.EndsWith("rubber_chevrons.svg", StringComparison.OrdinalIgnoreCase))
        {
            textures = new[] { "res://assets/textures/overlays/edge_scuffs.svg", "res://assets/textures/overlays/grime.svg" };
            tint = new Color("263b37");
            opacity = 0.3f;
            microTexture = "res://assets/textures/overlays/micro_grain.svg";
            microOpacity = 0.38f;
        }
        else if (baseTexturePath.EndsWith("caramel_plates.svg", StringComparison.OrdinalIgnoreCase) ||
                 baseTexturePath.EndsWith("sugar_glaze.svg", StringComparison.OrdinalIgnoreCase))
        {
            textures = new[] { "res://assets/textures/overlays/sugar_dust.svg", "res://assets/textures/overlays/drips.svg", "res://assets/textures/overlays/edge_scuffs.svg" };
            tint = new Color("6f432d");
            opacity = 0.31f;
            microTexture = "res://assets/textures/overlays/micro_grain.svg";
            microOpacity = 0.36f;
        }
        else if (baseTexturePath.EndsWith("industrial_concrete.png", StringComparison.OrdinalIgnoreCase))
        {
            textures = new[] { "res://assets/textures/overlays/grime.svg", "res://assets/textures/overlays/cracks.svg", "res://assets/textures/overlays/edge_scuffs.svg" };
            tint = new Color("3d4544");
            opacity = 0.34f;
            microTexture = "res://assets/textures/overlays/micro_concrete.png";
            microOpacity = 0.16f;
        }
        else if (baseTexturePath.EndsWith("hazard_grate.svg", StringComparison.OrdinalIgnoreCase))
        {
            textures = new[] { "res://assets/textures/overlays/grime.svg", "res://assets/textures/overlays/oil_rings.svg" };
            tint = new Color("211c19");
            opacity = 0.4f;
            microTexture = "res://assets/textures/overlays/micro_metal_wear.png";
            microOpacity = 0.2f;
        }
        else
        {
            textures = new[] { "res://assets/textures/overlays/grime.svg", "res://assets/textures/overlays/scratches.svg", "res://assets/textures/overlays/patina.svg", "res://assets/textures/overlays/edge_scuffs.svg" };
            tint = new Color("354247");
            opacity = 0.3f;
            microTexture = "res://assets/textures/overlays/micro_metal_wear.png";
            microOpacity = baseTexturePath.EndsWith("diamond_plate.png", StringComparison.OrdinalIgnoreCase) ? 0.12f : 0.18f;
        }

        int hash = StableHash(surfaceName);
        string texture = textures[(hash & 0x7fffffff) % textures.Length];
        float offsetA = ((((hash >> 8) & 255) / 255.0f) - 0.5f) * 0.48f;
        float offsetB = ((((hash >> 16) & 255) / 255.0f) - 0.5f) * 0.48f;
        float angle = Mathf.DegToRad(-17.0f + ((((hash >> 24) & 255) / 255.0f) * 34.0f));
        const float lift = 0.012f;

        if (size.Y <= size.X && size.Y <= size.Z && size.X >= 3.5f && size.Z >= 3.5f)
        {
            bool ceiling = surfaceName.Contains("Ceiling", StringComparison.OrdinalIgnoreCase);
            ContainedOverlay wear = ConstrainOverlayToFace(
                new Vector2(size.X, size.Z),
                new Vector2(offsetA * size.X, offsetB * size.Z),
                new Vector2(Mathf.Clamp(size.X * 0.58f, 3.2f, 9.0f), Mathf.Clamp(size.Z * 0.54f, 3.0f, 8.0f)),
                angle);
            Vector3 detailPosition = new(wear.Center.X, ceiling ? -(size.Y * 0.5f) - lift : (size.Y * 0.5f) + lift, wear.Center.Y);
            Vector3 detailRotation = new(ceiling ? Mathf.Pi / 2.0f : -Mathf.Pi / 2.0f, 0.0f, angle);
            AddSurfaceGrain(
                parent,
                new Vector3(0.0f, ceiling ? -(size.Y * 0.5f) - (lift * 0.55f) : (size.Y * 0.5f) + (lift * 0.55f), 0.0f),
                new Vector3(ceiling ? Mathf.Pi / 2.0f : -Mathf.Pi / 2.0f, 0.0f, 0.0f),
                new Vector2(size.X, size.Z),
                microTexture,
                microOpacity);
            AddWearStack(
                parent,
                detailPosition,
                detailRotation,
                wear.Size,
                texture,
                tint,
                opacity);
            return;
        }

        if (size.X <= size.Y && size.X <= size.Z && size.Y >= 3.5f && size.Z >= 3.5f)
        {
            bool positiveFace = surfaceName.Contains("Left", StringComparison.OrdinalIgnoreCase) ||
                (!surfaceName.Contains("Right", StringComparison.OrdinalIgnoreCase) && (hash & 1) == 0);
            ContainedOverlay wear = ConstrainOverlayToFace(
                new Vector2(size.Z, size.Y),
                new Vector2(offsetA * size.Z, offsetB * size.Y),
                new Vector2(
                    Mathf.Clamp(size.Z * 0.58f, 3.2f, 10.0f),
                    (hash & 3) == 0
                        ? Mathf.Clamp(size.Y * 0.92f, 4.0f, 14.0f)
                        : Mathf.Clamp(size.Y * 0.56f, 3.2f, 9.0f)),
                angle);
            Vector3 detailPosition = new(positiveFace ? (size.X * 0.5f) + lift : -(size.X * 0.5f) - lift, wear.Center.Y, wear.Center.X);
            Vector3 detailRotation = new(0.0f, positiveFace ? Mathf.Pi / 2.0f : -Mathf.Pi / 2.0f, angle);
            AddSurfaceGrain(
                parent,
                new Vector3(positiveFace ? (size.X * 0.5f) + (lift * 0.55f) : -(size.X * 0.5f) - (lift * 0.55f), 0.0f, 0.0f),
                new Vector3(0.0f, positiveFace ? Mathf.Pi / 2.0f : -Mathf.Pi / 2.0f, 0.0f),
                new Vector2(size.Z, size.Y),
                microTexture,
                microOpacity);
            AddWearStack(
                parent,
                detailPosition,
                detailRotation,
                wear.Size,
                texture,
                tint,
                opacity);
            return;
        }

        if (size.Z <= size.X && size.Z <= size.Y && size.X >= 3.5f && size.Y >= 3.5f)
        {
            bool positiveFace = surfaceName.Contains("Exit", StringComparison.OrdinalIgnoreCase) ||
                (!surfaceName.Contains("Back", StringComparison.OrdinalIgnoreCase) && (hash & 1) == 0);
            ContainedOverlay wear = ConstrainOverlayToFace(
                new Vector2(size.X, size.Y),
                new Vector2(offsetA * size.X, offsetB * size.Y),
                new Vector2(
                    Mathf.Clamp(size.X * 0.58f, 3.2f, 10.0f),
                    (hash & 3) == 0
                        ? Mathf.Clamp(size.Y * 0.92f, 4.0f, 14.0f)
                        : Mathf.Clamp(size.Y * 0.56f, 3.2f, 9.0f)),
                angle);
            Vector3 detailPosition = new(wear.Center.X, wear.Center.Y, positiveFace ? (size.Z * 0.5f) + lift : -(size.Z * 0.5f) - lift);
            Vector3 detailRotation = new(0.0f, positiveFace ? 0.0f : Mathf.Pi, angle);
            AddSurfaceGrain(
                parent,
                new Vector3(0.0f, 0.0f, positiveFace ? (size.Z * 0.5f) + (lift * 0.55f) : -(size.Z * 0.5f) - (lift * 0.55f)),
                new Vector3(0.0f, positiveFace ? 0.0f : Mathf.Pi, 0.0f),
                new Vector2(size.X, size.Y),
                microTexture,
                microOpacity);
            AddWearStack(
                parent,
                detailPosition,
                detailRotation,
                wear.Size,
                texture,
                tint,
                opacity);
        }
    }

    private static void AddSurfaceGrain(
        Node3D parent,
        Vector3 position,
        Vector3 rotation,
        Vector2 size,
        string texturePath,
        float opacity)
    {
        const float microDetailWorldSize = 3.0f;
        AddOverlay(
            parent,
            "SurfaceMicroGrain",
            position,
            rotation,
            size * 0.992f,
            texturePath,
            Colors.White,
            opacity,
            new Vector2(Mathf.Max(size.X / microDetailWorldSize, 0.125f), Mathf.Max(size.Y / microDetailWorldSize, 0.125f)),
            containWithinQuad: false);
    }

    private static void AddWearStack(
        Node3D parent,
        Vector3 position,
        Vector3 rotation,
        Vector2 size,
        string wearTexture,
        Color tint,
        float opacity)
    {
        Vector2 localizedSize = new(
            Mathf.Max(size.X, 0.48f),
            Mathf.Max(size.Y, 0.48f));
        MeshInstance3D wear = AddOverlay(parent, "AutomaticWear", position, rotation, localizedSize, wearTexture, tint, opacity);
        AddOverlay(
            wear,
            "MicroGrain",
            new Vector3(0.0f, 0.0f, 0.006f),
            Vector3.Zero,
            localizedSize * 0.96f,
            "res://assets/textures/overlays/micro_grain.svg",
            tint.Lerp(Colors.White, 0.24f),
            Mathf.Clamp(opacity * 0.82f, 0.2f, 0.34f),
            containWithinQuad: false);
    }

    private static ContainedOverlay ConstrainOverlayToFace(
        Vector2 faceSize,
        Vector2 desiredCenter,
        Vector2 desiredSize,
        float angle)
    {
        Vector2 available = new(
            Mathf.Max(faceSize.X - 0.08f, 0.08f),
            Mathf.Max(faceSize.Y - 0.08f, 0.08f));
        float cosine = Mathf.Abs(Mathf.Cos(angle));
        float sine = Mathf.Abs(Mathf.Sin(angle));
        float projectedWidth = (cosine * desiredSize.X) + (sine * desiredSize.Y);
        float projectedHeight = (sine * desiredSize.X) + (cosine * desiredSize.Y);
        float scale = Mathf.Min(
            1.0f,
            Mathf.Min(
                available.X / Mathf.Max(projectedWidth, 0.001f),
                available.Y / Mathf.Max(projectedHeight, 0.001f)));
        Vector2 containedSize = desiredSize * scale;
        Vector2 projectedHalfSize = new(
            ((cosine * containedSize.X) + (sine * containedSize.Y)) * 0.5f,
            ((sine * containedSize.X) + (cosine * containedSize.Y)) * 0.5f);
        Vector2 halfFace = faceSize * 0.5f;
        Vector2 containedCenter = new(
            Mathf.Clamp(desiredCenter.X, -halfFace.X + projectedHalfSize.X, halfFace.X - projectedHalfSize.X),
            Mathf.Clamp(desiredCenter.Y, -halfFace.Y + projectedHalfSize.Y, halfFace.Y - projectedHalfSize.Y));
        return new ContainedOverlay(containedCenter, containedSize);
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 17;
            foreach (char character in value)
            {
                hash = (hash * 31) + character;
            }

            return hash;
        }
    }

    private static string ResolveOverlayTexturePath(Node parent, string name, Vector3 position, string texturePath)
    {
        if (!texturePath.EndsWith("/grime.svg", StringComparison.OrdinalIgnoreCase))
        {
            return texturePath;
        }

        int hash = StableHash($"{parent.Name}|{name}");
        unchecked
        {
            hash = (hash * 31) + Mathf.RoundToInt(position.X * 1000.0f);
            hash = (hash * 31) + Mathf.RoundToInt(position.Y * 1000.0f);
            hash = (hash * 31) + Mathf.RoundToInt(position.Z * 1000.0f);
        }

        return GrimeVariants[(hash & 0x7fffffff) % GrimeVariants.Length];
    }

    public static MeshInstance3D AddOverlay(
        Node parent,
        string name,
        Vector3 position,
        Vector3 rotation,
        Vector2 size,
        string texturePath,
        Color tint,
        float opacity = 0.72f,
        Vector2? uvScale = null,
        bool containWithinQuad = true)
    {
        Vector2 resolvedUvScale = uvScale ?? Vector2.One;
        string resolvedTexturePath = ResolveOverlayTexturePath(parent, name, position, texturePath);
        Material material;
        if (containWithinQuad)
        {
            ShaderMaterial contained = new()
            {
                Shader = GD.Load<Shader>("res://resources/shaders/contained_overlay.gdshader"),
            };
            contained.SetShaderParameter("overlay_texture", GD.Load<Texture2D>(resolvedTexturePath));
            contained.SetShaderParameter("overlay_tint", tint);
            contained.SetShaderParameter("overlay_opacity", Mathf.Clamp(opacity, 0.0f, 1.0f));
            contained.SetShaderParameter("uv_scale", resolvedUvScale);
            material = contained;
        }
        else
        {
            Color color = tint;
            color.A = Mathf.Clamp(opacity, 0.0f, 1.0f);
            material = new StandardMaterial3D
            {
                AlbedoTexture = GD.Load<Texture2D>(resolvedTexturePath),
                AlbedoColor = color,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                Roughness = 0.92f,
                Metallic = 0.0f,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
                TextureRepeat = true,
                Uv1Scale = new Vector3(resolvedUvScale.X, resolvedUvScale.Y, 1.0f),
            };
        }

        MeshInstance3D overlay = new()
        {
            Name = name,
            Position = position,
            Rotation = rotation,
            Mesh = new QuadMesh { Size = size },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(overlay);
        return overlay;
    }
}
