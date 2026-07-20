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
    [Export] public int ProjectileLifetimeTicks { get; set; } = 100;
    [Export] public int PoolSize { get; set; } = 6;
    [Export] public bool EnableAudio { get; set; } = true;

    public int ShotsFired { get; private set; }
    public bool UsesRandomizedTiming => InitialDelayJitterTicks > 0 && CadenceJitterTicks > 0;
    public bool HasSolidBodyHitbox =>
        GetNodeOrNull<StaticBody3D>("CannonHitbox")?.GetNodeOrNull<CollisionShape3D>("BodyEnvelopeHitbox") is { Disabled: false };

    private readonly List<ProjectileState> _projectiles = new();
    private StaticBody3D _cannonHitbox = null!;
    private Node3D _barrelRoot = null!;
    private MeshInstance3D _warningLamp = null!;
    private OmniLight3D _warningLight = null!;
    private AudioStreamPlayer3D? _fireAudio;
    private readonly RandomNumberGenerator _timingRng = new();
    private int _tick;
    private int _lastFireTick;
    private int _nextFireTick;
    private bool _deterministicSmokeTiming;

    public override void _Ready()
    {
        BuildVisual();
        BuildProjectilePool();
        _deterministicSmokeTiming = OS.GetCmdlineUserArgs().Any(argument => argument.Contains("solution-smoke", StringComparison.Ordinal));
        if (_deterministicSmokeTiming)
        {
            _timingRng.Seed = StableSeed(Name.ToString());
        }
        else
        {
            _timingRng.Randomize();
        }
        ScheduleInitialFire();
    }

    public override void _Process(double delta)
    {
        int cycleLength = Math.Max(1, _nextFireTick - _lastFireTick);
        int cycleTick = Mathf.Clamp(_tick - _lastFireTick, 0, cycleLength);
        float charge = Mathf.Clamp(cycleTick / (float)cycleLength, 0.0f, 1.0f);
        float pulse = 1.0f + (charge * 0.28f);
        _warningLamp.Scale = Vector3.One * pulse;
        _warningLight.LightEnergy = 0.35f + (charge * 2.25f);
    }

    public void AdvancePhysicsTick()
    {
        _tick++;
        if (_tick >= _nextFireTick)
        {
            FireProjectile();
            _lastFireTick = _tick;
            _nextFireTick = _tick + SampleInterval(CadenceTicks, CadenceJitterTicks);
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
        _nextFireTick = SampleInterval(InitialDelayTicks, InitialDelayJitterTicks);
    }

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

        float firingDirection = GetFiringDirection();
        _barrelRoot.Position = new Vector3(-firingDirection * 0.28f, 0.0f, 0.0f);
        Tween tween = CreateTween().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_barrelRoot, "position", Vector3.Zero, 0.18f);
        ProjectileFired?.Invoke();
    }

    private void BuildProjectilePool()
    {
        StandardMaterial3D projectileMaterial = RoomGeometry.CreateMaterial(
            "res://assets/textures/rubber_chevrons.svg",
            new Color("d2603e"),
            0.08f,
            0.82f,
            emissionEnabled: true,
            emission: new Color("5c160b"));

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
            body.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.62f } });
            body.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.62f, Height = 1.24f, RadialSegments = 20, Rings = 10 },
                MaterialOverride = projectileMaterial,
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
        StandardMaterial3D steel = RoomGeometry.CreateMaterial("res://assets/textures/brushed_metal.png", new Color("77858c"), 0.46f, 0.58f);
        StandardMaterial3D dark = RoomGeometry.CreateMaterial("res://assets/textures/rubber_chevrons.svg", new Color("252c32"), 0.04f, 0.94f);
        StandardMaterial3D warning = RoomGeometry.CreateMaterial("res://assets/textures/sugar_glaze.svg", new Color("e2683f"), 0.08f, 0.4f, emissionEnabled: true, emission: new Color("7d1d0b"));
        float firingDirection = GetFiringDirection();

        RoomGeometry.AddVisualBox(this, "CannonMount", new Vector3(0.36f, 3.2f, 2.2f), new Vector3(firingDirection * 0.08f, 1.6f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, steel);
        _barrelRoot = new Node3D { Name = "BarrelRoot" };
        AddChild(_barrelRoot);
        RoomGeometry.AddVisualBox(_barrelRoot, "CannonHousing", new Vector3(2.4f, 1.25f, 1.25f), new Vector3(firingDirection * 1.35f, 2.35f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, dark);
        RoomGeometry.AddVisualBox(_barrelRoot, "MuzzleFrame", new Vector3(0.24f, 1.65f, 1.65f), new Vector3(firingDirection * 2.62f, 2.35f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, steel);
        RoomGeometry.AddVisualBox(_barrelRoot, "MuzzleOpening", new Vector3(0.08f, 1.02f, 1.02f), new Vector3(firingDirection * 2.76f, 2.35f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, dark);

        _cannonHitbox = new StaticBody3D
        {
            Name = "CannonHitbox",
            CollisionLayer = 1,
            CollisionMask = 1,
        };
        _cannonHitbox.AddChild(new CollisionShape3D
        {
            Name = "BodyEnvelopeHitbox",
            Position = new Vector3(firingDirection * 1.34f, 1.66f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(3.08f, 3.32f, 2.24f) },
        });
        _cannonHitbox.AddChild(new CollisionShape3D
        {
            Name = "MountHitbox",
            Position = new Vector3(firingDirection * 0.08f, 1.6f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(0.36f, 3.2f, 2.2f) },
        });
        _cannonHitbox.AddChild(new CollisionShape3D
        {
            Name = "HousingHitbox",
            Position = new Vector3(firingDirection * 1.35f, 2.35f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(2.4f, 1.25f, 1.25f) },
        });
        _cannonHitbox.AddChild(new CollisionShape3D
        {
            Name = "MuzzleHitbox",
            Position = new Vector3(firingDirection * 2.62f, 2.35f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(0.32f, 1.65f, 1.65f) },
        });
        AddChild(_cannonHitbox);

        _warningLamp = new MeshInstance3D
        {
            Name = "WarningLamp",
            Position = new Vector3(0.0f, 2.5f, 0.0f),
            Mesh = new SphereMesh { Radius = 0.36f, Height = 0.72f, RadialSegments = 16, Rings = 8 },
            MaterialOverride = warning,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_warningLamp);
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
