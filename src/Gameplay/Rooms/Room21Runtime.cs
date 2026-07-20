using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Physics;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Visual;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room21Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_21_solution.tres";
    private const string SurfacePath = "res://resources/surfaces/absorbing.tres";
    private const int RequiredSolutionRuns = 10;
    private const int MaximumSolutionTicksPerRun = 2600;
    private const int RequiredButtons = 3;
    private const int RequiredNeutralAbsorptionTicks = 24;
    private const float MinimumAbsorberEntrySpeed = 6.0f;
    private const float MaximumRetainedSpeedRatio = 0.22f;
    private const float StopTargetZ = -21.0f;
    private const float StopTargetRadius = 1.65f;

    private readonly List<RouteCheckpoint3D> _sequenceButtons = new();
    private readonly Dictionary<RouteCheckpoint3D, Material> _buttonIdleMaterials = new();
    private readonly Dictionary<RouteCheckpoint3D, Tween> _wrongOrderTweens = new();
    private readonly List<MeshInstance3D> _targetChargeSegments = new();

    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private Area3D _goal = null!;
    private MeshInstance3D _stopTargetRing = null!;
    private MeshInstance3D _stopTargetCore = null!;
    private Material _targetIdleMaterial = null!;
    private Material _targetActiveMaterial = null!;
    private Material _chargeEmptyMaterial = null!;
    private Material _chargeActiveMaterial = null!;
    private Material _wrongOrderMaterial = null!;
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private bool _runSolutionSmoke;
    private bool _runShellSmoke;
    private bool _runPreview;
    private bool _runMechanicsSmoke;
    private bool _solutionSmokeFinishing;
    private bool _touchedAbsorber;
    private bool _verifiedAbsorption;
    private bool _wasOnAbsorber;
    private int _nextSequenceButton;
    private int _solutionRun;
    private int _solutionTick;
    private int _shellSmokeTick;
    private int _previewFrames;
    private int _neutralAbsorptionTicks;
    private int _mechanicsSmokeTick;
    private float _entrySpeed;
    private float _minimumSpeed;
    private float _verifiedEntrySpeed;
    private float _verifiedMinimumSpeed;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] args = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(args, value => value == "--room21-solution-smoke");
        _runShellSmoke = Array.Exists(args, value => value == "--room-shell-smoke");
        _runPreview = Array.Exists(args, value => value == "--room21-preview");
        _runMechanicsSmoke = Array.Exists(args, value => value == "--room21-mechanics-smoke");

        BuildRoom();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room21_a", new Vector3(8.2f, 22.0f, 46.0f), new Vector3(0.0f, 7.0f, 2.0f), 57.0f),
            new("room21_b", new Vector3(-8.0f, 10.5f, -10.0f), new Vector3(0.0f, 5.5f, -22.0f), 56.0f),
        });

        _player = GetNode<PlayerBall>("Player");
        _cameraRig = GetNode<PlayerCameraRig>("CameraRig");
        _spawnTransform = _player.GlobalTransform;
        _cameraRig.Follow(_player);
        _player.MovementBasis = _cameraRig.MovementBasis;
        if (_runPreview) { _cameraRig.SetInputEnabled(false); }

        if (_runSolutionSmoke)
        {
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            if (_solutionTrace is null ||
                _solutionTrace.RoomId != RoomId ||
                _solutionTrace.MoveInputs.Count < 7 ||
                !_solutionTrace.MoveInputs.Any(value => value.LengthSquared() < 0.0001f))
            {
                FailSolutionSmoke("The Room 21 SolutionTrace must contain the three-way slalom and a neutral precision stop.");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_runPreview && ++_previewFrames >= 30)
        {
            string path = ProjectSettings.GlobalizePath("user://room21-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"ROOM21_PREVIEW_CAPTURE: {path}");
            GetTree().Quit(0);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_runShellSmoke) { RunShellSmokeTick(); return; }
        if (_runMechanicsSmoke) { RunMechanicsSmokeTick(); return; }
        if (_runSolutionSmoke) { RunSolutionTick(); return; }
        TrackAbsorber(ReadMoveInput());
        if (_player.GlobalPosition.Y < -7.0f) { RestartRoom(); }
    }

    public override void RestartRoom()
    {
        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _player.ResetTo(_spawnTransform);
        ResetPuzzleState();
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
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 21 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }
        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 21 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void RunMechanicsSmokeTick()
    {
        if (++_mechanicsSmokeTick == 1)
        {
            _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
            if (IsComplete || IsExitTraversalPending)
            {
                FailMechanicsSmoke("The direct goal entered before any absorber or button requirement.");
                return;
            }

            _sequenceButtons[1].Press(_player);
            if (_nextSequenceButton != 0 || _sequenceButtons[1].IsActivated)
            {
                FailMechanicsSmoke("The second button accepted a wrong-order entry.");
                return;
            }

            foreach (RouteCheckpoint3D button in _sequenceButtons)
            {
                button.Press(_player);
            }
            _touchedAbsorber = true;
            _verifiedAbsorption = true;
            _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
            if (!IsComplete && !IsExitTraversalPending)
            {
                FailMechanicsSmoke("The complete three-button and precision-stop state did not open the exit.");
                return;
            }

            if (GetNodeOrNull("AbsorbingSlalom") is not StaticBody3D ||
                GetNodeOrNull("PrecisionStopTarget") is not Node3D)
            {
                FailMechanicsSmoke("The absorber slalom or its visible stop target is missing.");
                return;
            }

            GD.Print("ROOM21_MECHANICS_PASS: wrong order stayed inactive; three ordered foam buttons and the precision absorption stop were all required.");
            GetTree().Quit(0);
        }
    }

    private void RunSolutionTick()
    {
        if (_solutionTrace is null || _solutionSmokeFinishing) { return; }
        if (IsComplete)
        {
            if (!_touchedAbsorber || !_verifiedAbsorption || _nextSequenceButton != RequiredButtons)
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} bypassed the absorber slalom: buttons={_nextSequenceButton}/{RequiredButtons}, touched={_touchedAbsorber}, verified={_verifiedAbsorption}, entry={_entrySpeed:F2}, minimum={_minimumSpeed:F2}.");
                return;
            }
            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print($"ROOM21_SOLUTION_PASS: SolutionTrace crossed three ordered foam buttons and stopped inside the precision target from {_verifiedEntrySpeed:F2} to {_verifiedMinimumSpeed:F2} m/s for {_solutionRun} consecutive completions.");
                FinishSolutionSmoke(0);
                return;
            }
            ClearCompletionState();
            _player.ResetTo(_spawnTransform);
            _solutionTick = 0;
            ResetPuzzleState();
            return;
        }
        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} timed out at {_player.GlobalPosition}; buttons={_nextSequenceButton}/{RequiredButtons}, touched={_touchedAbsorber}, verified={_verifiedAbsorption}, speed={_player.LinearVelocity.Length():F2}.");
            return;
        }
        Vector2 move = ResolveTraceInput(_solutionTick - 1);
        _player.SimulatedMoveInput = move;
        TrackAbsorber(move);
    }

    private void TrackAbsorber(Vector2 move)
    {
        bool onAbsorber = _player.GroundSurfaceKind == SurfaceKind.Absorbing;
        float planarSpeed = _player.LinearVelocity.Slide(Vector3.Up).Length();
        if (onAbsorber && !_wasOnAbsorber)
        {
            _touchedAbsorber = true;
            _entrySpeed = planarSpeed;
            _minimumSpeed = planarSpeed;
        }
        if (onAbsorber)
        {
            _minimumSpeed = Mathf.Min(_minimumSpeed, planarSpeed);
        }

        Vector2 targetOffset = new(_player.GlobalPosition.X, _player.GlobalPosition.Z - StopTargetZ);
        bool validPrecisionStop = onAbsorber &&
            _nextSequenceButton == RequiredButtons &&
            move.LengthSquared() < 0.0001f &&
            targetOffset.Length() <= StopTargetRadius &&
            _entrySpeed >= MinimumAbsorberEntrySpeed &&
            planarSpeed <= _entrySpeed * MaximumRetainedSpeedRatio;
        _neutralAbsorptionTicks = validPrecisionStop ? _neutralAbsorptionTicks + 1 : 0;
        UpdateTargetChargeVisual();
        if (!_verifiedAbsorption && _neutralAbsorptionTicks >= RequiredNeutralAbsorptionTicks)
        {
            _verifiedAbsorption = true;
            _verifiedEntrySpeed = _entrySpeed;
            _verifiedMinimumSpeed = planarSpeed;
            _stopTargetRing.MaterialOverride = _targetActiveMaterial;
            _stopTargetCore.MaterialOverride = _targetActiveMaterial;
            if (_runSolutionSmoke)
            {
                GD.Print($"ROOM21_STOP_TRACE: tick={_solutionTick}, position={_player.GlobalPosition}, entry={_entrySpeed:F2}, speed={planarSpeed:F2}.");
            }
        }
        _wasOnAbsorber = onAbsorber;
    }

    private Vector2 ReadMoveInput()
    {
        if (_player.SimulatedMoveInput.HasValue) { return _player.SimulatedMoveInput.Value; }
        return new Vector2(
            Godot.Input.GetActionStrength(InputDefaults.MoveRight) - Godot.Input.GetActionStrength(InputDefaults.MoveLeft),
            Godot.Input.GetActionStrength(InputDefaults.MoveBack) - Godot.Input.GetActionStrength(InputDefaults.MoveForward)).LimitLength(1.0f);
    }

    private Vector2 ResolveTraceInput(int tick)
    {
        if (_solutionTrace is null) { return Vector2.Zero; }
        int remaining = tick;
        for (int index = 0; index < _solutionTrace.MoveInputs.Count; index++)
        {
            int duration = _solutionTrace.MoveDurationsTicks[index];
            if (remaining < duration) { return _solutionTrace.MoveInputs[index]; }
            remaining -= duration;
        }
        return _solutionTrace.HoldLastInput ? _solutionTrace.MoveInputs[^1] : Vector2.Zero;
    }

    private void ResetPuzzleState()
    {
        _touchedAbsorber = false;
        _verifiedAbsorption = false;
        _wasOnAbsorber = false;
        _nextSequenceButton = 0;
        _neutralAbsorptionTicks = 0;
        _entrySpeed = 0.0f;
        _minimumSpeed = float.MaxValue;
        _verifiedEntrySpeed = 0.0f;
        _verifiedMinimumSpeed = 0.0f;
        UpdateTargetChargeVisual();
        if (IsInstanceValid(_stopTargetRing)) { _stopTargetRing.MaterialOverride = _targetIdleMaterial; }
        if (IsInstanceValid(_stopTargetCore)) { _stopTargetCore.MaterialOverride = _targetIdleMaterial; }
        foreach (RouteCheckpoint3D button in _sequenceButtons)
        {
            button.ResetCheckpoint();
            if (_buttonIdleMaterials.TryGetValue(button, out Material? idleMaterial) &&
                button.GetNodeOrNull<MeshInstance3D>("InsetPlate") is MeshInstance3D inset)
            {
                SetButtonVisual(inset, idleMaterial, true);
            }
        }
        foreach (Tween tween in _wrongOrderTweens.Values) { tween.Kill(); }
        _wrongOrderTweens.Clear();
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string concrete = "res://assets/textures/industrial_concrete.png";
        const string foam = "res://assets/textures/absorbing_foam.svg";
        Color wall = new("69766f");
        Color frame = new("394a43");
        SurfaceProfile profile = GD.Load<SurfaceProfile>(SurfacePath) ?? new SurfaceProfile { Kind = SurfaceKind.Absorbing, Friction = 0.22f, LinearDrag = 3.4f };
        ShaderMaterial caramelMaterial = (ShaderMaterial)GD.Load<ShaderMaterial>("res://resources/materials/sticky_caramel.tres").Duplicate();

        _wrongOrderMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color("d62f2f"),
            Roughness = 0.54f,
            EmissionEnabled = true,
            Emission = new Color("721010"),
            EmissionEnergyMultiplier = 1.15f,
        };
        _targetIdleMaterial = RoomGeometry.CreateMaterial("res://assets/textures/copper_rivets.svg", new Color("d0a365"), 0.38f, 0.56f);
        _targetActiveMaterial = RoomGeometry.CreateMaterial("res://assets/textures/sugar_glaze.svg", new Color("84d1aa"), 0.08f, 0.48f, emissionEnabled: true, emission: new Color("1e614b"));
        _chargeEmptyMaterial = RoomGeometry.CreateMaterial(metal, new Color("283432"), 0.32f, 0.72f);
        _chargeActiveMaterial = RoomGeometry.CreateMaterial("res://assets/textures/sugar_glaze.svg", new Color("8ed7b5"), 0.06f, 0.44f, emissionEnabled: true, emission: new Color("2b795c"));

        RoomGeometry.AddClosedRoomShell(this, "RoomShell", Vector3.Zero, new Vector2(20.0f, 104.0f), -3.0f, 38.0f, concrete, wall, new Color("29352f"), body =>
        {
            if (body is PlayerBall) { RestartRoom(); }
        });

        const float routeWidth = 19.5f;
        RoomGeometry.AddBox(this, "SafeStart", new Vector3(routeWidth, 0.55f, 16.0f), new Vector3(0.0f, 16.0f, 44.0f), Vector3.Zero, metal, new Color("aebbb3"), 0.42f, 0.65f);
        AddSlopeBetween("MomentumDescent", routeWidth, 0.55f, 36.0f, 16.275f, 10.0f, 5.0f, metal, new Color("899a90"));
        RoomGeometry.AddBox(this, "AbsorbingSlalom", new Vector3(routeWidth, 0.72f, 36.0f), new Vector3(0.0f, 4.64f, -8.0f), Vector3.Zero, string.Empty, Colors.White, 0.02f, 0.98f, bounce: 0.0f, surfaceProfile: profile, materialOverride: caramelMaterial);
        RoomGeometry.AddBox(this, "ExitRunout", new Vector3(routeWidth, 0.5f, 26.0f), new Vector3(0.0f, 4.75f, -39.0f), Vector3.Zero, metal, new Color("a4b0aa"), 0.4f, 0.66f);

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(this, $"StartRail{side}", new Vector3(0.32f, 1.45f, 16.0f), new Vector3(side * 9.59f, 16.8f, 44.0f), Vector3.Zero, metal, frame, 0.4f, 0.66f);
            AddSlopeRail($"SlopeRail{side}", side * 9.59f, 36.0f, 16.275f, 10.0f, 5.0f, metal, frame);
            RoomGeometry.AddBox(this, $"FoamKerb{side}", new Vector3(0.32f, 1.1f, 36.0f), new Vector3(side * 9.59f, 5.25f, -8.0f), Vector3.Zero, foam, new Color("68756c"), 0.02f, 0.98f);
            RoomGeometry.AddBox(this, $"ExitRail{side}", new Vector3(0.32f, 1.35f, 26.0f), new Vector3(side * 9.59f, 5.45f, -39.0f), Vector3.Zero, metal, frame, 0.4f, 0.66f);
        }

        AddSequenceButton("FoamButtonOne", 0, new Vector3(-5.2f, 5.06f, 3.5f));
        AddSequenceButton("FoamButtonTwo", 1, new Vector3(5.2f, 5.06f, -5.0f));
        AddSequenceButton("FoamButtonThree", 2, new Vector3(-4.2f, 5.06f, -13.5f));
        BuildStopTarget(new Vector3(0.0f, 5.02f, StopTargetZ));

        StandardMaterial3D routeMaterial = RoomGeometry.CreateMaterial(metal, new Color("dceadf"), 0.18f, 0.62f, emissionEnabled: true, emission: new Color("315742"));
        AddRouteRibbon(new Vector3(0.0f, 5.02f, 8.0f), new Vector3(-5.2f, 5.02f, 3.5f), routeMaterial);
        AddRouteRibbon(new Vector3(-5.2f, 5.02f, 3.5f), new Vector3(5.2f, 5.02f, -5.0f), routeMaterial);
        AddRouteRibbon(new Vector3(5.2f, 5.02f, -5.0f), new Vector3(-4.2f, 5.02f, -13.5f), routeMaterial);
        AddRouteRibbon(new Vector3(-4.2f, 5.02f, -13.5f), new Vector3(0.0f, 5.02f, StopTargetZ), routeMaterial);
    }

    private void AddSlopeBetween(string name, float width, float thickness, float highZ, float highY, float lowZ, float lowY, string texture, Color tint)
    {
        float run = highZ - lowZ;
        float drop = highY - lowY;
        float angle = -Mathf.Atan2(drop, run);
        float length = Mathf.Sqrt((run * run) + (drop * drop));
        Vector3 normal = new(0.0f, Mathf.Cos(angle), Mathf.Sin(angle));
        Vector3 topCenter = new(0.0f, (highY + lowY) * 0.5f, (highZ + lowZ) * 0.5f);
        Vector3 bodyCenter = topCenter - (normal * (thickness * 0.5f));
        RoomGeometry.AddBox(this, name, new Vector3(width, thickness, length), bodyCenter, new Vector3(angle, 0.0f, 0.0f), texture, tint, 0.38f, 0.6f);
    }

    private void AddSlopeRail(string name, float x, float highZ, float highY, float lowZ, float lowY, string texture, Color tint)
    {
        float run = highZ - lowZ;
        float drop = highY - lowY;
        float angle = -Mathf.Atan2(drop, run);
        float length = Mathf.Sqrt((run * run) + (drop * drop));
        Vector3 normal = new(0.0f, Mathf.Cos(angle), Mathf.Sin(angle));
        Vector3 topCenter = new(x, ((highY + lowY) * 0.5f) + 0.72f, (highZ + lowZ) * 0.5f);
        Vector3 bodyCenter = topCenter - (normal * 0.16f);
        RoomGeometry.AddBox(this, name, new Vector3(0.32f, 1.45f, length), bodyCenter, new Vector3(angle, 0.0f, 0.0f), texture, tint, 0.38f, 0.6f);
    }

    private void AddSequenceButton(string name, int index, Vector3 position)
    {
        RouteCheckpoint3D button = new()
        {
            Name = name,
            Position = position,
            CheckpointIndex = index,
            TriggerSize = new Vector3(3.8f, 3.0f, 3.8f),
            FrameTint = RoomGeometry.SequenceButtonFrameTint,
            FlatFloorMarker = true,
        };
        button.Entered += (entered, player) =>
        {
            if (player != _player) { return; }
            if (entered.CheckpointIndex == _nextSequenceButton)
            {
                entered.Activate();
                _nextSequenceButton++;
                if (_runSolutionSmoke)
                {
                    GD.Print($"ROOM21_BUTTON_TRACE: button={_nextSequenceButton}/{RequiredButtons}, tick={_solutionTick}, position={player.GlobalPosition}, speed={player.LinearVelocity.Length():F2}.");
                }
            }
            else
            {
                FlashWrongOrder(entered);
            }
        };
        AddChild(button);
        MeshInstance3D inset = button.GetNode<MeshInstance3D>("InsetPlate");
        if (inset.MaterialOverride is Material idleMaterial) { _buttonIdleMaterials[button] = idleMaterial; }
        RoomGeometry.AddSequencePips(inset, index + 1);
        _sequenceButtons.Add(button);
    }

    private void FlashWrongOrder(RouteCheckpoint3D button)
    {
        button.FlashDenied();
    }

    private static void SetButtonVisual(MeshInstance3D inset, Material material, bool showPips)
    {
        inset.MaterialOverride = material;
        foreach (MeshInstance3D pip in inset.GetChildren().OfType<MeshInstance3D>())
        {
            if (pip.Name.ToString().StartsWith("SequencePip", StringComparison.Ordinal)) { pip.Visible = showPips; }
        }
    }

    private void AddRouteRibbon(Vector3 start, Vector3 end, Material material)
    {
        Vector3 delta = end - start;
        float length = new Vector2(delta.X, delta.Z).Length();
        float yaw = Mathf.Atan2(delta.X, delta.Z);
        MeshInstance3D ribbon = new()
        {
            Name = $"FoamRoute{GetChildCount()}",
            Position = (start + end) * 0.5f,
            Rotation = new Vector3(0.0f, yaw, 0.0f),
            Mesh = new BoxMesh { Size = new Vector3(0.34f, 0.025f, length) },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(ribbon);
    }

    private void BuildStopTarget(Vector3 position)
    {
        Node3D target = new() { Name = "PrecisionStopTarget", Position = position };
        AddChild(target);
        _stopTargetRing = new MeshInstance3D
        {
            Name = "StopTargetRing",
            Mesh = new TorusMesh { InnerRadius = 1.56f, OuterRadius = 1.74f, Rings = 40, RingSegments = 10 },
            MaterialOverride = _targetIdleMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        target.AddChild(_stopTargetRing);
        _stopTargetCore = new MeshInstance3D
        {
            Name = "StopTargetCore",
            Mesh = new CylinderMesh { TopRadius = 0.34f, BottomRadius = 0.34f, Height = 0.035f, RadialSegments = 32 },
            MaterialOverride = _targetIdleMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        target.AddChild(_stopTargetCore);

        const int segmentCount = 12;
        for (int index = 0; index < segmentCount; index++)
        {
            float angle = index * Mathf.Tau / segmentCount;
            MeshInstance3D segment = RoomGeometry.AddVisualBox(
                target,
                $"TargetChargeSegment{index + 1}",
                new Vector3(0.34f, 0.055f, 0.16f),
                new Vector3(Mathf.Sin(angle) * 1.98f, 0.055f, Mathf.Cos(angle) * 1.98f),
                new Vector3(0.0f, angle, 0.0f),
                string.Empty,
                Colors.White,
                0.0f,
                1.0f,
                _chargeEmptyMaterial as StandardMaterial3D);
            segment.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _targetChargeSegments.Add(segment);
        }
    }

    private void UpdateTargetChargeVisual()
    {
        float charge = Mathf.Clamp(_neutralAbsorptionTicks / (float)RequiredNeutralAbsorptionTicks, 0.0f, 1.0f);
        int filledSegments = Mathf.CeilToInt(charge * _targetChargeSegments.Count);
        for (int index = 0; index < _targetChargeSegments.Count; index++)
        {
            _targetChargeSegments[index].MaterialOverride = index < filledSegments ? _chargeActiveMaterial : _chargeEmptyMaterial;
        }
    }

    private void BuildGoal()
    {
        Vector3 position = new(0.0f, 5.85f, -51.0f);
        _goal = new Area3D { Name = "GoalCup", Position = position, CollisionLayer = 0, CollisionMask = 1, Monitoring = true };
        _goal.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 2.0f, Height = 3.0f } });
        _goal.BodyEntered += body =>
        {
            if (body is PlayerBall && _verifiedAbsorption && _nextSequenceButton == RequiredButtons) { CompleteRoom(); }
        };
        AddChild(_goal);
        RoomGeometry.AddGoalExitDoor(this, position);
    }

    private void FailMechanicsSmoke(string message)
    {
        GD.PushError($"ROOM21_MECHANICS_FAIL: {message}");
        GetTree().Quit(1);
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM21_SOLUTION_FAIL: {message}");
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
