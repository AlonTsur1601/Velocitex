using Godot;
using Velocitex.Gameplay.Physics;

namespace Velocitex.Tests;

public partial class CannonTimingSmokeTest : Node3D
{
    public override async void _Ready()
    {
        List<InterferenceCannon3D> cannons = new();
        Dictionary<InterferenceCannon3D, List<int>> shotTicks = new();
        int currentTick = 0;
        for (int index = 0; index < 24; index++)
        {
            InterferenceCannon3D cannon = new()
            {
                Name = $"RandomTimingCannon{index + 1}",
                EnableAudio = false,
                PoolSize = 1,
            };
            List<int> ticks = new();
            cannon.ProjectileFired += () => ticks.Add(currentTick);
            AddChild(cannon);
            cannons.Add(cannon);
            shotTicks[cannon] = ticks;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (currentTick = 1; currentTick <= 650; currentTick++)
        {
            foreach (InterferenceCannon3D cannon in cannons)
            {
                cannon.AdvancePhysicsTick();
            }
        }

        int distinctInitialTicks = shotTicks.Values.Select(ticks => ticks[0]).Distinct().Count();
        if (distinctInitialTicks < 12)
        {
            Fail($"Only {distinctInitialTicks}/24 independent initial fire times were produced.");
            return;
        }
        foreach ((InterferenceCannon3D cannon, List<int> ticks) in shotTicks)
        {
            if (ticks.Count < 3 || ticks[0] is < 15 or > 180)
            {
                Fail($"{cannon.Name} did not fire with a valid randomized initial delay: {string.Join(',', ticks)}.");
                return;
            }
            for (int index = 1; index < ticks.Count; index++)
            {
                int interval = ticks[index] - ticks[index - 1];
                if (interval is < 120 or > 180)
                {
                    Fail($"{cannon.Name} cadence was {interval} ticks instead of 120-180 ticks.");
                    return;
                }
            }
        }

        GD.Print($"CANNON_TIMING_PASS: 24 cannons used {distinctInitialTicks} distinct starts and independent 2-3 second cadences.");
        GetTree().Quit(0);
    }

    private void Fail(string message)
    {
        GD.PushError($"CANNON_TIMING_FAIL: {message}");
        GetTree().Quit(1);
    }
}
