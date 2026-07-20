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

public partial class Room20Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_20_solution.tres";
    private const byte InteractAction = 1;
    private const int RequiredSolutionRuns = 1;
    private const int MaximumSolutionTicksPerRun = 1900;
    private const float CleanTransitRailOffset = 6.0f;

    private readonly List<FlightGate3D> _pistonFlightGates = new();
    private readonly List<InterferenceCannon3D> _interferenceCannons = new();
    private readonly List<Area3D> _gauntletCheckpoints = new();
    private readonly List<RouteCheckpoint3D> _balancePlates = new();
    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private PlayerCannon3D _playerCannon = null!;
    private MovingPlatform3D _movingPlatform = null!;
    private MechanicalLever _transitLever = null!;
    private MomentumPiston3D _piston = null!;
    private Area3D _goal = null!;
    private CollisionShape3D _departureGateCollision = null!;
    private MeshInstance3D _departureGateVisual = null!;
    private Vector3 _departureGateRestPosition;
    private Tween? _departureGateTween;
    private AudioStreamPlayer3D? _playerCannonAudio;
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private bool _showPrompts;
    private bool _highContrastPrompts;
    private bool _playerCannonFired;
    private bool _movingBoarded;
    private bool _movingArrived;
    private bool _movingStayedAboard;
    private bool _transitLeverActivated;
    private bool _transitStarted;
    private bool _cleanAssemblyEligible = true;
    private bool _pistonArmed;
    private bool _pistonFired;
    private float _pistonLaunchSpeed;
    private int _projectileHits;
    private bool _runSolutionSmoke;
    private bool _runPreview;
    private bool _runPanoramaCapture;
    private bool _runShellSmoke;
    private bool _runMechanicsSmoke;
    private bool _runAchievementPositiveSmoke;
    private bool _runAchievementNegativeSmoke;
    private bool _solutionSmokeFinishing;
    private int _solutionRun;
    private int _solutionTick;
    private int _previewFrames;
    private int _shellSmokeTick;
    private int _nextGauntletCheckpoint;
    private int _nextBalancePlate;
    private int _nextPistonFlightGate;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] args = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(args, argument => argument == "--room20-solution-smoke");
        _runPreview = Array.Exists(args, argument => argument == "--room20-preview");
        _runShellSmoke = Array.Exists(args, argument => argument == "--room-shell-smoke");
        _runMechanicsSmoke = Array.Exists(args, argument => argument == "--room20-mechanics-smoke");
        _runAchievementPositiveSmoke = Array.Exists(args, argument => argument == "--room20-achievement-positive-smoke");
        _runAchievementNegativeSmoke = Array.Exists(args, argument => argument == "--room20-achievement-negative-smoke");

        BuildRoom();
        if (Array.Exists(args, argument => argument == "--panorama-capture=room20_b"))
        {
            _movingPlatform.Position += _movingPlatform.EndOffset * 0.55f;
        }
        BuildGoal();
        _runPanoramaCapture = PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room20_a", new Vector3(10.0f, 17.0f, 57.0f), new Vector3(0.0f, 24.0f, -91.0f), 60.0f),
            new("room20_b", new Vector3(-11.0f, 31.0f, -26.0f), new Vector3(0.0f, 24.0f, -68.0f), 61.0f),
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
        _playerCannon.SetKeyLabel(key == Key.None ? "E" : key.ToString());
        _transitLever.SetKeyLabel(key == Key.None ? "E" : key.ToString());
        if (_runPreview || _runPanoramaCapture)
        {
            _cameraRig.SetInputEnabled(false);
            _showPrompts = false;
        }

        _playerCannon.Fired += body =>
        {
            if (body == _player)
            {
                _playerCannonFired = true;
                _playerCannonAudio?.Play();
            }
        };
        _movingPlatform.PlayerBoarded += player =>
        {
            if (player == _player)
            {
                _movingBoarded = true;
                _movingStayedAboard = true;
            }
        };
        _movingPlatform.PlayerLeftDuringTransit += player =>
        {
            if (player == _player)
            {
                _movingStayedAboard = false;
                _cleanAssemblyEligible = false;
            }
        };
        _movingPlatform.Departed += () =>
        {
            _transitStarted = true;
            foreach (RouteCheckpoint3D plate in _balancePlates)
            {
                plate.ResetCheckpoint();
            }
        };
        _movingPlatform.ArrivedAtDestination += () =>
        {
            _transitStarted = false;
            _movingArrived = _movingStayedAboard &&
                _movingPlatform.HasOccupant(_player) &&
                _nextBalancePlate == _balancePlates.Count;
        };
        _piston.Armed += player => _pistonArmed |= player == _player;
        _piston.Fired += body =>
        {
            if (body == _player)
            {
                _pistonFired = true;
                _pistonLaunchSpeed = body.LinearVelocity.Length();
            }
        };

        if (_runSolutionSmoke)
        {
            _showPrompts = false;
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            if (_solutionTrace is null || _solutionTrace.RoomId != RoomId || !_solutionTrace.ActionFlags.Contains(InteractAction))
            {
                FailSolutionSmoke("The Room 20 SolutionTrace is invalid or does not fire the player cannon.");
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
            string path = ProjectSettings.GlobalizePath("user://room20-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"ROOM20_PREVIEW_CAPTURE: {path}");
            GetTree().Quit(0);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        TrackTransitControl();
        if (_runShellSmoke)
        {
            RunShellSmokeTick();
            return;
        }

        foreach (InterferenceCannon3D cannon in _interferenceCannons)
        {
            cannon.AdvancePhysicsTick();
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

        bool cannonFocused = _playerCannon.CanInteract(_player) &&
            _cameraRig.IsLookingAt(_playerCannon.GlobalPosition + Vector3.Up * 1.8f);
        bool leverFocused = _transitLever.CanInteract(_player) &&
            _cameraRig.IsLookingAt(_transitLever.GlobalPosition + Vector3.Up * 1.75f);
        _playerCannon.SetFocused(cannonFocused && _showPrompts, _highContrastPrompts);
        _transitLever.SetFocused(leverFocused && _showPrompts, _highContrastPrompts);
        if (Godot.Input.IsActionJustPressed(InputDefaults.Interact))
        {
            if (cannonFocused)
            {
                _playerCannon.Interact(_player);
            }
            else if (leverFocused)
            {
                _transitLever.Interact(_player);
            }
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
            FailSolutionSmoke($"Run {_solutionRun + 1} hit the maintenance floor at {_player.GlobalPosition}; cannon={_playerCannonFired}, gauntlet={_nextGauntletCheckpoint}/{_gauntletCheckpoints.Count}, hits={_projectileHits}, boarded={_movingBoarded}, stayed={_movingStayedAboard}, lever={_transitLeverActivated}, progress={_movingPlatform.Progress:F2}, arrived={_movingArrived}, plates={_nextBalancePlate}/{_balancePlates.Count}, piston={_pistonFired}, gates={_nextPistonFlightGate}/{_pistonFlightGates.Count}.");
            return;
        }

        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        ResetDevices();
        ResetDepartureGate();
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
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 20 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }
        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 20 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void RunSolutionTick()
    {
        if (_solutionTrace is null || _solutionSmokeFinishing) { return; }
        if (IsComplete)
        {
            if (!CanCompleteAssembly() || !CompletedAdvancementIds.Contains("clean-assembly"))
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} skipped a chapter-exam leg or failed Clean Assembly: cannon={_playerCannonFired}, gauntlet={_nextGauntletCheckpoint}/{_gauntletCheckpoints.Count}, shots={TotalInterferenceShots()}, hits={_projectileHits}, lever={_transitLeverActivated}, boarded={_movingBoarded}, plates={_nextBalancePlate}/{_balancePlates.Count}, arrived={_movingArrived}, clean={_cleanAssemblyEligible}, piston={_pistonFired}, gates={_nextPistonFlightGate}/{_pistonFlightGates.Count}, launch={_pistonLaunchSpeed:F2}.");
                return;
            }
            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print($"ROOM20_SOLUTION_PASS: SolutionTrace timed the forty-cannon gauntlet, balanced the lever-gated transit and completed the level-base piston arc with Clean Assembly for {_solutionRun} consecutive completions; piston launch={_pistonLaunchSpeed:F2} m/s.");
                FinishSolutionSmoke(0);
                return;
            }
            ClearCompletionState();
            ResetDevices();
            ResetDepartureGate();
            _player.ResetTo(_spawnTransform);
            _solutionTick = 0;
            ResetRunState();
            return;
        }
        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} timed out at {_player.GlobalPosition}; cannon={_playerCannonFired}, gauntlet={_nextGauntletCheckpoint}/{_gauntletCheckpoints.Count}, hits={_projectileHits}, lever={_transitLeverActivated}, plates={_nextBalancePlate}/{_balancePlates.Count}, platform={_movingArrived}, piston={_pistonFired}.");
            return;
        }
        (Vector2 move, byte flags) = ResolveTraceStep(_solutionTick - 1);
        _player.SimulatedMoveInput = move;
        if ((flags & InteractAction) != 0)
        {
            if (!_playerCannonFired)
            {
                _playerCannon.Interact(_player);
            }
            else
            {
                _transitLever.Interact(_player);
            }
        }
    }

    private (Vector2, byte) ResolveTraceStep(int tick)
    {
        if (_solutionTrace is null) { return (Vector2.Zero, 0); }
        int remaining = tick;
        for (int index = 0; index < _solutionTrace.MoveInputs.Count; index++)
        {
            int duration = _solutionTrace.MoveDurationsTicks[index];
            if (remaining < duration) { return (_solutionTrace.MoveInputs[index], _solutionTrace.ActionFlags[index]); }
            remaining -= duration;
        }
        return (_solutionTrace.HoldLastInput ? _solutionTrace.MoveInputs[^1] : Vector2.Zero, 0);
    }

    private void ResetDevices()
    {
        _playerCannonAudio?.Stop();
        _playerCannon.ResetCannon();
        foreach (InterferenceCannon3D cannon in _interferenceCannons)
        {
            cannon.ResetCannon();
        }
        _movingPlatform.ResetPlatform();
        _transitLever.ResetLever();
        _piston.ResetPiston();
    }

    private void ResetRunState()
    {
        _playerCannonFired = false;
        _movingBoarded = false;
        _movingArrived = false;
        _movingStayedAboard = false;
        _transitLeverActivated = false;
        _transitStarted = false;
        _cleanAssemblyEligible = true;
        _pistonArmed = false;
        _pistonFired = false;
        _pistonLaunchSpeed = 0.0f;
        _projectileHits = 0;
        _nextGauntletCheckpoint = 0;
        _nextBalancePlate = 0;
        _nextPistonFlightGate = 0;
        foreach (RouteCheckpoint3D plate in _balancePlates)
        {
            plate.ResetCheckpoint();
        }
        foreach (FlightGate3D gate in _pistonFlightGates)
        {
            gate.ResetGate();
        }
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string copper = "res://assets/textures/copper_rivets.svg";
        Color pale = new("afb6b8");
        Color frame = new("52555f");
        RoomGeometry.AddClosedRoomShell(this, "RoomShell", new Vector3(0.0f, 0.0f, -67.5f), new Vector2(26.0f, 265.0f), -3.0f, 50.0f, metal, new Color("565661"), new Color("373843"), body =>
        {
            if (body is PlayerBall) { RestartRoom(); }
        });

        RoomGeometry.AddBox(this, "SafeStart", new Vector3(12.0f, 0.5f, 22.775f), new Vector3(0.0f, 6.0f, 53.3875f), Vector3.Zero, metal, pale, 0.44f, 0.62f);
        RoomGeometry.AddBox(this, "TransitApproach", new Vector3(14.0f, 0.5f, 14.0f), new Vector3(0.0f, 8.0f, -27.0f), Vector3.Zero, metal, pale.Darkened(0.04f), 0.44f, 0.62f);
        RoomGeometry.AddBox(this, "PistonStation", new Vector3(14.0f, 0.5f, 14.0f), new Vector3(0.0f, 18.0f, -77.0f), Vector3.Zero, copper, new Color("897064"), 0.38f, 0.58f);
        RoomGeometry.AddBox(this, "FinalDeck", new Vector3(15.0f, 0.5f, 50.0f), new Vector3(0.0f, 30.0f, -175.0f), Vector3.Zero, metal, pale.Lightened(0.06f), 0.44f, 0.62f);

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(this, $"StartRail{side}", new Vector3(0.36f, 1.4f, 22.775f), new Vector3(side * 6.35f, 6.75f, 53.3875f), Vector3.Zero, copper, frame, 0.38f, 0.58f);
            RoomGeometry.AddBox(this, $"TransitRail{side}", new Vector3(0.36f, 1.4f, 14.0f), new Vector3(side * 7.35f, 8.75f, -27.0f), Vector3.Zero, metal, frame, 0.42f, 0.62f);
            RoomGeometry.AddBox(this, $"PistonRail{side}", new Vector3(0.36f, 1.4f, 14.0f), new Vector3(side * 7.35f, 18.75f, -77.0f), Vector3.Zero, copper, frame, 0.38f, 0.58f);
            RoomGeometry.AddBox(this, $"FinalRail{side}", new Vector3(0.36f, 1.5f, 50.0f), new Vector3(side * 7.85f, 30.75f, -175.0f), Vector3.Zero, metal, frame, 0.42f, 0.62f);
            RoomGeometry.AddVisualBox(this, $"TransitGuide{side}", new Vector3(0.34f, 0.34f, 27.85f), new Vector3(side * 4.8f, 13.0f, -52.0f), new Vector3(Mathf.DegToRad(-158.96f), 0.0f, 0.0f), copper, new Color("80645d"), 0.38f, 0.58f);
        }

        _playerCannon = new PlayerCannon3D
        {
            Name = "PlayerCannon",
            Position = new Vector3(0.0f, 6.25f, 47.0f),
            LaunchVelocity = new Vector3(0.0f, 10.0f, -18.0f),
            MuzzleOffset = new Vector3(0.0f, 2.2f, -1.0f),
            ActivationRadius = 4.5f,
        };
        AddChild(_playerCannon);

        ForceVolume3D lowGravity = new()
        {
            Name = "LowGravityCannonGauntlet",
            Position = new Vector3(0.0f, 24.0f, 11.0f),
            CollisionMask = 1,
            Profile = GD.Load<ForceVolumeProfile>("res://resources/force_volumes/low_gravity.tres"),
        };
        lowGravity.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(22.0f, 48.0f, 58.0f) } });
        AddChild(lowGravity);

        float[] gauntletZ = Enumerable.Range(0, 14).Select(index => Mathf.Lerp(36.0f, -14.0f, index / 13.0f)).ToArray();
        for (int column = 0; column < gauntletZ.Length; column++)
        {
            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                int index = (column * 2) + sideIndex;
                float side = sideIndex == 0 ? -1.0f : 1.0f;
                float cannonY = column % 2 == 0 ? 11.15f : 18.15f;
                AddInterferenceCannon(
                    $"InterferenceCannon{index + 1}",
                    new Vector3(side * 9.5f, cannonY, gauntletZ[column]),
                    new Vector3(-side * 3.0f, 2.6f, 0.0f),
                    new Vector3(-side * 23.0f, 0.0f, 0.0f),
                    8 + (column * 3) + sideIndex,
                    89 + index);
                RoomGeometry.AddBox(this, $"InterferenceMount{index + 1}", new Vector3(4.0f, 0.55f, 3.2f), new Vector3(side * 9.5f, cannonY - 0.35f, gauntletZ[column]), Vector3.Zero, copper, new Color("725f62"), 0.38f, 0.58f);

                int highIndex = (gauntletZ.Length * 2) + index;
                const float highCannonY = 45.9f;
                AddInterferenceCannon(
                    $"InterferenceCannon{highIndex + 1}",
                    new Vector3(side * 9.5f, highCannonY, gauntletZ[column]),
                    new Vector3(-side * 3.0f, 2.6f, 0.0f),
                    new Vector3(-side * 23.0f, 0.0f, 0.0f),
                    12 + (column * 3) + sideIndex,
                    89 + highIndex);
                RoomGeometry.AddBox(this, $"InterferenceMount{highIndex + 1}", new Vector3(4.0f, 0.55f, 3.2f), new Vector3(side * 9.5f, highCannonY - 0.35f, gauntletZ[column]), Vector3.Zero, copper, new Color("725f62"), 0.38f, 0.58f);
            }
            AddGauntletCheckpoint($"GauntletLane{column + 1}", column, new Vector3(0.0f, 18.0f, gauntletZ[column]));
        }

        _movingPlatform = new MovingPlatform3D
        {
            Name = "MovingPlatform",
            Position = new Vector3(0.0f, 7.95f, -39.0f),
            PlatformSize = new Vector3(12.6f, 0.6f, 14.0f),
            EndOffset = new Vector3(0.0f, 10.0f, -26.0f),
            DepartureDelayTicks = 36,
            TravelTicks = 360,
            EnableAudio = !_runSolutionSmoke,
            RequiresActivation = true,
            EnableRearGate = false,
        };
        AddChild(_movingPlatform);

        _transitLever = new MechanicalLever
        {
            Name = "TransitLever",
            Position = new Vector3(-4.3f, 0.35f, 3.6f),
            ActivationRadius = 10.0f,
        };
        _transitLever.Activated += () =>
        {
            _transitLeverActivated = true;
            CloseDepartureGate();
            _movingPlatform.Activate();
        };
        _movingPlatform.AddChild(_transitLever);
        AddBalancePlate("BalancePlateRight", 0, new Vector3(2.55f, 1.18f, -0.7f));
        AddBalancePlate("BalancePlateLeft", 1, new Vector3(-1.8f, 1.18f, -0.7f));
        BuildDepartureGate(metal, frame);

        _piston = new MomentumPiston3D
        {
            Name = "MomentumPiston",
            Position = new Vector3(0.0f, 18.25f, -81.0f),
            Rotation = Vector3.Zero,
            LaunchVelocity = ResolvePistonVelocity(-42.0f),
            SeatOffset = new Vector3(0.0f, 1.15f, 0.0f),
            WindUpTicks = 24,
            EnableAudio = !_runSolutionSmoke,
        };
        AddChild(_piston);
        SetPistonPitch(-42.0f);

        if (!_runSolutionSmoke)
        {
            _playerCannonAudio = new AudioStreamPlayer3D { Name = "PlayerCannonSfx", Stream = GD.Load<AudioStream>("res://assets/audio/sfx/device_player_cannon_fire.wav"), Bus = "SFX", Position = _playerCannon.Position, MaxDistance = 50.0f, UnitSize = 9.0f };
            AddChild(_playerCannonAudio);
        }

        AddPistonFlightGate("PistonFlightGateNear", 0, new Vector3(0.0f, 24.0f, -88.0f), 2.8f);
        AddPistonFlightGate("PistonFlightGateFar", 1, new Vector3(0.0f, 33.0f, -137.0f), 2.8f);

    }

    private void AddInterferenceCannon(
        string name,
        Vector3 position,
        Vector3 muzzleOffset,
        Vector3 projectileVelocity,
        int initialDelayTicks,
        int cadenceTicks)
    {
        InterferenceCannon3D cannon = new()
        {
            Name = name,
            Position = position,
            MuzzleOffset = muzzleOffset,
            ProjectileVelocity = projectileVelocity,
            InitialDelayTicks = initialDelayTicks,
            CadenceTicks = cadenceTicks,
            ProjectileLifetimeTicks = 72,
            PoolSize = 3,
            EnableAudio = !_runSolutionSmoke,
        };
        cannon.PlayerHit += player =>
        {
            if (player != _player)
            {
                return;
            }

            _projectileHits++;
            _cleanAssemblyEligible = false;
            Vector3 impactDirection = projectileVelocity.Normalized();
            player.LinearVelocity += (impactDirection * 7.5f) + (Vector3.Up * 1.8f);
            player.Sleeping = false;
        };
        AddChild(cannon);
        _interferenceCannons.Add(cannon);
    }

    private void AddGauntletCheckpoint(string name, int index, Vector3 position)
    {
        Area3D checkpoint = new()
        {
            Name = name,
            Position = position,
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true,
            Monitorable = false,
        };
        checkpoint.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(20.0f, 42.0f, 2.2f) },
        });
        checkpoint.BodyEntered += body =>
        {
            if (body == _player &&
                _playerCannonFired &&
                !_player.IsGrounded &&
                index == _nextGauntletCheckpoint)
            {
                _nextGauntletCheckpoint++;
            }
        };
        AddChild(checkpoint);
        _gauntletCheckpoints.Add(checkpoint);
    }

    private void AddBalancePlate(string name, int index, Vector3 localPosition)
    {
        RouteCheckpoint3D plate = new()
        {
            Name = name,
            Position = localPosition,
            CheckpointIndex = index,
            TriggerSize = new Vector3(4.5f, 2.0f, 16.0f),
            FrameTint = index == 0 ? new Color("8ea7b0") : new Color("c18a63"),
            FlatFloorMarker = true,
        };
        plate.Entered += (entered, player) =>
        {
            if (player != _player)
            {
                return;
            }
            if (!_transitStarted || entered.CheckpointIndex != _nextBalancePlate) { entered.FlashDenied(); return; }

            entered.Activate();
            _nextBalancePlate++;
        };
        _movingPlatform.AddChild(plate);
        _balancePlates.Add(plate);
    }

    private void BuildDepartureGate(string texture, Color tint)
    {
        Vector3 size = new(13.0f, 2.2f, 0.42f);
        _departureGateRestPosition = new Vector3(0.0f, 1.15f, 7.22f);
        StaticBody3D gate = new() { Name = "DepartureGate" };
        _departureGateCollision = new CollisionShape3D
        {
            Position = _departureGateRestPosition,
            Shape = new BoxShape3D { Size = size },
            Disabled = true,
        };
        _departureGateVisual = new MeshInstance3D
        {
            Name = "DepartureGateVisual",
            Position = _departureGateRestPosition + (Vector3.Down * 2.4f),
            Mesh = SurfaceMeshFactory.CreateTiledBox(size),
            MaterialOverride = RoomGeometry.CreateMaterial(texture, tint, 0.42f, 0.62f),
        };
        gate.AddChild(_departureGateCollision);
        gate.AddChild(_departureGateVisual);
        _movingPlatform.AddChild(gate);
    }

    private void CloseDepartureGate()
    {
        _departureGateTween?.Kill();
        _departureGateCollision.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
        _departureGateTween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        _departureGateTween.TweenProperty(_departureGateVisual, "position", _departureGateRestPosition, 0.28f);
    }

    private void ResetDepartureGate()
    {
        if (_departureGateVisual is null || _departureGateCollision is null)
        {
            return;
        }

        _departureGateTween?.Kill();
        _departureGateTween = null;
        _departureGateCollision.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        _departureGateVisual.Position = _departureGateRestPosition + (Vector3.Down * 2.4f);
    }

    private void TrackTransitControl()
    {
        if (!_transitStarted || !_movingPlatform.HasOccupant(_player))
        {
            return;
        }

        float lateralOffset = Mathf.Abs(_movingPlatform.ToLocal(_player.GlobalPosition).X);
        if (lateralOffset >= CleanTransitRailOffset)
        {
            _cleanAssemblyEligible = false;
        }
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

    private void AddPistonFlightGate(string name, int index, Vector3 position, float radius)
    {
        FlightGate3D gate = new()
        {
            Name = name,
            Position = position,
            Radius = radius,
            FrameTint = index == 0 ? new Color("a65f49") : new Color("7b5048"),
            EnableAudio = !_runSolutionSmoke,
            MinimumExitSpeed = index == 0 ? 25.0f : 35.0f,
            SpeedGain = 5.0f,
            SpeedMultiplier = 1.2f,
            AxialBoostOnly = index == 1,
            MaximumDownwardExitSpeed = index == 1 ? 1.0f : float.PositiveInfinity,
        };
        gate.Passed += player =>
        {
            if (player == _player && index == _nextPistonFlightGate)
            {
                _nextPistonFlightGate++;
            }
        };
        AddChild(gate);
        _pistonFlightGates.Add(gate);
    }

    private void BuildGoal()
    {
        Vector3 position = new(0.0f, 31.15f, -198.9f);
        _goal = new Area3D { Name = "GoalCup", Position = position, CollisionLayer = 0, CollisionMask = 1, Monitoring = true };
        _goal.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 1.9f, Height = 3.0f } });
        _goal.BodyEntered += body =>
        {
            if (body is PlayerBall && CanCompleteAssembly())
            {
                TryAwardCleanAssembly();
                CompleteRoom();
            }
        };
        AddChild(_goal);
        RoomGeometry.AddGoalExitDoor(this, position);
    }

    private bool CanCompleteAssembly()
    {
        return _playerCannonFired &&
            _nextGauntletCheckpoint == _gauntletCheckpoints.Count &&
            TotalInterferenceShots() >= _interferenceCannons.Count &&
            _movingBoarded &&
            _movingStayedAboard &&
            _transitLeverActivated &&
            _nextBalancePlate == _balancePlates.Count &&
            _movingArrived &&
            _pistonArmed &&
            _pistonFired &&
            _pistonLaunchSpeed >= 19.5f &&
            _nextPistonFlightGate == _pistonFlightGates.Count;
    }

    private int TotalInterferenceShots()
    {
        return _interferenceCannons.Sum(cannon => cannon.ShotsFired);
    }

    private void TryAwardCleanAssembly()
    {
        if (_cleanAssemblyEligible && _projectileHits == 0 && CanCompleteAssembly())
        {
            MarkAdvancementCondition("clean-assembly");
        }
    }

    private void RunRequestedSmoke()
    {
        if (_runMechanicsSmoke)
        {
            bool startRailsReachBackWall = GetNode<StaticBody3D>("StartRail-1").Position.Z + 11.3875f >= 64.7f &&
                GetNode<StaticBody3D>("StartRail1").Position.Z + 11.3875f >= 64.7f;
            bool cannonGrid = _interferenceCannons.Count == 56 &&
                _interferenceCannons.Max(cannon => cannon.InitialDelayTicks) <= 52 &&
                _interferenceCannons.Select(cannon => cannon.CadenceTicks).Distinct().Count() == 56 &&
                _interferenceCannons.All(cannon => cannon.ProjectileVelocity.Length() >= 22.5f && cannon.HasSolidBodyHitbox && cannon.UsesRandomizedTiming);
            bool lowGravityContract = GetNodeOrNull<ForceVolume3D>("LowGravityCannonGauntlet") is { Profile.AirControlAcceleration: > 0.0f } &&
                _gauntletCheckpoints.Count == 14;
            bool platformContract = _movingPlatform.RequiresActivation &&
                !_movingPlatform.EnableRearGate &&
                _balancePlates.Count == 2 &&
                _departureGateCollision.Disabled;
            bool levelPistonBase = _piston.Rotation.IsEqualApprox(Vector3.Zero) &&
                Mathf.Abs(_piston.Position.Y - 18.25f) < 0.01f;
            bool wallExit = Mathf.Abs(_goal.Position.Z - -198.9f) < 0.01f;
            if (!startRailsReachBackWall || !cannonGrid || !lowGravityContract || !platformContract || !levelPistonBase || !wallExit)
            {
                GD.PushError($"ROOM20_MECHANICS_FAIL: start_rails={startRailsReachBackWall}, cannons={_interferenceCannons.Count}, platform={platformContract}, plates={_balancePlates.Count}, piston={levelPistonBase}, exit={wallExit}.");
                GetTree().Quit(1);
                return;
            }

            GD.Print("ROOM20_MECHANICS_PASS: forty staggered fast cannons densely cover ten airborne lanes inside a low-gravity steering volume, with a lever-gated two-plate transit, level piston base and wall-mounted exit.");
            GetTree().Quit(0);
            return;
        }

        _playerCannonFired = true;
        _nextGauntletCheckpoint = _gauntletCheckpoints.Count;
        _movingBoarded = true;
        _movingStayedAboard = true;
        _transitLeverActivated = true;
        _nextBalancePlate = _balancePlates.Count;
        _movingArrived = true;
        _pistonArmed = true;
        _pistonFired = true;
        _pistonLaunchSpeed = 20.2f;
        _nextPistonFlightGate = _pistonFlightGates.Count;
        _cleanAssemblyEligible = _runAchievementPositiveSmoke;
        _projectileHits = _runAchievementPositiveSmoke ? 0 : 1;
        foreach (InterferenceCannon3D cannon in _interferenceCannons)
        {
            typeof(InterferenceCannon3D).GetProperty(nameof(InterferenceCannon3D.ShotsFired))?.SetValue(cannon, 1);
        }
        TryAwardCleanAssembly();
        bool awarded = CompletedAdvancementIds.Contains("clean-assembly");
        bool expected = _runAchievementPositiveSmoke;
        if (awarded != expected)
        {
            GD.PushError($"ROOM20_ACHIEVEMENT_FAIL: expected={expected}, awarded={awarded}, hits={_projectileHits}, clean={_cleanAssemblyEligible}.");
            GetTree().Quit(1);
            return;
        }

        GD.Print(expected
            ? "ROOM20_ACHIEVEMENT_POSITIVE_PASS: a hit-free rail-clean four-stage run awarded Clean Assembly."
            : "ROOM20_ACHIEVEMENT_NEGATIVE_PASS: a projectile hit denied Clean Assembly without blocking room completion.");
        GetTree().Quit(0);
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM20_SOLUTION_FAIL: {message}");
        FinishSolutionSmoke(1);
    }

    private async void FinishSolutionSmoke(int code)
    {
        if (_solutionSmokeFinishing) { return; }
        _solutionSmokeFinishing = true;
        if (_player is not null) { _player.SimulatedMoveInput = null; }
        foreach (FlightGate3D gate in _pistonFlightGates)
        {
            gate.ResetGate();
            gate.QueueFree();
        }
        _pistonFlightGates.Clear();
        _solutionTrace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(code);
    }
}
