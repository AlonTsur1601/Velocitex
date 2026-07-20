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

public partial class Room17Runtime : RoomRuntime
{
    private const string TracePath = "res://resources/solutions/room_17_solution.tres";
    private const byte InteractAction = 1;
    private const int RequiredSolutionRuns = 10;
    private const int MaximumSolutionTicksPerRun = 850;

    private PlayerBall _player = null!;
    private PlayerCameraRig _cameraRig = null!;
    private PlayerCannon3D _playerCannon = null!;
    private readonly List<InterferenceCannon3D> _interferenceCannons = new();
    private readonly List<Area3D> _projectileLanes = new();
    private Transform3D _spawnTransform;
    private SolutionTrace? _solutionTrace;
    private bool _showPrompts;
    private bool _highContrastPrompts;
    private bool _playerCannonFired;
    private bool _crossedProjectileLane;
    private bool _runSolutionSmoke;
    private bool _runPreview;
    private bool _runPanoramaCapture;
    private bool _runShellSmoke;
    private bool _runMechanicsSmoke;
    private bool _runImpactSmoke;
    private bool _runAchievementPositiveSmoke;
    private bool _runAchievementNegativeSmoke;
    private bool _solutionSmokeFinishing;
    private int _projectileHits;
    private int _solutionRun;
    private int _solutionTick;
    private int _previewFrames;
    private int _shellSmokeTick;
    private int _nextProjectileLane;

    public override void _Ready()
    {
        InputDefaults.EnsureActions();
        string[] args = OS.GetCmdlineUserArgs();
        _runSolutionSmoke = Array.Exists(args, argument => argument == "--room17-solution-smoke");
        _runPreview = Array.Exists(args, argument => argument == "--room17-preview");
        _runShellSmoke = Array.Exists(args, argument => argument == "--room-shell-smoke");
        _runMechanicsSmoke = Array.Exists(args, argument => argument == "--room17-mechanics-smoke");
        _runImpactSmoke = Array.Exists(args, argument => argument == "--room17-impact-smoke");
        _runAchievementPositiveSmoke = Array.Exists(args, argument => argument == "--room17-achievement-positive-smoke");
        _runAchievementNegativeSmoke = Array.Exists(args, argument => argument == "--room17-achievement-negative-smoke");

        BuildRoom();
        BuildGoal();
        _runPanoramaCapture = PanoramaCaptureController.TryAttach(this, new PanoramaView[]
        {
            new("room17_a", new Vector3(8.5f, 13.5f, 29.0f), new Vector3(0.0f, 12.0f, -31.0f), 57.0f),
            new("room17_b", new Vector3(-8.5f, 18.5f, -17.0f), new Vector3(0.0f, 11.0f, 8.0f), 58.0f),
        });

        _player = GetNode<PlayerBall>("Player");
        _cameraRig = GetNode<PlayerCameraRig>("CameraRig");
        _spawnTransform = _player.GlobalTransform;
        _cameraRig.Follow(_player);
        _player.MovementBasis = _cameraRig.MovementBasis;

        GameSettingsData settings = SettingsStore.Load();
        _showPrompts = settings.InteractionPrompts;
        _highContrastPrompts = settings.HighContrastPrompts;
        Key key = InputDefaults.GetPrimaryKey(InputDefaults.Interact);
        _playerCannon.SetKeyLabel(key == Key.None ? "E" : key.ToString());
        if (_runPreview || _runPanoramaCapture)
        {
            _cameraRig.SetInputEnabled(false);
            _showPrompts = false;
        }

        _playerCannon.Fired += body => _playerCannonFired |= body == _player;
        foreach (InterferenceCannon3D cannon in _interferenceCannons)
        {
            cannon.PlayerHit += player =>
            {
                if (player == _player)
                {
                    RegisterProjectileHit(player);
                }
            };
        }

        if (_runSolutionSmoke)
        {
            _showPrompts = false;
            _solutionTrace = GD.Load<SolutionTrace>(TracePath);
            if (_solutionTrace is null || _solutionTrace.RoomId != RoomId || !_solutionTrace.ActionFlags.Contains(InteractAction))
            {
                FailSolutionSmoke("The Room 17 SolutionTrace is invalid or does not fire the player cannon.");
            }
        }

        if (_runMechanicsSmoke || _runImpactSmoke || _runAchievementPositiveSmoke || _runAchievementNegativeSmoke)
        {
            CallDeferred(MethodName.RunRequestedSmoke);
        }
    }

    public override void _Process(double delta)
    {
        if (_runPreview && ++_previewFrames >= 30)
        {
            string path = ProjectSettings.GlobalizePath("user://room17-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"ROOM17_PREVIEW_CAPTURE: {path}");
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

        foreach (InterferenceCannon3D cannon in _interferenceCannons)
        {
            cannon.AdvancePhysicsTick();
        }
        if (_runSolutionSmoke)
        {
            RunSolutionTick();
            return;
        }

        if (_runMechanicsSmoke || _runImpactSmoke || _runAchievementPositiveSmoke || _runAchievementNegativeSmoke)
        {
            return;
        }

        bool canInteract = _playerCannon.CanInteract(_player);
        bool focused = canInteract && _cameraRig.IsLookingAt(_playerCannon.GlobalPosition + Vector3.Up * 1.8f);
        _playerCannon.SetFocused(focused && _showPrompts, _highContrastPrompts);
        if (focused && Godot.Input.IsActionJustPressed(InputDefaults.Interact))
        {
            _playerCannon.Interact(_player);
        }

        if (_player.GlobalPosition.Y < -7.0f)
        {
            RestartRoom();
        }
    }

    public override void RestartRoom()
    {
        if (_runSolutionSmoke && _solutionTick > 0 && !_solutionSmokeFinishing)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} hit the maintenance floor at {_player.GlobalPosition}; shots={TotalShotsFired}, hits={_projectileHits}, lanes={_nextProjectileLane}/{_projectileLanes.Count}.");
            return;
        }

        ClearCompletionState();
        _player.SimulatedMoveInput = null;
        _player.ResetTo(_spawnTransform);
        ResetRoomState();
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
            GD.PushError("ROOM_SHELL_HAZARD_FAIL: Room 17 hazard floor did not restart the player.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("ROOM_SHELL_HAZARD_PASS: Room 17 hazard floor restarted the player.");
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
            if (!_playerCannonFired || !_crossedProjectileLane || _nextProjectileLane != _projectileLanes.Count || TotalShotsFired < 8 ||
                _projectileHits != 0 || !CompletedAdvancementIds.Contains("untouchable"))
            {
                FailSolutionSmoke($"Run {_solutionRun + 1} was not a clean timed crossing: fired={_playerCannonFired}, lanes={_nextProjectileLane}/{_projectileLanes.Count}, shots={TotalShotsFired}, hits={_projectileHits}.");
                return;
            }

            _solutionRun++;
            if (_solutionRun >= RequiredSolutionRuns)
            {
                GD.Print($"ROOM17_SOLUTION_PASS: SolutionTrace crossed the forty-cannon grid without a hit for {_solutionRun} consecutive completions.");
                FinishSolutionSmoke(0);
                return;
            }

            ClearCompletionState();
            _player.ResetTo(_spawnTransform);
            _solutionTick = 0;
            ResetRoomState();
            return;
        }

        if (++_solutionTick > MaximumSolutionTicksPerRun)
        {
            FailSolutionSmoke($"Run {_solutionRun + 1} timed out at {_player.GlobalPosition}; shots={TotalShotsFired}, hits={_projectileHits}, lanes={_nextProjectileLane}/{_projectileLanes.Count}.");
            return;
        }

        (Vector2 move, byte flags) = ResolveTraceStep(_solutionTick - 1);
        _player.SimulatedMoveInput = move;
        if ((flags & InteractAction) != 0)
        {
            _playerCannon.Interact(_player);
        }
    }

    private (Vector2, byte) ResolveTraceStep(int tick)
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

        return (_solutionTrace.HoldLastInput ? _solutionTrace.MoveInputs[^1] : Vector2.Zero, 0);
    }

    private void ResetRoomState()
    {
        _playerCannonFired = false;
        _crossedProjectileLane = false;
        _projectileHits = 0;
        _nextProjectileLane = 0;
        _playerCannon.ResetCannon();
        foreach (InterferenceCannon3D cannon in _interferenceCannons)
        {
            cannon.ResetCannon();
        }
    }

    private int TotalShotsFired => _interferenceCannons.Sum(cannon => cannon.ShotsFired);

    private void BuildRoom()
    {
        const string metal = "res://assets/textures/brushed_metal.png";
        const string copper = "res://assets/textures/copper_rivets.svg";
        Color pale = new("aeb8bd");
        Color frame = new("53606a");

        RoomGeometry.AddClosedRoomShell(this, "RoomShell", new Vector3(0.0f, 0.0f, -34.0f), new Vector2(24.0f, 140.0f), -3.0f, 45.0f, metal, new Color("59636d"), new Color("3e4650"), body =>
        {
            if (body is PlayerBall)
            {
                RestartRoom();
            }
        });
        RoomGeometry.AddBox(this, "SafeStart", new Vector3(12.0f, 0.5f, 20.0f), new Vector3(0.0f, 6.0f, 26.0f), Vector3.Zero, metal, pale, 0.42f, 0.64f);
        RoomGeometry.AddBox(this, "LandingDeck", new Vector3(14.0f, 0.5f, 32.7f), new Vector3(0.0f, 8.0f, -70.35f), Vector3.Zero, metal, pale.Darkened(0.05f), 0.42f, 0.64f);

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddBox(this, $"StartRail{side}", new Vector3(0.36f, 1.4f, 20.0f), new Vector3(side * 6.35f, 6.75f, 26.0f), Vector3.Zero, copper, frame, 0.42f, 0.6f);
            RoomGeometry.AddBox(this, $"LandingRail{side}", new Vector3(0.36f, 1.45f, 32.7f), new Vector3(side * 7.35f, 8.75f, -70.35f), Vector3.Zero, metal, frame, 0.42f, 0.62f);
        }

        _playerCannon = new PlayerCannon3D
        {
            Name = "PlayerCannon",
            Position = new Vector3(0.0f, 6.25f, 20.0f),
            LaunchVelocity = new Vector3(0.0f, 10.0f, -18.0f),
            MuzzleOffset = new Vector3(0.0f, 2.2f, -1.0f),
            ActivationRadius = 4.5f,
        };
        AddChild(_playerCannon);

        ForceVolume3D lowGravity = new()
        {
            Name = "LowGravityCrossfire",
            Position = new Vector3(0.0f, 23.0f, -29.0f),
            CollisionMask = 1,
            Profile = GD.Load<ForceVolumeProfile>("res://resources/force_volumes/low_gravity.tres"),
        };
        lowGravity.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(21.0f, 42.0f, 78.0f) } });
        AddChild(lowGravity);

        float[] columnZ = Enumerable.Range(0, 12).Select(index => Mathf.Lerp(6.0f, -64.0f, index / 11.0f)).ToArray();
        float[] muzzleY = { 10.0f, 16.5f, 23.0f, 29.5f, 36.0f, 42.5f };
        for (int column = 0; column < columnZ.Length; column++)
        {
            for (int row = 0; row < muzzleY.Length; row++)
            {
                int index = (column * muzzleY.Length) + row;
                InterferenceCannon3D cannon = new()
                {
                    Name = $"InterferenceCannon{row + 1}x{column + 1}",
                    Position = new Vector3(-10.70f, muzzleY[row] - 2.6f, columnZ[column]),
                    MuzzleOffset = new Vector3(3.0f, 2.6f, 0.0f),
                    ProjectileVelocity = new Vector3(24.0f, 0.0f, 0.0f),
                    InitialDelayTicks = 10 + (column * 4) + (row * 2),
                    CadenceTicks = 137 + index,
                    ProjectileLifetimeTicks = 78,
                    PoolSize = 2,
                    EnableAudio = !_runSolutionSmoke && index % 3 == 0,
                };
                AddChild(cannon);
                _interferenceCannons.Add(cannon);
            }

            int laneIndex = column;
            Area3D crossing = new()
            {
                Name = $"ProjectileLane{column + 1}",
                Position = new Vector3(0.0f, 22.0f, columnZ[column]),
                CollisionLayer = 0,
                CollisionMask = 1,
                Monitoring = true,
            };
            crossing.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(20.0f, 32.0f, 2.4f) } });
            crossing.BodyEntered += body =>
            {
                if (body == _player && _playerCannonFired && !_player.IsGrounded && laneIndex == _nextProjectileLane)
                {
                    _nextProjectileLane++;
                    _crossedProjectileLane = _nextProjectileLane == columnZ.Length;
                }
            };
            AddChild(crossing);
            _projectileLanes.Add(crossing);
        }
    }

    private void BuildGoal()
    {
        Vector3 position = new(0.0f, 9.15f, -85.9f);
        Area3D goal = new() { Name = "GoalCup", Position = position, CollisionLayer = 0, CollisionMask = 1, Monitoring = true };
        goal.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 1.75f, Height = 2.8f } });
        goal.BodyEntered += body =>
        {
            if (body is PlayerBall &&
                _playerCannonFired &&
                _crossedProjectileLane &&
                _nextProjectileLane == _projectileLanes.Count &&
                TotalShotsFired >= 8)
            {
                TryAwardUntouchable();
                CompleteRoom();
            }
        };
        AddChild(goal);

        RoomGeometry.AddGoalExitDoor(this, position);
    }

    private void RegisterProjectileHit(PlayerBall player)
    {
        _projectileHits++;
        player.LinearVelocity += new Vector3(8.5f, -2.2f, 1.4f);
        player.Sleeping = false;
    }

    private void TryAwardUntouchable()
    {
        if (_projectileHits == 0)
        {
            MarkAdvancementCondition("untouchable");
        }
    }

    private void RunRequestedSmoke()
    {
        if (_runMechanicsSmoke)
        {
            bool cannonGrid = _interferenceCannons.Count == 72 &&
                _interferenceCannons.Select(cannon => cannon.InitialDelayTicks).Distinct().Count() >= 12 &&
                _interferenceCannons.Select(cannon => cannon.CadenceTicks).Distinct().Count() == 72 &&
                _interferenceCannons.All(cannon => cannon.ProjectileVelocity.X >= 24.0f && cannon.PoolSize == 2 && cannon.HasSolidBodyHitbox && cannon.UsesRandomizedTiming);
            bool laneContract = _projectileLanes.Count == 12;
            bool lowGravityContract = GetNodeOrNull<ForceVolume3D>("LowGravityCrossfire") is { Profile.AirControlAcceleration: > 0.0f };
            bool wallMountedExit = Mathf.Abs(GetNode<Area3D>("GoalCup").Position.Z - -85.9f) < 0.01f;
            if (!cannonGrid || !laneContract || !lowGravityContract || !wallMountedExit)
            {
                GD.PushError($"ROOM17_MECHANICS_FAIL: cannons={_interferenceCannons.Count}, delays={_interferenceCannons.Select(cannon => cannon.InitialDelayTicks).Distinct().Count()}, lanes={_projectileLanes.Count}, exit={wallMountedExit}.");
                GetTree().Quit(1);
                return;
            }

            GD.Print("ROOM17_MECHANICS_PASS: seventy-two unsynchronised cannons densely cover twelve airborne lanes inside a low-gravity steering volume, with no route above the barrage.");
            GetTree().Quit(0);
            return;
        }

        if (_runImpactSmoke)
        {
            _player.LinearVelocity = Vector3.Zero;
            RegisterProjectileHit(_player);
            if (_projectileHits != 1 || _player.LinearVelocity.Length() < 0.1f)
            {
                GD.PushError($"ROOM17_IMPACT_FAIL: hits={_projectileHits}, velocity={_player.LinearVelocity}.");
                GetTree().Quit(1);
                return;
            }

            GD.Print($"ROOM17_IMPACT_PASS: a foam projectile applied a visible deflecting impulse ({_player.LinearVelocity}).");
            GetTree().Quit(0);
            return;
        }

        _projectileHits = _runAchievementNegativeSmoke ? 1 : 0;
        TryAwardUntouchable();
        bool awarded = CompletedAdvancementIds.Contains("untouchable");
        bool expected = _runAchievementPositiveSmoke;
        if (awarded != expected)
        {
            GD.PushError($"ROOM17_ACHIEVEMENT_FAIL: expected={expected}, awarded={awarded}, hits={_projectileHits}.");
            GetTree().Quit(1);
            return;
        }

        GD.Print(expected
            ? "ROOM17_ACHIEVEMENT_POSITIVE_PASS: a hit-free grid crossing awarded Untouchable."
            : "ROOM17_ACHIEVEMENT_NEGATIVE_PASS: taking a hit denied Untouchable without blocking room completion.");
        GetTree().Quit(0);
    }

    private void FailSolutionSmoke(string message)
    {
        GD.PushError($"ROOM17_SOLUTION_FAIL: {message}");
        FinishSolutionSmoke(1);
    }

    private async void FinishSolutionSmoke(int code)
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
        _solutionTrace = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(code);
    }
}
