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

public partial class Room04Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_04_solution.tres";
    private const byte InteractAction = 1;
    private const int RequiredSolutionRuns = 10;
    private const int MaximumSolutionTicksPerRun = 1800;

    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private MechanicalLever _lever = null!;
    private readonly List<RouteCheckpoint3D> _sequenceButtons = new();
    private readonly Dictionary<RouteCheckpoint3D, Material> _buttonIdleMaterials = new();
    private readonly Dictionary<RouteCheckpoint3D, Tween> _wrongOrderTweens = new();
    private StaticBody3D _gate = null!;
    private Transform3D _spawnTransform;
    private Vector3 _gateClosedPosition;
    private SolutionTrace? _solutionTrace;
    private Tween? _gateTween;
    private bool _showInteractionPrompts;
    private bool _highContrastPrompts;
    private bool _leverActivatedThisRun;
    private int _nextSequenceButton;
    private bool _gateRaised;
    private bool _runSolutionSmoke;
    private bool _runPreview;
    private bool _runShellSmoke;
    private bool _runRecoverySmoke;
    private bool _runSequenceSmoke;
    private StandardMaterial3D _wrongOrderMaterial = null!;
    private int _solutionRun;
    private int _solutionTick;
    private int _solutionWarmupTicks = 12;
    private int _previewFrames;
    private int _shellSmokeTick;
    private int _recoverySmokeTick;
    private int _sequenceSmokeTick;
    private int _wrongOrderFeedbackCount;
    private Vector3 _lastInteractionAttemptPosition;
    private float _lastInteractionAttemptDistance;
    private bool _lastLeverCanInteract;
    private float _closestGoalPlanarDistance = float.PositiveInfinity;
    private Vector3 _closestGoalPosition;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        BuildRoom();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room04_a", new Vector3(5.8f, 8.8f, 13.4f), new Vector3(0.0f, 2.8f, -5.3f), 54.0f),
            new("room04_b", new Vector3(-5.8f, 5.8f, -5.0f), new Vector3(1.2f, 3.4f, 5.6f), 56.0f),
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

        string[] userArguments = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(userArguments, argument => argument == "--room04-solution-smoke");
        _runPreview = Array.Exists(userArguments, argument => argument == "--room04-preview");
        _runShellSmoke = Array.Exists(userArguments, argument => argument == "--room-shell-smoke");
        _runRecoverySmoke = Array.Exists(userArguments, argument => argument == "--room04-recovery-smoke");
        _runSequenceSmoke = Array.Exists(userArguments, argument => argument == "--room04-sequence-smoke");
        if (_runPreview)
        {
            _cameraRig.SetInputEnabled(false);
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
                FailSolutionSmoke($"The Room 04 SolutionTrace is invalid ({details}).");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_runPreview && ++_previewFrames >= 30)
        {
            string capturePath = ProjectSettings.GlobalizePath("user://room04-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(capturePath);
            GD.Print($"ROOM04_PREVIEW_CAPTURE: {capturePath}");
            GetTree().Quit(0);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_runSequenceSmoke)
        {
            RunSequenceSmokeTick();
            return;
        }

        if (_runRecoverySmoke)
        {
            RunRecoverySmokeTick();
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
        if (_runSolutionSmoke && _solutionTick > 0)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} touched the hazard floor at {_player.GlobalPosition}; lever={_leverActivatedThisRun}, closest_goal={_closestGoalPlanarDistance:F2} at {_closestGoalPosition}.");
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
        _gateTween.TweenProperty(_gate, "position:y", _gateClosedPosition.Y + 5.4f, 0.68f);
    }

    private void ResetMechanism()
    {
        _gateTween?.Kill();
        _gateTween = null;
        _leverActivatedThisRun = false;
        _nextSequenceButton = 0;
        _gateRaised = false;
        _lever.ResetLever();
        foreach (Tween tween in _wrongOrderTweens.Values)
        {
            tween.Kill();
        }
        _wrongOrderTweens.Clear();
        foreach (RouteCheckpoint3D button in _sequenceButtons)
        {
            button.ResetCheckpoint();
            if (_buttonIdleMaterials.TryGetValue(button, out Material? idleMaterial) &&
                button.GetNodeOrNull<MeshInstance3D>("InsetPlate") is MeshInstance3D insetPlate)
            {
                SetWrongOrderVisual(insetPlate, idleMaterial, showSequencePips: true);
            }
        }
        _gate.Position = _gateClosedPosition;
        _gate.CollisionLayer = 1;
        _gate.CollisionMask = 1;
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
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 04 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 04 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void RunRecoverySmokeTick()
    {
        _recoverySmokeTick++;
        if (_recoverySmokeTick == 1)
        {
            _player.ResetTo(new Transform3D(Basis.Identity, new Vector3(0.0f, 4.55f, -1.2f)));
            return;
        }

        if (_recoverySmokeTick == 45)
        {
            if (_player.GlobalPosition.Y < 3.6f)
            {
                GD.PushError($"ROOM04_RECOVERY_FAIL: the floor in front of the closed barrier was not continuous; position={_player.GlobalPosition}, resets={_player.ResetCount}.");
                GetTree().Quit(1);
                return;
            }

            _player.ResetTo(new Transform3D(Basis.Identity, _lever.GlobalPosition + new Vector3(2.0f, 0.73f, 0.0f)));
            _lever.Interact(_player);
            return;
        }

        if (_recoverySmokeTick >= 100)
        {
            bool barrierRaised = _leverActivatedThisRun && _gateRaised &&
                _gate.Position.Y >= _gateClosedPosition.Y + 5.25f;
            if (!barrierRaised)
            {
                GD.PushError($"ROOM04_RECOVERY_FAIL: the lever did not raise the barrier above the continuous floor; lever={_leverActivatedThisRun}, gateRaised={_gateRaised}, gateY={_gate.Position.Y:F3}, expected={_gateClosedPosition.Y + 5.25f:F3}.");
                GetTree().Quit(1);
                return;
            }

            GD.Print("ROOM04_RECOVERY_PASS: the closed barrier has a continuous safe floor and the lever raises it without a softlock pit.");
            GetTree().Quit(0);
        }
    }

    private void RunSequenceSmokeTick()
    {
        _sequenceSmokeTick++;
        if (_sequenceSmokeTick == 1)
        {
            _player.Freeze = true;
            _sequenceButtons[1].Press(_player);
            return;
        }

        if (_sequenceSmokeTick == 2)
        {
            AssertWrongOrderFeedback(_sequenceButtons[1], expectedFeedbackCount: 1, "second button before the lever");
            return;
        }

        if (_sequenceSmokeTick == 32)
        {
            AssertIdleButtonVisual(_sequenceButtons[1], "second button after its wrong-order flash");
            _sequenceButtons[0].Press(_player);
            return;
        }

        if (_sequenceSmokeTick == 33)
        {
            AssertWrongOrderFeedback(_sequenceButtons[0], expectedFeedbackCount: 2, "first button before the lever");
            return;
        }

        if (_sequenceSmokeTick == 63)
        {
            AssertIdleButtonVisual(_sequenceButtons[0], "first button after its wrong-order flash");
            _player.ResetTo(new Transform3D(Basis.Identity, _lever.GlobalPosition + new Vector3(1.4f, 0.73f, 0.0f)));
            _lever.Interact(_player);
            _sequenceButtons[0].Press(_player);
            _sequenceButtons[1].Press(_player);
            return;
        }

        if (_sequenceSmokeTick < 64)
        {
            return;
        }

        bool orderedSequenceComplete =
            _leverActivatedThisRun &&
            _nextSequenceButton == _sequenceButtons.Count &&
            _sequenceButtons.All(button => button.IsActivated);
        if (!orderedSequenceComplete)
        {
            FailSequenceSmoke($"lever/button order did not complete; lever={_leverActivatedThisRun}, buttons={_nextSequenceButton}/{_sequenceButtons.Count}.");
            return;
        }

        foreach (RouteCheckpoint3D button in _sequenceButtons)
        {
            MeshInstance3D insetPlate = button.GetNode<MeshInstance3D>("InsetPlate");
            foreach (MeshInstance3D pip in insetPlate.GetChildren().OfType<MeshInstance3D>())
            {
                if (!pip.Name.ToString().StartsWith("SequencePip", StringComparison.Ordinal) ||
                    pip.GetParent() != insetPlate ||
                    !pip.Visible ||
                    Mathf.Abs(pip.Position.Y - 0.065f) > 0.001f)
                {
                    FailSequenceSmoke($"{button.Name}/{pip.Name} did not remain attached to the depressed inset plate.");
                    return;
                }
            }
        }

        MeshInstance3D pedestal = _lever.GetNode<MeshInstance3D>("Pedestal");
        CollisionShape3D pedestalHitbox = _lever.GetNode<CollisionShape3D>("BaseCollision/PedestalHitbox");
        if (Mathf.Abs(pedestal.Rotation.Z) > 0.001f || Mathf.Abs(pedestalHitbox.Rotation.Z) > 0.001f)
        {
            FailSequenceSmoke($"lever pedestal is tilted; visual={Mathf.RadToDeg(pedestal.Rotation.Z):F2} degrees, collision={Mathf.RadToDeg(pedestalHitbox.Rotation.Z):F2} degrees.");
            return;
        }

        ExitDoor3D door = GetNode<ExitDoor3D>("ExitDoor");
        if (door.GetNodeOrNull<MeshInstance3D>("LeftFrame") is null ||
            door.GetNodeOrNull<MeshInstance3D>("RightFrame") is null ||
            door.GetNodeOrNull<MeshInstance3D>("Header") is null ||
            door.GetNodeOrNull<MeshInstance3D>("ChevronLeft") is null ||
            door.GetNodeOrNull<MeshInstance3D>("ChevronRight") is null)
        {
            FailSequenceSmoke("exit frame or its fixed chevron is missing.");
            return;
        }

        GD.Print("ROOM04_SEQUENCE_PASS: the upright lever is required before two ordered floor buttons, wrong input flashes solid red, and sequence pips remain attached to the depressed plates.");
        GetTree().Quit(0);
    }

    private void AssertWrongOrderFeedback(RouteCheckpoint3D button, int expectedFeedbackCount, string context)
    {
        MeshInstance3D insetPlate = button.GetNode<MeshInstance3D>("InsetPlate");
        bool pipsVisible = insetPlate.GetChildren().OfType<MeshInstance3D>()
            .Where(child => child.Name.ToString().StartsWith("SequencePip", StringComparison.Ordinal))
            .All(pip => pip.Visible);
        if (_wrongOrderFeedbackCount != expectedFeedbackCount ||
            button.IsActivated ||
            _nextSequenceButton != 0 ||
            !button.IsDeniedFeedbackActive ||
            !pipsVisible)
        {
            FailSequenceSmoke($"{context} did not display consistent denied feedback without advancing the sequence (feedback={_wrongOrderFeedbackCount}/{expectedFeedbackCount}, activated={button.IsActivated}, sequence={_nextSequenceButton}, denied={button.IsDeniedFeedbackActive}, pipsVisible={pipsVisible}).");
        }
    }

    private void AssertIdleButtonVisual(RouteCheckpoint3D button, string context)
    {
        MeshInstance3D insetPlate = button.GetNode<MeshInstance3D>("InsetPlate");
        bool pipsVisible = insetPlate.GetChildren().OfType<MeshInstance3D>()
            .Where(child => child.Name.ToString().StartsWith("SequencePip", StringComparison.Ordinal))
            .All(pip => pip.Visible);
        if (!_buttonIdleMaterials.TryGetValue(button, out Material? idleMaterial) ||
            insetPlate.MaterialOverride != idleMaterial ||
            !pipsVisible)
        {
            FailSequenceSmoke($"{context} did not return to its readable idle state.");
        }
    }

    private void FailSequenceSmoke(string message)
    {
        GD.PushError($"ROOM04_SEQUENCE_FAIL: {message}");
        GetTree().Quit(1);
    }

    private void RunSolutionTick()
    {
        if (_solutionTrace is null)
        {
            return;
        }

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

        if (_leverActivatedThisRun)
        {
            Vector3 goalPosition = GetNode<Area3D>("GoalCup").GlobalPosition;
            float planarDistance = new Vector2(
                _player.GlobalPosition.X - goalPosition.X,
                _player.GlobalPosition.Z - goalPosition.Z).Length();
            if (planarDistance < _closestGoalPlanarDistance)
            {
                _closestGoalPlanarDistance = planarDistance;
                _closestGoalPosition = _player.GlobalPosition;
            }
        }

        if (IsComplete)
        {
            if (!_leverActivatedThisRun || _nextSequenceButton != _sequenceButtons.Count)
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} bypassed a required step; lever={_leverActivatedThisRun}, buttons={_nextSequenceButton}/{_sequenceButtons.Count}.");
                return;
            }

            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print($"ROOM04_SOLUTION_PASS: SolutionTrace used E and completed Room 04 {_solutionRun} consecutive times.");
                GetTree().Quit(0);
                return;
            }

            ClearCompletionState();
            _player.SimulatedMoveInput = null;
            _player.Freeze = true;
            _player.ResetTo(_spawnTransform);
            _solutionTick = 0;
            _solutionWarmupTicks = 12;
            _closestGoalPlanarDistance = float.PositiveInfinity;
            _closestGoalPosition = Vector3.Zero;
            ResetMechanism();
            return;
        }

        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} timed out at position {_player.GlobalPosition}; lever={_leverActivatedThisRun}, buttons={_nextSequenceButton}/{_sequenceButtons.Count}, interaction_attempt={_lastInteractionAttemptPosition}, distance={_lastInteractionAttemptDistance:F2}, radius={_lever.ActivationRadius:F2}, can_interact={_lastLeverCanInteract}.");
            return;
        }

        (Vector2 moveInput, byte actionFlags) = ResolveTraceStep(_solutionTick - 1);
        _player.SimulatedMoveInput = moveInput;
        if ((actionFlags & InteractAction) != 0)
        {
            _lastInteractionAttemptPosition = _player.GlobalPosition;
            _lastInteractionAttemptDistance = _player.GlobalPosition.DistanceTo(_lever.GlobalPosition);
            _lastLeverCanInteract = _lever.CanInteract(_player);
            if (!_lastLeverCanInteract)
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} attempted E outside the lever hitbox at {_lastInteractionAttemptPosition}; distance={_lastInteractionAttemptDistance:F2}, radius={_lever.ActivationRadius:F2}.");
                return;
            }
            _lever.Interact(_player);
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
        Color steel = new("8d9aa5");
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
            new Vector3(-1.0f, 0.0f, 0.0f),
            new Vector2(27.0f, 40.0f),
            -2.0f,
            12.5f,
            metal,
            new Color("7d8997"),
            new Color("ad765f"),
            body =>
            {
                if (body is PlayerBall)
                {
                    RestartRoom();
                }
            });

        // Three flush slabs form one uninterrupted route from the shell wall to
        // the exit. The lever controls only the visible barrier; there is no
        // hidden pit or recovery trigger under it.
        RoomGeometry.AddBox(this, "StartDeck", new Vector3(18.0f, 0.5f, 25.05f), new Vector3(-1.0f, 3.5f, 7.225f), Vector3.Zero, metal, steel, 0.46f, 0.62f);
        RoomGeometry.AddBox(this, "ExitRun", new Vector3(18.0f, 0.5f, 14.45f), new Vector3(-1.0f, 3.5f, -12.525f), Vector3.Zero, metal, steel, 0.46f, 0.62f);

        _lever = new MechanicalLever
        {
            Name = "GateLever",
            Position = new Vector3(-6.5f, 3.73f, 4.5f),
            ActivationRadius = 3.2f,
        };
        _lever.Activated += OnLeverActivated;
        AddChild(_lever);
        AlignLeverToFloor();

        BuildGate();

        // Low guards define the safe starting platform without filling the
        // room with tall internal walls. The shell wall closes the rear edge.
        RoomGeometry.AddBox(this, "StartGuardLeft", new Vector3(0.36f, 0.58f, 22.55f), new Vector3(-10.18f, 4.04f, 8.475f), Vector3.Zero, metal, new Color("657584"), 0.5f, 0.56f);
        RoomGeometry.AddBox(this, "StartGuardRight", new Vector3(0.36f, 0.58f, 22.55f), new Vector3(8.18f, 4.04f, 8.475f), Vector3.Zero, metal, new Color("657584"), 0.5f, 0.56f);
        RoomGeometry.AddBox(this, "ExitGuardLeft", new Vector3(0.36f, 0.58f, 16.95f), new Vector3(-10.18f, 4.04f, -11.275f), Vector3.Zero, metal, new Color("657584"), 0.5f, 0.56f);
        RoomGeometry.AddBox(this, "ExitGuardRight", new Vector3(0.36f, 0.58f, 16.95f), new Vector3(8.18f, 4.04f, -11.275f), Vector3.Zero, metal, new Color("657584"), 0.5f, 0.56f);

        AddSequenceButton("SequenceButtonOne", 0, new Vector3(-4.0f, 5.01f, -7.0f));
        AddSequenceButton("SequenceButtonTwo", 1, new Vector3(-1.0f, 5.01f, -12.2f));

        SurfaceDetail.AddOverlay(this, "StartScuffs", new Vector3(-1.7f, 3.765f, 9.6f), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(12.0f)), new Vector2(2.8f, 1.7f), "res://assets/textures/overlays/edge_scuffs.svg", new Color("d8d7d0"), 0.32f);
        SurfaceDetail.AddOverlay(this, "LowerScratches", new Vector3(1.8f, 3.765f, -5.4f), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(-16.0f)), new Vector2(3.2f, 1.8f), "res://assets/textures/overlays/scratches.svg", new Color("d7dce2"), 0.34f);
        SurfaceDetail.AddOverlay(this, "ExitGrime", new Vector3(-2.1f, 3.762f, -10.4f), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(8.0f)), new Vector2(3.6f, 2.2f), "res://assets/textures/overlays/grime.svg", new Color("252934"), 0.4f);
    }

    private void BuildGate()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string copper = "res://assets/textures/copper_rivets.svg";
        StandardMaterial3D frame = RoomGeometry.CreateMaterial(metal, new Color("9ba7b2"), 0.5f, 0.55f);
        StandardMaterial3D slats = RoomGeometry.CreateMaterial(copper, new Color("ad6f51"), 0.42f, 0.52f);

        _gate = new StaticBody3D
        {
            Name = "RelayGate",
            Position = new Vector3(-1.0f, 0.0f, -2.8f),
            CollisionLayer = 1,
            CollisionMask = 1,
        };
        _gate.AddChild(new CollisionShape3D
        {
            Position = new Vector3(0.0f, 5.36f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(18.35f, 3.1f, 0.52f) },
        });
        AddChild(_gate);
        _gateClosedPosition = _gate.Position;

        RoomGeometry.AddVisualBox(_gate, "TopBeam", new Vector3(18.45f, 0.48f, 0.62f), new Vector3(0.0f, 6.92f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frame);
        RoomGeometry.AddVisualBox(_gate, "BottomBeam", new Vector3(18.45f, 0.35f, 0.62f), new Vector3(0.0f, 3.96f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frame);
        for (int index = 0; index < 14; index++)
        {
            float x = -8.125f + (index * 1.25f);
            RoomGeometry.AddVisualBox(_gate, $"GateSlat{index}", new Vector3(0.34f, 2.65f, 0.42f), new Vector3(x, 5.44f, 0.0f), new Vector3(0.0f, 0.0f, Mathf.DegToRad(index % 2 == 0 ? 2.0f : -2.0f)), string.Empty, Colors.White, 0.0f, 1.0f, slats);
        }

        StandardMaterial3D latch = RoomGeometry.CreateMaterial(copper, new Color("d09464"), 0.44f, 0.5f);
        RoomGeometry.AddCylinder(_gate, "LockHub", new Vector3(0.0f, 5.44f, 0.38f), new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f), 0.48f, 0.22f, latch);
        RoomGeometry.AddVisualBox(_gate, "LockArmA", new Vector3(2.9f, 0.2f, 0.2f), new Vector3(0.0f, 5.44f, 0.36f), new Vector3(0.0f, 0.0f, Mathf.DegToRad(42.0f)), string.Empty, Colors.White, 0.0f, 1.0f, latch);
        RoomGeometry.AddVisualBox(_gate, "LockArmB", new Vector3(2.9f, 0.2f, 0.2f), new Vector3(0.0f, 5.44f, 0.36f), new Vector3(0.0f, 0.0f, Mathf.DegToRad(-42.0f)), string.Empty, Colors.White, 0.0f, 1.0f, latch);
    }

    private void BuildGoal()
    {
        Vector3 goalPosition = new(-1.0f, 4.65f, -18.67f);
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
            Shape = new CylinderShape3D { Radius = 3.0f, Height = 2.5f },
        });
        goal.BodyEntered += body =>
        {
            if (body is PlayerBall &&
                _leverActivatedThisRun &&
                _nextSequenceButton == _sequenceButtons.Count)
            {
                CompleteRoom();
            }
        };
        AddChild(goal);

        RoomGeometry.AddGoalExitDoor(this, goalPosition);
    }

    private void AddSequenceButton(string name, int index, Vector3 position)
    {
        Vector3 triggerSize = new(3.0f, 3.0f, 3.0f);
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
                    GD.Print($"ROOM04_BUTTON_TRACE: button={_nextSequenceButton}/{_sequenceButtons.Count}, tick={_solutionTick}, position={player.GlobalPosition}, velocity={player.LinearVelocity}.");
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
        GD.PushError($"ROOM04_SOLUTION_FAIL: {message}");
        GetTree().Quit(1);
    }
}
