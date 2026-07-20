using Godot;
using Velocitex.Core.Input;
using Velocitex.Gameplay.Player;

namespace Velocitex.Tests;

public partial class PlayerSfxSmokeTest : Node
{
    public override async void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] paths =
        {
            "res://assets/audio/sfx/player_roll_metal_loop.wav",
            "res://assets/audio/sfx/player_roll_glass_loop.wav",
            "res://assets/audio/sfx/player_roll_soft_loop.wav",
            "res://assets/audio/sfx/player_roll_rubber_loop.wav",
            "res://assets/audio/sfx/player_air_wind_loop.wav",
            "res://assets/audio/sfx/player_land_soft.wav",
            "res://assets/audio/sfx/player_land_hard.wav",
            "res://assets/audio/sfx/player_impact_metal_tap.wav",
            "res://assets/audio/sfx/player_impact_metal_light.wav",
            "res://assets/audio/sfx/player_impact_metal_medium.wav",
            "res://assets/audio/sfx/player_impact_metal_heavy.wav",
            "res://assets/audio/sfx/player_impact_metal_crash.wav",
            "res://assets/audio/sfx/ui_advancement.wav",
            "res://assets/audio/sfx/device_player_cannon_fire.wav",
            "res://assets/audio/sfx/device_interference_cannon_fire.wav",
            "res://assets/audio/sfx/device_piston_fire.wav",
            "res://assets/audio/sfx/device_moving_platform.wav",
            "res://assets/audio/sfx/device_mechanical_click.wav",
            "res://assets/audio/sfx/surface_sticky_contact.wav",
            "res://assets/audio/sfx/surface_accelerator_contact.wav",
            "res://assets/audio/sfx/surface_super_elastic_bounce.wav",
            "res://assets/audio/sfx/force_strong_gravity_enter.wav",
        };
        foreach (string path in paths)
        {
            double minimumLength = path.Contains("device_mechanical_click", StringComparison.Ordinal)
                ? 0.12
                : path.Contains("player_impact_metal_", StringComparison.Ordinal) ||
                    path.Contains("device_interference_cannon_fire", StringComparison.Ordinal)
                        ? 0.18
                        : 0.35;
            if (GD.Load<AudioStreamWav>(path) is not { Stereo: true } stream || stream.GetLength() < minimumLength)
            {
                Fail($"Stereo SFX is missing, mono, or too short: {path}.");
                return;
            }
        }

        PackedScene playerScene = GD.Load<PackedScene>("res://scenes/PlayerBall.tscn");
        PlayerBall player = playerScene.Instantiate<PlayerBall>();
        player.Position = new Vector3(0.0f, 2.0f, 0.0f);
        AddChild(player);
        PlayerAudioController controller = player.GetNode<PlayerAudioController>("Audio");
        if (controller.LoadedLoopStreamCount != 5 || !controller.AllLoopStreamsStereo ||
            controller.ImpactVoiceCount != 4 || controller.ImpactTierCount != 5)
        {
            Fail($"Player audio controller loaded {controller.LoadedLoopStreamCount} loop families, {controller.ImpactTierCount} impact tiers and {controller.ImpactVoiceCount} voices instead of five, five and four.");
            return;
        }

        float[] tierProbeSpeeds = { 0.2f, 0.9f, 2.0f, 5.0f, 10.0f };
        float previousVolume = float.NegativeInfinity;
        for (int index = 0; index < tierProbeSpeeds.Length; index++)
        {
            float speed = tierProbeSpeeds[index];
            if (PlayerAudioController.ResolveImpactTier(speed) != index)
            {
                Fail($"Impact speed {speed:F1} m/s did not select metal tier {index}.");
                return;
            }

            float volume = PlayerAudioController.ResolveImpactVolumeDb(speed);
            if (volume <= previousVolume)
            {
                Fail("Impact volume is not strictly increasing across the five metal tiers.");
                return;
            }
            previousVolume = volume;
        }
        if (PlayerAudioController.ResolveImpactVolumeDb(0.42f) > -33.0f)
        {
            Fail("A small landing is mixed too loudly.");
            return;
        }

        StaticBody3D floor = new() { Name = "TestFloorA", Position = new Vector3(-1.25f, -0.5f, 0.0f) };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(5.5f, 1.0f, 12.0f) } });
        AddChild(floor);
        StaticBody3D adjacentFloor = new() { Name = "TestFloorB", Position = new Vector3(4.25f, -0.5f, 0.0f) };
        adjacentFloor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(5.5f, 1.0f, 12.0f) } });
        AddChild(adjacentFloor);
        player.LinearVelocity = new Vector3(0.0f, -2.0f, 0.0f);
        for (int tick = 0; tick < 180 && player.CollisionImpactCount == 0; tick++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        if (player.CollisionImpactCount == 0 || player.LastCollisionImpactSpeed < 0.42f)
        {
            Fail("The player did not report the ordinary floor collision to the impact-audio path.");
            return;
        }

        int floorImpactCount = player.CollisionImpactCount;
        StaticBody3D wall = new() { Name = "TestWall", Position = new Vector3(6.5f, 1.5f, 0.0f) };
        wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.0f, 4.0f, 12.0f) } });
        AddChild(wall);
        player.LinearVelocity = new Vector3(4.0f, 0.0f, 0.0f);
        for (int tick = 0; tick < 40; tick++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        if (controller.CurrentRollVolumeDb <= -15.0f || !controller.CurrentRollPlaying || controller.CurrentRollPlaybackPosition <= 0.15)
        {
            Fail($"Rolling did not remain audibly active at normal speed ({controller.CurrentRollVolumeDb:F1} dB, playing={controller.CurrentRollPlaying}, position={controller.CurrentRollPlaybackPosition:F2}s).");
            return;
        }
        if (player.CollisionImpactCount != floorImpactCount)
        {
            Fail("Crossing a flush floor seam was reported as a new collision impact.");
            return;
        }

        for (int tick = 0; tick < 90 && player.CollisionImpactCount == floorImpactCount; tick++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        if (player.CollisionImpactCount == floorImpactCount || player.LastCollisionImpactSpeed < 0.18f)
        {
            Fail("The player did not report an ordinary side-wall collision to the impact-audio path.");
            return;
        }

        GD.Print($"PLAYER_SFX_SMOKE_PASS: audible active rolling, five monotonic metal-impact tiers, seam suppression and four pooled voices load through the SFX bus.");
        player.QueueFree();
        floor.QueueFree();
        adjacentFloor.QueueFree();
        wall.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(0);
    }

    private void Fail(string message)
    {
        GD.PushError($"PLAYER_SFX_SMOKE_FAIL: {message}");
        GetTree().Quit(1);
    }
}
