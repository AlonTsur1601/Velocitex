using Godot;

namespace Velocitex;

/// <summary>
/// Temporary startup scene used to verify the local toolchain before gameplay systems are added.
/// </summary>
public partial class Bootstrap : Node
{
    public override void _Ready()
    {
        Engine.PhysicsTicksPerSecond = 60;
        GD.Print($"Velocitex bootstrap ready. Godot {Engine.GetVersionInfo()["string"]}; physics={Engine.PhysicsTicksPerSecond} Hz.");
    }
}

