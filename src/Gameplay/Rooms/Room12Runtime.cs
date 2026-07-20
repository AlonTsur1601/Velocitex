using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Physics;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Physics;
using Velocitex.Gameplay.Player;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room12Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_12_solution.tres";
    private const string ElasticSurfacePath = "res://resources/surfaces/super_elastic.tres";
    private const string ElasticMaterialPath = "res://resources/materials/super_elastic_membrane.tres";
    private const int RequiredSolutionRuns = 10;
    private const int MaximumSolutionTicksPerRun = 1100;

    private readonly List<RouteCheckpoint3D> _sequencePads = new();
    private readonly List<FlightGate3D> _transferRings = new();
    private readonly HashSet<ulong> _verifiedElasticSurfaces = new();
    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private ForceVolume3D _strongGravityVolume = null!;
    private ForceVolume3D _secondStrongGravityVolume = null!;
    private ForceVolume3D _lowGravityTransfer = null!;
    private StaticBody3D _dropBarrier = null!;
    private CollisionShape3D _dropBarrierCollision = null!;
    private AudioStreamPlayer3D? _gravityAudio;
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private bool _touchedStrongGravity;
    private bool _trackingDrop;
    private bool _verifiedStrongGravity;
    private bool _verifiedElasticLaunch;
    private bool _runSolutionSmoke;
    private bool _runMechanicsSmoke;
    private bool _runPreview;
    private bool _runShellSmoke;
    private bool _solutionSmokeFinishing;
    private int _dropTicks;
    private int _lastBounceCount;
    private int _nextTransferRing;
    private int _nextSequencePad;
    private int _wrongOrderCount;
    private int _solutionRun;
    private int _solutionTick;
    private int _previewFrames;
    private int _shellSmokeTick;
    private float _dropStartVelocityY;
    private float _minimumDropVelocityY;
    private float _measuredVelocityGain;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] arguments = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(arguments, value => value == "--room12-solution-smoke");
        _runMechanicsSmoke = Array.Exists(arguments, value => value == "--room12-mechanics-smoke");
        _runPreview = Array.Exists(arguments, value => value == "--room12-preview");
        _runShellSmoke = Array.Exists(arguments, value => value == "--room-shell-smoke");

        BuildRoom();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room12_a", new Vector3(10.2f, 27.0f, 31.0f), new Vector3(0.0f, 11.0f, -10.0f), 58.0f),
            new("room12_b", new Vector3(-10.0f, 21.0f, -31.0f), new Vector3(0.0f, 9.0f, -5.0f), 58.0f),
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

        _strongGravityVolume.RigidBodyEntered += OnStrongGravityEntered;
        _secondStrongGravityVolume.RigidBodyEntered += OnStrongGravityEntered;

        if (_runSolutionSmoke)
        {
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            if (_solutionTrace is null ||
                _solutionTrace.RoomId != RoomId ||
                _solutionTrace.MoveInputs.Count < 4 ||
                _solutionTrace.MoveDurationsTicks.Length != _solutionTrace.MoveInputs.Count ||
                !_solutionTrace.MoveInputs.Any(input => input.X < -0.25f) ||
                !_solutionTrace.MoveInputs.Any(input => input.X > 0.25f))
            {
                FailSolutionSmoke("The SolutionTrace must contain a deliberate left-right arming route before the central drop.");
            }
        }

        if (_runMechanicsSmoke)
        {
            CallDeferred(MethodName.RunMechanicsSmoke);
        }
    }

    public override void _Process(double delta)
    {
        if (_runPreview && ++_previewFrames >= 30)
        {
            string capturePath = ProjectSettings.GlobalizePath("user://room12-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(capturePath);
            GD.Print($"ROOM12_PREVIEW_CAPTURE: {capturePath}");
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

        if (_runMechanicsSmoke)
        {
            return;
        }

        if (_runSolutionSmoke)
        {
            RunSolutionTick();
            return;
        }

        TrackStrongGravityDropAndBounce();
        if (_player.GlobalPosition.Y < -7.0f)
        {
            RestartRoom();
        }
    }

    public override void RestartRoom()
    {
        if (_runSolutionSmoke && _solutionTick > 0 && !_solutionSmokeFinishing)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} fell to the maintenance floor at {_player.GlobalPosition}; pads={_nextSequencePad}/2, gravity={_verifiedStrongGravity}, elastic={_verifiedElasticLaunch}.");
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
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 12 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 12 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void RunSolutionTick()
    {
        if (_solutionTrace is null || _solutionSmokeFinishing)
        {
            return;
        }

        TrackStrongGravityDropAndBounce();
        if (IsComplete)
        {
            if (!CanCompleteRoute())
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} bypassed the puzzle; pads={_nextSequencePad}/2, gravity={_verifiedStrongGravity}, elastic={_verifiedElasticLaunch}, rings={_nextTransferRing}/{_transferRings.Count}, surfaces={_verifiedElasticSurfaces.Count}, gain={_measuredVelocityGain:F2}.");
                return;
            }

            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print($"ROOM12_SOLUTION_PASS: SolutionTrace armed both ordered pads, completed two strong-gravity elastic drops and crossed both low-gravity acceleration rings for {_solutionRun} consecutive completions; gain={_measuredVelocityGain:F2}.");
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
            FailSolutionSmoke($"Run {_solutionRun + 1} timed out at {_player.GlobalPosition}; pads={_nextSequencePad}/2, gravity={_verifiedStrongGravity}, elastic={_verifiedElasticLaunch}, rings={_nextTransferRing}/{_transferRings.Count}, surfaces={_verifiedElasticSurfaces.Count}, bounce_count={_player.SuperElasticBounceCount}.");
            return;
        }

        _player.SimulatedMoveInput = ResolveTraceInput(_solutionTick - 1);
    }

    private async void RunMechanicsSmoke()
    {
        if (_sequencePads.Count != 2 || _transferRings.Count != 2 || _dropBarrierCollision.Disabled)
        {
            FailMechanicsSmoke("room did not create two sequence pads, two low-gravity acceleration rings and a closed physical drop barrier.");
            return;
        }

        _sequencePads[1].Press(_player);
        if (_nextSequencePad != 0 || _dropBarrierCollision.Disabled || _wrongOrderCount != 1)
        {
            FailMechanicsSmoke("wrong-order pad entry advanced the puzzle or failed to record negative feedback.");
            return;
        }

        _sequencePads[0].Press(_player);
        _sequencePads[1].Press(_player);
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (_nextSequencePad != 2 || !_dropBarrierCollision.Disabled)
        {
            FailMechanicsSmoke("correct pad order did not open the physical drop barrier.");
            return;
        }

        _touchedStrongGravity = true;
        _verifiedStrongGravity = true;
        _verifiedElasticLaunch = false;
        if (CanCompleteRoute())
        {
            FailMechanicsSmoke("strong gravity without the elastic launch incorrectly satisfied completion.");
            return;
        }

        _verifiedStrongGravity = false;
        _verifiedElasticLaunch = true;
        if (CanCompleteRoute())
        {
            FailMechanicsSmoke("elastic contact without a measured strong-gravity drop incorrectly satisfied completion.");
            return;
        }

        _verifiedStrongGravity = true;
        _nextTransferRing = _transferRings.Count;
        _verifiedElasticSurfaces.Add(101UL);
        _verifiedElasticSurfaces.Add(202UL);
        if (!CanCompleteRoute())
        {
            FailMechanicsSmoke("the complete ordered-pad, strong-gravity and elastic state was rejected.");
            return;
        }

        GD.Print("ROOM12_MECHANICS_PASS: wrong order stayed closed; both positive prerequisites and both negative missing-mechanic cases behaved correctly.");
        FinishMechanicsSmoke(0);
    }

    private void TrackStrongGravityDropAndBounce()
    {
        if (_verifiedStrongGravity && _verifiedElasticLaunch)
        {
            return;
        }

        bool inside = _strongGravityVolume.ContainsBody(_player) || _secondStrongGravityVolume.ContainsBody(_player);
        if (!_trackingDrop && inside && !_player.IsGrounded && _player.GlobalPosition.Y < 20.0f)
        {
            _trackingDrop = true;
            _dropTicks = 0;
            _dropStartVelocityY = _player.LinearVelocity.Y;
            _minimumDropVelocityY = _dropStartVelocityY;
            _measuredVelocityGain = 0.0f;
        }

        if (_trackingDrop && !_player.IsGrounded)
        {
            _dropTicks++;
            _minimumDropVelocityY = Mathf.Min(_minimumDropVelocityY, _player.LinearVelocity.Y);
            _measuredVelocityGain = _dropStartVelocityY - _minimumDropVelocityY;
        }

        if (_player.SuperElasticBounceCount <= _lastBounceCount)
        {
            return;
        }

        _lastBounceCount = _player.SuperElasticBounceCount;
        bool validGravityDrop = _trackingDrop && _touchedStrongGravity && _dropTicks >= 24 && _minimumDropVelocityY <= -14.0f && _measuredVelocityGain >= 9.0f;
        bool validElasticLaunch = validGravityDrop &&
            _player.LastSuperElasticImpactSpeed >= 12.0f &&
            _player.LastSuperElasticLaunchSpeed >= _player.LastSuperElasticImpactSpeed * 1.6f;
        if (validElasticLaunch && _player.LastElasticBounceSurfaceInstanceId != 0UL)
        {
            _verifiedElasticSurfaces.Add(_player.LastElasticBounceSurfaceInstanceId);
        }
        _verifiedStrongGravity = _verifiedElasticSurfaces.Count >= 1;
        _verifiedElasticLaunch = _verifiedElasticSurfaces.Count >= 2;
        _trackingDrop = false;
        if (_runSolutionSmoke)
        {
            GD.Print($"ROOM12_DROP_TRACE: ticks={_dropTicks}, minimum_y={_minimumDropVelocityY:F2}, gain={_measuredVelocityGain:F2}, impact={_player.LastSuperElasticImpactSpeed:F2}, launch={_player.LastSuperElasticLaunchSpeed:F2}, verified={_verifiedStrongGravity && _verifiedElasticLaunch}.");
        }
    }

    private bool CanCompleteRoute() =>
        _nextSequencePad == _sequencePads.Count &&
        _touchedStrongGravity &&
        _verifiedStrongGravity &&
        _verifiedElasticLaunch &&
        _verifiedElasticSurfaces.Count >= 2 &&
        _nextTransferRing == _transferRings.Count;

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
        _touchedStrongGravity = false;
        _trackingDrop = false;
        _verifiedStrongGravity = false;
        _verifiedElasticLaunch = false;
        _dropTicks = 0;
        _lastBounceCount = 0;
        _nextTransferRing = 0;
        _nextSequencePad = 0;
        _wrongOrderCount = 0;
        _dropStartVelocityY = 0.0f;
        _minimumDropVelocityY = 0.0f;
        _measuredVelocityGain = 0.0f;
        _verifiedElasticSurfaces.Clear();
        foreach (RouteCheckpoint3D pad in _sequencePads)
        {
            pad.ResetCheckpoint();
        }
        foreach (FlightGate3D ring in _transferRings)
        {
            ring.ResetGate();
        }
        SetDropBarrierOpen(false);
        _gravityAudio?.Stop();
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string copper = "res://assets/textures/copper_rivets.svg";
        Color paleSteel = new("b8b0a8");
        Color darkFrame = new("554a48");

        RoomGeometry.AddClosedRoomShell(
            this,
            "RoomShell",
            new Vector3(0.0f, 0.0f, -45.0f),
            new Vector2(26.0f, 190.0f),
            -3.0f,
            36.0f,
            metal,
            new Color("817570"),
            new Color("4d4545"),
            body => { if (body is PlayerBall) RestartRoom(); });

        RoomGeometry.AddBox(this, "SafeStart", new Vector3(12.0f, 0.5f, 41.0f), new Vector3(0.0f, 22.0f, 29.5f), Vector3.Zero, metal, paleSteel, 0.4f, 0.66f);
        RoomGeometry.AddBox(this, "StartSideWallLeft", new Vector3(0.36f, 1.5f, 41.0f), new Vector3(-6.18f, 22.75f, 29.5f), Vector3.Zero, copper, darkFrame, 0.42f, 0.6f);
        RoomGeometry.AddBox(this, "StartSideWallRight", new Vector3(0.36f, 1.5f, 41.0f), new Vector3(6.18f, 22.75f, 29.5f), Vector3.Zero, copper, darkFrame, 0.42f, 0.6f);

        AddSequencePad("ArmingPadOne", 0, new Vector3(-4.0f, 22.95f, 29.0f), new Color("b7845f"));
        AddSequencePad("ArmingPadTwo", 1, new Vector3(4.0f, 22.95f, 19.0f), new Color("d0a36d"));

        _dropBarrier = RoomGeometry.AddBox(this, "DropBarrier", new Vector3(12.0f, 3.0f, 0.55f), new Vector3(0.0f, 23.75f, 8.75f), Vector3.Zero, copper, new Color("6f5148"), 0.4f, 0.58f);
        _dropBarrierCollision = _dropBarrier.GetChildren().OfType<CollisionShape3D>().First();
        AddBarrierStatusLights();

        SurfaceProfile elasticProfile = GD.Load<SurfaceProfile>(ElasticSurfacePath);
        ShaderMaterial elasticMaterial = (ShaderMaterial)GD.Load<ShaderMaterial>(ElasticMaterialPath).Duplicate();
        ShaderMaterial secondElasticMaterial = (ShaderMaterial)elasticMaterial.Duplicate();
        RoomGeometry.AddBox(
            this,
            "GravityElasticMembrane",
            new Vector3(14.0f, 0.5f, 25.0f),
            new Vector3(0.0f, 2.0f, -7.0f),
            Vector3.Zero,
            string.Empty,
            Colors.White,
            0.0f,
            0.7f,
            friction: elasticProfile.Friction,
            surfaceProfile: elasticProfile,
            materialOverride: elasticMaterial);

        RoomGeometry.AddBox(
            this,
            "SecondGravityElasticMembrane",
            new Vector3(14.0f, 0.5f, 16.0f),
            new Vector3(0.0f, 2.0f, -60.0f),
            Vector3.Zero,
            string.Empty,
            Colors.White,
            0.0f,
            0.7f,
            friction: elasticProfile.Friction,
            surfaceProfile: elasticProfile,
            materialOverride: secondElasticMaterial);

        RoomGeometry.AddBox(this, "ExitDeck", new Vector3(18.0f, 0.5f, 72.5f), new Vector3(0.0f, 4.0f, -103.25f), Vector3.Zero, metal, paleSteel.Darkened(0.04f), 0.4f, 0.66f);
        RoomGeometry.AddBox(this, "ExitSideWallLeft", new Vector3(0.36f, 1.5f, 72.5f), new Vector3(-9.18f, 4.75f, -103.25f), Vector3.Zero, copper, darkFrame, 0.42f, 0.6f);
        RoomGeometry.AddBox(this, "ExitSideWallRight", new Vector3(0.36f, 1.5f, 72.5f), new Vector3(9.18f, 4.75f, -103.25f), Vector3.Zero, copper, darkFrame, 0.42f, 0.6f);

        _strongGravityVolume = new ForceVolume3D
        {
            Name = "StrongGravityVolume",
            Position = new Vector3(0.0f, 12.0f, -2.0f),
            CollisionMask = 1,
            Profile = GD.Load<ForceVolumeProfile>("res://resources/force_volumes/strong_gravity.tres"),
        };
        _strongGravityVolume.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(16.0f, 28.0f, 25.0f) } });
        AddChild(_strongGravityVolume);
        AddGravityParticles();

        _lowGravityTransfer = new ForceVolume3D
        {
            Name = "LowGravityRingTransfer",
            Position = new Vector3(0.0f, 24.0f, -25.5f),
            CollisionMask = 1,
            Profile = GD.Load<ForceVolumeProfile>("res://resources/force_volumes/low_gravity.tres"),
        };
        _lowGravityTransfer.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(22.0f, 24.0f, 27.0f) } });
        AddChild(_lowGravityTransfer);
        AddTransferRing("LowGravityBoostRingOne", 0, new Vector3(0.0f, 26.0f, -24.0f));
        AddTransferRing("LowGravityBoostRingTwo", 1, new Vector3(0.0f, 20.0f, -36.0f));

        _secondStrongGravityVolume = new ForceVolume3D
        {
            Name = "SecondStrongGravityVolume",
            Position = new Vector3(0.0f, 19.0f, -53.0f),
            CollisionMask = 1,
            Profile = GD.Load<ForceVolumeProfile>("res://resources/force_volumes/strong_gravity.tres"),
        };
        _secondStrongGravityVolume.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(16.0f, 34.0f, 34.0f) } });
        AddChild(_secondStrongGravityVolume);

        if (!_runSolutionSmoke && !_runMechanicsSmoke && !_runShellSmoke)
        {
            _gravityAudio = new AudioStreamPlayer3D
            {
                Name = "StrongGravityEnterSfx",
                Stream = GD.Load<AudioStream>("res://assets/audio/sfx/force_strong_gravity_enter.wav"),
                Bus = "SFX",
                MaxDistance = 36.0f,
                UnitSize = 8.0f,
            };
            AddChild(_gravityAudio);
        }
    }

    private void AddSequencePad(string name, int index, Vector3 position, Color tint)
    {
        RouteCheckpoint3D pad = new()
        {
            Name = name,
            Position = position,
            CheckpointIndex = index,
            TriggerSize = new Vector3(3.6f, 1.6f, 3.6f),
            FrameTint = RoomGeometry.SequenceButtonFrameTint,
            FlatFloorMarker = true,
        };
        pad.Entered += (entered, player) =>
        {
            if (player != _player)
            {
                return;
            }
            if (entered.CheckpointIndex != _nextSequencePad)
            {
                _wrongOrderCount++;
                entered.FlashDenied();
                return;
            }
            entered.Activate();
            _nextSequencePad++;
            if (_nextSequencePad == 2)
            {
                SetDropBarrierOpen(true);
            }
            if (_runSolutionSmoke)
            {
                GD.Print($"ROOM12_PAD_TRACE: pad={_nextSequencePad}/2, tick={_solutionTick}, position={player.GlobalPosition}.");
            }
        };
        AddChild(pad);
        MeshInstance3D inset = pad.GetNode<MeshInstance3D>("InsetPlate");
        RoomGeometry.AddSequencePips(inset, index + 1);
        _sequencePads.Add(pad);
    }

    private void AddBarrierStatusLights()
    {
        StandardMaterial3D lightMaterial = RoomGeometry.CreateMaterial("res://assets/textures/sugar_glaze.svg", new Color("d7a35f"), 0.12f, 0.5f, emissionEnabled: true, emission: new Color("9f6232"));
        for (int index = 0; index < 2; index++)
        {
            RoomGeometry.AddVisualBox(_dropBarrier, $"ArmingLamp{index + 1}", new Vector3(1.0f, 0.3f, 0.12f), new Vector3((index == 0 ? -1.1f : 1.1f), 0.65f, -0.34f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, lightMaterial);
        }
    }

    private void SetDropBarrierOpen(bool open)
    {
        if (_dropBarrierCollision is null)
        {
            return;
        }
        _dropBarrierCollision.SetDeferred(CollisionShape3D.PropertyName.Disabled, open);
        _dropBarrier.Position = new Vector3(0.0f, open ? 19.0f : 23.75f, 8.75f);
    }

    private void AddGravityParticles()
    {
        StandardMaterial3D material = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = Colors.White,
            AlbedoTexture = GD.Load<Texture2D>("res://assets/textures/high_gravity_arrow.svg"),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            EmissionEnabled = true,
            Emission = new Color("c58c4f"),
            EmissionEnergyMultiplier = 1.35f,
        };
        ParticleProcessMaterial process = new()
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(7.0f, 12.0f, 10.5f),
            Direction = Vector3.Down,
            Spread = 5.0f,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 3.0f,
            InitialVelocityMax = 5.5f,
            ScaleMin = 0.35f,
            ScaleMax = 0.9f,
        };
        AddChild(new GpuParticles3D
        {
            Name = "DownwardGravityArrows",
            Position = new Vector3(0.0f, 12.0f, -2.0f),
            Amount = 48,
            Lifetime = 2.6,
            Randomness = 0.65f,
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh { Size = new Vector2(0.65f, 0.95f), Material = material },
        });
    }

    private void AddTransferRing(string name, int index, Vector3 position)
    {
        FlightGate3D ring = new()
        {
            Name = name,
            Position = position,
            Radius = index == 0 ? 6.5f : 7.5f,
            EnableAudio = !_runSolutionSmoke && !_runMechanicsSmoke,
            MinimumExitSpeed = 18.0f,
            SpeedGain = 3.5f,
            SpeedMultiplier = 1.04f,
            MaximumExitSpeed = 28.0f,
            AxialBoostOnly = true,
            MaximumDownwardExitSpeed = 6.0f,
        };
        ring.Passed += player =>
        {
            if (player == _player && index == _nextTransferRing)
            {
                _nextTransferRing++;
                if (_runSolutionSmoke)
                {
                    GD.Print($"ROOM12_RING_TRACE: ring={_nextTransferRing}/{_transferRings.Count}, tick={_solutionTick}, position={player.GlobalPosition}, velocity={player.LinearVelocity}.");
                }
            }
        };
        AddChild(ring);
        _transferRings.Add(ring);
    }

    private void BuildGoal()
    {
        Vector3 goalPosition = new(0.0f, 5.35f, -136.5f);
        Area3D goal = new() { Name = "GoalCup", Position = goalPosition, CollisionLayer = 0, CollisionMask = 1, Monitoring = true };
        goal.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 1.8f, Height = 2.7f } });
        goal.BodyEntered += body => { if (body is PlayerBall && CanCompleteRoute()) CompleteRoom(); };
        AddChild(goal);
        RoomGeometry.AddGoalExitDoor(this, goalPosition);
    }

    private void OnStrongGravityEntered(RigidBody3D body)
    {
        if (body != _player)
        {
            return;
        }
        _touchedStrongGravity = true;
        if (_gravityAudio is not null)
        {
            _gravityAudio.GlobalPosition = _player.GlobalPosition;
            _gravityAudio.Play();
        }
    }

    private void FailMechanicsSmoke(string message)
    {
        GD.PushError($"ROOM12_MECHANICS_FAIL: {message}");
        FinishMechanicsSmoke(1);
    }

    private async void FinishMechanicsSmoke(int exitCode)
    {
        SceneTree tree = GetTree();
        _player.SimulatedMoveInput = null;
        _gravityAudio?.Stop();
        QueueFree();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        tree.Quit(exitCode);
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM12_SOLUTION_FAIL: {message}");
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
        _gravityAudio?.Stop();
        _solutionTrace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(exitCode);
    }

    public override void _ExitTree()
    {
        if (_strongGravityVolume is not null)
        {
            _strongGravityVolume.RigidBodyEntered -= OnStrongGravityEntered;
        }
        if (_secondStrongGravityVolume is not null)
        {
            _secondStrongGravityVolume.RigidBodyEntered -= OnStrongGravityEntered;
        }
        if (_gravityAudio is not null)
        {
            _gravityAudio.Stop();
            _gravityAudio.Stream = null;
        }
        _solutionTrace = null;
    }
}
