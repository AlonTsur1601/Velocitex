using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Physics;
using Velocitex.Core.Rooms;
using Velocitex.Core.Settings;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Visual;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room07Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_07_solution.tres";
    private const string SurfacePath = "res://resources/surfaces/sticky.tres";
    private const string MaterialPath = "res://resources/materials/sticky_caramel.tres";
    private const string ContactSfxPath = "res://assets/audio/sfx/surface_sticky_contact.wav";
    private const int RequiredSolutionRuns = 10;
    private const int MaximumSolutionTicksPerRun = 2400;
    private const int RequiredButtons = 2;
    private const int RequiredPerfectBrakeTicks = 36;
    private const float TargetChargeSeconds = 0.8f;
    private const float StickyEntrySpeedRequirement = 5.0f;
    private const float RegularStopRadius = 1.65f;
    private const float RegularStopSpeed = 0.95f;
    private const float PerfectStopRadius = 0.58f;
    private const float PerfectStopSpeed = 0.28f;
    private const float StopTargetZ = -16.2f;

    private readonly List<RouteCheckpoint3D> _sequenceButtons = new();
    private readonly Dictionary<RouteCheckpoint3D, Material> _buttonIdleMaterials = new();
    private readonly Dictionary<RouteCheckpoint3D, Tween> _wrongOrderTweens = new();
    private readonly List<MeshInstance3D> _targetChargeSegments = new();
    private readonly List<Node3D> _targetLatches = new();

    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private Area3D _goal = null!;
    private AudioStreamPlayer3D? _stickyContactAudio;
    private MeshInstance3D _stopTargetRing = null!;
    private MeshInstance3D _stopTargetCore = null!;
    private Material _targetIdleMaterial = null!;
    private Material _targetActiveMaterial = null!;
    private Material _wrongOrderMaterial = null!;
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private bool _touchedStickyThisRun;
    private bool _verifiedStickySlowdownThisRun;
    private bool _wasOnSticky;
    private bool _brakeLatchedThisRun;
    private bool _perfectStopThisRun;
    private bool _overshotTargetThisRun;
    private bool _runSolutionSmoke;
    private bool _runPreview;
    private bool _runShellSmoke;
    private bool _runAchievementPositiveSmoke;
    private bool _runAchievementNegativeSmoke;
    private bool _suppressDeviceAudio;
    private bool _solutionSmokeFinishing;
    private int _nextSequenceButton;
    private int _brakeHoldTicks;
    private int _perfectBrakeHoldTicks;
    private int _solutionRun;
    private int _solutionTick;
    private int _previewFrames;
    private int _shellSmokeTick;
    private float _stickyEntrySpeed;
    private float _minimumStickySpeed;
    private float _targetLatchAmount;
    private float _targetChargeAmount;
    private StandardMaterial3D _chargeEmptyMaterial = null!;
    private StandardMaterial3D _chargeActiveMaterial = null!;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] userArguments = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(userArguments, argument => argument == "--room07-solution-smoke");
        _runPreview = Array.Exists(userArguments, argument => argument == "--room07-preview");
        _runShellSmoke = Array.Exists(userArguments, argument => argument == "--room-shell-smoke");
        _runAchievementPositiveSmoke = Array.Exists(userArguments, argument => argument == "--room07-achievement-positive-solution-smoke");
        _runAchievementNegativeSmoke = Array.Exists(userArguments, argument => argument == "--room07-achievement-negative-solution-smoke");
        bool panoramaCapture = Array.Exists(userArguments, argument => argument.StartsWith("--panorama-capture=", StringComparison.Ordinal));
        _suppressDeviceAudio = panoramaCapture || _runPreview || userArguments.Any(argument =>
            argument.Contains("smoke", StringComparison.Ordinal) ||
            argument.Contains("bypass", StringComparison.Ordinal) ||
            argument.StartsWith("--surface-room=", StringComparison.Ordinal) ||
            argument.StartsWith("--containment-room=", StringComparison.Ordinal));
        bool reducedMotion = SettingsStore.Load().ReducedMotion || _runPreview || panoramaCapture;

        BuildRoom(reducedMotion);
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room07_a", new Vector3(7.3f, 8.8f, 18.8f), new Vector3(0.0f, 4.1f, -11.5f), 54.0f),
            new("room07_b", new Vector3(-7.0f, 7.4f, -5.0f), new Vector3(0.0f, 4.0f, -16.2f), 55.0f),
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
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            if (_solutionTrace is null ||
                _solutionTrace.RoomId != RoomId ||
                _solutionTrace.MoveInputs.Count < 5 ||
                _solutionTrace.MoveDurationsTicks.Length != _solutionTrace.MoveInputs.Count ||
                !_solutionTrace.MoveInputs.Any(input => input.LengthSquared() < 0.0001f) ||
                !_solutionTrace.MoveInputs.Any(input => input.X < -0.2f) ||
                !_solutionTrace.MoveInputs.Any(input => input.X > 0.2f))
            {
                string details = _solutionTrace is null
                    ? "trace=null"
                    : $"trace_room='{_solutionTrace.RoomId}', inputs={_solutionTrace.MoveInputs.Count}, durations={_solutionTrace.MoveDurationsTicks.Length}";
                FailSolutionSmoke($"The Room 07 SolutionTrace is invalid ({details}).");
            }
        }

        if (_runAchievementPositiveSmoke || _runAchievementNegativeSmoke)
        {
            Callable.From(RunAchievementSmoke).CallDeferred();
        }
    }

    public override void _Process(double delta)
    {
        if (_runPreview && ++_previewFrames >= 30)
        {
            string capturePath = ProjectSettings.GlobalizePath("user://room07-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(capturePath);
            GD.Print($"ROOM07_PREVIEW_CAPTURE: {capturePath}");
            GetTree().Quit(0);
            return;
        }

        float target = _brakeLatchedThisRun ? 1.0f : 0.0f;
        _targetLatchAmount = Mathf.MoveToward(_targetLatchAmount, target, (float)delta * 4.8f);
        for (int index = 0; index < _targetLatches.Count; index++)
        {
            float angle = index * Mathf.Pi * 0.5f;
            float radius = 1.92f + (_targetLatchAmount * 0.42f);
            _targetLatches[index].Position = new Vector3(
                Mathf.Cos(angle) * radius,
                0.16f,
                Mathf.Sin(angle) * radius);
            _targetLatches[index].Rotation = new Vector3(0.0f, -angle, 0.0f);
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

        if (_runAchievementPositiveSmoke || _runAchievementNegativeSmoke)
        {
            return;
        }

        TrackPuzzleState();
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
                $"Run {_solutionRun + 1} touched the hazard floor at {_player.GlobalPosition}; " +
                $"buttons={_nextSequenceButton}/{RequiredButtons}, touched={_touchedStickyThisRun}, " +
                $"slowed={_verifiedStickySlowdownThisRun}, stop={_brakeLatchedThisRun}.");
            return;
        }

        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _player.ResetTo(_spawnTransform);
        ResetPuzzleState();
    }

    public override void _ExitTree()
    {
        foreach (Tween tween in _wrongOrderTweens.Values)
        {
            tween.Kill();
        }
        _wrongOrderTweens.Clear();
        _stickyContactAudio?.Stop();
        if (_stickyContactAudio is not null)
        {
            _stickyContactAudio.Stream = null;
        }
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
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 07 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 07 hazard floor restarted the player.");
        if (_stickyContactAudio is not null)
        {
            _stickyContactAudio.Stop();
            _stickyContactAudio.Stream = null;
        }
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
            if (!_touchedStickyThisRun ||
                !_verifiedStickySlowdownThisRun ||
                _nextSequenceButton != RequiredButtons ||
                !_brakeLatchedThisRun)
            {
                FailSolutionSmoke(
                    $"Run {_solutionRun + 1} bypassed the brake puzzle; buttons={_nextSequenceButton}/{RequiredButtons}, " +
                    $"touched={_touchedStickyThisRun}, slowed={_verifiedStickySlowdownThisRun}, " +
                    $"stop={_brakeLatchedThisRun}, entry={_stickyEntrySpeed:F2}, minimum={_minimumStickySpeed:F2}.");
                return;
            }

            if (!_perfectStopThisRun || !CompletedAdvancementIds.Contains("perfect-stop"))
            {
                FailSolutionSmoke(
                    $"Run {_solutionRun + 1} completed without the trace proving the optional precision stop; " +
                    $"perfect={_perfectStopThisRun}, overshot={_overshotTargetThisRun}, perfect_ticks={_perfectBrakeHoldTicks}.");
                return;
            }

            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print(
                    $"ROOM07_SOLUTION_PASS: SolutionTrace used both ordered buttons, measured sticky momentum loss, " +
                    $"and held the inner precision target for {_solutionRun} consecutive completions.");
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
                $"buttons={_nextSequenceButton}/{RequiredButtons}, touched={_touchedStickyThisRun}, " +
                $"slowed={_verifiedStickySlowdownThisRun}, stop={_brakeLatchedThisRun}, perfect={_perfectStopThisRun}.");
            return;
        }

        Vector2 moveInput = ResolveTraceInput(_solutionTick - 1);
        _player.SimulatedMoveInput = moveInput;
        TrackPuzzleState(moveInput);
    }

    private void TrackPuzzleState(Vector2? moveInput = null)
    {
        bool onSticky = _player.GroundSurfaceKind == SurfaceKind.Sticky;
        float planarSpeed = _player.LinearVelocity.Slide(Vector3.Up).Length();
        if (onSticky && !_wasOnSticky)
        {
            _touchedStickyThisRun = true;
            _stickyEntrySpeed = planarSpeed;
            _minimumStickySpeed = planarSpeed;
            if (_stickyContactAudio is not null)
            {
                _stickyContactAudio.GlobalPosition = _player.GlobalPosition;
                _stickyContactAudio.PitchScale = 0.96f + ((_solutionRun % 3) * 0.03f);
                _stickyContactAudio.Play();
            }
        }

        if (onSticky)
        {
            _minimumStickySpeed = Mathf.Min(_minimumStickySpeed, planarSpeed);
            if (_stickyEntrySpeed >= StickyEntrySpeedRequirement &&
                _minimumStickySpeed <= _stickyEntrySpeed * 0.48f)
            {
                _verifiedStickySlowdownThisRun = true;
            }
        }

        _wasOnSticky = onSticky;
        if (_touchedStickyThisRun && !_brakeLatchedThisRun && _player.GlobalPosition.Z < StopTargetZ - 0.72f)
        {
            _overshotTargetThisRun = true;
        }
        if (_nextSequenceButton != RequiredButtons || !_verifiedStickySlowdownThisRun)
        {
            return;
        }

        Vector2 activeInput = moveInput ?? _player.CurrentMoveInput;
        bool neutral = activeInput.LengthSquared() < 0.0001f;
        Vector2 targetOffset = new(_player.GlobalPosition.X, _player.GlobalPosition.Z - StopTargetZ);
        float targetDistance = targetOffset.Length();
        bool regularStop = onSticky && neutral && targetDistance <= RegularStopRadius && planarSpeed <= RegularStopSpeed;
        _brakeHoldTicks = regularStop ? _brakeHoldTicks + 1 : 0;
        float chargeStep = (float)GetPhysicsProcessDeltaTime() / TargetChargeSeconds;
        _targetChargeAmount = regularStop
            ? Mathf.MoveToward(_targetChargeAmount, 1.0f, chargeStep)
            : Mathf.MoveToward(_targetChargeAmount, 0.0f, chargeStep * 2.0f);
        UpdateTargetChargeVisual();

        bool perfectStop = onSticky && neutral && targetDistance <= PerfectStopRadius && planarSpeed <= PerfectStopSpeed;
        _perfectBrakeHoldTicks = perfectStop ? _perfectBrakeHoldTicks + 1 : 0;
        if (_perfectBrakeHoldTicks >= RequiredPerfectBrakeTicks && !_overshotTargetThisRun)
        {
            _perfectStopThisRun = true;
        }

        if (_targetChargeAmount >= 0.999f && !_brakeLatchedThisRun)
        {
            _brakeLatchedThisRun = true;
            _stopTargetRing.MaterialOverride = _targetActiveMaterial;
            _stopTargetCore.MaterialOverride = _targetActiveMaterial;
            if (_runSolutionSmoke)
            {
                GD.Print(
                    $"ROOM07_STOP_TRACE: tick={_solutionTick}, position={_player.GlobalPosition}, speed={planarSpeed:F2}, " +
                    $"perfect_ticks={_perfectBrakeHoldTicks}, overshot={_overshotTargetThisRun}.");
            }
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

    private void ResetPuzzleState()
    {
        _touchedStickyThisRun = false;
        _verifiedStickySlowdownThisRun = false;
        _wasOnSticky = false;
        _brakeLatchedThisRun = false;
        _perfectStopThisRun = false;
        _overshotTargetThisRun = false;
        _nextSequenceButton = 0;
        _brakeHoldTicks = 0;
        _perfectBrakeHoldTicks = 0;
        _stickyEntrySpeed = 0.0f;
        _minimumStickySpeed = float.MaxValue;
        _targetLatchAmount = 0.0f;
        _targetChargeAmount = 0.0f;
        _stickyContactAudio?.Stop();
        if (IsInstanceValid(_stopTargetRing))
        {
            _stopTargetRing.MaterialOverride = _targetIdleMaterial;
        }
        if (IsInstanceValid(_stopTargetCore))
        {
            _stopTargetCore.MaterialOverride = _targetIdleMaterial;
        }
        UpdateTargetChargeVisual();
        foreach (RouteCheckpoint3D button in _sequenceButtons)
        {
            button.ResetCheckpoint();
            if (_buttonIdleMaterials.TryGetValue(button, out Material? idleMaterial) &&
                button.GetNodeOrNull<MeshInstance3D>("InsetPlate") is MeshInstance3D inset)
            {
                SetWrongOrderVisual(inset, idleMaterial, showSequencePips: true);
            }
        }
        foreach (Tween tween in _wrongOrderTweens.Values)
        {
            tween.Kill();
        }
        _wrongOrderTweens.Clear();
    }

    private void BuildRoom(bool reducedMotion)
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string copper = "res://assets/textures/copper_rivets.svg";
        Color steel = new("99a5a3");
        Color copperTint = new("936047");
        SurfaceProfile stickyProfile = GD.Load<SurfaceProfile>(SurfacePath);
        ShaderMaterial stickyMaterial = (ShaderMaterial)GD.Load<ShaderMaterial>(MaterialPath).Duplicate();
        stickyMaterial.SetShaderParameter("motion_scale", reducedMotion ? 0.0f : 1.0f);

        _wrongOrderMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color("d62f2f"),
            Metallic = 0.0f,
            Roughness = 0.54f,
            EmissionEnabled = true,
            Emission = new Color("721010"),
            EmissionEnergyMultiplier = 1.15f,
        };
        _targetIdleMaterial = RoomGeometry.CreateMaterial(copper, new Color("d0a365"), 0.38f, 0.56f);
        _targetActiveMaterial = RoomGeometry.CreateMaterial(
            "res://assets/textures/sugar_glaze.svg",
            new Color("84d1aa"),
            0.08f,
            0.48f,
            emissionEnabled: true,
            emission: new Color("1e614b"));
        _chargeEmptyMaterial = RoomGeometry.CreateMaterial(
            metal,
            new Color("283432"),
            0.32f,
            0.72f);
        _chargeActiveMaterial = RoomGeometry.CreateMaterial(
            "res://assets/textures/sugar_glaze.svg",
            new Color("8ed7b5"),
            0.06f,
            0.44f,
            emissionEnabled: true,
            emission: new Color("2b795c"));

        RoomGeometry.AddClosedRoomShell(
            this,
            "RoomShell",
            new Vector3(0.0f, 0.0f, -5.5f),
            new Vector2(18.0f, 54.5f),
            -2.4f,
            13.0f,
            metal,
            new Color("817971"),
            new Color("875b46"),
            body =>
            {
                if (body is PlayerBall)
                {
                    RestartRoom();
                }
            });

        const float routeWidth = 17.55f;
        const float startBackZ = 21.525f;
        const float startFrontZ = 14.0f;
        const float slopeEndZ = 4.0f;
        const float stickyEndZ = -21.0f;
        const float exitWallZ = -32.525f;

        RoomGeometry.AddBox(
            this,
            "SafeStart",
            new Vector3(routeWidth, 0.5f, startBackZ - startFrontZ),
            new Vector3(0.0f, 4.75f, (startBackZ + startFrontZ) * 0.5f),
            Vector3.Zero,
            metal,
            steel,
            0.4f,
            0.66f);

        AddSlopeBetween(
            "MomentumApproach",
            routeWidth,
            0.5f,
            startFrontZ,
            5.0f,
            slopeEndZ,
            4.0f,
            copper,
            copperTint);

        StaticBody3D sticky = RoomGeometry.AddBox(
            this,
            "StickyBrakeField",
            new Vector3(routeWidth, 0.5f, slopeEndZ - stickyEndZ),
            new Vector3(0.0f, 3.75f, (slopeEndZ + stickyEndZ) * 0.5f),
            Vector3.Zero,
            string.Empty,
            Colors.White,
            0.0f,
            0.54f,
            friction: stickyProfile.Friction,
            surfaceProfile: stickyProfile,
            materialOverride: stickyMaterial);

        RoomGeometry.AddBox(
            this,
            "ExitDeck",
            new Vector3(routeWidth, 0.5f, stickyEndZ - exitWallZ),
            new Vector3(0.0f, 3.75f, (stickyEndZ + exitWallZ) * 0.5f),
            Vector3.Zero,
            metal,
            steel,
            0.4f,
            0.66f);

        AddSequenceButton("BrakeButtonOne", 0, new Vector3(-4.0f, 5.51f, -1.5f));
        AddSequenceButton("BrakeButtonTwo", 1, new Vector3(4.0f, 5.51f, -8.5f));
        BuildStopTarget(new Vector3(0.0f, 4.01f, StopTargetZ));

        StandardMaterial3D routeMaterial = RoomGeometry.CreateMaterial(
            copper,
            new Color("ddbd7c"),
            0.34f,
            0.56f,
            emissionEnabled: true,
            emission: new Color("493515"));
        AddRouteRibbon(new Vector3(0.0f, 4.02f, 2.8f), new Vector3(-4.0f, 4.02f, -1.5f), routeMaterial);
        AddRouteRibbon(new Vector3(-4.0f, 4.02f, -1.5f), new Vector3(4.0f, 4.02f, -8.5f), routeMaterial);
        AddRouteRibbon(new Vector3(4.0f, 4.02f, -8.5f), new Vector3(0.0f, 4.02f, StopTargetZ), routeMaterial);

        SurfaceDetail.AddOverlay(
            this,
            "ApproachScuffs",
            new Vector3(-2.6f, 4.66f, 10.0f),
            new Vector3(Mathf.DegToRad(-95.7f), 0.0f, Mathf.DegToRad(8.0f)),
            new Vector2(3.2f, 1.8f),
            "res://assets/textures/overlays/edge_scuffs.svg",
            new Color("dfbd9f"),
            0.3f);
        SurfaceDetail.AddOverlay(
            this,
            "ExitScuffs",
            new Vector3(2.8f, 4.015f, -26.4f),
            new Vector3(-Mathf.Pi * 0.5f, 0.0f, Mathf.DegToRad(-12.0f)),
            new Vector2(3.4f, 1.8f),
            "res://assets/textures/overlays/scratches.svg",
            new Color("d4c7b8"),
            0.32f);

        if (!_suppressDeviceAudio)
        {
            _stickyContactAudio = new AudioStreamPlayer3D
            {
                Name = "StickyContactSfx",
                Stream = GD.Load<AudioStream>(ContactSfxPath),
                Bus = "SFX",
                MaxDistance = 20.0f,
                UnitSize = 5.0f,
            };
            sticky.AddChild(_stickyContactAudio);
        }
    }

    private void AddSlopeBetween(
        string name,
        float width,
        float thickness,
        float highZ,
        float highY,
        float lowZ,
        float lowY,
        string texture,
        Color tint)
    {
        float run = highZ - lowZ;
        float drop = highY - lowY;
        float angle = -Mathf.Atan2(drop, run);
        float length = Mathf.Sqrt((run * run) + (drop * drop));
        Vector3 normal = new(0.0f, Mathf.Cos(angle), Mathf.Sin(angle));
        Vector3 topCenter = new(0.0f, (highY + lowY) * 0.5f, (highZ + lowZ) * 0.5f);
        Vector3 bodyCenter = topCenter - (normal * (thickness * 0.5f));
        RoomGeometry.AddBox(
            this,
            name,
            new Vector3(width, thickness, length),
            bodyCenter,
            new Vector3(angle, 0.0f, 0.0f),
            texture,
            tint,
            0.38f,
            0.6f);
    }

    private void AddSequenceButton(string name, int index, Vector3 position)
    {
        RouteCheckpoint3D button = new()
        {
            Name = name,
            Position = position,
            CheckpointIndex = index,
            TriggerSize = new Vector3(3.7f, 3.0f, 3.7f),
            FrameTint = RoomGeometry.SequenceButtonFrameTint,
            FlatFloorMarker = true,
        };
        button.Entered += (entered, player) =>
        {
            if (player != _player)
            {
                return;
            }

            if (entered.CheckpointIndex == _nextSequenceButton)
            {
                entered.Activate();
                _nextSequenceButton++;
                if (_runSolutionSmoke)
                {
                    GD.Print($"ROOM07_BUTTON_TRACE: activated={_nextSequenceButton}/{RequiredButtons}, tick={_solutionTick}, position={player.GlobalPosition}, speed={player.LinearVelocity.Length():F2}.");
                }
            }
            else
            {
                FlashWrongOrder(entered);
            }
        };
        AddChild(button);
        MeshInstance3D insetPlate = button.GetNode<MeshInstance3D>("InsetPlate");
        if (insetPlate.MaterialOverride is Material idleMaterial)
        {
            _buttonIdleMaterials[button] = idleMaterial;
        }
        RoomGeometry.AddSequencePips(insetPlate, index + 1);
        _sequenceButtons.Add(button);
    }

    private void FlashWrongOrder(RouteCheckpoint3D button)
    {
        button.FlashDenied();
    }

    private static void SetWrongOrderVisual(MeshInstance3D insetPlate, Material material, bool showSequencePips)
    {
        insetPlate.MaterialOverride = material;
        foreach (MeshInstance3D pip in insetPlate.GetChildren().OfType<MeshInstance3D>())
        {
            if (pip.Name.ToString().StartsWith("SequencePip", StringComparison.Ordinal))
            {
                pip.Visible = showSequencePips;
            }
        }
    }

    private void BuildStopTarget(Vector3 position)
    {
        Node3D target = new() { Name = "PrecisionStopTarget", Position = position };
        AddChild(target);
        _stopTargetRing = new MeshInstance3D
        {
            Name = "StopTargetRing",
            Mesh = new TorusMesh
            {
                InnerRadius = 1.56f,
                OuterRadius = 1.74f,
                Rings = 40,
                RingSegments = 10,
            },
            MaterialOverride = _targetIdleMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        target.AddChild(_stopTargetRing);
        _stopTargetCore = new MeshInstance3D
        {
            Name = "PerfectStopCore",
            Position = new Vector3(0.0f, 0.025f, 0.0f),
            Mesh = new CylinderMesh
            {
                TopRadius = PerfectStopRadius,
                BottomRadius = PerfectStopRadius,
                Height = 0.035f,
                RadialSegments = 32,
            },
            MaterialOverride = _targetIdleMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        target.AddChild(_stopTargetCore);

        const int chargeSegmentCount = 12;
        for (int index = 0; index < chargeSegmentCount; index++)
        {
            float angle = index * Mathf.Tau / chargeSegmentCount;
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
                _chargeEmptyMaterial);
            segment.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _targetChargeSegments.Add(segment);
        }

        StandardMaterial3D latchMaterial = RoomGeometry.CreateMaterial(
            "res://assets/textures/copper_rivets.svg",
            new Color("8f634f"),
            0.44f,
            0.56f);
        for (int index = 0; index < 4; index++)
        {
            Node3D latch = new() { Name = $"StopLatch{index}" };
            RoomGeometry.AddVisualBox(
                latch,
                "Jaw",
                new Vector3(0.68f, 0.13f, 0.34f),
                Vector3.Zero,
                Vector3.Zero,
                string.Empty,
                Colors.White,
                0.0f,
                1.0f,
                latchMaterial);
            target.AddChild(latch);
            _targetLatches.Add(latch);
        }
    }

    private void UpdateTargetChargeVisual()
    {
        int filledSegments = Mathf.CeilToInt(Mathf.Clamp(_targetChargeAmount, 0.0f, 1.0f) * _targetChargeSegments.Count);
        for (int index = 0; index < _targetChargeSegments.Count; index++)
        {
            _targetChargeSegments[index].MaterialOverride = index < filledSegments
                ? _chargeActiveMaterial
                : _chargeEmptyMaterial;
        }
    }

    private void AddRouteRibbon(Vector3 start, Vector3 end, StandardMaterial3D material)
    {
        Vector3 delta = end - start;
        float length = new Vector2(delta.X, delta.Z).Length();
        float yaw = Mathf.Atan2(delta.X, delta.Z);
        RoomGeometry.AddVisualBox(
            this,
            $"RouteRibbon{GetChildCount()}",
            new Vector3(0.18f, 0.025f, length),
            (start + end) * 0.5f,
            new Vector3(0.0f, yaw, 0.0f),
            string.Empty,
            Colors.White,
            0.0f,
            1.0f,
            material);
    }

    private void BuildGoal()
    {
        Vector3 goalPosition = new(0.0f, 4.65f, -31.67f);
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
            Shape = new CylinderShape3D { Radius = 2.0f, Height = 2.7f },
        });
        _goal.BodyEntered += body =>
        {
            if (body is PlayerBall &&
                _touchedStickyThisRun &&
                _verifiedStickySlowdownThisRun &&
                _nextSequenceButton == RequiredButtons &&
                _brakeLatchedThisRun)
            {
                if (_perfectStopThisRun)
                {
                    MarkAdvancementCondition("perfect-stop");
                }
                CompleteRoom();
            }
        };
        AddChild(_goal);
        RoomGeometry.AddGoalExitDoor(this, goalPosition);
    }

    private void RunAchievementSmoke()
    {
        _touchedStickyThisRun = true;
        _verifiedStickySlowdownThisRun = true;
        _nextSequenceButton = RequiredButtons;
        _brakeLatchedThisRun = true;
        _perfectStopThisRun = _runAchievementPositiveSmoke;
        foreach (RouteCheckpoint3D button in _sequenceButtons)
        {
            button.Activate();
        }
        _goal.EmitSignal(Area3D.SignalName.BodyEntered, _player);

        bool completed = IsComplete || IsExitTraversalPending;
        bool unlocked = CompletedAdvancementIds.Contains("perfect-stop");
        bool expectedUnlock = _runAchievementPositiveSmoke;
        if (!completed || unlocked != expectedUnlock)
        {
            GD.PushError(
                $"ROOM07_ACHIEVEMENT_FAIL: completed={completed}, unlocked={unlocked}, expected={expectedUnlock}.");
            GetTree().Quit(1);
            return;
        }

        string mode = expectedUnlock ? "positive" : "negative";
        GD.Print($"ROOM07_ACHIEVEMENT_PASS: {mode} precision-stop condition behaved correctly.");
        GetTree().Quit(0);
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM07_SOLUTION_FAIL: {message}");
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
        _stickyContactAudio?.Stop();
        if (_stickyContactAudio is not null)
        {
            _stickyContactAudio.Stream = null;
        }
        _solutionTrace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(exitCode);
    }
}
