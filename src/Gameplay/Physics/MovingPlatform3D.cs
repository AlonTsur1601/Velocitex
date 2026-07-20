using Godot;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Rooms;
using Velocitex.Gameplay.Visual;

namespace Velocitex.Gameplay.Physics;

public partial class MovingPlatform3D : AnimatableBody3D
{
    private enum PlatformState
    {
        Docked,
        DepartureDelay,
        MovingOut,
        AtDestination,
    }

    public event Action<PlayerBall>? PlayerBoarded;
    public event Action<PlayerBall>? PlayerLeftDuringTransit;
    public event Action? Departed;
    public event Action? ArrivedAtDestination;

    [Export] public Vector3 PlatformSize { get; set; } = new(9.0f, 0.6f, 10.0f);
    [Export] public Vector3 EndOffset { get; set; } = new(0.0f, 11.0f, -28.0f);
    [Export] public int DepartureDelayTicks { get; set; } = 30;
    [Export] public int TravelTicks { get; set; } = 240;
    [Export] public bool EnableAudio { get; set; } = true;
    [Export] public bool EnableRearGate { get; set; } = true;
    [Export] public bool RequiresActivation { get; set; }

    public bool HasReachedDestination { get; private set; }
    public float Progress { get; private set; }
    public bool HasOccupant(PlayerBall player) => _occupants.Contains(player);

    private readonly HashSet<PlayerBall> _occupants = new();
    private Vector3 _startPosition;
    private PlatformState _state;
    private int _stateTick;
    private bool _activated;
    private AudioStreamPlayer3D? _motionAudio;
    private readonly List<MeshInstance3D> _rollers = new();
    private CollisionShape3D _frontGateCollision = null!;
    private MeshInstance3D _frontGateVisual = null!;
    private Vector3 _frontGateRestPosition;
    private CollisionShape3D _rearGateCollision = null!;
    private MeshInstance3D _rearGateVisual = null!;
    private Vector3 _rearGateRestPosition;

    public override void _Ready()
    {
        _startPosition = Position;
        SyncToPhysics = true;
        BuildPlatform();
    }

    public override void _PhysicsProcess(double delta)
    {
        switch (_state)
        {
            case PlatformState.Docked:
                if (_occupants.Count > 0 && (!RequiresActivation || _activated))
                {
                    _state = PlatformState.DepartureDelay;
                    _stateTick = 0;
                }
                break;
            case PlatformState.DepartureDelay:
                if (++_stateTick >= Math.Max(1, DepartureDelayTicks))
                {
                    _state = PlatformState.MovingOut;
                    _stateTick = 0;
                    _motionAudio?.Play();
                    Departed?.Invoke();
                }
                break;
            case PlatformState.MovingOut:
                _stateTick++;
                Progress = Mathf.Clamp(_stateTick / (float)Math.Max(1, TravelTicks), 0.0f, 1.0f);
                float smoothProgress = Progress * Progress * (3.0f - (2.0f * Progress));
                Vector3 previousPosition = Position;
                Position = _startPosition.Lerp(_startPosition + EndOffset, smoothProgress);
                Vector3 platformDelta = Position - previousPosition;
                foreach (PlayerBall occupant in _occupants)
                {
                    if (IsInstanceValid(occupant))
                    {
                        occupant.GlobalPosition += platformDelta;
                    }
                }
                foreach (MeshInstance3D roller in _rollers)
                {
                    roller.RotateX(0.16f);
                }

                if (Progress >= 1.0f)
                {
                    _state = PlatformState.AtDestination;
                    HasReachedDestination = true;
                    _motionAudio?.Stop();
                    _frontGateCollision.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
                    Tween gateTween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
                    gateTween.TweenProperty(_frontGateVisual, "position", _frontGateRestPosition + (Vector3.Down * 1.65f), 0.28f);
                    ArrivedAtDestination?.Invoke();
                }
                break;
        }
    }

    public void ResetPlatform()
    {
        _state = PlatformState.Docked;
        _stateTick = 0;
        Progress = 0.0f;
        HasReachedDestination = false;
        _activated = false;
        Position = _startPosition;
        _occupants.Clear();
        _motionAudio?.Stop();
        _frontGateCollision.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
        _frontGateVisual.Position = _frontGateRestPosition;
        _rearGateCollision.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        _rearGateVisual.Position = _rearGateRestPosition + (Vector3.Down * 1.65f);
    }

    public void Activate()
    {
        _activated = true;
        if (_state == PlatformState.Docked && _occupants.Count > 0)
        {
            _state = PlatformState.DepartureDelay;
            _stateTick = 0;
        }
    }

    private void BuildPlatform()
    {
        StandardMaterial3D deckMaterial = RoomGeometry.CreateMaterial("res://assets/textures/diamond_plate.png", new Color("8299a3"), 0.55f, 0.58f);
        StandardMaterial3D frameMaterial = RoomGeometry.CreateMaterial("res://assets/textures/copper_rivets.svg", new Color("6f5e55"), 0.4f, 0.56f);
        StandardMaterial3D rollerMaterial = RoomGeometry.CreateMaterial("res://assets/textures/rubber_chevrons.svg", new Color("252d33"), 0.03f, 0.94f);

        PhysicsMaterial physicsMaterial = new() { Friction = 1.25f, Rough = true, Bounce = 0.02f };
        PhysicsMaterialOverride = physicsMaterial;
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = PlatformSize } });
        AddChild(new MeshInstance3D
        {
            Name = "TiledDeck",
            Mesh = SurfaceMeshFactory.CreateTiledBox(PlatformSize),
            MaterialOverride = deckMaterial,
        });

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            Vector3 railSize = new(0.34f, 1.25f, PlatformSize.Z);
            Vector3 railPosition = new(side * ((PlatformSize.X * 0.5f) + 0.22f), 0.62f, 0.0f);
            AddChild(new CollisionShape3D { Position = railPosition, Shape = new BoxShape3D { Size = railSize } });
            AddChild(new MeshInstance3D
            {
                Name = side < 0.0f ? "LeftRail" : "RightRail",
                Position = railPosition,
                Mesh = SurfaceMeshFactory.CreateTiledBox(railSize),
                MaterialOverride = frameMaterial,
            });
        }

        Vector3 gateSize = new(PlatformSize.X, 1.35f, 0.34f);
        _frontGateRestPosition = new Vector3(0.0f, 0.68f, (-PlatformSize.Z * 0.5f) - 0.22f);
        _frontGateCollision = new CollisionShape3D { Position = _frontGateRestPosition, Shape = new BoxShape3D { Size = gateSize } };
        AddChild(_frontGateCollision);
        _frontGateVisual = new MeshInstance3D
        {
            Name = "SafetyGate",
            Position = _frontGateRestPosition,
            Mesh = SurfaceMeshFactory.CreateTiledBox(gateSize),
            MaterialOverride = frameMaterial,
        };
        AddChild(_frontGateVisual);

        _rearGateRestPosition = new Vector3(0.0f, 0.68f, (PlatformSize.Z * 0.5f) + 0.22f);
        _rearGateCollision = new CollisionShape3D { Position = _rearGateRestPosition, Shape = new BoxShape3D { Size = gateSize }, Disabled = true };
        AddChild(_rearGateCollision);
        _rearGateVisual = new MeshInstance3D
        {
            Name = "RearSafetyRail",
            Position = _rearGateRestPosition + (Vector3.Down * 1.65f),
            Mesh = SurfaceMeshFactory.CreateTiledBox(gateSize),
            MaterialOverride = frameMaterial,
        };
        AddChild(_rearGateVisual);

        foreach (float x in new[] { -3.5f, 3.5f })
        {
            foreach (float z in new[] { -3.8f, 3.8f })
            {
                MeshInstance3D roller = new()
                {
                    Name = $"Roller{x}_{z}",
                    Position = new Vector3(x, -0.55f, z),
                    Rotation = new Vector3(0.0f, 0.0f, Mathf.Pi / 2.0f),
                    Mesh = new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.42f, Height = 0.7f, RadialSegments = 16 },
                    MaterialOverride = rollerMaterial,
                };
                AddChild(roller);
                _rollers.Add(roller);
            }
        }

        Area3D boardingArea = new() { Name = "BoardingArea", Position = new Vector3(0.0f, 1.25f, 0.0f), CollisionLayer = 0, CollisionMask = 1, Monitoring = true };
        boardingArea.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(PlatformSize.X - 0.2f, 2.5f, PlatformSize.Z - 0.2f) } });
        boardingArea.BodyEntered += body =>
        {
            if (body is PlayerBall player && _occupants.Add(player))
            {
                if (_state == PlatformState.Docked && (!RequiresActivation || _activated))
                {
                    _state = PlatformState.DepartureDelay;
                    _stateTick = 0;
                }
                if (EnableRearGate)
                {
                    _rearGateCollision.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
                    Tween rearGateTween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
                    rearGateTween.TweenProperty(_rearGateVisual, "position", _rearGateRestPosition, 0.24f);
                }
                PlayerBoarded?.Invoke(player);
            }
        };
        boardingArea.BodyExited += body =>
        {
            if (body is PlayerBall player && _occupants.Remove(player) && _state is PlatformState.DepartureDelay or PlatformState.MovingOut)
            {
                PlayerLeftDuringTransit?.Invoke(player);
            }
        };
        AddChild(boardingArea);

        if (EnableAudio)
        {
            _motionAudio = new AudioStreamPlayer3D
            {
                Name = "PlatformMotionSfx",
                Stream = GD.Load<AudioStream>("res://assets/audio/sfx/device_moving_platform.wav"),
                Bus = "SFX",
                Position = Vector3.Zero,
                MaxDistance = 48.0f,
                UnitSize = 9.0f,
            };
            AddChild(_motionAudio);
        }
    }
}
