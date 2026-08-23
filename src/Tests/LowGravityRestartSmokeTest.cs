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
        RoomRuntime? room = GD.Load<PackedScene>("res://scenes/Room11.tscn")?.Instantiate<RoomRuntime>();
        if (room is null)
        {
            Fail("Room 11 could not be instantiated.");
            return;
        }

        AddChild(room);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        DisableRuntimeAudio(room);
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
            Fail("The player did not enter Room 11's low-gravity volume before restart.");
            return;
        }

        Vector3 staleLinearMomentum = new(6.0f, 9.0f, -4.0f);
        Vector3 staleAngularMomentum = new(4.0f, -3.0f, 2.0f);
        player.LinearVelocity = staleLinearMomentum;
        player.AngularVelocity = staleAngularMomentum;
        room.RestartRoom();
        if (!player.LinearVelocity.IsZeroApprox() || !player.AngularVelocity.IsZeroApprox())
        {
            Fail($"Restart did not clear momentum immediately: linear={player.LinearVelocity}, angular={player.AngularVelocity}.");
            return;
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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
        player.BeginManualRestartStabilization();
        player.SetDeferred(RigidBody3D.PropertyName.LinearVelocity, staleLinearMomentum);
        player.SetDeferred(RigidBody3D.PropertyName.AngularVelocity, staleAngularMomentum);
        if (!player.LinearVelocity.IsZeroApprox() || !player.AngularVelocity.IsZeroApprox() ||
            !player.Freeze || !player.IsManualRestartStabilizing)
        {
            GetTree().Paused = false;
            Fail($"Paused restart did not enter the pinned zero-momentum window before resuming physics: linear={player.LinearVelocity}, angular={player.AngularVelocity}, frozen={player.Freeze}, stabilizing={player.IsManualRestartStabilizing}.");
            return;
        }
        Vector3 pausedSpawnPosition = player.GlobalPosition;
        float pausedSpawnY = pausedSpawnPosition.Y;
        GetTree().Paused = false;
        float pausedMaximumRise = 0.0f;
        float pausedMaximumUpwardSpeed = 0.0f;
        float pausedMaximumPositionDrift = 0.0f;
        for (int frame = 0; frame < 40; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            pausedMaximumRise = Mathf.Max(pausedMaximumRise, player.GlobalPosition.Y - pausedSpawnY);
            pausedMaximumUpwardSpeed = Mathf.Max(pausedMaximumUpwardSpeed, player.LinearVelocity.Y);
            if (player.IsManualRestartStabilizing)
            {
                pausedMaximumPositionDrift = Mathf.Max(
                    pausedMaximumPositionDrift,
                    player.GlobalPosition.DistanceTo(pausedSpawnPosition));
            }
        }
        if (player.Freeze || player.IsManualRestartStabilizing || player.IsManualRestartImpactSuppressionActive ||
            lowGravity.ContainsBody(player) || pausedMaximumPositionDrift > 0.001f ||
            pausedMaximumRise > 0.002f || pausedMaximumUpwardSpeed > 0.01f)
        {
            Fail($"Paused Room 11 restart did not survive a late stale write or settle without bounce: frozen={player.Freeze}, stabilizing={player.IsManualRestartStabilizing}, impact_suppression={player.IsManualRestartImpactSuppressionActive}, still_overlapping={lowGravity.ContainsBody(player)}, pinned_drift={pausedMaximumPositionDrift:F4}, rise={pausedMaximumRise:F4}, upward_speed={pausedMaximumUpwardSpeed:F4}.");
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
        SendKey(Key.D, false);
        if (speedBeforeSecondKey < 10.0f || sustainedRightSpeed < 2.5f ||
            sustainedForwardSpeed < speedBeforeSecondKey - 0.5f)
        {
            SendKey(Key.W, false);
            Fail($"Adding D while W was already at speed did not add a sustained right component while retaining forward momentum: before={speedBeforeSecondKey:F3}, right={sustainedRightSpeed:F3}, forward={sustainedForwardSpeed:F3}.");
            return;
        }

        for (int forwardOnlyFrame = 0; forwardOnlyFrame < 30; forwardOnlyFrame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (player.CurrentMoveInput.X != 0.0f || player.CurrentMoveInput.Y >= 0.0f)
            {
                SendKey(Key.W, false);
                Fail($"Releasing D while holding W did not preserve forward-only input on frame {forwardOnlyFrame}: input={player.CurrentMoveInput}.");
                return;
            }
        }
        float rightSpeedWhileStillHoldingW = player.LinearVelocity.X;
        float activeSteeringDecay = sustainedRightSpeed - rightSpeedWhileStillHoldingW;
        float speedBeforeCoasting = new Vector2(player.LinearVelocity.X, player.LinearVelocity.Z).Length();
        SendKey(Key.W, false);
        for (int coastFrame = 0; coastFrame < 30; coastFrame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        float speedAfterCoasting = new Vector2(player.LinearVelocity.X, player.LinearVelocity.Z).Length();
        float coastingDecay = speedBeforeCoasting - speedAfterCoasting;
        if (rightSpeedWhileStillHoldingW < sustainedRightSpeed - 0.45f || activeSteeringDecay < -0.08f || activeSteeringDecay > 0.45f ||
            coastingDecay < 0.45f || coastingDecay <= activeSteeringDecay + 0.20f ||
            player.CurrentMoveInput != Vector2.Zero)
        {
            Fail($"Vector damping did not keep the released diagonal component while another key remained held, or did not restore normal coasting afterward: held_right={rightSpeedWhileStillHoldingW:F3}, active_decay={activeSteeringDecay:F3}, coast_decay={coastingDecay:F3}, released_input={player.CurrentMoveInput}.");
            return;
        }

        player.ResetTo(diagonalStart);
        for (int settleFrame = 0; settleFrame < 3; settleFrame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        SendKey(Key.W, true);
        for (int frame = 0; frame < 70; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        SendKey(Key.D, true);
        for (int frame = 0; frame < 45; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        float rightSpeedBeforeReversal = player.LinearVelocity.X;
        SendKey(Key.D, false);
        SendKey(Key.A, true);
        bool crossedHorizontalZero = false;
        bool acceleratedLeftAfterCrossing = false;
        float minimumLeftSpeed = 0.0f;
        for (int frame = 0; frame < 90; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (player.CurrentMoveInput.X >= 0.0f || player.CurrentMoveInput.Y >= 0.0f)
            {
                SendKey(Key.W, false);
                SendKey(Key.A, false);
                Fail($"W+A was not continuously held during horizontal reversal on frame {frame}: input={player.CurrentMoveInput}.");
                return;
            }
            crossedHorizontalZero |= player.LinearVelocity.X <= 0.0f;
            minimumLeftSpeed = Mathf.Min(minimumLeftSpeed, player.LinearVelocity.X);
            acceleratedLeftAfterCrossing |= crossedHorizontalZero && player.LinearVelocity.X < -2.0f;
        }
        float forwardSpeedAfterReversal = -player.LinearVelocity.Z;
        SendKey(Key.W, false);
        SendKey(Key.A, false);
        if (rightSpeedBeforeReversal < 2.0f || !crossedHorizontalZero || !acceleratedLeftAfterCrossing ||
            minimumLeftSpeed > -2.0f || forwardSpeedAfterReversal < 8.0f)
        {
            Fail($"W+D to W+A locked the horizontal axis instead of crossing zero: before_right={rightSpeedBeforeReversal:F3}, crossed={crossedHorizontalZero}, minimum_x={minimumLeftSpeed:F3}, forward={forwardSpeedAfterReversal:F3}.");
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

        GD.Print($"LOW_GRAVITY_RESTART_PASS: Room 11 cleared linear and angular momentum on every axis, stayed pinned for 12 physics frames, and suppressed contact bounce for 18 released frames despite a late stale write (drift={pausedMaximumPositionDrift:F4}, rise={pausedMaximumRise:F4}, upward_speed={pausedMaximumUpwardSpeed:F4}); adding D after W reached {speedBeforeSecondKey:F2} m/s sustained {sustainedRightSpeed:F2} m/s right and {sustainedForwardSpeed:F2} m/s forward; W+D to W+A crossed horizontal zero from {rightSpeedBeforeReversal:F2} to {minimumLeftSpeed:F2} m/s while retaining {forwardSpeedAfterReversal:F2} m/s forward; after D release the right component decayed only {activeSteeringDecay:F3} m/s while W remained held, versus {coastingDecay:F3} m/s vector coasting with no keys; WA/WD/SA/SD remained two-axis input for every held frame.");
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

    private static void DisableRuntimeAudio(Node root)
    {
        foreach (Node node in EnumerateDescendants(root))
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
        }
    }

    private static IEnumerable<Node> EnumerateDescendants(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            yield return child;
            foreach (Node descendant in EnumerateDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private void Fail(string message)
    {
        GD.PushError($"LOW_GRAVITY_RESTART_FAIL: {message}");
        GetTree().Quit(1);
    }
}
