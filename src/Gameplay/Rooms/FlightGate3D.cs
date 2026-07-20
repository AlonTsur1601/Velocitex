using Godot;
using Velocitex.Gameplay.Player;

namespace Velocitex.Gameplay.Rooms;

public partial class FlightGate3D : Area3D
{
    private const float PlayerCollisionRadius = 0.6f;
    private static readonly Color CanonicalFrameTint = new("b8844f");

    public event Action<PlayerBall>? Passed;

    public float Radius { get; set; } = 2.2f;
    public float OpeningContactMargin { get; set; }
    public Color FrameTint { get; set; } = new("b8844f");
    public bool EnableAudio { get; set; } = true;
    public float MinimumExitSpeed { get; set; } = 15.0f;
    public float SpeedGain { get; set; } = 3.0f;
    public float SpeedMultiplier { get; set; } = 1.10f;
    public float MaximumExitSpeed { get; set; } = float.PositiveInfinity;
    public bool AxialBoostOnly { get; set; }
    public float MaximumDownwardExitSpeed { get; set; } = float.PositiveInfinity;
    public bool IsActivated { get; private set; }
    public float LastEntrySpeed { get; private set; }
    public float LastExitSpeed { get; private set; }
    public float TriggerRadius { get; private set; }

    private readonly List<Node3D> _latches = new();
    private readonly Dictionary<PlayerBall, Vector3> _trackedPlayers = new();
    private MeshInstance3D _activeRing = null!;
    private AudioStreamPlayer3D? _activationSfx;
    private float _activationAmount;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = 1;
        Monitoring = true;
        Monitorable = false;

        // The broad phase includes the player's collision radius so a fast ball
        // cannot skip detection. Activation itself is evaluated separately from
        // the ball's center, which may use the entire visible opening.
        TriggerRadius = Mathf.Max(0.35f, Radius);
        CollisionShape3D trigger = new()
        {
            Rotation = new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f),
            Shape = new CylinderShape3D
            {
                Radius = TriggerRadius + PlayerCollisionRadius,
                Height = 0.9f,
            },
        };
        AddChild(trigger);

        StandardMaterial3D frame = RoomGeometry.CreateMaterial(
            "res://assets/textures/copper_rivets.svg",
            CanonicalFrameTint,
            0.42f,
            0.58f);
        StandardMaterial3D active = RoomGeometry.CreateMaterial(
            "res://assets/textures/sugar_glaze.svg",
            new Color("8fd0bd"),
            0.08f,
            0.46f,
            emissionEnabled: true,
            emission: new Color("1b554b"));

        AddChild(new MeshInstance3D
        {
            Name = "MechanicalRing",
            Rotation = new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f),
            Mesh = new TorusMesh
            {
                InnerRadius = Radius,
                OuterRadius = Radius + 0.18f,
                Rings = 40,
                RingSegments = 10,
            },
            MaterialOverride = frame,
        });

        _activeRing = new MeshInstance3D
        {
            Name = "ActivatedRing",
            Rotation = new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f),
            Mesh = new TorusMesh
            {
                InnerRadius = Radius - 0.08f,
                OuterRadius = Radius + 0.02f,
                Rings = 40,
                RingSegments = 8,
            },
            MaterialOverride = active,
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_activeRing);

        for (int index = 0; index < 4; index++)
        {
            float angle = index * Mathf.Pi / 2.0f;
            Node3D latch = new()
            {
                Name = $"Latch{index}",
                Position = new Vector3(Mathf.Cos(angle) * (Radius + 0.34f), Mathf.Sin(angle) * (Radius + 0.34f), 0.0f),
                Rotation = new Vector3(0.0f, 0.0f, angle),
            };
            RoomGeometry.AddVisualBox(
                latch,
                "Jaw",
                new Vector3(0.76f, 0.34f, 0.48f),
                Vector3.Zero,
                Vector3.Zero,
                string.Empty,
                Colors.White,
                0.0f,
                1.0f,
                frame);
            AddChild(latch);
            _latches.Add(latch);
        }

        if (EnableAudio)
        {
            _activationSfx = new AudioStreamPlayer3D
            {
                Name = "ActivationSfx",
                Stream = GD.Load<AudioStream>("res://assets/audio/sfx/device_rail_attach.wav"),
                Bus = "SFX",
                VolumeDb = -8.0f,
                MaxDistance = 22.0f,
            };
            AddChild(_activationSfx);
        }
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsActivated || _trackedPlayers.Count == 0)
        {
            return;
        }

        foreach (PlayerBall player in _trackedPlayers.Keys.ToArray())
        {
            if (!IsInstanceValid(player))
            {
                _trackedPlayers.Remove(player);
                continue;
            }

            Vector3 previous = _trackedPlayers[player];
            Vector3 current = ToLocal(player.GlobalPosition);
            _trackedPlayers[player] = current;
            if (CenterEnteredOpening(previous, current))
            {
                Activate(player);
                return;
            }
        }
    }

    public override void _Process(double delta)
    {
        float target = IsActivated ? 1.0f : 0.0f;
        _activationAmount = Mathf.MoveToward(_activationAmount, target, (float)delta * 4.8f);
        for (int index = 0; index < _latches.Count; index++)
        {
            float angle = index * Mathf.Pi / 2.0f;
            float radius = Radius + 0.34f + (_activationAmount * 0.42f);
            _latches[index].Position = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0.0f);
            _latches[index].Rotation = new Vector3(0.0f, 0.0f, angle);
        }
        _activeRing.Scale = Vector3.One * Mathf.Lerp(0.92f, 1.04f, _activationAmount);
    }

    public override void _ExitTree()
    {
        BodyEntered -= OnBodyEntered;
        BodyExited -= OnBodyExited;
        _activationSfx?.Stop();
        if (_activationSfx is not null)
        {
            _activationSfx.Stream = null;
        }
        Passed = null;
        _latches.Clear();
        _trackedPlayers.Clear();
    }

    public void ResetGate()
    {
        IsActivated = false;
        LastEntrySpeed = 0.0f;
        LastExitSpeed = 0.0f;
        _activationAmount = 0.0f;
        _trackedPlayers.Clear();
        if (IsInstanceValid(_activeRing))
        {
            _activeRing.Hide();
        }
        _activationSfx?.Stop();
    }

    private void OnBodyEntered(Node3D body)
    {
        if (IsActivated || body is not PlayerBall player)
        {
            return;
        }

        Vector3 center = ToLocal(player.GlobalPosition);
        _trackedPlayers[player] = center;
        if (CenterInsideOpening(center))
        {
            Activate(player);
        }
    }

    private void OnBodyExited(Node3D body)
    {
        if (body is PlayerBall player)
        {
            _trackedPlayers.Remove(player);
        }
    }

    private bool CenterEnteredOpening(Vector3 previous, Vector3 current)
    {
        if (CenterInsideOpening(current))
        {
            return true;
        }

        Vector2 previousCenter = new(previous.X, previous.Y);
        Vector2 currentCenter = new(current.X, current.Y);
        Vector2 movement = currentCenter - previousCenter;
        if (movement.LengthSquared() <= 0.000001f)
        {
            return false;
        }

        float closestPoint = Mathf.Clamp(-previousCenter.Dot(movement) / movement.LengthSquared(), 0.0f, 1.0f);
        return (previousCenter + (movement * closestPoint)).LengthSquared() <= TriggerRadius * TriggerRadius;
    }

    private bool CenterInsideOpening(Vector3 center)
    {
        float activationRadius = TriggerRadius + Mathf.Clamp(OpeningContactMargin, 0.0f, PlayerCollisionRadius);
        return new Vector2(center.X, center.Y).LengthSquared() <= activationRadius * activationRadius;
    }

    private void Activate(PlayerBall player)
    {
        if (IsActivated)
        {
            return;
        }

        Vector3 entryVelocity = player.LinearVelocity;
        LastEntrySpeed = entryVelocity.Length();
        LastExitSpeed = Mathf.Max(
            MinimumExitSpeed,
            Mathf.Max(LastEntrySpeed + SpeedGain, LastEntrySpeed * SpeedMultiplier));
        LastExitSpeed = Mathf.Min(LastExitSpeed, MaximumExitSpeed);
        Vector3 boostableVelocity;
        Vector3 preservedVelocity;
        if (AxialBoostOnly)
        {
            Vector3 axialDirection = -GlobalBasis.Z.Normalized();
            if (entryVelocity.Dot(axialDirection) < 0.0f)
            {
                axialDirection = -axialDirection;
            }
            boostableVelocity = axialDirection * entryVelocity.Dot(axialDirection);
            preservedVelocity = entryVelocity - boostableVelocity;
        }
        else
        {
            Vector3 lateralAxis = GlobalBasis.X.Normalized();
            preservedVelocity = lateralAxis * entryVelocity.Dot(lateralAxis);
            boostableVelocity = entryVelocity - preservedVelocity;
        }
        float exitBoostableSpeed = Mathf.Sqrt(Mathf.Max(
            0.0f,
            (LastExitSpeed * LastExitSpeed) - preservedVelocity.LengthSquared()));
        Vector3 boostDirection = boostableVelocity.LengthSquared() > 0.01f
            ? boostableVelocity.Normalized()
            : -GlobalBasis.Z.Normalized();
        Vector3 exitVelocity = preservedVelocity + (boostDirection * exitBoostableSpeed);
        if (!float.IsPositiveInfinity(MaximumDownwardExitSpeed) && exitVelocity.Y < -MaximumDownwardExitSpeed)
        {
            exitVelocity.Y = -MaximumDownwardExitSpeed;
        }
        player.LinearVelocity = exitVelocity;

        IsActivated = true;
        _trackedPlayers.Clear();
        _activeRing.Show();
        _activationSfx?.Play();
        Passed?.Invoke(player);
    }
}
