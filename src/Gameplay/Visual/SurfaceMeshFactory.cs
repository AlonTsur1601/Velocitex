using Godot;

namespace Velocitex.Gameplay.Visual;

public static class SurfaceMeshFactory
{
    public const float DefaultTileWorldSize = 12.0f;

    public static ArrayMesh CreateTiledBox(Vector3 size, float tileWorldSize = DefaultTileWorldSize)
    {
        float tile = Mathf.Max(0.25f, tileWorldSize);
        Vector3 half = size * 0.5f;
        SurfaceTool surface = new();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        AddQuad(surface,
            new Vector3(-half.X, -half.Y, half.Z), new Vector3(-half.X, half.Y, half.Z),
            new Vector3(half.X, half.Y, half.Z), new Vector3(half.X, -half.Y, half.Z),
            Vector3.Back, size.X / tile, size.Y / tile);
        AddQuad(surface,
            new Vector3(half.X, -half.Y, -half.Z), new Vector3(half.X, half.Y, -half.Z),
            new Vector3(-half.X, half.Y, -half.Z), new Vector3(-half.X, -half.Y, -half.Z),
            Vector3.Forward, size.X / tile, size.Y / tile);
        AddQuad(surface,
            new Vector3(half.X, -half.Y, half.Z), new Vector3(half.X, half.Y, half.Z),
            new Vector3(half.X, half.Y, -half.Z), new Vector3(half.X, -half.Y, -half.Z),
            Vector3.Right, size.Z / tile, size.Y / tile);
        AddQuad(surface,
            new Vector3(-half.X, -half.Y, -half.Z), new Vector3(-half.X, half.Y, -half.Z),
            new Vector3(-half.X, half.Y, half.Z), new Vector3(-half.X, -half.Y, half.Z),
            Vector3.Left, size.Z / tile, size.Y / tile);
        AddQuad(surface,
            new Vector3(-half.X, half.Y, half.Z), new Vector3(-half.X, half.Y, -half.Z),
            new Vector3(half.X, half.Y, -half.Z), new Vector3(half.X, half.Y, half.Z),
            Vector3.Up, size.X / tile, size.Z / tile);
        AddQuad(surface,
            new Vector3(-half.X, -half.Y, -half.Z), new Vector3(-half.X, -half.Y, half.Z),
            new Vector3(half.X, -half.Y, half.Z), new Vector3(half.X, -half.Y, -half.Z),
            Vector3.Down, size.X / tile, size.Z / tile);

        return surface.Commit()!;
    }

    private static void AddQuad(
        SurfaceTool surface,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        Vector3 normal,
        float uTiles,
        float vTiles)
    {
        float u = Mathf.Max(0.125f, uTiles);
        float v = Mathf.Max(0.125f, vTiles);
        AddVertex(surface, a, normal, new Vector2(0.0f, v));
        AddVertex(surface, b, normal, Vector2.Zero);
        AddVertex(surface, c, normal, new Vector2(u, 0.0f));
        AddVertex(surface, a, normal, new Vector2(0.0f, v));
        AddVertex(surface, c, normal, new Vector2(u, 0.0f));
        AddVertex(surface, d, normal, new Vector2(u, v));
    }

    private static void AddVertex(SurfaceTool surface, Vector3 position, Vector3 normal, Vector2 uv)
    {
        surface.SetNormal(normal);
        surface.SetUV(uv);
        surface.AddVertex(position);
    }
}
