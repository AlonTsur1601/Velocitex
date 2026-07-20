using Godot;
using Velocitex.Gameplay.Player;

namespace Velocitex.Gameplay.Rooms;

public partial class RouteCheckpoint3D : Area3D
{
    private const float DeniedFlashDuration = 0.48f;
    private const float DeniedFlashInterval = 0.12f;
    private const float PlayerBallRadius = 0.6f;
    private const float FloorPressTolerance = 0.04f;

    public event Action<RouteCheckpoint3D, PlayerBall>? Entered;

    public int CheckpointIndex { get; set; }
    public Vector3 TriggerSize { get; set; } = new(4.0f, 2.4f, 1.6f);
    public Color FrameTint { get; set; } = new("a76d50");
    public bool FlatFloorMarker { get; set; }
    public bool RequireFloorContact { get; set; } = true;
    public bool AllowAreaPressFallback { get; set; }
    public bool ShowFloorButtonIndicators { get; set; }
    public float FloorMarkerInset { get; set; }
    public bool IsPhysicalFloorButton => FlatFloorMarker && RequireFloorContact;
    public bool IsActivated { get; private set; }
    public bool IsDeniedFeedbackActive => _deniedFlashTime > 0.0f;

    private readonly List<Node3D> _latches = new();
    private readonly HashSet<ulong> _handledFloorContacts = new();
    private MeshInstance3D _framePlate = null!;
    private MeshInstance3D _innerPlate = null!;
    private AudioStreamPlayer3D? _activationAudio;
    private Material _frameMaterial = null!;
    private Material _idleMaterial = null!;
    private Material _activeMaterial = null!;
    private Material _deniedMaterial = null!;
    private Material _deniedDimMaterial = null!;
    private float _activationAmount;
    private float _deniedFlashTime;
    private bool _isPressAttempt;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 1;
        Monitoring = true;
        Monitorable = false;

        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = TriggerSize },
        });

        StandardMaterial3D frame = RoomGeometry.CreateMaterial(
            "res://assets/textures/copper_rivets.svg",
            FrameTint,
            0.46f,
            0.58f);
        _frameMaterial = frame;
        _idleMaterial = RoomGeometry.CreateMaterial(
            "res://assets/textures/brushed_metal.png",
            new Color("73888a"),
            0.38f,
            0.62f);
        _activeMaterial = RoomGeometry.CreateMaterial(
            "res://assets/textures/sugar_glaze.svg",
            new Color("8fc3ad"),
            0.08f,
            0.5f);
        _deniedMaterial = RoomGeometry.CreateMaterial(
            string.Empty,
            new Color("e23f45"),
            0.06f,
            0.42f,
            emissionEnabled: true,
            emission: new Color("8f1018"));
        _deniedDimMaterial = RoomGeometry.CreateMaterial(
            string.Empty,
            new Color("8f1f28"),
            0.08f,
            0.48f,
            emissionEnabled: true,
            emission: new Color("4f080d"));

        Vector3 plateSize = new(
            Mathf.Max(1.8f, TriggerSize.X * 0.72f),
            0.12f,
            Mathf.Max(1.2f, TriggerSize.Z * 0.72f));
        _framePlate = RoomGeometry.AddVisualBox(this, "FramePlate", plateSize + new Vector3(0.38f, 0.04f, 0.38f), new Vector3(0.0f, -TriggerSize.Y * 0.42f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frame);
        _innerPlate = RoomGeometry.AddVisualBox(this, "InsetPlate", plateSize, new Vector3(0.0f, (-TriggerSize.Y * 0.42f) + 0.08f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, _idleMaterial as StandardMaterial3D);

        if (!FlatFloorMarker)
        {
            Vector2 half = new(plateSize.X * 0.42f, plateSize.Z * 0.42f);
            for (int index = 0; index < 4; index++)
            {
                float x = index % 2 == 0 ? -half.X : half.X;
                float z = index < 2 ? -half.Y : half.Y;
                Node3D latch = new()
                {
                    Name = $"MechanicalLatch{index}",
                    Position = new Vector3(x, (-TriggerSize.Y * 0.42f) + 0.18f, z),
                };
                RoomGeometry.AddVisualBox(latch, "Jaw", new Vector3(0.62f, 0.22f, 0.34f), Vector3.Zero, Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frame);
                AddChild(latch);
                _latches.Add(latch);
            }
        }

        if (!FlatFloorMarker)
        {
            for (int index = 0; index < 3; index++)
            {
                float offset = (index - 1) * plateSize.X * 0.22f;
                RoomGeometry.AddVisualBox(
                    _innerPlate,
                    $"DirectionRib{index}",
                    new Vector3(0.12f, 0.035f, plateSize.Z * 0.55f),
                    new Vector3(offset, 0.08f, 0.0f),
                    new Vector3(0.0f, Mathf.DegToRad(35.0f), 0.0f),
                    string.Empty,
                    Colors.White,
                    0.0f,
                    1.0f,
                    _activeMaterial as StandardMaterial3D);
            }
        }

        if (!IsPhysicalFloorButton || AllowAreaPressFallback)
        {
            BodyEntered += OnBodyEntered;
        }

        _activationAudio = new AudioStreamPlayer3D
        {
            Name = "ActivationClickSfx",
            Stream = GD.Load<AudioStream>("res://assets/audio/sfx/device_mechanical_click.wav"),
            Bus = "SFX",
            VolumeDb = -5.0f,
            MaxDistance = 24.0f,
            UnitSize = 5.0f,
        };
        AddChild(_activationAudio);
    }

    public override void _Process(double delta)
    {
        float target = IsActivated ? 1.0f : 0.0f;
        _activationAmount = Mathf.MoveToward(_activationAmount, target, (float)delta * 5.0f);
        _deniedFlashTime = Mathf.Max(0.0f, _deniedFlashTime - (float)delta);
        float deniedElapsed = DeniedFlashDuration - _deniedFlashTime;
        bool deniedActive = _deniedFlashTime > 0.0f;
        bool brightDenied = (int)(deniedElapsed / DeniedFlashInterval) % 2 == 0;
        Material deniedPhaseMaterial = brightDenied ? _deniedMaterial : _deniedDimMaterial;
        _framePlate.MaterialOverride = _frameMaterial;
        _innerPlate.MaterialOverride = deniedActive ? deniedPhaseMaterial : (IsActivated ? _activeMaterial : _idleMaterial);
        SetSequencePipsVisible(true);
        _innerPlate.Position = new Vector3(
            _innerPlate.Position.X,
            (-TriggerSize.Y * 0.42f) + Mathf.Lerp(0.08f, -0.02f, _activationAmount),
            _innerPlate.Position.Z);
        for (int index = 0; index < _latches.Count; index++)
        {
            float direction = index % 2 == 0 ? -1.0f : 1.0f;
            _latches[index].Rotation = new Vector3(0.0f, direction * _activationAmount * 0.7f, 0.0f);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsPhysicalFloorButton && !IsActivated)
        {
            ProcessFloorContacts();
        }
    }

    public void Activate()
    {
        if (IsActivated)
        {
            return;
        }

        IsActivated = true;
        _deniedFlashTime = 0.0f;
        _framePlate.MaterialOverride = _frameMaterial;
        _innerPlate.MaterialOverride = _activeMaterial;
        SetSequencePipsVisible(true);
        _activationAudio?.Play();
    }

    public void FlashDenied()
    {
        if (!IsActivated)
        {
            _deniedFlashTime = DeniedFlashDuration;
            _framePlate.MaterialOverride = _frameMaterial;
            _innerPlate.MaterialOverride = _deniedMaterial;
            SetSequencePipsVisible(true);
        }
    }

    public void Press(PlayerBall player)
    {
        DispatchPress(player);
    }

    private void SetSequencePipsVisible(bool visible)
    {
        foreach (Node child in _innerPlate.GetChildren())
        {
            if (child is MeshInstance3D pip && child.Name.ToString().StartsWith("SequencePip", StringComparison.Ordinal))
            {
                pip.Visible = visible;
            }
        }
    }

    public void ResetCheckpoint()
    {
        IsActivated = false;
        _activationAmount = 0.0f;
        _deniedFlashTime = 0.0f;
        _framePlate.MaterialOverride = _frameMaterial;
        _innerPlate.MaterialOverride = _idleMaterial;
        SetSequencePipsVisible(true);
        _handledFloorContacts.Clear();
    }

    public override void _ExitTree()
    {
        if (!IsPhysicalFloorButton || AllowAreaPressFallback)
        {
            BodyEntered -= OnBodyEntered;
        }
        Entered = null;
        _activationAudio?.Stop();
        if (_activationAudio is not null)
        {
            _activationAudio.Stream = null;
        }
        _latches.Clear();
        _handledFloorContacts.Clear();
    }

    private void ProcessFloorContacts()
    {
        PlayerBall[] overlappingPlayers = GetOverlappingBodies().OfType<PlayerBall>().ToArray();
        HashSet<ulong> overlappingIds = overlappingPlayers.Select(player => player.GetInstanceId()).ToHashSet();
        _handledFloorContacts.RemoveWhere(id => !overlappingIds.Contains(id));
        foreach (PlayerBall player in overlappingPlayers)
        {
            ulong id = player.GetInstanceId();
            bool touchingPlate = player.GlobalPosition.Y - PlayerBallRadius <=
                _innerPlate.GlobalPosition.Y + FloorPressTolerance;
            if (!touchingPlate || _handledFloorContacts.Contains(id))
            {
                continue;
            }

            _handledFloorContacts.Add(id);
            DispatchPress(player);
        }
    }

    private void DispatchPress(PlayerBall player)
    {
        _isPressAttempt = true;
        try
        {
            OnBodyEntered(player);
        }
        finally
        {
            _isPressAttempt = false;
        }
    }

    private void OnBodyEntered(Node3D body)
    {
        if (!IsActivated && body is PlayerBall player)
        {
            Entered?.Invoke(this, player);
            if (!IsActivated && _isPressAttempt)
            {
                FlashDenied();
            }
        }
    }
}
