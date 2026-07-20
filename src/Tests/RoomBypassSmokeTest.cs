using Godot;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Player;

namespace Velocitex.Tests;

public partial class RoomBypassSmokeTest : Node
{
    private const int ForwardOnlyTicks = 2600;
    private const int SteeringPatternTicks = 2600;
    private RoomRuntime? _activeRoom;
    private PlayerBall? _activePlayer;
    private PackedScene? _loadedScene;
    private bool _finishing;

    public override async void _Ready()
    {
        string[] arguments = OS.GetCmdlineUserArgs();
        string? forwardArgument = arguments.FirstOrDefault(value => value.StartsWith("--forward-bypass-room=", StringComparison.Ordinal));
        string? directArgument = arguments.FirstOrDefault(value => value.StartsWith("--direct-goal-bypass-room=", StringComparison.Ordinal));
        string? steeringArgument = arguments.FirstOrDefault(value => value.StartsWith("--steering-bypass-room=", StringComparison.Ordinal));
        string? selectedArgument = forwardArgument ?? directArgument ?? steeringArgument;
        if (selectedArgument is null || !int.TryParse(selectedArgument.Split('=')[1], out int roomNumber))
        {
            Fail("No valid room number was supplied.");
            return;
        }

        RoomCatalogEntry? entry = RoomCatalog.Find(roomNumber);
        PackedScene? packed = entry is null ? null : GD.Load<PackedScene>(entry.ScenePath);
        RoomRuntime? room = packed?.Instantiate<RoomRuntime>();
        if (entry is null || room is null)
        {
            Fail($"Room {roomNumber:00} could not be instantiated.");
            return;
        }

        room.RoomNumber = entry.Number;
        room.RoomId = entry.Id;
        room.RoomDisplayName = entry.DisplayName;
        AddChild(room);
        _loadedScene = packed;
        _activeRoom = room;
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        PlayerBall? player = room.GetNodeOrNull<PlayerBall>("Player");
        Area3D? goal = room.GetNodeOrNull<Area3D>("GoalCup");
        if (player is null || goal is null)
        {
            Fail($"Room {roomNumber:00} does not expose Player and GoalCup nodes.");
            return;
        }
        _activePlayer = player;

        if (directArgument is not null)
        {
            player.GravityScale = 0.0f;
            player.ResetTo(new Transform3D(Basis.Identity, goal.GlobalPosition));
            for (int tick = 0; tick < 20; tick++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }

            if (room.IsComplete)
            {
                Fail($"Room {roomNumber:00} accepted direct GoalCup entry without its intended route state.");
                return;
            }

            GD.Print($"DIRECT_GOAL_BYPASS_PASS: Room {roomNumber:00} rejected direct entry with all required mechanics untouched.");
            Finish(0);
            return;
        }

        if (steeringArgument is not null)
        {
            Vector2[][] patterns =
            {
                new[] { Vector2.Right },
                new[] { Vector2.Left },
                new[] { Vector2.Down },
                new[] { Vector2.Up, Vector2.Right, Vector2.Down, Vector2.Left },
                new[]
                {
                    new Vector2(1.0f, -1.0f).Normalized(),
                    new Vector2(1.0f, 1.0f).Normalized(),
                    new Vector2(-1.0f, 1.0f).Normalized(),
                    new Vector2(-1.0f, -1.0f).Normalized(),
                },
                new[]
                {
                    new Vector2(0.35f, -1.0f).Normalized(),
                    new Vector2(-0.8f, -0.4f).Normalized(),
                    new Vector2(1.0f, 0.2f).Normalized(),
                    new Vector2(-0.25f, 1.0f).Normalized(),
                },
            };

            for (int patternIndex = 0; patternIndex < patterns.Length; patternIndex++)
            {
                room.RestartRoom();
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                Vector2[] pattern = patterns[patternIndex];
                for (int tick = 0; tick < SteeringPatternTicks; tick++)
                {
                    player.SimulatedMoveInput = pattern[(tick / 150) % pattern.Length];
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    if (room.IsComplete)
                    {
                        Fail($"Room {roomNumber:00} completed with steering bypass pattern {patternIndex + 1} at tick {tick + 1}.");
                        return;
                    }
                }
            }

            GD.Print($"STEERING_BYPASS_PASS: Room {roomNumber:00} rejected {patterns.Length} sustained axis, loop and diagonal steering patterns without interactions.");
            Finish(0);
            return;
        }

        for (int tick = 0; tick < ForwardOnlyTicks; tick++)
        {
            player.SimulatedMoveInput = new Vector2(0.0f, -1.0f);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (room.IsComplete)
            {
                Fail($"Room {roomNumber:00} completed by holding forward for {tick + 1} ticks.");
                return;
            }
        }

        GD.Print($"FORWARD_BYPASS_PASS: Room {roomNumber:00} did not complete after {ForwardOnlyTicks} ticks of forward-only input.");
        Finish(0);
    }

    private async void Finish(int exitCode)
    {
        if (_finishing)
        {
            return;
        }
        _finishing = true;
        if (_activePlayer is not null)
        {
            _activePlayer.SimulatedMoveInput = null;
        }
        if (_activeRoom is not null)
        {
            StopAndReleaseAudio(_activeRoom);
        }
        _activeRoom?.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _activePlayer = null;
        _activeRoom = null;
        _loadedScene = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(exitCode);
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
        GD.PushError($"ROOM_BYPASS_FAIL: {message}");
        Finish(1);
    }
}
