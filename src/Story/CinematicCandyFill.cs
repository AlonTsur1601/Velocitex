using Godot;

namespace Velocitex.Story;

internal static class CinematicCandyFill
{
    public const float CandyRadius = 0.055f;

    private static readonly Color[] CandyColors =
    {
        new("d93c50"),
        new("e7c84f"),
        new("59a86b"),
        new("586fc7"),
        new("884ba8"),
    };

    public static void AddPackedGlobe(Node3D parent, string name, Vector3 center, Vector3? reservedPosition = null)
    {
        const float fillRadius = 0.76f;
        const float horizontalStep = 0.108f;
        const float rowStep = 0.094f;
        const float layerStep = 0.096f;
        List<Vector3> positions = new();

        int layer = 0;
        for (float y = -fillRadius; y <= fillRadius; y += layerStep, layer++)
        {
            int row = 0;
            for (float z = -fillRadius; z <= fillRadius; z += rowStep, row++)
            {
                float xOffset = ((row + layer) & 1) == 0 ? 0.0f : horizontalStep * 0.5f;
                for (float x = -fillRadius + xOffset; x <= fillRadius; x += horizontalStep)
                {
                    Vector3 localPosition = new(x, y, z);
                    if (localPosition.LengthSquared() > fillRadius * fillRadius)
                    {
                        continue;
                    }

                    Vector3 worldPosition = center + localPosition;
                    if (reservedPosition.HasValue && worldPosition.DistanceSquaredTo(reservedPosition.Value) < 0.018f)
                    {
                        continue;
                    }

                    positions.Add(worldPosition);
                }
            }
        }

        StandardMaterial3D material = new()
        {
            AlbedoColor = Colors.White,
            Roughness = 0.34f,
            VertexColorUseAsAlbedo = true,
        };
        SphereMesh candyMesh = new()
        {
            Radius = CandyRadius,
            Height = CandyRadius * 2.0f,
            RadialSegments = 12,
            Rings = 6,
            Material = material,
        };
        MultiMesh multiMesh = new()
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = candyMesh,
            InstanceCount = positions.Count,
        };

        for (int index = 0; index < positions.Count; index++)
        {
            float yaw = Mathf.PosMod(index * 2.39996f, Mathf.Tau);
            Basis rotation = Basis.FromEuler(new Vector3(yaw * 0.37f, yaw, yaw * 0.19f));
            multiMesh.SetInstanceTransform(index, new Transform3D(rotation, positions[index]));
            multiMesh.SetInstanceColor(index, CandyColors[(index * 3 + (index / 7)) % CandyColors.Length]);
        }

        parent.AddChild(new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = multiMesh,
        });
    }
}
