using Godot;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Tests;

public partial class Room02RouteFeedbackSmokeTest : Node
{
    public override async void _Ready()
    {
        PackedScene? packed = GD.Load<PackedScene>("res://scenes/Room02.tscn");
        RoomRuntime? room = packed?.Instantiate<RoomRuntime>();
        if (room is null)
        {
            Fail("Room 02 could not be instantiated.");
            return;
        }

        AddChild(room);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        PlayerBall? player = room.GetNodeOrNull<PlayerBall>("Player");
        ExitDoor3D? door = room.GetNodeOrNull<ExitDoor3D>("ExitDoor");
        CollisionShape3D? routeLock = door?.GetNodeOrNull<CollisionShape3D>("RouteLockBarrier/RouteLockCollision");
        CollisionShape3D? closedDoorBlocker = door?.GetNodeOrNull<CollisionShape3D>("ClosedDoorBlocker/CollisionShape3D");
        RouteCheckpoint3D[] checkpoints = EnumerateDescendants(room)
            .OfType<RouteCheckpoint3D>()
            .OrderBy(checkpoint => checkpoint.CheckpointIndex)
            .ToArray();
        if (player is null || door is null || routeLock is null || closedDoorBlocker is null || checkpoints.Length != 4)
        {
            Fail("Room 02 is missing its player, four route buttons or route-locked exit.");
            return;
        }

        if (door.ProcessMode == ProcessModeEnum.Disabled || routeLock.Disabled || closedDoorBlocker.Disabled)
        {
            Fail("The visible exit door or one of its physical blockers was missing before completing the route.");
            return;
        }

        if (checkpoints[2].GlobalPosition.X >= checkpoints[0].GlobalPosition.X)
        {
            Fail("Route button 3 is not on the left side of route button 1 when viewed from the room start.");
            return;
        }

        MeshInstance3D wrongPlate = checkpoints[2].GetNode<MeshInstance3D>("InsetPlate");
        MeshInstance3D wrongFrame = checkpoints[2].GetNode<MeshInstance3D>("FramePlate");
        Material? idleMaterial = wrongPlate.MaterialOverride;
        Material? frameMaterial = wrongFrame.MaterialOverride;
        await MovePlayerTo(player, checkpoints[2], Vector3.Zero);
        if (checkpoints[2].IsActivated || wrongPlate.MaterialOverride == idleMaterial)
        {
            Fail($"An out-of-order button activated or failed to flash its red error material. activated={checkpoints[2].IsActivated}, denied={checkpoints[2].IsDeniedFeedbackActive}, grounded={player.IsGrounded}, playerY={player.GlobalPosition.Y:0.###}, plateY={wrongPlate.GlobalPosition.Y:0.###}.");
            return;
        }
        if (wrongPlate.MaterialOverride is not StandardMaterial3D errorMaterial ||
            errorMaterial.AlbedoTexture is not null ||
            errorMaterial.AlbedoColor.R <= errorMaterial.AlbedoColor.G * 2.0f ||
            wrongFrame.MaterialOverride != frameMaterial ||
            wrongPlate.GetChildren().Any(child => child.Name.ToString().StartsWith("SequencePip", StringComparison.Ordinal) && child is GeometryInstance3D { Visible: false }))
        {
            Fail("The out-of-order feedback did not flash only the inset button red while preserving its base and number dots.");
            return;
        }

        float pipHeightBeforeActivation = GetPipHeightAbovePlate(checkpoints[0]);

        for (int frame = 0; frame < 30; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (wrongPlate.MaterialOverride != idleMaterial)
        {
            Fail("The out-of-order red flash did not restore the button's normal material.");
            return;
        }

        for (int index = 0; index < checkpoints.Length; index++)
        {
            await MovePlayerAway(player);
            Vector3 velocity = index == 2 ? Vector3.Right * 0.5f : Vector3.Forward * 2.0f;
            await MovePlayerTo(player, checkpoints[index], velocity);
            if (!checkpoints[index].IsActivated)
            {
                Fail($"Route button {index + 1} did not activate in the intended order.");
                return;
            }
        }

        float pipHeightAfterActivation = GetPipHeightAbovePlate(checkpoints[0]);
        if (Mathf.Abs(pipHeightBeforeActivation - 0.065f) > 0.001f ||
            Mathf.Abs(pipHeightAfterActivation - pipHeightBeforeActivation) > 0.01f)
        {
            Fail("The sequence dots are not embedded into the floor button or detached after it was pressed.");
            return;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        int doorIndicators = door.GetChildren().Count(child => child.Name.ToString().StartsWith("ButtonRequirementIndicator", StringComparison.Ordinal));
        if (doorIndicators != 4)
        {
            Fail($"Room 02's four floor buttons produced {doorIndicators} door indicators instead of four.");
            return;
        }
        if (door.ProcessMode == ProcessModeEnum.Disabled || !routeLock.Disabled)
        {
            Fail("Completing the four-button sequence did not unlock the exit.");
            return;
        }

        GD.Print("ROOM02_ROUTE_FEEDBACK_PASS: out-of-order input flashed red, the four-button sequence activated in order and the exit stayed physically locked until completion.");
        StopAndReleaseAudio(room);
        room.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(0);
    }

    private async Task MovePlayerAway(PlayerBall player)
    {
        player.Freeze = true;
        player.GlobalPosition = new Vector3(0.0f, 9.0f, 30.0f);
        player.LinearVelocity = Vector3.Zero;
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    private async Task MovePlayerTo(PlayerBall player, RouteCheckpoint3D checkpoint, Vector3 velocity)
    {
        player.Freeze = false;
        player.GravityScale = 0.0f;
        MeshInstance3D plate = checkpoint.GetNode<MeshInstance3D>("InsetPlate");
        player.GlobalPosition = new Vector3(checkpoint.GlobalPosition.X, plate.GlobalPosition.Y + 0.6f, checkpoint.GlobalPosition.Z);
        player.LinearVelocity = velocity;
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
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

    private static float GetPipHeightAbovePlate(RouteCheckpoint3D checkpoint)
    {
        MeshInstance3D plate = checkpoint.GetNode<MeshInstance3D>("InsetPlate");
        MeshInstance3D pip = plate.GetChildren().OfType<MeshInstance3D>().First(child => child.Name.ToString().StartsWith("SequencePip", StringComparison.Ordinal));
        return pip.GlobalPosition.Y - plate.GlobalPosition.Y;
    }

    private static void StopAndReleaseAudio(Node node)
    {
        if (node is AudioStreamPlayer player)
        {
            player.Stop();
            player.Stream = null;
        }
        else if (node is AudioStreamPlayer3D player3D)
        {
            player3D.Stop();
            player3D.Stream = null;
        }
        foreach (Node child in node.GetChildren())
        {
            StopAndReleaseAudio(child);
        }
    }

    private void Fail(string message)
    {
        GD.PushError($"ROOM02_ROUTE_FEEDBACK_FAIL: {message}");
        GetTree().Quit(1);
    }
}
