using Godot;
using Velocitex.Core.Interaction;
using Velocitex.Core.Physics;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Gameplay.Interaction;

public partial class PlayerCannon3D : Node3D, IInteractable, IImpulseDevice
{
    public event Action<RigidBody3D>? Fired;

    [Export] public Vector3 LaunchVelocity { get; set; } = new(0.0f, 12.5f, -18.0f);
    [Export] public Vector3 MuzzleOffset { get; set; } = new(0.0f, 2.2f, -2.0f);
    [Export] public float ActivationRadius { get; set; } = 4.5f;

    public string InteractionPrompt => "FIRE CANNON";
    public bool HasFired { get; private set; }
    public bool HasSolidBodyHitbox =>
        GetNodeOrNull<StaticBody3D>("CannonHitbox")?.GetChildren().OfType<CollisionShape3D>().Count(shape => !shape.Disabled) >= 4;

    private Label3D _keyLabel = null!;
    private MeshInstance3D _focusRing = null!;
    private Node3D _barrelRoot = null!;

    public override void _Ready()
    {
        BuildVisual();
    }

    public bool CanInteract(Node interactor)
    {
        return !HasFired && interactor is Node3D node && node.GlobalPosition.DistanceTo(GlobalPosition) <= ActivationRadius;
    }

    public void Interact(Node interactor)
    {
        if (!CanInteract(interactor) || interactor is not RigidBody3D body)
        {
            return;
        }

        TryApplyImpulse(body);
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
        target.GlobalPosition = ToGlobal(MuzzleOffset);
        target.LinearVelocity = PreviewImpulse(target);
        target.AngularVelocity = Vector3.Zero;
        target.Sleeping = false;
        _keyLabel.Hide();
        _barrelRoot.Scale = new Vector3(1.08f, 0.92f, 1.08f);
        Tween tween = CreateTween().SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_barrelRoot, "scale", Vector3.One, 0.34f);
        Fired?.Invoke(target);
        return true;
    }

    public void ResetCannon()
    {
        HasFired = false;
        _keyLabel.Hide();
        _focusRing.Scale = Vector3.One;
        _barrelRoot.Scale = Vector3.One;
    }

    public void SetFocused(bool focused, bool highContrast)
    {
        bool show = focused && !HasFired;
        _keyLabel.Visible = show;
        _keyLabel.Modulate = highContrast ? Colors.White : new Color("f4d68b");
        _focusRing.Scale = show ? Vector3.One * 1.08f : Vector3.One;
    }

    public void SetKeyLabel(string keyLabel)
    {
        _keyLabel.Text = $"[ {keyLabel} ]";
    }

    private void BuildVisual()
    {
        StandardMaterial3D bodyMaterial = RoomGeometry.CreateMaterial("res://assets/textures/brushed_metal.png", new Color("788b96"), 0.46f, 0.56f);
        StandardMaterial3D barrelMaterial = RoomGeometry.CreateMaterial("res://assets/textures/copper_rivets.svg", new Color("9c694d"), 0.44f, 0.5f);
        StandardMaterial3D darkMaterial = RoomGeometry.CreateMaterial("res://assets/textures/rubber_chevrons.svg", new Color("26333a"), 0.04f, 0.94f);
        StandardMaterial3D ringMaterial = RoomGeometry.CreateMaterial("res://assets/textures/brushed_metal.png", new Color("e0bd72"), 0.2f, 0.62f, emissionEnabled: true, emission: new Color("614418"));

        RoomGeometry.AddCylinder(this, "Turntable", new Vector3(0.0f, 0.22f, 0.0f), Vector3.Zero, 1.7f, 0.44f, bodyMaterial);
        RoomGeometry.AddVisualBox(this, "Seat", new Vector3(1.7f, 0.75f, 1.5f), new Vector3(0.0f, 0.75f, 1.2f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, darkMaterial);

        _barrelRoot = new Node3D { Name = "BarrelRoot", Position = new Vector3(0.0f, 1.65f, -0.7f) };
        AddChild(_barrelRoot);
        RoomGeometry.AddCylinder(_barrelRoot, "WideBarrel", Vector3.Zero, new Vector3(Mathf.DegToRad(-55.0f), 0.0f, 0.0f), 1.1f, 4.4f, barrelMaterial, 0.86f);
        RoomGeometry.AddCylinder(_barrelRoot, "DarkMuzzle", new Vector3(0.0f, 1.25f, -1.78f), new Vector3(Mathf.DegToRad(-55.0f), 0.0f, 0.0f), 0.82f, 0.16f, darkMaterial);
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            RoomGeometry.AddVisualBox(this, $"Support{side}", new Vector3(0.36f, 2.4f, 0.5f), new Vector3(side * 1.25f, 1.35f, -0.4f), new Vector3(0.0f, 0.0f, Mathf.DegToRad(side * 8.0f)), string.Empty, Colors.White, 0.0f, 1.0f, bodyMaterial);
        }

        StaticBody3D hitbox = new()
        {
            Name = "CannonHitbox",
            CollisionLayer = 1,
            CollisionMask = 1,
        };
        hitbox.AddChild(new CollisionShape3D
        {
            Name = "TurntableHitbox",
            Position = new Vector3(0.0f, 0.22f, 0.0f),
            Shape = new CylinderShape3D { Radius = 1.7f, Height = 0.44f },
        });
        hitbox.AddChild(new CollisionShape3D
        {
            Name = "SeatHitbox",
            Position = new Vector3(0.0f, 0.75f, 1.2f),
            Shape = new BoxShape3D { Size = new Vector3(1.7f, 0.75f, 1.5f) },
        });
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            hitbox.AddChild(new CollisionShape3D
            {
                Name = side < 0.0f ? "LeftSupportHitbox" : "RightSupportHitbox",
                Position = new Vector3(side * 1.25f, 1.35f, -0.4f),
                Rotation = new Vector3(0.0f, 0.0f, Mathf.DegToRad(side * 8.0f)),
                Shape = new BoxShape3D { Size = new Vector3(0.36f, 2.4f, 0.5f) },
            });
        }
        AddChild(hitbox);

        _focusRing = new MeshInstance3D { Name = "FocusRing", Position = new Vector3(0.0f, 0.12f, 0.0f), Mesh = new TorusMesh { InnerRadius = 1.82f, OuterRadius = 1.96f, Rings = 32, RingSegments = 8 }, MaterialOverride = ringMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_focusRing);
        _keyLabel = new Label3D { Name = "KeyLabel", Position = new Vector3(0.0f, 4.6f, 0.0f), Text = "[ E ]", FontSize = 68, OutlineSize = 14, Modulate = new Color("f4d68b"), OutlineModulate = new Color(0.03f, 0.04f, 0.05f, 0.95f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, Visible = false };
        AddChild(_keyLabel);
    }
}
