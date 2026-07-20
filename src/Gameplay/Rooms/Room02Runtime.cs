using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Visual;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room02Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_02_solution.tres";
    private const int RequiredSolutionRuns = 10;
    private const int MaximumSolutionTicksPerRun = 1700;

    private readonly List<RouteCheckpoint3D> _routeCheckpoints = new();
    private readonly Dictionary<RouteCheckpoint3D, Material> _checkpointIdleMaterials = new();
    private readonly Dictionary<RouteCheckpoint3D, Tween> _wrongOrderTweens = new();
    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private bool _runSolutionSmoke;
    private bool _runPreview;
    private bool _runShellSmoke;
    private int _solutionRun;
    private int _solutionTick;
    private int _previewFrames;
    private int _shellSmokeTick;
    private int _nextRouteCheckpoint;
    private int _solutionWarmupTicks = 6;
    private StandardMaterial3D? _wrongOrderMaterial;
    private ExitDoor3D? _exitDoor;
    private CollisionShape3D? _routeLockCollision;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        BuildRoom();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room02_a", new Vector3(-8.5f, 16.5f, 27.0f), new Vector3(7.5f, 6.0f, -12.0f), 57.0f),
            new("room02_b", new Vector3(23.0f, 15.0f, -10.0f), new Vector3(6.0f, 5.5f, -24.0f), 58.0f),
        });

        _player = GetNode<PlayerBall>("Player");
        _cameraRig = GetNode<PlayerCameraRig>("CameraRig");
        _spawnTransform = _player.GlobalTransform;
        _cameraRig.Follow(_player);
        _player.MovementBasis = _cameraRig.MovementBasis;

        string[] userArguments = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(userArguments, argument => argument == "--room02-solution-smoke");
        _runPreview = Array.Exists(userArguments, argument => argument == "--room02-preview");
        _runShellSmoke = Array.Exists(userArguments, argument => argument == "--room-shell-smoke");
        if (_runPreview)
        {
            _cameraRig.SetInputEnabled(false);
        }

        if (_runSolutionSmoke)
        {
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            if (_solutionTrace is null ||
                _solutionTrace.RoomId != RoomId ||
                _solutionTrace.MoveInputs.Count == 0 ||
                _solutionTrace.MoveDurationsTicks.Length != _solutionTrace.MoveInputs.Count)
            {
                string details = _solutionTrace is null
                    ? "trace=null"
                    : $"trace_room='{_solutionTrace.RoomId}', runtime_room='{RoomId}', inputs={_solutionTrace.MoveInputs.Count}, durations={_solutionTrace.MoveDurationsTicks.Length}";
                FailSolutionSmoke($"The Room 02 SolutionTrace is invalid ({details}).");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_runPreview && ++_previewFrames >= 30)
        {
            string capturePath = ProjectSettings.GlobalizePath("user://room02-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(capturePath);
            GD.Print($"ROOM02_PREVIEW_CAPTURE: {capturePath}");
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

        if (_player.GlobalPosition.Y < -8.0f)
        {
            RestartRoom();
        }

    }

    public override void RestartRoom()
    {
        if (_runSolutionSmoke && _solutionTick > 0)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} touched the hazard floor at {_player.GlobalPosition}.");
            return;
        }

        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _player.ResetTo(_spawnTransform);
        ResetRouteCheckpoints();
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
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 02 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 02 hazard floor restarted the player.");
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
            _solutionWarmupTicks--;
            return;
        }

        if (IsComplete)
        {
            if (_nextRouteCheckpoint != _routeCheckpoints.Count)
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} reached the cup after only {_nextRouteCheckpoint}/{_routeCheckpoints.Count} route latches.");
                return;
            }

            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print($"ROOM02_SOLUTION_PASS: SolutionTrace completed Room 02 {_solutionRun} consecutive times.");
                GetTree().Quit(0);
                return;
            }

            ClearCompletionState();
            _player.SimulatedMoveInput = null;
            _player.ResetTo(_spawnTransform);
            _solutionTick = 0;
            _solutionWarmupTicks = 6;
            ResetRouteCheckpoints();
            return;
        }

        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} timed out at position {_player.GlobalPosition}, velocity={_player.LinearVelocity}, grounded={_player.IsGrounded}, input={ResolveTraceInput(_solutionTick - 1)}, route={_nextRouteCheckpoint}/{_routeCheckpoints.Count}.");
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

        return _solutionTrace.HoldLastInput
            ? _solutionTrace.MoveInputs[^1]
            : Vector2.Zero;
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM02_SOLUTION_FAIL: {message}");
        GetTree().Quit(1);
    }

    private void BuildRoom()
    {
        const string copper = "res://assets/textures/copper_rivets.svg";
        const string metal = "res://assets/textures/brushed_metal.png";
        Color copperTint = new("b57758");
        Color darkCopper = new("785448");
        Color steelTint = new("789096");
        _wrongOrderMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color("d62f2f"),
            Metallic = 0.0f,
            Roughness = 0.54f,
            EmissionEnabled = true,
            Emission = new Color("721010"),
            EmissionEnergyMultiplier = 1.15f,
        };

        RoomGeometry.AddClosedRoomShell(
            this,
            "RoomShell",
            Vector3.Zero,
            new Vector2(46.0f, 76.0f),
            -4.0f,
            22.0f,
            metal,
            new Color("59716e"),
            new Color("93624d"),
            body =>
            {
                if (body is PlayerBall)
                {
                    RestartRoom();
                }
            });

        // Room 02 is deliberately built from one central rectangle and two
        // mirrored ramps. The four ordered floor buttons still require the
        // player to charge right, reverse across the basin, then commit to the
        // exit; the simplified footprint avoids a maze of fragile wall joins.
        const float lowFloorY = 5.5f;
        const float lowTopY = 5.75f;
        const float highFloorY = 10.5f;
        const float slopeAngle = 26.565052f;
        const float slopeLength = 11.18034f;

        RoomGeometry.AddBox(this, "SafeStart", new Vector3(8.0f, 0.5f, 23.775f), new Vector3(0.0f, lowFloorY, 25.8875f), Vector3.Zero, metal, new Color("a2afad"), 0.46f, 0.64f);
        RoomGeometry.AddBox(this, "MomentumBasin", new Vector3(16.0f, 0.5f, 18.0f), new Vector3(0.0f, lowFloorY, 5.0f), Vector3.Zero, metal, steelTint, 0.48f, 0.64f);
        RoomGeometry.AddBox(this, "ExitRun", new Vector3(8.0f, 0.5f, 33.75f), new Vector3(0.0f, lowFloorY, -20.875f), Vector3.Zero, metal, new Color("9aa9a8"), 0.48f, 0.62f);

        RoomGeometry.AddBox(this, "RightChargeSlope", new Vector3(slopeLength, 0.5f, 18.0f), new Vector3(13.111803f, 8.026393f, 5.0f), new Vector3(0.0f, 0.0f, Mathf.DegToRad(slopeAngle)), copper, new Color("b77a58"), 0.48f, 0.62f);
        RoomGeometry.AddBox(this, "RightButtonDeck", new Vector3(4.75f, 0.5f, 18.0f), new Vector3(20.375f, highFloorY, 5.0f), Vector3.Zero, copper, new Color("aa6d50"), 0.48f, 0.62f);
        RoomGeometry.AddBox(this, "LeftReturnSlope", new Vector3(slopeLength, 0.5f, 18.0f), new Vector3(-13.111803f, 8.026393f, 5.0f), new Vector3(0.0f, 0.0f, Mathf.DegToRad(-slopeAngle)), copper, new Color("a9694e"), 0.48f, 0.62f);
        RoomGeometry.AddBox(this, "LeftButtonDeck", new Vector3(4.75f, 0.5f, 18.0f), new Vector3(-20.375f, highFloorY, 5.0f), Vector3.Zero, copper, new Color("a9694e"), 0.48f, 0.62f);

        AddRouteCheckpoint("BasinEntryLatch", 0, new Vector3(0.0f, 7.0f, 8.0f), new Vector3(5.0f, 3.0f, 4.0f));
        _routeCheckpoints[0].FloorMarkerInset = 0.10f;
        AddRouteCheckpoint("RightChargeLatch", 1, new Vector3(20.0f, 12.8f, 3.0f), new Vector3(4.0f, 5.0f, 10.0f));
        AddRouteCheckpoint("LeftReturnLatch", 2, new Vector3(-20.0f, 12.8f, 3.0f), new Vector3(4.0f, 5.0f, 10.0f));
        AddRouteCheckpoint("ExitCommitLatch", 3, new Vector3(0.0f, 7.0f, -12.0f), new Vector3(5.0f, 3.0f, 5.0f));

        // Long uninterrupted side guards cover every playable edge. Short
        // basin segments leave only the intentional ramp and exit openings.
        foreach (float x in new[] { -4.0f, 4.0f })
        {
            RoomGeometry.AddBox(this, $"StartRail{x}", new Vector3(0.34f, 1.2f, 23.775f), new Vector3(x, 6.35f, 25.8875f), Vector3.Zero, metal, steelTint, 0.5f, 0.58f);
            RoomGeometry.AddBox(this, $"ExitRunRail{x}", new Vector3(0.34f, 1.2f, 33.75f), new Vector3(x, 6.35f, -20.875f), Vector3.Zero, metal, steelTint, 0.5f, 0.58f);
        }
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(this, $"BasinNorthRail{side}", new Vector3(4.0f, 1.2f, 0.34f), new Vector3(side * 6.0f, 6.35f, 14.0f), Vector3.Zero, metal, steelTint, 0.5f, 0.58f);
            RoomGeometry.AddBox(this, $"BasinSouthRail{side}", new Vector3(4.0f, 1.2f, 0.34f), new Vector3(side * 6.0f, 6.35f, -4.0f), Vector3.Zero, metal, steelTint, 0.5f, 0.58f);
        }
        foreach (float z in new[] { -4.0f, 14.0f })
        {
            RoomGeometry.AddBox(this, $"RightSlopeRail{z}", new Vector3(slopeLength, 1.2f, 0.34f), new Vector3(12.731672f, 8.786657f, z), new Vector3(0.0f, 0.0f, Mathf.DegToRad(slopeAngle)), metal, steelTint, 0.5f, 0.58f);
            RoomGeometry.AddBox(this, $"LeftSlopeRail{z}", new Vector3(slopeLength, 1.2f, 0.34f), new Vector3(-12.731672f, 8.786657f, z), new Vector3(0.0f, 0.0f, Mathf.DegToRad(-slopeAngle)), metal, steelTint, 0.5f, 0.58f);
            RoomGeometry.AddBox(this, $"RightDeckRail{z}", new Vector3(4.75f, 1.2f, 0.34f), new Vector3(20.375f, 11.35f, z), Vector3.Zero, metal, steelTint, 0.5f, 0.58f);
            RoomGeometry.AddBox(this, $"LeftDeckRail{z}", new Vector3(4.75f, 1.2f, 0.34f), new Vector3(-20.375f, 11.35f, z), Vector3.Zero, metal, steelTint, 0.5f, 0.58f);
        }

        SurfaceDetail.AddOverlay(this, "BasinGrime", new Vector3(-1.8f, lowTopY + 0.015f, 4.2f), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(13.0f)), new Vector2(6.2f, 3.8f), "res://assets/textures/overlays/grime.svg", new Color("26352f"), 0.48f);
        SurfaceDetail.AddOverlay(this, "ApproachPatina", new Vector3(-1.3f, lowTopY + 0.015f, 22.0f), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(-9.0f)), new Vector2(5.2f, 3.4f), "res://assets/textures/overlays/patina.svg", new Color("2f6d65"), 0.48f);
        SurfaceDetail.AddOverlay(this, "ExitSugarDust", new Vector3(0.0f, lowTopY + 0.015f, -20.0f), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(-14.0f)), new Vector2(4.0f, 2.5f), "res://assets/textures/overlays/sugar_dust.svg", new Color("f9e1bc"), 0.5f);
    }

    private void BuildGoal()
    {
        // The shared door is placed directly in the carved shell opening.
        // This keeps the complete frame and fixed chevron in front of the wall
        // instead of embedding them in a remote backing partition.
        Vector3 goalPosition = new(0.0f, 7.0f, -36.67f);
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
            Shape = new CylinderShape3D { Radius = 1.45f, Height = 2.4f },
        });
        goal.BodyEntered += body =>
        {
            if (body is PlayerBall && _nextRouteCheckpoint == _routeCheckpoints.Count)
            {
                CompleteRoom();
            }
        };
        AddChild(goal);

        _exitDoor = RoomGeometry.AddGoalExitDoor(this, goalPosition);
        AddRouteLockCollision(_exitDoor);
        LockExitDoor();
    }

    private void AddRouteCheckpoint(
        string name,
        int index,
        Vector3 position,
        Vector3 triggerSize,
        Vector3? rotation = null)
    {
        RouteCheckpoint3D checkpoint = new()
        {
            Name = name,
            CheckpointIndex = index,
            Position = position,
            Rotation = rotation ?? Vector3.Zero,
            TriggerSize = triggerSize,
            FrameTint = RoomGeometry.SequenceButtonFrameTint,
            FlatFloorMarker = true,
        };
        checkpoint.Entered += (entered, player) =>
        {
            if (player != _player)
            {
                return;
            }

            if (entered.CheckpointIndex == _nextRouteCheckpoint)
            {
                entered.Activate();
                _nextRouteCheckpoint++;
                if (_runSolutionSmoke)
                {
                    GD.Print($"ROOM02_TRACE_POINT: route={_nextRouteCheckpoint}/{_routeCheckpoints.Count}, tick={_solutionTick}, position={player.GlobalPosition}, velocity={player.LinearVelocity}.");
                }
                if (_nextRouteCheckpoint == _routeCheckpoints.Count)
                {
                    UnlockExitDoor();
                }
            }
            else
            {
                FlashWrongOrder(entered);
            }
        };
        AddChild(checkpoint);
        MeshInstance3D insetPlate = checkpoint.GetNode<MeshInstance3D>("InsetPlate");
        if (insetPlate.MaterialOverride is Material idleMaterial)
        {
            _checkpointIdleMaterials[checkpoint] = idleMaterial;
        }
        RoomGeometry.AddSequencePips(insetPlate, index + 1);
        _routeCheckpoints.Add(checkpoint);
    }

    private void FlashWrongOrder(RouteCheckpoint3D checkpoint)
    {
        checkpoint.FlashDenied();
    }

    private static void SetWrongOrderVisual(
        RouteCheckpoint3D checkpoint,
        MeshInstance3D insetPlate,
        Material material,
        bool showSequencePips)
    {
        insetPlate.MaterialOverride = material;
        foreach (Node child in insetPlate.GetChildren())
        {
            if (child is MeshInstance3D pip && child.Name.ToString().StartsWith("SequencePip", StringComparison.Ordinal))
            {
                pip.Visible = showSequencePips;
            }
        }
    }


    private void AddRouteLockCollision(ExitDoor3D door)
    {
        StaticBody3D lockBody = new()
        {
            Name = "RouteLockBarrier",
            Position = new Vector3(0.0f, 0.0f, -0.26f),
            CollisionLayer = 1,
            CollisionMask = 1,
        };
        _routeLockCollision = new CollisionShape3D
        {
            Name = "RouteLockCollision",
            Position = new Vector3(0.0f, 2.0f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(3.55f, 3.75f, 0.3f) },
        };
        lockBody.AddChild(_routeLockCollision);
        door.AddChild(lockBody);
    }

    private void LockExitDoor()
    {
        if (IsInstanceValid(_exitDoor))
        {
            _exitDoor!.ResetClosed();
            // Keep the actual door alive and visible while locked. Its own
            // closed-door collider and the route barrier both remain solid;
            // only completion of the ordered button route can release them.
            _exitDoor.ProcessMode = ProcessModeEnum.Inherit;
        }
        if (IsInstanceValid(_routeLockCollision))
        {
            _routeLockCollision!.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
        }
    }

    private void UnlockExitDoor()
    {
        if (IsInstanceValid(_routeLockCollision))
        {
            _routeLockCollision!.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
        }
        if (IsInstanceValid(_exitDoor))
        {
            _exitDoor!.ProcessMode = ProcessModeEnum.Inherit;
        }
    }

    private void ResetRouteCheckpoints()
    {
        _nextRouteCheckpoint = 0;
        foreach (Tween tween in _wrongOrderTweens.Values)
        {
            tween.Kill();
        }
        _wrongOrderTweens.Clear();
        foreach (RouteCheckpoint3D checkpoint in _routeCheckpoints)
        {
            checkpoint.ResetCheckpoint();
            if (_checkpointIdleMaterials.TryGetValue(checkpoint, out Material? idleMaterial) &&
                checkpoint.GetNodeOrNull<MeshInstance3D>("InsetPlate") is MeshInstance3D insetPlate)
            {
                SetWrongOrderVisual(checkpoint, insetPlate, idleMaterial, showSequencePips: true);
            }
        }
        LockExitDoor();
    }
}
