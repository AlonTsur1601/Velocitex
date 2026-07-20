using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Physics;
using Velocitex.Core.Rooms;
using Velocitex.Gameplay.Camera;
using Velocitex.Gameplay.Physics;
using Velocitex.Gameplay.Player;
using Velocitex.UI.Visual;

namespace Velocitex.Gameplay.Rooms;

public partial class Room15Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_15_solution.tres";
    private const int RequiredSolutionRuns = 10;
    private const int MaximumSolutionTicksPerRun = 2400;
    private const int RequiredFullRailTicks = 88;

    private readonly List<FlightGate3D> _lowGravityGates = new();
    private readonly List<Node3D> _fanRotors = new();
    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private ForceVolume3D _lowGravity = null!;
    private ForceVolume3D _wind = null!;
    private MomentumRail3D _correctRail = null!;
    private MomentumRail3D _recoveryRail = null!;
    private MomentumRail3D _correctionRail = null!;
    private RouteCheckpoint3D _windAlignmentPad = null!;
    private RouteCheckpoint3D _finalLatch = null!;
    private AudioStreamPlayer3D? _deviceAudio;
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private bool _touchedLowGravity;
    private bool _touchedWind;
    private bool _alignedForWind;
    private bool _releasedAnyRail;
    private bool _usedCorrectRail;
    private bool _usedRecoveryRoute;
    private bool _perfectSwitchEligible;
    private bool _runSolutionSmoke;
    private bool _runMechanicsSmoke;
    private bool _runAchievementPositiveSmoke;
    private bool _runAchievementNegativeSmoke;
    private bool _runPreview;
    private bool _runShellSmoke;
    private bool _solutionSmokeFinishing;
    private int _nextLowGravityGate;
    private int _correctRailAttachCount;
    private int _correctRailTicks;
    private int _solutionRun;
    private int _solutionTick;
    private int _previewFrames;
    private int _shellSmokeTick;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] arguments = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(arguments, value => value == "--room15-solution-smoke");
        _runMechanicsSmoke = Array.Exists(arguments, value => value == "--room15-mechanics-smoke");
        _runAchievementPositiveSmoke = Array.Exists(arguments, value => value == "--room15-achievement-positive-smoke");
        _runAchievementNegativeSmoke = Array.Exists(arguments, value => value == "--room15-achievement-negative-smoke");
        _runPreview = Array.Exists(arguments, value => value == "--room15-preview");
        _runShellSmoke = Array.Exists(arguments, value => value == "--room-shell-smoke");

        BuildRoom();
        BuildGoal();
        PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room15_a", new Vector3(14.5f, 24.0f, 52.0f), new Vector3(0.0f, 7.0f, -42.0f), 59.0f),
            new("room15_b", new Vector3(-16.0f, 24.0f, -112.0f), new Vector3(1.0f, 13.0f, -145.0f), 58.0f),
        });

        _player = GetNode<PlayerBall>("Player");
        _cameraRig = GetNode<PlayerCameraRig>("CameraRig");
        _spawnTransform = _player.GlobalTransform;
        _cameraRig.Follow(_player);
        _player.MovementBasis = _cameraRig.MovementBasis;
        if (_runPreview) _cameraRig.SetInputEnabled(false);

        _lowGravity.RigidBodyEntered += OnLowGravityEntered;
        _wind.RigidBodyEntered += OnWindEntered;
        BindRail(_correctRail, correct: true, recovery: false);
        BindRail(_recoveryRail, correct: false, recovery: true);
        BindRail(_correctionRail, correct: false, recovery: true);

        if (_runSolutionSmoke)
        {
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            if (_solutionTrace is null ||
                _solutionTrace.RoomId != RoomId ||
                _solutionTrace.MoveInputs.Count < 5 ||
                _solutionTrace.MoveDurationsTicks.Length != _solutionTrace.MoveInputs.Count ||
                !_solutionTrace.MoveInputs.Any(input => input.X < -0.25f) ||
                !_solutionTrace.MoveInputs.Any(input => input.X > 0.25f))
            {
                FailSolutionSmoke("The SolutionTrace must steer both ways through low gravity, align against wind and choose a rail.");
            }
        }

        if (_runMechanicsSmoke || _runAchievementPositiveSmoke || _runAchievementNegativeSmoke)
        {
            CallDeferred(MethodName.RunRequestedSmoke);
        }
    }

    public override void _Process(double delta)
    {
        float rotorStep = (float)delta * 7.5f;
        foreach (Node3D rotor in _fanRotors)
        {
            rotor.RotateObjectLocal(Vector3.Right, rotorStep);
        }

        if (_runPreview && ++_previewFrames >= 30)
        {
            string path = ProjectSettings.GlobalizePath("user://room15-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"ROOM15_PREVIEW_CAPTURE: {path}");
            GetTree().Quit(0);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        TrackEnvironmentalContacts();
        if (_runShellSmoke) { RunShellSmokeTick(); return; }
        if (_runMechanicsSmoke || _runAchievementPositiveSmoke || _runAchievementNegativeSmoke) return;
        if (_runSolutionSmoke) { RunSolutionTick(); return; }
        TrackRailRide();
        if (_player.GlobalPosition.Y < -7.0f) RestartRoom();
    }

    public override void RestartRoom()
    {
        if (_runSolutionSmoke && _solutionTick > 0 && !_solutionSmokeFinishing)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} fell at {_player.GlobalPosition}; low={_nextLowGravityGate}/{_lowGravityGates.Count}, wind={_touchedWind}/{_alignedForWind}, correct={_usedCorrectRail}, recovery={_usedRecoveryRoute}, rail_ticks={_correctRailTicks}.");
            return;
        }
        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        ResetRails();
        _player.ResetTo(_spawnTransform);
        ResetExamState();
    }

    private void RunShellSmokeTick()
    {
        _shellSmokeTick++;
        if (_shellSmokeTick == 1)
        {
            _player.ResetTo(new Transform3D(Basis.Identity, GetNode<Area3D>("RoomShell/HazardTrigger").GlobalPosition));
            return;
        }
        if (_shellSmokeTick < 12) return;
        if (_player.GlobalPosition.DistanceTo(_spawnTransform.Origin) > 0.15f)
        {
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 15 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }
        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 15 hazard floor restarted the player.");
        GetTree().Quit(0);
    }

    private void RunSolutionTick()
    {
        if (_solutionTrace is null || _solutionSmokeFinishing) return;
        TrackRailRide();
        if (IsComplete)
        {
            if (!CanCompleteExam() || !_usedCorrectRail || _usedRecoveryRoute || _correctRailTicks < RequiredFullRailTicks)
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} bypassed the chapter exam: low={_nextLowGravityGate}/{_lowGravityGates.Count}, wind={_touchedWind}/{_alignedForWind}, correct={_usedCorrectRail}, recovery={_usedRecoveryRoute}, released={_releasedAnyRail}, rail_ticks={_correctRailTicks}.");
                return;
            }
            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print($"ROOM15_SOLUTION_PASS: SolutionTrace steered through all {_lowGravityGates.Count} low-gravity rings, aligned against wind and completed the full correct rail for {_solutionRun} consecutive chapter-exam runs.");
                FinishSolutionSmoke(0);
                return;
            }
            ClearCompletionState();
            ResetRails();
            _player.ResetTo(_spawnTransform);
            _solutionTick = 0;
            ResetExamState();
            return;
        }

        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} timed out at {_player.GlobalPosition}; low={_nextLowGravityGate}/{_lowGravityGates.Count}, wind={_touchedWind}/{_alignedForWind}, correct={_usedCorrectRail}, recovery={_usedRecoveryRoute}, rail_ticks={_correctRailTicks}.");
            return;
        }
        _player.SimulatedMoveInput = ResolveTraceInput(_solutionTick - 1);
    }

    private void RunRequestedSmoke()
    {
        if (_runMechanicsSmoke)
        {
            if (_lowGravityGates.Count != 6 || _fanRotors.Count < 3 || CountPhysicalRailBeams() < 6)
            {
                FailSmoke("ROOM15_MECHANICS_FAIL", "the room is missing its six steering rings, rotating fan bank or physical rail pairs.");
                return;
            }
            (string Name, Action RemoveEvidence)[] negativeCases =
            {
                ("low-gravity contact", () => _touchedLowGravity = false),
                ("both low-gravity steering gates", () => _nextLowGravityGate = _lowGravityGates.Count - 1),
                ("wind alignment", () => _alignedForWind = false),
                ("wind contact", () => _touchedWind = false),
                ("a completed rail", () => { _usedCorrectRail = false; _usedRecoveryRoute = false; }),
                ("rail release", () => _releasedAnyRail = false),
                ("final landing latch", () => _finalLatch.ResetCheckpoint()),
            };
            foreach ((string name, Action removeEvidence) in negativeCases)
            {
                SetCompletePrerequisites();
                removeEvidence();
                if (CanCompleteExam())
                {
                    FailSmoke("ROOM15_MECHANICS_FAIL", $"the exam completed without {name}.");
                    return;
                }
            }
            GD.Print("ROOM15_MECHANICS_PASS: low gravity, wind alignment, physical rail release and final latch are independently required; fan and rail geometry is present.");
            FinishSmoke(0);
            return;
        }

        if (_runAchievementNegativeSmoke)
        {
            SetCompletePrerequisites();
            _correctRailAttachCount = 1;
            _correctRailTicks = RequiredFullRailTicks - 1;
            _perfectSwitchEligible = true;
            TryAwardPerfectSwitch();
            if (CompletedAdvancementIds.Contains("perfect-switch"))
            {
                FailSmoke("ROOM15_ACHIEVEMENT_FAIL", "a short correct-rail touch incorrectly awarded Perfect Switch.");
                return;
            }

            ClearCompletionState();
            SetCompletePrerequisites();
            _correctRailAttachCount = 2;
            _correctRailTicks = RequiredFullRailTicks + 12;
            _perfectSwitchEligible = false;
            TryAwardPerfectSwitch();
            if (CompletedAdvancementIds.Contains("perfect-switch"))
            {
                FailSmoke("ROOM15_ACHIEVEMENT_FAIL", "reattaching to the correct rail incorrectly awarded Perfect Switch.");
                return;
            }

            ClearCompletionState();
            SetCompletePrerequisites();
            _correctRailAttachCount = 1;
            _correctRailTicks = RequiredFullRailTicks + 12;
            _usedRecoveryRoute = true;
            _perfectSwitchEligible = false;
        }
        else
        {
            SetCompletePrerequisites();
            _correctRailAttachCount = 1;
            _correctRailTicks = RequiredFullRailTicks + 12;
            _perfectSwitchEligible = true;
        }
        TryAwardPerfectSwitch();
        bool awarded = CompletedAdvancementIds.Contains("perfect-switch");
        if (_runAchievementPositiveSmoke && !awarded)
        {
            FailSmoke("ROOM15_ACHIEVEMENT_FAIL", "a full first-choice correct rail ride did not award Perfect Switch.");
            return;
        }
        if (_runAchievementNegativeSmoke && awarded)
        {
            FailSmoke("ROOM15_ACHIEVEMENT_FAIL", "the recovery/switch route incorrectly awarded Perfect Switch.");
            return;
        }
        GD.Print(_runAchievementPositiveSmoke
            ? "ROOM15_ACHIEVEMENT_POSITIVE_PASS: a full first-choice correct rail ride awarded Perfect Switch."
            : "ROOM15_ACHIEVEMENT_NEGATIVE_PASS: a short ride, correct-rail reattachment and the recovery route all denied Perfect Switch.");
        FinishSmoke(0);
    }

    private void SetCompletePrerequisites()
    {
        _touchedLowGravity = true;
        _nextLowGravityGate = _lowGravityGates.Count;
        _alignedForWind = true;
        _touchedWind = true;
        _usedCorrectRail = true;
        _releasedAnyRail = true;
        _finalLatch.Activate();
    }

    private int CountPhysicalRailBeams() => GetChildren().OfType<StaticBody3D>().Count(body => body.Name.ToString().StartsWith("PhysicalRailBeam", StringComparison.Ordinal));

    private void TrackRailRide()
    {
        if (_correctRail.IsAttached(_player)) _correctRailTicks++;
    }

    private void TrackEnvironmentalContacts()
    {
        if (_lowGravity.ContainsBody(_player)) _touchedLowGravity = true;
        if (_wind.ContainsBody(_player)) _touchedWind = true;
    }

    private void BindRail(MomentumRail3D rail, bool correct, bool recovery)
    {
        rail.Attached += body =>
        {
            if (body != _player) return;
            if (correct)
            {
                _usedCorrectRail = true;
                _correctRailAttachCount++;
                if (_correctRailAttachCount == 1 && !_usedRecoveryRoute) _perfectSwitchEligible = true;
            }
            if (recovery)
            {
                _usedRecoveryRoute = true;
                _perfectSwitchEligible = false;
            }
            PlayDeviceSound(body.GlobalPosition);
        };
        rail.Released += body => { if (body == _player) _releasedAnyRail = true; };
    }

    private bool CanCompleteExam() =>
        _touchedLowGravity &&
        _nextLowGravityGate == _lowGravityGates.Count &&
        _alignedForWind &&
        _touchedWind &&
        (_usedCorrectRail || _usedRecoveryRoute) &&
        _releasedAnyRail &&
        _finalLatch.IsActivated;

    private void TryAwardPerfectSwitch()
    {
        if (_perfectSwitchEligible && _usedCorrectRail && !_usedRecoveryRoute && _correctRailAttachCount == 1 && _correctRailTicks >= RequiredFullRailTicks)
        {
            MarkAdvancementCondition("perfect-switch");
        }
    }

    private Vector2 ResolveTraceInput(int tick)
    {
        if (_solutionTrace is null) return Vector2.Zero;
        int remaining = tick;
        for (int index = 0; index < _solutionTrace.MoveInputs.Count; index++)
        {
            int duration = _solutionTrace.MoveDurationsTicks[index];
            if (remaining < duration) return _solutionTrace.MoveInputs[index];
            remaining -= duration;
        }
        return _solutionTrace.HoldLastInput ? _solutionTrace.MoveInputs[^1] : Vector2.Zero;
    }

    private void ResetExamState()
    {
        _touchedLowGravity = false;
        _touchedWind = false;
        _alignedForWind = false;
        _releasedAnyRail = false;
        _usedCorrectRail = false;
        _usedRecoveryRoute = false;
        _perfectSwitchEligible = false;
        _nextLowGravityGate = 0;
        _correctRailAttachCount = 0;
        _correctRailTicks = 0;
        foreach (FlightGate3D gate in _lowGravityGates) gate.ResetGate();
        _windAlignmentPad.ResetCheckpoint();
        _finalLatch.ResetCheckpoint();
        _deviceAudio?.Stop();
    }

    private void ResetRails()
    {
        _correctRail.ResetBody(_player);
        _recoveryRail.ResetBody(_player);
        _correctionRail.ResetBody(_player);
    }

    private void PlayDeviceSound(Vector3 position)
    {
        if (_deviceAudio is null) return;
        _deviceAudio.GlobalPosition = position;
        _deviceAudio.Play();
    }

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string copper = "res://assets/textures/copper_rivets.svg";
        Color pale = new("abb9b4");
        Color frame = new("485e59");
        RoomGeometry.AddClosedRoomShell(this, "RoomShell", new Vector3(0.0f, 0.0f, -85.0f), new Vector2(40.0f, 300.0f), -3.0f, 40.0f, metal, new Color("687c76"), new Color("3f504d"), body => { if (body is PlayerBall) RestartRoom(); });

        RoomGeometry.AddBox(this, "SafeStart", new Vector3(14.0f, 0.5f, 26.075f), new Vector3(0.0f, 12.0f, 51.7375f), Vector3.Zero, metal, pale, 0.4f, 0.66f);
        AddSlopeBetween("LowGravityApproachSlope", 14.0f, 38.7f, 12.25f, 23.7f, 8.25f, copper, new Color("71837b"));
        AddSlopeBetween("LowGravityLaunchRamp", 14.0f, 23.7f, 8.25f, 18.0f, 11.25f, metal, pale.Darkened(0.03f));
        RoomGeometry.AddBox(this, "LowGravityLandingDeck", new Vector3(18.0f, 0.5f, 20.0f), new Vector3(0.0f, 5.0f, -91.0f), Vector3.Zero, metal, new Color("a5b9b2"), 0.4f, 0.66f);
        RoomGeometry.AddBox(this, "WindPreparationDeck", new Vector3(18.0f, 0.5f, 16.0f), new Vector3(0.0f, 5.0f, -109.0f), Vector3.Zero, copper, new Color("71858a"), 0.38f, 0.6f);
        AddSlopeBetween("WindLaunchSlope", 18.0f, -117.0f, 5.25f, -125.0f, 10.25f, metal, new Color("8aa1a4"));
        RoomGeometry.AddBox(this, "RailChoiceYard", new Vector3(22.0f, 0.5f, 18.0f), new Vector3(0.0f, 4.0f, -151.0f), Vector3.Zero, copper, new Color("747f78"), 0.38f, 0.6f);
        RoomGeometry.AddBox(this, "RecoveryDeck", new Vector3(9.0f, 0.5f, 18.0f), new Vector3(9.0f, 9.5f, -188.0f), Vector3.Zero, metal, new Color("87918e"), 0.4f, 0.64f);
        RoomGeometry.AddBox(this, "FinalDeck", new Vector3(16.0f, 0.5f, 32.7f), new Vector3(0.0f, 18.5f, -218.35f), Vector3.Zero, metal, pale, 0.4f, 0.66f);

        AddGroundedSideWalls(metal, copper, frame);

        _lowGravity = AddForceVolume("LowGravityVolume", new Vector3(0.0f, 13.0f, -31.0f), new Vector3(38.0f, 28.0f, 104.0f), "res://resources/force_volumes/low_gravity.tres");
        _wind = AddForceVolume("WindVolume", new Vector3(0.0f, 8.0f, -133.0f), new Vector3(32.0f, 18.0f, 24.0f), "res://resources/force_volumes/crosswind.tres");
        AddLowGravityParticles();
        AddWindParticles();
        AddLowGravityGate(0, new Vector3(-3.2f, 17.0f, 2.0f), 3.0f);
        AddLowGravityGate(1, new Vector3(3.2f, 17.4f, -24.0f), 3.0f);
        AddLowGravityGate(2, new Vector3(-1.0f, 13.2f, -40.0f), 3.3f);
        AddLowGravityGate(3, new Vector3(2.0f, 11.5f, -55.0f), 3.3f);
        AddLowGravityGate(4, new Vector3(-1.0f, 9.7f, -69.0f), 3.3f);
        AddLowGravityGate(5, new Vector3(1.5f, 6.4f, -79.0f), 3.3f);

        _windAlignmentPad = AddCheckpoint("WindAlignmentPad", 0, new Vector3(-5.0f, 5.95f, -112.0f), new Vector3(4.0f, 1.6f, 4.0f), new Color("78a1a9"));
        _windAlignmentPad.Entered += (pad, player) => { if (player == _player) { pad.Activate(); _alignedForWind = true; } };

        _correctRail = AddRail("CorrectRail", new Vector3(-1.0f, 4.8f, -158.0f), new Vector3(-2.0f, 19.65f, -201.0f), 12.5f, new Color("8ec4a9"), 0);
        _recoveryRail = AddRail("RecoveryRail", new Vector3(5.0f, 4.8f, -158.0f), new Vector3(9.0f, 10.2f, -179.0f), 11.0f, new Color("bd886c"), 2);
        _correctionRail = AddRail("CorrectionRail", new Vector3(9.0f, 10.2f, -196.0f), new Vector3(4.0f, 19.65f, -201.0f), 11.0f, new Color("9b789d"), 4);

        // Catch rail exits above the front of the final deck.  The field begins
        // at the deck lip and spans all the way to the ceiling so the pink
        // recovery rail cannot carry the ball over the platform.
        AddForceVolume(
            "FinalDeckStrongGravityCatch",
            new Vector3(0.0f, 29.35f, -205.0f),
            new Vector3(17.4f, 21.0f, 8.0f),
            "res://resources/force_volumes/strong_gravity.tres");

        _finalLatch = AddCheckpoint("FinalRailLatch", 1, new Vector3(0.0f, 19.45f, -216.0f), new Vector3(11.0f, 2.2f, 8.0f), new Color("7b8f84"));
        _finalLatch.Entered += (latch, player) =>
        {
            if (player != _player) { return; }
            if (_usedCorrectRail || _usedRecoveryRoute) { latch.Activate(); return; }
            latch.FlashDenied();
        };

        AddFanBank();
        if (!_runSolutionSmoke && !_runMechanicsSmoke && !_runAchievementPositiveSmoke && !_runAchievementNegativeSmoke && !_runShellSmoke)
        {
            _deviceAudio = new AudioStreamPlayer3D { Name = "ExamDeviceSfx", Stream = GD.Load<AudioStream>("res://assets/audio/sfx/device_rail_attach.wav"), Bus = "SFX", MaxDistance = 42.0f, UnitSize = 8.0f };
            AddChild(_deviceAudio);
        }
    }

    private void AddGroundedSideWalls(string metal, string copper, Color frame)
    {
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(this, $"StartSideWall{side}", new Vector3(0.36f, 1.45f, 26.075f), new Vector3(side * 7.18f, 12.725f, 51.7375f), Vector3.Zero, metal, frame, 0.42f, 0.62f);
            AddSlopeWall($"ApproachSideWall{side}", side * 7.18f, 38.7f, 12.25f, 23.7f, 8.25f, metal, frame);
            AddSlopeWall($"LowLaunchSideWall{side}", side * 7.18f, 23.7f, 8.25f, 18.0f, 11.25f, metal, frame);
            RoomGeometry.AddBox(this, $"LowLandingSideWall{side}", new Vector3(0.36f, 1.45f, 20.0f), new Vector3(side * 9.18f, 5.725f, -91.0f), Vector3.Zero, copper, frame, 0.42f, 0.6f);
            RoomGeometry.AddBox(this, $"WindPrepSideWall{side}", new Vector3(0.36f, 1.45f, 16.0f), new Vector3(side * 9.18f, 5.725f, -109.0f), Vector3.Zero, metal, frame, 0.42f, 0.62f);
            AddSlopeWall($"WindSlopeSideWall{side}", side * 9.18f, -117.0f, 5.25f, -125.0f, 10.25f, copper, frame);
            RoomGeometry.AddBox(this, $"ChoiceSideWall{side}", new Vector3(0.36f, 1.45f, 18.0f), new Vector3(side * 11.18f, 4.725f, -151.0f), Vector3.Zero, metal, frame, 0.42f, 0.62f);
            RoomGeometry.AddBox(this, $"FinalSideWall{side}", new Vector3(0.36f, 1.45f, 32.7f), new Vector3(side * 8.18f, 19.225f, -218.35f), Vector3.Zero, copper, frame, 0.42f, 0.6f);
        }
        RoomGeometry.AddBox(this, "RecoveryOuterSideWall", new Vector3(0.36f, 1.45f, 18.0f), new Vector3(13.68f, 10.225f, -188.0f), Vector3.Zero, copper, frame, 0.42f, 0.6f);
    }

    private StaticBody3D AddSlopeBetween(string name, float width, float backZ, float backTopY, float frontZ, float frontTopY, string texture, Color tint)
    {
        float run = backZ - frontZ;
        float rise = backTopY - frontTopY;
        float angle = -Mathf.Atan2(rise, run);
        float length = Mathf.Sqrt(run * run + rise * rise);
        const float thickness = 0.5f;
        Vector3 up = new Basis(Vector3.Right, angle) * Vector3.Up;
        Vector3 topCenter = new(0.0f, (backTopY + frontTopY) * 0.5f, (backZ + frontZ) * 0.5f);
        return RoomGeometry.AddBox(this, name, new Vector3(width, thickness, length), topCenter - up * thickness * 0.5f, new Vector3(angle, 0.0f, 0.0f), texture, tint, 0.4f, 0.62f);
    }

    private void AddSlopeWall(string name, float x, float backZ, float backTopY, float frontZ, float frontTopY, string texture, Color tint)
    {
        float run = backZ - frontZ;
        float rise = backTopY - frontTopY;
        float angle = -Mathf.Atan2(rise, run);
        float length = Mathf.Sqrt(run * run + rise * rise);
        Vector3 up = new Basis(Vector3.Right, angle) * Vector3.Up;
        Vector3 topCenter = new(x, (backTopY + frontTopY) * 0.5f + 0.725f, (backZ + frontZ) * 0.5f);
        RoomGeometry.AddBox(this, name, new Vector3(0.36f, 1.45f, length), topCenter - up * 0.725f, new Vector3(angle, 0.0f, 0.0f), texture, tint, 0.42f, 0.6f);
    }

    private ForceVolume3D AddForceVolume(string name, Vector3 position, Vector3 size, string profilePath)
    {
        ForceVolume3D volume = new() { Name = name, Position = position, CollisionMask = 1, Profile = GD.Load<ForceVolumeProfile>(profilePath) };
        volume.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(volume);
        return volume;
    }

    private void AddLowGravityGate(int index, Vector3 position, float radius)
    {
        FlightGate3D gate = new()
        {
            Name = $"LowGravitySteeringGate{index + 1}",
            Position = position,
            Radius = radius,
            OpeningContactMargin = index >= 4 ? 0.5f : 0.0f,
            FrameTint = index == 0 ? new Color("78a99c") : new Color("b4946b"),
            EnableAudio = !_runSolutionSmoke && !_runMechanicsSmoke && !_runAchievementPositiveSmoke && !_runAchievementNegativeSmoke,
            MinimumExitSpeed = 0.0f,
            SpeedGain = 0.0f,
            SpeedMultiplier = 1.0f,
            AxialBoostOnly = true,
            MaximumDownwardExitSpeed = 0.0f,
        };
        gate.Passed += player =>
        {
            if (player == _player && gate == _lowGravityGates[_nextLowGravityGate])
            {
                _nextLowGravityGate++;
                if (_runSolutionSmoke) GD.Print($"ROOM15_GATE_PASS: gate={_nextLowGravityGate}, tick={_solutionTick}, position={_player.GlobalPosition}.");
            }
        };
        AddChild(gate);
        _lowGravityGates.Add(gate);
    }

    private RouteCheckpoint3D AddCheckpoint(string name, int index, Vector3 position, Vector3 size, Color tint)
    {
        RouteCheckpoint3D checkpoint = new() { Name = name, CheckpointIndex = index, Position = position, TriggerSize = size, FrameTint = tint, FlatFloorMarker = true };
        AddChild(checkpoint);
        return checkpoint;
    }

    private MomentumRail3D AddRail(string name, Vector3 start, Vector3 end, float minimumSpeed, Color tint, int beamIndexOffset)
    {
        MomentumRail3D rail = new() { Name = name, LocalStart = start, LocalEnd = end, CaptureRadius = 2.05f, MinimumSpeed = minimumSpeed, CollisionMask = 1 };
        AddChild(rail);
        AddRailVisualsAndCollision(start, end, tint, beamIndexOffset);
        return rail;
    }

    private void AddRailVisualsAndCollision(Vector3 start, Vector3 end, Color tint, int beamIndexOffset)
    {
        Vector3 path = end - start;
        float length = path.Length();
        Vector3 direction = path.Normalized();
        Vector3 center = (start + end) * 0.5f;
        Basis pathBasis = new(new Quaternion(Vector3.Back, direction));
        Vector3 lateral = pathBasis.X.Normalized();
        StandardMaterial3D beamMaterial = RoomGeometry.CreateMaterial("res://assets/textures/brushed_metal.png", tint, 0.4f, 0.62f);
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            StaticBody3D beam = new() { Name = $"PhysicalRailBeam{beamIndexOffset + (side > 0 ? 1 : 0)}", Position = center - (Vector3.Up * 0.48f) + lateral * side * 0.42f, Basis = pathBasis };
            beam.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.18f, length) }, MaterialOverride = beamMaterial });
            beam.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.18f, 0.18f, length) } });
            AddChild(beam);
        }
    }

    private void AddFanBank()
    {
        StandardMaterial3D metal = RoomGeometry.CreateMaterial("res://assets/textures/brushed_metal.png", new Color("75949a"), 0.4f, 0.64f);
        StandardMaterial3D blades = RoomGeometry.CreateMaterial("res://assets/textures/copper_rivets.svg", new Color("b6d7da"), 0.34f, 0.58f);
        foreach ((float y, float z) in new[] { (7.0f, -128.0f), (13.0f, -133.0f), (7.0f, -138.0f) })
        {
            Node3D rotor = new() { Name = $"WindFanRotor{_fanRotors.Count + 1}", Position = new Vector3(-17.0f, y, z) };
            RoomGeometry.AddVisualBox(rotor, "Hub", new Vector3(1.1f, 0.8f, 0.8f), Vector3.Zero, Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, metal);
            for (int bladeIndex = 0; bladeIndex < 4; bladeIndex++)
            {
                Node3D bladeRoot = new() { Name = $"Blade{bladeIndex + 1}", Rotation = new Vector3((bladeIndex * Mathf.Pi) / 2.0f, 0.0f, 0.0f) };
                RoomGeometry.AddVisualBox(bladeRoot, "BladeMesh", new Vector3(0.18f, 2.8f, 0.62f), new Vector3(0.0f, 1.55f, 0.0f), new Vector3(0.0f, 0.0f, Mathf.DegToRad(12.0f)), string.Empty, Colors.White, 0.0f, 1.0f, blades);
                rotor.AddChild(bladeRoot);
            }
            AddChild(rotor);
            _fanRotors.Add(rotor);
        }
    }

    private void AddLowGravityParticles()
    {
        StandardMaterial3D material = new() { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color("c6eadc"), EmissionEnabled = true, Emission = new Color("659b88") };
        ParticleProcessMaterial process = new() { EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(11.0f, 12.0f, 51.0f), Direction = Vector3.Up, Spread = 15.0f, Gravity = Vector3.Zero, InitialVelocityMin = 0.3f, InitialVelocityMax = 0.8f };
        AddChild(new GpuParticles3D { Name = "LowGravityMotes", Position = new Vector3(0.0f, 13.0f, -31.0f), Amount = 126, Lifetime = 7.0, Randomness = 0.8f, ProcessMaterial = process, DrawPass1 = new SphereMesh { Radius = 0.05f, Height = 0.1f, RadialSegments = 8, Rings = 4, Material = material } });
    }

    private void AddWindParticles()
    {
        StandardMaterial3D material = new() { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color("d8edf2"), EmissionEnabled = true, Emission = new Color("7fb3c2") };
        ParticleProcessMaterial process = new() { EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(15.0f, 9.0f, 11.0f), Direction = Vector3.Right, Spread = 7.0f, Gravity = Vector3.Zero, InitialVelocityMin = 5.5f, InitialVelocityMax = 8.0f };
        AddChild(new GpuParticles3D { Name = "WindStreaks", Position = new Vector3(0.0f, 8.0f, -133.0f), Amount = 86, Lifetime = 2.0, Randomness = 0.7f, ProcessMaterial = process, DrawPass1 = new BoxMesh { Size = new Vector3(0.9f, 0.035f, 0.035f), Material = material } });
    }

    private void BuildGoal()
    {
        Vector3 goalPosition = new(0.0f, 19.85f, -233.62f);
        Area3D goal = new() { Name = "GoalCup", Position = goalPosition, CollisionLayer = 0, CollisionMask = 1, Monitoring = true };
        goal.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 1.8f, Height = 2.7f } });
        goal.BodyEntered += body =>
        {
            if (body is not PlayerBall || !CanCompleteExam()) return;
            CompleteRoom();
        };
        AddChild(goal);
        RoomGeometry.AddGoalExitDoor(this, goalPosition);
    }

    private void OnLowGravityEntered(RigidBody3D body)
    {
        if (body == _player) { _touchedLowGravity = true; PlayDeviceSound(body.GlobalPosition); }
    }

    private void OnWindEntered(RigidBody3D body)
    {
        if (body == _player) { _touchedWind = true; PlayDeviceSound(body.GlobalPosition); }
    }

    private void FailSmoke(string marker, string message)
    {
        GD.PushError($"{marker}: {message}");
        FinishSmoke(1);
    }

    private async void FinishSmoke(int exitCode)
    {
        SceneTree tree = GetTree();
        _player.SimulatedMoveInput = null;
        ResetRails();
        QueueFree();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        tree.Quit(exitCode);
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM15_SOLUTION_FAIL: {message}");
        FinishSolutionSmoke(1);
    }

    private async void FinishSolutionSmoke(int exitCode)
    {
        if (_solutionSmokeFinishing) return;
        _solutionSmokeFinishing = true;
        _player.SimulatedMoveInput = null;
        ResetRails();
        if (_deviceAudio is not null) { _deviceAudio.Stop(); _deviceAudio.Stream = null; }
        _solutionTrace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(exitCode);
    }

    public override void _ExitTree()
    {
        if (_lowGravity is not null) _lowGravity.RigidBodyEntered -= OnLowGravityEntered;
        if (_wind is not null) _wind.RigidBodyEntered -= OnWindEntered;
        if (_deviceAudio is not null) { _deviceAudio.Stop(); _deviceAudio.Stream = null; }
        _solutionTrace = null;
    }
}
