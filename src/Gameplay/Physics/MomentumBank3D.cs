using Godot;
using Velocitex.Core.Interaction;
using Velocitex.Core.Physics;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Gameplay.Physics;

public partial class MomentumBank3D : Node3D, IInteractable, IImpulseDevice
{
    [Signal] public delegate void MomentumCapturedEventHandler(PlayerBall player, float storedSpeed);
    [Signal] public delegate void MomentumReleasedEventHandler(PlayerBall player, float releaseSpeed);
    public string InteractionPrompt => "RELEASE MOMENTUM";
    public float ActivationRadius { get; set; } = 4.2f;
    public Vector3 ReleaseDirection { get; set; } = new(0.0f, 0.9f, -1.0f);
    public float ReleaseMultiplier { get; set; } = 1.35f;
    public float MinimumReleaseSpeed { get; set; } = 21.5f;
    public bool ChargeByTime { get; set; }
    public float ChargeDurationSeconds { get; set; } = 3.0f;
    public float ChargeCapacity { get; set; } = 24.0f;
    public bool OpenApproach { get; set; }
    public bool EnableAudio { get; set; } = true;
    public bool HasCharge => _capturedPlayer is not null;
    public bool IsFullyCharged => !ChargeByTime || StoredSpeed >= ChargeCapacity - 0.01f;
    public float StoredSpeed { get; private set; }

    private PlayerBall? _capturedPlayer;
    private Vector3 _seatPosition;
    private Label3D _keyLabel = null!;
    private MeshInstance3D[] _meterSegments = Array.Empty<MeshInstance3D>();
    private StandardMaterial3D _meterOff = null!;
    private StandardMaterial3D _meterOn = null!;
    private AudioStreamPlayer3D? _audio;
    private bool _chargeCompleteNotified;

    public override void _Ready()
    {
        BuildVisual();
        Area3D capture = new() { Name = "CaptureArea", Position = new Vector3(0.0f, 1.0f, 1.1f), CollisionLayer = 0, CollisionMask = 1, Monitoring = true };
        capture.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(4.2f, 2.6f, 3.8f) } });
        capture.BodyEntered += Capture;
        AddChild(capture);
        _seatPosition = new Vector3(0.0f, 1.15f, -0.25f);
        if (EnableAudio)
        {
            _audio = new AudioStreamPlayer3D { Name = "ReleaseSfx", Stream = GD.Load<AudioStream>("res://assets/audio/sfx/device_piston_fire.wav"), Bus = "SFX", MaxDistance = 50.0f, UnitSize = 8.0f };
            AddChild(_audio);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_capturedPlayer is null) { return; }
        if (ChargeByTime && !IsFullyCharged)
        {
            float chargePerSecond = ChargeCapacity / Mathf.Max(ChargeDurationSeconds, 0.1f);
            StoredSpeed = Mathf.Min(ChargeCapacity, StoredSpeed + (chargePerSecond * (float)delta));
            UpdateMeter(StoredSpeed);
            if (IsFullyCharged && !_chargeCompleteNotified)
            {
                _chargeCompleteNotified = true;
                EmitSignal(SignalName.MomentumCaptured, _capturedPlayer, StoredSpeed);
            }
        }
        _capturedPlayer.GlobalPosition = ToGlobal(_seatPosition);
        _capturedPlayer.LinearVelocity = Vector3.Zero;
        _capturedPlayer.AngularVelocity = Vector3.Zero;
    }

    public bool CanInteract(Node interactor) =>
        _capturedPlayer == interactor &&
        interactor is Node3D node &&
        node.GlobalPosition.DistanceTo(GlobalPosition) <= ActivationRadius;

    public void Interact(Node interactor)
    {
        if (!CanInteract(interactor) || _capturedPlayer is null) { return; }
        TryApplyImpulse(_capturedPlayer);
    }

    public Vector3 PreviewImpulse(RigidBody3D target)
    {
        float speed;
        if (ChargeByTime)
        {
            float chargeRatio = Mathf.Clamp(StoredSpeed / Mathf.Max(ChargeCapacity, 0.01f), 0.0f, 1.0f);
            float maximumReleaseSpeed = ChargeCapacity * ReleaseMultiplier;
            speed = Mathf.Lerp(MinimumReleaseSpeed, maximumReleaseSpeed, chargeRatio);
        }
        else
        {
            speed = Mathf.Max(MinimumReleaseSpeed, StoredSpeed * ReleaseMultiplier);
        }
        return (GlobalBasis * ReleaseDirection).Normalized() * speed;
    }

    public bool TryApplyImpulse(RigidBody3D target)
    {
        if (_capturedPlayer != target) { return false; }
        PlayerBall player = _capturedPlayer;
        Vector3 velocity = PreviewImpulse(target);
        _capturedPlayer = null;
        player.Freeze = false;
        player.LinearVelocity = velocity;
        player.AngularVelocity = GlobalBasis.X * (velocity.Length() * 0.8f);
        player.Sleeping = false;
        _keyLabel.Hide();
        UpdateMeter(0.0f);
        _audio?.Play();
        EmitSignal(SignalName.MomentumReleased, player, velocity.Length());
        return true;
    }

    public void SetFocused(bool focused, bool highContrast)
    {
        _keyLabel.Visible = focused && HasCharge;
        _keyLabel.Modulate = highContrast ? Colors.White : new Color("f2d781");
    }

    public void SetKeyLabel(string keyLabel) => _keyLabel.Text = $"[ {keyLabel} ]";

    public void ResetBank()
    {
        if (_capturedPlayer is not null) { _capturedPlayer.Freeze = false; }
        _capturedPlayer = null; StoredSpeed = 0.0f; _chargeCompleteNotified = false; _keyLabel.Hide(); UpdateMeter(0.0f); _audio?.Stop();
    }

    private void Capture(Node3D body)
    {
        if (_capturedPlayer is not null || body is not PlayerBall player) { return; }
        StoredSpeed = ChargeByTime ? 0.0f : Mathf.Max(player.LinearVelocity.Length(), 8.0f);
        _chargeCompleteNotified = !ChargeByTime;
        _capturedPlayer = player;
        player.LinearVelocity = Vector3.Zero; player.AngularVelocity = Vector3.Zero; player.GlobalPosition = ToGlobal(_seatPosition); player.Freeze = true;
        UpdateMeter(StoredSpeed);
        if (!ChargeByTime) { EmitSignal(SignalName.MomentumCaptured, player, StoredSpeed); }
    }

    private void UpdateMeter(float speed)
    {
        float segmentSpeed = ChargeByTime ? ChargeCapacity / _meterSegments.Length : 3.0f;
        int lit = Mathf.Clamp(Mathf.CeilToInt(speed / Mathf.Max(segmentSpeed, 0.01f)), 0, _meterSegments.Length);
        for (int index = 0; index < _meterSegments.Length; index++) { _meterSegments[index].MaterialOverride = index < lit ? _meterOn : _meterOff; }
    }

    private void BuildVisual()
    {
        const string metal = "res://assets/textures/brushed_metal.png"; const string copper = "res://assets/textures/copper_rivets.svg";
        StandardMaterial3D frame = RoomGeometry.CreateMaterial(metal, new Color("5f6b70"), 0.48f, 0.55f);
        StandardMaterial3D wheel = RoomGeometry.CreateMaterial(copper, new Color("9e6f49"), 0.45f, 0.52f);
        _meterOff = RoomGeometry.CreateMaterial(metal, new Color("242b2e"), 0.18f, 0.78f);
        _meterOn = RoomGeometry.CreateMaterial(copper, new Color("e2c15f"), 0.22f, 0.48f, emissionEnabled: true, emission: new Color("80631b"));
        RoomGeometry.AddVisualBox(this, "Base", new Vector3(5.2f, 0.45f, 4.4f), new Vector3(0.0f, 0.22f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frame);
        if (OpenApproach)
        {
            RoomGeometry.AddVisualBox(this, "LeftPylon", new Vector3(0.5f, 3.8f, 0.5f), new Vector3(-2.35f, 2.0f, 1.55f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frame);
            RoomGeometry.AddVisualBox(this, "RightPylon", new Vector3(0.5f, 3.8f, 0.5f), new Vector3(2.35f, 2.0f, 1.55f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frame);
            RoomGeometry.AddVisualBox(this, "TopBeam", new Vector3(5.2f, 0.5f, 0.5f), new Vector3(0.0f, 3.65f, 1.55f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frame);
        }
        else
        {
            RoomGeometry.AddVisualBox(this, "Back", new Vector3(5.2f, 3.8f, 0.5f), new Vector3(0.0f, 2.0f, 1.55f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, frame);
        }
        float wheelX = 0.0f;
        float wheelY = OpenApproach ? 2.35f : 2.05f;
        float wheelRadius = OpenApproach ? 0.9f : 1.45f;
        RoomGeometry.AddCylinder(this, "Flywheel", new Vector3(wheelX, wheelY, 1.18f), new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f), wheelRadius, 0.55f, wheel);
        RoomGeometry.AddCylinder(this, "Hub", new Vector3(wheelX, wheelY, 0.86f), new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f), OpenApproach ? 0.3f : 0.42f, 0.65f, frame);
        _meterSegments = new MeshInstance3D[8];
        for (int index = 0; index < _meterSegments.Length; index++)
        {
            MeshInstance3D segment;
            if (ChargeByTime)
            {
                segment = RoomGeometry.AddVisualBox(this, $"Meter{index}", new Vector3(0.4f, 0.22f, 0.13f), new Vector3(-1.54f + (index * 0.44f), 4.14f, 0.78f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, _meterOff);
            }
            else
            {
                float angle = Mathf.Lerp(-2.3f, 0.45f, index / 7.0f);
                segment = RoomGeometry.AddVisualBox(this, $"Meter{index}", new Vector3(0.22f, 0.5f, 0.13f), new Vector3(Mathf.Cos(angle) * 1.85f, 2.05f + (Mathf.Sin(angle) * 1.85f), 0.78f), new Vector3(0.0f, 0.0f, angle - (Mathf.Pi / 2.0f)), string.Empty, Colors.White, 0.0f, 1.0f, _meterOff);
            }
            _meterSegments[index] = segment;
        }
        _keyLabel = new Label3D { Name = "KeyLabel", Position = new Vector3(0.0f, 4.8f, 0.0f), Text = "[ E ]", FontSize = 68, OutlineSize = 14, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, Visible = false };
        AddChild(_keyLabel);
    }
}
