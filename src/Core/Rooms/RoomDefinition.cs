using Godot;

namespace Velocitex.Core.Rooms;

[GlobalClass]
public partial class RoomDefinition : Resource
{
    [Export] public int Number { get; set; } = 1;
    [Export] public string Id { get; set; } = "room-01";
    [Export] public string DisplayName { get; set; } = "The Drop";
    [Export] public string ChapterId { get; set; } = "coin-mechanism";
    [Export] public string PostRoomDialogueId { get; set; } = string.Empty;
    [Export] public PackedScene? Scene { get; set; }
    [Export] public Texture2D? MechanicIcon { get; set; }
    [Export] public Godot.Collections.Array<StringName> AdvancementIds { get; set; } = new();
}

