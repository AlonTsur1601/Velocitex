using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Physics;
using Velocitex.Gameplay.Player;

namespace Velocitex.Tests;

public partial class LowGravityRestartSmokeTest : Node
{
    public override async void _Ready()
    {
        RoomRuntime? room = GD.Load<PackedScene>("res://scenes/Room15.tscn")?.Instantiate<RoomRuntime>();
        if (room is null)
        {
            Fail("Room 15 could not be instantiated.");
            return;
        }

        AddChild(room);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        PlayerBall player = room.GetNode<PlayerBall>("Player");
        ForceVolume3D lowGravity = room.GetNode<ForceVolume3D>("LowGravityVolume");
        player.Freeze = false;
        player.GlobalPosition = lowGravity.GlobalPosition;
        player.LinearVelocity = Vector3.Zero;
        for (int frame = 0; frame < 4; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        if (!lowGravity.ContainsBody(player))
        {
            Fail("The player did not enter Room 15's low-gravity volume before restart.");
            return;
        }

        room.RestartRoom();
        float spawnY = player.GlobalPosition.Y;
        float maximumRise = 0.0f;
        float maximumUpwardSpeed = 0.0f;
        for (int frame = 0; frame < 18; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            maximumRise = Mathf.Max(maximumRise, player.GlobalPosition.Y - spawnY);
            maximumUpwardSpeed = Mathf.Max(maximumUpwardSpeed, player.LinearVelocity.Y);
        }

        if (player.Freeze || lowGravity.ContainsBody(player) || maximumRise > 0.035f || maximumUpwardSpeed > 0.12f)
        {
            Fail($"Restart retained low-gravity lift: frozen={player.Freeze}, still_overlapping={lowGravity.ContainsBody(player)}, rise={maximumRise:F4}, upward_speed={maximumUpwardSpeed:F4}.");
            return;
        }

        StaticBody3D inputTestFloor = new()
        {
            Name = "SustainedDiagonalInputFloor",
            Position = new Vector3(500.0f, 0.0f, 500.0f),
        };
        inputTestFloor.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(80.0f, 1.0f, 80.0f) },
        });
        AddChild(inputTestFloor);
        Node3D inputBasis = new() { Name = "SustainedDiagonalInputBasis" };
        AddChild(inputBasis);
        player.MovementBasis = inputBasis;
        Transform3D diagonalStart = new(Basis.Identity, new Vector3(500.0f, 1.1f, 500.0f));
        player.ResetTo(diagonalStart);
        for (int settleFrame = 0; settleFrame < 4; settleFrame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        SendKey(Key.W, true);
        for (int forwardFrame = 0; forwardFrame < 70; forwardFrame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        float speedBeforeSecondKey = -player.LinearVelocity.Z;
        SendKey(Key.D, true);
        for (int diagonalFrame = 0; diagonalFrame < 60; diagonalFrame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (player.CurrentMoveInput.X <= 0.0f || player.CurrentMoveInput.Y >= 0.0f)
            {
                SendKey(Key.W, false);
                SendKey(Key.D, false);
                Fail($"W+D stopped reporting both held axes on frame {diagonalFrame}: input={player.CurrentMoveInput}.");
                return;
            }
        }
        float sustainedRightSpeed = player.LinearVelocity.X;
        float sustainedForwardSpeed = -player.LinearVelocity.Z;
        SendKey(Key.W, false);
        SendKey(Key.D, false);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (speedBeforeSecondKey < 10.0f || sustainedRightSpeed < 5.5f || sustainedForwardSpeed < 5.5f)
        {
            Fail($"Adding D while W was already at speed produced only a momentary turn: before={speedBeforeSecondKey:F3}, right={sustainedRightSpeed:F3}, forward={sustainedForwardSpeed:F3}.");
            return;
        }

        (Key first, Key second, Vector2 expected)[] diagonalPairs =
        {
            (Key.W, Key.A, new Vector2(-1.0f, -1.0f)),
            (Key.W, Key.D, new Vector2(1.0f, -1.0f)),
            (Key.S, Key.A, new Vector2(-1.0f, 1.0f)),
            (Key.S, Key.D, new Vector2(1.0f, 1.0f)),
        };
        foreach ((Key first, Key second, Vector2 expected) in diagonalPairs)
        {
            player.ResetTo(diagonalStart);
            for (int settleFrame = 0; settleFrame < 3; settleFrame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }

            Vector3 startPosition = player.GlobalPosition;
            SendKey(first, true);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            SendKey(second, true);
            for (int heldFrame = 0; heldFrame < 45; heldFrame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                Vector2 input = player.CurrentMoveInput;
                if (Mathf.Sign(input.X) != Mathf.Sign(expected.X) ||
                    Mathf.Sign(input.Y) != Mathf.Sign(expected.Y) ||
                    input.X == 0.0f || input.Y == 0.0f)
                {
                    SendKey(first, false);
                    SendKey(second, false);
                    Fail($"Held diagonal {first}+{second} was lost on frame {heldFrame}: input={input}.");
                    return;
                }
            }

            Vector3 displacement = player.GlobalPosition - startPosition;
            Vector3 cameraRight = player.MovementBasis!.GlobalBasis.X.Slide(Vector3.Up).Normalized();
            Vector3 cameraForward = (-player.MovementBasis.GlobalBasis.Z).Slide(Vector3.Up).Normalized();
            float rightDistance = displacement.Dot(cameraRight) * Mathf.Sign(expected.X);
            float forwardDistance = displacement.Dot(cameraForward) * -Mathf.Sign(expected.Y);
            SendKey(first, false);
            SendKey(second, false);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (rightDistance < 0.08f || forwardDistance < 0.08f || player.CurrentMoveInput != Vector2.Zero)
            {
                Fail($"Held diagonal {first}+{second} did not produce sustained two-axis movement or release cleanly: right={rightDistance:F3}, forward={forwardDistance:F3}, released_input={player.CurrentMoveInput}.");
                return;
            }
        }

        GD.Print($"LOW_GRAVITY_RESTART_PASS: Room 15 restart exited the old force volume without a jump (rise={maximumRise:F4}, upward_speed={maximumUpwardSpeed:F4}); adding D after W reached {speedBeforeSecondKey:F2} m/s sustained {sustainedRightSpeed:F2} m/s right and {sustainedForwardSpeed:F2} m/s forward; WA/WD/SA/SD remained two-axis input for every held frame.");
        room.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(0);
    }

    private static void SendKey(Key key, bool pressed)
    {
        Godot.Input.ParseInputEvent(new InputEventKey
        {
            PhysicalKeycode = key,
            Pressed = pressed,
        });
    }

    private void Fail(string message)
    {
        GD.PushError($"LOW_GRAVITY_RESTART_FAIL: {message}");
        GetTree().Quit(1);
    }
}
