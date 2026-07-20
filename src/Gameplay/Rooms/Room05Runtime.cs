using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Rooms;
using Velocitex.Core.Settings;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Interaction;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Visual;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room05Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_05_solution.tres";
    private const byte InteractAction = 1;
    private const int RequiredSolutionRuns = 10;
    private const int MaximumSolutionTicksPerRun = 1800;
    private const float OriginalFirstGapExtension = 2.5f;
    private const float FirstGapExtension = 6.5f;
    private const float FirstGapAdditionalLength = FirstGapExtension - OriginalFirstGapExtension;
    private const float OriginalSecondGapExtension = 2.0f;
    private const float SecondGapExtension = 10.0f;
    private const float SecondGapAdditionalLength = SecondGapExtension - OriginalSecondGapExtension;
    private const float DownstreamDrop = 0.75f;
    private const float IntermediateRunExtension = 18.0f;

    private readonly List<FlightGate3D> _flightGates = new();
    private readonly List<RouteCheckpoint3D> _sequenceButtons = new();
    private readonly Dictionary<RouteCheckpoint3D, Material> _buttonIdleMaterials = new();
    private readonly Dictionary<RouteCheckpoint3D, Tween> _wrongOrderTweens = new();
    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private MechanicalLever _lever = null!;
    private StaticBody3D _gate = null!;
    private Transform3D _spawnTransform;
    private Vector3 _gateClosedPosition;
    private Vector3 _goalPosition;
    private SolutionTrace? _solutionTrace;
    private Tween? _gateTween;
    private bool _showInteractionPrompts;
    private bool _highContrastPrompts;
    private bool _leverActivatedThisRun;
    private bool _sawAirborneThisRun;
    private bool _gateRaised;
    private bool _runSolutionSmoke;
    private bool _runPreview;
    private bool _runShellSmoke;
    private bool _runSequenceSmoke;
    private bool _solutionSmokeFinishing;
    private int _nextFlightGate;
    private int _nextSequenceButton;
    private int _solutionRun;
    private int _solutionTick;
    private int _solutionWarmupTicks = 6;
    private int _previewFrames;
    private int _shellSmokeTick;
    private int _sequenceSmokeTick;
    private int _wrongOrderFeedbackCount;
    private Vector3 _lastInteractionAttemptPosition;
    private float _lastInteractionAttemptDistance;
    private float[] _closestGateRadialDistances = Array.Empty<float>();
    private Vector3[] _closestGatePositions = Array.Empty<Vector3>();
    private StandardMaterial3D _wrongOrderMaterial = null!;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] userArguments = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(userArguments, argument => argument == "--room05-solution-smoke");
        _runPreview = Array.Exists(userArguments, argument => argument == "--room05-preview");
        _runShellSmoke = Array.Exists(userArguments, argument => argument == "--room-shell-smoke");
        _runSequenceSmoke = Array.Exists(userArguments, argument => argument == "--room05-sequence-smoke");
        BuildRoom();
        BuildGoal();
        _closestGateRadialDistances = Enumerable.Repeat(float.PositiveInfinity, _flightGates.Count).ToArray();
        _closestGatePositions = new Vector3[_flightGates.Count];
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room05_a", new Vector3(6.1f, 9.2f, 16.0f), new Vector3(0.0f, 1.4f, -8.0f), 54.0f),
            new("room05_b", new Vector3(-6.2f, 5.2f, -2.0f), new Vector3(0.0f, 1.1f, -12.5f), 56.0f),
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
        string interactKeyLabel = interactKey == Key.None ? "E" : interactKey.ToString();
        _lever.SetKeyLabel(interactKeyLabel);

        if (Array.Exists(userArguments, argument => argument.StartsWith("--panorama-capture=", StringComparison.Ordinal)))
        {
            _showInteractionPrompts = false;
        }

        if (_runPreview)
        {
            _cameraRig.SetInputEnabled(false);
        }

        if (_runSequenceSmoke)
        {
            _player.Freeze = true;
        }

        if (_runSolutionSmoke)
        {
            _player.Freeze = true;
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            if (_solutionTrace is null ||
                _solutionTrace.RoomId != RoomId ||
                _solutionTrace.MoveInputs.Count == 0 ||
                _solutionTrace.MoveDurationsTicks.Length != _solutionTrace.MoveInputs.Count ||
                _solutionTrace.ActionFlags.Length != _solutionTrace.MoveInputs.Count ||
                !_solutionTrace.ActionFlags.Contains(InteractAction))
            {
                string details = _solutionTrace is null
                    ? "trace=null"
                    : $"trace_room='{_solutionTrace.RoomId}', inputs={_solutionTrace.MoveInputs.Count}, durations={_solutionTrace.MoveDurationsTicks.Length}, actions={_solutionTrace.ActionFlags.Length}";
                FailSolutionSmoke($"The Room 05 SolutionTrace is invalid ({details}).");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_runPreview && ++_previewFrames >= 30)
        {
            string capturePath = ProjectSettings.GlobalizePath("user://room05-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(capturePath);
            GD.Print($"ROOM05_PREVIEW_CAPTURE: {capturePath}");
            GetTree().Quit(0);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        TryCompleteAtGoal();

        if (_runSequenceSmoke)
        {
            RunSequenceSmokeTick();
            return;
        }

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

        if (!_player.IsGrounded && _player.GlobalPosition.Z < 8.0f && _player.GlobalPosition.Z > -40.0f)
        {
            _sawAirborneThisRun = true;
        }

        bool canInteract = _lever.CanInteract(_player);
        bool isFocused = canInteract && _cameraRig.IsLookingAt(_lever.GlobalPosition + (Vector3.Up * 1.75f));
        _lever.SetFocused(isFocused && _showInteractionPrompts, _highContrastPrompts);
        if (isFocused && Godot.Input.IsActionJustPressed(InputDefaults.Interact))
        {
            _lever.Interact(_player);
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
            FailSolutionSmoke($"Run {_solutionRun + 1} touched the hazard floor at {_player.GlobalPosition} after {_nextFlightGate}/{_flightGates.Count} flight gates; lever={_leverActivatedThisRun}, interaction={_lastInteractionAttemptPosition}, distance={_lastInteractionAttemptDistance:F2}. {DescribeGateDiagnostics()}");
            return;
        }

        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _player.ResetTo(_spawnTransform);
        ResetMechanism();
    }

    private void OnLeverActivated()
    {
        if (_gateRaised)
        {
            return;
        }

        _leverActivatedThisRun = true;
        _gateRaised = true;
        _lever.SetFocused(false, _highContrastPrompts);
        _gate.CollisionLayer = 0;
        _gate.CollisionMask = 0;
        _gateTween?.Kill();
        _gateTween = CreateTween().SetTrans(Tween.TransitionType.Quint).SetEase(Tween.EaseType.Out);
        _gateTween.TweenProperty(_gate, "position:y", _gateClosedPosition.Y + 5.0f, 0.7f);
    }

    private void ResetMechanism()
    {
        _gateTween?.Kill();
        _gateTween = null;
        _leverActivatedThisRun = false;
        _sawAirborneThisRun = false;
        _wrongOrderFeedbackCount = 0;
        _gateRaised = false;
        _lever.ResetLever();
        foreach (Tween tween in _wrongOrderTweens.Values)
        {
            tween.Kill();
        }
        _wrongOrderTweens.Clear();
        _gate.Position = _gateClosedPosition;
        _gate.CollisionLayer = 1;
        _gate.CollisionMask = 1;
        _nextFlightGate = 0;
        _nextSequenceButton = 0;
        foreach (RouteCheckpoint3D button in _sequenceButtons)
        {
            button.ResetCheckpoint();
            if (_buttonIdleMaterials.TryGetValue(button, out Material? idleMaterial) &&
                button.GetNodeOrNull<MeshInstance3D>("InsetPlate") is MeshInstance3D insetPlate)
            {
                SetWrongOrderVisual(insetPlate, idleMaterial, showSequencePips: true);
            }
        }
        for (int index = 0; index < _flightGates.Count; index++)
        {
            _flightGates[index].ResetGate();
            _closestGateRadialDistances[index] = float.PositiveInfinity;
            _closestGatePositions[index] = Vector3.Zero;
        }
    }

    private void RunShellSmokeTick()
    {
        _shellSmokeTick++;
        if (_shellSmokeTick == 1)
        {
            Area3D trigger = GetNode<Area3D>("RoomShell/HazardTrigger");
            _player.ResetTo(new Transform3D(Basis.Identity, trigger.GlobalPosition));
            return;
        }

        if (_shellSmokeTick < 12)
        {
            return;
        }

        if (_player.GlobalPosition.DistanceTo(_spawnTransform.Origin) > 0.15f)
        {
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 05 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 05 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void RunSequenceSmokeTick()
    {
        _sequenceSmokeTick++;
        if (_sequenceSmokeTick == 1)
        {
            AssertChapterLayout();
            _sequenceButtons[1].Press(_player);
            return;
        }

        if (_sequenceSmokeTick == 2)
        {
            AssertWrongOrderFeedback(_sequenceButtons[1]);
            return;
        }

        if (_sequenceSmokeTick == 32)
        {
            AssertIdleButtonVisual(_sequenceButtons[1]);
            _player.ResetTo(new Transform3D(Basis.Identity, _lever.GlobalPosition + new Vector3(1.4f, 0.73f, 0.0f)));
            _lever.Interact(_player);
            _sequenceButtons[0].Press(_player);
            _sequenceButtons[1].Press(_player);
            _sequenceButtons[2].Press(_player);
            return;
        }

        if (_sequenceSmokeTick < 33)
        {
            return;
        }

        if (!_leverActivatedThisRun ||
            _nextSequenceButton != _sequenceButtons.Count ||
            _sequenceButtons.Any(button => !button.IsActivated))
        {
            FailSequenceSmoke($"lever/button order did not complete; lever={_leverActivatedThisRun}, buttons={_nextSequenceButton}/{_sequenceButtons.Count}.");
            return;
        }

        foreach (RouteCheckpoint3D button in _sequenceButtons)
        {
            MeshInstance3D insetPlate = button.GetNode<MeshInstance3D>("InsetPlate");
            MeshInstance3D[] pips = insetPlate.GetChildren().OfType<MeshInstance3D>()
                .Where(child => child.Name.ToString().StartsWith("SequencePip", StringComparison.Ordinal))
                .ToArray();
            if (pips.Length != button.CheckpointIndex + 1 ||
                pips.Any(pip => pip.GetParent() != insetPlate || !pip.Visible || Mathf.Abs(pip.Position.Y - 0.065f) > 0.001f))
            {
                FailSequenceSmoke($"{button.Name} did not keep its sequence dots attached to the depressed inset plate.");
                return;
            }
        }

        GD.Print("ROOM05_SEQUENCE_PASS: the upright lever, three ordered floor buttons, attached sequence pips, solid-red wrong-order feedback and full-width start barrier are all enforced.");
        GetTree().Quit(0);
    }

    private void AssertChapterLayout()
    {
        if (_sequenceButtons.Count != 3 || _flightGates.Count != 2)
        {
            FailSequenceSmoke($"expected three ordered buttons and two flight gates; buttons={_sequenceButtons.Count}, gates={_flightGates.Count}.");
            return;
        }

        RouteCheckpoint3D middleButton = _sequenceButtons[1];
        StaticBody3D middleLanding = GetNode<StaticBody3D>("FirstLanding");
        CollisionShape3D middleLandingCollision = middleLanding.GetChildren().OfType<CollisionShape3D>().Single();
        Vector3 localButtonPosition = middleLandingCollision.ToLocal(middleButton.GlobalPosition);
        if (middleLandingCollision.Shape is not BoxShape3D middleLandingBox ||
            middleButton.GlobalPosition.Y < 5.9f ||
            Mathf.Abs(localButtonPosition.X) > (middleLandingBox.Size.X * 0.5f) - 2.1f ||
            Mathf.Abs(localButtonPosition.Z) > (middleLandingBox.Size.Z * 0.5f) - 2.1f)
        {
            FailSequenceSmoke($"button 2 is not mounted on the offset middle landing; button={middleButton.GlobalPosition}, landing={middleLanding.GlobalPosition}.");
            return;
        }

        CollisionShape3D barrierCollision = _gate.GetChildren().OfType<CollisionShape3D>().Single();
        if (barrierCollision.Shape is not BoxShape3D barrierBox ||
            barrierBox.Size.X < 20.45f ||
            barrierBox.Size.Y < 6.7f ||
            barrierCollision.GlobalPosition.Y - (barrierBox.Size.Y * 0.5f) > 10.27f)
        {
            FailSequenceSmoke("the closed start barrier leaves a side or vertical rebound bypass.");
            return;
        }

        MeshInstance3D pedestal = _lever.GetNode<MeshInstance3D>("Pedestal");
        CollisionShape3D pedestalHitbox = _lever.GetNode<CollisionShape3D>("BaseCollision/PedestalHitbox");
        if (Mathf.Abs(pedestal.Rotation.Z) > 0.001f || Mathf.Abs(pedestalHitbox.Rotation.Z) > 0.001f)
        {
            FailSequenceSmoke($"lever pedestal is tilted; visual={Mathf.RadToDeg(pedestal.Rotation.Z):F2}, collision={Mathf.RadToDeg(pedestalHitbox.Rotation.Z):F2}.");
            return;
        }

        ExitDoor3D door = GetNode<ExitDoor3D>("ExitDoor");
        if (Mathf.Abs(door.GlobalPosition.X) > 0.001f || Mathf.Abs(door.Rotation.Y) > 0.001f)
        {
            FailSequenceSmoke($"the shared exit door has a Room 05-only offset; position={door.GlobalPosition}, rotation={door.Rotation}.");
        }
    }

    private void AssertWrongOrderFeedback(RouteCheckpoint3D button)
    {
        MeshInstance3D insetPlate = button.GetNode<MeshInstance3D>("InsetPlate");
        bool pipsVisible = insetPlate.GetChildren().OfType<MeshInstance3D>()
            .Where(child => child.Name.ToString().StartsWith("SequencePip", StringComparison.Ordinal))
            .All(pip => pip.Visible);
        if (_wrongOrderFeedbackCount != 1 ||
            button.IsActivated ||
            _nextSequenceButton != 0 ||
            !button.IsDeniedFeedbackActive ||
            !pipsVisible)
        {
            FailSequenceSmoke("an out-of-order button did not show consistent denied feedback without advancing the sequence.");
        }
    }

    private void AssertIdleButtonVisual(RouteCheckpoint3D button)
    {
        MeshInstance3D insetPlate = button.GetNode<MeshInstance3D>("InsetPlate");
        bool pipsVisible = insetPlate.GetChildren().OfType<MeshInstance3D>()
            .Where(child => child.Name.ToString().StartsWith("SequencePip", StringComparison.Ordinal))
            .All(pip => pip.Visible);
        if (!_buttonIdleMaterials.TryGetValue(button, out Material? idleMaterial) ||
            insetPlate.MaterialOverride != idleMaterial ||
            !pipsVisible)
        {
            FailSequenceSmoke("the wrong-order flash did not return to the readable idle button state.");
        }
    }

    private void FailSequenceSmoke(string message)
    {
        GD.PushError($"ROOM05_SEQUENCE_FAIL: {message}");
        GetTree().Quit(1);
    }

    private void RunSolutionTick()
    {
        if (_solutionTrace is null)
        {
            return;
        }

        TrackGateDiagnostics();

        if (_solutionWarmupTicks > 0)
        {
            _player.SimulatedMoveInput = null;
            if (_solutionWarmupTicks == 3)
            {
                _player.Freeze = false;
            }
            _solutionWarmupTicks--;
            return;
        }

        if (!_player.IsGrounded && _player.GlobalPosition.Z < 8.0f && _player.GlobalPosition.Z > -40.0f)
        {
            _sawAirborneThisRun = true;
        }

        if (IsComplete)
        {
            if (!_leverActivatedThisRun ||
                !_sawAirborneThisRun ||
                _nextFlightGate != _flightGates.Count ||
                _nextSequenceButton != _sequenceButtons.Count)
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} bypassed a required chapter step; lever={_leverActivatedThisRun}, airborne={_sawAirborneThisRun}, gates={_nextFlightGate}/{_flightGates.Count}, buttons={_nextSequenceButton}/{_sequenceButtons.Count}.");
                return;
            }

            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print($"ROOM05_SOLUTION_PASS: SolutionTrace used the lever, completed all three ordered buttons, activated both flight gates and completed Room 05 {_solutionRun} consecutive times.");
                FinishSolutionSmoke(0);
                return;
            }

            ClearCompletionState();
            _player.SimulatedMoveInput = null;
            _player.Freeze = true;
            _player.ResetTo(_spawnTransform);
            _solutionTick = 0;
            _solutionWarmupTicks = 6;
            ResetMechanism();
            return;
        }

        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} timed out at position {_player.GlobalPosition}; lever={_leverActivatedThisRun}, airborne={_sawAirborneThisRun}, gates={_nextFlightGate}/{_flightGates.Count}. {DescribeGateDiagnostics()}");
            return;
        }

        (Vector2 moveInput, byte actionFlags) = ResolveTraceStep(_solutionTick - 1);
        _player.SimulatedMoveInput = moveInput;
        if ((actionFlags & InteractAction) != 0)
        {
            _lastInteractionAttemptPosition = _player.GlobalPosition;
            _lastInteractionAttemptDistance = _player.GlobalPosition.DistanceTo(_lever.GlobalPosition);
            bool canInteract = _lever.CanInteract(_player);
            _lever.Interact(_player);
            if (!_leverActivatedThisRun)
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} attempted E without activating the lever at {_lastInteractionAttemptPosition}; distance={_lastInteractionAttemptDistance:F2}, radius={_lever.ActivationRadius:F2}, can_interact={canInteract}.");
                return;
            }
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

        return _solutionTrace.HoldLastInput
            ? (_solutionTrace.MoveInputs[^1], (byte)0)
            : (Vector2.Zero, (byte)0);
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string copper = "res://assets/textures/copper_rivets.svg";
        const string caramel = "res://assets/textures/caramel_plates.svg";
        const string rubber = "res://assets/textures/rubber_chevrons.svg";
        Color paleSteel = new("a9b4ba");
        Color deepBlue = new("536b7a");
        Color warmCopper = new("b77a54");
        _wrongOrderMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color("d62f2f"),
            Metallic = 0.0f,
            Roughness = 0.54f,
            EmissionEnabled = true,
            Emission = new Color("721010"),
            EmissionEnergyMultiplier = 1.15f,
        };

        RoomGeometry.AddClosedRoomShell(
            this,
            "RoomShell",
            new Vector3(0.0f, 0.0f, -15.0f - ((FirstGapAdditionalLength + IntermediateRunExtension + SecondGapAdditionalLength) * 0.5f)),
            new Vector2(21.0f, 112.0f + FirstGapAdditionalLength + IntermediateRunExtension + SecondGapAdditionalLength),
            -4.5f,
            17.0f,
            metal,
            new Color("77838a"),
            new Color("a36c53"),
            body =>
            {
                if (body is PlayerBall)
                {
                    RestartRoom();
                }
            });

        // The start slab reaches the shell's inner back wall while preserving
        // the original launch edge, so there is no pointless pit behind spawn.
        RoomGeometry.AddBox(this, "StartDeck", new Vector3(20.5f, 0.5f, 14.063f), new Vector3(0.0f, 10.0f, 33.7185f), Vector3.Zero, metal, paleSteel, 0.42f, 0.64f);
        RoomGeometry.AddBox(this, "FirstLaunchSlope", new Vector3(17.0f, 0.5f, 17.012464f), new Vector3(0.0f, 7.925604f, 18.500690f), new Vector3(Mathf.DegToRad(-14.167753f), 0.0f, 0.0f), caramel, warmCopper, 0.18f, 0.66f);
        RoomGeometry.AddBox(this, "FirstLaunchLip", new Vector3(17.0f, 0.32f, 2.042f), new Vector3(0.0f, 5.926f, 9.171f), Vector3.Zero, caramel, warmCopper, 0.18f, 0.66f);
        RoomGeometry.AddBox(this, "FirstLanding", new Vector3(17.0f, 0.6f, 22.7f + IntermediateRunExtension), new Vector3(0.0f, 5.25f - DownstreamDrop, -5.8f - FirstGapExtension - (IntermediateRunExtension * 0.5f)), Vector3.Zero, rubber, deepBlue, 0.04f, 0.9f);
        RoomGeometry.AddBox(this, "SecondLaunchSlope", new Vector3(17.0f, 0.5f, 16.964397f), new Vector3(0.0f, 3.250463f - DownstreamDrop, -25.318373f - FirstGapExtension - IntermediateRunExtension), new Vector3(Mathf.DegToRad(-14.034605f), 0.0f, 0.0f), caramel, new Color("a96f4d"), 0.18f, 0.66f);
        RoomGeometry.AddBox(this, "SecondLaunchLip", new Vector3(17.0f, 0.32f, 2.042f), new Vector3(0.0f, 1.276f - DownstreamDrop, -34.629f - FirstGapExtension - IntermediateRunExtension), Vector3.Zero, caramel, new Color("a96f4d"), 0.18f, 0.66f);
        RoomGeometry.AddBox(this, "SecondLanding", new Vector3(17.0f, 0.6f, 10.0f), new Vector3(0.0f, 0.7f - DownstreamDrop, -43.5f - FirstGapExtension - SecondGapExtension - IntermediateRunExtension), Vector3.Zero, metal, paleSteel, 0.42f, 0.64f);
        RoomGeometry.AddBox(this, "ExitRun", new Vector3(17.0f, 0.6f, 17.75f), new Vector3(0.0f, 0.7f - DownstreamDrop, -57.375f - FirstGapExtension - SecondGapExtension - IntermediateRunExtension), Vector3.Zero, metal, paleSteel, 0.42f, 0.64f);

        foreach (float x in new[] { -8.75f, 8.75f })
        {
            RoomGeometry.AddBox(this, $"ExitRail{x}", new Vector3(0.34f, 1.35f, 27.75f), new Vector3(x, 1.38f - DownstreamDrop, -52.375f - FirstGapExtension - SecondGapExtension - IntermediateRunExtension), Vector3.Zero, copper, warmCopper, 0.38f, 0.56f);
        }

        _lever = new MechanicalLever
        {
            Name = "StartLever",
            Position = new Vector3(-8.0f, 10.27f, 31.0f),
            ActivationRadius = 3.6f,
        };
        _lever.Activated += OnLeverActivated;
        AddChild(_lever);
        AlignLeverToFloor();

        BuildStartGate();
        AddFlightGate("FlightGateNear", 0, new Vector3(-4.0f, 5.55f, 6.7f - (FirstGapExtension * 0.5f)), 2.25f);
        AddFlightGate("FlightGateFar", 1, new Vector3(0.0f, 1.45f, -36.0f - FirstGapExtension - (SecondGapExtension * 0.5f) - IntermediateRunExtension), 3.15f);
        AddSequenceButton("ChapterButtonOne", 0, new Vector3(3.5f, 11.51f, 31.0f));
        AddSequenceButton("ChapterButtonTwo", 1, new Vector3(-3.5f, 6.06f, -31.0f));
        AddSequenceButton("ChapterButtonThree", 2, new Vector3(3.5f, 1.51f, -63.5f - FirstGapExtension - SecondGapExtension - IntermediateRunExtension));

        SurfaceDetail.AddOverlay(this, "StartScuffs", new Vector3(-1.9f, 10.265f, 30.0f), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(10.0f)), new Vector2(3.2f, 1.9f), "res://assets/textures/overlays/edge_scuffs.svg", new Color("e2e0d4"), 0.3f);
        SurfaceDetail.AddOverlay(this, "SlopeSugar", new Vector3(-2.2f, 8.1f, 18.2f), new Vector3(Mathf.DegToRad(-104.0f), 0.0f, Mathf.DegToRad(-12.0f)), new Vector2(4.0f, 2.3f), "res://assets/textures/overlays/sugar_dust.svg", new Color("ffe9bf"), 0.45f);
        SurfaceDetail.AddOverlay(this, "LandingScratches", new Vector3(2.5f, 5.565f - DownstreamDrop, -5.8f - FirstGapExtension - (IntermediateRunExtension * 0.5f)), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(-18.0f)), new Vector2(3.5f, 2.0f), "res://assets/textures/overlays/scratches.svg", new Color("d7e5e8"), 0.35f);
        SurfaceDetail.AddOverlay(this, "ExitGrime", new Vector3(-1.9f, 1.015f - DownstreamDrop, -56.8f - FirstGapExtension - SecondGapExtension - IntermediateRunExtension), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(8.0f)), new Vector2(3.3f, 2.1f), "res://assets/textures/overlays/grime.svg", new Color("26282b"), 0.4f);
    }

    private void BuildStartGate()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string copper = "res://assets/textures/copper_rivets.svg";
        StandardMaterial3D frame = RoomGeometry.CreateMaterial(metal, new Color("a5b0b6"), 0.42f, 0.6f);
        StandardMaterial3D slats = RoomGeometry.CreateMaterial(copper, new Color("b27652"), 0.38f, 0.56f);

        _gate = new StaticBody3D
        {
            Name = "StartGate",
            // Sit on the exact front edge of StartDeck. The previous Z=25
            // placement put the lower beam and collision onto the descending
            // launch slope instead of closing the end of the first platform.
            Position = new Vector3(0.0f, 0.0f, 26.687f),
            CollisionLayer = 1,
            CollisionMask = 1,
        };
        _gate.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0.0f, 13.625f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(20.5f, 6.75f, 0.62f) },
        });
        AddChild(_gate);
        _gateClosedPosition = _gate.Position;

        RoomGeometry.AddVisualBox(_gate, "TopBeam", new Vector3(20.5f, 0.48f, 0.72f), new Vector3(0.0f, 16.7f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frame);
        RoomGeometry.AddVisualBox(_gate, "BottomBeam", new Vector3(20.5f, 0.35f, 0.72f), new Vector3(0.0f, 10.25f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frame);
        for (int index = 0; index < 16; index++)
        {
            float x = -9.375f + (index * 1.25f);
            RoomGeometry.AddVisualBox(_gate, $"GateSlat{index}", new Vector3(0.34f, 6.15f, 0.48f), new Vector3(x, 13.47f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, slats);
        }

        StandardMaterial3D latch = RoomGeometry.CreateMaterial(copper, new Color("d09464"), 0.44f, 0.5f);
        RoomGeometry.AddCylinder(_gate, "LockHub", new Vector3(0.0f, 13.47f, 0.42f), new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f), 0.5f, 0.22f, latch);
        RoomGeometry.AddVisualBox(_gate, "LockArmA", new Vector3(3.0f, 0.2f, 0.2f), new Vector3(0.0f, 13.47f, 0.4f), new Vector3(0.0f, 0.0f, Mathf.DegToRad(42.0f)), string.Empty, Colors.White, 0.0f, 1.0f, latch);
        RoomGeometry.AddVisualBox(_gate, "LockArmB", new Vector3(3.0f, 0.2f, 0.2f), new Vector3(0.0f, 13.47f, 0.4f), new Vector3(0.0f, 0.0f, Mathf.DegToRad(-42.0f)), string.Empty, Colors.White, 0.0f, 1.0f, latch);
    }

    private void AddFlightGate(string name, int index, Vector3 position, float radius)
    {
        FlightGate3D gate = new()
        {
            Name = name,
            Position = position,
            Radius = radius,
            FrameTint = index == 0 ? new Color("b77a54") : new Color("8f6b55"),
            EnableAudio = !_runSolutionSmoke,
            MinimumExitSpeed = index == 0 ? 30.0f : 35.0f,
            SpeedGain = 6.0f,
            SpeedMultiplier = index == 0 ? 1.3f : 1.25f,
            AxialBoostOnly = true,
            MaximumDownwardExitSpeed = index == 0 ? 2.5f : 1.5f,
        };
        gate.Passed += player =>
        {
            if (player == _player && index == _nextFlightGate)
            {
                // The second ring deliberately turns the long low jump into a
                // rising arc. This makes its assistance obvious and gives the
                // player a stable landing window; a zero-boost test ring does
                // not receive this vertical impulse.
                if (index == 1 && gate.LastExitSpeed > gate.LastEntrySpeed + 1.0f)
                {
                    Vector3 boostedVelocity = player.LinearVelocity;
                    boostedVelocity.Y = Mathf.Max(boostedVelocity.Y, 5.5f);
                    player.LinearVelocity = boostedVelocity;
                }
                _nextFlightGate++;
                if (_runSolutionSmoke)
                {
                    GD.Print($"ROOM05_GATE_TRACE: gate={index + 1}, tick={_solutionTick}, position={_player.GlobalPosition}, velocity={_player.LinearVelocity}.");
                }
            }
        };
        AddChild(gate);
        _flightGates.Add(gate);
    }

    private void TrackGateDiagnostics()
    {
        for (int index = 0; index < _flightGates.Count; index++)
        {
            Vector3 localPosition = _flightGates[index].ToLocal(_player.GlobalPosition);
            if (Mathf.Abs(localPosition.Z) > 1.2f)
            {
                continue;
            }

            float radialDistance = new Vector2(localPosition.X, localPosition.Y).Length();
            if (radialDistance < _closestGateRadialDistances[index])
            {
                _closestGateRadialDistances[index] = radialDistance;
                _closestGatePositions[index] = _player.GlobalPosition;
            }
        }
    }

    private string DescribeGateDiagnostics()
    {
        return string.Join("; ", _flightGates.Select((gate, index) =>
            float.IsPositiveInfinity(_closestGateRadialDistances[index])
                ? $"gate{index + 1}=no plane crossing"
                : $"gate{index + 1}=radial {_closestGateRadialDistances[index]:F2} at {_closestGatePositions[index]} (required center <= {gate.TriggerRadius:F2})"));
    }

    private void BuildGoal()
    {
        Vector3 doorPosition = new(0.0f, 1.65f - DownstreamDrop, -65.17f - FirstGapExtension - SecondGapExtension - IntermediateRunExtension);
        Vector3 goalPosition = doorPosition;
        _goalPosition = goalPosition;
        Area3D goal = new()
        {
            Name = "GoalCup",
            Position = goalPosition,
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true,
        };
        goal.AddChild(new CollisionShape3D
        {
            Shape = new CylinderShape3D { Radius = 2.2f, Height = 2.8f },
        });
        goal.BodyEntered += body =>
        {
            if (body is PlayerBall &&
                _leverActivatedThisRun &&
                _sawAirborneThisRun &&
                _nextFlightGate == _flightGates.Count &&
                _nextSequenceButton == _sequenceButtons.Count)
            {
                CompleteRoom();
            }
        };
        AddChild(goal);

        RoomGeometry.AddGoalExitDoor(this, doorPosition);
    }

    private void TryCompleteAtGoal()
    {
        if (IsComplete || IsExitTraversalPending ||
            !_leverActivatedThisRun ||
            !_sawAirborneThisRun ||
            _nextFlightGate != _flightGates.Count ||
            _nextSequenceButton != _sequenceButtons.Count)
        {
            return;
        }

        Vector2 planarOffset = new(_player.GlobalPosition.X - _goalPosition.X, _player.GlobalPosition.Z - _goalPosition.Z);
        if (planarOffset.Length() <= 4.5f && Mathf.Abs(_player.GlobalPosition.Y - _goalPosition.Y) <= 2.5f)
        {
            CompleteRoom();
        }
    }

    private void AddSequenceButton(string name, int index, Vector3 position)
    {
        Vector3 triggerSize = index == 1
            ? new Vector3(4.8f, 3.0f, 4.2f)
            : new Vector3(4.2f, 3.0f, 4.2f);
        RouteCheckpoint3D button = new()
        {
            Name = name,
            Position = position,
            CheckpointIndex = index,
            TriggerSize = triggerSize,
            FrameTint = RoomGeometry.SequenceButtonFrameTint,
            FlatFloorMarker = true,
        };
        button.Entered += (entered, player) =>
        {
            if (player != _player)
            {
                return;
            }

            if (_leverActivatedThisRun && entered.CheckpointIndex == _nextSequenceButton)
            {
                entered.Activate();
                _nextSequenceButton++;
                if (_runSolutionSmoke)
                {
                    GD.Print($"ROOM05_BUTTON_TRACE: button={_nextSequenceButton}/{_sequenceButtons.Count}, tick={_solutionTick}, position={player.GlobalPosition}, velocity={player.LinearVelocity}.");
                }
            }
            else
            {
                FlashWrongOrder(entered);
            }
        };
        AddChild(button);
        MeshInstance3D insetPlate = button.GetNode<MeshInstance3D>("InsetPlate");
        if (insetPlate.MaterialOverride is Material idleMaterial)
        {
            _buttonIdleMaterials[button] = idleMaterial;
        }
        RoomGeometry.AddSequencePips(insetPlate, index + 1);
        _sequenceButtons.Add(button);
    }

    private void FlashWrongOrder(RouteCheckpoint3D button)
    {
        _wrongOrderFeedbackCount++;
        button.FlashDenied();
    }

    private static void SetWrongOrderVisual(MeshInstance3D insetPlate, Material material, bool showSequencePips)
    {
        insetPlate.MaterialOverride = material;
        foreach (MeshInstance3D pip in insetPlate.GetChildren().OfType<MeshInstance3D>())
        {
            if (pip.Name.ToString().StartsWith("SequencePip", StringComparison.Ordinal))
            {
                pip.Visible = showSequencePips;
            }
        }
    }

    private void AlignLeverToFloor()
    {
        if (_lever.GetNodeOrNull<MeshInstance3D>("Pedestal") is MeshInstance3D pedestal)
        {
            pedestal.Rotation = Vector3.Zero;
        }
        if (_lever.GetNodeOrNull<CollisionShape3D>("BaseCollision/PedestalHitbox") is CollisionShape3D pedestalHitbox)
        {
            pedestalHitbox.Rotation = Vector3.Zero;
        }
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM05_SOLUTION_FAIL: {message}");
        FinishSolutionSmoke(1);
    }

    private async void FinishSolutionSmoke(int exitCode)
    {
        if (_solutionSmokeFinishing)
        {
            return;
        }

        _solutionSmokeFinishing = true;
        _player.SimulatedMoveInput = null;
        foreach (FlightGate3D gate in _flightGates)
        {
            gate.ResetGate();
            gate.QueueFree();
        }
        _flightGates.Clear();
        _solutionTrace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(exitCode);
    }
}
