using Godot;
using Velocitex.Core.Physics;
using Velocitex.Gameplay.Player;

namespace Velocitex.Gameplay.Physics;

public partial class ForceVolume3D : Area3D
{
    [Export] public ForceVolumeProfile? Profile { get; set; }
    [Export] public bool AirborneOnly { get; set; }

    public event Action<RigidBody3D>? RigidBodyEntered;
    public event Action<RigidBody3D>? RigidBodyExited;

    private readonly HashSet<RigidBody3D> _rigidBodies = new();
    private Vector3 _defaultGravity;

    public override void _Ready()
    {
        CollisionLayer = 0;
        Monitoring = true;
        Monitorable = true;
        float gravityStrength = (float)ProjectSettings.GetSetting("physics/3d/default_gravity", 9.8).AsDouble();
        Vector3 gravityDirection = ProjectSettings.GetSetting("physics/3d/default_gravity_vector", Vector3.Down).AsVector3().Normalized();
        _defaultGravity = gravityDirection * gravityStrength;
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Profile is null)
        {
            return;
        }

        foreach (RigidBody3D body in _rigidBodies.ToArray())
        {
            if (!GodotObject.IsInstanceValid(body))
            {
                _rigidBodies.Remove(body);
                continue;
            }

            if (AirborneOnly && body is PlayerBall { IsGrounded: true })
            {
                continue;
            }

            Vector3 direction = Profile.Direction.LengthSquared() < 0.0001f
                ? Vector3.Zero
                : Profile.Direction.Normalized();
            Vector3 acceleration = Profile.Kind == ForceVolumeKind.Gravity
                ? (direction * Profile.Strength) - _defaultGravity
                : direction * Profile.Strength;
            body.ApplyCentralForce(acceleration * body.Mass);
        }
    }

    public bool ContainsBody(RigidBody3D body)
    {
        return _rigidBodies.Contains(body);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is not RigidBody3D rigidBody || !_rigidBodies.Add(rigidBody))
        {
            return;
        }

        if (rigidBody is PlayerBall player && Profile?.AirControlAcceleration > 0.0f)
        {
            player.SetAirControlSource(
                GetInstanceId(),
                Profile.AirControlAcceleration,
                Profile.AirControlMaximumSpeed);
        }

        RigidBodyEntered?.Invoke(rigidBody);
    }

    private void OnBodyExited(Node3D body)
    {
        if (body is not RigidBody3D rigidBody || !_rigidBodies.Remove(rigidBody))
        {
            return;
        }

        if (rigidBody is PlayerBall player)
        {
            player.ClearAirControlSource(GetInstanceId());
        }

        RigidBodyExited?.Invoke(rigidBody);
    }
}
