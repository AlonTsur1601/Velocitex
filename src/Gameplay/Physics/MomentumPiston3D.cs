using Godot;
using Velocitex.Core.Physics;
using Velocitex.Gameplay.Player;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Gameplay.Physics;

public partial class MomentumPiston3D : Node3D, IImpulseDevice
{
    public event Action<PlayerBall>? Armed;
    public event Action<RigidBody3D>? Fired;

    [Export] public Vector3 LaunchVelocity { get; set; } = new(0.0f, 20.2f, 0.0f);
    [Export] public Vector3 SeatOffset { get; set; } = new(0.0f, 1.15f, 0.0f);
    [Export] public int WindUpTicks { get; set; } = 24;
    [Export] public bool EnableAudio { get; set; } = true;

    public bool HasFired { get; private set; }
    public bool IsArmed => _target is not null && !HasFired;

    private RigidBody3D? _target;
    private Node3D _headRoot = null!;
    private MeshInstance3D _chargeRing = null!;
    private OmniLight3D _chargeLight = null!;
    private AudioStreamPlayer3D? _fireAudio;
    private int _windUpTick;

    public override void _Ready()
    {
        BuildVisual();
        BuildCaptureArea();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_target is null || HasFired || !IsInstanceValid(_target))
        {
            return;
        }

        _target.GlobalPosition = ToGlobal(SeatOffset);
        _target.LinearVelocity = Vector3.Zero;
        _target.AngularVelocity = Vector3.Zero;
        _target.Sleeping = false;
        _windUpTick++;
        float charge = Mathf.Clamp(_windUpTick / (float)Math.Max(1, WindUpTicks), 0.0f, 1.0f);
        _chargeRing.Scale = Vector3.One * (1.0f + (charge * 0.22f));
        _chargeLight.LightEnergy = 0.4f + (charge * 2.8f);
        _headRoot.Position = new Vector3(0.0f, 1.3f - (charge * 0.42f), 0.0f);

        if (_windUpTick >= Math.Max(1, WindUpTicks))
        {
            TryApplyImpulse(_target);
        }
    }

    public Vector3 PreviewImpulse(RigidBody3D target)
    {
        return GlobalBasis * LaunchVelocity;
    }

    public bool TryApplyImpulse(RigidBody3D target)
    {
        if (HasFired)
        {
            return false;
        }

        HasFired = true;
        target.GlobalPosition = ToGlobal(SeatOffset);
        target.LinearVelocity = PreviewImpulse(target);
        target.AngularVelocity = Vector3.Zero;
        target.Sleeping = false;
        _target = null;
        _fireAudio?.Play();
        _headRoot.Position = new Vector3(0.0f, 1.85f, 0.0f);
        Tween tween = CreateTween().SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_headRoot, "position", new Vector3(0.0f, 1.3f, 0.0f), 0.42f);
        Fired?.Invoke(target);
        return true;
    }

    public void ResetPiston()
    {
        _target = null;
        _windUpTick = 0;
        HasFired = false;
        _headRoot.Position = new Vector3(0.0f, 1.3f, 0.0f);
        _chargeRing.Scale = Vector3.One;
        _chargeLight.LightEnergy = 0.4f;
        _fireAudio?.Stop();
    }

    private void BuildCaptureArea()
    {
        Area3D captureArea = new()
        {
            Name = "CaptureCradle",
            Position = SeatOffset,
            CollisionLayer = 0,
            CollisionMask = 1,
            Monitoring = true,
        };
        captureArea.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3.2f, 2.5f, 3.2f) } });
        captureArea.BodyEntered += body =>
        {
            if (!HasFired && _target is null && body is PlayerBall player)
            {
                _target = player;
                _windUpTick = 0;
                Armed?.Invoke(player);
            }
        };
        AddChild(captureArea);
    }

    private void BuildVisual()
    {
        StandardMaterial3D steel = RoomGeometry.CreateMaterial("res://assets/textures/brushed_metal.png", new Color("7f8c91"), 0.5f, 0.58f);
        StandardMaterial3D copper = RoomGeometry.CreateMaterial("res://assets/textures/copper_rivets.svg", new Color("9b684d"), 0.4f, 0.52f);
        StandardMaterial3D dark = RoomGeometry.CreateMaterial("res://assets/textures/rubber_chevrons.svg", new Color("263038"), 0.04f, 0.94f);
        StandardMaterial3D charge = RoomGeometry.CreateMaterial("res://assets/textures/sugar_glaze.svg", new Color("f2bd58"), 0.12f, 0.38f, emissionEnabled: true, emission: new Color("744511"));

        RoomGeometry.AddCylinder(this, "Base", new Vector3(0.0f, 0.25f, 0.0f), Vector3.Zero, 1.65f, 0.5f, steel);
        RoomGeometry.AddCylinder(this, "Sleeve", new Vector3(0.0f, 0.72f, 0.0f), Vector3.Zero, 1.05f, 0.95f, dark);
        foreach (float y in new[] { 0.52f, 0.78f, 1.04f, 1.3f })
        {
            AddChild(new MeshInstance3D
            {
                Name = $"Coil{y}",
                Position = new Vector3(0.0f, y, 0.0f),
                Mesh = new TorusMesh { InnerRadius = 1.02f, OuterRadius = 1.18f, Rings = 28, RingSegments = 8 },
                MaterialOverride = copper,
            });
        }

        _headRoot = new Node3D { Name = "PistonHead", Position = new Vector3(0.0f, 1.3f, 0.0f) };
        AddChild(_headRoot);
        RoomGeometry.AddCylinder(_headRoot, "Ram", Vector3.Zero, Vector3.Zero, 0.68f, 1.35f, steel);
        RoomGeometry.AddCylinder(_headRoot, "CradlePad", new Vector3(0.0f, 0.78f, 0.0f), Vector3.Zero, 1.32f, 0.26f, dark);
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddVisualBox(_headRoot, $"CradleJaw{side}", new Vector3(0.3f, 0.75f, 1.9f), new Vector3(side * 1.2f, 0.85f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, copper);
        }

        _chargeRing = new MeshInstance3D
        {
            Name = "ChargeRing",
            Position = new Vector3(0.0f, 0.18f, 0.0f),
            Mesh = new TorusMesh { InnerRadius = 1.75f, OuterRadius = 1.9f, Rings = 36, RingSegments = 8 },
            MaterialOverride = charge,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_chargeRing);
        _chargeLight = new OmniLight3D { Name = "ChargeLight", Position = SeatOffset, LightColor = new Color("f0a942"), LightEnergy = 0.4f, OmniRange = 8.0f, ShadowEnabled = false };
        AddChild(_chargeLight);

        if (EnableAudio)
        {
            _fireAudio = new AudioStreamPlayer3D
            {
                Name = "PistonFireSfx",
                Stream = GD.Load<AudioStream>("res://assets/audio/sfx/device_piston_fire.wav"),
                Bus = "SFX",
                Position = SeatOffset,
                MaxDistance = 46.0f,
                UnitSize = 9.0f,
            };
            AddChild(_fireAudio);
        }
    }
}
