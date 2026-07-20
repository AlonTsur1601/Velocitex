using Godot;
using Velocitex.Core.Interaction;
using Velocitex.Gameplay.Rooms;

namespace Velocitex.Gameplay.Interaction;

public partial class MechanicalLever : Node3D, IInteractable
{
    public event Action? Activated;

    public string InteractionPrompt => "PULL LEVER";
    public float ActivationRadius { get; set; } = 3.4f;
    public bool InteractionEnabled { get; set; } = true;
    public bool IsActivated { get; private set; }

    private Node3D _handleRoot = null!;
    private Label3D _keyLabel = null!;
    private MeshInstance3D _focusRing = null!;
    private AudioStreamPlayer3D? _activationAudio;
    private Tween? _handleTween;

    public override void _Ready()
    {
        BuildVisual();
    }

    public bool CanInteract(Node interactor)
    {
        return InteractionEnabled && !IsActivated &&
            interactor is Node3D interactor3D &&
            interactor3D.GlobalPosition.DistanceTo(GlobalPosition) <= ActivationRadius;
    }

    public void Interact(Node interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        IsActivated = true;
        _keyLabel.Hide();
        _handleTween?.Kill();
        _handleTween = CreateTween().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _handleTween.TweenProperty(_handleRoot, "rotation:z", Mathf.DegToRad(42.0f), 0.34f);
        _activationAudio?.Play();
        Activated?.Invoke();
    }

    public void ResetLever()
    {
        _handleTween?.Kill();
        _handleTween = null;
        IsActivated = false;
        _handleRoot.Rotation = new Vector3(0.0f, 0.0f, Mathf.DegToRad(-38.0f));
        _keyLabel.Hide();
        _focusRing.Scale = Vector3.One;
    }

    public void SetFocused(bool focused, bool highContrast)
    {
        bool show = focused && !IsActivated;
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
        // The removed broad foot used to lift the narrow mechanism by 0.16 m.
        // Shift every remaining part down together so the pedestal now rests
        // on the surface represented by this node's origin.
        const float footRemovalOffset = 0.16f;
        const string metal = "res://assets/textures/brushed_metal.png";
        const string copper = "res://assets/textures/copper_rivets.svg";
        const string rubber = "res://assets/textures/rubber_chevrons.svg";

        StandardMaterial3D baseMaterial = RoomGeometry.CreateMaterial(metal, new Color("8c9ba3"), 0.46f, 0.58f);
        StandardMaterial3D insetMaterial = RoomGeometry.CreateMaterial(rubber, new Color("3f555f"), 0.04f, 0.9f);
        StandardMaterial3D copperMaterial = RoomGeometry.CreateMaterial(copper, new Color("c17b55"), 0.42f, 0.5f);
        StandardMaterial3D gripMaterial = RoomGeometry.CreateMaterial(rubber, new Color("68584d"), 0.02f, 0.92f);
        StandardMaterial3D ringMaterial = RoomGeometry.CreateMaterial(
            metal,
            new Color("d0b56f"),
            0.18f,
            0.66f,
            emissionEnabled: true,
            emission: new Color("3f3217"));

        RoomGeometry.AddVisualBox(this, "Pedestal", new Vector3(1.25f, 1.5f, 1.1f), new Vector3(0.0f, 0.75f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, baseMaterial);
        RoomGeometry.AddVisualBox(this, "Inset", new Vector3(0.82f, 0.72f, 0.08f), new Vector3(0.0f, 1.0f - footRemovalOffset, -0.58f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, insetMaterial);
        RoomGeometry.AddCylinder(this, "Pivot", new Vector3(0.0f, 1.5f, -0.02f), new Vector3(Mathf.Pi / 2.0f, 0.0f, 0.0f), 0.38f, 1.3f, copperMaterial);

        StaticBody3D baseCollision = new() { Name = "BaseCollision" };
        baseCollision.AddChild(new CollisionShape3D
        {
            Name = "PedestalHitbox",
            Position = new Vector3(0.0f, 0.75f, 0.0f),
            Rotation = Vector3.Zero,
            Shape = new BoxShape3D { Size = new Vector3(1.25f, 1.5f, 1.1f) },
        });
        AddChild(baseCollision);

        _handleRoot = new Node3D
        {
            Name = "Handle",
            Position = new Vector3(0.0f, 1.5f, -0.05f),
            Rotation = new Vector3(0.0f, 0.0f, Mathf.DegToRad(-38.0f)),
        };
        AddChild(_handleRoot);
        RoomGeometry.AddVisualBox(_handleRoot, "Stem", new Vector3(0.2f, 1.5f, 0.2f), new Vector3(0.0f, 0.7f, 0.0f), Vector3.Zero, string.Empty, Colors.White, 0.0f, 1.0f, copperMaterial);
        _handleRoot.AddChild(new MeshInstance3D
        {
            Name = "Grip",
            Position = new Vector3(0.0f, 1.48f, 0.0f),
            Mesh = new SphereMesh
            {
                Radius = 0.34f,
                Height = 0.68f,
                RadialSegments = 20,
                Rings = 10,
            },
            MaterialOverride = gripMaterial,
        });
        AnimatableBody3D handleCollision = new() { Name = "HandleCollision" };
        handleCollision.AddChild(new CollisionShape3D
        {
            Name = "StemHitbox",
            Position = new Vector3(0.0f, 0.7f, 0.0f),
            Shape = new BoxShape3D { Size = new Vector3(0.24f, 1.5f, 0.24f) },
        });
        handleCollision.AddChild(new CollisionShape3D
        {
            Name = "GripHitbox",
            Position = new Vector3(0.0f, 1.48f, 0.0f),
            Shape = new SphereShape3D { Radius = 0.34f },
        });
        _handleRoot.AddChild(handleCollision);

        _focusRing = new MeshInstance3D
        {
            Name = "FocusRing",
            Position = Vector3.Zero,
            Mesh = new TorusMesh
            {
                InnerRadius = 1.05f,
                OuterRadius = 1.17f,
                Rings = 32,
                RingSegments = 8,
            },
            MaterialOverride = ringMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_focusRing);

        _keyLabel = new Label3D
        {
            Name = "KeyLabel",
            Position = new Vector3(0.0f, 3.55f - footRemovalOffset, 0.0f),
            Text = "[ E ]",
            FontSize = 68,
            OutlineSize = 14,
            Modulate = new Color("f4d68b"),
            OutlineModulate = new Color(0.03f, 0.04f, 0.05f, 0.95f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Visible = false,
        };
        AddChild(_keyLabel);

        _activationAudio = new AudioStreamPlayer3D
        {
            Name = "ActivationClickSfx",
            Stream = GD.Load<AudioStream>("res://assets/audio/sfx/device_mechanical_click.wav"),
            Bus = "SFX",
            VolumeDb = -4.0f,
            MaxDistance = 26.0f,
            UnitSize = 5.0f,
        };
        AddChild(_activationAudio);
    }

    public override void _ExitTree()
    {
        _handleTween?.Kill();
        _activationAudio?.Stop();
        if (_activationAudio is not null)
        {
            _activationAudio.Stream = null;
        }
        Activated = null;
    }
}
