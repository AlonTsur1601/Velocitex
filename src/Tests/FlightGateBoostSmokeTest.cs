using Godot;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Tests;

public partial class FlightGateBoostSmokeTest : Node3D
{
    public override async void _Ready()
    {
        FlightGate3D gate = new()
        {
            Name = "BoostGate",
            Radius = 2.2f,
            EnableAudio = false,
        };
        AddChild(gate);
        Vector3[] initialLatchRotations = new Vector3[4];
        for (int index = 0; index < initialLatchRotations.Length; index++)
        {
            initialLatchRotations[index] = gate.GetNode<Node3D>($"Latch{index}").Rotation;
        }

        PlayerBall player = GD.Load<PackedScene>("res://scenes/PlayerBall.tscn").Instantiate<PlayerBall>();
        player.GravityScale = 0.0f;
        player.Position = new Vector3(0.0f, 0.0f, 3.0f);
        AddChild(player);
        player.LinearVelocity = new Vector3(0.0f, 0.0f, -8.0f);

        for (int tick = 0; tick < 120 && !gate.IsActivated; tick++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        if (!gate.IsActivated ||
            gate.LastEntrySpeed < 7.5f ||
            gate.LastExitSpeed < gate.LastEntrySpeed + 6.9f ||
            player.LinearVelocity.Length() < gate.LastExitSpeed - 0.2f)
        {
            GD.PushError($"FLIGHT_GATE_BOOST_FAIL: active={gate.IsActivated}, entry={gate.LastEntrySpeed:F2}, exit={gate.LastExitSpeed:F2}, player={player.LinearVelocity.Length():F2}.");
            GetTree().Quit(1);
            return;
        }

        for (int index = 0; index < initialLatchRotations.Length; index++)
        {
            Vector3 currentRotation = gate.GetNode<Node3D>($"Latch{index}").Rotation;
            if (!currentRotation.IsEqualApprox(initialLatchRotations[index]))
            {
                GD.PushError($"FLIGHT_GATE_LATCH_FAIL: latch {index} rotated from {initialLatchRotations[index]} to {currentRotation} during activation.");
                GetTree().Quit(1);
                return;
            }
        }

        FlightGate3D centerInsideGate = new()
        {
            Name = "CenterInsideGate",
            Position = new Vector3(10.0f, 0.0f, 0.0f),
            Radius = 2.2f,
            EnableAudio = false,
        };
        AddChild(centerInsideGate);
        PlayerBall centerInsidePlayer = GD.Load<PackedScene>("res://scenes/PlayerBall.tscn").Instantiate<PlayerBall>();
        centerInsidePlayer.GravityScale = 0.0f;
        centerInsidePlayer.Position = centerInsideGate.Position + new Vector3(2.15f, 0.0f, 0.0f);
        AddChild(centerInsidePlayer);

        FlightGate3D centerOutsideGate = new()
        {
            Name = "CenterOutsideGate",
            Position = new Vector3(-10.0f, 0.0f, 0.0f),
            Radius = 2.2f,
            EnableAudio = false,
        };
        AddChild(centerOutsideGate);
        PlayerBall centerOutsidePlayer = GD.Load<PackedScene>("res://scenes/PlayerBall.tscn").Instantiate<PlayerBall>();
        centerOutsidePlayer.GravityScale = 0.0f;
        centerOutsidePlayer.Position = centerOutsideGate.Position + new Vector3(2.25f, 0.0f, 0.0f);
        AddChild(centerOutsidePlayer);

        for (int tick = 0; tick < 8; tick++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        if (!centerInsideGate.IsActivated || centerOutsideGate.IsActivated ||
            !Mathf.IsEqualApprox(centerInsideGate.TriggerRadius, centerInsideGate.Radius))
        {
            GD.PushError($"FLIGHT_GATE_CENTER_FAIL: inside={centerInsideGate.IsActivated}, outside={centerOutsideGate.IsActivated}, trigger={centerInsideGate.TriggerRadius:F2}, opening={centerInsideGate.Radius:F2}.");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"FLIGHT_GATE_BOOST_PASS: gate raised speed from {gate.LastEntrySpeed:F2} to {gate.LastExitSpeed:F2} m/s, accepted a center 0.05 m inside the opening, rejected one 0.05 m outside and kept its four side spikes oriented.");
        GetTree().Quit(0);
    }
}
