using Godot;
using Velocitex.Core.Profile;
using Velocitex.Core.Save;

namespace Velocitex.Tests;

public partial class ChallengeModesSmokeTest : Node
{
    public override void _Ready()
    {
        string normal = "user://challenge-smoke-normal";
        string hard = "user://challenge-smoke-hard";
        string extreme = "user://challenge-smoke-extreme";
        foreach (string root in new[] { normal, hard, extreme }) CampaignSaveService.DeleteAll(out _, root);

        CampaignSnapshot checkpoint = new() { RoomId = "room-06", RoomName = "Smoke", RoomNumber = 6, Kind = SnapshotKind.RoomStart };
        if (!CampaignSaveService.Save(checkpoint, null, out _, hard) || CampaignSaveService.LoadAll(out _, normal).Count != 0 || CampaignSaveService.LoadAll(out _, extreme).Count != 0 ||
            !CampaignSaveService.GetReachedCheckpoints(hard).SetEquals(new[] { 6 }) ||
            CampaignModeRules.AllowsManualLoad(CampaignMode.Extreme) || CampaignModeRules.AllowsCheckpointSelection(CampaignMode.Extreme) ||
            !CampaignModeRules.AllowsManualLoad(CampaignMode.Hard) || !CampaignModeRules.AllowsCheckpointSelection(CampaignMode.Hard) ||
            CampaignModeRules.RestartRoomAfterFailure(CampaignMode.Hard, 24) != 21 || CampaignModeRules.RestartRoomAfterFailure(CampaignMode.Extreme, 24) != 1 ||
            CampaignModeRules.SavesRoomStart(CampaignMode.Hard, 24) || !CampaignModeRules.SavesRoomStart(CampaignMode.Hard, 26))
        {
            Fail("save isolation, menu permissions, checkpoint filtering, or failure routing diverged"); return;
        }

        CampaignSnapshot normalStart = new() { RoomId = "room-01", RoomName = "Normal", RoomNumber = 1, Kind = SnapshotKind.RoomStart };
        CampaignSnapshot extremeStart = new() { RoomId = "room-11", RoomName = "Extreme", RoomNumber = 11, Kind = SnapshotKind.RoomStart };
        if (!CampaignSaveService.Save(normalStart, null, out _, normal) ||
            !CampaignSaveService.Save(extremeStart, null, out _, extreme) ||
            !CampaignSaveService.DeleteAll(out _, hard) ||
            CampaignSaveService.LoadAll(out _, hard).Count != 0 ||
            CampaignSaveService.LoadAll(out _, normal).Count != 1 ||
            CampaignSaveService.LoadAll(out _, extreme).Count != 1)
        {
            Fail("starting a new Hard run modified Normal or Extreme saves"); return;
        }

        if (!CampaignSaveService.Save(checkpoint, null, out _, hard) ||
            !CampaignSaveService.DeleteAll(out _, extreme) ||
            CampaignSaveService.LoadAll(out _, extreme).Count != 0 ||
            CampaignSaveService.LoadAll(out _, normal).Count != 1 ||
            CampaignSaveService.LoadAll(out _, hard).Count != 1)
        {
            Fail("starting a new Extreme run modified Normal or Hard saves"); return;
        }

        PlayerProfile profile = ProfileStore.CreateDefault();
        if (!AdvancementService.TryUnlock(profile, "jawbreaker", out _, out CosmeticDefinition? reward) || reward?.Id != "sparkling" ||
            !profile.UnlockedAdvancementIds.Contains("jawbreaker") || !profile.UnlockedCosmeticIds.Contains("sparkling") ||
            !AdvancementService.TryUnlock(profile, "flawless-campaign", out _, out CosmeticDefinition? bronzeReward) || bronzeReward?.Id != "bronze-crown" ||
            !AdvancementService.TryUnlock(profile, "hard-mode-complete", out _, out CosmeticDefinition? hardReward) || hardReward?.Id != "silver-crown" ||
            !AdvancementService.TryUnlock(profile, "extreme-mode-complete", out _, out CosmeticDefinition? extremeReward) || extremeReward?.Id != "gold-crown" ||
            CosmeticCatalog.OfKind(CosmeticKind.Crown).Select(crown => crown.Id).SequenceEqual(new[] { "none-crown", "bronze-crown", "silver-crown", "gold-crown" }) == false ||
            AdvancementCatalog.Find("smooth-operator")?.RewardCosmeticId != "silk-blue" ||
            CosmeticCatalog.Find(CosmeticKind.Trail, "silk-blue") is not { DisplayName: "Red Trail", PreviewValue: "#D12B3F" } ||
            CosmeticCatalog.Find(CosmeticKind.Color, "silk-blue") is not null ||
            AdvancementCatalog.Find("final-inspection")?.RewardCosmeticId != "inspection-grid" ||
            CosmeticCatalog.OfKind(CosmeticKind.Finish).Count() != 6 || CosmeticCatalog.OfKind(CosmeticKind.TrailStyle).Count() != 5)
        {
            Fail("badge-only achievements or challenge cosmetic catalogs diverged"); return;
        }

        foreach (string root in new[] { normal, hard, extreme }) CampaignSaveService.DeleteAll(out _, root);
        GD.Print("CHALLENGE_MODES_SMOKE_PASS: isolated modes, challenge failure rules, Jawbreaker, and Bronze/Silver/Gold campaign crowns work.");
        GetTree().Quit(0);
    }

    private void Fail(string message) { GD.PushError($"CHALLENGE_MODES_SMOKE_FAIL: {message}"); GetTree().Quit(1); }
}
