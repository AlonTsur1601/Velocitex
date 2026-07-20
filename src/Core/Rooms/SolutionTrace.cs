using Godot;

namespace Velocitex.Core.Rooms;

[GlobalClass]
public partial class SolutionTrace : Resource
{
    [Export] public string RoomId { get; set; } = string.Empty;
    [Export] public int PhysicsTicksPerSecond { get; set; } = 60;
    [Export] public Godot.Collections.Array<Vector2> MoveInputs { get; set; } = new();
    [Export] public byte[] ActionFlags { get; set; } = Array.Empty<byte>();
    [Export] public int[] MoveDurationsTicks { get; set; } = Array.Empty<int>();
    [Export] public bool HoldLastInput { get; set; }
}
