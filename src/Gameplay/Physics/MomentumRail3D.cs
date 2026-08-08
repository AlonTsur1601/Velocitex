using Godot;

namespace Velocitex.Gameplay.Physics;

public partial class MomentumRail3D : Area3D
{
    private static readonly Dictionary<RigidBody3D, MomentumRail3D> ActiveBodyRails = new();
    [Export] public Vector3 LocalStart { get; set; } = new(0.0f, 0.0f, 10.0f);
    [Export] public Vector3 LocalEnd { get; set; } = new(0.0f, 5.0f, -10.0f);
    [Export] public float CaptureRadius { get; set; } = 2.2f;
    [Export] public float MinimumSpeed { get; set; } = 13.0f;
    [Export] public float RideClearance { get; set; }
    [Export] public bool ReleaseOnBodyExit { get; set; } = true;

    public event Action<RigidBody3D>? Attached;
    public event Action<RigidBody3D>? Released;

    private readonly Dictionary<RigidBody3D, RailRide> _attachedBodies = new();

    private sealed class RailRide(float storedGravityScale, float directionSign)
    {
        public float StoredGravityScale { get; } = storedGravityScale;
        public float DirectionSign { get; } = directionSign;
        public float ClearanceOffset { get; set; }
    }

    public override void _Ready()
    {
        CollisionLayer = 0;
        Monitoring = true;
        Monitorable = true;
        BuildCaptureShape();
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _ExitTree()
    {
        BodyEntered -= OnBodyEntered;
        BodyExited -= OnBodyExited;
        foreach ((RigidBody3D body, RailRide ride) in _attachedBodies.ToArray())
        {
            if (GodotObject.IsInstanceValid(body))
            {
                body.GlobalPosition -= Vector3.Up * ride.ClearanceOffset;
                body.GravityScale = ride.StoredGravityScale;
            }
            ClearActiveClaim(body);
        }
        _attachedBodies.Clear();
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector3 start = ToGlobal(LocalStart);
        Vector3 end = ToGlobal(LocalEnd);
        Vector3 path = end - start;
        float length = path.Length();
        if (length < 0.01f)
        {
            return;
        }

        Vector3 direction = path / length;
        foreach ((RigidBody3D body, RailRide ride) in _attachedBodies.ToArray())
        {
            if (!GodotObject.IsInstanceValid(body))
            {
                _attachedBodies.Remove(body);
                ClearActiveClaim(body);
                continue;
            }

            Vector3 travelStart = ride.DirectionSign > 0.0f ? start : end;
            Vector3 travelDirection = direction * ride.DirectionSign;
            Vector3 baselinePosition = body.GlobalPosition - Vector3.Up * ride.ClearanceOffset;
            float progress = (baselinePosition - travelStart).Dot(travelDirection) / length;
            float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
            Vector3 closest = travelStart + travelDirection * clampedProgress * length;
            float clearanceOffset = Mathf.Sin(clampedProgress * Mathf.Pi) * RideClearance;
            body.GlobalPosition += Vector3.Up * (clearanceOffset - ride.ClearanceOffset);
            ride.ClearanceOffset = clearanceOffset;
            Vector3 correction = closest - baselinePosition;
            if (progress >= 0.975f)
            {
                ReleaseBody(body, ride, travelDirection);
                continue;
            }

            if (progress < -0.18f || correction.Length() > CaptureRadius * 2.6f)
            {
                ReleaseBody(body, ride, travelDirection);
                continue;
            }

            float forwardSpeed = Mathf.Max(MinimumSpeed, body.LinearVelocity.Dot(travelDirection));
            body.GravityScale = 0.0f;
            body.LinearVelocity = travelDirection * forwardSpeed + correction.LimitLength(1.4f) * 7.0f;
        }
    }

    public bool IsAttached(RigidBody3D body)
    {
        return _attachedBodies.ContainsKey(body);
    }

    public void ResetBody(RigidBody3D body)
    {
        if (_attachedBodies.Remove(body, out RailRide? ride) && ride is not null)
        {
            body.GlobalPosition -= Vector3.Up * ride.ClearanceOffset;
            body.GravityScale = ride.StoredGravityScale;
            ClearActiveClaim(body);
        }
    }

    private void BuildCaptureShape()
    {
        Vector3 path = LocalEnd - LocalStart;
        float length = path.Length();
        Vector3 direction = path.Normalized();
        Basis pathBasis = new(new Quaternion(Vector3.Back, direction));
        AddChild(new CollisionShape3D
        {
            Name = "CaptureShape",
            Position = (LocalStart + LocalEnd) * 0.5f,
            Basis = pathBasis,
            Shape = new BoxShape3D { Size = new Vector3(CaptureRadius * 2.0f, CaptureRadius * 2.0f, length) },
        });
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is not RigidBody3D rigidBody || _attachedBodies.ContainsKey(rigidBody))
        {
            return;
        }

        if (ActiveBodyRails.TryGetValue(rigidBody, out MomentumRail3D? activeRail))
        {
            if (GodotObject.IsInstanceValid(activeRail) && activeRail.IsAttached(rigidBody))
            {
                return;
            }
            ActiveBodyRails.Remove(rigidBody);
        }

        Vector3 start = ToGlobal(LocalStart);
        Vector3 end = ToGlobal(LocalEnd);
        Vector3 direction = (end - start).Normalized();
        float directionalSpeed = rigidBody.LinearVelocity.Dot(direction);
        float directionSign = Mathf.Abs(directionalSpeed) > 0.35f
            ? Mathf.Sign(directionalSpeed)
            : (rigidBody.GlobalPosition.DistanceSquaredTo(start) <= rigidBody.GlobalPosition.DistanceSquaredTo(end) ? 1.0f : -1.0f);
        _attachedBodies[rigidBody] = new RailRide(rigidBody.GravityScale, directionSign);
        ActiveBodyRails[rigidBody] = this;
        rigidBody.GravityScale = 0.0f;
        Attached?.Invoke(rigidBody);
    }

    private void OnBodyExited(Node3D body)
    {
        if (!ReleaseOnBodyExit)
        {
            return;
        }

        if (body is not RigidBody3D rigidBody || !_attachedBodies.TryGetValue(rigidBody, out RailRide? ride) || ride is null)
        {
            return;
        }

        Vector3 direction = (ToGlobal(LocalEnd) - ToGlobal(LocalStart)).Normalized() * ride.DirectionSign;
        ReleaseBody(rigidBody, ride, direction);
    }

    private void ReleaseBody(RigidBody3D body, RailRide ride, Vector3 direction)
    {
        if (!_attachedBodies.Remove(body))
        {
            return;
        }

        ClearActiveClaim(body);
        float speed = Mathf.Max(MinimumSpeed, body.LinearVelocity.Dot(direction));
        body.GlobalPosition -= Vector3.Up * ride.ClearanceOffset;
        body.GravityScale = ride.StoredGravityScale;
        body.LinearVelocity = direction * speed;
        Released?.Invoke(body);
    }

    private void ClearActiveClaim(RigidBody3D body)
    {
        if (ActiveBodyRails.TryGetValue(body, out MomentumRail3D? rail) && rail == this)
        {
            ActiveBodyRails.Remove(body);
        }
    }
}
