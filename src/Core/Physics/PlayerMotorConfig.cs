using Godot;

namespace Velocitex.Core.Physics;

[GlobalClass]
public partial class PlayerMotorConfig : Resource
{
    [Export] public float GroundAcceleration { get; set; } = 22.0f;
    [Export] public float GroundBraking { get; set; } = 14.0f;
    [Export] public float CoastingDeceleration { get; set; } = 1.35f;
    [Export] public float MaximumDriveSpeed { get; set; } = 12.0f;
    [Export] public float MinimumGroundNormalY { get; set; } = 0.55f;
    [Export] public float CameraSensitivity { get; set; } = 0.0025f;
}
