using Godot;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Physics;
using Velocitex.Gameplay.Player;

namespace Velocitex.Tests;

public partial class Room30MomentumThresholdSmokeTest : Node
{
    public override async void _Ready()
    {
        if (!await VerifyCharge(storedSpeed: 4.0f, shouldReachCatchDeck: false))
        {
            return;
        }
        if (!await VerifyCharge(storedSpeed: 16.0f, shouldReachCatchDeck: true))
        {
            return;
        }

        GD.Print("ROOM30_MOMENTUM_THRESHOLD_PASS: 1/8 falls before the catch deck while 4/8 lands on it through real physics.");
        GetTree().Quit(0);
    }

    private async Task<bool> VerifyCharge(float storedSpeed, bool shouldReachCatchDeck)
    {
        PackedScene? packed = GD.Load<PackedScene>("res://scenes/Room30.tscn");
        RoomRuntime? room = packed?.Instantiate<RoomRuntime>();
        if (room is null)
        {
            Fail("Could not instantiate Room 30.");
            return false;
        }

        AddChild(room);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        PlayerBall player = room.GetNode<PlayerBall>("Player");
        MomentumBank3D bank = room.GetNode<MomentumBank3D>("FinalMomentumBank");
        player.ResetTo(new Transform3D(Basis.Identity, bank.GlobalPosition + new Vector3(0.0f, 1.6f, 1.1f)));

        for (int frame = 0; frame < 30 && !bank.HasCharge; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        if (!bank.HasCharge)
        {
            Fail($"The bank did not capture the player for the {storedSpeed:0}/32 threshold run.");
            return false;
        }

        for (int frame = 0; frame < 900 && bank.StoredSpeed < storedSpeed; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        bank.Interact(player);
        int resetCount = player.ResetCount;
        bool reachedCatchDeck = false;
        bool fellBeforeCatchDeck = false;
        for (int frame = 0; frame < 600; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            reachedCatchDeck |= player.IsGrounded && player.GlobalPosition.Z <= -3.0f && player.GlobalPosition.Z >= -77.0f;
            if (player.ResetCount > resetCount)
            {
                fellBeforeCatchDeck = true;
                break;
            }
        }

        bool passed = shouldReachCatchDeck ? reachedCatchDeck : fellBeforeCatchDeck && !reachedCatchDeck;
        if (!passed)
        {
            Fail($"Charge {storedSpeed:0}/32 produced the wrong landing result: reached={reachedCatchDeck}, fell={fellBeforeCatchDeck}, position={player.GlobalPosition}.");
            return false;
        }

        room.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        return true;
    }

    private void Fail(string message)
    {
        GD.PushError($"ROOM30_MOMENTUM_THRESHOLD_FAIL: {message}");
        GetTree().Quit(1);
    }
}
