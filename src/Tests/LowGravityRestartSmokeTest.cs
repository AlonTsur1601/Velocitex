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

        Vector3 staleLinearMomentum = new(6.0f, 9.0f, -4.0f);
        Vector3 staleAngularMomentum = new(4.0f, -3.0f, 2.0f);
        player.LinearVelocity = staleLinearMomentum;
        player.AngularVelocity = staleAngularMomentum;
        player.SetDeferred(RigidBody3D.PropertyName.LinearVelocity, staleLinearMomentum);
        player.SetDeferred(RigidBody3D.PropertyName.AngularVelocity, staleAngularMomentum);
        room.RestartRoom();
        if (!player.LinearVelocity.IsZeroApprox() || !player.AngularVelocity.IsZeroApprox())
        {
            Fail($"Restart did not clear momentum immediately: linear={player.LinearVelocity}, angular={player.AngularVelocity}.");
            return;
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!player.LinearVelocity.IsZeroApprox() || !player.AngularVelocity.IsZeroApprox())
        {
            Fail($"A stale physics write restored momentum after restart: linear={player.LinearVelocity}, angular={player.AngularVelocity}.");
            return;
        }
        if (player.Freeze)
        {
            Fail("Restart left the player frozen instead of completing the same-frame momentum reset.");
            return;
        }
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
            Fail($"Restart did not clear low-gravity lift in its own frame: frozen={player.Freeze}, still_overlapping={lowGravity.ContainsBody(player)}, rise={maximumRise:F4}, upward_speed={maximumUpwardSpeed:F4}.");
            return;
        }

        // The real pause-menu restart calls RestartRoom while the SceneTree is
        // paused, then resumes physics. Reproduce that ordering as well; Area3D
        // overlap notifications from the old position must not reapply a force
        // after the teleport to the spawn floor.
        player.GlobalPosition = lowGravity.GlobalPosition;
        player.LinearVelocity = Vector3.Zero;
        for (int frame = 0; frame < 4; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        if (!lowGravity.ContainsBody(player))
        {
            Fail("The player did not re-enter low gravity before the paused restart case.");
            return;
        }

        GetTree().Paused = true;
        room.RestartRoom();
        if (!player.LinearVelocity.IsZeroApprox() || !player.AngularVelocity.IsZeroApprox() || player.Freeze)
        {
            Fail($"Paused restart did not clear momentum before resuming physics: linear={player.LinearVelocity}, angular={player.AngularVelocity}, frozen={player.Freeze}.");
            return;
        }
        float pausedSpawnY = player.GlobalPosition.Y;
        GetTree().Paused = false;
        float pausedMaximumRise = 0.0f;
        float pausedMaximumUpwardSpeed = 0.0f;
        for (int frame = 0; frame < 18; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            pausedMaximumRise = Mathf.Max(pausedMaximumRise, player.GlobalPosition.Y - pausedSpawnY);
            pausedMaximumUpwardSpeed = Mathf.Max(pausedMaximumUpwardSpeed, player.LinearVelocity.Y);
        }
        if (player.Freeze || lowGravity.ContainsBody(player) || pausedMaximumRise > 0.035f || pausedMaximumUpwardSpeed > 0.12f)
        {
            Fail($"Paused restart did not preserve the same-frame zero momentum: frozen={player.Freeze}, still_overlapping={lowGravity.ContainsBody(player)}, rise={pausedMaximumRise:F4}, upward_speed={pausedMaximumUpwardSpeed:F4}.");
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
        float sequentialAxisDifference = Mathf.Abs(sustainedRightSpeed - sustainedForwardSpeed);
        if (speedBeforeSecondKey < 10.0f || sustainedRightSpeed < 7.5f || sustainedForwardSpeed < 7.5f || sequentialAxisDifference > 0.18f)
        {
            Fail($"Adding D while W was already at speed did not converge to equal sustained axes: before={speedBeforeSecondKey:F3}, right={sustainedRightSpeed:F3}, forward={sustainedForwardSpeed:F3}, difference={sequentialAxisDifference:F3}.");
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
            SendKey(second, true);
            float maximumVelocityDifference = 0.0f;
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

                Vector3 sampleRight = player.MovementBasis!.GlobalBasis.X.Slide(Vector3.Up).Normalized();
                Vector3 sampleForward = (-player.MovementBasis.GlobalBasis.Z).Slide(Vector3.Up).Normalized();
                float rightSpeed = player.LinearVelocity.Dot(sampleRight) * Mathf.Sign(expected.X);
                float forwardSpeed = player.LinearVelocity.Dot(sampleForward) * -Mathf.Sign(expected.Y);
                if (rightSpeed > 0.05f || forwardSpeed > 0.05f)
                {
                    maximumVelocityDifference = Mathf.Max(maximumVelocityDifference, Mathf.Abs(rightSpeed - forwardSpeed));
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
            float distanceDifference = Mathf.Abs(rightDistance - forwardDistance);
            if (rightDistance < 0.08f || forwardDistance < 0.08f ||
                maximumVelocityDifference > 0.12f || distanceDifference > 0.08f ||
                player.CurrentMoveInput != Vector2.Zero)
            {
                Fail($"Held diagonal {first}+{second} was not symmetric or did not release cleanly: right={rightDistance:F3}, forward={forwardDistance:F3}, distance_difference={distanceDifference:F3}, maximum_velocity_difference={maximumVelocityDifference:F3}, released_input={player.CurrentMoveInput}.");
                return;
            }
        }

        GD.Print($"LOW_GRAVITY_RESTART_PASS: Room 15 cleared linear and angular momentum on every axis in the restart frame, then exited the old force volume without a jump both live (rise={maximumRise:F4}, upward_speed={maximumUpwardSpeed:F4}) and through the paused menu path (rise={pausedMaximumRise:F4}, upward_speed={pausedMaximumUpwardSpeed:F4}); adding D after W reached {speedBeforeSecondKey:F2} m/s sustained {sustainedRightSpeed:F2} m/s right and {sustainedForwardSpeed:F2} m/s forward; WA/WD/SA/SD remained two-axis input for every held frame.");
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
