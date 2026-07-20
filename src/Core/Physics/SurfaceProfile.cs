using Godot;

namespace Velocitex.Core.Physics;

public enum SurfaceKind
{
    Standard,
    Frictionless,
    Sticky,
    Accelerator,
    SuperElastic,
    Absorbing,
    OneWayGrip,
    Brittle,
    Gelatin,
    MomentumBank,
}

[GlobalClass]
public partial class SurfaceProfile : Resource
{
    [Export] public SurfaceKind Kind { get; set; } = SurfaceKind.Standard;
    [Export] public float Friction { get; set; } = 0.8f;
    [Export] public float LinearDrag { get; set; }
    [Export] public float BounceMultiplier { get; set; } = 1.0f;
    [Export] public Vector3 Acceleration { get; set; } = Vector3.Zero;
    [Export] public Vector3 GripDirection { get; set; } = Vector3.Forward;
}

