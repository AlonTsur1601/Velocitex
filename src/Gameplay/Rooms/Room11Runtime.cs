using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Physics;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Physics;
using Velocitex.Gameplay.Player;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room11Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_11_solution.tres";
    private const string SuperElasticSurfacePath = "res://resources/surfaces/super_elastic.tres";
    private const string SuperElasticMaterialPath = "res://resources/materials/super_elastic_membrane.tres";
    private const int RequiredSolutionRuns = 10;
    private const int MaximumSolutionTicksPerRun = 1200;

    private static readonly (Vector3 Position, float Radius)[] AirCourse =
    {
        (new Vector3(-3.0f, 12.5f, -8.0f), 2.3f),
        (new Vector3(3.0f, 21.5f, -35.0f), 2.35f),
        (new Vector3(-3.0f, 24.2f, -67.0f), 2.4f),
        (new Vector3(-3.5f, 24.0f, -98.0f), 2.5f),
    };

    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private ForceVolume3D _lowGravityVolume = null!;
    private Area3D _goal = null!;
    private AudioStreamPlayer3D? _gravityAudio;
    private AudioStreamPlayer3D? _bounceAudio;
    private readonly List<FlightGate3D> _airGates = new();
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private bool _runSolutionSmoke;
    private bool _runPreview;
    private bool _runShellSmoke;
    private bool _runAirControlSmoke;
    private bool _solutionSmokeFinishing;
    private bool _touchedLowGravity;
    private bool _touchedSuperElastic;
    private bool _verifiedBounceAmplification;
    private bool _verifiedAirSteering;
    private bool _verifiedAirControlCleared;
    private bool _verifiedResetCleared;
    private bool _wasInsideLowGravity;
    private int _nextAirGate;
    private int _lastBounceCount;
    private int _solutionRun;
    private int _solutionTick;
    private int _previewFrames;
    private int _shellSmokeTick;
    private int _airControlSmokeTick;
    private float _maximumLateralAirSpeed;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] arguments = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(arguments, argument => argument == "--room11-solution-smoke");
        _runPreview = Array.Exists(arguments, argument => argument == "--room11-preview");
        _runShellSmoke = Array.Exists(arguments, argument => argument == "--room-shell-smoke");
        _runAirControlSmoke = Array.Exists(arguments, argument => argument == "--room11-air-control-smoke");

        BuildRoom();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room11_a", new Vector3(9.0f, 17.0f, 32.0f), new Vector3(0.0f, 15.0f, -58.0f), 58.0f),
            new("room11_b", new Vector3(-10.0f, 21.0f, -91.0f), new Vector3(0.0f, 12.0f, -151.0f), 58.0f),
        });

        _player = GetNode<PlayerBall>("Player");
        _cameraRig = GetNode<PlayerCameraRig>("CameraRig");
        _spawnTransform = _player.GlobalTransform;
        _cameraRig.Follow(_player);
        _player.MovementBasis = _cameraRig.MovementBasis;
        _verifiedResetCleared = _player.AirControlAcceleration <= 0.001f;
        if (_runPreview || _runAirControlSmoke)
        {
            _cameraRig.SetInputEnabled(false);
        }

        _lowGravityVolume.RigidBodyEntered += OnLowGravityEntered;
        _lowGravityVolume.RigidBodyExited += OnLowGravityExited;

        if (_runSolutionSmoke)
        {
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            if (_solutionTrace is null ||
                _solutionTrace.RoomId != RoomId ||
                _solutionTrace.MoveInputs.Count < 5 ||
                _solutionTrace.MoveDurationsTicks.Length != _solutionTrace.MoveInputs.Count ||
                !_solutionTrace.MoveInputs.Any(input => input.X < -0.5f) ||
                !_solutionTrace.MoveInputs.Any(input => input.X > 0.5f))
            {
                FailSolutionSmoke("The Room 11 SolutionTrace must contain deliberate left and right low-gravity steering segments.");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_runPreview && ++_previewFrames >= 30)
        {
            string capturePath = ProjectSettings.GlobalizePath("user://room11-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(capturePath);
            GD.Print($"ROOM11_PREVIEW_CAPTURE: {capturePath}");
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

        if (_runAirControlSmoke)
        {
            RunAirControlSmokeTick();
            return;
        }

        if (_runSolutionSmoke)
        {
            RunSolutionTick();
            return;
        }

        TrackRequiredMechanics();
        TryCompleteGoal();
        if (_player.GlobalPosition.Y < -7.0f)
        {
            RestartRoom();
        }
    }

    public override void RestartRoom()
    {
        if (_runSolutionSmoke && _solutionTick > 0 && !_solutionSmokeFinishing)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} hit a hazard at {_player.GlobalPosition} with velocity {_player.LinearVelocity}; gates={_nextAirGate}/{_airGates.Count}.");
            return;
        }

        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        ResetDevices();
        _player.ResetTo(_spawnTransform);
        ResetRouteState();
        _verifiedResetCleared = _player.AirControlAcceleration <= 0.001f;
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

        if (_player.GlobalPosition.DistanceTo(_spawnTransform.Origin) > 0.15f || _player.AirControlAcceleration > 0.001f)
        {
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 11 hazard floor did not restart the player with air control cleared.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 11 hazard floor restarted the player and cleared low-gravity air control.");
        GetTree().Quit(0);
    }

    private void RunAirControlSmokeTick()
    {
        _airControlSmokeTick++;
        switch (_airControlSmokeTick)
        {
            case 1:
                _player.GravityScale = 0.0f;
                _player.ResetTo(new Transform3D(Basis.Identity, new Vector3(0.0f, 12.0f, -20.0f)));
                break;
            case 8:
                if (!_lowGravityVolume.ContainsBody(_player) || _player.AirControlAcceleration <= 0.001f)
                {
                    FinishAirControlSmoke(1, "Low-gravity volume did not enable air control while the player was inside.");
                    return;
                }
                _player.ResetTo(new Transform3D(Basis.Identity, new Vector3(0.0f, 12.0f, 18.0f)));
                break;
            case 15:
                if (_lowGravityVolume.ContainsBody(_player) || _player.AirControlAcceleration > 0.001f)
                {
                    FinishAirControlSmoke(1, "Air control remained active after leaving the low-gravity volume.");
                    return;
                }
                _player.ResetTo(new Transform3D(Basis.Identity, new Vector3(0.0f, 12.0f, -20.0f)));
                break;
            case 22:
                if (_player.AirControlAcceleration <= 0.001f)
                {
                    FinishAirControlSmoke(1, "Low-gravity air control did not reactivate on re-entry.");
                    return;
                }
                RestartRoom();
                break;
            case 23:
                if (_player.AirControlAcceleration > 0.001f || _player.GlobalPosition.DistanceTo(_spawnTransform.Origin) > 0.15f)
                {
                    FinishAirControlSmoke(1, "Respawn did not immediately clear air control and restore the spawn transform.");
                    return;
                }
                FinishAirControlSmoke(0, "Low-gravity air control enabled only inside the volume and cleared on exit and respawn.");
                break;
        }
    }

    private async void FinishAirControlSmoke(int exitCode, string message)
    {
        if (_solutionSmokeFinishing)
        {
            return;
        }
        _solutionSmokeFinishing = true;
        if (exitCode == 0)
        {
            GD.Print($"ROOM11_AIR_CONTROL_PASS: {message}");
        }
        else
        {
            GD.PushError($"ROOM11_AIR_CONTROL_FAIL: {message}");
        }
        _player.GravityScale = 1.0f;
        _player.SimulatedMoveInput = null;
        ResetDevices();
        if (_gravityAudio is not null)
        {
            _gravityAudio.Stream = null;
        }
        if (_bounceAudio is not null)
        {
            _bounceAudio.Stream = null;
        }
        StopAndReleaseAudio(this);
        for (int frame = 0; frame < 12; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GetTree().Quit(exitCode);
    }

    private static void StopAndReleaseAudio(Node node)
    {
        if (node is AudioStreamPlayer player)
        {
            player.Stop();
            player.Stream = null;
        }
        else if (node is AudioStreamPlayer3D player3D)
        {
            player3D.Stop();
            player3D.Stream = null;
        }

        foreach (Node child in node.GetChildren())
        {
            StopAndReleaseAudio(child);
        }
    }

    private void RunSolutionTick()
    {
        if (_solutionTrace is null || _solutionSmokeFinishing)
        {
            return;
        }

        TrackRequiredMechanics();
        TryCompleteGoal();
        if (IsComplete)
        {
            if (!RouteRequirementsMet())
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} bypassed the intended route: {DescribeEvidence()}.");
                return;
            }

            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print($"ROOM11_SOLUTION_PASS: SolutionTrace used the canonical SuperElastic launch, steered through all {_airGates.Count} low-gravity rings, and restored normal no-air-control behavior for {_solutionRun} consecutive completions.");
                FinishSolutionSmoke(0);
                return;
            }

            ClearCompletionState();
            ResetDevices();
            _player.ResetTo(_spawnTransform);
            _solutionTick = 0;
            ResetRouteState();
            _verifiedResetCleared = _player.AirControlAcceleration <= 0.001f;
            return;
        }

        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} timed out at {_player.GlobalPosition}, velocity={_player.LinearVelocity}; {DescribeEvidence()}.");
            return;
        }

        _player.SimulatedMoveInput = ResolveTraceInput(_solutionTick - 1);
        if (_solutionTick % 30 == 0)
        {
            GD.Print($"ROOM11_TRACE: tick={_solutionTick}, position={_player.GlobalPosition}, velocity={_player.LinearVelocity}, grounded={_player.IsGrounded}, air_control={_player.AirControlAcceleration:F2}, gates={_nextAirGate}/{_airGates.Count}, bounces={_player.SuperElasticBounceCount}.");
        }
    }

    private void TrackRequiredMechanics()
    {
        bool inside = _lowGravityVolume.ContainsBody(_player);
        if (inside)
        {
            _touchedLowGravity = true;
            if (!_player.IsGrounded && _player.AirControlAcceleration > 0.001f)
            {
                _maximumLateralAirSpeed = Mathf.Max(_maximumLateralAirSpeed, Mathf.Abs(_player.LinearVelocity.X));
                _verifiedAirSteering |= _maximumLateralAirSpeed >= 1.8f;
            }
        }
        else if (_wasInsideLowGravity && _player.GlobalPosition.Z < -120.0f)
        {
            _verifiedAirControlCleared = _player.AirControlAcceleration <= 0.001f;
        }
        _wasInsideLowGravity = inside;

        if (_player.SuperElasticBounceCount > _lastBounceCount)
        {
            _lastBounceCount = _player.SuperElasticBounceCount;
            _touchedSuperElastic = true;
            _verifiedBounceAmplification = _player.LastSuperElasticLaunchSpeed > _player.LastSuperElasticImpactSpeed + 1.0f;
            _bounceAudio?.Play();
        }
    }

    private bool RouteRequirementsMet() =>
        _touchedLowGravity &&
        _touchedSuperElastic &&
        _verifiedBounceAmplification &&
        _verifiedAirSteering &&
        _verifiedAirControlCleared &&
        _verifiedResetCleared &&
        _nextAirGate == _airGates.Count;

    private string DescribeEvidence() =>
        $"low={_touchedLowGravity}, elastic={_touchedSuperElastic}, amplified={_verifiedBounceAmplification}, steering={_verifiedAirSteering}, cleared={_verifiedAirControlCleared}, reset_cleared={_verifiedResetCleared}, gates={_nextAirGate}/{_airGates.Count}, lateral_speed={_maximumLateralAirSpeed:F2}";

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

    private void ResetDevices()
    {
        foreach (FlightGate3D gate in _airGates)
        {
            gate.ResetGate();
        }
        _gravityAudio?.Stop();
        _bounceAudio?.Stop();
    }

    private void ResetRouteState()
    {
        _touchedLowGravity = false;
        _touchedSuperElastic = false;
        _verifiedBounceAmplification = false;
        _verifiedAirSteering = false;
        _verifiedAirControlCleared = false;
        _wasInsideLowGravity = false;
        _nextAirGate = 0;
        _lastBounceCount = 0;
        _maximumLateralAirSpeed = 0.0f;
    }

    private void OnLowGravityEntered(RigidBody3D body)
    {
        if (body != _player)
        {
            return;
        }
        _touchedLowGravity = true;
        if (_gravityAudio is not null)
        {
            _gravityAudio.GlobalPosition = _player.GlobalPosition;
            _gravityAudio.Play();
        }
    }

    private void OnLowGravityExited(RigidBody3D body)
    {
        if (body == _player && _player.GlobalPosition.Z < -120.0f)
        {
            _verifiedAirControlCleared = _player.AirControlAcceleration <= 0.001f;
        }
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string copper = "res://assets/textures/copper_rivets.svg";
        Color paleSteel = new("aebcb7");
        Color darkFrame = new("405e59");

        RoomGeometry.AddClosedRoomShell(
            this,
            "RoomShell",
            new Vector3(0.0f, 0.0f, -80.0f),
            new Vector2(28.0f, 240.0f),
            -3.2f,
            36.0f,
            metal,
            new Color("738883"),
            new Color("435653"),
            body =>
            {
                if (body is PlayerBall)
                {
                    RestartRoom();
                }
            });

        RoomGeometry.AddBox(this, "SafeStart", new Vector3(12.0f, 0.5f, 18.775f), new Vector3(0.0f, 9.0f, 30.3875f), Vector3.Zero, metal, paleSteel, 0.4f, 0.66f);

        const float rampAngle = -0.36717384f;
        Vector3 rampRotation = new(rampAngle, 0.0f, 0.0f);
        RoomGeometry.AddBox(this, "MomentumRamp", new Vector3(12.0f, 0.5f, 13.928389f), new Vector3(0.0f, 6.51666f, 14.58972f), rampRotation, copper, new Color("806650"), 0.34f, 0.62f);

        SurfaceProfile superElastic = GD.Load<SurfaceProfile>(SuperElasticSurfacePath);
        ShaderMaterial bounceMaterial = (ShaderMaterial)GD.Load<ShaderMaterial>(SuperElasticMaterialPath).Duplicate();
        StaticBody3D membrane = RoomGeometry.AddBox(
            this,
            "SuperElasticLaunch",
            new Vector3(12.0f, 0.5f, 12.0f),
            new Vector3(0.0f, 2.5f, 0.0f),
            Vector3.Zero,
            string.Empty,
            Colors.White,
            0.0f,
            0.7f,
            friction: superElastic.Friction,
            surfaceProfile: superElastic,
            materialOverride: bounceMaterial);

        RoomGeometry.AddBox(this, "LandingDeck", new Vector3(14.0f, 0.5f, 50.0f), new Vector3(0.0f, 2.75f, -175.0f), Vector3.Zero, metal, paleSteel.Darkened(0.04f), 0.4f, 0.66f);

        AddSideWalls("Start", new Vector3(0.0f, 9.92f, 30.3875f), 18.775f, Vector3.Zero, 6.18f, metal, darkFrame);
        AddSideWalls("Ramp", new Vector3(0.0f, 7.36f, 14.45f), 14.2f, rampRotation, 6.18f, metal, darkFrame);
        AddSideWalls("Landing", new Vector3(0.0f, 3.72f, -175.0f), 50.0f, Vector3.Zero, 7.18f, metal, darkFrame);
        RoomGeometry.AddBox(this, "MembraneRimLeft", new Vector3(0.42f, 0.68f, 12.0f), new Vector3(-6.22f, 2.62f, 0.0f), Vector3.Zero, copper, darkFrame, 0.42f, 0.58f);
        RoomGeometry.AddBox(this, "MembraneRimRight", new Vector3(0.42f, 0.68f, 12.0f), new Vector3(6.22f, 2.62f, 0.0f), Vector3.Zero, copper, darkFrame, 0.42f, 0.58f);

        _lowGravityVolume = new ForceVolume3D
        {
            Name = "LowGravityVolume",
            Position = new Vector3(0.0f, 14.0f, -59.0f),
            CollisionMask = 1,
            Profile = GD.Load<ForceVolumeProfile>("res://resources/force_volumes/low_gravity.tres"),
        };
        _lowGravityVolume.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(24.0f, 30.0f, 132.0f) },
        });
        AddChild(_lowGravityVolume);

        AddGravityParticles();
        for (int index = 0; index < AirCourse.Length; index++)
        {
            AddAirGate(index, AirCourse[index].Position, AirCourse[index].Radius);
        }

        membrane.AddChild(new OmniLight3D
        {
            Name = "MembranePractical",
            Position = new Vector3(0.0f, 1.4f, 0.0f),
            LightColor = new Color("a8dccc"),
            LightEnergy = 1.35f,
            OmniRange = 12.0f,
            ShadowEnabled = false,
        });

        if (!_runSolutionSmoke && !_runAirControlSmoke)
        {
            _gravityAudio = CreateAudio("LowGravityEnterSfx", "res://assets/audio/sfx/force_low_gravity_enter.wav", 34.0f);
            _bounceAudio = CreateAudio("SuperElasticBounceSfx", "res://assets/audio/sfx/surface_super_elastic_bounce.wav", 34.0f);
        }
    }

    private void AddSideWalls(string prefix, Vector3 center, float length, Vector3 rotation, float x, string texture, Color tint)
    {
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(
                this,
                $"{prefix}SideWall{(side < 0.0f ? "Left" : "Right")}",
                new Vector3(0.36f, 1.35f, length),
                new Vector3(side * x, center.Y, center.Z),
                rotation,
                texture,
                tint,
                0.42f,
                0.62f);
        }
    }

    private void AddAirGate(int index, Vector3 position, float radius)
    {
        FlightGate3D gate = new()
        {
            Name = $"LowGravitySteeringRing{index + 1}",
            Position = position,
            Radius = radius,
            FrameTint = index % 2 == 0 ? new Color("7aa99b") : new Color("b18b62"),
            EnableAudio = !_runSolutionSmoke && !_runAirControlSmoke,
            MinimumExitSpeed = 0.0f,
            SpeedGain = 3.0f,
            SpeedMultiplier = 1.0f,
            MaximumExitSpeed = float.PositiveInfinity,
            AxialBoostOnly = true,
        };
        gate.Passed += player =>
        {
            if (player != _player || index != _nextAirGate)
            {
                return;
            }
            _nextAirGate++;
            if (_runSolutionSmoke)
            {
                GD.Print($"ROOM11_RING_PASS: ring={_nextAirGate}/{_airGates.Count}, tick={_solutionTick}, position={player.GlobalPosition}, velocity={player.LinearVelocity}.");
            }
        };
        AddChild(gate);
        _airGates.Add(gate);
    }

    private void AddGravityParticles()
    {
        StandardMaterial3D moteMaterial = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color("bce9d8"),
            EmissionEnabled = true,
            Emission = new Color("6cae9c"),
        };
        ParticleProcessMaterial process = new()
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(11.0f, 13.0f, 64.0f),
            Direction = Vector3.Up,
            Spread = 18.0f,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 0.35f,
            InitialVelocityMax = 0.8f,
            ScaleMin = 0.45f,
            ScaleMax = 1.0f,
        };
        AddChild(new GpuParticles3D
        {
            Name = "FloatingMotes",
            Position = new Vector3(0.0f, 14.0f, -59.0f),
            Amount = 82,
            Lifetime = 7.0,
            Randomness = 0.8f,
            LocalCoords = true,
            ProcessMaterial = process,
            DrawPass1 = new SphereMesh { Radius = 0.055f, Height = 0.11f, RadialSegments = 8, Rings = 4, Material = moteMaterial },
        });
    }

    private AudioStreamPlayer3D CreateAudio(string name, string path, float maxDistance)
    {
        AudioStreamPlayer3D audio = new()
        {
            Name = name,
            Stream = GD.Load<AudioStream>(path),
            Bus = "SFX",
            MaxDistance = maxDistance,
            UnitSize = 8.0f,
        };
        AddChild(audio);
        return audio;
    }

    private void BuildGoal()
    {
        Vector3 goalPosition = new(0.0f, 3.9f, -196.4f);
        _goal = new Area3D
        {
            Name = "GoalCup",
            Position = goalPosition,
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true,
        };
        _goal.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 1.65f, Height = 2.7f } });
        _goal.BodyEntered += body =>
        {
            if (body is PlayerBall)
            {
                TryCompleteGoal();
            }
        };
        AddChild(_goal);
        RoomGeometry.AddGoalExitDoor(this, goalPosition);
    }

    private void TryCompleteGoal()
    {
        if (RouteRequirementsMet() && _goal.GetOverlappingBodies().Contains(_player))
        {
            CompleteRoom();
        }
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM11_SOLUTION_FAIL: {message}");
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
        ResetDevices();
        _gravityAudio?.SetStream(null);
        _bounceAudio?.SetStream(null);
        _solutionTrace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(exitCode);
    }
}
