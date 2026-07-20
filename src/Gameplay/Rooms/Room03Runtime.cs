using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Visual;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room03Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_03_solution.tres";
    private const int RequiredSolutionRuns = 10;
    private const int MaximumSolutionTicksPerRun = 2100;
    private const float GapExtension = 3.5f;
    private const float ThirdGapExtension = 6.0f;

    private readonly List<FlightGate3D> _flightGates = new();
    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private bool _runSolutionSmoke;
    private bool _runPreview;
    private bool _runShellSmoke;
    private bool _solutionFinishing;
    private int _nextFlightGate;
    private int _solutionRun;
    private int _solutionTick;
    private int _solutionWarmupTicks = 6;
    private int _previewFrames;
    private int _shellSmokeTick;
    private float[] _closestGateRadialDistances = Array.Empty<float>();
    private Vector3[] _closestGatePositions = Array.Empty<Vector3>();

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] userArguments = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(userArguments, argument => argument == "--room03-solution-smoke");
        _runPreview = Array.Exists(userArguments, argument => argument == "--room03-preview");
        _runShellSmoke = Array.Exists(userArguments, argument => argument == "--room-shell-smoke");
        BuildRoom();
        BuildGoal();
        _closestGateRadialDistances = new float[_flightGates.Count];
        _closestGatePositions = new Vector3[_flightGates.Count];
        ResetGateDiagnostics();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room03_a", new Vector3(9.5f, 24.0f, 38.0f), new Vector3(0.0f, 7.5f, -34.0f), 59.0f),
            new("room03_b", new Vector3(-9.8f, 15.0f, -38.0f), new Vector3(0.0f, 2.0f, -78.0f), 58.0f),
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
            _player.Freeze = true;
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            if (_solutionTrace is null ||
                _solutionTrace.RoomId != RoomId ||
                _solutionTrace.MoveInputs.Count == 0 ||
                _solutionTrace.MoveDurationsTicks.Length != _solutionTrace.MoveInputs.Count)
            {
                string details = _solutionTrace is null
                    ? "trace=null"
                    : $"trace_room='{_solutionTrace.RoomId}', runtime_room='{RoomId}', inputs={_solutionTrace.MoveInputs.Count}, durations={_solutionTrace.MoveDurationsTicks.Length}";
                FailSolutionSmoke($"The Room 03 SolutionTrace is invalid ({details}).");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_runPreview && ++_previewFrames >= 30)
        {
            string capturePath = ProjectSettings.GlobalizePath("user://room03-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(capturePath);
            GD.Print($"ROOM03_PREVIEW_CAPTURE: {capturePath}");
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

        if (_player.GlobalPosition.Y < -10.0f)
        {
            RestartRoom();
        }
    }

    public override void RestartRoom()
    {
        if (_runSolutionSmoke && _solutionTick > 0)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} touched the hazard floor at {_player.GlobalPosition} after {_nextFlightGate}/{_flightGates.Count} gates. {DescribeGateDiagnostics()}");
            return;
        }

        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _player.ResetTo(_spawnTransform);
        ResetFlightGates();
    }

    private void ResetFlightGates()
    {
        _nextFlightGate = 0;
        ResetGateDiagnostics();
        foreach (FlightGate3D gate in _flightGates)
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
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 03 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 03 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void RunSolutionTick()
    {
        if (_solutionTrace is null)
        {
            return;
        }

        if (_solutionWarmupTicks > 0)
        {
            _player.SimulatedMoveInput = null;
            if (_solutionWarmupTicks == 3)
            {
                _player.Freeze = false;
            }
            _solutionWarmupTicks--;
            return;
        }

        TrackGateDiagnostics();

        if (IsComplete)
        {
            if (_nextFlightGate != _flightGates.Count)
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} reached the cup after only {_nextFlightGate}/{_flightGates.Count} flight gates.");
                return;
            }

            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print($"ROOM03_SOLUTION_PASS: SolutionTrace activated all {_flightGates.Count} flight gates and completed Room 03 {_solutionRun} consecutive times.");
                FinishSolutionSmoke(0);
                return;
            }

            ClearCompletionState();
            _player.Freeze = true;
            _player.ResetTo(_spawnTransform);
            _solutionTick = 0;
            _solutionWarmupTicks = 6;
            ResetFlightGates();
            return;
        }

        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} timed out at {_player.GlobalPosition}, velocity={_player.LinearVelocity}, input={ResolveTraceInput(_solutionTick - 1)}, gates={_nextFlightGate}/{_flightGates.Count}. {DescribeGateDiagnostics()}");
            return;
        }

        _player.SimulatedMoveInput = ResolveTraceInput(_solutionTick - 1);
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

    private void ResetGateDiagnostics()
    {
        for (int index = 0; index < _closestGateRadialDistances.Length; index++)
        {
            _closestGateRadialDistances[index] = float.PositiveInfinity;
            _closestGatePositions[index] = Vector3.Zero;
        }
    }

    private void TrackGateDiagnostics()
    {
        for (int index = 0; index < _flightGates.Count; index++)
        {
            Vector3 localPosition = _flightGates[index].ToLocal(_player.GlobalPosition);
            if (Mathf.Abs(localPosition.Z) > 1.2f)
            {
                continue;
            }

            float radialDistance = new Vector2(localPosition.X, localPosition.Y).Length();
            if (radialDistance < _closestGateRadialDistances[index])
            {
                _closestGateRadialDistances[index] = radialDistance;
                _closestGatePositions[index] = _player.GlobalPosition;
            }
        }
    }

    private string DescribeGateDiagnostics()
    {
        List<string> entries = new();
        for (int index = 0; index < _flightGates.Count; index++)
        {
            float distance = _closestGateRadialDistances[index];
            entries.Add(float.IsPositiveInfinity(distance)
                ? $"gate{index + 1}=no plane crossing"
                : $"gate{index + 1}=radial {distance:F2} at {_closestGatePositions[index]} (required center <= {_flightGates[index].TriggerRadius:F2})");
        }
        return string.Join("; ", entries);
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string caramel = "res://assets/textures/caramel_plates.svg";
        Color paleSteel = new("9eaaa8");

        RoomGeometry.AddClosedRoomShell(
            this,
            "RoomShell",
            new Vector3(0.0f, 0.0f, -39.0f - (ThirdGapExtension * 0.5f)),
            new Vector2(26.0f, 159.0f + ThirdGapExtension),
            -6.0f,
            24.0f,
            metal,
            new Color("68706d"),
            new Color("794936"),
            body =>
            {
                if (body is PlayerBall)
                {
                    RestartRoom();
                }
            });

        // Extend only the rear of the start deck to the inside face of the
        // shell. Its front edge remains flush with LaunchSlope1.
        AddCourseBox("SafeStart", new Vector3(17.0f, 0.5f, 8.588f), new Vector3(0.0f, 17.948910f, 35.981f), Vector3.Zero, metal, paleSteel);
        Color startWallTint = new("626d6b");
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(this, $"StartSideWall{side}", new Vector3(0.34f, 1.2f, 8.588f), new Vector3(side * 8.67f, 18.82f, 35.981f), Vector3.Zero, metal, startWallTint, 0.44f, 0.64f);
        }
        AddCourseBox("LaunchSlope1", new Vector3(17.0f, 0.5f, 17.0f), new Vector3(0.0f, 15.9f, 23.5f), new Vector3(Mathf.DegToRad(-14.0f), 0.0f, 0.0f), caramel, new Color("b67b50"));
        AddCourseBox("LaunchLip1", new Vector3(17.0f, 0.32f, 2.092006f), new Vector3(0.0f, 13.926f, 14.146003f), Vector3.Zero, caramel, new Color("b67b50"));
        AddCourseBox("Landing1", new Vector3(17.0f, 0.6f, 9.313f), new Vector3(0.0f, 13.0f, 6.3435f - GapExtension), Vector3.Zero, metal, paleSteel);

        AddCourseBox("LaunchSlope2", new Vector3(17.0f, 0.5f, 17.0f), new Vector3(0.0f, 11.001f, -6.5f - GapExtension), new Vector3(Mathf.DegToRad(-14.0f), 0.0f, 0.0f), caramel, new Color("ad744d"));
        AddCourseBox("LaunchLip2", new Vector3(17.0f, 0.32f, 2.092006f), new Vector3(0.0f, 9.027f, -15.853997f - GapExtension), Vector3.Zero, caramel, new Color("ad744d"));
        AddCourseBox("Landing2", new Vector3(17.0f, 0.6f, 10.7f), new Vector3(3.4f, 8.0f, -24.0f - (GapExtension * 2.0f)), Vector3.Zero, metal, new Color("93a09e"));

        AddCourseBox("LaunchSlope3", new Vector3(17.0f, 0.5f, 17.0f), new Vector3(3.4f, 6.001f, -37.537033f - (GapExtension * 2.0f)), new Vector3(Mathf.DegToRad(-14.0f), 0.0f, 0.0f), caramel, new Color("a66c48"));
        AddCourseBox("LaunchLip3", new Vector3(17.0f, 0.32f, 2.054973f), new Vector3(3.4f, 4.027f, -46.872514f - (GapExtension * 2.0f)), Vector3.Zero, caramel, new Color("a66c48"));
        // Keep the far edge joined to LaunchSlope4, but bring the near edge
        // towards FlightGate3.  The old landing began almost nine metres past
        // the ring, so a valid centre crossing could still drop into the
        // hazard before the player had any grounded control again.
        AddCourseBox("Landing3", new Vector3(17.0f, 0.6f, 15.0f), new Vector3(3.4f, 3.0f, -52.85f - (GapExtension * 3.0f) - ThirdGapExtension), Vector3.Zero, metal, new Color("8d9a99"));

        AddCourseBox("LaunchSlope4", new Vector3(17.0f, 0.5f, 17.0f), new Vector3(3.4f, 1.001f, -68.537033f - (GapExtension * 3.0f) - ThirdGapExtension), new Vector3(Mathf.DegToRad(-14.0f), 0.0f, 0.0f), caramel, new Color("9c6545"));
        AddCourseBox("LaunchLip4", new Vector3(17.0f, 0.32f, 2.054973f), new Vector3(3.4f, -0.973f, -77.872514f - (GapExtension * 3.0f) - ThirdGapExtension), Vector3.Zero, caramel, new Color("9c6545"));
        AddCourseBox("Landing4", new Vector3(17.0f, 0.6f, 10.0f), new Vector3(-3.4f, -2.0f, -86.0f - (GapExtension * 4.0f) - ThirdGapExtension), Vector3.Zero, metal, new Color("879392"));
        AddCourseBox("ExitRunout", new Vector3(17.0f, 0.6f, 12.0f), new Vector3(-3.4f, -2.0f, -97.0f - (GapExtension * 4.0f) - ThirdGapExtension), Vector3.Zero, metal, paleSteel);

        AddFlightGate("FlightGate1", 0, new Vector3(0.0f, 14.15f, 12.0f - (GapExtension * 0.5f)), 3.4f);
        // The apertures alternate away from the center line. Their large
        // openings remain forgiving, but holding straight forward can no
        // longer solve the sequence.
        AddFlightGate("FlightGate2", 1, new Vector3(3.4f, 9.65f, -18.0f - (GapExtension * 1.5f)), 3.0f);
        AddFlightGate("FlightGate3", 2, new Vector3(3.4f, 4.7f, -49.0f - (GapExtension * 2.5f)), 3.4f);
        AddFlightGate("FlightGate4", 3, new Vector3(-3.4f, -0.5f, -80.0f - (GapExtension * 3.5f) - ThirdGapExtension), 3.0f);

        SurfaceDetail.AddOverlay(this, "Landing1Grime", new Vector3(-3.6f, 13.315f, 5.8f - GapExtension), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(8.0f)), new Vector2(4.8f, 3.0f), "res://assets/textures/overlays/grime.svg", new Color("27302d"), 0.44f);
        SurfaceDetail.AddOverlay(this, "Landing2Scratches", new Vector3(3.5f, 8.315f, -24.2f - (GapExtension * 2.0f)), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(-17.0f)), new Vector2(4.2f, 2.5f), "res://assets/textures/overlays/scratches.svg", new Color("d7ddd8"), 0.36f);
        SurfaceDetail.AddOverlay(this, "Landing3Oil", new Vector3(-2.8f, 3.315f, -55.4f - (GapExtension * 3.0f) - ThirdGapExtension), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(13.0f)), new Vector2(4.5f, 2.8f), "res://assets/textures/overlays/oil_rings.svg", new Color("1d2220"), 0.46f);
        SurfaceDetail.AddOverlay(this, "ExitSugar", new Vector3(2.4f, -1.685f, -99.0f - (GapExtension * 4.0f) - ThirdGapExtension), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(-12.0f)), new Vector2(4.0f, 2.6f), "res://assets/textures/overlays/sugar_dust.svg", new Color("f1dfbb"), 0.46f);
    }

    private void AddCourseBox(string name, Vector3 size, Vector3 position, Vector3 rotation, string texture, Color tint)
    {
        RoomGeometry.AddBox(this, name, size, position, rotation, texture, tint, 0.38f, 0.64f);
    }

    private void AddFlightGate(
        string name,
        int index,
        Vector3 position,
        float radius = 2.8f,
        float maximumDownwardExitSpeed = 2.5f)
    {
        FlightGate3D gate = new()
        {
            Name = name,
            Position = position,
            Radius = radius,
            FrameTint = new Color(index % 2 == 0 ? "b78350" : "95715a"),
            EnableAudio = !_runSolutionSmoke,
            MinimumExitSpeed = 20.0f,
            SpeedGain = 5.0f,
            SpeedMultiplier = 1.25f,
            AxialBoostOnly = true,
            MaximumDownwardExitSpeed = maximumDownwardExitSpeed,
        };
        gate.Passed += player =>
        {
            if (player == _player && index == _nextFlightGate)
            {
                _nextFlightGate++;
                if (_runSolutionSmoke)
                {
                    GD.Print($"ROOM03_GATE_TRACE: gate={index + 1}, tick={_solutionTick}, position={_player.GlobalPosition}, velocity={_player.LinearVelocity}.");
                }
            }
        };
        AddChild(gate);
        _flightGates.Add(gate);
        AddFlightAperture(index, position, radius);
    }

    private void AddFlightAperture(int index, Vector3 center, float ringRadius)
    {
        const float roomHalfWidth = 13.0f;
        const float hazardFloor = -6.0f;
        const float ceiling = 24.0f;
        const float panelDepth = 0.52f;
        // Leave enough clearance for the complete player collision sphere
        // while the FlightGate itself still decides whether the ball's center
        // crossed the circular opening.
        // The latch tips reach ringRadius + 0.72 m. Leave a small visual
        // border around them so no part of the mechanism is coplanar with, or
        // clipped by, the aperture panels.
        float openingHalf = ringRadius + 0.80f;
        string texture = "res://assets/textures/brushed_metal.png";
        Color tint = new(index % 2 == 0 ? "596562" : "655a52");

        float leftEdge = center.X - openingHalf;
        float leftWidth = leftEdge + roomHalfWidth;
        if (leftWidth > 0.01f)
        {
            RoomGeometry.AddBox(this, $"FlightAperture{index + 1}Left", new Vector3(leftWidth, ceiling - hazardFloor, panelDepth), new Vector3(-roomHalfWidth + (leftWidth * 0.5f), (ceiling + hazardFloor) * 0.5f, center.Z), Vector3.Zero, texture, tint, 0.46f, 0.66f);
        }

        float rightEdge = center.X + openingHalf;
        float rightWidth = roomHalfWidth - rightEdge;
        if (rightWidth > 0.01f)
        {
            RoomGeometry.AddBox(this, $"FlightAperture{index + 1}Right", new Vector3(rightWidth, ceiling - hazardFloor, panelDepth), new Vector3(rightEdge + (rightWidth * 0.5f), (ceiling + hazardFloor) * 0.5f, center.Z), Vector3.Zero, texture, tint, 0.46f, 0.66f);
        }

        float lowerEdge = center.Y - openingHalf;
        float lowerHeight = lowerEdge - hazardFloor;
        if (lowerHeight > 0.01f)
        {
            RoomGeometry.AddBox(this, $"FlightAperture{index + 1}Lower", new Vector3(openingHalf * 2.0f, lowerHeight, panelDepth), new Vector3(center.X, hazardFloor + (lowerHeight * 0.5f), center.Z), Vector3.Zero, texture, tint.Darkened(0.08f), 0.46f, 0.66f);
        }

        float upperEdge = center.Y + openingHalf;
        float upperHeight = ceiling - upperEdge;
        if (upperHeight > 0.01f)
        {
            RoomGeometry.AddBox(this, $"FlightAperture{index + 1}Upper", new Vector3(openingHalf * 2.0f, upperHeight, panelDepth), new Vector3(center.X, upperEdge + (upperHeight * 0.5f), center.Z), Vector3.Zero, texture, tint.Darkened(0.08f), 0.46f, 0.66f);
        }
    }

    private void BuildGoal()
    {
        // Place the door plane on the inner face of the shell's exit wall.
        // This lets the shared doorway carve expose the complete frame instead
        // of generating a second partition through the frame in front of it.
        Vector3 goalPosition = new(-3.4f, -1.05f, -103.17f - (GapExtension * 4.0f) - ThirdGapExtension);
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
            Shape = new CylinderShape3D { Radius = 4.0f, Height = 2.8f },
        });
        goal.BodyEntered += body =>
        {
            if (body is PlayerBall && _nextFlightGate == _flightGates.Count)
            {
                CompleteRoom();
            }
        };
        AddChild(goal);

        // Complete the puzzle on the final runout, before the player reaches
        // the door plane. GoalCup stays at the standard doorway anchor so the
        // common exit remains aligned and the door opens in time for a fast
        // valid arrival.
        Area3D runoutLatch = new()
        {
            Name = "RouteCompletionTrigger",
            Position = new Vector3(-3.4f, -1.05f, -94.0f - (GapExtension * 4.0f) - ThirdGapExtension),
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true,
        };
        runoutLatch.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(6.8f, 4.5f, 2.0f) },
        });
        runoutLatch.BodyEntered += body =>
        {
            if (body is PlayerBall && _nextFlightGate == _flightGates.Count)
            {
                CompleteRoom();
            }
        };
        AddChild(runoutLatch);

        RoomGeometry.AddGoalExitDoor(this, goalPosition);
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM03_SOLUTION_FAIL: {message}");
        FinishSolutionSmoke(1);
    }

    private async void FinishSolutionSmoke(int exitCode)
    {
        if (_solutionFinishing)
        {
            return;
        }

        _solutionFinishing = true;
        _player.SimulatedMoveInput = null;
        foreach (FlightGate3D gate in _flightGates)
        {
            gate.ResetGate();
        }
        _solutionTrace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(exitCode);
    }
}
