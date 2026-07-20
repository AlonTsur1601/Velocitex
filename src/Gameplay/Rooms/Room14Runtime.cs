using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Physics;
using Velocitex.Gameplay.Player;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room14Runtime : RoomRuntime
{
    private const string DirectTracePath = "res://resources/solutions/room_14_solution.tres";
    private const string CorrectionTracePath = "res://resources/solutions/room_14_correction_solution.tres";
    private const int RequiredSolutionRuns = 10;
    private const int MaximumSolutionTicksPerRun = 1750;
    private const int RequiredSegmentTicks = 40;

    private enum RailChoice
    {
        None,
        Violet,
        Amber,
        Teal,
        Rose,
    }

    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private readonly List<MomentumRail3D> _routeRails = new();
    private readonly Dictionary<MomentumRail3D, (RailChoice Choice, bool Incoming)> _railRoutes = new();
    private readonly List<RouteCheckpoint3D> _sequencePlates = new();
    private AudioStreamPlayer3D? _railAudio;
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private RailChoice _firstChoice;
    private RailChoice _activeRail;
    private bool _completedIncomingRail;
    private bool _completedOutgoingRail;
    private bool _switchedRail;
    private bool _runSolutionSmoke;
    private bool _runCorrectionSmoke;
    private bool _runPreview;
    private bool _runShellSmoke;
    private bool _solutionSmokeFinishing;
    private int _activeSegmentTicks;
    private int _attachmentCount;
    private int _nextSequencePlate;
    private int _solutionRun;
    private int _solutionTick;
    private int _previewFrames;
    private int _shellSmokeTick;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] arguments = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(arguments, argument => argument == "--room14-solution-smoke");
        _runCorrectionSmoke = Array.Exists(arguments, argument => argument == "--room14-correction-smoke");
        _runPreview = Array.Exists(arguments, argument => argument == "--room14-preview");
        _runShellSmoke = Array.Exists(arguments, argument => argument == "--room-shell-smoke");

        BuildRoom();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room14_a", new Vector3(12.0f, 13.5f, 43.0f), new Vector3(0.0f, 13.0f, -42.0f), 58.0f),
            new("room14_b", new Vector3(-15.0f, 24.0f, -62.0f), new Vector3(3.0f, 11.0f, -24.0f), 59.0f),
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

        foreach (MomentumRail3D rail in _routeRails)
        {
            (RailChoice choice, bool incoming) = _railRoutes[rail];
            ConnectRail(rail, choice, incoming);
        }

        if (_runSolutionSmoke || _runCorrectionSmoke)
        {
            _solutionTrace = GD.Load<SolutionTrace>(_runCorrectionSmoke ? CorrectionTracePath : DirectTracePath);
            if (_solutionTrace is null ||
                _solutionTrace.RoomId != RoomId ||
                _solutionTrace.MoveInputs.Count < 4 ||
                _solutionTrace.MoveDurationsTicks.Length != _solutionTrace.MoveInputs.Count ||
                !_solutionTrace.MoveInputs.Any(input => input.X < -0.35f) ||
                !_solutionTrace.MoveInputs.Any(input => input.X > 0.35f))
            {
                FailSolutionSmoke("The selected Room 14 trace must contain deliberate branch and final-switch steering.");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_runPreview && ++_previewFrames >= 30)
        {
            string path = ProjectSettings.GlobalizePath("user://room14-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"ROOM14_PREVIEW_CAPTURE: {path}");
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
        if (_runSolutionSmoke || _runCorrectionSmoke)
        {
            RunSolutionTick();
            return;
        }

        TrackRailRide();
        TryCompleteGoal();
        if (_player.GlobalPosition.Y < -7.0f)
        {
            RestartRoom();
        }
    }

    public override void RestartRoom()
    {
        if ((_runSolutionSmoke || _runCorrectionSmoke) && _solutionTick > 0 && !_solutionSmokeFinishing)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} hit a hazard at {_player.GlobalPosition}; {DescribeEvidence()}.");
            return;
        }

        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        ResetRails();
        _player.ResetTo(_spawnTransform);
        ResetRouteState();
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
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 14 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }
        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 14 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void RunSolutionTick()
    {
        if (_solutionTrace is null || _solutionSmokeFinishing)
        {
            return;
        }

        TrackRailRide();
        TryCompleteGoal();
        if (IsComplete)
        {
            if (!RouteRequirementsMet())
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} bypassed the rail-switch puzzle: {DescribeEvidence()}.");
                return;
            }

            string[] openedAdvancementIds = CompletedAdvancementIds.ToArray();
            bool anyAdvancementOpened = openedAdvancementIds.Length > 0;
            string advancementEvidence = anyAdvancementOpened
                ? string.Join(",", openedAdvancementIds)
                : "none";
            if (_runCorrectionSmoke)
            {
                if (_firstChoice == RailChoice.None ||
                    !_completedIncomingRail ||
                    !_completedOutgoingRail ||
                    !_switchedRail ||
                    anyAdvancementOpened)
                {
                    FailSolutionSmoke($"Correction route did not complete with every unrelated advancement withheld: advancements={advancementEvidence}, {DescribeEvidence()}.");
                    return;
                }

                GD.Print($"ROOM14_CORRECTION_PASS: The central interchange allowed a different outgoing route, completed all three switch plates and withheld Perfect Switch; {DescribeEvidence()}.");
                FinishSolutionSmoke(0);
                return;
            }

            if (!CleanRouteMet() || !CompletedAdvancementIds.Contains("perfect-switch"))
            {
                FailSolutionSmoke($"The clean route did not stay on its starting color or failed to open Perfect Switch: advancements={advancementEvidence}, {DescribeEvidence()}.");
                return;
            }

            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print($"ROOM14_SOLUTION_PASS: SolutionTrace stayed on its starting rail color through the central interchange, solved all three final switch plates and opened Perfect Switch for {_solutionRun} consecutive completions; {DescribeEvidence()}.");
                FinishSolutionSmoke(0);
                return;
            }

            ClearCompletionState();
            ResetRails();
            _player.ResetTo(_spawnTransform);
            _solutionTick = 0;
            ResetRouteState();
            return;
        }

        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} timed out at {_player.GlobalPosition}, velocity={_player.LinearVelocity}; {DescribeEvidence()}.");
            return;
        }

        _player.SimulatedMoveInput = ResolveTraceInput(_solutionTick - 1);
        if (_solutionTick % 30 == 0)
        {
            GD.Print($"ROOM14_TRACE: tick={_solutionTick}, position={_player.GlobalPosition}, velocity={_player.LinearVelocity}, grounded={_player.IsGrounded}, active={_activeRail}, {DescribeEvidence()}.");
        }
    }

    private void ConnectRail(MomentumRail3D rail, RailChoice choice, bool incoming)
    {
        rail.Attached += body =>
        {
            if (body != _player)
            {
                return;
            }

            if (_firstChoice == RailChoice.None)
            {
                _firstChoice = choice;
            }
            else if (_firstChoice != choice)
            {
                _switchedRail = true;
            }
            _attachmentCount++;
            _activeRail = choice;
            _activeSegmentTicks = 0;
            _railAudio?.Play();
            if (_runSolutionSmoke || _runCorrectionSmoke)
            {
                GD.Print($"ROOM14_RAIL_ATTACH: rail={choice}, attachment={_attachmentCount}, tick={_solutionTick}, position={_player.GlobalPosition}.");
            }
        };

        rail.Released += body =>
        {
            if (body != _player)
            {
                return;
            }

            if (_activeSegmentTicks >= RequiredSegmentTicks)
            {
                if (incoming)
                {
                    _completedIncomingRail = true;
                }
                else if (_completedIncomingRail)
                {
                    _completedOutgoingRail = true;
                }
            }
            _activeRail = RailChoice.None;
            if (_runSolutionSmoke || _runCorrectionSmoke)
            {
                GD.Print($"ROOM14_RAIL_RELEASE: rail={choice}, tick={_solutionTick}, position={_player.GlobalPosition}; {DescribeEvidence()}.");
            }
        };
    }

    private void TrackRailRide()
    {
        if (_routeRails.Any(rail => rail.IsAttached(_player)))
        {
            _activeSegmentTicks++;
        }
    }

    private bool RouteRequirementsMet()
    {
        return _completedIncomingRail && _completedOutgoingRail && _nextSequencePlate == _sequencePlates.Count;
    }

    private bool CleanRouteMet() =>
        _firstChoice != RailChoice.None &&
        _completedIncomingRail &&
        _completedOutgoingRail &&
        !_switchedRail &&
        _attachmentCount == 2;

    private string DescribeEvidence() =>
        $"first={_firstChoice}, active={_activeRail}, switched={_switchedRail}, attachments={_attachmentCount}, incoming={_completedIncomingRail}, outgoing={_completedOutgoingRail}, segment_ticks={_activeSegmentTicks}, plates={_nextSequencePlate}/{_sequencePlates.Count}";

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

    private void ResetRails()
    {
        foreach (MomentumRail3D rail in _routeRails)
        {
            rail.ResetBody(_player);
        }
        _railAudio?.Stop();
        foreach (RouteCheckpoint3D plate in _sequencePlates)
        {
            plate.ResetCheckpoint();
        }
    }

    private void ResetRouteState()
    {
        _firstChoice = RailChoice.None;
        _activeRail = RailChoice.None;
        _completedIncomingRail = false;
        _completedOutgoingRail = false;
        _switchedRail = false;
        _activeSegmentTicks = 0;
        _attachmentCount = 0;
        _nextSequencePlate = 0;
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string copper = "res://assets/textures/copper_rivets.svg";
        Color pale = new("b9b3c4");
        Color frame = new("554b66");

        RoomGeometry.AddClosedRoomShell(
            this,
            "RoomShell",
            new Vector3(0.0f, 0.0f, -50.0f),
            new Vector2(40.0f, 200.0f),
            -3.0f,
            40.0f,
            metal,
            new Color("726b7d"),
            new Color("433d4e"),
            body =>
            {
                if (body is PlayerBall)
                {
                    RestartRoom();
                }
            });

        RoomGeometry.AddBox(this, "SafeStart", new Vector3(12.0f, 0.5f, 17.775f), new Vector3(0.0f, 8.0f, 40.8875f), Vector3.Zero, metal, pale, 0.4f, 0.66f);

        const float rampAngle = -0.2914568f;
        Vector3 rampRotation = new(rampAngle, 0.0f, 0.0f);
        RoomGeometry.AddBox(this, "ChoiceDescent", new Vector3(12.0f, 0.5f, 10.440307f), new Vector3(0.0f, 6.510774f, 27.071838f), rampRotation, copper, new Color("786c82"), 0.38f, 0.6f);
        RoomGeometry.AddBox(this, "ChoiceDeck", new Vector3(18.0f, 0.5f, 17.0f), new Vector3(0.0f, 5.0f, 13.5f), Vector3.Zero, metal, pale.Darkened(0.04f), 0.4f, 0.66f);

        AddSideWalls("Start", new Vector3(0.0f, 8.925f, 40.8875f), 17.775f, Vector3.Zero, 6.18f, metal, frame);
        Vector3 rampWallCenter = new(0.0f, 7.39645f, 26.8060f);
        AddSideWalls("Descent", rampWallCenter, 10.440307f, rampRotation, 6.18f, metal, frame);
        AddSideWalls("Choice", new Vector3(0.0f, 5.925f, 13.5f), 17.0f, Vector3.Zero, 9.18f, metal, frame);
        RoomGeometry.AddBox(this, "CentralInterchange", new Vector3(24.0f, 0.5f, 14.0f), new Vector3(0.0f, 12.0f, -48.0f), Vector3.Zero, metal, pale.Darkened(0.08f), 0.4f, 0.66f);
        RoomGeometry.AddBox(this, "InterchangeExtensionIncoming", new Vector3(24.0f, 0.5f, 4.0f), new Vector3(0.0f, 12.0f, -39.0f), Vector3.Zero, metal, pale.Darkened(0.08f), 0.4f, 0.66f);
        RoomGeometry.AddBox(this, "InterchangeExtensionOutgoing", new Vector3(24.0f, 0.5f, 4.0f), new Vector3(0.0f, 12.0f, -57.0f), Vector3.Zero, metal, pale.Darkened(0.08f), 0.4f, 0.66f);
        AddSideWalls("Interchange", new Vector3(0.0f, 12.925f, -48.0f), 22.0f, Vector3.Zero, 12.18f, copper, frame);

        RoomGeometry.AddBox(this, "FinalDeck", new Vector3(18.0f, 0.5f, 85.0f), new Vector3(0.0f, 15.75f, -107.5f), Vector3.Zero, metal, pale.Darkened(0.02f), 0.4f, 0.66f);
        AddSideWalls("Final", new Vector3(0.0f, 16.675f, -107.5f), 85.0f, Vector3.Zero, 9.18f, metal, frame);

        (RailChoice Choice, float IncomingX, float InterchangeX, float OutgoingX, float LandingX, Color Color)[] routes =
        {
            (RailChoice.Violet, -6.0f, -6.0f, -6.0f, -6.0f, new Color("b894d0")),
            (RailChoice.Amber, -2.0f, -2.0f, 2.0f, 2.0f, new Color("c09365")),
            (RailChoice.Teal, 2.0f, 2.0f, -2.0f, -2.0f, new Color("78a99c")),
            (RailChoice.Rose, 6.0f, 6.0f, 6.0f, 6.0f, new Color("c8798d")),
        };
        foreach ((RailChoice choice, float incomingX, float interchangeX, float outgoingX, float landingX, Color color) in routes)
        {
            AddInterchangeStripe($"{choice}ChoiceGuide", new Vector3(incomingX, 5.28f, 12.0f), new Vector3(incomingX, 5.28f, 3.2f), color);
            MomentumRail3D incoming = CreateRail(
                $"{choice}IncomingRail",
                new Vector3(incomingX, 5.85f, 3.0f),
                new Vector3(interchangeX, 13.1f, -40.5f),
                0.95f,
                14.5f,
                color,
                coilCount: 10);
            RegisterRouteRail(incoming, choice, incoming: true);

            AddInterchangeStripe($"{choice}InterchangeStripe", new Vector3(interchangeX, 12.28f, -41.2f), new Vector3(outgoingX, 12.28f, -54.0f), color);

            MomentumRail3D outgoing = CreateRail(
                $"{choice}OutgoingRail",
                new Vector3(outgoingX, 13.1f, -54.5f),
                new Vector3(landingX, 17.0f, -68.0f),
                0.95f,
                14.5f,
                color,
                coilCount: 7);
            RegisterRouteRail(outgoing, choice, incoming: false);
            AddInterchangeStripe($"{choice}LandingGuide", new Vector3(landingX, 16.03f, -68.0f), new Vector3(landingX, 16.03f, -76.0f), color);
        }

        AddSlalomBarrier("FinalSlalomOne", -89.0f, openingRight: true, copper, frame);
        AddSequencePlate("FinalSwitchOne", 0, new Vector3(6.0f, 16.68f, -98.0f));
        AddSlalomBarrier("FinalSlalomTwo", -106.0f, openingRight: false, copper, frame);
        AddSequencePlate("FinalSwitchTwo", 1, new Vector3(-6.0f, 16.68f, -114.0f));
        AddSlalomBarrier("FinalSlalomThree", -136.0f, openingRight: true, copper, frame);
        AddSequencePlate("FinalSwitchThree", 2, new Vector3(5.5f, 16.68f, -142.0f));

        if (!_runSolutionSmoke && !_runCorrectionSmoke)
        {
            _railAudio = new AudioStreamPlayer3D
            {
                Name = "RailAttachSfx",
                Stream = GD.Load<AudioStream>("res://assets/audio/sfx/device_rail_attach.wav"),
                Bus = "SFX",
                Position = new Vector3(0.0f, 11.0f, -30.0f),
                MaxDistance = 44.0f,
                UnitSize = 8.0f,
            };
            AddChild(_railAudio);
        }
    }

    private void AddSlalomBarrier(string name, float z, bool openingRight, string texture, Color tint)
    {
        const float blockedWidth = 11.4f;
        float centerX = openingRight ? -3.3f : 3.3f;
        RoomGeometry.AddBox(
            this,
            name,
            new Vector3(blockedWidth, 2.6f, 0.55f),
            new Vector3(centerX, 17.3f, z),
            Vector3.Zero,
            texture,
            tint,
            0.42f,
            0.62f);
    }

    private void AddSideWalls(
        string prefix,
        Vector3 center,
        float length,
        Vector3 rotation,
        float x,
        string texture,
        Color tint,
        bool useExplicitX = false)
    {
        if (useExplicitX)
        {
            RoomGeometry.AddBox(this, $"{prefix}SideWall", new Vector3(0.36f, 1.35f, length), center, rotation, texture, tint, 0.42f, 0.62f);
            return;
        }

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(
                this,
                $"{prefix}SideWall{(side < 0.0f ? "Left" : "Right")}",
                new Vector3(0.36f, 1.35f, length),
                new Vector3(side * x, center.Y, center.Z),
                rotation,
                texture,
                tint,
                0.42f,
                0.62f);
        }
    }

    private MomentumRail3D CreateRail(
        string name,
        Vector3 start,
        Vector3 end,
        float captureRadius,
        float minimumSpeed,
        Color color,
        int coilCount)
    {
        MomentumRail3D rail = new()
        {
            Name = name,
            LocalStart = start,
            LocalEnd = end,
            CaptureRadius = captureRadius,
            MinimumSpeed = minimumSpeed,
            CollisionMask = 1,
        };
        AddChild(rail);
        AddRailVisuals(name, start, end, color, coilCount);
        return rail;
    }

    private void RegisterRouteRail(MomentumRail3D rail, RailChoice choice, bool incoming)
    {
        _routeRails.Add(rail);
        _railRoutes[rail] = (choice, incoming);
    }

    private void AddInterchangeStripe(string name, Vector3 start, Vector3 end, Color color)
    {
        Vector3 path = end - start;
        float length = path.Length();
        float yaw = Mathf.Atan2(path.X, path.Z);
        StandardMaterial3D material = RoomGeometry.CreateMaterial(
            "res://assets/textures/brushed_metal.png",
            color.Lerp(Colors.White, 0.18f),
            0.44f,
            0.55f);
        RoomGeometry.AddVisualBox(
            this,
            name,
            new Vector3(0.34f, 0.035f, length),
            (start + end) * 0.5f,
            new Vector3(0.0f, yaw, 0.0f),
            string.Empty,
            Colors.White,
            0.0f,
            1.0f,
            material);
    }

    private void AddRailVisuals(string prefix, Vector3 start, Vector3 end, Color color, int coilCount)
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        Vector3 path = end - start;
        float length = path.Length();
        Vector3 direction = path.Normalized();
        Vector3 center = (start + end) * 0.5f;
        Basis pathBasis = new(new Quaternion(Vector3.Back, direction));
        Vector3 lateral = pathBasis.X.Normalized();

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            MeshInstance3D beam = RoomGeometry.AddVisualBox(
                this,
                $"{prefix}GuideBeam{(side < 0.0f ? "Left" : "Right")}",
                new Vector3(0.18f, 0.18f, length),
                center - (Vector3.Up * 0.48f) + (lateral * side * 0.42f),
                Vector3.Zero,
                metal,
                color.Lerp(Colors.White, 0.18f),
                0.44f,
                0.55f);
            beam.Basis = pathBasis;
        }

    }

    private void AddSequencePlate(string name, int index, Vector3 position)
    {
        RouteCheckpoint3D plate = new()
        {
            Name = name,
            CheckpointIndex = index,
            Position = position,
            TriggerSize = new Vector3(5.0f, 1.5f, 4.2f),
            FrameTint = RoomGeometry.SequenceButtonFrameTint,
            FlatFloorMarker = true,
            FloorMarkerInset = 0.10f,
        };
        plate.Entered += (entered, player) =>
        {
            if (player != _player)
            {
                return;
            }
            if (entered.CheckpointIndex != _nextSequencePlate) { entered.FlashDenied(); return; }
            entered.Activate();
            _nextSequencePlate++;
            if (_runSolutionSmoke || _runCorrectionSmoke)
            {
                GD.Print($"ROOM14_PLATE_PASS: plate={_nextSequencePlate}/{_sequencePlates.Count}, tick={_solutionTick}, position={player.GlobalPosition}.");
            }
        };
        AddChild(plate);
        _sequencePlates.Add(plate);

        MeshInstance3D inset = plate.GetNode<MeshInstance3D>("InsetPlate");
        RoomGeometry.AddSequencePips(inset, index + 1);
    }

    private void BuildGoal()
    {
        // The door plane now sits directly in the carved front shell wall at
        // Z=-150.  Keeping the goal 1.08 m inside matches AddGoalExitDoor and
        // removes the former pocket of wall geometry in front of the frame.
        Vector3 goalPosition = new(5.5f, 16.9f, -148.92f);
        Area3D goal = new()
        {
            Name = "GoalCup",
            Position = goalPosition,
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true,
        };
        goal.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 2.0f, Height = 2.7f } });
        goal.BodyEntered += body =>
        {
            if (body is PlayerBall)
            {
                TryCompleteGoal();
            }
        };
        AddChild(goal);
        RoomGeometry.AddGoalExitDoor(this, goalPosition);
    }

    private void TryCompleteGoal()
    {
        if (!RouteRequirementsMet() || !_goalOverlapsPlayer())
        {
            return;
        }

        if (CleanRouteMet())
        {
            MarkAdvancementCondition("perfect-switch");
        }
        CompleteRoom();
    }

    private bool _goalOverlapsPlayer()
    {
        Area3D? goal = GetNodeOrNull<Area3D>("GoalCup");
        return goal is not null && goal.GetOverlappingBodies().Contains(_player);
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM14_SOLUTION_FAIL: {message}");
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
        ResetRails();
        if (_railAudio is not null)
        {
            _railAudio.Stop();
            _railAudio.Stream = null;
        }
        _solutionTrace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(exitCode);
    }
}
