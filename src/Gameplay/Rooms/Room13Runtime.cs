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

public partial class Room13Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_13_solution.tres";
    private const byte InteractAction = 1;
    private const int RequiredSolutionRuns = 10;
    private const int MaximumSolutionTicksPerRun = 1200;
    private const float WindBaseStrength = 8.5f;
    private const float WindPulseAmplitude = 3.0f;
    private const int WindPulseTicks = 150;

    private readonly List<Area3D> _windCourseGates = new();
    private readonly List<Node3D> _fanRotors = new();
    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private ForceVolume3D _windVolume = null!;
    private ForceVolumeProfile _windProfile = null!;
    private MechanicalLever _launchGateLever = null!;
    private StaticBody3D _launchBarrier = null!;
    private CollisionShape3D _launchBarrierCollision = null!;
    private GpuParticles3D _windParticles = null!;
    private ParticleProcessMaterial _windParticleProcess = null!;
    private AudioStreamPlayer3D? _windAudio;
    private Transform3D _spawnTransform;
    private Vector3 _launchBarrierClosedPosition;
    private SolutionTrace? _solutionTrace;
    private bool _touchedWind;
    private bool _trackingWindFlight;
    private bool _verifiedWindFlight;
    private bool _activatedLaunchGate;
    private bool _touchedAccelerator;
    private bool _verifiedAccelerator;
    private bool _windCollisionOccurred;
    private bool _showInteractionPrompts;
    private bool _highContrastPrompts;
    private bool _runSolutionSmoke;
    private bool _runPreview;
    private bool _runShellSmoke;
    private bool _runAchievementLogicSmoke;
    private bool _runVisualSmoke;
    private bool _panoramaCapture;
    private bool _solutionSmokeFinishing;
    private int _nextWindGate;
    private int _lastObservedCollisionImpactCount;
    private int _windTicks;
    private int _gustTick;
    private int _solutionRun;
    private int _solutionTick;
    private int _solutionWarmupTicks;
    private int _previewFrames;
    private int _shellSmokeTick;
    private int _visualSmokeFrames;
    private float _flightStartX;
    private float _flightStartVelocityX;
    private float _maximumXDisplacement;
    private float _maximumVelocityXGain;
    private float _minimumWindStrength = float.PositiveInfinity;
    private float _maximumWindStrength;
    private float _acceleratorEntrySpeed;
    private float _acceleratorMaximumSpeed;
    private float _barrierOpenAmount;
    private Basis[] _initialFanBases = Array.Empty<Basis>();

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] arguments = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(arguments, argument => argument == "--room13-solution-smoke");
        _runPreview = Array.Exists(arguments, argument => argument == "--room13-preview");
        _runShellSmoke = Array.Exists(arguments, argument => argument == "--room-shell-smoke");
        _runAchievementLogicSmoke = Array.Exists(arguments, argument => argument == "--room13-achievement-logic-smoke");
        _runVisualSmoke = Array.Exists(arguments, argument => argument == "--room13-visual-smoke");
        _panoramaCapture = Array.Exists(arguments, argument => argument.StartsWith("--panorama-capture=", StringComparison.Ordinal));

        BuildRoom();
        BuildGoal();
        _initialFanBases = _fanRotors.Select(rotor => rotor.Transform.Basis).ToArray();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room13_a", new Vector3(11.5f, 15.5f, 44.0f), new Vector3(0.0f, 7.0f, -39.0f), 57.0f),
            new("room13_b", new Vector3(-11.0f, 15.0f, -35.0f), new Vector3(1.0f, 7.0f, -79.0f), 58.0f),
        });

        _player = GetNode<PlayerBall>("Player");
        _cameraRig = GetNode<PlayerCameraRig>("CameraRig");
        _spawnTransform = _player.GlobalTransform;
        _cameraRig.Follow(_player);
        _player.MovementBasis = _cameraRig.MovementBasis;
        _lastObservedCollisionImpactCount = _player.CollisionImpactCount;
        GameSettingsData settings = SettingsStore.Load();
        _showInteractionPrompts = settings.InteractionPrompts;
        _highContrastPrompts = settings.HighContrastPrompts;
        Key interactKey = InputDefaults.GetPrimaryKey(InputDefaults.Interact);
        _launchGateLever.SetKeyLabel(interactKey == Key.None ? "E" : interactKey.ToString());
        if (_runPreview)
        {
            _cameraRig.SetInputEnabled(false);
        }

        _windVolume.RigidBodyEntered += body =>
        {
            if (body != _player)
            {
                return;
            }

            _touchedWind = true;
            if (_windAudio is not null)
            {
                _windAudio.GlobalPosition = _player.GlobalPosition;
                _windAudio.Play();
            }
        };

        if (_runAchievementLogicSmoke)
        {
            CallDeferred(nameof(RunAchievementLogicSmoke));
            return;
        }

        if (_runSolutionSmoke)
        {
            _solutionWarmupTicks = 6;
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            if (_solutionTrace is null ||
                _solutionTrace.RoomId != RoomId ||
                _solutionTrace.MoveInputs.Count < 4 ||
                _solutionTrace.MoveDurationsTicks.Length != _solutionTrace.MoveInputs.Count ||
                _solutionTrace.ActionFlags.Length != _solutionTrace.MoveInputs.Count ||
                !_solutionTrace.ActionFlags.Contains(InteractAction))
            {
                FailSolutionSmoke("The Room 13 SolutionTrace is invalid.");
            }
        }
    }

    public override void _Process(double delta)
    {
        float visualStrength = _runPreview || _panoramaCapture
            ? WindBaseStrength + WindPulseAmplitude
            : _windProfile.Strength;
        float fanSpeed = Mathf.Lerp(1.2f, 7.4f, Mathf.Clamp(visualStrength / (WindBaseStrength + WindPulseAmplitude), 0.0f, 1.0f));
        foreach (Node3D rotor in _fanRotors)
        {
            rotor.RotateObjectLocal(Vector3.Forward, fanSpeed * (float)delta);
        }

        _barrierOpenAmount = Mathf.MoveToward(_barrierOpenAmount, _activatedLaunchGate ? 1.0f : 0.0f, (float)delta * 2.8f);
        _launchBarrier.Position = _launchBarrierClosedPosition + (Vector3.Up * _barrierOpenAmount * 5.0f);

        if (_runPreview && ++_previewFrames >= 30)
        {
            string path = ProjectSettings.GlobalizePath("user://room13-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"ROOM13_PREVIEW_CAPTURE: {path}");
            GetTree().Quit(0);
        }

        if (_runVisualSmoke && ++_visualSmokeFrames >= 45)
        {
            RunVisualSmoke();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateWindPulse();
        if (_runShellSmoke)
        {
            RunShellSmokeTick();
            return;
        }

        if (_runAchievementLogicSmoke)
        {
            return;
        }

        if (_runSolutionSmoke)
        {
            RunSolutionTick();
            return;
        }

        bool canInteract = _launchGateLever.CanInteract(_player);
        bool isFocused = canInteract && _cameraRig.IsLookingAt(_launchGateLever.GlobalPosition + (Vector3.Up * 1.75f));
        _launchGateLever.SetFocused(isFocused && _showInteractionPrompts, _highContrastPrompts);
        if (isFocused && Godot.Input.IsActionJustPressed(InputDefaults.Interact))
        {
            _launchGateLever.Interact(_player);
        }

        TrackWindRoute();
        if (_player.GlobalPosition.Y < -7.0f)
        {
            RestartRoom();
        }
    }

    public override void RestartRoom()
    {
        if (_runSolutionSmoke && _solutionTick > 0 && !_solutionSmokeFinishing)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} hit the maintenance floor at {_player.GlobalPosition}; {DescribeRouteState()}.");
            return;
        }

        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _player.ResetTo(_spawnTransform);
        ResetWindState();
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
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 13 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 13 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void RunSolutionTick()
    {
        if (_solutionTrace is null || _solutionSmokeFinishing)
        {
            return;
        }

        if (_solutionWarmupTicks > 0)
        {
            _player.SimulatedMoveInput = null;
            _solutionWarmupTicks--;
            return;
        }

        TrackWindRoute();
        if (IsComplete)
        {
            if (!AllRequirementsSatisfied())
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} bypassed the wind puzzle: {DescribeRouteState()}.");
                return;
            }

            if (!CompletedAdvancementIds.Contains("against-the-wind"))
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} completed the clean intended route without receiving Against the Wind.");
                return;
            }

            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print(
                    $"ROOM13_SOLUTION_PASS: SolutionTrace completed the lever, accelerator, two-gate pulsing-wind route {_solutionRun} consecutive times; " +
                    $"wind={_minimumWindStrength:F2}-{_maximumWindStrength:F2} m/s^2, displacement={_maximumXDisplacement:F2} m, " +
                    $"velocity_gain={_maximumVelocityXGain:F2} m/s, against_the_wind={CompletedAdvancementIds.Contains("against-the-wind")}.");
                FinishSolutionSmoke(0);
                return;
            }

            ClearCompletionState();
            _player.SimulatedMoveInput = null;
            _player.ResetTo(_spawnTransform);
            _solutionTick = 0;
            _solutionWarmupTicks = 6;
            ResetWindState();
            return;
        }

        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} timed out at {_player.GlobalPosition} with velocity {_player.LinearVelocity}; {DescribeRouteState()}.");
            return;
        }

        (Vector2 moveInput, byte actionFlags) = ResolveTraceStep(_solutionTick - 1);
        _player.SimulatedMoveInput = moveInput;
        if ((actionFlags & InteractAction) != 0)
        {
            _launchGateLever.Interact(_player);
        }
    }

    private void TrackWindRoute()
    {
        SurfaceKind surface = _player.GroundSurfaceKind;
        float planarSpeed = _player.LinearVelocity.Slide(Vector3.Up).Length();
        if (surface == SurfaceKind.Accelerator)
        {
            if (!_touchedAccelerator)
            {
                _touchedAccelerator = true;
                _acceleratorEntrySpeed = planarSpeed;
                _acceleratorMaximumSpeed = planarSpeed;
                TraceState("accelerator-entry");
            }
            _acceleratorMaximumSpeed = Mathf.Max(_acceleratorMaximumSpeed, planarSpeed);
            _verifiedAccelerator |= _acceleratorMaximumSpeed >= _acceleratorEntrySpeed + 4.0f;
        }

        bool insideWind = _windVolume.ContainsBody(_player);
        bool airborne = !_player.IsGrounded;
        int currentCollisions = _player.CollisionImpactCount;
        if (ShouldCountWindCollision(insideWind, airborne, _lastObservedCollisionImpactCount, currentCollisions))
        {
            _windCollisionOccurred = true;
            TraceState("wind-collision");
        }
        _lastObservedCollisionImpactCount = currentCollisions;

        if (!_trackingWindFlight && !_verifiedWindFlight && insideWind && airborne)
        {
            _trackingWindFlight = true;
            _windTicks = 0;
            _flightStartX = _player.GlobalPosition.X;
            _flightStartVelocityX = _player.LinearVelocity.X;
            _maximumXDisplacement = 0.0f;
            _maximumVelocityXGain = 0.0f;
            _minimumWindStrength = float.PositiveInfinity;
            _maximumWindStrength = 0.0f;
            TraceState("wind-flight-start");
        }

        if (!_trackingWindFlight)
        {
            return;
        }

        if (insideWind && airborne)
        {
            _windTicks++;
            _maximumXDisplacement = Mathf.Max(_maximumXDisplacement, _player.GlobalPosition.X - _flightStartX);
            _maximumVelocityXGain = Mathf.Max(_maximumVelocityXGain, _player.LinearVelocity.X - _flightStartVelocityX);
            _minimumWindStrength = Mathf.Min(_minimumWindStrength, _windProfile.Strength);
            _maximumWindStrength = Mathf.Max(_maximumWindStrength, _windProfile.Strength);
            return;
        }

        _verifiedWindFlight |=
            _windTicks >= 45 &&
            _maximumXDisplacement >= 5.0f &&
            _maximumVelocityXGain >= 5.0f &&
            _maximumWindStrength >= 10.5f;
        if (_runSolutionSmoke)
        {
            GD.Print(
                $"ROOM13_WIND_MEASURE: ticks={_windTicks}, strength={_minimumWindStrength:F2}-{_maximumWindStrength:F2}, " +
                $"displacement={_maximumXDisplacement:F2}, velocity_gain={_maximumVelocityXGain:F2}, verified={_verifiedWindFlight}.");
        }
        _trackingWindFlight = false;
    }

    public static bool ShouldCountWindCollision(bool insideWind, bool airborne, int previousCollisionCount, int currentCollisionCount) =>
        insideWind && airborne && currentCollisionCount > previousCollisionCount;

    private bool AllRequirementsSatisfied() =>
        _activatedLaunchGate &&
        _touchedAccelerator &&
        _verifiedAccelerator &&
        _touchedWind &&
        _verifiedWindFlight &&
        _nextWindGate == _windCourseGates.Count;

    private string DescribeRouteState() =>
        $"lever={_activatedLaunchGate}, accelerator={_touchedAccelerator}/{_verifiedAccelerator}({_acceleratorEntrySpeed:F2}->{_acceleratorMaximumSpeed:F2}), " +
        $"wind={_touchedWind}/{_verifiedWindFlight}({_minimumWindStrength:F2}-{_maximumWindStrength:F2}, ticks={_windTicks}, " +
        $"displacement={_maximumXDisplacement:F2}, velocity_gain={_maximumVelocityXGain:F2}), gates={_nextWindGate}/{_windCourseGates.Count}, " +
        $"wind_collision={_windCollisionOccurred}";

    private void TraceState(string eventName)
    {
        if (_runSolutionSmoke)
        {
            GD.Print($"ROOM13_TRACE: event={eventName}, tick={_solutionTick}, position={_player.GlobalPosition}, velocity={_player.LinearVelocity}, {DescribeRouteState()}.");
        }
    }

    private void UpdateWindPulse()
    {
        _gustTick++;
        float phase = (_gustTick % WindPulseTicks) / (float)WindPulseTicks;
        _windProfile.Strength = WindBaseStrength + (Mathf.Sin(phase * Mathf.Tau) * WindPulseAmplitude);
        if (IsInstanceValid(_windParticleProcess))
        {
            _windParticleProcess.InitialVelocityMin = 5.5f + (_windProfile.Strength * 0.32f);
            _windParticleProcess.InitialVelocityMax = 8.5f + (_windProfile.Strength * 0.48f);
        }
    }

    private (Vector2 MoveInput, byte ActionFlags) ResolveTraceStep(int tick)
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

        return _solutionTrace.HoldLastInput
            ? (_solutionTrace.MoveInputs[^1], _solutionTrace.ActionFlags[^1])
            : (Vector2.Zero, (byte)0);
    }

    private void ResetWindState()
    {
        _touchedWind = false;
        _trackingWindFlight = false;
        _verifiedWindFlight = false;
        _activatedLaunchGate = false;
        _touchedAccelerator = false;
        _verifiedAccelerator = false;
        _windCollisionOccurred = false;
        _nextWindGate = 0;
        _lastObservedCollisionImpactCount = _player.CollisionImpactCount;
        _windTicks = 0;
        _gustTick = 0;
        _flightStartX = 0.0f;
        _flightStartVelocityX = 0.0f;
        _maximumXDisplacement = 0.0f;
        _maximumVelocityXGain = 0.0f;
        _minimumWindStrength = float.PositiveInfinity;
        _maximumWindStrength = 0.0f;
        _acceleratorEntrySpeed = 0.0f;
        _acceleratorMaximumSpeed = 0.0f;
        _barrierOpenAmount = 0.0f;
        _launchBarrier.Position = _launchBarrierClosedPosition;
        _launchBarrierCollision.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
        _launchGateLever.ResetLever();
        foreach (Area3D gate in _windCourseGates)
        {
            gate.SetMeta("activated", false);
            gate.GetNode<MeshInstance3D>("ActiveRing").Hide();
        }
        _windAudio?.Stop();
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string copper = "res://assets/textures/copper_rivets.svg";
        Color paleSteel = new("afbdc2");
        Color frame = new("405b66");
        SurfaceProfile sticky = GD.Load<SurfaceProfile>("res://resources/surfaces/sticky.tres");
        SurfaceProfile accelerator = GD.Load<SurfaceProfile>("res://resources/surfaces/accelerator.tres");
        ShaderMaterial stickyMaterial = (ShaderMaterial)GD.Load<ShaderMaterial>("res://resources/materials/sticky_caramel.tres").Duplicate();
        ShaderMaterial acceleratorMaterial = (ShaderMaterial)GD.Load<ShaderMaterial>("res://resources/materials/accelerator_belt.tres").Duplicate();
        bool reducedMotion = SettingsStore.Load().ReducedMotion || _runPreview || _panoramaCapture;
        stickyMaterial.SetShaderParameter("motion_scale", reducedMotion ? 0.0f : 1.0f);
        acceleratorMaterial.SetShaderParameter("motion_scale", reducedMotion ? 0.0f : 1.0f);

        RoomGeometry.AddClosedRoomShell(
            this,
            "RoomShell",
            new Vector3(0.0f, 0.0f, -30.0f),
            new Vector2(30.0f, 170.0f),
            -3.0f,
            34.0f,
            metal,
            new Color("687e88"),
            new Color("394c55"),
            body =>
            {
                if (body is PlayerBall)
                {
                    RestartRoom();
                }
            });

        RoomGeometry.AddBox(this, "SafeStart", new Vector3(16.0f, 0.5f, 19.775f), new Vector3(0.0f, 10.0f, 44.8875f), Vector3.Zero, metal, paleSteel, 0.4f, 0.66f);
        AddSlopeBetween("CenteredDescent", 16.0f, 35.0f, 10.25f, 19.0f, 5.25f, copper, new Color("718790"), 0.38f, 0.6f);
        StaticBody3D stickyYard = RoomGeometry.AddBox(this, "StickyTimingYard", new Vector3(26.0f, 0.5f, 16.0f), new Vector3(0.0f, 5.0f, 11.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 0.65f, friction: sticky.Friction, surfaceProfile: sticky, materialOverride: stickyMaterial);
        StaticBody3D acceleratorLane = RoomGeometry.AddBox(this, "WindLaunchAccelerator", new Vector3(7.0f, 0.5f, 15.0f), new Vector3(-6.0f, 5.0f, -4.5f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 0.62f, friction: accelerator.Friction, surfaceProfile: accelerator, materialOverride: acceleratorMaterial);
        AddSlopeBetween("WindLaunchRamp", 7.0f, -12.0f, 5.25f, -21.0f, 7.75f, copper, new Color("718790"), 0.38f, 0.6f, x: -6.0f);
        RoomGeometry.AddBox(this, "WindLandingDeck", new Vector3(26.0f, 0.5f, 34.0f), new Vector3(0.0f, 4.0f, -75.0f), Vector3.Zero, metal, paleSteel.Darkened(0.04f), 0.4f, 0.66f);
        RoomGeometry.AddBox(this, "CenteredRecoveryRun", new Vector3(14.0f, 0.5f, 22.775f), new Vector3(0.0f, 4.0f, -103.3875f), Vector3.Zero, metal, paleSteel.Darkened(0.08f), 0.4f, 0.66f);

        AddContinuousRails(metal, copper, frame);
        BuildLaunchGate(metal, frame);

        _launchGateLever = new MechanicalLever
        {
            Name = "LaunchGateLever",
            Position = new Vector3(7.0f, 5.25f, 12.0f),
            ActivationRadius = 3.1f,
        };
        _launchGateLever.Activated += () =>
        {
            _activatedLaunchGate = true;
            _launchBarrierCollision.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
            TraceState("launch-gate-open");
        };
        AddChild(_launchGateLever);

        _windProfile = (ForceVolumeProfile)GD.Load<ForceVolumeProfile>("res://resources/force_volumes/crosswind.tres").Duplicate();
        _windProfile.Strength = WindBaseStrength;
        _windVolume = new ForceVolume3D
        {
            Name = "PulsingCrosswindVolume",
            Position = new Vector3(0.0f, 11.0f, -45.0f),
            CollisionMask = 1,
            Profile = _windProfile,
        };
        _windVolume.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(28.0f, 25.0f, 50.0f) },
        });
        AddChild(_windVolume);

        AddWindCourseGate("WindGuideGateA", 0, new Vector3(-3.8f, 10.7f, -39.0f), 4.6f);
        AddWindCourseGate("WindGuideGateB", 1, new Vector3(2.0f, 8.8f, -56.0f), 4.8f);
        AddWindParticles();
        AddFanBanks();

        if (!_runSolutionSmoke && !_runAchievementLogicSmoke)
        {
            _windAudio = new AudioStreamPlayer3D
            {
                Name = "WindEnterSfx",
                Stream = GD.Load<AudioStream>("res://assets/audio/sfx/force_wind_enter.wav"),
                Bus = "SFX",
                MaxDistance = 42.0f,
                UnitSize = 8.0f,
            };
            AddChild(_windAudio);
        }

        stickyYard.AddChild(CreatePractical("StickyYardLight", new Color("df913e"), 11.0f));
        acceleratorLane.AddChild(CreatePractical("AcceleratorLight", new Color("70d5d1"), 11.0f));
    }

    private StaticBody3D AddSlopeBetween(
        string name,
        float width,
        float backZ,
        float backTopY,
        float frontZ,
        float frontTopY,
        string texture,
        Color tint,
        float roughness,
        float metallic,
        float x = 0.0f)
    {
        float run = backZ - frontZ;
        float rise = backTopY - frontTopY;
        float angle = -Mathf.Atan2(rise, run);
        float length = Mathf.Sqrt((run * run) + (rise * rise));
        const float thickness = 0.5f;
        Vector3 up = new Basis(Vector3.Right, angle) * Vector3.Up;
        Vector3 topCenter = new(x, (backTopY + frontTopY) * 0.5f, (backZ + frontZ) * 0.5f);
        return RoomGeometry.AddBox(this, name, new Vector3(width, thickness, length), topCenter - (up * thickness * 0.5f), new Vector3(angle, 0.0f, 0.0f), texture, tint, roughness, metallic);
    }

    private void AddContinuousRails(string metal, string copper, Color frame)
    {
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(this, $"StartRail{side}", new Vector3(0.38f, 1.45f, 19.775f), new Vector3(side * 8.2f, 10.975f, 44.8875f), Vector3.Zero, metal, frame, 0.42f, 0.62f);
            AddSlopeRail($"DescentRail{side}", side * 8.2f, 35.0f, 10.25f, 19.0f, 5.25f, metal, frame);
            RoomGeometry.AddBox(this, $"StickyOuterRail{side}", new Vector3(0.38f, 1.45f, 16.0f), new Vector3(side * 13.2f, 5.975f, 11.0f), Vector3.Zero, copper, frame, 0.42f, 0.58f);
            RoomGeometry.AddBox(this, $"AcceleratorRail{side}", new Vector3(0.38f, 1.45f, 15.0f), new Vector3(-6.0f + (side * 3.7f), 5.975f, -4.5f), Vector3.Zero, metal, frame, 0.42f, 0.62f);
            AddSlopeRail($"LaunchRail{side}", -6.0f + (side * 3.7f), -12.0f, 5.25f, -21.0f, 7.75f, copper, frame);
            RoomGeometry.AddBox(this, $"LandingRail{side}", new Vector3(0.38f, 1.45f, 34.0f), new Vector3(side * 13.2f, 4.975f, -75.0f), Vector3.Zero, metal, frame, 0.42f, 0.62f);
            RoomGeometry.AddBox(this, $"RecoveryRail{side}", new Vector3(0.38f, 1.45f, 22.775f), new Vector3(side * 7.2f, 4.975f, -103.3875f), Vector3.Zero, metal, frame, 0.42f, 0.62f);
        }

        // Only the left launch bay remains open at the front of the timing
        // yard.  Reaching it after the right-side lever is the ground-choice
        // part of the puzzle; no unrelated wall divides the landing deck.
        RoomGeometry.AddBox(this, "YardFrontBarrierLeft", new Vector3(3.3f, 1.45f, 0.38f), new Vector3(-11.35f, 5.975f, 2.8f), Vector3.Zero, copper, frame, 0.42f, 0.58f);
        RoomGeometry.AddBox(this, "YardFrontBarrierRight", new Vector3(15.3f, 1.45f, 0.38f), new Vector3(5.35f, 5.975f, 2.8f), Vector3.Zero, copper, frame, 0.42f, 0.58f);
        RoomGeometry.AddBox(this, "RecoveryEntryBarrierLeft", new Vector3(6.0f, 1.45f, 0.38f), new Vector3(-10.0f, 4.975f, -92.2f), Vector3.Zero, metal, frame, 0.42f, 0.62f);
        RoomGeometry.AddBox(this, "RecoveryEntryBarrierRight", new Vector3(6.0f, 1.45f, 0.38f), new Vector3(10.0f, 4.975f, -92.2f), Vector3.Zero, metal, frame, 0.42f, 0.62f);
    }

    private void AddSlopeRail(string name, float x, float backZ, float backTopY, float frontZ, float frontTopY, string texture, Color tint)
    {
        float run = backZ - frontZ;
        float rise = backTopY - frontTopY;
        float angle = -Mathf.Atan2(rise, run);
        float length = Mathf.Sqrt((run * run) + (rise * rise));
        Vector3 up = new Basis(Vector3.Right, angle) * Vector3.Up;
        Vector3 topCenter = new(x, ((backTopY + frontTopY) * 0.5f) + 0.725f, (backZ + frontZ) * 0.5f);
        RoomGeometry.AddBox(this, name, new Vector3(0.38f, 1.45f, length), topCenter - (up * 0.725f), new Vector3(angle, 0.0f, 0.0f), texture, tint, 0.42f, 0.62f);
    }

    private void BuildLaunchGate(string metal, Color frame)
    {
        _launchBarrier = RoomGeometry.AddBox(
            this,
            "LaunchGateBarrier",
            new Vector3(7.0f, 4.5f, 0.46f),
            new Vector3(-6.0f, 7.5f, 2.72f),
            Vector3.Zero,
            metal,
            frame,
            0.42f,
            0.62f);
        _launchBarrierClosedPosition = _launchBarrier.Position;
        _launchBarrierCollision = _launchBarrier.GetChildren().OfType<CollisionShape3D>().First();
    }

    private void AddWindCourseGate(string name, int index, Vector3 position, float radius)
    {
        Area3D gate = new()
        {
            Name = name,
            Position = position,
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true,
            Monitorable = false,
        };
        gate.SetMeta("activated", false);
        gate.AddChild(new CollisionShape3D
        {
            Rotation = new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f),
            Shape = new CylinderShape3D { Radius = radius, Height = 1.6f },
        });
        StandardMaterial3D frameMaterial = RoomGeometry.CreateMaterial("res://assets/textures/copper_rivets.svg", new Color("6c8993"), 0.36f, 0.62f);
        StandardMaterial3D activeMaterial = RoomGeometry.CreateMaterial(
            "res://assets/textures/sugar_glaze.svg",
            new Color("91d8d5"),
            0.08f,
            0.48f,
            emissionEnabled: true,
            emission: new Color("245b5d"));
        gate.AddChild(new MeshInstance3D
        {
            Name = "WindRibbonRing",
            Rotation = new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f),
            Mesh = new TorusMesh { InnerRadius = radius, OuterRadius = radius + 0.16f, Rings = 40, RingSegments = 10 },
            MaterialOverride = frameMaterial,
        });
        MeshInstance3D active = new()
        {
            Name = "ActiveRing",
            Rotation = new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f),
            Mesh = new TorusMesh { InnerRadius = radius - 0.1f, OuterRadius = radius + 0.01f, Rings = 40, RingSegments = 8 },
            MaterialOverride = activeMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        gate.AddChild(active);
        gate.BodyEntered += body =>
        {
            if (body != _player || index != _nextWindGate || (gate.HasMeta("activated") && gate.GetMeta("activated").AsBool()))
            {
                return;
            }
            gate.SetMeta("activated", true);
            active.Show();
            _nextWindGate++;
            TraceState($"wind-gate-{_nextWindGate}");
        };
        AddChild(gate);
        _windCourseGates.Add(gate);
    }

    private void AddWindParticles()
    {
        StandardMaterial3D particleMaterial = new()
        {
            AlbedoColor = new Color("c8e6ec"),
            Roughness = 0.7f,
            EmissionEnabled = true,
            Emission = new Color("4f8f9d"),
            EmissionEnergyMultiplier = 0.65f,
        };
        _windParticleProcess = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(13.0f, 10.0f, 24.0f),
            Direction = Vector3.Right,
            Spread = 6.0f,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 8.0f,
            InitialVelocityMax = 13.0f,
            ScaleMin = 0.38f,
            ScaleMax = 1.0f,
        };
        _windParticles = new GpuParticles3D
        {
            Name = "PersistentCrosswindStreaks",
            Position = new Vector3(0.0f, 11.0f, -45.0f),
            Amount = 120,
            Lifetime = 2.5f,
            Randomness = 0.72f,
            LocalCoords = true,
            ProcessMode = ProcessModeEnum.Always,
            VisibilityAabb = new Aabb(new Vector3(-24.0f, -18.0f, -35.0f), new Vector3(48.0f, 36.0f, 70.0f)),
            ExtraCullMargin = 48.0f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            ProcessMaterial = _windParticleProcess,
            DrawPass1 = new BoxMesh { Size = new Vector3(1.1f, 0.035f, 0.035f), Material = particleMaterial },
        };
        AddChild(_windParticles);
    }

    private void AddFanBanks()
    {
        StandardMaterial3D hubMaterial = RoomGeometry.CreateMaterial("res://assets/textures/copper_rivets.svg", new Color("607985"), 0.4f, 0.58f);
        StandardMaterial3D bladeMaterial = RoomGeometry.CreateMaterial("res://assets/textures/brushed_metal.png", new Color("b7c6cc"), 0.42f, 0.62f);
        foreach (float z in new[] { -30.0f, -45.0f, -60.0f })
        {
            Node3D housing = new()
            {
                Name = $"WindFanHousing{Mathf.Abs(z):F0}",
                Position = new Vector3(-13.7f, 10.0f, z),
                Rotation = new Vector3(0.0f, Mathf.Pi / 2.0f, 0.0f),
            };
            AddChild(housing);
            RoomGeometry.AddCylinder(housing, "Hub", Vector3.Zero, new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f), 0.48f, 0.8f, hubMaterial);
            Node3D rotor = new() { Name = "Rotor" };
            housing.AddChild(rotor);
            for (int index = 0; index < 5; index++)
            {
                float angle = index * Mathf.Tau / 5.0f;
                RoomGeometry.AddVisualBox(
                    rotor,
                    $"Blade{index}",
                    new Vector3(0.3f, 3.1f, 0.16f),
                    new Vector3(Mathf.Sin(angle) * 1.4f, Mathf.Cos(angle) * 1.4f, 0.0f),
                    new Vector3(0.0f, 0.0f, angle),
                    string.Empty,
                    Colors.White,
                    0.0f,
                    1.0f,
                    bladeMaterial);
            }
            _fanRotors.Add(rotor);
        }
    }

    private static OmniLight3D CreatePractical(string name, Color color, float range) => new()
    {
        Name = name,
        Position = new Vector3(0.0f, 1.5f, 0.0f),
        LightColor = color,
        LightEnergy = 1.0f,
        OmniRange = range,
        ShadowEnabled = false,
    };

    private void BuildGoal()
    {
        Vector3 goalPosition = new(0.0f, 5.9f, -113.92f);
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
            Shape = new CylinderShape3D { Radius = 1.7f, Height = 2.7f },
        });
        goal.BodyEntered += body =>
        {
            if (body is PlayerBall && AllRequirementsSatisfied())
            {
                AwardAgainstTheWindIfEligible();
                CompleteRoom();
            }
        };
        AddChild(goal);

        Area3D routeCompletion = new()
        {
            Name = "RouteCompletionTrigger",
            Position = new Vector3(0.0f, 5.9f, -104.0f),
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true,
        };
        routeCompletion.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(11.0f, 4.2f, 2.0f) },
        });
        routeCompletion.BodyEntered += body =>
        {
            if (body is PlayerBall && AllRequirementsSatisfied())
            {
                AwardAgainstTheWindIfEligible();
                CompleteRoom();
            }
        };
        AddChild(routeCompletion);

        RoomGeometry.AddGoalExitDoor(this, goalPosition);
    }

    private void AwardAgainstTheWindIfEligible()
    {
        if (!_windCollisionOccurred)
        {
            MarkAdvancementCondition("against-the-wind");
        }
    }

    private void RunAchievementLogicSmoke()
    {
        bool outsideBounceIgnored = !ShouldCountWindCollision(false, true, 4, 5);
        bool groundedWindImpactIgnored = !ShouldCountWindCollision(true, false, 4, 5);
        bool unchangedCountIgnored = !ShouldCountWindCollision(true, true, 4, 4);
        bool airborneWindImpactCounted = ShouldCountWindCollision(true, true, 4, 5);
        if (!outsideBounceIgnored || !groundedWindImpactIgnored || !unchangedCountIgnored || !airborneWindImpactCounted)
        {
            GD.PushError("ROOM13_ACHIEVEMENT_LOGIC_FAIL: collision filtering did not isolate airborne impacts inside the wind volume.");
            GetTree().Quit(1);
            return;
        }

        _windCollisionOccurred = false;
        AwardAgainstTheWindIfEligible();
        bool positive = CompletedAdvancementIds.Contains("against-the-wind");
        ClearCompletionState();
        _windCollisionOccurred = true;
        AwardAgainstTheWindIfEligible();
        bool negative = !CompletedAdvancementIds.Contains("against-the-wind");
        if (!positive || !negative)
        {
            GD.PushError("ROOM13_ACHIEVEMENT_LOGIC_FAIL: positive or negative award path failed.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ROOM13_ACHIEVEMENT_LOGIC_PASS: only airborne collision deltas inside the wind volume block Against the Wind; outside rebounds are ignored.");
        GetTree().Quit(0);
    }

    private void RunVisualSmoke()
    {
        _runVisualSmoke = false;
        bool completeFanBank = _fanRotors.Count == 3 &&
            _fanRotors.All(rotor => rotor.GetChildCount() == 5);
        bool everyFanRotated = _initialFanBases.Length == _fanRotors.Count &&
            _fanRotors.Select((rotor, index) => rotor.Transform.Basis.X.DistanceTo(_initialFanBases[index].X))
                .All(distance => distance > 0.05f);
        bool particlesArePersistent = _windParticles.Emitting &&
            _windParticles.Amount >= 100 &&
            _windParticles.ProcessMode == ProcessModeEnum.Always &&
            _windParticles.VisibilityAabb.Size.X >= 28.0f &&
            _windParticles.VisibilityAabb.Size.Y >= 25.0f &&
            _windParticles.VisibilityAabb.Size.Z >= 50.0f;
        bool particlesShowWindDirection = _windParticleProcess.Direction.Normalized().Dot(Vector3.Right) > 0.99f &&
            _windParticleProcess.Gravity.IsZeroApprox() &&
            _windParticleProcess.InitialVelocityMin > 5.0f &&
            _windParticleProcess.InitialVelocityMax > _windParticleProcess.InitialVelocityMin &&
            _windParticles.DrawPass1 is BoxMesh streak &&
            streak.Size.X >= streak.Size.Y * 20.0f &&
            streak.Size.X >= streak.Size.Z * 20.0f;
        float expectedMinimumVelocity = 5.5f + (_windProfile.Strength * 0.32f);
        float expectedMaximumVelocity = 8.5f + (_windProfile.Strength * 0.48f);
        bool visualsFollowPulse = Mathf.IsEqualApprox(_windParticleProcess.InitialVelocityMin, expectedMinimumVelocity) &&
            Mathf.IsEqualApprox(_windParticleProcess.InitialVelocityMax, expectedMaximumVelocity);

        if (!completeFanBank || !everyFanRotated || !particlesArePersistent || !particlesShowWindDirection || !visualsFollowPulse)
        {
            GD.PushError(
                $"ROOM13_VISUAL_FAIL: fans={completeFanBank}/{everyFanRotated}, particles={particlesArePersistent}/{particlesShowWindDirection}, pulse={visualsFollowPulse}.");
            GetTree().Quit(1);
            return;
        }

        GD.Print(
            $"ROOM13_VISUAL_PASS: all three five-blade fans rotated; {_windParticles.Amount} persistent horizontal streaks follow the pulsing wind at " +
            $"{_windProfile.Strength:F2} m/s^2 ({_windParticleProcess.InitialVelocityMin:F2}-{_windParticleProcess.InitialVelocityMax:F2} m/s).");
        _windParticles.Emitting = false;
        GetTree().Quit(0);
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM13_SOLUTION_FAIL: {message}");
        FinishSolutionSmoke(1);
    }

    private async void FinishSolutionSmoke(int exitCode)
    {
        if (_solutionSmokeFinishing)
        {
            return;
        }

        _solutionSmokeFinishing = true;
        if (_player is not null)
        {
            _player.SimulatedMoveInput = null;
        }
        if (_windAudio is not null)
        {
            _windAudio.Stop();
            _windAudio.Stream = null;
        }
        _windParticles.Emitting = false;
        _launchGateLever.ResetLever();
        _solutionTrace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GetTree().Quit(exitCode);
    }
}
