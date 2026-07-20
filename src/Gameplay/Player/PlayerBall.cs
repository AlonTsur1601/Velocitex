using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Physics;
using Velocitex.Core.Profile;
using Velocitex.Core.Save;
using Velocitex.Gameplay.Physics;

namespace Velocitex.Gameplay.Player;

public partial class PlayerBall : RigidBody3D
{
    private const float Radius = 0.6f;
    private const float FirstPersonTrailHideSpeed = 32.0f;
    public static readonly StringName PlayerGroup = "player_ball";

    [Export] public PlayerMotorConfig? MotorConfig { get; set; }

    public Node3D? MovementBasis { get; set; }
    public Vector2? SimulatedMoveInput { get; set; }
    public Vector2 CurrentMoveInput { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsVisualVisible => _visual.Visible;
    public bool IsTrailEmitting => _trail.Emitting;
    public bool IsTrailVisible => _trail.Visible;
    public bool IsTrailEnabled => _trailEnabled;
    public string AppliedPatternId { get; private set; } = "none";
    public Vector3 GroundNormal { get; private set; } = Vector3.Up;
    public SurfaceKind GroundSurfaceKind { get; private set; } = SurfaceKind.Standard;
    public float GroundTraction { get; private set; } = 1.0f;
    public float GroundLinearDrag { get; private set; }
    public Vector3 GroundSurfaceAcceleration { get; private set; } = Vector3.Zero;
    public Vector3 GroundGripDirection { get; private set; } = Vector3.Forward;
    public float GroundBounceMultiplier { get; private set; } = 1.0f;
    public float LastSuperElasticImpactSpeed { get; private set; }
    public float LastSuperElasticLaunchSpeed { get; private set; }
    public int SuperElasticBounceCount { get; private set; }
    public int ConsecutiveElasticBounceCount { get; private set; }
    public ulong LastElasticBounceSurfaceInstanceId => _lastElasticBounceSurfaceInstanceId;
    public int ResetCount { get; private set; }
    public float MaximumSpeedSinceReset { get; private set; }
    public bool TouchedSideBoundarySinceReset { get; private set; }
    public bool AirborneCollisionSinceReset { get; private set; }
    public float LastLandingImpactSpeed { get; private set; }
    public int CollisionImpactCount { get; private set; }
    public float LastCollisionImpactSpeed { get; private set; }
    public SurfaceKind LastCollisionSurfaceKind { get; private set; } = SurfaceKind.Standard;
    public float AirControlAcceleration => _airControlSources.Count == 0
        ? 0.0f
        : _airControlSources.Values.Max(source => source.Acceleration);
    public event Action? ResetPerformed;

    private PlayerMotorConfig _config = null!;
    private MeshInstance3D _visual = null!;
    private ShaderMaterial _candyMaterial = null!;
    private GpuParticles3D _trail = null!;
    private StandardMaterial3D _trailMaterial = null!;
    private Color _baseTrailColor = Colors.White;
    private bool _glassTrailTintApplied;
    private bool _trailEnabled;
    private bool _firstPersonView;
    private float _oneWayRetainedForwardSpeed;
    private ulong _oneWaySurfaceInstanceId;
    private Vector3 _previousLinearVelocity;
    private bool _wasGroundedOnSuperElastic;
    private bool _groundedOnStaticSurface;
    private ulong _groundSurfaceInstanceId;
    private ulong _lastElasticBounceSurfaceInstanceId;
    private readonly HashSet<ulong> _previousContactIds = new();
    private readonly HashSet<ulong> _currentContactIds = new();
    private readonly Dictionary<ulong, AirControlSource> _airControlSources = new();

    private readonly record struct AirControlSource(float Acceleration, float MaximumSpeed);

    public override void _Ready()
    {
        _config = MotorConfig ?? new PlayerMotorConfig();
        _visual = GetNode<MeshInstance3D>("Visual");
        _trail = GetNode<GpuParticles3D>("Trail");
        _trail.Layers = 1u << 1;
        _candyMaterial = (ShaderMaterial)_visual.MaterialOverride.Duplicate();
        _visual.MaterialOverride = _candyMaterial;
        SphereMesh trailMesh = (SphereMesh)_trail.DrawPass1;
        _trailMaterial = (StandardMaterial3D)trailMesh.Material.Duplicate();
        trailMesh = (SphereMesh)trailMesh.Duplicate();
        trailMesh.Material = _trailMaterial;
        _trail.DrawPass1 = trailMesh;
        ContactMonitor = true;
        MaxContactsReported = 8;
        CanSleep = false;
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        UpdateTrailEmission(state.LinearVelocity);
        UpdateGroundState(state);
        SuppressStaticSurfaceSeamLift(state);
        ApplySuperElasticBounce(state);
        try
        {
            if (!IsGrounded)
            {
                ApplyAirControl(state);
                return;
            }

            ApplyGroundLinearDrag(state);
            ApplyOneWayGrip(state);
            ApplyGroundSurfaceAcceleration(state);
            Vector2 input = SimulatedMoveInput ?? ReadMoveInput();
            CurrentMoveInput = input;
            if (MovementBasis is null)
            {
                return;
            }

            if (input.LengthSquared() < 0.0001f)
            {
                ApplyCoastingDeceleration(state);
                return;
            }

            input = input.LimitLength(1.0f);
            Vector3 cameraRight = MovementBasis.GlobalBasis.X;
            Vector3 cameraForward = -MovementBasis.GlobalBasis.Z;
            Vector3 desiredDirection = (cameraRight * input.X) + (cameraForward * -input.Y);
            desiredDirection = desiredDirection.Slide(GroundNormal).Normalized();

            Vector3 planarVelocity = state.LinearVelocity.Slide(GroundNormal);
            float speedInDesiredDirection = planarVelocity.Dot(desiredDirection);
            if (speedInDesiredDirection >= _config.MaximumDriveSpeed)
            {
                return;
            }

            float driveTraction = ResolveDriveTraction(desiredDirection);
            float acceleration = (planarVelocity.Dot(desiredDirection) < -0.1f
                ? _config.GroundBraking
                : _config.GroundAcceleration) * driveTraction;
            float remainingSpeed = _config.MaximumDriveSpeed - speedInDesiredDirection;
            float allowedAcceleration = Mathf.Min(acceleration, remainingSpeed / (float)state.Step);
            Vector3 force = desiredDirection * allowedAcceleration * Mass;

            state.ApplyCentralForce(force);
            state.ApplyTorque(GroundNormal.Cross(desiredDirection) * allowedAcceleration * Mass * Radius * 0.45f);
        }
        finally
        {
            MaximumSpeedSinceReset = Mathf.Max(MaximumSpeedSinceReset, state.LinearVelocity.Length());
            _previousLinearVelocity = state.LinearVelocity;
            _wasGroundedOnSuperElastic = IsGrounded && IsElasticSurface(GroundSurfaceKind);
        }
    }

    public void ResetTo(Transform3D spawnTransform)
    {
        ResetCount++;
        IsGrounded = false;
        GroundNormal = Vector3.Up;
        GroundSurfaceKind = SurfaceKind.Standard;
        GroundTraction = 1.0f;
        GroundLinearDrag = 0.0f;
        GroundSurfaceAcceleration = Vector3.Zero;
        GroundGripDirection = Vector3.Forward;
        GroundBounceMultiplier = 1.0f;
        CurrentMoveInput = Vector2.Zero;
        LastSuperElasticImpactSpeed = 0.0f;
        LastSuperElasticLaunchSpeed = 0.0f;
        SuperElasticBounceCount = 0;
        ConsecutiveElasticBounceCount = 0;
        MaximumSpeedSinceReset = 0.0f;
        TouchedSideBoundarySinceReset = false;
        AirborneCollisionSinceReset = false;
        LastLandingImpactSpeed = 0.0f;
        CollisionImpactCount = 0;
        LastCollisionImpactSpeed = 0.0f;
        LastCollisionSurfaceKind = SurfaceKind.Standard;
        _previousContactIds.Clear();
        _currentContactIds.Clear();
        _previousLinearVelocity = Vector3.Zero;
        _wasGroundedOnSuperElastic = false;
        _groundedOnStaticSurface = false;
        _groundSurfaceInstanceId = 0UL;
        _lastElasticBounceSurfaceInstanceId = 0UL;
        _airControlSources.Clear();
        _oneWayRetainedForwardSpeed = 0.0f;
        _oneWaySurfaceInstanceId = 0UL;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        GlobalTransform = spawnTransform;
        Sleeping = false;
        _trail.Emitting = false;
        ResetPerformed?.Invoke();
    }

    public void SetAirControlSource(ulong sourceId, float acceleration, float maximumSpeed)
    {
        if (acceleration <= 0.0f)
        {
            _airControlSources.Remove(sourceId);
            return;
        }

        _airControlSources[sourceId] = new AirControlSource(
            Mathf.Max(acceleration, 0.0f),
            Mathf.Max(maximumSpeed, 0.1f));
    }

    public void ClearAirControlSource(ulong sourceId)
    {
        _airControlSources.Remove(sourceId);
    }

    public void SetFirstPersonView(bool firstPerson)
    {
        _firstPersonView = firstPerson;
        _visual.Visible = !firstPerson;
        UpdateTrailVisibility(LinearVelocity);
    }

    public void ApplyProfile(PlayerProfile profile, bool trailAllowed)
    {
        CandyVisualStyle.ApplyCandyMaterial(_candyMaterial, profile);
        AppliedPatternId = profile.PatternId;
        bool showTrail = trailAllowed && !string.Equals(profile.TrailId, "off", StringComparison.Ordinal);
        _trailEnabled = showTrail;
        _trail.Emitting = showTrail && LinearVelocity.LengthSquared() >= 1.0f;
        UpdateTrailVisibility(LinearVelocity);
        if (!showTrail)
        {
            return;
        }

        Color trailColor = CandyVisualStyle.ResolveTrailColor(profile.TrailId);
        trailColor.A = 0.62f;
        _baseTrailColor = trailColor;
        _glassTrailTintApplied = false;
        ApplyTrailColor(trailColor);
    }

    private void UpdateTrailEmission(Vector3 velocity)
    {
        float speedSquared = velocity.LengthSquared();
        _trail.Emitting = _trailEnabled && speedSquared >= 1.0f;
        UpdateTrailVisibility(velocity);
        if (!_trail.Emitting)
        {
            return;
        }

        bool shouldUseGlassTint = GroundSurfaceKind == SurfaceKind.Frictionless;
        if (shouldUseGlassTint != _glassTrailTintApplied)
        {
            _glassTrailTintApplied = shouldUseGlassTint;
            Color color = shouldUseGlassTint
                ? _baseTrailColor.Lerp(Colors.White, 0.48f)
                : _baseTrailColor;
            color.A = _baseTrailColor.A;
            ApplyTrailColor(color);
        }

        _trail.GlobalPosition = GlobalPosition;
    }

    private void UpdateTrailVisibility(Vector3 velocity)
    {
        _trail.Visible = !_firstPersonView || velocity.LengthSquared() < FirstPersonTrailHideSpeed * FirstPersonTrailHideSpeed;
    }

    private void ApplyTrailColor(Color color)
    {
        _trailMaterial.AlbedoColor = color;
        _trailMaterial.Emission = new Color(color.R, color.G, color.B, 1.0f);
    }

    private void UpdateGroundState(PhysicsDirectBodyState3D state)
    {
        bool wasGrounded = IsGrounded;
        IsGrounded = false;
        GroundNormal = Vector3.Up;
        GroundSurfaceKind = SurfaceKind.Standard;
        GroundTraction = 1.0f;
        GroundLinearDrag = 0.0f;
        GroundSurfaceAcceleration = Vector3.Zero;
        GroundGripDirection = Vector3.Forward;
        GroundBounceMultiplier = 1.0f;
        _groundedOnStaticSurface = false;
        _groundSurfaceInstanceId = 0UL;
        _currentContactIds.Clear();
        float strongestNewImpact = 0.0f;
        SurfaceKind strongestImpactSurface = SurfaceKind.Standard;

        for (int contactIndex = 0; contactIndex < state.GetContactCount(); contactIndex++)
        {
            Vector3 normal = state.GetContactLocalNormal(contactIndex).Normalized();
            GodotObject? collider = state.GetContactColliderObject(contactIndex);
            ulong colliderId = collider?.GetInstanceId() ?? 0UL;
            _currentContactIds.Add(colliderId);
            float impactSpeed = Mathf.Max(0.0f, -_previousLinearVelocity.Dot(normal));
            bool supportContact = normal.Dot(Vector3.Up) >= _config.MinimumGroundNormalY;
            float minimumAudibleImpact = supportContact
                ? (wasGrounded ? 0.9f : 0.42f)
                : 0.18f;
            if (!_previousContactIds.Contains(colliderId) &&
                impactSpeed >= minimumAudibleImpact &&
                impactSpeed > strongestNewImpact)
            {
                strongestNewImpact = impactSpeed;
                strongestImpactSurface = ResolveSurfaceKind(collider);
            }
            if (normal.Dot(Vector3.Up) < _config.MinimumGroundNormalY)
            {
                if (IsSideBoundary(collider))
                {
                    TouchedSideBoundarySinceReset = true;
                }

                if (!wasGrounded)
                {
                    AirborneCollisionSinceReset = true;
                }
            }

            if (normal.Dot(Vector3.Up) < _config.MinimumGroundNormalY)
            {
                continue;
            }

            ProfiledSurfaceBody? profiledSurface = collider as ProfiledSurfaceBody;
            SurfaceProfile? profile = profiledSurface?.Profile;
            float traction = Mathf.Clamp(profile?.Friction ?? 1.0f, 0.0f, 1.0f);
            float linearDrag = Mathf.Max(profile?.LinearDrag ?? 0.0f, 0.0f);
            if (!IsGrounded ||
                traction < GroundTraction ||
                (Mathf.IsEqualApprox(traction, GroundTraction) && linearDrag > GroundLinearDrag))
            {
                GroundNormal = normal;
                GroundSurfaceKind = profile?.Kind ?? SurfaceKind.Standard;
                GroundTraction = traction;
                GroundLinearDrag = linearDrag;
                GroundSurfaceAcceleration = profiledSurface is null || profile is null
                    ? Vector3.Zero
                    : profiledSurface.GlobalBasis * profile.Acceleration;
                GroundGripDirection = profiledSurface is null || profile is null
                    ? Vector3.Forward
                    : (profiledSurface.GlobalBasis * profile.GripDirection).Normalized();
                GroundBounceMultiplier = Mathf.Max(profile?.BounceMultiplier ?? 1.0f, 0.0f);
                _groundedOnStaticSurface = collider is StaticBody3D;
                _groundSurfaceInstanceId = colliderId;
            }

            IsGrounded = true;
        }

        if (IsGrounded && !IsElasticSurface(GroundSurfaceKind))
        {
            ConsecutiveElasticBounceCount = 0;
            _lastElasticBounceSurfaceInstanceId = 0UL;
        }

        if (!wasGrounded && IsGrounded)
        {
            LastLandingImpactSpeed = Mathf.Max(0.0f, -_previousLinearVelocity.Dot(GroundNormal));
        }

        _previousContactIds.Clear();
        foreach (ulong colliderId in _currentContactIds)
        {
            _previousContactIds.Add(colliderId);
        }

        if (strongestNewImpact > 0.0f)
        {
            LastCollisionImpactSpeed = strongestNewImpact;
            LastCollisionSurfaceKind = strongestImpactSurface;
            CollisionImpactCount++;
        }
    }

    private void ApplyGroundLinearDrag(PhysicsDirectBodyState3D state)
    {
        if (GroundLinearDrag <= 0.0f)
        {
            return;
        }

        Vector3 normalVelocity = state.LinearVelocity.Project(GroundNormal);
        Vector3 planarVelocity = state.LinearVelocity - normalVelocity;
        float retainedVelocity = Mathf.Exp(-GroundLinearDrag * (float)state.Step);
        state.LinearVelocity = normalVelocity + (planarVelocity * retainedVelocity);
    }

    private void ApplyCoastingDeceleration(PhysicsDirectBodyState3D state)
    {
        Vector3 normalVelocity = state.LinearVelocity.Project(GroundNormal);
        Vector3 planarVelocity = state.LinearVelocity - normalVelocity;
        float planarSpeed = planarVelocity.Length();
        float deceleration = _config.CoastingDeceleration * GroundTraction;
        float angularSpeed = state.AngularVelocity.Length();
        if (angularSpeed > 0.001f)
        {
            float angularDeceleration = deceleration / Radius;
            float nextAngularSpeed = Mathf.MoveToward(
                angularSpeed,
                0.0f,
                angularDeceleration * (float)state.Step);
            state.AngularVelocity *= nextAngularSpeed / angularSpeed;
        }

        if (planarSpeed < 0.001f)
        {
            state.LinearVelocity = normalVelocity;
            return;
        }

        float nextSpeed = Mathf.MoveToward(planarSpeed, 0.0f, deceleration * (float)state.Step);
        state.LinearVelocity = normalVelocity + (planarVelocity * (nextSpeed / planarSpeed));
    }

    private static Vector2 ReadMoveInput()
    {
        Vector2 input = new(
            Godot.Input.GetActionStrength(InputDefaults.MoveRight) - Godot.Input.GetActionStrength(InputDefaults.MoveLeft),
            Godot.Input.GetActionStrength(InputDefaults.MoveBack) - Godot.Input.GetActionStrength(InputDefaults.MoveForward));
        return input.LimitLength(1.0f);
    }

    private void ApplyAirControl(PhysicsDirectBodyState3D state)
    {
        if (_airControlSources.Count == 0 || MovementBasis is null)
        {
            CurrentMoveInput = Vector2.Zero;
            return;
        }

        Vector2 input = (SimulatedMoveInput ?? ReadMoveInput()).LimitLength(1.0f);
        CurrentMoveInput = input;
        if (input.LengthSquared() < 0.0001f)
        {
            return;
        }

        float acceleration = _airControlSources.Values.Max(source => source.Acceleration);
        float maximumSpeed = _airControlSources.Values.Max(source => source.MaximumSpeed);
        Vector3 cameraRight = MovementBasis.GlobalBasis.X.Slide(Vector3.Up).Normalized();
        Vector3 cameraForward = (-MovementBasis.GlobalBasis.Z).Slide(Vector3.Up).Normalized();
        Vector3 desiredDirection = ((cameraRight * input.X) + (cameraForward * -input.Y)).Normalized();
        float speedInDesiredDirection = state.LinearVelocity.Slide(Vector3.Up).Dot(desiredDirection);
        if (speedInDesiredDirection >= maximumSpeed)
        {
            return;
        }

        float remainingSpeed = maximumSpeed - speedInDesiredDirection;
        float allowedAcceleration = Mathf.Min(acceleration, remainingSpeed / (float)state.Step);
        state.ApplyCentralForce(desiredDirection * allowedAcceleration * Mass);
    }

    private void SuppressStaticSurfaceSeamLift(PhysicsDirectBodyState3D state)
    {
        if (!IsGrounded ||
            !_groundedOnStaticSurface ||
            IsElasticSurface(GroundSurfaceKind))
        {
            return;
        }

        float separatingSpeed = state.LinearVelocity.Dot(GroundNormal);
        if (separatingSpeed > 0.0f && separatingSpeed <= 2.4f)
        {
            state.LinearVelocity -= GroundNormal * separatingSpeed;
        }
    }

    private void ApplySuperElasticBounce(PhysicsDirectBodyState3D state)
    {
        if (!IsElasticSurface(GroundSurfaceKind) ||
            GroundBounceMultiplier <= 1.0f ||
            _wasGroundedOnSuperElastic)
        {
            return;
        }

        float incomingSpeed = Mathf.Max(
            -_previousLinearVelocity.Dot(GroundNormal),
            -state.LinearVelocity.Dot(GroundNormal));
        if (incomingSpeed < 0.35f)
        {
            return;
        }

        float launchSpeed = Mathf.Max(
            incomingSpeed * Mathf.Max(GroundBounceMultiplier, 1.2f),
            incomingSpeed + 2.5f);
        Vector3 previousTangentialVelocity = _previousLinearVelocity.Slide(GroundNormal);
        Vector3 currentTangentialVelocity = state.LinearVelocity.Slide(GroundNormal);
        Vector3 preservedTangentialVelocity = previousTangentialVelocity.LengthSquared() >= currentTangentialVelocity.LengthSquared()
            ? previousTangentialVelocity
            : currentTangentialVelocity;
        state.LinearVelocity = preservedTangentialVelocity + (GroundNormal * launchSpeed);
        LastSuperElasticImpactSpeed = incomingSpeed;
        LastSuperElasticLaunchSpeed = launchSpeed;
        SuperElasticBounceCount++;
        ConsecutiveElasticBounceCount = ResolveConsecutiveElasticSurfaceCount(
            _lastElasticBounceSurfaceInstanceId,
            _groundSurfaceInstanceId,
            ConsecutiveElasticBounceCount);
        _lastElasticBounceSurfaceInstanceId = _groundSurfaceInstanceId;
    }

    public static int ResolveConsecutiveElasticSurfaceCount(
        ulong previousSurfaceInstanceId,
        ulong currentSurfaceInstanceId,
        int currentCount)
    {
        if (currentCount <= 0 || previousSurfaceInstanceId == 0UL)
        {
            return 1;
        }

        return previousSurfaceInstanceId == currentSurfaceInstanceId
            ? currentCount
            : currentCount + 1;
    }

    private void ApplyGroundSurfaceAcceleration(PhysicsDirectBodyState3D state)
    {
        Vector3 acceleration = GroundSurfaceAcceleration.Slide(GroundNormal);
        if (acceleration.LengthSquared() < 0.0001f)
        {
            return;
        }

        state.ApplyCentralForce(acceleration * Mass);
        state.ApplyTorque(GroundNormal.Cross(acceleration) * Mass * Radius * 0.45f);
    }

    private static bool IsElasticSurface(SurfaceKind kind) => kind is SurfaceKind.SuperElastic or SurfaceKind.Gelatin;

    private static SurfaceKind ResolveSurfaceKind(GodotObject? collider) =>
        collider is ProfiledSurfaceBody { Profile: not null } surface
            ? surface.Profile.Kind
            : SurfaceKind.Standard;

    private static bool IsSideBoundary(GodotObject? collider)
    {
        if (collider is not Node node)
        {
            return false;
        }

        string name = node.Name.ToString();
        return name.Contains("Wall", StringComparison.OrdinalIgnoreCase) ||
            (name.Contains("Rail", StringComparison.OrdinalIgnoreCase) &&
             (name.Contains("Left", StringComparison.OrdinalIgnoreCase) ||
              name.Contains("Right", StringComparison.OrdinalIgnoreCase)));
    }

    private float ResolveDriveTraction(Vector3 desiredDirection)
    {
        // Low surface friction controls how well existing momentum is retained;
        // it must not make deliberate ground steering almost disappear.  Glass
        // therefore keeps its long coast while retaining enough drive authority
        // to build and redirect momentum before the player becomes airborne.
        if (GroundSurfaceKind == SurfaceKind.Frictionless)
        {
            return Mathf.Max(GroundTraction, 0.42f);
        }

        if (GroundSurfaceKind != SurfaceKind.OneWayGrip)
        {
            return GroundTraction;
        }

        Vector3 gripDirection = GroundGripDirection.Slide(GroundNormal).Normalized();
        return desiredDirection.Dot(gripDirection) >= 0.05f
            ? GroundTraction
            : Mathf.Min(GroundTraction, 0.06f);
    }

    private void ApplyOneWayGrip(PhysicsDirectBodyState3D state)
    {
        if (GroundSurfaceKind != SurfaceKind.OneWayGrip)
        {
            _oneWayRetainedForwardSpeed = 0.0f;
            _oneWaySurfaceInstanceId = 0UL;
            return;
        }

        Vector3 gripDirection = GroundGripDirection.Slide(GroundNormal).Normalized();
        if (gripDirection.LengthSquared() < 0.0001f)
        {
            return;
        }

        float speedAlongGrip = state.LinearVelocity.Dot(gripDirection);
        if (_oneWaySurfaceInstanceId != _groundSurfaceInstanceId)
        {
            _oneWaySurfaceInstanceId = _groundSurfaceInstanceId;
            _oneWayRetainedForwardSpeed = Mathf.Max(0.0f, speedAlongGrip);
            return;
        }

        if (speedAlongGrip > _oneWayRetainedForwardSpeed)
        {
            _oneWayRetainedForwardSpeed = speedAlongGrip;
            return;
        }

        if (_oneWayRetainedForwardSpeed > 0.0f && speedAlongGrip < _oneWayRetainedForwardSpeed)
        {
            state.LinearVelocity += gripDirection * (_oneWayRetainedForwardSpeed - speedAlongGrip);
        }
    }
}
