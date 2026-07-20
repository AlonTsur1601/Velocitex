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

public partial class Room19Runtime : RoomRuntime
{
    private const float ParkedPistonPitchDegrees = -18.0f;
    private const float ArmedPistonPitchDegrees = -42.0f;
    private const float OriginalLandingShift = 8.0f;
    private const float LandingShift = 55.0f;
    private const float LandingAdditionalLength = LandingShift - OriginalLandingShift;
    private const string TracePath = "res://resources/solutions/room_19_solution.tres";
    private const byte InteractAction = 1;
    private const int RequiredSolutionRuns = 10;
    private const int MaximumSolutionTicksPerRun = 900;

    private readonly List<ForceVolume3D> _magneticFields = new();
    private readonly List<RouteCheckpoint3D> _trajectoryPlates = new();
    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private MomentumPiston3D _piston = null!;
    private ForceVolume3D _lowGravityVolume = null!;
    private MechanicalLever _trajectoryLever = null!;
    private Area3D _goal = null!;
    private CollisionShape3D _trajectoryBarrierCollision = null!;
    private MeshInstance3D _trajectoryBarrierVisual = null!;
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private bool _pistonArmed;
    private bool _trajectoryLeverActivated;
    private bool _pistonFired;
    private float _launchSpeed;
    private bool _showInteractionPrompts;
    private bool _highContrastPrompts;
    private bool _runSolutionSmoke;
    private bool _runPreview;
    private bool _runShellSmoke;
    private bool _runMechanicsSmoke;
    private bool _runAchievementPositiveSmoke;
    private bool _runAchievementNegativeSmoke;
    private bool _solutionSmokeFinishing;
    private int _solutionRun;
    private int _solutionTick;
    private int _previewFrames;
    private int _shellSmokeTick;
    private int _nextMagneticField;
    private bool _touchedLowGravity;
    private bool _airControlCleared;
    private int _nextTrajectoryPlate;
    private int _leverActivationTick = -1;
    private bool _pistonPerfectEligible;
    private float _minimumGoalDistance = float.PositiveInfinity;
    private Vector3 _closestGoalPosition;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] args = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(args, argument => argument == "--room19-solution-smoke");
        _runPreview = Array.Exists(args, argument => argument == "--room19-preview");
        _runShellSmoke = Array.Exists(args, argument => argument == "--room-shell-smoke");
        _runMechanicsSmoke = Array.Exists(args, argument => argument == "--room19-mechanics-smoke");
        _runAchievementPositiveSmoke = Array.Exists(args, argument => argument == "--room19-achievement-positive-smoke");
        _runAchievementNegativeSmoke = Array.Exists(args, argument => argument == "--room19-achievement-negative-smoke");
        BuildRoom();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room19_a", new Vector3(9.0f, 14.0f, 30.0f), new Vector3(0.0f, 12.0f, -31.0f), 58.0f),
            new("room19_b", new Vector3(-10.5f, 13.5f, 10.0f), new Vector3(0.0f, 7.0f, -4.0f), 61.0f),
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
        _trajectoryLever.SetKeyLabel(interactKey == Key.None ? "E" : interactKey.ToString());
        if (_runPreview)
        {
            _cameraRig.SetInputEnabled(false);
        }

        _piston.Armed += player => _pistonArmed |= player == _player;
        _piston.Fired += body =>
        {
            if (body == _player)
            {
                _pistonFired = true;
                _launchSpeed = body.LinearVelocity.Length();
                _pistonPerfectEligible = _leverActivationTick >= 0 && _leverActivationTick <= 120 && _nextTrajectoryPlate == _trajectoryPlates.Count;
                TryAwardPistonPerfect();
            }
        };

        if (_runSolutionSmoke)
        {
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            if (_solutionTrace is null || _solutionTrace.RoomId != RoomId || _solutionTrace.MoveInputs.Count == 0 ||
                _solutionTrace.ActionFlags.Length != _solutionTrace.MoveInputs.Count || !_solutionTrace.ActionFlags.Contains(InteractAction))
            {
                FailSolutionSmoke("The Room 19 SolutionTrace is invalid.");
            }
        }

        if (_runMechanicsSmoke || _runAchievementPositiveSmoke || _runAchievementNegativeSmoke)
        {
            CallDeferred(MethodName.RunRequestedSmoke);
        }
    }

    public override void _Process(double delta)
    {
        if (_runPreview && ++_previewFrames >= 30)
        {
            string path = ProjectSettings.GlobalizePath("user://room19-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"ROOM19_PREVIEW_CAPTURE: {path}");
            GetTree().Quit(0);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_leverActivationTick >= 0)
        {
            _leverActivationTick++;
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
        if (_runMechanicsSmoke || _runAchievementPositiveSmoke || _runAchievementNegativeSmoke)
        {
            return;
        }
        bool canInteract = _trajectoryLever.CanInteract(_player);
        bool focused = canInteract && _cameraRig.IsLookingAt(_trajectoryLever.GlobalPosition + (Vector3.Up * 1.75f));
        _trajectoryLever.SetFocused(focused && _showInteractionPrompts, _highContrastPrompts);
        if (focused && Godot.Input.IsActionJustPressed(InputDefaults.Interact))
        {
            _trajectoryLever.Interact(_player);
        }
        if (_player.GlobalPosition.Y < -7.0f)
        {
            RestartRoom();
        }
    }

    public override void RestartRoom()
    {
        if (_runSolutionSmoke && _solutionTick > 0 && !_solutionSmokeFinishing)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} hit the maintenance floor at {_player.GlobalPosition}; plates={_nextTrajectoryPlate}/{_trajectoryPlates.Count}, lever={_trajectoryLeverActivated}, armed={_pistonArmed}, fired={_pistonFired}, launch={_launchSpeed:F2}, closest_goal={_minimumGoalDistance:F2} at {_closestGoalPosition}.");
            return;
        }
        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _piston.ResetPiston();
        _trajectoryLever.ResetLever();
        _player.ResetTo(_spawnTransform);
        ResetRunState();
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
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 19 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }
        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 19 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void RunSolutionTick()
    {
        if (_solutionTrace is null || _solutionSmokeFinishing) { return; }
        float goalDistance = _player.GlobalPosition.DistanceTo(_goal.GlobalPosition);
        if (goalDistance < _minimumGoalDistance)
        {
            _minimumGoalDistance = goalDistance;
            _closestGoalPosition = _player.GlobalPosition;
        }
        if (IsComplete)
        {
            if (_nextTrajectoryPlate != _trajectoryPlates.Count || !_trajectoryLeverActivated || !_pistonArmed || !_pistonFired || _launchSpeed < 19.5f ||
                !_touchedLowGravity || _nextMagneticField != _magneticFields.Count || !_airControlCleared ||
                !CompletedAdvancementIds.Contains("piston-perfect"))
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} bypassed the piston or its achievement: plates={_nextTrajectoryPlate}/{_trajectoryPlates.Count}, armed={_pistonArmed}, fired={_pistonFired}, launch={_launchSpeed:F2}, timed={_pistonPerfectEligible}.");
                return;
            }
            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print($"ROOM19_SOLUTION_PASS: SolutionTrace used the angled piston at {_launchSpeed:F2} m/s for {_solutionRun} consecutive completions.");
                FinishSolutionSmoke(0);
                return;
            }
            ClearCompletionState();
            _piston.ResetPiston();
            _trajectoryLever.ResetLever();
            _player.ResetTo(_spawnTransform);
            _solutionTick = 0;
            ResetRunState();
            return;
        }
        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} timed out at {_player.GlobalPosition}; plates={_nextTrajectoryPlate}/{_trajectoryPlates.Count}, lever={_trajectoryLeverActivated}, armed={_pistonArmed}, fired={_pistonFired}, low_gravity={_touchedLowGravity}, magnets={_nextMagneticField}/{_magneticFields.Count}, air_cleared={_airControlCleared}, launch={_launchSpeed:F2}, closest_goal={_minimumGoalDistance:F2} at {_closestGoalPosition}.");
            return;
        }
        (Vector2 moveInput, byte actionFlags) = ResolveTraceStep(_solutionTick - 1);
        _player.SimulatedMoveInput = moveInput;
        if ((actionFlags & InteractAction) != 0)
        {
            _trajectoryLever.Interact(_player);
        }
    }

    private (Vector2 MoveInput, byte ActionFlags) ResolveTraceStep(int tick)
    {
        if (_solutionTrace is null) { return (Vector2.Zero, (byte)0); }
        int remaining = tick;
        for (int index = 0; index < _solutionTrace.MoveInputs.Count; index++)
        {
            int duration = _solutionTrace.MoveDurationsTicks[index];
            if (remaining < duration) { return (_solutionTrace.MoveInputs[index], _solutionTrace.ActionFlags[index]); }
            remaining -= duration;
        }
        return _solutionTrace.HoldLastInput ? (_solutionTrace.MoveInputs[^1], _solutionTrace.ActionFlags[^1]) : (Vector2.Zero, (byte)0);
    }

    private void ResetRunState()
    {
        _pistonArmed = false;
        _trajectoryLeverActivated = false;
        _pistonFired = false;
        _launchSpeed = 0.0f;
        _nextMagneticField = 0;
        _touchedLowGravity = false;
        _airControlCleared = false;
        _nextTrajectoryPlate = 0;
        _leverActivationTick = -1;
        _pistonPerfectEligible = false;
        SetPistonPitch(ParkedPistonPitchDegrees);
        CloseTrajectoryBarrier();
        _minimumGoalDistance = float.PositiveInfinity;
        _closestGoalPosition = Vector3.Zero;
        foreach (RouteCheckpoint3D plate in _trajectoryPlates)
        {
            plate.ResetCheckpoint();
        }
        _trajectoryLever?.ResetLever();
        if (_trajectoryLever is not null)
        {
            _trajectoryLever.InteractionEnabled = false;
        }
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string copper = "res://assets/textures/copper_rivets.svg";
        const string rubber = "res://assets/textures/rubber_chevrons.svg";
        Color pale = new("b5afa5");
        Color frame = new("67584f");
        RoomGeometry.AddClosedRoomShell(this, "RoomShell", new Vector3(0.0f, 0.0f, -12.0f - (LandingAdditionalLength * 0.5f)), new Vector2(24.0f, 100.0f + LandingAdditionalLength), -3.0f, 36.0f, metal, new Color("6b625b"), new Color("463d39"), body =>
        {
            if (body is PlayerBall) { RestartRoom(); }
        });
        RoomGeometry.AddBox(this, "SafeStart", new Vector3(12.0f, 0.5f, 17.275f), new Vector3(0.0f, 7.1f, 29.1375f), Vector3.Zero, metal, pale, 0.42f, 0.62f);
        RoomGeometry.AddBox(this, "ApproachRamp", new Vector3(10.0f, 0.55f, 18.069311f), new Vector3(0.0f, 4.833664f, 11.818486f), new Vector3(Mathf.DegToRad(-14.420773f), 0.0f, 0.0f), copper, new Color("a16e50"), 0.34f, 0.58f);
        RoomGeometry.AddBox(this, "PistonPad", new Vector3(10.0f, 0.5f, 6.0f), new Vector3(0.0f, 2.6f, 0.0f), Vector3.Zero, rubber, new Color("454c4f"), 0.04f, 0.92f);
        RoomGeometry.AddBox(this, "ExitDeck", new Vector3(20.0f, 0.5f, 35.7f), new Vector3(0.0f, 14.0f, -90.85f), Vector3.Zero, metal, pale.Lightened(0.04f), 0.42f, 0.62f);
        _trajectoryLever = new MechanicalLever
        {
            Name = "TrajectoryLever",
            Position = new Vector3(0.0f, 7.35f, 21.8f),
            ActivationRadius = 4.5f,
            InteractionEnabled = false,
        };
        _trajectoryLever.Activated += () =>
        {
            if (_nextTrajectoryPlate != _trajectoryPlates.Count)
            {
                return;
            }
            _trajectoryLeverActivated = true;
            _leverActivationTick = 0;
            SetPistonPitch(ArmedPistonPitchDegrees);
            OpenTrajectoryBarrier();
        };
        AddChild(_trajectoryLever);
        AddTrajectoryPlate("TrajectoryPlateLeft", 0, new Vector3(-2.8f, 8.35f, 30.0f));
        AddTrajectoryPlate("TrajectoryPlateRight", 1, new Vector3(2.8f, 8.35f, 25.8f));
        BuildTrajectoryBarrier(metal, frame);
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(this, $"StartRail{side}", new Vector3(0.36f, 1.4f, 17.275f), new Vector3(side * 6.35f, 7.85f, 29.1375f), Vector3.Zero, metal, frame, 0.42f, 0.62f);
            RoomGeometry.AddBox(this, $"ApproachRail{side}", new Vector3(0.36f, 1.45f, 18.069311f), new Vector3(side * 5.35f, 5.802104f, 11.569220f), new Vector3(Mathf.DegToRad(-14.420773f), 0.0f, 0.0f), metal, frame, 0.42f, 0.62f);
            RoomGeometry.AddBox(this, $"ExitRail{side}", new Vector3(0.36f, 1.45f, 35.7f), new Vector3(side * 10.35f, 14.75f, -90.85f), Vector3.Zero, metal, frame, 0.42f, 0.62f);
        }
        _piston = new MomentumPiston3D
        {
            Name = "MomentumPiston",
            Position = new Vector3(0.0f, 2.85f, 1.0f),
            Rotation = Vector3.Zero,
            LaunchVelocity = ResolvePistonVelocity(ParkedPistonPitchDegrees),
            SeatOffset = new Vector3(0.0f, 1.15f, 0.0f),
            WindUpTicks = 24,
            EnableAudio = !_runSolutionSmoke,
        };
        AddChild(_piston);

        BuildLowGravityMagnetCourse();
    }

    private void AddTrajectoryPlate(string name, int index, Vector3 position)
    {
        RouteCheckpoint3D plate = new()
        {
            Name = name,
            Position = position,
            CheckpointIndex = index,
            TriggerSize = new Vector3(3.0f, 1.9f, 3.0f),
            FrameTint = index == 0 ? new Color("b77a55") : new Color("d2a35e"),
            FlatFloorMarker = true,
        };
        plate.Entered += (entered, player) =>
        {
            if (player == _player && entered.CheckpointIndex == _nextTrajectoryPlate)
            {
                entered.Activate();
                _nextTrajectoryPlate++;
                if (_nextTrajectoryPlate == _trajectoryPlates.Count)
                {
                    _trajectoryLever.InteractionEnabled = true;
                }
            }
            else if (player == _player)
            {
                entered.FlashDenied();
            }
        };
        AddChild(plate);
        _trajectoryPlates.Add(plate);
    }

    private void BuildTrajectoryBarrier(string texture, Color tint)
    {
        Vector3 size = new(11.4f, 3.2f, 0.5f);
        StaticBody3D barrier = new() { Name = "TrajectoryBarrier", Position = new Vector3(0.0f, 9.15f, 20.65f) };
        _trajectoryBarrierCollision = new CollisionShape3D { Shape = new BoxShape3D { Size = size } };
        _trajectoryBarrierVisual = new MeshInstance3D
        {
            Name = "TrajectoryBarrierVisual",
            Mesh = SurfaceMeshFactory.CreateTiledBox(size),
            MaterialOverride = RoomGeometry.CreateMaterial(texture, tint, 0.42f, 0.62f),
        };
        barrier.AddChild(_trajectoryBarrierCollision);
        barrier.AddChild(_trajectoryBarrierVisual);
        AddChild(barrier);
    }

    private void OpenTrajectoryBarrier()
    {
        _trajectoryBarrierCollision.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        Tween tween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(_trajectoryBarrierVisual, "position", new Vector3(0.0f, -3.7f, 0.0f), 0.32f);
    }

    private void CloseTrajectoryBarrier()
    {
        if (_trajectoryBarrierCollision is null || _trajectoryBarrierVisual is null)
        {
            return;
        }
        _trajectoryBarrierCollision.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
        _trajectoryBarrierVisual.Position = Vector3.Zero;
    }

    private void SetPistonPitch(float pitchDegrees)
    {
        _piston.Rotation = Vector3.Zero;
        _piston.LaunchVelocity = ResolvePistonVelocity(pitchDegrees);
        Node3D? head = _piston.GetNodeOrNull<Node3D>("PistonHead");
        if (head is not null)
        {
            head.Rotation = new Vector3(Mathf.DegToRad(pitchDegrees), 0.0f, 0.0f);
        }
    }

    private static Vector3 ResolvePistonVelocity(float pitchDegrees)
    {
        float pitch = Mathf.DegToRad(pitchDegrees);
        return new Vector3(0.0f, Mathf.Cos(pitch) * 20.2f, Mathf.Sin(pitch) * 20.2f);
    }

    private void BuildLowGravityMagnetCourse()
    {
        _lowGravityVolume = new ForceVolume3D
        {
            Name = "PostPistonLowGravity",
            Position = new Vector3(0.0f, 16.0f, -39.0f),
            CollisionMask = 1,
            Profile = GD.Load<ForceVolumeProfile>("res://resources/force_volumes/low_gravity.tres"),
        };
        _lowGravityVolume.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(23.4f, 30.0f, 72.0f) },
        });
        _lowGravityVolume.RigidBodyEntered += body =>
        {
            if (body == _player)
            {
                _touchedLowGravity = true;
                _airControlCleared = false;
            }
        };
        _lowGravityVolume.RigidBodyExited += body =>
        {
            if (body == _player && _touchedLowGravity)
            {
                _airControlCleared = _player.AirControlAcceleration <= 0.001f;
            }
        };
        AddChild(_lowGravityVolume);

        AddLowGravityParticles();
        AddMagneticField("RightPushMagnet", 0, new Vector3(-9.6f, 15.0f, -25.0f), Vector3.Right, new Color("df6f73"));
        AddMagneticField("ForwardPushMagnet", 1, new Vector3(0.0f, 16.0f, -91.0f), Vector3.Forward, new Color("6fa9df"));
    }

    private void AddMagneticField(string name, int index, Vector3 magnetPosition, Vector3 direction, Color tint)
    {
        bool forwardPlatformField = index == 1;
        float fieldCenterY = forwardPlatformField ? 19.0f : 16.0f;
        float fieldHeight = forwardPlatformField ? 10.0f : 28.0f;
        float fieldCenterZ = forwardPlatformField ? -96.0f : magnetPosition.Z;
        float fieldDepth = forwardPlatformField ? 25.4f : 22.0f;
        float fieldWidth = forwardPlatformField ? 19.4f : 23.0f;
        ForceVolume3D field = new()
        {
            Name = $"{name}Field",
            Position = new Vector3(0.0f, fieldCenterY, fieldCenterZ),
            CollisionMask = 1,
            AirborneOnly = forwardPlatformField,
            Profile = new ForceVolumeProfile
            {
                Kind = ForceVolumeKind.Magnetic,
                Direction = direction,
                Strength = index == 0 ? 3.0f : 12.0f,
                AirControlAcceleration = 0.0f,
            },
        };
        field.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(fieldWidth, fieldHeight, fieldDepth) },
        });
        field.RigidBodyEntered += body =>
        {
            if (body == _player && index == _nextMagneticField)
            {
                _nextMagneticField++;
            }
        };
        AddChild(field);
        _magneticFields.Add(field);

        BuildFloatingMagnet(name, magnetPosition, direction, tint);
        AddMagneticStreaks(name, fieldCenterY, fieldCenterZ, direction, tint, fieldHeight * 0.42f);
    }

    private void BuildFloatingMagnet(string name, Vector3 position, Vector3 direction, Color tint)
    {
        bool alongRoute = Mathf.Abs(direction.Z) > 0.5f;
        Node3D magnet = new()
        {
            Name = name,
            Position = position,
            Rotation = alongRoute ? new Vector3(0.0f, direction.Z < 0.0f ? Mathf.Pi / 2.0f : -Mathf.Pi / 2.0f, 0.0f) : Vector3.Zero,
        };
        float armDirection = alongRoute || direction.X > 0.0f ? 1.0f : -1.0f;
        StandardMaterial3D bodyMaterial = RoomGeometry.CreateMaterial(
            "res://assets/textures/brushed_metal.png", tint, 0.3f, 0.62f, emissionEnabled: true, emission: tint.Darkened(0.48f));
        StandardMaterial3D poleMaterial = RoomGeometry.CreateMaterial(
            "res://assets/textures/copper_rivets.svg", new Color("d7c8a4"), 0.28f, 0.7f, emissionEnabled: true, emission: tint.Darkened(0.62f));

        RoomGeometry.AddBox(magnet, "MagnetBack", new Vector3(1.15f, 5.6f, 2.2f), Vector3.Zero, Vector3.Zero,
            string.Empty, Colors.White, 0.0f, 1.0f, materialOverride: bodyMaterial);
        for (int pole = -1; pole <= 1; pole += 2)
        {
            RoomGeometry.AddBox(magnet, $"MagnetArm{pole}", new Vector3(4.2f, 1.15f, 2.2f),
                new Vector3(armDirection * 1.75f, pole * 2.2f, 0.0f), Vector3.Zero,
                string.Empty, Colors.White, 0.0f, 1.0f, materialOverride: bodyMaterial);
            RoomGeometry.AddBox(magnet, $"GlowingPole{pole}", new Vector3(0.5f, 1.25f, 2.35f),
                new Vector3(armDirection * 3.95f, pole * 2.2f, 0.0f), Vector3.Zero,
                string.Empty, Colors.White, 0.0f, 1.0f, materialOverride: poleMaterial);
        }
        magnet.AddChild(new OmniLight3D
        {
            Name = "MagnetGlow",
            LightColor = tint,
            LightEnergy = 1.4f,
            OmniRange = 11.0f,
            ShadowEnabled = false,
        });
        AddChild(magnet);
    }

    private void AddLowGravityParticles()
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
            EmissionBoxExtents = new Vector3(11.0f, 13.0f, 35.0f),
            Direction = Vector3.Up,
            Spread = 15.0f,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 0.3f,
            InitialVelocityMax = 0.8f,
        };
        AddChild(new GpuParticles3D
        {
            Name = "PostPistonLowGravityMotes",
            Position = new Vector3(0.0f, 16.0f, -39.0f),
            Amount = 96,
            Lifetime = 6.0,
            Randomness = 0.8f,
            ProcessMaterial = process,
            DrawPass1 = new SphereMesh { Radius = 0.05f, Height = 0.1f, RadialSegments = 8, Rings = 4, Material = material },
        });
    }

    private void AddMagneticStreaks(string name, float centerY, float z, Vector3 direction, Color tint, float verticalExtent)
    {
        StandardMaterial3D material = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = tint.Lightened(0.22f),
            EmissionEnabled = true,
            Emission = tint,
        };
        ParticleProcessMaterial process = new()
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(10.0f, verticalExtent, 9.0f),
            Direction = direction,
            Spread = 4.0f,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 4.0f,
            InitialVelocityMax = 6.5f,
        };
        AddChild(new GpuParticles3D
        {
            Name = $"{name}ForceStreaks",
            Position = new Vector3(0.0f, centerY, z),
            Amount = 54,
            Lifetime = 1.8,
            Randomness = 0.55f,
            ProcessMaterial = process,
            DrawPass1 = new BoxMesh { Size = new Vector3(0.9f, 0.045f, 0.045f), Material = material },
        });
    }

    private void BuildGoal()
    {
        Vector3 position = new(0.0f, 15.15f, -107.9f);
        _goal = new Area3D { Name = "GoalCup", Position = position, CollisionLayer = 0, CollisionMask = 1, Monitoring = true };
        _goal.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 2.0f, Height = 2.8f } });
        _goal.BodyEntered += body =>
        {
            if (body is PlayerBall &&
                _nextTrajectoryPlate == _trajectoryPlates.Count &&
                _pistonArmed &&
                _trajectoryLeverActivated &&
                _pistonFired &&
                _launchSpeed >= 19.5f &&
                _touchedLowGravity &&
                _nextMagneticField == _magneticFields.Count &&
                _airControlCleared &&
                _player.AirControlAcceleration <= 0.001f)
            {
                CompleteRoom();
            }
        };
        AddChild(_goal);
        RoomGeometry.AddGoalExitDoor(this, position);
    }

    private void TryAwardPistonPerfect()
    {
        if (_pistonPerfectEligible)
        {
            MarkAdvancementCondition("piston-perfect");
        }
    }

    private void RunRequestedSmoke()
    {
        if (_runMechanicsSmoke)
        {
            bool baseOnFloor = Mathf.Abs(_piston.Position.Y - 2.85f) < 0.01f && _piston.Rotation.IsEqualApprox(Vector3.Zero);
            bool realSequence = _trajectoryPlates.Count == 2 && !_trajectoryBarrierCollision.Disabled;
            bool wallExit = Mathf.Abs(_goal.Position.X) < 0.01f && Mathf.Abs(_goal.Position.Z - -107.9f) < 0.01f;
            bool magnetCourse = _lowGravityVolume.Profile?.Kind == ForceVolumeKind.Gravity &&
                _magneticFields.Count == 2 &&
                _magneticFields.All(field => field.Profile?.Kind == ForceVolumeKind.Magnetic && field.Profile.AirControlAcceleration <= 0.001f);
            if (!baseOnFloor || !realSequence || !wallExit || !magnetCourse)
            {
                GD.PushError($"ROOM19_MECHANICS_FAIL: base={baseOnFloor}, plates={_trajectoryPlates.Count}, barrier={!_trajectoryBarrierCollision.Disabled}, magnets={_magneticFields.Count}, exit={wallExit}.");
                GetTree().Quit(1);
                return;
            }

            GD.Print("ROOM19_MECHANICS_PASS: the piston base stays level, two ordered plates arm it, and two visible magnetic fields cross a bounded low-gravity course before the wall-mounted exit.");
            GetTree().Quit(0);
            return;
        }

        _nextTrajectoryPlate = _trajectoryPlates.Count;
        _leverActivationTick = _runAchievementPositiveSmoke ? 80 : 160;
        _pistonPerfectEligible = _leverActivationTick <= 120;
        TryAwardPistonPerfect();
        bool awarded = CompletedAdvancementIds.Contains("piston-perfect");
        bool expected = _runAchievementPositiveSmoke;
        if (awarded != expected)
        {
            GD.PushError($"ROOM19_ACHIEVEMENT_FAIL: expected={expected}, awarded={awarded}, ticks={_leverActivationTick}.");
            GetTree().Quit(1);
            return;
        }

        GD.Print(expected
            ? "ROOM19_ACHIEVEMENT_POSITIVE_PASS: entering the piston promptly after the ordered setup awarded Piston Perfect."
            : "ROOM19_ACHIEVEMENT_NEGATIVE_PASS: a delayed piston entry denied Piston Perfect without blocking completion.");
        GetTree().Quit(0);
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM19_SOLUTION_FAIL: {message}");
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
