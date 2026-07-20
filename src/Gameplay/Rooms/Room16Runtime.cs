using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Rooms;
using Velocitex.Core.Settings;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Interaction;
using Velocitex.Gameplay.Player;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room16Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_16_solution.tres";
    private const byte InteractAction = 1;
    private const int RequiredSolutionRuns = 10;
    private const int MaximumSolutionTicksPerRun = 1200;
    private const float UncalibratedCannonYawDegrees = 14.0f;
    private const float BullseyeRadius = 1.0f;
    private const float AdvancementRadius = 0.25f;
    private const float ExitWallZ = -44.0f;

    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private PlayerCannon3D _cannon = null!;
    private RouteCheckpoint3D _landingLatch = null!;
    private FlightGate3D _accelerationRing = null!;
    private MeshInstance3D _aimButton = null!;
    private StaticBody3D _landingDeck = null!;
    private ExitDoor3D _exitDoor = null!;
    private AudioStreamPlayer3D? _fireAudio;
    private Tween? _aimTween;
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private bool _showPrompts;
    private bool _highContrastPrompts;
    private bool _fired;
    private bool _aimCalibrated;
    private bool _hitBullseye;
    private bool _activatedLandingLatch;
    private bool _runSolutionSmoke;
    private bool _runMechanicsSmoke;
    private bool _runUncalibratedFireSmoke;
    private bool _runStraightLandingSmoke;
    private bool _runAchievementPositiveSmoke;
    private bool _runAchievementNegativeSmoke;
    private bool _runPreview;
    private bool _runPanoramaCapture;
    private bool _runShellSmoke;
    private bool _solutionSmokeFinishing;
    private int _solutionRun;
    private int _solutionTick;
    private int _previewFrames;
    private int _shellSmokeTick;
    private int _uncalibratedSmokeTick;
    private int _straightLandingSmokeTick;
    private float _bullseyeOffset;
    private bool _uncalibratedShotMissed;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] arguments = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(arguments, value => value == "--room16-solution-smoke");
        _runMechanicsSmoke = Array.Exists(arguments, value => value == "--room16-mechanics-smoke");
        _runUncalibratedFireSmoke = Array.Exists(arguments, value => value == "--room16-uncalibrated-fire-smoke");
        _runStraightLandingSmoke = Array.Exists(arguments, value => value == "--room16-straight-landing-smoke");
        _runAchievementPositiveSmoke = Array.Exists(arguments, value => value == "--room16-achievement-positive-smoke");
        _runAchievementNegativeSmoke = Array.Exists(arguments, value => value == "--room16-achievement-negative-smoke");
        _runPreview = Array.Exists(arguments, value => value == "--room16-preview");
        _runShellSmoke = Array.Exists(arguments, value => value == "--room-shell-smoke");

        BuildRoom();
        BuildGoal();
        _runPanoramaCapture = PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room16_a", new Vector3(7.5f, 12.5f, 29.0f), new Vector3(0.0f, 12.0f, -27.0f), 57.0f),
            new("room16_b", new Vector3(-8.0f, 17.0f, -20.0f), new Vector3(0.0f, 11.0f, 7.0f), 58.0f),
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
        _cannon.SetKeyLabel(key == Key.None ? "E" : key.ToString());
        if (_runPreview || _runPanoramaCapture)
        {
            _cameraRig.SetInputEnabled(false);
            _showPrompts = false;
        }

        _cannon.Fired += body =>
        {
            if (body == _player)
            {
                _fired = true;
                _accelerationRing.ResetGate();
                _fireAudio?.Play();
            }
        };

        if (_runSolutionSmoke)
        {
            _showPrompts = false;
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            if (_solutionTrace is null ||
                _solutionTrace.RoomId != RoomId ||
                _solutionTrace.MoveInputs.Count < 5 ||
                _solutionTrace.MoveDurationsTicks.Length != _solutionTrace.MoveInputs.Count ||
                !_solutionTrace.ActionFlags.Contains(InteractAction) ||
                !_solutionTrace.MoveInputs.Any(input => input.X < -0.25f) ||
                !_solutionTrace.MoveInputs.Any(input => input.X > 0.25f))
            {
                FailSolutionSmoke("The SolutionTrace must operate the aim button, fire the cannon and steer both ways on the compact landing deck.");
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
            string path = ProjectSettings.GlobalizePath("user://room16-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"ROOM16_PREVIEW_CAPTURE: {path}");
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

        if (_runUncalibratedFireSmoke)
        {
            RunUncalibratedFireSmokeTick();
            return;
        }

        if (_runStraightLandingSmoke)
        {
            RunStraightLandingSmokeTick();
            return;
        }

        if (_runMechanicsSmoke || _runAchievementPositiveSmoke || _runAchievementNegativeSmoke)
        {
            return;
        }

        if (_runSolutionSmoke)
        {
            RunSolutionTick();
            return;
        }

        bool canInteract = _cannon.CanInteract(_player);
        bool focused = canInteract && _cameraRig.IsLookingAt(_cannon.GlobalPosition + Vector3.Up * 1.8f);
        _cannon.SetFocused(focused && _showPrompts, _highContrastPrompts);
        if (focused && Godot.Input.IsActionJustPressed(InputDefaults.Interact))
        {
            _cannon.Interact(_player);
        }

        if (_player.GlobalPosition.Y < -7.0f)
        {
            RestartRoom();
        }
    }

    public override void RestartRoom()
    {
        if (_runUncalibratedFireSmoke && _uncalibratedSmokeTick > 1)
        {
            _uncalibratedShotMissed |= _fired && !_hitBullseye && !_activatedLandingLatch && !IsComplete;
            ClearCompletionState();
            _player.SimulatedMoveInput = null;
            _player.ResetTo(_spawnTransform);
            ResetPuzzleState();
            return;
        }

        if (_runStraightLandingSmoke && _straightLandingSmokeTick > 1)
        {
            FailSmoke("ROOM16_STRAIGHT_LANDING_FAIL", "the centered no-steering landing fell off the compact catch deck.");
            return;
        }

        if (_runSolutionSmoke && _solutionTick > 0 && !_solutionSmokeFinishing)
        {
            FailSolutionSmoke(
                $"Run {_solutionRun + 1} hit the maintenance floor at {_player.GlobalPosition} with velocity {_player.LinearVelocity}; " +
                $"calibrated={_aimCalibrated}, fired={_fired}, bullseye={_hitBullseye}, latch={_activatedLandingLatch}, offset={_bullseyeOffset:F2}.");
            return;
        }

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

        if (_shellSmokeTick < 12)
        {
            return;
        }

        if (_player.GlobalPosition.DistanceTo(_spawnTransform.Origin) > 0.15f)
        {
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 16 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 16 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void RunUncalibratedFireSmokeTick()
    {
        _uncalibratedSmokeTick++;
        if (_uncalibratedSmokeTick == 1)
        {
            if (!_cannon.CanInteract(_player))
            {
                FailSmoke("ROOM16_UNCALIBRATED_FIRE_FAIL", "the untouched cannon was not interactable from the safe start.");
                return;
            }

            _cannon.Interact(_player);
            if (!_fired || _aimCalibrated)
            {
                FailSmoke("ROOM16_UNCALIBRATED_FIRE_FAIL", "the untouched cannon did not fire in its visibly offset state.");
            }
            return;
        }

        if (_uncalibratedSmokeTick < 360)
        {
            return;
        }

        if (!_uncalibratedShotMissed || _hitBullseye || _activatedLandingLatch || IsComplete)
        {
            FailSmoke(
                "ROOM16_UNCALIBRATED_FIRE_FAIL",
                $"missed={_uncalibratedShotMissed}, bullseye={_hitBullseye}, latch={_activatedLandingLatch}, complete={IsComplete}.");
            return;
        }

        GD.Print("ROOM16_UNCALIBRATED_FIRE_PASS: firing before pressing the orange aim control missed the target and fell to the maintenance floor.");
        FinishSmoke(0);
    }

    private void RunStraightLandingSmokeTick()
    {
        _straightLandingSmokeTick++;
        if (_straightLandingSmokeTick == 1)
        {
            ApplyAimCalibration();
            _cannon.Interact(_player);
            if (!_fired)
            {
                FailSmoke("ROOM16_STRAIGHT_LANDING_FAIL", "the calibrated cannon did not fire.");
            }
            return;
        }

        if (_straightLandingSmokeTick < 360)
        {
            return;
        }

        if (!_hitBullseye || _activatedLandingLatch || IsComplete)
        {
            FailSmoke(
                "ROOM16_STRAIGHT_LANDING_FAIL",
                $"bullseye={_hitBullseye}, latch={_activatedLandingLatch}, complete={IsComplete}, position={_player.GlobalPosition}.");
            return;
        }

        GD.Print("ROOM16_STRAIGHT_LANDING_PASS: a centered cannon landing hit the target but missed the offset latch and could not complete without grounded steering.");
        FinishSmoke(0);
    }

    private void RunSolutionTick()
    {
        if (_solutionTrace is null || _solutionSmokeFinishing)
        {
            return;
        }

        if (IsComplete)
        {
            if (!_aimCalibrated || !_fired || !_hitBullseye || !_activatedLandingLatch ||
                !CompletedAdvancementIds.Contains("bullseye"))
            {
                FailSolutionSmoke(
                    $"Run {_solutionRun + 1} bypassed the cannon puzzle: calibrated={_aimCalibrated}, fired={_fired}, " +
                    $"bullseye={_hitBullseye}, latch={_activatedLandingLatch}, achievement={CompletedAdvancementIds.Contains("bullseye")}, offset={_bullseyeOffset:F2}.");
                return;
            }

            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print(
                    $"ROOM16_SOLUTION_PASS: SolutionTrace deliberately aligned and fired the cannon, passed the target center, " +
                    $"steered through the offset landing latch and reached the wall-mounted exit with {_bullseyeOffset:F2} m target offset " +
                    $"for {_solutionRun} consecutive completions; bullseye=True.");
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
            FailSolutionSmoke(
                $"Run {_solutionRun + 1} timed out at {_player.GlobalPosition} with velocity {_player.LinearVelocity}; " +
                $"calibrated={_aimCalibrated}, fired={_fired}, bullseye={_hitBullseye}, latch={_activatedLandingLatch}.");
            return;
        }

        (Vector2 move, byte flags) = ResolveTraceStep(_solutionTick - 1);
        _player.SimulatedMoveInput = move;
        if ((flags & InteractAction) != 0)
        {
            _cannon.Interact(_player);
        }

        if (_solutionTick % 30 == 0)
        {
            GD.Print(
                $"ROOM16_TRACE: tick={_solutionTick}, input={move}, position={_player.GlobalPosition}, velocity={_player.LinearVelocity}, " +
                $"grounded={_player.IsGrounded}, calibrated={_aimCalibrated}, fired={_fired}, bullseye={_hitBullseye}, " +
                $"latch={_activatedLandingLatch}, offset={_bullseyeOffset:F2}.");
        }
    }

    private (Vector2 Move, byte Flags) ResolveTraceStep(int tick)
    {
        if (_solutionTrace is null)
        {
            return (Vector2.Zero, 0);
        }

        int remaining = tick;
        for (int index = 0; index < _solutionTrace.MoveInputs.Count; index++)
        {
            int duration = _solutionTrace.MoveDurationsTicks[index];
            if (remaining < duration)
            {
                return (_solutionTrace.MoveInputs[index], _solutionTrace.ActionFlags[index]);
            }

            remaining -= duration;
        }

        return (_solutionTrace.HoldLastInput ? _solutionTrace.MoveInputs[^1] : Vector2.Zero, 0);
    }

    private void RunRequestedSmoke()
    {
        if (_runMechanicsSmoke)
        {
            float initialYaw = Mathf.RadToDeg(_cannon.Rotation.Y);
            float initialLateralVelocity = Mathf.Abs(_cannon.PreviewImpulse(_player).X);
            ApplyAimCalibration();
            float calibratedYaw = Mathf.Abs(Mathf.RadToDeg(_cannon.Rotation.Y));
            float calibratedLateralVelocity = Mathf.Abs(_cannon.PreviewImpulse(_player).X);

            CollisionShape3D? landingCollision = _landingDeck.GetChildren().OfType<CollisionShape3D>().FirstOrDefault();
            bool compactDeck = landingCollision?.Shape is BoxShape3D landingShape &&
                landingShape.Size.X <= 10.0f && landingShape.Size.Z <= 20.0f;
            bool meaningfulAimControl = initialYaw >= 13.5f && initialLateralVelocity >= 3.5f &&
                calibratedYaw <= 0.01f && calibratedLateralVelocity <= 0.01f;
            bool offsetLandingChoice = Mathf.Abs(_landingLatch.Position.X) >= 1.4f &&
                _landingLatch.TriggerSize.X <= 2.1f && _landingLatch.TriggerSize.Z >= 12.0f;
            bool wallMountedExit = Mathf.Abs(_exitDoor.GlobalPosition.Z - ExitWallZ) <= 0.02f &&
                Mathf.Abs(GetNode<Area3D>("GoalCup").Position.Z - (ExitWallZ + 1.08f)) <= 0.02f;
            bool cannonSolid = _cannon.HasSolidBodyHitbox;

            if (!meaningfulAimControl || !compactDeck || !offsetLandingChoice || !wallMountedExit || !cannonSolid)
            {
                FailSmoke(
                    "ROOM16_MECHANICS_FAIL",
                    $"aim={meaningfulAimControl} ({initialYaw:F2}deg/{initialLateralVelocity:F2}mps -> {calibratedYaw:F2}deg/{calibratedLateralVelocity:F2}mps), " +
                    $"compact={compactDeck}, landing_choice={offsetLandingChoice}, wall_exit={wallMountedExit}, cannon_hitbox={cannonSolid} at {_exitDoor.GlobalPosition}.");
                return;
            }

            GD.Print(
                "ROOM16_MECHANICS_PASS: the orange control removes a 14-degree target miss, the cannon requires a deliberate fire, " +
                "the landing latch requires grounded steering, and the compact catch deck ends flush at a carved shell-wall exit.");
            FinishSmoke(0);
            return;
        }

        _aimCalibrated = true;
        _fired = true;
        _hitBullseye = true;
        _activatedLandingLatch = true;
        _bullseyeOffset = _runAchievementPositiveSmoke ? AdvancementRadius - 0.05f : AdvancementRadius + 0.05f;
        TryAwardBullseye();
        bool awarded = CompletedAdvancementIds.Contains("bullseye");
        if (_runAchievementPositiveSmoke && !awarded)
        {
            FailSmoke("ROOM16_ACHIEVEMENT_FAIL", "a 0.20 m center pass did not award Bullseye.");
            return;
        }

        if (_runAchievementNegativeSmoke && awarded)
        {
            FailSmoke("ROOM16_ACHIEVEMENT_FAIL", "a 0.30 m off-center pass incorrectly awarded Bullseye.");
            return;
        }

        GD.Print(_runAchievementPositiveSmoke
            ? "ROOM16_ACHIEVEMENT_POSITIVE_PASS: a 0.20 m target-center pass awarded Bullseye."
            : "ROOM16_ACHIEVEMENT_NEGATIVE_PASS: a valid but 0.30 m off-center target pass completed no Bullseye condition.");
        FinishSmoke(0);
    }

    private void ResetPuzzleState()
    {
        _fired = false;
        _aimCalibrated = false;
        _hitBullseye = false;
        _activatedLandingLatch = false;
        _bullseyeOffset = 0.0f;
        _fireAudio?.Stop();
        _aimTween?.Kill();
        _aimTween = null;
        _cannon.ResetCannon();
        _accelerationRing.ResetGate();
        _cannon.Rotation = new Vector3(0.0f, Mathf.DegToRad(UncalibratedCannonYawDegrees), 0.0f);
        _aimButton.Position = new Vector3(_aimButton.Position.X, 6.48f, _aimButton.Position.Z);
        _landingLatch.ResetCheckpoint();
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string copper = "res://assets/textures/copper_rivets.svg";
        Color pale = new("b9b2a8");
        Color frame = new("665a50");

        RoomGeometry.AddClosedRoomShell(
            this,
            "RoomShell",
            new Vector3(0.0f, 0.0f, -6.0f),
            new Vector2(28.0f, 76.0f),
            -3.0f,
            30.0f,
            metal,
            new Color("807267"),
            new Color("4d423b"),
            body =>
            {
                if (body is PlayerBall)
                {
                    RestartRoom();
                }
            });

        RoomGeometry.AddBox(this, "SafeStart", new Vector3(12.0f, 0.5f, 16.775f), new Vector3(0.0f, 6.0f, 23.3875f), Vector3.Zero, metal, pale, 0.4f, 0.66f);
        _landingDeck = RoomGeometry.AddBox(this, "CompactLandingDeck", new Vector3(9.5f, 0.5f, 19.55f), new Vector3(0.0f, 10.0f, -34.0f), Vector3.Zero, metal, pale.Darkened(0.04f), 0.4f, 0.66f);

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(this, $"StartRail{side}", new Vector3(0.36f, 1.4f, 16.775f), new Vector3(side * 6.18f, 6.75f, 23.3875f), Vector3.Zero, copper, frame, 0.42f, 0.58f);
            RoomGeometry.AddBox(this, $"LandingRail{side}", new Vector3(0.36f, 1.45f, 19.55f), new Vector3(side * 4.93f, 10.75f, -34.0f), Vector3.Zero, metal, frame, 0.42f, 0.62f);
        }

        _cannon = new PlayerCannon3D
        {
            Name = "PlayerCannon",
            Position = new Vector3(0.0f, 6.25f, 19.0f),
            Rotation = new Vector3(0.0f, Mathf.DegToRad(UncalibratedCannonYawDegrees), 0.0f),
            LaunchVelocity = new Vector3(0.0f, 12.5f, -18.0f),
            MuzzleOffset = new Vector3(0.0f, 2.2f, -2.0f),
            ActivationRadius = 4.5f,
        };
        AddChild(_cannon);

        AddAimCalibrationPad();
        _landingLatch = new RouteCheckpoint3D
        {
            Name = "OffsetLandingLatch",
            CheckpointIndex = 0,
            Position = new Vector3(2.0f, 10.95f, -35.0f),
            TriggerSize = new Vector3(2.0f, 3.0f, 14.0f),
            FrameTint = new Color("8a6e58"),
            FlatFloorMarker = true,
        };
        _landingLatch.Entered += (latch, player) =>
        {
            if (player == _player && _fired && _hitBullseye)
            {
                latch.Activate();
                _activatedLandingLatch = true;
                TryCompleteFromExitApproach();
            }
            else if (player == _player)
            {
                latch.FlashDenied();
            }
        };
        AddChild(_landingLatch);

        AddAccelerationRing();
        bool enableAudio = !_runSolutionSmoke && !_runMechanicsSmoke && !_runUncalibratedFireSmoke && !_runStraightLandingSmoke &&
            !_runAchievementPositiveSmoke && !_runAchievementNegativeSmoke;
        if (enableAudio)
        {
            _fireAudio = new AudioStreamPlayer3D
            {
                Name = "CannonFireSfx",
                Stream = GD.Load<AudioStream>("res://assets/audio/sfx/device_player_cannon_fire.wav"),
                Bus = "SFX",
                Position = _cannon.Position,
                MaxDistance = 45.0f,
                UnitSize = 9.0f,
            };
            AddChild(_fireAudio);
        }
    }

    private void AddAimCalibrationPad()
    {
        Vector3 padPosition = new(-4.0f, 6.3f, 25.5f);
        Area3D pad = new()
        {
            Name = "OrangeAimControl",
            Position = padPosition + Vector3.Up * 0.55f,
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true,
        };
        pad.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3.0f, 1.2f, 3.0f) } });
        pad.BodyEntered += body =>
        {
            if (body == _player)
            {
                ApplyAimCalibration();
            }
        };
        AddChild(pad);

        StandardMaterial3D buttonMaterial = RoomGeometry.CreateMaterial(
            "res://assets/textures/copper_rivets.svg",
            new Color("d07835"),
            0.48f,
            0.5f,
            emissionEnabled: true,
            emission: new Color("6b2b0d"));
        StandardMaterial3D cableMaterial = RoomGeometry.CreateMaterial(
            "res://assets/textures/rubber_chevrons.svg",
            new Color("3b302b"),
            0.08f,
            0.9f);
        RoomGeometry.AddVisualBox(this, "AimControlBase", new Vector3(3.0f, 0.12f, 3.0f), padPosition, Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, cableMaterial);
        _aimButton = RoomGeometry.AddCylinder(this, "OrangeAimButton", new Vector3(padPosition.X, 6.48f, padPosition.Z), Vector3.Zero, 0.82f, 0.18f, buttonMaterial);
        RoomGeometry.AddVisualBox(this, "AimCalibrationCable", new Vector3(0.16f, 0.02f, 7.63f), new Vector3(-2.0f, 6.305f, 22.25f), new Vector3(0.0f, Mathf.DegToRad(-31.6f), 0.0f), string.Empty, Colors.White, 0.0f, 1.0f, cableMaterial);

        for (int index = 0; index < 3; index++)
        {
            float yaw = Mathf.DegToRad(UncalibratedCannonYawDegrees * (index / 2.0f));
            RoomGeometry.AddVisualBox(
                this,
                $"AimIndex{index}",
                new Vector3(0.14f, 0.04f, 1.25f),
                new Vector3(Mathf.Sin(yaw) * 2.15f, 6.49f, 19.0f - (Mathf.Cos(yaw) * 2.15f)),
                new Vector3(0.0f, yaw, 0.0f),
                string.Empty,
                Colors.White,
                0.0f,
                1.0f,
                index == 0 ? buttonMaterial : cableMaterial);
        }
    }

    private void ApplyAimCalibration()
    {
        if (_aimCalibrated)
        {
            return;
        }

        _aimCalibrated = true;
        _aimTween?.Kill();
        if (_runSolutionSmoke || _runMechanicsSmoke || _runStraightLandingSmoke ||
            _runAchievementPositiveSmoke || _runAchievementNegativeSmoke)
        {
            _cannon.Rotation = Vector3.Zero;
            _aimButton.Position = new Vector3(_aimButton.Position.X, 6.39f, _aimButton.Position.Z);
            return;
        }

        _aimTween = CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _aimTween.TweenProperty(_cannon, "rotation:y", 0.0f, 0.55f);
        _aimTween.TweenProperty(_aimButton, "position:y", 6.39f, 0.16f);
    }

    private void AddAccelerationRing()
    {
        Vector3 center = new(0.0f, 16.25f, -8.0f);
        _accelerationRing = new FlightGate3D
        {
            Name = "AccelerationRing",
            Position = center,
            Radius = 2.2f,
            MinimumExitSpeed = 0.0f,
            SpeedGain = 4.0f,
            SpeedMultiplier = 1.12f,
            MaximumExitSpeed = 36.0f,
            AxialBoostOnly = true,
            MaximumDownwardExitSpeed = 8.0f,
            EnableAudio = !_runSolutionSmoke && !_runMechanicsSmoke && !_runStraightLandingSmoke &&
                !_runAchievementPositiveSmoke && !_runAchievementNegativeSmoke,
        };
        _accelerationRing.Passed += body =>
        {
            if (body != _player || !_fired)
            {
                return;
            }

            _bullseyeOffset = new Vector2(body.GlobalPosition.X - center.X, body.GlobalPosition.Y - center.Y).Length();
            _hitBullseye = true;
        };
        AddChild(_accelerationRing);
    }

    private void BuildGoal()
    {
        Vector3 position = new(0.0f, 11.15f, ExitWallZ + 1.08f);
        Area3D goal = new()
        {
            Name = "GoalCup",
            Position = position,
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true,
        };
        goal.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 2.0f, Height = 2.8f } });
        goal.BodyEntered += body =>
        {
            if (body is PlayerBall)
            {
                TryCompleteFromExitApproach();
            }
        };
        AddChild(goal);
        _exitDoor = RoomGeometry.AddGoalExitDoor(this, position, Vector3.Forward);
    }

    private bool CanCompletePuzzle() => _aimCalibrated && _fired && _hitBullseye && _activatedLandingLatch;

    private void TryCompleteFromExitApproach()
    {
        Area3D? goal = GetNodeOrNull<Area3D>("GoalCup");
        if (!CanCompletePuzzle() || goal is null || !goal.OverlapsBody(_player))
        {
            return;
        }

        TryAwardBullseye();
        CompleteRoom();
    }

    private void TryAwardBullseye()
    {
        if (CanCompletePuzzle() && _bullseyeOffset <= AdvancementRadius)
        {
            MarkAdvancementCondition("bullseye");
        }
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM16_SOLUTION_FAIL: {message}");
        FinishSolutionSmoke(1);
    }

    private async void FinishSolutionSmoke(int code)
    {
        if (_solutionSmokeFinishing)
        {
            return;
        }

        _solutionSmokeFinishing = true;
        _player.SimulatedMoveInput = null;
        if (_fireAudio is not null)
        {
            _fireAudio.Stop();
            _fireAudio.Stream = null;
        }
        _landingLatch.ResetCheckpoint();
        _landingLatch.QueueFree();
        _solutionTrace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(code);
    }

    private void FailSmoke(string marker, string message)
    {
        GD.PushError($"{marker}: {message}");
        FinishSmoke(1);
    }

    private async void FinishSmoke(int code)
    {
        SceneTree tree = GetTree();
        _player.SimulatedMoveInput = null;
        if (_fireAudio is not null)
        {
            _fireAudio.Stop();
            _fireAudio.Stream = null;
        }
        QueueFree();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        tree.Quit(code);
    }

    public override void _ExitTree()
    {
        if (_fireAudio is not null)
        {
            _fireAudio.Stop();
            _fireAudio.Stream = null;
        }
        _solutionTrace = null;
    }
}
