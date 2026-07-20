using Godot;
using Velocitex.Core.Profile;

namespace Velocitex.UI;

public partial class AdvancementNotificationPresenter : Control
{
    private const float NormalEnterSeconds = 0.38f;
    private const float NormalHoldSeconds = 4.6f;
    private const float NormalExitSeconds = 0.32f;
    private const float ReducedEnterSeconds = 0.12f;
    private const float ReducedHoldSeconds = 4.6f;
    private const float ReducedExitSeconds = 0.12f;

    private readonly Queue<AdvancementDefinition> _queue = new();
    private PanelContainer _panel = null!;
    private TextureRect _icon = null!;
    private Label _name = null!;
    private Label _description = null!;
    private AudioStreamPlayer _chime = null!;
    private Phase _phase = Phase.Hidden;
    private float _phaseTime;

    public bool ReducedMotion { get; set; }
    public bool SmokeMode { get; set; }
    public int DisplayedCount { get; private set; }
    public string LastDisplayedId { get; private set; } = string.Empty;
    public int PendingCount => _queue.Count + (_phase == Phase.Hidden ? 0 : 1);
    public bool IsDisplaying => _phase != Phase.Hidden;

    private enum Phase
    {
        Hidden,
        Entering,
        Holding,
        Exiting,
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 100;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildInterface();
        _chime = new AudioStreamPlayer
        {
            Name = "UnlockChime",
            Bus = "SFX",
            Stream = GD.Load<AudioStream>("res://assets/audio/sfx/ui_advancement.wav"),
            VolumeDb = -5.0f,
        };
        AddChild(_chime);
        HidePanel();
    }

    public override void _Process(double delta)
    {
        if (_phase == Phase.Hidden)
        {
            ShowNext();
            return;
        }

        _phaseTime += (float)delta;
        float enter = SmokeMode ? 0.025f : ReducedMotion ? ReducedEnterSeconds : NormalEnterSeconds;
        float hold = SmokeMode ? 0.05f : ReducedMotion ? ReducedHoldSeconds : NormalHoldSeconds;
        float exit = SmokeMode ? 0.025f : ReducedMotion ? ReducedExitSeconds : NormalExitSeconds;

        switch (_phase)
        {
            case Phase.Entering:
                ApplyEntrance(Mathf.Clamp(_phaseTime / enter, 0.0f, 1.0f));
                if (_phaseTime >= enter)
                {
                    BeginPhase(Phase.Holding);
                }
                break;
            case Phase.Holding:
                if (_phaseTime >= hold)
                {
                    BeginPhase(Phase.Exiting);
                }
                break;
            case Phase.Exiting:
                ApplyExit(Mathf.Clamp(_phaseTime / exit, 0.0f, 1.0f));
                if (_phaseTime >= exit)
                {
                    HidePanel();
                    ShowNext();
                }
                break;
        }
    }

    public void Enqueue(AdvancementDefinition advancement)
    {
        _queue.Enqueue(advancement);
        if (_phase == Phase.Hidden && IsNodeReady())
        {
            ShowNext();
        }
    }

    public override void _ExitTree()
    {
        _chime?.Stop();
        _queue.Clear();
    }

    private void ShowNext()
    {
        if (_queue.Count == 0)
        {
            return;
        }

        AdvancementDefinition advancement = _queue.Dequeue();
        Texture2D? texture = GD.Load<Texture2D>($"res://assets/ui/advancements/{advancement.Id}.svg");
        _icon.Texture = texture;
        _name.Text = advancement.DisplayName.ToUpperInvariant();
        _description.Text = advancement.Description;
        LastDisplayedId = advancement.Id;
        DisplayedCount++;
        if (!SmokeMode)
        {
            _chime.Play();
        }
        _panel.Show();
        BeginPhase(Phase.Entering);
        ApplyEntrance(0.0f);
    }

    private void BeginPhase(Phase phase)
    {
        _phase = phase;
        _phaseTime = 0.0f;
    }

    private void ApplyEntrance(float progress)
    {
        float eased = 1.0f - Mathf.Pow(1.0f - progress, 3.0f);
        _panel.Modulate = new Color(1.0f, 1.0f, 1.0f, eased);
        SetPanelShift(ReducedMotion ? 0.0f : Mathf.Lerp(38.0f, 0.0f, eased));
    }

    private void ApplyExit(float progress)
    {
        float eased = progress * progress;
        _panel.Modulate = new Color(1.0f, 1.0f, 1.0f, 1.0f - eased);
        SetPanelShift(ReducedMotion ? 0.0f : Mathf.Lerp(0.0f, 26.0f, eased));
    }

    private void SetPanelShift(float shift)
    {
        _panel.OffsetLeft = -438.0f + shift;
        _panel.OffsetRight = -22.0f + shift;
    }

    private void HidePanel()
    {
        _phase = Phase.Hidden;
        _phaseTime = 0.0f;
        _panel?.Hide();
    }

    private void BuildInterface()
    {
        _panel = new PanelContainer
        {
            Name = "NotificationPanel",
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(416.0f, 104.0f),
        };
        _panel.SetAnchorsPreset(LayoutPreset.BottomRight);
        _panel.OffsetLeft = -438.0f;
        _panel.OffsetRight = -22.0f;
        // The shadow extends farther downward than sideways.  An extra eight
        // pixels of panel margin keeps the visible card equally inset.
        _panel.OffsetTop = -134.0f;
        _panel.OffsetBottom = -30.0f;
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("17252aF5"),
            BorderColor = new Color("79c8c4"),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ShadowColor = new Color(0.0f, 0.0f, 0.0f, 0.42f),
            ShadowSize = 12,
            ShadowOffset = new Vector2(0.0f, 5.0f),
        });
        AddChild(_panel);

        MarginContainer margin = new() { MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        _panel.AddChild(margin);

        HBoxContainer row = new() { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 14);
        margin.AddChild(row);

        _icon = new TextureRect
        {
            Name = "Icon",
            CustomMinimumSize = new Vector2(72.0f, 72.0f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddChild(_icon);

        VBoxContainer text = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        text.AddThemeConstantOverride("separation", 2);
        row.AddChild(text);

        Label unlocked = new()
        {
            Text = "ADVANCEMENT UNLOCKED",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        unlocked.AddThemeColorOverride("font_color", new Color("79c8c4"));
        unlocked.AddThemeFontSizeOverride("font_size", 13);
        text.AddChild(unlocked);

        _name = new Label { MouseFilter = MouseFilterEnum.Ignore };
        _name.AddThemeColorOverride("font_color", new Color("fff1ca"));
        _name.AddThemeFontSizeOverride("font_size", 20);
        text.AddChild(_name);

        _description = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _description.AddThemeColorOverride("font_color", new Color("c4d1d1"));
        _description.AddThemeFontSizeOverride("font_size", 14);
        text.AddChild(_description);
    }
}
