using System;
using Godot;

namespace Velocitex.UI;

public sealed partial class MenuConfirmationPopup : Control
{
    public event Action? Confirmed;

    public string Title { get => _title.Text; set => _title.Text = value; }
    public string DialogText { get => _message.Text; set => _message.Text = value; }
    public string OkButtonText { get => _confirm.Text; set => _confirm.Text = value; }
    public string CancelButtonText { get => _cancel.Text; set => _cancel.Text = value; }

    private readonly PanelContainer _panel;
    private readonly Label _title;
    private readonly Label _message;
    private readonly Button _confirm;
    private readonly Button _cancel;

    public MenuConfirmationPopup()
    {
        Name = "MenuConfirmationPopup";
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        ColorRect backdrop = new()
        {
            Color = new Color(0.015f, 0.023f, 0.031f, 0.72f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        CenterContainer center = new();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        _panel = new PanelContainer();
        center.AddChild(_panel);

        VBoxContainer layout = new();
        layout.AddThemeConstantOverride("separation", 14);
        _panel.AddChild(layout);

        _title = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _title.AddThemeColorOverride("font_color", new Color("fae8c7"));
        _title.AddThemeFontSizeOverride("font_size", 30);
        layout.AddChild(_title);

        layout.AddChild(new HSeparator());

        _message = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        layout.AddChild(_message);

        HBoxContainer actions = new()
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        actions.AddThemeConstantOverride("separation", 12);
        layout.AddChild(actions);

        _confirm = new Button { CustomMinimumSize = new Vector2(170.0f, 44.0f), FocusMode = FocusModeEnum.None };
        _cancel = new Button { CustomMinimumSize = new Vector2(170.0f, 44.0f), FocusMode = FocusModeEnum.None };
        actions.AddChild(_confirm);
        actions.AddChild(_cancel);

        _confirm.Pressed += () =>
        {
            Hide();
            Confirmed?.Invoke();
        };
        _cancel.Pressed += Hide;
    }

    public void PopupCentered(Vector2I size)
    {
        _panel.CustomMinimumSize = size;
        Show();
        MoveToFront();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event.IsActionPressed("ui_cancel"))
        {
            Hide();
            GetViewport().SetInputAsHandled();
        }
    }
}
