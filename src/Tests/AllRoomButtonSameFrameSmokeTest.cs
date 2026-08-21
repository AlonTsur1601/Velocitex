using Godot;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Interaction;
using Velocitex.Gameplay.Physics;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Tests;

public partial class AllRoomButtonSameFrameSmokeTest : Node
{
    private static readonly string[] RoomScenes = new[] { "res://scenes/MovementTestRoom.tscn" }
        .Concat(Enumerable.Range(2, 29).Select(room => $"res://scenes/Room{room:00}.tscn"))
        .ToArray();

    public override async void _Ready()
    {
        string? roomArgument = OS.GetCmdlineUserArgs()
            .FirstOrDefault(argument => argument.StartsWith("--button-room=", StringComparison.Ordinal));
        int requestedRoom = 0;
        if (roomArgument is not null &&
            (!int.TryParse(roomArgument["--button-room=".Length..], out requestedRoom) || requestedRoom < 1 || requestedRoom > 30))
        {
            Fail($"Invalid room selector: {roomArgument}.");
            return;
        }

        int buttonCount = 0;
        for (int roomIndex = 0; roomIndex < RoomScenes.Length; roomIndex++)
        {
            string scenePath = RoomScenes[roomIndex];
            int roomNumber = roomIndex + 1;
            if (requestedRoom != 0 && roomNumber != requestedRoom)
            {
                continue;
            }
            PackedScene? packed = GD.Load<PackedScene>(scenePath);
            RoomRuntime? room = packed?.Instantiate<RoomRuntime>();
            if (room is null)
            {
                Fail($"Could not instantiate {scenePath}.");
                return;
            }

            AddChild(room);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            RouteCheckpoint3D[] buttons = EnumerateDescendants(room).OfType<RouteCheckpoint3D>().ToArray();
            MeshInstance3D[] orphanSharedButtonVisuals = EnumerateDescendants(room)
                .OfType<MeshInstance3D>()
                .Where(mesh => mesh.Name.ToString() is "FramePlate" or "InsetPlate")
                .Where(mesh => mesh.GetParent() is not RouteCheckpoint3D)
                .ToArray();
            if (orphanSharedButtonVisuals.Length > 0)
            {
                Fail($"{scenePath}: found button plate visuals outside the shared RouteCheckpoint3D implementation: {string.Join(", ", orphanSharedButtonVisuals.Select(mesh => mesh.GetPath()))}.");
                return;
            }
            RouteCheckpoint3D[] physicalButtonsOutsideSharedAutomaticPath = buttons
                .Where(button => button.IsPhysicalFloorButton && !button.AutomaticPressEnabled)
                .ToArray();
            if (physicalButtonsOutsideSharedAutomaticPath.Length > 0)
            {
                Fail($"{scenePath}: found physical buttons outside the shared automatic press path: {string.Join(", ", physicalButtonsOutsideSharedAutomaticPath.Select(button => button.Name))}.");
                return;
            }
            PlayerBall? player = room.GetNodeOrNull<PlayerBall>("Player");
            if (buttons.Length > 0 && player is null)
            {
                Fail($"{scenePath}: contains {buttons.Length} buttons but no PlayerBall for the physical press path.");
                return;
            }

            foreach (RouteCheckpoint3D button in buttons)
            {
                MeshInstance3D plate = button.GetNode<MeshInstance3D>("InsetPlate");
                float expectedPressedY = (-button.TriggerSize.Y * 0.42f) - 0.16f;
                float expectedIdleY = (-button.TriggerSize.Y * 0.42f) + 0.08f;
                bool physicalPressDispatched = false;
                bool acceptedPressWasAlreadyDepressedInCallback = false;
                void ObservePhysicalPress(RouteCheckpoint3D pressed, PlayerBall enteredPlayer)
                {
                    if (pressed == button && enteredPlayer == player)
                    {
                        physicalPressDispatched = true;
                        acceptedPressWasAlreadyDepressedInCallback = !pressed.IsActivated ||
                            Mathf.IsEqualApprox(plate.Position.Y, expectedPressedY);
                    }
                }

                button.Entered += ObservePhysicalPress;
                player!.Freeze = false;
                player.ResetTo(new Transform3D(Basis.Identity, plate.GlobalPosition + (Vector3.Up * 1.6f)));
                for (int frame = 0; frame < 60 && !physicalPressDispatched; frame++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                }
                button.Entered -= ObservePhysicalPress;
                bool physicalPressHadCorrectImmediateState = button.IsActivated
                    ? Mathf.IsEqualApprox(plate.Position.Y, expectedPressedY) &&
                      !button.IsDeniedFeedbackActive &&
                      !IsVisiblyRed(plate.MaterialOverride)
                    : !button.IsDeniedFeedbackActive &&
                      Mathf.IsEqualApprox(plate.Position.Y, expectedIdleY) &&
                      !IsVisiblyRed(plate.MaterialOverride);
                if (!physicalPressDispatched || !physicalPressHadCorrectImmediateState ||
                    (button.IsActivated && !acceptedPressWasAlreadyDepressedInCallback))
                {
                    Fail($"{scenePath} / {button.Name}: real floor contact did not apply the correct same-frame state. dispatched={physicalPressDispatched}, callback_depressed={acceptedPressWasAlreadyDepressedInCallback}, activated={button.IsActivated}, denied={button.IsDeniedFeedbackActive}, actualY={plate.Position.Y:0.####}, expectedPressedY={expectedPressedY:0.####}, expectedIdleY={expectedIdleY:0.####}.");
                    return;
                }

                bool activatedByPress = button.IsActivated;
                bool sawBrightPhase = false;
                bool sawIdlePhase = false;
                for (int frame = 0; frame < 50; frame++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    bool red = IsVisiblyRed(plate.MaterialOverride);
                    sawBrightPhase |= button.IsDeniedFeedbackActive && red;
                    sawIdlePhase |= button.IsDeniedFeedbackActive && button.IsShowingIdleVisual && !red;

                    if (activatedByPress && (button.IsDeniedFeedbackActive || red))
                    {
                        Fail($"{scenePath} / {button.Name}: an accepted press entered denied feedback during its animation.");
                        return;
                    }
                }

                if (!activatedByPress &&
                    (!sawBrightPhase || !sawIdlePhase ||
                     button.IsDeniedFeedbackActive || IsVisiblyRed(plate.MaterialOverride) ||
                     !Mathf.IsEqualApprox(plate.Position.Y, expectedIdleY)))
                {
                    Fail($"{scenePath} / {button.Name}: denied feedback did not alternate red and the true idle material before returning to idle. bright={sawBrightPhase}, idle={sawIdlePhase}, active={button.IsDeniedFeedbackActive}, finalRed={IsVisiblyRed(plate.MaterialOverride)}, actualY={plate.Position.Y:0.####}.");
                    return;
                }

                GD.Print($"ROOM_BUTTON_SAME_FRAME_ITEM_PASS: Room {roomNumber:00} / {button.Name}.");
                button.ResetCheckpoint();
                if (roomNumber == 27 && button.CheckpointIndex == 0)
                {
                    MechanicalLever? lever = EnumerateDescendants(room).OfType<MechanicalLever>().FirstOrDefault();
                    if (lever is null)
                    {
                        Fail("Room 27 did not expose its required reversal lever.");
                        return;
                    }
                    player.GlobalPosition = lever.GlobalPosition + (Vector3.Up * 0.6f);
                    player.ForceUpdateTransform();
                    lever.Interact(player);
                }
                buttonCount++;
            }

            GD.Print($"ROOM_BUTTON_SAME_FRAME_COUNT: Room {roomNumber:00}: {buttons.Length} buttons.");

            PlayerCannon3D[] playerCannons = EnumerateDescendants(room).OfType<PlayerCannon3D>().ToArray();
            InterferenceCannon3D[] interferenceCannons = EnumerateDescendants(room).OfType<InterferenceCannon3D>().ToArray();
            PlayerCannon3D? playerCannonWithoutHitbox = playerCannons.FirstOrDefault(cannon => !cannon.HasSolidBodyHitbox);
            InterferenceCannon3D? interferenceCannonWithoutHitbox = interferenceCannons.FirstOrDefault(cannon => !cannon.HasSolidBodyHitbox);
            if (playerCannonWithoutHitbox is not null || interferenceCannonWithoutHitbox is not null)
            {
                Fail($"Room {roomNumber:00} contains a cannon without a complete solid hitbox: {playerCannonWithoutHitbox?.Name ?? interferenceCannonWithoutHitbox?.Name}.");
                return;
            }
            GD.Print($"ROOM_CANNON_HITBOX_COUNT: Room {roomNumber:00}: player={playerCannons.Length}, interference={interferenceCannons.Length}.");

            room.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (requestedRoom != 0)
        {
            GD.Print($"ROOM_BUTTON_SAME_FRAME_ROOM_PASS: Room {requestedRoom:00}, verified {buttonCount} buttons through real floor contact.");
        }
        else
        {
            GD.Print($"ALL_ROOM_BUTTON_SAME_FRAME_PASS: verified {buttonCount} buttons across Rooms 01-30.");
        }
        GetTree().Quit(0);
    }

    private static IEnumerable<Node> EnumerateDescendants(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            yield return child;
            foreach (Node descendant in EnumerateDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool IsVisiblyRed(Material? material)
    {
        if (material is not StandardMaterial3D standard)
        {
            return false;
        }

        Color color = standard.AlbedoColor;
        Color emission = standard.Emission;
        return standard.EmissionEnabled &&
            color.R >= color.G * 1.7f &&
            color.R >= color.B * 1.35f &&
            emission.R >= emission.G * 2.0f &&
            emission.R >= emission.B * 1.5f;
    }

    private static float RedBrightness(Material? material)
    {
        return material is StandardMaterial3D standard && IsVisiblyRed(material)
            ? standard.AlbedoColor.R
            : 0.0f;
    }

    private void Fail(string message)
    {
        GD.PushError($"ALL_ROOM_BUTTON_SAME_FRAME_FAIL: {message}");
        GetTree().Quit(1);
    }
}
