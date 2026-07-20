using Godot;
using Velocitex.Core.Profile;
using Velocitex.Core.Save;
using Velocitex.UI;

namespace Velocitex.Tests;

public partial class AdvancementNotificationSmokeTest : Node
{
    private AdvancementNotificationPresenter _presenter = null!;
    private float _elapsed;
    private bool _failed;
    private bool _finishing;

    public override void _Ready()
    {
        foreach (AdvancementDefinition definition in AdvancementCatalog.All)
        {
            string iconPath = $"res://assets/ui/advancements/{definition.Id}.svg";
            if (!ResourceLoader.Exists(iconPath) || GD.Load<Texture2D>(iconPath) is null)
            {
                Fail($"Icon is missing or invalid for {definition.Id}.");
                return;
            }
        }

        _presenter = new AdvancementNotificationPresenter
        {
            SmokeMode = true,
            ReducedMotion = true,
        };
        AddChild(_presenter);

        PlayerProfile profile = ProfileStore.CreateDefault();
        foreach (string id in new[] { "fresh-from-the-globe", "clean-wrapper" })
        {
            if (!AdvancementService.TryUnlock(profile, id, out AdvancementDefinition? definition, out _))
            {
                Fail($"New unlock was rejected for {id}.");
                return;
            }

            _presenter.Enqueue(definition!);
            if (AdvancementService.TryUnlock(profile, id, out _, out _))
            {
                Fail($"Duplicate unlock was accepted for {id}.");
                return;
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_failed || _finishing || _presenter is null)
        {
            return;
        }

        _elapsed += (float)delta;
        if (_presenter.DisplayedCount == 2 && _presenter.PendingCount == 0)
        {
            PanelContainer panel = _presenter.GetNode<PanelContainer>("NotificationPanel");
            if (_presenter.LastDisplayedId != "clean-wrapper" ||
                _presenter.MouseFilter != Control.MouseFilterEnum.Ignore ||
                !Mathf.IsEqualApprox(panel.OffsetRight, -22.0f) ||
                !Mathf.IsEqualApprox(panel.OffsetBottom, -30.0f))
            {
                Fail("Notification order, non-interactive input behavior or compensated screen margins are incorrect.");
                return;
            }

            GD.Print("ADVANCEMENT_NOTIFICATION_SMOKE_PASS: 20 icons load and two new unlocks queue once in order with Reduced Motion.");
            FinishCleanly();
            return;
        }

        if (_elapsed > 2.0f)
        {
            Fail($"Notification queue timed out: displayed={_presenter.DisplayedCount}, pending={_presenter.PendingCount}.");
        }
    }

    private void Fail(string message)
    {
        _failed = true;
        GD.PushError($"ADVANCEMENT_NOTIFICATION_SMOKE_FAIL: {message}");
        GetTree().Quit(1);
    }

    private async void FinishCleanly()
    {
        _finishing = true;
        _presenter.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(0);
    }
}
