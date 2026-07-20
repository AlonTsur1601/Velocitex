using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Physics;
using Velocitex.Core.Rooms;
using Velocitex.Core.Save;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Physics;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Rooms;
using Velocitex.Gameplay.Visual;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.TestRoom;

public partial class MovementTestRoom : RoomRuntime
{
    private const string Room01TracePath = "res://resources/solutions/room_01_solution.tres";
    private const string StickySurfacePath = "res://resources/surfaces/sticky.tres";
    private const string StickyMaterialPath = "res://resources/materials/sticky_caramel.tres";
    private const string StickyContactSfxPath = "res://assets/audio/sfx/surface_sticky_contact.wav";
    private const string AcceleratorSurfacePath = "res://resources/surfaces/accelerator.tres";
    private const string AcceleratorMaterialPath = "res://resources/materials/accelerator_belt.tres";
    private const string AcceleratorContactSfxPath = "res://assets/audio/sfx/surface_accelerator_contact.wav";
    private const string OneWayTeethTexturePath = "res://assets/textures/one_way_teeth.svg";
    private const string OneWaySurfacePath = "res://resources/surfaces/one_way_grip.tres";
    private const string FrictionlessSurfacePath = "res://resources/surfaces/frictionless.tres";
    private const string FrictionlessTexturePath = "res://assets/textures/frictionless_glass.svg";
    private const string SuperElasticSurfacePath = "res://resources/surfaces/super_elastic.tres";
    private const string SuperElasticMaterialPath = "res://resources/materials/super_elastic_membrane.tres";
    private const string SuperElasticBounceSfxPath = "res://assets/audio/sfx/surface_super_elastic_bounce.wav";
    private const int RequiredSolutionRuns = 10;
    private const int MaximumSolutionTicksPerRun = 1400;
    private const float Room01IntermediatePlatformExtension = 20.0f;
    private const float Room01FirstGapExtension = 12.0f;
    private const float Room01DownstreamOffset = Room01IntermediatePlatformExtension + Room01FirstGapExtension;
    private const float Room01SecondGapExtension = 20.0f;
    private const float Room01TotalExtension = Room01DownstreamOffset + Room01SecondGapExtension;

    private readonly List<FlightGate3D> _room01FlightGates = new();
    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private bool _runMovementSmoke;
    private bool _runRoom01SolutionSmoke;
    private bool _runRoom01GateBypassSmoke;
    private bool _runRoom01Preview;
    private bool _runBallSpinPreview;
    private bool _runShellSmoke;
    private int _previewFrames;
    private int _shellSmokeTick;
    private int _smokeTick;
    private int _solutionRun;
    private int _solutionTick;
    private int _solutionWarmupTicks = 6;
    private int _gateBypassTick;
    private int _gateBypassPhase;
    private int _gateBypassObservedGateMask;
    private int _gateBypassStartResetCount;
    private int _nextRoom01FlightGate;
    private int _room01RestartCount;
    private float _room01SecondGateClosestDistance = float.PositiveInfinity;
    private Vector3 _room01SecondGateClosestPosition;
    private bool _solutionSmokeFinishing;
    private float _airborneStartVelocityX;
    private float _stickyStartSpeed;
    private float _acceleratorStartSpeed;
    private float _coastingStartSpeed;
    private float _coastingStartAngularSpeed;
    private ProfiledSurfaceBody? _timedGlassSmokePad;
    private ExitDoor3D? _exitDoor;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] userArguments = OS.GetCmdlineUserArgs();
        _runMovementSmoke = Array.Exists(userArguments, argument => argument == "--movement-smoke");
        _runRoom01SolutionSmoke = Array.Exists(userArguments, argument => argument == "--room01-solution-smoke");
        _runRoom01GateBypassSmoke = Array.Exists(userArguments, argument => argument == "--room01-gate-bypass-smoke");
        _runRoom01Preview = Array.Exists(userArguments, argument => argument == "--room01-preview");
        _runBallSpinPreview = Array.Exists(userArguments, argument => argument == "--ball-spin-preview");
        _runShellSmoke = Array.Exists(userArguments, argument => argument == "--room-shell-smoke");
        BuildRoom01();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room01_a", new Vector3(7.4f, 13.2f, 28.0f), new Vector3(0.0f, 3.6f, -22.0f), 58.0f),
            new("room01_b", new Vector3(-7.1f, 10.5f, -43.0f), new Vector3(0.0f, 4.0f, 5.0f), 60.0f),
        });

        _player = GetNode<PlayerBall>("Player");
        _cameraRig = GetNode<PlayerCameraRig>("CameraRig");
        _spawnTransform = _player.GlobalTransform;
        _cameraRig.Follow(_player);
        _player.MovementBasis = _cameraRig.MovementBasis;

        if (_runMovementSmoke)
        {
            BuildMovementSmokeStickyPad();
            BuildMovementSmokeAcceleratorPad();
            BuildMovementSmokeSuperElasticPad();
            BuildMovementSmokeTimedGlassPad();
        }

        if (_runRoom01Preview || _runBallSpinPreview)
        {
            _cameraRig.SetInputEnabled(false);
        }

        if (_runRoom01SolutionSmoke)
        {
            _solutionTrace = GD.Load<SolutionTrace>(Room01TracePath);
            if (_solutionTrace is null ||
                _solutionTrace.RoomId != RoomId ||
                _solutionTrace.MoveInputs.Count == 0 ||
                _solutionTrace.MoveDurationsTicks.Length != _solutionTrace.MoveInputs.Count)
            {
                string details = _solutionTrace is null
                    ? "trace=null"
                    : $"trace_room='{_solutionTrace.RoomId}', runtime_room='{RoomId}', inputs={_solutionTrace.MoveInputs.Count}, hold_last={_solutionTrace.HoldLastInput}";
                FailSolutionSmoke($"The Room 01 SolutionTrace could not be loaded or did not match the room ({details}).");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_runRoom01Preview && ++_previewFrames >= 30)
        {
            string capturePath = ProjectSettings.GlobalizePath("user://room01-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(capturePath);
            GD.Print($"ROOM01_PREVIEW_CAPTURE: {capturePath}");
            GetTree().Quit(0);
        }

        if (_runBallSpinPreview)
        {
            _previewFrames++;
            if (_previewFrames == 14 || _previewFrames == 46)
            {
                string suffix = _previewFrames == 14 ? "a" : "b";
                string capturePath = ProjectSettings.GlobalizePath($"user://ball-spin-{suffix}.png");
                GetViewport().GetTexture().GetImage().SavePng(capturePath);
                GD.Print($"BALL_SPIN_PREVIEW_CAPTURE: {capturePath}");
                if (_previewFrames == 46)
                {
                    GetTree().Quit(0);
                }
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_runBallSpinPreview)
        {
            _player.SimulatedMoveInput = new Vector2(0.0f, -1.0f);
            return;
        }

        if (_runShellSmoke)
        {
            RunShellSmokeTick();
            return;
        }

        if (_runMovementSmoke)
        {
            RunMovementSmokeTick();
            return;
        }

        if (_runRoom01SolutionSmoke)
        {
            RunRoom01SolutionTick();
            return;
        }

        if (_runRoom01GateBypassSmoke)
        {
            RunRoom01GateBypassTick();
            return;
        }

        if (_player.GlobalPosition.Y < -10.0f)
        {
            RestartRoom();
        }

    }

    public override void RestartRoom()
    {
        if (_runRoom01SolutionSmoke && _solutionTick > 0)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} touched the hazard floor at {_player.GlobalPosition} after {_nextRoom01FlightGate}/{_room01FlightGates.Count} gates; closest second-ring pass={_room01SecondGateClosestDistance:F2}m at {_room01SecondGateClosestPosition}.");
            return;
        }
        _room01RestartCount++;
        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _player.ResetTo(_spawnTransform);
        _nextRoom01FlightGate = 0;
        _exitDoor?.ResetClosed();
        foreach (FlightGate3D gate in _room01FlightGates)
        {
            gate.ResetGate();
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
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 01 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 01 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void RunMovementSmokeTick()
    {
        _smokeTick++;

        if (_smokeTick == 1)
        {
            _player.LinearVelocity = new Vector3(0.0f, 0.0f, -40.0f);
            _player.ApplyProfile(new PlayerProfile { TrailId = "trail-gold" }, trailAllowed: true);
            if (!_player.IsTrailEnabled || !_player.IsTrailEmitting || !_player.IsTrailVisible)
            {
                FailMovementSmoke("A configured high-speed trail was not visible in third person.");
                return;
            }

            _cameraRig.SetFirstPerson(true);
            if (!_cameraRig.IsFirstPerson || _player.IsVisualVisible ||
                !_player.IsTrailEmitting || _player.IsTrailVisible || !_cameraRig.IsTrailLayerVisible)
            {
                FailMovementSmoke("First-person camera did not hide the trail at extreme speed.");
                return;
            }

            _player.LinearVelocity = new Vector3(0.0f, 0.0f, -12.0f);
            _cameraRig.SetFirstPerson(true);
            if (!_player.IsTrailEmitting || !_player.IsTrailVisible || !_cameraRig.IsTrailLayerVisible)
            {
                FailMovementSmoke("First-person camera did not show the trail at normal movement speed.");
                return;
            }

            _cameraRig.SetFirstPerson(false);
            if (_cameraRig.IsFirstPerson || !_player.IsVisualVisible ||
                !_player.IsTrailEmitting || !_player.IsTrailVisible || !_cameraRig.IsTrailLayerVisible)
            {
                FailMovementSmoke("Third-person camera did not restore both the player visual and its configured trail.");
                return;
            }

            _cameraRig.MovementBasis.Rotation = new Vector3(0.0f, 1.0f, 0.0f);
            _player.ResetTo(_spawnTransform);
            if (Mathf.Abs(_cameraRig.MovementBasis.Rotation.Y) > 0.001f)
            {
                FailMovementSmoke("Respawning did not restore the room's initial camera direction.");
                return;
            }
        }

        if (_smokeTick <= 100)
        {
            _player.SimulatedMoveInput = new Vector2(0.0f, -1.0f);
            return;
        }

        if (_smokeTick == 101)
        {
            if (_player.GlobalPosition.Z > _spawnTransform.Origin.Z - 2.0f)
            {
                FailMovementSmoke("Ground input did not move the player forward.");
                return;
            }

            _player.ResetTo(_spawnTransform);
            _player.LinearVelocity = new Vector3(0.0f, 0.0f, -30.0f);
            return;
        }

        if (_smokeTick < 107)
        {
            return;
        }

        if (_smokeTick == 107)
        {
            if (-_player.LinearVelocity.Z < 20.0f)
            {
                FailMovementSmoke("Externally supplied momentum was clamped to drive speed.");
                return;
            }

            _player.ResetTo(new Transform3D(Basis.Identity, new Vector3(0.0f, 5.0f, 2.0f)));
            _player.LinearVelocity = new Vector3(2.0f, 0.0f, 0.0f);
            _player.SimulatedMoveInput = new Vector2(-1.0f, 0.0f);
            _airborneStartVelocityX = _player.LinearVelocity.X;
            return;
        }

        if (_smokeTick < 127)
        {
            return;
        }

        if (_smokeTick == 127)
        {
            if (_player.IsGrounded)
            {
                FailMovementSmoke("Player became grounded before the airborne-control check completed.");
                return;
            }

            if (Mathf.Abs(_player.LinearVelocity.X - _airborneStartVelocityX) > 0.05f)
            {
                FailMovementSmoke($"Airborne input changed horizontal velocity from {_airborneStartVelocityX:F3} to {_player.LinearVelocity.X:F3}.");
                return;
            }

            _player.ResetTo(new Transform3D(Basis.Identity, new Vector3(40.0f, 1.15f, 0.0f)));
            _player.LinearVelocity = new Vector3(10.0f, 0.0f, 0.0f);
            _player.SimulatedMoveInput = Vector2.Zero;
            _stickyStartSpeed = _player.LinearVelocity.X;
            return;
        }

        if (_smokeTick < 190)
        {
            return;
        }

        if (_smokeTick == 190)
        {
            if (!_player.IsGrounded || _player.GroundSurfaceKind != SurfaceKind.Sticky || _player.GroundLinearDrag < 2.39f)
            {
                FailMovementSmoke($"Sticky surface was not detected correctly: grounded={_player.IsGrounded}, kind={_player.GroundSurfaceKind}, drag={_player.GroundLinearDrag:F2}.");
                return;
            }

            float stickyEndSpeed = Mathf.Abs(_player.LinearVelocity.X);
            if (stickyEndSpeed >= _stickyStartSpeed * 0.35f)
            {
                FailMovementSmoke($"Sticky surface did not remove enough momentum: {_stickyStartSpeed:F2} -> {stickyEndSpeed:F2} m/s.");
                return;
            }

            _player.ResetTo(new Transform3D(Basis.Identity, new Vector3(60.0f, 1.15f, 5.0f)));
            _player.SimulatedMoveInput = Vector2.Zero;
            _acceleratorStartSpeed = -_player.LinearVelocity.Z;
            return;
        }

        if (_smokeTick < 250)
        {
            return;
        }

        if (_smokeTick == 250)
        {
            float acceleratorEndSpeed = -_player.LinearVelocity.Z;
            if (!_player.IsGrounded ||
                _player.GroundSurfaceKind != SurfaceKind.Accelerator ||
                _player.GroundSurfaceAcceleration.Z > -17.9f)
            {
                FailMovementSmoke($"Accelerator surface was not detected correctly: grounded={_player.IsGrounded}, kind={_player.GroundSurfaceKind}, acceleration={_player.GroundSurfaceAcceleration}.");
                return;
            }

            if (acceleratorEndSpeed < _acceleratorStartSpeed + 12.0f)
            {
                FailMovementSmoke($"Accelerator did not add enough no-input momentum: {_acceleratorStartSpeed:F2} -> {acceleratorEndSpeed:F2} m/s.");
                return;
            }

            _player.ResetTo(new Transform3D(Basis.Identity, new Vector3(80.0f, 8.0f, 0.0f)));
            _player.LinearVelocity = new Vector3(6.0f, 0.0f, 0.0f);
            _player.SimulatedMoveInput = Vector2.Zero;
            return;
        }

        if (_smokeTick < 345)
        {
            return;
        }

        if (_smokeTick == 345)
        {
            if (_player.SuperElasticBounceCount != 1 ||
                _player.LastSuperElasticImpactSpeed < 9.0f ||
                _player.LastSuperElasticLaunchSpeed < _player.LastSuperElasticImpactSpeed * 1.6f ||
                _player.LinearVelocity.Y <= 0.0f ||
                Mathf.Abs(_player.LinearVelocity.X) < 5.8f)
            {
                FailMovementSmoke($"Super-elastic bounce did not add height while preserving tangential momentum: count={_player.SuperElasticBounceCount}, impact={_player.LastSuperElasticImpactSpeed:F2}, launch={_player.LastSuperElasticLaunchSpeed:F2}, velocity={_player.LinearVelocity}.");
                return;
            }

            if (_timedGlassSmokePad is null)
            {
                FailMovementSmoke("Timed glass pad was not created.");
                return;
            }

            _player.ResetTo(new Transform3D(Basis.Identity, new Vector3(100.0f, 0.9f, 0.0f)));
            _player.SimulatedMoveInput = Vector2.Zero;
            return;
        }

        if (_smokeTick == 358)
        {
            if (_timedGlassSmokePad!.IsBroken ||
                _timedGlassSmokePad.LongestContinuousGlassContactSeconds >= _timedGlassSmokePad.BreakDelaySeconds)
            {
                FailMovementSmoke($"Timed glass broke before its contact delay: contact={_timedGlassSmokePad.LongestContinuousGlassContactSeconds:F3}, delay={_timedGlassSmokePad.BreakDelaySeconds:F3}.");
                return;
            }
        }

        if (_smokeTick == 370)
        {
            CollisionShape3D glassCollision = _timedGlassSmokePad!.GetChildren().OfType<CollisionShape3D>().First();
            if (!_timedGlassSmokePad.IsBroken || !glassCollision.Disabled)
            {
                FailMovementSmoke($"Timed glass did not break after uninterrupted contact: broken={_timedGlassSmokePad.IsBroken}, collision_disabled={glassCollision.Disabled}, contact={_timedGlassSmokePad.LongestContinuousGlassContactSeconds:F3}, delay={_timedGlassSmokePad.BreakDelaySeconds:F3}.");
                return;
            }

            _player.ResetTo(_spawnTransform);
            _player.SimulatedMoveInput = null;
            return;
        }

        if (_smokeTick == 373)
        {
            CollisionShape3D glassCollision = _timedGlassSmokePad!.GetChildren().OfType<CollisionShape3D>().First();
            if (_timedGlassSmokePad.IsBroken || glassCollision.Disabled)
            {
                FailMovementSmoke($"Timed glass did not restore on respawn: broken={_timedGlassSmokePad.IsBroken}, collision_disabled={glassCollision.Disabled}.");
                return;
            }

            _player.SimulatedMoveInput = new Vector2(1.0f, -1.0f);
            return;
        }

        if (_smokeTick < 403)
        {
            return;
        }

        if (_smokeTick == 403)
        {
            if (Mathf.Abs(_player.LinearVelocity.X) < 2.0f || -_player.LinearVelocity.Z < 2.0f)
            {
                FailMovementSmoke($"Diagonal input did not drive both axes: velocity={_player.LinearVelocity}.");
                return;
            }

            _player.ResetTo(_spawnTransform);
            _player.LinearVelocity = new Vector3(5.0f, 0.0f, 0.0f);
            _player.AngularVelocity = new Vector3(0.0f, 0.0f, -5.0f / 0.6f);
            _player.SimulatedMoveInput = Vector2.Zero;
            _coastingStartSpeed = _player.LinearVelocity.Length();
            _coastingStartAngularSpeed = _player.AngularVelocity.Length();
            return;
        }

        if (_smokeTick < 463)
        {
            return;
        }

        float coastingEndSpeed = _player.LinearVelocity.Slide(_player.GroundNormal).Length();
        float coastingEndAngularSpeed = _player.AngularVelocity.Length();
        if (!_player.IsGrounded ||
            coastingEndSpeed >= _coastingStartSpeed - 0.2f ||
            coastingEndSpeed <= _coastingStartSpeed - 2.5f ||
            coastingEndAngularSpeed >= _coastingStartAngularSpeed - 0.2f ||
            coastingEndAngularSpeed <= _coastingStartAngularSpeed - 4.2f)
        {
            FailMovementSmoke($"No-input coasting did not slow movement and visible rotation together: linear {_coastingStartSpeed:F2} -> {coastingEndSpeed:F2} m/s, angular {_coastingStartAngularSpeed:F2} -> {coastingEndAngularSpeed:F2} rad/s, grounded={_player.IsGrounded}.");
            return;
        }

        GD.Print($"MOVEMENT_SMOKE_PASS: grounded and diagonal drive, synchronized linear and visible rotational coasting, external momentum, zero air control, canonical sticky/elastic surfaces, distinct-surface Double Bounce tracking, centered directional materials, timed glass break/restore and momentum-preserving super-elastic bounce work.");
        GetTree().Quit(0);
    }

    private void BuildMovementSmokeStickyPad()
    {
        SurfaceProfile? stickyProfile = GD.Load<SurfaceProfile>(StickySurfacePath);
        ShaderMaterial? stickyMaterial = GD.Load<ShaderMaterial>(StickyMaterialPath);
        AudioStream? stickyContactSfx = GD.Load<AudioStream>(StickyContactSfxPath);
        if (stickyProfile is null ||
            stickyProfile.Kind != SurfaceKind.Sticky ||
            stickyProfile.Friction < 0.0f ||
            stickyProfile.LinearDrag <= 0.0f ||
            stickyMaterial?.Shader is null ||
            stickyContactSfx is null)
        {
            FailMovementSmoke("The production sticky SurfaceProfile is missing or invalid.");
            return;
        }

        StaticBody3D stickyPad = RoomGeometry.AddBox(
            this,
            "MovementSmokeStickyPad",
            new Vector3(16.0f, 0.5f, 8.0f),
            new Vector3(40.0f, 0.0f, 0.0f),
            Vector3.Zero,
            string.Empty,
            Colors.White,
            0.0f,
            1.0f,
            surfaceProfile: stickyProfile,
            materialOverride: stickyMaterial);
        stickyPad.AddChild(new AudioStreamPlayer3D
        {
            Name = "StickyContactSfx",
            Stream = stickyContactSfx,
            Bus = "SFX",
        });
    }

    private void BuildMovementSmokeAcceleratorPad()
    {
        SurfaceProfile? acceleratorProfile = GD.Load<SurfaceProfile>(AcceleratorSurfacePath);
        ShaderMaterial? acceleratorMaterial = GD.Load<ShaderMaterial>(AcceleratorMaterialPath);
        AudioStream? acceleratorContactSfx = GD.Load<AudioStream>(AcceleratorContactSfxPath);
        string acceleratorShaderCode = acceleratorMaterial?.Shader?.Code ?? string.Empty;
        string oneWayTeethTexture = Godot.FileAccess.GetFileAsString(OneWayTeethTexturePath);
        if (acceleratorProfile is null ||
            acceleratorProfile.Kind != SurfaceKind.Accelerator ||
            acceleratorProfile.Acceleration.Length() < 17.9f ||
            acceleratorMaterial?.Shader is null ||
            !acceleratorShaderCode.Contains("surface_u_span", StringComparison.Ordinal) ||
            !acceleratorShaderCode.Contains("UV.x / max(surface_u_span", StringComparison.Ordinal) ||
            !acceleratorShaderCode.Contains("head_width", StringComparison.Ordinal) ||
            !acceleratorShaderCode.Contains("smoothstep(-0.45, -0.40, cell.y)", StringComparison.Ordinal) ||
            acceleratorShaderCode.Contains("step(-0.27, cell.y)", StringComparison.Ordinal) ||
            acceleratorShaderCode.Contains("UV.x * 3.0", StringComparison.Ordinal) ||
            !oneWayTeethTexture.Contains("One centered directional-tooth column", StringComparison.Ordinal) ||
            oneWayTeethTexture.Contains("Four-by-four directional teeth", StringComparison.Ordinal) ||
            acceleratorContactSfx is null)
        {
            FailMovementSmoke("The production accelerator profile, single centered arrow-column material or contact SFX is missing or invalid.");
            return;
        }

        StaticBody3D acceleratorPad = RoomGeometry.AddBox(
            this,
            "MovementSmokeAcceleratorPad",
            new Vector3(8.0f, 0.5f, 16.0f),
            new Vector3(60.0f, 0.0f, 0.0f),
            Vector3.Zero,
            string.Empty,
            Colors.White,
            0.0f,
            1.0f,
            surfaceProfile: acceleratorProfile,
            materialOverride: acceleratorMaterial);
        ShaderMaterial resolvedAcceleratorMaterial = (ShaderMaterial)((MeshInstance3D)acceleratorPad.GetChild(0)).MaterialOverride;
        float expectedAcceleratorSpan = 8.0f / SurfaceMeshFactory.DefaultTileWorldSize;
        float actualAcceleratorSpan = resolvedAcceleratorMaterial.GetShaderParameter("surface_u_span").AsSingle();
        if (Mathf.Abs(actualAcceleratorSpan - expectedAcceleratorSpan) > 0.001f)
        {
            FailMovementSmoke($"Accelerator arrow column was not centered for its real UV span: expected={expectedAcceleratorSpan:F3}, actual={actualAcceleratorSpan:F3}.");
            return;
        }

        SurfaceProfile? oneWayProfile = GD.Load<SurfaceProfile>(OneWaySurfacePath);
        if (oneWayProfile is null || oneWayProfile.Kind != SurfaceKind.OneWayGrip)
        {
            FailMovementSmoke("The production one-way surface profile is missing or invalid.");
            return;
        }

        StaticBody3D oneWayPad = RoomGeometry.AddBox(
            this,
            "MovementSmokeOneWayPad",
            new Vector3(8.0f, 0.5f, 16.0f),
            new Vector3(120.0f, 0.0f, 0.0f),
            Vector3.Zero,
            OneWayTeethTexturePath,
            Colors.White,
            0.0f,
            1.0f,
            surfaceProfile: oneWayProfile);
        StandardMaterial3D resolvedOneWayMaterial = (StandardMaterial3D)((MeshInstance3D)oneWayPad.GetChild(0)).MaterialOverride;
        float expectedOneWayScale = SurfaceMeshFactory.DefaultTileWorldSize / 8.0f;
        if (Mathf.Abs(resolvedOneWayMaterial.Uv1Scale.X - expectedOneWayScale) > 0.001f)
        {
            FailMovementSmoke($"One-way tooth column was not centered for its real UV span: expected={expectedOneWayScale:F3}, actual={resolvedOneWayMaterial.Uv1Scale.X:F3}.");
            return;
        }

        string room08Source = Godot.FileAccess.GetFileAsString("res://src/Gameplay/Rooms/Room08Runtime.cs");
        string room10Source = Godot.FileAccess.GetFileAsString("res://src/Gameplay/Rooms/Room10Runtime.cs");
        string room22Source = Godot.FileAccess.GetFileAsString("res://src/Gameplay/Rooms/Room22Runtime.cs");
        if (room08Source.Contains("AddPhysicalArrows", StringComparison.Ordinal) ||
            room10Source.Contains("AddAcceleratorArrows", StringComparison.Ordinal) ||
            room22Source.Contains("RaisedTooth", StringComparison.Ordinal))
        {
            FailMovementSmoke("A room still adds a duplicate physical arrow or tooth layer above the shared directional material.");
            return;
        }
        acceleratorPad.AddChild(new AudioStreamPlayer3D
        {
            Name = "AcceleratorContactSfx",
            Stream = acceleratorContactSfx,
            Bus = "SFX",
        });
    }

    private void BuildMovementSmokeSuperElasticPad()
    {
        if (PlayerBall.ResolveConsecutiveElasticSurfaceCount(0UL, 101UL, 0) != 1 ||
            PlayerBall.ResolveConsecutiveElasticSurfaceCount(101UL, 101UL, 1) != 1 ||
            PlayerBall.ResolveConsecutiveElasticSurfaceCount(101UL, 202UL, 1) != 2)
        {
            FailMovementSmoke("Double Bounce progression did not require two distinct elastic surface instances.");
            return;
        }

        SurfaceProfile? bounceProfile = GD.Load<SurfaceProfile>(SuperElasticSurfacePath);
        ShaderMaterial? bounceMaterial = GD.Load<ShaderMaterial>(SuperElasticMaterialPath);
        AudioStream? bounceSfx = GD.Load<AudioStream>(SuperElasticBounceSfxPath);
        if (bounceProfile is null ||
            bounceProfile.Kind != SurfaceKind.SuperElastic ||
            bounceProfile.BounceMultiplier < 1.6f ||
            bounceMaterial?.Shader is null ||
            bounceSfx is null)
        {
            FailMovementSmoke("The production super-elastic profile, material or bounce SFX is missing or invalid.");
            return;
        }

        StaticBody3D bouncePad = RoomGeometry.AddBox(
            this,
            "MovementSmokeSuperElasticPad",
            new Vector3(30.0f, 0.5f, 10.0f),
            new Vector3(80.0f, 0.0f, 0.0f),
            Vector3.Zero,
            string.Empty,
            Colors.White,
            0.0f,
            1.0f,
            surfaceProfile: bounceProfile,
            materialOverride: bounceMaterial);
        bouncePad.AddChild(new AudioStreamPlayer3D
        {
            Name = "SuperElasticBounceSfx",
            Stream = bounceSfx,
            Bus = "SFX",
        });
    }

    private void BuildMovementSmokeTimedGlassPad()
    {
        SurfaceProfile? glassProfile = GD.Load<SurfaceProfile>(FrictionlessSurfacePath);
        if (glassProfile is null || glassProfile.Kind != SurfaceKind.Frictionless)
        {
            FailMovementSmoke("The production frictionless glass profile is missing or invalid.");
            return;
        }

        _timedGlassSmokePad = (ProfiledSurfaceBody)RoomGeometry.AddBox(
            this,
            "MovementSmokeTimedGlassPad",
            new Vector3(10.0f, 0.5f, 10.0f),
            new Vector3(100.0f, 0.0f, 0.0f),
            Vector3.Zero,
            FrictionlessTexturePath,
            new Color("a9d4df"),
            0.06f,
            0.2f,
            surfaceProfile: glassProfile);
        _timedGlassSmokePad.SetMeta("break_delay_seconds", 0.3f);
    }

    private void RunRoom01SolutionTick()
    {
        if (_solutionTrace is null)
        {
            return;
        }

        if (_solutionWarmupTicks > 0)
        {
            _player.SimulatedMoveInput = null;
            _solutionWarmupTicks--;
            return;
        }

        if (IsComplete)
        {
            if (_nextRoom01FlightGate != _room01FlightGates.Count)
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} reached the cup after only {_nextRoom01FlightGate}/{_room01FlightGates.Count} flight gates.");
                return;
            }

            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print($"ROOM01_SOLUTION_PASS: SolutionTrace completed Room 01 {_solutionRun} consecutive times.");
                FinishSolutionSmoke(0);
                return;
            }

            ClearCompletionState();
            _player.SimulatedMoveInput = null;
            _player.ResetTo(_spawnTransform);
            _solutionTick = 0;
            _solutionWarmupTicks = 6;
            _nextRoom01FlightGate = 0;
            _room01SecondGateClosestDistance = float.PositiveInfinity;
            _room01SecondGateClosestPosition = Vector3.Zero;
            foreach (FlightGate3D gate in _room01FlightGates)
            {
                gate.ResetGate();
            }
            return;
        }

        if (_nextRoom01FlightGate == 1 && _room01FlightGates.Count > 1)
        {
            Vector3 ringPosition = _room01FlightGates[1].GlobalPosition;
            if (Mathf.Abs(_player.GlobalPosition.Z - ringPosition.Z) <= 2.5f)
            {
                float openingDistance = new Vector2(
                    _player.GlobalPosition.X - ringPosition.X,
                    _player.GlobalPosition.Y - ringPosition.Y).Length();
                if (openingDistance < _room01SecondGateClosestDistance)
                {
                    _room01SecondGateClosestDistance = openingDistance;
                    _room01SecondGateClosestPosition = _player.GlobalPosition;
                }
            }
        }

        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} timed out at {_player.GlobalPosition}, velocity={_player.LinearVelocity}, grounded={_player.IsGrounded}, input={ResolveRoom01TraceInput(_solutionTick - 1)}, restarts={_room01RestartCount}, after {_nextRoom01FlightGate}/{_room01FlightGates.Count} flight gates; closest second-ring pass={_room01SecondGateClosestDistance:F2}m at {_room01SecondGateClosestPosition}.");
            return;
        }

        _player.SimulatedMoveInput = ResolveRoom01TraceInput(_solutionTick - 1);
    }

    private void RunRoom01GateBypassTick()
    {
        if (_solutionSmokeFinishing)
        {
            return;
        }

        _gateBypassTick++;
        if (_gateBypassPhase == 0 && _gateBypassTick == 1)
        {
            _player.GravityScale = 0.0f;
            FlightGate3D gate = _room01FlightGates[0];
            Vector3 outsideOpening = gate.ToGlobal(new Vector3(gate.Radius - 0.25f, 0.0f, 0.0f));
            _player.ResetTo(new Transform3D(Basis.Identity, outsideOpening));
            return;
        }

        if (_gateBypassPhase == 0 && _gateBypassTick == 10)
        {
            FlightGate3D gate = _room01FlightGates[0];
            if (!gate.IsActivated)
            {
                FailRoom01GateBypassSmoke("The first gate did not activate even though the ball's center was inside its clear opening.");
                return;
            }

            gate.ResetGate();
            _nextRoom01FlightGate = 0;
            _gateBypassPhase = 1;
            _gateBypassTick = 0;
            return;
        }

        if (_gateBypassPhase == 1 && _gateBypassTick == 1)
        {
            BeginRoom01BypassFlight(
                new Vector3(3.5f, 6.75f, 7.65f),
                new Vector3(0.0f, 0.0f, -30.0f),
                nextExpectedGate: 0);
            return;
        }

        if (_gateBypassPhase == 1 && _player.ResetCount > _gateBypassStartResetCount)
        {
            if (_nextRoom01FlightGate != 0)
            {
                FailRoom01GateBypassSmoke("The first no-ring jump accidentally activated a flight gate.");
                return;
            }
            if (_gateBypassObservedGateMask != 0)
            {
                FailRoom01GateBypassSmoke($"The first no-ring jump crossed a gate before its fall (mask={_gateBypassObservedGateMask}).");
                return;
            }
            _gateBypassPhase = 2;
            _gateBypassTick = 0;
            return;
        }
        if (_gateBypassPhase == 1 && _gateBypassTick >= 150)
        {
            FailRoom01GateBypassSmoke($"A 30 m/s no-ring launch crossed the first physical gap and remained at {_player.GlobalPosition}.");
            return;
        }

        if (_gateBypassPhase == 2 && _gateBypassTick == 1)
        {
            BeginRoom01BypassFlight(
                new Vector3(-3.0f, 0.5f, -35.85f - Room01DownstreamOffset),
                new Vector3(0.0f, 0.0f, -24.0f),
                nextExpectedGate: 1);
            return;
        }
        if (_gateBypassPhase == 2 && _player.ResetCount > _gateBypassStartResetCount)
        {
            if (_room01FlightGates[1].IsActivated)
            {
                FailRoom01GateBypassSmoke("The second no-ring jump accidentally activated its flight gate.");
                return;
            }
            if (_gateBypassObservedGateMask != 0)
            {
                FailRoom01GateBypassSmoke($"The second no-ring jump crossed a gate before its fall (mask={_gateBypassObservedGateMask}).");
                return;
            }
            _gateBypassPhase = 3;
            _gateBypassTick = 0;
            return;
        }
        if (_gateBypassPhase == 2 && _gateBypassTick >= 150)
        {
            FailRoom01GateBypassSmoke($"A 24 m/s no-ring launch crossed the second physical gap and remained at {_player.GlobalPosition}.");
            return;
        }

        if (_gateBypassPhase >= 3 && _gateBypassPhase <= 6 && _gateBypassTick == 1)
        {
            bool firstGap = _gateBypassPhase <= 4;
            bool rightWall = (_gateBypassPhase & 1) == 1;
            BeginRoom01BypassFlight(
                firstGap
                    ? new Vector3(rightWall ? 6.4f : -6.4f, 6.75f, 7.65f)
                    : new Vector3(rightWall ? 6.4f : -6.4f, 0.5f, -35.85f - Room01DownstreamOffset),
                firstGap
                    ? new Vector3(rightWall ? 10.0f : -10.0f, 0.0f, -30.0f)
                    : new Vector3(rightWall ? 10.0f : -10.0f, 0.0f, -24.0f),
                nextExpectedGate: firstGap ? 0 : 1);
            return;
        }

        if (_gateBypassPhase >= 3 && _gateBypassPhase <= 6 && _player.ResetCount > _gateBypassStartResetCount)
        {
            if (_gateBypassObservedGateMask != 0)
            {
                FailRoom01GateBypassSmoke($"Wall-bounce attempt {_gateBypassPhase - 2} crossed a flight gate before its fall (mask={_gateBypassObservedGateMask}).");
                return;
            }
            _gateBypassPhase++;
            _gateBypassTick = 0;
            return;
        }
        if (_gateBypassPhase >= 3 && _gateBypassPhase <= 6 && _gateBypassTick >= 180)
        {
            FailRoom01GateBypassSmoke($"Wall-bounce attempt {_gateBypassPhase - 2} avoided its required ring without falling; player={_player.GlobalPosition}, velocity={_player.LinearVelocity}.");
            return;
        }

        if (_gateBypassPhase == 7 && _gateBypassTick == 1)
        {
            _player.GravityScale = 0.0f;
            _nextRoom01FlightGate = 1;
            _exitDoor?.ResetClosed();
            _player.ResetTo(GetNode<Area3D>("GoalCup").GlobalTransform);
            return;
        }
        if (_gateBypassPhase == 7 && _gateBypassTick == 20)
        {
            if (IsComplete || IsExitTraversalPending || (_exitDoor?.OpenAmount ?? 0.0f) > 0.01f)
            {
                FailRoom01GateBypassSmoke($"The exit accepted a route with the second ring missing: complete={IsComplete}, traversal={IsExitTraversalPending}, door={_exitDoor?.OpenAmount:F2}.");
                return;
            }
            _gateBypassPhase = 8;
            _gateBypassTick = 0;
            return;
        }

        if (_gateBypassPhase == 8 && _gateBypassTick == 1)
        {
            ClearCompletionState();
            _nextRoom01FlightGate = _room01FlightGates.Count;
            _player.GravityScale = 0.0f;
            _player.ResetTo(new Transform3D(
                Basis.Identity,
                new Vector3(0.0f, -1.65f, -57.0f - Room01TotalExtension)));
            _player.LinearVelocity = new Vector3(0.0f, 0.0f, -42.0f);
            _gateBypassStartResetCount = _player.ResetCount;
            return;
        }
        if (_gateBypassPhase == 8)
        {
            if (_player.ResetCount > _gateBypassStartResetCount || _player.LinearVelocity.Z > 2.0f)
            {
                FailRoom01GateBypassSmoke($"A valid high-speed exit approach rebounded toward the second gap: player={_player.GlobalPosition}, velocity={_player.LinearVelocity}, door={_exitDoor?.OpenAmount:F2}.");
                return;
            }
            if (IsComplete)
            {
                GD.Print("ROOM01_GATE_BYPASS_PASS: center-inside contact activated, both rings rejected straight and left/right wall-bounce bypasses, the one-ring exit stayed locked, and a valid 42 m/s approach traversed without rebound.");
                FinishSolutionSmoke(0);
                return;
            }
            if (_gateBypassTick >= 300)
            {
                FailRoom01GateBypassSmoke($"A valid high-speed approach did not traverse the exit; pending={IsExitTraversalPending}, player={_player.GlobalPosition}, velocity={_player.LinearVelocity}, door={_exitDoor?.OpenAmount:F2}.");
            }
        }
    }

    private void BeginRoom01BypassFlight(Vector3 position, Vector3 velocity, int nextExpectedGate)
    {
        _nextRoom01FlightGate = nextExpectedGate;
        _gateBypassObservedGateMask = 0;
        _player.GravityScale = 1.0f;
        _player.SimulatedMoveInput = Vector2.Zero;
        _player.ResetTo(new Transform3D(Basis.Identity, position));
        _player.LinearVelocity = velocity;
        _gateBypassStartResetCount = _player.ResetCount;
    }

    private void FailRoom01GateBypassSmoke(string message)
    {
        GD.PushError($"ROOM01_GATE_BYPASS_FAIL: {message}");
        FinishSolutionSmoke(1);
    }

    private Vector2 ResolveRoom01TraceInput(int tick)
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

    private void FailMovementSmoke(string message)
    {
        GD.PushError($"MOVEMENT_SMOKE_FAIL: {message}");
        GetTree().Quit(1);
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM01_SOLUTION_FAIL: {message}");
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
        foreach (FlightGate3D gate in _room01FlightGates)
        {
            gate.ResetGate();
            gate.QueueFree();
        }
        _room01FlightGates.Clear();
        _solutionTrace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(exitCode);
    }

    private void BuildRoom01()
    {
        const string metalTexture = "res://assets/textures/brushed_metal.png";
        const string caramelTexture = "res://assets/textures/caramel_plates.svg";

        RoomGeometry.AddClosedRoomShell(
            this,
            "RoomShell",
            new Vector3(0.0f, 0.0f, -15.0f - (Room01TotalExtension * 0.5f)),
            new Vector2(19.0f, 110.0f + Room01TotalExtension),
            -4.5f,
            16.0f,
            metalTexture,
            new Color("8b8176"),
            new Color("a06b4b"),
            body =>
            {
                if (body is PlayerBall)
                {
                    RestartRoom();
                }
            });

        // The ramp endpoints are calculated to meet the neighbouring flat top at
        // the same height.  The tiny overlap is horizontal, never a raised lip.
        // Preserve the original front edge at z=26.5 while extending the safe
        // spawn deck to the inside face of the back wall.  There is no hazard
        // slit behind the player before the first puzzle element.
        RoomGeometry.AddBox(this, "SafeStartDeck", new Vector3(15.0f, 0.5f, 13.275f), new Vector3(0.0f, 10.0f, 33.1375f), Vector3.Zero, metalTexture, new Color("aaa39a"), 0.48f, 0.62f);
        RoomGeometry.AddBox(this, "FirstLaunchSlope", new Vector3(15.0f, 0.5f, 16.908134f), new Vector3(0.0f, 7.950513f, 18.360829f), new Vector3(Mathf.DegToRad(-14.082268f), 0.0f, 0.0f), caramelTexture, new Color("b3784e"), 0.12f, 0.64f);
        RoomGeometry.AddBox(this, "FirstLaunchLip", new Vector3(15.0f, 0.32f, 2.2f), new Vector3(0.0f, 5.976f, 9.0f), Vector3.Zero, caramelTexture, new Color("b3784e"), 0.12f, 0.64f);
        RoomGeometry.AddBox(this, "FirstLanding", new Vector3(15.0f, 0.6f, 13.5f + Room01IntermediatePlatformExtension), new Vector3(0.0f, 3.7f, -10.25f - (Room01IntermediatePlatformExtension * 0.5f) - Room01FirstGapExtension), Vector3.Zero, metalTexture, new Color("9da5a2"), 0.42f, 0.64f);
        RoomGeometry.AddBox(this, "SecondLaunchSlope", new Vector3(15.0f, 0.5f, 16.908134f), new Vector3(0.0f, 1.700513f, -25.139171f - Room01DownstreamOffset), new Vector3(Mathf.DegToRad(-14.082268f), 0.0f, 0.0f), caramelTexture, new Color("aa7049"), 0.12f, 0.66f);
        RoomGeometry.AddBox(this, "SecondLaunchLip", new Vector3(15.0f, 0.32f, 2.2f), new Vector3(0.0f, -0.274f, -34.5f - Room01DownstreamOffset), Vector3.Zero, caramelTexture, new Color("aa7049"), 0.12f, 0.66f);
        RoomGeometry.AddBox(this, "SecondLanding", new Vector3(15.0f, 0.6f, 4.0f), new Vector3(0.0f, -2.55f, -56.6f - Room01TotalExtension), Vector3.Zero, metalTexture, new Color("98a19f"), 0.44f, 0.64f);
        RoomGeometry.AddBox(this, "ExitRunout", new Vector3(15.0f, 0.6f, 10.85f), new Vector3(0.0f, -2.55f, -64.025f - Room01TotalExtension), Vector3.Zero, metalTexture, new Color("a49d94"), 0.46f, 0.62f);

        foreach (float x in new[] { -7.67f, 7.67f })
        {
            RoomGeometry.AddBox(
                this,
                $"SafeStartRail{x}",
                new Vector3(0.34f, 1.2f, 13.275f),
                new Vector3(x, 10.85f, 33.1375f),
                Vector3.Zero,
                metalTexture,
                new Color("746b65"),
                0.48f,
                0.58f);
        }

        AddRoom01FlightGate("FirstFlightGate", 0, new Vector3(-3.0f, 6.6f, 5.6f));
        AddRoom01FlightGate("SecondFlightGate", 1, new Vector3(0.8f, 3.5f, -28.5f - Room01DownstreamOffset));

        RoomGeometry.AddBox(this, "FirstAimDivider", new Vector3(0.28f, 0.9f, 7.0f), new Vector3(0.0f, 8.75f, 19.0f), new Vector3(Mathf.DegToRad(-14.0f), 0.0f, 0.0f), metalTexture, new Color("746b65"), 0.48f, 0.58f);

        foreach (float x in new[] { -7.75f, 7.75f })
        {
            RoomGeometry.AddBox(this, $"FirstSlopeRail{x}", new Vector3(0.34f, 1.0f, 16.908134f), new Vector3(x, 8.400513f, 18.360829f), new Vector3(Mathf.DegToRad(-14.082268f), 0.0f, 0.0f), metalTexture, new Color("746b65"), 0.48f, 0.58f);
            RoomGeometry.AddBox(this, $"FirstLandingRail{x}", new Vector3(0.34f, 1.2f, 13.5f + Room01IntermediatePlatformExtension), new Vector3(x, 4.3f, -10.25f - (Room01IntermediatePlatformExtension * 0.5f) - Room01FirstGapExtension), Vector3.Zero, metalTexture, new Color("746b65"), 0.48f, 0.58f);
            RoomGeometry.AddBox(this, $"SecondSlopeRail{x}", new Vector3(0.34f, 1.0f, 16.908134f), new Vector3(x, 2.150513f, -25.139171f - Room01DownstreamOffset), new Vector3(Mathf.DegToRad(-14.082268f), 0.0f, 0.0f), metalTexture, new Color("746b65"), 0.48f, 0.58f);
            RoomGeometry.AddBox(this, $"FinalRail{x}", new Vector3(0.34f, 1.2f, 14.85f), new Vector3(x, -1.95f, -62.025f - Room01TotalExtension), Vector3.Zero, metalTexture, new Color("746b65"), 0.48f, 0.58f);
        }

        SurfaceDetail.AddOverlay(this, "FirstLandingGrime", new Vector3(-3.8f, 4.015f, -9.9f - Room01FirstGapExtension), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(11.0f)), new Vector2(4.6f, 2.8f), "res://assets/textures/overlays/grime.svg", new Color("3a281f"), 0.48f);
        SurfaceDetail.AddOverlay(this, "SecondLandingScratches", new Vector3(3.5f, -2.235f, -56.5f - Room01TotalExtension), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(-18.0f)), new Vector2(3.6f, 2.1f), "res://assets/textures/overlays/scratches.svg", new Color("d8d1bc"), 0.38f);
        SurfaceDetail.AddOverlay(this, "FirstSlopeScuffs", new Vector3(2.2f, 8.2f, 18.2f), new Vector3(Mathf.DegToRad(-104.0f), 0.0f, Mathf.DegToRad(7.0f)), new Vector2(5.1f, 2.8f), "res://assets/textures/overlays/edge_scuffs.svg", new Color("f1c383"), 0.4f);
        SurfaceDetail.AddOverlay(this, "ExitSugarDust", new Vector3(-2.8f, -2.235f, -64.0f - Room01TotalExtension), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(17.0f)), new Vector2(4.2f, 2.8f), "res://assets/textures/overlays/sugar_dust.svg", new Color("ffe5b2"), 0.48f);
    }

    private void AddRoom01FlightGate(string name, int index, Vector3 position)
    {
        FlightGate3D gate = new()
        {
            Name = name,
            Position = position,
            Radius = index == 0 ? 2.25f : 3.0f,
            MinimumExitSpeed = index == 0 ? 42.0f : 64.0f,
            SpeedGain = index == 0 ? 5.0f : 10.0f,
            SpeedMultiplier = index == 0 ? 1.25f : 1.8f,
            MaximumExitSpeed = index == 0 ? 46.0f : 56.0f,
            AxialBoostOnly = true,
            FrameTint = index == 0 ? new Color("b88955") : new Color("9c795c"),
            EnableAudio = !_runRoom01SolutionSmoke && !_runRoom01GateBypassSmoke,
        };
        gate.Passed += player =>
        {
            if (player == _player && _runRoom01GateBypassSmoke)
            {
                _gateBypassObservedGateMask |= 1 << index;
            }
            if (player == _player && index == _nextRoom01FlightGate)
            {
                _nextRoom01FlightGate++;
                if (_runRoom01SolutionSmoke)
                {
                    GD.Print($"ROOM01_GATE_TRACE: gate={index + 1}, tick={_solutionTick}, position={_player.GlobalPosition}, velocity={_player.LinearVelocity}.");
                }
            }
        };
        AddChild(gate);
        _room01FlightGates.Add(gate);
    }

    private void BuildGoal()
    {
        Area3D routeCompletionTrigger = new()
        {
            Name = "RouteCompletionTrigger",
            Position = new Vector3(0.0f, -1.0f, -59.2f - Room01TotalExtension),
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true,
        };
        routeCompletionTrigger.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D
            {
                Size = new Vector3(15.0f, 4.0f, 1.6f),
            },
        });
        routeCompletionTrigger.BodyEntered += body =>
        {
            if (body is PlayerBall && _nextRoom01FlightGate == _room01FlightGates.Count)
            {
                CompleteRoom();
            }
        };
        AddChild(routeCompletionTrigger);

        Area3D goal = new()
        {
            Name = "GoalCup",
            Position = new Vector3(0.0f, -0.55f, -68.4f - Room01TotalExtension),
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true,
        };
        goal.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D
            {
                Size = new Vector3(3.35f, 3.4f, 1.6f),
            },
        });
        goal.BodyEntered += body =>
        {
            if (body is PlayerBall && _nextRoom01FlightGate == _room01FlightGates.Count)
            {
                CompleteRoom();
            }
        };
        AddChild(goal);
        _exitDoor = RoomGeometry.AddGoalExitDoor(this, goal.Position);
    }

    private void CreateRail(Vector3 position, float length)
    {
        StandardMaterial3D material = CreateMaterial(
            "res://assets/textures/brushed_metal.png",
            new Color("7a8992"),
            0.86f,
            0.34f);
        CreateVisualCylinder(
            $"Rail{position.X:F0}",
            position,
            new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f),
            0.13f,
            length,
            material);

        for (int index = 0; index < 7; index++)
        {
            CreateVisualBox(
                $"RailBracket{position.X:F0}_{index}",
                new Vector3(0.55f, 0.18f, 0.24f),
                new Vector3(position.X, position.Y - 0.2f, position.Z - (length * 0.5f) + 1.0f + (index * 2.35f)),
                Vector3.Zero,
                "res://assets/textures/brushed_metal.png",
                new Color("57636b"),
                0.8f,
                0.42f);
        }
    }

    private void CreateBox(
        string name,
        Vector3 size,
        Vector3 position,
        Vector3 rotation,
        string texturePath,
        float metallic,
        float roughness)
    {
        string resolvedTexturePath = RoomGeometry.ResolveSurfaceTexture(name, size, texturePath);
        StaticBody3D body = new()
        {
            Name = name,
            Position = position,
            Rotation = rotation,
            PhysicsMaterialOverride = new PhysicsMaterial
            {
                Friction = 1.0f,
                Bounce = 0.0f,
            },
        };
        body.AddChild(new MeshInstance3D
        {
            Mesh = SurfaceMeshFactory.CreateTiledBox(size),
            MaterialOverride = CreateMaterial(resolvedTexturePath, Colors.White, metallic, roughness, size),
        });
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = size },
        });
        SurfaceDetail.AddBoxWear(body, name, size, resolvedTexturePath);
        AddChild(body);
    }

    private void CreateVisualBox(
        string name,
        Vector3 size,
        Vector3 position,
        Vector3 rotation,
        string texturePath,
        Color tint,
        float metallic,
        float roughness)
    {
        string resolvedTexturePath = RoomGeometry.ResolveSurfaceTexture(name, size, texturePath);
        MeshInstance3D visual = new()
        {
            Name = name,
            Position = position,
            Rotation = rotation,
            Mesh = SurfaceMeshFactory.CreateTiledBox(size),
            MaterialOverride = CreateMaterial(resolvedTexturePath, tint, metallic, roughness, size),
        };
        SurfaceDetail.AddBoxWear(visual, name, size, resolvedTexturePath);
        AddChild(visual);
    }

    private void CreateVisualBox(
        string name,
        Vector3 size,
        Vector3 position,
        Vector3 rotation,
        StandardMaterial3D material)
    {
        AddChild(new MeshInstance3D
        {
            Name = name,
            Position = position,
            Rotation = rotation,
            Mesh = SurfaceMeshFactory.CreateTiledBox(size),
            MaterialOverride = material,
        });
    }

    private void CreateVisualCylinder(
        string name,
        Vector3 position,
        Vector3 rotation,
        float topRadius,
        float height,
        StandardMaterial3D material,
        float? bottomRadius = null)
    {
        AddChild(new MeshInstance3D
        {
            Name = name,
            Position = position,
            Rotation = rotation,
            Mesh = new CylinderMesh
            {
                TopRadius = topRadius,
                BottomRadius = bottomRadius ?? topRadius,
                Height = height,
                RadialSegments = 24,
            },
            MaterialOverride = material,
        });
    }

    private static StandardMaterial3D CreateMaterial(
        string texturePath,
        Color tint,
        float metallic,
        float roughness,
        Vector3? size = null,
        bool emissionEnabled = false,
        Color? emission = null)
    {
        StandardMaterial3D material = new()
        {
            AlbedoTexture = GD.Load<Texture2D>(texturePath),
            AlbedoColor = tint,
            Metallic = Mathf.Min(metallic, 0.5f),
            Roughness = roughness,
            Uv1Scale = Vector3.One,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
            EmissionEnabled = emissionEnabled,
            Emission = emission ?? Colors.Black,
            EmissionEnergyMultiplier = emissionEnabled ? 1.65f : 1.0f,
        };
        return material;
    }
}
