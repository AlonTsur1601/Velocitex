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

public partial class Room06Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_06_solution.tres";
    private const string SurfacePath = "res://resources/surfaces/frictionless.tres";
    private const int RequiredSolutionRuns = 10;
    private const int RequiredGlassBridges = 4;
    private const int MaximumSolutionTicksPerRun = 1100;

    private readonly List<(MeshInstance3D Band, ProfiledSurfaceBody Glass)> _sheenBands = new();
    private readonly bool[] _crossedGlassBridges = new bool[RequiredGlassBridges];
    private FlightGate3D _momentumGate = null!;
    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private StaticBody3D _returnShutter = null!;
    private CollisionShape3D _returnShutterCollision = null!;
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private bool _touchedGlassThisRun;
    private bool _maintainedGlassMomentumThisRun;
    private bool _passedMomentumGateThisRun;
    private bool _runSolutionSmoke;
    private bool _runPreview;
    private bool _runShellSmoke;
    private int _routeProgress;
    private int _solutionRun;
    private int _solutionTick;
    private int _previewFrames;
    private int _shellSmokeTick;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        BuildRoom();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room06_a", new Vector3(8.2f, 9.0f, 35.0f), new Vector3(0.0f, 2.2f, -12.0f), 54.0f),
            new("room06_b", new Vector3(-8.0f, 7.0f, -18.0f), new Vector3(0.0f, 1.8f, -53.0f), 56.0f),
        });

        _player = GetNode<PlayerBall>("Player");
        _cameraRig = GetNode<PlayerCameraRig>("CameraRig");
        _spawnTransform = _player.GlobalTransform;
        _cameraRig.Follow(_player);
        _player.MovementBasis = _cameraRig.MovementBasis;

        string[] userArguments = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(userArguments, argument => argument == "--room06-solution-smoke");
        _runPreview = Array.Exists(userArguments, argument => argument == "--room06-preview");
        _runShellSmoke = Array.Exists(userArguments, argument => argument == "--room-shell-smoke");
        if (_runPreview)
        {
            _cameraRig.SetInputEnabled(false);
        }

        if (_runSolutionSmoke)
        {
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            bool hasSteeringChoice = _solutionTrace is not null &&
                _solutionTrace.MoveInputs.Any(input => Mathf.Abs(input.X) >= 0.2f);
            int timedBridgeCount = GetTree().GetNodesInGroup("timed_breakable_glass")
                .OfType<StaticBody3D>()
                .Count(body => body.Name.ToString().StartsWith("TimedGlassBridge", StringComparison.Ordinal));
            if (_solutionTrace is null ||
                _solutionTrace.RoomId != RoomId ||
                _solutionTrace.MoveInputs.Count < 4 ||
                _solutionTrace.MoveDurationsTicks.Length != _solutionTrace.MoveInputs.Count ||
                !hasSteeringChoice ||
                timedBridgeCount != RequiredGlassBridges)
            {
                string details = _solutionTrace is null
                    ? "trace=null"
                    : $"trace_room='{_solutionTrace.RoomId}', inputs={_solutionTrace.MoveInputs.Count}, durations={_solutionTrace.MoveDurationsTicks.Length}, steering={hasSteeringChoice}, bridges={timedBridgeCount}";
                FailSolutionSmoke($"The Room 06 SolutionTrace or timed-glass route is invalid ({details}).");
            }
        }
    }

    public override void _Process(double delta)
    {
        foreach ((MeshInstance3D band, ProfiledSurfaceBody glass) in _sheenBands)
        {
            band.Visible = !glass.IsBroken;
            if (!band.Visible)
            {
                continue;
            }

            Vector3 position = band.Position;
            position.Z -= 2.0f * (float)delta;
            float minimumZ = band.GetMeta("sheen_minimum_z").AsSingle();
            float wrapDistance = band.GetMeta("sheen_wrap_distance").AsSingle();
            if (position.Z < minimumZ)
            {
                position.Z += wrapDistance;
            }

            band.Position = position;
        }

        if (_runPreview && ++_previewFrames >= 30)
        {
            string capturePath = ProjectSettings.GlobalizePath("user://room06-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(capturePath);
            GD.Print($"ROOM06_PREVIEW_CAPTURE: {capturePath}");
            GetTree().Quit(0);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_runShellSmoke)
        {
            RunShellSmokeTick();
            return;
        }

        UpdateGlassProgress();

        if (_runSolutionSmoke)
        {
            RunSolutionTick();
            return;
        }

        if (_player.GlobalPosition.Y < -7.0f)
        {
            RestartRoom();
        }
    }

    public override void RestartRoom()
    {
        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _player.ResetTo(_spawnTransform);
        ResetRouteState();
    }

    private void ResetRouteState()
    {
        _touchedGlassThisRun = false;
        _maintainedGlassMomentumThisRun = false;
        _passedMomentumGateThisRun = false;
        _routeProgress = 0;
        Array.Fill(_crossedGlassBridges, false);
        _momentumGate.ResetGate();
        _returnShutter.Hide();
        _returnShutterCollision.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
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
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 06 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 06 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void RunSolutionTick()
    {
        if (_solutionTrace is null)
        {
            return;
        }

        if (IsComplete)
        {
            if (!_touchedGlassThisRun ||
                !_maintainedGlassMomentumThisRun ||
                !_passedMomentumGateThisRun ||
                _routeProgress != RequiredGlassBridges ||
                _crossedGlassBridges.Any(crossed => !crossed) ||
                _momentumGate.LastEntrySpeed < 4.5f ||
                _momentumGate.LastExitSpeed < 15.5f)
            {
                FailSolutionSmoke(
                    $"Run {_solutionRun + 1} bypassed the one-shot ring or timed-glass route; " +
                    $"gate={_passedMomentumGateThisRun}, speed={_momentumGate.LastEntrySpeed:F2}->{_momentumGate.LastExitSpeed:F2}, " +
                    $"glass={_touchedGlassThisRun}, momentum={_maintainedGlassMomentumThisRun}, route={_routeProgress}/{RequiredGlassBridges}.");
                return;
            }

            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print(
                    $"ROOM06_SOLUTION_PASS: SolutionTrace used the one-shot ring and crossed all {RequiredGlassBridges} timed glass bridges for {_solutionRun} consecutive completions.");
                GetTree().Quit(0);
                return;
            }

            ClearCompletionState();
            _player.ResetTo(_spawnTransform);
            _solutionTick = 0;
            ResetRouteState();
            return;
        }

        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke(
                $"Run {_solutionRun + 1} timed out at position {_player.GlobalPosition}; " +
                $"surface={_player.GroundSurfaceKind}, gate={_passedMomentumGateThisRun}, route={_routeProgress}/{RequiredGlassBridges}.");
            return;
        }

        _player.SimulatedMoveInput = ResolveTraceInput(_solutionTick - 1);
    }

    private void UpdateGlassProgress()
    {
        if (_player.GroundSurfaceKind != SurfaceKind.Frictionless)
        {
            return;
        }

        _touchedGlassThisRun = true;
        if (_player.LinearVelocity.Slide(Vector3.Up).Length() >= 7.0f)
        {
            _maintainedGlassMomentumThisRun = true;
        }
    }

    private Vector2 ResolveTraceInput(int tick)
    {
        if (_solutionTrace is null)
        {
            return Vector2.Zero;
        }

        int remaining = tick;
        for (int index = 0; index < _solutionTrace.MoveInputs.Count; index++)
        {
            int duration = _solutionTrace.MoveDurationsTicks[index];
            if (remaining < duration)
            {
                return _solutionTrace.MoveInputs[index];
            }

            remaining -= duration;
        }

        return _solutionTrace.HoldLastInput ? _solutionTrace.MoveInputs[^1] : Vector2.Zero;
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string glassTexture = "res://assets/textures/frictionless_glass.svg";
        Color paleSteel = new("aab4b7");
        Color blueFrame = new("506c78");
        SurfaceProfile frictionless = GD.Load<SurfaceProfile>(SurfacePath);

        RoomGeometry.AddClosedRoomShell(
            this,
            "RoomShell",
            new Vector3(0.0f, 0.0f, -20.0f),
            new Vector2(20.0f, 125.55f),
            -2.0f,
            14.5f,
            metal,
            new Color("77858a"),
            new Color("7e6172"),
            body =>
            {
                if (body is PlayerBall)
                {
                    RestartRoom();
                }
            });

        RoomGeometry.AddBox(this, "SafeStart", new Vector3(10.0f, 0.5f, 15.0f), new Vector3(0.0f, 2.75f, 35.275f), Vector3.Zero, metal, paleSteel, 0.4f, 0.66f);
        AddFlatRails("Start", 0.0f, 10.0f, 35.275f, 15.0f, 3.0f, metal, blueFrame);

        Vector3 launchRotation = new(Mathf.DegToRad(8.0f), 0.0f, 0.0f);
        Vector3 launchCenter = new(0.0f, 3.445f, 22.79f);
        RoomGeometry.AddBox(this, "LaunchSlope", new Vector3(10.0f, 0.5f, 10.0f), launchCenter, launchRotation, metal, paleSteel, 0.4f, 0.66f);
        AddSlopedRails("Launch", launchCenter, 10.0f, 10.0f, launchRotation, metal, blueFrame);

        _momentumGate = new FlightGate3D
        {
            Name = "GlassMomentumGate",
            Position = new Vector3(0.0f, 4.72f, 16.0f),
            Radius = 1.9f,
            MinimumExitSpeed = 16.5f,
            SpeedGain = 8.0f,
            SpeedMultiplier = 1.8f,
            MaximumExitSpeed = 16.5f,
            AxialBoostOnly = true,
            MaximumDownwardExitSpeed = 2.0f,
            FrameTint = new Color("725f50"),
            EnableAudio = !OS.GetCmdlineUserArgs().Contains("--room06-solution-smoke"),
        };
        _momentumGate.Passed += player =>
        {
            if (player == _player)
            {
                _passedMomentumGateThisRun = true;
            }
        };
        AddChild(_momentumGate);

        AddTimedGlassBridge("TimedGlassBridge01", 0.0f, 6.0f, -3.0f, 15.0f, frictionless, glassTexture, blueFrame, true, true);
        AddDecisionIsland("DecisionIsland01", -14.0f, 7.0f, metal, paleSteel, blueFrame);
        AddTimedGlassBridge("TimedGlassBridge02", -3.1f, 5.5f, -25.75f, 16.5f, frictionless, glassTexture, blueFrame, true, false);
        AddDecisionIsland("DecisionIsland02", -37.75f, 7.5f, metal, paleSteel, blueFrame);
        AddTimedGlassBridge("TimedGlassBridge03", 3.1f, 5.5f, -50.25f, 17.5f, frictionless, glassTexture, blueFrame, false, true);
        AddDecisionIsland("DecisionIsland03", -63.0f, 8.0f, metal, paleSteel, blueFrame);
        AddTimedGlassBridge("TimedGlassBridge04", 0.0f, 5.5f, -70.75f, 7.5f, frictionless, glassTexture, blueFrame, true, true);

        RoomGeometry.AddBox(this, "ExitDeck", new Vector3(12.0f, 0.5f, 8.275f), new Vector3(0.0f, 1.25f, -78.6375f), Vector3.Zero, metal, paleSteel.Darkened(0.04f), 0.4f, 0.68f);
        AddFlatRails("Exit", 0.0f, 12.0f, -78.6375f, 8.275f, 1.5f, metal, blueFrame);

        AddRouteSensor("GlassRoute01", 0, new Vector3(0.0f, 2.15f, -4.0f), new Vector3(5.4f, 2.8f, 2.0f));
        AddRouteSensor("GlassRoute02", 1, new Vector3(-3.1f, 2.15f, -29.0f), new Vector3(4.9f, 2.8f, 2.0f));
        AddRouteSensor("GlassRoute03", 2, new Vector3(3.1f, 2.15f, -54.0f), new Vector3(4.9f, 2.8f, 2.0f));
        AddRouteSensor("GlassRoute04", 3, new Vector3(0.0f, 2.15f, -72.0f), new Vector3(4.9f, 2.8f, 2.0f));

        _returnShutter = RoomGeometry.AddBox(
            this,
            "SpentRingReturnShutter",
            new Vector3(6.4f, 4.5f, 0.45f),
            new Vector3(0.0f, 2.0f, 4.25f),
            Vector3.Zero,
            metal,
            blueFrame.Darkened(0.22f),
            0.48f,
            0.6f);
        _returnShutterCollision = _returnShutter.GetChildren().OfType<CollisionShape3D>().First();
        _returnShutter.Hide();
        _returnShutterCollision.Disabled = true;

        SurfaceDetail.AddOverlay(this, "StartScuffs", new Vector3(-1.6f, 3.015f, 37.0f), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(8.0f)), new Vector2(3.0f, 1.8f), "res://assets/textures/overlays/edge_scuffs.svg", new Color("e0e3df"), 0.3f);
        SurfaceDetail.AddOverlay(this, "ExitScratches", new Vector3(2.0f, 1.515f, -79.0f), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(-17.0f)), new Vector2(3.4f, 2.0f), "res://assets/textures/overlays/scratches.svg", new Color("d5dfdc"), 0.35f);
    }

    private void AddTimedGlassBridge(
        string name,
        float centerX,
        float width,
        float centerZ,
        float length,
        SurfaceProfile profile,
        string texture,
        Color frameTint,
        bool addLeftRail,
        bool addRightRail)
    {
        StaticBody3D bridge = RoomGeometry.AddBox(
            this,
            name,
            new Vector3(width, 0.5f, length),
            new Vector3(centerX, 1.25f, centerZ),
            Vector3.Zero,
            texture,
            new Color("b2d3dc"),
            0.08f,
            0.2f,
            friction: profile.Friction,
            surfaceProfile: profile);
        bridge.AddToGroup("timed_breakable_glass");
        bridge.SetMeta("break_delay_seconds", ProfiledSurfaceBody.DefaultGlassBreakDelaySeconds);

        MeshInstance3D visual = bridge.GetChildren().OfType<MeshInstance3D>().First();
        StandardMaterial3D material = (StandardMaterial3D)visual.MaterialOverride;
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        material.AlbedoColor = new Color(0.56f, 0.76f, 0.82f, 0.74f);

        AddFlatRails(name, centerX, width, centerZ, length, 1.5f, "res://assets/textures/brushed_metal.png", frameTint, addLeftRail, addRightRail);
        AddGlassUnderframe(name, centerX, width, centerZ, length, frameTint);
        AddSheenBand(name, (ProfiledSurfaceBody)bridge, centerX, width, centerZ, length);
    }

    private void AddDecisionIsland(string name, float centerZ, float length, string texture, Color tint, Color frameTint)
    {
        RoomGeometry.AddBox(this, name, new Vector3(12.0f, 0.5f, length), new Vector3(0.0f, 1.25f, centerZ), Vector3.Zero, texture, tint, 0.4f, 0.66f);
        AddFlatRails(name, 0.0f, 12.0f, centerZ, length, 1.5f, texture, frameTint);
    }

    private void AddFlatRails(
        string prefix,
        float centerX,
        float width,
        float centerZ,
        float length,
        float surfaceTopY,
        string texture,
        Color tint,
        bool addLeftRail = true,
        bool addRightRail = true)
    {
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            if ((side < 0.0f && !addLeftRail) || (side > 0.0f && !addRightRail))
            {
                continue;
            }

            RoomGeometry.AddBox(
                this,
                $"{prefix}Rail{(side < 0.0f ? "Left" : "Right")}",
                new Vector3(0.36f, 1.0f, length),
                new Vector3(centerX + (side * ((width * 0.5f) + 0.18f)), surfaceTopY + 0.5f, centerZ),
                Vector3.Zero,
                texture,
                tint,
                0.42f,
                0.62f);
        }
    }

    private void AddSlopedRails(string prefix, Vector3 surfaceCenter, float width, float length, Vector3 rotation, string texture, Color tint)
    {
        Basis basis = Basis.FromEuler(rotation);
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            Vector3 position = surfaceCenter + (basis * new Vector3(side * ((width * 0.5f) + 0.18f), 0.75f, 0.0f));
            RoomGeometry.AddBox(
                this,
                $"{prefix}Rail{(side < 0.0f ? "Left" : "Right")}",
                new Vector3(0.36f, 1.0f, length),
                position,
                rotation,
                texture,
                tint,
                0.42f,
                0.62f);
        }
    }

    private void AddGlassUnderframe(string prefix, float centerX, float width, float centerZ, float length, Color tint)
    {
        StandardMaterial3D material = RoomGeometry.CreateMaterial("res://assets/textures/brushed_metal.png", tint, 0.42f, 0.62f);
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddVisualBox(
                this,
                $"{prefix}Underframe{(side < 0.0f ? "Left" : "Right")}",
                new Vector3(0.18f, 0.16f, length),
                new Vector3(centerX + (side * ((width * 0.5f) - 0.22f)), 0.91f, centerZ),
                Vector3.Zero,
                string.Empty,
                Colors.White,
                0.0f,
                1.0f,
                material);
        }
    }

    private void AddSheenBand(
        string prefix,
        ProfiledSurfaceBody glass,
        float centerX,
        float width,
        float centerZ,
        float length)
    {
        StandardMaterial3D sheen = new()
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.82f, 0.96f, 1.0f, 0.12f),
            EmissionEnabled = true,
            Emission = new Color("9bdde7"),
            EmissionEnergyMultiplier = 0.24f,
        };
        MeshInstance3D band = RoomGeometry.AddVisualBox(
            this,
            $"{prefix}MovingSheen",
            new Vector3(width - 0.5f, 0.025f, 0.58f),
            new Vector3(centerX, 1.515f, centerZ + (length * 0.32f)),
            Vector3.Zero,
            string.Empty,
            Colors.White,
            0.0f,
            1.0f,
            sheen);
        band.SetMeta("sheen_minimum_z", centerZ - (length * 0.45f));
        band.SetMeta("sheen_wrap_distance", length * 0.9f);
        _sheenBands.Add((band, glass));
    }

    private void AddRouteSensor(string name, int index, Vector3 position, Vector3 size)
    {
        Area3D sensor = new()
        {
            Name = name,
            Position = position,
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true,
            Monitorable = false,
        };
        sensor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        sensor.BodyEntered += body =>
        {
            if (body != _player || index != _routeProgress)
            {
                return;
            }

            _crossedGlassBridges[index] = true;
            _routeProgress++;
            if (index == 0)
            {
                _returnShutter.Show();
                _returnShutterCollision.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
            }
        };
        AddChild(sensor);
    }

    private void BuildGoal()
    {
        Vector3 goalPosition = new(0.0f, 2.35f, -80.2f);
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
            Shape = new CylinderShape3D { Radius = 1.55f, Height = 2.5f },
        });
        goal.BodyEntered += body =>
        {
            if (body is PlayerBall player &&
                _passedMomentumGateThisRun &&
                _momentumGate.LastExitSpeed >= 15.5f &&
                _touchedGlassThisRun &&
                _maintainedGlassMomentumThisRun &&
                _routeProgress == RequiredGlassBridges &&
                _crossedGlassBridges.All(crossed => crossed))
            {
                if (!player.TouchedSideBoundarySinceReset)
                {
                    MarkAdvancementCondition("straight-as-glass");
                }
                CompleteRoom();
            }
        };
        AddChild(goal);

        RoomGeometry.AddGoalExitDoor(this, goalPosition);
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM06_SOLUTION_FAIL: {message}");
        GetTree().Quit(1);
    }
}
