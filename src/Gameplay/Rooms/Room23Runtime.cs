using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Rooms;
using Velocitex.Core.Settings;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Physics;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Visual;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room23Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_23_solution.tres";
    private const byte InteractAction = 1;
    private const int RequiredRouteStages = 2;
    private const int RequiredSolutionRuns = 1;
    private const int MaximumSolutionTicksPerRun = 1400;

    private readonly List<RouteCheckpoint3D> _routeStages = new();
    private readonly List<FlightGate3D> _flightGates = new();
    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private MomentumBank3D _bank = null!;
    private Area3D _goal = null!;
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private bool _runSolutionSmoke;
    private bool _runShellSmoke;
    private bool _runPreview;
    private bool _runMechanicsSmoke;
    private bool _solutionSmokeFinishing;
    private bool _capturedFullCharge;
    private bool _released;
    private bool _landedHigh;
    private bool _showPrompts;
    private bool _highContrastPrompts;
    private int _nextRouteStage;
    private int _nextFlightGate;
    private int _solutionRun;
    private int _solutionTick;
    private int _shellSmokeTick;
    private int _previewFrames;
    private int _mechanicsSmokeTick;
    private float _storedSpeed;
    private float _releaseSpeed;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] args = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(args, value => value == "--room23-solution-smoke");
        _runShellSmoke = Array.Exists(args, value => value == "--room-shell-smoke");
        _runPreview = Array.Exists(args, value => value == "--room23-preview");
        _runMechanicsSmoke = Array.Exists(args, value => value == "--room23-mechanics-smoke");

        BuildRoom();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room23_a", new Vector3(12.0f, 20.0f, 51.0f), new Vector3(-1.0f, 9.0f, -8.0f), 58.0f),
            new("room23_b", new Vector3(-12.0f, 18.0f, -20.0f), new Vector3(0.0f, 11.0f, -64.0f), 58.0f),
        });

        _player = GetNode<PlayerBall>("Player");
        _cameraRig = GetNode<PlayerCameraRig>("CameraRig");
        _spawnTransform = _player.GlobalTransform;
        _cameraRig.Follow(_player);
        _player.MovementBasis = _cameraRig.MovementBasis;
        GameSettingsData settings = SettingsStore.Load();
        _showPrompts = settings.InteractionPrompts;
        _highContrastPrompts = settings.HighContrastPrompts;
        Key key = InputDefaults.GetPrimaryKey(InputDefaults.Interact);
        _bank.SetKeyLabel(key == Key.None ? "E" : key.ToString());
        if (_runPreview) { _cameraRig.SetInputEnabled(false); _showPrompts = false; }

        _bank.MomentumCaptured += OnBankFullyCharged;
        _bank.MomentumReleased += OnBankReleased;

        if (_runSolutionSmoke)
        {
            _showPrompts = false;
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            if (_solutionTrace is null ||
                _solutionTrace.RoomId != RoomId ||
                _solutionTrace.ActionFlags.Length != _solutionTrace.MoveInputs.Count ||
                !_solutionTrace.ActionFlags.Contains(InteractAction) ||
                _solutionTrace.MoveInputs.Count < 8)
            {
                FailSolutionSmoke("The Room 23 SolutionTrace must steer the descent, wait for the full timed charge, release it and clear both flight gates.");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_runPreview && ++_previewFrames >= 30)
        {
            string path = ProjectSettings.GlobalizePath("user://room23-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"ROOM23_PREVIEW_CAPTURE: {path}");
            GetTree().Quit(0);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_runShellSmoke) { RunShellSmokeTick(); return; }
        if (_runMechanicsSmoke) { RunMechanicsSmokeTick(); return; }
        TrackLandingAndGoal();
        if (_runSolutionSmoke) { RunSolutionTick(); return; }

        bool focused = _bank.CanInteract(_player) && _cameraRig.IsLookingAt(_bank.GlobalPosition + (Vector3.Up * 2.0f));
        _bank.SetFocused(focused && _showPrompts, _highContrastPrompts);
        if (focused && Godot.Input.IsActionJustPressed(InputDefaults.Interact)) { _bank.Interact(_player); }
        if (_player.GlobalPosition.Y < -7.0f) { RestartRoom(); }
    }

    public override void RestartRoom()
    {
        if (_runSolutionSmoke && _solutionTick > 0 && !_solutionSmokeFinishing)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} touched the hazard floor after {_nextRouteStage}/{RequiredRouteStages} route stages and {_nextFlightGate}/{_flightGates.Count} flight gates.");
            return;
        }
        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _bank.ResetBank();
        _player.ResetTo(_spawnTransform);
        ResetState();
    }

    private void RunSolutionTick()
    {
        if (_solutionTrace is null || _solutionSmokeFinishing) { return; }
        if (IsComplete)
        {
            if (!_released || !_landedHigh || _releaseSpeed < 23.9f || _nextRouteStage != RequiredRouteStages || _nextFlightGate != _flightGates.Count)
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} bypassed the vault: route={_nextRouteStage}/{RequiredRouteStages}, charged={_capturedFullCharge}, released={_released}, landed={_landedHigh}, gates={_nextFlightGate}/{_flightGates.Count}, stored={_storedSpeed:F2}, release={_releaseSpeed:F2}.");
                return;
            }
            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print($"ROOM23_SOLUTION_PASS: SolutionTrace steered all {RequiredRouteStages} descent stages, released a {_releaseSpeed:F2} m/s partial timed charge, cleared both precision flight gates and landed on the compact catch deck.");
                FinishSolutionSmoke(0);
                return;
            }
            ClearCompletionState();
            _bank.ResetBank();
            _player.ResetTo(_spawnTransform);
            _solutionTick = 0;
            ResetState();
            return;
        }
        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} timed out at {_player.GlobalPosition}; route={_nextRouteStage}/{RequiredRouteStages}, charged={_capturedFullCharge}, released={_released}, landed={_landedHigh}, gates={_nextFlightGate}/{_flightGates.Count}.");
            return;
        }
        (Vector2 moveInput, byte actionFlags) = ResolveTraceStep(_solutionTick - 1);
        _player.SimulatedMoveInput = moveInput;
        if ((actionFlags & InteractAction) != 0) { _bank.Interact(_player); }
    }

    private (Vector2 MoveInput, byte ActionFlags) ResolveTraceStep(int tick)
    {
        if (_solutionTrace is null) { return (Vector2.Zero, 0); }
        int remaining = tick;
        for (int index = 0; index < _solutionTrace.MoveInputs.Count; index++)
        {
            int duration = _solutionTrace.MoveDurationsTicks[index];
            if (remaining < duration) { return (_solutionTrace.MoveInputs[index], _solutionTrace.ActionFlags[index]); }
            remaining -= duration;
        }
        return _solutionTrace.HoldLastInput ? (_solutionTrace.MoveInputs[^1], _solutionTrace.ActionFlags[^1]) : (Vector2.Zero, (byte)0);
    }

    private void ResetState()
    {
        _capturedFullCharge = false;
        _released = false;
        _landedHigh = false;
        _storedSpeed = 0.0f;
        _releaseSpeed = 0.0f;
        _nextRouteStage = 0;
        _nextFlightGate = 0;
        foreach (RouteCheckpoint3D stage in _routeStages) { stage.ResetCheckpoint(); }
        foreach (FlightGate3D gate in _flightGates) { gate.ResetGate(); }
    }

    private void OnBankFullyCharged(PlayerBall player, float speed)
    {
        if (player != _player) { return; }
        _capturedFullCharge = true;
        _storedSpeed = speed;
        if (_runSolutionSmoke) { GD.Print($"ROOM23_CHARGE_TRACE: tick={_solutionTick}, stored={speed:F2}, route={_nextRouteStage}/{RequiredRouteStages}."); }
    }

    private void OnBankReleased(PlayerBall player, float speed)
    {
        if (player != _player) { return; }
        _released = true;
        _releaseSpeed = speed;
        if (_capturedFullCharge && speed >= 31.9f)
        {
            MarkAdvancementCondition("full-account");
        }
        if (_runSolutionSmoke) { GD.Print($"ROOM23_RELEASE_TRACE: tick={_solutionTick}, speed={speed:F2}, position={player.GlobalPosition}."); }
    }

    private void TrackLandingAndGoal()
    {
        Vector3 position = _player.GlobalPosition;
        if (_released && _nextFlightGate == _flightGates.Count && _player.IsGrounded && Mathf.Abs(position.X) <= 10.5f && position.Z <= -58.0f && position.Z >= -80.0f && position.Y >= 10.35f)
        {
            _landedHigh = true;
        }
        Vector2 goalOffset = new(position.X - _goal.GlobalPosition.X, position.Z - _goal.GlobalPosition.Z);
        if (_released && _landedHigh && _nextRouteStage == RequiredRouteStages && _nextFlightGate == _flightGates.Count && goalOffset.Length() <= 2.0f && Mathf.Abs(position.Y - _goal.GlobalPosition.Y) <= 1.6f)
        {
            CompleteRoom();
        }
    }

    private void RunMechanicsSmokeTick()
    {
        _mechanicsSmokeTick++;
        if (_mechanicsSmokeTick == 1)
        {
            _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
            if (IsComplete || IsExitTraversalPending) { FailMechanicsSmoke("Direct goal entry completed the room."); return; }
            Area3D capture = _bank.GetNode<Area3D>("CaptureArea");
            capture.EmitSignal(Area3D.SignalName.BodyEntered, _player);
            if (!_bank.HasCharge || _bank.IsFullyCharged) { FailMechanicsSmoke("The bank started full instead of beginning its timed charge."); }
            return;
        }
        if (_mechanicsSmokeTick == 30)
        {
            _bank.Interact(_player);
            if (!_released || _releaseSpeed >= 23.9f || _bank.IsFullyCharged) { FailMechanicsSmoke("A release below half charge incorrectly provided enough speed for the vault."); return; }
            _bank.ResetBank();
            _released = false;
            _releaseSpeed = 0.0f;
            _bank.GetNode<Area3D>("CaptureArea").EmitSignal(Area3D.SignalName.BodyEntered, _player);
            return;
        }
        if (_mechanicsSmokeTick == 390)
        {
            _bank.Interact(_player);
            if (!_released || _releaseSpeed < 23.9f || _releaseSpeed >= 31.9f || _bank.IsFullyCharged) { FailMechanicsSmoke("A half charge did not provide a usable proportional non-full launch."); return; }
            _bank.ResetBank();
            _released = false;
            _releaseSpeed = 0.0f;
            _bank.GetNode<Area3D>("CaptureArea").EmitSignal(Area3D.SignalName.BodyEntered, _player);
            return;
        }
        if (_mechanicsSmokeTick == 1140)
        {
            if (!_bank.IsFullyCharged || !_capturedFullCharge) { FailMechanicsSmoke("The eight top charge segments did not finish after the timed wait."); return; }
            _bank.Interact(_player);
            if (!_released || _releaseSpeed < 31.9f || !CompletedAdvancementIds.Contains("full-account")) { FailMechanicsSmoke("The full-power release did not reach maximum speed or award Full Account."); return; }
            foreach (RouteCheckpoint3D stage in _routeStages) { stage.Press(_player); }
            foreach (FlightGate3D gate in _flightGates)
            {
                _player.ResetTo(new Transform3D(Basis.Identity, gate.GlobalPosition));
                gate.EmitSignal(Area3D.SignalName.BodyEntered, _player);
            }
            _landedHigh = true;
            _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
            if (!IsComplete && !IsExitTraversalPending) { FailMechanicsSmoke("The complete timed-charge route did not open the exit."); return; }
            GD.Print("ROOM23_MECHANICS_PASS: sub-half charge fell short, half charge produced a usable proportional launch, and the twelve-second full charge uniquely awarded Full Account.");
            GetTree().Quit(0);
        }
    }

    private void RunShellSmokeTick()
    {
        _shellSmokeTick++;
        if (_shellSmokeTick == 1)
        {
            _player.ResetTo(new Transform3D(Basis.Identity, GetNode<Area3D>("RoomShell/HazardTrigger").GlobalPosition));
            return;
        }
        if (_shellSmokeTick < 12) { return; }
        if (_player.GlobalPosition.DistanceTo(_spawnTransform.Origin) > 0.15f)
        {
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 23 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }
        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 23 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string concrete = "res://assets/textures/industrial_concrete.png";
        const string copper = "res://assets/textures/copper_rivets.svg";
        Color rail = new("414850");
        RoomGeometry.AddClosedRoomShell(this, "RoomShell", new Vector3(0.0f, 0.0f, -12.0f), new Vector2(30.0f, 136.0f), -3.0f, 36.0f, concrete, new Color("5e6772"), new Color("262d35"), body =>
        {
            if (body is PlayerBall) { RestartRoom(); }
        });

        const float deckWidth = 29.5f;
        const float rampWidth = 10.0f;
        RoomGeometry.AddBox(this, "SafeStart", new Vector3(deckWidth, 0.5f, 16.0f), new Vector3(0.0f, 14.0f, 48.0f), Vector3.Zero, metal, new Color("a5adb7"), 0.42f, 0.64f);
        AddSlope("SteeringDescentOne", -5.0f, rampWidth, 40.0f, 14.25f, 22.0f, 9.0f, copper, new Color("967a69"));
        RoomGeometry.AddBox(this, "FirstSwitchDeck", new Vector3(deckWidth, 0.5f, 12.0f), new Vector3(0.0f, 8.75f, 16.0f), Vector3.Zero, metal, new Color("929da6"), 0.42f, 0.64f);
        AddSlope("SteeringDescentTwo", 5.0f, rampWidth, 10.0f, 9.0f, -8.0f, 4.0f, copper, new Color("8b7165"));
        RoomGeometry.AddBox(this, "BankDeck", new Vector3(deckWidth, 0.5f, 23.0f), new Vector3(0.0f, 3.75f, -19.5f), Vector3.Zero, metal, new Color("89939c"), 0.42f, 0.64f);
        RoomGeometry.AddBox(this, "HighCatch", new Vector3(22.0f, 0.5f, 22.0f), new Vector3(0.0f, 9.75f, -69.0f), Vector3.Zero, metal, new Color("aeb5bc"), 0.42f, 0.64f);

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(this, $"StartRail{side}", new Vector3(0.4f, 1.5f, 16.0f), new Vector3(side * 14.56f, 14.85f, 48.0f), Vector3.Zero, metal, rail, 0.42f, 0.64f);
            RoomGeometry.AddBox(this, $"FirstDeckRail{side}", new Vector3(0.4f, 1.5f, 12.0f), new Vector3(side * 14.56f, 9.6f, 16.0f), Vector3.Zero, metal, rail, 0.42f, 0.64f);
            RoomGeometry.AddBox(this, $"BankDeckRail{side}", new Vector3(0.4f, 1.5f, 23.0f), new Vector3(side * 14.56f, 4.6f, -19.5f), Vector3.Zero, metal, rail, 0.42f, 0.64f);
            RoomGeometry.AddBox(this, $"CatchRail{side}", new Vector3(0.4f, 1.5f, 22.0f), new Vector3(side * 11.18f, 10.6f, -69.0f), Vector3.Zero, metal, rail, 0.42f, 0.64f);
        }
        AddSlopeRails("One", -5.0f, rampWidth, 40.0f, 14.25f, 22.0f, 9.0f, metal, rail);
        AddSlopeRails("Two", 5.0f, rampWidth, 10.0f, 9.0f, -8.0f, 4.0f, metal, rail);

        AddRouteStage("DescentStageOne", 0, new Vector3(-5.0f, 9.06f, 19.0f));
        AddRouteStage("DescentStageThree", 1, new Vector3(-5.0f, 4.06f, -13.0f));

        _bank = new MomentumBank3D
        {
            Name = "MomentumBank",
            Position = new Vector3(0.0f, 4.0f, -24.0f),
            ReleaseDirection = new Vector3(0.0f, 0.72f, -1.0f),
            ReleaseMultiplier = 1.0f,
            MinimumReleaseSpeed = 16.0f,
            ChargeByTime = true,
            ChargeDurationSeconds = 12.0f,
            ChargeCapacity = 32.0f,
            OpenApproach = true,
            EnableAudio = !_runSolutionSmoke,
        };
        AddChild(_bank);

        Area3D landing = new() { Name = "HighLanding", Position = new Vector3(0.0f, 10.5f, -69.0f), CollisionLayer = 0, CollisionMask = 1, Monitoring = true };
        landing.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(20.0f, 3.0f, 20.0f) } });
        landing.BodyEntered += body => { if (body == _player && _released && _nextFlightGate == _flightGates.Count) { _landedHigh = true; } };
        AddChild(landing);

        AddFlightGate("BankFlightGateNear", 0, new Vector3(0.0f, 13.7f, -41.0f), 2.4f);
        AddFlightGate("BankFlightGateFar", 1, new Vector3(0.0f, 14.2f, -57.5f), 5.4f);
        SurfaceDetail.AddOverlay(this, "CatchWear", new Vector3(1.5f, 10.015f, -69.0f), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(9.0f)), new Vector2(5.0f, 3.0f), "res://assets/textures/overlays/scratches.svg", new Color("d8dce2"), 0.34f);
    }

    private void AddSlope(string name, float x, float width, float backZ, float backY, float frontZ, float frontY, string texture, Color tint)
    {
        const float thickness = 0.62f;
        float run = backZ - frontZ;
        float rise = frontY - backY;
        float angle = Mathf.Atan2(rise, run);
        float length = Mathf.Sqrt((run * run) + (rise * rise));
        Vector3 up = new(0.0f, Mathf.Cos(angle), Mathf.Sin(angle));
        Vector3 topCenter = new(x, (backY + frontY) * 0.5f, (backZ + frontZ) * 0.5f);
        RoomGeometry.AddBox(this, name, new Vector3(width, thickness, length), topCenter - (up * thickness * 0.5f), new Vector3(angle, 0.0f, 0.0f), texture, tint, 0.4f, 0.62f);
    }

    private void AddSlopeRails(string suffix, float x, float width, float backZ, float backY, float frontZ, float frontY, string texture, Color tint)
    {
        float run = backZ - frontZ;
        float rise = frontY - backY;
        float angle = Mathf.Atan2(rise, run);
        float length = Mathf.Sqrt((run * run) + (rise * rise));
        Vector3 up = new(0.0f, Mathf.Cos(angle), Mathf.Sin(angle));
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            Vector3 topCenter = new(x + (side * ((width * 0.5f) + 0.2f)), ((backY + frontY) * 0.5f) + 0.78f, (backZ + frontZ) * 0.5f);
            RoomGeometry.AddBox(this, $"SlopeRail{suffix}{side}", new Vector3(0.4f, 1.55f, length), topCenter - (up * 0.18f), new Vector3(angle, 0.0f, 0.0f), texture, tint, 0.42f, 0.64f);
        }
    }

    private void AddRouteStage(string name, int index, Vector3 position)
    {
        RouteCheckpoint3D stage = new()
        {
            Name = name,
            Position = position,
            CheckpointIndex = index,
            TriggerSize = new Vector3(8.5f, 3.0f, 3.5f),
            FrameTint = index switch { 0 => new Color("8e765c"), 1 => new Color("ad8c64"), _ => new Color("cda56d") },
            FlatFloorMarker = true,
        };
        stage.Entered += (entered, player) =>
        {
            if (player != _player) { return; }
            if (entered.CheckpointIndex != _nextRouteStage) { entered.FlashDenied(); return; }
            entered.Activate();
            _nextRouteStage++;
            if (_runSolutionSmoke) { GD.Print($"ROOM23_ROUTE_TRACE: stage={_nextRouteStage}/{RequiredRouteStages}, tick={_solutionTick}, position={player.GlobalPosition}."); }
        };
        AddChild(stage);
        _routeStages.Add(stage);
    }

    private void AddFlightGate(string name, int index, Vector3 position, float radius)
    {
        FlightGate3D gate = new()
        {
            Name = name,
            Position = position,
            Radius = radius,
            FrameTint = index == 0 ? new Color("9b754e") : new Color("6e5e4e"),
            EnableAudio = !_runSolutionSmoke,
            MinimumExitSpeed = 24.0f,
            SpeedGain = 0.6f,
            SpeedMultiplier = 1.02f,
            MaximumExitSpeed = 27.0f,
            AxialBoostOnly = true,
            MaximumDownwardExitSpeed = 3.0f,
        };
        gate.Passed += player =>
        {
            if (player != _player || index != _nextFlightGate) { return; }
            _nextFlightGate++;
            if (_runSolutionSmoke) { GD.Print($"ROOM23_GATE_TRACE: gate={_nextFlightGate}/{_flightGates.Count}, tick={_solutionTick}, position={player.GlobalPosition}."); }
        };
        AddChild(gate);
        _flightGates.Add(gate);
    }

    private void BuildGoal()
    {
        Vector3 goalPosition = new(0.0f, 10.85f, -77.0f);
        _goal = new Area3D { Name = "GoalCup", Position = goalPosition, CollisionLayer = 0, CollisionMask = 1, Monitoring = true };
        _goal.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 2.0f, Height = 3.0f } });
        _goal.BodyEntered += body =>
        {
            if (body is PlayerBall && _released && _landedHigh && _nextRouteStage == RequiredRouteStages && _nextFlightGate == _flightGates.Count)
            {
                CompleteRoom();
            }
        };
        AddChild(_goal);
        RoomGeometry.AddGoalExitDoor(this, goalPosition);
    }

    private void FailMechanicsSmoke(string message)
    {
        GD.PushError($"ROOM23_MECHANICS_FAIL: {message}");
        GetTree().Quit(1);
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM23_SOLUTION_FAIL: {message}");
        FinishSolutionSmoke(1);
    }

    private async void FinishSolutionSmoke(int code)
    {
        if (_solutionSmokeFinishing) { return; }
        _solutionSmokeFinishing = true;
        if (_player is not null) { _player.SimulatedMoveInput = null; }
        foreach (FlightGate3D gate in _flightGates) { gate.ResetGate(); }
        _solutionTrace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(code);
    }
}
