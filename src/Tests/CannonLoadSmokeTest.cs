using Godot;
using System.Diagnostics;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Physics;

namespace Velocitex.Tests;

public partial class CannonLoadSmokeTest : Node
{
    public override async void _Ready()
    {
        string argument = OS.GetCmdlineUserArgs()
            .FirstOrDefault(value => value.StartsWith("--cannon-load-room=", StringComparison.Ordinal)) ??
            "--cannon-load-room=30";
        if (!int.TryParse(argument.Split('=')[1], out int roomNumber) ||
            roomNumber is not (17 or 20 or 30))
        {
            Fail($"Invalid cannon room argument: {argument}.");
            return;
        }

        if (!await VerifyRoom(roomNumber))
        {
            return;
        }

        GD.Print($"CANNON_LOAD_PASS: Room {roomNumber} loaded with zero eager projectile bodies; each cannon keeps an independent hitbox, schedule and lazy projectile pool.");
        GetTree().Quit(0);
    }

    private async Task<bool> VerifyRoom(int roomNumber)
    {
        PackedScene? packed = GD.Load<PackedScene>($"res://scenes/Room{roomNumber:00}.tscn");
        if (packed is null)
        {
            Fail($"Room {roomNumber} could not be loaded.");
            return false;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        RoomRuntime room = packed.Instantiate<RoomRuntime>();
        AddChild(room);
        GD.Print($"CANNON_LOAD_TRACE: room={roomNumber} added");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GD.Print($"CANNON_LOAD_TRACE: room={roomNumber} first_frame");
        stopwatch.Stop();

        InterferenceCannon3D[] cannons = EnumerateDescendants(room).OfType<InterferenceCannon3D>().ToArray();
        if (cannons.Length == 0 || cannons.Any(cannon => cannon.ProjectilePoolCount != 0 || !cannon.HasSolidBodyHitbox))
        {
            Fail($"Room {roomNumber} eagerly created projectiles or lost an independent cannon hitbox.");
            return false;
        }

        InterferenceCannon3D firingCannon = cannons.First(cannon => !cannon.EnableAudio);
        InterferenceCannon3D untouchedCannon = cannons.First(cannon => cannon != firingCannon);
        int firstFireTick = firingCannon.ScheduledFirstFireTick;
        for (int tick = 0; tick <= firstFireTick; tick++)
        {
            firingCannon.AdvancePhysicsTick();
        }
        GD.Print($"CANNON_LOAD_TRACE: room={roomNumber} fired");
        if (firingCannon.ShotsFired == 0 ||
            firingCannon.ProjectilePoolCount != Math.Max(2, firingCannon.PoolSize) ||
            untouchedCannon.ShotsFired != 0 ||
            untouchedCannon.ProjectilePoolCount != 0)
        {
            Fail($"Room {roomNumber} cannon pools are not lazy and independent after the first scheduled shot.");
            return false;
        }

        GD.Print($"CANNON_LOAD_ROOM: room={roomNumber}, build_ms={stopwatch.ElapsedMilliseconds}, cannons={cannons.Length}, eager_projectiles=0, fired_pool={firingCannon.ProjectilePoolCount}, untouched_pool={untouchedCannon.ProjectilePoolCount}.");
        room.QueueFree();
        GD.Print($"CANNON_LOAD_TRACE: room={roomNumber} queued_free");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        return true;
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
        GD.PushError($"CANNON_LOAD_FAIL: {message}");
        GetTree().Quit(1);
    }
}
