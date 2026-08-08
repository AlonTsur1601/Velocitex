using Godot;
using Velocitex.Core.Profile;

namespace Velocitex.Gameplay.Visual;

public partial class CandyCrown3D : Node3D
{
    public const float VisualScale = 0.5f;
    public bool FollowParentUpright { get; set; }
    public string AppliedCrownId { get; private set; } = "none-crown";

    private readonly List<ShaderMaterial> _materials = new();

    public override void _Ready()
    {
        BuildCrown();
        if (FollowParentUpright)
        {
            TopLevel = true;
        }
    }

    public override void _Process(double delta)
    {
        if (FollowParentUpright && GetParent() is Node3D owner)
        {
            // At half scale the band's bottom sits 0.01 m below this node's
            // origin, so 0.61 m keeps it seated on the 0.60 m candy radius.
            GlobalTransform = new Transform3D(
                Basis.Identity.Scaled(Vector3.One * VisualScale),
                owner.GlobalPosition + Vector3.Up * 0.61f);
        }
    }

    public void Apply(string crownId)
    {
        AppliedCrownId = CosmeticCatalog.Find(CosmeticKind.Crown, crownId)?.Id ?? "none-crown";
        Visible = !string.Equals(AppliedCrownId, "none-crown", StringComparison.Ordinal);
        Color color = AppliedCrownId switch
        {
            "bronze-crown" => new Color("d67a3d"),
            "silver-crown" => new Color("ffffff"),
            "gold-crown" => new Color("ffb52e"),
            _ => Colors.Transparent,
        };
        foreach (ShaderMaterial material in _materials)
        {
            material.SetShaderParameter("base_color", color);
        }
    }

    private void BuildCrown()
    {
        Shader crownShader = new()
        {
            Code = """
                shader_type spatial;
                render_mode diffuse_burley, specular_schlick_ggx;
                uniform vec4 base_color : source_color = vec4(1.0);
                uniform float gameplay_boost = 0.0;
                void fragment() {
                    vec3 n = normalize(NORMAL);
                    vec3 v = normalize(VIEW);
                    float facing = max(dot(n, v), 0.0);
                    float rim = pow(1.0 - facing, 3.0);
                    vec3 shine_dir = normalize(vec3(-0.55, 0.72, 0.42));
                    vec3 reflected_view = reflect(-v, n);
                    vec3 reflection_dir = normalize(vec3(0.62, 0.58, 0.53));
                    vec3 reflection_dir_2 = normalize(vec3(-0.74, 0.38, -0.55));
                    float broad_shine = pow(max(dot(n, shine_dir), 0.0), 7.0);
                    float sharp_shine = pow(max(dot(n, shine_dir), 0.0), 34.0);
                    float reflected_glint = pow(max(dot(reflected_view, reflection_dir), 0.0), 18.0);
                    float reflected_glint_2 = pow(max(dot(reflected_view, reflection_dir_2), 0.0), 28.0);
                    float metal_reflection = smoothstep(0.15, 0.92, reflected_view.y * 0.5 + 0.5);
                    ALBEDO = base_color.rgb * (0.78 + broad_shine * (0.72 + gameplay_boost * 0.24) + metal_reflection * (0.34 + gameplay_boost * 0.20));
                    METALLIC = 1.0;
                    ROUGHNESS = 0.005;
                    SPECULAR = 1.0;
                    CLEARCOAT = 1.0;
                    CLEARCOAT_ROUGHNESS = 0.0;
                    EMISSION = base_color.rgb * (0.38 + gameplay_boost * 0.14 + broad_shine * (0.62 + gameplay_boost * 0.32) + sharp_shine * (1.35 + gameplay_boost * 0.55) + reflected_glint * (1.85 + gameplay_boost * 0.85) + reflected_glint_2 * (1.35 + gameplay_boost * 0.65) + rim * (0.22 + gameplay_boost * 0.10));
                }
                """,
        };
        ShaderMaterial material = new() { Shader = crownShader };
        material.SetShaderParameter("gameplay_boost", FollowParentUpright ? 1.0f : 0.0f);
        _materials.Add(material);
        AddChild(new MeshInstance3D
        {
            Name = "CrownBand",
            Mesh = new CylinderMesh { TopRadius = 0.39f, BottomRadius = 0.43f, Height = 0.2f, RadialSegments = 32 },
            MaterialOverride = material,
            Position = Vector3.Up * 0.08f,
        });

        for (int index = 0; index < 6; index++)
        {
            float angle = Mathf.Tau * index / 6.0f;
            Vector3 radial = new(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
            float pointHeight = index % 2 == 0 ? 0.48f : 0.38f;
            AddChild(new MeshInstance3D
            {
                Name = $"CrownPoint{index + 1}",
                Mesh = new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.13f, Height = pointHeight, RadialSegments = 12 },
                MaterialOverride = material,
                Position = (radial * 0.31f) + Vector3.Up * (0.17f + pointHeight * 0.5f),
            });
            AddChild(new MeshInstance3D
            {
                Name = $"CrownTip{index + 1}",
                Mesh = new SphereMesh { Radius = 0.055f, Height = 0.11f, RadialSegments = 12, Rings = 6 },
                MaterialOverride = material,
                Position = (radial * 0.31f) + Vector3.Up * (0.17f + pointHeight),
            });
        }
        Apply("none-crown");
    }
}
