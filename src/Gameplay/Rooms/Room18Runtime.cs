using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Rooms;
using Velocitex.Core.Settings;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Interaction;
using Velocitex.Gameplay.Physics;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Visual;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room18Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_18_solution.tres";
    private const byte InteractAction = 1;
    private const int MaximumSolutionTicksPerRun = 1400;
    private const float AchievementRailContactOffset = 5.65f;

    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private MovingPlatform3D _platform = null!;
    private MechanicalLever _transitLever = null!;
    private readonly List<RouteCheckpoint3D> _balancePlates = new();
    private CollisionShape3D _departureGateCollision = null!;
    private MeshInstance3D _departureGateVisual = null!;
    private Vector3 _departureGateRestPosition;
    private Tween? _departureGateTween;
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private bool _boardedPlatform;
    private bool _reachedDestination;
    private bool _stayedAboard;
    private bool _transitLeverActivated;
    private bool _transitStarted;
    private bool _cleanTransitEligible;
    private bool _showInteractionPrompts;
    private bool _highContrastPrompts;
    private bool _runSolutionSmoke;
    private bool _runPreview;
    private bool _runShellSmoke;
    private bool _runMechanicsSmoke;
    private bool _runAchievementPositiveSmoke;
    private bool _runAchievementNegativeSmoke;
    private bool _solutionSmokeFinishing;
    private int _solutionRun;
    private int _solutionTick;
    private int _previewFrames;
    private int _shellSmokeTick;
    private int _nextBalancePlate;
    private float _maximumLateralOffset;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] args = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(args, argument => argument == "--room18-solution-smoke");
        _runPreview = Array.Exists(args, argument => argument == "--room18-preview");
        _runShellSmoke = Array.Exists(args, argument => argument == "--room-shell-smoke");
        _runMechanicsSmoke = Array.Exists(args, argument => argument == "--room18-mechanics-smoke");
        _runAchievementPositiveSmoke = Array.Exists(args, argument => argument == "--room18-achievement-positive-smoke");
        _runAchievementNegativeSmoke = Array.Exists(args, argument => argument == "--room18-achievement-negative-smoke");
        BuildRoom();
        if (Array.Exists(args, argument => argument == "--panorama-capture=room18_b"))
        {
            _platform.Position += _platform.EndOffset * 0.52f;
        }
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room18_a", new Vector3(9.0f, 14.0f, 31.0f), new Vector3(0.0f, 15.0f, -34.0f), 58.0f),
            new("room18_b", new Vector3(-11.0f, 20.0f, 5.0f), new Vector3(0.0f, 14.0f, -17.0f), 60.0f),
        });

        _player = GetNode<PlayerBall>("Player");
        _cameraRig = GetNode<PlayerCameraRig>("CameraRig");
        _spawnTransform = _player.GlobalTransform;
        _cameraRig.Follow(_player);
        _player.MovementBasis = _cameraRig.MovementBasis;
        GameSettingsData settings = SettingsStore.Load();
        _showInteractionPrompts = settings.InteractionPrompts;
        _highContrastPrompts = settings.HighContrastPrompts;
        Key interactKey = InputDefaults.GetPrimaryKey(InputDefaults.Interact);
        _transitLever.SetKeyLabel(interactKey == Key.None ? "E" : interactKey.ToString());
        if (_runPreview)
        {
            _cameraRig.SetInputEnabled(false);
        }

        _platform.PlayerBoarded += player =>
        {
            if (player == _player)
            {
                _boardedPlatform = true;
                _stayedAboard = true;
            }
        };
        _platform.PlayerLeftDuringTransit += player =>
        {
            if (player == _player)
            {
                _stayedAboard = false;
            }
        };
        _platform.Departed += () =>
        {
            _transitStarted = true;
            _cleanTransitEligible = true;
            _maximumLateralOffset = 0.0f;
        };
        _platform.ArrivedAtDestination += () =>
        {
            _reachedDestination = _stayedAboard && _platform.HasOccupant(_player);
            _transitStarted = false;
        };

        if (_runSolutionSmoke)
        {
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            if (_solutionTrace is null || _solutionTrace.RoomId != RoomId || _solutionTrace.MoveInputs.Count < 3 ||
                _solutionTrace.ActionFlags.Length != _solutionTrace.MoveInputs.Count || !_solutionTrace.ActionFlags.Contains(InteractAction))
            {
                FailSolutionSmoke("The Room 18 SolutionTrace is invalid.");
            }
        }

        if (_runMechanicsSmoke || _runAchievementPositiveSmoke || _runAchievementNegativeSmoke)
        {
            CallDeferred(MethodName.RunRequestedSmoke);
        }
    }

    public override void _Process(double delta)
    {
        if (_runPreview && ++_previewFrames >= 30)
        {
            string path = ProjectSettings.GlobalizePath("user://room18-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"ROOM18_PREVIEW_CAPTURE: {path}");
            GetTree().Quit(0);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        TrackTransitControl();

        if (_runShellSmoke)
        {
            RunShellSmokeTick();
            return;
        }

        if (_runSolutionSmoke)
        {
            RunSolutionTick();
            return;
        }

        if (_runMechanicsSmoke || _runAchievementPositiveSmoke || _runAchievementNegativeSmoke)
        {
            return;
        }

        bool canInteract = _transitLever.CanInteract(_player);
        bool focused = canInteract && _cameraRig.IsLookingAt(_transitLever.GlobalPosition + (Vector3.Up * 1.75f));
        _transitLever.SetFocused(focused && _showInteractionPrompts, _highContrastPrompts);
        if (focused && Godot.Input.IsActionJustPressed(InputDefaults.Interact))
        {
            _transitLever.Interact(_player);
        }

        if (_player.GlobalPosition.Y < -7.0f)
        {
            RestartRoom();
        }
    }

    public override void RestartRoom()
    {
        if (_runSolutionSmoke && _solutionTick > 0 && !_solutionSmokeFinishing)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} hit the maintenance floor at {_player.GlobalPosition}; boarded={_boardedPlatform}, destination={_reachedDestination}, progress={_platform.Progress:F2}.");
            return;
        }

        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _platform.ResetPlatform();
        _transitLever.ResetLever();
        ResetDepartureGate();
        _player.ResetTo(_spawnTransform);
        ResetTransitState();
    }

    private void RunShellSmokeTick()
    {
        _shellSmokeTick++;
        if (_shellSmokeTick == 1)
        {
            _player.ResetTo(new Transform3D(Basis.Identity, GetNode<Area3D>("RoomShell/HazardTrigger").GlobalPosition));
            return;
        }

        if (_shellSmokeTick < 12)
        {
            return;
        }

        if (_player.GlobalPosition.DistanceTo(_spawnTransform.Origin) > 0.15f)
        {
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 18 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 18 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void RunSolutionTick()
    {
        if (_solutionTrace is null || _solutionSmokeFinishing)
        {
            return;
        }

        if (IsComplete)
        {
            if (!_boardedPlatform || !_stayedAboard || !_reachedDestination || !_platform.HasReachedDestination || !_transitLeverActivated ||
                _nextBalancePlate != _balancePlates.Count ||
                !CompletedAdvancementIds.Contains("moving-with-it"))
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} bypassed the moving-platform exam or its achievement: boarded={_boardedPlatform}, destination={_reachedDestination}, plates={_nextBalancePlate}/{_balancePlates.Count}, clean={_cleanTransitEligible}, lateral={_maximumLateralOffset:F2}.");
                return;
            }

            GD.Print("ROOM18_SOLUTION_RUN_PASS: SolutionTrace boarded, balanced and completed the diagonal platform ride.");
            FinishSolutionSmoke(0);
            return;
        }

        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} timed out at {_player.GlobalPosition}; boarded={_boardedPlatform}, stayed={_stayedAboard}, destination={_reachedDestination}, progress={_platform.Progress:F2}, plates={_nextBalancePlate}/{_balancePlates.Count}, clean={_cleanTransitEligible}, lateral={_maximumLateralOffset:F2}.");
            return;
        }

        (Vector2 moveInput, byte actionFlags) = ResolveTraceStep(_solutionTick - 1);
        _player.SimulatedMoveInput = moveInput;
        if ((actionFlags & InteractAction) != 0)
        {
            _transitLever.Interact(_player);
        }
    }

    private (Vector2 MoveInput, byte ActionFlags) ResolveTraceStep(int tick)
    {
        if (_solutionTrace is null)
        {
            return (Vector2.Zero, (byte)0);
        }

        int remaining = tick;
        for (int index = 0; index < _solutionTrace.MoveInputs.Count; index++)
        {
            int duration = _solutionTrace.MoveDurationsTicks[index];
            if (remaining < duration)
            {
                return (_solutionTrace.MoveInputs[index], _solutionTrace.ActionFlags[index]);
            }
            remaining -= duration;
        }
        return _solutionTrace.HoldLastInput ? (_solutionTrace.MoveInputs[^1], _solutionTrace.ActionFlags[^1]) : (Vector2.Zero, (byte)0);
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string copper = "res://assets/textures/copper_rivets.svg";
        Color pale = new("abb7bb");
        Color frame = new("4f5f68");

        RoomGeometry.AddClosedRoomShell(this, "RoomShell", new Vector3(0.0f, 0.0f, -7.0f), new Vector2(24.0f, 82.0f), -3.0f, 38.0f, metal, new Color("59656a"), new Color("3d4b52"), body =>
        {
            if (body is PlayerBall)
            {
                RestartRoom();
            }
        });
        RoomGeometry.AddBox(this, "SafeStart", new Vector3(12.0f, 0.5f, 20.775f), new Vector3(0.0f, 6.0f, 23.3875f), Vector3.Zero, metal, pale, 0.48f, 0.62f);
        RoomGeometry.AddBox(this, "ExitDeck", new Vector3(14.0f, 0.5f, 20.7f), new Vector3(0.0f, 17.0f, -37.35f), Vector3.Zero, metal, pale.Lightened(0.04f), 0.48f, 0.62f);

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(this, $"StartRail{side}", new Vector3(0.36f, 1.4f, 20.775f), new Vector3(side * 6.35f, 6.75f, 23.3875f), Vector3.Zero, copper, frame, 0.38f, 0.6f);
            RoomGeometry.AddBox(this, $"ExitRail{side}", new Vector3(0.36f, 1.45f, 20.7f), new Vector3(side * 7.35f, 17.75f, -37.35f), Vector3.Zero, metal, frame, 0.42f, 0.62f);
            RoomGeometry.AddVisualBox(this, $"GuideRail{side}", new Vector3(0.34f, 0.34f, 30.1f), new Vector3(side * 6.0f, 10.7f, -6.0f), new Vector3(Mathf.DegToRad(-158.55f), 0.0f, 0.0f), copper, new Color("8a6956"), 0.36f, 0.58f);
        }

        foreach (float z in new[] { 5.0f, -5.0f, -15.0f })
        {
            float progress = (8.0f - z) / 28.0f;
            float y = 4.2f + (progress * 11.0f);
            RoomGeometry.AddVisualBox(this, $"GuidePylon{z}", new Vector3(10.4f, 0.3f, 0.5f), new Vector3(0.0f, y, z), Vector3.Zero, metal, frame.Darkened(0.08f), 0.42f, 0.62f);
        }

        _platform = new MovingPlatform3D
        {
            Name = "MovingPlatform",
            Position = new Vector3(0.0f, 5.95f, 8.0f),
            PlatformSize = new Vector3(12.0f, 0.6f, 14.0f),
            EndOffset = new Vector3(0.0f, 11.0f, -28.0f),
            DepartureDelayTicks = 30,
            TravelTicks = 240,
            EnableAudio = !_runSolutionSmoke,
            RequiresActivation = true,
            EnableRearGate = false,
        };
        AddChild(_platform);

        _transitLever = new MechanicalLever
        {
            Name = "TransitLever",
            Position = new Vector3(0.0f, 0.35f, -0.8f),
            ActivationRadius = 5.0f,
        };
        _transitLever.Activated += () =>
        {
            _transitLeverActivated = true;
            CloseDepartureGate();
            _platform.Activate();
        };
        _platform.AddChild(_transitLever);

        AddBalancePlate("BalancePlateLeft", 0, new Vector3(-2.35f, 1.18f, 0.35f));
        AddBalancePlate("BalancePlateRight", 1, new Vector3(2.35f, 1.18f, -0.35f));
        BuildDepartureGate(metal, frame);
    }

    private void BuildGoal()
    {
        Vector3 position = new(0.0f, 18.15f, -46.9f);
        Area3D goal = new() { Name = "GoalCup", Position = position, CollisionLayer = 0, CollisionMask = 1, Monitoring = true };
        goal.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 1.75f, Height = 2.8f } });
        goal.BodyEntered += body =>
        {
            if (body is PlayerBall && CanCompleteTransit())
            {
                TryAwardMovingWithIt();
                CompleteRoom();
            }
        };
        AddChild(goal);

        RoomGeometry.AddGoalExitDoor(this, position);
    }

    private void TrackTransitControl()
    {
        if (!_transitStarted || _player is null || !_platform.HasOccupant(_player))
        {
            return;
        }

        Vector3 localPosition = _platform.ToLocal(_player.GlobalPosition);
        if (_nextBalancePlate == 0 && localPosition.X <= -0.25f)
        {
            _balancePlates[0].Activate();
            _nextBalancePlate = 1;
        }
        else if (_nextBalancePlate == 1 && localPosition.X >= 0.25f)
        {
            _balancePlates[1].Activate();
            _nextBalancePlate = 2;
        }
        _maximumLateralOffset = Mathf.Max(_maximumLateralOffset, Mathf.Abs(localPosition.X));
        if (_maximumLateralOffset > AchievementRailContactOffset)
        {
            _cleanTransitEligible = false;
        }
    }

    private bool CanCompleteTransit() =>
        _boardedPlatform &&
        _stayedAboard &&
        _reachedDestination &&
        _platform.HasReachedDestination &&
        _transitLeverActivated &&
        _nextBalancePlate == _balancePlates.Count;

    private void TryAwardMovingWithIt()
    {
        if (CanCompleteTransit() && _cleanTransitEligible && _maximumLateralOffset <= AchievementRailContactOffset)
        {
            MarkAdvancementCondition("moving-with-it");
        }
    }

    private void AddBalancePlate(string name, int index, Vector3 localPosition)
    {
        RouteCheckpoint3D plate = new()
        {
            Name = name,
            Position = localPosition,
            CheckpointIndex = index,
            TriggerSize = new Vector3(2.6f, 2.0f, 13.5f),
            FrameTint = index == 0 ? new Color("8ea7b0") : new Color("c18a63"),
            FlatFloorMarker = true,
        };
        plate.Entered += (entered, player) =>
        {
            if (player != _player)
            {
                return;
            }
            if (!_transitStarted || entered.CheckpointIndex != _nextBalancePlate) { entered.FlashDenied(); return; }

            entered.Activate();
            _nextBalancePlate++;
        };
        _platform.AddChild(plate);
        _balancePlates.Add(plate);
    }

    private void BuildDepartureGate(string texture, Color tint)
    {
        Vector3 size = new(12.4f, 2.2f, 0.42f);
        _departureGateRestPosition = new Vector3(0.0f, 1.15f, 7.22f);
        StaticBody3D gate = new() { Name = "DepartureGate" };
        _departureGateCollision = new CollisionShape3D
        {
            Position = _departureGateRestPosition,
            Shape = new BoxShape3D { Size = size },
            Disabled = true,
        };
        _departureGateVisual = new MeshInstance3D
        {
            Name = "DepartureGateVisual",
            Position = _departureGateRestPosition + (Vector3.Down * 2.4f),
            Mesh = SurfaceMeshFactory.CreateTiledBox(size),
            MaterialOverride = RoomGeometry.CreateMaterial(texture, tint, 0.42f, 0.62f),
        };
        gate.AddChild(_departureGateCollision);
        gate.AddChild(_departureGateVisual);
        _platform.AddChild(gate);
    }

    private void CloseDepartureGate()
    {
        _departureGateTween?.Kill();
        _departureGateCollision.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
        _departureGateTween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _departureGateTween.TweenProperty(_departureGateVisual, "position", _departureGateRestPosition, 0.28f);
    }

    private void ResetDepartureGate()
    {
        if (_departureGateVisual is null || _departureGateCollision is null)
        {
            return;
        }

        _departureGateTween?.Kill();
        _departureGateTween = null;
        _departureGateCollision.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        _departureGateVisual.Position = _departureGateRestPosition + (Vector3.Down * 2.4f);
    }

    private void ResetTransitState()
    {
        _boardedPlatform = false;
        _reachedDestination = false;
        _stayedAboard = false;
        _transitLeverActivated = false;
        _transitStarted = false;
        _cleanTransitEligible = false;
        _nextBalancePlate = 0;
        _maximumLateralOffset = 0.0f;
        foreach (RouteCheckpoint3D plate in _balancePlates)
        {
            plate.ResetCheckpoint();
        }
    }

    private void RunRequestedSmoke()
    {
        if (_runMechanicsSmoke)
        {
            bool deckReachesDoor = GetNode<StaticBody3D>("ExitDeck").Position.Z - 10.35f <= -47.6f;
            bool platformContract = _platform.RequiresActivation && !_platform.EnableRearGate && _balancePlates.Count == 2;
            bool leverSupported = Mathf.Abs(_transitLever.Position.Y - 0.35f) < 0.01f;
            if (!deckReachesDoor || !platformContract || !leverSupported)
            {
                GD.PushError($"ROOM18_MECHANICS_FAIL: deck={deckReachesDoor}, platform={platformContract}, lever={leverSupported}, plates={_balancePlates.Count}.");
                GetTree().Quit(1);
                return;
            }
            GD.Print("ROOM18_MECHANICS_PASS: the sealed route, lever-gated departure and two moving balance plates are present.");
            GetTree().Quit(0);
            return;
        }

        _boardedPlatform = true;
        _stayedAboard = true;
        _reachedDestination = true;
        _transitLeverActivated = true;
        _nextBalancePlate = _balancePlates.Count;
        _cleanTransitEligible = _runAchievementPositiveSmoke;
        _maximumLateralOffset = _runAchievementPositiveSmoke ? 4.1f : 5.9f;
        typeof(MovingPlatform3D).GetProperty(nameof(MovingPlatform3D.HasReachedDestination))?.SetValue(_platform, true);
        TryAwardMovingWithIt();
        bool awarded = CompletedAdvancementIds.Contains("moving-with-it");
        bool expected = _runAchievementPositiveSmoke;
        if (awarded != expected)
        {
            GD.PushError($"ROOM18_ACHIEVEMENT_FAIL: expected={expected}, awarded={awarded}.");
            GetTree().Quit(1);
            return;
        }
        GD.Print(expected
            ? "ROOM18_ACHIEVEMENT_POSITIVE_PASS: a controlled clean transit awarded Moving With It."
            : "ROOM18_ACHIEVEMENT_NEGATIVE_PASS: rail contact denied Moving With It without blocking completion.");
        GetTree().Quit(0);
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM18_SOLUTION_FAIL: {message}");
        FinishSolutionSmoke(1);
    }

    private async void FinishSolutionSmoke(int code)
    {
        if (_solutionSmokeFinishing)
        {
            return;
        }
        _solutionSmokeFinishing = true;
        if (_player is not null)
        {
            _player.SimulatedMoveInput = null;
        }
        _solutionTrace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(code);
    }
}
