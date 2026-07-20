using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Physics;
using Velocitex.Core.Rooms;
using Velocitex.Core.Settings;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Player;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room10Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_10_solution.tres";
    private const int RequiredSolutionRuns = 10;
    private const int MaximumSolutionTicksPerRun = 1500;

    private readonly List<RouteCheckpoint3D> _sequenceButtons = new();
    private readonly List<FlightGate3D> _flightGates = new();
    private readonly HashSet<ulong> _distinctBounceSurfaceIds = new();
    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private AudioStreamPlayer3D? _stickyAudio;
    private AudioStreamPlayer3D? _acceleratorAudio;
    private AudioStreamPlayer3D? _bounceAudio;
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private SurfaceKind _lastSurfaceKind = SurfaceKind.Standard;
    private bool _touchedGlass;
    private bool _touchedSticky;
    private bool _touchedAccelerator;
    private bool _verifiedSticky;
    private bool _verifiedAccelerator;
    private bool _verifiedBounce;
    private bool _verifiedDoubleBounce;
    private bool _runSolutionSmoke;
    private bool _runPreview;
    private bool _runShellSmoke;
    private bool _reducedMotion;
    private bool _solutionSmokeFinishing;
    private int _nextSequenceButton;
    private int _nextFlightGate;
    private int _lastBounceCount;
    private int _solutionRun;
    private int _solutionTick;
    private int _solutionWarmupTicks;
    private int _previewFrames;
    private int _shellSmokeTick;
    private float _stickyEntrySpeed;
    private float _stickyMinimumSpeed;
    private float _acceleratorEntrySpeed;
    private float _acceleratorMaximumSpeed;
    private float _bounceImpactSpeed;
    private float _bounceLaunchSpeed;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] userArguments = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(userArguments, argument => argument == "--room10-solution-smoke");
        _runPreview = Array.Exists(userArguments, argument => argument == "--room10-preview");
        _runShellSmoke = Array.Exists(userArguments, argument => argument == "--room-shell-smoke");
        bool panoramaCapture = Array.Exists(userArguments, argument => argument.StartsWith("--panorama-capture=", StringComparison.Ordinal));
        _reducedMotion = SettingsStore.Load().ReducedMotion || _runPreview || panoramaCapture;

        BuildRoom();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room10_a", new Vector3(10.2f, 17.5f, 57.0f), new Vector3(0.0f, 8.0f, -35.0f), 57.0f),
            new("room10_b", new Vector3(-10.0f, 17.5f, -82.0f), new Vector3(0.0f, 9.5f, -151.0f), 58.0f),
        });

        _player = GetNode<PlayerBall>("Player");
        _cameraRig = GetNode<PlayerCameraRig>("CameraRig");
        _spawnTransform = _player.GlobalTransform;
        _cameraRig.Follow(_player);
        _player.MovementBasis = _cameraRig.MovementBasis;
        if (_runPreview)
        {
            _cameraRig.SetInputEnabled(false);
        }

        if (_runSolutionSmoke)
        {
            _solutionWarmupTicks = 6;
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            if (_solutionTrace is null ||
                _solutionTrace.RoomId != RoomId ||
                _solutionTrace.MoveInputs.Count < 4 ||
                _solutionTrace.MoveDurationsTicks.Length != _solutionTrace.MoveInputs.Count)
            {
                string details = _solutionTrace is null
                    ? "trace=null"
                    : $"trace_room='{_solutionTrace.RoomId}', inputs={_solutionTrace.MoveInputs.Count}, durations={_solutionTrace.MoveDurationsTicks.Length}";
                FailSolutionSmoke($"The Room 10 SolutionTrace is invalid ({details}).");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_runPreview && ++_previewFrames >= 30)
        {
            string capturePath = ProjectSettings.GlobalizePath("user://room10-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(capturePath);
            GD.Print($"ROOM10_PREVIEW_CAPTURE: {capturePath}");
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

        TrackSurfaceExam();
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
                $"Run {_solutionRun + 1} touched the hazard floor at tick {_solutionTick}, position {_player.GlobalPosition}, " +
                $"velocity {_player.LinearVelocity}; sequence={_nextSequenceButton}/{_sequenceButtons.Count}, " +
                $"glass={_touchedGlass}, sticky={_touchedSticky}/{_verifiedSticky}, accelerator={_touchedAccelerator}/{_verifiedAccelerator}, " +
                $"bounces={_distinctBounceSurfaceIds.Count}, double={_verifiedDoubleBounce}, gate={_nextFlightGate}/{_flightGates.Count}.");
            return;
        }

        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _player.ResetTo(_spawnTransform);
        ResetExamState();
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
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 10 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 10 hazard floor restarted the player.");
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

        TrackSurfaceExam();
        if (IsComplete)
        {
            if (!AllRequirementsSatisfied())
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} reached completion without the full chapter exam state: {DescribeExamState()}.");
                return;
            }

            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print(
                    $"ROOM10_SOLUTION_PASS: SolutionTrace completed the ordered glass, sticky, accelerator, two-body rebound and required ring exam " +
                    $"{_solutionRun} consecutive times; double_bounce={CompletedAdvancementIds.Contains("double-bounce")}.");
                FinishSolutionSmoke(0);
                return;
            }

            ClearCompletionState();
            _player.SimulatedMoveInput = null;
            _player.ResetTo(_spawnTransform);
            _solutionTick = 0;
            _solutionWarmupTicks = 6;
            ResetExamState();
            return;
        }

        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke(
                $"Run {_solutionRun + 1} timed out at {_player.GlobalPosition} with velocity {_player.LinearVelocity}; {DescribeExamState()}.");
            return;
        }

        _player.SimulatedMoveInput = ResolveTraceInput(_solutionTick - 1);
    }

    private void TrackSurfaceExam()
    {
        SurfaceKind surface = _player.GroundSurfaceKind;
        float planarSpeed = _player.LinearVelocity.Slide(Vector3.Up).Length();
        if (surface == SurfaceKind.Frictionless)
        {
            _touchedGlass = true;
        }
        else if (surface == SurfaceKind.Sticky)
        {
            if (_lastSurfaceKind != SurfaceKind.Sticky)
            {
                _touchedSticky = true;
                _stickyEntrySpeed = planarSpeed;
                _stickyMinimumSpeed = planarSpeed;
                PlaySurfaceAudio(_stickyAudio);
                TraceState("sticky-entry");
            }

            _stickyMinimumSpeed = Mathf.Min(_stickyMinimumSpeed, planarSpeed);
            _verifiedSticky |= _stickyEntrySpeed >= 4.5f && _stickyMinimumSpeed <= _stickyEntrySpeed * 0.92f;
        }
        else if (surface == SurfaceKind.Accelerator)
        {
            if (_lastSurfaceKind != SurfaceKind.Accelerator)
            {
                _touchedAccelerator = true;
                _acceleratorEntrySpeed = planarSpeed;
                _acceleratorMaximumSpeed = planarSpeed;
                PlaySurfaceAudio(_acceleratorAudio);
                TraceState("accelerator-entry");
            }

            _acceleratorMaximumSpeed = Mathf.Max(_acceleratorMaximumSpeed, planarSpeed);
            _verifiedAccelerator |= _acceleratorMaximumSpeed >= _acceleratorEntrySpeed + 4.0f;
        }

        if (_player.SuperElasticBounceCount > _lastBounceCount)
        {
            _lastBounceCount = _player.SuperElasticBounceCount;
            _bounceImpactSpeed = _player.LastSuperElasticImpactSpeed;
            _bounceLaunchSpeed = _player.LastSuperElasticLaunchSpeed;
            _verifiedBounce |= _bounceImpactSpeed >= 6.0f && _bounceLaunchSpeed >= _bounceImpactSpeed * 1.6f;
            if (_player.LastElasticBounceSurfaceInstanceId != 0UL)
            {
                _distinctBounceSurfaceIds.Add(_player.LastElasticBounceSurfaceInstanceId);
            }

            if (_distinctBounceSurfaceIds.Count >= 2 && _player.ConsecutiveElasticBounceCount >= 2)
            {
                _verifiedDoubleBounce = true;
                MarkAdvancementCondition("double-bounce");
            }

            PlaySurfaceAudio(_bounceAudio);
            TraceState($"bounce-{_distinctBounceSurfaceIds.Count}");
        }

        _lastSurfaceKind = surface;
    }

    private bool AllRequirementsSatisfied() =>
        _nextSequenceButton == _sequenceButtons.Count &&
        _touchedGlass &&
        _touchedSticky &&
        _verifiedSticky &&
        _touchedAccelerator &&
        _verifiedAccelerator &&
        _verifiedBounce &&
        _verifiedDoubleBounce &&
        _distinctBounceSurfaceIds.Count >= 2 &&
        _nextFlightGate == _flightGates.Count;

    private string DescribeExamState() =>
        $"sequence={_nextSequenceButton}/{_sequenceButtons.Count}, glass={_touchedGlass}, " +
        $"sticky={_touchedSticky}/{_verifiedSticky}({_stickyEntrySpeed:F2}->{_stickyMinimumSpeed:F2}), " +
        $"accelerator={_touchedAccelerator}/{_verifiedAccelerator}({_acceleratorEntrySpeed:F2}->{_acceleratorMaximumSpeed:F2}), " +
        $"bounces={_distinctBounceSurfaceIds.Count}/{_player.ConsecutiveElasticBounceCount}, " +
        $"bounce={_bounceImpactSpeed:F2}->{_bounceLaunchSpeed:F2}/{_verifiedBounce}, double={_verifiedDoubleBounce}, " +
        $"gate={_nextFlightGate}/{_flightGates.Count}";

    private void TraceState(string eventName)
    {
        if (_runSolutionSmoke)
        {
            GD.Print(
                $"ROOM10_TRACE: event={eventName}, tick={_solutionTick}, position={_player.GlobalPosition}, velocity={_player.LinearVelocity}, {DescribeExamState()}.");
        }
    }

    private void PlaySurfaceAudio(AudioStreamPlayer3D? audio)
    {
        if (audio is null)
        {
            return;
        }

        audio.GlobalPosition = _player.GlobalPosition;
        audio.Play();
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

    private void ResetExamState()
    {
        _lastSurfaceKind = SurfaceKind.Standard;
        _touchedGlass = false;
        _touchedSticky = false;
        _touchedAccelerator = false;
        _verifiedSticky = false;
        _verifiedAccelerator = false;
        _verifiedBounce = false;
        _verifiedDoubleBounce = false;
        _nextSequenceButton = 0;
        _nextFlightGate = 0;
        _lastBounceCount = 0;
        _stickyEntrySpeed = 0.0f;
        _stickyMinimumSpeed = 0.0f;
        _acceleratorEntrySpeed = 0.0f;
        _acceleratorMaximumSpeed = 0.0f;
        _bounceImpactSpeed = 0.0f;
        _bounceLaunchSpeed = 0.0f;
        _distinctBounceSurfaceIds.Clear();
        foreach (RouteCheckpoint3D button in _sequenceButtons)
        {
            button.ResetCheckpoint();
        }
        foreach (FlightGate3D gate in _flightGates)
        {
            gate.ResetGate();
        }
        _stickyAudio?.Stop();
        _acceleratorAudio?.Stop();
        _bounceAudio?.Stop();
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string glassTexture = "res://assets/textures/frictionless_glass.svg";
        const string copper = "res://assets/textures/copper_rivets.svg";
        Color paleSteel = new("b2babc");
        Color darkFrame = new("354348");
        Color membraneFrame = new("73597f");
        SurfaceProfile frictionless = GD.Load<SurfaceProfile>("res://resources/surfaces/frictionless.tres");
        SurfaceProfile sticky = GD.Load<SurfaceProfile>("res://resources/surfaces/sticky.tres");
        SurfaceProfile accelerator = GD.Load<SurfaceProfile>("res://resources/surfaces/accelerator.tres");
        SurfaceProfile superElastic = GD.Load<SurfaceProfile>("res://resources/surfaces/super_elastic.tres");
        ShaderMaterial stickyMaterial = (ShaderMaterial)GD.Load<ShaderMaterial>("res://resources/materials/sticky_caramel.tres").Duplicate();
        ShaderMaterial acceleratorMaterial = (ShaderMaterial)GD.Load<ShaderMaterial>("res://resources/materials/accelerator_belt.tres").Duplicate();
        ShaderMaterial firstBounceMaterial = (ShaderMaterial)GD.Load<ShaderMaterial>("res://resources/materials/super_elastic_membrane.tres").Duplicate();
        ShaderMaterial secondBounceMaterial = (ShaderMaterial)firstBounceMaterial.Duplicate();
        stickyMaterial.SetShaderParameter("motion_scale", _reducedMotion ? 0.0f : 1.0f);
        acceleratorMaterial.SetShaderParameter("motion_scale", _reducedMotion ? 0.0f : 1.0f);
        firstBounceMaterial.SetShaderParameter("motion_scale", _reducedMotion ? 0.0f : 1.0f);
        secondBounceMaterial.SetShaderParameter("motion_scale", _reducedMotion ? 0.0f : 1.0f);

        RoomGeometry.AddClosedRoomShell(
            this,
            "RoomShell",
            new Vector3(0.0f, 0.0f, -75.0f),
            new Vector2(28.0f, 284.0f),
            -2.8f,
            42.0f,
            metal,
            new Color("738488"),
            new Color("544952"),
            body =>
            {
                if (body is PlayerBall)
                {
                    RestartRoom();
                }
            });

        RoomGeometry.AddBox(this, "SafeStart", new Vector3(14.0f, 0.5f, 30.775f), new Vector3(0.0f, 12.0f, 51.3875f), Vector3.Zero, metal, paleSteel, 0.4f, 0.66f);
        AddSlopeBetween("MomentumRamp", 14.0f, 36.0f, 12.25f, 13.0f, 5.85f, copper, new Color("886b57"), 0.38f, 0.62f);
        StaticBody3D glassRun = RoomGeometry.AddBox(this, "FrictionlessRun", new Vector3(14.0f, 0.5f, 13.0f), new Vector3(0.0f, 5.6f, 6.5f), Vector3.Zero, glassTexture, new Color("a9d4df"), 0.06f, 0.2f, friction: frictionless.Friction, surfaceProfile: frictionless);
        ConfigureGlass(glassRun);
        StaticBody3D stickyYard = RoomGeometry.AddBox(this, "StickyDecisionYard", new Vector3(26.0f, 0.5f, 14.0f), new Vector3(0.0f, 5.6f, -7.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 0.65f, friction: sticky.Friction, surfaceProfile: sticky, materialOverride: stickyMaterial);
        StaticBody3D acceleratorRamp = AddProfiledSlopeBetween(
            "AcceleratorClimb",
            8.0f,
            -14.0f,
            5.85f,
            -30.0f,
            5.85f,
            accelerator,
            acceleratorMaterial,
            0.58f);
        RoomGeometry.AddBox(this, "LaunchAimDeck", new Vector3(8.0f, 0.5f, 8.0f), new Vector3(0.0f, 5.6f, -34.0f), Vector3.Zero, metal, paleSteel.Darkened(0.06f), 0.4f, 0.66f);

        StaticBody3D firstMembrane = RoomGeometry.AddBox(
            this,
            "SuperElasticMembraneA",
            new Vector3(14.0f, 0.5f, 24.0f),
            new Vector3(0.0f, 2.5f, -62.0f),
            Vector3.Zero,
            string.Empty,
            Colors.White,
            0.0f,
            0.7f,
            friction: superElastic.Friction,
            surfaceProfile: superElastic,
            materialOverride: firstBounceMaterial);

        RoomGeometry.AddBox(this, "SecondMembraneTower", new Vector3(16.0f, 11.0f, 22.0f), new Vector3(0.0f, 4.2f, -112.0f), Vector3.Zero, copper, new Color("514959"), 0.42f, 0.62f);
        StaticBody3D secondMembrane = RoomGeometry.AddBox(
            this,
            "SuperElasticMembraneB",
            new Vector3(14.0f, 0.5f, 20.0f),
            new Vector3(0.0f, 9.75f, -112.0f),
            Vector3.Zero,
            string.Empty,
            Colors.White,
            0.0f,
            0.7f,
            friction: superElastic.Friction,
            surfaceProfile: superElastic,
            materialOverride: secondBounceMaterial);

        RoomGeometry.AddBox(this, "FinalRunout", new Vector3(14.0f, 0.5f, 62.775f), new Vector3(0.0f, 9.75f, -185.3875f), Vector3.Zero, metal, paleSteel.Darkened(0.05f), 0.4f, 0.66f);

        AddContinuousRails(metal, copper, darkFrame, membraneFrame);
        AddSequenceButton("StickySequenceOne", 0, new Vector3(5.0f, 6.44f, -1.4f));
        AddSequenceButton("StickySequenceTwo", 1, new Vector3(-5.8f, 6.44f, -12.0f));
        AddFlightGate(new Vector3(0.0f, 11.0f, -89.0f), 4.0f);

        if (!_runSolutionSmoke)
        {
            _stickyAudio = CreateSurfaceAudio("StickyContactSfx", "res://assets/audio/sfx/surface_sticky_contact.wav");
            _acceleratorAudio = CreateSurfaceAudio("AcceleratorContactSfx", "res://assets/audio/sfx/surface_accelerator_contact.wav");
            _bounceAudio = CreateSurfaceAudio("SuperElasticBounceSfx", "res://assets/audio/sfx/surface_super_elastic_bounce.wav");
        }

        stickyYard.AddChild(CreatePractical("StickyPractical", new Color("df913e"), 10.0f));
        acceleratorRamp.AddChild(CreatePractical("AcceleratorPractical", new Color("58c9cf"), 10.0f));
        firstMembrane.AddChild(CreatePractical("MembranePracticalA", new Color("aa78c4"), 12.0f));
        secondMembrane.AddChild(CreatePractical("MembranePracticalB", new Color("c092d4"), 12.0f));
    }

    private static void ConfigureGlass(StaticBody3D body)
    {
        MeshInstance3D visual = (MeshInstance3D)body.GetChild(0);
        StandardMaterial3D material = (StandardMaterial3D)visual.MaterialOverride;
        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        material.AlbedoColor = new Color(0.58f, 0.78f, 0.84f, 0.76f);
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
        float metallic)
    {
        float run = backZ - frontZ;
        float rise = backTopY - frontTopY;
        float angle = -Mathf.Atan2(rise, run);
        float length = Mathf.Sqrt((run * run) + (rise * rise));
        const float thickness = 0.5f;
        Vector3 up = new Basis(Vector3.Right, angle) * Vector3.Up;
        Vector3 topCenter = new(0.0f, (backTopY + frontTopY) * 0.5f, (backZ + frontZ) * 0.5f);
        return RoomGeometry.AddBox(this, name, new Vector3(width, thickness, length), topCenter - (up * thickness * 0.5f), new Vector3(angle, 0.0f, 0.0f), texture, tint, roughness, metallic);
    }

    private StaticBody3D AddProfiledSlopeBetween(
        string name,
        float width,
        float backZ,
        float backTopY,
        float frontZ,
        float frontTopY,
        SurfaceProfile profile,
        Material material,
        float metallic)
    {
        float run = backZ - frontZ;
        float rise = backTopY - frontTopY;
        float angle = -Mathf.Atan2(rise, run);
        float length = Mathf.Sqrt((run * run) + (rise * rise));
        const float thickness = 0.5f;
        Vector3 up = new Basis(Vector3.Right, angle) * Vector3.Up;
        Vector3 topCenter = new(0.0f, (backTopY + frontTopY) * 0.5f, (backZ + frontZ) * 0.5f);
        return RoomGeometry.AddBox(
            this,
            name,
            new Vector3(width, thickness, length),
            topCenter - (up * thickness * 0.5f),
            new Vector3(angle, 0.0f, 0.0f),
            string.Empty,
            Colors.White,
            0.0f,
            metallic,
            friction: profile.Friction,
            surfaceProfile: profile,
            materialOverride: material);
    }

    private void AddContinuousRails(string metal, string copper, Color darkFrame, Color membraneFrame)
    {
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(this, $"StartRail{side}", new Vector3(0.38f, 1.45f, 30.775f), new Vector3(side * 7.2f, 12.975f, 51.3875f), Vector3.Zero, metal, darkFrame, 0.42f, 0.62f);
            AddSlopeRail($"MomentumRail{side}", side * 7.2f, 36.0f, 12.25f, 13.0f, 5.85f, metal, darkFrame);
            RoomGeometry.AddBox(this, $"GlassRail{side}", new Vector3(0.38f, 1.45f, 13.0f), new Vector3(side * 7.2f, 6.575f, 6.5f), Vector3.Zero, metal, darkFrame, 0.42f, 0.62f);
            RoomGeometry.AddBox(this, $"StickyOuterRail{side}", new Vector3(0.38f, 1.45f, 14.0f), new Vector3(side * 13.2f, 6.575f, -7.0f), Vector3.Zero, copper, new Color("795a48"), 0.42f, 0.58f);
            AddSlopeRail($"AcceleratorRail{side}", side * 4.2f, -14.0f, 5.85f, -30.0f, 5.85f, metal, darkFrame);
            RoomGeometry.AddBox(this, $"AimRail{side}", new Vector3(0.38f, 1.45f, 8.0f), new Vector3(side * 4.2f, 6.575f, -34.0f), Vector3.Zero, metal, darkFrame, 0.42f, 0.62f);
            RoomGeometry.AddBox(this, $"MembraneARim{side}", new Vector3(0.46f, 0.72f, 24.4f), new Vector3(side * 7.2f, 2.5f, -62.0f), Vector3.Zero, copper, membraneFrame, 0.42f, 0.56f);
            RoomGeometry.AddBox(this, $"MembraneBRim{side}", new Vector3(0.46f, 0.72f, 20.4f), new Vector3(side * 7.2f, 9.75f, -112.0f), Vector3.Zero, copper, membraneFrame, 0.42f, 0.56f);
            RoomGeometry.AddBox(this, $"FinalRail{side}", new Vector3(0.38f, 1.45f, 62.775f), new Vector3(side * 7.2f, 10.725f, -185.3875f), Vector3.Zero, metal, darkFrame, 0.42f, 0.62f);
        }

        // The sticky yard has one deliberate central exit.  The solid front
        // blocks make the shared accelerator lane physically mandatory.
        RoomGeometry.AddBox(this, "StickyFrontBarrierLeft", new Vector3(9.0f, 1.45f, 0.38f), new Vector3(-8.5f, 6.575f, -14.2f), Vector3.Zero, copper, new Color("795a48"), 0.42f, 0.58f);
        RoomGeometry.AddBox(this, "StickyFrontBarrierRight", new Vector3(9.0f, 1.45f, 0.38f), new Vector3(8.5f, 6.575f, -14.2f), Vector3.Zero, copper, new Color("795a48"), 0.42f, 0.58f);
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

    private void AddSequenceButton(string name, int index, Vector3 position)
    {
        RouteCheckpoint3D button = new()
        {
            Name = name,
            Position = position,
            CheckpointIndex = index,
            TriggerSize = new Vector3(3.0f, 1.4f, 3.0f),
            FrameTint = RoomGeometry.SequenceButtonFrameTint,
            FlatFloorMarker = true,
        };
        button.Entered += (entered, player) =>
        {
            if (player != _player)
            {
                return;
            }
            if (entered.CheckpointIndex != _nextSequenceButton) { entered.FlashDenied(); return; }

            entered.Activate();
            _nextSequenceButton++;
            TraceState($"sequence-{_nextSequenceButton}");
        };
        AddChild(button);
        _sequenceButtons.Add(button);

        MeshInstance3D inset = button.GetNode<MeshInstance3D>("InsetPlate");
        RoomGeometry.AddSequencePips(inset, index + 1);
    }

    private void AddFlightGate(Vector3 position, float radius)
    {
        FlightGate3D gate = new()
        {
            Name = "RequiredReboundGate",
            Position = position,
            Radius = radius,
            FrameTint = new Color("715a7e"),
            EnableAudio = !_runSolutionSmoke,
            MinimumExitSpeed = 20.0f,
            SpeedGain = 3.0f,
            SpeedMultiplier = 1.12f,
            AxialBoostOnly = true,
            MaximumDownwardExitSpeed = float.PositiveInfinity,
        };
        gate.Passed += player =>
        {
            if (player == _player && _nextFlightGate == 0)
            {
                _nextFlightGate = 1;
                TraceState("required-ring");
            }
        };
        AddChild(gate);
        _flightGates.Add(gate);
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

    private AudioStreamPlayer3D CreateSurfaceAudio(string name, string path)
    {
        AudioStreamPlayer3D player = new()
        {
            Name = name,
            Stream = GD.Load<AudioStream>(path),
            Bus = "SFX",
            MaxDistance = 34.0f,
            UnitSize = 7.0f,
        };
        AddChild(player);
        return player;
    }

    private void BuildGoal()
    {
        Vector3 goalPosition = new(0.0f, 11.4f, -215.92f);
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
            Shape = new CylinderShape3D { Radius = 1.65f, Height = 2.7f },
        });
        goal.BodyEntered += body =>
        {
            if (body is PlayerBall && AllRequirementsSatisfied())
            {
                CompleteRoom();
            }
        };
        AddChild(goal);

        // Arm the shared door with enough runout for a high-momentum valid
        // arrival.  Invalid routes leave the physical leaves locked.
        Area3D routeCompletion = new()
        {
            Name = "RouteCompletionTrigger",
            Position = new Vector3(0.0f, 11.4f, -205.5f),
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
                CompleteRoom();
            }
        };
        AddChild(routeCompletion);

        RoomGeometry.AddGoalExitDoor(this, goalPosition);
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM10_SOLUTION_FAIL: {message}");
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
        foreach (AudioStreamPlayer3D? audio in new[] { _stickyAudio, _acceleratorAudio, _bounceAudio })
        {
            if (audio is not null)
            {
                audio.Stop();
                audio.Stream = null;
            }
        }
        foreach (FlightGate3D gate in _flightGates)
        {
            gate.ResetGate();
            gate.QueueFree();
        }
        _flightGates.Clear();
        foreach (RouteCheckpoint3D button in _sequenceButtons)
        {
            button.ResetCheckpoint();
            button.QueueFree();
        }
        _sequenceButtons.Clear();
        _solutionTrace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GetTree().Quit(exitCode);
    }
}
