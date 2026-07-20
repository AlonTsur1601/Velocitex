using Godot;
using Velocitex.Core.Physics;
using Velocitex.Gameplay.Player;

namespace Velocitex.Gameplay.Physics;

public partial class ProfiledSurfaceBody : StaticBody3D
{
    public const float DefaultGlassBreakDelaySeconds = 1.25f;

    public SurfaceProfile Profile { get; set; } = new();
    public bool IsBroken { get; private set; }
    public float BreakDelaySeconds { get; private set; } = DefaultGlassBreakDelaySeconds;
    public float LongestContinuousGlassContactSeconds { get; private set; }

    private readonly Dictionary<PlayerBall, float> _glassContactSeconds = new();
    private readonly HashSet<PlayerBall> _observedPlayers = new();
    private readonly Dictionary<CollisionShape3D, bool> _originalCollisionStates = new();
    private readonly Dictionary<GeometryInstance3D, bool> _originalVisualStates = new();
    private Area3D? _glassContactArea;
    private GpuParticles3D? _glassBreakParticles;

    public override void _Ready()
    {
        if (Profile.Kind == SurfaceKind.Frictionless)
        {
            ConfigureTimedGlass();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Profile.Kind != SurfaceKind.Frictionless)
        {
            return;
        }

        RefreshBreakDelay();
        if (IsBroken || _glassContactSeconds.Count == 0)
        {
            return;
        }

        foreach (PlayerBall player in _glassContactSeconds.Keys.ToArray())
        {
            if (!IsInstanceValid(player))
            {
                _glassContactSeconds.Remove(player);
                continue;
            }

            if (!player.IsGrounded || player.GroundSurfaceKind != SurfaceKind.Frictionless)
            {
                _glassContactSeconds[player] = 0.0f;
                continue;
            }

            float contactSeconds = _glassContactSeconds[player] + (float)delta;
            _glassContactSeconds[player] = contactSeconds;
            LongestContinuousGlassContactSeconds = Mathf.Max(LongestContinuousGlassContactSeconds, contactSeconds);
            if (contactSeconds >= BreakDelaySeconds)
            {
                BreakGlass();
                return;
            }
        }
    }

    public override void _ExitTree()
    {
        if (_glassContactArea is not null)
        {
            _glassContactArea.BodyEntered -= OnGlassBodyEntered;
            _glassContactArea.BodyExited -= OnGlassBodyExited;
        }

        foreach (PlayerBall player in _observedPlayers)
        {
            if (IsInstanceValid(player))
            {
                player.ResetPerformed -= RestoreGlass;
            }
        }

        _glassContactSeconds.Clear();
        _observedPlayers.Clear();
        _originalCollisionStates.Clear();
        _originalVisualStates.Clear();
    }

    public void RestoreGlass()
    {
        IsBroken = false;
        LongestContinuousGlassContactSeconds = 0.0f;
        _glassContactSeconds.Clear();
        foreach ((CollisionShape3D collision, bool wasDisabled) in _originalCollisionStates)
        {
            if (IsInstanceValid(collision))
            {
                collision.SetDeferred(CollisionShape3D.PropertyName.Disabled, wasDisabled);
            }
        }

        foreach ((GeometryInstance3D visual, bool wasVisible) in _originalVisualStates)
        {
            if (IsInstanceValid(visual))
            {
                visual.Visible = wasVisible;
            }
        }

        if (IsInstanceValid(_glassBreakParticles))
        {
            _glassBreakParticles!.Emitting = false;
        }
    }

    private void ConfigureTimedGlass()
    {
        AddToGroup("timed_breakable_glass");
        if (!HasMeta("break_delay_seconds"))
        {
            SetMeta("break_delay_seconds", DefaultGlassBreakDelaySeconds);
        }
        RefreshBreakDelay();

        CollisionShape3D? sourceCollision = GetChildren().OfType<CollisionShape3D>()
            .FirstOrDefault(collision => !collision.Disabled && collision.Shape is BoxShape3D);
        if (sourceCollision?.Shape is not BoxShape3D sourceBox)
        {
            GD.PushError($"Timed frictionless surface {GetPath()} requires a box collision shape.");
            return;
        }

        foreach (CollisionShape3D collision in GetChildren().OfType<CollisionShape3D>())
        {
            _originalCollisionStates[collision] = collision.Disabled;
        }
        foreach (GeometryInstance3D visual in EnumerateDescendants(this).OfType<GeometryInstance3D>())
        {
            _originalVisualStates[visual] = visual.Visible;
        }

        _glassContactArea = new Area3D
        {
            Name = "TimedGlassContactArea",
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true,
            Monitorable = false,
        };
        _glassContactArea.AddChild(new CollisionShape3D
        {
            Position = sourceCollision.Position + new Vector3(0.0f, 0.65f, 0.0f),
            Rotation = sourceCollision.Rotation,
            Shape = new BoxShape3D
            {
                Size = new Vector3(sourceBox.Size.X + 0.18f, sourceBox.Size.Y + 1.3f, sourceBox.Size.Z + 0.18f),
            },
        });
        AddChild(_glassContactArea);
        _glassContactArea.BodyEntered += OnGlassBodyEntered;
        _glassContactArea.BodyExited += OnGlassBodyExited;

        StandardMaterial3D shardMaterial = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.65f, 0.88f, 0.95f, 0.82f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        _glassBreakParticles = new GpuParticles3D
        {
            Name = "GlassBreakParticles",
            Position = new Vector3(0.0f, sourceBox.Size.Y * 0.5f, 0.0f),
            Amount = 28,
            Lifetime = 1.4,
            OneShot = true,
            Explosiveness = 0.92f,
            Randomness = 0.72f,
            Emitting = false,
            LocalCoords = false,
            ProcessMaterial = new ParticleProcessMaterial
            {
                EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
                EmissionBoxExtents = new Vector3(sourceBox.Size.X * 0.46f, 0.08f, sourceBox.Size.Z * 0.46f),
                Direction = Vector3.Down,
                Spread = 32.0f,
                Gravity = new Vector3(0.0f, -9.8f, 0.0f),
                InitialVelocityMin = 1.2f,
                InitialVelocityMax = 3.0f,
                ScaleMin = 0.55f,
                ScaleMax = 1.25f,
            },
            DrawPass1 = new BoxMesh
            {
                Size = new Vector3(0.16f, 0.045f, 0.22f),
                Material = shardMaterial,
            },
        };
        AddChild(_glassBreakParticles);
    }

    private void RefreshBreakDelay()
    {
        if (HasMeta("break_delay_seconds"))
        {
            BreakDelaySeconds = Mathf.Max(0.15f, GetMeta("break_delay_seconds").AsSingle());
        }
    }

    private void OnGlassBodyEntered(Node3D body)
    {
        if (body is not PlayerBall player || IsBroken)
        {
            return;
        }

        _glassContactSeconds[player] = 0.0f;
        if (_observedPlayers.Add(player))
        {
            player.ResetPerformed += RestoreGlass;
        }
    }

    private void OnGlassBodyExited(Node3D body)
    {
        if (body is PlayerBall player)
        {
            _glassContactSeconds.Remove(player);
        }
    }

    private void BreakGlass()
    {
        if (IsBroken)
        {
            return;
        }

        IsBroken = true;
        foreach (CollisionShape3D collision in _originalCollisionStates.Keys)
        {
            if (IsInstanceValid(collision))
            {
                collision.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
            }
        }
        foreach (GeometryInstance3D visual in _originalVisualStates.Keys)
        {
            if (IsInstanceValid(visual))
            {
                visual.Visible = false;
            }
        }

        if (IsInstanceValid(_glassBreakParticles))
        {
            _glassBreakParticles!.Restart();
            _glassBreakParticles.Emitting = true;
        }
        _glassContactSeconds.Clear();
    }

    private static IEnumerable<Node> EnumerateDescendants(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            yield return child;
            foreach (Node descendant in EnumerateDescendants(child))
            {
                yield return descendant;
            }
        }
    }
}
