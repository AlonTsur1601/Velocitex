using Velocitex.Core.Save;

namespace Velocitex.Core.Profile;

public static class AdvancementService
{
    public static IReadOnlyList<string> RoomCompletionMilestones(int roomNumber)
    {
        return roomNumber switch
        {
            1 => new[] { "fresh-from-the-globe" },
            5 => new[] { "five-star-batch" },
            _ => Array.Empty<string>(),
        };
    }

    public static bool TryUnlock(
        PlayerProfile profile,
        string advancementId,
        out AdvancementDefinition? advancement,
        out CosmeticDefinition? reward)
    {
        advancement = AdvancementCatalog.Find(advancementId);
        reward = null;
        if (advancement is null || profile.UnlockedAdvancementIds.Contains(advancementId))
        {
            return false;
        }

        reward = CosmeticCatalog.FindById(advancement.RewardCosmeticId);
        if (reward is null || reward.UnlockedByDefault)
        {
            return false;
        }

        profile.UnlockedAdvancementIds.Add(advancement.Id);
        profile.UnlockedCosmeticIds.Add(reward.Id);
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        return true;
    }
}
