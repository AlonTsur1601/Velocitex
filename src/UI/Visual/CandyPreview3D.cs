using Godot;
using Velocitex.Core.Profile;
using Velocitex.Core.Save;
using Velocitex.Gameplay.Visual;

namespace Velocitex.UI.Visual;

public partial class CandyPreview3D : Node3D
{
    [Export] public NodePath BallPath { get; set; } = "Ball";
    [Export] public NodePath TrailRootPath { get; set; } = "TrailRoot";

    public bool MotionEnabled { get; set; } = true;
    public string AppliedPatternId { get; private set; } = "none";
    public string AppliedTrailId { get; private set; } = "off";
    public string AppliedCrownId => _crown?.AppliedCrownId ?? "none-crown";
    public bool IsCrownVisible => _crown?.Visible == true;
    public Vector3 CrownLocalPosition => _crown?.Position ?? Vector3.Zero;
    public Vector3 BallLocalPosition => _ball?.Position ?? Vector3.Zero;

    private MeshInstance3D _ball = null!;
    private Node3D _trailRoot = null!;
    private ShaderMaterial _candyMaterial = null!;
    private StandardMaterial3D _trailMaterial = null!;
    private CandyCrown3D _crown = null!;
    private float _elapsed;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _ball = GetNode<MeshInstance3D>(BallPath);
        _trailRoot = GetNode<Node3D>(TrailRootPath);
        _candyMaterial = (ShaderMaterial)_ball.MaterialOverride.Duplicate();
        _ball.MaterialOverride = _candyMaterial;
        BuildTrailDots();
        // Center on the actual preview mesh (which is intentionally offset in
        // its viewport) and place the band directly on its 0.90 m radius.
        _crown = new CandyCrown3D
        {
            Name = "Crown",
            Position = _ball.Position + Vector3.Up * 0.91f,
            Scale = Vector3.One * CandyCrown3D.VisualScale,
        };
        AddChild(_crown);
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || !MotionEnabled)
        {
            return;
        }

        _elapsed += (float)delta;
        _ball.Rotation = new Vector3(_elapsed * 0.28f, _elapsed * 0.72f, Mathf.Sin(_elapsed * 0.7f) * 0.08f);
        for (int index = 0; index < _trailRoot.GetChildCount(); index++)
        {
            if (_trailRoot.GetChild(index) is Node3D dot)
            {
                Vector3 position = dot.Position;
                position.Y = Mathf.Sin((_elapsed * 2.2f) - (index * 0.7f)) * 0.08f;
                dot.Position = position;
            }
        }
    }

    public void Apply(PlayerProfile profile)
    {
        CosmeticDefinition trail = CosmeticCatalog.Find(CosmeticKind.Trail, profile.TrailId)
            ?? CosmeticCatalog.Find(CosmeticKind.Trail, "off")!;

        AppliedPatternId = profile.PatternId;
        AppliedTrailId = profile.TrailId;
        CandyVisualStyle.ApplyCandyMaterial(_candyMaterial, profile);
        _crown.Apply(profile.CrownId);

        bool showTrail = !string.Equals(trail.Id, "off", StringComparison.Ordinal);
        _trailRoot.Visible = showTrail;
        if (showTrail)
        {
            Color trailColor = CandyVisualStyle.ResolveTrailColor(trail.Id);
            trailColor.A = 0.72f;
            _trailMaterial.AlbedoColor = trailColor;
            _trailMaterial.Emission = new Color(trailColor.R, trailColor.G, trailColor.B, 1.0f);
        }
    }

    private void BuildTrailDots()
    {
        _trailMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            EmissionEnabled = true,
            EmissionEnergyMultiplier = 1.35f,
        };
        SphereMesh dotMesh = new()
        {
            Radius = 0.14f,
            Height = 0.28f,
            RadialSegments = 16,
            Rings = 8,
        };

        for (int index = 0; index < 6; index++)
        {
            float scale = 1.0f - (index * 0.11f);
            MeshInstance3D dot = new()
            {
                Mesh = dotMesh,
                MaterialOverride = _trailMaterial,
                Position = new Vector3(-0.52f - (index * 0.18f), 0.0f, 0.12f + (index * 0.04f)),
                Scale = Vector3.One * scale,
            };
            _trailRoot.AddChild(dot);
        }
    }
}
