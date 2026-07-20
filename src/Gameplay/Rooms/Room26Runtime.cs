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

public partial class Room26Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_26_solution.tres";
    private const byte InteractAction = 1;
    private const int RequiredGates = 4;
    private const int MaximumSolutionTicks = 1500;

    private readonly List<Area3D> _airGates = new();
    private readonly List<InterferenceCannon3D> _interferenceCannons = new();
    private PlayerBall _player = null!;
    private PlayerCameraRig _camera = null!;
    private PlayerCannon3D _cannon = null!;
    private MechanicalLever _vacuumValve = null!;
    private ForceVolume3D _vacuum = null!;
    private Area3D _goal = null!;
    private Transform3D _spawn;
    private SolutionTrace? _trace;
    private bool _solutionSmoke;
    private bool _shellSmoke;
    private bool _preview;
    private bool _mechanicsSmoke;
    private bool _finishing;
    private bool _valveOpened;
    private bool _cannonFired;
    private bool _enteredVacuum;
    private bool _landedExit;
    private bool _touchedChamberWall;
    private bool _showPrompts;
    private bool _highContrastPrompts;
    private int _nextGate;
    private int _solutionTick;
    private int _shellTick;
    private int _previewFrames;
    private int _mechanicsTick;
    private float _maximumRise;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] args = OS.GetCmdlineUserArgs();
        _solutionSmoke = Array.Exists(args, value => value == "--room26-solution-smoke");
        _shellSmoke = Array.Exists(args, value => value == "--room-shell-smoke");
        _preview = Array.Exists(args, value => value == "--room26-preview");
        _mechanicsSmoke = Array.Exists(args, value => value == "--room26-mechanics-smoke");

        BuildRoom();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room26_a", new Vector3(12.0f, 16.0f, 53.0f), new Vector3(0.0f, 14.0f, -12.0f), 59.0f),
            new("room26_b", new Vector3(-12.0f, 29.0f, -19.0f), new Vector3(2.0f, 18.0f, -57.0f), 58.0f),
        });

        _player = GetNode<PlayerBall>("Player");
        _camera = GetNode<PlayerCameraRig>("CameraRig");
        _spawn = _player.GlobalTransform;
        _camera.Follow(_player);
        _player.MovementBasis = _camera.MovementBasis;
        GameSettingsData settings = SettingsStore.Load();
        _showPrompts = settings.InteractionPrompts;
        _highContrastPrompts = settings.HighContrastPrompts;
        Key interactKey = InputDefaults.GetPrimaryKey(InputDefaults.Interact);
        string keyLabel = interactKey == Key.None ? "E" : interactKey.ToString();
        _cannon.SetKeyLabel(keyLabel);
        _vacuumValve.SetKeyLabel(keyLabel);
        if (_preview) { _camera.SetInputEnabled(false); _showPrompts = false; }

        _vacuumValve.Activated += OpenVacuumValve;
        _cannon.Fired += body => _cannonFired |= body == _player && _valveOpened;
        _vacuum.RigidBodyEntered += body => _enteredVacuum |= body == _player && _valveOpened && _cannonFired;
        _vacuum.SetPhysicsProcess(false);

        if (_solutionSmoke)
        {
            _showPrompts = false;
            _trace = GD.Load<SolutionTrace>(TracePath);
            if (_trace is null || _trace.RoomId != RoomId || !_trace.ActionFlags.Contains(InteractAction) || _trace.MoveInputs.Count < 8)
            {
                FailSolution("The Room 26 SolutionTrace must operate the valve, steer through four low-gravity rings and survive the cannon crossfire.");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_preview && ++_previewFrames >= 30)
        {
            string path = ProjectSettings.GlobalizePath("user://room26-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"ROOM26_PREVIEW_CAPTURE: {path}");
            GetTree().Quit(0);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_shellSmoke) { RunShellSmoke(); return; }
        if (_mechanicsSmoke) { RunMechanicsSmoke(); return; }
        foreach (InterferenceCannon3D cannon in _interferenceCannons) { cannon.AdvancePhysicsTick(); }
        TrackFlight();
        if (_solutionSmoke) { RunSolution(); return; }

        bool valveFocus = !_valveOpened && _vacuumValve.CanInteract(_player) && _camera.IsLookingAt(_vacuumValve.GlobalPosition + (Vector3.Up * 1.75f));
        _vacuumValve.SetFocused(valveFocus && _showPrompts, _highContrastPrompts);
        bool cannonFocus = _valveOpened && _cannon.CanInteract(_player) && _camera.IsLookingAt(_cannon.GlobalPosition + (Vector3.Up * 1.8f));
        _cannon.SetFocused(cannonFocus && _showPrompts, _highContrastPrompts);
        if (Godot.Input.IsActionJustPressed(InputDefaults.Interact))
        {
            if (valveFocus) { _vacuumValve.Interact(_player); }
            else if (cannonFocus) { _cannon.Interact(_player); }
        }
        if (_player.GlobalPosition.Y < -7.0f) { RestartRoom(); }
    }

    public override void RestartRoom()
    {
        if (_solutionSmoke && _solutionTick > 0 && !_finishing)
        {
            FailSolution($"Flight fell out of the chamber at {_player.GlobalPosition}; valve={_valveOpened}, cannon={_cannonFired}, vacuum={_enteredVacuum}, gates={_nextGate}/{RequiredGates}, rise={_maximumRise:F2}, landed={_landedExit}.");
            return;
        }
        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _cannon.ResetCannon();
        foreach (InterferenceCannon3D cannon in _interferenceCannons) { cannon.ResetCannon(); }
        _vacuumValve.ResetLever();
        _vacuum.SetPhysicsProcess(false);
        _player.ResetTo(_spawn);
        ResetState();
    }

    private void OpenVacuumValve()
    {
        _valveOpened = true;
        _vacuum.SetPhysicsProcess(true);
    }

    private void TrackFlight()
    {
        _maximumRise = Mathf.Max(_maximumRise, _player.GlobalPosition.Y - _spawn.Origin.Y);
        Vector3 position = _player.GlobalPosition;
        if (_enteredVacuum && (Mathf.Abs(position.X) >= 12.35f || position.Y >= 34.2f)) { _touchedChamberWall = true; }
        if (_enteredVacuum && _nextGate == RequiredGates && _player.IsGrounded && position.Y >= 8.75f && position.Y <= 9.35f && position.Z <= -45.0f && position.Z >= -83.5f)
        {
            _landedExit = true;
        }
        Vector2 goalOffset = new(position.X - _goal.GlobalPosition.X, position.Z - _goal.GlobalPosition.Z);
        if (HasCompletedFlight() && goalOffset.Length() <= 2.1f && Mathf.Abs(position.Y - _goal.GlobalPosition.Y) <= 1.6f)
        {
            if (!_touchedChamberWall) { MarkAdvancementCondition("vacuum-packed"); }
            CompleteRoom();
        }
    }

    private void ApplyVacuumSteering()
    {
        if (!_enteredVacuum) { return; }
        float steering = _solutionSmoke
            ? (_player.SimulatedMoveInput?.X ?? 0.0f)
            : Godot.Input.GetActionStrength(InputDefaults.MoveRight) - Godot.Input.GetActionStrength(InputDefaults.MoveLeft);
        Vector3 velocity = _player.LinearVelocity;
        velocity.X = Mathf.MoveToward(velocity.X, steering * 9.0f, 2.0f);
        _player.LinearVelocity = velocity;
    }

    private bool HasCompletedFlight() =>
        _valveOpened &&
        _cannonFired &&
        _enteredVacuum &&
        _nextGate == RequiredGates &&
        _maximumRise >= 16.0f &&
        _landedExit;

    private void RunSolution()
    {
        if (_trace is null || _finishing) { return; }
        if (IsComplete)
        {
            if (!HasCompletedFlight())
            {
                FailSolution($"The trace bypassed the flight: valve={_valveOpened}, cannon={_cannonFired}, vacuum={_enteredVacuum}, gates={_nextGate}/{RequiredGates}, rise={_maximumRise:F2}, landed={_landedExit}.");
                return;
            }
            GD.Print($"ROOM26_SOLUTION_PASS: SolutionTrace opened the valve, fired the cannon, steered through all four low-gravity rings under crossfire, rose {_maximumRise:F2} m and landed on the exit deck.");
            FinishSolution(0);
            return;
        }
        if (++_solutionTick > MaximumSolutionTicks)
        {
            FailSolution($"Timed out at {_player.GlobalPosition}; valve={_valveOpened}, cannon={_cannonFired}, vacuum={_enteredVacuum}, gates={_nextGate}/{RequiredGates}, rise={_maximumRise:F2}, landed={_landedExit}.");
            return;
        }
        (Vector2 input, byte actions) = ResolveTraceStep(_solutionTick - 1);
        _player.SimulatedMoveInput = input;
        if ((actions & InteractAction) != 0)
        {
            if (!_valveOpened) { _vacuumValve.Interact(_player); }
            else if (!_cannonFired) { _cannon.Interact(_player); }
        }
        if (_solutionTick % 120 == 0)
        {
            GD.Print($"ROOM26_TRACE: tick={_solutionTick}, position={_player.GlobalPosition}, velocity={_player.LinearVelocity}, input={_player.CurrentMoveInput}, air={_player.AirControlAcceleration:F2}, gates={_nextGate}/{RequiredGates}.");
        }
    }

    private (Vector2 Input, byte Actions) ResolveTraceStep(int tick)
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
        _valveOpened = false;
        _cannonFired = false;
        _enteredVacuum = false;
        _landedExit = false;
        _touchedChamberWall = false;
        _nextGate = 0;
        _maximumRise = 0.0f;
        foreach (Area3D gate in _airGates)
        {
            gate.SetMeta("activated", false);
            gate.GetNode<MeshInstance3D>("Ring").Scale = Vector3.One;
        }
    }

    private void RunMechanicsSmoke()
    {
        if (++_mechanicsTick != 1) { return; }
        _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
        if (IsComplete || IsExitTraversalPending) { FailMechanics("Direct goal entry completed the room."); return; }
        _vacuumValve.Interact(_player);
        _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
        if (IsComplete || IsExitTraversalPending) { FailMechanics("Opening the valve alone completed the room."); return; }
        _cannon.TryApplyImpulse(_player);
        _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
        if (IsComplete || IsExitTraversalPending) { FailMechanics("Valve and cannon alone completed the room without steering the gates."); return; }
        _enteredVacuum = true;
        _nextGate = RequiredGates;
        _maximumRise = 17.0f;
        _landedExit = true;
        _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);
        if (!IsComplete && !IsExitTraversalPending) { FailMechanics("The complete four-ring low-gravity flight did not open the exit."); return; }
        bool cannonGrid = _interferenceCannons.Count == 12 &&
            _interferenceCannons.Select(cannon => cannon.CadenceTicks).Distinct().Count() == 12 &&
            _interferenceCannons.All(cannon => cannon.HasSolidBodyHitbox && cannon.InitialDelayTicks <= 34 && cannon.UsesRandomizedTiming);
        if (!cannonGrid || !_cannon.HasSolidBodyHitbox) { FailMechanics($"The cannon hitboxes or airborne grid are incomplete: {_interferenceCannons.Count} interference cannons, launcher={_cannon.HasSolidBodyHitbox}."); return; }
        GD.Print("ROOM26_MECHANICS_PASS: direct entry, valve-only and valve-plus-cannon entry failed; four low-gravity rings, early crossfire, sufficient rise and exit landing were required.");
        GetTree().Quit(0);
    }

    private void RunShellSmoke()
    {
        if (++_shellTick == 1)
        {
            _player.ResetTo(new Transform3D(Basis.Identity, GetNode<Area3D>("RoomShell/HazardTrigger").GlobalPosition));
            return;
        }
        if (_shellTick < 12) { return; }
        if (_player.GlobalPosition.DistanceTo(_spawn.Origin) > 0.15f)
        {
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 26 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }
        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 26 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string concrete = "res://assets/textures/industrial_concrete.png";
        Color frame = new("315563");
        RoomGeometry.AddClosedRoomShell(this, "RoomShell", new Vector3(0.0f, 0.0f, -16.0f), new Vector2(28.0f, 142.0f), -3.0f, 36.0f, concrete, new Color("486c77"), new Color("183943"), body =>
        {
            if (body is PlayerBall) { RestartRoom(); }
        });
        RoomGeometry.AddBox(this, "StartDeck", new Vector3(27.5f, 0.5f, 22.0f), new Vector3(0.0f, 4.25f, 44.0f), Vector3.Zero, metal, new Color("a3b6bb"), 0.42f, 0.64f);
        RoomGeometry.AddBox(this, "ExitDeck", new Vector3(20.0f, 0.5f, 35.5f), new Vector3(2.0f, 8.25f, -62.75f), Vector3.Zero, metal, new Color("9dafb5"), 0.42f, 0.64f);

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(this, $"StartRail{side}", new Vector3(0.42f, 1.8f, 22.0f), new Vector3(side * 13.58f, 5.35f, 44.0f), Vector3.Zero, metal, frame, 0.42f, 0.64f);
            RoomGeometry.AddBox(this, $"ExitRail{side}", new Vector3(0.42f, 1.8f, 35.5f), new Vector3(2.0f + (side * 10.18f), 9.35f, -62.75f), Vector3.Zero, metal, frame, 0.42f, 0.64f);
        }

        _vacuumValve = new MechanicalLever { Name = "VacuumValve", Position = new Vector3(-5.8f, 4.5f, 49.0f), ActivationRadius = 6.5f };
        AddChild(_vacuumValve);
        _cannon = new PlayerCannon3D
        {
            Name = "VacuumCannon",
            Position = new Vector3(0.0f, 4.5f, 35.0f),
            LaunchVelocity = new Vector3(0.0f, 14.0f, -14.0f),
            MuzzleOffset = new Vector3(0.0f, 2.2f, -1.0f),
            ActivationRadius = 10.0f,
        };
        AddChild(_cannon);

        ForceVolumeProfile profile = (ForceVolumeProfile)GD.Load<ForceVolumeProfile>("res://resources/force_volumes/low_gravity.tres").Duplicate();
        _vacuum = new ForceVolume3D { Name = "LowGravityFlight", Profile = profile, Position = new Vector3(0.0f, 18.0f, -21.0f), CollisionLayer = 0, CollisionMask = 1 };
        _vacuum.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(26.0f, 34.0f, 112.0f) } });
        AddChild(_vacuum);

        AddAirGate("LowGravityRingOne", 0, new Vector3(-1.5f, 21.0f, 13.0f), new Color("69d5df"));
        AddAirGate("LowGravityRingTwo", 1, new Vector3(2.2f, 31.2f, -10.0f), new Color("d6a76b"));
        AddAirGate("LowGravityRingThree", 2, new Vector3(2.5f, 25.0f, -34.0f), new Color("75cfa9"));
        AddAirGate("LowGravityRingFour", 3, new Vector3(4.0f, 12.0f, -53.0f), new Color("c18acb"));
        BuildAirborneCrossfire();
        ForceVolume3D landingGravity = new()
        {
            Name = "ExitLandingStrongGravity",
            Position = new Vector3(2.0f, 21.0f, -66.5f),
            CollisionLayer = 0,
            CollisionMask = 1,
            Profile = GD.Load<ForceVolumeProfile>("res://resources/force_volumes/strong_gravity.tres"),
        };
        landingGravity.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(19.0f, 25.0f, 25.0f) } });
        AddChild(landingGravity);
        AddLowGravityMotes();

        StandardMaterial3D ductMaterial = RoomGeometry.CreateMaterial(metal, frame, 0.42f, 0.6f);
        for (int index = 0; index < 7; index++)
        {
            float z = 26.0f - (index * 13.0f);
            float y = 8.0f + (index * 2.5f);
            Node3D duct = new() { Name = $"VacuumDuctRib{index + 1}", Position = new Vector3(0.0f, y, z) };
            AddChild(duct);
            RoomGeometry.AddVisualBox(duct, "Left", new Vector3(0.42f, 8.0f, 0.42f), new Vector3(-12.2f, 0.0f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, ductMaterial);
            RoomGeometry.AddVisualBox(duct, "Right", new Vector3(0.42f, 8.0f, 0.42f), new Vector3(12.2f, 0.0f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, ductMaterial);
            RoomGeometry.AddVisualBox(duct, "Top", new Vector3(24.8f, 0.42f, 0.42f), new Vector3(0.0f, 4.0f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, ductMaterial);
        }

        StandardMaterial3D fanMaterial = RoomGeometry.CreateMaterial("res://assets/textures/copper_rivets.svg", new Color("4f8190"), 0.38f, 0.6f);
        Node3D fan = new() { Name = "VacuumFan", Position = new Vector3(0.0f, 28.0f, -51.0f) };
        AddChild(fan);
        for (int index = 0; index < 6; index++)
        {
            RoomGeometry.AddVisualBox(fan, $"Blade{index + 1}", new Vector3(0.55f, 5.5f, 0.25f), Vector3.Zero, new Vector3(0.0f, 0.0f, index * Mathf.Pi / 3.0f), string.Empty, Colors.White, 0.0f, 1.0f, fanMaterial);
        }
    }

    private void BuildAirborneCrossfire()
    {
        float[] columnZ = { 16.0f, 2.0f, -12.0f, -26.0f, -40.0f, -54.0f };
        for (int column = 0; column < columnZ.Length; column++)
        {
            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                float side = sideIndex == 0 ? -1.0f : 1.0f;
                float y = 13.0f + ((column % 3) * 8.5f);
                InterferenceCannon3D cannon = new()
                {
                    Name = $"AirborneInterferenceCannon{column + 1}_{sideIndex + 1}",
                    Position = new Vector3(side * 10.6f, y, columnZ[column]),
                    MuzzleOffset = new Vector3(-side * 3.0f, 2.6f, 0.0f),
                    ProjectileVelocity = new Vector3(-side * 23.0f, 0.0f, 0.0f),
                    InitialDelayTicks = 8 + (column * 4) + sideIndex,
                    CadenceTicks = 93 + (((column * 2) + sideIndex) * 3),
                    ProjectileLifetimeTicks = 72,
                    PoolSize = 3,
                    EnableAudio = !_solutionSmoke && (column + sideIndex) % 3 == 0,
                };
                AddChild(cannon);
                _interferenceCannons.Add(cannon);
            }
        }
    }

    private void AddLowGravityMotes()
    {
        StandardMaterial3D material = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color("c6eadc"),
            EmissionEnabled = true,
            Emission = new Color("659b88"),
        };
        ParticleProcessMaterial process = new()
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(12.0f, 14.0f, 40.0f),
            Direction = Vector3.Up,
            Spread = 15.0f,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 0.3f,
            InitialVelocityMax = 0.8f,
        };
        AddChild(new GpuParticles3D
        {
            Name = "LowGravityMotes",
            Position = new Vector3(0.0f, 18.0f, -21.0f),
            Amount = 96,
            Lifetime = 6.0,
            Randomness = 0.8f,
            ProcessMaterial = process,
            DrawPass1 = new SphereMesh { Radius = 0.05f, Height = 0.1f, RadialSegments = 8, Rings = 4, Material = material },
        });
    }

    private void AddAirGate(string name, int index, Vector3 position, Color tint)
    {
        Area3D gate = new() { Name = name, Position = position, CollisionLayer = 0, CollisionMask = 1, Monitoring = true };
        gate.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(9.2f, 9.2f, 3.5f) } });
        StandardMaterial3D material = RoomGeometry.CreateMaterial("res://assets/textures/copper_rivets.svg", tint, 0.24f, 0.5f, emissionEnabled: true, emission: tint.Darkened(0.65f));
        MeshInstance3D ring = new()
        {
            Name = "Ring",
            Rotation = new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f),
            Mesh = new TorusMesh { InnerRadius = 4.35f, OuterRadius = 4.75f, Rings = 32, RingSegments = 10 },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        gate.AddChild(ring);
        gate.BodyEntered += body =>
        {
            if (body != _player || index != _nextGate || !_enteredVacuum) { return; }
            gate.SetMeta("activated", true);
            ring.Scale = Vector3.One * 1.12f;
            _nextGate++;
            if (_solutionSmoke) { GD.Print($"ROOM26_GATE_TRACE: gate={_nextGate}/{RequiredGates}, tick={_solutionTick}, position={_player.GlobalPosition}."); }
        };
        AddChild(gate);
        _airGates.Add(gate);
    }

    private void BuildGoal()
    {
        Vector3 position = new(0.0f, 9.35f, -78.0f);
        _goal = new Area3D { Name = "GoalCup", Position = position, CollisionLayer = 0, CollisionMask = 1, Monitoring = true };
        _goal.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 2.0f, Height = 3.0f } });
        _goal.BodyEntered += body =>
        {
            if (body is not PlayerBall || !HasCompletedFlight()) { return; }
            if (!_touchedChamberWall) { MarkAdvancementCondition("vacuum-packed"); }
            CompleteRoom();
        };
        AddChild(_goal);
        RoomGeometry.AddGoalExitDoor(this, position, Vector3.Forward);
    }

    private void FailMechanics(string message)
    {
        GD.PushError($"ROOM26_MECHANICS_FAIL: {message}");
        GetTree().Quit(1);
    }

    private void FailSolution(string message)
    {
        GD.PushError($"ROOM26_SOLUTION_FAIL: {message}");
        FinishSolution(1);
    }

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
