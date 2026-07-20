using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Physics;
using Velocitex.Core.Rooms;
using Velocitex.Core.Settings;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Interaction;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Visual;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room22Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_22_solution.tres";
    private const byte InteractAction = 1;
    private const int RequiredSolutionRuns = 1;
    private const int MaximumSolutionTicksPerRun = 1750;
    private const int RequiredClimbStages = 3;

    private readonly List<RouteCheckpoint3D> _climbStages = new();
    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private MechanicalLever _ratchetReleaseLever = null!;
    private StaticBody3D _releaseGate = null!;
    private Area3D _goal = null!;
    private Transform3D _spawnTransform;
    private Vector3 _releaseGateClosedPosition;
    private SolutionTrace? _solutionTrace;
    private Tween? _gateTween;
    private bool _runSolutionSmoke;
    private bool _runShellSmoke;
    private bool _runPreview;
    private bool _runMechanicsSmoke;
    private bool _solutionSmokeFinishing;
    private bool _touchedRatchet;
    private bool _ratchetReleased;
    private bool _showInteractionPrompts;
    private bool _highContrastPrompts;
    private int _nextClimbStage;
    private int _solutionRun;
    private int _solutionTick;
    private int _shellSmokeTick;
    private int _previewFrames;
    private int _mechanicsSmokeTick;
    private float _maximumRise;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] args = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(args, value => value == "--room22-solution-smoke");
        _runShellSmoke = Array.Exists(args, value => value == "--room-shell-smoke");
        _runPreview = Array.Exists(args, value => value == "--room22-preview");
        _runMechanicsSmoke = Array.Exists(args, value => value == "--room22-mechanics-smoke");
        BuildRoom();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room22_a", new Vector3(11.0f, 12.5f, 48.0f), new Vector3(-4.0f, 10.0f, 2.0f), 58.0f),
            new("room22_b", new Vector3(-11.0f, 28.0f, -20.0f), new Vector3(3.0f, 18.0f, -58.0f), 58.0f),
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
        _ratchetReleaseLever.SetKeyLabel(interactKey == Key.None ? "E" : interactKey.ToString());
        if (_runPreview) { _cameraRig.SetInputEnabled(false); }
        if (_runSolutionSmoke)
        {
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            if (_solutionTrace is null ||
                _solutionTrace.RoomId != RoomId ||
                _solutionTrace.ActionFlags.Length != _solutionTrace.MoveInputs.Count ||
                !_solutionTrace.ActionFlags.Contains(InteractAction) ||
                _solutionTrace.MoveInputs.Count < 9)
            {
                FailSolutionSmoke("The Room 22 SolutionTrace must climb all three offset ratchets and release the top gate.");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_runPreview && ++_previewFrames >= 30)
        {
            string path = ProjectSettings.GlobalizePath("user://room22-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"ROOM22_PREVIEW_CAPTURE: {path}");
            GetTree().Quit(0);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        ProcessClimbStageContact();
        if (_runShellSmoke) { RunShellSmokeTick(); return; }
        if (_runMechanicsSmoke) { RunMechanicsSmokeTick(); return; }
        if (_runSolutionSmoke) { RunSolutionTick(); return; }
        TrackLesson();
        bool canInteract = _ratchetReleaseLever.CanInteract(_player);
        bool focused = canInteract && _cameraRig.IsLookingAt(_ratchetReleaseLever.GlobalPosition + (Vector3.Up * 1.75f));
        _ratchetReleaseLever.SetFocused(focused && _showInteractionPrompts, _highContrastPrompts);
        if (focused && Godot.Input.IsActionJustPressed(InputDefaults.Interact)) { _ratchetReleaseLever.Interact(_player); }
        if (_player.GlobalPosition.Y < -7.0f) { RestartRoom(); }
    }

    public override void RestartRoom()
    {
        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _player.ResetTo(_spawnTransform);
        _ratchetReleaseLever.ResetLever();
        ResetLesson();
    }

    private void TrackLesson()
    {
        if (_player.GroundSurfaceKind == SurfaceKind.OneWayGrip)
        {
            _touchedRatchet = true;
            _maximumRise = Mathf.Max(_maximumRise, _player.GlobalPosition.Y - _spawnTransform.Origin.Y);
        }
    }

    private void ProcessClimbStageContact()
    {
        if (_nextClimbStage < 0 || _nextClimbStage >= _climbStages.Count)
        {
            return;
        }

        RouteCheckpoint3D stage = _climbStages[_nextClimbStage];
        Vector3 localPlayer = stage.ToLocal(_player.GlobalPosition);
        float halfWidth = (stage.TriggerSize.X * 0.5f) + 0.6f;
        float halfDepth = (stage.TriggerSize.Z * 0.5f) + 0.6f;
        if (Mathf.Abs(localPlayer.X) <= halfWidth &&
            Mathf.Abs(localPlayer.Z) <= halfDepth &&
            localPlayer.Y >= -1.1f &&
            localPlayer.Y <= 0.3f)
        {
            stage.Press(_player);
        }
    }

    private void RunSolutionTick()
    {
        if (_solutionTrace is null || _solutionSmokeFinishing) { return; }
        if (IsComplete)
        {
            if (!_touchedRatchet || _maximumRise < 16.0f || !_ratchetReleased || _nextClimbStage != RequiredClimbStages)
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} bypassed the switchback climb: stages={_nextClimbStage}/{RequiredClimbStages}, touched={_touchedRatchet}, rise={_maximumRise:F2}, released={_ratchetReleased}.");
                return;
            }
            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print($"ROOM22_SOLUTION_PASS: SolutionTrace climbed three offset one-way ramps, rose {_maximumRise:F2} m and released the physical top gate for {_solutionRun} consecutive completions.");
                FinishSolutionSmoke(0);
                return;
            }
            ClearCompletionState();
            _player.ResetTo(_spawnTransform);
            _ratchetReleaseLever.ResetLever();
            _solutionTick = 0;
            ResetLesson();
            return;
        }
        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} timed out at {_player.GlobalPosition}; stages={_nextClimbStage}/{RequiredClimbStages}, touched={_touchedRatchet}, rise={_maximumRise:F2}, released={_ratchetReleased}.");
            return;
        }
        (Vector2 moveInput, byte actionFlags) = ResolveTraceStep(_solutionTick - 1);
        _player.SimulatedMoveInput = moveInput;
        if ((actionFlags & InteractAction) != 0)
        {
            _ratchetReleaseLever.Interact(_player);
        }
        TrackLesson();
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

    private void ResetLesson()
    {
        _touchedRatchet = false;
        _ratchetReleased = false;
        _maximumRise = 0.0f;
        _nextClimbStage = 0;
        foreach (RouteCheckpoint3D stage in _climbStages) { stage.ResetCheckpoint(); }
        _gateTween?.Kill();
        _gateTween = null;
        if (IsInstanceValid(_releaseGate)) { _releaseGate.Position = _releaseGateClosedPosition; }
    }

    private void RunMechanicsSmokeTick()
    {
        _mechanicsSmokeTick++;
        if (_mechanicsSmokeTick == 1)
        {
            _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
            if (IsComplete || IsExitTraversalPending) { FailMechanicsSmoke("Direct goal entry completed the room."); return; }
            _player.ResetTo(new Transform3D(Basis.Identity, _ratchetReleaseLever.GlobalPosition + new Vector3(0.0f, 0.6f, 2.0f)));
            _ratchetReleaseLever.Interact(_player);
            if (!_ratchetReleased) { FailMechanicsSmoke("The nearby lever did not start the gate release."); return; }
            _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
            if (IsComplete || IsExitTraversalPending) { FailMechanicsSmoke("The lever alone bypassed the three ramp stages."); return; }
            _touchedRatchet = true;
            _maximumRise = 17.0f;
            foreach (RouteCheckpoint3D stage in _climbStages) { stage.Press(_player); }
            _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
            if (!IsComplete && !IsExitTraversalPending) { FailMechanicsSmoke("The complete climb and lever state did not open the exit."); }
            return;
        }
        if (_mechanicsSmokeTick >= 10)
        {
            if (_releaseGate.Position.Y <= _releaseGateClosedPosition.Y + 0.1f) { FailMechanicsSmoke("The lever did not begin raising the physical top gate."); return; }
            GD.Print("ROOM22_MECHANICS_PASS: lever-only entry failed; all three ordered ramps and the raised physical top gate were required.");
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
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 22 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }
        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 22 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string concrete = "res://assets/textures/industrial_concrete.png";
        const string teeth = "res://assets/textures/one_way_teeth.svg";
        Color rail = new("4c493d");
        RoomGeometry.AddClosedRoomShell(this, "RoomShell", new Vector3(0.0f, 0.0f, -15.0f), new Vector2(26.0f, 142.0f), -3.0f, 38.0f, concrete, new Color("756f5c"), new Color("39362b"), body =>
        {
            if (body is PlayerBall) { RestartRoom(); }
        });

        SurfaceProfile profile = GD.Load<SurfaceProfile>("res://resources/surfaces/one_way_grip.tres") ?? new SurfaceProfile { Kind = SurfaceKind.OneWayGrip, Friction = 0.96f, GripDirection = Vector3.Forward };
        const float deckWidth = 25.5f;
        const float rampWidth = 9.0f;
        RoomGeometry.AddBox(this, "SafeStart", new Vector3(deckWidth, 0.5f, 16.0f), new Vector3(0.0f, 4.0f, 48.0f), Vector3.Zero, metal, new Color("a9a486"), 0.42f, 0.64f);
        AddRatchetRamp("RatchetClimbOne", -7.0f, rampWidth, 40.0f, 4.25f, 20.0f, 10.0f, teeth, profile);
        RoomGeometry.AddBox(this, "FirstSwitchDeck", new Vector3(deckWidth, 0.5f, 20.0f), new Vector3(0.0f, 9.75f, 10.0f), Vector3.Zero, metal, new Color("9e997e"), 0.42f, 0.64f);
        AddRatchetRamp("RatchetClimbTwo", 0.0f, rampWidth, 0.0f, 10.0f, -20.0f, 16.0f, teeth, profile);
        RoomGeometry.AddBox(this, "SecondSwitchDeck", new Vector3(deckWidth, 0.5f, 20.0f), new Vector3(0.0f, 15.75f, -30.0f), Vector3.Zero, metal, new Color("a4a086"), 0.42f, 0.64f);
        AddRatchetRamp("RatchetClimbThree", 7.0f, rampWidth, -40.0f, 16.0f, -60.0f, 22.0f, teeth, profile);
        RoomGeometry.AddBox(this, "HighDeck", new Vector3(deckWidth, 0.5f, 26.0f), new Vector3(0.0f, 21.75f, -73.0f), Vector3.Zero, metal, new Color("aba88f"), 0.42f, 0.64f);

        AddClimbStage("RatchetStageOne", 0, new Vector3(-7.0f, 11.86f, 17.0f));
        AddClimbStage("RatchetStageTwo", 1, new Vector3(0.0f, 17.86f, -23.0f));
        AddClimbStage("RatchetStageThree", 2, new Vector3(7.0f, 23.86f, -63.0f));

        _ratchetReleaseLever = new MechanicalLever { Name = "RatchetReleaseLever", Position = new Vector3(-6.0f, 22.0f, -70.0f), ActivationRadius = 5.0f };
        _ratchetReleaseLever.Activated += OpenReleaseGate;
        AddChild(_ratchetReleaseLever);

        _releaseGate = RoomGeometry.AddBox(this, "TopReleaseGate", new Vector3(25.0f, 5.0f, 0.65f), new Vector3(0.0f, 24.5f, -80.0f), Vector3.Zero, metal, new Color("74653f"), 0.45f, 0.58f);
        _releaseGateClosedPosition = _releaseGate.Position;

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(this, $"StartRail{side}", new Vector3(0.35f, 1.35f, 16.0f), new Vector3(side * 12.59f, 4.7f, 48.0f), Vector3.Zero, metal, rail, 0.42f, 0.64f);
            RoomGeometry.AddBox(this, $"FirstDeckRail{side}", new Vector3(0.35f, 1.35f, 20.0f), new Vector3(side * 12.59f, 10.45f, 10.0f), Vector3.Zero, metal, rail, 0.42f, 0.64f);
            RoomGeometry.AddBox(this, $"SecondDeckRail{side}", new Vector3(0.35f, 1.35f, 20.0f), new Vector3(side * 12.59f, 16.45f, -30.0f), Vector3.Zero, metal, rail, 0.42f, 0.64f);
            RoomGeometry.AddBox(this, $"HighDeckRail{side}", new Vector3(0.35f, 1.35f, 26.0f), new Vector3(side * 12.59f, 22.45f, -73.0f), Vector3.Zero, metal, rail, 0.42f, 0.64f);
        }
        AddRampRails("One", -7.0f, rampWidth, 40.0f, 4.25f, 20.0f, 10.0f, metal, rail);
        AddRampRails("Two", 0.0f, rampWidth, 0.0f, 10.0f, -20.0f, 16.0f, metal, rail);
        AddRampRails("Three", 7.0f, rampWidth, -40.0f, 16.0f, -60.0f, 22.0f, metal, rail);
    }

    private void AddRatchetRamp(string name, float x, float width, float backZ, float backY, float frontZ, float frontY, string texture, SurfaceProfile profile)
    {
        const float thickness = 0.68f;
        float run = backZ - frontZ;
        float rise = frontY - backY;
        float angle = Mathf.Atan2(rise, run);
        float length = Mathf.Sqrt((run * run) + (rise * rise));
        Vector3 up = new(0.0f, Mathf.Cos(angle), Mathf.Sin(angle));
        Vector3 topCenter = new(x, (backY + frontY) * 0.5f, (backZ + frontZ) * 0.5f);
        RoomGeometry.AddBox(this, name, new Vector3(width, thickness, length), topCenter - (up * thickness * 0.5f), new Vector3(angle, 0.0f, 0.0f), texture, new Color("d1cb8b"), 0.34f, 0.68f, surfaceProfile: profile);
    }

    private void AddRampRails(string suffix, float x, float width, float backZ, float backY, float frontZ, float frontY, string texture, Color tint)
    {
        float run = backZ - frontZ;
        float rise = frontY - backY;
        float angle = Mathf.Atan2(rise, run);
        float length = Mathf.Sqrt((run * run) + (rise * rise));
        Vector3 up = new(0.0f, Mathf.Cos(angle), Mathf.Sin(angle));
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            Vector3 topCenter = new(x + (side * ((width * 0.5f) + 0.18f)), ((backY + frontY) * 0.5f) + 0.72f, (backZ + frontZ) * 0.5f);
            RoomGeometry.AddBox(this, $"RatchetRail{suffix}{side}", new Vector3(0.36f, 1.45f, length), topCenter - (up * 0.18f), new Vector3(angle, 0.0f, 0.0f), texture, tint, 0.42f, 0.64f);
        }
    }

    private void AddClimbStage(string name, int index, Vector3 position)
    {
        RouteCheckpoint3D stage = new()
        {
            Name = name,
            Position = position,
            CheckpointIndex = index,
            TriggerSize = new Vector3(8.5f, 3.0f, 4.0f),
            FrameTint = RoomGeometry.SequenceButtonFrameTint,
            FlatFloorMarker = true,
        };
        stage.Entered += (entered, player) =>
        {
            if (player != _player) { return; }
            if (entered.CheckpointIndex != _nextClimbStage) { entered.FlashDenied(); return; }
            entered.Activate();
            _nextClimbStage++;
            if (_runSolutionSmoke)
            {
                GD.Print($"ROOM22_STAGE_TRACE: stage={_nextClimbStage}/{RequiredClimbStages}, tick={_solutionTick}, position={player.GlobalPosition}.");
            }
        };
        AddChild(stage);
        MeshInstance3D inset = stage.GetNode<MeshInstance3D>("InsetPlate");
        RoomGeometry.AddSequencePips(inset, index + 1);
        _climbStages.Add(stage);
    }

    private void OpenReleaseGate()
    {
        if (_ratchetReleased) { return; }
        _ratchetReleased = true;
        _gateTween?.Kill();
        _gateTween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        _gateTween.TweenProperty(_releaseGate, "position:y", _releaseGateClosedPosition.Y + 7.0f, 0.65f);
    }

    private void BuildGoal()
    {
        Vector3 goalPosition = new(0.0f, 22.85f, -85.0f);
        _goal = new Area3D { Name = "GoalCup", Position = goalPosition, CollisionLayer = 0, CollisionMask = 1, Monitoring = true };
        _goal.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 2.0f, Height = 3.0f } });
        _goal.BodyEntered += body =>
        {
            if (body is PlayerBall && _touchedRatchet && _maximumRise >= 16.0f && _ratchetReleased && _nextClimbStage == RequiredClimbStages) { CompleteRoom(); }
        };
        AddChild(_goal);
        RoomGeometry.AddGoalExitDoor(this, goalPosition);
    }

    private void FailMechanicsSmoke(string message)
    {
        GD.PushError($"ROOM22_MECHANICS_FAIL: {message}");
        GetTree().Quit(1);
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM22_SOLUTION_FAIL: {message}");
        FinishSolutionSmoke(1);
    }

    private async void FinishSolutionSmoke(int code)
    {
        if (_solutionSmokeFinishing) { return; }
        _solutionSmokeFinishing = true;
        if (_player is not null) { _player.SimulatedMoveInput = null; }
        _solutionTrace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(code);
    }
}
