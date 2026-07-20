using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Physics;
using Velocitex.Core.Rooms;
using Velocitex.Core.Settings;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Interaction;
using Velocitex.Gameplay.Physics;
using Velocitex.Gameplay.Player;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room27Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_27_solution.tres";
    private const byte InteractAction = 1;
    private const int RequiredGates = 4;
    private readonly List<RouteCheckpoint3D> _gates = new();
    private readonly List<MomentumRail3D> _rails = new();
    private readonly bool[] _fieldVisits = new bool[RequiredGates];
    private readonly bool[] _railVisits = new bool[RequiredGates];
    private PlayerBall _player = null!;
    private PlayerCameraRig _camera = null!;
    private MechanicalLever _reversalLever = null!;
    private Area3D _goal = null!;
    private FlightGate3D _airRing = null!;
    private Transform3D _spawn;
    private SolutionTrace? _trace;
    private bool _solutionSmoke;
    private bool _shellSmoke;
    private bool _preview;
    private bool _mechanicsSmoke;
    private bool _finishing;
    private bool _leverUsed;
    private bool _airRingPassed;
    private bool _showPrompts;
    private bool _highContrastPrompts;
    private int _nextGate;
    private int _tick;
    private int _shellTick;
    private int _previewFrames;
    private int _mechanicsTick;
    private float _minimumX = float.MaxValue;
    private float _maximumX = float.MinValue;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] args = OS.GetCmdlineUserArgs();
        _solutionSmoke = Array.Exists(args, value => value == "--room27-solution-smoke");
        _shellSmoke = Array.Exists(args, value => value == "--room-shell-smoke");
        _preview = Array.Exists(args, value => value == "--room27-preview");
        _mechanicsSmoke = Array.Exists(args, value => value == "--room27-mechanics-smoke");
        BuildRoom();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room27_a", new Vector3(14.0f, 24.0f, 51.0f), new Vector3(0.0f, 11.0f, -22.0f), 62.0f),
            new("room27_b", new Vector3(-14.0f, 25.0f, -43.0f), new Vector3(0.0f, 12.0f, -12.0f), 64.0f),
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
        _reversalLever.SetKeyLabel(key == Key.None ? "E" : key.ToString());
        if (_preview) { _camera.SetInputEnabled(false); _showPrompts = false; }
        _reversalLever.Activated += () =>
        {
            if (_nextGate == 1) { _leverUsed = true; }
        };
        if (_solutionSmoke)
        {
            _showPrompts = false;
            _trace = GD.Load<SolutionTrace>(TracePath);
            if (_trace is null || _trace.RoomId != RoomId || !_trace.ActionFlags.Contains(InteractAction) || _trace.MoveInputs.Count < 8)
            {
                FailSolution("The Room 27 SolutionTrace must weave four polarity gates and operate the reversal lever.");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_preview && ++_previewFrames >= 30)
        {
            string path = ProjectSettings.GlobalizePath("user://room27-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GetTree().Quit(0);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_shellSmoke) { RunShellSmoke(); return; }
        if (_mechanicsSmoke) { RunMechanicsSmoke(); return; }
        ProcessGateContact();
        TrackWeave();
        if (_solutionSmoke) { RunSolution(); return; }
        bool canUse = _nextGate == 1 && !_leverUsed && _reversalLever.CanInteract(_player);
        bool focused = canUse && _camera.IsLookingAt(_reversalLever.GlobalPosition + (Vector3.Up * 1.75f));
        _reversalLever.SetFocused(focused && _showPrompts, _highContrastPrompts);
        if (focused && Godot.Input.IsActionJustPressed(InputDefaults.Interact)) { _reversalLever.Interact(_player); }
        if (_player.GlobalPosition.Y < -7.0f) { RestartRoom(); }
    }

    public override void RestartRoom()
    {
        if (_solutionSmoke && _tick > 0 && !_finishing)
        {
            FailSolution($"The weave left the route at {_player.GlobalPosition}; gates={_nextGate}/{RequiredGates}, lever={_leverUsed}, fields={VisitedFieldCount()}, x=[{_minimumX:F2},{_maximumX:F2}].");
            return;
        }
        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _player.ResetTo(_spawn);
        ResetState();
    }

    private void TrackWeave()
    {
        _minimumX = Mathf.Min(_minimumX, _player.GlobalPosition.X);
        _maximumX = Mathf.Max(_maximumX, _player.GlobalPosition.X);
        Vector3 position = _player.GlobalPosition;
        Vector2 offset = new(position.X - _goal.GlobalPosition.X, position.Z - _goal.GlobalPosition.Z);
        if (HasCompletedWeave() && offset.Length() <= 2.1f && Mathf.Abs(position.Y - _goal.GlobalPosition.Y) <= 1.6f) { CompleteRoom(); }
    }

    private void ProcessGateContact()
    {
        if (_nextGate < 0 || _nextGate >= _gates.Count)
        {
            return;
        }

        RouteCheckpoint3D gate = _gates[_nextGate];
        Vector3 localPlayer = gate.ToLocal(_player.GlobalPosition);
        if (Mathf.Abs(localPlayer.X) <= (gate.TriggerSize.X * 0.5f) + 0.6f &&
            Mathf.Abs(localPlayer.Z) <= (gate.TriggerSize.Z * 0.5f) + 0.6f &&
            localPlayer.Y >= -1.3f &&
            localPlayer.Y <= 0.4f)
        {
            gate.Press(_player);
        }
    }

    private bool HasCompletedWeave() =>
        _leverUsed &&
        _airRingPassed &&
        _nextGate == RequiredGates &&
        VisitedFieldCount() == RequiredGates &&
        _railVisits.All(value => value) &&
        _minimumX <= -5.0f &&
        _maximumX >= 5.0f;

    private int VisitedFieldCount() => _fieldVisits.Count(value => value);

    private void RunSolution()
    {
        if (_trace is null || _finishing) { return; }
        if (IsComplete)
        {
            if (!HasCompletedWeave()) { FailSolution($"The trace bypassed the weave: gates={_nextGate}/{RequiredGates}, lever={_leverUsed}, fields={VisitedFieldCount()}, x=[{_minimumX:F2},{_maximumX:F2}]."); return; }
            GD.Print($"ROOM27_SOLUTION_PASS: SolutionTrace crossed four alternating polarity fields and gates, used the reversal lever and traversed x=[{_minimumX:F2},{_maximumX:F2}].");
            FinishSolution(0);
            return;
        }
        if (++_tick > 1800) { FailSolution($"Timed out at {_player.GlobalPosition}; gates={_nextGate}/{RequiredGates}, lever={_leverUsed}, fields={VisitedFieldCount()}."); return; }
        (Vector2 input, byte actions) = Resolve(_tick - 1);
        _player.SimulatedMoveInput = input;
        if ((actions & InteractAction) != 0 && _nextGate == 1) { _reversalLever.Interact(_player); }
        if (_tick % 120 == 0) { GD.Print($"ROOM27_TRACE: tick={_tick}, position={_player.GlobalPosition}, gates={_nextGate}/{RequiredGates}, lever={_leverUsed}."); }
    }

    private (Vector2 Input, byte Actions) Resolve(int tick)
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
        _leverUsed = false;
        _airRingPassed = false;
        _nextGate = 0;
        _minimumX = float.MaxValue;
        _maximumX = float.MinValue;
        Array.Fill(_fieldVisits, false);
        Array.Fill(_railVisits, false);
        _reversalLever.ResetLever();
        foreach (RouteCheckpoint3D gate in _gates) { gate.ResetCheckpoint(); }
        _airRing.ResetGate();
        foreach (MomentumRail3D rail in _rails) { rail.ResetBody(_player); }
    }

    private void RunMechanicsSmoke()
    {
        if (++_mechanicsTick != 1) { return; }
        _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
        if (IsComplete || IsExitTraversalPending) { FailMechanics("Direct goal entry completed the room."); return; }
        _leverUsed = true;
        _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
        if (IsComplete || IsExitTraversalPending) { FailMechanics("The reversal lever alone completed the room."); return; }
        _nextGate = RequiredGates;
        Array.Fill(_fieldVisits, true);
        Array.Fill(_railVisits, true);
        _airRingPassed = true;
        _minimumX = -6.0f;
        _maximumX = 6.0f;
        _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
        if (!IsComplete && !IsExitTraversalPending) { FailMechanics("The complete polarity weave did not open the exit."); return; }
        GD.Print("ROOM27_MECHANICS_PASS: direct entry and lever-only entry failed; four crossed magnetic rails, four ordered gates, the low-gravity ring and both lateral extremes were required.");
        GetTree().Quit(0);
    }

    private void RunShellSmoke()
    {
        if (++_shellTick == 1) { _player.ResetTo(new Transform3D(Basis.Identity, GetNode<Area3D>("RoomShell/HazardTrigger").GlobalPosition)); return; }
        if (_shellTick < 12) { return; }
        if (_player.GlobalPosition.DistanceTo(_spawn.Origin) > 0.15f) { GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 27"); GetTree().Quit(1); return; }
        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 27 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string floorTexture = "res://assets/textures/diamond_plate.png";
        Color wall = new("775b6a");
        Color rail = new("4a3842");
        RoomGeometry.AddClosedRoomShell(this, "RoomShell", new Vector3(0.0f, 0.0f, -20.0f), new Vector2(34.0f, 160.0f), -3.0f, 34.0f, "res://assets/textures/industrial_concrete.png", wall, wall.Darkened(0.55f), body =>
        {
            if (body is PlayerBall) { RestartRoom(); }
        });
        SurfaceProfile sticky = GD.Load<SurfaceProfile>("res://resources/surfaces/sticky.tres")!;
        SurfaceProfile elastic = GD.Load<SurfaceProfile>("res://resources/surfaces/super_elastic.tres")!;
        ShaderMaterial caramel = (ShaderMaterial)GD.Load<ShaderMaterial>("res://resources/materials/sticky_caramel.tres")!.Duplicate();

        // Four elevated islands and four crossing rails replace the former
        // floor-wide slalom. Every transition changes height and polarity.
        RoomGeometry.AddBox(this, "StartDeck", new Vector3(18.0f, 0.5f, 25.775f), new Vector3(0.0f, 4.25f, 46.8875f), Vector3.Zero, floorTexture, new Color("a8969f"), 0.4f, 0.66f);
        RoomGeometry.AddBox(this, "JunctionOne", new Vector3(11.0f, 0.5f, 15.0f), new Vector3(9.0f, 8.25f, 10.0f), Vector3.Zero, floorTexture, new Color("a18491"), 0.4f, 0.66f);
        RoomGeometry.AddBox(this, "CaramelControlPad", new Vector3(8.0f, 0.28f, 5.0f), new Vector3(9.0f, 8.64f, 6.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 0.72f, surfaceProfile: sticky, materialOverride: caramel);
        RoomGeometry.AddBox(this, "JunctionTwo", new Vector3(11.0f, 0.5f, 15.0f), new Vector3(-9.0f, 12.25f, -25.0f), Vector3.Zero, metal, new Color("8793a5"), 0.4f, 0.66f);
        ShaderMaterial elasticMaterial = (ShaderMaterial)GD.Load<ShaderMaterial>("res://resources/materials/super_elastic_membrane.tres")!.Duplicate();
        RoomGeometry.AddBox(this, "ElasticRailLaunch", new Vector3(7.5f, 0.30f, 5.0f), new Vector3(-9.0f, 12.65f, -29.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 0.32f, surfaceProfile: elastic, materialOverride: elasticMaterial);
        RoomGeometry.AddBox(this, "JunctionThree", new Vector3(11.0f, 0.5f, 15.0f), new Vector3(9.0f, 16.25f, -55.0f), Vector3.Zero, floorTexture, new Color("a18491"), 0.4f, 0.66f);
        RoomGeometry.AddBox(this, "FinalDeck", new Vector3(12.0f, 0.5f, 20.0f), new Vector3(-9.0f, 8.25f, -86.0f), Vector3.Zero, floorTexture, new Color("9d919a"), 0.4f, 0.66f);

        AddMomentumRail(0, "PositiveClimbRail", new Vector3(0.0f, 5.0f, 35.0f), new Vector3(9.0f, 9.0f, 16.0f), new Color("e37c9f"));
        AddMomentumRail(1, "NegativeLowGravityRail", new Vector3(9.0f, 9.0f, 3.0f), new Vector3(-9.0f, 13.0f, -19.0f), new Color("72a9e8"));
        AddMomentumRail(2, "PositiveBounceRail", new Vector3(-9.0f, 13.2f, -32.0f), new Vector3(9.0f, 17.0f, -49.0f), new Color("e37c9f"));
        AddMomentumRail(3, "NegativeDropRail", new Vector3(9.0f, 17.0f, -62.0f), new Vector3(-9.0f, 9.0f, -77.0f), new Color("72a9e8"));

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(this, $"StartRail{side}", new Vector3(0.42f, 1.8f, 25.775f), new Vector3(side * 9.18f, 5.35f, 46.8875f), Vector3.Zero, metal, rail, 0.42f, 0.65f);
            RoomGeometry.AddBox(this, $"JunctionOneRail{side}", new Vector3(0.42f, 2.2f, 15.0f), new Vector3(9.0f + (side * 5.68f), 9.6f, 10.0f), Vector3.Zero, metal, rail, 0.42f, 0.65f);
            RoomGeometry.AddBox(this, $"JunctionTwoRail{side}", new Vector3(0.42f, 2.2f, 15.0f), new Vector3(-9.0f + (side * 5.68f), 13.6f, -25.0f), Vector3.Zero, metal, rail, 0.42f, 0.65f);
            RoomGeometry.AddBox(this, $"JunctionThreeRail{side}", new Vector3(0.42f, 2.2f, 15.0f), new Vector3(9.0f + (side * 5.68f), 17.6f, -55.0f), Vector3.Zero, metal, rail, 0.42f, 0.65f);
            RoomGeometry.AddBox(this, $"FinalRail{side}", new Vector3(0.42f, 1.8f, 20.0f), new Vector3(-9.0f + (side * 6.18f), 9.35f, -86.0f), Vector3.Zero, metal, rail, 0.42f, 0.65f);
        }

        AddField(0, "PositiveFieldOne", new Vector3(4.5f, 9.0f, 25.0f), new Vector3(14.0f, 10.0f, 22.0f), Vector3.Right, new Color("e37c9f"));
        AddField(1, "NegativeFieldTwo", new Vector3(0.0f, 13.0f, -8.0f), new Vector3(26.0f, 14.0f, 24.0f), Vector3.Left, new Color("72a9e8"));
        AddField(2, "PositiveFieldThree", new Vector3(0.0f, 17.0f, -40.5f), new Vector3(26.0f, 14.0f, 19.0f), Vector3.Right, new Color("e37c9f"));
        AddField(3, "NegativeFieldFour", new Vector3(0.0f, 14.0f, -69.5f), new Vector3(26.0f, 18.0f, 17.0f), Vector3.Left, new Color("72a9e8"));

        ForceVolume3D lowGravity = new()
        {
            Name = "LowGravityRailCrossing",
            Position = new Vector3(0.0f, 14.0f, -8.0f),
            Profile = GD.Load<ForceVolumeProfile>("res://resources/force_volumes/low_gravity.tres"),
            CollisionLayer = 0,
            CollisionMask = 1,
        };
        lowGravity.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(29.0f, 20.0f, 27.0f) } });
        AddChild(lowGravity);

        _airRing = new FlightGate3D
        {
            Name = "LowGravityTransferRing",
            Position = new Vector3(0.0f, 11.0f, -8.0f),
            Rotation = new Vector3(0.0f, Mathf.DegToRad(39.0f), 0.0f),
            Radius = 3.2f,
            MinimumExitSpeed = 12.0f,
            SpeedGain = 0.0f,
            SpeedMultiplier = 1.0f,
            EnableAudio = !_solutionSmoke,
        };
        _airRing.Passed += player => { if (player == _player) { _airRingPassed = true; } };
        AddChild(_airRing);

        AddGate("PositiveGateOne", 0, new Vector3(9.0f, 9.76f, 13.0f), new Color("e37c9f"));
        AddGate("NegativeGateTwo", 1, new Vector3(-9.0f, 14.16f, -23.0f), new Color("72a9e8"));
        AddGate("PositiveGateThree", 2, new Vector3(9.0f, 18.16f, -53.0f), new Color("e37c9f"));
        AddGate("NegativeGateFour", 3, new Vector3(-9.0f, 10.16f, -81.0f), new Color("72a9e8"));

        _reversalLever = new MechanicalLever { Name = "PolarityReversalLever", Position = new Vector3(9.0f, 8.5f, 6.0f), ActivationRadius = 5.5f };
        AddChild(_reversalLever);
    }

    private void AddMomentumRail(int index, string name, Vector3 start, Vector3 end, Color color)
    {
        MomentumRail3D rail = new()
        {
            Name = name,
            LocalStart = start,
            LocalEnd = end,
            CaptureRadius = 4.8f,
            MinimumSpeed = 13.0f,
            CollisionMask = 1,
        };
        rail.Attached += body =>
        {
            if (body == _player)
            {
                _railVisits[index] = true;
            }
        };
        AddChild(rail);
        _rails.Add(rail);

        Vector3 path = end - start;
        float length = path.Length();
        Vector3 center = (start + end) * 0.5f;
        Basis pathBasis = new(new Quaternion(Vector3.Back, path.Normalized()));
        Vector3 lateral = pathBasis.X.Normalized();
        StandardMaterial3D beamMaterial = RoomGeometry.CreateMaterial(
            "res://assets/textures/brushed_metal.png",
            color.Lerp(Colors.White, 0.16f),
            0.38f,
            0.56f,
            emissionEnabled: true,
            emission: color.Darkened(0.58f));
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            MeshInstance3D beam = RoomGeometry.AddVisualBox(
                this,
                $"{name}GuideBeam{(side < 0.0f ? "Left" : "Right")}",
                new Vector3(0.18f, 0.18f, length),
                center - (Vector3.Up * 0.48f) + (lateral * side * 0.42f),
                Vector3.Zero,
                string.Empty,
                Colors.White,
                0.0f,
                1.0f,
                beamMaterial);
            beam.Basis = pathBasis;
        }
    }

    private void AddField(int index, string name, Vector3 position, Vector3 size, Vector3 direction, Color tint)
    {
        ForceVolume3D field = new()
        {
            Name = name,
            Position = position,
            Profile = new ForceVolumeProfile { Kind = ForceVolumeKind.Magnetic, Direction = direction, Strength = index == 3 ? 7.0f : 5.2f },
            CollisionLayer = 0,
            CollisionMask = 1,
        };
        field.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        field.RigidBodyEntered += body => { if (body == _player) { _fieldVisits[index] = true; } };
        AddChild(field);
    }

    private void AddGate(string name, int index, Vector3 position, Color tint)
    {
        RouteCheckpoint3D gate = new() { Name = name, Position = position, CheckpointIndex = index, TriggerSize = new Vector3(6.0f, 3.0f, 5.0f), FrameTint = tint, FlatFloorMarker = true };
        gate.Entered += (entered, player) =>
        {
            if (player != _player) { return; }
            if (index != _nextGate || (index > 0 && !_leverUsed)) { entered.FlashDenied(); return; }
            entered.Activate();
            _nextGate++;
            if (_solutionSmoke) { GD.Print($"ROOM27_GATE_TRACE: gate={_nextGate}/{RequiredGates}, tick={_tick}, position={_player.GlobalPosition}."); }
        };
        AddChild(gate);
        _gates.Add(gate);
    }

    private void AddChicane(string name, float z, bool openingLeft, string texture, Color tint)
    {
        float centerX = openingLeft ? 4.75f : -4.75f;
        RoomGeometry.AddBox(this, name, new Vector3(19.5f, 3.0f, 0.65f), new Vector3(centerX, 5.75f, z), Vector3.Zero, texture, tint, 0.42f, 0.64f);
        float openingCenter = openingLeft ? -10.0f : 10.0f;
        StandardMaterial3D marker = RoomGeometry.CreateMaterial("res://assets/textures/copper_rivets.svg", openingLeft ? new Color("72a9e8") : new Color("e37c9f"), 0.25f, 0.55f, emissionEnabled: true, emission: new Color("3b263d"));
        RoomGeometry.AddVisualBox(this, $"{name}OpeningMarker", new Vector3(7.5f, 0.05f, 1.4f), new Vector3(openingCenter, 4.54f, z), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, marker);
    }

    private void BuildGoal()
    {
        Vector3 position = new(-9.0f, 9.35f, -93.0f);
        _goal = new Area3D { Name = "GoalCup", Position = position, CollisionLayer = 0, CollisionMask = 1, Monitoring = true };
        _goal.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 2.0f, Height = 3.0f } });
        _goal.BodyEntered += body => { if (body is PlayerBall && HasCompletedWeave()) { CompleteRoom(); } };
        AddChild(_goal);
        RoomGeometry.AddGoalExitDoor(this, position, Vector3.Forward);
    }

    private void FailMechanics(string message) { GD.PushError($"ROOM27_MECHANICS_FAIL: {message}"); GetTree().Quit(1); }
    private void FailSolution(string message) { GD.PushError($"ROOM27_SOLUTION_FAIL: {message}"); FinishSolution(1); }
    private async void FinishSolution(int code)
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
