using Godot;
using Velocitex.Core.Profile;
using Velocitex.Core.Save;

namespace Velocitex.Tests;

public partial class AdvancementSmokeTest : Node
{
    private const string TestPath = "user://advancement-smoke-profile.json";

    public override void _Ready()
    {
        ProfileStore.DeleteTestFiles(TestPath);
        if (AdvancementCatalog.All.Count != 20 ||
            AdvancementCatalog.All.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != 20 ||
            AdvancementCatalog.All.Select(item => item.RewardCosmeticId).Distinct(StringComparer.Ordinal).Count() != 20)
        {
            Fail("Advancement IDs or rewards are not a unique set of 20.");
            return;
        }

        if (AdvancementCatalog.All.Any(item =>
            CosmeticCatalog.FindById(item.RewardCosmeticId) is not { UnlockedByDefault: false }))
        {
            Fail("An advancement reward is missing or incorrectly unlocked by default.");
            return;
        }

        if (AdvancementCatalog.All.Any(item =>
            item.Description.Contains("metres", StringComparison.OrdinalIgnoreCase) ||
            item.Description.Contains("centre", StringComparison.OrdinalIgnoreCase)))
        {
            Fail("Advancement descriptions must use American English meters/center spelling.");
            return;
        }

        if (!AdvancementService.RoomCompletionMilestones(1).SequenceEqual(new[] { "fresh-from-the-globe" }) ||
            AdvancementService.RoomCompletionMilestones(4).Count != 0 ||
            !AdvancementService.RoomCompletionMilestones(5).SequenceEqual(new[] { "five-star-batch" }) ||
            AdvancementService.RoomCompletionMilestones(25).Count != 0)
        {
            Fail("Room-completion milestones do not award Five-Star Batch exactly after Room 05.");
            return;
        }

        PlayerProfile profile = ProfileStore.CreateDefault();
        if (!AdvancementService.TryUnlock(
                profile,
                "clean-wrapper",
                out AdvancementDefinition? advancement,
                out CosmeticDefinition? reward) ||
            advancement?.Id != "clean-wrapper" || reward?.Id != "rose" ||
            !profile.UnlockedCosmeticIds.Contains("rose"))
        {
            Fail("Advancement did not unlock its cosmetic reward.");
            return;
        }

        if (AdvancementService.TryUnlock(profile, "clean-wrapper", out _, out _) ||
            AdvancementService.TryUnlock(profile, "not-real", out _, out _))
        {
            Fail("Duplicate or unknown advancements were accepted.");
            return;
        }

        PlayerProfile completeProfile = ProfileStore.CreateDefault();
        foreach (AdvancementDefinition definition in AdvancementCatalog.All)
        {
            if (!AdvancementService.TryUnlock(completeProfile, definition.Id, out _, out _) ||
                AdvancementService.TryUnlock(completeProfile, definition.Id, out _, out _))
            {
                Fail($"Positive or negative unlock check failed for {definition.Id}.");
                return;
            }
        }

        if (completeProfile.UnlockedAdvancementIds.Count != 20 ||
            completeProfile.UnlockedCosmeticIds.Count != 39)
        {
            Fail("Unlocking all 20 advancements did not produce all 20 unique rewards.");
            return;
        }

        profile.PrimaryColorId = "rose";
        if (!ProfileStore.Save(profile, out string? saveError, TestPath))
        {
            Fail($"Rewarded profile could not be saved: {saveError}");
            return;
        }

        PlayerProfile loaded = ProfileStore.Load(out string? warning, TestPath);
        if (warning is not null || loaded.PrimaryColorId != "rose" ||
            !loaded.UnlockedAdvancementIds.Contains("clean-wrapper") ||
            !loaded.UnlockedCosmeticIds.Contains("rose"))
        {
            Fail($"Advancement or reward did not survive profile persistence: {warning}");
            return;
        }

        ProfileStore.DeleteTestFiles(TestPath);
        GD.Print("ADVANCEMENT_SMOKE_PASS: all 20 positive and negative unlock paths, unique rewards and persistence work.");
        GetTree().Quit(0);
    }

    private void Fail(string message)
    {
        ProfileStore.DeleteTestFiles(TestPath);
        GD.PushError($"ADVANCEMENT_SMOKE_FAIL: {message}");
        GetTree().Quit(1);
    }
}
