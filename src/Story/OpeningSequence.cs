using Godot;
using Velocitex.Core.Profile;
using Velocitex.Core.Save;
using Velocitex.Gameplay.Visual;

namespace Velocitex.Story;

public partial class OpeningSequence : Node3D
{
    [Signal]
    public delegate void FinishedEventHandler();

    [Export] public bool SmokeMode { get; set; }
    [Export] public bool SubtitlesEnabled { get; set; } = true;
    [Export] public int SubtitleScalePercent { get; set; } = 100;
    [Export] public bool SubtitleBackgroundEnabled { get; set; } = true;
    public PlayerProfile CandyProfile { get; set; } = new();

    private const float DurationSeconds = 12.5f;

    private Camera3D _camera = null!;
    private Control _subtitleContainer = null!;
    private PanelContainer _subtitlePanel = null!;
    private Label _speaker = null!;
    private Label _subtitle = null!;
    private AudioStreamPlayer _voice = null!;
    private AudioStreamPlayer _music = null!;
    private Node3D _coin = null!;
    private Node3D _knob = null!;
    private Node3D _maintenanceDoor = null!;
    private Node3D _playerCandy = null!;
    private Node3D _child = null!;
    private Node3D _mother = null!;
    private MeshInstance3D _childReachArm = null!;
    private float _childScale;
    private float _elapsed;
    private bool _finished;
    private bool _capturePreview;
    private bool _previewCaptured;
    private int _voiceCue = -1;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera3D");
        _subtitleContainer = GetNode<Control>("Ui/Subtitles");
        _subtitlePanel = GetNode<PanelContainer>("Ui/Subtitles/Panel");
        _speaker = GetNode<Label>("Ui/Subtitles/Panel/Layout/Speaker");
        _subtitle = GetNode<Label>("Ui/Subtitles/Panel/Layout/Line");
        _voice = GetNode<AudioStreamPlayer>("Voice");
        _music = GetNode<AudioStreamPlayer>("Music");
        _capturePreview = Array.Exists(OS.GetCmdlineUserArgs(), argument => argument == "--opening-preview");
        ApplySubtitleSettings();
        BuildSet();
        if (!ValidatePlayerCandyMaterial())
        {
            GD.PushError("OPENING_CANDY_STYLE_FAIL: player candy is not using the current gameplay candy shader.");
            if (SmokeMode)
            {
                GetTree().Quit(1);
                return;
            }
        }
        if (!SmokeMode)
        {
            _music.Stream = GD.Load<AudioStream>("res://assets/audio/music/machine_ambient.wav");
            _music.Play();
        }
        if (SmokeMode)
        {
            GD.Print("OPENING_SMOKE_READY: opening set built.");
        }
        _camera.LookAt(new Vector3(-0.4f, 1.45f, 0.0f), Vector3.Up);
        UpdateSequence(0.0f);
    }

    public override void _Process(double delta)
    {
        if (_finished)
        {
            return;
        }

        _elapsed += (float)delta * (SmokeMode ? 8.0f : 1.0f);
        UpdateSequence(_elapsed);
        if (_capturePreview && !_previewCaptured && _elapsed >= 8.6f)
        {
            _previewCaptured = true;
            string capturePath = ProjectSettings.GlobalizePath("user://opening-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(capturePath);
            GD.Print($"OPENING_PREVIEW_CAPTURE: {capturePath}");
            GetTree().Quit(0);
            return;
        }

        if (_elapsed >= DurationSeconds)
        {
            Finish();
        }
    }

    private void UpdateSequence(float time)
    {
        if (time < 2.9f)
        {
            SetSubtitle("CHILD", "Mom, can I get one?");
            PlayVoiceCue(0, "res://assets/audio/voice/opening_01_child.mp3");
        }
        else if (time < 5.05f)
        {
            SetSubtitle("MOTHER", "One candy. Then we're going.");
            PlayVoiceCue(1, "res://assets/audio/voice/opening_02_mother.mp3");
        }
        else if (time < 6.35f)
        {
            SetSubtitle(string.Empty, string.Empty);
        }
        else if (time < 8.75f)
        {
            SetSubtitle("CHILD", "Did it get stuck?");
            PlayVoiceCue(2, "res://assets/audio/voice/opening_03_child.mp3");
        }
        else if (time < 10.3f)
        {
            SetSubtitle("MOTHER", "Give it a moment.");
            PlayVoiceCue(3, "res://assets/audio/voice/opening_04_mother.mp3");
        }
        else
        {
            SetSubtitle(string.Empty, string.Empty);
        }

        float childWalk = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp((time - 2.35f) / 1.35f, 0.0f, 1.0f));
        _child.Position = new Vector3(-2.5f, 0.0f, 2.0f).Lerp(new Vector3(-0.88f, 0.0f, 1.34f), childWalk);
        _mother.Position = new Vector3(-4.1f, 0.0f, 2.2f).Lerp(new Vector3(-1.78f, 0.0f, 1.72f), childWalk);
        if (childWalk > 0.01f)
        {
            _child.LookAt(new Vector3(0.0f, 0.0f, 0.0f), Vector3.Up, useModelFront: true);
            _child.Position += Vector3.Up * (Mathf.Sin(childWalk * Mathf.Pi * 6.0f) * 0.025f);
            _mother.LookAt(new Vector3(0.0f, 0.0f, 0.0f), Vector3.Up, useModelFront: true);
            _mother.Position += Vector3.Up * (Mathf.Sin((childWalk * Mathf.Pi * 6.0f) + Mathf.Pi) * 0.018f);
        }

        float coinTravel = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp((time - 3.72f) / 0.86f, 0.0f, 1.0f));
        Vector3 shoulder = _child.ToGlobal(new Vector3(0.4f, 1.43f, 0.2f) * _childScale);
        Vector3 restingHand = _child.ToGlobal(new Vector3(0.47f, 0.92f, 0.08f) * _childScale);
        Vector3 slotHand = new(-0.25f, 0.77f, 0.73f);
        Vector3 handPosition = restingHand.Lerp(slotHand, coinTravel);
        _coin.GlobalPosition = PlaceChildArm(shoulder, handPosition);
        _coin.Rotation = new Vector3(Mathf.Pi * 0.5f, 0.0f, time * 4.0f);
        _coin.Visible = coinTravel < 0.985f;

        float knobTurn = Mathf.Clamp((time - 4.8f) / 1.0f, 0.0f, 1.0f);
        _knob.Rotation = new Vector3(0.0f, 0.0f, knobTurn * Mathf.Pi * 0.85f);

        float doorOpen = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp((time - 5.7f) / 1.2f, 0.0f, 1.0f));
        _maintenanceDoor.Rotation = new Vector3(0.0f, 0.0f, -doorOpen * 1.18f);

        float candyDrop = Mathf.Clamp((time - 6.1f) / 4.8f, 0.0f, 1.0f);
        if (candyDrop < 0.45f)
        {
            float first = Mathf.SmoothStep(0.0f, 1.0f, candyDrop / 0.45f);
            _playerCandy.Position = new Vector3(
                Mathf.Lerp(0.32f, 0.45f, first),
                Mathf.Lerp(2.0f, 1.1f, first),
                Mathf.Lerp(0.0f, 0.72f, first));
        }
        else
        {
            float second = Mathf.SmoothStep(0.0f, 1.0f, (candyDrop - 0.45f) / 0.55f);
            _playerCandy.Position = new Vector3(
                Mathf.Lerp(0.45f, 0.0f, second),
                Mathf.Lerp(1.1f, -0.8f, second),
                Mathf.Lerp(0.72f, -1.8f, second));
        }

        _playerCandy.Rotation = new Vector3(time * 2.2f, time * 1.6f, time * 0.8f);

        float cameraMove = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp((time - 6.25f) / 1.45f, 0.0f, 1.0f));
        Vector3 widePosition = new(5.2f, 3.2f, 7.2f);
        Vector3 closeUpPosition = new(2.6f, 2.0f, 4.6f);
        _camera.Position = widePosition.Lerp(closeUpPosition, cameraMove);
        _camera.Fov = Mathf.Lerp(52.0f, 11.0f, cameraMove);
        _camera.LookAt(new Vector3(-0.4f, 1.45f, 0.0f).Lerp(_playerCandy.Position, cameraMove), Vector3.Up);
    }

    private void SetSubtitle(string speaker, string line)
    {
        _subtitleContainer.Visible = SubtitlesEnabled && !string.IsNullOrWhiteSpace(line);
        _speaker.Text = speaker;
        _subtitle.Text = line;
    }

    private void ApplySubtitleSettings()
    {
        float scale = Mathf.Clamp(SubtitleScalePercent, 75, 150) / 100.0f;
        _speaker.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(15.0f * scale));
        _subtitle.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(22.0f * scale));
        _subtitlePanel.AddThemeStyleboxOverride("panel", CreateSubtitleStyle(SubtitleBackgroundEnabled));
    }

    private static StyleBoxFlat CreateSubtitleStyle(bool withBackground)
    {
        StyleBoxFlat style = new()
        {
            BgColor = withBackground ? new Color(0.02f, 0.035f, 0.045f, 0.88f) : Colors.Transparent,
            BorderColor = withBackground ? new Color("6c9296") : Colors.Transparent,
            BorderWidthLeft = withBackground ? 2 : 0,
            BorderWidthTop = withBackground ? 2 : 0,
            BorderWidthRight = withBackground ? 2 : 0,
            BorderWidthBottom = withBackground ? 2 : 0,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
        };
        style.ContentMarginLeft = 22.0f;
        style.ContentMarginRight = 22.0f;
        style.ContentMarginTop = 12.0f;
        style.ContentMarginBottom = 12.0f;
        return style;
    }

    private void PlayVoiceCue(int cue, string path)
    {
        if (SmokeMode || _voiceCue == cue)
        {
            return;
        }

        _voiceCue = cue;
        _voice.Stop();
        _voice.Stream = GD.Load<AudioStream>(path);
        _voice.Play();
    }

    private void Finish()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        _voice.Stop();
        _music.Stop();
        EmitSignal(SignalName.Finished);
    }

    private void BuildSet()
    {
        StandardMaterial3D metal = CreateTexturedMaterial("res://assets/textures/brushed_metal.png", 0.58f, 0.5f);
        StandardMaterial3D caramel = CreateTexturedMaterial("res://assets/textures/caramel_plates.svg", 0.08f, 0.62f);
        StandardMaterial3D rubber = CreateTexturedMaterial("res://assets/textures/rubber_chevrons.svg", 0.0f, 0.88f);
        ShaderMaterial candy = CreateCurrentCandyMaterial();
        StandardMaterial3D concrete = CreateTexturedMaterial("res://assets/textures/industrial_concrete.png", 0.0f, 0.86f);
        StandardMaterial3D copper = CreateTexturedMaterial("res://assets/textures/copper_rivets.svg", 0.38f, 0.56f);
        caramel.AlbedoColor = new Color("d06b4f");
        StandardMaterial3D dark = new() { AlbedoColor = new Color("11151a"), Roughness = 0.94f };

        AddMesh("Floor", SurfaceMeshFactory.CreateTiledBox(new Vector3(60.0f, 0.3f, 28.0f)), concrete, new Vector3(0.0f, -0.15f, 0.0f));
        AddMesh("ShopWall", SurfaceMeshFactory.CreateTiledBox(new Vector3(60.0f, 14.0f, 0.3f)), concrete, new Vector3(0.0f, 7.0f, -4.85f));
        AddMesh("WallBand", SurfaceMeshFactory.CreateTiledBox(new Vector3(60.0f, 0.55f, 0.18f)), caramel, new Vector3(0.0f, 1.05f, -4.66f));
        AddMesh("LeftColumn", SurfaceMeshFactory.CreateTiledBox(new Vector3(0.7f, 7.2f, 0.7f)), copper, new Vector3(-8.4f, 3.6f, -4.25f));
        AddMesh("DisplayPanelA", SurfaceMeshFactory.CreateTiledBox(new Vector3(3.2f, 2.2f, 0.16f)), caramel, new Vector3(-6.0f, 3.1f, -4.62f));
        AddMesh("DisplayPanelB", SurfaceMeshFactory.CreateTiledBox(new Vector3(2.5f, 2.2f, 0.16f)), rubber, new Vector3(-2.7f, 3.1f, -4.62f));
        AddMesh("Shelf", SurfaceMeshFactory.CreateTiledBox(new Vector3(6.0f, 0.24f, 1.1f)), metal, new Vector3(-5.0f, 1.55f, -3.95f));
        for (int jar = 0; jar < 4; jar++)
        {
            AddMesh($"ShelfJar{jar}", new CylinderMesh { TopRadius = 0.38f, BottomRadius = 0.38f, Height = 0.9f, RadialSegments = 18 }, new StandardMaterial3D { AlbedoColor = new Color(0.42f + (jar * 0.08f), 0.66f, 0.72f - (jar * 0.07f), 1.0f), Roughness = 0.3f }, new Vector3(-7.0f + (jar * 1.25f), 2.12f, -3.9f));
        }

        AddMesh("Pedestal", SurfaceMeshFactory.CreateTiledBox(new Vector3(1.44f, 1.06f, 1.19f)), caramel, new Vector3(0.0f, 0.53f, 0.0f));
        AddMesh("PedestalFoot", SurfaceMeshFactory.CreateTiledBox(new Vector3(1.69f, 0.16f, 1.37f)), metal, new Vector3(0.0f, 0.08f, 0.0f));
        AddMesh("GlobeCollar", new CylinderMesh { TopRadius = 0.73f, BottomRadius = 0.64f, Height = 0.24f, RadialSegments = 36 }, copper, new Vector3(0.0f, 1.18f, 0.0f));
        AddMesh("GlobeCap", new CylinderMesh { TopRadius = 0.34f, BottomRadius = 0.47f, Height = 0.18f, RadialSegments = 32 }, copper, new Vector3(0.0f, 2.93f, 0.0f));
        AddMesh("CoinPlate", SurfaceMeshFactory.CreateTiledBox(new Vector3(0.68f, 0.61f, 0.09f)), metal, new Vector3(0.0f, 0.7f, 0.63f));
        AddMesh("CoinSlot", SurfaceMeshFactory.CreateTiledBox(new Vector3(0.25f, 0.05f, 0.05f)), dark, new Vector3(-0.25f, 0.77f, 0.69f));
        AddMesh("OutletFrame", SurfaceMeshFactory.CreateTiledBox(new Vector3(0.65f, 0.35f, 0.12f)), copper, new Vector3(0.0f, 0.3f, 0.67f));
        AddMesh("OutletDark", SurfaceMeshFactory.CreateTiledBox(new Vector3(0.42f, 0.2f, 0.06f)), dark, new Vector3(0.0f, 0.29f, 0.73f));
        AddMesh("Chute", SurfaceMeshFactory.CreateTiledBox(new Vector3(0.78f, 0.16f, 1.0f)), rubber, new Vector3(0.0f, 0.34f, 0.78f), new Vector3(-0.28f, 0.0f, 0.0f));
        SurfaceDetail.AddOverlay(this, "FloorWear", new Vector3(-3.8f, 0.012f, 1.1f), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(14.0f)), new Vector2(3.2f, 2.1f), "res://assets/textures/overlays/grime.svg", new Color("30241f"), 0.42f);
        SurfaceDetail.AddOverlay(this, "MachineScratches", new Vector3(0.25f, 0.58f, 0.61f), new Vector3(0.0f, 0.0f, Mathf.DegToRad(-8.0f)), new Vector2(0.58f, 0.67f), "res://assets/textures/overlays/scratches.svg", new Color("e4c8a7"), 0.3f);
        SurfaceDetail.AddOverlay(this, "FloorOilRings", new Vector3(3.8f, 0.015f, -2.0f), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(-17.0f)), new Vector2(3.5f, 2.4f), "res://assets/textures/overlays/oil_rings.svg", new Color("211b18"), 0.42f);
        SurfaceDetail.AddOverlay(this, "PedestalScuffs", new Vector3(0.0f, 0.36f, 0.6f), new Vector3(0.0f, 0.0f, Mathf.DegToRad(-3.0f)), new Vector2(1.1f, 0.45f), "res://assets/textures/overlays/edge_scuffs.svg", new Color("e2dfd0"), 0.3f);

        StandardMaterial3D glass = new()
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(0.55f, 0.82f, 0.9f, 0.18f),
            Metallic = 0.08f,
            Roughness = 0.08f,
        };
        AddMesh("Globe", new SphereMesh { Radius = 0.92f, Height = 1.84f, RadialSegments = 44, Rings = 24 }, glass, new Vector3(0.0f, 2.03f, 0.0f));

        CinematicCandyFill.AddPackedGlobe(this, "GlobeCandies", new Vector3(0.0f, 2.03f, 0.0f), new Vector3(0.32f, 2.0f, 0.0f));

        _playerCandy = AddMesh(
            "PlayerCandy",
            new SphereMesh { Radius = CinematicCandyFill.CandyRadius, Height = CinematicCandyFill.CandyRadius * 2.0f, RadialSegments = 32, Rings = 16 },
            candy,
            new Vector3(0.32f, 2.0f, 0.0f));
        _coin = AddMesh(
            "Coin",
            new CylinderMesh { TopRadius = 0.085f, BottomRadius = 0.085f, Height = 0.025f, RadialSegments = 24 },
            new StandardMaterial3D { AlbedoColor = new Color("d8ac45"), Metallic = 0.72f, Roughness = 0.28f },
            new Vector3(-2.2f, 2.0f, 2.5f));
        _knob = AddMesh(
            "Knob",
            new CylinderMesh { TopRadius = 0.19f, BottomRadius = 0.19f, Height = 0.12f, RadialSegments = 20 },
            metal,
            new Vector3(0.25f, 0.59f, 0.72f),
            new Vector3(Mathf.Pi * 0.5f, 0.0f, 0.0f));
        _maintenanceDoor = AddMesh(
            "MaintenanceDoor",
            SurfaceMeshFactory.CreateTiledBox(new Vector3(0.37f, 0.32f, 0.07f), 0.8f),
            metal,
            new Vector3(0.45f, 0.99f, 0.63f));

        BuildPerson("Mother", new Vector3(-4.1f, 0.0f, 2.2f), 1.0f, new Color("6d4c82"));
        BuildPerson("Child", new Vector3(-2.5f, 0.0f, 2.0f), 0.72f, new Color("3e7c91"));
    }

    private void BuildPerson(string name, Vector3 position, float scale, Color clothingColor)
    {
        Node3D person = new() { Name = name, Position = position };
        StandardMaterial3D skin = new() { AlbedoColor = new Color("d9a57d"), Roughness = 0.68f };
        StandardMaterial3D face = new() { AlbedoColor = new Color("272429"), Roughness = 0.82f };
        StandardMaterial3D hair = new() { AlbedoColor = name == "Child" ? new Color("3a241d") : new Color("6a3d2e"), Roughness = 0.84f };
        MeshInstance3D body = new()
        {
            Position = new Vector3(0.0f, 1.15f * scale, 0.0f),
            Mesh = new CapsuleMesh { Radius = 0.42f * scale, Height = 1.7f * scale, RadialSegments = 16, Rings = 6 },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = clothingColor, Roughness = 0.72f },
        };
        MeshInstance3D head = new()
        {
            Position = new Vector3(0.0f, 2.25f * scale, 0.0f),
            Mesh = new SphereMesh { Radius = 0.43f * scale, Height = 0.86f * scale, RadialSegments = 18, Rings = 10 },
            MaterialOverride = skin,
        };
        person.AddChild(body);
        person.AddChild(head);
        person.AddChild(CreatePersonPart(
            new Vector3(0.0f, 2.52f, -0.04f) * scale,
            new SphereMesh { Radius = 0.44f * scale, Height = 0.42f * scale, RadialSegments = 18, Rings = 8 },
            hair));
        if (name == "Mother")
        {
            person.AddChild(CreatePersonPart(
                new Vector3(0.0f, 2.12f, -0.28f) * scale,
                new CapsuleMesh { Radius = 0.31f * scale, Height = 1.05f * scale, RadialSegments = 14, Rings = 6 },
                hair));
        }
        person.AddChild(CreatePersonPart(new Vector3(-0.14f, 2.32f, 0.4f) * scale, new SphereMesh { Radius = 0.055f * scale, Height = 0.11f * scale, RadialSegments = 10, Rings = 5 }, face));
        person.AddChild(CreatePersonPart(new Vector3(0.14f, 2.32f, 0.4f) * scale, new SphereMesh { Radius = 0.055f * scale, Height = 0.11f * scale, RadialSegments = 10, Rings = 5 }, face));
        person.AddChild(CreatePersonPart(new Vector3(0.0f, 2.11f, 0.42f) * scale, new BoxMesh { Size = new Vector3(0.2f, 0.035f, 0.035f) * scale }, face));
        person.AddChild(CreatePersonPart(new Vector3(-0.47f, 1.2f, 0.08f) * scale, new CapsuleMesh { Radius = 0.12f * scale, Height = 0.82f * scale, RadialSegments = 10, Rings = 4 }, skin));
        if (name == "Child")
        {
            _child = person;
            _childScale = scale;
            _childReachArm = CreatePersonPart(
                new Vector3(0.47f, 1.2f, 0.08f) * scale,
                new CapsuleMesh { Radius = 0.12f * scale, Height = 0.82f * scale, RadialSegments = 10, Rings = 4 },
                skin);
            person.AddChild(_childReachArm);
        }
        else
        {
            _mother = person;
            person.AddChild(CreatePersonPart(new Vector3(0.47f, 1.2f, 0.08f) * scale, new CapsuleMesh { Radius = 0.12f * scale, Height = 0.82f * scale, RadialSegments = 10, Rings = 4 }, skin));
        }
        AddChild(person);
    }

    private Vector3 PlaceChildArm(Vector3 shoulder, Vector3 target)
    {
        Vector3 targetOffset = target - shoulder;
        Vector3 direction = targetOffset.LengthSquared() > 0.000001f ? targetOffset.Normalized() : Vector3.Down;
        Vector3 hand = shoulder + (direction * (0.82f * _childScale));
        PlaceArmSegment(_childReachArm, shoulder, hand);
        return hand;
    }

    private static void PlaceArmSegment(MeshInstance3D segment, Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start;
        float length = Math.Max(direction.Length(), 0.001f);
        segment.GlobalPosition = start.Lerp(end, 0.5f);
        segment.GlobalBasis = new Basis(new Quaternion(Vector3.Up, direction / length));
    }

    private static MeshInstance3D CreatePersonPart(Vector3 position, Mesh mesh, Material material) => new()
    {
        Position = position,
        Mesh = mesh,
        MaterialOverride = material,
    };

    private Node3D AddMesh(
        string name,
        Mesh mesh,
        Material material,
        Vector3 position,
        Vector3? rotation = null)
    {
        MeshInstance3D instance = new()
        {
            Name = name,
            Mesh = mesh,
            MaterialOverride = material,
            Position = position,
            Rotation = rotation ?? Vector3.Zero,
        };
        AddChild(instance);
        return instance;
    }

    private static StandardMaterial3D CreateTexturedMaterial(string path, float metallic, float roughness)
    {
        return new StandardMaterial3D
        {
            AlbedoTexture = GD.Load<Texture2D>(path),
            Metallic = Mathf.Min(metallic, 0.5f),
            Roughness = roughness,
            Uv1Scale = Vector3.One,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
    }

    private ShaderMaterial CreateCurrentCandyMaterial()
    {
        ShaderMaterial material = new()
        {
            Shader = GD.Load<Shader>("res://resources/shaders/candy_preview.gdshader"),
        };
        CandyVisualStyle.ApplyCandyMaterial(material, CandyProfile);
        return material;
    }

    private bool ValidatePlayerCandyMaterial()
    {
        return _playerCandy is MeshInstance3D
        {
            MaterialOverride: ShaderMaterial
            {
                Shader.ResourcePath: "res://resources/shaders/candy_preview.gdshader",
            },
        };
    }
}
