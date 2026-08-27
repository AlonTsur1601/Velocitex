using Godot;
using Velocitex.Core.Input;
using Velocitex.Core.Physics;
using Velocitex.Core.Profile;
using Velocitex.Core.Save;
using Velocitex.Gameplay.Physics;
using Velocitex.Gameplay.Rooms;
using Velocitex.Gameplay.Visual;

namespace Velocitex.Gameplay.Player;

public partial class PlayerBall : RigidBody3D
{
    private const float Radius = 0.6f;
    private const float FirstPersonTrailHideSpeed = 32.0f;
    private const int ManualRestartStabilizationPhysicsFrames = 12;
    private const int ManualRestartNoBouncePhysicsFrames = 18;
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
    public Color TrailColor => _trailMaterial.AlbedoColor;
    public string AppliedPatternId { get; private set; } = "none";
    public string AppliedCrownId => _crown?.AppliedCrownId ?? "none-crown";
    public bool IsCrownVisible => _crown?.Visible == true;
    public Vector3 GroundNormal { get; private set; } = Vector3.Up;
    public SurfaceKind GroundSurfaceKind { get; private set; } = SurfaceKind.Standard;
    public bool GroundUsesStickyRollSfx { get; private set; }
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
    public bool IsManualRestartStabilizing => _manualRestartStabilizationFramesRemaining > 0;
    public bool IsManualRestartImpactSuppressionActive => _manualRestartNoBounceFramesRemaining > 0;

    private PlayerMotorConfig _config = null!;
    private MeshInstance3D _visual = null!;
    private ShaderMaterial _candyMaterial = null!;
    private GpuParticles3D _trail = null!;
    private StandardMaterial3D _trailMaterial = null!;
    private CandyCrown3D _crown = null!;
    private bool _trailEnabled;
    private bool _firstPersonView;
    private float _oneWayRetainedForwardSpeed;
    private ulong _oneWaySurfaceInstanceId;
    private Vector3 _previousLinearVelocity;
    private bool _wasGroundedOnSuperElastic;
    private bool _groundedOnStaticSurface;
    private ulong _groundSurfaceInstanceId;
    private ulong _lastElasticBounceSurfaceInstanceId;
    private Basis _visualRollingBasis = Basis.Identity;
    private bool _zeroMomentumGuardEnabled;
    private ulong _zeroMomentumUntilPhysicsFrame;
    private int _manualRestartStabilizationFramesRemaining;
    private int _manualRestartNoBounceFramesRemaining;
    private Transform3D _manualRestartTransform;
    private PhysicsMaterial? _manualRestartOriginalPhysicsMaterial;
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
        _crown = new CandyCrown3D { Name = "Crown", FollowParentUpright = true };
        AddChild(_crown);
        ContactMonitor = true;
        MaxContactsReported = 8;
        CanSleep = false;
        _visualRollingBasis = _visual.GlobalBasis.Orthonormalized();
    }

    public override void _Process(double delta)
    {
        _visual.GlobalBasis = _visualRollingBasis;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_manualRestartStabilizationFramesRemaining <= 0)
        {
            if (_manualRestartNoBounceFramesRemaining > 0 &&
                --_manualRestartNoBounceFramesRemaining == 0)
            {
                RestoreManualRestartPhysicsMaterial();
            }
            return;
        }

        // The pause-menu restart can resume into Area3D overlap notifications,
        // stale direct-body state and a contact-recovery impulse from the old
        // low-gravity frame. Pin the ball at spawn for a short, deterministic
        // physics window and clear every velocity path on every one of those
        // frames. This intentionally favors a guaranteed still restart over a
        // visually imperceptible fraction of a second of immediate input.
        GlobalTransform = _manualRestartTransform;
        ClearMomentumThroughPhysicsFrame(
            Engine.GetPhysicsFrames() + (ulong)_manualRestartStabilizationFramesRemaining + 2UL);
        Freeze = true;
        Sleeping = false;
        _manualRestartStabilizationFramesRemaining--;
        if (_manualRestartStabilizationFramesRemaining > 0)
        {
            return;
        }

        Freeze = false;
        Sleeping = false;
        GlobalTransform = _manualRestartTransform;
        ClearMomentumThroughPhysicsFrame(Engine.GetPhysicsFrames() + 2UL);
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (_zeroMomentumGuardEnabled && Engine.GetPhysicsFrames() <= _zeroMomentumUntilPhysicsFrame)
        {
            // ResetRoom can run from an Area3D callback while this physics step
            // still owns an older direct-body snapshot. Overwrite that snapshot
            // in the same step so no force, impulse or cached velocity can win.
            state.Transform = GlobalTransform;
            state.LinearVelocity = Vector3.Zero;
            state.AngularVelocity = Vector3.Zero;
            state.Sleeping = false;
            _previousLinearVelocity = Vector3.Zero;
            return;
        }

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
            bool usesSimulatedInput = SimulatedMoveInput.HasValue;
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
            Vector3 cameraForward = (-MovementBasis.GlobalBasis.Z).Slide(GroundNormal).Normalized();
            Vector3 cameraRight = cameraForward.Cross(GroundNormal).Normalized();
            Vector3 desiredDirection = ((cameraRight * input.X) + (cameraForward * -input.Y)).Normalized();
            Vector3 planarVelocity = state.LinearVelocity.Slide(GroundNormal);
            float driveTraction = ResolveDriveTraction(desiredDirection);
            Vector3 driveAcceleration = usesSimulatedInput
                ? ResolveVectorDriveAcceleration(planarVelocity, desiredDirection, driveTraction, (float)state.Step)
                : ResolveKeyboardDriveAcceleration(
                    planarVelocity,
                    cameraRight,
                    cameraForward,
                    input,
                    driveTraction,
                    (float)state.Step);
            if (driveAcceleration.LengthSquared() > 0.0001f)
            {
                state.ApplyCentralForce(driveAcceleration * Mass);
                state.ApplyTorque(GroundNormal.Cross(driveAcceleration) * Mass * Radius * 0.45f);
            }
        }
        finally
        {
            UpdateVisualRollingBasis(state);
            MaximumSpeedSinceReset = Mathf.Max(MaximumSpeedSinceReset, state.LinearVelocity.Length());
            _previousLinearVelocity = state.LinearVelocity;
            _wasGroundedOnSuperElastic = IsGrounded && IsElasticSurface(GroundSurfaceKind);
        }
    }

    public void ResetTo(Transform3D spawnTransform)
    {
        RestoreManualRestartPhysicsMaterial();
        _manualRestartStabilizationFramesRemaining = 0;
        _zeroMomentumGuardEnabled = false;
        Freeze = false;
        ResetCount++;
        IsGrounded = false;
        GroundNormal = Vector3.Up;
        GroundSurfaceKind = SurfaceKind.Standard;
        GroundUsesStickyRollSfx = false;
        GroundTraction = 1.0f;
        GroundLinearDrag = 0.0f;
        GroundSurfaceAcceleration = Vector3.Zero;
        GroundGripDirection = Vector3.Forward;
        GroundBounceMultiplier = 1.0f;
        CurrentMoveInput = Vector2.Zero;
        _visualRollingBasis = spawnTransform.Basis.Orthonormalized();
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
        ReleaseFromForceVolumes();
        _airControlSources.Clear();
        _oneWayRetainedForwardSpeed = 0.0f;
        _oneWaySurfaceInstanceId = 0UL;
        GlobalTransform = spawnTransform;
        ClearMomentumInCurrentPhysicsFrame();
        Sleeping = false;
        _trail.Emitting = false;
        ResetPerformed?.Invoke();
    }

    public void BeginManualRestartStabilization()
    {
        BeginManualRestartImpactSuppression();
        _manualRestartTransform = GlobalTransform;
        _manualRestartStabilizationFramesRemaining = ManualRestartStabilizationPhysicsFrames;
        Freeze = true;
        Sleeping = false;
        ClearMomentumThroughPhysicsFrame(
            Engine.GetPhysicsFrames() + (ulong)ManualRestartStabilizationPhysicsFrames + 2UL);
    }

    private void BeginManualRestartImpactSuppression()
    {
        RestoreManualRestartPhysicsMaterial();
        _manualRestartNoBounceFramesRemaining = ManualRestartNoBouncePhysicsFrames;
        if (PhysicsMaterialOverride is not PhysicsMaterial original)
        {
            return;
        }

        _manualRestartOriginalPhysicsMaterial = original;
        PhysicsMaterial zeroBounce = (PhysicsMaterial)original.Duplicate();
        zeroBounce.Bounce = 0.0f;
        PhysicsMaterialOverride = zeroBounce;
    }

    private void RestoreManualRestartPhysicsMaterial()
    {
        if (_manualRestartOriginalPhysicsMaterial is not null)
        {
            PhysicsMaterialOverride = _manualRestartOriginalPhysicsMaterial;
            _manualRestartOriginalPhysicsMaterial = null;
        }
        _manualRestartNoBounceFramesRemaining = 0;
    }

    private void ClearMomentumInCurrentPhysicsFrame()
    {
        ClearMomentumNow(includeDeferredWrites: false);
    }

    private void ClearMomentumThroughPhysicsFrame(ulong physicsFrame)
    {
        _zeroMomentumGuardEnabled = true;
        _zeroMomentumUntilPhysicsFrame = Math.Max(_zeroMomentumUntilPhysicsFrame, physicsFrame);
        ClearMomentumNow(includeDeferredWrites: true);
    }

    private void ClearMomentumNow(bool includeDeferredWrites)
    {
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        ConstantForce = Vector3.Zero;
        ConstantTorque = Vector3.Zero;
        if (includeDeferredWrites)
        {
            SetDeferred(RigidBody3D.PropertyName.LinearVelocity, Vector3.Zero);
            SetDeferred(RigidBody3D.PropertyName.AngularVelocity, Vector3.Zero);
        }

        Rid body = GetRid();
        if (body.IsValid)
        {
            PhysicsServer3D.BodySetState(body, PhysicsServer3D.BodyState.Transform, GlobalTransform);
            PhysicsServer3D.BodySetState(body, PhysicsServer3D.BodyState.LinearVelocity, Vector3.Zero);
            PhysicsServer3D.BodySetState(body, PhysicsServer3D.BodyState.AngularVelocity, Vector3.Zero);
            PhysicsServer3D.BodySetState(body, PhysicsServer3D.BodyState.Sleeping, false);
        }
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
        UpdateCrownVisibility();
        UpdateTrailVisibility(LinearVelocity);
    }

    public void ApplyProfile(PlayerProfile profile, bool trailAllowed)
    {
        CandyVisualStyle.ApplyCandyMaterial(_candyMaterial, profile);
        _crown.Apply(profile.CrownId);
        UpdateCrownVisibility();
        AppliedPatternId = profile.PatternId;
        bool showTrail = trailAllowed && !string.Equals(profile.TrailId, "off", StringComparison.Ordinal);
        _trailEnabled = showTrail;
        _trail.Amount = profile.TrailStyleId switch
        {
            "solid-line" => 54,
            "dotted" => 18,
            "dashed" => 28,
            "pulse" => 40,
            _ => 30,
        };
        _trail.Lifetime = profile.TrailStyleId switch
        {
            "solid-line" => 0.95,
            "dotted" => 0.58,
            "dashed" => 0.76,
            "pulse" => 1.05,
            _ => 0.72,
        };
        _trail.Emitting = showTrail && LinearVelocity.LengthSquared() >= 1.0f;
        UpdateTrailVisibility(LinearVelocity);
        if (!showTrail)
        {
            return;
        }

        Color trailColor = CandyVisualStyle.ResolveTrailColor(profile.TrailId);
        trailColor.A = 1.0f;
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

        _trail.GlobalPosition = GlobalPosition;
    }

    private void UpdateTrailVisibility(Vector3 velocity)
    {
        _trail.Visible = !_firstPersonView || velocity.LengthSquared() < FirstPersonTrailHideSpeed * FirstPersonTrailHideSpeed;
    }

    private void UpdateCrownVisibility()
    {
        _crown.Visible = !_firstPersonView &&
            !string.Equals(_crown.AppliedCrownId, "none-crown", StringComparison.Ordinal);
    }

    private void ApplyTrailColor(Color color)
    {
        _trailMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _trailMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Disabled;
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
        bool stickyGroundContact = false;

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
            bool usesStickyRollSfx = collider?.HasMeta(RoomGeometry.StickyRollSfxMetadata) == true &&
                collider.GetMeta(RoomGeometry.StickyRollSfxMetadata).AsBool();
            stickyGroundContact |= usesStickyRollSfx;
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

        GroundUsesStickyRollSfx = IsGrounded && stickyGroundContact;

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
        return Godot.Input.GetVector(
            InputDefaults.MoveLeft,
            InputDefaults.MoveRight,
            InputDefaults.MoveForward,
            InputDefaults.MoveBack,
            0.0f);
    }

    private Vector3 ResolveKeyboardDriveAcceleration(
        Vector3 planarVelocity,
        Vector3 cameraRight,
        Vector3 cameraForward,
        Vector2 input,
        float driveTraction,
        float step)
    {
        Vector3 acceleration = Vector3.Zero;
        bool horizontalHeld = Mathf.Abs(input.X) > 0.0001f;
        bool verticalHeld = Mathf.Abs(input.Y) > 0.0001f;
        if (horizontalHeld)
        {
            acceleration += cameraRight * ResolveHeldAxisAcceleration(
                planarVelocity.Dot(cameraRight),
                input.X * _config.MaximumDriveSpeed,
                driveTraction,
                step);
        }
        if (verticalHeld)
        {
            acceleration += cameraForward * ResolveHeldAxisAcceleration(
                planarVelocity.Dot(cameraForward),
                -input.Y * _config.MaximumDriveSpeed,
                driveTraction,
                step);
        }

        // While some input remains held, bleed only the travel component
        // perpendicular to that input. The perpendicular component is one
        // world-space vector (not separate X/Z damping), so releasing one key
        // of a diagonal preserves that direction gently without braking
        // externally supplied momentum that already follows the held key.
        // When both axes are held they are both actively driven. In particular,
        // W+D -> W+A must cross zero and accelerate left even if forward speed
        // is already at its cap; forward speed cannot consume the horizontal
        // axis's acceleration budget.
        if (horizontalHeld != verticalHeld)
        {
            Vector3 desiredDirection = ((cameraRight * input.X) + (cameraForward * -input.Y)).Normalized();
            float speedInDesiredDirection = planarVelocity.Dot(desiredDirection);
            Vector3 lateralVelocity = planarVelocity - desiredDirection * speedInDesiredDirection;
            float lateralSpeed = lateralVelocity.Length();
            if (lateralSpeed > 0.0001f)
            {
                float steeringDeceleration = _config.ActiveSteeringDeceleration * driveTraction;
                acceleration -= lateralVelocity / lateralSpeed *
                    Mathf.Min(steeringDeceleration, lateralSpeed / step);
            }
        }

        float maximumAcceleration = Mathf.Max(_config.GroundAcceleration, _config.GroundBraking) * driveTraction;
        return acceleration.LimitLength(maximumAcceleration);
    }

    private float ResolveHeldAxisAcceleration(
        float currentSpeed,
        float targetSpeed,
        float driveTraction,
        float step)
    {
        if (Mathf.Sign(currentSpeed) == Mathf.Sign(targetSpeed) &&
            Mathf.Abs(currentSpeed) >= Mathf.Abs(targetSpeed))
        {
            // Input must never clamp externally supplied momentum that already
            // travels in the requested direction.
            return 0.0f;
        }

        float rate = currentSpeed * targetSpeed < -0.01f
            ? _config.GroundBraking
            : _config.GroundAcceleration;
        float requiredAcceleration = (targetSpeed - currentSpeed) / step;
        return Mathf.Clamp(requiredAcceleration, -rate * driveTraction, rate * driveTraction);
    }

    private Vector3 ResolveVectorDriveAcceleration(
        Vector3 planarVelocity,
        Vector3 desiredDirection,
        float driveTraction,
        float step)
    {
        float speedInDesiredDirection = planarVelocity.Dot(desiredDirection);
        if (speedInDesiredDirection >= _config.MaximumDriveSpeed)
        {
            return Vector3.Zero;
        }

        float acceleration = (speedInDesiredDirection < -0.1f
            ? _config.GroundBraking
            : _config.GroundAcceleration) * driveTraction;
        float remainingSpeed = _config.MaximumDriveSpeed - speedInDesiredDirection;
        float allowedAcceleration = Mathf.Min(acceleration, remainingSpeed / step);
        return desiredDirection * allowedAcceleration;
    }

    private bool ReleaseFromForceVolumes()
    {
        if (!IsInsideTree())
        {
            return false;
        }

        bool released = false;
        foreach (Node node in GetTree().GetNodesInGroup(ForceVolume3D.ForceVolumeGroup))
        {
            if (node is ForceVolume3D forceVolume)
            {
                released |= forceVolume.ContainsBody(this);
                forceVolume.ReleaseBody(this);
            }
        }

        return released;
    }

    private void UpdateVisualRollingBasis(PhysicsDirectBodyState3D state)
    {
        Vector3 visualAngularVelocity = state.AngularVelocity;
        if (IsGrounded)
        {
            Vector3 planarVelocity = state.LinearVelocity.Slide(GroundNormal);
            Vector3 rollingAngularVelocity = GroundNormal.Cross(planarVelocity) / Radius;
            visualAngularVelocity = rollingAngularVelocity + state.AngularVelocity.Project(GroundNormal);
        }

        float angularSpeed = visualAngularVelocity.Length();
        if (angularSpeed <= 0.0001f)
        {
            return;
        }

        Basis rotation = new(visualAngularVelocity / angularSpeed, angularSpeed * (float)state.Step);
        _visualRollingBasis = (rotation * _visualRollingBasis).Orthonormalized();
    }

    private void ApplyAirControl(PhysicsDirectBodyState3D state)
    {
        Vector2 input = (SimulatedMoveInput ?? ReadMoveInput()).LimitLength(1.0f);
        CurrentMoveInput = input;
        if (_airControlSources.Count == 0 || MovementBasis is null)
        {
            return;
        }

        if (input.LengthSquared() < 0.0001f)
        {
            return;
        }

        float acceleration = _airControlSources.Values.Max(source => source.Acceleration);
        float maximumSpeed = _airControlSources.Values.Max(source => source.MaximumSpeed);
        Vector3 cameraRight = MovementBasis.GlobalBasis.X.Slide(Vector3.Up).Normalized();
        Vector3 cameraForward = (-MovementBasis.GlobalBasis.Z).Slide(Vector3.Up).Normalized();
        Vector3 planarVelocity = state.LinearVelocity.Slide(Vector3.Up);
        Vector3 airAcceleration = Vector3.Zero;
        if (Mathf.Abs(input.X) > 0.0001f)
        {
            airAcceleration += cameraRight * ResolveAirAxisAcceleration(
                planarVelocity.Dot(cameraRight),
                input.X * maximumSpeed,
                acceleration,
                (float)state.Step);
        }
        if (Mathf.Abs(input.Y) > 0.0001f)
        {
            airAcceleration += cameraForward * ResolveAirAxisAcceleration(
                planarVelocity.Dot(cameraForward),
                -input.Y * maximumSpeed,
                acceleration,
                (float)state.Step);
        }

        airAcceleration = airAcceleration.LimitLength(acceleration);
        state.ApplyCentralForce(airAcceleration * Mass);
    }

    private static float ResolveAirAxisAcceleration(
        float currentSpeed,
        float targetSpeed,
        float acceleration,
        float step)
    {
        if (Mathf.Sign(currentSpeed) == Mathf.Sign(targetSpeed) &&
            Mathf.Abs(currentSpeed) >= Mathf.Abs(targetSpeed))
        {
            return 0.0f;
        }

        return Mathf.Clamp(
            (targetSpeed - currentSpeed) / step,
            -acceleration,
            acceleration);
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
