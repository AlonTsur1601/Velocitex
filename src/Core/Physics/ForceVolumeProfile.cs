using Godot;

namespace Velocitex.Core.Physics;

public enum ForceVolumeKind
{
    Gravity,
    Wind,
    Suction,
    Magnetic,
}

[GlobalClass]
public partial class ForceVolumeProfile : Resource
{
    [Export] public ForceVolumeKind Kind { get; set; } = ForceVolumeKind.Gravity;
    [Export] public Vector3 Direction { get; set; } = Vector3.Down;
    [Export] public float Strength { get; set; } = 9.8f;
    [Export] public float FalloffExponent { get; set; }
    [Export] public float AirControlAcceleration { get; set; }
    [Export] public float AirControlMaximumSpeed { get; set; } = 8.0f;
}
