using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Physics;
using Velocitex.Core.Rooms;
using Velocitex.Core.Settings;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Interaction;
using Velocitex.Gameplay.Player;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room08Runtime : RoomRuntime
{
    private sealed record BoostZone(int Index, StaticBody3D Surface, Vector3 Size);

    private const string TracePath = "res://resources/solutions/room_08_solution.tres";
    private const string SurfacePath = "res://resources/surfaces/accelerator.tres";
    private const string MaterialPath = "res://resources/materials/accelerator_belt.tres";
    private const string ContactSfxPath = "res://assets/audio/sfx/surface_accelerator_contact.wav";
    private const byte InteractAction = 1;
    private const int RequiredAcceleratorMask = 0b111;
    private const int RequiredSolutionRuns = 10;
    private const int MaximumSolutionTicksPerRun = 1800;
    private const int MaximumBlueStreakTicks = 900;
    private const float RequiredSpeedGain = 2.5f;

    private readonly List<BoostZone> _boostZones = new();

    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private MechanicalLever _routeLever = null!;
    private StaticBody3D _routeGate = null!;
    private Area3D _goal = null!;
    private AudioStreamPlayer3D? _acceleratorContactAudio;
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private bool _runSolutionSmoke;
    private bool _runPreview;
    private bool _runShellSmoke;
    private bool _runAchievementPositiveSmoke;
    private bool _runAchievementNegativeSmoke;
    private bool _suppressDeviceAudio;
    private bool _reducedMotion;
    private bool _solutionSmokeFinishing;
    private bool _blueStreakEligible;
    private bool _routeLeverActivated;
    private bool _showInteractionPrompts;
    private bool _highContrastPrompts;
    private int _missingAcceleratorSmokeIndex = -1;
    private int _acceleratorTouchedMask;
    private int _acceleratorVerifiedMask;
    private int _nextAcceleratorIndex;
    private int _activeAcceleratorIndex = -1;
    private int _blueStreakStartTick = -1;
    private int _roomTick;
    private int _solutionRun;
    private int _solutionTick;
    private int _previewFrames;
    private int _shellSmokeTick;
    private float _activeEntrySpeed;
    private float _activeMaximumSpeed;
    private Vector3 _routeGateClosedPosition;
    private Tween? _routeGateTween;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] userArguments = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(userArguments, argument => argument == "--room08-solution-smoke");
        _runPreview = Array.Exists(userArguments, argument => argument == "--room08-preview");
        _runShellSmoke = Array.Exists(userArguments, argument => argument == "--room-shell-smoke");
        _runAchievementPositiveSmoke = Array.Exists(userArguments, argument => argument == "--room08-achievement-positive-solution-smoke");
        _runAchievementNegativeSmoke = Array.Exists(userArguments, argument => argument == "--room08-achievement-negative-solution-smoke");
        foreach (string argument in userArguments)
        {
            const string missingPrefix = "--room08-missing-accelerator-solution-smoke=";
            if (argument.StartsWith(missingPrefix, StringComparison.Ordinal) &&
                int.TryParse(argument[missingPrefix.Length..], out int parsedIndex))
            {
                _missingAcceleratorSmokeIndex = parsedIndex;
            }
        }

        bool panoramaCapture = Array.Exists(userArguments, argument => argument.StartsWith("--panorama-capture=", StringComparison.Ordinal));
        _suppressDeviceAudio = panoramaCapture || _runPreview || userArguments.Any(argument =>
            argument.Contains("smoke", StringComparison.Ordinal) ||
            argument.Contains("bypass", StringComparison.Ordinal) ||
            argument.StartsWith("--surface-room=", StringComparison.Ordinal) ||
            argument.StartsWith("--containment-room=", StringComparison.Ordinal));
        _reducedMotion = SettingsStore.Load().ReducedMotion || _runPreview || panoramaCapture;

        BuildRoom();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room08_a", new Vector3(16.0f, 12.0f, 29.0f), new Vector3(-2.0f, 5.0f, 2.0f), 57.0f),
            new("room08_b", new Vector3(-15.0f, 11.0f, 4.0f), new Vector3(6.0f, 5.1f, -8.0f), 58.0f),
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
        _routeLever.SetKeyLabel(interactKey == Key.None ? "E" : interactKey.ToString());
        if (panoramaCapture)
        {
            _showInteractionPrompts = false;
        }
        if (_runPreview)
        {
            _cameraRig.SetInputEnabled(false);
        }

        ResetPuzzleState();
        if (_runSolutionSmoke)
        {
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            if (_solutionTrace is null ||
                _solutionTrace.RoomId != RoomId ||
                _solutionTrace.MoveInputs.Count < 5 ||
                _solutionTrace.MoveDurationsTicks.Length != _solutionTrace.MoveInputs.Count ||
                _solutionTrace.ActionFlags.Length != _solutionTrace.MoveInputs.Count ||
                !_solutionTrace.ActionFlags.Contains(InteractAction) ||
                !_solutionTrace.MoveInputs.Any(input => input.X > 0.2f) ||
                !_solutionTrace.MoveInputs.Any(input => input.Y < -0.2f) ||
                !_solutionTrace.MoveInputs.Any(input => input.LengthSquared() < 0.0001f))
            {
                string details = _solutionTrace is null
                    ? "trace=null"
                    : $"trace_room='{_solutionTrace.RoomId}', inputs={_solutionTrace.MoveInputs.Count}, durations={_solutionTrace.MoveDurationsTicks.Length}";
                FailSolutionSmoke($"The Room 08 SolutionTrace is invalid ({details}).");
            }
        }

        if (_runAchievementPositiveSmoke || _runAchievementNegativeSmoke || _missingAcceleratorSmokeIndex >= 0)
        {
            Callable.From(RunAchievementSmoke).CallDeferred();
        }
    }

    public override void _Process(double delta)
    {
        if (_runPreview && ++_previewFrames >= 30)
        {
            string capturePath = ProjectSettings.GlobalizePath("user://room08-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(capturePath);
            GD.Print($"ROOM08_PREVIEW_CAPTURE: {capturePath}");
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

        if (_runSolutionSmoke)
        {
            RunSolutionTick();
            return;
        }

        if (_runAchievementPositiveSmoke || _runAchievementNegativeSmoke || _missingAcceleratorSmokeIndex >= 0)
        {
            return;
        }

        _roomTick++;
        TrackAcceleratorSequence();
        bool canInteract = _routeLever.CanInteract(_player);
        bool isFocused = canInteract && _cameraRig.IsLookingAt(_routeLever.GlobalPosition + (Vector3.Up * 1.75f));
        _routeLever.SetFocused(isFocused && _showInteractionPrompts, _highContrastPrompts);
        if (isFocused && Godot.Input.IsActionJustPressed(InputDefaults.Interact))
        {
            _routeLever.Interact(_player);
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
            FailSolutionSmoke(
                $"Run {_solutionRun + 1} touched the hazard floor at {_player.GlobalPosition}; lever={_routeLeverActivated}, " +
                $"touched={Convert.ToString(_acceleratorTouchedMask, 2)}, verified={Convert.ToString(_acceleratorVerifiedMask, 2)}, next={_nextAcceleratorIndex}.");
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
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 08 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 08 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void RunSolutionTick()
    {
        if (_solutionTrace is null || _solutionSmokeFinishing)
        {
            return;
        }

        if (IsComplete)
        {
            if (_acceleratorTouchedMask != RequiredAcceleratorMask ||
                _acceleratorVerifiedMask != RequiredAcceleratorMask ||
                _nextAcceleratorIndex != _boostZones.Count ||
                !_routeLeverActivated ||
                !CompletedAdvancementIds.Contains("blue-streak"))
            {
                FailSolutionSmoke(
                    $"Run {_solutionRun + 1} bypassed the three-boost sequence; " +
                    $"touched={Convert.ToString(_acceleratorTouchedMask, 2)}, verified={Convert.ToString(_acceleratorVerifiedMask, 2)}, " +
                    $"next={_nextAcceleratorIndex}, lever={_routeLeverActivated}, streak={_blueStreakEligible}, achievement={CompletedAdvancementIds.Contains("blue-streak")}.");
                return;
            }

            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print(
                    $"ROOM08_SOLUTION_PASS: SolutionTrace crossed and accelerated on all three directional belts, " +
                    $"earned Blue Streak and completed Room 08 {_solutionRun} consecutive times.");
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
                $"Run {_solutionRun + 1} timed out at {_player.GlobalPosition}; speed={_player.LinearVelocity.Length():F2}, " +
                $"touched={Convert.ToString(_acceleratorTouchedMask, 2)}, verified={Convert.ToString(_acceleratorVerifiedMask, 2)}, " +
                $"next={_nextAcceleratorIndex}, active={_activeAcceleratorIndex}, streak={_blueStreakEligible}.");
            return;
        }

        _roomTick++;
        (Vector2 moveInput, byte actionFlags) = ResolveTraceStep(_solutionTick - 1);
        _player.SimulatedMoveInput = moveInput;
        if ((actionFlags & InteractAction) != 0 && !_routeLeverActivated)
        {
            if (_routeLever.CanInteract(_player))
            {
                _routeLever.Interact(_player);
            }
        }
        TrackAcceleratorSequence(moveInput);
    }

    private void TrackAcceleratorSequence(Vector2? moveInput = null)
    {
        int currentIndex = FindCurrentAcceleratorIndex();
        if (currentIndex != _activeAcceleratorIndex)
        {
            if (_activeAcceleratorIndex >= 0)
            {
                FinalizeActiveAccelerator();
            }

            _activeAcceleratorIndex = currentIndex;
            if (currentIndex >= 0)
            {
                EnterAccelerator(currentIndex);
            }
        }

        if (_activeAcceleratorIndex >= 0)
        {
            BoostZone zone = _boostZones[_activeAcceleratorIndex];
            Vector3 direction = (zone.Surface.GlobalBasis * Vector3.Forward).Normalized();
            float directionalSpeed = _player.LinearVelocity.Dot(direction);
            _activeMaximumSpeed = Mathf.Max(_activeMaximumSpeed, directionalSpeed);
        }

        if (_blueStreakStartTick < 0 || _nextAcceleratorIndex >= _boostZones.Count)
        {
            return;
        }

        int elapsed = _roomTick - _blueStreakStartTick;
        if (elapsed > MaximumBlueStreakTicks)
        {
            _blueStreakEligible = false;
        }

    }

    private int FindCurrentAcceleratorIndex()
    {
        if (_player.GroundSurfaceKind != SurfaceKind.Accelerator)
        {
            return -1;
        }

        foreach (BoostZone zone in _boostZones)
        {
            Vector3 localPlayer = zone.Surface.ToLocal(_player.GlobalPosition);
            if (Mathf.Abs(localPlayer.X) <= (zone.Size.X * 0.5f) + 0.45f &&
                Mathf.Abs(localPlayer.Z) <= (zone.Size.Z * 0.5f) + 0.45f &&
                localPlayer.Y >= 0.0f &&
                localPlayer.Y <= 1.8f)
            {
                return zone.Index;
            }
        }

        return -1;
    }

    private void EnterAccelerator(int index)
    {
        BoostZone zone = _boostZones[index];
        Vector3 direction = (zone.Surface.GlobalBasis * Vector3.Forward).Normalized();
        _activeEntrySpeed = _player.LinearVelocity.Dot(direction);
        _activeMaximumSpeed = _activeEntrySpeed;

        if (index == _nextAcceleratorIndex)
        {
            _acceleratorTouchedMask |= 1 << index;
            _nextAcceleratorIndex++;
            if (index == 0)
            {
                _blueStreakStartTick = _roomTick;
            }
        }
        else if (index != _nextAcceleratorIndex - 1)
        {
            // A contact can flicker for one physics frame at a box seam. Re-entering
            // the most recently crossed belt is still the same pass; reaching any
            // other belt out of order invalidates the optional streak.
            _blueStreakEligible = false;
        }

        if (_acceleratorContactAudio is not null)
        {
            _acceleratorContactAudio.GlobalPosition = _player.GlobalPosition;
            _acceleratorContactAudio.PitchScale = 0.93f + (index * 0.05f);
            _acceleratorContactAudio.Play();
        }
    }

    private void FinalizeActiveAccelerator()
    {
        if (_activeMaximumSpeed >= _activeEntrySpeed + RequiredSpeedGain && _activeMaximumSpeed >= 8.0f)
        {
            _acceleratorVerifiedMask |= 1 << _activeAcceleratorIndex;
        }
    }

    private (Vector2 MoveInput, byte ActionFlags) ResolveTraceStep(int tick)
    {
        if (_solutionTrace is null)
        {
            return (Vector2.Zero, (byte)0);
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

        return _solutionTrace.HoldLastInput
            ? (_solutionTrace.MoveInputs[^1], (byte)0)
            : (Vector2.Zero, (byte)0);
    }

    private void ResetPuzzleState()
    {
        _acceleratorTouchedMask = 0;
        _acceleratorVerifiedMask = 0;
        _nextAcceleratorIndex = 0;
        _activeAcceleratorIndex = -1;
        _blueStreakStartTick = -1;
        _roomTick = 0;
        _activeEntrySpeed = 0.0f;
        _activeMaximumSpeed = 0.0f;
        _blueStreakEligible = true;
        _routeLeverActivated = false;
        _routeGateTween?.Kill();
        _routeGateTween = null;
        if (_routeLever is not null)
        {
            _routeLever.ResetLever();
        }
        if (_routeGate is not null)
        {
            _routeGate.Position = _routeGateClosedPosition;
            _routeGate.CollisionLayer = 1;
            _routeGate.CollisionMask = 1;
        }
        _acceleratorContactAudio?.Stop();
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string copper = "res://assets/textures/copper_rivets.svg";
        Color paleSteel = new("a8b6b5");
        Color darkFrame = new("33484b");
        Color copperTint = new("856451");
        SurfaceProfile acceleratorProfile = GD.Load<SurfaceProfile>(SurfacePath);
        ShaderMaterial acceleratorMaterial = (ShaderMaterial)GD.Load<ShaderMaterial>(MaterialPath).Duplicate();
        acceleratorMaterial.SetShaderParameter("motion_scale", _reducedMotion ? 0.0f : 1.0f);

        RoomGeometry.AddClosedRoomShell(
            this,
            "RoomShell",
            Vector3.Zero,
            new Vector2(36.0f, 70.0f),
            -2.8f,
            16.0f,
            metal,
            new Color("71878a"),
            new Color("574c48"),
            body =>
            {
                if (body is PlayerBall)
                {
                    RestartRoom();
                }
            });

        AddTrackBox("SafeStart", new Vector3(6.0f, 0.5f, 10.525f), new Vector3(-10.0f, 4.8f, 29.5125f), metal, paleSteel);
        AddAccelerator("Accelerator01", 0, new Vector3(6.0f, 0.5f, 14.0f), new Vector3(-10.0f, 4.8f, 17.25f), Vector3.Zero, acceleratorProfile, acceleratorMaterial);
        AddTrackBox("TurnBasin01", new Vector3(10.0f, 0.5f, 8.0f), new Vector3(-8.0f, 4.8f, 6.25f), metal, paleSteel.Darkened(0.04f));
        AddAccelerator("Accelerator02", 1, new Vector3(6.0f, 0.5f, 12.0f), new Vector3(3.0f, 4.8f, 6.25f), new Vector3(0.0f, -Mathf.Pi / 2.0f, 0.0f), acceleratorProfile, acceleratorMaterial);
        AddTrackBox("TurnBasin02", new Vector3(6.0f, 0.5f, 7.0f), new Vector3(12.0f, 4.8f, 6.75f), metal, paleSteel.Darkened(0.08f));
        AddAccelerator("Accelerator03", 2, new Vector3(6.0f, 0.5f, 14.0f), new Vector3(12.0f, 4.8f, -3.75f), Vector3.Zero, acceleratorProfile, acceleratorMaterial);
        AddTrackBox("ExitRun", new Vector3(6.0f, 0.5f, 24.025f), new Vector3(12.0f, 4.8f, -22.7625f), metal, paleSteel.Darkened(0.06f));

        const float railHeight = 1.5f;
        const float railThickness = 0.34f;
        const float railY = 5.8f;
        AddRail("WestContinuousRail", new Vector3(railThickness, railHeight, 32.525f), new Vector3(-13.17f, railY, 18.5125f), metal, darkFrame);
        AddRail("StartEastRail", new Vector3(railThickness, railHeight, 24.525f), new Vector3(-6.83f, railY, 22.5125f), copper, copperTint.Darkened(0.12f));
        AddRail("FirstTurnEndRail", new Vector3(10.0f, railHeight, railThickness), new Vector3(-8.0f, railY, 2.08f), copper, copperTint.Darkened(0.15f));
        AddRail("FirstTurnNorthCap", new Vector3(4.0f, railHeight, railThickness), new Vector3(-5.0f, railY, 10.42f), metal, darkFrame);
        AddRail("CrossLaneNorthRail", new Vector3(12.0f, railHeight, railThickness), new Vector3(3.0f, railY, 9.42f), metal, darkFrame);
        AddRail("CrossLaneSouthRail", new Vector3(12.0f, railHeight, railThickness), new Vector3(3.0f, railY, 3.08f), copper, copperTint.Darkened(0.12f));
        AddRail("SecondTurnNorthRail", new Vector3(6.0f, railHeight, railThickness), new Vector3(12.0f, railY, 10.42f), metal, darkFrame);
        AddRail("EastContinuousRail", new Vector3(railThickness, railHeight, 45.025f), new Vector3(15.17f, railY, -12.2625f), copper, copperTint.Darkened(0.15f));
        AddRail("FinalLaneWestRail", new Vector3(railThickness, railHeight, 33.25f), new Vector3(8.83f, railY, -13.375f), metal, darkFrame);

        _routeLever = new MechanicalLever
        {
            Name = "PulseTurnLever",
            Position = new Vector3(-5.15f, 5.05f, 5.85f),
            ActivationRadius = 5.8f,
        };
        _routeLever.Activated += OnRouteLeverActivated;
        AddChild(_routeLever);
        AlignLeverToFloor();
        _routeGate = RoomGeometry.AddBox(
            this,
            "CrossLaneGate",
            new Vector3(0.42f, 4.5f, 6.0f),
            new Vector3(-2.72f, 7.3f, 6.25f),
            Vector3.Zero,
            copper,
            copperTint.Darkened(0.08f),
            0.42f,
            0.54f);
        _routeGateClosedPosition = _routeGate.Position;

        AddSequenceBeacon(0, new Vector3(-10.0f, 7.35f, 12.0f), Vector3.Zero, metal, copper);
        AddSequenceBeacon(1, new Vector3(8.0f, 7.35f, 6.25f), new Vector3(0.0f, -Mathf.Pi / 2.0f, 0.0f), metal, copper);
        AddSequenceBeacon(2, new Vector3(12.0f, 7.35f, -10.0f), Vector3.Zero, metal, copper);

        if (!_suppressDeviceAudio)
        {
            _acceleratorContactAudio = new AudioStreamPlayer3D
            {
                Name = "AcceleratorContactSfx",
                Stream = GD.Load<AudioStream>(ContactSfxPath),
                Bus = "SFX",
                MaxDistance = 28.0f,
                UnitSize = 6.0f,
            };
            AddChild(_acceleratorContactAudio);
        }
    }

    private StaticBody3D AddTrackBox(string name, Vector3 size, Vector3 position, string texture, Color tint)
    {
        return RoomGeometry.AddBox(this, name, size, position, Vector3.Zero, texture, tint, 0.38f, 0.68f);
    }

    private void AddAccelerator(
        string name,
        int index,
        Vector3 size,
        Vector3 position,
        Vector3 rotation,
        SurfaceProfile profile,
        ShaderMaterial material)
    {
        StaticBody3D accelerator = RoomGeometry.AddBox(
            this,
            name,
            size,
            position,
            rotation,
            string.Empty,
            Colors.White,
            0.0f,
            0.58f,
            friction: profile.Friction,
            surfaceProfile: profile,
            materialOverride: material);
        _boostZones.Add(new BoostZone(index, accelerator, size));
        AddAttachedBoostRoller(accelerator, size);
    }

    private void AddRail(string name, Vector3 size, Vector3 position, string texture, Color tint)
    {
        RoomGeometry.AddBox(this, name, size, position, Vector3.Zero, texture, tint, 0.42f, 0.62f);
    }

    private void OnRouteLeverActivated()
    {
        if (_routeLeverActivated)
        {
            return;
        }

        _routeLeverActivated = true;
        _routeGate.CollisionLayer = 0;
        _routeGate.CollisionMask = 0;
        _routeGateTween?.Kill();
        _routeGateTween = CreateTween().SetTrans(Tween.TransitionType.Quint).SetEase(Tween.EaseType.Out);
        _routeGateTween.TweenProperty(_routeGate, "position:y", _routeGateClosedPosition.Y + 5.2f, 0.62f);
    }

    private void AlignLeverToFloor()
    {
        if (_routeLever.GetNodeOrNull<MeshInstance3D>("Pedestal") is MeshInstance3D pedestal)
        {
            pedestal.Rotation = Vector3.Zero;
        }
        if (_routeLever.GetNodeOrNull<CollisionShape3D>("BaseCollision/PedestalHitbox") is CollisionShape3D pedestalHitbox)
        {
            pedestalHitbox.Rotation = Vector3.Zero;
        }
    }

    private void AddSequenceBeacon(int index, Vector3 position, Vector3 rotation, string metalTexture, string copperTexture)
    {
        Node3D beacon = new() { Name = $"BoostBeacon{index + 1}", Position = position, Rotation = rotation };
        AddChild(beacon);
        StandardMaterial3D frameMaterial = RoomGeometry.CreateMaterial(metalTexture, new Color("40565a"), 0.48f, 0.52f);
        StandardMaterial3D pulseMaterial = RoomGeometry.CreateMaterial(copperTexture, new Color("61c8ce"), 0.32f, 0.48f);
        RoomGeometry.AddVisualBox(beacon, "LeftPost", new Vector3(0.34f, 3.2f, 0.34f), new Vector3(-3.35f, 0.0f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frameMaterial);
        RoomGeometry.AddVisualBox(beacon, "RightPost", new Vector3(0.34f, 3.2f, 0.34f), new Vector3(3.35f, 0.0f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frameMaterial);
        RoomGeometry.AddVisualBox(beacon, "Header", new Vector3(7.04f, 0.34f, 0.34f), new Vector3(0.0f, 1.43f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frameMaterial);
        for (int bar = 0; bar <= index; bar++)
        {
            RoomGeometry.AddVisualBox(
                beacon,
                $"SequenceBar{bar + 1}",
                new Vector3(0.7f, 0.16f, 0.15f),
                new Vector3((bar - (index * 0.5f)) * 0.95f, 1.15f, -0.24f),
                Vector3.Zero,
                string.Empty,
                Colors.White,
                0.0f,
                1.0f,
                pulseMaterial);
        }
    }

    private static void AddAttachedBoostRoller(StaticBody3D accelerator, Vector3 size)
    {
        StandardMaterial3D rollerMaterial = RoomGeometry.CreateMaterial(
            "res://assets/textures/brushed_metal.png",
            new Color("29494d"),
            0.5f,
            0.46f);
        RoomGeometry.AddCylinder(
            accelerator,
            "AttachedBoostEntryRoller",
            new Vector3(0.0f, -0.03f, (size.Z * 0.5f) - 0.18f),
            new Vector3(0.0f, 0.0f, Mathf.Pi / 2.0f),
            0.28f,
            Mathf.Max(0.8f, size.X - 0.34f),
            rollerMaterial);
    }

    private void BuildGoal()
    {
        Vector3 goalPosition = new(12.0f, 5.95f, -33.695f);
        _goal = new Area3D
        {
            Name = "GoalCup",
            Position = goalPosition,
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true,
        };
        _goal.AddChild(new CollisionShape3D
        {
            Shape = new CylinderShape3D { Radius = 1.65f, Height = 2.7f },
        });
        _goal.BodyEntered += body =>
        {
            if (body is not PlayerBall ||
                _acceleratorTouchedMask != RequiredAcceleratorMask ||
                _acceleratorVerifiedMask != RequiredAcceleratorMask ||
                _nextAcceleratorIndex != _boostZones.Count ||
                !_routeLeverActivated)
            {
                return;
            }

            int streakTicks = _blueStreakStartTick < 0 ? int.MaxValue : _roomTick - _blueStreakStartTick;
            if (_blueStreakEligible && streakTicks <= MaximumBlueStreakTicks)
            {
                MarkAdvancementCondition("blue-streak");
            }
            CompleteRoom();
        };
        AddChild(_goal);
        RoomGeometry.AddGoalExitDoor(this, goalPosition);
    }

    private void RunAchievementSmoke()
    {
        int fullMask = RequiredAcceleratorMask;
        if (_missingAcceleratorSmokeIndex >= 0)
        {
            fullMask &= ~(1 << _missingAcceleratorSmokeIndex);
        }

        _acceleratorTouchedMask = fullMask;
        _acceleratorVerifiedMask = fullMask;
        _nextAcceleratorIndex = _missingAcceleratorSmokeIndex >= 0 ? 2 : _boostZones.Count;
        _routeLeverActivated = true;
        _blueStreakStartTick = 0;
        _roomTick = 120;
        _blueStreakEligible = !_runAchievementNegativeSmoke;
        _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);

        bool completed = IsComplete || IsExitTraversalPending;
        bool unlocked = CompletedAdvancementIds.Contains("blue-streak");
        if (_missingAcceleratorSmokeIndex >= 0)
        {
            if (completed || unlocked)
            {
                GD.PushError(
                    $"ROOM08_ACCELERATOR_REQUIREMENT_FAIL: missing={_missingAcceleratorSmokeIndex + 1}, completed={completed}, unlocked={unlocked}.");
                GetTree().Quit(1);
                return;
            }

            GD.Print($"ROOM08_ACCELERATOR_REQUIREMENT_PASS: accelerator {_missingAcceleratorSmokeIndex + 1} is required for completion.");
            GetTree().Quit(0);
            return;
        }

        bool expectedUnlock = _runAchievementPositiveSmoke;
        if (!completed || unlocked != expectedUnlock)
        {
            GD.PushError($"ROOM08_ACHIEVEMENT_FAIL: completed={completed}, unlocked={unlocked}, expected={expectedUnlock}.");
            GetTree().Quit(1);
            return;
        }

        string mode = expectedUnlock ? "positive" : "negative";
        GD.Print($"ROOM08_ACHIEVEMENT_PASS: {mode} three-boost streak condition behaved correctly.");
        GetTree().Quit(0);
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM08_SOLUTION_FAIL: {message}");
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
        if (_acceleratorContactAudio is not null)
        {
            _acceleratorContactAudio.Stop();
            _acceleratorContactAudio.Stream = null;
        }
        _solutionTrace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(exitCode);
    }
}
