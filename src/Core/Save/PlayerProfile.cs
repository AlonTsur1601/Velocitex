namespace Velocitex.Core.Save;

public sealed class PlayerProfile
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;
    public HashSet<string> UnlockedCosmeticIds { get; init; } = new(StringComparer.Ordinal);
    public HashSet<string> UnlockedAdvancementIds { get; init; } = new(StringComparer.Ordinal);
    public string PrimaryColorId { get; set; } = "cherry";
    public string SecondaryColorId { get; set; } = "vanilla";
    public string PatternId { get; set; } = "none";
    public string TrailId { get; set; } = "off";
    public int CleanRoomStreak { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
