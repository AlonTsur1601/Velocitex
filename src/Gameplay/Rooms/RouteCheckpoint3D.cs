using Godot;
using Velocitex.Gameplay.Player;

namespace Velocitex.Gameplay.Rooms;

public partial class RouteCheckpoint3D : Area3D
{
    private const float DeniedFlashDuration = 0.64f;
    private const float DeniedFlashInterval = 0.08f;
    private const float PlayerBallRadius = 0.6f;
    private const float FloorPressAboveTolerance = 0.16f;
    private const float FloorPressBelowTolerance = 0.46f;
    private const float PressedPlateOffset = -0.16f;
    private const string PhysicalFloorButtonGroup = "physical_floor_buttons";
    private const string AcceptedButtonPhysicsFrameMetadata = "velocitex_accepted_button_physics_frame";

    public event Action<RouteCheckpoint3D, PlayerBall>? Entered;

    public int CheckpointIndex { get; set; }
    public Vector3 TriggerSize { get; set; } = new(4.0f, 2.4f, 1.6f);
    public Color FrameTint { get; set; } = new("a76d50");
    public bool FlatFloorMarker { get; set; }
    public bool RequireFloorContact { get; set; } = true;
    public bool AllowAreaPressFallback { get; set; }
    // Rooms with moving or high-speed routes can perform their own exact visible-plate
    // check.  Keeping the Area3D callback enabled there causes a late duplicate press.
    public bool AutomaticPressEnabled { get; set; } = true;
    public bool ShowFloorButtonIndicators { get; set; }
    public float FloorMarkerInset { get; set; }
    public bool IsPhysicalFloorButton => FlatFloorMarker && (RequireFloorContact || ShowFloorButtonIndicators);
    public bool IsActivated { get; private set; }
    public bool IsDeniedFeedbackActive => _deniedFlashTime > 0.0f;
    public bool IsShowingIdleVisual => _innerPlate is not null && _innerPlate.MaterialOverride == _idleMaterial;
    public bool IsShowingDeniedRed => _innerPlate is not null &&
        _innerPlate.MaterialOverride == _deniedMaterial;

    private readonly List<Node3D> _latches = new();
    private readonly HashSet<ulong> _handledFloorContacts = new();
    private MeshInstance3D _framePlate = null!;
    private MeshInstance3D _innerPlate = null!;
    private AudioStreamPlayer3D? _activationAudio;
    private Material _frameMaterial = null!;
    private Material _idleMaterial = null!;
    private Material _activeMaterial = null!;
    private Material _deniedMaterial = null!;
    private float _activationAmount;
    private float _deniedFlashTime;
    private float _deniedFlashElapsed;
    private ulong _suppressAutomaticPressUntilPhysicsFrame;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 1;
        Monitoring = true;
        Monitorable = false;
        if (IsPhysicalFloorButton)
        {
            AddToGroup(PhysicalFloorButtonGroup);
        }

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
            new Color("d71920"),
            0.06f,
            0.42f,
            emissionEnabled: true,
            emission: new Color("8f080e"));
        Vector3 plateSize = new(
            Mathf.Max(1.8f, TriggerSize.X * 0.72f),
            0.12f,
            Mathf.Max(1.2f, TriggerSize.Z * 0.72f));
        Vector3 platePosition = new(0.0f, -TriggerSize.Y * 0.42f, 0.0f);
        CollisionShape3D triggerShape = new()
        {
            // A floor button must only react to a ball standing on its visible plate.
            // The route-sized volume used previously overlapped nearby ramps and
            // buttons, which could report an out-of-order press before the player
            // reached the intended button.
            Shape = new BoxShape3D
            {
                Size = IsPhysicalFloorButton
                    ? new Vector3(plateSize.X, 0.50f, plateSize.Z)
                    : TriggerSize,
            },
            Position = IsPhysicalFloorButton
                ? platePosition - new Vector3(0.0f, 0.11f, 0.0f)
                : Vector3.Zero,
        };
        AddChild(triggerShape);
        _framePlate = RoomGeometry.AddVisualBox(this, "FramePlate", plateSize + new Vector3(0.38f, 0.04f, 0.38f), platePosition, Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frame);
        _innerPlate = RoomGeometry.AddVisualBox(this, "InsetPlate", plateSize, platePosition + new Vector3(0.0f, 0.08f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, _idleMaterial as StandardMaterial3D);

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

        if (AutomaticPressEnabled && IsPhysicalFloorButton)
        {
            BodyEntered += OnPhysicalBodyEntered;
        }
        else if (AutomaticPressEnabled && AllowAreaPressFallback)
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
        _activationAmount = IsActivated
            ? 1.0f
            : Mathf.MoveToward(_activationAmount, 0.0f, (float)delta * 5.0f);
        if (_deniedFlashTime > 0.0f)
        {
            _deniedFlashElapsed += (float)delta;
            _deniedFlashTime = Mathf.Max(0.0f, _deniedFlashTime - (float)delta);
        }
        bool deniedActive = _deniedFlashTime > 0.0f;
        bool deniedRedPhase = ((int)(_deniedFlashElapsed / DeniedFlashInterval) & 1) == 0;
        _framePlate.MaterialOverride = _frameMaterial;
        _innerPlate.MaterialOverride = deniedActive
            ? (deniedRedPhase ? _deniedMaterial : _idleMaterial)
            : (IsActivated ? _activeMaterial : _idleMaterial);
        SetSequencePipsVisible(true);
        _innerPlate.Position = new Vector3(
            _innerPlate.Position.X,
            (-TriggerSize.Y * 0.42f) + Mathf.Lerp(0.08f, PressedPlateOffset, _activationAmount),
            _innerPlate.Position.Z);
        for (int index = 0; index < _latches.Count; index++)
        {
            float direction = index % 2 == 0 ? -1.0f : 1.0f;
            _latches[index].Rotation = new Vector3(0.0f, direction * _activationAmount * 0.7f, 0.0f);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (AutomaticPressEnabled && IsPhysicalFloorButton)
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
        foreach (Node node in GetTree().GetNodesInGroup(PhysicalFloorButtonGroup))
        {
            if (node is RouteCheckpoint3D checkpoint && checkpoint != this)
            {
                checkpoint.ClearDeniedFeedback();
            }
        }
        ApplyAcceptedPressVisualImmediately();
        _activationAudio?.Play();
    }

    internal void ApplyDeniedFeedback()
    {
        if (!IsActivated)
        {
            _deniedFlashTime = DeniedFlashDuration;
            _deniedFlashElapsed = 0.0f;
            _framePlate.MaterialOverride = _frameMaterial;
            _innerPlate.MaterialOverride = _deniedMaterial;
            SetSequencePipsVisible(true);
        }
    }

    internal void ScheduleDeniedFeedback(PlayerBall player, ulong pressPhysicsFrame)
    {
        Callable.From(() =>
        {
            bool acceptedInPressFrame = GodotObject.IsInstanceValid(player) &&
                player.HasMeta(AcceptedButtonPhysicsFrameMetadata) &&
                player.GetMeta(AcceptedButtonPhysicsFrameMetadata).AsUInt64() == pressPhysicsFrame;
            if (!IsActivated && GodotObject.IsInstanceValid(player) && !acceptedInPressFrame)
            {
                ApplyDeniedFeedback();
            }
        }).CallDeferred();
    }

    public void Press(PlayerBall player)
    {
        RouteCheckpointPressResult result = RouteCheckpointPressPolicy.Apply(this, player, () => DispatchPress(player));
        if (result == RouteCheckpointPressResult.Activated)
        {
            player.SetMeta(AcceptedButtonPhysicsFrameMetadata, Engine.GetPhysicsFrames());
            ClearAllDeniedFeedback();
            ApplyAcceptedPressVisualImmediately();
        }
    }

    internal static bool PlayerAlreadyActivatedButtonThisPhysicsFrame(PlayerBall player)
    {
        return player.HasMeta(AcceptedButtonPhysicsFrameMetadata) &&
            player.GetMeta(AcceptedButtonPhysicsFrameMetadata).AsUInt64() == Engine.GetPhysicsFrames();
    }

    private void ClearAllDeniedFeedback()
    {
        foreach (Node node in GetTree().GetNodesInGroup(PhysicalFloorButtonGroup))
        {
            if (node is RouteCheckpoint3D checkpoint)
            {
                checkpoint.ClearDeniedFeedback();
            }
        }
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
        _deniedFlashElapsed = 0.0f;
        _framePlate.MaterialOverride = _frameMaterial;
        _innerPlate.MaterialOverride = _idleMaterial;
        SetSequencePipsVisible(true);
        _handledFloorContacts.Clear();
        _suppressAutomaticPressUntilPhysicsFrame = Engine.GetPhysicsFrames() + 2;
        CaptureOverlappingPlayersAsHandled();
    }

    public override void _ExitTree()
    {
        if (AutomaticPressEnabled && IsPhysicalFloorButton)
        {
            BodyEntered -= OnPhysicalBodyEntered;
        }
        else if (AutomaticPressEnabled && AllowAreaPressFallback)
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
        RemoveFromGroup(PhysicalFloorButtonGroup);
        _handledFloorContacts.Clear();
    }

    private void ProcessFloorContacts()
    {
        if (Engine.GetPhysicsFrames() <= _suppressAutomaticPressUntilPhysicsFrame)
        {
            CaptureOverlappingPlayersAsHandled();
            return;
        }

        PlayerBall[] overlappingPlayers = GetOverlappingBodies().OfType<PlayerBall>().ToArray();
        HashSet<ulong> touchingIds = new();
        foreach (PlayerBall player in overlappingPlayers)
        {
            ulong id = player.GetInstanceId();
            if (!IsWithinFloorPressHeight(player))
            {
                continue;
            }

            touchingIds.Add(id);
            if (IsActivated || _handledFloorContacts.Contains(id) || !IsClosestTouchedFloorButton(player))
            {
                continue;
            }

            _handledFloorContacts.Add(id);
            Press(player);
        }

        foreach (ulong id in _handledFloorContacts.Where(id => !touchingIds.Contains(id)).ToArray())
        {
            _handledFloorContacts.Remove(id);
        }
    }

    private bool IsClosestTouchedFloorButton(PlayerBall player)
    {
        RouteCheckpoint3D? closest = null;
        float closestDistanceSquared = float.PositiveInfinity;
        foreach (Node node in GetTree().GetNodesInGroup(PhysicalFloorButtonGroup))
        {
            if (node is not RouteCheckpoint3D checkpoint ||
                !checkpoint.IsPlayerOnVisiblePlate(player))
            {
                continue;
            }

            Vector3 idlePlatePosition = checkpoint.GetIdlePlateGlobalPosition();
            float distanceSquared = new Vector2(
                player.GlobalPosition.X - idlePlatePosition.X,
                player.GlobalPosition.Z - idlePlatePosition.Z).LengthSquared();
            if (distanceSquared < closestDistanceSquared ||
                (Mathf.IsEqualApprox(distanceSquared, closestDistanceSquared) &&
                 (closest is null || checkpoint.GetInstanceId() < closest.GetInstanceId())))
            {
                closest = checkpoint;
                closestDistanceSquared = distanceSquared;
            }
        }

        return closest == this;
    }

    private bool IsPlayerOnVisiblePlate(PlayerBall player)
    {
        if (!IsWithinFloorPressHeight(player))
        {
            return false;
        }

        Vector3 localPlayer = ToLocal(player.GlobalPosition);
        float halfWidth = Mathf.Max(1.8f, TriggerSize.X * 0.72f) * 0.5f;
        float halfDepth = Mathf.Max(1.2f, TriggerSize.Z * 0.72f) * 0.5f;
        return Mathf.Abs(localPlayer.X - _innerPlate.Position.X) <= halfWidth + PlayerBallRadius &&
            Mathf.Abs(localPlayer.Z - _innerPlate.Position.Z) <= halfDepth + PlayerBallRadius;
    }

    private bool IsWithinFloorPressHeight(PlayerBall player)
    {
        // Use the plate's fixed idle height for contact arbitration.  A valid
        // button depresses in this same physics tick; measuring against its new
        // lower transform could otherwise let an overlapping invalid button
        // claim the contact immediately afterwards and flash red.
        float bottomOffset = (player.GlobalPosition.Y - PlayerBallRadius) - GetIdlePlateGlobalPosition().Y;
        if (bottomOffset < -FloorPressBelowTolerance || bottomOffset > FloorPressAboveTolerance)
        {
            return false;
        }

        return true;
    }

    private Vector3 GetIdlePlateGlobalPosition()
    {
        return ToGlobal(new Vector3(
            _innerPlate.Position.X,
            (-TriggerSize.Y * 0.42f) + 0.08f,
            _innerPlate.Position.Z));
    }

    private void DispatchPress(PlayerBall player)
    {
        OnBodyEntered(player);
    }

    private void OnPhysicalBodyEntered(Node3D body)
    {
        if (body is not PlayerBall player || IsActivated || !IsWithinFloorPressHeight(player))
        {
            return;
        }

        ulong id = player.GetInstanceId();
        if (Engine.GetPhysicsFrames() <= _suppressAutomaticPressUntilPhysicsFrame)
        {
            _handledFloorContacts.Add(id);
            return;
        }

        if (_handledFloorContacts.Contains(id) || !IsClosestTouchedFloorButton(player))
        {
            return;
        }

        _handledFloorContacts.Add(id);
        Press(player);
    }

    private void CaptureOverlappingPlayersAsHandled()
    {
        foreach (PlayerBall player in GetOverlappingBodies().OfType<PlayerBall>())
        {
            _handledFloorContacts.Add(player.GetInstanceId());
        }
    }

    private void ApplyAcceptedPressVisualImmediately()
    {
        _activationAmount = 1.0f;
        ClearDeniedFeedback();
        _framePlate.MaterialOverride = _frameMaterial;
        _innerPlate.MaterialOverride = _activeMaterial;
        SetSequencePipsVisible(true);
        _innerPlate.Position = new Vector3(
            _innerPlate.Position.X,
            (-TriggerSize.Y * 0.42f) + PressedPlateOffset,
            _innerPlate.Position.Z);
        _innerPlate.ForceUpdateTransform();
    }

    private void ClearDeniedFeedback()
    {
        _deniedFlashTime = 0.0f;
        _deniedFlashElapsed = 0.0f;
        if (!IsActivated && IsInstanceValid(_innerPlate))
        {
            _innerPlate.MaterialOverride = _idleMaterial;
        }
    }

    private void OnBodyEntered(Node3D body)
    {
        if (!IsActivated && body is PlayerBall player)
        {
            Entered?.Invoke(this, player);
        }
    }
}
