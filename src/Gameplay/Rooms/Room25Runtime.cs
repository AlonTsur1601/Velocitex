using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Physics;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Physics;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Visual;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room25Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_25_solution.tres";
    private const int RequiredStages = 4;
    private const int RequiredRuns = 1;
    private const int MaxTicks = 1700;

    private readonly List<RouteCheckpoint3D> _stages = new();
    private PlayerBall _player = null!;
    private PlayerCameraRig _camera = null!;
    private BrittleBarrier3D _barrier = null!;
    private Area3D _goal = null!;
    private Transform3D _spawn;
    private SolutionTrace? _trace;
    private bool _solutionSmoke;
    private bool _shellSmoke;
    private bool _preview;
    private bool _mechanicsSmoke;
    private bool _finishing;
    private bool _touchedAccelerator;
    private bool _barrierBroken;
    private bool _touchedSticky;
    private bool _verifiedStickySlowdown;
    private bool _bounced;
    private bool _landedExit;
    private int _nextStage;
    private int _run;
    private int _tick;
    private int _shellTick;
    private int _previewFrames;
    private int _mechanicsTick;
    private float _maximumAcceleratorSpeed;
    private float _barrierImpact;
    private float _stickyEntrySpeed;
    private float _stickyMinimumSpeed;
    private float _bounceLaunchSpeed;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] args = OS.GetCmdlineUserArgs();
        _solutionSmoke = Array.Exists(args, value => value == "--room25-solution-smoke");
        _shellSmoke = Array.Exists(args, value => value == "--room-shell-smoke");
        _preview = Array.Exists(args, value => value == "--room25-preview");
        _mechanicsSmoke = Array.Exists(args, value => value == "--room25-mechanics-smoke");
        BuildRoom();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room25_a", new Vector3(12.0f, 15.0f, 51.0f), new Vector3(-1.0f, 5.0f, -12.0f), 59.0f),
            new("room25_b", new Vector3(-11.0f, 13.0f, -12.0f), new Vector3(4.0f, 5.0f, -58.0f), 58.0f),
        });
        _player = GetNode<PlayerBall>("Player");
        _camera = GetNode<PlayerCameraRig>("CameraRig");
        _spawn = _player.GlobalTransform;
        _camera.Follow(_player);
        _player.MovementBasis = _camera.MovementBasis;
        if (_preview) { _camera.SetInputEnabled(false); }
        _barrier.Broken += (player, speed) =>
        {
            if (player == _player) { _barrierBroken = true; _barrierImpact = speed; }
        };
        if (_solutionSmoke)
        {
            _trace = GD.Load<SolutionTrace>(TracePath);
            if (_trace is null || _trace.RoomId != RoomId || _trace.MoveInputs.Count < 7)
            {
                Fail("The Room 25 SolutionTrace must steer through all four surface relay stages.");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_preview && ++_previewFrames >= 30)
        {
            string path = ProjectSettings.GlobalizePath("user://room25-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"ROOM25_PREVIEW_CAPTURE: {path}");
            GetTree().Quit(0);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_shellSmoke) { RunShellSmoke(); return; }
        if (_mechanicsSmoke) { RunMechanicsSmoke(); return; }
        TrackSurfaceRelay();
        if (_solutionSmoke) { RunSolution(); return; }
        if (_player.GlobalPosition.Y < -7.0f) { RestartRoom(); }
    }

    public override void RestartRoom()
    {
        if (_solutionSmoke && _tick > 0 && !_finishing)
        {
            Fail($"Run {_run + 1} missed the relay landing at {_player.GlobalPosition}; stages={_nextStage}/{RequiredStages}, accelerator={_maximumAcceleratorSpeed:F2}, barrier={_barrierBroken}, sticky={_verifiedStickySlowdown}, bounce={_bounceLaunchSpeed:F2}.");
            return;
        }
        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _player.ResetTo(_spawn);
        ResetState();
    }

    private void TrackSurfaceRelay()
    {
        float planarSpeed = _player.LinearVelocity.Slide(Vector3.Up).Length();
        if (_player.GroundSurfaceKind == SurfaceKind.Accelerator)
        {
            _touchedAccelerator = true;
            _maximumAcceleratorSpeed = Mathf.Max(_maximumAcceleratorSpeed, planarSpeed);
        }
        bool sticky = _player.GroundSurfaceKind == SurfaceKind.Sticky;
        if (sticky && !_touchedSticky)
        {
            _touchedSticky = true;
            _stickyEntrySpeed = planarSpeed;
            _stickyMinimumSpeed = planarSpeed;
        }
        if (sticky)
        {
            _stickyMinimumSpeed = Mathf.Min(_stickyMinimumSpeed, planarSpeed);
            if (_stickyEntrySpeed >= 5.0f && _stickyMinimumSpeed <= _stickyEntrySpeed * 0.58f) { _verifiedStickySlowdown = true; }
        }
        if (_player.SuperElasticBounceCount > 0)
        {
            _bounced = true;
            _bounceLaunchSpeed = Mathf.Max(_bounceLaunchSpeed, _player.LastSuperElasticLaunchSpeed);
        }
        Vector3 position = _player.GlobalPosition;
        if (_bounced && _player.IsGrounded && position.Y >= 4.35f && position.Z <= -84.0f && position.Z >= -107.0f && position.X >= -5.5f && position.X <= 11.5f)
        {
            _landedExit = true;
        }
        Vector2 goalOffset = new(position.X - _goal.GlobalPosition.X, position.Z - _goal.GlobalPosition.Z);
        if (HasCompletedRelay() && goalOffset.Length() <= 2.0f && Mathf.Abs(position.Y - _goal.GlobalPosition.Y) <= 1.6f)
        {
            CompleteRoom();
        }
    }

    private bool HasCompletedRelay() =>
        _nextStage == RequiredStages &&
        _touchedAccelerator &&
        _maximumAcceleratorSpeed >= 16.0f &&
        _barrierBroken &&
        _barrierImpact >= 14.0f &&
        _touchedSticky &&
        _verifiedStickySlowdown &&
        _bounced &&
        _bounceLaunchSpeed >= 9.0f &&
        _landedExit;

    private void RunSolution()
    {
        if (_trace is null || _finishing) { return; }
        if (IsComplete)
        {
            if (!HasCompletedRelay())
            {
                Fail($"Run {_run + 1} bypassed the relay: stages={_nextStage}/{RequiredStages}, accelerator={_maximumAcceleratorSpeed:F2}, barrier={_barrierImpact:F2}, sticky={_stickyEntrySpeed:F2}->{_stickyMinimumSpeed:F2}, bounce={_bounceLaunchSpeed:F2}, landed={_landedExit}.");
                return;
            }
            _run++;
            if (_run >= RequiredRuns)
            {
                GD.Print($"ROOM25_SOLUTION_PASS: SolutionTrace steered four surface stages, accelerated to {_maximumAcceleratorSpeed:F2} m/s, shattered at {_barrierImpact:F2} m/s, slowed on sticky from {_stickyEntrySpeed:F2} to {_stickyMinimumSpeed:F2} m/s and bounced at {_bounceLaunchSpeed:F2} m/s onto the exit deck.");
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
            Fail($"Run {_run + 1} timed out at {_player.GlobalPosition}; stages={_nextStage}/{RequiredStages}, accelerator={_maximumAcceleratorSpeed:F2}, barrier={_barrierBroken}, sticky={_verifiedStickySlowdown}, bounce={_bounceLaunchSpeed:F2}, landed={_landedExit}.");
            return;
        }
        _player.SimulatedMoveInput = Resolve(_tick - 1);
    }

    private Vector2 Resolve(int tick)
    {
        if (_trace is null) { return Vector2.Zero; }
        int remaining = tick;
        for (int index = 0; index < _trace.MoveInputs.Count; index++)
        {
            int duration = _trace.MoveDurationsTicks[index];
            if (remaining < duration) { return _trace.MoveInputs[index]; }
            remaining -= duration;
        }
        return _trace.HoldLastInput ? _trace.MoveInputs[^1] : Vector2.Zero;
    }

    private void ResetState()
    {
        _barrier.ResetBarrier();
        foreach (RouteCheckpoint3D stage in _stages) { stage.ResetCheckpoint(); }
        _touchedAccelerator = false;
        _barrierBroken = false;
        _touchedSticky = false;
        _verifiedStickySlowdown = false;
        _bounced = false;
        _landedExit = false;
        _nextStage = 0;
        _maximumAcceleratorSpeed = 0.0f;
        _barrierImpact = 0.0f;
        _stickyEntrySpeed = 0.0f;
        _stickyMinimumSpeed = float.MaxValue;
        _bounceLaunchSpeed = 0.0f;
    }

    private void RunMechanicsSmoke()
    {
        _mechanicsTick++;
        if (_mechanicsTick != 1) { return; }
        _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
        if (IsComplete || IsExitTraversalPending) { FailMechanics("Direct goal entry completed the room."); return; }
        Area3D sensor = _barrier.GetNode<Area3D>("ImpactSensor");
        _player.LinearVelocity = Vector3.Forward * 10.0f;
        sensor.EmitSignal(Area3D.SignalName.BodyEntered, _player);
        if (_barrier.IsBroken) { FailMechanics("A no-boost 10 m/s impact broke the required barrier."); return; }
        _player.LinearVelocity = Vector3.Forward * 18.0f;
        sensor.EmitSignal(Area3D.SignalName.BodyEntered, _player);
        if (!_barrier.IsBroken) { FailMechanics("An accelerated 18 m/s impact did not break the required barrier."); return; }
        _nextStage = RequiredStages;
        _touchedAccelerator = true;
        _maximumAcceleratorSpeed = 18.0f;
        _touchedSticky = true;
        _verifiedStickySlowdown = true;
        _stickyEntrySpeed = 10.0f;
        _stickyMinimumSpeed = 4.0f;
        _bounced = true;
        _bounceLaunchSpeed = 10.0f;
        _landedExit = true;
        _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
        if (!IsComplete && !IsExitTraversalPending) { FailMechanics("The complete surface relay did not open the exit."); return; }
        GD.Print("ROOM25_MECHANICS_PASS: direct entry and a no-boost impact failed; all four steering stages, accelerator break, sticky slowdown, elastic bounce and landing were required.");
        GetTree().Quit(0);
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
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 25 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }
        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 25 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string concrete = "res://assets/textures/industrial_concrete.png";
        Color rail = new("443b49");
        RoomGeometry.AddClosedRoomShell(this, "RoomShell", new Vector3(0.0f, 0.0f, -27.0f), new Vector2(30.0f, 168.0f), -3.0f, 30.0f, concrete, new Color("685d71"), new Color("2c2533"), body =>
        {
            if (body is PlayerBall) { RestartRoom(); }
        });
        SurfaceProfile accelerator = GD.Load<SurfaceProfile>("res://resources/surfaces/accelerator.tres")!;
        SurfaceProfile sticky = GD.Load<SurfaceProfile>("res://resources/surfaces/sticky.tres")!;
        SurfaceProfile elastic = GD.Load<SurfaceProfile>("res://resources/surfaces/super_elastic.tres")!;
        ShaderMaterial acceleratorBelt = (ShaderMaterial)GD.Load<ShaderMaterial>("res://resources/materials/accelerator_belt.tres")!.Duplicate();
        acceleratorBelt.SetShaderParameter("motion_scale", 1.0f);

        RoomGeometry.AddBox(this, "SafeStart", new Vector3(29.5f, 0.5f, 16.75f), new Vector3(0.0f, 4.25f, 48.375f), Vector3.Zero, metal, new Color("afa6b5"), 0.42f, 0.64f);
        RoomGeometry.AddBox(this, "OffsetAccelerator", new Vector3(10.0f, 0.55f, 31.0f), new Vector3(-5.5f, 4.225f, 24.5f), Vector3.Zero, metal, Colors.White, 0.0f, 1.0f, surfaceProfile: accelerator, materialOverride: acceleratorBelt);
        RoomGeometry.AddBox(this, "StickySlalom", new Vector3(29.5f, 0.5f, 44.0f), new Vector3(0.0f, 4.25f, -13.0f), Vector3.Zero, "res://assets/textures/sugar_glaze.svg", new Color("d98b45"), 0.0f, 0.7f, surfaceProfile: sticky);
        RoomGeometry.AddBox(this, "LaunchAccelerator", new Vector3(13.0f, 0.55f, 15.0f), new Vector3(4.5f, 4.225f, -42.5f), Vector3.Zero, metal, Colors.White, 0.0f, 1.0f, surfaceProfile: accelerator, materialOverride: acceleratorBelt);
        RoomGeometry.AddBox(this, "ElasticDropPad", new Vector3(14.0f, 0.8f, 25.0f), new Vector3(4.0f, 0.4f, -63.5f), Vector3.Zero, "res://assets/textures/gelatin_cells.svg", new Color("d287d8"), 0.05f, 0.48f, surfaceProfile: elastic);
        RoomGeometry.AddBox(this, "HighExitDeck", new Vector3(17.0f, 0.5f, 23.0f), new Vector3(3.0f, 4.25f, -95.5f), Vector3.Zero, metal, new Color("aaa5af"), 0.42f, 0.64f);

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(this, $"OuterRail{side}", new Vector3(0.42f, 1.8f, 164.0f), new Vector3(side * 14.78f, 5.35f, -25.0f), Vector3.Zero, metal, rail, 0.42f, 0.65f);
            RoomGeometry.AddBox(this, $"AcceleratorRail{side}", new Vector3(0.42f, 2.0f, 31.0f), new Vector3(-5.5f + (side * 5.2f), 5.5f, 24.5f), Vector3.Zero, metal, rail, 0.42f, 0.65f);
            RoomGeometry.AddBox(this, $"LaunchRail{side}", new Vector3(0.42f, 2.0f, 15.0f), new Vector3(4.5f + (side * 6.7f), 5.5f, -42.5f), Vector3.Zero, metal, rail, 0.42f, 0.65f);
            RoomGeometry.AddBox(this, $"ElasticRail{side}", new Vector3(0.42f, 2.6f, 25.0f), new Vector3(4.0f + (side * 7.2f), 2.0f, -63.5f), Vector3.Zero, metal, rail, 0.42f, 0.65f);
            RoomGeometry.AddBox(this, $"ExitRail{side}", new Vector3(0.42f, 1.8f, 23.0f), new Vector3(3.0f + (side * 8.7f), 5.35f, -95.5f), Vector3.Zero, metal, rail, 0.42f, 0.65f);
        }

        _barrier = new BrittleBarrier3D { Name = "AcceleratorExamBarrier", Position = new Vector3(-5.5f, 7.25f, 9.0f), BarrierSize = new Vector2(9.5f, 5.5f), RequiredSpeed = 14.0f, EnableAudio = !_solutionSmoke };
        AddChild(_barrier);
        AddStage("AcceleratorStage", 0, new Vector3(-5.5f, 4.56f, 42.5f));
        AddStage("StickyStageRight", 1, new Vector3(5.5f, 4.56f, -10.0f));
        AddStage("StickyStageLeft", 2, new Vector3(-5.5f, 4.56f, -24.0f));
        AddStage("ExitDeckStage", 3, new Vector3(-2.0f, 5.06f, -103.5f));
    }

    private void AddStage(string name, int index, Vector3 position)
    {
        RouteCheckpoint3D stage = new()
        {
            Name = name,
            Position = position,
            CheckpointIndex = index,
            TriggerSize = index == 3 ? new Vector3(5.0f, 3.0f, 3.5f) : new Vector3(8.0f, 3.0f, 3.5f),
            FrameTint = index switch { 0 => new Color("5ea8b2"), 1 => new Color("d58b48"), 2 => new Color("c57945"), _ => new Color("bd74c8") },
            FlatFloorMarker = true,
        };
        stage.Entered += (entered, player) =>
        {
            if (player != _player) { return; }
            if (entered.CheckpointIndex != _nextStage) { entered.FlashDenied(); return; }
            entered.Activate();
            _nextStage++;
            if (_solutionSmoke) { GD.Print($"ROOM25_STAGE_TRACE: stage={_nextStage}/{RequiredStages}, tick={_tick}, position={player.GlobalPosition}."); }
        };
        AddChild(stage);
        _stages.Add(stage);
    }

    private void BuildGoal()
    {
        Vector3 position = new(5.5f, 5.35f, -105.0f);
        _goal = new Area3D { Name = "GoalCup", Position = position, CollisionLayer = 0, CollisionMask = 1, Monitoring = true };
        _goal.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 2.0f, Height = 3.0f } });
        _goal.BodyEntered += body =>
        {
            if (body is PlayerBall && HasCompletedRelay())
            {
                if (_stickyMinimumSpeed <= _stickyEntrySpeed * 0.4f && _bounceLaunchSpeed >= 12.0f) { MarkAdvancementCondition("surface-master"); }
                CompleteRoom();
            }
        };
        AddChild(_goal);
        RoomGeometry.AddGoalExitDoor(this, position);
    }

    private void FailMechanics(string message)
    {
        GD.PushError($"ROOM25_MECHANICS_FAIL: {message}");
        GetTree().Quit(1);
    }

    private void Fail(string message)
    {
        GD.PushError($"ROOM25_SOLUTION_FAIL: {message}");
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
