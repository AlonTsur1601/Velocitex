using Godot;
using Velocitex.Core.Interaction;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Tests;

public partial class FlightGateRouteRequirementSmokeTest : Node
{
    private static readonly int[] GateRooms = { 1, 3, 5, 6, 9, 10, 20 };

    public override async void _Ready()
    {
        string? argument = OS.GetCmdlineUserArgs()
            .FirstOrDefault(value => value.StartsWith("--flight-gate-route-room=", StringComparison.Ordinal));
        string? gateIndexArgument = OS.GetCmdlineUserArgs()
            .FirstOrDefault(value => value.StartsWith("--flight-gate-disabled-index=", StringComparison.Ordinal));
        int disabledGateIndex = gateIndexArgument is not null &&
            int.TryParse(gateIndexArgument.Split('=')[1], out int parsedGateIndex)
                ? parsedGateIndex
                : -1;
        if (argument is null ||
            !int.TryParse(argument.Split('=')[1], out int roomNumber) ||
            !GateRooms.Contains(roomNumber))
        {
            Fail("Expected a campaign room that contains flight gates.");
            return;
        }

        RoomCatalogEntry? entry = RoomCatalog.Find(roomNumber);
        PackedScene? packed = entry is null ? null : GD.Load<PackedScene>(entry.ScenePath);
        RoomRuntime? room = packed?.Instantiate<RoomRuntime>();
        SolutionTrace? trace = GD.Load<SolutionTrace>($"res://resources/solutions/room_{roomNumber:00}_solution.tres");
        if (entry is null || room is null || trace is null)
        {
            Fail($"Room {roomNumber:00} or its SolutionTrace could not be loaded.");
            return;
        }

        room.RoomNumber = entry.Number;
        room.RoomId = entry.Id;
        room.RoomDisplayName = entry.DisplayName;
        AddChild(room);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        PlayerBall? player = room.GetNodeOrNull<PlayerBall>("Player");
        FlightGate3D[] gates = EnumerateDescendants(room).OfType<FlightGate3D>().ToArray();
        IInteractable[] interactables = EnumerateDescendants(room).OfType<IInteractable>().ToArray();
        if (player is null || gates.Length == 0)
        {
            Fail($"Room {roomNumber:00} does not expose the expected player and flight gates.");
            return;
        }

        if (disabledGateIndex >= gates.Length)
        {
            Fail($"Room {roomNumber:00} has no flight gate at index {disabledGateIndex}.");
            return;
        }

        foreach ((FlightGate3D gate, int index) in gates.Select((gate, index) => (gate, index)))
        {
            if (disabledGateIndex >= 0 && index != disabledGateIndex)
            {
                continue;
            }
            gate.MinimumExitSpeed = 0.0f;
            gate.SpeedGain = 0.0f;
            gate.SpeedMultiplier = 1.0f;
            gate.MaximumDownwardExitSpeed = float.PositiveInfinity;
        }

        int initialResetCount = player.ResetCount;
        int totalTicks = trace.MoveDurationsTicks.Sum() + 1000;
        for (int tick = 0; tick < totalTicks; tick++)
        {
            (Vector2 move, byte flags) = Resolve(trace, tick);
            player.SimulatedMoveInput = move;
            if ((flags & 1) != 0)
            {
                foreach (IInteractable interactable in interactables.Where(value => value.CanInteract(player)))
                {
                    interactable.Interact(player);
                }
            }

            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (room.IsComplete)
            {
                string gateEvidence = string.Join(", ", gates.Select((gate, index) =>
                    $"gate{index + 1}={gate.LastEntrySpeed:F2}->{gate.LastExitSpeed:F2}"));
                Fail($"Room {roomNumber:00} remained physically completable when flight gate {(disabledGateIndex < 0 ? "set" : (disabledGateIndex + 1).ToString())} supplied zero boost; player={player.GlobalPosition}, {gateEvidence}.");
                return;
            }

            if (player.ResetCount > initialResetCount)
            {
                GD.Print($"FLIGHT_GATE_ROUTE_REQUIREMENT_PASS: Room {roomNumber:00} fell to its hazard when flight gate {(disabledGateIndex < 0 ? "set" : (disabledGateIndex + 1).ToString())} supplied zero boost.");
                Finish(room, player, 0);
                return;
            }
        }

        string timeoutEvidence = string.Join(", ", gates.Select((gate, index) =>
            $"gate{index + 1}=active:{gate.IsActivated},speed:{gate.LastEntrySpeed:F2}->{gate.LastExitSpeed:F2}"));
        if (disabledGateIndex >= 0 && !gates[disabledGateIndex].IsActivated)
        {
            Fail($"Room {roomNumber:00} SolutionTrace did not traverse disabled flight gate {disabledGateIndex + 1}; player={player.GlobalPosition}, {timeoutEvidence}.");
            return;
        }

        GD.Print($"FLIGHT_GATE_ROUTE_REQUIREMENT_PASS: Room {roomNumber:00} remained locked when flight gate {(disabledGateIndex < 0 ? "set" : (disabledGateIndex + 1).ToString())} supplied zero boost; player={player.GlobalPosition}, {timeoutEvidence}.");
        Finish(room, player, 0);
    }

    private static (Vector2 Move, byte Flags) Resolve(SolutionTrace trace, int tick)
    {
        int remaining = tick;
        for (int index = 0; index < trace.MoveInputs.Count; index++)
        {
            int duration = trace.MoveDurationsTicks[index];
            if (remaining < duration)
            {
                return (trace.MoveInputs[index], trace.ActionFlags[index]);
            }
            remaining -= duration;
        }

        return trace.HoldLastInput
            ? (trace.MoveInputs[^1], (byte)0)
            : (Vector2.Zero, (byte)0);
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

    private void Finish(RoomRuntime room, PlayerBall player, int exitCode)
    {
        player.SimulatedMoveInput = null;
        room.QueueFree();
        QuitAfterCleanup(exitCode);
    }

    private async void QuitAfterCleanup(int exitCode)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(exitCode);
    }

    private void Fail(string message)
    {
        GD.PushError($"FLIGHT_GATE_ROUTE_REQUIREMENT_FAIL: {message}");
        GetTree().Quit(1);
    }
}
