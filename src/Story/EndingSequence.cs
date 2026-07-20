using Godot;
using Velocitex.Core.Profile;
using Velocitex.Core.Save;
using Velocitex.Gameplay.Visual;

namespace Velocitex.Story;

public partial class EndingSequence : Node3D
{
    [Signal]
    public delegate void FinishedEventHandler();

    [Export] public bool SmokeMode { get; set; }
    [Export] public bool SubtitlesEnabled { get; set; } = true;
    [Export] public int SubtitleScalePercent { get; set; } = 100;
    [Export] public bool SubtitleBackgroundEnabled { get; set; } = true;
    public PlayerProfile CandyProfile { get; set; } = new();

    public bool FreezeFrameReached { get; private set; }
    public bool BlackoutReached { get; private set; }
    public bool CreditsSequenceCompleted { get; private set; }
    public float CreditsWidthRatio { get; private set; }
    public float CreditsHeightRatio { get; private set; }

    private const float FreezeTime = 6.8f;
    private Camera3D _camera = null!;
    private Control _subtitleContainer = null!;
    private PanelContainer _subtitlePanel = null!;
    private Label _speaker = null!;
    private Label _subtitle = null!;
    private AudioStreamPlayer _voice = null!;
    private AudioStreamPlayer _music = null!;
    private ColorRect _blackOverlay = null!;
    private VBoxContainer _creditsGroup = null!;
    private Node3D _playerCandy = null!;
    private Node3D _child = null!;
    private Node3D _mother = null!;
    private MeshInstance3D _childReachArm = null!;
    private MeshInstance3D _childMouth = null!;
    private MeshInstance3D _motherMouth = null!;
    private Node3D _childSmile = null!;
    private Node3D _motherSmile = null!;
    private float _childScale;
    private float _elapsed;
    private bool _voicePlayed;
    private bool _finished;
    private bool _creditsSequenceStarted;
    private bool _capturePreview;
    private bool _captureSmilePreview;
    private bool _captureCreditsPreview;
    private bool _previewCaptured;
    private float _childMachineFacingYaw;
    private float _motherMachineFacingYaw;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera3D");
        _subtitleContainer = GetNode<Control>("Ui/Subtitles");
        _subtitlePanel = GetNode<PanelContainer>("Ui/Subtitles/Panel");
        _speaker = GetNode<Label>("Ui/Subtitles/Panel/Layout/Speaker");
        _subtitle = GetNode<Label>("Ui/Subtitles/Panel/Layout/Line");
        _voice = GetNode<AudioStreamPlayer>("Voice");
        _music = GetNode<AudioStreamPlayer>("Music");
        _capturePreview = Array.Exists(OS.GetCmdlineUserArgs(), argument => argument == "--ending-preview");
        _captureSmilePreview = Array.Exists(OS.GetCmdlineUserArgs(), argument => argument == "--ending-smile-preview");
        _captureCreditsPreview = Array.Exists(OS.GetCmdlineUserArgs(), argument => argument == "--ending-credits-preview");
        ApplySubtitleSettings();
        BuildSet();
        BuildCreditsUi();
        if (!ValidatePlayerCandyMaterial())
        {
            GD.PushError("ENDING_CANDY_STYLE_FAIL: player candy is not using the current gameplay candy shader.");
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
        _camera.LookAt(new Vector3(0.0f, 1.8f, 1.0f), Vector3.Up);
        UpdateSequence(0.0f);
        if (SmokeMode)
        {
            GD.Print("ENDING_SMOKE_READY: ending set built with no skip input.");
        }
    }

    public override void _Process(double delta)
    {
        if (_finished)
        {
            return;
        }

        _elapsed += (float)delta * (SmokeMode ? 8.0f : 1.0f);
        UpdateSequence(Mathf.Min(_elapsed, FreezeTime));
        if (_captureSmilePreview && !_previewCaptured && _elapsed >= 3.65f)
        {
            _previewCaptured = true;
            string path = ProjectSettings.GlobalizePath("user://ending-smile-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"ENDING_SMILE_PREVIEW_CAPTURE: {path}");
            GetTree().Quit(0);
            return;
        }

        if (_capturePreview && FreezeFrameReached && !_previewCaptured)
        {
            _previewCaptured = true;
            string path = ProjectSettings.GlobalizePath("user://ending-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"ENDING_PREVIEW_CAPTURE: {path}");
            GetTree().Quit(0);
            return;
        }

        if (FreezeFrameReached && !_creditsSequenceStarted)
        {
            _creditsSequenceStarted = true;
            PlayCreditsSequenceAsync();
        }
    }

    private void UpdateSequence(float time)
    {
        Vector3 outlet = new(3.0f, 0.4f, 0.84f);
        float walk = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp((time - 0.45f) / 2.35f, 0.0f, 1.0f));
        _child.Position = new Vector3(-1.8f, 0.0f, 2.0f).Lerp(new Vector3(2.18f, 0.0f, 1.02f), walk);
        _mother.Position = new Vector3(-4.1f, 0.0f, 2.25f).Lerp(new Vector3(0.82f, 0.0f, 1.7f), walk);
        if (walk > 0.01f && time < 3.2f)
        {
            _child.LookAt(new Vector3(3.0f, 0.0f, 0.0f), Vector3.Up, useModelFront: true);
            _childMachineFacingYaw = _child.Rotation.Y;
            _child.Position += Vector3.Up * (Mathf.Sin(walk * Mathf.Pi * 10.0f) * 0.025f);
            _mother.LookAt(new Vector3(3.0f, 0.0f, 0.0f), Vector3.Up, useModelFront: true);
            _motherMachineFacingYaw = _mother.Rotation.Y;
            _mother.Position += Vector3.Up * (Mathf.Sin((walk * Mathf.Pi * 10.0f) + Mathf.Pi) * 0.018f);
        }
        else if (time >= 3.2f)
        {
            float turn = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp((time - 3.2f) / 0.9f, 0.0f, 1.0f));
            _child.Rotation = new Vector3(0.0f, Mathf.LerpAngle(_childMachineFacingYaw, 0.0f, turn), 0.0f);
            _mother.Rotation = new Vector3(0.0f, Mathf.LerpAngle(_motherMachineFacingYaw, 0.0f, turn), 0.0f);
        }

        Vector3 mouth = _childMouth.GlobalPosition;
        Vector3 shoulder = _child.ToGlobal(new Vector3(0.4f, 1.43f, 0.2f) * _childScale);
        Vector3 restingHand = _child.ToGlobal(new Vector3(0.47f, 0.92f, 0.08f) * _childScale);
        if (time < 3.2f)
        {
            float reach = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp((time - 2.45f) / 0.75f, 0.0f, 1.0f));
            Vector3 hand = restingHand.Lerp(outlet, reach);
            PlaceChildArm(shoulder, hand);
            _playerCandy.Position = outlet;
            SetSubtitle(string.Empty, string.Empty);
        }
        else
        {
            float lift = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp((time - 3.2f) / (FreezeTime - 3.2f), 0.0f, 1.0f));
            Vector3 arc = outlet.Lerp(mouth, lift) + (Vector3.Up * Mathf.Sin(lift * Mathf.Pi) * 0.3f);
            _playerCandy.Position = PlaceChildArm(shoulder, arc);
            if (time < 5.4f)
            {
                SetSubtitle("CHILD", "Finally!");
                if (!_voicePlayed && !SmokeMode)
                {
                    _voice.Stream = GD.Load<AudioStream>("res://assets/audio/voice/ending_finally.mp3");
                    _voice.Play();
                    _voicePlayed = true;
                }
            }
            else
            {
                SetSubtitle(string.Empty, string.Empty);
            }
        }

        _playerCandy.Rotation = new Vector3(time * 1.55f, time * 2.1f, time * 0.7f);
        bool smiling = time >= 2.75f;
        float mouthOpen = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp((time - 4.25f) / 0.85f, 0.0f, 1.0f));
        _childSmile.Visible = smiling && mouthOpen < 0.02f;
        _childMouth.Visible = !smiling || mouthOpen >= 0.02f;
        _motherSmile.Visible = smiling;
        _motherMouth.Visible = !smiling;
        _childMouth.Scale = new Vector3(0.26f, Mathf.Lerp(0.035f, 0.28f, mouthOpen), 0.08f);
        float cameraMove = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp((time - 3.65f) / (FreezeTime - 3.65f), 0.0f, 1.0f));
        _camera.Position = new Vector3(8.2f, 5.1f, 11.2f).Lerp(new Vector3(2.15f, 1.95f, 4.55f), cameraMove);
        _camera.Fov = Mathf.Lerp(50.0f, 36.0f, cameraMove);
        _camera.LookAt(new Vector3(0.2f, 2.6f, 0.6f).Lerp(mouth, cameraMove), Vector3.Up);

        if (time >= FreezeTime && !FreezeFrameReached)
        {
            FreezeFrameReached = true;
            _playerCandy.Visible = false;
            GD.Print("ENDING_FREEZE_FRAME: candy reached the child's mouth and all scene motion stopped.");
        }
    }

    private void BuildCreditsUi()
    {
        CanvasLayer layer = new() { Name = "FinaleLayer", Layer = 30 };
        AddChild(layer);

        _blackOverlay = new ColorRect
        {
            Name = "Blackout",
            Color = Colors.Black,
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        layer.AddChild(_blackOverlay);
        _blackOverlay.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _blackOverlay.Position = Vector2.Zero;
        _blackOverlay.Size = GetViewport().GetVisibleRect().Size;

        CenterContainer center = new()
        {
            Name = "CreditsCenter",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        layer.AddChild(center);
        center.AnchorLeft = 0.07f;
        center.AnchorTop = 0.08f;
        center.AnchorRight = 0.93f;
        center.AnchorBottom = 0.92f;

        _creditsGroup = new VBoxContainer
        {
            Name = "Credits",
            CustomMinimumSize = new Vector2(1080.0f, 500.0f),
            Alignment = BoxContainer.AlignmentMode.Center,
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
            LayoutDirection = Control.LayoutDirectionEnum.Ltr,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _creditsGroup.AddThemeConstantOverride("separation", 20);
        center.AddChild(_creditsGroup);

        Color creditColor = new("fff5df");
        const int creditFontSize = 58;
        AddCreditLine("The game was created by:", creditFontSize, creditColor);
        AddCreditLine("Codex with GPT 5.6 Sol & Terra", creditFontSize, creditColor);
        AddCreditLine("Alon Tsur", creditFontSize, creditColor);
        AddCreditLine("Thank you for playing :)", creditFontSize, creditColor, 118.0f);
    }

    private void AddCreditLine(string text, int fontSize, Color color, float minimumHeight = 88.0f)
    {
        Label line = new()
        {
            Text = text,
            CustomMinimumSize = new Vector2(0.0f, minimumHeight),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            LayoutDirection = Control.LayoutDirectionEnum.Ltr,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        line.AddThemeColorOverride("font_color", color);
        line.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.95f));
        line.AddThemeConstantOverride("shadow_offset_x", 3);
        line.AddThemeConstantOverride("shadow_offset_y", 3);
        line.AddThemeFontSizeOverride("font_size", fontSize);
        _creditsGroup.AddChild(line);
    }

    private async void PlayCreditsSequenceAsync()
    {
        _subtitleContainer.Hide();
        _voice.Stop();
        double fadeToBlackSeconds = SmokeMode ? 0.04 : 1.25;
        double creditsFadeSeconds = SmokeMode ? 0.04 : 1.15;
        double creditsHoldSeconds = SmokeMode ? 0.08 : 4.0;

        Tween blackout = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        blackout.TweenProperty(_blackOverlay, "modulate:a", 1.0f, fadeToBlackSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        await ToSignal(blackout, Tween.SignalName.Finished);
        Vector2 blackoutSize = _blackOverlay.GetGlobalRect().Size;
        Vector2 blackoutViewportSize = GetViewport().GetVisibleRect().Size;
        BlackoutReached = _blackOverlay.Modulate.A >= 0.99f &&
            blackoutSize.X >= blackoutViewportSize.X * 0.99f &&
            blackoutSize.Y >= blackoutViewportSize.Y * 0.99f;

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        Vector2 creditsSize = _creditsGroup.GetGlobalRect().Size;
        CreditsWidthRatio = creditsSize.X / Mathf.Max(viewportSize.X, 1.0f);
        CreditsHeightRatio = creditsSize.Y / Mathf.Max(viewportSize.Y, 1.0f);

        Tween fadeIn = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        fadeIn.TweenProperty(_creditsGroup, "modulate:a", 1.0f, creditsFadeSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        await ToSignal(fadeIn, Tween.SignalName.Finished);
        if (_captureCreditsPreview)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = ProjectSettings.GlobalizePath("user://ending-credits-preview.png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"ENDING_CREDITS_PREVIEW_CAPTURE: {path}");
            GetTree().Quit(0);
            return;
        }
        await ToSignal(GetTree().CreateTimer(creditsHoldSeconds, processAlways: true), SceneTreeTimer.SignalName.Timeout);

        Tween fadeOut = CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
        fadeOut.TweenProperty(_creditsGroup, "modulate:a", 0.0f, creditsFadeSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.In);
        await ToSignal(fadeOut, Tween.SignalName.Finished);

        CreditsSequenceCompleted = true;
        _finished = true;
        _music.Stop();
        GD.Print($"ENDING_CREDITS_COMPLETE: blackout and large credits completed; coverage={CreditsWidthRatio:F2}x{CreditsHeightRatio:F2}.");
        EmitSignal(SignalName.Finished);
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

    private void BuildSet()
    {
        StandardMaterial3D metal = CreateTexturedMaterial("res://assets/textures/brushed_metal.png", 0.46f, 0.55f);
        StandardMaterial3D caramel = CreateTexturedMaterial("res://assets/textures/caramel_plates.svg", 0.08f, 0.64f);
        StandardMaterial3D rubber = CreateTexturedMaterial("res://assets/textures/rubber_chevrons.svg", 0.0f, 0.9f);
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

        AddMesh("Pedestal", SurfaceMeshFactory.CreateTiledBox(new Vector3(1.44f, 1.06f, 1.19f)), caramel, new Vector3(3.0f, 0.53f, 0.0f));
        AddMesh("PedestalFoot", SurfaceMeshFactory.CreateTiledBox(new Vector3(1.69f, 0.16f, 1.37f)), metal, new Vector3(3.0f, 0.08f, 0.0f));
        AddMesh("GlobeCollar", new CylinderMesh { TopRadius = 0.73f, BottomRadius = 0.64f, Height = 0.24f, RadialSegments = 36 }, copper, new Vector3(3.0f, 1.18f, 0.0f));
        AddMesh("GlobeCap", new CylinderMesh { TopRadius = 0.34f, BottomRadius = 0.47f, Height = 0.18f, RadialSegments = 32 }, copper, new Vector3(3.0f, 2.93f, 0.0f));
        AddMesh("CoinPlate", SurfaceMeshFactory.CreateTiledBox(new Vector3(0.68f, 0.61f, 0.09f)), metal, new Vector3(3.0f, 0.7f, 0.63f));
        AddMesh("CoinSlot", SurfaceMeshFactory.CreateTiledBox(new Vector3(0.25f, 0.05f, 0.05f)), dark, new Vector3(2.75f, 0.77f, 0.69f));
        AddMesh("OutletFrame", SurfaceMeshFactory.CreateTiledBox(new Vector3(0.65f, 0.35f, 0.12f)), copper, new Vector3(3.0f, 0.3f, 0.67f));
        AddMesh("OutletDark", SurfaceMeshFactory.CreateTiledBox(new Vector3(0.42f, 0.2f, 0.06f)), dark, new Vector3(3.0f, 0.29f, 0.73f));
        SurfaceDetail.AddOverlay(this, "FloorWear", new Vector3(-2.0f, 0.012f, 0.7f), new Vector3(-Mathf.Pi / 2.0f, 0.0f, Mathf.DegToRad(9.0f)), new Vector2(5.2f, 3.0f), "res://assets/textures/overlays/grime_03.svg", new Color("2c2926"), 0.34f);
        SurfaceDetail.AddOverlay(this, "MachineWear", new Vector3(3.25f, 0.58f, 0.61f), new Vector3(0.0f, 0.0f, Mathf.DegToRad(-7.0f)), new Vector2(0.58f, 0.63f), "res://assets/textures/overlays/scratches.svg", new Color("e3c8a0"), 0.3f);
        StandardMaterial3D glass = new() { Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(0.55f, 0.82f, 0.9f, 0.18f), Metallic = 0.08f, Roughness = 0.08f };
        AddMesh("Globe", new SphereMesh { Radius = 0.92f, Height = 1.84f, RadialSegments = 44, Rings = 24 }, glass, new Vector3(3.0f, 2.03f, 0.0f));
        CinematicCandyFill.AddPackedGlobe(this, "GlobeCandies", new Vector3(3.0f, 2.03f, 0.0f));
        BuildPerson("Mother", new Vector3(-4.1f, 0.0f, 2.25f), 1.0f, new Color("6d4c82"));
        BuildPerson("Child", new Vector3(-1.8f, 0.0f, 2.0f), 0.72f, new Color("3e7c91"));
        _playerCandy = AddMesh("PlayerCandy", new SphereMesh { Radius = CinematicCandyFill.CandyRadius, Height = CinematicCandyFill.CandyRadius * 2.0f, RadialSegments = 32, Rings = 16 }, candy, new Vector3(3.0f, 0.4f, 0.84f));
    }

    private void BuildPerson(string name, Vector3 position, float scale, Color clothing)
    {
        Node3D person = new() { Name = name, Position = position };
        StandardMaterial3D skin = new() { AlbedoColor = new Color("d9a57d"), Roughness = 0.68f };
        StandardMaterial3D face = new() { AlbedoColor = new Color("272429"), Roughness = 0.82f };
        StandardMaterial3D hair = new() { AlbedoColor = name == "Child" ? new Color("3a241d") : new Color("6a3d2e"), Roughness = 0.84f };
        person.AddChild(new MeshInstance3D { Position = new Vector3(0.0f, 1.15f * scale, 0.0f), Mesh = new CapsuleMesh { Radius = 0.42f * scale, Height = 1.7f * scale, RadialSegments = 16, Rings = 6 }, MaterialOverride = new StandardMaterial3D { AlbedoColor = clothing, Roughness = 0.72f } });
        person.AddChild(new MeshInstance3D { Position = new Vector3(0.0f, 2.25f * scale, 0.0f), Mesh = new SphereMesh { Radius = 0.43f * scale, Height = 0.86f * scale, RadialSegments = 18, Rings = 10 }, MaterialOverride = skin });
        person.AddChild(CreatePersonPart(new Vector3(0.0f, 2.52f, -0.04f) * scale, new SphereMesh { Radius = 0.44f * scale, Height = 0.42f * scale, RadialSegments = 18, Rings = 8 }, hair));
        if (name == "Mother")
        {
            person.AddChild(CreatePersonPart(new Vector3(0.0f, 2.12f, -0.28f) * scale, new CapsuleMesh { Radius = 0.31f * scale, Height = 1.05f * scale, RadialSegments = 14, Rings = 6 }, hair));
        }
        person.AddChild(CreatePersonPart(new Vector3(-0.14f, 2.32f, 0.4f) * scale, new SphereMesh { Radius = 0.055f * scale, Height = 0.11f * scale, RadialSegments = 10, Rings = 5 }, face));
        person.AddChild(CreatePersonPart(new Vector3(0.14f, 2.32f, 0.4f) * scale, new SphereMesh { Radius = 0.055f * scale, Height = 0.11f * scale, RadialSegments = 10, Rings = 5 }, face));
        MeshInstance3D mouth = CreatePersonPart(new Vector3(0.0f, 2.11f, 0.42f) * scale, new SphereMesh { Radius = 0.5f, Height = 1.0f, RadialSegments = 14, Rings = 7 }, face);
        mouth.Scale = name == "Child" ? new Vector3(0.26f, 0.035f, 0.08f) : new Vector3(0.2f, 0.035f, 0.06f);
        person.AddChild(mouth);
        Node3D smile = CreateSmile(scale, face);
        smile.Visible = false;
        person.AddChild(smile);
        if (name == "Child")
        {
            _child = person;
            _childScale = scale;
            _childMouth = mouth;
            _childSmile = smile;
            _childReachArm = CreatePersonPart(new Vector3(0.47f, 1.2f, 0.08f) * scale, new CapsuleMesh { Radius = 0.12f * scale, Height = 0.82f * scale, RadialSegments = 10, Rings = 4 }, skin);
            person.AddChild(_childReachArm);
        }
        else
        {
            _mother = person;
            _motherMouth = mouth;
            _motherSmile = smile;
            person.AddChild(CreatePersonPart(new Vector3(0.47f, 1.2f, 0.08f) * scale, new CapsuleMesh { Radius = 0.12f * scale, Height = 0.82f * scale, RadialSegments = 10, Rings = 4 }, skin));
        }
        person.AddChild(CreatePersonPart(new Vector3(-0.47f, 1.2f, 0.08f) * scale, new CapsuleMesh { Radius = 0.12f * scale, Height = 0.82f * scale, RadialSegments = 10, Rings = 4 }, skin));
        AddChild(person);
    }

    private static Node3D CreateSmile(float scale, Material material)
    {
        Node3D smile = new() { Name = "Smile" };
        MeshInstance3D left = CreatePersonPart(new Vector3(-0.055f, 2.09f, 0.435f) * scale, new BoxMesh { Size = new Vector3(0.12f, 0.025f, 0.03f) * scale }, material);
        MeshInstance3D right = CreatePersonPart(new Vector3(0.055f, 2.09f, 0.435f) * scale, new BoxMesh { Size = new Vector3(0.12f, 0.025f, 0.03f) * scale }, material);
        left.Rotation = new Vector3(0.0f, 0.0f, -0.28f);
        right.Rotation = new Vector3(0.0f, 0.0f, 0.28f);
        smile.AddChild(left);
        smile.AddChild(right);
        return smile;
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

    private Node3D AddMesh(string name, Mesh mesh, Material material, Vector3 position, Vector3? rotation = null)
    {
        MeshInstance3D instance = new() { Name = name, Mesh = mesh, MaterialOverride = material, Position = position, Rotation = rotation ?? Vector3.Zero };
        AddChild(instance);
        return instance;
    }

    private static StandardMaterial3D CreateTexturedMaterial(string path, float metallic, float roughness) => new()
    {
        AlbedoTexture = GD.Load<Texture2D>(path),
        Metallic = Mathf.Min(metallic, 0.5f),
        Roughness = roughness,
        TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
    };

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
