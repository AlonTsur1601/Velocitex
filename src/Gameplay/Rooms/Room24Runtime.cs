using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Physics;
using Velocitex.Core.Rooms;
using Velocitex.Core.Settings;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Interaction;
using Velocitex.Gameplay.Physics;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Visual;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room24Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_24_solution.tres";
    private const byte InteractAction = 1;
    private const int RequiredRuns = 1;
    private const int MaxTicks = 1200;

    private PlayerBall _player = null!;
    private PlayerCameraRig _camera = null!;
    private BrittleBarrier3D _firstBarrier = null!;
    private BrittleBarrier3D _routeBarrier = null!;
    private BrittleBarrier3D _optionalBarrier = null!;
    private MechanicalLever _safetyLever = null!;
    private StaticBody3D _safetyGate = null!;
    private Area3D _goal = null!;
    private Transform3D _spawn;
    private Vector3 _safetyGateClosedPosition;
    private SolutionTrace? _trace;
    private Tween? _gateTween;
    private bool _smoke;
    private bool _shell;
    private bool _preview;
    private bool _mechanicsSmoke;
    private bool _finishing;
    private bool _firstBroken;
    private bool _routeBroken;
    private bool _optionalBroken;
    private bool _safetyReleased;
    private bool _showPrompts;
    private bool _highContrastPrompts;
    private int _run;
    private int _tick;
    private int _shellTick;
    private int _previewFrames;
    private int _mechanicsTick;
    private float _firstImpact;
    private float _routeImpact;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] args = OS.GetCmdlineUserArgs();
        _smoke = Array.Exists(args, value => value == "--room24-solution-smoke");
        _shell = Array.Exists(args, value => value == "--room-shell-smoke");
        _preview = Array.Exists(args, value => value == "--room24-preview");
        _mechanicsSmoke = Array.Exists(args, value => value == "--room24-mechanics-smoke");
        BuildRoom();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room24_a", new Vector3(12.0f, 13.0f, 44.0f), new Vector3(0.0f, 6.0f, -10.0f), 58.0f),
            new("room24_b", new Vector3(-12.0f, 13.0f, -9.0f), new Vector3(4.0f, 6.0f, -42.0f), 58.0f),
        });
        _player = GetNode<PlayerBall>("Player");
        _camera = GetNode<PlayerCameraRig>("CameraRig");
        _spawn = _player.GlobalTransform;
        _camera.Follow(_player);
        _player.MovementBasis = _camera.MovementBasis;
        GameSettingsData settings = SettingsStore.Load();
        _showPrompts = settings.InteractionPrompts;
        _highContrastPrompts = settings.HighContrastPrompts;
        Key key = InputDefaults.GetPrimaryKey(InputDefaults.Interact);
        _safetyLever.SetKeyLabel(key == Key.None ? "E" : key.ToString());
        if (_preview) { _camera.SetInputEnabled(false); }

        _firstBarrier.Broken += (player, speed) => { if (player == _player) { _firstBroken = true; _firstImpact = speed; } };
        _routeBarrier.Broken += (player, speed) => { if (player == _player) { _routeBroken = true; _routeImpact = speed; } };
        _optionalBarrier.Broken += (player, speed) => { if (player == _player) { _optionalBroken = true; } };
        if (_smoke)
        {
            _showPrompts = false;
            _trace = GD.Load<SolutionTrace>(TracePath);
            if (_trace is null || _trace.RoomId != RoomId || _trace.ActionFlags.Length != _trace.MoveInputs.Count || !_trace.ActionFlags.Contains(InteractAction) || _trace.MoveInputs.Count < 5)
            {
                Fail("The Room 24 SolutionTrace must shatter both required barriers and release the safety gate.");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_preview && ++_previewFrames >= 30)
        {
            string path = ProjectSettings.GlobalizePath("user://room24-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"ROOM24_PREVIEW_CAPTURE: {path}");
            GetTree().Quit(0);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_shell) { RunShellSmoke(); return; }
        if (_mechanicsSmoke) { RunMechanicsSmoke(); return; }
        if (_smoke) { RunSolution(); return; }
        bool canInteract = _safetyLever.CanInteract(_player);
        bool focused = canInteract && _camera.IsLookingAt(_safetyLever.GlobalPosition + (Vector3.Up * 1.75f));
        _safetyLever.SetFocused(focused && _showPrompts, _highContrastPrompts);
        if (focused && Godot.Input.IsActionJustPressed(InputDefaults.Interact)) { _safetyLever.Interact(_player); }
        if (_player.GlobalPosition.Y < -7.0f) { RestartRoom(); }
    }

    public override void RestartRoom()
    {
        if (_smoke && _tick > 0 && !_finishing)
        {
            Fail($"Run {_run + 1} left the two-lane route; first={_firstBroken}, route={_routeBroken}, released={_safetyReleased}, impacts={_firstImpact:F2}/{_routeImpact:F2}.");
            return;
        }
        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _player.ResetTo(_spawn);
        ResetState();
    }

    private void RunSolution()
    {
        if (_trace is null || _finishing) { return; }
        if (IsComplete)
        {
            if (!_firstBroken || !_routeBroken || !_safetyReleased || _firstImpact < 14.0f || _routeImpact < 12.0f)
            {
                Fail($"Run {_run + 1} bypassed the brittle route: first={_firstBroken} at {_firstImpact:F2}, route={_routeBroken} at {_routeImpact:F2}, released={_safetyReleased}.");
                return;
            }
            _run++;
            if (_run >= RequiredRuns)
            {
                GD.Print($"ROOM24_SOLUTION_PASS: SolutionTrace shattered the entry barrier at {_firstImpact:F2} m/s, braked left to release the gate, crossed to the right accelerator lane and shattered the route barrier at {_routeImpact:F2} m/s.");
                Finish(0);
                return;
            }
            ClearCompletionState();
            _player.ResetTo(_spawn);
            _tick = 0;
            ResetState();
            return;
        }
        if (++_tick > MaxTicks)
        {
            Fail($"Run {_run + 1} timed out at {_player.GlobalPosition}; first={_firstBroken}, route={_routeBroken}, released={_safetyReleased}.");
            return;
        }
        (Vector2 moveInput, byte actionFlags) = Resolve(_tick - 1);
        _player.SimulatedMoveInput = moveInput;
        if ((actionFlags & InteractAction) != 0) { _safetyLever.Interact(_player); }
    }

    private (Vector2 MoveInput, byte ActionFlags) Resolve(int tick)
    {
        if (_trace is null) { return (Vector2.Zero, 0); }
        int remaining = tick;
        for (int index = 0; index < _trace.MoveInputs.Count; index++)
        {
            int duration = _trace.MoveDurationsTicks[index];
            if (remaining < duration) { return (_trace.MoveInputs[index], _trace.ActionFlags[index]); }
            remaining -= duration;
        }
        return _trace.HoldLastInput ? (_trace.MoveInputs[^1], _trace.ActionFlags[^1]) : (Vector2.Zero, (byte)0);
    }

    private void ResetState()
    {
        _firstBarrier.ResetBarrier();
        _routeBarrier.ResetBarrier();
        _optionalBarrier.ResetBarrier();
        _safetyLever.ResetLever();
        _gateTween?.Kill();
        _gateTween = null;
        _safetyGate.Position = _safetyGateClosedPosition;
        _firstBroken = false;
        _routeBroken = false;
        _optionalBroken = false;
        _safetyReleased = false;
        _firstImpact = 0.0f;
        _routeImpact = 0.0f;
    }

    private void OpenSafetyGate()
    {
        if (_safetyReleased) { return; }
        _safetyReleased = true;
        _gateTween?.Kill();
        _gateTween = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        _gateTween.TweenProperty(_safetyGate, "position:y", _safetyGateClosedPosition.Y + 6.0f, 0.62f);
    }

    private void RunMechanicsSmoke()
    {
        _mechanicsTick++;
        if (_mechanicsTick == 1)
        {
            _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
            if (IsComplete || IsExitTraversalPending) { FailMechanics("Direct goal entry completed the room."); return; }
            _player.ResetTo(new Transform3D(Basis.Identity, _safetyLever.GlobalPosition + new Vector3(0.0f, 0.6f, 2.0f)));
            _safetyLever.Interact(_player);
            if (!_safetyReleased) { FailMechanics("The nearby level lever did not start raising the physical gate."); return; }
            _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
            if (IsComplete || IsExitTraversalPending) { FailMechanics("The lever alone bypassed both breakable barriers."); return; }
            _firstBroken = true;
            _routeBroken = true;
            _firstImpact = 15.0f;
            _routeImpact = 13.0f;
            _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
            if (!IsComplete && !IsExitTraversalPending) { FailMechanics("The complete two-barrier route did not open the exit."); }
            return;
        }
        if (_mechanicsTick >= 10)
        {
            if (_safetyGate.Position.Y <= _safetyGateClosedPosition.Y + 0.1f) { FailMechanics("The lever did not raise the physical right-lane gate."); return; }
            GD.Print("ROOM24_MECHANICS_PASS: lever-only entry failed; both speed barriers and the raised physical gate were required.");
            GetTree().Quit(0);
        }
    }

    private void RunShellSmoke()
    {
        _shellTick++;
        if (_shellTick == 1)
        {
            _player.ResetTo(new Transform3D(Basis.Identity, GetNode<Area3D>("RoomShell/HazardTrigger").GlobalPosition));
            return;
        }
        if (_shellTick < 12) { return; }
        if (_player.GlobalPosition.DistanceTo(_spawn.Origin) > 0.15f)
        {
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 24 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }
        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 24 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string concrete = "res://assets/textures/industrial_concrete.png";
        Color rail = new("35484b");
        RoomGeometry.AddClosedRoomShell(this, "RoomShell", new Vector3(0.0f, 0.0f, -7.0f), new Vector2(32.0f, 112.0f), -3.0f, 34.0f, concrete, new Color("587075"), new Color("22383b"), body =>
        {
            if (body is PlayerBall) { RestartRoom(); }
        });
        SurfaceProfile accelerator = GD.Load<SurfaceProfile>("res://resources/surfaces/accelerator.tres")!;
        SurfaceProfile returnAccelerator = (SurfaceProfile)accelerator.Duplicate();
        returnAccelerator.Acceleration = new Vector3(0.0f, 0.0f, -26.0f);
        SurfaceProfile sticky = GD.Load<SurfaceProfile>("res://resources/surfaces/sticky.tres")!;
        ShaderMaterial acceleratorBelt = (ShaderMaterial)GD.Load<ShaderMaterial>("res://resources/materials/accelerator_belt.tres")!.Duplicate();
        acceleratorBelt.SetShaderParameter("motion_scale", 1.0f);
        ShaderMaterial caramelMaterial = (ShaderMaterial)GD.Load<ShaderMaterial>("res://resources/materials/sticky_caramel.tres")!.Duplicate();

        RoomGeometry.AddBox(this, "SafeStart", new Vector3(31.5f, 0.5f, 18.5f), new Vector3(0.0f, 4.25f, 39.5f), Vector3.Zero, metal, new Color("a2b2b5"), 0.42f, 0.64f);
        RoomGeometry.AddBox(this, "EntryBreakRunway", new Vector3(10.0f, 0.55f, 31.25f), new Vector3(0.0f, 4.225f, 14.625f), Vector3.Zero, metal, Colors.White, 0.0f, 1.0f, surfaceProfile: accelerator, materialOverride: acceleratorBelt);
        RoomGeometry.AddBox(this, "LeverJunction", new Vector3(31.5f, 0.5f, 15.0f), new Vector3(0.0f, 4.25f, -8.5f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 0.72f, surfaceProfile: sticky, materialOverride: caramelMaterial);
        RoomGeometry.AddBox(this, "OptionalLeftLane", new Vector3(10.0f, 0.5f, 18.0f), new Vector3(-5.0f, 4.25f, -25.0f), Vector3.Zero, metal, new Color("829396"), 0.42f, 0.64f);
        RoomGeometry.AddBox(this, "RequiredRightLane", new Vector3(10.0f, 0.55f, 18.0f), new Vector3(5.0f, 4.225f, -25.0f), Vector3.Zero, metal, Colors.White, 0.0f, 1.0f, surfaceProfile: accelerator, materialOverride: acceleratorBelt);
        RoomGeometry.AddBox(this, "LeftReturnBoost", new Vector3(10.0f, 0.55f, 12.0f), new Vector3(-5.0f, 4.225f, -41.0f), new Vector3(0.0f, Mathf.Pi, 0.0f), metal, Colors.White, 0.0f, 1.0f, surfaceProfile: returnAccelerator, materialOverride: acceleratorBelt);
        RoomGeometry.AddBox(this, "ExitDeck", new Vector3(31.5f, 0.5f, 17.5f), new Vector3(0.0f, 4.25f, -42.75f), Vector3.Zero, metal, new Color("9caaad"), 0.42f, 0.64f);

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(this, $"OuterRail{side}", new Vector3(0.42f, 2.0f, 103.0f), new Vector3(side * 15.78f, 5.5f, -2.5f), Vector3.Zero, metal, rail, 0.42f, 0.65f);
            RoomGeometry.AddBox(this, $"EntryLaneRail{side}", new Vector3(0.42f, 2.2f, 31.25f), new Vector3(side * 5.25f, 5.65f, 14.625f), Vector3.Zero, metal, rail, 0.42f, 0.65f);
            RoomGeometry.AddBox(this, $"LeftLaneRail{side}", new Vector3(0.42f, 2.2f, 18.0f), new Vector3(-5.0f + (side * 5.2f), 5.65f, -25.0f), Vector3.Zero, metal, rail, 0.42f, 0.65f);
            RoomGeometry.AddBox(this, $"RightLaneRail{side}", new Vector3(0.42f, 2.2f, 18.0f), new Vector3(5.0f + (side * 5.2f), 5.65f, -25.0f), Vector3.Zero, metal, rail, 0.42f, 0.65f);
        }

        _firstBarrier = new BrittleBarrier3D { Name = "EntryBarrier", Position = new Vector3(0.0f, 7.25f, -1.0f), BarrierSize = new Vector2(9.5f, 5.5f), RequiredSpeed = 14.0f, EnableAudio = !_smoke };
        AddChild(_firstBarrier);
        _routeBarrier = new BrittleBarrier3D { Name = "RequiredRightBarrier", Position = new Vector3(5.0f, 7.25f, -34.0f), BarrierSize = new Vector2(9.5f, 5.5f), RequiredSpeed = 12.0f, EnableAudio = !_smoke };
        AddChild(_routeBarrier);
        _optionalBarrier = new BrittleBarrier3D { Name = "OptionalLeftBarrier", Position = new Vector3(-5.0f, 7.25f, -34.0f), BarrierSize = new Vector2(9.5f, 5.5f), RequiredSpeed = 18.0f, EnableAudio = !_smoke };
        AddChild(_optionalBarrier);

        _safetyLever = new MechanicalLever { Name = "SafetyReleaseLever", Position = new Vector3(-5.0f, 4.5f, -11.5f), ActivationRadius = 4.8f };
        _safetyLever.Activated += OpenSafetyGate;
        AddChild(_safetyLever);
        _safetyGate = RoomGeometry.AddBox(this, "RightLaneSafetyGate", new Vector3(10.0f, 5.0f, 0.65f), new Vector3(5.0f, 6.75f, -16.0f), Vector3.Zero, metal, new Color("75623d"), 0.45f, 0.58f);
        _safetyGateClosedPosition = _safetyGate.Position;
        RoomGeometry.AddBox(this, "LeftLaneLeverSideBlock", new Vector3(10.0f, 5.0f, 0.65f), new Vector3(-5.0f, 6.75f, -16.0f), Vector3.Zero, metal, new Color("4b5c5f"), 0.45f, 0.62f);

        SurfaceDetail.AddOverlay(this, "RunoutShards", new Vector3(3.0f, 4.515f, -39.5f), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(-12.0f)), new Vector2(5.0f, 3.0f), "res://assets/textures/overlays/cracks.svg", new Color("d7ece8"), 0.48f);
    }

    private void BuildGoal()
    {
        Vector3 position = new(5.0f, 5.35f, -49.0f);
        _goal = new Area3D { Name = "GoalCup", Position = position, CollisionLayer = 0, CollisionMask = 1, Monitoring = true };
        _goal.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 2.0f, Height = 3.0f } });
        _goal.BodyEntered += body =>
        {
            if (body is PlayerBall && _firstBroken && _routeBroken && _safetyReleased)
            {
                if (_optionalBroken) { MarkAdvancementCondition("sugar-breaker"); }
                CompleteRoom();
            }
        };
        AddChild(_goal);
        RoomGeometry.AddGoalExitDoor(this, position);
    }

    private void FailMechanics(string message)
    {
        GD.PushError($"ROOM24_MECHANICS_FAIL: {message}");
        GetTree().Quit(1);
    }

    private void Fail(string message)
    {
        GD.PushError($"ROOM24_SOLUTION_FAIL: {message}");
        Finish(1);
    }

    private async void Finish(int code)
    {
        if (_finishing) { return; }
        _finishing = true;
        if (_player is not null) { _player.SimulatedMoveInput = null; }
        _trace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(code);
    }
}
