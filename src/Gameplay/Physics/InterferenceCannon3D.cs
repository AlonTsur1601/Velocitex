using Godot;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Gameplay.Physics;

public partial class InterferenceCannon3D : Node3D
{
    private sealed class ProjectileState
    {
        public required RigidBody3D Body { get; init; }
        public int RemainingTicks { get; set; }
        public bool Active { get; set; }
    }

    public event Action? ProjectileFired;
    public event Action<PlayerBall>? PlayerHit;

    [Export] public Vector3 MuzzleOffset { get; set; } = new(3.0f, 2.6f, 0.0f);
    [Export] public Vector3 ProjectileVelocity { get; set; } = new(16.0f, 0.0f, 0.0f);
    [Export] public int InitialDelayTicks { get; set; } = 32;
    [Export] public int CadenceTicks { get; set; } = 120;
    [Export] public int InitialDelayJitterTicks { get; set; } = 3;
    [Export] public int CadenceJitterTicks { get; set; } = 18;
    [Export] public int RandomInitialDelayMinTicks { get; set; } = 15;
    [Export] public int RandomInitialDelayMaxTicks { get; set; } = 180;
    [Export] public int RandomCadenceMinTicks { get; set; } = 120;
    [Export] public int RandomCadenceMaxTicks { get; set; } = 180;
    [Export] public int ProjectileLifetimeTicks { get; set; } = 100;
    [Export] public int PoolSize { get; set; } = 6;
    [Export] public bool EnableAudio { get; set; } = true;
    [Export] public bool EnableWarningLight { get; set; } = true;
    [Export] public bool UseBatchedDenseVisuals { get; set; }

    public int ShotsFired { get; private set; }
    public int ProjectilePoolCount => _projectiles.Count;
    public int ScheduledFirstFireTick => _nextFireTick;
    public bool UsesRandomizedTiming => InitialDelayJitterTicks > 0 && CadenceJitterTicks > 0;
    public bool HasSolidBodyHitbox =>
        GetNodeOrNull<StaticBody3D>("CannonHitbox")?.GetNodeOrNull<CollisionShape3D>("BodyEnvelopeHitbox") is { Disabled: false };

    private readonly List<ProjectileState> _projectiles = new();
    private StaticBody3D _cannonHitbox = null!;
    private Node3D _barrelRoot = null!;
    private MeshInstance3D? _warningLamp;
    private OmniLight3D? _warningLight;
    private AudioStreamPlayer3D? _fireAudio;
    private readonly RandomNumberGenerator _timingRng = new();
    private int _tick;
    private int _lastFireTick;
    private int _nextFireTick;
    private bool _deterministicSmokeTiming;

    public override void _Ready()
    {
        BuildVisual();
        SetProcess(_warningLamp is not null);
        _deterministicSmokeTiming = OS.GetCmdlineUserArgs().Any(argument => argument.Contains("solution-smoke", StringComparison.Ordinal));
        if (_deterministicSmokeTiming)
        {
            _timingRng.Seed = StableSeed(Name.ToString());
        }
        else
        {
            _timingRng.Seed = StableSeed(Name.ToString()) ^ (ulong)Time.GetTicksUsec() ^ GetInstanceId();
        }
        ScheduleInitialFire();
    }

    public override void _Process(double delta)
    {
        if (_warningLamp is null)
        {
            return;
        }

        int cycleLength = Math.Max(1, _nextFireTick - _lastFireTick);
        int cycleTick = Mathf.Clamp(_tick - _lastFireTick, 0, cycleLength);
        float charge = Mathf.Clamp(cycleTick / (float)cycleLength, 0.0f, 1.0f);
        float pulse = 1.0f + (charge * 0.28f);
        _warningLamp.Scale = Vector3.One * pulse;
        if (_warningLight is not null)
        {
            _warningLight.LightEnergy = 0.35f + (charge * 2.25f);
        }
    }

    public override void _ExitTree()
    {
        _fireAudio?.Stop();
        if (_fireAudio is not null)
        {
            _fireAudio.Stream = null;
        }
        foreach (ProjectileState projectile in _projectiles)
        {
            Deactivate(projectile);
        }
        ProjectileFired = null;
        PlayerHit = null;
        _projectiles.Clear();
    }

    public void AdvancePhysicsTick()
    {
        _tick++;
        if (_tick >= _nextFireTick)
        {
            FireProjectile();
            _lastFireTick = _tick;
            _nextFireTick = _tick + SampleCadenceInterval();
        }

        foreach (ProjectileState projectile in _projectiles)
        {
            if (!projectile.Active)
            {
                continue;
            }

            projectile.RemainingTicks--;
            if (projectile.RemainingTicks <= 0)
            {
                Deactivate(projectile);
            }
        }
    }

    public void ResetCannon()
    {
        _tick = 0;
        _lastFireTick = 0;
        ShotsFired = 0;
        if (_deterministicSmokeTiming)
        {
            _timingRng.Seed = StableSeed(Name.ToString());
        }
        ScheduleInitialFire();
        foreach (ProjectileState projectile in _projectiles)
        {
            Deactivate(projectile);
        }

        _barrelRoot.Position = Vector3.Zero;
    }

    private void ScheduleInitialFire()
    {
        _nextFireTick = _deterministicSmokeTiming
            ? SampleInterval(InitialDelayTicks, InitialDelayJitterTicks)
            : _timingRng.RandiRange(
                Math.Max(1, RandomInitialDelayMinTicks),
                Math.Max(Math.Max(1, RandomInitialDelayMinTicks), RandomInitialDelayMaxTicks));
    }

    private int SampleCadenceInterval() => _deterministicSmokeTiming
        ? SampleInterval(CadenceTicks, CadenceJitterTicks)
        : _timingRng.RandiRange(
            Math.Max(1, RandomCadenceMinTicks),
            Math.Max(Math.Max(1, RandomCadenceMinTicks), RandomCadenceMaxTicks));

    private int SampleInterval(int baseTicks, int jitterTicks)
    {
        int safeBase = Math.Max(1, baseTicks);
        int safeJitter = Math.Max(0, jitterTicks);
        if (_deterministicSmokeTiming || safeJitter == 0)
        {
            return safeBase;
        }

        return Math.Max(1, safeBase + _timingRng.RandiRange(-safeJitter, safeJitter));
    }

    private static ulong StableSeed(string value)
    {
        ulong hash = 1469598103934665603UL;
        foreach (char character in value)
        {
            hash ^= character;
            hash *= 1099511628211UL;
        }
        return hash;
    }

    private void FireProjectile()
    {
        EnsureProjectilePool();
        ProjectileState projectile = _projectiles.FirstOrDefault(state => !state.Active) ?? _projectiles[0];
        Deactivate(projectile);
        RigidBody3D body = projectile.Body;
        body.GlobalPosition = ToGlobal(MuzzleOffset);
        body.LinearVelocity = GlobalBasis * ProjectileVelocity;
        body.AngularVelocity = new Vector3(0.0f, 0.0f, -10.0f);
        body.CollisionLayer = 4;
        body.CollisionMask = 1;
        body.Visible = true;
        body.Freeze = false;
        body.Sleeping = false;
        projectile.Active = true;
        projectile.RemainingTicks = ProjectileLifetimeTicks;
        ShotsFired++;
        _fireAudio?.Play();

        if (!UseBatchedDenseVisuals)
        {
            float firingDirection = GetFiringDirection();
            _barrelRoot.Position = new Vector3(-firingDirection * 0.28f, 0.0f, 0.0f);
            Tween tween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(_barrelRoot, "position", Vector3.Zero, 0.18f);
        }
        ProjectileFired?.Invoke();
    }

    private static SphereMesh? _sharedProjectileMesh;
    private static SphereShape3D? _sharedProjectileShape;
    private static StandardMaterial3D? _sharedProjectileMaterial;
    private static BoxShape3D? _sharedBodyEnvelopeShape;
    private static BoxShape3D? _sharedMountShape;
    private static BoxShape3D? _sharedHousingShape;
    private static BoxShape3D? _sharedMuzzleShape;

    private static SphereMesh SharedProjectileMesh => _sharedProjectileMesh ??= new SphereMesh { Radius = 0.62f, Height = 1.24f, RadialSegments = 20, Rings = 10 };

    private static SphereShape3D SharedProjectileShape => _sharedProjectileShape ??= new SphereShape3D { Radius = 0.62f };

    private static BoxShape3D SharedBodyEnvelopeShape => _sharedBodyEnvelopeShape ??= new BoxShape3D { Size = new Vector3(3.08f, 3.32f, 2.24f) };

    private static BoxShape3D SharedMountShape => _sharedMountShape ??= new BoxShape3D { Size = new Vector3(0.36f, 3.2f, 2.2f) };

    private static BoxShape3D SharedHousingShape => _sharedHousingShape ??= new BoxShape3D { Size = new Vector3(2.4f, 1.25f, 1.25f) };

    private static BoxShape3D SharedMuzzleShape => _sharedMuzzleShape ??= new BoxShape3D { Size = new Vector3(0.32f, 1.65f, 1.65f) };

    private static StandardMaterial3D SharedProjectileMaterial => _sharedProjectileMaterial ??= RoomGeometry.CreateMaterial(
        "res://assets/textures/rubber_chevrons.svg",
        new Color("d2603e"),
        0.08f,
        0.82f,
        emissionEnabled: true,
        emission: new Color("5c160b"));

    private void EnsureProjectilePool()
    {
        if (_projectiles.Count > 0)
        {
            return;
        }

        // Every cannon calls this regardless of UseBatchedDenseVisuals, and
        // rooms with dozens of cannons x PoolSize projectiles each used to
        // allocate a brand new SphereMesh/SphereShape3D/StandardMaterial3D
        // per projectile - hundreds of duplicate resources with identical
        // values. Sharing one instance of each across every cannon in every
        // room cuts that allocation cost drastically without changing cannon
        // count, fire rate, or appearance at all.
        for (int index = 0; index < Math.Max(2, PoolSize); index++)
        {
            RigidBody3D body = new()
            {
                Name = $"Projectile{index + 1:00}",
                Mass = 1.35f,
                GravityScale = 0.0f,
                ContinuousCd = true,
                ContactMonitor = true,
                MaxContactsReported = 4,
                Freeze = true,
                CollisionLayer = 0,
                CollisionMask = 0,
                Visible = false,
            };
            body.AddCollisionExceptionWith(_cannonHitbox);
            body.AddChild(new CollisionShape3D { Shape = SharedProjectileShape });
            body.AddChild(new MeshInstance3D
            {
                Mesh = SharedProjectileMesh,
                MaterialOverride = SharedProjectileMaterial,
            });
            ProjectileState state = new() { Body = body };
            body.BodyEntered += otherBody =>
            {
                if (state.Active && otherBody is PlayerBall player)
                {
                    PlayerHit?.Invoke(player);
                }
            };
            AddChild(body);
            _projectiles.Add(state);
        }
    }

    private static void Deactivate(ProjectileState projectile)
    {
        projectile.Active = false;
        projectile.RemainingTicks = 0;
        projectile.Body.Freeze = true;
        projectile.Body.LinearVelocity = Vector3.Zero;
        projectile.Body.AngularVelocity = Vector3.Zero;
        projectile.Body.CollisionLayer = 0;
        projectile.Body.CollisionMask = 0;
        projectile.Body.Visible = false;
    }

    private void BuildVisual()
    {
        float firingDirection = GetFiringDirection();

        _barrelRoot = new Node3D { Name = "BarrelRoot" };
        AddChild(_barrelRoot);
        if (!UseBatchedDenseVisuals)
        {
            StandardMaterial3D steel = RoomGeometry.CreateMaterial("res://assets/textures/brushed_metal.png", new Color("77858c"), 0.46f, 0.58f);
            StandardMaterial3D dark = RoomGeometry.CreateMaterial("res://assets/textures/rubber_chevrons.svg", new Color("252c32"), 0.04f, 0.94f);
            StandardMaterial3D warning = RoomGeometry.CreateMaterial("res://assets/textures/sugar_glaze.svg", new Color("e2683f"), 0.08f, 0.4f, emissionEnabled: true, emission: new Color("7d1d0b"));
            RoomGeometry.AddVisualBox(this, "CannonMount", new Vector3(0.36f, 3.2f, 2.2f), new Vector3(firingDirection * 0.08f, 1.6f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, steel);
            RoomGeometry.AddVisualBox(_barrelRoot, "CannonHousing", new Vector3(2.4f, 1.25f, 1.25f), new Vector3(firingDirection * 1.35f, 2.35f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, dark);
            RoomGeometry.AddVisualBox(_barrelRoot, "MuzzleFrame", new Vector3(0.24f, 1.65f, 1.65f), new Vector3(firingDirection * 2.62f, 2.35f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, steel);
            RoomGeometry.AddVisualBox(_barrelRoot, "MuzzleOpening", new Vector3(0.08f, 1.02f, 1.02f), new Vector3(firingDirection * 2.76f, 2.35f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, dark);

            _warningLamp = new MeshInstance3D
            {
                Name = "WarningLamp",
                Position = new Vector3(0.0f, 2.5f, 0.0f),
                Mesh = new SphereMesh { Radius = 0.36f, Height = 0.72f, RadialSegments = 16, Rings = 8 },
                MaterialOverride = warning,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(_warningLamp);
            if (EnableWarningLight)
            {
                _warningLight = new OmniLight3D
                {
                    Name = "WarningLight",
                    Position = _warningLamp.Position,
                    LightColor = new Color("ef7046"),
                    LightEnergy = 0.35f,
                    OmniRange = 7.0f,
                    ShadowEnabled = false,
                };
                AddChild(_warningLight);
            }
        }

        _cannonHitbox = new StaticBody3D
        {
            Name = "CannonHitbox",
            CollisionLayer = 1,
            CollisionMask = 1,
        };
        // These four hitbox shapes are the same size for every cannon (only
        // their per-CollisionShape3D Position offset varies, which is fine
        // to share since offset lives on the node, not the Shape resource).
        _cannonHitbox.AddChild(new CollisionShape3D
        {
            Name = "BodyEnvelopeHitbox",
            Position = new Vector3(firingDirection * 1.34f, 1.66f, 0.0f),
            Shape = SharedBodyEnvelopeShape,
        });
        if (!UseBatchedDenseVisuals)
        {
            _cannonHitbox.AddChild(new CollisionShape3D
            {
                Name = "MountHitbox",
                Position = new Vector3(firingDirection * 0.08f, 1.6f, 0.0f),
                Shape = SharedMountShape,
            });
            _cannonHitbox.AddChild(new CollisionShape3D
            {
                Name = "HousingHitbox",
                Position = new Vector3(firingDirection * 1.35f, 2.35f, 0.0f),
                Shape = SharedHousingShape,
            });
            _cannonHitbox.AddChild(new CollisionShape3D
            {
                Name = "MuzzleHitbox",
                Position = new Vector3(firingDirection * 2.62f, 2.35f, 0.0f),
                Shape = SharedMuzzleShape,
            });
        }
        AddChild(_cannonHitbox);

        if (EnableAudio)
        {
            _fireAudio = new AudioStreamPlayer3D
            {
                Name = "ProjectileFireSfx",
                Stream = GD.Load<AudioStream>("res://assets/audio/sfx/device_interference_cannon_fire.wav"),
                Bus = "SFX",
                Position = MuzzleOffset,
                MaxDistance = 42.0f,
                UnitSize = 8.0f,
            };
            AddChild(_fireAudio);
        }
    }

    public static void AddDenseVisualBatch(Node3D parent, IReadOnlyList<InterferenceCannon3D> cannons)
    {
        if (cannons.Count == 0)
        {
            return;
        }

        StandardMaterial3D steel = RoomGeometry.CreateMaterial("res://assets/textures/brushed_metal.png", new Color("77858c"), 0.46f, 0.58f);
        StandardMaterial3D dark = RoomGeometry.CreateMaterial("res://assets/textures/rubber_chevrons.svg", new Color("252c32"), 0.04f, 0.94f);
        StandardMaterial3D warning = RoomGeometry.CreateMaterial("res://assets/textures/sugar_glaze.svg", new Color("e2683f"), 0.08f, 0.4f, emissionEnabled: true, emission: new Color("7d1d0b"));

        AddDenseBoxBatch(parent, "DenseCannonMounts", cannons, new Vector3(0.36f, 3.2f, 2.2f), cannon => new Vector3(cannon.GetFiringDirection() * 0.08f, 1.6f, 0.0f), steel);
        AddDenseBoxBatch(parent, "DenseCannonHousings", cannons, new Vector3(2.4f, 1.25f, 1.25f), cannon => new Vector3(cannon.GetFiringDirection() * 1.35f, 2.35f, 0.0f), dark);
        AddDenseBoxBatch(parent, "DenseCannonMuzzleFrames", cannons, new Vector3(0.24f, 1.65f, 1.65f), cannon => new Vector3(cannon.GetFiringDirection() * 2.62f, 2.35f, 0.0f), steel);
        AddDenseBoxBatch(parent, "DenseCannonOpenings", cannons, new Vector3(0.08f, 1.02f, 1.02f), cannon => new Vector3(cannon.GetFiringDirection() * 2.76f, 2.35f, 0.0f), dark);
        AddDenseMeshBatch(
            parent,
            "DenseCannonWarningLamps",
            cannons,
            new SphereMesh { Radius = 0.36f, Height = 0.72f, RadialSegments = 12, Rings = 6 },
            _ => new Vector3(0.0f, 2.5f, 0.0f),
            warning,
            GeometryInstance3D.ShadowCastingSetting.Off);
    }

    public static bool HasDenseVisualBatch(Node parent, int expectedCount)
    {
        string[] batchNames =
        {
            "DenseCannonMounts",
            "DenseCannonHousings",
            "DenseCannonMuzzleFrames",
            "DenseCannonOpenings",
            "DenseCannonWarningLamps",
        };
        return batchNames.All(name =>
            parent.GetNodeOrNull<MultiMeshInstance3D>(name)?.Multimesh?.InstanceCount == expectedCount);
    }

    private static void AddDenseBoxBatch(
        Node3D parent,
        string name,
        IReadOnlyList<InterferenceCannon3D> cannons,
        Vector3 size,
        Func<InterferenceCannon3D, Vector3> localPosition,
        Material material)
    {
        AddDenseMeshBatch(parent, name, cannons, new BoxMesh { Size = size }, localPosition, material, GeometryInstance3D.ShadowCastingSetting.On);
    }

    private static void AddDenseMeshBatch(
        Node3D parent,
        string name,
        IReadOnlyList<InterferenceCannon3D> cannons,
        Mesh mesh,
        Func<InterferenceCannon3D, Vector3> localPosition,
        Material material,
        GeometryInstance3D.ShadowCastingSetting shadowCasting)
    {
        MultiMesh multiMesh = new()
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = cannons.Count,
        };
        for (int index = 0; index < cannons.Count; index++)
        {
            InterferenceCannon3D cannon = cannons[index];
            Transform3D localPart = new(Basis.Identity, localPosition(cannon));
            multiMesh.SetInstanceTransform(index, cannon.Transform * localPart);
        }

        parent.AddChild(new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = multiMesh,
            MaterialOverride = material,
            CastShadow = shadowCasting,
        });
    }

    private float GetFiringDirection()
    {
        float direction = Mathf.Sign(MuzzleOffset.X);
        if (Mathf.IsZeroApprox(direction))
        {
            direction = Mathf.Sign(ProjectileVelocity.X);
        }

        return Mathf.IsZeroApprox(direction) ? 1.0f : direction;
    }
}
