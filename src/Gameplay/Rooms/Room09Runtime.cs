using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Physics;
using Velocitex.Core.Rooms;
using Velocitex.Core.Settings;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Player;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room09Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_09_solution.tres";
    private const string SurfacePath = "res://resources/surfaces/super_elastic.tres";
    private const string MaterialPath = "res://resources/materials/super_elastic_membrane.tres";
    private const string BounceSfxPath = "res://assets/audio/sfx/surface_super_elastic_bounce.wav";
    private const int RequiredSolutionRuns = 10;
    private const int MaximumSolutionTicksPerRun = 1200;

    private readonly List<FlightGate3D> _flightGates = new();
    private readonly List<RouteCheckpoint3D> _sequenceButtons = new();
    private readonly HashSet<ulong> _distinctBounceSurfaceIds = new();
    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private AudioStreamPlayer3D? _bounceAudio;
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private bool _touchedMembraneThisRun;
    private bool _verifiedBounceThisRun;
    private bool _completedDoubleBounceThisRun;
    private bool _runSolutionSmoke;
    private bool _runPreview;
    private bool _runShellSmoke;
    private bool _reducedMotion;
    private bool _solutionSmokeFinishing;
    private int _lastBounceCount;
    private int _solutionRun;
    private int _solutionTick;
    private int _previewFrames;
    private int _shellSmokeTick;
    private int _nextFlightGate;
    private int _nextSequenceButton;
    private float _impactSpeed;
    private float _launchSpeed;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] userArguments = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(userArguments, argument => argument == "--room09-solution-smoke");
        _runPreview = Array.Exists(userArguments, argument => argument == "--room09-preview");
        _runShellSmoke = Array.Exists(userArguments, argument => argument == "--room-shell-smoke");
        bool panoramaCapture = Array.Exists(userArguments, argument => argument.StartsWith("--panorama-capture=", StringComparison.Ordinal));
        _reducedMotion = SettingsStore.Load().ReducedMotion || _runPreview || panoramaCapture;

        BuildRoom();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room09_a", new Vector3(7.1f, 15.2f, 23.0f), new Vector3(0.0f, 13.0f, -27.0f), 56.0f),
            new("room09_b", new Vector3(9.0f, 29.0f, -89.0f), new Vector3(0.0f, 17.0f, -50.0f), 58.0f),
            new("room09_c", new Vector3(-8.0f, 10.5f, -126.0f), new Vector3(0.0f, 18.0f, -63.0f), 57.0f),
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
                _solutionTrace.MoveInputs.Count < 3 ||
                _solutionTrace.MoveDurationsTicks.Length != _solutionTrace.MoveInputs.Count ||
                !_solutionTrace.MoveInputs.Any(input => input.LengthSquared() < 0.0001f))
            {
                string details = _solutionTrace is null
                    ? "trace=null"
                    : $"trace_room='{_solutionTrace.RoomId}', inputs={_solutionTrace.MoveInputs.Count}, durations={_solutionTrace.MoveDurationsTicks.Length}";
                FailSolutionSmoke($"The Room 09 SolutionTrace is invalid ({details}).");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_runPreview && ++_previewFrames >= 30)
        {
            string capturePath = ProjectSettings.GlobalizePath("user://room09-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(capturePath);
            GD.Print($"ROOM09_PREVIEW_CAPTURE: {capturePath}");
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

        TrackSuperElasticBounce();
        if (_player.GlobalPosition.Y < -7.0f)
        {
            RestartRoom();
        }
    }

    public override void RestartRoom()
    {
        if (_runSolutionSmoke && _solutionTick > 0 && !_solutionSmokeFinishing)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} touched the hazard floor at {_player.GlobalPosition} after {_nextFlightGate}/{_flightGates.Count} flight gates; {string.Join(", ", _flightGates.Select((gate, index) => $"gate{index + 1}={gate.LastEntrySpeed:F2}->{gate.LastExitSpeed:F2}"))}.");
            return;
        }

        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _player.ResetTo(_spawnTransform);
        ResetBounceState();
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
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 09 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 09 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void RunSolutionTick()
    {
        if (_solutionTrace is null || _solutionSmokeFinishing)
        {
            return;
        }

        TrackSuperElasticBounce();
        if (IsComplete)
        {
            if (!_touchedMembraneThisRun || !_verifiedBounceThisRun ||
                _distinctBounceSurfaceIds.Count < 2 ||
                !_completedDoubleBounceThisRun ||
                _nextFlightGate != _flightGates.Count ||
                _nextSequenceButton != _sequenceButtons.Count)
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} bypassed the super-elastic sequence; buttons={_nextSequenceButton}/{_sequenceButtons.Count}, distinct_bounces={_distinctBounceSurfaceIds.Count}, consecutive={_player.ConsecutiveElasticBounceCount}, gates={_nextFlightGate}/{_flightGates.Count}, impact={_impactSpeed:F2}, launch={_launchSpeed:F2}.");
                return;
            }

            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print($"ROOM09_SOLUTION_PASS: SolutionTrace used two distinct super-elastic bodies, both centered flight gates and the ordered start sequence for {_solutionRun} consecutive completions; double_bounce={CompletedAdvancementIds.Contains("double-bounce")}.");
                FinishSolutionSmoke(0);
                return;
            }

            ClearCompletionState();
            _player.ResetTo(_spawnTransform);
            _solutionTick = 0;
            ResetBounceState();
            return;
        }

        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} timed out at position {_player.GlobalPosition}; buttons={_nextSequenceButton}/{_sequenceButtons.Count}, surface={_player.GroundSurfaceKind}, gates={_nextFlightGate}/{_flightGates.Count}, distinct_bounces={_distinctBounceSurfaceIds.Count}, bounces={_player.SuperElasticBounceCount}, impact={_impactSpeed:F2}, launch={_launchSpeed:F2}.");
            return;
        }

        _player.SimulatedMoveInput = ResolveTraceInput(_solutionTick - 1);
    }

    private void TrackSuperElasticBounce()
    {
        if (_player.SuperElasticBounceCount <= _lastBounceCount)
        {
            return;
        }

        _lastBounceCount = _player.SuperElasticBounceCount;
        _touchedMembraneThisRun = true;
        _impactSpeed = _player.LastSuperElasticImpactSpeed;
        _launchSpeed = _player.LastSuperElasticLaunchSpeed;
        if (_player.LastElasticBounceSurfaceInstanceId != 0UL)
        {
            _distinctBounceSurfaceIds.Add(_player.LastElasticBounceSurfaceInstanceId);
        }
        _verifiedBounceThisRun |= _impactSpeed >= 7.0f && _launchSpeed >= _impactSpeed * 1.6f;
        if (_distinctBounceSurfaceIds.Count >= 2)
        {
            _completedDoubleBounceThisRun = true;
            MarkAdvancementCondition("double-bounce");
        }
        if (_bounceAudio is not null)
        {
            _bounceAudio.GlobalPosition = _player.GlobalPosition;
            _bounceAudio.PitchScale = 0.96f + ((_lastBounceCount % 3) * 0.035f);
            _bounceAudio.Play();
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

    private void ResetBounceState()
    {
        _touchedMembraneThisRun = false;
        _verifiedBounceThisRun = false;
        _completedDoubleBounceThisRun = false;
        _lastBounceCount = 0;
        _impactSpeed = 0.0f;
        _launchSpeed = 0.0f;
        _distinctBounceSurfaceIds.Clear();
        _nextSequenceButton = 0;
        foreach (RouteCheckpoint3D button in _sequenceButtons)
        {
            button.ResetCheckpoint();
        }
        _bounceAudio?.Stop();
        _nextFlightGate = 0;
        foreach (FlightGate3D gate in _flightGates)
        {
            gate.ResetGate();
        }
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string copper = "res://assets/textures/copper_rivets.svg";
        const string rubber = "res://assets/textures/rubber_chevrons.svg";
        Color paleSteel = new("b0b8b3");
        Color darkFrame = new("393846");
        Color violetFrame = new("685277");
        SurfaceProfile bounceProfile = GD.Load<SurfaceProfile>(SurfacePath);
        ShaderMaterial firstBounceMaterial = (ShaderMaterial)GD.Load<ShaderMaterial>(MaterialPath).Duplicate();
        firstBounceMaterial.SetShaderParameter("motion_scale", _reducedMotion ? 0.0f : 1.0f);
        ShaderMaterial secondBounceMaterial = (ShaderMaterial)firstBounceMaterial.Duplicate();

        RoomGeometry.AddClosedRoomShell(
            this,
            "RoomShell",
            new Vector3(0.0f, 0.0f, -50.0f),
            new Vector2(24.0f, 170.0f),
            -2.8f,
            42.0f,
            metal,
            new Color("7c7882"),
            new Color("4d4653"),
            body =>
            {
                if (body is PlayerBall)
                {
                    RestartRoom();
                }
            });

        RoomGeometry.AddBox(this, "SafeStart", new Vector3(14.0f, 0.5f, 22.0f), new Vector3(0.0f, 11.483f, 24.0f), Vector3.Zero, metal, paleSteel, 0.38f, 0.68f);
        RoomGeometry.AddBox(this, "LaunchLip", new Vector3(14.0f, 0.5f, 3.0f), new Vector3(0.0f, 11.14f, 11.62f), new Vector3(Mathf.DegToRad(-13.5f), 0.0f, 0.0f), rubber, new Color("6a6270"), 0.02f, 0.9f);

        StaticBody3D firstMembrane = RoomGeometry.AddBox(
            this,
            "SuperElasticMembraneA",
            new Vector3(14.0f, 0.5f, 19.0f),
            new Vector3(0.0f, 2.4955f, 0.5055f),
            new Vector3(Mathf.DegToRad(-6.0f), 0.0f, 0.0f),
            string.Empty,
            Colors.White,
            0.0f,
            0.7f,
            friction: bounceProfile.Friction,
            surfaceProfile: bounceProfile,
            materialOverride: firstBounceMaterial);

        // The second membrane is visibly and physically a separate body.  Its
        // raised tower catches the descending first arc without adding random
        // support columns or allowing a route underneath it.
        RoomGeometry.AddBox(this, "SecondMembraneTower", new Vector3(16.0f, 19.25f, 34.0f), new Vector3(0.0f, 6.825f, -49.0f), Vector3.Zero, copper, new Color("4f4758"), 0.42f, 0.62f);
        StaticBody3D secondMembrane = RoomGeometry.AddBox(
            this,
            "SuperElasticMembraneB",
            new Vector3(14.0f, 0.5f, 34.0f),
            new Vector3(0.0f, 16.7f, -49.0f),
            Vector3.Zero,
            string.Empty,
            Colors.White,
            0.0f,
            0.7f,
            friction: bounceProfile.Friction,
            surfaceProfile: bounceProfile,
            materialOverride: secondBounceMaterial);

        RoomGeometry.AddBox(this, "LandingDeck", new Vector3(18.0f, 0.5f, 65.0f), new Vector3(0.0f, 2.6f, -101.0f), Vector3.Zero, metal, paleSteel.Darkened(0.05f), 0.4f, 0.66f);

        RoomGeometry.AddBox(this, "StartRailLeft", new Vector3(0.36f, 1.35f, 22.0f), new Vector3(-7.18f, 12.408f, 24.0f), Vector3.Zero, metal, darkFrame, 0.4f, 0.64f);
        RoomGeometry.AddBox(this, "StartRailRight", new Vector3(0.36f, 1.35f, 22.0f), new Vector3(7.18f, 12.408f, 24.0f), Vector3.Zero, metal, darkFrame, 0.4f, 0.64f);
        Vector3 launchRailRotation = new(Mathf.DegToRad(-13.5f), 0.0f, 0.0f);
        RoomGeometry.AddBox(this, "LaunchRailLeft", new Vector3(0.36f, 1.35f, 3.0f), new Vector3(-7.18f, 12.02f, 11.62f), launchRailRotation, metal, darkFrame, 0.4f, 0.64f);
        RoomGeometry.AddBox(this, "LaunchRailRight", new Vector3(0.36f, 1.35f, 3.0f), new Vector3(7.18f, 12.02f, 11.62f), launchRailRotation, metal, darkFrame, 0.4f, 0.64f);
        AddMembraneRim("FirstMembrane", new Vector3(0.0f, 2.4955f, 0.5055f), 19.0f, new Vector3(Mathf.DegToRad(-6.0f), 0.0f, 0.0f), copper, violetFrame);
        AddMembraneRim("SecondMembrane", new Vector3(0.0f, 16.7f, -49.0f), 34.0f, Vector3.Zero, copper, violetFrame);
        RoomGeometry.AddBox(this, "LandingRailLeft", new Vector3(0.36f, 1.45f, 65.0f), new Vector3(-9.18f, 3.575f, -101.0f), Vector3.Zero, metal, darkFrame, 0.4f, 0.64f);
        RoomGeometry.AddBox(this, "LandingRailRight", new Vector3(0.36f, 1.45f, 65.0f), new Vector3(9.18f, 3.575f, -101.0f), Vector3.Zero, metal, darkFrame, 0.4f, 0.64f);

        AddSequenceButton("StartSequenceLeft", 0, new Vector3(-4.0f, 12.30f, 27.2f));
        AddSequenceButton("FinalSequenceRight", 1, new Vector3(7.0f, 3.42f, -126.0f));

        AddFlightGate("FlightGateFirstArc", 0, new Vector3(0.0f, 20.0f, -29.5f), 3.8f);
        AddFlightGate("FlightGateSecondArc", 1, new Vector3(0.0f, 26.0f, -75.0f), 5.0f);
        AddSecondGateAperture(metal, darkFrame);

        if (!_runSolutionSmoke)
        {
            _bounceAudio = new AudioStreamPlayer3D
            {
                Name = "SuperElasticBounceSfx",
                Stream = GD.Load<AudioStream>(BounceSfxPath),
                Bus = "SFX",
                MaxDistance = 34.0f,
                UnitSize = 7.0f,
            };
            AddChild(_bounceAudio);
        }

        firstMembrane.AddChild(new OmniLight3D
        {
            Name = "MembranePracticalA",
            Position = new Vector3(0.0f, 1.2f, 0.0f),
            LightColor = new Color("9e73bd"),
            LightEnergy = 1.25f,
            OmniRange = 11.0f,
            ShadowEnabled = false,
        });
        secondMembrane.AddChild(new OmniLight3D
        {
            Name = "MembranePracticalB",
            Position = new Vector3(0.0f, 1.2f, 0.0f),
            LightColor = new Color("b58ad0"),
            LightEnergy = 1.15f,
            OmniRange = 10.0f,
            ShadowEnabled = false,
        });
    }

    private void AddMembraneRim(string prefix, Vector3 center, float length, Vector3 rotation, string texture, Color tint)
    {
        RoomGeometry.AddBox(this, $"{prefix}RimLeft", new Vector3(0.46f, 0.72f, length + 0.4f), center + new Vector3(-7.2f, 0.0f, 0.0f), rotation, texture, tint, 0.42f, 0.56f);
        RoomGeometry.AddBox(this, $"{prefix}RimRight", new Vector3(0.46f, 0.72f, length + 0.4f), center + new Vector3(7.2f, 0.0f, 0.0f), rotation, texture, tint, 0.42f, 0.56f);
    }

    private void AddSequenceButton(string name, int index, Vector3 position)
    {
        RouteCheckpoint3D button = new()
        {
            Name = name,
            Position = position,
            CheckpointIndex = index,
            TriggerSize = new Vector3(3.2f, 1.4f, 3.2f),
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
            if (_runSolutionSmoke)
            {
                GD.Print($"ROOM09_SEQUENCE_TRACE: button={_nextSequenceButton}/{_sequenceButtons.Count}, tick={_solutionTick}, position={player.GlobalPosition}.");
            }
        };
        AddChild(button);
        _sequenceButtons.Add(button);

        MeshInstance3D inset = button.GetNode<MeshInstance3D>("InsetPlate");
        RoomGeometry.AddSequencePips(inset, index + 1);
    }

    private void AddFlightGate(string name, int index, Vector3 position, float radius)
    {
        FlightGate3D gate = new()
        {
            Name = name,
            Position = position,
            Radius = radius,
            FrameTint = index == 0 ? new Color("8b6c92") : new Color("6f5b7d"),
            EnableAudio = !_runSolutionSmoke,
            MinimumExitSpeed = 20.0f,
            SpeedGain = index == 0 ? 5.0f : 2.0f,
            SpeedMultiplier = index == 0 ? 1.3f : 1.05f,
            MaximumExitSpeed = index == 0 ? float.PositiveInfinity : 24.0f,
            AxialBoostOnly = index == 1,
            MaximumDownwardExitSpeed = index == 1 ? 1.0f : float.PositiveInfinity,
        };
        gate.Passed += player =>
        {
            if (player == _player && index == _nextFlightGate)
            {
                _nextFlightGate++;
                if (_runSolutionSmoke)
                {
                    GD.Print($"ROOM09_GATE_TRACE: gate={index + 1}, tick={_solutionTick}, position={_player.GlobalPosition}, velocity={_player.LinearVelocity}.");
                }
            }
        };
        AddChild(gate);
        _flightGates.Add(gate);
    }

    private void AddSecondGateAperture(string texture, Color tint)
    {
        const float wallZ = -75.0f;
        const float wallThickness = 0.6f;
        const float roomHalfWidth = 9.0f;
        const float openingHalfWidth = 5.35f;
        const float openingBottom = 20.65f;
        const float openingTop = 31.35f;
        const float floorY = 2.85f;
        const float ceilingY = 42.0f;

        RoomGeometry.AddBox(this, "SecondGateApertureLower", new Vector3(18.0f, openingBottom - floorY, wallThickness), new Vector3(0.0f, (floorY + openingBottom) * 0.5f, wallZ), Vector3.Zero, texture, tint, 0.42f, 0.64f);
        RoomGeometry.AddBox(this, "SecondGateApertureUpper", new Vector3(18.0f, ceilingY - openingTop, wallThickness), new Vector3(0.0f, (openingTop + ceilingY) * 0.5f, wallZ), Vector3.Zero, texture, tint, 0.42f, 0.64f);

        float sideWidth = roomHalfWidth - openingHalfWidth;
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(
                this,
                side < 0.0f ? "SecondGateApertureLeft" : "SecondGateApertureRight",
                new Vector3(sideWidth, openingTop - openingBottom, wallThickness),
                new Vector3(side * (openingHalfWidth + (sideWidth * 0.5f)), (openingBottom + openingTop) * 0.5f, wallZ),
                Vector3.Zero,
                texture,
                tint,
                0.42f,
                0.64f);
        }
    }

    private void BuildGoal()
    {
        Vector3 goalPosition = new(0.0f, 3.75f, -128.4f);
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
            TrackSuperElasticBounce();
            if (body is PlayerBall &&
                _nextSequenceButton == _sequenceButtons.Count &&
                _touchedMembraneThisRun &&
                _verifiedBounceThisRun &&
                _distinctBounceSurfaceIds.Count >= 2 &&
                _completedDoubleBounceThisRun &&
                _nextFlightGate == _flightGates.Count)
            {
                MarkAdvancementCondition("double-bounce");
                CompleteRoom();
            }
        };
        AddChild(goal);

        RoomGeometry.AddGoalExitDoor(this, goalPosition);
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM09_SOLUTION_FAIL: {message}");
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

        if (_bounceAudio is not null)
        {
            _bounceAudio.Stop();
            _bounceAudio.Stream = null;
        }

        foreach (FlightGate3D gate in _flightGates)
        {
            gate.ResetGate();
            gate.QueueFree();
        }
        _flightGates.Clear();

        _solutionTrace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GetTree().Quit(exitCode);
    }
}
