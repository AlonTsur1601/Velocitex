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
        if (AdvancementCatalog.All.Count != 26 ||
            AdvancementCatalog.All.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != 26 ||
            AdvancementCatalog.All.Any(item => item.RewardCosmeticId is null))
        {
            Fail("Advancement IDs or their regular cosmetic rewards are incomplete.");
            return;
        }

        if (AdvancementCatalog.All.Where(item => item.RewardCosmeticId is not null).Any(item =>
            CosmeticCatalog.FindById(item.RewardCosmeticId!) is not { UnlockedByDefault: false }))
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

        Dictionary<string, string> roomSpecificDescriptions = new()
        {
            ["fresh-from-the-globe"] = "Room 01",
            ["five-star-batch"] = "Rooms 01",
            ["straight-as-glass"] = "Room 06",
            ["perfect-stop"] = "Room 07",
            ["blue-streak"] = "Room 08",
            ["feather-touch"] = "Room 11",
            ["against-the-wind"] = "Room 13",
            ["perfect-switch"] = "Room 14",
            ["bullseye"] = "Room 16",
            ["untouchable"] = "Room 17",
            ["moving-with-it"] = "Room 18",
            ["piston-perfect"] = "Room 19",
            ["clean-assembly"] = "Room 20",
            ["full-account"] = "Room 23",
            ["sugar-breaker"] = "Room 24",
            ["vacuum-packed"] = "Room 26",
            ["smooth-operator"] = "Room 29",
            ["final-inspection"] = "Room 30",
        };
        if (roomSpecificDescriptions.Any(pair =>
            AdvancementCatalog.Find(pair.Key)?.Description.Contains(pair.Value, StringComparison.Ordinal) != true))
        {
            Fail("Every room-specific achievement description must identify the room by number.");
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

        if (completeProfile.UnlockedAdvancementIds.Count != 26 ||
            completeProfile.UnlockedCosmeticIds.Count != 47)
        {
            Fail("Unlocking all 26 advancements did not produce the expected cosmetic rewards.");
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
        GD.Print("ADVANCEMENT_SMOKE_PASS: all 26 unique achievement rewards and profile persistence work.");
        GetTree().Quit(0);
    }

    private void Fail(string message)
    {
        ProfileStore.DeleteTestFiles(TestPath);
        GD.PushError($"ADVANCEMENT_SMOKE_FAIL: {message}");
        GetTree().Quit(1);
    }
}
