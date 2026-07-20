using Godot;
using Velocitex.Core.Physics;

namespace Velocitex.Gameplay.Player;

public partial class PlayerAudioController : Node
{
    private readonly Dictionary<SurfaceKind, AudioStreamWav> _rollStreams = new();
    private PlayerBall _player = null!;
    private AudioStreamPlayer _rollA = null!;
    private AudioStreamPlayer _rollB = null!;
    private AudioStreamPlayer _wind = null!;
    private AudioStreamPlayer _elasticImpact = null!;
    private AudioStream[] _impactStreams = Array.Empty<AudioStream>();
    private readonly List<AudioStreamPlayer> _impactVoices = new();
    private AudioStreamPlayer _activeRoll = null!;
    private AudioStreamPlayer _fadingRoll = null!;
    private SurfaceKind _audibleSurface = SurfaceKind.Standard;
    private int _lastBounceCount;
    private int _lastCollisionImpactCount;
    private int _nextImpactVoice;

    public int LoadedLoopStreamCount => _rollStreams.Values.Distinct().Count() + (_wind.Stream is null ? 0 : 1);
    public bool AllLoopStreamsStereo => _rollStreams.Values.All(stream => stream.Stereo) && _wind.Stream is AudioStreamWav { Stereo: true };
    public int ImpactVoiceCount => _impactVoices.Count;
    public int ImpactTierCount => _impactStreams.Length;
    public float CurrentRollVolumeDb => _activeRoll.VolumeDb;
    public bool CurrentRollPlaying => _activeRoll.Playing;
    public double CurrentRollPlaybackPosition => _activeRoll.GetPlaybackPosition();

    public override void _Ready()
    {
        string[] arguments = OS.GetCmdlineUserArgs();
        if (Array.Exists(arguments, argument =>
                argument == "--movement-smoke" ||
                argument == "--room-shell-smoke" ||
                argument == "--room04-recovery-smoke" ||
                argument == "--flight-gate-boost-smoke" ||
                argument == "--room01-gate-bypass-smoke" ||
                argument.Contains("bypass-room=", StringComparison.Ordinal) ||
                argument.Contains("solution-smoke", StringComparison.Ordinal)))
        {
            SetPhysicsProcess(false);
            return;
        }

        _player = GetParent<PlayerBall>();
        LoadStreams();
        _rollA = CreatePlayer("RollA");
        _rollB = CreatePlayer("RollB");
        _wind = CreatePlayer("AirWind");
        _impactStreams = new AudioStream[]
        {
            GD.Load<AudioStream>("res://assets/audio/sfx/player_impact_metal_tap.wav"),
            GD.Load<AudioStream>("res://assets/audio/sfx/player_impact_metal_light.wav"),
            GD.Load<AudioStream>("res://assets/audio/sfx/player_impact_metal_medium.wav"),
            GD.Load<AudioStream>("res://assets/audio/sfx/player_impact_metal_heavy.wav"),
            GD.Load<AudioStream>("res://assets/audio/sfx/player_impact_metal_crash.wav"),
        };
        for (int index = 0; index < 4; index++)
        {
            _impactVoices.Add(CreatePlayer($"Impact{index + 1}"));
        }
        _elasticImpact = CreatePlayer("ElasticImpact", "res://assets/audio/sfx/surface_super_elastic_bounce.wav");
        _activeRoll = _rollA;
        _fadingRoll = _rollB;
        _activeRoll.Stream = _rollStreams[SurfaceKind.Standard];
        _activeRoll.VolumeDb = -60.0f;
        _activeRoll.Play();
        _wind.Stream = LoadLoop("res://assets/audio/sfx/player_air_wind_loop.wav");
        _wind.VolumeDb = -60.0f;
        _wind.Play();
        _lastCollisionImpactCount = _player.CollisionImpactCount;
    }

    public override void _PhysicsProcess(double delta)
    {
        float seconds = (float)delta;
        SurfaceKind surface = ResolveAudibleSurface(_player.GroundSurfaceKind);
        if (_player.IsGrounded && surface != _audibleSurface)
        {
            SwitchRollSurface(surface);
        }

        float planarSpeed = _player.LinearVelocity.Slide(_player.GroundNormal).Length();
        float speed = _player.LinearVelocity.Length();
        float rollAmount = _player.IsGrounded ? Mathf.Clamp((planarSpeed - 0.03f) / 16.0f, 0.0f, 1.0f) : 0.0f;
        float rollTarget = rollAmount <= 0.0f
            ? -60.0f
            : Mathf.Lerp(-12.0f, -1.5f, Mathf.Sqrt(rollAmount));
        _activeRoll.VolumeDb = Mathf.MoveToward(_activeRoll.VolumeDb, rollTarget, 180.0f * seconds);
        _activeRoll.PitchScale = Mathf.Lerp(0.72f, 1.48f, Mathf.Clamp(planarSpeed / 28.0f, 0.0f, 1.0f));
        _fadingRoll.VolumeDb = Mathf.MoveToward(_fadingRoll.VolumeDb, -60.0f, 78.0f * seconds);
        if (_fadingRoll.Playing && _fadingRoll.VolumeDb <= -58.0f)
        {
            _fadingRoll.Stop();
        }

        float windAmount = _player.IsGrounded
            ? Mathf.Clamp((speed - 17.0f) / 25.0f, 0.0f, 1.0f)
            : Mathf.Clamp((speed - 5.0f) / 27.0f, 0.0f, 1.0f);
        float windTarget = Mathf.Lerp(-60.0f, -10.0f, windAmount * windAmount);
        _wind.VolumeDb = Mathf.MoveToward(_wind.VolumeDb, windTarget, 46.0f * seconds);
        _wind.PitchScale = Mathf.Lerp(0.72f, 1.32f, windAmount);

        bool elasticImpact = _player.SuperElasticBounceCount > _lastBounceCount;
        if (elasticImpact)
        {
            _elasticImpact.PitchScale = Mathf.Clamp(0.9f + (_player.LastSuperElasticLaunchSpeed / 80.0f), 0.9f, 1.28f);
            _elasticImpact.VolumeDb = Mathf.Lerp(-16.0f, -5.0f, Mathf.Clamp(_player.LastSuperElasticLaunchSpeed / 28.0f, 0.0f, 1.0f));
            _elasticImpact.Play();
        }

        if (_player.CollisionImpactCount > _lastCollisionImpactCount && !elasticImpact)
        {
            PlayCollision(_player.LastCollisionImpactSpeed);
        }

        _lastBounceCount = _player.SuperElasticBounceCount;
        _lastCollisionImpactCount = _player.CollisionImpactCount;
    }

    public override void _ExitTree()
    {
        foreach (AudioStreamPlayer voice in _impactVoices)
        {
            voice.Stop();
            voice.Stream = null;
        }
        _impactVoices.Clear();
        foreach (AudioStreamPlayer loop in new[] { _rollA, _rollB, _wind, _elasticImpact })
        {
            if (!IsInstanceValid(loop))
            {
                continue;
            }
            loop.Stop();
            loop.Stream = null;
        }
        _impactStreams = Array.Empty<AudioStream>();
        _rollStreams.Clear();
    }

    private void LoadStreams()
    {
        AudioStreamWav metal = LoadLoop("res://assets/audio/sfx/player_roll_metal_loop.wav");
        AudioStreamWav glass = LoadLoop("res://assets/audio/sfx/player_roll_glass_loop.wav");
        AudioStreamWav soft = LoadLoop("res://assets/audio/sfx/player_roll_soft_loop.wav");
        AudioStreamWav rubber = LoadLoop("res://assets/audio/sfx/player_roll_rubber_loop.wav");
        _rollStreams[SurfaceKind.Standard] = metal;
        _rollStreams[SurfaceKind.Frictionless] = glass;
        _rollStreams[SurfaceKind.Sticky] = soft;
        _rollStreams[SurfaceKind.Accelerator] = metal;
        _rollStreams[SurfaceKind.SuperElastic] = rubber;
        _rollStreams[SurfaceKind.Absorbing] = soft;
        _rollStreams[SurfaceKind.OneWayGrip] = rubber;
        _rollStreams[SurfaceKind.Brittle] = glass;
        _rollStreams[SurfaceKind.Gelatin] = rubber;
        _rollStreams[SurfaceKind.MomentumBank] = metal;
    }

    private void SwitchRollSurface(SurfaceKind surface)
    {
        (_activeRoll, _fadingRoll) = (_fadingRoll, _activeRoll);
        _activeRoll.Stream = _rollStreams[surface];
        _activeRoll.VolumeDb = -60.0f;
        _activeRoll.PitchScale = 1.0f;
        _activeRoll.Play();
        _audibleSurface = surface;
    }

    private void PlayCollision(float impactSpeed)
    {
        AudioStreamPlayer impact = _impactVoices.FirstOrDefault(voice => !voice.Playing)
            ?? _impactVoices[_nextImpactVoice];
        _nextImpactVoice = (_nextImpactVoice + 1) % _impactVoices.Count;
        impact.Stream = _impactStreams[ResolveImpactTier(impactSpeed)];
        impact.VolumeDb = ResolveImpactVolumeDb(impactSpeed);
        impact.PitchScale = 0.98f + (GD.Randf() * 0.04f);
        impact.Play();
    }

    public static int ResolveImpactTier(float impactSpeed) => Mathf.Max(impactSpeed, 0.0f) switch
    {
        < 0.75f => 0,
        < 1.6f => 1,
        < 3.5f => 2,
        < 7.5f => 3,
        _ => 4,
    };

    public static float ResolveImpactVolumeDb(float impactSpeed)
    {
        float speed = Mathf.Max(impactSpeed, 0.0f);
        if (speed < 0.75f)
        {
            return Mathf.Lerp(-38.0f, -31.0f, speed / 0.75f);
        }
        if (speed < 1.6f)
        {
            return Mathf.Lerp(-31.0f, -24.0f, (speed - 0.75f) / 0.85f);
        }
        if (speed < 3.5f)
        {
            return Mathf.Lerp(-24.0f, -15.0f, (speed - 1.6f) / 1.9f);
        }
        if (speed < 7.5f)
        {
            return Mathf.Lerp(-15.0f, -7.0f, (speed - 3.5f) / 4.0f);
        }
        return Mathf.Lerp(-7.0f, -1.5f, Mathf.Clamp((speed - 7.5f) / 10.5f, 0.0f, 1.0f));
    }

    private AudioStreamPlayer CreatePlayer(string name, string? streamPath = null)
    {
        AudioStreamPlayer player = new()
        {
            Name = name,
            Bus = "SFX",
            VolumeDb = -60.0f,
            Stream = streamPath is null ? null : GD.Load<AudioStream>(streamPath),
        };
        AddChild(player);
        return player;
    }

    private static AudioStreamWav LoadLoop(string path)
    {
        AudioStreamWav stream = (AudioStreamWav)GD.Load<AudioStreamWav>(path).Duplicate();
        stream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
        stream.LoopBegin = 0;
        stream.LoopEnd = Math.Max(1, Mathf.RoundToInt((float)(stream.GetLength() * stream.MixRate)));
        return stream;
    }

    private static SurfaceKind ResolveAudibleSurface(SurfaceKind surface) => surface switch
    {
        SurfaceKind.Frictionless => SurfaceKind.Frictionless,
        SurfaceKind.Sticky => SurfaceKind.Sticky,
        SurfaceKind.SuperElastic => SurfaceKind.SuperElastic,
        SurfaceKind.Absorbing => SurfaceKind.Absorbing,
        SurfaceKind.OneWayGrip => SurfaceKind.OneWayGrip,
        SurfaceKind.Brittle => SurfaceKind.Brittle,
        SurfaceKind.Gelatin => SurfaceKind.Gelatin,
        _ => SurfaceKind.Standard,
    };
}
